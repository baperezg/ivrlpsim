import sys
import os
import gzip
import struct
import numpy as np
import nrrd

# ---------------------------------------------------------------------------
# Tissue Labels, Colors, and Metadata matching C# Tissue enum
# Colors in RGBA (0-255)
# ---------------------------------------------------------------------------
TISSUE_METADATA = {
    0:  {"name": "Air",                   "color": (0, 0, 0, 0)},
    1:  {"name": "SoftTissue",            "color": (200, 160, 150, 255)},
    2:  {"name": "Fat",                   "color": (242, 217, 102, 255)},
    3:  {"name": "Skin",                  "color": (242, 191, 166, 255)},
    4:  {"name": "Muscle",                "color": (191, 51, 51, 255)},
    5:  {"name": "DuralSac",              "color": (255, 224, 115, 255)},
    6:  {"name": "EpiduralSpace",         "color": (115, 191, 255, 255)},
    7:  {"name": "OtherLigaments",        "color": (204, 204, 178, 255)},
    8:  {"name": "SupraspinousLigament",  "color": (217, 178, 115, 255)},
    9:  {"name": "LigamentumFlavum",      "color": (250, 209, 26, 255)},
    10: {"name": "IntervertebralDisc",    "color": (115, 166, 230, 255)},
    11: {"name": "Bone",                  "color": (240, 232, 214, 255)},
}


def read_tissue_map(file_path: str):
    """
    Decompresses and parses TissueMap.bytes layout:
      - Magic: uint32 (0x314A544D, "MTJ1")
      - nx, ny, nz: int32, int32, int32
      - origin (ox, oy, oz): float32, float32, float32
      - spacing: float32
      - labels: uint8 array of size nx * ny * nz
    """
    if not os.path.exists(file_path):
        raise FileNotFoundError(f"File not found: {file_path}")

    print(f"Reading binary data from: {file_path}")
    with open(file_path, "rb") as f:
        with gzip.GzipFile(fileobj=f, mode="rb") as gz:
            header_bytes = gz.read(32)
            if len(header_bytes) < 32:
                raise ValueError("Corrupt file: Header smaller than 32 bytes.")

            magic, nx, ny, nz, ox, oy, oz, spacing = struct.unpack("<Iiii4f", header_bytes)

            if magic != 0x314A544D:
                raise ValueError(f"Invalid magic number: {hex(magic)} (expected 0x314A544D)")

            print(f"Grid dimensions: {nx} x {ny} x {nz} ({nx * ny * nz:,} voxels)")
            print(f"Native origin (mm): ({ox:.2f}, {oy:.2f}, {oz:.2f})")
            print(f"Pitch: {spacing} mm")

            raw_labels = gz.read(nx * ny * nz)
            if len(raw_labels) < (nx * ny * nz):
                raise ValueError("Incomplete voxel payload.")

            # Reshape into (nz, ny, nx) based on C-order index = i + nx * (j + ny * k)
            grid = np.frombuffer(raw_labels, dtype=np.uint8).reshape((nz, ny, nx), order="C")

    # Transpose from (nz, ny, nx) -> (nx, ny, nz) so 3D Slicer / NRRD indices correspond to (X, Y, Z)
    volume_xyz = np.transpose(grid, (2, 1, 0))
    return volume_xyz, (ox, oy, oz), spacing


def export_slicer_color_table(table_path: str):
    """
    Creates a 3D Slicer color table (.ctbl) matching the tissue labels.
    Format per line: LabelNumber LabelName R G B A (normalized 0-1 or 0-255).
    """
    with open(table_path, "w") as f:
        f.write("# Slicer Color Table File\n")
        f.write("# ID Name R G B A\n")
        for tid in range(12):
            meta = TISSUE_METADATA.get(tid, {"name": f"Tissue_{tid}", "color": (128, 128, 128, 255)})
            r, g, b, a = meta["color"]
            f.write(f"{tid} {meta['name']} {r} {g} {b} {a}\n")
    print(f"Created 3D Slicer color table: {table_path}")


def convert_bytes_to_nrrd(input_bytes_path: str, output_nrrd_path: str = "TissueMap.seg.nrrd"):
    volume_xyz, origin, spacing = read_tissue_map(input_bytes_path)

    # In your project:
    # +X: Left (+1) / -X: Right
    # +Y: Head/Superior (+1) / -Y: Feet
    # +Z: Anterior/Abdomen (+1) / -Z: Posterior
    # This coordinate frame corresponds to Left-Anterior-Superior (LAS) space.
    ox, oy, oz = origin

    # Build the NRRD header specifying radiological metadata
    header = {
        "type": "unsigned char",
        "dimension": 3,
        "space": "left-anterior-superior",  # LAS coordinate system
        "sizes": list(volume_xyz.shape),
        "space directions": [
            [spacing, 0.0, 0.0],  # Voxel step along X (Left)
            [0.0, 0.0, spacing],  # glTF/Unity +Z (Anterior) maps to LAS Anterior
            [0.0, spacing, 0.0],  # glTF/Unity +Y (Head/Superior) maps to LAS Superior
        ],
        "space origin": [ox, oz, oy],
        "kinds": ["domain", "domain", "domain"],
        "endian": "little",
        "encoding": "gzip",
    }

    print(f"Writing NRRD segmentation volume to: {output_nrrd_path}...")
    nrrd.write(output_nrrd_path, volume_xyz, header)
    print("NRRD export completed successfully.")

    # Generate the companion color table file
    color_table_path = os.path.splitext(output_nrrd_path)[0] + ".ctbl"
    export_slicer_color_table(color_table_path)


if __name__ == "__main__":
    src_file = sys.argv[1] if len(sys.argv) > 1 else "TissueMap.bytes"
    dst_file = sys.argv[2] if len(sys.argv) > 2 else "TissueMap.seg.nrrd"

    convert_bytes_to_nrrd(src_file, dst_file)