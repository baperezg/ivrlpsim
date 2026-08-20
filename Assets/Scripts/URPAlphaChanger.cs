using UnityEngine;

public class URPAlphaChanger : MonoBehaviour
{
    [Header("Target Configuration")]
    [Tooltip("The main GameObject whose material transparency will be modified. Defaults to this object if empty.")]
    [SerializeField] private GameObject targetGameObject;

    [Tooltip("The second GameObject to toggle ON when transparent, and OFF when opaque.")]
    [SerializeField] private GameObject secondGameObject;

    [Header("Materials")]
    [Tooltip("Material preset with Surface Type set to Opaque.")]
    [SerializeField] private Material opaqueMaterial;

    [Tooltip("Material preset with Surface Type set to Transparent.")]
    [SerializeField] private Material transparentMaterial;

    [Header("Transparency Settings")]
    [Range(0f, 1f)]
    [Tooltip("The target alpha value applied when transparent.")]
    [SerializeField] private float transparentAlpha = 0.5f;

    [Header("Slider toggle")]
    [SerializeField] private GameObject internalAnatomySlider;

    private Renderer targetRenderer;
    private Material runtimeTransparentMaterialInstance;
    private bool isTransparent = false;

    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        InitializeTarget();
    }

    private void Start()
    {
        if (secondGameObject != null)
        {
            secondGameObject.SetActive(isTransparent);
        }
    }

    /// <summary>
    /// Sets up the target object and prepares the transparent material instance.
    /// </summary>
    private void InitializeTarget()
    {
        if (targetGameObject == null)
        {
            targetGameObject = gameObject;
        }

        targetRenderer = targetGameObject.GetComponent<Renderer>();

        if (targetRenderer == null)
        {
            Debug.LogWarning($"[URPAlphaChanger] No Renderer found on '{targetGameObject.name}'!", targetGameObject);
            return;
        }

        // Instantiate a runtime copy of the transparent material so changing alpha doesn't modify the asset on disk
        if (transparentMaterial != null)
        {
            runtimeTransparentMaterialInstance = new Material(transparentMaterial);
        }
    }

    /// <summary>
    /// Toggles material between Opaque and Transparent AND flips the active state of the second object.
    /// </summary>
    public void ToggleTransparency()
    {
        isTransparent = !isTransparent;

        if (isTransparent)
        {
            SetTransparentState();
        }
        else
        {
            SetOpaqueState();
        }

        SetSecondObjectActive(isTransparent);
    }

    private void SetTransparentState()
    {
        if (targetRenderer == null || runtimeTransparentMaterialInstance == null) return;

        // Apply custom alpha to the transparent material instance
        Color currentColor = runtimeTransparentMaterialInstance.GetColor(BaseColorID);
        currentColor.a = transparentAlpha;
        runtimeTransparentMaterialInstance.SetColor(BaseColorID, currentColor);

        // Assign the transparent material instance to the renderer
        targetRenderer.material = runtimeTransparentMaterialInstance;
    }

    private void SetOpaqueState()
    {
        if (targetRenderer == null || opaqueMaterial == null) return;

        // Assign the opaque material to the renderer
        targetRenderer.material = opaqueMaterial;
    }

    private void SetSecondObjectActive(bool active)
    {
        if (secondGameObject != null)
        {
            secondGameObject.SetActive(active);
        }
    }

    public void SetTargetObject(GameObject newTarget)
    {
        targetGameObject = newTarget;
        InitializeTarget();
    }

    public void SetSecondObject(GameObject newSecondObject)
    {
        secondGameObject = newSecondObject;
        SetSecondObjectActive(isTransparent);
    }

    private void OnDestroy()
    {
        // Clean up the instantiated runtime material to prevent memory leaks
        if (runtimeTransparentMaterialInstance != null)
        {
            Destroy(runtimeTransparentMaterialInstance);
        }
    }

    public void ToggleSlider()
    {
        if (internalAnatomySlider.activeSelf)
            internalAnatomySlider.SetActive(false);
        else
            internalAnatomySlider.SetActive(true);
    }
}