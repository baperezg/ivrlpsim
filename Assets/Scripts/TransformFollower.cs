using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class TransformFollower : MonoBehaviour
{
    public enum UpdateTiming
    {
        LateUpdate,   // Recommended: runs after animations/physics calculations
        Update,       // Standard frame loop
        FixedUpdate   // Synchronized with PhysX/haptics loop
    }

    [Header("Target")]
    [Tooltip("The GameObject whose position, rotation, and scale will be copied.")]
    public Transform target;

    [Header("Sync Channels")]
    public bool syncPosition = true;
    public bool syncRotation = true;
    public bool syncScale = true;

    [Header("Execution Timing")]
    [Tooltip("LateUpdate is optimal to avoid 1-frame lagging behind moving targets.")]
    public UpdateTiming timing = UpdateTiming.LateUpdate;

    [Header("Offsets (Optional)")]
    [Tooltip("Position offset relative to the target's orientation.")]
    public Vector3 localPositionOffset = Vector3.zero;

    [Tooltip("Euler angle rotation offset added to the target.")]
    public Vector3 rotationOffsetEuler = Vector3.zero;

    private void Update()
    {
        if (timing == UpdateTiming.Update)
        {
            ApplyTransformSync();
        }
    }

    private void LateUpdate()
    {
        if (timing == UpdateTiming.LateUpdate)
        {
            ApplyTransformSync();
        }
    }

    private void FixedUpdate()
    {
        if (timing == UpdateTiming.FixedUpdate)
        {
            ApplyTransformSync();
        }
    }

    /// <summary>
    /// Copies position, rotation, and scale from the target transform.
    /// </summary>
    public void ApplyTransformSync()
    {
        if (target == null) return;

        // 1. Sync Rotation
        if (syncRotation)
        {
            if (rotationOffsetEuler != Vector3.zero)
            {
                transform.rotation = target.rotation * Quaternion.Euler(rotationOffsetEuler);
            }
            else
            {
                transform.rotation = target.rotation;
            }
        }

        // 2. Sync Position
        if (syncPosition)
        {
            if (localPositionOffset != Vector3.zero)
            {
                // Applies offset respecting the target's current orientation
                transform.position = target.position + target.TransformDirection(localPositionOffset);
            }
            else
            {
                transform.position = target.position;
            }
        }

        // 3. Sync Scale
        if (syncScale)
        {
            // Use lossyScale so hierarchy nesting on the target does not distort size
            transform.localScale = target.lossyScale;
        }
    }
}