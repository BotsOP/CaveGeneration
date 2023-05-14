using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(CaveManager))]
public class CaveVectorField : MonoBehaviour
{
    public Transform player;
    private CaveManager caveManager;
    private Queue<PathResult> results = new Queue<PathResult>();
    private ComputeShader vectorFieldShader;
    public RenderTexture vectorField;
    private int chunkSize => caveManager.chunkSize;
    private CaveChunk[,,] chunks => caveManager.chunks;
    private Vector3[] caveBounds => caveManager.caveBounds;
    private int amountChunksHorizontal => caveManager.amountChunksHorizontal;
    private int amountChunksVertical => caveManager.amountChunksVertical;
    private float isoLevel => caveManager.isoLevel;
    private ComputeBuffer countBuffer;
    private ComputeBuffer pointsBufferPing;
    private ComputeBuffer pointsBufferPong;
    private bool shouldPing;
    private Vector3 threadGroupSize;

    private ComputeBuffer appendPoints
    {
        get
        {
            if (shouldPing)
                return pointsBufferPing;
            
            return pointsBufferPong;
        }
    }
    private ComputeBuffer consumePoints
    {
        get
        {
            if (shouldPing)
                return pointsBufferPong;
            
            return pointsBufferPing;
        }
    }

    private void OnEnable()
    {
        caveManager = gameObject.GetComponent<CaveManager>();
        
        vectorFieldShader = Resources.Load<ComputeShader>("SDFVectorFieldPathfinder");
        pointsBufferPing = new ComputeBuffer(1000, sizeof(int) * 3, ComputeBufferType.Structured);
        pointsBufferPong = new ComputeBuffer(1000, sizeof(int) * 3, ComputeBufferType.Structured);
        countBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Structured);
        
        vectorFieldShader.GetKernelThreadGroupSizes(0, out uint threadGroupSizeX, out uint threadGroupSizeY, out uint threadGroupSizeZ);
        threadGroupSize = new Vector3(threadGroupSizeX, threadGroupSizeY, threadGroupSizeZ);
        
        vectorField = new RenderTexture(chunkSize, chunkSize, 0, RenderTextureFormat.ARGBFloat)
        {
            dimension = TextureDimension.Tex3D,
            volumeDepth = chunkSize,
            enableRandomWrite = true,
        };
    }

    private void OnDisable()
    {
        pointsBufferPing.Dispose();
        pointsBufferPing = null;
        pointsBufferPong.Dispose();
        pointsBufferPong = null;
        countBuffer.Dispose();
        countBuffer = null;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.N))
        {
            GenerateVectorField(player.position);
        }
    }

    public RenderTexture GenerateVectorField(Vector3 _worldPos)
    {
        vectorField.Release();

        Vector3 chunkIndex = GetChunkIndex(_worldPos);
        CaveChunk chunk = chunks[(int)chunkIndex.x, (int)chunkIndex.y, (int)chunkIndex.z];

        Vector3Int startPos = new Vector3Int((int)(_worldPos.x - chunk.position.x),
            (int)(_worldPos.y - chunk.position.y), (int)(_worldPos.z - chunk.position.z));
        Vector3Int[] startPosArray = { startPos };
        appendPoints.SetData(startPosArray);

        int amountLoops = 0;
        int amountPointsToCheck = 1;
        while (amountPointsToCheck > 0)
        {
            int threadGroupX = Mathf.CeilToInt(amountPointsToCheck / threadGroupSize.x);
            
            appendPoints.SetCounterValue(0);

            vectorFieldShader.SetTexture(0, "vectorField", vectorField);
            vectorFieldShader.SetTexture(0, "noiseTex", chunk.noiseTex);
            vectorFieldShader.SetBuffer(0, "consumePoints", consumePoints);
            vectorFieldShader.SetBuffer(0, "appendPoints", appendPoints);
            vectorFieldShader.SetBuffer(0, "counter", countBuffer);
            vectorFieldShader.SetFloat("isoLevel", isoLevel);
            
            vectorFieldShader.Dispatch(0, threadGroupX, 1, 1);

            int[] countArray = new int[1];
            countBuffer.GetData(countArray);
            amountPointsToCheck = countArray[0];
            countBuffer.SetData(new int[1]);

            amountLoops++;
            if (amountLoops > 10000)
            {
                Debug.LogWarning($"exited vector field generation after looping {amountLoops} times");
                break;
            }
        }
        Debug.Log(amountLoops);
        return vectorField;
    }

    private Vector3 GetChunkIndex(Vector3 _pos)
    {
        return _pos.Remap(
            caveBounds[0], caveBounds[1], Vector3.zero,
            new Vector3(amountChunksHorizontal, amountChunksVertical, amountChunksHorizontal));
    }

    // public float GetTerrainValue(Vector3 _worldPos)
    // {
    //     //This doesnt work properly when its at the edge of a chunk but I dont care
    //     float[] corners = new float[27];
    //     
    //     Vector3 chunkIndex = GetChunkIndex(_worldPos);
    //     CaveChunk chunk = chunks[(int)chunkIndex.x, (int)chunkIndex.y, (int)chunkIndex.z];
    //     Vector3 subChunkPos = new Vector3((int)_worldPos.x - chunk.position.x, (int)_worldPos.y - chunk.position.y, (int)_worldPos.z - chunk.position.z);
    //     
    //     getSDFInfoShader.SetTexture(4, "noiseTex", chunk.noiseTex);
    //     getSDFInfoShader.SetBuffer(4, "neighbours", neighboursBuffer);
    //     getSDFInfoShader.SetVector("currentPos", subChunkPos);
    //
    //     getSDFInfoShader.Dispatch(4, 1, 1, 1);
    //
    //     neighboursBuffer.GetData(corners);
    //     
    //     Vector3 posInsideSubChunk = _worldPos - chunk.position - subChunkPos;
    //     posInsideSubChunk = new Vector3(Mathf.Abs(posInsideSubChunk.x), Mathf.Abs(posInsideSubChunk.y), Mathf.Abs(posInsideSubChunk.z));
    //     
    //     float c00 = corners[0] * (1 - posInsideSubChunk.x) + corners[1] * posInsideSubChunk.x;
    //     float c01 = corners[2] * (1 - posInsideSubChunk.x) + corners[3] * posInsideSubChunk.x;
    //     float c10 = corners[4] * (1 - posInsideSubChunk.x) + corners[5] * posInsideSubChunk.x;
    //     float c11 = corners[6] * (1 - posInsideSubChunk.x) + corners[7] * posInsideSubChunk.x;
    //
    //     float c0 = c00 * (1 - posInsideSubChunk.y) + c01 * posInsideSubChunk.y;
    //     float c1 = c10 * (1 - posInsideSubChunk.y) + c11 * posInsideSubChunk.y;
    //
    //     float result = c0 * (1 - posInsideSubChunk.z) + c1 * posInsideSubChunk.z;
    //     
    //     return result;
    // }
}

