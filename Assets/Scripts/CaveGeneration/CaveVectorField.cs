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
    public RenderTexture combinedNoiseTex;
    private RenderTexture emptyTex;
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
    private Vector3Int cachedPos;
    private Vector3Int cachedChunkIndex;
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
            
        pointsBufferPing = new ComputeBuffer(2000000, sizeof(int) * 3, ComputeBufferType.Append);
        pointsBufferPong = new ComputeBuffer(2000000, sizeof(int) * 3, ComputeBufferType.Append);
        directionBuffer = new ComputeBuffer(1, sizeof(float) * 3, ComputeBufferType.Structured);
        countBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Structured);
        countBuffer.SetData(new int[1]);

        instance = this;
        
        vectorFieldShader.GetKernelThreadGroupSizes(0, out uint threadGroupSizeX, out uint threadGroupSizeY, out uint threadGroupSizeZ);
        threadGroupSize = new Vector3(threadGroupSizeX, threadGroupSizeY, threadGroupSizeZ);
        
        vectorField = new RenderTexture(chunkSize * 3, chunkSize * 3, 0, RenderTextureFormat.ARGB32)
        {
            filterMode = FilterMode.Point,
            dimension = TextureDimension.Tex3D,
            volumeDepth = chunkSize * 3,
            enableRandomWrite = true,
        };
        ClearVectorField();
        
        combinedNoiseTex = new RenderTexture(chunkSize * 3, chunkSize * 3, 0, RenderTextureFormat.R8)
        {
            filterMode = FilterMode.Point,
            dimension = TextureDimension.Tex3D,
            volumeDepth = chunkSize * 3,
            enableRandomWrite = true,
        };
        
        emptyTex = new RenderTexture(chunkSize, chunkSize, 0, RenderTextureFormat.R8)
        {
            filterMode = FilterMode.Point,
            dimension = TextureDimension.Tex3D,
            volumeDepth = chunkSize,
            enableRandomWrite = true,
        };
        vectorFieldShader.SetTexture(1, "vectorField", emptyTex);
        vectorFieldShader.Dispatch(1, 4, 4, 4);
    }

    private void OnDisable()
    {
        pointsBufferPing.Dispose();
        pointsBufferPing = null;
        pointsBufferPong.Dispose();
        pointsBufferPong = null;
        countBuffer.Dispose();
        countBuffer = null;
        directionBuffer?.Dispose();
        directionBuffer = null;
    }

    private void Update()
    {
        Vector3 pos = player.position;
        Vector3Int playerPos = new Vector3Int((int)pos.x, (int)pos.y, (int)pos.z);
        if (Input.GetKey(KeyCode.N))
        {
            UpdateCombinedNoiseTex(playerPos);
        }

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
        ClearVectorField();

        Vector3Int chunkIndex = GetChunkIndex(_worldPos);
        //Vector3Int chunkIndex = new Vector3Int(4, 0, 4);
        CaveChunk chunk = chunks[chunkIndex.x, chunkIndex.y, chunkIndex.z];
        Vector3Int startPos = new Vector3Int((int)(_worldPos.x - chunk.position.x), (int)(_worldPos.y - chunk.position.y), (int)(_worldPos.z - chunk.position.z));
        startPos += new Vector3Int(32, 32, 32);
        consumePoints.SetData(new[] { startPos });

        if (chunkIndex != cachedChunkIndex)
        {
            cachedChunkIndex = chunkIndex;
            UpdateCombinedNoiseTex(chunkIndex);
        }

        consumePoints.SetCounterValue(1);
        appendPoints.SetCounterValue(0);

        int amountPointsToCheck = 1;
        int amountLoops = 0;
        while (amountPointsToCheck > 0)
        {
            int threadGroupX = Mathf.CeilToInt(amountPointsToCheck / threadGroupSize.x);
            
            vectorFieldShader.SetTexture(0, "vectorField", vectorField);
            vectorFieldShader.SetTexture(0, "noiseTex", combinedNoiseTex);
            vectorFieldShader.SetBuffer(0, "consumePoints", consumePoints);
            vectorFieldShader.SetBuffer(0, "appendPoints", appendPoints);
            vectorFieldShader.SetBuffer(0, "counter", countBuffer);
            vectorFieldShader.SetFloat("isoLevel", isoLevel);
            vectorFieldShader.SetInt("chunkSize", chunkSize * 3);
            
            vectorFieldShader.Dispatch(0, threadGroupX, 1, 1);

            int[] countArray = new int[1];
            countBuffer.GetData(countArray);
            amountPointsToCheck = countArray[0];
            countBuffer.SetData(new int[1]);

            // Int3[] ints = new Int3[amountPointsToCheck];
            // consumePoints.GetData(ints);
            //
            // Int3[] ints2 = new Int3[amountPointsToCheck];
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
    private void ClearVectorField()
    {
        vectorFieldShader.SetTexture(1, "vectorField", vectorField);
        int threadSizeClear = Mathf.CeilToInt(vectorField.width / 8f);
        vectorFieldShader.Dispatch(1, threadSizeClear, threadSizeClear, threadSizeClear);
    }

    private void UpdateCombinedNoiseTex(Vector3Int _chunkIndex)
    {
        Debug.Log(_chunkIndex);
        //_chunkIndex = new Vector3Int(1, 0, 1);
        CaveChunk chunk = chunks[_chunkIndex.x, _chunkIndex.y, _chunkIndex.z];
        vectorFieldShader.SetTexture(2, "noiseTex", chunk.noiseTex);

        CaveChunk chunkAdjacent;
        if (_chunkIndex.y - 1 >= 0)
        {
            chunkAdjacent = chunks[_chunkIndex.x - 1, _chunkIndex.y - 1, _chunkIndex.z - 1];
            vectorFieldShader.SetTexture(2, "noiseTexBottomLeftBack", chunkAdjacent.noiseTex);
            chunkAdjacent = chunks[_chunkIndex.x, _chunkIndex.y - 1, _chunkIndex.z - 1];
            vectorFieldShader.SetTexture(2, "noiseTexBottomMiddleBack", chunkAdjacent.noiseTex);
            chunkAdjacent = chunks[_chunkIndex.x + 1, _chunkIndex.y - 1, _chunkIndex.z - 1];
            vectorFieldShader.SetTexture(2, "noiseTexBottomRightBack", chunkAdjacent.noiseTex);
            
            chunkAdjacent = chunks[_chunkIndex.x - 1, _chunkIndex.y - 1, _chunkIndex.z];
            vectorFieldShader.SetTexture(2, "noiseTexBottomLeftMiddle", chunkAdjacent.noiseTex);
            chunkAdjacent = chunks[_chunkIndex.x, _chunkIndex.y - 1, _chunkIndex.z];
            vectorFieldShader.SetTexture(2, "noiseTexBottomMiddleMiddle", chunkAdjacent.noiseTex);
            chunkAdjacent = chunks[_chunkIndex.x + 1, _chunkIndex.y - 1, _chunkIndex.z];
            vectorFieldShader.SetTexture(2, "noiseTexBottomRightMiddle", chunkAdjacent.noiseTex);
            
            chunkAdjacent = chunks[_chunkIndex.x - 1, _chunkIndex.y - 1, _chunkIndex.z + 1];
            vectorFieldShader.SetTexture(2, "noiseTexBottomLeftForward", chunkAdjacent.noiseTex);
            chunkAdjacent = chunks[_chunkIndex.x, _chunkIndex.y - 1, _chunkIndex.z + 1];
            vectorFieldShader.SetTexture(2, "noiseTexBottomMiddleForward", chunkAdjacent.noiseTex);
            chunkAdjacent = chunks[_chunkIndex.x + 1, _chunkIndex.y - 1, _chunkIndex.z + 1];
            vectorFieldShader.SetTexture(2, "noiseTexBottomRightForward", chunkAdjacent.noiseTex);
        }
        else
        {
            vectorFieldShader.SetTexture(2, "noiseTexBottomLeftBack", emptyTex);
            vectorFieldShader.SetTexture(2, "noiseTexBottomMiddleBack", emptyTex);
            vectorFieldShader.SetTexture(2, "noiseTexBottomRightBack", emptyTex);
            vectorFieldShader.SetTexture(2, "noiseTexBottomLeftMiddle", emptyTex);
            vectorFieldShader.SetTexture(2, "noiseTexBottomMiddleMiddle", emptyTex);
            vectorFieldShader.SetTexture(2, "noiseTexBottomRightMiddle", emptyTex);
            vectorFieldShader.SetTexture(2, "noiseTexBottomLeftForward", emptyTex);
            vectorFieldShader.SetTexture(2, "noiseTexBottomMiddleForward", emptyTex);
            vectorFieldShader.SetTexture(2, "noiseTexBottomRightForward", emptyTex);

        }
        if (_chunkIndex.y + 1 < amountChunksVertical)
        {
            chunkAdjacent = chunks[_chunkIndex.x - 1, _chunkIndex.y + 1, _chunkIndex.z - 1];
            vectorFieldShader.SetTexture(2, "noiseTexTopLeftBack", chunkAdjacent.noiseTex);
            chunkAdjacent = chunks[_chunkIndex.x, _chunkIndex.y + 1, _chunkIndex.z - 1];
            vectorFieldShader.SetTexture(2, "noiseTexTopMiddleBack", chunkAdjacent.noiseTex);
            chunkAdjacent = chunks[_chunkIndex.x + 1, _chunkIndex.y + 1, _chunkIndex.z - 1];
            vectorFieldShader.SetTexture(2, "noiseTexTopRightBack", chunkAdjacent.noiseTex);
            
            chunkAdjacent = chunks[_chunkIndex.x - 1, _chunkIndex.y + 1, _chunkIndex.z];
            vectorFieldShader.SetTexture(2, "noiseTexTopLeftMiddle", chunkAdjacent.noiseTex);
            chunkAdjacent = chunks[_chunkIndex.x, _chunkIndex.y + 1, _chunkIndex.z];
            vectorFieldShader.SetTexture(2, "noiseTexTopMiddleMiddle", chunkAdjacent.noiseTex);
            chunkAdjacent = chunks[_chunkIndex.x + 1, _chunkIndex.y + 1, _chunkIndex.z];
            vectorFieldShader.SetTexture(2, "noiseTexTopRightMiddle", chunkAdjacent.noiseTex);
            
            chunkAdjacent = chunks[_chunkIndex.x - 1, _chunkIndex.y + 1, _chunkIndex.z + 1];
            vectorFieldShader.SetTexture(2, "noiseTexTopLeftForward", chunkAdjacent.noiseTex);
            chunkAdjacent = chunks[_chunkIndex.x, _chunkIndex.y + 1, _chunkIndex.z + 1];
            vectorFieldShader.SetTexture(2, "noiseTexTopMiddleForward", chunkAdjacent.noiseTex);
            chunkAdjacent = chunks[_chunkIndex.x + 1, _chunkIndex.y + 1, _chunkIndex.z + 1];
            vectorFieldShader.SetTexture(2, "noiseTexTopRightForward", chunkAdjacent.noiseTex);
        }
        else
        {
            vectorFieldShader.SetTexture(2, "noiseTexTopLeftBack", emptyTex);
            vectorFieldShader.SetTexture(2, "noiseTexTopMiddleBack", emptyTex);
            vectorFieldShader.SetTexture(2, "noiseTexTopRightBack", emptyTex);
            vectorFieldShader.SetTexture(2, "noiseTexTopLeftMiddle", emptyTex);
            vectorFieldShader.SetTexture(2, "noiseTexTopMiddleMiddle", emptyTex);
            vectorFieldShader.SetTexture(2, "noiseTexTopRightMiddle", emptyTex);
            vectorFieldShader.SetTexture(2, "noiseTexTopLeftForward", emptyTex);
            vectorFieldShader.SetTexture(2, "noiseTexTopMiddleForward", emptyTex);
            vectorFieldShader.SetTexture(2, "noiseTexTopRightForward", emptyTex);
        }
        
        chunkAdjacent = chunks[_chunkIndex.x - 1, _chunkIndex.y, _chunkIndex.z - 1];
        vectorFieldShader.SetTexture(2, "noiseTexMiddleLeftBack", chunkAdjacent.noiseTex);
        chunkAdjacent = chunks[_chunkIndex.x, _chunkIndex.y, _chunkIndex.z - 1];
        vectorFieldShader.SetTexture(2, "noiseTexMiddleMiddleBack", chunkAdjacent.noiseTex);
        chunkAdjacent = chunks[_chunkIndex.x + 1, _chunkIndex.y, _chunkIndex.z - 1];
        vectorFieldShader.SetTexture(2, "noiseTexMiddleRightBack", chunkAdjacent.noiseTex);
            
        chunkAdjacent = chunks[_chunkIndex.x - 1, _chunkIndex.y, _chunkIndex.z];
        vectorFieldShader.SetTexture(2, "noiseTexMiddleLeftMiddle", chunkAdjacent.noiseTex);
        chunkAdjacent = chunks[_chunkIndex.x + 1, _chunkIndex.y, _chunkIndex.z];
        vectorFieldShader.SetTexture(2, "noiseTexMiddleRightMiddle", chunkAdjacent.noiseTex);
            
        chunkAdjacent = chunks[_chunkIndex.x - 1, _chunkIndex.y, _chunkIndex.z + 1];
        vectorFieldShader.SetTexture(2, "noiseTexMiddleLeftForward", chunkAdjacent.noiseTex);
        chunkAdjacent = chunks[_chunkIndex.x, _chunkIndex.y, _chunkIndex.z + 1];
        vectorFieldShader.SetTexture(2, "noiseTexMiddleMiddleForward", chunkAdjacent.noiseTex);
        chunkAdjacent = chunks[_chunkIndex.x + 1, _chunkIndex.y, _chunkIndex.z + 1];
        vectorFieldShader.SetTexture(2, "noiseTexMiddleRightForward", chunkAdjacent.noiseTex);

        vectorFieldShader.SetTexture(2, "combinedNoiseTex", combinedNoiseTex);

        int threadSize = Mathf.CeilToInt(chunkSize * 3f / 8);
        vectorFieldShader.Dispatch(2, threadSize, threadSize, threadSize);
    }

    private Vector3Int GetChunkIndex(Vector3 _pos)
    {
        Vector3 remapped = _pos.Remap(
            caveBounds[0], caveBounds[1], Vector3.zero,
            new Vector3(amountChunksHorizontal, amountChunksVertical, amountChunksHorizontal));
        return new Vector3Int((int)remapped.x, (int)remapped.y, (int)remapped.z);
    }

    struct Int3
    {
        public int x;
        public int y;
        public int z;
    }
}

