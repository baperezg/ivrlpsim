using UnityEngine;

public class PatientSwitcher : MonoBehaviour
{
    [SerializeField] private GameObject objectA;
    [SerializeField] private GameObject objectB;
    [SerializeField] private GameObject lpAnatomy;

    public Vector3 lpSitPos;
    public Vector3 lpSitRot;
    public Vector3 lpLayPos;
    public Vector3 lpLayRot;


    private GameObject activeObject;

    void Start()
    {
        // Initialize with objectA active by default
        SetActiveObject(objectA);
        lpAnatomy.transform.position = lpSitPos;
        lpAnatomy.transform.rotation = Quaternion.Euler(lpSitRot);
    }

    /// <summary>
    /// Switches the active object between A and B.
    /// </summary>
    public void SwitchObjects()
    {
        if (activeObject == objectA)
        {
            SetActiveObject(objectB);
            lpAnatomy.transform.position = lpLayPos;
            lpAnatomy.transform.rotation = Quaternion.Euler(lpLayRot);
        }
        else
        {
            SetActiveObject(objectA);
            lpAnatomy.transform.position = lpSitPos;
            lpAnatomy.transform.rotation = Quaternion.Euler(lpSitRot);

        }
    }

    /// <summary>
    /// Helper function to activate one object and deactivate the other.
    /// </summary>
    private void SetActiveObject(GameObject target)
    {
        objectA.SetActive(target == objectA);
        objectB.SetActive(target == objectB);
        activeObject = target;

    }
}
