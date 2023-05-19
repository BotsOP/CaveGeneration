using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class CaveChunk
{
    public Mesh caveMesh;
    public RenderTexture noiseTex;
    public Vector3 position;
    public GameObject gameObject;
    public MeshFilter meshFilter;
    public List<GameObject> decorations;
    public float isoLevel;
    private ComputeShader caveGenerationShader;
    private ComputeShader noiseGenerationShader;
    private Vector3 threadGroupSizeOut;
    private Vector3 threadGroupSize;
    private int kernelIndex;
    private int chunkSize;
    private int amountChunksVertial;
    private Bounds meshBounds;
    public GraphicsBuffer vertexBuffer;
    public GraphicsBuffer indexBuffer;
    private ComputeBuffer amountVertsBuffer;
    private GraphicsBuffer appendTrianglesBuffer;

    public CaveChunk(float _chunkSize, int _amountChunksVertial, Vector3 _position, float _isoLevel, float _noiseScale, int _amountDecorations, GameObject _gameObject, GameObject _decorationObject)
    {
        chunkSize = (int)_chunkSize;
        amountChunksVertial = _amountChunksVertial;
        position = _position;
        meshFilter = _gameObject.GetComponent<MeshFilter>();
        gameObject = _gameObject;
        isoLevel = _isoLevel;
        decorations = new List<GameObject>();
        caveGenerationShader = Resources.Load<ComputeShader>("CaveGeneration");
        noiseGenerationShader = Resources.Load<ComputeShader>("3DNoise");
        
        caveMesh = new Mesh();
        caveMesh.vertexBufferTarget |= GraphicsBuffer.Target.Structured;
        caveMesh.indexBufferTarget |= GraphicsBuffer.Target.Structured;
        caveMesh.AddVertexAttribute(new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, dimension: 3));
        caveMesh.indexFormat = IndexFormat.UInt32;
        
        float boundsSize = _chunkSize / 2;
        meshBounds = new Bounds(new Vector3(boundsSize, boundsSize, boundsSize), new Vector3(_chunkSize, _chunkSize, _chunkSize));
        meshFilter.mesh = caveMesh;

        noiseTex = new RenderTexture(chunkSize, chunkSize, 0, RenderTextureFormat.R8)
        {
            filterMode = FilterMode.Point,
            dimension = TextureDimension.Tex3D,
            volumeDepth = chunkSize,
            enableRandomWrite = true,
        };
        
        amountVertsBuffer = new ComputeBuffer(1, sizeof(uint));
        amountVertsBuffer.SetData(new [] { 0 });
        int amountMaxVerts = (int)Mathf.Pow(chunkSize, 3) * 3;
        appendTrianglesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Append, amountMaxVerts, sizeof(float) * (3 + 3));

        Initialize(_noiseScale);
        GenerateMesh();
        SpawnDecorations(_decorationObject);
    }
    public CaveChunk(float _chunkSize, int _amountChunksVertial, Vector3 _position, float _isoLevel, float _noiseScale, GameObject _gameObject, MeshFilter _meshFilter, List<GameObject> _decorations)
    {
        chunkSize = (int)_chunkSize;
        amountChunksVertial = _amountChunksVertial;
        position = _position;
        meshFilter = _meshFilter;
        gameObject = _gameObject;
        decorations = _decorations;
        isoLevel = _isoLevel;
        caveGenerationShader = Resources.Load<ComputeShader>("CaveGeneration");
        noiseGenerationShader = Resources.Load<ComputeShader>("3DNoise");
        
        caveMesh = new Mesh();
        caveMesh.vertexBufferTarget |= GraphicsBuffer.Target.Structured;
        caveMesh.indexBufferTarget |= GraphicsBuffer.Target.Structured;
        caveMesh.AddVertexAttribute(new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, dimension: 3));
        caveMesh.indexFormat = IndexFormat.UInt32;
        float boundsSize = _chunkSize / 2;
        
        meshBounds = new Bounds(new Vector3(boundsSize, boundsSize, boundsSize), new Vector3(_chunkSize, _chunkSize, _chunkSize));
        meshFilter.mesh = caveMesh;

        noiseTex = new RenderTexture(chunkSize, chunkSize, 0, RenderTextureFormat.R8)
        {
            dimension = TextureDimension.Tex3D,
            volumeDepth = chunkSize,
            enableRandomWrite = true,
        };
        
        amountVertsBuffer = new ComputeBuffer(1, sizeof(uint));
        amountVertsBuffer.SetData(new [] { 0 });
        int amountMaxVerts = (int)Mathf.Pow(chunkSize, 3) * 3;
        appendTrianglesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Append, amountMaxVerts, sizeof(float) * (3 + 3));

        Initialize(_noiseScale);
        GenerateMesh();
        SpawnDecorations();
    }

    public void OnDestroy()
    {
        appendTrianglesBuffer?.Dispose();
        vertexBuffer?.Dispose();
        indexBuffer?.Dispose();
        amountVertsBuffer?.Dispose();
        noiseTex.Release();
    }

    private void Initialize(float _noiseScale)
    {
        noiseGenerationShader.GetKernelThreadGroupSizes(0, out uint threadGroupSizeX, out uint threadGroupSizeY, out uint threadGroupSizeZ);
        
        threadGroupSize.x = Mathf.CeilToInt((float)chunkSize / threadGroupSizeX);
        threadGroupSize.y = Mathf.CeilToInt((float)chunkSize / threadGroupSizeY);
        threadGroupSize.z = Mathf.CeilToInt((float)chunkSize / threadGroupSizeZ);
        
        noiseGenerationShader.SetTexture(0, "noiseTex", noiseTex);
        noiseGenerationShader.SetFloat("noiseScale", chunkSize * _noiseScale);
        noiseGenerationShader.SetVector("noiseOffset", position);
        noiseGenerationShader.SetInt("roof", chunkSize * amountChunksVertial);
        noiseGenerationShader.Dispatch(0, (int)threadGroupSize.x, (int)threadGroupSize.y, (int)threadGroupSize.z);
    }

    public void GenerateMesh()
    {
        appendTrianglesBuffer.SetCounterValue(0);

        caveGenerationShader.GetKernelThreadGroupSizes(0, out uint threadGroupSizeX, out uint threadGroupSizeY, out uint threadGroupSizeZ);
        
        threadGroupSize.x = Mathf.CeilToInt((float)chunkSize / threadGroupSizeX);
        threadGroupSize.y = Mathf.CeilToInt((float)chunkSize / threadGroupSizeY);
        threadGroupSize.z = Mathf.CeilToInt((float)chunkSize / threadGroupSizeZ);
        
        caveGenerationShader.SetBuffer(0, "appendTriangleBuffer", appendTrianglesBuffer);
        caveGenerationShader.SetBuffer(0, "amountTrianglesBuffer", amountVertsBuffer);
        caveGenerationShader.SetTexture(0, "noiseTex", noiseTex);
        caveGenerationShader.SetInt("chunkSize",chunkSize);
        caveGenerationShader.SetFloat("isoLevel", isoLevel);
        caveGenerationShader.Dispatch(0, (int)threadGroupSize.x, (int)threadGroupSize.y, (int)threadGroupSize.z);
        
        int[] amountTrianglesArray = new int[1];
        amountVertsBuffer.GetData(amountTrianglesArray);
        int amountTriangles = amountTrianglesArray[0];
        int amountVerts = amountTriangles * 3;

        if (amountVerts == 0)
        {
            return;
        }

        caveMesh.SetVertices(new Vector3[amountVerts]);
        caveMesh.SetIndices(new int[amountVerts], MeshTopology.Triangles, 0);

        vertexBuffer?.Dispose();
        indexBuffer?.Dispose();
        vertexBuffer = caveMesh.GetVertexBuffer(0);
        indexBuffer = caveMesh.GetIndexBuffer();
        
        caveGenerationShader.GetKernelThreadGroupSizes(1, out threadGroupSizeX, out _, out _);
        
        threadGroupSize.x = Mathf.CeilToInt((float)amountTriangles / threadGroupSizeX);
        caveGenerationShader.SetBuffer(1, "triangleBuffer", appendTrianglesBuffer);
        caveGenerationShader.SetBuffer(1, "vertexBuffer", vertexBuffer);
        caveGenerationShader.SetBuffer(1, "indexBuffer", indexBuffer);
        caveGenerationShader.Dispatch(1, (int)threadGroupSize.x, 1, 1);

        caveMesh.bounds = meshBounds;
        amountVertsBuffer.SetData(new [] { 0 });
    }

    private void SpawnDecorations(GameObject _decoration)
    {
        Vector3 rayOrigin = position + new Vector3(chunkSize / 2f, chunkSize / 2f, chunkSize / 2f);
        RayOutput rayOutput;
        int maxAmountOfRays = 10;
        int index = 0;
        while (true)
        {
            Vector3 rayDirection = new Vector3(Random.Range(-1, 1), Random.Range(-1, 1), Random.Range(-1, 1)).normalized * 32;
            if (GPUPhysics.RayIntersectMesh(vertexBuffer, indexBuffer, position, rayOrigin, rayDirection, out rayOutput))
            {
                GameObject tempDeco = Object.Instantiate(_decoration, rayOutput.position, Quaternion.LookRotation(rayOutput.normal));
                decorations.Add(tempDeco);
                return;
            }

            index++;
            if (index > maxAmountOfRays)
            {
                Debug.LogWarning($"After shooting {maxAmountOfRays} rays still couldnt hit anything");
                return;
            }
        }
        
    }
    private void SpawnDecorations()
    {
        foreach (var decoration in decorations)
        {
            Vector3 rayOrigin = position + new Vector3(chunkSize / 2f, chunkSize / 2f, chunkSize / 2f);
            RayOutput rayOutput;
            int maxAmountOfRays = 10;
            int index = 0;
            while (true)
            {
                Vector3 rayDirection = new Vector3(Random.Range(-1, 1), Random.Range(-1, 1), Random.Range(-1, 1)).normalized * 32;
                if (GPUPhysics.RayIntersectMesh(vertexBuffer, indexBuffer, position, rayOrigin, rayDirection, out rayOutput))
                {
                    decoration.transform.position = rayOutput.position;
                    decoration.transform.rotation = Quaternion.LookRotation(rayOutput.normal);
                    return;
                }

                index++;
                if (index > maxAmountOfRays)
                {
                    Debug.LogWarning($"After shooting {maxAmountOfRays} rays still couldnt hit anything2");
                    return;
                }
            }
        }
    }
    
    struct Vertex
    {
        public Vector3 position;
        public Vector3 normal;
    }
    
}
