using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[SelectionBase]
[DisallowMultipleComponent]
public class AnatomyVoxelCollider : MonoBehaviour
{
    public enum TissueType : byte
    {
        Air = 0,
        SoftTissue = 1,
        Fat = 2,
        Skin = 3,
        Muscle = 4,
        DuralSac = 5,
        EpiduralSpace = 6,
        OtherLigaments = 7,
        SupraspinousLigament = 8,
        LigamentumFlavum = 9,
        IntervertebralDisc = 10,
        Bone = 11
    }

    [System.Serializable]
    public struct SphereCollisionResult
    {
        public bool hasContact;
        public TissueType dominantTissue;
        public TissueType centerTissue;
        public int solidVoxelCount;
        public float penetrationDepth;
        public Vector3 contactPoint;
        public Vector3 contactNormal;
    }

    // -----------------------------------------------------------------------
    // Internal Voxel Data Structure
    // -----------------------------------------------------------------------
    private class VoxelVolume
    {
        public const uint Magic = 0x314A544D; // "MTJ1"
        public int nx, ny, nz;
        public Vector3 originMm;
        public float pitchMm;
        public byte[] labels;
        private float invPitch;

        public static VoxelVolume LoadFromGzip(string fullPath)
        {
            if (!File.Exists(fullPath)) throw new FileNotFoundException($"Cannot find: {fullPath}");
            using (var fs = File.OpenRead(fullPath))
            using (var gz = new GZipStream(fs, CompressionMode.Decompress))
            using (var br = new BinaryReader(gz))
            {
                uint m = br.ReadUInt32();
                if (m != Magic) throw new FormatException($"Invalid magic number: 0x{m:X8}");
                int x = br.ReadInt32();
                int y = br.ReadInt32();
                int z = br.ReadInt32();
                Vector3 orig = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                float pitch = br.ReadSingle();

                int total = x * y * z;
                byte[] data = new byte[total];
                int read = 0;
                while (read < total)
                {
                    int r = br.Read(data, read, total - read);
                    if (r <= 0) break;
                    read += r;
                }

                return new VoxelVolume
                {
                    nx = x,
                    ny = y,
                    nz = z,
                    originMm = orig,
                    pitchMm = pitch,
                    labels = data,
                    invPitch = 1.0f / pitch
                };
            }
        }

        public TissueType SampleMm(Vector3 localMm)
        {
            // FloorToInt matches np.floor((pos - origin) / spacing) so voxel (0,0,0) spans [origin, origin + pitch]
            int i = Mathf.FloorToInt((localMm.x - originMm.x) * invPitch);
            int j = Mathf.FloorToInt((localMm.y - originMm.y) * invPitch);
            int k = Mathf.FloorToInt((localMm.z - originMm.z) * invPitch);

            if ((uint)i >= (uint)nx || (uint)j >= (uint)ny || (uint)k >= (uint)nz)
                return TissueType.Air;

            return (TissueType)labels[i + nx * (j + ny * k)];
        }
    }

    [Header("Data File")]
    [Tooltip("File name inside Assets/StreamingAssets/")]
    public string fileName = "TissueMap.bytes";

    [Header("Coordinate & Unit Alignment")]
    [Tooltip("Unit conversion: Set to 0.001 if voxels are in millimeters and Unity is in meters.")]
    public float voxelToUnityUnitScale = 0.001f;

    [Tooltip("Sign flip per axis if your importer mirrors any coordinate.")]
    public Vector3 axisMultiplier = new Vector3(1f, 1f, 1f);

    [Tooltip("Fine-tune position offset (in millimeters) relative to this transform's pivot.")]
    public Vector3 manualOffsetMm = Vector3.zero;

    [Tooltip("Manual rotation offset in degrees (e.g. Set Y to 180 to flip Anterior <-> Posterior).")]
    public Vector3 manualRotationOffsetEuler = Vector3.zero;

    [Header("Collision Target (Sphere)")]
    [Tooltip("Target sphere transform to test in real-time. If null, you can query via code.")]
    public Transform testSphere;
    [Range(0.5f, 30f)]
    [Tooltip("Sphere radius in millimeters.")]
    public float sphereRadiusMm = 3.5f;

    [Header("Collision Diagnostics (Read-Only)")]
    public SphereCollisionResult lastCollision;

    [Header("In-Scene Voxel Visualizer")]
    public bool showBoundingBox = true;
    public bool showVoxels = false;

    [Range(1, 16)]
    [Tooltip("Stride to skip voxels during preview (prevents editor lag). 2 = 1/8th of voxels, 4 = 1/64th.")]
    public int renderDownsampleStride = 3;

    [Tooltip("Draw as 3D cubes instead of dots.")]
    public bool renderAsCubes = false;

    [Header("Tissue Visibility Filters")]
    public bool showBones = true;
    public bool showDuralSac = true;
    public bool showLigaments = true;
    public bool showMuscles = false;
    public bool showSkinAndFat = false;

    private VoxelVolume volume;
    private Matrix4x4 voxelToWorldMat;
    private Matrix4x4 worldToVoxelMat;

    // Palette
    private static readonly Color[] TissuePalette = new Color[]
    {
        Color.clear,                          // Air
        new Color(0.80f, 0.70f, 0.60f, 0.3f), // SoftTissue
        new Color(0.95f, 0.85f, 0.20f, 0.3f), // Fat
        new Color(0.90f, 0.65f, 0.55f, 0.3f), // Skin
        new Color(0.75f, 0.20f, 0.20f, 0.4f), // Muscle
        new Color(0.20f, 0.70f, 1.00f, 0.9f), // DuralSac
        new Color(0.90f, 0.90f, 0.50f, 0.8f), // EpiduralSpace
        new Color(0.30f, 0.80f, 0.50f, 0.8f), // OtherLigaments
        new Color(0.20f, 0.90f, 0.30f, 0.9f), // SupraspinousLigament
        new Color(0.10f, 0.60f, 0.20f, 0.9f), // LigamentumFlavum
        new Color(0.40f, 0.70f, 0.90f, 0.9f), // IntervertebralDisc
        new Color(0.95f, 0.95f, 0.90f, 0.95f) // Bone
    };

    void OnEnable()
    {
        LoadVolume();
        UpdateMatrices();
    }

    void Update()
    {
        UpdateMatrices();

        if (testSphere != null && volume != null)
        {
            lastCollision = CheckSphereCollision(testSphere.position, sphereRadiusMm);
        }
    }

    [ContextMenu("Reload Volume File")]
    public void LoadVolume()
    {
        try
        {
            string path = Path.Combine(Application.streamingAssetsPath, fileName);
            volume = VoxelVolume.LoadFromGzip(path);
            Debug.Log($"[AnatomyVoxelCollider] Loaded {volume.nx}x{volume.ny}x{volume.nz} voxels (Pitch: {volume.pitchMm} mm).");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AnatomyVoxelCollider] Failed loading {fileName}: {ex.Message}");
            volume = null;
        }
    }

    private void UpdateMatrices()
    {
        // 1. Scale mm to meters, apply axis inversion
        Vector3 finalScale = axisMultiplier * voxelToUnityUnitScale;

        // 2. Local rotation and position offsets
        Quaternion localRotOffset = Quaternion.Euler(manualRotationOffsetEuler);
        Vector3 localPosOffsetMeters = manualOffsetMm * voxelToUnityUnitScale;

        // 3. Combine local TRS: Position offset -> Rotation offset -> Scale
        Matrix4x4 localVoxelTransform = Matrix4x4.TRS(localPosOffsetMeters, localRotOffset, finalScale);

        // 4. Attach voxel coordinate space directly to this GameObject's world transform
        voxelToWorldMat = transform.localToWorldMatrix * localVoxelTransform;
        worldToVoxelMat = voxelToWorldMat.inverse;
    }

    /// <summary>
    /// Checks collision between an arbitrary world-space sphere and the voxel volume.
    /// </summary>
    public SphereCollisionResult CheckSphereCollision(Vector3 sphereWorldPos, float radiusInMm)
    {
        SphereCollisionResult res = new SphereCollisionResult();
        if (volume == null) return res;

        // Convert world sphere position into the local voxel mm space
        Vector3 probeLocalMm = worldToVoxelMat.MultiplyPoint3x4(sphereWorldPos);
        res.centerTissue = volume.SampleMm(probeLocalMm);

        float pitch = volume.pitchMm;
        int step = Mathf.CeilToInt(radiusInMm / pitch);
        float sqrRadius = radiusInMm * radiusInMm;

        int[] tissueHistogram = new int[12];
        Vector3 avgNormal = Vector3.zero;
        int solidCount = 0;

        for (int x = -step; x <= step; x++)
        {
            for (int y = -step; y <= step; y++)
            {
                for (int z = -step; z <= step; z++)
                {
                    Vector3 offsetMm = new Vector3(x, y, z) * pitch;
                    if (offsetMm.sqrMagnitude <= sqrRadius)
                    {
                        Vector3 samplePoint = probeLocalMm + offsetMm;
                        TissueType t = volume.SampleMm(samplePoint);

                        if (t != TissueType.Air)
                        {
                            solidCount++;
                            tissueHistogram[(int)t]++;

                            // Vector pointing away from penetrated voxel towards probe center
                            avgNormal -= offsetMm;
                        }
                    }
                }
            }
        }

        res.solidVoxelCount = solidCount;
        res.hasContact = solidCount > 0;

        if (res.hasContact)
        {
            // Determine dominant tissue touched
            int maxCount = 0;
            for (int i = 1; i < tissueHistogram.Length; i++)
            {
                if (tissueHistogram[i] > maxCount)
                {
                    maxCount = tissueHistogram[i];
                    res.dominantTissue = (TissueType)i;
                }
            }

            // Calculate world normal and penetration
            if (avgNormal.sqrMagnitude > 0.001f)
            {
                Vector3 localNormal = avgNormal.normalized;
                res.contactNormal = voxelToWorldMat.MultiplyVector(localNormal).normalized;
            }
            else
            {
                res.contactNormal = Vector3.up;
            }

            float fillRatio = Mathf.Clamp01((float)solidCount / ((4f / 3f) * Mathf.PI * Mathf.Pow(step, 3)));
            res.penetrationDepth = fillRatio * radiusInMm * voxelToUnityUnitScale;
            res.contactPoint = sphereWorldPos - (res.contactNormal * (radiusInMm * voxelToUnityUnitScale - res.penetrationDepth));
        }

        return res;
    }

    private bool IsTissueVisible(TissueType t)
    {
        switch (t)
        {
            case TissueType.Bone: return showBones;
            case TissueType.DuralSac: return showDuralSac;
            case TissueType.SupraspinousLigament:
            case TissueType.LigamentumFlavum:
            case TissueType.OtherLigaments:
            case TissueType.IntervertebralDisc: return showLigaments;
            case TissueType.Muscle: return showMuscles;
            case TissueType.Skin:
            case TissueType.Fat:
            case TissueType.SoftTissue: return showSkinAndFat;
            default: return false;
        }
    }

    // -----------------------------------------------------------------------
    // Gizmos & Visualizer
    // -----------------------------------------------------------------------
    void OnDrawGizmos()
    {
        if (volume == null) return;
        UpdateMatrices();

        // 1. Draw Bounding Box matching arbitrary position and rotation
        if (showBoundingBox)
        {
            Gizmos.color = new Color(0.1f, 0.8f, 1f, 0.6f);
            Vector3 sizeMm = new Vector3(volume.nx * volume.pitchMm, volume.ny * volume.pitchMm, volume.nz * volume.pitchMm);
            Vector3 centerMm = volume.originMm + sizeMm * 0.5f;

            Gizmos.matrix = voxelToWorldMat;
            Gizmos.DrawWireCube(centerMm, sizeMm);
            Gizmos.matrix = Matrix4x4.identity;
        }

        // 2. Render Voxel Grid (with adjustable stride)
        if (showVoxels)
        {
            int stride = Mathf.Max(1, renderDownsampleStride);
            Gizmos.matrix = voxelToWorldMat;

            for (int z = 0; z < volume.nz; z += stride)
            {
                for (int y = 0; y < volume.ny; y += stride)
                {
                    int rowOffset = volume.nx * (y + volume.ny * z);
                    for (int x = 0; x < volume.nx; x += stride)
                    {
                        byte tag = volume.labels[x + rowOffset];
                        if (tag == 0) continue; // Air

                        TissueType t = (TissueType)tag;
                        if (!IsTissueVisible(t)) continue;

                        Gizmos.color = TissuePalette[tag];
                        // Center each voxel within its [origin + idx * pitch, origin + (idx + 1) * pitch] cell
                        Vector3 posMm = volume.originMm + (new Vector3(x, y, z) + Vector3.one * 0.5f) * volume.pitchMm;

                        if (renderAsCubes)
                            Gizmos.DrawCube(posMm, Vector3.one * (volume.pitchMm * stride));
                        else
                            Gizmos.DrawSphere(posMm, volume.pitchMm * 0.4f * stride);
                    }
                }
            }
            Gizmos.matrix = Matrix4x4.identity;
        }

        // 3. Draw Contact Visuals for Test Sphere
        if (testSphere != null && lastCollision.hasContact)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(lastCollision.contactPoint, lastCollision.contactPoint + lastCollision.contactNormal * 0.03f);
            Gizmos.DrawWireSphere(lastCollision.contactPoint, 0.002f);
        }
    }
}