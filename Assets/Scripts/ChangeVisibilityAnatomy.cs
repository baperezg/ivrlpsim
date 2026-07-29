using System.Collections.Generic;
using UnityEngine;

public class ChangeVisibilityAnatomy : MonoBehaviour
{

    [Header("Anatomy Layers (Ordered Outer to Inner)")]
    [Tooltip("Add your anatomy GameObjects in order: Subcutaneous, Muscle, Supraspinous, Interspinous, Flavum, Epidural, Dura, Vertebrae.")]
    [SerializeField] private List<GameObject> anatomyTissues = new List<GameObject>();

    private int currentVisibleCount = -1;

    public void OnSliderValueChanged(float sliderValue)
    {
        if (anatomyTissues.Count == 0) return;

        // Clamp input value safety check
        sliderValue = Mathf.Clamp01(sliderValue);

        // Map normalized value (0.0 to 1.0) to discrete steps (0 to total list count)
        int targetStep = Mathf.RoundToInt(sliderValue * anatomyTissues.Count);

        // Update active states only when shifting to a new step
        if (targetStep != currentVisibleCount)
        {
            UpdateTissueVisibility(targetStep);
        }
    }

    private void UpdateTissueVisibility(int visibleCount)
    {
        currentVisibleCount = visibleCount;

        for (int i = 0; i < anatomyTissues.Count; i++)
        {
            if (anatomyTissues[i] != null)
            {
                // Shows tissues up to the current step index, hides the rest
                anatomyTissues[i].SetActive(i < visibleCount);
            }
        }
    }
}
