using Oculus.Haptics;
using System;
using UnityEngine;

public class HapticsOVRLp : MonoBehaviour
{
    [SerializeField] private TriggerHapticOnGrab triggerHapticOnGrab;
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    // Update is called once per frame
    void Update()
    {
        //HandleControllerInput(OVRInput.Controller.LTouch, Controller.Left);
        HandleControllerInput(OVRInput.Controller.RTouch, Controller.Right);

    }
    void HandleControllerInput(OVRInput.Controller controller, Controller hand)
    {
        try
        {
            // Play first clip with default priority using the index trigger
            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, controller))
            {
                triggerHapticOnGrab.PlayClipSkinLoop(hand);
            }

            // Play second clip with higher priority using the grab button
            if (OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, controller))
            {
                triggerHapticOnGrab.PlayClipSkinPeak(hand);
            }

            // Stop first clip when releasing the index trigger
            if (OVRInput.GetUp(OVRInput.Button.PrimaryIndexTrigger, controller))
            {
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
    }

}
