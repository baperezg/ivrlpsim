using System.Collections.Generic;
using UnityEngine;


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

public class LumbarPunctureDirectPluginForce : MonoBehaviour
{
    [Header("Bounding Volume (Activation Box)")]
    [Tooltip("BoxCollider defining the active physical volume for the tissue. MeshRenderer can be disabled.")]
    public BoxCollider targetBoundingBox;

    [Tooltip("Maximum entry approach distance from the surface aperture to initiate puncture (in mm).")]
    public float surfaceEntryProximityMm = 5.0f;

    [Tooltip("Maximum allowable entry aperture radius in mm from the circle center to allow puncture initiation.")]
    public float maxEntryApertureRadiusMm = 15.0f;

    [Header("Tracking Transforms")]
    [Tooltip("The 2D circle / entry aperture transform (its forward vector defines penetration direction).")]
    public Transform entryCircleTransform;

    [Tooltip("The needle tip / stylus proxy tracked by the haptic device.")]
    public Transform needleTip;

    [Tooltip("Visual representation of the needle mesh.")]
    public Transform needleMesh;

    [Header("Device Hardware Limit")]
    [Tooltip("Maximum continuous force output for 3D Systems Touch/Omni (3.3 N).")]
    public float maxDeviceForceN = 3.3f;

    [Header("1D Constraint Virtual Fixture (Channel)")]
    [Tooltip("Stiffness of the virtual channel in N/m opposing lateral deviation.")]
    [Range(50f, 600f)]
    public float channelStiffnessK = 50.0f;

    [Tooltip("Damping factor in N·s/m opposing lateral drift.")]
    [Range(0f, 15f)]
    public float channelDampingB = 4.0f;

    [Header("Stabilization & Inward Damping")]
    [Tooltip("Viscous damping factor (N·s/m) along puncture axis to cancel boundary flutter.")]
    [Range(0.0f, 25.0f)]
    public float insertionDamping = 4.0f;

    [Tooltip("Transition zone in mm to smooth step discontinuities at layer boundaries.")]
    [Range(0.2f, 2.0f)]
    public float boundaryBlendWidthMm = 1.0f;

    [Header("Patient & Scaling Modifiers")]
    [Tooltip("Hardware force scaling factor from the original simulator to avoid saturation.")]
    public float deviceForceScaling = (1.8f / 3.6767f) * 0.65f;

    [Tooltip("Patient-specific layer scaling coefficient.")]
    public float scalingCO = 1.0f;

    [Header("Cumulative Layer Depths (mm)")]
    public float thickSkin = 13.92f;
    public float thickFat = 17.15f;
    public float thickMS_Before = 19.37f;
    public float thickMS_After = 20.0f;
    public float thickIL_Before = 23.18f;
    public float thickIL_After = 41.18f;
    public float thickLF_Before = 44.79f;
    public float thickLF_After = 48.38f;
    public float thickES = 56.98f; // Epidural space end (Dura boundary start)
    [Tooltip("Thickness of the Dura Mater before yielding into the subarachnoid space (in mm).")]
    public float thickDura = 60.0f;

    [Header("Dura & Bone Force Settings")]
    [Tooltip("Peak puncture resistance force of the dura mater.")]
    public float duraPunctureForceN = 3.0f;

    [Header("Bone Collision Constraints")]
    [Tooltip("Radial deviation threshold in mm from center to trigger bone contact.")]
    public float boneRadiusLimitMm = 10.0f;
    [Tooltip("Resistive stiffness force when colliding with bone.")]
    public float boneStrikeForceN = 3.0f;

    [Header("Runtime Telemetry (Read-Only)")]
    [SerializeField] private LumbarLayer currentLayer = LumbarLayer.Outside;
    [SerializeField] private float depthMillimeters = 0.0f;
    [SerializeField] private float lateralOffsetMm = 0.0f;
    [SerializeField] public float appliedForceN = 0.0f;
    [SerializeField] public bool isPenetrating = false;
    [SerializeField] public bool isInsideBoundingBox = false;
    [SerializeField] public bool duralPunctureOccurred = false;

    private HapticPlugin hapticPlugin;
    private bool forceActive = false;
    private Quaternion entryOrientation;

    // Public getters/setters for encapsulation by State classes
    public LumbarLayer CurrentLayer { get => currentLayer; set => currentLayer = value; }
    public float DepthMillimeters => depthMillimeters;
    public float LateralOffsetMm => lateralOffsetMm;

    //=================== Start of State machine implementation ===================//

    // State Pattern Engine
    private ITissueState currentState;
    private readonly Dictionary<LumbarLayer, ITissueState> states = new Dictionary<LumbarLayer, ITissueState>();

    private void Awake()
    {
        InitializeStateMachine();
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

    #region State Pattern Architecture

    public interface ITissueState
    {
        LumbarLayer Layer { get; }
        void Enter(LumbarPunctureDirectPluginForce ctx);
        float CalculateForce(LumbarPunctureDirectPluginForce ctx);
        void Exit(LumbarPunctureDirectPluginForce ctx);
    }

    private class OutsideState : ITissueState
    {
        public LumbarLayer Layer => LumbarLayer.Outside;
        public void Enter(LumbarPunctureDirectPluginForce ctx) { }
        public float CalculateForce(LumbarPunctureDirectPluginForce ctx) => 0.0f;
        public void Exit(LumbarPunctureDirectPluginForce ctx) { }
    }

    private class BoneStrikeState : ITissueState
    {
        public LumbarLayer Layer => LumbarLayer.BoneStrike;
        public void Enter(LumbarPunctureDirectPluginForce ctx) { }
        public float CalculateForce(LumbarPunctureDirectPluginForce ctx) => ctx.boneStrikeForceN;
        public void Exit(LumbarPunctureDirectPluginForce ctx) { }
    }

    private class SkinState : ITissueState
    {
        public LumbarLayer Layer => LumbarLayer.Skin;
        public void Enter(LumbarPunctureDirectPluginForce ctx) { ctx.duralPunctureOccurred = false; }
        public float CalculateForce(LumbarPunctureDirectPluginForce ctx)
        {
            float xFixed = ctx.DepthMillimeters;
            float force = (0.0235f + 0.0116f * (xFixed / ctx.scalingCO)
                          - 0.0046f * Mathf.Pow(xFixed / ctx.scalingCO, 2)
                          + 0.0025f * Mathf.Pow(xFixed / ctx.scalingCO, 3)) * ctx.deviceForceScaling;
            return Mathf.Max(0.0f, force);
        }
        public void Exit(LumbarPunctureDirectPluginForce ctx) { }
    }

    private class FatState : ITissueState
    {
        public LumbarLayer Layer => LumbarLayer.Fat;
        public void Enter(LumbarPunctureDirectPluginForce ctx) { }
        public float CalculateForce(LumbarPunctureDirectPluginForce ctx)
        {
            float xFixed = ctx.DepthMillimeters - ctx.thickSkin;
            float rawFatForce = (6.0372f + 0.4516f * (xFixed / ctx.scalingCO)
                                - 0.5287f * Mathf.Pow(xFixed / ctx.scalingCO, 2)) * ctx.deviceForceScaling;

            float skinExitForce = (0.0235f + 0.0116f * (ctx.thickSkin / ctx.scalingCO)
                                  - 0.0046f * Mathf.Pow(ctx.thickSkin / ctx.scalingCO, 2)
                                  + 0.0025f * Mathf.Pow(ctx.thickSkin / ctx.scalingCO, 3)) * ctx.deviceForceScaling;

            float t = Mathf.Clamp01(xFixed / ctx.boundaryBlendWidthMm);
            float force = Mathf.Lerp(skinExitForce, rawFatForce, Mathf.SmoothStep(0.0f, 1.0f, t));
            return Mathf.Max(0.0f, force);
        }
        public void Exit(LumbarPunctureDirectPluginForce ctx) { }
    }

    private class SupraspinousLigamentState : ITissueState
    {
        public LumbarLayer Layer => LumbarLayer.SupraspinousLigamentMS;
        public void Enter(LumbarPunctureDirectPluginForce ctx) { }
        public float CalculateForce(LumbarPunctureDirectPluginForce ctx)
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
        public void Exit(LumbarPunctureDirectPluginForce ctx) { }
    }

    private class InterspinousLigamentState : ITissueState
    {
        public LumbarLayer Layer => LumbarLayer.InterspinousLigamentIL;
        public void Enter(LumbarPunctureDirectPluginForce ctx) { }
        public float CalculateForce(LumbarPunctureDirectPluginForce ctx)
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
        public void Exit(LumbarPunctureDirectPluginForce ctx) { }
    }

    private class LigamentumFlavumState : ITissueState
    {
        public LumbarLayer Layer => LumbarLayer.LigamentumFlavumLF;
        public void Enter(LumbarPunctureDirectPluginForce ctx) { }
        public float CalculateForce(LumbarPunctureDirectPluginForce ctx)
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
        public void Exit(LumbarPunctureDirectPluginForce ctx) { }
    }

    private class EpiduralSpaceState : ITissueState
    {
        public LumbarLayer Layer => LumbarLayer.EpiduralSpaceES;
        public void Enter(LumbarPunctureDirectPluginForce ctx) { }
        public float CalculateForce(LumbarPunctureDirectPluginForce ctx) => 0.0f;
        public void Exit(LumbarPunctureDirectPluginForce ctx) { }
    }

    private class DuraMaterState : ITissueState
    {
        public LumbarLayer Layer => LumbarLayer.DuraMater;
        public void Enter(LumbarPunctureDirectPluginForce ctx) { }
        public float CalculateForce(LumbarPunctureDirectPluginForce ctx)
        {
            float xFixed = ctx.DepthMillimeters - ctx.thickES;
            float blendRange = Mathf.Min(ctx.boundaryBlendWidthMm, ctx.thickDura);
            float t = Mathf.Clamp01(xFixed / blendRange);
            float force = Mathf.Lerp(0.0f, ctx.duraPunctureForceN, Mathf.SmoothStep(0.0f, 1.0f, t));
            return Mathf.Max(0.0f, force);
        }
        public void Exit(LumbarPunctureDirectPluginForce ctx) { }
    }

    private class SubarachnoidSpaceState : ITissueState
    {
        public LumbarLayer Layer => LumbarLayer.SubarachnoidSpace;
        public void Enter(LumbarPunctureDirectPluginForce ctx) { ctx.duralPunctureOccurred = true; }
        public float CalculateForce(LumbarPunctureDirectPluginForce ctx) => 0.0f;
        public void Exit(LumbarPunctureDirectPluginForce ctx) { }
    }

    #endregion


    //=================== End of State machine implementation ===================//

    private void Start()
    {
        hapticPlugin = Object.FindAnyObjectByType<HapticPlugin>();
        if (hapticPlugin == null)
        {
            Debug.LogError("LumbarPunctureDirectPluginForce: No HapticPlugin found in the scene.");
        }
    }

    private void FixedUpdate()
    {
        if (entryCircleTransform == null || needleTip == null || hapticPlugin == null)
        {
            ResetPluginForce();
            return;
        }

        // 1. Verify if needleTip is inside the configured bounding box volume
        isInsideBoundingBox = CheckInsideBoundingBox(needleTip.position);

        if (!isInsideBoundingBox)
        {
            // Instantly cancel force if needle leaves the anatomical bounding box
            isPenetrating = false;
            ResetPluginForce();
            return;
        }

        // 2. Vector from entry circle center to needle tip
        Vector3 displacement = needleTip.position - entryCircleTransform.position;
        Vector3 punctureAxis = entryCircleTransform.forward.normalized;

        // 3. Project to compute depth in millimeters along insertion axis
        float depthMeters = Vector3.Dot(displacement, punctureAxis);
        depthMillimeters = depthMeters * 1000.0f;

        // 4. Lateral deviation vector from the 1D line
        Vector3 targetPointOnAxis = entryCircleTransform.position + (punctureAxis * depthMeters);
        Vector3 lateralDeviation = needleTip.position - targetPointOnAxis;
        lateralOffsetMm = lateralDeviation.magnitude * 1000.0f;

        // 5. Check if entry is permitted (only starts if crossing the front surface close to the aperture)
        if (!isPenetrating)
        {
            bool withinApproachDistance = depthMillimeters > 0.0f && depthMillimeters <= surfaceEntryProximityMm;
            bool withinApertureRadius = lateralOffsetMm <= maxEntryApertureRadiusMm;

            if (withinApproachDistance && withinApertureRadius)
            {
                isPenetrating = true;
                entryOrientation = (needleMesh != null) ? needleMesh.rotation : entryCircleTransform.rotation;
            }
            else
            {
                // Needle is inside the bounding box but entered from the side/back or bypassed the skin aperture
                ResetPluginForce();
                return;
            }
        }

        // 6. Active penetration handling
        if (depthMillimeters > 0.0f)
        {
            // Lock visual mesh rotation along axis to mimic rigid needle in tissue
            if (needleMesh != null)
            {
                needleMesh.rotation = entryOrientation;
            }

            // Resolve State Transitions dynamically (bidirectional traversal & bone collisions)
            LumbarLayer targetLayer = ResolveTargetLayer(depthMillimeters, lateralOffsetMm);
            TransitionTo(targetLayer);

            // Delegate calculation to the active state
            float layerForceN = currentState.CalculateForce(this);

            // Retrieve physical device linear velocity (CurrentVelocity is in mm/s -> convert to m/s)
            Vector3 worldVelocity = hapticPlugin.transform.TransformDirection(hapticPlugin.CurrentVelocity * 0.001f);
            float axialVelocity = Vector3.Dot(worldVelocity, punctureAxis);
            Vector3 lateralVelocity = worldVelocity - (punctureAxis * axialVelocity);

            // Axial damping (oppose inward travel flutter)
            float axialDampingForce = (axialVelocity > 0.0f) ? (insertionDamping * axialVelocity) : 0.0f;
            Vector3 axialForceVector = -punctureAxis * (layerForceN + axialDampingForce);

            // Virtual fixture channel (PD Controller)
            Vector3 lateralRestoringForce = (-channelStiffnessK * lateralDeviation) - (channelDampingB * lateralVelocity);

            // Combine axial resistance + lateral channel force
            Vector3 totalForceVector = axialForceVector + lateralRestoringForce;
            float totalForceMag = totalForceVector.magnitude;

            // Clamp to Touch/Omni continuous limit (3.3 N)
            appliedForceN = Mathf.Clamp(totalForceMag, 0.0f, maxDeviceForceN);

            Vector3 forceDirection = (totalForceMag > 0.0001f) ? (totalForceVector / totalForceMag) : Vector3.zero;
            Vector3 deviceLocalDir = hapticPlugin.transform.InverseTransformDirection(forceDirection).normalized;

            // Deliver force directly to device hardware channel
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

    private LumbarLayer ResolveTargetLayer(float depth, float lateralOffset)
    {
        // Bone collision condition
        if (depth > thickIL_Before && depth < thickLF_After && lateralOffset > boneRadiusLimitMm)
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

    private bool CheckInsideBoundingBox(Vector3 worldPos)
    {
        if (targetBoundingBox == null)
            return true; // If unassigned, fall back to free volume

        // Convert world point to BoxCollider local space to account for position, rotation, scale, and center
        Vector3 localPoint = targetBoundingBox.transform.InverseTransformPoint(worldPos) - targetBoundingBox.center;
        Vector3 halfSize = targetBoundingBox.size * 0.5f;

        return Mathf.Abs(localPoint.x) <= halfSize.x &&
               Mathf.Abs(localPoint.y) <= halfSize.y &&
               Mathf.Abs(localPoint.z) <= halfSize.z;
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
            Gizmos.DrawRay(entryCircleTransform.position, entryCircleTransform.forward * ((thickES + thickDura) / 1000.0f));

            Vector3 esPoint = entryCircleTransform.position + entryCircleTransform.forward * (thickLF_After / 1000.0f);
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(esPoint, 0.005f);
        }
    }
}