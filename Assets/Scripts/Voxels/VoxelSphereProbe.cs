using UnityEngine;

[ExecuteAlways]
[SelectionBase]
public class VoxelSphereProbe : MonoBehaviour
{
    [Header("Anatomy Collider Reference")]
    [Tooltip("Reference to the AnatomyVoxelCollider script on the spine/lumbar model.")]
    public AnatomyVoxelCollider anatomyCollider;

    [Header("Probe Settings")]
    [Range(0.5f, 25f)]
    [Tooltip("Radius of the probe in millimeters (in voxel space).")]
    public float radiusMm = 3.5f;

    [Tooltip("Automatically tint the sphere's material using the color of the contacted tissue.")]
    public bool updateMaterialColor = true;

    [Tooltip("Display an on-screen HUD in the Game and Scene views.")]
    public bool showOnScreenHUD = true;

    [Header("Live Collision Diagnostics")]
    [SerializeField] private bool hasContact;
    [SerializeField] private AnatomyVoxelCollider.TissueType dominantTissue;
    [SerializeField] private AnatomyVoxelCollider.TissueType centerTissue;
    [SerializeField] private int solidVoxelsTouched;
    [SerializeField] private float penetrationDepthMm;
    [SerializeField] private Vector3 contactNormal;

    private MaterialPropertyBlock propertyBlock;
    private Renderer sphereRenderer;

    // Palette matching the voxel definitions
    private static readonly Color[] TissuePalette = new Color[]
    {
        Color.white,                           // Air
        new Color(0.80f, 0.70f, 0.60f, 1.0f),  // SoftTissue
        new Color(0.95f, 0.85f, 0.20f, 1.0f),  // Fat
        new Color(0.90f, 0.65f, 0.55f, 1.0f),  // Skin
        new Color(0.75f, 0.20f, 0.20f, 1.0f),  // Muscle
        new Color(0.20f, 0.70f, 1.00f, 1.0f),  // DuralSac
        new Color(0.90f, 0.90f, 0.50f, 1.0f),  // EpiduralSpace
        new Color(0.30f, 0.80f, 0.50f, 1.0f),  // OtherLigaments
        new Color(0.20f, 0.90f, 0.30f, 1.0f),  // SupraspinousLigament
        new Color(0.10f, 0.60f, 0.20f, 1.0f),  // LigamentumFlavum
        new Color(0.40f, 0.70f, 0.90f, 1.0f),  // IntervertebralDisc
        new Color(0.95f, 0.95f, 0.90f, 1.0f)   // Bone
    };

    void OnEnable()
    {
        sphereRenderer = GetComponent<Renderer>();
        propertyBlock = new MaterialPropertyBlock();

        if (anatomyCollider == null)
            anatomyCollider = FindFirstObjectByType<AnatomyVoxelCollider>();
    }

    void Update()
    {
        if (anatomyCollider == null) return;

        // Query the AnatomyVoxelCollider attached to the spine
        var result = anatomyCollider.CheckSphereCollision(transform.position, radiusMm);

        // Update inspector fields
        hasContact = result.hasContact;
        dominantTissue = result.dominantTissue;
        centerTissue = result.centerTissue;
        solidVoxelsTouched = result.solidVoxelCount;
        penetrationDepthMm = result.penetrationDepth * 1000f; // Convert meters back to mm
        contactNormal = result.contactNormal;

        // Visual feedback on the sphere mesh
        if (updateMaterialColor && sphereRenderer != null)
        {
            Color targetColor = hasContact ? TissuePalette[(int)dominantTissue] : Color.white;
            sphereRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_Color", targetColor);
            propertyBlock.SetColor("_BaseColor", targetColor); // URP support
            sphereRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    void OnDrawGizmos()
    {
        // Draw the probe's bounding radius in scene coordinates
        Gizmos.color = hasContact ? TissuePalette[(int)dominantTissue] : Color.green;
        Gizmos.DrawWireSphere(transform.position, radiusMm * 0.001f);

        // Draw contact normal vector in red
        if (hasContact && contactNormal.sqrMagnitude > 0.01f)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, contactNormal * (radiusMm * 0.0015f));
        }
    }

    void OnGUI()
    {
        if (!showOnScreenHUD) return;

        GUI.Box(new Rect(10, 10, 280, 140), "Sphere Collision Probe");
        GUI.Label(new Rect(20, 35, 260, 20), $"Status: <b>{(hasContact ? "<color=red>CONTACT</color>" : "<color=green>CLEAR (Air)</color>")}</b>");
        GUI.Label(new Rect(20, 55, 260, 20), $"Dominant Tissue: <b>{dominantTissue}</b>");
        GUI.Label(new Rect(20, 75, 260, 20), $"Center Tissue: {centerTissue}");
        GUI.Label(new Rect(20, 95, 260, 20), $"Solid Voxels: {solidVoxelsTouched}");
        GUI.Label(new Rect(20, 115, 260, 20), $"Penetration: {penetrationDepthMm:F2} mm");
    }
}