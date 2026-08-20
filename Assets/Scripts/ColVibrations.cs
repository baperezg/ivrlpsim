using Oculus.Haptics;
using System;
using TMPro;
using UnityEngine;

public class ColVibrations : MonoBehaviour
{
    [Header("Haptics & Simulation References")]
    [SerializeField] private TriggerHapticOnGrab triggerHapticOnGrab;
    [SerializeField] private CSFDropletEmitter dropletEmitter;
    [SerializeField] private PrimaryButAction primaryButAction;

    [Header("UI Feedback")]
    [Tooltip("Assign the TextMeshPro-Text component from your Canvas / UI.")]
    [SerializeField] private TextMeshProUGUI tissueStatusText;

    //private OVRInput.Controller controller;
    private Controller hand;

    public void Start()
    {
        //controller = OVRInput.Controller.RTouch;
        hand = Controller.Right;
        UpdateTissueText("External / Air");
    }
    void Update()
    {
        //HandleControllerInput(OVRInput.Controller.LTouch, Controller.Left);
        HandleControllerInput(OVRInput.Controller.RTouch, Controller.Right);

    }

    public void OnTriggerEnter(Collider other)
    {
        string tag = other.gameObject.tag;

        if (other.gameObject.CompareTag("SkinLoop"))
        {
            triggerHapticOnGrab.PlayClipSkinLoop(hand);
            UpdateTissueText("Skin");
        }
        if (other.gameObject.CompareTag("SkinPeak"))
        {
            triggerHapticOnGrab.PlayClipSkinPeak(hand);
            UpdateTissueText("Skin");
        }
        if (other.gameObject.CompareTag("SubLoop"))
        {
            triggerHapticOnGrab.PlayClipSubcutLoop(hand);
            UpdateTissueText("Subcutaneous tissue");
        }
        if (other.gameObject.CompareTag("LigamentsLoop"))
        {
            triggerHapticOnGrab.PlayClipLigamentsLoop(hand);
            UpdateTissueText("Supraspinous / Interspinous Ligament");
        }
        if (other.gameObject.CompareTag("LigamentsPeek"))
        {
            triggerHapticOnGrab.PlayClipLigamentsPeak(hand);
            UpdateTissueText("Supraspinous / Interspinous Ligament");
        }
        if (other.gameObject.CompareTag("FlavumLoop"))
        {
            triggerHapticOnGrab.PlayClipFlavumLoop(hand);
            UpdateTissueText("Ligamentum Flavum");
        }
        if (other.gameObject.CompareTag("EpiduralLoop"))
        {
            triggerHapticOnGrab.PlayClipEpiduralLoop(hand);
            UpdateTissueText("Epidural Space");
        }
        if (other.gameObject.CompareTag("DuraLoop"))
        {
            triggerHapticOnGrab.PlayClipDuraLoop(hand);
            UpdateTissueText("Subarachnoid Space (Dura Mater)");
        }
        if (other.gameObject.CompareTag("DuraPeak"))
        {
            triggerHapticOnGrab.PlayClipDuraPeak(hand);
            UpdateTissueText("Subarachnoid Space (Dura Mater)");
        }
        if (other.gameObject.CompareTag("Bone"))
        {            
            UpdateTissueText("Bone");
        }


    }

    public void OnTriggerStay(Collider other)
    {
        if (other.gameObject.CompareTag("SkinLoop"))
        {
            triggerHapticOnGrab.PlayClipSkinLoop(hand);
        }
        if(other.gameObject.CompareTag("SubLoop"))
        {
            triggerHapticOnGrab.PlayClipSubcutLoop(hand);
        }
        if (other.gameObject.CompareTag("LigamentsLoop"))
        {
            triggerHapticOnGrab.PlayClipLigamentsLoop(hand);
        }
        if (other.gameObject.CompareTag("FlavumLoop"))
        {
            triggerHapticOnGrab.PlayClipFlavumLoop(hand);
        }
        if (other.gameObject.CompareTag("EpiduralLoop"))
        {
            triggerHapticOnGrab.PlayClipEpiduralLoop(hand);
        }
        if (other.gameObject.CompareTag("DuraLoop"))
        {
            triggerHapticOnGrab.PlayClipDuraLoop(hand);
            if(primaryButAction.checkCSF.checking)                        
                dropletEmitter.IsDripping = true;                
        }
        if (other.gameObject.CompareTag("Bone"))
        {
            UpdateTissueText("Bone");
        }

    }

    public void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("SkinLoop"))
        {
            triggerHapticOnGrab.StopClipSkinLoop(hand);
        }
        if (other.gameObject.CompareTag("SkinPeak"))
        {
            triggerHapticOnGrab.StopClipSkinPeak(hand);
        }
        if (other.gameObject.CompareTag("SubLoop"))
        {
            triggerHapticOnGrab.StopClipSubcutLoop(hand);
        }
        if (other.gameObject.CompareTag("LigamentsLoop"))
        {
            triggerHapticOnGrab.StopClipLigamentsLoop(hand);
        }
        if (other.gameObject.CompareTag("LigamentsPeek"))
        {
            triggerHapticOnGrab.StopClipLigamentsPeak(hand);
        }
        if (other.gameObject.CompareTag("FlavumLoop"))
        {
            triggerHapticOnGrab.StopClipFlavumLoop(hand);
        }
        if (other.gameObject.CompareTag("EpiduralLoop"))
        {
            triggerHapticOnGrab.StopClipEpiduralLoop(hand);
        }
        if (other.gameObject.CompareTag("DuraLoop"))
        {
            triggerHapticOnGrab.StopClipDuraLoop(hand);
            dropletEmitter.SetDripping(true);
            
        }
        if (other.gameObject.CompareTag("DuraPeak"))
        {
            triggerHapticOnGrab.StopClipDuraPeak(hand);
            dropletEmitter.SetDripping(false);
        }

        // Reset text when pulling needle out to exterior
        if (other.gameObject.CompareTag("SkinLoop") || other.gameObject.CompareTag("SkinPeak"))
        {
            UpdateTissueText("External / Air");
        }
    }

    /// <summary>
    /// Updates the TextMeshPro UI text component safely.
    /// </summary>
    private void UpdateTissueText(string text)
    {
        if (tissueStatusText != null)
        {
            tissueStatusText.text = text;
        }
    }

    void HandleControllerInput(OVRInput.Controller controller, Controller hand)
    {
        /*
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
        */

    }


}
