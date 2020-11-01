using System.Linq;
using Pathfinding;
using UnityEngine;
using UnityEngine.Rendering;

public class MapGenerator : MonoBehaviour
{
    [SerializeField] private Vector2Int size;
    public float seed;
    public float perlinNoiseScale = 0.1f;

    public float waterDepth = 0.2f;
    public float edgeDepth = 0.2f;
    
    public Texture2D textureAtlas;
    
    private int[,] _biomeMap;    
    private float[,] _heightMap;
    
    private Biome[] _biomes;
    public Biome water;
    public Biome sand;
    public Biome grass;

    private AstarPath _astarPath;
    private GridGraph _gridGraph;
    private MeshCollider _meshCollider;
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    
    private void Start()
    {
        _astarPath = GetComponent<AstarPath>();
        _gridGraph = _astarPath.data.gridGraph;
        _meshCollider = GetComponent<MeshCollider>();
        _meshFilter = GetComponent<MeshFilter>();
        _meshRenderer = GetComponent<MeshRenderer>();
        _biomes = new[] {water, sand, grass};
        seed = Random.Range(0f, 10000f);
        GenerateTexture();
        GenerateHeightMap();
        GenerateBiomeMap();
        GenerateMesh();

        _gridGraph.center = new Vector3(size.x / 2f, 0, size.y / 2f);
        _gridGraph.SetDimensions(size.x, size.y, 1);
        _astarPath.Scan();
    }


    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (Input.GetKey(KeyCode.S)) seed = Random.Range(0f, 10000f);
            GenerateTexture();
            GenerateHeightMap();
            GenerateBiomeMap();
            GenerateMesh();
        }
    }

    private void GenerateTexture()
    {
        var texture = new Texture2D(_biomes.Sum(p => p.numSteps), 1);
        var uvIndex = 0;

        foreach (var biome in _biomes)
            for (var i = 0; i < biome.numSteps; i++, uvIndex++)
            {
                var color = biome.startColor * (biome.numSteps - 1 - i) / (biome.numSteps - 1)
                            + biome.endColor * i / (biome.numSteps - 1);
                texture.SetPixel(uvIndex, 0, color);
            }

        texture.filterMode = FilterMode.Point;
        texture.Apply();
        textureAtlas = texture;
    }

    private void GenerateHeightMap()
    {
        _heightMap = new float[size.x, size.y];

        for (var x = 0; x < size.x; x++)
        for (var y = 0; y < size.y; y++)
        {
            var xCoord = seed + x * perlinNoiseScale;
            var yCoord = seed + y * perlinNoiseScale;
            var height = Mathf.PerlinNoise(xCoord, yCoord);
            height = Mathf.Clamp(height, 0f, 0.999f);
            _heightMap[x, y] = height;
        }
    }

    private void GenerateBiomeMap()
    {
        _biomeMap = new int[size.x, size.y];

        for (var x = 0; x < size.x; x++)
        for (var y = 0; y < size.y; y++)
        {
            var biomeIndex = 0;
            for (var i = 0; i < _biomes.Length; i++)
            {
                var upperBorder = i == _biomes.Length - 1 ? 1 : _biomes[i + 1].startHeight;
                if (_heightMap[x, y] > upperBorder) continue;
                biomeIndex = i;
                break;
            }

            _biomeMap[x, y] = biomeIndex;
        }
    }

    void GenerateMesh()
    {
        var mesh = new Mesh {indexFormat = IndexFormat.UInt32};
        mesh.name = "MapMesh";

        var vertices = new Vector3[size.x * size.y * 8];
        var triangles = new int[size.x * size.y * 12];
        var uvs = new Vector2[vertices.Length];
        
        var vertNum = 0;
        var trNum = 0;
        
        for (var y = 0; y < size.y; y++)
        for (var x = 0; x < size.x; x++, vertNum += 4, trNum += 6)
        {
            Vector3[] offsetDirection = {Vector3.back, Vector3.back, Vector3.right, Vector3.right};
            Vector3Int[] isNeed = {new Vector3Int(0, -1, 0), new Vector3Int(size.x - 1, 1, 0), new Vector3Int(0, 0, -1), new Vector3Int(size.y - 1, 0, 1)};
            Vector2Int[] mainVertPosition = {new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(0, 0), new Vector2Int(0, 1)};
            int[][] trianglesVariants = {new[] {0, 1, 2, 1, 3, 2}, new[] {0, 2, 1, 1, 2, 3}, new[] {0, 1, 2, 2, 1, 3}, new[] {0, 2, 1, 2, 3, 1}};

            var isWaterTile = _biomeMap[x, y] == 0;
            var height = isWaterTile ? -waterDepth : 0;
            var uv = GetUv(x, y);

            vertices[vertNum] = new Vector3(x, height, y + 1);
            vertices[vertNum + 1] = new Vector3(x + 1, height, y + 1);
            vertices[vertNum + 2] = new Vector3(x, height, y);
            vertices[vertNum + 3] = new Vector3(x + 1, height, y);

            for(var i = 0; i < 4; i++) uvs[vertNum + i] = uv; //Uv

            triangles[trNum] = vertNum; //Triangles
            triangles[trNum + 1] = vertNum + 1;
            triangles[trNum + 2] = vertNum + 2;
            triangles[trNum + 3] = vertNum + 1;
            triangles[trNum + 4] = vertNum + 3;
            triangles[trNum + 5] = vertNum + 2;

            var isEdgeTile = x == 0 || y == 0 || x == size.x - 1 || y == size.y - 1;
            var isLandTile = !isWaterTile;
            
            //Перегородки
            if (isLandTile || isEdgeTile)
                for (var v = 0; v < 4; v++)
                    if ((v < 2 ? x : y) == isNeed[v].x || isLandTile && _biomeMap[x + isNeed[v].y, y + isNeed[v].z] == 0)
                    {
                        var nws = new Vector3(x + mainVertPosition[v].x, height, y + mainVertPosition[v].y);
                        var nes = nws + offsetDirection[v];
                        vertNum += 4;
                        vertices[vertNum] = nws;
                        vertices[vertNum + 1] = nes;
                        vertices[vertNum + 2] = nws + Vector3.down * edgeDepth + new Vector3(0, isWaterTile ? 0 : -waterDepth, 0);
                        vertices[vertNum + 3] = nes + Vector3.down * edgeDepth + new Vector3(0, isWaterTile ? 0 : -waterDepth, 0);

                        for(var i = 0; i < 4; i++) uvs[vertNum + i] = uv;

                        trNum += 6;
                        for(var i = 0; i < 6; i++) triangles[trNum + i] = vertNum + trianglesVariants[v][i];
                    }
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0, true);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        _meshFilter.mesh = mesh;
        _meshCollider.sharedMesh = mesh;
        _meshRenderer.material.SetTexture("_MainTex", textureAtlas);
    }

    private Vector2 GetUv(int x, int y)
    {
        var uvx = 0;

        for (var i = 0; i < _biomes.Length; i++)
        {
            var biome = _biomes[i];
            var upperBorder = i == _biomes.Length - 1 ? 1 : _biomes[i + 1].startHeight;
            var isNeedToBreak = false;
            for (var j = 1; j <= biome.numSteps; j++)
                if (_heightMap[x, y] - biome.startHeight < (upperBorder - biome.startHeight) / biome.numSteps * j)
                {
                    uvx += j - 1;
                    isNeedToBreak = true;
                    break;
                }

            if (isNeedToBreak) break;
            uvx += biome.numSteps;
        }

        return new Vector2(1f / _biomes.Sum(p => p.numSteps) * uvx, 0);
    }
}