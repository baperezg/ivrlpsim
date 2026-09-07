using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using UnityEngine;

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
    [Tooltip("Sampling interval in milliseconds (e.g., 10 for 100Hz, 20 for 50Hz)")]
    [Range(5, 500)]
    [SerializeField] private int sampleIntervalMs = 10;

    [Header("Output Settings")]
    [SerializeField] private string filePrefix = "LP_Kinematics";
    [SerializeField] private string outputSubfolder = "Recordings";

    // Attempt and State tracking
    private bool _isRecording = false;
    private int _attemptCounter = 0;
    private float _timeAccumulator = 0f;
    private float _sampleIntervalSec;
    private float _attemptStartTime;

    // Kinematics state registers
    private Vector3 _lastPos;
    private Vector3 _lastVel;
    private Vector3 _lastAcc;
    private float _lastSampleTime;
    private bool _hasFirstSample = false;

    // Multi-threaded file writer primitives
    private readonly ConcurrentQueue<string> _logQueue = new ConcurrentQueue<string>();
    private Thread _writeThread;
    private volatile bool _isRunning = false;
    private string _directoryPath;

    // Reflection cache for force retrieval if standard properties are named differently
    private FieldInfo _vector3ForceField;
    private PropertyInfo _vector3ForceProp;
    private FieldInfo _floatForceField;
    private PropertyInfo _floatForceProp;

    private const string FILE_SWITCH_PREFIX = "__SWITCH_FILE__:";
    private const string CSV_HEADER = "Timestamp_s," +
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

        CacheForceMembers();
    }

    private void Start()
    {
        _sampleIntervalSec = sampleIntervalMs / 1000f;

        _directoryPath = Path.Combine(Application.dataPath, "..", outputSubfolder);
        if (!Directory.Exists(_directoryPath))
        {
            Directory.CreateDirectory(_directoryPath);
        }

        // Spin up background logging thread once for the entire session
        _isRunning = true;
        _writeThread = new Thread(ProcessFileQueue)
        {
            IsBackground = true,
            Priority = System.Threading.ThreadPriority.BelowNormal
        };
        _writeThread.Start();
    }

    private void Update()
    {
        if (pluginForce == null || needleTransform == null) return;

        // Condition to start: penetration within the bounding box and dura not yet pierced
        bool shouldStart = pluginForce.isPenetrating &&
                           pluginForce.isInsideBoundingBox &&
                           !pluginForce.duralPunctureOccurred;

        // Condition 1 to stop: dura mater reached (Success)
        bool duraReached = pluginForce.duralPunctureOccurred;

        // Condition 2 to stop: needle withdrawn outside target bounds (Aborted/Incomplete attempt)
        bool needleWithdrawn = !pluginForce.isPenetrating && !pluginForce.isInsideBoundingBox;

        // State Transitions
        if (shouldStart && !_isRecording)
        {
            StartNewAttempt();
        }
        else if (_isRecording && (duraReached || needleWithdrawn))
        {
            // If the dura was reached, capture the final terminal data point
            if (duraReached)
            {
                SampleData(isDuraTerminalSample: true);
                Debug.Log($"[KinematicsRecorder] DURAL PUNCTURE DETECTED! Target reached. Finalized Attempt {_attemptCounter}.");
            }
            else
            {
                Debug.Log($"[KinematicsRecorder] Needle withdrawn before reaching dura. Finalized Attempt {_attemptCounter}.");
            }

            StopAttempt();
        }

        // Periodic sampling loop during an active attempt
        if (_isRecording)
        {
            _timeAccumulator += Time.unscaledDeltaTime;

            while (_timeAccumulator >= _sampleIntervalSec)
            {
                SampleData(isDuraTerminalSample: false);
                _timeAccumulator -= _sampleIntervalSec;
            }
        }
    }

    private void StartNewAttempt()
    {
        _attemptCounter++;
        _isRecording = true;
        _hasFirstSample = false;
        _attemptStartTime = Time.unscaledTime;
        _timeAccumulator = 0f;

        // Build unique file path for this specific attempt
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"{filePrefix}_Attempt_{_attemptCounter:D2}_{timestamp}_{sampleIntervalMs}ms.csv";
        string filePath = Path.Combine(_directoryPath, fileName);

        // Instruct worker thread to switch to the new attempt file
        _logQueue.Enqueue(FILE_SWITCH_PREFIX + filePath);
        _logQueue.Enqueue(CSV_HEADER);

        Debug.Log($"[KinematicsRecorder] Started Attempt {_attemptCounter}. Output: {fileName}");
    }

    private void StopAttempt()
    {
        _isRecording = false;
    }

    private void SampleData(bool isDuraTerminalSample)
    {
        float currentTime = Time.unscaledTime;
        Vector3 currentPos = needleTransform.position;
        Quaternion currentRot = needleTransform.rotation;
        Vector3 currentEuler = currentRot.eulerAngles;

        Vector3 vel = Vector3.zero;
        Vector3 acc = Vector3.zero;
        Vector3 jerk = Vector3.zero;

        if (_hasFirstSample)
        {
            float dt = currentTime - _lastSampleTime;
            if (dt > 0.0001f)
            {
                // Numerical derivatives
                vel = (currentPos - _lastPos) / dt;
                acc = (vel - _lastVel) / dt;
                jerk = (acc - _lastAcc) / dt;
            }
        }
        else
        {
            _hasFirstSample = true;
        }

        // Update tracking registers
        _lastPos = currentPos;
        _lastVel = vel;
        _lastAcc = acc;
        _lastSampleTime = currentTime;

        // Force extraction
        Vector3 forceVector = GetCurrentNeedleForce();

        // Target distance
        float targetDistance = 0f;
        if (targetTransform != null)
        {
            targetDistance = Vector3.Distance(currentPos, targetTransform.position);
        }

        float elapsedTime = currentTime - _attemptStartTime;
        int duraFlagValue = isDuraTerminalSample ? 1 : 0;

        string csvLine = string.Format(
            CultureInfo.InvariantCulture,
            "{0:F4}," +                                       // 0: Timestamp
            "{1:F5},{2:F5},{3:F5}," +                         // 1-3: Pos X, Y, Z
            "{4:F3},{5:F3},{6:F3}," +                         // 4-6: Rot Euler X, Y, Z
            "{7:F4},{8:F4},{9:F4},{10:F4}," +                 // 7-10: Quat X, Y, Z, W
            "{11:F5},{12:F5},{13:F5},{14:F5}," +              // 11-14: Vel X, Y, Z, Speed
            "{15:F5},{16:F5},{17:F5},{18:F5}," +              // 15-18: Acc X, Y, Z, AccMag
            "{19:F5},{20:F5},{21:F5},{22:F5}," +              // 19-22: Jerk X, Y, Z, JerkMag
            "{23:F4},{24:F4},{25:F4},{26:F4}," +              // 23-26: Force X, Y, Z, ForceMag
            "{27:F5}," +                                      // 27: Target Distance
            "{28}",                                           // 28: IsDuraReached (0 or 1)
            elapsedTime,
            currentPos.x, currentPos.y, currentPos.z,
            currentEuler.x, currentEuler.y, currentEuler.z,
            currentRot.x, currentRot.y, currentRot.z, currentRot.w,
            vel.x, vel.y, vel.z, vel.magnitude,
            acc.x, acc.y, acc.z, acc.magnitude,
            jerk.x, jerk.y, jerk.z, jerk.magnitude,
            forceVector.x, forceVector.y, forceVector.z, forceVector.magnitude,
            targetDistance,
            duraFlagValue
        );

        _logQueue.Enqueue(csvLine);
    }

    private Vector3 GetCurrentNeedleForce()
    {
        if (pluginForce == null)
            return Vector3.zero;
        else
            return new Vector3(0, 0, pluginForce.appliedForceN);
        /*
        if (_vector3ForceField != null)
            return (Vector3)_vector3ForceField.GetValue(pluginForce);

        if (_vector3ForceProp != null)
            return (Vector3)_vector3ForceProp.GetValue(pluginForce);

        if (_floatForceField != null)
        {
            float f = (float)_floatForceField.GetValue(pluginForce);
            return needleTransform.forward * f;
        }

        if (_floatForceProp != null)
        {
            float f = (float)_floatForceProp.GetValue(pluginForce);
            return needleTransform.forward * f;
        }

        return Vector3.zero;
        */
    }

    private void CacheForceMembers()
    {
        if (pluginForce == null) return;

        Type type = pluginForce.GetType();
        BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        string[] vectorCandidates = { "currentForce", "appliedForce", "needleForce", "forceVector", "totalForce", "force" };
        string[] floatCandidates = { "forceMagnitude", "currentForceMag", "reactionForce", "penetrationForce" };

        foreach (string name in vectorCandidates)
        {
            FieldInfo f = type.GetField(name, flags);
            if (f != null && f.FieldType == typeof(Vector3)) { _vector3ForceField = f; return; }

            PropertyInfo p = type.GetProperty(name, flags);
            if (p != null && p.PropertyType == typeof(Vector3)) { _vector3ForceProp = p; return; }
        }

        foreach (string name in floatCandidates)
        {
            FieldInfo f = type.GetField(name, flags);
            if (f != null && (f.FieldType == typeof(float) || f.FieldType == typeof(double))) { _floatForceField = f; return; }

            PropertyInfo p = type.GetProperty(name, flags);
            if (p != null && (p.PropertyType == typeof(float) || p.PropertyType == typeof(double))) { _floatForceProp = p; return; }
        }
    }

    private void ProcessFileQueue()
    {
        StreamWriter currentWriter = null;

        try
        {
            while (_isRunning || !_logQueue.IsEmpty)
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

                Thread.Sleep(10);
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
        _isRunning = false;
        if (_writeThread != null && _writeThread.IsAlive)
            _writeThread.Join(2000);
    }

    private void OnApplicationQuit()
    {
        _isRunning = false;
        if (_writeThread != null && _writeThread.IsAlive)
            _writeThread.Join(2000);
    }
}