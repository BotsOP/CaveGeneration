
using UnityEngine;

public class CaveGenerator
{
    public Mesh caveMesh;
    private ComputeShader caveGenerationShader;
    private Vector3 threadGroupSizeOut;
    private Vector3 threadGroupSize;
    private int kernelIndex;
    private ComputeBuffer pointsBuffer;
    private ComputeBuffer resultsBuffer;
    private int chunkSize;
    private Vector3 chunkPosition;

    public CaveGenerator(int _chunkSize, Vector3 _chunkPosition)
    {
        chunkSize = _chunkSize;
        chunkPosition = _chunkPosition;
        caveGenerationShader = Resources.Load<ComputeShader>("MatchDrawingShader");
        caveMesh = new Mesh();
        caveMesh.vertexBufferTarget |= GraphicsBuffer.Target.Structured;
        caveMesh.indexBufferTarget |= GraphicsBuffer.Target.Structured;
    }

    public Mesh GenerateMesh()
    {
        GraphicsBuffer newVertexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Append,
            (int)Mathf.Pow(chunkSize, 3), sizeof(float) * (3 + 3 + 4));
        GraphicsBuffer newIndexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Append,
            (int)Mathf.Pow(chunkSize, 3), sizeof(int));
        
        
        
        newVertexBuffer.Dispose();
        newIndexBuffer.Dispose();
        return caveMesh;
    }
    
}
