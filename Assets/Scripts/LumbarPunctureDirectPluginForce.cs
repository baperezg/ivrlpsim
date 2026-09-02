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
    BoneStrike = 8
}

public class LumbarPunctureDirectPluginForce : MonoBehaviour
{
    [Header("Tracking Transforms")]
    [Tooltip("The 2D circle / entry aperture transform (its forward vector defines penetration direction).")]
    public Transform entryCircleTransform;

    [Tooltip("The needle tip / stylus proxy tracked by the haptic device.")]
    public Transform needleTip;

    [Header("Device Hardware Limit")]
    [Tooltip("Maximum continuous force output for 3D Systems Touch/Omni (3.3 N).")]
    public float maxDeviceForceN = 3.3f;

    [Header("Stabilization & Damping")]
    [Tooltip("Viscous damping factor (N·s/m) along puncture axis to cancel flutter/buzzing.")]
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
    public float thickSkin = 5.0f;
    public float thickFat = 15.0f;
    public float thickMS_Before = 20.0f;
    public float thickMS_After = 25.0f;
    public float thickIL_Before = 30.0f;
    public float thickIL_After = 35.0f;
    public float thickLF_Before = 40.0f;
    public float thickLF_After = 44.0f;
    public float thickES = 50.0f; // Beyond LF_After up to Dura

    [Header("Bone Collision Constraints")]
    [Tooltip("Radial deviation threshold in mm from center to trigger bone contact.")]
    public float boneRadiusLimitMm = 10.0f;
    [Tooltip("Resistive stiffness force when colliding with bone.")]
    public float boneStrikeForceN = 3.0f;

    [Header("Runtime Telemetry (Read-Only)")]
    [SerializeField] private LumbarLayer currentLayer = LumbarLayer.Outside;
    [SerializeField] private float depthMillimeters = 0.0f;
    [SerializeField] private float lateralOffsetMm = 0.0f;
    [SerializeField] private float appliedForceN = 0.0f;
    [SerializeField] private bool isPenetrating = false;
    [SerializeField] private bool duralPunctureOccurred = false;

    private HapticPlugin hapticPlugin;
    private bool forceActive = false;

    private void Start()
    {
        // Automatically locate the active HapticPlugin in the scene        
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

        // 1. Vector from entry circle center to needle tip
        Vector3 displacement = needleTip.position - entryCircleTransform.position;

        // 2. Trajectory axis pointing inward along circle's forward vector
        Vector3 punctureAxis = entryCircleTransform.forward.normalized;

        // 3. Project to compute depth in millimeters along insertion axis
        float depthMeters = Vector3.Dot(displacement, punctureAxis);
        depthMillimeters = depthMeters * 1000.0f;

        // 4. Calculate lateral deviation off-axis (in mm) for bone collisions
        Vector3 lateralVector = displacement - (depthMeters * punctureAxis);
        lateralOffsetMm = lateralVector.magnitude * 1000.0f;

        if (depthMillimeters > 0.0f)
        {
            isPenetrating = true;

            // 5. Compute the piecewise polynomial force based on current tissue layer
            float rawForce = CalculateLayerForce(depthMillimeters, lateralOffsetMm);

            // 6. Clamp total force to Touch/Omni continuous limit (3.3 N)
            appliedForceN = Mathf.Clamp(rawForce, 0.0f, maxDeviceForceN);

            // 7. Force direction opposes insertion (points back outward)
            Vector3 opposingDirection = -punctureAxis;

            // 8. Transform direction to HapticPlugin local space
            Vector3 deviceLocalDir = hapticPlugin.transform.InverseTransformDirection(opposingDirection).normalized;

            // 9. Deliver force directly to device hardware channel
            hapticPlugin.ConstForceGDir = deviceLocalDir; //[cite: 3]
            hapticPlugin.ConstForceGMag = appliedForceN / maxDeviceForceN; //[cite: 3]

            if (!forceActive)
            {
                hapticPlugin.EnableConstantForce(); //[cite: 3]
                forceActive = true;
            }
        }
        else
        {
            isPenetrating = false;
            ResetPluginForce();
        }
    }

    private float CalculateLayerForce(float depth, float lateralOffset)
    {
        // Bone collision: Between Interspinous Ligament and Ligamentum Flavum
        if (depth > thickIL_Before && depth < thickLF_After && lateralOffset > boneRadiusLimitMm)
        {
            currentLayer = LumbarLayer.BoneStrike;
            return boneStrikeForceN;
        }

        float xFixed = 0.0f;
        float force = 0.0f;

        // 1. Skin (Cubic polynomial)
        if (depth <= thickSkin)
        {
            currentLayer = LumbarLayer.Skin;
            xFixed = depth;
            force = (0.0235f + 0.0116f * (xFixed / scalingCO)
                    - 0.0046f * Mathf.Pow(xFixed / scalingCO, 2)
                    + 0.0025f * Mathf.Pow(xFixed / scalingCO, 3)) * deviceForceScaling; //[cite: 1]
        }
        // 2. Fat (Quadratic polynomial, with smooth blend at boundary to prevent flutter)
        else if (depth <= thickFat)
        {
            currentLayer = LumbarLayer.Fat;
            xFixed = depth - thickSkin;
            float rawFatForce = (6.0372f + 0.4516f * (xFixed / scalingCO)
                                - 0.5287f * Mathf.Pow(xFixed / scalingCO, 2)) * deviceForceScaling; //[cite: 1]

            // End-of-skin base force
            float skinExitForce = (0.0235f + 0.0116f * (thickSkin / scalingCO)
                                  - 0.0046f * Mathf.Pow(thickSkin / scalingCO, 2)
                                  + 0.0025f * Mathf.Pow(thickSkin / scalingCO, 3)) * deviceForceScaling; //[cite: 1]

            // Blend smoothly over boundaryBlendWidthMm from skin to fat
            float t = Mathf.Clamp01(xFixed / boundaryBlendWidthMm);
            force = Mathf.Lerp(skinExitForce, rawFatForce, Mathf.SmoothStep(0.0f, 1.0f, t));
        }
        // 3. Supraspinous Ligament (MS) - Region 1
        else if (depth <= thickMS_Before)
        {
            currentLayer = LumbarLayer.SupraspinousLigamentMS;
            xFixed = depth - thickFat;
            force = (1.9736f + 0.8287f * (xFixed / scalingCO)
                    + 0.1078f * Mathf.Pow(xFixed / scalingCO, 2)) * deviceForceScaling; //[cite: 1]
        }
        // 4. Supraspinous Ligament (MS) - Region 2
        else if (depth <= thickMS_After)
        {
            currentLayer = LumbarLayer.SupraspinousLigamentMS;
            xFixed = depth - thickMS_Before;
            force = (4.354f - 2.2543f * (xFixed / scalingCO)
                    + 0.2902f * Mathf.Pow(xFixed / scalingCO, 2)) * deviceForceScaling; //[cite: 1]
        }
        // 5. Interspinous Ligament (IL) - Linear ramp
        else if (depth <= thickIL_Before)
        {
            currentLayer = LumbarLayer.InterspinousLigamentIL;
            xFixed = depth - thickMS_After;
            force = (4.4062f + 0.9598f * (xFixed / scalingCO)) * deviceForceScaling; //[cite: 1]
        }
        // 6. Interspinous Ligament (IL) - Plateau
        else if (depth <= thickIL_After)
        {
            currentLayer = LumbarLayer.InterspinousLigamentIL;
            force = 7.467f * deviceForceScaling; //[cite: 1]
        }
        // 7. Ligamentum Flavum (LF) - Region 1
        else if (depth <= thickLF_Before)
        {
            currentLayer = LumbarLayer.LigamentumFlavumLF;
            xFixed = depth - thickIL_After;
            force = (7.467f + 1.5029f * (xFixed / scalingCO)
                    - 0.0583f * Mathf.Pow(xFixed / scalingCO, 2)) * deviceForceScaling; //[cite: 1]
        }
        // 8. Ligamentum Flavum (LF) - Peak prior to release
        else if (depth <= thickLF_After)
        {
            currentLayer = LumbarLayer.LigamentumFlavumLF;
            xFixed = depth - thickLF_Before;
            force = (12.133f - 0.1693f * (xFixed / scalingCO)
                    - 0.1177f * Mathf.Pow(xFixed / scalingCO, 2)) * deviceForceScaling; //[cite: 1]
        }
        // 9. Epidural Space (Loss of Resistance - LOR)
        else if (depth <= thickES)
        {
            currentLayer = LumbarLayer.EpiduralSpaceES;
            force = 0.0f; // Drop to 0 N[cite: 1]
        }
        // 10. Dura Mater / Dural Puncture
        else
        {
            currentLayer = LumbarLayer.DuraMater;
            duralPunctureOccurred = true; //[cite: 1]
            force = 0.0f;
        }

        return Mathf.Max(0.0f, force);
    }

    private void ResetPluginForce()
    {
        currentLayer = LumbarLayer.Outside;
        appliedForceN = 0.0f;

        if (hapticPlugin != null && forceActive)
        {
            hapticPlugin.ConstForceGMag = 0.0f; //[cite: 3]
            hapticPlugin.DisableContantForce(); //[cite: 3]
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
            // Entry point disc
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(entryCircleTransform.position, 0.015f);

            // Trajectory ray
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(entryCircleTransform.position, entryCircleTransform.forward * (thickES / 1000.0f));

            // Epidural loss-of-resistance point
            Vector3 esPoint = entryCircleTransform.position + entryCircleTransform.forward * (thickLF_After / 1000.0f);
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(esPoint, 0.005f);
        }
    }
}