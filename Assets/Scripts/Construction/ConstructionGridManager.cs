using UnityEngine;
using UnityEngine.InputSystem;

namespace DisasterSim.Construction
{
    /// <summary>
    /// Builds and displays the 3D construction grid, and raycasts the mouse
    /// against the ground plane to highlight the currently selected cell.
    /// Later phases (Building/Construction) will use SelectedCell to place components.
    /// </summary>
    [DisallowMultipleComponent]
    public class ConstructionGridManager : MonoBehaviour
    {
        public static ConstructionGridManager Instance { get; private set; }

        [Header("Grid Size")]
        [SerializeField] private int columns = 20;
        [SerializeField] private int rows = 20;
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private Vector3 gridOrigin = Vector3.zero;

        [Header("Rendering")]
        [SerializeField] private Material gridLineMaterial;
        [SerializeField] private Material highlightMaterial;
        [SerializeField] private LayerMask groundLayerMask = ~0;

        private MeshFilter gridMeshFilter;
        private MeshRenderer gridMeshRenderer;

        private GameObject highlightCell;
        private Camera mainCamera;

        /// <summary>Currently hovered grid cell coordinate, or null if none.</summary>
        public Vector2Int? HoveredCell { get; private set; }

        /// <summary>World-space size of the whole grid.</summary>
        public Vector2 GridWorldSize => new Vector2(columns * cellSize, rows * cellSize);

        public int Columns => columns;
        public int Rows => rows;
        public float CellSize => cellSize;
        public Vector3 GridOrigin => gridOrigin;

        private void Awake()
        {
            Instance = this;
            mainCamera = Camera.main;

            BuildGridMesh();
            BuildHighlightCell();
        }

        private void Update()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            UpdateHover();
        }

        /// <summary>Converts a world position to the nearest grid cell coordinate.</summary>
        public Vector2Int WorldToCell(Vector3 worldPosition)
        {
            Vector3 local = worldPosition - gridOrigin;
            int cx = Mathf.FloorToInt(local.x / cellSize);
            int cz = Mathf.FloorToInt(local.z / cellSize);
            return new Vector2Int(cx, cz);
        }

        /// <summary>Converts a grid cell coordinate to the world position of its center.</summary>
        public Vector3 CellToWorldCenter(Vector2Int cell)
        {
            float x = gridOrigin.x + (cell.x + 0.5f) * cellSize;
            float z = gridOrigin.z + (cell.y + 0.5f) * cellSize;
            return new Vector3(x, gridOrigin.y, z);
        }

        public bool IsCellInBounds(Vector2Int cell)
        {
            int minX = -columns / 2;
            int maxX = columns / 2;
            int minZ = -rows / 2;
            int maxZ = rows / 2;
            return cell.x >= minX && cell.x < maxX && cell.y >= minZ && cell.y < maxZ;
        }

        private void UpdateHover()
        {
            if (mainCamera == null || highlightCell == null) return;

            var mouse = Mouse.current;
            if (mouse == null)
            {
                HoveredCell = null;
                highlightCell.SetActive(false);
                return;
            }

            Vector2 mousePos = mouse.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(mousePos);
            if (UnityEngine.Physics.Raycast(ray, out RaycastHit hit, 500f, groundLayerMask))
            {
                Vector2Int cell = WorldToCell(hit.point);
                HoveredCell = cell;
                highlightCell.SetActive(true);
                highlightCell.transform.position = CellToWorldCenter(cell) + Vector3.up * 0.02f;
            }
            else
            {
                HoveredCell = null;
                highlightCell.SetActive(false);
            }
        }

        private void BuildGridMesh()
        {
            GameObject gridObj = new GameObject("GridMesh");
            gridObj.transform.SetParent(transform, false);

            gridMeshFilter = gridObj.AddComponent<MeshFilter>();
            gridMeshRenderer = gridObj.AddComponent<MeshRenderer>();
            gridMeshRenderer.sharedMaterial = gridLineMaterial;
            gridMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            gridMeshRenderer.receiveShadows = false;

            int minX = -columns / 2;
            int maxX = columns / 2;
            int minZ = -rows / 2;
            int maxZ = rows / 2;

            int lineCount = (columns + 1) + (rows + 1);
            var vertices = new Vector3[lineCount * 2];
            var indices = new int[lineCount * 2];

            int v = 0;
            for (int x = minX; x <= maxX; x++)
            {
                float wx = gridOrigin.x + x * cellSize;
                vertices[v] = new Vector3(wx, gridOrigin.y + 0.01f, gridOrigin.z + minZ * cellSize);
                indices[v] = v;
                v++;
                vertices[v] = new Vector3(wx, gridOrigin.y + 0.01f, gridOrigin.z + maxZ * cellSize);
                indices[v] = v;
                v++;
            }
            for (int z = minZ; z <= maxZ; z++)
            {
                float wz = gridOrigin.z + z * cellSize;
                vertices[v] = new Vector3(gridOrigin.x + minX * cellSize, gridOrigin.y + 0.01f, wz);
                indices[v] = v;
                v++;
                vertices[v] = new Vector3(gridOrigin.x + maxX * cellSize, gridOrigin.y + 0.01f, wz);
                indices[v] = v;
                v++;
            }

            var mesh = new Mesh { name = "ConstructionGridMesh" };
            mesh.SetVertices(vertices);
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
            gridMeshFilter.sharedMesh = mesh;
        }

        private void BuildHighlightCell()
        {
            highlightCell = GameObject.CreatePrimitive(PrimitiveType.Quad);
            highlightCell.name = "GridHighlightCell";
            Object.DestroyImmediate(highlightCell.GetComponent<Collider>());
            highlightCell.transform.SetParent(transform, false);
            highlightCell.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            highlightCell.transform.localScale = new Vector3(cellSize * 0.95f, cellSize * 0.95f, 1f);

            var renderer = highlightCell.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = highlightMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            highlightCell.SetActive(false);
        }
    }
}
