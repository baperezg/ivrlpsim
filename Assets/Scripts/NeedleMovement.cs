using UnityEngine;
using UnityEngine.XR.OpenXR.Features.Interactions;

public class NeedleMovement : MonoBehaviour
{
    [SerializeField] private Transform questControllerTransform;

    

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // The needle moves and rotates as the quest controller does
        this.transform.position = questControllerTransform.transform.position;
        this.transform.rotation = questControllerTransform.transform.rotation;
    }

}
