using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;


public class MapGenerator : MonoBehaviour
{
    [SerializeField] private Vector2Int size;
    float[,] heightMap;
    int[,] biomeMap;

    public Texture2D textureAtlas;

    public float seed;
    public float perlinNoiseScale = 0.1f;

    public float waterDepth = 0.2f;
    public float edgeDepth = 0.2f;

    private Biome[] biomes; //Чтобы добавить биом: 1) создать переменную биома 2) добавить в массив биомов в Start
    public Biome water;
    public Biome sand;
    public Biome grass;

    void Start()
    {
        biomes = new[]{water, sand, grass};
        seed = Random.Range(0f, 10000f);
        GenerateTexture();
        GenerateHeightMap();
        GenerateBiomeMap();
        GenerateMesh();
    }


    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (Input.GetKey(KeyCode.S))
            {
                seed = Random.Range(0f, 10000f);
            }
            GenerateTexture();
            GenerateHeightMap();
            GenerateBiomeMap();
            GenerateMesh();
        }
    }

    void GenerateTexture()
    {
        Texture2D texture = new Texture2D(biomes.Sum(p => p.numSteps), 1);
        int uvIndex = 0;
        
        foreach(Biome biome in biomes){
            for(int i = 0; i < biome.numSteps; i++, uvIndex++){
                Color color = biome.startColor * (biome.numSteps - 1 - i) / (biome.numSteps - 1) 
                    + biome.endColor * i / (biome.numSteps - 1);
                texture.SetPixel(uvIndex, 0, color);
            }
        }

        texture.filterMode = FilterMode.Point;
        texture.Apply();
        textureAtlas = texture;
    }

    void GenerateHeightMap()
    {
        heightMap = new float[size.x, size.y];

        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                float xCoord = seed + x * perlinNoiseScale;
                float yCoord = seed + y * perlinNoiseScale;
                float height = Mathf.PerlinNoise(xCoord, yCoord);
                height = Mathf.Clamp(height, 0f, 0.999f);
                heightMap[x, y] = height;
            }
        }
    }

    void GenerateBiomeMap()
    {
        biomeMap = new int[size.x, size.y];
        
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                int b = 0;
                for (int i = 0; i < biomes.Length; i++)
                {
                    float upperBorder = i == biomes.Length - 1 ? 1 : biomes[i + 1].startHeight;
                    if (heightMap[x, y] > upperBorder)
                    {
                        continue;
                    }
                    else
                    {
                        b = i;
                        break;
                    }
                }
                
                biomeMap[x, y] = b;
            }
        }
    }

    public void GenerateMesh()
    {
        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        var vertices = new Vector3[size.x * size.y * 8];
        var triangles = new int[size.x * size.y * 12];
        var uvs = new Vector2[vertices.Length];
        var normals = new List<Vector3>();

        //int sqNum = 0;
        int trNum = 0;
        int vertNum = 0;

        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++, vertNum += 4, trNum += 6)
            {
                bool isWaterTile = biomeMap[x, y] == 0 ? true : false;
                float height = isWaterTile ? -waterDepth : 0;
                Vector2 uv = GetUV(x, y);

                vertices[vertNum] = new Vector3(0 + x, height, 0 + y + 1);
                vertices[vertNum + 1] = new Vector3(1 + x, height, 0 + y + 1);
                vertices[vertNum + 2] = new Vector3(0 + x, height, 0 + y);
                vertices[vertNum + 3] = new Vector3(1 + x, height, 0 + y);

                uvs[vertNum] = uv;
                uvs[vertNum + 1] = uv;
                uvs[vertNum + 2] = uv;
                uvs[vertNum + 3] = uv;

                triangles[trNum] = (vertNum);
                triangles[trNum + 1] = (vertNum + 1);
                triangles[trNum + 2] = (vertNum + 2);
                triangles[trNum + 3] = (vertNum + 1);
                triangles[trNum + 4] = (vertNum + 3);
                triangles[trNum + 5] = (vertNum + 2);

                bool isEdgeTile = (x == 0 ? true : false) || (y == 0 ? true : false) || (x == size.x - 1 ? true : false) || (y == size.y - 1 ? true : false);
                bool isLandTile = !isWaterTile;

                void AddVertices(Vector3 nws, Vector3 nes, int v){
                        vertNum += 4;
                        vertices[vertNum] = nws;
                        vertices[vertNum + 1] = nes;
                        vertices[vertNum + 2] = nws + Vector3.down * edgeDepth + new Vector3(0, (isWaterTile ? 0 : -waterDepth), 0);
                        vertices[vertNum + 3] = nes + Vector3.down * edgeDepth + new Vector3(0, (isWaterTile ? 0 : -waterDepth), 0);
                        uvs[vertNum] = uv;
                        uvs[vertNum + 1] = uv;
                        uvs[vertNum + 2] = uv;
                        uvs[vertNum + 3] = uv;

                        int[][] variants = new int[][]{new int[]{0, 1, 2, 1, 3, 2}, new int[]{0, 2, 1, 1, 2, 3}, new int[]{0, 1, 2, 2, 1, 3}, new int[]{0, 2, 1, 2, 3, 1}};
                        trNum += 6;
                        triangles[trNum] = (vertNum);
                        triangles[trNum + 1] = (vertNum + variants[v][1]);
                        triangles[trNum + 2] = (vertNum + variants[v][2]);
                        triangles[trNum + 3] = (vertNum + variants[v][3]);
                        triangles[trNum + 4] = (vertNum + variants[v][4]);
                        triangles[trNum + 5] = (vertNum + variants[v][5]);
                }

                //Страшный кусок кода, отвечающий за перегородки
                if(isLandTile || isEdgeTile)
                {
                    if(x == 0 || (isLandTile && biomeMap[x - 1, y] == 0))
                    {
                        Vector3 nws = new Vector3(x, height, y + 1);
                        Vector3 nes = nws + Vector3.back;
                        AddVertices(nws, nes, 0);
                    }

                    if(x == size.x - 1 || (isLandTile && biomeMap[x + 1, y] == 0))
                    {
                        Vector3 nws = new Vector3(x + 1 , height, y + 1);
                        Vector3 nes = nws + Vector3.back;
                        AddVertices(nws, nes, 1);
                    }

                    if (y == 0 || (isLandTile && biomeMap[x, y - 1] == 0))
                    {
                        Vector3 nws = new Vector3(x, height, y);
                        Vector3 nes = nws + Vector3.right;
                        AddVertices(nws, nes, 2);
                    }

                    if (y == size.y - 1 || isLandTile && biomeMap[x, y + 1] == 0)
                    {
                        Vector3 nws = new Vector3(x, height, y + 1);
                        Vector3 nes = nws + Vector3.right;
                        AddVertices(nws, nes, 3);
                    }                    
                }
            }
        }
        
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0, true);
        mesh.SetUVs (0, uvs);
        mesh.RecalculateNormals();
        GetComponent<MeshFilter>().mesh = mesh;
        GetComponent<MeshRenderer>().material.SetTexture("_MainTex", textureAtlas);
    }

    Vector2 GetUV(int x, int y){
        Biome biome;
        int uvx = 0;

        for (int i = 0; i < biomes.Length; i++)
        {
            biome = biomes[i];
            float upperBorder = i == biomes.Length - 1? 1 : biomes[i + 1].startHeight;
            bool isNeedToBreak = false;
            for(int j = 1; j <= biome.numSteps; j++){
                if(heightMap[x,y] - biome.startHeight < (upperBorder - biome.startHeight) / biome.numSteps * j){
                    uvx += j - 1;
                    isNeedToBreak = true;
                    break;
                }
            }

            if (isNeedToBreak)
            {
                break;
            }
            uvx += biome.numSteps;
        }

        // if(biome == 0){
        //     for(int i = 1; i <= water.numSteps; i++){
        //         if(heightMap[x,y] < sand.startHeight / water.numSteps * i){
        //             uvx += i - 1;
        //             break;
        //         }
        //     }
        // }
        // else if(biome == 1){
        //     uvx += water.numSteps;
        //
        //     for(int i = 1; i <= sand.numSteps; i++){
        //         if(heightMap[x,y] - sand.startHeight < (grass.startHeight - sand.startHeight) / sand.numSteps * i){
        //             uvx += i - 1;
        //             break;
        //         }
        //     }
        // }
        // else if(biome == 2){
        //     uvx += water.numSteps;
        //     uvx += sand.numSteps;
        //
        //     for(int i = 1; i <= grass.numSteps; i++){
        //         if(heightMap[x,y] - grass.startHeight < (1 - grass.startHeight) / grass.numSteps * i){
        //             //Debug.Log("WHY!" + " " + (uvx + i));
        //             uvx += i - 1;
        //             break;
        //         }
        //     }
        // }

        return new Vector2(1f / biomes.Sum(p => p.numSteps) * uvx, 0);
    }
}



