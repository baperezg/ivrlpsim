using UnityEngine;

public class CSFDropletEmitter : MonoBehaviour
{
    [Header("Droplet Reference")]
    [SerializeField] private Transform dropletTransform;

    [Header("Drip Parameters")]
    [SerializeField] private bool _isDripping = false;

    // Public property to control dripping from other scripts
    public bool IsDripping
    {
        get => _isDripping;
        set => SetDripping(value);
    }

    [SerializeField] private float fallDuration = 0.5f;
    [SerializeField] private float fallDistance = 0.15f;
    [SerializeField] private Vector3 dropletScale = new Vector3(0.004f, 0.004f, 0.004f);

    [Header("Volume Calibration")]
    [SerializeField] private float mlPerDrop = 0.05f;

    [Header("Telemetry")]
    [SerializeField] private int totalDropCount = 0;
    [SerializeField] private float totalVolume = 0;

    public int DropCount => totalDropCount;
    public float TotalVolumeMl => totalDropCount * mlPerDrop;

    private float _timer = 0f;
    private MeshRenderer _dropletRenderer;

    private void Awake()
    {
        if (dropletTransform != null)
        {
            _dropletRenderer = dropletTransform.GetComponent<MeshRenderer>();
            dropletTransform.localScale = dropletScale;
        }

        // Apply initial state
        SetDripping(_isDripping);
    }

    private void Update()
    {
        if (!_isDripping) return;

        _timer += Time.deltaTime;
        float progress = _timer / fallDuration;

        if (progress < 1.0f)
        {
            float fallProgress = progress * progress;
            Vector3 gravityDir = Physics.gravity.normalized;
            dropletTransform.position = transform.position + (gravityDir * (fallDistance * fallProgress));
        }
        else
        {
            totalDropCount++;
            ResetDropPosition();
        }
    }

    /// <summary>
    /// Enables/disables dripping and directly controls the MeshRenderer.
    /// </summary>
    public void SetDripping(bool enable)
    {
        _isDripping = enable;

        if (_dropletRenderer != null)
        {
            _dropletRenderer.enabled = enable;
        }

        if (!enable)
        {
            ResetDropPosition();
        }
    }

    private void ResetDropPosition()
    {
        _timer = 0f;
        if (dropletTransform != null)
        {
            dropletTransform.position = transform.position;
        }
    }

    public void ResetDropCounter() => totalDropCount = 0;

    public float GetTotalVolumeMl()
    {
        totalVolume = totalDropCount * mlPerDrop;
        return totalVolume;
    }

}
