import os
import glob
import numpy as np
import pandas as pd
import matplotlib.pyplot as plt
from mpl_toolkits.mplot3d import Axes3D
from scipy.signal import butter, filtfilt

def load_attempt_data(csv_path):
    df = pd.read_csv(csv_path)
    df.columns = [c.strip() for c in df.columns]
    return df

def apply_lowpass_filter(data, fs, cutoff_freq=12.0, order=4):
    """Zero-phase Butterworth low-pass filter to reveal underlying trends."""
    nyquist = 0.5 * fs
    normal_cutoff = min(cutoff_freq / nyquist, 0.99)
    b, a = butter(order, normal_cutoff, btype='low', analog=False)
    return filtfilt(b, a, data)

def compute_timing_and_kinematics_metrics(df):
    time = df['Timestamp_s'].values
    dt_ms = df['DeltaTime_s'].values[1:] * 1000.0 # Exclude sample 0 (dt = 0)
    
    # Timing Statistics
    mean_dt = np.mean(dt_ms)
    std_dt = np.std(dt_ms)
    max_dt = np.max(dt_ms)
    min_dt = np.min(dt_ms)
    effective_fs = 1000.0 / mean_dt if mean_dt > 0 else 0

    # Path & Kinematics
    positions = df[['PosX', 'PosY', 'PosZ']].values
    displacements = np.linalg.norm(np.diff(positions, axis=0), axis=1)
    total_path = np.sum(displacements)
    straight_dist = np.linalg.norm(positions[-1] - positions[0])
    path_eff = straight_dist / total_path if total_path > 0 else 0.0

    duration = time[-1] - time[0]
    speed = df['Speed_m_s'].values
    v_peak = np.max(speed) if np.max(speed) > 1e-6 else 1e-6
    jerk_mag = df['JerkMag_m_s3'].values
    integrated_sq_jerk = np.trapezoid(jerk_mag**2, time)

    ldlj = -np.log((integrated_sq_jerk * (duration**3)) / (v_peak**2)) if duration > 0 else 0.0
    dura_reached = bool(df['IsDuraReached'].iloc[-1]) if 'IsDuraReached' in df.columns else False

    return {
        "Dura Reached (Success)": dura_reached,
        "Total Duration (s)": duration,
        "Nominal Target Interval": f"~{round(mean_dt)} ms",
        "Mean Delta Time (ms)": mean_dt,
        "Delta Time StdDev / Jitter (ms)": std_dt,
        "Min / Max Delta Time (ms)": f"{min_dt:.2f} / {max_dt:.2f}",
        "Effective Sampling Rate (Hz)": effective_fs,
        "Total Path Length (mm)": total_path * 1000.0,
        "Path Efficiency Ratio": path_eff,
        "Peak Needle Speed (mm/s)": np.max(speed) * 1000.0,
        "Max Needle Force (N)": df['ForceMag_N'].max(),
        "Log Dimensionless Jerk (LDLJ)": ldlj
    }

def plot_comprehensive_dashboard(df, filename=""):
    time = df['Timestamp_s'].values
    dt_ms = df['DeltaTime_s'].values * 1000.0
    speed = df['Speed_m_s'].values * 1000.0 # mm/s
    acc = df['AccMag_m_s2'].values
    jerk = df['JerkMag_m_s3'].values
    force = df['ForceMag_N'].values
    target_dist = df['TargetDistance_m'].values * 1000.0 # mm

    # Filter design for jerk trend
    valid_dt = dt_ms[1:]
    fs = 1000.0 / np.mean(valid_dt)
    filtered_jerk = apply_lowpass_filter(jerk, fs, cutoff_freq=12.0)

    nominal_dt = round(np.mean(valid_dt))

    fig, axs = plt.subplots(6, 1, figsize=(12, 13), sharex=True)
    fig.suptitle(f"Needle Kinematics, Dynamics & Timing Stability\n{filename}", fontsize=13, fontweight='bold')

    # 1. Target Distance
    axs[0].plot(time, target_dist, color='#1f77b4', lw=1.8)
    axs[0].set_ylabel("Target Dist\n(mm)", fontsize=9)
    axs[0].grid(True, linestyle='--', alpha=0.5)

    # 2. Speed
    axs[1].plot(time, speed, color='#2ca02c', lw=1.8)
    axs[1].set_ylabel("Speed\n(mm/s)", fontsize=9)
    axs[1].grid(True, linestyle='--', alpha=0.5)

    # 3. Acceleration
    axs[2].plot(time, acc, color='#ff7f0e', lw=1.5)
    axs[2].set_ylabel("Acc\n(m/s²)", fontsize=9)
    axs[2].grid(True, linestyle='--', alpha=0.5)

    # 4. Jerk (Raw + Filtered)
    axs[3].plot(time, jerk, color='#9467bd', alpha=0.25, label='Raw Jerk')
    axs[3].plot(time, filtered_jerk, color='#6a1b9a', lw=1.8, label='12 Hz LPF')
    axs[3].set_ylabel("Jerk\n(m/s³)", fontsize=9)
    axs[3].legend(loc="upper right", fontsize=8)
    axs[3].grid(True, linestyle='--', alpha=0.5)

    # 5. Applied Needle Force
    axs[4].plot(time, force, color='#d62728', lw=1.8)
    if 'IsDuraReached' in df.columns and df['IsDuraReached'].iloc[-1] == 1:
        axs[4].scatter(time[-1], force[-1], color='purple', s=60, zorder=5, label='Dural Pop')
        axs[4].legend(loc="upper right", fontsize=8)
    axs[4].set_ylabel("Force\n(N)", fontsize=9)
    axs[4].grid(True, linestyle='--', alpha=0.5)

    # 6. Measured Delta Time (dt) Jitter
    axs[5].plot(time[1:], dt_ms[1:], color='#00838f', lw=1.2, label='Actual dt')
    axs[5].axhline(nominal_dt, color='red', linestyle='--', lw=1.2, label=f'Nominal ({nominal_dt:.1f} ms)')
    axs[5].fill_between(time[1:], nominal_dt - 1.0, nominal_dt + 1.0, color='gray', alpha=0.15, label='±1.0 ms Band')
    axs[5].set_ylabel("Δt\n(ms)", fontsize=9)
    axs[5].set_xlabel("Time from Penetration Start (s)", fontsize=10)
    axs[5].legend(loc="upper right", fontsize=8)
    axs[5].grid(True, linestyle='--', alpha=0.5)

    # Set reasonable y-limits on dt graph to prevent initial spike from blowing out the axis
    axs[5].set_ylim(max(0, nominal_dt - 6.0), nominal_dt + 6.0)

    plt.tight_layout()
    return fig

def plot_3d_spatial_path(df, filename=""):
    fig = plt.figure(figsize=(9, 7))
    ax = fig.add_subplot(111, projection='3d')

    x = df['PosX'].values * 1000.0
    y = df['PosY'].values * 1000.0
    z = df['PosZ'].values * 1000.0
    speed = df['Speed_m_s'].values * 1000.0

    scatter = ax.scatter(x, y, z, c=speed, cmap='plasma', s=10, alpha=0.8)
    ax.plot(x, y, z, color='gray', alpha=0.4, linestyle=':')

    ax.scatter([x[0]], [y[0]], [z[0]], color='green', s=80, marker='o', label='Penetration Entry')
    ax.scatter([x[-1]], [y[-1]], [z[-1]], color='crimson', s=90, marker='X', label='Terminal Point')

    ax.set_title(f"3D Needle Trajectory (Colored by Speed)\n{filename}", fontsize=11, fontweight='bold')
    ax.set_xlabel("Lateral X (mm)")
    ax.set_ylabel("Vertical Y (mm)")
    ax.set_zlabel("Axial Z (mm)")
    ax.legend(loc="upper left")

    cbar = fig.colorbar(scatter, ax=ax, shrink=0.6, pad=0.1)
    cbar.set_label("Speed (mm/s)")

    plt.tight_layout()
    return fig

# --- Execution ---
if __name__ == "__main__":
    recordings_dir = os.path.join("..", "Recordings")
    csv_files = glob.glob(os.path.join(recordings_dir, "*.csv")) or glob.glob("*.csv")

    if not csv_files:
        print(f"No CSV recordings found in '{recordings_dir}' or the active working directory.")
    else:
        latest_file = max(csv_files, key=os.path.getctime)
        filename = os.path.basename(latest_file)
        print(f"Loading attempt file: {filename}\n")

        df = load_attempt_data(latest_file)

        # 1. Print Timing Health & Dexterity Metrics
        metrics = compute_timing_and_kinematics_metrics(df)
        print("=== ATTEMPT KINEMATICS & TIMING HEALTH REPORT ===")
        for k, v in metrics.items():
            if isinstance(v, float):
                print(f"{k:<34}: {v:.4f}")
            else:
                print(f"{k:<34}: {v}")
        print("=================================================\n")

        # 2. Render Dashboards
        plot_comprehensive_dashboard(df, filename=filename)
        plot_3d_spatial_path(df, filename=filename)

        plt.show()