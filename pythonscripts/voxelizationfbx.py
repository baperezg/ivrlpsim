import sys
import os
import gzip
import struct
import numpy as np
import trimesh

try:
    import assimp_py
except ImportError as e:
    raise ImportError(
        "assimp_py is required to read FBX files. Install it via `pip install assimp_py`."
    ) from e

# ---------------------------------------------------------------------------
# 1. TISSUE ENUMERATION (Mapped to English names)
# NOTE: The numerical IDs (0 to 11) strictly match the underlying C# enum 
# layout from ParametrosFuerza.cs and MapaTejidos.cs[cite: 1].
# ---------------------------------------------------------------------------
TISSUE_IDS = {
    "Air": 0,                     # C# Tejido.Aire[cite: 1]
    "SoftTissue": 1,              # C# Tejido.TejidoBlando[cite: 1]
    "Fat": 2,                     # C# Tejido.Grasa[cite: 1]
    "Skin": 3,                    # C# Tejido.Piel[cite: 1]
    "Muscle": 4,                  # C# Tejido.Musculo[cite: 1]
    "DuralSac": 5,                # C# Tejido.SacoDural[cite: 1]
    "EpiduralSpace": 6,           # C# Tejido.EspacioEpidural[cite: 1]
    "OtherLigaments": 7,          # C# Tejido.OtrosLigamentos[cite: 1]
    "SupraspinousLigament": 8,    # C# Tejido.LigSupraespinoso[cite: 1]
    "LigamentumFlavum": 9,        # C# Tejido.LigAmarillo[cite: 1]
    "IntervertebralDisc": 10,     # C# Tejido.Disco[cite: 1]
    "Bone": 11,                   # C# Tejido.Hueso[cite: 1]
}

# ---------------------------------------------------------------------------
# 2. NAME MAPPING: FBX node/mesh identifiers -> English tissue types[cite: 1]
# ---------------------------------------------------------------------------
NAME_TO_TISSUE = {
    # Skin[cite: 1]
    "SkinThin": "Skin",
    "skin": "Skin",
    
    # Subcutaneous Fat[cite: 1]
    "fat": "Fat",
    "subcut": "Fat",
    
    # Muscle[cite: 1]
    "leftdeepbackmuscle": "Muscle",
    "rightdeepbackmuscle": "Muscle",
    "muscle": "Muscle",
    
    # Specific Ligaments[cite: 1]
    "SupraspinousLigament": "SupraspinousLigament",
    "YellowLigament": "LigamentumFlavum",
    "ligamentumflavum": "LigamentumFlavum",
    "Ligaments": "OtherLigaments",
    
    # Dural Sac & Epidural Space[cite: 1]
    "EpiduralSpace": "EpiduralSpace",
    "epidural": "EpiduralSpace",
    "spinalcord": "DuralSac",
    "spinal cord": "DuralSac",
    "duralsac": "DuralSac",
    "sacodural": "DuralSac",
    
    # Intervertebral Discs[cite: 1]
    "intervertebral_discs": "IntervertebralDisc",
    "intervertebraldiscs": "IntervertebralDisc",
    "disc": "IntervertebralDisc",
    
    # Bone Structures[cite: 1]
    "T12vertebra": "Bone",
    "T11vertebra": "Bone",
    "S1vertebra": "Bone",
    "L5vertebra": "Bone",
    "L4vertebra": "Bone",
    "L3vertebra": "Bone",
    "L2vertebra": "Bone",
    "L1vertebra": "Bone",
    "vertebra": "Bone",
    "sacrum": "Bone",
    "righthip": "Bone",
    "lefthip": "Bone",
    "hip": "Bone",
    "bone": "Bone",
}

# ---------------------------------------------------------------------------
# 3. OVERLAP RESOLUTION PRIORITY
# Earlier items are placed first. Later items OVERWRITE them.
# Skin and Fat are rasterized early so internal structures retain their volume[cite: 1].
# ---------------------------------------------------------------------------
PRIORITY = [
    "Air",
    "SoftTissue",
    "Fat",
    "Skin",
    "Muscle",
    "OtherLigaments",
    "SupraspinousLigament",
    "LigamentumFlavum",
    "IntervertebralDisc",
    "DuralSac",
    "EpiduralSpace",
    "Bone",  # Hardest unyielding contact placed last[cite: 1]
]


def _get_flag(name: str) -> int:
    """Safely resolves assimp_py flag regardless of snake_case or CamelCase."""
    return getattr(assimp_py, name.lower(), getattr(assimp_py, name, 0))


def resolve_tissue_type(name: str) -> str:
    """Matches the FBX node/mesh name against classified anatomical tissue keys[cite: 1]."""
    if not name:
        return None
    clean = name.lower().replace(" ", "").replace("_", "").strip()
    for pattern in sorted(NAME_TO_TISSUE.keys(), key=len, reverse=True):
        pattern_clean = pattern.lower().replace(" ", "").replace("_", "")
        if pattern_clean in clean:
            return NAME_TO_TISSUE[pattern]
    return None


def _traverse_nodes(node, parent_transform: np.ndarray, scene_meshes: list, results: list):
    """Recursively bakes FBX node transforms while keeping both node and mesh names."""
    mat = np.array(getattr(node, "transformation", np.eye(4)), dtype=np.float64).reshape(4, 4)
    world_transform = parent_transform @ mat

    node_name = getattr(node, "name", "")
    mesh_indices = getattr(node, "mesh_indices", getattr(node, "meshes", []))

    for idx in mesh_indices:
        mesh_obj = scene_meshes[idx] if isinstance(idx, int) else idx
        mesh_name = getattr(mesh_obj, "name", "")
        results.append((node_name, mesh_name, world_transform, mesh_obj))

    for child in getattr(node, "children", []):
        _traverse_nodes(child, world_transform, scene_meshes, results)


def extract_fbx_meshes(fbx_path: str, unit_scale: float, flip_x_for_unity: bool = True):
    """
    Loads an FBX scene via assimp_py, preserves node & mesh names,
    bakes world transforms, scales to mm, and computes exact FBX bounds.
    """
    classified = []
    global_min = np.array([np.inf, np.inf, np.inf])
    global_max = np.array([-np.inf, -np.inf, -np.inf])

    flags = _get_flag("Process_Triangulate") | _get_flag("Process_JoinIdenticalVertices")
    scene = assimp_py.import_file(fbx_path, flags)

    raw_items = []
    root = getattr(scene, "root_node", getattr(scene, "rootnode", None))
    if root is not None:
        _traverse_nodes(root, np.eye(4, dtype=np.float64), scene.meshes, raw_items)
    else:
        for m in scene.meshes:
            raw_items.append(("", getattr(m, "name", ""), np.eye(4, dtype=np.float64), m))

    for node_name, mesh_name, transform, mesh_obj in raw_items:
        vertices = np.array(mesh_obj.vertices, dtype=np.float64).reshape(-1, 3)
        if vertices.shape[0] == 0:
            continue

        faces = np.array(mesh_obj.indices, dtype=np.int64).reshape(-1, 3)
        if faces.shape[0] == 0:
            continue

        mesh = trimesh.Trimesh(vertices=vertices, faces=faces, process=False)
        mesh.apply_transform(transform)
        mesh.vertices *= unit_scale

        # Unity's default FBX importer negates X when converting right-handed FBX to left-handed Unity
        if flip_x_for_unity:
            mesh.vertices[:, 0] *= -1.0
            mesh.invert()

        bounds = mesh.bounds
        global_min = np.minimum(global_min, bounds[0])
        global_max = np.maximum(global_max, bounds[1])

        tissue_key = resolve_tissue_type(node_name) or resolve_tissue_type(mesh_name)
        if not tissue_key:
            print(f"  [Bounds Only] Unrecognized FBX node/mesh: '{node_name}' / '{mesh_name}'")
            continue

        print(f"  [Found] '{node_name}' ('{mesh_name}') -> Tissue: {tissue_key}")
        classified.append((tissue_key, mesh))

    return classified, global_min, global_max


def voxelize_fbx(
    fbx_path: str,
    output_bytes_path: str = "TissueMap.bytes",
    spacing: float = 0.5,         # Voxel pitch in mm (matches PasoCuerpo = 0.5 mm)[cite: 1]
    unit_scale: float = 10.0,     # Standard FBX native unit is cm -> 10.0 to get mm (use 1000.0 if exported in meters)
    flip_x_for_unity: bool = True # Set False if Bake Axis Conversion is enabled in Unity or MapaTejidos.cs flips X
):
    """Voxelizes an anatomical FBX file with exact bounding box and origin registration[cite: 1]."""
    if not os.path.exists(fbx_path):
        raise FileNotFoundError(f"Input file not found: {fbx_path}")

    print(f"Loading FBX scene: {fbx_path}")
    classified_meshes, global_min, global_max = extract_fbx_meshes(
        fbx_path, unit_scale=unit_scale, flip_x_for_unity=flip_x_for_unity
    )

    if not classified_meshes:
        raise ValueError("No matching segmented meshes found in the FBX file.")

    # Exact FBX bounding box origin and extent (zero margin offset)
    origin = global_min.copy()
    extent = global_max - global_min

    # Voxel counts spanning the exact FBX bounding box
    nx = int(np.ceil(extent[0] / spacing))
    ny = int(np.ceil(extent[1] / spacing))
    nz = int(np.ceil(extent[2] / spacing))

    print("\n--- Exact FBX Volume Bounds (mm) ---")
    print(f"Unity X-Flip Applied: {flip_x_for_unity}")
    print(f"FBX Min (Origin): ({origin[0]:.4f}, {origin[1]:.4f}, {origin[2]:.4f}) mm")
    print(f"FBX Max:          ({global_max[0]:.4f}, {global_max[1]:.4f}, {global_max[2]:.4f}) mm")
    print(f"FBX Size (Extent):({extent[0]:.4f}, {extent[1]:.4f}, {extent[2]:.4f}) mm")
    print(f"Grid Dimensions:  {nx} x {ny} x {nz} ({nx * ny * nz:,} voxels)")
    print(f"Voxel Pitch:      {spacing} mm")

    # Grid storage: (Z, Y, X) for C-contiguous flat layout: index = i + nx * (j + ny * k)[cite: 1]
    grid = np.zeros((nz, ny, nx), dtype=np.uint8)
    classified_meshes.sort(key=lambda item: PRIORITY.index(item[0]))

    inv = 1.0 / spacing
    for tissue_key, mesh in classified_meshes:
        tag_id = TISSUE_IDS[tissue_key]
        print(f"Voxelizing {tissue_key} (ID: {tag_id})...")

        vox = mesh.voxelized(pitch=spacing)
        vox = vox.fill()

        voxel_centers = vox.points
        idx = np.floor((voxel_centers - origin) * inv).astype(int)

        valid = (
            (idx[:, 0] >= 0) & (idx[:, 0] < nx) &
            (idx[:, 1] >= 0) & (idx[:, 1] < ny) &
            (idx[:, 2] >= 0) & (idx[:, 2] < nz)
        )
        idx = idx[valid]
        grid[idx[:, 2], idx[:, 1], idx[:, 0]] = tag_id

    # Flatten in C-contiguous order to preserve C# indexing[cite: 1]
    flat_labels = grid.flatten(order="C")

    # Write GZip-compressed binary volume compatible with MapaTejidos.Cargar()[cite: 1]
    MAGIC = 0x314A544D  # "MTJ1"[cite: 1]
    print(f"\nWriting compressed data to: {output_bytes_path}...")
    with open(output_bytes_path, "wb") as f:
        with gzip.GzipFile(fileobj=f, mode="wb") as gz:
            header = struct.pack(
                "<Iiii4f",
                MAGIC,
                nx,
                ny,
                nz,
                float(origin[0]),
                float(origin[1]),
                float(origin[2]),
                float(spacing),
            )
            gz.write(header)
            gz.write(flat_labels.tobytes())

    print(f"Done! Raw voxel buffer size: {flat_labels.nbytes / (1024 * 1024):.2f} MB.")


if __name__ == "__main__":
    input_file = sys.argv[1] if len(sys.argv) > 1 else "LPanatomy2026.fbx"
    output_file = sys.argv[2] if len(sys.argv) > 2 else "TissueMap.bytes"

    voxelize_fbx(
        fbx_path=input_file,
        output_bytes_path=output_file,
        spacing=0.5,
        unit_scale=10.0,        # FBX defaults to cm (10.0 -> mm). Use 1000.0 if exported in meters.
        flip_x_for_unity=True,  # Aligns directly with Unity's default FBX import
    )