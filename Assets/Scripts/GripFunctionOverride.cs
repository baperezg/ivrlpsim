using UnityEngine;
using UnityEngine.InputSystem;

public class GripFunctionOverride : MonoBehaviour
{
    public InputActionProperty myNewGripFunction;

    void OnEnable()
    {
        // Enable the action and subscribe to its 'performed' event.
        myNewGripFunction.action.Enable();
        myNewGripFunction.action.performed += OnGripPerformed;
    }

    void OnDisable()
    {
        // Unsubscribe from the event and disable the action.
        myNewGripFunction.action.performed -= OnGripPerformed;
        myNewGripFunction.action.Disable();
    }

    private void OnGripPerformed(InputAction.CallbackContext context)
    {
        // This function will be called every time the grip button is pressed.
        Debug.Log("My new grip function was performed!");
        // Add your custom logic here.
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
