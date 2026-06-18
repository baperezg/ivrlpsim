using UnityEngine;
using System;
using Oculus.Haptics;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.HID;

public class HideController : MonoBehaviour
{
    public GameObject m_ControlVisual;
    public InputActionReference customButton;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        customButton.action.started += ButtonWasPressed;
        //customButton.action.canceled += ButtonWasReleased;
    }

    void ButtonWasPressed(InputAction.CallbackContext context)
    {
        //Debug.Log("hide parts of the controller");
        
        if (m_ControlVisual != null)
        {
            // Get the current active state and set it to the opposite.
            m_ControlVisual.SetActive(!m_ControlVisual.activeSelf);
        }

        //RecursiveToggle(m_ControlVisual);
        /*foreach (Transform child in m_ControlVisual.transform)
        {
            // 'child' is the Transform component of each child object
           Debug.Log("Child name: " + child.name + " disable/Enable");

            // You can also access the GameObject
            GameObject childObject = child.gameObject;


            if (child.name.Equals("XRController_Thumbstick_Buttons"))
            {
                foreach (Transform child2 in child)
                {
                    GameObject childObject2 = child2.gameObject;
                    if (childObject2.GetComponent<MeshRenderer>() != null) { 
                        childObject2.GetComponent<MeshRenderer>().enabled = !childObject2.GetComponent<MeshRenderer>().enabled;
                    }
                }
            }
            else
            {
                // Example: Disable the child GameObject
                if (childObject.GetComponent<MeshRenderer>() != null)
                {
                    childObject.GetComponent<MeshRenderer>().enabled = !childObject.GetComponent<MeshRenderer>().enabled;
                }
                //Debug.Log("Child name: " + child.name+" disable/Enable");
            }
        
                
        }*/

    }

    private void RecursiveToggle(GameObject targetObject)
    {
        

        // Get the MeshRenderer component on the current GameObject.
        MeshRenderer meshRenderer = targetObject.GetComponent<MeshRenderer>();
        Debug.Log("Target name: " + targetObject.name + " disable/Enable");


        // If the component exists, toggle its enabled state.
        if (meshRenderer != null)
        {
            //meshRenderer.enabled = !meshRenderer.enabled;
            targetObject.GetComponent<MeshRenderer>().enabled = !targetObject.GetComponent<MeshRenderer>().enabled;
            Debug.Log("Target with mesh renderer: " + targetObject.name + " disable/Enable");
        }

        // Now, recursively call this function for each child of the current GameObject.
        for (int i = 0; i < targetObject.transform.childCount; i++)
        {
            Transform child = targetObject.transform.GetChild(i);
            
            // Call the function again on the child, continuing the recursion.
            RecursiveToggle(child.gameObject);
        }
    }


    void ButtonWasReleased(InputAction.CallbackContext context)
    {
        //mesh.enabled = !mesh.enabled;        
    }
    // Update is called once per frame
    void Update()
    {
        
        //HandleControllerInput(OVRInput.Controller.LTouch, Controller.Left);
       // HandleControllerInput(OVRInput.Controller.RTouch, Controller.Right);

    }
    void HandleControllerInput(OVRInput.Controller controller, Controller hand)
    {
        /*
                if (OVRInput.GetDown(OVRInput.Button.One, controller))
                {
                    Debug.Log("Botón A");
                }


                try
                {
                    // Hide/show controller visual using button A
                    if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, controller))
                    {
                        //m_ControlVisual.GetComponent<MeshRenderer>().enabled = !m_ControlVisual.GetComponent<MeshRenderer>().enabled;
                        //this.enabled = !this.enabled;

                        Debug.Log("Activate/Deactivate controller visual");
                        foreach (Transform child in m_ControlVisual.transform)
                        {
                            // 'child' is the Transform component of each child object
                            Debug.Log("Child name: " + child.name);

                            // You can also access the GameObject
                            GameObject childObject = child.gameObject;

                            // Example: Disable the child GameObject
                            childObject.GetComponent<MeshRenderer>().enabled = !childObject.GetComponent<MeshRenderer>().enabled;
                        }

                        //triggerHapticOnGrab.PlayClipSkinLoop(hand);
                    }

                    // Play second clip with higher priority using the grab button
                    if (OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, controller))
                    {
                        //triggerHapticOnGrab.PlayClipSkinPeak(hand);
                    }

                    // Stop first clip when releasing the index trigger
                    if (OVRInput.GetUp(OVRInput.Button.PrimaryIndexTrigger, controller))
                    {
                        /*
                        triggerHapticOnGrab.StopClipSkinLoop(hand);

                        // Stop second clip when releasing the grab button
                        if (OVRInput.GetUp(OVRInput.Button.PrimaryHandTrigger, controller))
                        {
                            triggerHapticOnGrab.StopClipSkinPeak(hand);
                        }

                        // Loop first clip using the B/Y-button
                        if (OVRInput.GetDown(OVRInput.Button.Two, controller))
                        {
                            triggerHapticOnGrab.SetLoopingOfClipSkinLoop(hand);
                        }

                        // Modulate the amplitude and frequency of the first clip using the thumbstick
                        // - Moving left/right modulates the frequency shift
                        // - Moving up/down modulates the amplitude
                        if (controller == OVRInput.Controller.LTouch)
                        {
                            Vector2 thumbstickInput = new Vector2(OVRInput.Get(OVRInput.RawAxis2D.LThumbstick).x,
                                Mathf.Clamp(1.0f + OVRInput.Get(OVRInput.RawAxis2D.LThumbstick).y, 0.0f, 2.0f));
                            triggerHapticOnGrab.ModulateAmplitudeAndFrequencyOfClipSkinLoop(hand, thumbstickInput);
                        }
                        else if (controller == OVRInput.Controller.RTouch)
                        {
                            Vector2 thumbstickInput = new Vector2(OVRInput.Get(OVRInput.RawAxis2D.RThumbstick).x,
                                Mathf.Clamp(1.0f + OVRInput.Get(OVRInput.RawAxis2D.RThumbstick).y, 0.0f, 2.0f));
                            triggerHapticOnGrab.ModulateAmplitudeAndFrequencyOfSkinPeak(hand, thumbstickInput);
                        }

                    }

                }

                // If any exceptions occur, we catch and log them here.
                catch (Exception e)
                {
                    Debug.LogError(e.Message);
                }
        */
    }

}
