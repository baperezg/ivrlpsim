using UnityEngine;

public class HookeDepthDirectPluginForce : MonoBehaviour
{
    [Header("Tracking Transforms")]
    [Tooltip("The 2D circle / entry aperture transform (forward points inward along insertion path).")]
    public Transform entryCircleTransform;

    [Tooltip("The stylus / needle tip tracked by the haptic device.")]
    public Transform needleTip;

    [Header("Spring Settings")]
    [Tooltip("Spring constant (stiffness) k in N/m.")]
    [Range(10f, 1000f)]
    public float stiffnessK = 250.0f;

    [Header("Device Constraints")]
    [Tooltip("Maximum force output of 3D Systems Touch/Omni (3.3 N).")]
    public float maxDeviceForceN = 3.3f;

    [Header("Runtime Telemetry (Read-Only)")]
    [SerializeField] private float depthMeters = 0.0f;
    [SerializeField] private float depthMillimeters = 0.0f;
    [SerializeField] private float appliedForceN = 0.0f;
    [SerializeField] private bool isPenetrating = false;

    private HapticPlugin hapticPlugin;
    private bool forceActive = false;

    private void Start()
    {
        // Find the active HapticPlugin instance in the scene
        hapticPlugin = Object.FindAnyObjectByType<HapticPlugin>();
        if (hapticPlugin == null)
        {
            Debug.LogError("No HapticPlugin found in the scene.");
        }
    }

    private void FixedUpdate()
    {
        if (entryCircleTransform == null || needleTip == null || hapticPlugin == null)
        {
            ResetPluginForce();
            return;
        }

        // 1. Vector from entry circle to needle tip
        Vector3 delta = needleTip.position - entryCircleTransform.position;

        // 2. Penetration axis pointing inward along the circle's forward vector
        Vector3 punctureAxis = entryCircleTransform.forward.normalized;

        // 3. Compute depth X (meters) along insertion axis
        depthMeters = Vector3.Dot(delta, punctureAxis);
        depthMillimeters = depthMeters * 1000.0f;

        if (depthMeters > 0.0f)
        {
            isPenetrating = true;

            // 4. Hooke's Law: F = k * X
            float rawForce = stiffnessK * depthMeters;
            appliedForceN = Mathf.Clamp(rawForce, 0.0f, maxDeviceForceN);

            // 5. Direction opposes penetration (points back outward along insertion axis)
            Vector3 opposingDirection = -punctureAxis;

            // 6. Convert direction from Unity World Space to Device Base Space
            // HapticPlugin tracks device-relative vectors via its own local transform
            Vector3 deviceLocalDir = hapticPlugin.transform.InverseTransformDirection(opposingDirection).normalized;

            // 7. Inject into HapticPlugin Global Constant Force members
            // ConstForceGMag expects a normalized value (0.0 to 1.0) relative to max force
            hapticPlugin.ConstForceGDir = deviceLocalDir;
            hapticPlugin.ConstForceGMag = appliedForceN / maxDeviceForceN;

            // Enable constant force loop on device if not already running
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

    private void ResetPluginForce()
    {
        if (hapticPlugin != null && forceActive)
        {
            hapticPlugin.ConstForceGMag = 0.0f;
            hapticPlugin.DisableContantForce();
            forceActive = false;
        }
        appliedForceN = 0.0f;
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
            Gizmos.DrawWireSphere(entryCircleTransform.position, 0.015f);
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(entryCircleTransform.position, entryCircleTransform.forward * 0.05f);
        }
    }
}