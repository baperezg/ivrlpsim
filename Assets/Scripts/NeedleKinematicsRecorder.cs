using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class NeedleKinematicsRecorder : MonoBehaviour
{
    [Header("Target References")]
    [Tooltip("Reference to the LumbarPunctureDirectPluginForce script containing state flags and force")]
    [SerializeField] private LumbarPunctureDirectPluginForce pluginForce;

    [Tooltip("Transform of the needle tip or needle body to track")]
    [SerializeField] private Transform needleTransform;

    [Tooltip("Optional static target position (e.g., subarachnoid target) for deviation metrics")]
    [SerializeField] private Transform targetTransform;

    [Header("Sampling Configuration")]
    [Tooltip("Sampling interval in milliseconds (e.g., 10 for 100Hz, 20 for 50Hz, 50 for 20Hz)")]
    [Range(2, 200)]
    [SerializeField] private int sampleIntervalMs = 10;

    [Header("Output Settings")]
    [SerializeField] private string filePrefix = "LP_Kinematics_SW";
    [SerializeField] private string outputSubfolder = "Recordings";

    // ------------------------------------------------------------------------
    // Thread-safe data state snapshot (Transferred from Unity to Sampling loop)
    // ------------------------------------------------------------------------
    private struct NeedleStateSnapshot
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 eulerAngles;
        public Vector3 force;
        public float targetDistance;
        public bool isPenetrating;
        public bool isInsideBoundingBox;
        public bool duralPunctureOccurred;
    }

    private NeedleStateSnapshot _latestSnapshot;
    private readonly object _snapshotLock = new object();

    // High-resolution Timing & Threads
    private Thread _samplingThread;
    private Thread _writingThread;
    private volatile bool _threadsActive = false;

    private readonly ConcurrentQueue<string> _logQueue = new ConcurrentQueue<string>();
    private string _directoryPath;
    private int _attemptCounter = 0;

    private const string FILE_SWITCH_PREFIX = "__SWITCH_FILE__:";

    // Added DeltaTime_s right after Timestamp_s
    private const string CSV_HEADER = "Timestamp_s,DeltaTime_s," +
                                      "PosX,PosY,PosZ," +
                                      "RotX,RotY,RotZ," +
                                      "QuatX,QuatY,QuatZ,QuatW," +
                                      "VelX,VelY,VelZ,Speed_m_s," +
                                      "AccX,AccY,AccZ,AccMag_m_s2," +
                                      "JerkX,JerkY,JerkZ,JerkMag_m_s3," +
                                      "ForceX_N,ForceY_N,ForceZ_N,ForceMag_N," +
                                      "TargetDistance_m," +
                                      "IsDuraReached";

    private void Awake()
    {
        if (needleTransform == null)
            needleTransform = transform;

        if (pluginForce == null)
            pluginForce = GetComponent<LumbarPunctureDirectPluginForce>();
    }

    private void Start()
    {
        _directoryPath = Path.Combine(Application.dataPath, "..", outputSubfolder);
        if (!Directory.Exists(_directoryPath))
        {
            Directory.CreateDirectory(_directoryPath);
        }

        _threadsActive = true;

        // 1. Thread for high-frequency kinematics sampling
        _samplingThread = new Thread(SamplingWorkerLoop)
        {
            Name = "Kinematics_Sampler_Thread",
            IsBackground = true,
            Priority = System.Threading.ThreadPriority.Highest
        };
        _samplingThread.Start();

        // 2. Thread for asynchronous disk I/O
        _writingThread = new Thread(FileWritingWorkerLoop)
        {
            Name = "Kinematics_Writer_Thread",
            IsBackground = true,
            Priority = System.Threading.ThreadPriority.BelowNormal
        };
        _writingThread.Start();
    }

    private void LateUpdate()
    {
        if (needleTransform == null || pluginForce == null) return;

        Vector3 pos = needleTransform.position;
        Quaternion rot = needleTransform.rotation;
        Vector3 euler = rot.eulerAngles;
        Vector3 force = GetCurrentNeedleForce();
        float dist = (targetTransform != null) ? Vector3.Distance(pos, targetTransform.position) : 0f;

        bool penetrating = pluginForce.isPenetrating;
        bool insideBox = pluginForce.isInsideBoundingBox;
        bool duraPunctured = pluginForce.duralPunctureOccurred;

        lock (_snapshotLock)
        {
            _latestSnapshot.position = pos;
            _latestSnapshot.rotation = rot;
            _latestSnapshot.eulerAngles = euler;
            _latestSnapshot.force = force;
            _latestSnapshot.targetDistance = dist;
            _latestSnapshot.isPenetrating = penetrating;
            _latestSnapshot.isInsideBoundingBox = insideBox;
            _latestSnapshot.duralPunctureOccurred = duraPunctured;
        }
    }

    private void SamplingWorkerLoop()
    {
        long ticksPerMillisecond = Stopwatch.Frequency / 1000;
        long sampleIntervalTicks = sampleIntervalMs * ticksPerMillisecond;

        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        long nextSampleTick = stopwatch.ElapsedTicks;
        long attemptStartTick = 0;
        long lastSampleTick = 0;

        bool isRecording = false;
        bool hasFirstSample = false;

        Vector3 lastPos = Vector3.zero;
        Vector3 lastVel = Vector3.zero;
        Vector3 lastAcc = Vector3.zero;

        while (_threadsActive)
        {
            long currentTicks = stopwatch.ElapsedTicks;

            if (currentTicks < nextSampleTick)
            {
                long ticksRemaining = nextSampleTick - currentTicks;
                if (ticksRemaining > (2 * ticksPerMillisecond))
                {
                    Thread.Sleep(1);
                }
                else
                {
                    Thread.SpinWait(10);
                }
                continue;
            }

            nextSampleTick += sampleIntervalTicks;

            if (currentTicks - nextSampleTick > sampleIntervalTicks)
            {
                nextSampleTick = currentTicks + sampleIntervalTicks;
            }

            NeedleStateSnapshot state;
            lock (_snapshotLock)
            {
                state = _latestSnapshot;
            }

            bool shouldRecord = state.isPenetrating && state.isInsideBoundingBox && !state.duralPunctureOccurred;
            bool duraReached = state.duralPunctureOccurred;
            bool withdrawn = !state.isPenetrating && !state.isInsideBoundingBox;

            // --- State Transitions ---
            if (shouldRecord && !isRecording)
            {
                _attemptCounter++;
                isRecording = true;
                hasFirstSample = false;
                attemptStartTick = currentTicks;
                lastSampleTick = currentTicks;

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"{filePrefix}_Attempt_{_attemptCounter:D2}_{timestamp}_{sampleIntervalMs}ms.csv";
                string filePath = Path.Combine(_directoryPath, fileName);

                _logQueue.Enqueue(FILE_SWITCH_PREFIX + filePath);
                _logQueue.Enqueue(CSV_HEADER);
            }
            else if (isRecording && (duraReached || withdrawn))
            {
                if (duraReached)
                {
                    EmitSample(ref state, currentTicks, attemptStartTick, lastSampleTick,
                               hasFirstSample, ref lastPos, ref lastVel, ref lastAcc, isDura: 1);
                }

                isRecording = false;
                hasFirstSample = false;
                continue;
            }

            // --- Active Sampling & Numerical Derivatives ---
            if (isRecording)
            {
                EmitSample(ref state, currentTicks, attemptStartTick, lastSampleTick,
                           hasFirstSample, ref lastPos, ref lastVel, ref lastAcc, isDura: 0);

                lastSampleTick = currentTicks;
                hasFirstSample = true;
            }
        }
    }

    private void EmitSample(
        ref NeedleStateSnapshot state,
        long currentTicks,
        long attemptStartTick,
        long lastSampleTick,
        bool hasFirstSample,
        ref Vector3 lastPos,
        ref Vector3 lastVel,
        ref Vector3 lastAcc,
        int isDura)
    {
        Vector3 vel = Vector3.zero;
        Vector3 acc = Vector3.zero;
        Vector3 jerk = Vector3.zero;

        // Compute ground-truth dt from elapsed stopwatch ticks
        float dt = 0f;
        if (hasFirstSample)
        {
            dt = (float)(currentTicks - lastSampleTick) / Stopwatch.Frequency;
            if (dt > 0.00001f)
            {
                // 1st Derivative: Velocity (m/s)
                vel = (state.position - lastPos) / dt;

                // 2nd Derivative: Acceleration (m/s²)
                acc = (vel - lastVel) / dt;

                // 3rd Derivative: Jerk (m/s³)
                jerk = (acc - lastAcc) / dt;
            }
        }

        lastPos = state.position;
        lastVel = vel;
        lastAcc = acc;

        float elapsedTime = (float)(currentTicks - attemptStartTick) / Stopwatch.Frequency;

        string csvLine = string.Format(
            CultureInfo.InvariantCulture,
            "{0:F5},{1:F6}," +                                // Timestamp_s, DeltaTime_s
            "{2:F5},{3:F5},{4:F5}," +                         // Pos X, Y, Z
            "{5:F3},{6:F3},{7:F3}," +                         // Rot Euler X, Y, Z
            "{8:F4},{9:F4},{10:F4},{11:F4}," +                // Quat X, Y, Z, W
            "{12:F5},{13:F5},{14:F5},{15:F5}," +              // Vel X, Y, Z, Speed
            "{16:F5},{17:F5},{18:F5},{19:F5}," +              // Acc X, Y, Z, AccMag
            "{20:F5},{21:F5},{22:F5},{23:F5}," +              // Jerk X, Y, Z, JerkMag
            "{24:F4},{25:F4},{26:F4},{27:F4}," +              // Force X, Y, Z, ForceMag
            "{28:F5}," +                                      // Target Distance
            "{29}",                                           // IsDuraReached (0 or 1)
            elapsedTime, dt,
            state.position.x, state.position.y, state.position.z,
            state.eulerAngles.x, state.eulerAngles.y, state.eulerAngles.z,
            state.rotation.x, state.rotation.y, state.rotation.z, state.rotation.w,
            vel.x, vel.y, vel.z, vel.magnitude,
            acc.x, acc.y, acc.z, acc.magnitude,
            jerk.x, jerk.y, jerk.z, jerk.magnitude,
            state.force.x, state.force.y, state.force.z, state.force.magnitude,
            state.targetDistance,
            isDura
        );

        _logQueue.Enqueue(csvLine);
    }

    private Vector3 GetCurrentNeedleForce()
    {
        if (pluginForce == null) return Vector3.zero;

        try
        {            
            return new Vector3(0, 0, pluginForce.appliedForceN);
        }
        catch
        {
            return Vector3.zero;
        }
    }

    private void FileWritingWorkerLoop()
    {
        StreamWriter currentWriter = null;

        try
        {
            while (_threadsActive || !_logQueue.IsEmpty)
            {
                while (_logQueue.TryDequeue(out string line))
                {
                    if (line.StartsWith(FILE_SWITCH_PREFIX))
                    {
                        if (currentWriter != null)
                        {
                            currentWriter.Flush();
                            currentWriter.Dispose();
                            currentWriter = null;
                        }

                        string targetPath = line.Substring(FILE_SWITCH_PREFIX.Length);
                        currentWriter = new StreamWriter(targetPath, append: false);
                    }
                    else if (currentWriter != null)
                    {
                        currentWriter.WriteLine(line);
                    }
                }

                if (currentWriter != null)
                {
                    currentWriter.Flush();
                }

                Thread.Sleep(15);
            }
        }
        finally
        {
            if (currentWriter != null)
            {
                currentWriter.Flush();
                currentWriter.Dispose();
            }
        }
    }

    private void OnDestroy()
    {
        ShutdownThreads();
    }

    private void OnApplicationQuit()
    {
        ShutdownThreads();
    }

    private void ShutdownThreads()
    {
        _threadsActive = false;

        if (_samplingThread != null && _samplingThread.IsAlive)
        {
            _samplingThread.Join(1000);
        }

        if (_writingThread != null && _writingThread.IsAlive)
        {
            _writingThread.Join(2000);
        }
    }
}
// Force:
// return new Vector3(0, 0, pluginForce.appliedForceN);