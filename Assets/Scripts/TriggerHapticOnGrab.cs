using UnityEngine;
using Oculus.Haptics;
using System;

public class TriggerHapticOnGrab : MonoBehaviour
{

    [SerializeField] private HapticClip clipSkinLoop; //1
    [SerializeField] private HapticClip clipSkinPeak; //2
    [SerializeField] private HapticClip clipSubcutLoop; //3
    [SerializeField] private HapticClip clipLigamentsLoop; //4
    [SerializeField] private HapticClip clipLigamentsPeak; //5
    [SerializeField] private HapticClip clipFlavumLoop; //6
    [SerializeField] private HapticClip clipEpiduralLoop; //7
    [SerializeField] private HapticClip clipDuraLoop; //8
    [SerializeField] private HapticClip clipDuraPeak; //9



    //One player for each controller
    //The initial version goes with only one controller
    
    //private HapticClipPlayer leftClipPlayer1;
    //private HapticClipPlayer leftClipPlayer2;

    private HapticClipPlayer rightClipPlayerSkinLoop; //1
    private HapticClipPlayer rightClipPlayerSkinPeak; //2
    private HapticClipPlayer rightClipPlayerSubcutLoop; //3
    private HapticClipPlayer rightClipPlayerLigamentsLoop; //4
    private HapticClipPlayer rightClipPlayerLigamentsPeak; //5
    private HapticClipPlayer rightClipPlayerFlavumLoop; //6
    private HapticClipPlayer rightClipPlayerEpiduralLoop; //7
    private HapticClipPlayer rightClipPlayerDuraLoop; //8
    private HapticClipPlayer rightClipPlayerDuraPeak;  //9

    //Clips with loop    

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    protected virtual void Start()
    {
        // We create two haptic clip players for each hand.
        //leftClipPlayer1 = new HapticClipPlayer(clip1);
        //leftClipPlayer2 = new HapticClipPlayer(clip2);
        rightClipPlayerSkinLoop = new HapticClipPlayer(clipSkinLoop); //1
        rightClipPlayerSkinPeak = new HapticClipPlayer(clipSkinPeak); //2
        rightClipPlayerSubcutLoop = new HapticClipPlayer(clipSkinPeak); //3
        rightClipPlayerLigamentsLoop = new HapticClipPlayer(clipLigamentsLoop); //4
        rightClipPlayerLigamentsPeak = new HapticClipPlayer(clipLigamentsPeak); //5
        rightClipPlayerFlavumLoop = new HapticClipPlayer(clipFlavumLoop); //6
        rightClipPlayerEpiduralLoop = new HapticClipPlayer(clipEpiduralLoop); //7
        rightClipPlayerDuraLoop = new HapticClipPlayer(clipDuraLoop);//8
        rightClipPlayerDuraPeak = new HapticClipPlayer(clipDuraPeak);//9

        // We increase the priority for the following players on both hands.
        rightClipPlayerDuraPeak.priority = 1;
        rightClipPlayerDuraLoop.priority = 2;
        rightClipPlayerEpiduralLoop.priority = 3;
        rightClipPlayerFlavumLoop.priority = 4;
        rightClipPlayerLigamentsPeak.priority = 5;
        rightClipPlayerLigamentsLoop.priority = 6;
        rightClipPlayerSubcutLoop.priority = 7;
        rightClipPlayerSkinPeak.priority = 8;
        rightClipPlayerSkinLoop.priority = 9;

        //Clips looping
        rightClipPlayerDuraLoop.isLooping = true;
        rightClipPlayerEpiduralLoop.isLooping = true;
        rightClipPlayerFlavumLoop.isLooping= true;
        rightClipPlayerLigamentsLoop.isLooping = true;
        rightClipPlayerSubcutLoop.isLooping = true;
        rightClipPlayerSkinLoop.isLooping = true;
    }

    //1 Skin Loop Play/Stop/Loop ////////////////////////////////////////////////////////////////
    //Play clip
    public void PlayClipSkinLoop(Controller hand)
    {
        switch (hand)
        {
            case Controller.Right:
                rightClipPlayerSkinLoop.Play(Controller.Right);
                break;
            //case Controller.Left:
            //    leftClipPlayerSkinLoop.Play(Controller.Left);
            //    break;
            default:
                //Debug.LogWarning("Input hand not mapped for: " + hand);
                break;
        }
        //Debug.Log("Should feel vibration from ClipSkinLoop on " + hand + " controller.");
    }

    //Stop clip
    public void StopClipSkinLoop(Controller hand)
    {
        switch (hand)
        {
            case Controller.Right:
                rightClipPlayerSkinLoop.Stop();
                break;
            //case Controller.Left:
            //    leftClipPlayerSkinLoop.Stop();
            //    break;
            default:
                //Debug.LogWarning("Input hand not mapped for: " + hand);
                break;
        }
        //Debug.Log("Vibration from clipPlayer1 should stop on hand " + hand + ".");
    }

    //Stop looping clip
    public void SetLoopingOfClipSkinLoop(Controller hand)
    {
        switch (hand)
        {
            case Controller.Right:
                rightClipPlayerSkinLoop.isLooping = !rightClipPlayerSkinLoop.isLooping;
                //Debug.Log(String.Format("Looping should be {0} on " + hand + " controller.", rightClipPlayerSkinLoop.isLooping));
                break;
            //case Controller.Left:
            //    leftClipPlayerSkinLoop.isLooping = !leftClipPlayerSkinLoop.isLooping;
            //    //Debug.Log(String.Format("Looping should be {0} on " + hand + " controller.", leftClipPlayer1.isLooping));
            //    break;
            default:
                //Debug.LogWarning("Input hand not mapped for: " + hand);
                break;
        }
    }

    /// <summary>
    /// Modulates amplitude and frequency of first clip, the x axis manages the frequency, the y axis the amplitude.
    /// </summary>
    /// <param name="hand"></param>
    /// <param name="input"></param>
    public void ModulateAmplitudeAndFrequencyOfClipSkinLoop(Controller hand, Vector2 input)
    {
        switch (hand)
        {
            case Controller.Right:
                rightClipPlayerSkinLoop.amplitude = input.y;
                rightClipPlayerSkinLoop.frequencyShift = input.x;
                break;
            //case Controller.Left:
            //    leftClipPlayerSkinLoop.amplitude = input.y;
            //    leftClipPlayerSkinLoop.frequencyShift = input.x;
            //    break;
            default:
                //Debug.LogWarning("Input hand not mapped for: " + hand);
                break;
        }
    }

    //2 Skin Peak Play/Stop/Loop ////////////////////////////////////////////////////////////////
    //Play clip
    public void PlayClipSkinPeak(Controller hand)
    {
        switch (hand)
        {
            case Controller.Right:
                rightClipPlayerSkinPeak.Play(Controller.Right);
                break;
            //case Controller.Left:
            //    leftClipPlayerSkinLoop.Play(Controller.Left);
            //    break;
            default:
                //Debug.LogWarning("Input hand not mapped for: " + hand);
                break;
        }
        //Debug.Log("Should feel vibration from ClipSkinLoop on " + hand + " controller.");
    }

    //Stop clip
    public void StopClipSkinPeak(Controller hand)
    {
        switch (hand)
        {
            case Controller.Right:
                rightClipPlayerSkinPeak.Stop();
                break;
            //case Controller.Left:
            //    leftClipPlayerSkinLoop.Stop();
            //    break;
            default:
                //Debug.LogWarning("Input hand not mapped for: " + hand);
                break;
        }
        //Debug.Log("Vibration from clipPlayer1 should stop on hand " + hand + ".");
    }

    //Stop looping clip
    public void SetLoopingOfClipSkinPeak(Controller hand)
    {
        switch (hand)
        {
            case Controller.Right:
                rightClipPlayerSkinPeak.isLooping = !rightClipPlayerSkinPeak.isLooping;
                //Debug.Log(String.Format("Looping should be {0} on " + hand + " controller.", rightClipPlayerSkinPeak.isLooping));
                break;
            //case Controller.Left:
            //    leftClipPlayerSkinPeak.isLooping = !leftClipPlayerSkinPeak.isLooping;
            //    //Debug.Log(String.Format("Looping should be {0} on " + hand + " controller.", leftClipPlayerSkinPeak.isLooping));
            //    break;
            default:
                //Debug.LogWarning("Input hand not mapped for: " + hand);
                break;
        }
    }

    /// <summary>
    /// Modulates amplitude and frequency of first clip, the x axis manages the frequency, the y axis the amplitude.
    /// </summary>
    /// <param name="hand"></param>
    /// <param name="input"></param>
    public void ModulateAmplitudeAndFrequencyOfSkinPeak(Controller hand, Vector2 input)
    {
        switch (hand)
        {
            case Controller.Right:
                rightClipPlayerSkinPeak.amplitude = input.y;
                rightClipPlayerSkinPeak.frequencyShift = input.x;
                break;
            //case Controller.Left:
            //    leftClipPlayerSkinPeak.amplitude = input.y;
            //    leftClipPlayerSkinPeak.frequencyShift = input.x;
            //    break;
            default:
                //Debug.LogWarning("Input hand not mapped for: " + hand);
                break;
        }
    }

    //3 Subcut Loop Play/Stop/Loop ////////////////////////////////////////////////////////////////
    //Play clip
    public void PlayClipSubcutLoop(Controller hand)
    {
        switch (hand)
        {
            case Controller.Right:
                rightClipPlayerSubcutLoop.Play(Controller.Right);
                break;
            //case Controller.Left:
            //    leftClipPlayerSubcutLoop.Play(Controller.Left);
            //    break;
            default:
                //Debug.LogWarning("Input hand not mapped for: " + hand);
                break;
        }
        //Debug.Log("Should feel vibration from ClipSubcutLoop on " + hand + " controller.");
    }

    //Stop clip
    public void StopClipSubcutLoop(Controller hand)
    {
        switch (hand)
        {
            case Controller.Right:
                rightClipPlayerSubcutLoop.Stop();
                break;
            //case Controller.Left:
            //    leftClipPlayerSubcutLoop.Stop();
            //    break;
            default:
                //Debug.LogWarning("Input hand not mapped for: " + hand);
                break;
        }
        //Debug.Log("Vibration from ClipPlayerSubcutLoop should stop on hand " + hand + ".");
    }

    //Stop looping clip
    public void SetLoopingOfClipSubcutLoop(Controller hand)
    {
        switch (hand)
        {
            case Controller.Right:
                rightClipPlayerSubcutLoop.isLooping = !rightClipPlayerSubcutLoop.isLooping;
                //Debug.Log(String.Format("Looping should be {0} on " + hand + " controller.", rightClipPlayerSubcutLoop.isLooping));
                break;
            //case Controller.Left:
            //    leftClipPlayerSubcutLoop.isLooping = !leftClipPlayerSubcutLoopk.isLooping;
            //    //Debug.Log(String.Format("Looping should be {0} on " + hand + " controller.", leftClipPlayerSkinPeak.isLooping));
            //    break;
            default:
                //Debug.LogWarning("Input hand not mapped for: " + hand);
                break;
        }
    }

    /// <summary>
    /// Modulates amplitude and frequency of first clip, the x axis manages the frequency, the y axis the amplitude.
    /// </summary>
    /// <param name="hand"></param>
    /// <param name="input"></param>
    public void ModulateAmplitudeAndFrequencyOfSubcutLoop(Controller hand, Vector2 input)
    {
        switch (hand)
        {
            case Controller.Right:
                rightClipPlayerSubcutLoop.amplitude = input.y;
                rightClipPlayerSubcutLoop.frequencyShift = input.x;
                break;
            //case Controller.Left:
            //    leftClipPlayerSubcutLoop.amplitude = input.y;
            //    leftClipPlayerSubcutLoop.frequencyShift = input.x;
            //    break;
            default:
                //Debug.LogWarning("Input hand not mapped for: " + hand);
                break;
        }
    }


    //4 Ligaments Loop Play/Stop/Loop ////////////////////////////////////////////////////////////////
    //Play clip
    public void PlayClipLigamentsLoop(Controller hand)
    {
        switch (hand)
        {
            case Controller.Right:
                rightClipPlayerLigamentsLoop.Play(Controller.Right);
                break;
            //case Controller.Left:
            //    leftClipPlayerLigamentsLoop.Play(Controller.Left);
            //    break;
            default:
                //Debug.LogWarning("Input hand not mapped for: " + hand);
                break;
        }
        //Debug.Log("Should feel vibration from ClipSkinLoop on " + hand + " controller.");
    }

    //Stop clip
    public void StopClipLigamentsLoop(Controller hand)
    {
        switch (hand)
        {
            case Controller.Right:
                rightClipPlayerLigamentsLoop.Stop();
                break;
            //case Controller.Left:
            //    leftClipPlayerLigamentsLoop.Stop();
            //    break;
            default:
                //Debug.LogWarning("Input hand not mapped for: " + hand);
                break;
        }
        //Debug.Log("Vibration from clipPlayer1 should stop on hand " + hand + ".");
    }

    //Stop looping clip
    public void SetLoopingOfClipLigamentsLoop(Controller hand)
    {
        switch (hand)
        {
            case Controller.Right:
                rightClipPlayerLigamentsLoop.isLooping = !rightClipPlayerLigamentsLoop.isLooping;
                //Debug.Log(String.Format("Looping should be {0} on " + hand + " controller.", rightClipPlayerLigamentsLoop.isLooping));
                break;
            //case Controller.Left:
            //    leftClipPlayerLigamentsLoop.isLooping = !leftClipPlayerLigamentsLoop.isLooping;
            //    //Debug.Log(String.Format("Looping should be {0} on " + hand + " controller.", leftClipPlayerLigamentsLoop.isLooping));
            //    break;
            default:
                //Debug.LogWarning("Input hand not mapped for: " + hand);
                break;
        }
    }

    /// <summary>
    /// Modulates amplitude and frequency of first clip, the x axis manages the frequency, the y axis the amplitude.
    /// </summary>
    /// <param name="hand"></param>
    /// <param name="input"></param>
    public void ModulateAmplitudeAndFrequencyOfLigamentstLoop(Controller hand, Vector2 input)
    {
        switch (hand)
        {
            case Controller.Right:
                rightClipPlayerLigamentsLoop.amplitude = input.y;
                rightClipPlayerLigamentsLoop.frequencyShift = input.x;
                break;
            //case Controller.Left:
            //    leftClipPlayerLigamentsLoop.amplitude = input.y;
            //    leftClipPlayerLigamentsLoop.frequencyShift = input.x;
            //    break;
            default:
                //Debug.LogWarning("Input hand not mapped for: " + hand);
                break;
        }
    }


    //5 Ligaments Peak Play/Stop/Loop ////////////////////////////////////////////////////////////////
    //Play clip
    public void PlayClipLigamentsPeak(Controller hand)
    {
        switch (hand)
        {
            case Controller.Right:
                rightClipPlayerLigamentsPeak.Play(Controller.Right);
                break;
            //case Controller.Left:
            //    leftClipPlayerLigamentsPeak.Play(Controller.Left);
            //    break;
            default:
                //Debug.LogWarning("Input hand not mapped for: " + hand);
                break;
        }
        //Debug.Log("Should feel vibration from LigamentsPeak on " + hand + " controller.");
    }

    //Stop clip
    public void StopClipLigamentsPeak(Controller hand)
    {
        switch (hand)
        {
            case Controller.Right:
                rightClipPlayerLigamentsPeak.Stop();
                break;
            //case Controller.Left:
            //    leftClipPlayerLigamentsPeak.Stop();
            //    break;
            default:
                //Debug.LogWarning("Input hand not mapped for: " + hand);
                break;
        }
        //Debug.Log("Vibration from clipPlayer1 should stop on hand " + hand + ".");
    }

    //Stop looping clip
    public void SetLoopingOfClipLigamentsPeak(Controller hand)
    {
        switch (hand)
        {
            case Controller.Right:
                rightClipPlayerLigamentsPeak.isLooping = !rightClipPlayerLigamentsPeak.isLooping;
                //Debug.Log(String.Format("Looping should be {0} on " + hand + " controller.", rightClipPlayerLigamentsPeak.isLooping));
                break;
            //case Controller.Left:
            //    leftClipPlayerLigamentsPeak.isLooping = !leftClipPlayerLigamentsPeak.isLooping;
            //    //Debug.Log(String.Format("Looping should be {0} on " + hand + " controller.", leftClipPlayerLigamentsPeak.isLooping));
            //    break;
            default:
                //Debug.LogWarning("Input hand not mapped for: " + hand);
                break;
        }
    }

    /// <summary>
    /// Modulates amplitude and frequency of first clip, the x axis manages the frequency, the y axis the amplitude.
    /// </summary>
    /// <param name="hand"></param>
    /// <param name="input"></param>
    public void ModulateAmplitudeAndFrequencyOfLigamentstPeak(Controller hand, Vector2 input)
    {
        switch (hand)
        {
            case Controller.Right:
                rightClipPlayerLigamentsPeak.amplitude = input.y;
                rightClipPlayerLigamentsPeak.frequencyShift = input.x;
                break;
            //case Controller.Left:
            //    leftClipPlayerLigamentsPeak.amplitude = input.y;
            //    leftClipPlayerLigamentsPeak.frequencyShift = input.x;
            //    break;
            default:
                //Debug.LogWarning("Input hand not mapped for: " + hand);
                break;
        }
    }

    //6 Flavum Loop Play/Stop/Loop ////////////////////////////////////////////////////////////////
    //Play clip
    public void PlayClipFlavumLoop(Controller hand)
    {
        switch (hand)
        {
            case Controller.Right:
                rightClipPlayerFlavumLoop.Play(Controller.Right);
                break;
            //case Controller.Left:
            //    leftClipPlayerFlavumLoop.Play(Controller.Left);
            //    break;
            default:
                //Debug.LogWarning("Input hand not mapped for: " + hand);
                break;
        }
        //Debug.Log("Should feel vibration from FlavumLoop on " + hand + " controller.");
    }

    //Stop clip
    public void StopClipFlavumLoop(Controller hand)
    {
        switch (hand)
        {
            case Controller.Right:
                rightClipPlayerFlavumLoop.Stop();
                break;
            //case Controller.Left:
            //    leftClipPlayerFlavumLoop.Stop();
            //    break;
            default:
                //Debug.LogWarning("Input hand not mapped for: " + hand);
                break;
        }
        //Debug.Log("Vibration from FlavumLoop should stop on hand " + hand + ".");
    }

    //Stop looping clip
    public void SetLoopingOfClipFlavumLoop(Controller hand)
    {
        switch (hand)
        {
            case Controller.Right:
                rightClipPlayerFlavumLoop.isLooping = !rightClipPlayerFlavumLoop.isLooping;
                //Debug.Log(String.Format("Looping should be {0} on " + hand + " controller.", rightClipPlayerFlavumLoop.isLooping));
                break;
            //case Controller.Left:
            //    leftClipPlayerFlavumLoop.isLooping = !leftClipPlayerFlavumLoop.isLooping;
            //    //Debug.Log(String.Format("Looping should be {0} on " + hand + " controller.", leftClipPlayerFlavumLoop.isLooping));
            //    break;
            default:
                //Debug.LogWarning("Input hand not mapped for: " + hand);
                break;
        }
    }

    /// <summary>
    /// Modulates amplitude and frequency of first clip, the x axis manages the frequency, the y axis the amplitude.
    /// </summary>
    /// <param name="hand"></param>
    /// <param name="input"></param>
    public void ModulateAmplitudeAndFrequencyOfFlavumLoop(Controller hand, Vector2 input)
    {
        switch (hand)
        {
            case Controller.Right:
                rightClipPlayerFlavumLoop.amplitude = input.y;
                rightClipPlayerFlavumLoop.frequencyShift = input.x;
                break;
            //case Controller.Left:
            //    leftClipPlayerFlavumLoop.amplitude = input.y;
            //    leftClipPlayerFlavumLoop.frequencyShift = input.x;
            //    break;
            default:
                //Debug.LogWarning("Input hand not mapped for: " + hand);
                break;
        }
    }

    //7 Epidural Loop Play/Stop/Loop ////////////////////////////////////////////////////////////////
    //Play clip
    public void PlayClipEpiduralLoop(Controller hand)
    {
        switch (hand)
        {
            case Controller.Right:
                rightClipPlayerEpiduralLoop.Play(Controller.Right);
                break;
            //case Controller.Left:
            //    leftClipPlayerEpiduralLoop.Play(Controller.Left);
            //    break;
            default:
                //Debug.LogWarning("Input hand not mapped for: " + hand);
                break;
        }
        //Debug.Log("Should feel vibration from EpiduralLoop on " + hand + " controller.");
    }

    //Stop clip
    public void StopClipEpiduralLoop(Controller hand)
    {
        switch (hand)
        {
            case Controller.Right:
                rightClipPlayerEpiduralLoop.Stop();
                break;
            //case Controller.Left:
            //    leftClipPlayerEpiduralLoop.Stop();
            //    break;
            default:
                //Debug.LogWarning("Input hand not mapped for: " + hand);
                break;
        }
        //Debug.Log("Vibration from EpiduralLoop should stop on hand " + hand + ".");
    }

    //Stop looping clip
    public void SetLoopingOfClipEpiduralLoop(Controller hand)
    {
        switch (hand)
        {
            case Controller.Right:
                rightClipPlayerEpiduralLoop.isLooping = !rightClipPlayerEpiduralLoop.isLooping;
                //Debug.Log(String.Format("Looping should be {0} on " + hand + " controller.", rightClipPlayerEpiduralLoop.isLooping));
                break;
            //case Controller.Left:
            //    leftClipPlayerEpiduralLoop.isLooping = !leftClipPlayerEpiduralLoop.isLooping;
            //    //Debug.Log(String.Format("Looping should be {0} on " + hand + " controller.", leftClipPlayerEpiduralLoop.isLooping));
            //    break;
            default:
                //Debug.LogWarning("Input hand not mapped for: " + hand);
                break;
        }
    }

    /// <summary>
    /// Modulates amplitude and frequency of first clip, the x axis manages the frequency, the y axis the amplitude.
    /// </summary>
    /// <param name="hand"></param>
    /// <param name="input"></param>
    public void ModulateAmplitudeAndFrequencyOfEpiduralLoop(Controller hand, Vector2 input)
    {
        switch (hand)
        {
            case Controller.Right:
                rightClipPlayerEpiduralLoop.amplitude = input.y;
                rightClipPlayerEpiduralLoop.frequencyShift = input.x;
                break;
            //case Controller.Left:
            //    leftClipPlayerEpiduralLoop.amplitude = input.y;
            //    leftClipPlayerEpiduralLoop.frequencyShift = input.x;
            //    break;
            default:
                //Debug.LogWarning("Input hand not mapped for: " + hand);
                break;
        }
    }

    //8 Dura Loop Play/Stop/Loop ////////////////////////////////////////////////////////////////
    //Play clip
    public void PlayClipDuraLoop(Controller hand)
    {
        switch (hand)
        {
            case Controller.Right:
                rightClipPlayerDuraLoop.Play(Controller.Right);
                break;
            //case Controller.Left:
            //    leftClipPlayerDuraLoop.Play(Controller.Left);
            //    break;
            default:
                //Debug.LogWarning("Input hand not mapped for: " + hand);
                break;
        }
        //Debug.Log("Should feel vibration from DuraLoop on " + hand + " controller.");
    }

    //Stop clip
    public void StopClipDuraLoop(Controller hand)
    {
        switch (hand)
        {
            case Controller.Right:
                rightClipPlayerDuraLoop.Stop();
                break;
            //case Controller.Left:
            //    leftClipPlayerDuraLoop.Stop();
            //    break;
            default:
                //Debug.LogWarning("Input hand not mapped for: " + hand);
                break;
        }
        //Debug.Log("Vibration from DuraLoop should stop on hand " + hand + ".");
    }

    //Stop looping clip
    public void SetLoopingOfClipDuraLoop(Controller hand)
    {
        switch (hand)
        {
            case Controller.Right:
                rightClipPlayerDuraLoop.isLooping = !rightClipPlayerDuraLoop.isLooping;
                //Debug.Log(String.Format("Looping should be {0} on " + hand + " controller.", rightClipPlayerDuraLoop.isLooping));
                break;
            //case Controller.Left:
            //    leftClipPlayerDuraLoop.isLooping = !leftClipPlayerDuraLoop.isLooping;
            //    //Debug.Log(String.Format("Looping should be {0} on " + hand + " controller.", leftClipPlayerDuraLoop.isLooping));
            //    break;
            default:
                //Debug.LogWarning("Input hand not mapped for: " + hand);
                break;
        }
    }

    /// <summary>
    /// Modulates amplitude and frequency of first clip, the x axis manages the frequency, the y axis the amplitude.
    /// </summary>
    /// <param name="hand"></param>
    /// <param name="input"></param>
    public void ModulateAmplitudeAndFrequencyOfDuraLoop(Controller hand, Vector2 input)
    {
        switch (hand)
        {
            case Controller.Right:
                rightClipPlayerDuraLoop.amplitude = input.y;
                rightClipPlayerDuraLoop.frequencyShift = input.x;
                break;
            //case Controller.Left:
            //    leftClipPlayerDuraLoop.amplitude = input.y;
            //    leftClipPlayerDuraLoop.frequencyShift = input.x;
            //    break;
            default:
                //Debug.LogWarning("Input hand not mapped for: " + hand);
                break;
        }
    }

    //9 Dura Peak Play/Stop/Loop ////////////////////////////////////////////////////////////////
    //Play clip
    public void PlayClipDuraPeak(Controller hand)
    {
        switch (hand)
        {
            case Controller.Right:
                rightClipPlayerDuraPeak.Play(Controller.Right);
                break;
            //case Controller.Left:
            //    leftClipPlayerDuraPeak.Play(Controller.Left);
            //    break;
            default:
                //Debug.LogWarning("Input hand not mapped for: " + hand);
                break;
        }
        //Debug.Log("Should feel vibration from DuraPeak on " + hand + " controller.");
    }

    //Stop clip
    public void StopClipDuraPeak(Controller hand)
    {
        switch (hand)
        {
            case Controller.Right:
                rightClipPlayerDuraPeak.Stop();
                break;
            //case Controller.Left:
            //    leftClipPlayerDuraPeak.Stop();
            //    break;
            default:
                //Debug.LogWarning("Input hand not mapped for: " + hand);
                break;
        }
        //Debug.Log("Vibration from DuraPeak should stop on hand " + hand + ".");
    }

    //Stop looping clip
    public void SetLoopingOfClipDuraPeak(Controller hand)
    {
        switch (hand)
        {
            case Controller.Right:
                rightClipPlayerDuraPeak.isLooping = !rightClipPlayerDuraPeak.isLooping;
                //Debug.Log(String.Format("Looping should be {0} on " + hand + " controller.", rightClipPlayerDuraPeak.isLooping));
                break;
            //case Controller.Left:
            //    leftClipPlayerDuraPeak.isLooping = !leftClipPlayerDuraPeak.isLooping;
            //    //Debug.Log(String.Format("Looping should be {0} on " + hand + " controller.", leftClipPlayerDuraPeak.isLooping));
            //    break;
            default:
                //Debug.LogWarning("Input hand not mapped for: " + hand);
                break;
        }
    }

    /// <summary>
    /// Modulates amplitude and frequency of first clip, the x axis manages the frequency, the y axis the amplitude.
    /// </summary>
    /// <param name="hand"></param>
    /// <param name="input"></param>
    public void ModulateAmplitudeAndFrequencyOfDuraPeak(Controller hand, Vector2 input)
    {
        switch (hand)
        {
            case Controller.Right:
                rightClipPlayerDuraPeak.amplitude = input.y;
                rightClipPlayerDuraPeak.frequencyShift = input.x;
                break;
            //case Controller.Left:
            //    leftClipPlayerDuraPeak.amplitude = input.y;
            //    leftClipPlayerDuraPeak.frequencyShift = input.x;
            //    break;
            default:
                //Debug.LogWarning("Input hand not mapped for: " + hand);
                break;
        }
    }


    protected virtual void OnDestroy()
    {
        rightClipPlayerDuraPeak?.Dispose();
        rightClipPlayerDuraLoop?.Dispose();
        rightClipPlayerEpiduralLoop?.Dispose();
        rightClipPlayerFlavumLoop?.Dispose();
        rightClipPlayerLigamentsPeak?.Dispose();
        rightClipPlayerLigamentsLoop?.Dispose(); ;
        rightClipPlayerSubcutLoop?.Dispose();
        rightClipPlayerSkinPeak?.Dispose();
        rightClipPlayerSkinLoop?.Dispose();

    }

    // Upon exiting the application (or when playmode is stopped) we release the haptic clip players and uninitialize (dispose) the SDK.
    protected virtual void OnApplicationQuit()
    {
        Haptics.Instance.Dispose();
    }
}
