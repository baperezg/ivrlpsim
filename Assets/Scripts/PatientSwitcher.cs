using UnityEngine;

public class PatientSwitcher : MonoBehaviour
{
    [SerializeField] private GameObject objectA;
    [SerializeField] private GameObject objectB;

    private GameObject activeObject;

    void Start()
    {
        // Initialize with objectA active by default
        SetActiveObject(objectA);
    }

    /// <summary>
    /// Switches the active object between A and B.
    /// </summary>
    public void SwitchObjects()
    {
        if (activeObject == objectA)
        {
            SetActiveObject(objectB);
        }
        else
        {
            SetActiveObject(objectA);
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
