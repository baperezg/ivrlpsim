using UnityEngine;

public class NewNeedleModel : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Object representing the Surface Contact Point (SCP)")]
    public GameObject scp;

    [SerializeField] private NewNeedleMov needleMovement;

    [Header("Settings")]
    [Tooltip("Direction vector along the length of the needle (usually Vector3.forward or Vector3.up)")]
    public Vector3 localInsertionAxis = Vector3.forward;

    [SerializeField] private bool isPunctured = false;
    [SerializeField] private Vector3 entryPoint;
    [SerializeField] private Vector3 insertionDirectionWorld;
    [SerializeField] private Quaternion entryRotation;

    private void Awake()
    {
        if (needleMovement == null)
            needleMovement = GetComponent<NewNeedleMov>();
    }

    private void Update()
    {
        if (needleMovement == null || !needleMovement.IsGrabbed)
            return;

        Vector3 targetPos = needleMovement.ActiveControllerTransform.position;
        Quaternion targetRot = needleMovement.ActiveControllerTransform.rotation;

        if (isPunctured)
        {
            // Project target controller position onto the 1D axis line from entryPoint
            Vector3 targetOffset = targetPos - entryPoint;
            float distanceAlongAxis = Vector3.Dot(targetOffset, insertionDirectionWorld);

            // Restrict movement strictly along insertion axis line
            transform.position = entryPoint + (insertionDirectionWorld * distanceAlongAxis);
            transform.rotation = entryRotation;

            // Lock rotation to initial entry direction to avoid bending inside tissue
            // (Remove or adjust if rotational twist around axis is allowed)
        }
        else
        {
            // Normal free movement when not inside tissue
            transform.position = targetPos;
            transform.rotation = targetRot;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isPunctured && other.CompareTag("Tissue"))
        {
            isPunctured = true;

            // Capture exact contact point on entry
            entryPoint = transform.position;
            entryRotation = transform.rotation;

            // Lock current insertion direction in world space
            insertionDirectionWorld = transform.TransformDirection(localInsertionAxis).normalized;

            if (scp != null)
            {
                scp.transform.position = entryPoint;
                scp.SetActive(true);
            }

            Debug.Log($"<b>[Puncture Initiated]</b> Entry Point: {entryPoint}");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Tissue"))
        {
            isPunctured = false;

            if (scp != null)
            {
                scp.SetActive(false);
            }

            Debug.Log("<b>[Puncture Exited]</b> Needle withdrawn.");
        }
    }
}