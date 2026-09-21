import numpy as np
import matplotlib.pyplot as plt

# -------------------------------------------------------------------------
# Parameters (matching the Unity LumbarPunctureDirectPluginForce script)
# -------------------------------------------------------------------------
device_force_scaling = (1.8 / 3.6767) * 0.65
scaling_co = 1.0
boundary_blend_width_mm = 1.0
dura_puncture_force_n = 3.0

# Cumulative Layer Depths (mm)
thick_skin      = 13.92
thick_fat       = 17.15
thick_ms_before = 19.37
thick_ms_after  = 20.0
thick_il_before = 23.18
thick_il_after  = 41.18
thick_lf_before = 44.79
thick_lf_after  = 48.38
thick_es        = 56.98
thick_dura      = 60.0   # Absolute cumulative depth of dura boundary
max_depth       = 65.0

# -------------------------------------------------------------------------
# Helper: Unity's Mathf.SmoothStep(a, b, t)
# -------------------------------------------------------------------------
def smoothstep(edge0, edge1, x):
    t = np.clip((x - edge0) / (edge1 - edge0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)

# -------------------------------------------------------------------------
# Force Computation Function
# -------------------------------------------------------------------------
def calculate_layer_force(depth):
    force = np.zeros_like(depth)

    for i, d in enumerate(depth):
        if d <= 0.0:
            force[i] = 0.0
        # 1. Skin
        elif d <= thick_skin:
            x_fixed = d
            force[i] = (
                0.0235
                + 0.0116 * (x_fixed / scaling_co)
                - 0.0046 * ((x_fixed / scaling_co) ** 2)
                + 0.0025 * ((x_fixed / scaling_co) ** 3)
            ) * device_force_scaling
        # 2. Fat
        elif d <= thick_fat:
            x_fixed = d - thick_skin
            raw_fat_force = (
                6.0372
                + 0.4516 * (x_fixed / scaling_co)
                - 0.5287 * ((x_fixed / scaling_co) ** 2)
            ) * device_force_scaling

            skin_exit_force = (
                0.0235
                + 0.0116 * (thick_skin / scaling_co)
                - 0.0046 * ((thick_skin / scaling_co) ** 2)
                + 0.0025 * ((thick_skin / scaling_co) ** 3)
            ) * device_force_scaling

            t = np.clip(x_fixed / boundary_blend_width_mm, 0.0, 1.0)
            blend = smoothstep(0.0, 1.0, t)
            force[i] = (1.0 - blend) * skin_exit_force + blend * raw_fat_force
        # 3. Supraspinous Ligament (MS) - Region 1
        elif d <= thick_ms_before:
            x_fixed = d - thick_fat
            force[i] = (
                1.9736
                + 0.8287 * (x_fixed / scaling_co)
                + 0.1078 * ((x_fixed / scaling_co) ** 2)
            ) * device_force_scaling
        # 4. Supraspinous Ligament (MS) - Region 2
        elif d <= thick_ms_after:
            x_fixed = d - thick_ms_before
            force[i] = (
                4.354
                - 2.2543 * (x_fixed / scaling_co)
                + 0.2902 * ((x_fixed / scaling_co) ** 2)
            ) * device_force_scaling
        # 5. Interspinous Ligament (IL) - Region 1 (Ramp)
        elif d <= thick_il_before:
            x_fixed = d - thick_ms_after
            force[i] = (
                4.4062 + 0.9598 * (x_fixed / scaling_co)
            ) * device_force_scaling
        # 6. Interspinous Ligament (IL) - Region 2 (Plateau)
        elif d <= thick_il_after:
            force[i] = 7.467 * device_force_scaling
        # 7. Ligamentum Flavum (LF) - Region 1
        elif d <= thick_lf_before:
            x_fixed = d - thick_il_after
            force[i] = (
                7.467
                + 1.5029 * (x_fixed / scaling_co)
                - 0.0583 * ((x_fixed / scaling_co) ** 2)
            ) * device_force_scaling
        # 8. Ligamentum Flavum (LF) - Region 2 (Peak)
        elif d <= thick_lf_after:
            x_fixed = d - thick_lf_before
            force[i] = (
                12.133
                - 0.1693 * (x_fixed / scaling_co)
                - 0.1177 * ((x_fixed / scaling_co) ** 2)
            ) * device_force_scaling
        # 9. Epidural Space (Loss of Resistance - LOR)
        elif d <= thick_es:
            force[i] = 0.0
        # 10. Dura Mater (Cumulative depth up to thick_dura)
        elif d <= thick_dura:
            x_fixed = d - thick_es
            dura_width = thick_dura - thick_es
            blend_range = min(boundary_blend_width_mm, dura_width)
            t = np.clip(x_fixed / blend_range, 0.0, 1.0)
            blend = smoothstep(0.0, 1.0, t)
            force[i] = blend * dura_puncture_force_n
        # 11. Subarachnoid Space (CSF / Pop-through)
        else:
            force[i] = 0.0

    return np.maximum(0.0, force)

# -------------------------------------------------------------------------
# Plotting
# -------------------------------------------------------------------------
depth_vals = np.linspace(0.0, max_depth, 2500)
force_vals = calculate_layer_force(depth_vals)

fig, ax = plt.subplots(figsize=(14, 6.5))
ax.plot(depth_vals, force_vals, color="#0B2545", linewidth=2.4, label="Penetration Force (N)", zorder=4)

# Cumulative boundary intervals: (start_mm, end_mm, name, color, y_text_pos)
layer_definitions = [
    (0.0, thick_skin, "Skin", "#FFE0BD", 3.75),
    (thick_skin, thick_fat, "Fat", "#FFF3A8", 3.25),
    (thick_fat, thick_ms_after, "Supraspinous (MS)", "#D4EDDA", 3.75),
    (thick_ms_after, thick_il_after, "Interspinous (IL)", "#CCE5FF", 3.75),
    (thick_il_after, thick_lf_after, "Lig. Flavum (LF)", "#FFEAA7", 3.75),
    (thick_lf_after, thick_es, "Epidural Space (LOR)", "#F8D7DA", 3.75),
    (thick_es, thick_dura, "Dura", "#E2D9F3", 3.75),
    (thick_dura, max_depth, "Subarachnoid\n(CSF)", "#D0F4DE", 3.75),
]

for start, end, label, color, y_pos in layer_definitions:
    ax.axvspan(start, end, alpha=0.45, color=color, zorder=1)
    midpoint = (start + end) / 2.0
    width = end - start
    
    # Adapt rotation and horizontal centering for narrow intervals
    rotation = 0 if width >= 7.0 else 90
    ax.text(
        midpoint,
        y_pos,
        label,
        rotation=rotation,
        ha="center",
        va="top",
        fontsize=8.5,
        fontweight="bold",
        color="#2B2D42",
        zorder=5,
        bbox=dict(boxstyle="round,pad=0.2", facecolor="white", alpha=0.75, edgecolor="none")
    )

# -------------------------------------------------------------------------
# Clinical Event Indicators
# -------------------------------------------------------------------------
# 1. Epidural Loss of Resistance (LOR)
ax.axvline(thick_lf_after, color="#B71C1C", linestyle="--", linewidth=1.4, zorder=3)
ax.annotate(
    "Epidural LOR\n(Drop to 0 N)",
    xy=(thick_lf_after, 0.05),
    xytext=(thick_lf_after - 4.5, 0.9),
    arrowprops=dict(arrowstyle="->", color="#B71C1C", lw=1.2),
    fontsize=9,
    fontweight="bold",
    color="#B71C1C",
    ha="right",
    zorder=6
)

# 2. Dural Puncture Pop-Through
ax.axvline(thick_dura, color="#4A148C", linestyle="--", linewidth=1.4, zorder=3)
ax.annotate(
    "Dural Pop\n(Target Reached)",
    xy=(thick_dura, 0.05),
    xytext=(thick_dura - 3.5, 1.8),
    arrowprops=dict(arrowstyle="->", color="#4A148C", lw=1.2),
    fontsize=9,
    fontweight="bold",
    color="#4A148C",
    ha="right",
    zorder=6
)

# Formatting
ax.set_title("Lumbar Puncture Simulation: Resistive Force vs. Penetration Depth", fontsize=13, fontweight="bold", pad=15)
ax.set_xlabel("Penetration Depth (mm)", fontsize=11, fontweight="semibold")
ax.set_ylabel("Haptic Device Force (N)", fontsize=11, fontweight="semibold")
ax.set_xlim(0.0, max_depth)
ax.set_ylim(-0.1, 4.0)
ax.grid(True, linestyle=":", alpha=0.6, zorder=2)
ax.legend(loc="upper left", framealpha=0.9)

plt.tight_layout()
plt.show()