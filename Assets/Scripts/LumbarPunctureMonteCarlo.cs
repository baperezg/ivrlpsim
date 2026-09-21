using System;
using System.Collections.Generic;
using UnityEngine;

public class LumbarPunctureMonteCarlo : MonoBehaviour
{
    public enum RenderMode
    {
        DoNotRender,      // Index 0: Render in Game View = false
        ShowAll,          // Index 1: Render in Game View = true (valid & invalid)
        ShowValidOnly     // Index 2: Render in Game View = true (only valid)
    }

    [Header("Controls")]
    [Tooltip("Check this box in the Inspector to execute the Monte Carlo simulation")]
    public bool runSimulation = false;

    [Tooltip("Run simulation automatically on Start()")]
    public bool runOnStart = false;

    [Header("Reference Areas")]
    [Tooltip("Transform of the 2D disc placed on the skin surface")]
    public Transform skinCircle;
    public float skinRadius = 0.015f; // 1.5 cm

    [Tooltip("Transform of the 2D disc placed on the dura mater target zone")]
    public Transform duraCircle;
    public float duraRadius = 0.008f; // 0.8 cm

    [Header("Collision Filtering")]
    [Tooltip("Tag used to identify bone colliders")]
    public string boneTag = "Bone";

    [Header("Monte Carlo Settings")]
    [Range(100, 10000)]
    public int sampleCount = 1000;

    [Header("Visualization & Filtering")]
    [Range(0f, 0.3f)]
    [Tooltip("Extends the rays outward past the skin (in meters) along the dura-to-skin vector")]
    public float rayExtension = 0.05f; // 5 cm extension outward

    [Header("Colors (Gizmos / Default Material Fallbacks)")]
    public Color validColor = Color.green;
    public Color invalidColor = new Color(1f, 0f, 0f, 0.25f);

    [Header("Game View Materials")]
    [Tooltip("Material used for valid rays in the Game view")]
    public Material validMaterial;

    [Tooltip("Material used for invalid rays in the Game view")]
    public Material invalidMaterial;

    [Header("Rendering Toggles")]
    public bool drawGizmos = true;

    [Header("Visualization Settings")]
    public RenderMode currentRenderMode = RenderMode.ShowAll;

    /// <summary>
    /// Updates the render mode, switches internal flags, and rebuilds the runtime line mesh.
    /// </summary>
    public void SetRenderMode(RenderMode mode)
    {
        currentRenderMode = mode;
        ApplyRenderMode();
    }

    private void ApplyRenderMode()
    {
        switch (currentRenderMode)
        {
            case RenderMode.DoNotRender:
                if (runtimeMeshHost != null)
                {
                    runtimeMeshHost.SetActive(false);
                }
                break;

            case RenderMode.ShowAll:
            case RenderMode.ShowValidOnly:
                if (trajectories.Count > 0)
                {
                    UpdateGameViewMesh();
                }
                break;
        }
    }

    public struct TrajectoryResult
    {
        public Vector3 start;
        public Vector3 end;
        public float score;
        public bool isValid;
        public bool hitBone;
    }

    private List<TrajectoryResult> trajectories = new List<TrajectoryResult>();

    // Game view runtime components
    private GameObject runtimeMeshHost;
    private MeshFilter runtimeMeshFilter;
    private MeshRenderer runtimeMeshRenderer;
    private Mesh linesMesh;

    private void Start()
    {
        if (runOnStart)
        {
            ExecuteSimulation();
        }
    }

    private void Update()
    {
        if (runSimulation)
        {
            runSimulation = false;
            ExecuteSimulation();
        }
    }

    private void OnValidate()
    {
        if (runSimulation)
        {
            runSimulation = false;
            ExecuteSimulation();
        }
        else if (trajectories.Count > 0)
        {
            ApplyRenderMode();
        }
    }

    [ContextMenu("Run Simulation")]
    public void ExecuteSimulation()
    {
        if (skinCircle == null || duraCircle == null)
        {
            Debug.LogError("Please assign both skinCircle and duraCircle transforms.");
            return;
        }

        trajectories.Clear();
        int validCount = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            Vector3 startPos = GetRandomPointInDisk(skinCircle, skinRadius);
            Vector3 endPos = GetRandomPointInDisk(duraCircle, duraRadius);

            TrajectoryResult result = EvaluateTrajectory(startPos, endPos);
            trajectories.Add(result);

            if (result.isValid) validCount++;
        }

        float successRate = ((float)validCount / sampleCount) * 100f;
        Debug.Log($"Monte Carlo finished: {validCount}/{sampleCount} valid trajectories ({successRate:F2}%).");

        ApplyRenderMode();
    }

    private Vector3 GetRandomPointInDisk(Transform diskTransform, float radius)
    {
        float u1 = UnityEngine.Random.value;
        float u2 = UnityEngine.Random.value;
        float r = radius * Mathf.Sqrt(u1);
        float theta = u2 * 2f * Mathf.PI;

        Vector3 localPos = new Vector3(r * Mathf.Cos(theta), r * Mathf.Sin(theta), 0f);
        return diskTransform.TransformPoint(localPos);
    }

    private TrajectoryResult EvaluateTrajectory(Vector3 start, Vector3 end)
    {
        Vector3 direction = end - start;
        float distance = direction.magnitude;
        Vector3 dirNorm = direction.normalized;
        int boneLayerMask = LayerMask.GetMask(boneTag);

        RaycastHit[] hits = Physics.RaycastAll(start, dirNorm, distance);
        //RaycastHit[] hits = Physics.RaycastAll(start, dirNorm, distance, boneLayerMask);

        Debug.Log("Hits" + hits.Length);

        bool hitBone = false;

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider.CompareTag(boneTag))
            {
                hitBone = true;
                break;
            }
        }
        Debug.Log("Hit bone" + hitBone);

        bool isValid = !hitBone;
        float score = 0f;

        if (isValid)
        {
            float clearPathScore = 60f;
            float localSkinDist = Vector3.Distance(start, skinCircle.position);
            float skinCentering = Mathf.Clamp01(1f - (localSkinDist / skinRadius)) * 20f;
            float angleAlignment = Mathf.Clamp01(Vector3.Dot(dirNorm, -duraCircle.forward)) * 20f;

            score = clearPathScore + skinCentering + angleAlignment;
        }

        return new TrajectoryResult
        {
            start = start,
            end = end,
            score = Mathf.Clamp(score, 0f, 100f),
            isValid = isValid,
            hitBone = hitBone
        };
    }

    private void UpdateGameViewMesh()
    {
        if (currentRenderMode == RenderMode.DoNotRender)
        {
            if (runtimeMeshHost != null) runtimeMeshHost.SetActive(false);
            return;
        }

        if (!EnsureMeshComponents()) return;

        runtimeMeshHost.SetActive(true);

        List<Vector3> vertices = new List<Vector3>();
        List<int> validIndices = new List<int>();
        List<int> invalidIndices = new List<int>();

        int vertexIndex = 0;
        Transform hostTransform = runtimeMeshHost.transform;

        for (int i = 0; i < trajectories.Count; i++)
        {
            var traj = trajectories[i];

            // Filter out invalid trajectories when ShowValidOnly is active
            if (currentRenderMode == RenderMode.ShowValidOnly && !traj.isValid)
            {
                continue;
            }

            Vector3 outwardDir = (traj.start - traj.end).normalized;
            Vector3 extendedStart = traj.start + (outwardDir * rayExtension);

            // Convert World Space coordinates to Local Space of the mesh host
            Vector3 localStart = hostTransform.InverseTransformPoint(extendedStart);
            Vector3 localEnd = hostTransform.InverseTransformPoint(traj.end);

            vertices.Add(localStart);
            vertices.Add(localEnd);

            if (traj.isValid)
            {
                validIndices.Add(vertexIndex);
                validIndices.Add(vertexIndex + 1);
            }
            else
            {
                invalidIndices.Add(vertexIndex);
                invalidIndices.Add(vertexIndex + 1);
            }

            vertexIndex += 2;
        }

        if (linesMesh == null)
        {
            linesMesh = new Mesh();
            linesMesh.name = "MonteCarlo_TrajectoryLines";
        }
        else
        {
            linesMesh.Clear();
        }

        linesMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        linesMesh.SetVertices(vertices);

        // Define 2 submeshes: 0 = Valid Rays, 1 = Invalid Rays
        linesMesh.subMeshCount = 2;
        linesMesh.SetIndices(validIndices.ToArray(), MeshTopology.Lines, 0);
        linesMesh.SetIndices(invalidIndices.ToArray(), MeshTopology.Lines, 1);

        runtimeMeshFilter.sharedMesh = linesMesh;

        // Apply both materials to the MeshRenderer
        runtimeMeshRenderer.sharedMaterials = new Material[] { validMaterial, invalidMaterial };
    }

    private bool EnsureMeshComponents()
    {
        if (runtimeMeshHost == null)
        {
            Transform existing = transform.Find("GameView_TrajectoryRenderer");
            if (existing != null)
            {
                runtimeMeshHost = existing.gameObject;
            }
            else
            {
                runtimeMeshHost = new GameObject("GameView_TrajectoryRenderer");
                runtimeMeshHost.transform.SetParent(transform, false);
                runtimeMeshHost.transform.localPosition = Vector3.zero;
                runtimeMeshHost.transform.localRotation = Quaternion.identity;
                runtimeMeshHost.transform.localScale = Vector3.one;
            }
        }

        if (runtimeMeshFilter == null)
        {
            runtimeMeshFilter = runtimeMeshHost.GetComponent<MeshFilter>();
            if (runtimeMeshFilter == null)
            {
                runtimeMeshFilter = runtimeMeshHost.AddComponent<MeshFilter>();
            }
        }

        if (runtimeMeshRenderer == null)
        {
            runtimeMeshRenderer = runtimeMeshHost.GetComponent<MeshRenderer>();
            if (runtimeMeshRenderer == null)
            {
                runtimeMeshRenderer = runtimeMeshHost.AddComponent<MeshRenderer>();
            }
        }

        // Generate fallback materials if unassigned
        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit")
                          ?? Shader.Find("Sprites/Default")
                          ?? Shader.Find("Hidden/Internal-Colored");

        if (validMaterial == null && unlitShader != null)
        {
            validMaterial = new Material(unlitShader);
            validMaterial.color = validColor;
            validMaterial.name = "Fallback_ValidRayMat";
        }

        if (invalidMaterial == null && unlitShader != null)
        {
            invalidMaterial = new Material(unlitShader);
            invalidMaterial.color = invalidColor;
            invalidMaterial.name = "Fallback_InvalidRayMat";
        }

        return runtimeMeshFilter != null && runtimeMeshRenderer != null;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos || trajectories == null) return;

        foreach (var traj in trajectories)
        {
            if (currentRenderMode == RenderMode.ShowValidOnly && !traj.isValid)
            {
                continue;
            }

            Gizmos.color = traj.isValid ? validColor : invalidColor;

            Vector3 outwardDir = (traj.start - traj.end).normalized;
            Vector3 extendedStart = traj.start + (outwardDir * rayExtension);

            Gizmos.DrawLine(extendedStart, traj.end);
        }
    }

    private void OnDestroy()
    {
        if (runtimeMeshHost != null)
        {
            if (Application.isPlaying)
            {
                Destroy(runtimeMeshHost);
            }
            else
            {
                DestroyImmediate(runtimeMeshHost);
            }
        }
    }

    public struct TrajectoryComparison
    {
        public bool hasValidMatch;
        public TrajectoryResult bestTrajectory;
        public float angularErrorDeg;
        public float entryDistanceError;
        public float targetMissDistance;
        public float matchedTrajectoryScore;
        public float combinedSimilarityScore; // 0 to 100%
    }

    /// <summary>
    /// Compares a needle insertion ray against all precalculated valid trajectories.
    /// </summary>
    public TrajectoryComparison CompareWithMonteCarlo(Vector3 needleEntryPoint, Vector3 needleDirection)
    {
        TrajectoryComparison comparison = new TrajectoryComparison();
        float bestMetric = float.MaxValue;
        bool foundValid = false;

        // 1. Calculate target intersection (plane intersection with duraCircle)
        Plane duraPlane = new Plane(duraCircle.forward, duraCircle.position);
        Ray needleRay = new Ray(needleEntryPoint, needleDirection);

        if (duraPlane.Raycast(needleRay, out float enterDist))
        {
            Vector3 targetHitPoint = needleRay.GetPoint(enterDist);
            comparison.targetMissDistance = Vector3.Distance(targetHitPoint, duraCircle.position);
        }
        else
        {
            comparison.targetMissDistance = float.MaxValue;
        }

        // 2. Search through generated trajectories
        for (int i = 0; i < trajectories.Count; i++)
        {
            var traj = trajectories[i];
            if (!traj.isValid) continue; // Only compare against valid paths

            Vector3 trajDir = (traj.end - traj.start).normalized;
            float angle = Vector3.Angle(needleDirection, trajDir);
            float entryDist = Vector3.Distance(needleEntryPoint, traj.start);

            // Combined cost metric (balancing angle in degrees and distance in meters)
            float cost = angle + (entryDist * 100f);

            if (cost < bestMetric)
            {
                bestMetric = cost;
                comparison.bestTrajectory = traj;
                comparison.angularErrorDeg = angle;
                comparison.entryDistanceError = entryDist;
                comparison.matchedTrajectoryScore = traj.score;
                foundValid = true;
            }
        }

        comparison.hasValidMatch = foundValid;

        if (foundValid)
        {
            // Compute 0-100% similarity score
            float angleScore = Mathf.Clamp01(1f - (comparison.angularErrorDeg / 15f)) * 40f;   // 15 deg max
            float entryScore = Mathf.Clamp01(1f - (comparison.entryDistanceError / skinRadius)) * 30f;
            float targetScore = Mathf.Clamp01(1f - (comparison.targetMissDistance / duraRadius)) * 30f;

            comparison.combinedSimilarityScore = Mathf.Clamp(angleScore + entryScore + targetScore, 0f, 100f);
        }

        return comparison;
    }

}