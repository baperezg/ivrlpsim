using System;
using System.Collections.Generic;
using UnityEngine;

public class VoxelDirectForce : MonoBehaviour
{
    public enum LumbarLayer
    {
        Outside = 0,
        Skin = 1,
        Fat = 2,
        SupraspinousLigamentMS = 3,
        InterspinousLigamentIL = 4,
        LigamentumFlavumLF = 5,
        EpiduralSpaceES = 6,
        DuraMater = 7,
        SubarachnoidSpace = 8,
        BoneStrike = 9
    }

    [Header("Collision & Voxel Source")]
    [Tooltip("Reference to the AnatomyVoxelCollider script on the spine model.")]
    public AnatomyVoxelCollider voxelCollider;

    [Tooltip("Reference to the VoxelMonteCarlo planner for trajectory comparison.")]
    public VoxelMonteCarlo voxelMonteCarlo;

    [Header("Tracking Transforms")]
    [Tooltip("The 2D circle / entry aperture transform (its forward vector defines entry normal).")]
    public Transform entryCircleTransform;

    [Tooltip("The needle tip / stylus proxy tracked by the haptic device.")]
    public Transform needleTip;

    [Tooltip("Visual representation of the needle mesh.")]
    public Transform needleMesh;

    [Header("Puncture Activation Tolerances")]
    [Tooltip("Maximum entry approach distance in mm along normal to start puncture.")]
    public float surfaceEntryProximityMm = 5.0f;

    [Tooltip("Maximum allowable radial deviation in mm from aperture center to start puncture.")]
    public float maxEntryApertureRadiusMm = 15.0f;

    [Tooltip("Ray-march step along normal to sample voxel thicknesses (in mm).")]
    public float voxelScanStepMm = 0.5f;

    [Tooltip("Maximum trajectory forward scan distance in mm.")]
    public float maxScanDistanceMm = 120.0f;

    [Header("Device Hardware Limit")]
    [Tooltip("Maximum continuous force output for Touch/Omni (3.3 N).")]
    public float maxDeviceForceN = 3.3f;

    [Header("1D Constraint Virtual Fixture (Channel)")]
    [Range(50f, 600f)] public float channelStiffnessK = 50.0f;
    [Range(0f, 15f)] public float channelDampingB = 4.0f;

    [Header("Stabilization & Inward Damping")]
    [Range(0.0f, 25.0f)] public float insertionDamping = 4.0f;
    [Range(0.2f, 2.0f)] public float boundaryBlendWidthMm = 1.0f;

    [Header("Force Scaling Modifiers")]
    public float deviceForceScaling = (1.8f / 3.6767f) * 0.65f;
    public float scalingCO = 1.0f;

    [Header("Dynamically Measured Cumulative Depths (mm)")]
    public float thickSkin = 13.92f;
    public float thickFat = 17.15f;
    public float thickMS_Before = 19.37f;
    public float thickMS_After = 20.0f;
    public float thickIL_Before = 23.18f;
    public float thickIL_After = 41.18f;
    public float thickLF_Before = 44.79f;
    public float thickLF_After = 48.38f;
    public float thickES = 56.98f;
    public float thickDura = 60.0f;

    [Header("Obstruction & Resistive Forces")]
    public float duraPunctureForceN = 3.0f;
    public float boneStrikeForceN = 3.0f;

    [Header("Runtime Telemetry (Read-Only)")]
    [SerializeField] private LumbarLayer currentLayer = LumbarLayer.Outside;
    [SerializeField] private float depthMillimeters = 0.0f;
    [SerializeField] private float lateralOffsetMm = 0.0f;
    [SerializeField] public float appliedForceN = 0.0f;
    [SerializeField] public bool isPenetrating = false;
    [SerializeField] public bool duralPunctureOccurred = false;
    [SerializeField] public bool trajectoryTerminatedByObstruction = false;
    [SerializeField] public float obstructionDepthMm = 0.0f;

    [Header("Trajectory Analytics (Read-Only)")]
    [SerializeField] private Vector3 recordedEntryPoint;
    [SerializeField] private Vector3 recordedDirection;
    [SerializeField] private float dotWithBestTrajectory;
    [SerializeField] private float angleWithBestTrajectoryDeg;
    [SerializeField] private float angleWithAxialPlaneDeg;     // Horizontal (XZ)
    [SerializeField] private float angleWithSagittalPlaneDeg;  // Vertical (YZ)

    public LumbarLayer CurrentLayer { get => currentLayer; set => currentLayer = value; }
    public float DepthMillimeters => depthMillimeters;
    public float LateralOffsetMm => lateralOffsetMm;

    private HapticPlugin hapticPlugin;
    private bool forceActive = false;
    private Quaternion entryOrientation;

    // State Machine
    private ITissueState currentState;
    private readonly Dictionary<LumbarLayer, ITissueState> states = new Dictionary<LumbarLayer, ITissueState>();

    private void Awake()
    {
        InitializeStateMachine();
    }

    private void Start()
    {
        hapticPlugin = UnityEngine.Object.FindAnyObjectByType<HapticPlugin>();
        if (hapticPlugin == null)
            Debug.LogWarning("[VoxelDirectForce] No HapticPlugin found in scene.");

        if (voxelCollider == null)
            voxelCollider = UnityEngine.Object.FindAnyObjectByType<AnatomyVoxelCollider>();

        if (voxelMonteCarlo == null)
            voxelMonteCarlo = UnityEngine.Object.FindAnyObjectByType<VoxelMonteCarlo>();
    }

    private void InitializeStateMachine()
    {
        states[LumbarLayer.Outside] = new OutsideState();
        states[LumbarLayer.BoneStrike] = new BoneStrikeState();
        states[LumbarLayer.Skin] = new SkinState();
        states[LumbarLayer.Fat] = new FatState();
        states[LumbarLayer.SupraspinousLigamentMS] = new SupraspinousLigamentState();
        states[LumbarLayer.InterspinousLigamentIL] = new InterspinousLigamentState();
        states[LumbarLayer.LigamentumFlavumLF] = new LigamentumFlavumState();
        states[LumbarLayer.EpiduralSpaceES] = new EpiduralSpaceState();
        states[LumbarLayer.DuraMater] = new DuraMaterState();
        states[LumbarLayer.SubarachnoidSpace] = new SubarachnoidSpaceState();

        currentState = states[LumbarLayer.Outside];
        currentState.Enter(this);
    }

    public void TransitionTo(LumbarLayer nextLayer)
    {
        if (currentState != null && currentState.Layer == nextLayer) return;

        currentState?.Exit(this);
        currentState = states[nextLayer];
        currentLayer = nextLayer;
        currentState.Enter(this);
    }

    #region State Implementations
    public interface ITissueState
    {
        LumbarLayer Layer { get; }
        void Enter(VoxelDirectForce ctx);
        float CalculateForce(VoxelDirectForce ctx);
        void Exit(VoxelDirectForce ctx);
    }

    private class OutsideState : ITissueState
    {
        public LumbarLayer Layer => LumbarLayer.Outside;
        public void Enter(VoxelDirectForce ctx) { }
        public float CalculateForce(VoxelDirectForce ctx) => 0.0f;
        public void Exit(VoxelDirectForce ctx) { }
    }

    private class BoneStrikeState : ITissueState
    {
        public LumbarLayer Layer => LumbarLayer.BoneStrike;
        public void Enter(VoxelDirectForce ctx) { }
        public float CalculateForce(VoxelDirectForce ctx) => ctx.boneStrikeForceN;
        public void Exit(VoxelDirectForce ctx) { }
    }

    private class SkinState : ITissueState
    {
        public LumbarLayer Layer => LumbarLayer.Skin;
        public void Enter(VoxelDirectForce ctx) { ctx.duralPunctureOccurred = false; }
        public float CalculateForce(VoxelDirectForce ctx)
        {
            float xFixed = ctx.DepthMillimeters;
            float force = (0.0235f + 0.0116f * (xFixed / ctx.scalingCO)
                          - 0.0046f * Mathf.Pow(xFixed / ctx.scalingCO, 2)
                          + 0.0025f * Mathf.Pow(xFixed / ctx.scalingCO, 3)) * ctx.deviceForceScaling;
            return Mathf.Max(0.0f, force);
        }
        public void Exit(VoxelDirectForce ctx) { }
    }

    private class FatState : ITissueState
    {
        public LumbarLayer Layer => LumbarLayer.Fat;
        public void Enter(VoxelDirectForce ctx) { }
        public float CalculateForce(VoxelDirectForce ctx)
        {
            float xFixed = ctx.DepthMillimeters - ctx.thickSkin;
            float rawFatForce = (6.0372f + 0.4516f * (xFixed / ctx.scalingCO)
                                - 0.5287f * Mathf.Pow(xFixed / ctx.scalingCO, 2)) * ctx.deviceForceScaling;

            float skinExitForce = (0.0235f + 0.0116f * (ctx.thickSkin / ctx.scalingCO)
                                  - 0.0046f * Mathf.Pow(ctx.thickSkin / ctx.scalingCO, 2)
                                  + 0.0025f * Mathf.Pow(ctx.thickSkin / ctx.scalingCO, 3)) * ctx.deviceForceScaling;

            float t = Mathf.Clamp01(xFixed / Mathf.Max(0.001f, ctx.boundaryBlendWidthMm));
            float force = Mathf.Lerp(skinExitForce, rawFatForce, Mathf.SmoothStep(0.0f, 1.0f, t));
            return Mathf.Max(0.0f, force);
        }
        public void Exit(VoxelDirectForce ctx) { }
    }

    private class SupraspinousLigamentState : ITissueState
    {
        public LumbarLayer Layer => LumbarLayer.SupraspinousLigamentMS;
        public void Enter(VoxelDirectForce ctx) { }
        public float CalculateForce(VoxelDirectForce ctx)
        {
            float depth = ctx.DepthMillimeters;
            float force;
            if (depth <= ctx.thickMS_Before)
            {
                float xFixed = depth - ctx.thickFat;
                force = (1.9736f + 0.8287f * (xFixed / ctx.scalingCO)
                        + 0.1078f * Mathf.Pow(xFixed / ctx.scalingCO, 2)) * ctx.deviceForceScaling;
            }
            else
            {
                float xFixed = depth - ctx.thickMS_Before;
                force = (4.354f - 2.2543f * (xFixed / ctx.scalingCO)
                        + 0.2902f * Mathf.Pow(xFixed / ctx.scalingCO, 2)) * ctx.deviceForceScaling;
            }
            return Mathf.Max(0.0f, force);
        }
        public void Exit(VoxelDirectForce ctx) { }
    }

    private class InterspinousLigamentState : ITissueState
    {
        public LumbarLayer Layer => LumbarLayer.InterspinousLigamentIL;
        public void Enter(VoxelDirectForce ctx) { }
        public float CalculateForce(VoxelDirectForce ctx)
        {
            float depth = ctx.DepthMillimeters;
            float force;
            if (depth <= ctx.thickIL_Before)
            {
                float xFixed = depth - ctx.thickMS_After;
                force = (4.4062f + 0.9598f * (xFixed / ctx.scalingCO)) * ctx.deviceForceScaling;
            }
            else
            {
                force = 7.467f * ctx.deviceForceScaling;
            }
            return Mathf.Max(0.0f, force);
        }
        public void Exit(VoxelDirectForce ctx) { }
    }

    private class LigamentumFlavumState : ITissueState
    {
        public LumbarLayer Layer => LumbarLayer.LigamentumFlavumLF;
        public void Enter(VoxelDirectForce ctx) { }
        public float CalculateForce(VoxelDirectForce ctx)
        {
            float depth = ctx.DepthMillimeters;
            float force;
            if (depth <= ctx.thickLF_Before)
            {
                float xFixed = depth - ctx.thickIL_After;
                force = (7.467f + 1.5029f * (xFixed / ctx.scalingCO)
                        - 0.0583f * Mathf.Pow(xFixed / ctx.scalingCO, 2)) * ctx.deviceForceScaling;
            }
            else
            {
                float xFixed = depth - ctx.thickLF_Before;
                force = (12.133f - 0.1693f * (xFixed / ctx.scalingCO)
                        - 0.1177f * Mathf.Pow(xFixed / ctx.scalingCO, 2)) * ctx.deviceForceScaling;
            }
            return Mathf.Max(0.0f, force);
        }
        public void Exit(VoxelDirectForce ctx) { }
    }

    private class EpiduralSpaceState : ITissueState
    {
        public LumbarLayer Layer => LumbarLayer.EpiduralSpaceES;
        public void Enter(VoxelDirectForce ctx) { }
        public float CalculateForce(VoxelDirectForce ctx) => 0.0f;
        public void Exit(VoxelDirectForce ctx) { }
    }

    private class DuraMaterState : ITissueState
    {
        public LumbarLayer Layer => LumbarLayer.DuraMater;
        public void Enter(VoxelDirectForce ctx) { }
        public float CalculateForce(VoxelDirectForce ctx)
        {
            float xFixed = ctx.DepthMillimeters - ctx.thickES;
            float blendRange = Mathf.Max(0.001f, Mathf.Min(ctx.boundaryBlendWidthMm, ctx.thickDura - ctx.thickES));
            float t = Mathf.Clamp01(xFixed / blendRange);
            float force = Mathf.Lerp(0.0f, ctx.duraPunctureForceN, Mathf.SmoothStep(0.0f, 1.0f, t));
            return Mathf.Max(0.0f, force);
        }
        public void Exit(VoxelDirectForce ctx) { }
    }

    private class SubarachnoidSpaceState : ITissueState
    {
        public LumbarLayer Layer => LumbarLayer.SubarachnoidSpace;
        public void Enter(VoxelDirectForce ctx) { ctx.duralPunctureOccurred = true; }
        public float CalculateForce(VoxelDirectForce ctx) => 0.0f;
        public void Exit(VoxelDirectForce ctx) { }
    }
    #endregion

    private void FixedUpdate()
    {
        if (entryCircleTransform == null || needleTip == null || hapticPlugin == null)
        {
            ResetPluginForce();
            return;
        }

        Vector3 displacement = needleTip.position - entryCircleTransform.position;
        Vector3 punctureAxis = entryCircleTransform.forward.normalized;

        float depthMeters = Vector3.Dot(displacement, punctureAxis);
        depthMillimeters = depthMeters * 1000.0f;

        Vector3 targetPointOnAxis = entryCircleTransform.position + (punctureAxis * depthMeters);
        Vector3 lateralDeviation = needleTip.position - targetPointOnAxis;
        lateralOffsetMm = lateralDeviation.magnitude * 1000.0f;

        // Puncture initiation
        if (!isPenetrating)
        {
            bool withinApproachDistance = depthMillimeters > 0.0f && depthMillimeters <= surfaceEntryProximityMm;
            bool withinApertureRadius = lateralOffsetMm <= maxEntryApertureRadiusMm;

            if (withinApproachDistance && withinApertureRadius)
            {
                isPenetrating = true;
                entryOrientation = (needleMesh != null) ? needleMesh.rotation : entryCircleTransform.rotation;

                // 1. Trace voxel column to extract real anatomical layer depths
                ExtractVoxelLayerDistances(entryCircleTransform.position, punctureAxis);

                // 2. Record trajectory and compute analytics against best trajectory and planes
                RecordAndAnalyzeTrajectory(entryCircleTransform.position, punctureAxis);
            }
            else
            {
                ResetPluginForce();
                return;
            }
        }

        // Active Puncture Processing
        if (depthMillimeters > 0.0f)
        {
            if (needleMesh != null)
            {
                needleMesh.rotation = entryOrientation;
            }

            LumbarLayer targetLayer = ResolveTargetLayer(depthMillimeters);
            TransitionTo(targetLayer);

            float layerForceN = currentState.CalculateForce(this);

            Vector3 worldVelocity = hapticPlugin.transform.TransformDirection(hapticPlugin.CurrentVelocity * 0.001f);
            float axialVelocity = Vector3.Dot(worldVelocity, punctureAxis);
            Vector3 lateralVelocity = worldVelocity - (punctureAxis * axialVelocity);

            float axialDampingForce = (axialVelocity > 0.0f) ? (insertionDamping * axialVelocity) : 0.0f;
            Vector3 axialForceVector = -punctureAxis * (layerForceN + axialDampingForce);

            Vector3 lateralRestoringForce = (-channelStiffnessK * lateralDeviation) - (channelDampingB * lateralVelocity);

            Vector3 totalForceVector = axialForceVector + lateralRestoringForce;
            float totalForceMag = totalForceVector.magnitude;

            appliedForceN = Mathf.Clamp(totalForceMag, 0.0f, maxDeviceForceN);

            Vector3 forceDirection = (totalForceMag > 0.0001f) ? (totalForceVector / totalForceMag) : Vector3.zero;
            Vector3 deviceLocalDir = hapticPlugin.transform.InverseTransformDirection(forceDirection).normalized;

            hapticPlugin.ConstForceGDir = deviceLocalDir;
            hapticPlugin.ConstForceGMag = appliedForceN / maxDeviceForceN;

            if (!forceActive)
            {
                hapticPlugin.EnableConstantForce();
                forceActive = true;
            }
        }
        else
        {
            isPenetrating = false;
            ResetPluginForce();
        }
    }

    /// <summary>
    /// Ray-marches through the voxel volume from the skin surface in the normal direction to establish layer depths.
    /// </summary>
    private void ExtractVoxelLayerDistances(Vector3 originWorld, Vector3 directionNorm)
    {
        if (voxelCollider == null) return;

        float stepMeters = voxelScanStepMm * 0.001f;
        int maxSteps = Mathf.CeilToInt((maxScanDistanceMm * 0.001f) / stepMeters);

        float skinLen = 0f;
        float fatLen = 0f;
        float msLen = 0f;
        float ilLen = 0f;
        float lfLen = 0f;
        float esLen = 0f;
        float duraLen = 0f;

        trajectoryTerminatedByObstruction = false;
        obstructionDepthMm = 0.0f;

        bool hasFoundAnatomy = false;

        for (int step = 0; step < maxSteps; step++)
        {
            float currentDistMeters = step * stepMeters;
            float currentDistMm = currentDistMeters * 1000.0f;
            Vector3 samplePoint = originWorld + (directionNorm * currentDistMeters);

            var query = voxelCollider.CheckSphereCollision(samplePoint, 0.1f);
            var tissue = query.centerTissue;

            if (!hasFoundAnatomy)
            {
                if (tissue == AnatomyVoxelCollider.TissueType.Air) continue;
                hasFoundAnatomy = true;
            }

            // Path obstruction check: bone or muscle
            if (tissue == AnatomyVoxelCollider.TissueType.Bone || tissue == AnatomyVoxelCollider.TissueType.Muscle)
            {
                trajectoryTerminatedByObstruction = true;
                obstructionDepthMm = currentDistMm;
                break;
            }

            switch (tissue)
            {
                case AnatomyVoxelCollider.TissueType.Skin:
                    skinLen += voxelScanStepMm;
                    break;
                case AnatomyVoxelCollider.TissueType.Fat:
                case AnatomyVoxelCollider.TissueType.SoftTissue:
                    fatLen += voxelScanStepMm;
                    break;
                case AnatomyVoxelCollider.TissueType.SupraspinousLigament:
                    msLen += voxelScanStepMm;
                    break;
                case AnatomyVoxelCollider.TissueType.OtherLigaments:
                    ilLen += voxelScanStepMm;
                    break;
                case AnatomyVoxelCollider.TissueType.LigamentumFlavum:
                    lfLen += voxelScanStepMm;
                    break;
                case AnatomyVoxelCollider.TissueType.EpiduralSpace:
                    esLen += voxelScanStepMm;
                    break;
                case AnatomyVoxelCollider.TissueType.DuralSac:
                    duraLen += voxelScanStepMm;
                    break;
            }

            if (duraLen > 3.0f) // Captured sufficient dural penetration
                break;
        }

        // Fallbacks for zero-thickness segments
        if (skinLen < 1.0f) skinLen = 13.92f;
        if (fatLen < 1.0f) fatLen = 3.23f;
        if (msLen < 0.5f) msLen = 2.85f;
        if (ilLen < 1.0f) ilLen = 21.18f;
        if (lfLen < 0.5f) lfLen = 7.20f;
        if (esLen < 0.5f) esLen = 8.60f;
        if (duraLen < 0.5f) duraLen = 3.02f;

        // Original script proportional ratios:
        // MS: (19.37 - 17.15) / (20.0 - 17.15) = 2.22 / 2.85 = 0.7789
        // IL: (23.18 - 20.0) / (41.18 - 20.0) = 3.18 / 21.18 = 0.1501
        // LF: (44.79 - 41.18) / (48.38 - 41.18) = 3.61 / 7.20 = 0.5014
        const float ratioMS = 0.7789f;
        const float ratioIL = 0.1501f;
        const float ratioLF = 0.5014f;

        thickSkin = skinLen;
        thickFat = thickSkin + fatLen;

        thickMS_Before = thickFat + (msLen * ratioMS);
        thickMS_After = thickFat + msLen;

        thickIL_Before = thickMS_After + (ilLen * ratioIL);
        thickIL_After = thickMS_After + ilLen;

        thickLF_Before = thickIL_After + (lfLen * ratioLF);
        thickLF_After = thickIL_After + lfLen;

        thickES = thickLF_After + esLen;
        thickDura = thickES + duraLen;
    }

    private LumbarLayer ResolveTargetLayer(float depth)
    {
        if (trajectoryTerminatedByObstruction && depth >= obstructionDepthMm)
        {
            return LumbarLayer.BoneStrike;
        }

        if (depth <= thickSkin) return LumbarLayer.Skin;
        if (depth <= thickFat) return LumbarLayer.Fat;
        if (depth <= thickMS_After) return LumbarLayer.SupraspinousLigamentMS;
        if (depth <= thickIL_After) return LumbarLayer.InterspinousLigamentIL;
        if (depth <= thickLF_After) return LumbarLayer.LigamentumFlavumLF;
        if (depth <= thickES) return LumbarLayer.EpiduralSpaceES;
        if (depth <= thickDura) return LumbarLayer.DuraMater;

        return LumbarLayer.SubarachnoidSpace;
    }

    private void RecordAndAnalyzeTrajectory(Vector3 entryPoint, Vector3 directionNorm)
    {
        recordedEntryPoint = entryPoint;
        recordedDirection = directionNorm;

        // 1. Comparison with VoxelMonteCarlo Best Trajectory
        if (voxelMonteCarlo != null && voxelMonteCarlo.BestCenterTrajectory.start != Vector3.zero)
        {
            var bestTraj = voxelMonteCarlo.BestCenterTrajectory;
            Vector3 bestDir = (bestTraj.end - bestTraj.start).normalized;

            dotWithBestTrajectory = Vector3.Dot(recordedDirection, bestDir);
            angleWithBestTrajectoryDeg = Vector3.Angle(recordedDirection, bestDir);
        }
        else
        {
            dotWithBestTrajectory = 1.0f;
            angleWithBestTrajectoryDeg = 0.0f;
        }

        // In native project space:
        // +X: Left / Right
        // +Y: Head (Superior) / Feet (Inferior)
        // +Z: Abdomen (Anterior) / Back (Posterior)

        // 2. Angle with Axial / Horizontal Plane (XZ plane, normal is +Y)
        // Angle between vector and plane = 90 - Angle(vector, planeNormal)
        float angleToY = Vector3.Angle(recordedDirection, Vector3.up);
        angleWithAxialPlaneDeg = Mathf.Abs(90.0f - angleToY);

        // 3. Angle with Sagittal / Vertical Plane (YZ plane, normal is +X)
        float angleToX = Vector3.Angle(recordedDirection, Vector3.right);
        angleWithSagittalPlaneDeg = Mathf.Abs(90.0f - angleToX);
    }

    private void ResetPluginForce()
    {
        currentLayer = LumbarLayer.Outside;
        appliedForceN = 0.0f;

        if (hapticPlugin != null && forceActive)
        {
            hapticPlugin.ConstForceGMag = 0.0f;
            hapticPlugin.DisableContantForce();
            forceActive = false;
        }
    }

    private void OnDisable()
    {
        ResetPluginForce();
    }

    private void OnDrawGizmosSelected()
    {
        if (entryCircleTransform != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(entryCircleTransform.position, maxEntryApertureRadiusMm * 0.001f);

            Gizmos.color = Color.blue;
            Gizmos.DrawRay(entryCircleTransform.position, entryCircleTransform.forward * (thickDura * 0.001f));

            if (trajectoryTerminatedByObstruction)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(entryCircleTransform.position + entryCircleTransform.forward * (obstructionDepthMm * 0.001f), 0.003f);
            }
        }
    }
}