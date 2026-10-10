using System;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class VoxelMonteCarlo : MonoBehaviour
{
    public enum RenderMode
    {
        DoNotRender,      // Index 0: Disable runtime line renderer
        ShowAll,          // Index 1: Show valid, invalid, and best rays
        ShowValidOnly,    // Index 2: Show only successful rays
        ShowInvalidOnly,  // Index 3: Show only obstructed/invalid rays
        ShowBestOnly      // Index 4: Show only the ideal center-to-center ray
    }

    [Header("Anatomy Voxel Reference")]
    [Tooltip("Reference to the AnatomyVoxelCollider script on the spine model.")]
    public AnatomyVoxelCollider voxelCollider;

    [Header("Controls")]
    [Tooltip("Check this box in the Inspector to execute the Monte Carlo simulation.")]
    public bool runSimulation = false;

    [Tooltip("Run simulation automatically on Start()")]
    public bool runOnStart = false;

    [Header("Reference Areas (In World Space)")]
    [Tooltip("Transform of the 2D disc placed on the skin surface.")]
    public Transform skinCircle;
    public float skinRadius = 0.015f; // 1.5 cm

    [Tooltip("Transform of the 2D disc placed on the dura mater target zone.")]
    public Transform duraCircle;
    public float duraRadius = 0.008f; // 0.8 cm

    [Header("Monte Carlo Settings")]
    [Range(100, 10000)]
    public int sampleCount = 1000;

    [Tooltip("Ray-marching step size along the needle path in millimeters.")]
    [Range(0.2f, 2.0f)]
    public float marchStepMm = 0.5f;

    [Header("Visualization & Filtering")]
    [Range(0f, 0.3f)]
    [Tooltip("Extends the rays outward past the skin along the insertion trajectory.")]
    public float rayExtension = 0.05f; // 5 cm extension outward

    [Header("Colors (Gizmos / Default Material Fallbacks)")]
    public Color validColor = Color.green;
    public Color invalidColor = new Color(1f, 0f, 0f, 0.25f);
    public Color bestTrajectoryColor = new Color(0.6f, 0.1f, 0.9f, 1.0f); // Purple

    [Header("Game View Materials")]
    [Tooltip("Material used for valid rays in the Game view.")]
    public Material validMaterial;

    [Tooltip("Material used for invalid rays in the Game view.")]
    public Material invalidMaterial;

    [Tooltip("Material used for the best center-to-center ray in the Game view.")]
    public Material bestTrajectoryMaterial;

    [Header("Rendering Toggles")]
    public bool drawGizmos = true;
    public RenderMode currentRenderMode = RenderMode.ShowAll;

    [System.Serializable]
    public struct TrajectoryResult
    {
        public Vector3 start;
        public Vector3 end;
        public float score;
        public bool isValid;
        public bool hitBone;
        public int traversedSteps;
        public AnatomyVoxelCollider.TissueType terminalTissue;
        public bool isBestTrajectory;
    }

    [Header("Ideal Target Reference")]
    [SerializeField]
    private TrajectoryResult bestCenterTrajectory;
    public TrajectoryResult BestCenterTrajectory => bestCenterTrajectory;

    [SerializeField]
    private List<TrajectoryResult> trajectories = new List<TrajectoryResult>();
    public List<TrajectoryResult> Trajectories => trajectories;

    // Runtime Line Mesh Host
    private GameObject runtimeMeshHost;
    private MeshFilter runtimeMeshFilter;
    private MeshRenderer runtimeMeshRenderer;
    private Mesh linesMesh;

    private void Start()
    {
        if (voxelCollider == null)
            voxelCollider = FindFirstObjectByType<AnatomyVoxelCollider>();

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
        else if (trajectories.Count > 0 || bestCenterTrajectory.start != Vector3.zero)
        {
            ApplyRenderMode();
        }
    }

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
                if (runtimeMeshHost != null) runtimeMeshHost.SetActive(false);
                break;

            case RenderMode.ShowAll:
            case RenderMode.ShowValidOnly:
            case RenderMode.ShowInvalidOnly:
            case RenderMode.ShowBestOnly:
                if (trajectories.Count > 0 || bestCenterTrajectory.start != Vector3.zero)
                {
                    UpdateGameViewMesh();
                }
                break;
        }
    }

    [ContextMenu("Run Voxel Monte Carlo Simulation")]
    public void ExecuteSimulation()
    {
        if (voxelCollider == null)
            voxelCollider = FindFirstObjectByType<AnatomyVoxelCollider>();

        if (voxelCollider == null)
        {
            Debug.LogError("[VoxelMonteCarlo] AnatomyVoxelCollider is not assigned or found in the scene.");
            return;
        }

        if (skinCircle == null || duraCircle == null)
        {
            Debug.LogError("[VoxelMonteCarlo] Please assign both skinCircle and duraCircle transforms.");
            return;
        }

        trajectories.Clear();
        int validCount = 0;

        // 1. Calculate the Best Trajectory (Skin Center -> Dura Center)
        bestCenterTrajectory = EvaluateVoxelTrajectory(skinCircle.position, duraCircle.position);
        bestCenterTrajectory.isBestTrajectory = true;
        bestCenterTrajectory.score = 100f; // Max baseline score

        // 2. Monte Carlo Random Sampling
        for (int i = 0; i < sampleCount; i++)
        {
            Vector3 startPos = GetRandomPointInDisk(skinCircle, skinRadius);
            Vector3 endPos = GetRandomPointInDisk(duraCircle, duraRadius);

            TrajectoryResult result = EvaluateVoxelTrajectory(startPos, endPos);
            trajectories.Add(result);

            if (result.isValid) validCount++;
        }

        float successRate = ((float)validCount / sampleCount) * 100f;
        Debug.Log($"[VoxelMonteCarlo] Finished: {validCount}/{sampleCount} valid trajectories ({successRate:F2}% clearance). Best Center Path Valid: {bestCenterTrajectory.isValid}.");

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

    private TrajectoryResult EvaluateVoxelTrajectory(Vector3 start, Vector3 end)
    {
        Vector3 delta = end - start;
        float totalDistMeters = delta.magnitude;
        Vector3 dirNorm = delta.normalized;

        float stepMeters = marchStepMm * 0.001f;
        int numSteps = Mathf.Max(1, Mathf.CeilToInt(totalDistMeters / stepMeters));

        bool hitBone = false;
        AnatomyVoxelCollider.TissueType lastTissue = AnatomyVoxelCollider.TissueType.Air;

        for (int step = 0; step <= numSteps; step++)
        {
            float t = (float)step / numSteps;
            Vector3 currentPos = Vector3.Lerp(start, end, t);

            var query = voxelCollider.CheckSphereCollision(currentPos, 0.1f);
            lastTissue = query.centerTissue;

            if (lastTissue == AnatomyVoxelCollider.TissueType.Bone)
            {
                hitBone = true;
                break;
            }
        }

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
            hitBone = hitBone,
            traversedSteps = numSteps,
            terminalTissue = lastTissue,
            isBestTrajectory = false
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
        List<int> bestIndices = new List<int>();

        int vertexIndex = 0;
        Transform hostTransform = runtimeMeshHost.transform;

        // 1. Draw Monte Carlo trajectories (if not in ShowBestOnly mode)
        if (currentRenderMode != RenderMode.ShowBestOnly)
        {
            for (int i = 0; i < trajectories.Count; i++)
            {
                var traj = trajectories[i];

                if (currentRenderMode == RenderMode.ShowValidOnly && !traj.isValid)
                    continue;
                if (currentRenderMode == RenderMode.ShowInvalidOnly && traj.isValid)
                    continue;

                Vector3 outwardDir = (traj.start - traj.end).normalized;
                Vector3 extendedStart = traj.start + (outwardDir * rayExtension);

                vertices.Add(hostTransform.InverseTransformPoint(extendedStart));
                vertices.Add(hostTransform.InverseTransformPoint(traj.end));

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
        }

        // 2. Draw Best Center Trajectory (shown in ShowAll and ShowBestOnly)
        if ((currentRenderMode == RenderMode.ShowAll || currentRenderMode == RenderMode.ShowBestOnly) && bestCenterTrajectory.start != Vector3.zero)
        {
            Vector3 outwardDir = (bestCenterTrajectory.start - bestCenterTrajectory.end).normalized;
            Vector3 extendedStart = bestCenterTrajectory.start + (outwardDir * rayExtension);

            vertices.Add(hostTransform.InverseTransformPoint(extendedStart));
            vertices.Add(hostTransform.InverseTransformPoint(bestCenterTrajectory.end));

            bestIndices.Add(vertexIndex);
            bestIndices.Add(vertexIndex + 1);
            vertexIndex += 2;
        }

        if (linesMesh == null)
        {
            linesMesh = new Mesh();
            linesMesh.name = "VoxelMonteCarlo_Lines";
        }
        else
        {
            linesMesh.Clear();
        }

        linesMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        linesMesh.SetVertices(vertices);

        // 3 submeshes: 0 = Valid (Green), 1 = Invalid (Red), 2 = Best (Purple)
        linesMesh.subMeshCount = 3;
        linesMesh.SetIndices(validIndices.ToArray(), MeshTopology.Lines, 0);
        linesMesh.SetIndices(invalidIndices.ToArray(), MeshTopology.Lines, 1);
        linesMesh.SetIndices(bestIndices.ToArray(), MeshTopology.Lines, 2);

        runtimeMeshFilter.sharedMesh = linesMesh;
        runtimeMeshRenderer.sharedMaterials = new Material[] { validMaterial, invalidMaterial, bestTrajectoryMaterial };
    }

    private bool EnsureMeshComponents()
    {
        if (runtimeMeshHost == null)
        {
            Transform existing = transform.Find("GameView_VoxelTrajectoryRenderer");
            if (existing != null)
            {
                runtimeMeshHost = existing.gameObject;
            }
            else
            {
                runtimeMeshHost = new GameObject("GameView_VoxelTrajectoryRenderer");
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
                runtimeMeshFilter = runtimeMeshHost.AddComponent<MeshFilter>();
        }

        if (runtimeMeshRenderer == null)
        {
            runtimeMeshRenderer = runtimeMeshHost.GetComponent<MeshRenderer>();
            if (runtimeMeshRenderer == null)
                runtimeMeshRenderer = runtimeMeshHost.AddComponent<MeshRenderer>();
        }

        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit")
                          ?? Shader.Find("Sprites/Default")
                          ?? Shader.Find("Hidden/Internal-Colored");

        if (validMaterial == null && unlitShader != null)
        {
            validMaterial = new Material(unlitShader);
            validMaterial.color = validColor;
            validMaterial.name = "Fallback_ValidVoxelMat";
        }

        if (invalidMaterial == null && unlitShader != null)
        {
            invalidMaterial = new Material(unlitShader);
            invalidMaterial.color = invalidColor;
            invalidMaterial.name = "Fallback_InvalidVoxelMat";
        }

        if (bestTrajectoryMaterial == null && unlitShader != null)
        {
            bestTrajectoryMaterial = new Material(unlitShader);
            bestTrajectoryMaterial.color = bestTrajectoryColor;
            bestTrajectoryMaterial.name = "Fallback_BestTrajectoryMat";
        }

        return runtimeMeshFilter != null && runtimeMeshRenderer != null;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        // 1. Draw Monte Carlo rays (if not isolated to Best Only)
        if (currentRenderMode != RenderMode.ShowBestOnly && trajectories != null)
        {
            foreach (var traj in trajectories)
            {
                if (currentRenderMode == RenderMode.ShowValidOnly && !traj.isValid)
                    continue;
                if (currentRenderMode == RenderMode.ShowInvalidOnly && traj.isValid)
                    continue;

                Gizmos.color = traj.isValid ? validColor : invalidColor;

                Vector3 outwardDir = (traj.start - traj.end).normalized;
                Vector3 extendedStart = traj.start + (outwardDir * rayExtension);

                Gizmos.DrawLine(extendedStart, traj.end);
            }
        }

        // 2. Draw Best Center Ray in Purple (visible in ShowAll and ShowBestOnly)
        if ((currentRenderMode == RenderMode.ShowAll || currentRenderMode == RenderMode.ShowBestOnly) && bestCenterTrajectory.start != Vector3.zero)
        {
            Gizmos.color = bestTrajectoryColor;
            Vector3 outwardDir = (bestCenterTrajectory.start - bestCenterTrajectory.end).normalized;
            Vector3 extendedStart = bestCenterTrajectory.start + (outwardDir * rayExtension);

            Gizmos.DrawLine(extendedStart, bestCenterTrajectory.end);
            Gizmos.DrawWireSphere(bestCenterTrajectory.start, 0.0015f);
            Gizmos.DrawWireSphere(bestCenterTrajectory.end, 0.0015f);
        }
    }

    private void OnDestroy()
    {
        if (runtimeMeshHost != null)
        {
            if (Application.isPlaying)
                Destroy(runtimeMeshHost);
            else
                DestroyImmediate(runtimeMeshHost);
        }
    }
}