import sys
import os
import gzip
import struct
import numpy as np
import pyvista as pv
from matplotlib.colors import ListedColormap

# ---------------------------------------------------------------------------
# Tissue Labels and Colors (Hex / RGB) matching C# Tejido Enum
# ---------------------------------------------------------------------------
TISSUE_METADATA = {
    0:  {"name": "Air",                   "color": "#000000", "alpha": 0.0},
    1:  {"name": "Soft Tissue",           "color": "#CCA88A", "alpha": 0.3},
    2:  {"name": "Subcutaneous Fat",      "color": "#E6D743", "alpha": 0.3},
    3:  {"name": "Skin",                  "color": "#E5A58C", "alpha": 0.35},
    4:  {"name": "Muscle",                "color": "#BF3333", "alpha": 0.4},
    5:  {"name": "Dural Sac",             "color": "#33B2FF", "alpha": 0.9},
    6:  {"name": "Epidural Space",         "color": "#E5E580", "alpha": 0.8},
    7:  {"name": "Other Ligaments",       "color": "#4CCC80", "alpha": 0.85},
    8:  {"name": "Supraspinous Ligament", "color": "#33E54C", "alpha": 0.9},
    9:  {"name": "Ligamentum Flavum",     "color": "#1A9933", "alpha": 0.95},
    10: {"name": "Intervertebral Disc",   "color": "#66B2E5", "alpha": 0.9},
    11: {"name": "Bone",                  "color": "#F2F2E5", "alpha": 0.95},
}


def load_mapa_tejidos(file_path: str):
    if not os.path.exists(file_path):
        raise FileNotFoundError(f"File not found: {file_path}")

    print(f"Reading: {file_path}")
    with open(file_path, "rb") as f:
        with gzip.GzipFile(fileobj=f, mode="rb") as gz:
            header_bytes = gz.read(32)
            if len(header_bytes) < 32:
                raise ValueError("Corrupt file: Header smaller than 32 bytes.")

            magic, nx, ny, nz, ox, oy, oz, spacing = struct.unpack("<Iiii4f", header_bytes)

            if magic != 0x314A544D:
                raise ValueError(f"Invalid magic number: {hex(magic)} (expected 0x314A544D)")

            print("\n--- Volume Properties (Native Unity mm) ---")
            print("Orientation: +X Left, +Y Head (Superior), +Z Abdomen (Anterior)")
            print(f"Dimensions (X x Y x Z): {nx} x {ny} x {nz} ({nx * ny * nz:,} voxels)")
            print(f"Origin (mm): ({ox:.2f}, {oy:.2f}, {oz:.2f})")
            print(f"Voxel Pitch: {spacing} mm")

            raw_labels = gz.read(nx * ny * nz)
            if len(raw_labels) < (nx * ny * nz):
                raise ValueError("Incomplete voxel payload encountered.")

            # Reshape into (nz, ny, nx) matching C-order index = i + nx * (j + ny * k)
            grid = np.frombuffer(raw_labels, dtype=np.uint8).reshape((nz, ny, nx), order="C")

    # Transpose from (Z, Y, X) to (X, Y, Z) so PyVista aligns with Cartesian (X, Y, Z)
    grid_xyz = np.transpose(grid, (2, 1, 0))
    return grid_xyz, (ox, oy, oz), spacing


def create_pyvista_grid(grid_xyz: np.ndarray, origin: tuple, spacing: float) -> pv.ImageData:
    nx, ny, nz = grid_xyz.shape
    image = pv.ImageData()
    image.dimensions = (nx, ny, nz)
    image.origin = origin
    image.spacing = (spacing, spacing, spacing)
    image.point_data["TissueID"] = grid_xyz.flatten(order="F")
    return image


def visualize_volume(file_path: str, mode: str = "slices"):
    grid_xyz, origin, spacing = load_mapa_tejidos(file_path)

    unique_ids, counts = np.unique(grid_xyz, return_counts=True)
    print("\n--- Detected Tissues ---")
    for tid, count in zip(unique_ids, counts):
        meta = TISSUE_METADATA.get(tid, {"name": f"Unknown ({tid})"})
        volume_cm3 = (count * (spacing ** 3)) / 1000.0
        print(f"  ID {tid:2d} | {meta['name']:<24} | {count:10,d} voxels | {volume_cm3:8.2f} cm³")

    grid = create_pyvista_grid(grid_xyz, origin, spacing)

    plotter = pv.Plotter(window_size=(1400, 900))
    plotter.set_background("#1E1E24")

    # Update axis labels and grid coordinates to match new orientation:
    # X = Left (+X) / Right (-X)
    # Y = Head / Superior (+Y) / Feet / Inferior (-Y)
    # Z = Anterior / Abdomen (+Z) / Posterior / Back (-Z)
    plotter.add_axes(line_width=3, xlabel="Left (+X)", ylabel="Head (+Y)", zlabel="Ant (+Z)")
    plotter.show_grid(
        color="#444444",
        xtitle="Left / Right (mm)",
        ytitle="Head / Feet (mm)",
        ztitle="Anterior / Posterior (mm)"
    )

    colors = [TISSUE_METADATA.get(i, {"color": "#000000"})["color"] for i in range(12)]
    cmap = ListedColormap(colors)

    if mode == "slices":
        print("\nOpening interactive orthogonal slicer...")
        plotter.add_text(
            "Slicer (Planes: Coronal XY, Sagittal YZ, Axial XZ)\n"
            "View from Back (-Z facing +Z)",
            position="upper_left",
            font_size=10,
            color="white"
        )

        plotter.add_mesh_slice_orthogonal(
            grid,
            scalars="TissueID",
            cmap=cmap,
            clim=[0, 11],
            tubing=False,
        )
    else:
        print("\nExtracting tissue surfaces (Ignoring Air)...")
        plotter.add_text("Extracted Tissue Volumes", position="upper_left", font_size=11, color="white")

        for tid in unique_ids:
            if tid == 0:
                continue
            meta = TISSUE_METADATA.get(tid, {"name": f"Tissue {tid}", "color": "#FFFFFF", "alpha": 0.5})

            subset = grid.threshold([tid - 0.1, tid + 0.1], scalars="TissueID")
            if subset.n_cells > 0:
                plotter.add_mesh(
                    subset.extract_geometry(),
                    color=meta["color"],
                    opacity=meta["alpha"],
                    label=meta["name"],
                    smooth_shading=True,
                )

        plotter.add_legend(bcolor="#2A2A32", border=True, size=(0.25, 0.35))

    # Set camera looking at the patient's back (-Z looking towards +Z) with Head pointing Up (+Y)
    center = np.array(origin) + np.array(grid_xyz.shape) * spacing * 0.5
    cam_dist = max(grid_xyz.shape) * spacing * 1.8
    plotter.camera.position = (center[0], center[1], center[2] - cam_dist)
    plotter.camera.focal_point = center
    plotter.camera.up = (0, 1, 0)  # +Y is Head / Up
    plotter.show()


if __name__ == "__main__":
    path = sys.argv[1] if len(sys.argv) > 1 else "TissueMap.bytes"
    display_mode = sys.argv[2] if len(sys.argv) > 2 else "slices"

    visualize_volume(file_path=path, mode=display_mode)