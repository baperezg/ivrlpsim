import argparse
import os
import sys
import numpy as np
import trimesh
from scipy.spatial import cKDTree
from scipy.spatial.transform import Rotation as R

# -------------------------------------------------------------------------
# 1. Dual Quaternion Operations
# -------------------------------------------------------------------------
def quat_mult(q1, q2):
    """Multiply two unit quaternions [x, y, z, w]."""
    x1, y1, z1, w1 = q1
    x2, y2, z2, w2 = q2
    return np.array([
        w1*x2 + x1*w2 + y1*z2 - z1*y2,
        w1*y2 - x1*z2 + y1*w2 + z1*x2,
        w1*z2 + x1*y2 - y1*x2 + z1*w2,
        w1*w2 - x1*x2 - y1*y2 - z1*z2
    ], dtype=np.float64)


def make_dual_quaternion(rot_euler_deg, trans, pivot):
    """
    Creates a unit dual quaternion (qr, qd) applying rotation about an
    anatomical pivot followed by an explicit translation vector (trans).
    """
    r = R.from_euler('xyz', rot_euler_deg, degrees=True)
    qr = r.as_quat()

    rotated_pivot = r.apply(pivot)
    # Total translation = translation vector + pivot correction
    total_trans = np.asarray(trans, dtype=np.float64) + pivot - rotated_pivot

    qt = np.array([total_trans[0], total_trans[1], total_trans[2], 0.0], dtype=np.float64)
    qd = 0.5 * quat_mult(qt, qr)
    return qr, qd


def transform_points_dq(points, qr, qd):
    """Rigid transform of Nx3 points via dual quaternion."""
    r = R.from_quat(qr)
    rot_p = r.apply(points)
    qr_conj = np.array([-qr[0], -qr[1], -qr[2], qr[3]], dtype=np.float64)
    t = (2.0 * quat_mult(qd, qr_conj))[:3]
    return rot_p + t


# -------------------------------------------------------------------------
# 2. Kinematic Defaults: Relative Rotations & Translations per Vertebra
# -------------------------------------------------------------------------
# Default segmental flexion angles [rx, ry, rz] in degrees (pitch around X)
DEFAULT_FLEXION_ANGLES = {
    "L5":  [4.0,  0.0, 0.0],  # L5 relative to S1/Sacrum
    "L4":  [8.0, 0.0, 0.0],  # L4 relative to L5
    "L3":  [8.5, 0.0, 0.0],  # L3 relative to L4
    "L2":  [8.5, 0.0, 0.0],  # L2 relative to L3
    "L1":  [8.5,  0.0, 0.0],  # L1 relative to L2
    "T12": [6.0,  0.0, 0.0],  # T12 relative to L1
    "T11": [4.0,  0.0, 0.0]   # T11 relative to T12
}

# Default segmental translation vectors [dx, dy, dz] in mm.
# +Z lifts the vertebra upward to make room for the disc.
# +Y translates along the sagittal/facet inclination plane.
DEFAULT_TRANSLATIONS = {
    "L5":  [0.0, 0.5, 2.0],
    "L4":  [0.0, 1.2, 4.0],  # Ample clearance for L4-L5
    "L3":  [0.0, 1.2, 4.0],  # Ample clearance for L3-L4
    "L2":  [0.0, 1.0, 3.5],  # Ample clearance for L2-L3
    "L1":  [0.0, 0.8, 3.0],  # Ample clearance for L1-L2
    "T12": [0.0, 0.5, 2.5],
    "T11": [0.0, 0.3, 2.0]
}

LEVELS = ["L5", "L4", "L3", "L2", "L1", "T12", "T11"]
ALL_ORDER = ["S1", "L5", "L4", "L3", "L2", "L1", "T12", "T11"]


def get_node_to_geometry_map(scene):
    node_map = {}
    if hasattr(scene, "graph") and hasattr(scene.graph, "nodes_geometry"):
        for node_name in scene.graph.nodes:
            geom_name = scene.graph.get(node_name)[1]
            if geom_name is not None and geom_name in scene.geometry:
                node_map[node_name.lower()] = geom_name

    for geom_name in scene.geometry.keys():
        lower_g = geom_name.lower()
        if lower_g not in node_map:
            node_map[lower_g] = geom_name
    return node_map


def extract_vertebral_data(scene, node_map):
    v_data = {}
    for lvl in ALL_ORDER:
        target_name = f"{lvl.lower()}vertebra"
        matched_geom = None
        for n_name, g_name in node_map.items():
            if target_name in n_name:
                matched_geom = scene.geometry[g_name]
                break

        if matched_geom is not None:
            bounds = matched_geom.bounds
            cx = (bounds[0][0] + bounds[1][0]) * 0.5
            cy = bounds[0][1] + (bounds[1][1] - bounds[0][1]) * 0.50
            cz = bounds[0][2]
            v_data[lvl] = {
                "pivot": np.array([cx, cy, cz], dtype=np.float64),
                "z_min": bounds[0][2],
                "z_max": bounds[1][2],
                "center_xy": np.array([cx, cy], dtype=np.float64),
                "mesh": matched_geom,
                "kdtree": cKDTree(matched_geom.vertices)
            }
        else:
            idx = ALL_ORDER.index(lvl)
            base_z = -286.0 + 35.0 * idx
            dummy_pts = np.array([[0.0, 180.0, base_z]])
            v_data[lvl] = {
                "pivot": np.array([0.0, 180.0, base_z], dtype=np.float64),
                "z_min": base_z,
                "z_max": base_z + 28.0,
                "center_xy": np.array([0.0, 180.0], dtype=np.float64),
                "mesh": None,
                "kdtree": cKDTree(dummy_pts)
            }
    return v_data


# -------------------------------------------------------------------------
# 3. Main Transformation Function
# -------------------------------------------------------------------------
def process_lumbar_model(input_gltf_path, output_gltf_path, angles_dict, trans_dict):
    print(f"Loading input model: {input_gltf_path}")
    loaded = trimesh.load(input_gltf_path)
    scene = trimesh.Scene(loaded) if isinstance(loaded, trimesh.Trimesh) else loaded

    node_map = get_node_to_geometry_map(scene)
    v_data = extract_vertebral_data(scene, node_map)

    # 1. Forward Kinematics Chain for Vertebrae
    vertebra_dqs = {}
    base_qr = np.array([0.0, 0.0, 0.0, 1.0], dtype=np.float64)
    base_qd = np.array([0.0, 0.0, 0.0, 0.0], dtype=np.float64)
    vertebra_dqs["S1"] = (base_qr.copy(), base_qd.copy())

    cur_qr = base_qr.copy()
    cur_qd = base_qd.copy()

    for lvl in LEVELS:
        rel_angles = angles_dict[lvl]
        rel_trans = trans_dict[lvl]
        
        # Dual quaternion combining relative rotation and translation
        rel_qr, rel_qd = make_dual_quaternion(rel_angles, rel_trans, v_data[lvl]["pivot"])
        
        # Q_cum = Q_rel * Q_prev
        new_qr = quat_mult(rel_qr, cur_qr)
        new_qd = quat_mult(rel_qr, cur_qd) + quat_mult(rel_qd, cur_qr)
        norm = np.linalg.norm(new_qr)
        cur_qr = new_qr / norm
        cur_qd = new_qd / norm
        vertebra_dqs[lvl] = (cur_qr, cur_qd)

    # 2. Intermediate Rigid Transforms for Discs (Midway between adjacent vertebrae)
    disc_dqs = {}
    disc_partitions = []
    for i in range(len(ALL_ORDER) - 1):
        low_lvl = ALL_ORDER[i]
        high_lvl = ALL_ORDER[i + 1]
        z_split = (v_data[low_lvl]["z_max"] + v_data[high_lvl]["z_min"]) * 0.5
        
        qr_low, qd_low = vertebra_dqs[low_lvl]
        qr_high, qd_high = vertebra_dqs[high_lvl]
        
        if np.dot(qr_low, qr_high) < 0.0:
            qr_high = -qr_high
            qd_high = -qd_high

        mid_qr = 0.5 * qr_low + 0.5 * qr_high
        mid_qd = 0.5 * qd_low + 0.5 * qd_high
        norm_mid = np.linalg.norm(mid_qr)
        mid_qr /= norm_mid
        mid_qd /= norm_mid

        disc_key = f"{low_lvl}_{high_lvl}"
        disc_dqs[disc_key] = (mid_qr, mid_qd)
        disc_partitions.append((z_split, disc_key))

    z_centers = np.array([(v_data[lvl]["z_min"] + v_data[lvl]["z_max"]) * 0.5 for lvl in ALL_ORDER])
    static_identifiers = ["s1vertebra", "righthip", "lefthip"]

    geom_to_label = {}
    for node_label, g_name in node_map.items():
        if g_name not in geom_to_label:
            geom_to_label[g_name] = node_label

    CONTACT_TOLERANCE = 2.0

    for g_name, mesh in scene.geometry.items():
        label = geom_to_label.get(g_name, g_name.lower())
        vertices = mesh.vertices.copy()

        # 1. Base Bones (Rigid Static)
        if any(stat in label for stat in static_identifiers):
            print(f"  [Static Base]      {label}")
            continue

        # 2. Vertebrae (Rigid 1.0 weight with segment translation)
        is_vertebra = False
        for lvl in LEVELS:
            if f"{lvl.lower()}vertebra" in label:
                qr, qd = vertebra_dqs[lvl]
                mesh.vertices = transform_points_dq(vertices, qr, qd)
                is_vertebra = True
                print(f"  [Rigid Vertebra]   {label} -> Rotated {angles_dict[lvl][0]:.1f}° | Lifted Z: {trans_dict[lvl][2]:.1f}mm")
                break

        if is_vertebra:
            continue

        is_disc = "disc" in label
        is_ligament = any(k in label for k in ["ligament", "supraspinous", "yellow"])

        # -------------------------------------------------------------
        # 3. INTERVERTEBRAL DISCS (Rigid, Preserving Shape & Height)
        # -------------------------------------------------------------
        if is_disc:
            print(f"  [Rigid Discs]      {label} ({len(vertices)} vertices) -> Preserved without stretching")
            deformed_verts = np.empty_like(vertices)
            
            disc_submeshes = mesh.split(only_watertight=False)
            print(f"    Separated into {len(disc_submeshes)} independent disc bodies")

            offset = 0
            for submesh in disc_submeshes:
                sub_verts = submesh.vertices
                sub_center_z = np.mean(sub_verts[:, 2])

                # Match to motion segment
                assigned_key = disc_partitions[0][1]
                for z_split, key in disc_partitions:
                    if sub_center_z >= z_split:
                        assigned_key = key

                qr, qd = disc_dqs[assigned_key]
                trans_sub_verts = transform_points_dq(sub_verts, qr, qd)

                deformed_verts[offset : offset + len(sub_verts)] = trans_sub_verts
                offset += len(sub_verts)

            mesh.vertices = deformed_verts

        # -------------------------------------------------------------
        # 4. LIGAMENTS, MUSCLES, CORD, FAT, SKIN (Smooth Deformation)
        # -------------------------------------------------------------
        else:
            print(f"  [Deforming Soft]   {label} ({len(vertices)} vertices)")
            deformed_verts = np.empty_like(vertices)

            for i, v in enumerate(vertices):
                vz = v[2]

                if vz <= z_centers[0]:
                    qr, qd = vertebra_dqs[ALL_ORDER[0]]
                    deformed_verts[i] = transform_points_dq(v.reshape(1, 3), qr, qd)[0]
                    continue
                elif vz >= z_centers[-1]:
                    qr, qd = vertebra_dqs[ALL_ORDER[-1]]
                    deformed_verts[i] = transform_points_dq(v.reshape(1, 3), qr, qd)[0]
                    continue

                idx = np.searchsorted(z_centers, vz) - 1
                idx = np.clip(idx, 0, len(ALL_ORDER) - 2)

                lvl_low = ALL_ORDER[idx]
                lvl_high = ALL_ORDER[idx + 1]

                dist_low, _ = v_data[lvl_low]["kdtree"].query(v)
                dist_high, _ = v_data[lvl_high]["kdtree"].query(v)

                if is_ligament:
                    # Enforce bone contact
                    if dist_low < CONTACT_TOLERANCE:
                        w_low, w_high = 1.0, 0.0
                    elif dist_high < CONTACT_TOLERANCE:
                        w_low, w_high = 0.0, 1.0
                    else:
                        inv_low = 1.0 / (dist_low ** 2)
                        inv_high = 1.0 / (dist_high ** 2)
                        w_low = inv_low / (inv_low + inv_high)
                        w_high = inv_high / (inv_low + inv_high)
                else:
                    inv_low = 1.0 / (dist_low + 1e-5)
                    inv_high = 1.0 / (dist_high + 1e-5)
                    w_low = inv_low / (inv_low + inv_high)
                    w_high = inv_high / (inv_low + inv_high)

                qr_low, qd_low = vertebra_dqs[lvl_low]
                qr_high, qd_high = vertebra_dqs[lvl_high]

                if np.dot(qr_low, qr_high) < 0.0:
                    qr_high = -qr_high
                    qd_high = -qd_high

                b_qr = w_low * qr_low + w_high * qr_high
                b_qd = w_low * qd_low + w_high * qd_high

                norm_b = np.linalg.norm(b_qr)
                b_qr /= norm_b
                b_qd /= norm_b

                deformed_verts[i] = transform_points_dq(v.reshape(1, 3), b_qr, b_qd)[0]

        mesh.vertices = deformed_verts

    print(f"Exporting transformed model to: {output_gltf_path}")
    scene.export(output_gltf_path)
    print("Process completed successfully.")


# -------------------------------------------------------------------------
# 4. CLI Argument Parsing
# -------------------------------------------------------------------------
if __name__ == "__main__":
    parser = argparse.ArgumentParser(
        description="Lumbar Spine Flexion Pipeline with Individual Segment Translation and Rotation Controls."
    )
    parser.add_argument("input", type=str, help="Path to input GLB file (e.g., lumbar_supine.glb)")
    parser.add_argument("-o", "--output", type=str, default=None, help="Output file path")
    parser.add_argument("--pitch-multiplier", type=float, default=1.0, help="Global multiplier for flexion angles")
    parser.add_argument("--lift-multiplier", type=float, default=1.0, help="Global multiplier for all Z-translations")

    # Optional individual Z-translation overrides (mm)
    parser.add_argument("--z-l5", type=float, default=None, help="L5 Z-translation in mm")
    parser.add_argument("--z-l4", type=float, default=None, help="L4 Z-translation in mm")
    parser.add_argument("--z-l3", type=float, default=None, help="L3 Z-translation in mm")
    parser.add_argument("--z-l2", type=float, default=None, help="L2 Z-translation in mm")
    parser.add_argument("--z-l1", type=float, default=None, help="L1 Z-translation in mm")
    parser.add_argument("--z-t12", type=float, default=None, help="T12 Z-translation in mm")
    parser.add_argument("--z-t11", type=float, default=None, help="T11 Z-translation in mm")

    # Optional individual pitch angle overrides (degrees)
    parser.add_argument("--pitch-l5", type=float, default=None, help="L5 pitch angle in deg")
    parser.add_argument("--pitch-l4", type=float, default=None, help="L4 pitch angle in deg")
    parser.add_argument("--pitch-l3", type=float, default=None, help="L3 pitch angle in deg")
    parser.add_argument("--pitch-l2", type=float, default=None, help="L2 pitch angle in deg")
    parser.add_argument("--pitch-l1", type=float, default=None, help="L1 pitch angle in deg")
    parser.add_argument("--pitch-t12", type=float, default=None, help="T12 pitch angle in deg")
    parser.add_argument("--pitch-t11", type=float, default=None, help="T11 pitch angle in deg")

    args = parser.parse_args()

    if not os.path.isfile(args.input):
        print(f"Error: {args.input} does not exist.", file=sys.stderr)
        sys.exit(1)

    if args.output is None:
        base, ext = os.path.splitext(args.input)
        args.output = f"{base}_adjusted_segments{ext}"

    # Build active dictionaries
    active_angles = {}
    active_translations = {}

    for lvl in LEVELS:
        # 1. Angles
        angle_val = DEFAULT_FLEXION_ANGLES[lvl][0] * args.pitch_multiplier
        override_pitch = getattr(args, f"pitch_{lvl.lower()}")
        if override_pitch is not None:
            angle_val = override_pitch
        active_angles[lvl] = [angle_val, 0.0, 0.0]

        # 2. Translations
        trans_vec = np.array(DEFAULT_TRANSLATIONS[lvl], dtype=np.float64)
        trans_vec[2] *= args.lift_multiplier
        override_z = getattr(args, f"z_{lvl.lower()}")
        if override_z is not None:
            trans_vec[2] = override_z
        active_translations[lvl] = trans_vec

    process_lumbar_model(args.input, args.output, active_angles, active_translations)