using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class NewNeedleMov : MonoBehaviour
{
    [SerializeField] private Transform leftControllerTransform;
    [SerializeField] private Transform rightControllerTransform;
    [SerializeField] private XRGrabInteractable grabInteractable;

    public Transform ActiveControllerTransform { get; private set; }
    public bool IsGrabbed => ActiveControllerTransform != null;

    private void Awake()
    {
        if (grabInteractable == null)
            grabInteractable = GetComponent<XRGrabInteractable>();

        grabInteractable.selectEntered.AddListener(OnGrab);
        grabInteractable.selectExited.AddListener(OnRelease);
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        if (args.interactorObject is XRBaseInputInteractor inputInteractor)
        {
            if (inputInteractor.handedness == InteractorHandedness.Left)
            {
                ActiveControllerTransform = leftControllerTransform;
            }
            else if (inputInteractor.handedness == InteractorHandedness.Right)
            {
                ActiveControllerTransform = rightControllerTransform;
            }
        }
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        ActiveControllerTransform = null;
    }

    private void OnDestroy()
    {
        grabInteractable.selectEntered.RemoveListener(OnGrab);
        grabInteractable.selectExited.RemoveListener(OnRelease);
    }
}
