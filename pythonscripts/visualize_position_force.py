import os
import glob
import numpy as np
import pandas as pd
import matplotlib.pyplot as plt
from mpl_toolkits.mplot3d import Axes3D

def load_data(csv_path):
    df = pd.read_csv(csv_path)
    df.columns = [c.strip() for c in df.columns]
    return df

def plot_force_and_position(df, filename=""):
    time = df['Timestamp_s'].values
    
    # Position in millimeters
    pos_x = df['PosX'].values * 1000
    pos_y = df['PosY'].values * 1000
    pos_z = df['PosZ'].values * 1000
    
    # Net insertion depth relative to entry point
    entry_pos = np.array([pos_x[0], pos_y[0], pos_z[0]])
    current_pos = np.column_stack((pos_x, pos_y, pos_z))
    depth_mm = np.linalg.norm(current_pos - entry_pos, axis=1)

    # Force components and magnitude (N)
    f_x = df['ForceX_N'].values
    f_y = df['ForceY_N'].values
    f_z = df['ForceZ_N'].values
    f_mag = df['ForceMag_N'].values

    # -------------------------------------------------------------
    # FIGURE 1: Synchronized Time-Series & Force vs Depth
    # -------------------------------------------------------------
    fig, axs = plt.subplots(3, 1, figsize=(10, 9), sharex=False)
    fig.suptitle(f"Needle Position & Force Profile\n{filename}", fontsize=13, fontweight='bold')

    # Panel 1: 3D Position Coordinates vs Time
    axs[0].plot(time, pos_x, label='X (Lateral)', color='#1f77b4', lw=1.5)
    axs[0].plot(time, pos_y, label='Y (Vertical)', color='#2ca02c', lw=1.5)
    axs[0].plot(time, pos_z, label='Z (Axial/Depth)', color='#9467bd', lw=1.8)
    axs[0].set_ylabel("Position (mm)")
    axs[0].set_xlabel("Time (s)")
    axs[0].legend(loc="upper right")
    axs[0].grid(True, linestyle='--', alpha=0.5)

    # Panel 2: Force Components and Total Magnitude vs Time
    axs[1].plot(time, f_x, label='Fx', color='#1f77b4', linestyle=':', lw=1.2)
    axs[1].plot(time, f_y, label='Fy', color='#2ca02c', linestyle=':', lw=1.2)
    axs[1].plot(time, f_z, label='Fz', color='#9467bd', linestyle=':', lw=1.2)
    axs[1].plot(time, f_mag, label='|F| Total Force', color='#d62728', lw=2.0)
    axs[1].set_ylabel("Force (N)")
    axs[1].set_xlabel("Time (s)")
    axs[1].legend(loc="upper right")
    axs[1].grid(True, linestyle='--', alpha=0.5)

    # Panel 3: Force vs. Insertion Depth (Haptic Signature)
    axs[2].plot(depth_mm, f_mag, color='#d62728', lw=2)
    axs[2].set_ylabel("Force |F| (N)")
    axs[2].set_xlabel("Insertion Depth from Surface (mm)")
    axs[2].set_title("Force vs. Depth Profile (Tissue Layers Signature)", fontsize=11)
    axs[2].grid(True, linestyle='--', alpha=0.5)

    plt.tight_layout()

    # -------------------------------------------------------------
    # FIGURE 2: 3D Trajectory Colored by Force Resistance
    # -------------------------------------------------------------
    fig_3d = plt.figure(figsize=(9, 7))
    ax_3d = fig_3d.add_subplot(111, projection='3d')

    scatter = ax_3d.scatter(pos_x, pos_y, pos_z, c=f_mag, cmap='hot_r', s=12, alpha=0.9)
    ax_3d.plot(pos_x, pos_y, pos_z, color='gray', alpha=0.35, linestyle='--')

    # Mark Start and End Points
    ax_3d.scatter([pos_x[0]], [pos_y[0]], [pos_z[0]], color='blue', s=70, marker='o', label='Entry Point')
    ax_3d.scatter([pos_x[-1]], [pos_y[-1]], [pos_z[-1]], color='green', s=70, marker='X', label='Target / Deepest Point')

    ax_3d.set_title(f"3D Path Colored by Force Magnitude (N)\n{filename}", fontsize=12, fontweight='bold')
    ax_3d.set_xlabel("X (mm)")
    ax_3d.set_ylabel("Y (mm)")
    ax_3d.set_zlabel("Z (mm)")
    ax_3d.legend(loc="upper left")

    cbar = fig_3d.colorbar(scatter, ax=ax_3d, shrink=0.6, pad=0.1)
    cbar.set_label("Force Magnitude (N)")

    plt.tight_layout()

    return fig, fig_3d

# --- Main Execution ---
if __name__ == "__main__":
    recordings_dir = os.path.join("..", "Recordings")
    csv_files = glob.glob(os.path.join(recordings_dir, "*.csv")) or glob.glob("*.csv")

    if not csv_files:
        print("No CSV files found in the directory.")
    else:
        latest_file = max(csv_files, key=os.path.getctime)
        filename = os.path.basename(latest_file)
        print(f"Plotting position and force for: {filename}")

        df = load_data(latest_file)
        plot_force_and_position(df, filename=filename)
        plt.show()