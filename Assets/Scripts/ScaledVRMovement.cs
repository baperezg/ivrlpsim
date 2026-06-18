using UnityEngine;

public class ScaledVRMovement : MonoBehaviour
{
    public int initialContact = 0;
    public Vector3 initialPos, secondPos,punctureDir;
    public float sphereRadious = 0.005f;

    
    // The game object you want to move.
    public Transform trackedGameObject;

    // The transform of your Meta Quest controller (e.g., OVRCameraRig/TrackingSpace/RightHandAnchor).
    public Transform questControllerTransform;

    // A private variable to store the controller's position from the previous frame.
    private Vector3 previousControllerPosition;

    // The desired scaling factor (5cm for every 10cm of controller movement).
    [Range(0.0f, 2.0f)]
    public float movementScale = 0.5f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Make sure the controller transform is not null before trying to access its position.
        if (questControllerTransform != null)
        {
            previousControllerPosition = questControllerTransform.position;
        }
    }

    // Update is called once per frame
    void Update()
    {

        if (initialContact > 1)
        {
            //this.transform.SetParent(null);
            Vector3 mov = punctureDir * movementScale*Time.deltaTime;
            //this.transform.position += mov;

            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);

            // Set the sphere's position to the stored position.
            sphere.transform.position = initialPos;

            // Set the sphere radius
            sphere.transform.localScale = new Vector3(sphereRadious, sphereRadious, sphereRadious);


        }
        // Check if both objects are assigned in the Inspector.
        /*
        if (trackedGameObject != null && questControllerTransform != null)
        {
            // Get the controller's current position.
            Vector3 currentControllerPosition = questControllerTransform.position;

            // Calculate the change in position (delta) since the last frame.
            Vector3 controllerDelta = currentControllerPosition - previousControllerPosition;

            // Scale the delta vector by the desired factor.
            Vector3 scaledDelta = controllerDelta * movementScale;

            // Apply the scaled delta to the target game object's position.
            trackedGameObject.position += scaledDelta;

            // Update the previous position for the next frame's calculation.
            previousControllerPosition = currentControllerPosition;
        }
        */
    }
    public void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("SkinPeak"))
        {
            if (initialContact == 0)
            {
                initialPos = questControllerTransform.position;


                // Create a sphere in the contact point
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);

                // Set the sphere's position to the stored position.
                sphere.transform.position = initialPos;

                // Set the sphere radius
                sphere.transform.localScale = new Vector3(sphereRadious, sphereRadious, sphereRadious);

                // 3. Get the MeshRenderer component from the new sphere.
                MeshRenderer renderer = sphere.GetComponent<MeshRenderer>();

                // 4. Change the color of the material.
                if (renderer != null)
                {
                    // You must get the material to change its color.
                    renderer.material.color = Color.red;
                }

                initialContact = 1;
            }

        }
    }

    public void OnTriggerStay(Collider other)
    {
        if (other.gameObject.CompareTag("SkinPeak"))
        {
            if (initialContact == 1)
            {
                secondPos = questControllerTransform.position;

                punctureDir = secondPos - initialPos;               

                initialContact = 2;
            }

        }
    }

    public void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("SkinPeak"))
        {
            //initialContact = 0;
        }
    }
}
