import os
import glob
import numpy as np
import pandas as pd
import matplotlib.pyplot as plt
from mpl_toolkits.mplot3d import Axes3D
from scipy.signal import butter, filtfilt

def load_attempt_data(csv_path):
    """Load recording CSV and normalize column names."""
    df = pd.read_csv(csv_path)
    df.columns = [c.strip() for c in df.columns]
    return df

def apply_lowpass_filter(data, sampling_freq, cutoff_freq=10.0, order=4):
    """
    Applies a zero-phase 4th-order Butterworth low-pass filter
    to smooth high-frequency numerical differentiation noise.
    """
    nyquist = 0.5 * sampling_freq
    normal_cutoff = min(cutoff_freq / nyquist, 0.99)
    b, a = butter(order, normal_cutoff, btype='low', analog=False)
    return filtfilt(b, a, data)

def compute_summary_metrics(df):
    """
    Computes standard biomechanical and surgical dexterity metrics.
    """
    dt = np.diff(df['Timestamp_s'].values)
    mean_dt = np.mean(dt[dt > 0]) if len(dt) > 0 else 0.01
    sampling_rate = 1.0 / mean_dt

    positions = df[['PosX', 'PosY', 'PosZ']].values
    displacements = np.linalg.norm(np.diff(positions, axis=0), axis=1)
    
    # 1. Total Path Length
    total_path_length = np.sum(displacements)
    
    # 2. Straight-line (ideal) distance
    straight_line_dist = np.linalg.norm(positions[-1] - positions[0])
    
    # 3. Path Efficiency (1.0 is a perfectly straight insertion)
    path_efficiency = straight_line_dist / total_path_length if total_path_length > 0 else 0.0

    # 4. Total Duration
    duration = df['Timestamp_s'].iloc[-1] - df['Timestamp_s'].iloc[0]

    # 5. Log Dimensionless Jerk (LDLJ) based on velocity
    # Standard surgical metric: Balasubramanian et al. (2015)
    speed = df['Speed_m_s'].values
    v_peak = np.max(speed) if np.max(speed) > 1e-6 else 1e-6
    jerk_mag = df['JerkMag_m_s3'].values
    integrated_squared_jerk = np.trapezoid(jerk_mag**2, df['Timestamp_s'].values)
    
    dimensionless_jerk = (integrated_squared_jerk * (duration**3)) / (v_peak**2)
    ldlj = -np.log(dimensionless_jerk) if dimensionless_jerk > 0 else 0.0

    return {
        "Duration (s)": duration,
        "Total Path Length (m)": total_path_length,
        "Straight Distance (m)": straight_line_dist,
        "Path Efficiency": path_efficiency,
        "Mean Speed (m/s)": df['Speed_m_s'].mean(),
        "Peak Jerk (m/s^3)": df['JerkMag_m_s3'].max(),
        "Mean Jerk (m/s^3)": df['JerkMag_m_s3'].mean(),
        "Log Dimensionless Jerk (LDLJ)": ldlj
    }

def plot_kinematics_dashboard(df, title_prefix="Attempt Analysis"):
    """
    Renders a multi-panel time-series figure for kinematics and target distance.
    """
    time = df['Timestamp_s'].values
    speed = df['Speed_m_s'].values
    acc = df['AccMag_m_s2'].values
    jerk = df['JerkMag_m_s3'].values
    target_dist = df['TargetDistance_m'].values

    # Estimate sampling frequency for smoothing line
    dt = np.diff(time)
    fs = 1.0 / np.mean(dt[dt > 0])
    
    # Filtered jerk to display trend through derivative noise
    filtered_jerk = apply_lowpass_filter(jerk, fs, cutoff_freq=12.0)

    fig, axs = plt.subplots(4, 1, figsize=(11, 10), sharex=True)
    fig.suptitle(f"{title_prefix} - Kinematic Profile", fontsize=14, fontweight='bold')

    # Panel 1: Target Distance
    axs[0].plot(time, target_dist * 1000, color='#1f77b4', lw=2)
    axs[0].set_ylabel("Distance to Target (mm)")
    axs[0].grid(True, linestyle='--', alpha=0.6)

    # Panel 2: Speed
    axs[1].plot(time, speed * 1000, color='#2ca02c', lw=2)
    axs[1].set_ylabel("Speed (mm/s)")
    axs[1].grid(True, linestyle='--', alpha=0.6)

    # Panel 3: Acceleration
    axs[2].plot(time, acc, color='#ff7f0e', lw=1.5)
    axs[2].set_ylabel("Acceleration (m/s²)")
    axs[2].grid(True, linestyle='--', alpha=0.6)

    # Panel 4: Jerk (Raw vs Filtered)
    axs[3].plot(time, jerk, color='#d62728', alpha=0.25, label='Raw Jerk')
    axs[3].plot(time, filtered_jerk, color='#9467bd', lw=1.8, label='Filtered Trend (12Hz LPF)')
    axs[3].set_ylabel("Jerk (m/s³)")
    axs[3].set_xlabel("Time from Penetration (s)")
    axs[3].legend(loc="upper right")
    axs[3].grid(True, linestyle='--', alpha=0.6)

    plt.tight_layout()
    return fig

def plot_3d_trajectory(df, title_prefix="Attempt Trajectory"):
    """
    Renders a 3D needle trajectory scatter plot colored by needle speed.
    """
    fig = plt.figure(figsize=(9, 7))
    ax = fig.add_subplot(111, projection='3d')

    x = df['PosX'].values * 1000 # convert to mm
    y = df['PosY'].values * 1000
    z = df['PosZ'].values * 1000
    speed = df['Speed_m_s'].values * 1000

    scatter = ax.scatter(x, y, z, c=speed, cmap='plasma', s=8, alpha=0.8)
    ax.plot(x, y, z, color='gray', alpha=0.4, linestyle=':')

    # Highlight Start and Endpoint
    ax.scatter([x[0]], [y[0]], [z[0]], color='green', s=70, marker='o', label='Entry Point')
    ax.scatter([x[-1]], [y[-1]], [z[-1]], color='red', s=70, marker='X', label='End/Withdrawal')

    ax.set_title(f"{title_prefix} - 3D Needle Path", fontsize=12, fontweight='bold')
    ax.set_xlabel("X (mm)")
    ax.set_ylabel("Y (mm)")
    ax.set_zlabel("Z (mm)")
    ax.legend(loc="upper left")

    cbar = fig.colorbar(scatter, ax=ax, shrink=0.6, pad=0.1)
    cbar.set_label("Speed (mm/s)")

    plt.tight_layout()
    return fig

# --- Execution ---
if __name__ == "__main__":
    # Point this to your Unity project's Recordings folder
    recordings_dir = os.path.join("..", "Recordings")
    
    csv_files = glob.glob(os.path.join(recordings_dir, "*.csv"))

    if not csv_files:
        # Fallback to local working directory
        csv_files = glob.glob("*.csv")

    if not csv_files:
        print(f"No CSV recordings found in '{recordings_dir}' or the current folder.")
    else:
        # Pick the most recent CSV attempt
        latest_file = max(csv_files, key=os.path.getctime)
        filename = os.path.basename(latest_file)
        print(f"Loading latest attempt: {filename}\n")

        df = load_attempt_data(latest_file)

        # 1. Print Expertise Metrics
        metrics = compute_summary_metrics(df)
        print("=== EXPERT/NOVICE QUANTITATIVE METRICS ===")
        for k, v in metrics.items():
            print(f"{k:<30}: {v:.4f}")
        print("=========================================\n")

        # 2. Render Figures
        plot_kinematics_dashboard(df, title_prefix=filename)
        plot_3d_trajectory(df, title_prefix=filename)

        plt.show()