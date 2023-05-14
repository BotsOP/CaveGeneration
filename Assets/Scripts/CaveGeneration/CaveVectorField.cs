using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(CaveManager))]
public class CaveVectorField : MonoBehaviour
{
    public Transform player;
    public RenderTexture vectorField;
    private CaveManager caveManager;
    private ComputeShader vectorFieldShader;
    private int chunkSize => caveManager.chunkSize;
    private CaveChunk[,,] chunks => caveManager.chunks;
    private Vector3[] caveBounds => caveManager.caveBounds;
    private int amountChunksHorizontal => caveManager.amountChunksHorizontal;
    private int amountChunksVertical => caveManager.amountChunksVertical;
    private float isoLevel => caveManager.isoLevel;
    private ComputeBuffer countBuffer;
    private ComputeBuffer appendPoints;
    private ComputeBuffer seedPoints;
    private Vector3 threadGroupSize;
    //private bool shouldPing;

    // private ComputeBuffer appendPoints
    // {
    //     get
    //     {
    //         if (shouldPing)
    //             return pointsBufferPing;
    //         
    //         return pointsBufferPong;
    //     }
    // }
    // private ComputeBuffer consumePoints
    // {
    //     get
    //     {
    //         if (shouldPing)
    //             return pointsBufferPong;
    //         
    //         return pointsBufferPing;
    //     }
    // }

    private void OnEnable()
    {
        caveManager = gameObject.GetComponent<CaveManager>();
        
        vectorFieldShader = Resources.Load<ComputeShader>("SDFVectorFieldPathfinder");
        appendPoints = new ComputeBuffer(20000, sizeof(int) * 3, ComputeBufferType.Append);
        seedPoints = new ComputeBuffer(20001, sizeof(int) * 3, ComputeBufferType.Structured);
        appendPoints.SetData(new Int3[20000]);
        appendPoints.SetCounterValue(0);
        seedPoints.SetData(new Int3[20000]);
        seedPoints.SetCounterValue(0);
        countBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Structured);
        countBuffer.SetData(new int[1]);
        
        vectorFieldShader.GetKernelThreadGroupSizes(0, out uint threadGroupSizeX, out uint threadGroupSizeY, out uint threadGroupSizeZ);
        threadGroupSize = new Vector3(threadGroupSizeX, threadGroupSizeY, threadGroupSizeZ);
        
        vectorField = new RenderTexture(chunkSize, chunkSize, 0, RenderTextureFormat.ARGBFloat)
        {
            filterMode = FilterMode.Point,
            dimension = TextureDimension.Tex3D,
            volumeDepth = chunkSize,
            enableRandomWrite = true,
        };
    }

    private void OnDisable()
    {
        appendPoints.Dispose();
        appendPoints = null;
        seedPoints.Dispose();
        seedPoints = null;
        countBuffer.Dispose();
        countBuffer = null;
    }

    private void Update()
    {
        if (Input.GetKey(KeyCode.N))
        {
            GenerateVectorField(player.position);
        }
    }

    public RenderTexture GenerateVectorField(Vector3 _worldPos)
    {
        vectorField.Release();
        appendPoints.SetData(new Int3[20000]);
        appendPoints.SetCounterValue(0);
        seedPoints.SetData(new Int3[20000]);
        countBuffer.SetData(new int[1]);

        Vector3 chunkIndex = GetChunkIndex(_worldPos);
        CaveChunk chunk = chunks[(int)chunkIndex.x, (int)chunkIndex.y, (int)chunkIndex.z];

        Vector3Int startPos = new Vector3Int((int)(_worldPos.x - chunk.position.x),
            (int)(_worldPos.y - chunk.position.y), (int)(_worldPos.z - chunk.position.z));
        Vector3Int[] startPosArray = { startPos };
        seedPoints.SetData(startPosArray);
        int amountPointsToCheck = 1;

        int amountLoops = 0;
        while (amountPointsToCheck > 0)
        {
            int threadGroupX = Mathf.CeilToInt(amountPointsToCheck / threadGroupSize.x);
            
            vectorFieldShader.SetTexture(0, "vectorField", vectorField);
            vectorFieldShader.SetTexture(0, "noiseTex", chunk.noiseTex);
            vectorFieldShader.SetBuffer(0, "seedPoints", seedPoints);
            vectorFieldShader.SetBuffer(0, "appendPoints", appendPoints);
            vectorFieldShader.SetBuffer(0, "counter", countBuffer);
            vectorFieldShader.SetFloat("isoLevel", isoLevel);
            vectorFieldShader.SetInt("amountPoints", amountPointsToCheck);
            
            vectorFieldShader.Dispatch(0, threadGroupX, 6, 1);

            int[] countArray = new int[1];
            countBuffer.GetData(countArray);
            amountPointsToCheck = countArray[0];
            countBuffer.SetData(new int[1]);

            Int3[] ints = new Int3[amountPointsToCheck];
            appendPoints.GetData(ints);
            ints = ints.Distinct().ToArray();
            
            appendPoints.SetCounterValue(0);
            Vector3Int[] test = new Vector3Int[10];
            seedPoints.GetData(test);
            seedPoints.SetData(ints);
            
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

    struct Int3
    {
        public int x;
        public int y;
        public int z;
    }
}

