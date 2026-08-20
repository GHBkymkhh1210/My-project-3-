using UnityEngine;

namespace DisasterSim.Disasters
{
    /// <summary>
    /// Procedural animated water surface for tsunami simulation.
    /// Uses a sine-wave mesh deformation — not CFD, but visually convincing.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class WaterSurface : MonoBehaviour
    {
        public static WaterSurface Instance { get; private set; }

        [SerializeField] private int meshResolution = 40;
        [SerializeField] private float oceanWidth = 80f;
        [SerializeField] private float oceanDepth = 60f;
        [SerializeField] private float baseWaterHeight = -0.5f;

        public float CurrentWaterHeight { get; private set; }
        public bool IsTsunamiActive { get; private set; }
        public float TsunamiWaveHeight { get; private set; }
        public float TsunamiProgress { get; private set; }

        private Mesh mesh;
        private Vector3[] baseVertices;
        private float waveSpeed = 5f;
        private float waveDuration = 12f;
        private float elapsed;

        private void Awake()
        {
            Instance = this;
            BuildMesh();
            CurrentWaterHeight = baseWaterHeight;
        }

        private void BuildMesh()
        {
            var mf = GetComponent<MeshFilter>();
            mesh = new Mesh { name = "WaterSurface" };

            int vertCount = (meshResolution + 1) * (meshResolution + 1);
            baseVertices = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            var tris = new int[meshResolution * meshResolution * 6];

            float halfW = oceanWidth * 0.5f;
            float halfD = oceanDepth * 0.5f;
            int vi = 0;
            for (int z = 0; z <= meshResolution; z++)
            {
                for (int x = 0; x <= meshResolution; x++)
                {
                    float px = Mathf.Lerp(-halfW, halfW, x / (float)meshResolution);
                    float pz = Mathf.Lerp(-halfD, halfD, z / (float)meshResolution);
                    baseVertices[vi] = new Vector3(px, baseWaterHeight, pz);
                    uvs[vi] = new Vector2(x / (float)meshResolution, z / (float)meshResolution);
                    vi++;
                }
            }

            int ti = 0;
            for (int z = 0; z < meshResolution; z++)
            {
                for (int x = 0; x < meshResolution; x++)
                {
                    int bl = z * (meshResolution + 1) + x;
                    int br = bl + 1;
                    int tl = bl + meshResolution + 1;
                    int tr = tl + 1;
                    tris[ti++] = bl; tris[ti++] = tl; tris[ti++] = br;
                    tris[ti++] = br; tris[ti++] = tl; tris[ti++] = tr;
                }
            }

            mesh.vertices = baseVertices;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mf.sharedMesh = mesh;
        }

        private void Update()
        {
            if (mesh == null) return;

            elapsed += Time.deltaTime;
            var verts = new Vector3[baseVertices.Length];

            for (int i = 0; i < baseVertices.Length; i++)
            {
                Vector3 v = baseVertices[i];
                float wave = 0f;

                if (IsTsunamiActive)
                {
                    // Incoming wave front moving along +Z
                    float frontZ = -oceanDepth * 0.5f + TsunamiProgress * oceanDepth;
                    float dist = v.z - frontZ;
                    if (dist < 5f)
                        wave = TsunamiWaveHeight * Mathf.Exp(-dist * dist / 8f) * Mathf.Sin(elapsed * 3f);
                }
                else
                {
                    wave = 0.1f * Mathf.Sin(v.x * 0.3f + elapsed) * Mathf.Cos(v.z * 0.2f + elapsed * 0.7f);
                }

                v.y = CurrentWaterHeight + wave;
                verts[i] = v;
            }

            mesh.vertices = verts;
            mesh.RecalculateNormals();
        }

        public void StartTsunami(float waveHeight, float duration, float speed)
        {
            IsTsunamiActive = true;
            TsunamiWaveHeight = waveHeight;
            waveDuration = duration;
            waveSpeed = speed;
            TsunamiProgress = 0f;
            elapsed = 0f;
        }

        public void UpdateTsunami(float deltaTime)
        {
            if (!IsTsunamiActive) return;
            TsunamiProgress += deltaTime * waveSpeed / oceanDepth;
            CurrentWaterHeight = baseWaterHeight + TsunamiWaveHeight * Mathf.Clamp01(TsunamiProgress);
            if (TsunamiProgress >= 1.2f) StopTsunami();
        }

        public void StopTsunami()
        {
            IsTsunamiActive = false;
            TsunamiProgress = 0f;
            CurrentWaterHeight = baseWaterHeight;
        }

        public void SetWaterLevel(float level)
        {
            if (!IsTsunamiActive) CurrentWaterHeight = baseWaterHeight + level;
        }

        /// <summary>Returns true if a world position is below the current water surface.</summary>
        public bool IsUnderWater(Vector3 worldPos)
        {
            return worldPos.y < CurrentWaterHeight;
        }
    }
}
