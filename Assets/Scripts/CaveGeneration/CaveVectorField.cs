using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(CaveManager))]
public class CaveVectorField : MonoBehaviour
{
    public static CaveVectorField instance;
    public Transform player;
    public RenderTexture vectorField;
    private CaveManager caveManager;
    private ComputeShader vectorFieldShader;
    private ComputeShader vectorFieldFollowShader;
    private int chunkSize => caveManager.chunkSize;
    private CaveChunk[,,] chunks => caveManager.chunks;
    private Vector3[] caveBounds => caveManager.caveBounds;
    private int amountChunksHorizontal => caveManager.amountChunksHorizontal;
    private int amountChunksVertical => caveManager.amountChunksVertical;
    private float isoLevel => caveManager.isoLevel;
    private ComputeBuffer directionBuffer;
    private ComputeBuffer countBuffer;
    private ComputeBuffer pointsBufferPing;
    private ComputeBuffer pointsBufferPong;
    private Vector3 threadGroupSize;
    private bool shouldPing;
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
        vectorFieldFollowShader = Resources.Load<ComputeShader>("FollowVectorField");
            
        pointsBufferPing = new ComputeBuffer(100000, sizeof(int) * 3, ComputeBufferType.Append);
        pointsBufferPong = new ComputeBuffer(100000, sizeof(int) * 3, ComputeBufferType.Append);
        directionBuffer = new ComputeBuffer(1, sizeof(float) * 3, ComputeBufferType.Structured);
        countBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Structured);
        countBuffer.SetData(new int[1]);

        instance = this;
        
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
        pointsBufferPing.Dispose();
        pointsBufferPing = null;
        pointsBufferPong.Dispose();
        pointsBufferPong = null;
        countBuffer.Dispose();
        countBuffer = null;
    }

    private Vector3Int cachedPos;
    private void Update()
    {
        if (Input.GetKey(KeyCode.N))
        {
            GenerateVectorField(player.position);
        }

        Vector3 pos = player.position;
        Vector3Int playerPos = new Vector3Int((int)pos.x, (int)pos.y, (int)pos.z);
        if (ManhattenDistance(playerPos, cachedPos) > 0)
        {
            cachedPos = playerPos;
            GenerateVectorField(player.position);
        }
    }

    private int ManhattenDistance(Vector3Int a, Vector3Int b)
    {
        int diffX = Mathf.Abs(a.x - b.x);
        int diffY = Mathf.Abs(a.y - b.y);
        int diffZ = Mathf.Abs(a.z - b.z);

        return diffX + diffY + diffZ;
    }

    public Vector3 GetDirection(Vector3 _worldPos)
    {
        Vector3 chunkIndex = GetChunkIndex(_worldPos);
        CaveChunk chunk = chunks[(int)chunkIndex.x, (int)chunkIndex.y, (int)chunkIndex.z];

        Vector3 startPos = new Vector3((int)(_worldPos.x - chunk.position.x),
            (int)(_worldPos.y - chunk.position.y), (int)(_worldPos.z - chunk.position.z));
        
        vectorFieldFollowShader.SetTexture(0, "vectorField", vectorField);
        vectorFieldFollowShader.SetBuffer(0, "dir", directionBuffer);
        vectorFieldFollowShader.SetVector("pos", startPos);
        
        vectorFieldFollowShader.Dispatch(0, 1, 1, 1);

        Vector3[] dirArray = new Vector3[1];
        directionBuffer.GetData(dirArray);
        return dirArray[0];
    }

    private void GenerateVectorField(Vector3 _worldPos)
    {
        vectorFieldShader.SetTexture(1, "vectorField", vectorField);
        vectorFieldShader.Dispatch(1, 4, 4, 4);

        Vector3 chunkIndex = GetChunkIndex(_worldPos);
        CaveChunk chunk = chunks[(int)chunkIndex.x, (int)chunkIndex.y, (int)chunkIndex.z];

        Vector3Int startPos = new Vector3Int((int)(_worldPos.x - chunk.position.x),
            (int)(_worldPos.y - chunk.position.y), (int)(_worldPos.z - chunk.position.z));
        Vector3Int[] startPosArray = { startPos };
        consumePoints.SetData(startPosArray);
        consumePoints.SetCounterValue(1);
        appendPoints.SetCounterValue(0);
        
        Int3[] ints3 = new Int3[10];
        consumePoints.GetData(ints3);

        int amountPointsToCheck = 1;
        int amountLoops = 0;
        while (amountPointsToCheck > 0)
        {
            int threadGroupX = Mathf.CeilToInt(amountPointsToCheck / threadGroupSize.x);
            
            vectorFieldShader.SetTexture(0, "vectorField", vectorField);
            vectorFieldShader.SetTexture(0, "noiseTex", chunk.noiseTex);
            vectorFieldShader.SetBuffer(0, "consumePoints", consumePoints);
            vectorFieldShader.SetBuffer(0, "appendPoints", appendPoints);
            vectorFieldShader.SetBuffer(0, "counter", countBuffer);
            vectorFieldShader.SetFloat("isoLevel", isoLevel);
            vectorFieldShader.SetInt("amountPoints", amountPointsToCheck);
            
            vectorFieldShader.Dispatch(0, threadGroupX, 1, 1);

            int[] countArray = new int[1];
            countBuffer.GetData(countArray);
            amountPointsToCheck = countArray[0];
            countBuffer.SetData(new int[1]);

            // Int3[] ints = new Int3[10];
            // consumePoints.GetData(ints);
            //
            // Int3[] ints2 = new Int3[10];
            // appendPoints.GetData(ints2);

            shouldPing = !shouldPing;
            appendPoints.SetCounterValue(0);
            consumePoints.SetCounterValue((uint)amountPointsToCheck);

            if (amountLoops == 0 && amountPointsToCheck == 0)
            {
                Debug.LogWarning("Couldtn find any points");
            }
            
            amountLoops++;
            if (amountLoops >= 10000)
            {
                Debug.LogWarning($"exited vector field generation after looping {amountLoops} times");
                break;
            }
        }
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

