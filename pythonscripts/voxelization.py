import sys
import os
import gzip
import struct
import numpy as np
import trimesh

# ---------------------------------------------------------------------------
# 1. TISSUE ENUMERATION (Mapped to English names)
# NOTE: The numerical IDs (0 to 11) strictly match the underlying C# enum 
# layout from ParametrosFuerza.cs and MapaTejidos.cs.
# ---------------------------------------------------------------------------
TISSUE_IDS = {
    "Air": 0,                     # C# Tejido.Aire
    "SoftTissue": 1,              # C# Tejido.TejidoBlando
    "Fat": 2,                     # C# Tejido.Grasa
    "Skin": 3,                    # C# Tejido.Piel
    "Muscle": 4,                  # C# Tejido.Musculo
    "DuralSac": 5,                # C# Tejido.SacoDural
    "EpiduralSpace": 6,           # C# Tejido.EspacioEpidural
    "OtherLigaments": 7,          # C# Tejido.OtrosLigamentos
    "SupraspinousLigament": 8,    # C# Tejido.LigSupraespinoso
    "LigamentumFlavum": 9,        # C# Tejido.LigAmarillo
    "IntervertebralDisc": 10,     # C# Tejido.Disco
    "Bone": 11,                   # C# Tejido.Hueso
}

# ---------------------------------------------------------------------------
# 2. NAME MAPPING: glTF node/mesh identifiers -> English tissue types
# ---------------------------------------------------------------------------
NAME_TO_TISSUE = {
    # Skin
    "SkinThin": "Skin",
    "skin": "Skin",
    
    # Subcutaneous Fat
    "fat": "Fat",
    "subcut": "Fat",
    
    # Muscle
    "leftdeepbackmuscle": "Muscle",
    "rightdeepbackmuscle": "Muscle",
    "muscle": "Muscle",
    
    # Specific Ligaments
    "SupraspinousLigament": "SupraspinousLigament",
    "YellowLigament": "LigamentumFlavum",
    "ligamentumflavum": "LigamentumFlavum",
    "Ligaments": "OtherLigaments",
    
    # Dural Sac & Epidural Space
    "EpiduralSpace": "EpiduralSpace",
    "epidural": "EpiduralSpace",
    "spinalcord": "DuralSac",
    "spinal cord": "DuralSac",
    "duralsac": "DuralSac",
    "sacodural": "DuralSac",
    
    # Intervertebral Discs
    "intervertebral_discs": "IntervertebralDisc",
    "intervertebraldiscs": "IntervertebralDisc",
    "disc": "IntervertebralDisc",
    
    # Bone Structures
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
# Skin and Fat are rasterized early so internal structures retain their volume.
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
    "Bone",  # Hardest unyielding contact placed last
]


def resolve_tissue_type(name: str) -> str:
    """Matches the glTF submesh name against classified anatomical tissue keys."""
    clean = name.lower().replace(" ", "").replace("_", "").strip()
    for pattern in sorted(NAME_TO_TISSUE.keys(), key=len, reverse=True):
        pattern_clean = pattern.lower().replace(" ", "").replace("_", "")
        if pattern_clean in clean:
            return NAME_TO_TISSUE[pattern]
    return None


def extract_gltf_meshes(scene: trimesh.Scene, unit_scale: float, flip_x_for_unity: bool = True):
    """
    Bakes node graph transforms into world coordinates, scales to mm,
    and computes exact scene bounding box matching the full GLB file.
    """
    classified = []
    global_min = np.array([np.inf, np.inf, np.inf])
    global_max = np.array([-np.inf, -np.inf, -np.inf])

    for node_name in scene.graph.nodes_geometry:
        transform, geom_name = scene.graph.get(node_name)
        geom = scene.geometry.get(geom_name)

        if not isinstance(geom, trimesh.Trimesh) or geom.vertices.shape[0] == 0:
            continue

        # Transform every mesh in the GLB first so global_min/max reflects the EXACT GLB bounding box
        mesh = geom.copy()
        mesh.apply_transform(transform)
        mesh.vertices *= unit_scale

        # Convert glTF right-handed (+X Left) to Unity left-handed (+X Right)
        if flip_x_for_unity:
            mesh.vertices[:, 0] *= -1.0
            mesh.invert()  # Keep face winding consistent after mirroring X

        bounds = mesh.bounds
        global_min = np.minimum(global_min, bounds[0])
        global_max = np.maximum(global_max, bounds[1])

        # Classify tissue for voxelization
        tissue_key = resolve_tissue_type(node_name) or resolve_tissue_type(geom_name)
        if not tissue_key:
            print(f"  [Bounds Only] Unrecognized glTF mesh/node: '{node_name}' ('{geom_name}')")
            continue

        print(f"  [Found] '{node_name}' -> Tissue: {tissue_key}")
        classified.append((tissue_key, mesh))

    return classified, global_min, global_max


def voxelize_gltf(
    gltf_path: str,
    output_bytes_path: str = "TissueMap.bytes",
    spacing: float = 0.5,         # Voxel pitch in mm (matches PasoCuerpo = 0.5 mm)
    unit_scale: float = 1000.0,   # Set to 10.0 if glTF is in cm; 1000.0 if in meters
    flip_x_for_unity: bool = True # Set False if MapaTejidos.cs already negates X on load
):
    """Voxelizes an anatomical glTF/GLB file with exact bounding box and origin registration."""
    if not os.path.exists(gltf_path):
        raise FileNotFoundError(f"Input file not found: {gltf_path}")

    print(f"Loading glTF scene: {gltf_path}")
    scene = trimesh.load(gltf_path, force="scene")

    classified_meshes, global_min, global_max = extract_gltf_meshes(
        scene, unit_scale=unit_scale, flip_x_for_unity=flip_x_for_unity
    )

    if not classified_meshes:
        raise ValueError("No matching segmented meshes found in the glTF file.")

    # Exact GLB bounding box origin and extent (zero margin offset)
    origin = global_min.copy()
    extent = global_max - global_min

    # Voxel counts spanning the exact GLB bounding box
    nx = int(np.ceil(extent[0] / spacing))
    ny = int(np.ceil(extent[1] / spacing))
    nz = int(np.ceil(extent[2] / spacing))

    print("\n--- Exact GLB Volume Bounds (mm) ---")
    print(f"Unity X-Flip Applied: {flip_x_for_unity}")
    print(f"GLB Min (Origin): ({origin[0]:.4f}, {origin[1]:.4f}, {origin[2]:.4f}) mm")
    print(f"GLB Max:          ({global_max[0]:.4f}, {global_max[1]:.4f}, {global_max[2]:.4f}) mm")
    print(f"GLB Size (Extent):({extent[0]:.4f}, {extent[1]:.4f}, {extent[2]:.4f}) mm")
    print(f"Grid Dimensions:  {nx} x {ny} x {nz} ({nx * ny * nz:,} voxels)")
    print(f"Voxel Pitch:      {spacing} mm")

    # Grid storage: (Z, Y, X) for C-contiguous flat layout: index = i + nx * (j + ny * k)
    grid = np.zeros((nz, ny, nx), dtype=np.uint8)
    classified_meshes.sort(key=lambda item: PRIORITY.index(item[0]))

    inv = 1.0 / spacing
    for tissue_key, mesh in classified_meshes:
        tag_id = TISSUE_IDS[tissue_key]
        print(f"Voxelizing {tissue_key} (ID: {tag_id})...")

        vox = mesh.voxelized(pitch=spacing)
        vox = vox.fill()

        # Map voxel center coordinates relative to exact GLB origin
        # Using floor((pos - origin) / spacing) places voxel (0,0,0) covering [origin, origin + spacing]
        voxel_centers = vox.points
        idx = np.floor((voxel_centers - origin) * inv).astype(int)

        valid = (
            (idx[:, 0] >= 0) & (idx[:, 0] < nx) &
            (idx[:, 1] >= 0) & (idx[:, 1] < ny) &
            (idx[:, 2] >= 0) & (idx[:, 2] < nz)
        )
        idx = idx[valid]
        grid[idx[:, 2], idx[:, 1], idx[:, 0]] = tag_id

    # Flatten in C-contiguous order to preserve C# indexing
    flat_labels = grid.flatten(order="C")

    # Write GZip-compressed binary volume compatible with MapaTejidos.Cargar()
    MAGIC = 0x314A544D  # "MTJ1"
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
    input_file = sys.argv[1] if len(sys.argv) > 1 else "LPanatomy2026.glb"
    output_file = sys.argv[2] if len(sys.argv) > 2 else "TissueMap.bytes"

    voxelize_gltf(
        gltf_path=input_file,
        output_bytes_path=output_file,
        spacing=0.5,
        unit_scale=1000.0,      # 1000.0 for standard glTF meters -> mm
        flip_x_for_unity=True,  # Aligns directly with Unity's left-handed coordinate space
    )