using UnityEngine;

public class NeedleModel : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Object representing the Surface Contact Point (SCP)")]

    public GameObject scp;

    [Header("Settings")]
    [Tooltip("Direction vector along the length of the needle (usually Vector3.forward or Vector3.up depending on model import)")]

    public Vector3 localInsertionAxis = Vector3.forward;

    private bool isPunctured = false;
    private Vector3 entryPoint;
    private Vector3 insertionDirectionWorld;

    void OnTriggerEnter(Collider other)

    {

        if (other.CompareTag("Tissue") || isPunctured == false)
        {
            isPunctured = true;

            // 1. Capture exact contact point on entry
            entryPoint = transform.position;


            // 2. Lock the current direction vector in world space
            insertionDirectionWorld = transform.TransformDirection(localInsertionAxis).normalized;

            // 3. Move SCP marker to entry location
            if (scp != null)
            {
                scp.transform.position = entryPoint;
                scp.SetActive(true);
            }

            Debug.Log($"<b>[Puncture Initiated]</b> Entry Point: {entryPoint}");
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (isPunctured)
        {
            ConstrainToInsertionAxis();
        }
    }



    void OnTriggerExit(Collider other)
    {
        isPunctured = false;

        if (scp != null)
        {
            scp.SetActive(false);
        }

        Debug.Log("<b>[Puncture Exited]</b> Needle withdrawn.");
    }



    /// <summary>
    /// Projects the user's current attempt position onto the 1D line running along the insertion axis.
    /// </summary>

    private void ConstrainToInsertionAxis()
    {

        // Vector from entry point to where the user is trying to move the needle
        Vector3 userOffset = transform.position - entryPoint;


        // Vector projection onto the needle's axis line
        float distanceAlongAxis = Vector3.Dot(userOffset, insertionDirectionWorld);

        // Calculate clamped/projected position in 3D world space
        Vector3 constrainedPosition = entryPoint + (insertionDirectionWorld * distanceAlongAxis);

        // Apply constrained position (maintains insertion depth, strips lateral motion)
        transform.position = constrainedPosition;
    }
}

