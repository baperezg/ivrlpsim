using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class NeedleMovement : MonoBehaviour
{
    [SerializeField] private Transform leftControllerTransform;
    [SerializeField] private Transform rightControllerTransform;
    [SerializeField] private XRGrabInteractable grabInteractable;

    private Transform activeControllerTransform;

    void Awake()
    {
        // Subscribe to grab/release events
        grabInteractable.selectEntered.AddListener(OnGrab);
        grabInteractable.selectExited.AddListener(OnRelease);
    }
    private void OnGrab(SelectEnterEventArgs args)
    {
        if (args.interactorObject is XRBaseInputInteractor inputInteractor)
        {
            if (inputInteractor.handedness == InteractorHandedness.Left)
            {
                Debug.Log("Grabbed with LEFT hand");
                activeControllerTransform = leftControllerTransform;
            }
            else if (inputInteractor.handedness == InteractorHandedness.Right)
            {
                Debug.Log("Grabbed with RIGHT hand");
                activeControllerTransform = rightControllerTransform;
            }
        }
    }


    private void OnRelease(SelectExitEventArgs args)
    {
        activeControllerTransform = null;
    }

    void Update()
    {
        if (activeControllerTransform != null)
        {
            transform.position = activeControllerTransform.position;
            transform.rotation = activeControllerTransform.rotation;
        }
    }

}
