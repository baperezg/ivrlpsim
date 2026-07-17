using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables; // XRI 3 namespace for interactables

[RequireComponent(typeof(XRGrabInteractable))]
public class PrimaryButAction : MonoBehaviour
{
    [Tooltip("Assign the Input Action for the X or A button (e.g., XRI LeftHand/Primary Button)")]
    [SerializeField] private InputActionReference primaryButtonAction;

    [Header("Hold Settings")]
    [Tooltip("How long (in seconds) the button must be held to trigger the Hold event.")]
    [SerializeField] private float holdDuration = 1.0f;

    private XRGrabInteractable grabInteractable;
    private bool isButtonDown = false;
    private float buttonPressedTime = 0f;
    private bool holdTriggered = false;

    public CheckCSF checkCSF;


    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
    }
    private void OnEnable()
    {
        if (primaryButtonAction != null)
        {
            // .started fires the frame the button is first touched/depressed
            primaryButtonAction.action.started += OnButtonStarted;
            // .canceled fires the frame the button is fully released
            primaryButtonAction.action.canceled += OnButtonReleased;
        }
    }

    private void OnDisable()
    {
        if (primaryButtonAction != null)
        {
            primaryButtonAction.action.started -= OnButtonStarted;
            primaryButtonAction.action.canceled -= OnButtonReleased;
        }
    }

    private void Update()
    {
        // Track the "Hold" state if the button is pressed and we haven't triggered the hold event yet
        if (isButtonDown && !holdTriggered && grabInteractable.isSelected)
        {
            if (Time.time - buttonPressedTime >= holdDuration)
            {
                holdTriggered = true;
                OnButtonHold();
            }
        }
    }

    // 1. ON CLICK (PRESS)
    private void OnButtonStarted(InputAction.CallbackContext context)
    {
        if (!grabInteractable.isSelected) return;

        isButtonDown = true;
        buttonPressedTime = Time.time;
        holdTriggered = false;

        OnClick();
    }

    // 2. ON RELEASE
    private void OnButtonReleased(InputAction.CallbackContext context)
    {
        isButtonDown = false;

        if (!grabInteractable.isSelected) return;

        OnRelease();
    }

    // --- Custom Logic Methods ---

    private void OnClick()
    {
        Debug.Log("Primary Button: Clicked!");
        checkCSF.moveForward();
    }

    private void OnRelease()
    {
        Debug.Log("Primary Button: Released!");
        checkCSF.moveBackward();
    }

    // 3. ON HOLD
    private void OnButtonHold()
    {
        Debug.Log($"Primary Button: Held down for {holdDuration} seconds!");
        checkCSF.checkCSF();
    }
}
