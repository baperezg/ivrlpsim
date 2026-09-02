using UnityEngine;
using HapticGUI;

[RequireComponent(typeof(HapticMaterial))]
public class HookeDepthHapticResponse : MonoBehaviour
{
    [Header("Tracking Transforms")]
    [Tooltip("The 2D circle / entry aperture transform (its forward vector defines penetration direction).")]
    public Transform entryCircleTransform;

    [Tooltip("The needle tip / stylus proxy tracked by the haptic device.")]
    public Transform needleTip;

    [Header("Spring Settings")]
    [Tooltip("Spring constant (stiffness) k in N/m.")]
    [Range(10f, 1000f)]
    public float stiffnessK = 250.0f;

    [Tooltip("Viscous damping to prevent jitter while pushing inward.")]
    [Range(0f, 1f)]
    public float surfaceViscosity = 0.15f;

    [Header("Device Constraints")]
    [Tooltip("Maximum continuous force output for 3D Systems Touch/Omni (3.3 N).")]
    public float maxDeviceForceN = 3.3f;

    [Header("Runtime Telemetry (Read-Only)")]
    [SerializeField] private float depthMeters = 0.0f;
    [SerializeField] private float depthMillimeters = 0.0f;
    [SerializeField] private float appliedForceN = 0.0f;
    [SerializeField] private bool isPenetrating = false;

    private HapticMaterial hapticMaterial;

    private void Awake()
    {
        hapticMaterial = GetComponent<HapticMaterial>();
    }

    private void FixedUpdate()
    {
        if (entryCircleTransform == null || needleTip == null || hapticMaterial == null)
        {
            ResetForce();
            return;
        }

        // 1. Vector from entry circle center to needle tip
        Vector3 displacement = needleTip.position - entryCircleTransform.position;

        // 2. Trajectory axis pointing inward along the circle's forward vector
        Vector3 punctureAxis = entryCircleTransform.forward.normalized;

        // 3. Compute penetration depth X (meters) along the axis
        depthMeters = Vector3.Dot(displacement, punctureAxis);
        depthMillimeters = depthMeters * 1000.0f;

        // Check if needle has passed the entry surface
        if (depthMeters > 0.0f)
        {
            isPenetrating = true;

            // 4. Hooke's Law: F = k * X
            float rawForce = stiffnessK * depthMeters;

            // 5. Clamp to device maximum limit (3.3 N)
            appliedForceN = Mathf.Clamp(rawForce, 0.0f, maxDeviceForceN);

            // 6. Direct force back towards the entrance (opposing penetration)
            hapticMaterial.hConstForceDir = -punctureAxis; //

            // 7. Normalize magnitude for HapticMaterial (0.0 to 1.0)
            hapticMaterial.hConstForceMag = appliedForceN / maxDeviceForceN; //[cite: 2]
            hapticMaterial.hViscosity = surfaceViscosity; //[cite: 2]
        }
        else
        {
            isPenetrating = false;
            ResetForce();
        }
    }

    private void ResetForce()
    {
        if (hapticMaterial != null)
        {
            hapticMaterial.hConstForceMag = 0.0f; //[cite: 2]
            hapticMaterial.hConstForceDir = Vector3.zero; //[cite: 2]
            hapticMaterial.hViscosity = 0.0f; //[cite: 2]
        }
        appliedForceN = 0.0f;
    }

    private void OnDisable()
    {
        ResetForce();
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