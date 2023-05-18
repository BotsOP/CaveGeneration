using System;
using System.Collections.Generic;
using System.Linq;
using Managers;
using UnityEngine;
using UnityEngine.Rendering;
using EventType = Managers.EventType;

[RequireComponent(typeof(CaveManager))]
public class CaveVectorField : MonoBehaviour
{
    public RenderTexture vectorField;
    public RenderTexture combinedNoiseTex;
    public Vector3 bottomLeftCorner
    {
        get 
        {
            Vector3 chunkIndex = GetChunkIndex(player.position);
            return chunks[(int)chunkIndex.x - 1, (int)chunkIndex.y, (int)chunkIndex.z - 1].position;
        }
    }
    private RenderTexture emptyTex;
    private CaveManager caveManager;
    private ComputeShader vectorFieldShader;
    private ComputeShader vectorFieldFollowShader;
    private Transform player => caveManager.player;
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

        vectorFieldShader.GetKernelThreadGroupSizes(0, out uint threadGroupSizeX, out uint threadGroupSizeY, out uint threadGroupSizeZ);
        threadGroupSize = new Vector3(threadGroupSizeX, threadGroupSizeY, threadGroupSizeZ);
        
        vectorField = new RenderTexture(chunkSize * 3, chunkSize * 2, 0, RenderTextureFormat.ARGBHalf)
        {
            filterMode = FilterMode.Point,
            dimension = TextureDimension.Tex3D,
            volumeDepth = chunkSize * 3,
            enableRandomWrite = true,
        };
        
        combinedNoiseTex = new RenderTexture(chunkSize * 3, chunkSize * 2, 0, RenderTextureFormat.R8)
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
        
        EventSystem<bool>.Subscribe(EventType.UPDATE_VECTORFIELD, GenerateVectorField);
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
        
        EventSystem<bool>.Unsubscribe(EventType.UPDATE_VECTORFIELD, GenerateVectorField);
    }

    private void Update()
    {
        if (Time.timeSinceLevelLoad > 3)
        {
            Vector3 pos = player.position;
            Vector3Int playerPos = new Vector3Int(Mathf.RoundToInt(pos.x), Mathf.RoundToInt(pos.y), Mathf.RoundToInt(pos.z));

            if (ManhattenDistance(playerPos, cachedPos) > 0)
            {
                cachedPos = playerPos;
                GenerateVectorField(false);
            }
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
        _worldPos -= bottomLeftCorner;
        
        vectorFieldFollowShader.SetTexture(0, "vectorField", vectorField);
        vectorFieldFollowShader.SetBuffer(0, "dir", directionBuffer);
        vectorFieldFollowShader.SetVector("pos", _worldPos);
        
        vectorFieldFollowShader.Dispatch(0, 1, 1, 1);

        Vector3[] dirArray = new Vector3[1];
        directionBuffer.GetData(dirArray);
        Debug.Log($"{dirArray[0]}  pos: {_worldPos}");
        return dirArray[0];
    }

    private void GenerateVectorField(bool editedTerrain)
    {
        ClearVectorField();

        Vector3 playerPos = player.position;
        Vector3Int chunkIndex = GetChunkIndex(playerPos);
        CaveChunk chunk = chunks[chunkIndex.x, chunkIndex.y, chunkIndex.z];
        Vector3Int startPos = new Vector3Int((int)(playerPos.x - chunk.position.x), (int)(playerPos.y - chunk.position.y), (int)(playerPos.z - chunk.position.z));
        startPos += new Vector3Int(chunkSize, 0, chunkSize);
        consumePoints.SetData(new[]
        {
            startPos + new Vector3Int(-1, -1 , -1),
            startPos + new Vector3Int(0, -1 , -1),
            startPos + new Vector3Int(1, -1 , -1),
            startPos + new Vector3Int(-1, -1 , 0),
            startPos + new Vector3Int(0, -1 , 0),
            startPos + new Vector3Int(1, -1 , 0),
            startPos + new Vector3Int(-1, -1 , 1),
            startPos + new Vector3Int(0, -1 , 1),
            startPos + new Vector3Int(1, -1 , 1),
            
            startPos + new Vector3Int(-1, 0 , -1),
            startPos + new Vector3Int(0, 0 , -1),
            startPos + new Vector3Int(1, 0 , -1),
            startPos + new Vector3Int(-1, 0 , 0),
            startPos,
            startPos + new Vector3Int(1, 0 , 0),
            startPos + new Vector3Int(-1, 0 , 1),
            startPos + new Vector3Int(0, 0 , 1),
            startPos + new Vector3Int(1, 0 , 1),
            
            startPos + new Vector3Int(-1, 1 , -1),
            startPos + new Vector3Int(0, 1 , -1),
            startPos + new Vector3Int(1, 1 , -1),
            startPos + new Vector3Int(-1, 1 , 0),
            startPos + new Vector3Int(0, 1 , 0),
            startPos + new Vector3Int(1, 1 , 0),
            startPos + new Vector3Int(-1, 1 , 1),
            startPos + new Vector3Int(0, 1 , 1),
            startPos + new Vector3Int(1, 1 , 1),
        });

        if (chunkIndex != cachedChunkIndex || editedTerrain)
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
        vectorFieldShader.SetVector("playerPos", player.position - bottomLeftCorner);
        
        vectorFieldShader.SetTexture(1, "vectorField", vectorField);
        int threadSizeClear = Mathf.CeilToInt(vectorField.width / 8f);
        int threadSizeClearY = Mathf.CeilToInt(vectorField.height / 8f);
        vectorFieldShader.Dispatch(1, threadSizeClear, threadSizeClearY, threadSizeClear);
    }

    private void UpdateCombinedNoiseTex(Vector3Int _chunkIndex)
    {
        Debug.Log(_chunkIndex);
        //_chunkIndex = new Vector3Int(1, 0, 1);
        CaveChunk chunk = chunks[_chunkIndex.x, _chunkIndex.y, _chunkIndex.z];
        vectorFieldShader.SetTexture(2, "noiseTex", chunk.noiseTex);

        CaveChunk chunkAdjacent;

        chunkAdjacent = chunks[_chunkIndex.x - 1, 1, _chunkIndex.z - 1];
        vectorFieldShader.SetTexture(2, "noiseTexTopLeftBack", chunkAdjacent.noiseTex);
        chunkAdjacent = chunks[_chunkIndex.x, 1, _chunkIndex.z - 1];
        vectorFieldShader.SetTexture(2, "noiseTexTopMiddleBack", chunkAdjacent.noiseTex);
        chunkAdjacent = chunks[_chunkIndex.x + 1, 1, _chunkIndex.z - 1];
        vectorFieldShader.SetTexture(2, "noiseTexTopRightBack", chunkAdjacent.noiseTex);
            
        chunkAdjacent = chunks[_chunkIndex.x - 1, 1, _chunkIndex.z];
        vectorFieldShader.SetTexture(2, "noiseTexTopLeftMiddle", chunkAdjacent.noiseTex);
        chunkAdjacent = chunks[_chunkIndex.x, 1, _chunkIndex.z];
        vectorFieldShader.SetTexture(2, "noiseTexTopMiddleMiddle", chunkAdjacent.noiseTex);
        chunkAdjacent = chunks[_chunkIndex.x + 1, 1, _chunkIndex.z];
        vectorFieldShader.SetTexture(2, "noiseTexTopRightMiddle", chunkAdjacent.noiseTex);
            
        chunkAdjacent = chunks[_chunkIndex.x - 1, 1, _chunkIndex.z + 1];
        vectorFieldShader.SetTexture(2, "noiseTexTopLeftForward", chunkAdjacent.noiseTex);
        chunkAdjacent = chunks[_chunkIndex.x, 1, _chunkIndex.z + 1];
        vectorFieldShader.SetTexture(2, "noiseTexTopMiddleForward", chunkAdjacent.noiseTex);
        chunkAdjacent = chunks[_chunkIndex.x + 1, 1, _chunkIndex.z + 1];
        vectorFieldShader.SetTexture(2, "noiseTexTopRightForward", chunkAdjacent.noiseTex);

        chunkAdjacent = chunks[_chunkIndex.x - 1, 0, _chunkIndex.z - 1];
        vectorFieldShader.SetTexture(2, "noiseTexMiddleLeftBack", chunkAdjacent.noiseTex);
        chunkAdjacent = chunks[_chunkIndex.x, 0, _chunkIndex.z - 1];
        vectorFieldShader.SetTexture(2, "noiseTexMiddleMiddleBack", chunkAdjacent.noiseTex);
        chunkAdjacent = chunks[_chunkIndex.x + 1, 0, _chunkIndex.z - 1];
        vectorFieldShader.SetTexture(2, "noiseTexMiddleRightBack", chunkAdjacent.noiseTex);
            
        chunkAdjacent = chunks[_chunkIndex.x - 1, 0, _chunkIndex.z];
        vectorFieldShader.SetTexture(2, "noiseTexMiddleLeftMiddle", chunkAdjacent.noiseTex);
        chunkAdjacent = chunks[_chunkIndex.x + 1, 0, _chunkIndex.z];
        vectorFieldShader.SetTexture(2, "noiseTexMiddleRightMiddle", chunkAdjacent.noiseTex);
            
        chunkAdjacent = chunks[_chunkIndex.x - 1, 0, _chunkIndex.z + 1];
        vectorFieldShader.SetTexture(2, "noiseTexMiddleLeftForward", chunkAdjacent.noiseTex);
        chunkAdjacent = chunks[_chunkIndex.x, 0, _chunkIndex.z + 1];
        vectorFieldShader.SetTexture(2, "noiseTexMiddleMiddleForward", chunkAdjacent.noiseTex);
        chunkAdjacent = chunks[_chunkIndex.x + 1, 0, _chunkIndex.z + 1];
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

