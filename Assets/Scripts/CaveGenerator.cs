
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

public class CaveGenerator
{
    public Mesh caveMesh;
    public RenderTexture noiseTex;
    public Vector3 chunkPosition;
    private ComputeShader caveGenerationShader;
    private ComputeShader noiseGenerationShader;
    private Vector3 threadGroupSizeOut;
    private Vector3 threadGroupSize;
    private int kernelIndex;
    private int chunkSize;
    private Bounds meshBounds;
    public GraphicsBuffer vertexBuffer;
    public GraphicsBuffer indexBuffer;
    private ComputeBuffer amountVertsBuffer;
    private GraphicsBuffer appendTrianglesBuffer;
    private MeshFilter meshFilter;

    public CaveGenerator(float _chunkSize, Vector3 _chunkPosition, float _isoLevel, float _noiseScale, MeshFilter _meshFilter)
    {
        chunkSize = (int)_chunkSize;
        chunkPosition = _chunkPosition;
        meshFilter = _meshFilter;
        caveGenerationShader = Resources.Load<ComputeShader>("CaveGeneration");
        noiseGenerationShader = Resources.Load<ComputeShader>("3DNoise");
        
        caveMesh = new Mesh();
        caveMesh.vertexBufferTarget |= GraphicsBuffer.Target.Structured;
        caveMesh.indexBufferTarget |= GraphicsBuffer.Target.Structured;
        caveMesh.AddVertexAttribute(new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, dimension: 3));
        caveMesh.indexFormat = IndexFormat.UInt32;
        float boundsSize = _chunkSize / 2;
        meshBounds = new Bounds(new Vector3(boundsSize, boundsSize, boundsSize), new Vector3(_chunkSize, _chunkSize, _chunkSize));
        meshFilter.sharedMesh = caveMesh;

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
        GenerateMesh(_isoLevel);
    }

    public void OnDestroy()
    {
        appendTrianglesBuffer?.Dispose();
        vertexBuffer?.Dispose();
        indexBuffer?.Dispose();
        amountVertsBuffer?.Dispose();
    }

    public RenderTexture Initialize(float _noiseScale)
    {
        noiseGenerationShader.GetKernelThreadGroupSizes(0, out uint threadGroupSizeX, out uint threadGroupSizeY, out uint threadGroupSizeZ);
        
        threadGroupSize.x = Mathf.CeilToInt((float)chunkSize / threadGroupSizeX);
        threadGroupSize.y = Mathf.CeilToInt((float)chunkSize / threadGroupSizeY);
        threadGroupSize.z = Mathf.CeilToInt((float)chunkSize / threadGroupSizeZ);
        
        noiseGenerationShader.SetTexture(0, "noiseTex", noiseTex);
        noiseGenerationShader.SetFloat("noiseScale", chunkSize * _noiseScale);
        noiseGenerationShader.SetVector("noiseOffset", chunkPosition);
        noiseGenerationShader.Dispatch(0, (int)threadGroupSize.x, (int)threadGroupSize.y, (int)threadGroupSize.z);
        return noiseTex;
    }

    public void GenerateMesh(float isoLevel)
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
    
    struct Vertex
    {
        public Vector3 position;
        public Vector3 normal;

        public Vertex(Vector3 _position, Vector3 _normal)
        {
            position = _position;
            normal = _normal;
        }
    }
    
}
