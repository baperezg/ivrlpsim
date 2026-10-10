using System;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class VoxelCollisionTester : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Root transform of the lumbar anatomy model in the scene")]
    public Transform anatomicalModel;

    [Tooltip("Sign per axis between Unity world and LPS space (glTFast inverts X)")]
    public Vector3 signsLPS = new Vector3(-1f, 1f, 1f);

    [Header("Probe Sphere Settings")]
    [Tooltip("Radius of the probe sphere in millimeters (LPS coordinate space)")]
    [Range(0.5f, 25f)]
    public float radiusMm = 3.0f;

    [Tooltip("Number of sample points distributed across the sphere surface")]
    [Range(6, 64)]
    public int surfaceSampleCount = 18;

    [Header("Real-Time Diagnostics")]
    [SerializeField] private Tissue centerTissue = Tissue.Air;
    [SerializeField] private Vector3 positionLpsMm;
    [SerializeField] private bool isInsideVolume = false;
    [SerializeField] private int solidVoxelCount = 0;

    private TissueMap tissueMap;
    private Matrix4x4 worldToLps;
    private Matrix4x4 lpsToWorld;

    // Visual debugging palette aligned with anatomical tissue classes
    private static readonly Color[] TissueColors = new Color[]
    {
        Color.clear,                          // Air (Tejido.Aire)
        new Color(0.8f, 0.7f, 0.6f),          // Soft Tissue
        new Color(0.95f, 0.85f, 0.2f),        // Subcutaneous Fat
        new Color(0.9f, 0.65f, 0.55f),        // Skin
        new Color(0.75f, 0.2f, 0.2f),         // Muscle
        new Color(0.2f, 0.7f, 1.0f),          // Dural Sac
        new Color(0.9f, 0.9f, 0.5f),          // Epidural Space
        new Color(0.3f, 0.8f, 0.5f),          // Other Ligaments
        new Color(0.2f, 0.9f, 0.3f),          // Supraspinous Ligament
        new Color(0.1f, 0.6f, 0.2f),          // Ligamentum Flavum
        new Color(0.4f, 0.7f, 0.9f),          // Intervertebral Disc
        new Color(0.95f, 0.95f, 0.9f),        // Bone
    };

    void OnEnable()
    {
        LoadTissueMap();
        UpdateCoordinateMatrices();
    }

    void Update()
    {
        if (tissueMap == null) return;

        UpdateCoordinateMatrices();
        EvaluateSphereCollision();
    }

    public void LoadTissueMap()
    {
        try
        {
            string defaultPath = TissueMap.RutaPorDefecto;
            tissueMap = TissueMap.Cargar(defaultPath);
            Debug.Log($"[VoxelCollisionTester] Tissue map loaded: {tissueMap.nx}x{tissueMap.ny}x{tissueMap.nz} voxels, pitch: {tissueMap.espaciado} mm");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[VoxelCollisionTester] Failed to load tissue voxel map: {ex.Message}");
            tissueMap = null;
        }
    }

    private void UpdateCoordinateMatrices()
    {
        // Use the root transform of LPanatomy (incorporates position & rotation)
        Transform refTransform = anatomicalModel != null ? anatomicalModel : transform;

        // Millimeters (voxel space) to Meters (Unity world space):
        // signsLPS should be (1, 1, 1) if voxelized with the latest native script
        Vector3 scaleFactor = signsLPS * 0.001f;

        // lpsToWorld handles: Voxel mm -> Meters -> Model Rotation -> Model Position
        lpsToWorld = refTransform.localToWorldMatrix * Matrix4x4.Scale(scaleFactor);

        // worldToLps handles: World Point -> Inverse Position & Rotation -> Meters to mm
        worldToLps = lpsToWorld.inverse;
    }

    private Transform FindReferenceBone(string boneName)
    {
        if (anatomicalModel == null) return null;
        foreach (var t in anatomicalModel.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == boneName) return t;
        }
        return null;
    }

    public Vector3 WorldToLps(Vector3 worldPos) => worldToLps.MultiplyPoint3x4(worldPos);
    public Vector3 LpsToWorld(Vector3 lpsPos) => lpsToWorld.MultiplyPoint3x4(lpsPos);

    private void EvaluateSphereCollision()
    {
        positionLpsMm = WorldToLps(transform.position);
        centerTissue = tissueMap.En(positionLpsMm);

        // Check whether probe position is within the bounding grid volume
        isInsideVolume = (positionLpsMm.x >= tissueMap.origen.x && positionLpsMm.x <= tissueMap.origen.x + tissueMap.nx * tissueMap.espaciado &&
                          positionLpsMm.y >= tissueMap.origen.y && positionLpsMm.y <= tissueMap.origen.y + tissueMap.ny * tissueMap.espaciado &&
                          positionLpsMm.z >= tissueMap.origen.z && positionLpsMm.z <= tissueMap.origen.z + tissueMap.nz * tissueMap.espaciado);

        // Discretize and sample the solid volume within the probe sphere
        solidVoxelCount = 0;
        float pitch = tissueMap.espaciado;
        int stepCount = Mathf.CeilToInt(radiusMm / pitch);
        float sqrRadius = radiusMm * radiusMm;

        for (int x = -stepCount; x <= stepCount; x++)
        {
            for (int y = -stepCount; y <= stepCount; y++)
            {
                for (int z = -stepCount; z <= stepCount; z++)
                {
                    Vector3 offsetMm = new Vector3(x, y, z) * pitch;
                    if (offsetMm.sqrMagnitude <= sqrRadius)
                    {
                        Tissue tissue = tissueMap.En(positionLpsMm + offsetMm);
                        if (tissue != Tissue.Air) solidVoxelCount++;
                    }
                }
            }
        }
    }

    void OnDrawGizmos()
    {
        if (tissueMap == null) return;

        UpdateCoordinateMatrices();
        Vector3 centerLps = WorldToLps(transform.position);
        Vector3 centerWorld = transform.position;

        // 1. Draw wireframe probe sphere tinted by current center tissue
        Color statusColor = TissueColors[(int)centerTissue];
        if (statusColor.a == 0) statusColor = Color.white;

        Gizmos.color = statusColor;
        Gizmos.DrawWireSphere(centerWorld, radiusMm * 0.001f); // Converted from mm to meters for Unity scene view

        // 2. Sample surface points using spherical Fibonacci spiral
        float goldenRatio = (1f + Mathf.Sqrt(5f)) * 0.5f;
        for (int i = 0; i < surfaceSampleCount; i++)
        {
            float theta = 2f * Mathf.PI * i / goldenRatio;
            float phi = Mathf.Acos(1f - 2f * (i + 0.5f) / surfaceSampleCount);

            Vector3 surfaceNormal = new Vector3(
                Mathf.Cos(theta) * Mathf.Sin(phi),
                Mathf.Sin(theta) * Mathf.Sin(phi),
                Mathf.Cos(phi)
            );

            Vector3 sampleLps = centerLps + surfaceNormal * radiusMm;
            Tissue sampleTissue = tissueMap.En(sampleLps);

            Gizmos.color = TissueColors[(int)sampleTissue];
            if (Gizmos.color.a > 0)
            {
                Gizmos.DrawSphere(LpsToWorld(sampleLps), 0.0006f); // 0.6 mm indicator dot
            }
        }

        // 3. Draw overall voxel bounding box in Unity scene coordinates
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
        Vector3 boundingSizeLps = new Vector3(tissueMap.nx * tissueMap.espaciado, tissueMap.ny * tissueMap.espaciado, tissueMap.nz * tissueMap.espaciado);
        Vector3 boundingCenterLps = tissueMap.origen + boundingSizeLps * 0.5f;
        Gizmos.matrix = lpsToWorld;
        Gizmos.DrawWireCube(boundingCenterLps, boundingSizeLps);
        Gizmos.matrix = Matrix4x4.identity;
    }

    void OnGUI()
    {
        GUI.Box(new Rect(10, 10, 270, 135), "Voxel Collision Diagnostics");
        GUI.Label(new Rect(20, 35, 250, 20), $"Center Tissue: <b>{centerTissue}</b>");
        GUI.Label(new Rect(20, 55, 250, 20), $"LPS: ({positionLpsMm.x:F1}, {positionLpsMm.y:F1}, {positionLpsMm.z:F1}) mm");
        GUI.Label(new Rect(20, 75, 250, 20), $"Solid Voxels in Probe: {solidVoxelCount}");
        GUI.Label(new Rect(20, 95, 250, 20), $"In Bounds: {(isInsideVolume ? "YES" : "NO (Out of bounds)")}");

        if (GUI.Button(new Rect(20, 115, 130, 20), "Reload Tissue Map"))
        {
            LoadTissueMap();
        }
    }
}