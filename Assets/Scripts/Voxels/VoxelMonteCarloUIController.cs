using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class VoxelMonteCarloUIController : MonoBehaviour
{
    [Header("Core References")]
    [Tooltip("Reference to the VoxelMonteCarlo planner component.")]
    public VoxelMonteCarlo voxelMonteCarlo;

    [Tooltip("Reference to the VisualAidsTrajectories script managing dropdown visualization.")]
    public VisualAidsTrajectories visualAidsTrajectories;

    [Header("UI Component Bindings")]
    [Tooltip("UI Toggle button to enable/disable Monte Carlo calculations.")]
    public Toggle simulationToggle;

    [Tooltip("UI Dropdown GameObject or container that should be toggled on/off.")]
    public GameObject dropdownContainer;

    [Tooltip("Optional direct TMP_Dropdown reference if controlling interactability instead of GameObject active state.")]
    public TMP_Dropdown trajectoryDropdown;

    [Header("Behavior Settings")]
    [Tooltip("Automatically re-run Monte Carlo simulation each time the toggle is turned ON.")]
    public bool recalculateOnEnable = true;

    [Tooltip("Disable dropdown interactability instead of hiding its GameObject.")]
    public bool disableDropdownInsteadOfHiding = false;

    private void Awake()
    {
        if (voxelMonteCarlo == null)
            voxelMonteCarlo = FindFirstObjectByType<VoxelMonteCarlo>();

        if (visualAidsTrajectories == null)
            visualAidsTrajectories = FindFirstObjectByType<VisualAidsTrajectories>();

        if (simulationToggle == null)
            simulationToggle = GetComponent<Toggle>();
    }

    private void OnEnable()
    {
        if (simulationToggle != null)
        {
            simulationToggle.onValueChanged.AddListener(OnToggleValueChanged);
            // Apply initial state
            OnToggleValueChanged(simulationToggle.isOn);
        }
    }

    private void OnDisable()
    {
        if (simulationToggle != null)
        {
            simulationToggle.onValueChanged.RemoveListener(OnToggleValueChanged);
        }
    }

    /// <summary>
    /// Handles state changes triggered by the UI Toggle.
    /// </summary>
    public void OnToggleValueChanged(bool isEnabled)
    {
        if (voxelMonteCarlo == null) return;

        if (isEnabled)
        {
            // 1. Run simulation calculations if configured or if no paths exist yet
            if (recalculateOnEnable || voxelMonteCarlo.Trajectories.Count == 0)
            {
                voxelMonteCarlo.ExecuteSimulation();
            }

            // 2. Restore trajectory line visualization via current dropdown selection
            SetDropdownVisibility(true);
            SyncVisualAidsState();
        }
        else
        {
            // 1. Hide trajectory line rendering in Game View and Gizmos
            voxelMonteCarlo.SetRenderMode(VoxelMonteCarlo.RenderMode.DoNotRender);

            // 2. Hide or disable the dropdown selector UI
            SetDropdownVisibility(false);
        }
    }

    private void SetDropdownVisibility(bool visible)
    {
        if (dropdownContainer != null && !disableDropdownInsteadOfHiding)
        {
            dropdownContainer.SetActive(visible);
        }
        else if (trajectoryDropdown != null)
        {
            if (disableDropdownInsteadOfHiding)
            {
                trajectoryDropdown.interactable = visible;
            }
            else
            {
                trajectoryDropdown.gameObject.SetActive(visible);
            }
        }
    }

    private void SyncVisualAidsState()
    {
        if (visualAidsTrajectories != null)
        {
            // Triggers VisualAidsTrajectories to refresh the line render mode
            visualAidsTrajectories.gameObject.SendMessage("ApplyCurrentSelection", SendMessageOptions.DontRequireReceiver);
        }
        else if (trajectoryDropdown != null)
        {
            // Fallback: Map dropdown index directly to VoxelMonteCarlo.RenderMode
            voxelMonteCarlo.SetRenderMode((VoxelMonteCarlo.RenderMode)trajectoryDropdown.value);
        }
        else
        {
            voxelMonteCarlo.SetRenderMode(VoxelMonteCarlo.RenderMode.ShowAll);
        }
    }
}