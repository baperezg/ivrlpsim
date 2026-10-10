using TMPro;
using UnityEngine;

public class VisualAidsTrajectories : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private VoxelMonteCarlo monteCarloScript;
    //   [SerializeField] private LumbarPunctureMonteCarlo monteCarloScript; // Original line, commented out for clarity

    [SerializeField] private TMP_Dropdown dropdown; // Change to 'Dropdown' if using legacy UI

    private void Awake()
    {
        if (dropdown == null)
        {
            dropdown = GetComponent<TMP_Dropdown>();
        }
    }

    private void Start()
    {
        if (dropdown != null)
        {
            // Sync dropdown with the simulation's current state on start
            dropdown.value = (int)monteCarloScript.currentRenderMode;

            // Subscribe to the value changed event
            dropdown.onValueChanged.AddListener(OnDropdownValueChanged);
        }
    }

    private void OnDestroy()
    {
        if (dropdown != null)
        {
            dropdown.onValueChanged.RemoveListener(OnDropdownValueChanged);
        }
    }

    /// <summary>
    /// Invoked whenever the user selects a new dropdown option.
    /// </summary>
    /// <param name="index">The 0-based index of the chosen option.</param>
    public void OnDropdownValueChanged(int index)
    {
        if (monteCarloScript == null)
        {
            Debug.LogWarning("MonteCarloSimulation reference is missing on " + gameObject.name);
            return;
        }

        // Cast index directly to the enum (0, 1, 2, or 3)
        //LumbarPunctureMonteCarlo.RenderMode selectedMode = (LumbarPunctureMonteCarlo.RenderMode)index;
        VoxelMonteCarlo.RenderMode selectedMode = (VoxelMonteCarlo.RenderMode)index;
        monteCarloScript.SetRenderMode(selectedMode);
    }
}
