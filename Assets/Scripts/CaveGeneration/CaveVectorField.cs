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
    public Transform player => caveManager.player;
    public int chunkSize => caveManager.chunkSize;
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
    private List<RenderTexture> combinedTextures = new List<RenderTexture>();
    private Vector3Int[] chunkPositions;
    private int floodKernel;
    private int clearKernel;
    private int combineKernel;
    private int updateKernel;
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

        floodKernel = vectorFieldShader.FindKernel("FloodSDF");
        clearKernel = vectorFieldShader.FindKernel("ClearSDF");
        combineKernel = vectorFieldShader.FindKernel("CombineTexturesCube");
        updateKernel = vectorFieldShader.FindKernel("UpdateCombinedSDF");

        vectorFieldShader.GetKernelThreadGroupSizes(floodKernel, out uint threadGroupSizeX, out uint threadGroupSizeY, out uint threadGroupSizeZ);
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
        vectorFieldShader.SetTexture(clearKernel, "vectorField", emptyTex);
        vectorFieldShader.Dispatch(clearKernel, 4, 4, 4);
        
        chunkPositions = new []
        {
            new Vector3Int(-1, -1 , -1),
            new Vector3Int(0, -1 , -1),
            new Vector3Int(1, -1 , -1),
            new Vector3Int(-1, -1 , 0),
            new Vector3Int(0, -1 , 0),
            new Vector3Int(1, -1 , 0),
            new Vector3Int(-1, -1 , 1),
            new Vector3Int(0, -1 , 1),
            new Vector3Int(1, -1 , 1),
            
            new Vector3Int(-1, 0 , -1),
            new Vector3Int(0, 0 , -1),
            new Vector3Int(1, 0 , -1),
            new Vector3Int(-1, 0 , 0),
            new Vector3Int(0, 0 , 0),
            new Vector3Int(1, 0 , 0),
            new Vector3Int(-1, 0 , 1),
            new Vector3Int(0, 0 , 1),
            new Vector3Int(1, 0 , 1),
            
            new Vector3Int(-1, 1 , -1),
            new Vector3Int(0, 1 , -1),
            new Vector3Int(1, 1 , -1),
            new Vector3Int(-1, 1 , 0),
            new Vector3Int(0, 1 , 0),
            new Vector3Int(1, 1 , 0),
            new Vector3Int(-1, 1 , 1),
            new Vector3Int(0, 1 , 1),
            new Vector3Int(1, 1 , 1),
        };

        EventSystem<List<ChangedChunk>>.Subscribe(EventType.UPDATE_VECTORFIELD, UpdateCombinedNoiseTex);
        EventSystem.Subscribe(EventType.GENERATE_VECTORFIELD, GenerateNewNoiseTex);
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
        
        EventSystem<List<ChangedChunk>>.Unsubscribe(EventType.UPDATE_VECTORFIELD, UpdateCombinedNoiseTex);
        EventSystem.Unsubscribe(EventType.GENERATE_VECTORFIELD, GenerateNewNoiseTex);
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
    
    private void Update()
    {
        if (Time.timeSinceLevelLoad > 3)
        {
            Vector3 pos = player.position;
            Vector3Int playerPos = new Vector3Int(Mathf.RoundToInt(pos.x), Mathf.RoundToInt(pos.y), Mathf.RoundToInt(pos.z));

            if (ManhattenDistance(playerPos, cachedPos) > 0)
            {
                cachedPos = playerPos;
                GenerateVectorField();
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

    private void GenerateVectorField()
    {
        ClearVectorField();

        Vector3 playerPos = player.position;
        Vector3Int chunkIndex = GetChunkIndex(playerPos);
        CaveChunk chunk = chunks[chunkIndex.x, chunkIndex.y, chunkIndex.z];
        Vector3Int startPos = new Vector3Int((int)(playerPos.x - chunk.position.x), (int)(playerPos.y - chunk.position.y), (int)(playerPos.z - chunk.position.z));
        startPos += new Vector3Int(chunkSize, 0, chunkSize);
        
        int amountDimensions = 3;
        int baseAmount = -Mathf.FloorToInt(3 / 2.0f);
        int UpperAmount = Mathf.FloorToInt(3 / 2.0f);
        Vector3Int[] consumePointsArray = new Vector3Int[amountDimensions * amountDimensions * amountDimensions];
        
        for (int x = baseAmount; x <= UpperAmount; x++)
        for (int y = baseAmount; y <= UpperAmount; y++)
        for (int z = baseAmount; z <= UpperAmount; z++)
        {
            int index = x - baseAmount + ((y - baseAmount) * 9) + (z - baseAmount) * 3;
            consumePointsArray[index] = startPos + new Vector3Int(x, y, z);
        }
        
        consumePoints.SetData(consumePointsArray);

        consumePoints.SetCounterValue((uint)consumePointsArray.Length);
        appendPoints.SetCounterValue(0);

        int amountPointsToCheck = 1;
        int amountLoops = 0;
        while (amountPointsToCheck > 0)
        {
            int threadGroupX = Mathf.CeilToInt(amountPointsToCheck / threadGroupSize.x);
            
            vectorFieldShader.SetTexture(floodKernel, "vectorField", vectorField);
            vectorFieldShader.SetTexture(floodKernel, "noiseTex", combinedNoiseTex);
            vectorFieldShader.SetBuffer(floodKernel, "consumePoints", consumePoints);
            vectorFieldShader.SetBuffer(floodKernel, "appendPoints", appendPoints);
            vectorFieldShader.SetBuffer(floodKernel, "counter", countBuffer);
            vectorFieldShader.SetFloat("isoLevel", isoLevel);
            vectorFieldShader.SetInt("chunkSize", chunkSize * 3);
            
            vectorFieldShader.Dispatch(floodKernel, threadGroupX, 1, 1);
        
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
        
        vectorFieldShader.SetTexture(clearKernel, "vectorField", vectorField);
        int threadSizeClear = Mathf.CeilToInt(vectorField.width / 8f);
        int threadSizeClearY = Mathf.CeilToInt(vectorField.height / 8f);
        vectorFieldShader.Dispatch(clearKernel, threadSizeClear, threadSizeClearY, threadSizeClear);
    }

    private void GenerateNewNoiseTex()
    {
        Debug.Log($"generated new combined tex");
        combinedTextures.Clear();

        Vector3 playerPos = player.position;
        Vector3Int chunkIndex = GetChunkIndex(playerPos);
        CaveChunk chunk;
        
        chunk = chunks[chunkIndex.x - 1, 0, chunkIndex.z - 1];
        combinedTextures.Add(chunk.noiseTex);
        vectorFieldShader.SetTexture(combineKernel, "noiseTexMiddleLeftBack", chunk.noiseTex);
        chunk = chunks[chunkIndex.x, 0, chunkIndex.z - 1];
        combinedTextures.Add(chunk.noiseTex);
        vectorFieldShader.SetTexture(combineKernel, "noiseTexMiddleMiddleBack", chunk.noiseTex);
        chunk = chunks[chunkIndex.x + 1, 0, chunkIndex.z - 1];
        combinedTextures.Add(chunk.noiseTex);
        vectorFieldShader.SetTexture(combineKernel, "noiseTexMiddleRightBack", chunk.noiseTex);
            
        chunk = chunks[chunkIndex.x - 1, 0, chunkIndex.z];
        combinedTextures.Add(chunk.noiseTex);
        vectorFieldShader.SetTexture(combineKernel, "noiseTexMiddleLeftMiddle", chunk.noiseTex);
        chunk = chunks[chunkIndex.x, 0, chunkIndex.z];
        combinedTextures.Add(chunk.noiseTex);
        vectorFieldShader.SetTexture(combineKernel, "noiseTex", chunk.noiseTex);
        chunk = chunks[chunkIndex.x + 1, 0, chunkIndex.z];
        combinedTextures.Add(chunk.noiseTex);
        vectorFieldShader.SetTexture(combineKernel, "noiseTexMiddleRightMiddle", chunk.noiseTex);
            
        chunk = chunks[chunkIndex.x - 1, 0, chunkIndex.z + 1];
        combinedTextures.Add(chunk.noiseTex);
        vectorFieldShader.SetTexture(combineKernel, "noiseTexMiddleLeftForward", chunk.noiseTex);
        chunk = chunks[chunkIndex.x, 0, chunkIndex.z + 1];
        combinedTextures.Add(chunk.noiseTex);
        vectorFieldShader.SetTexture(combineKernel, "noiseTexMiddleMiddleForward", chunk.noiseTex);
        chunk = chunks[chunkIndex.x + 1, 0, chunkIndex.z + 1];
        combinedTextures.Add(chunk.noiseTex);
        vectorFieldShader.SetTexture(combineKernel, "noiseTexMiddleRightForward", chunk.noiseTex);
        
        
        chunk = chunks[chunkIndex.x - 1, 1, chunkIndex.z - 1];
        combinedTextures.Add(chunk.noiseTex);
        vectorFieldShader.SetTexture(combineKernel, "noiseTexTopLeftBack", chunk.noiseTex);
        chunk = chunks[chunkIndex.x, 1, chunkIndex.z - 1];
        combinedTextures.Add(chunk.noiseTex);
        vectorFieldShader.SetTexture(combineKernel, "noiseTexTopMiddleBack", chunk.noiseTex);
        chunk = chunks[chunkIndex.x + 1, 1, chunkIndex.z - 1];
        combinedTextures.Add(chunk.noiseTex);
        vectorFieldShader.SetTexture(combineKernel, "noiseTexTopRightBack", chunk.noiseTex);
            
        chunk = chunks[chunkIndex.x - 1, 1, chunkIndex.z];
        combinedTextures.Add(chunk.noiseTex);
        vectorFieldShader.SetTexture(combineKernel, "noiseTexTopLeftMiddle", chunk.noiseTex);
        chunk = chunks[chunkIndex.x, 1, chunkIndex.z];
        combinedTextures.Add(chunk.noiseTex);
        vectorFieldShader.SetTexture(combineKernel, "noiseTexTopMiddleMiddle", chunk.noiseTex);
        chunk = chunks[chunkIndex.x + 1, 1, chunkIndex.z];
        combinedTextures.Add(chunk.noiseTex);
        vectorFieldShader.SetTexture(combineKernel, "noiseTexTopRightMiddle", chunk.noiseTex);
            
        chunk = chunks[chunkIndex.x - 1, 1, chunkIndex.z + 1];
        combinedTextures.Add(chunk.noiseTex);
        vectorFieldShader.SetTexture(combineKernel, "noiseTexTopLeftForward", chunk.noiseTex);
        chunk = chunks[chunkIndex.x, 1, chunkIndex.z + 1];
        combinedTextures.Add(chunk.noiseTex);
        vectorFieldShader.SetTexture(combineKernel, "noiseTexTopMiddleForward", chunk.noiseTex);
        chunk = chunks[chunkIndex.x + 1, 1, chunkIndex.z + 1];
        combinedTextures.Add(chunk.noiseTex);
        vectorFieldShader.SetTexture(combineKernel, "noiseTexTopRightForward", chunk.noiseTex);

        vectorFieldShader.SetTexture(combineKernel, "combinedNoiseTex", combinedNoiseTex);

        int threadSize = Mathf.CeilToInt(chunkSize * 3f / 8);
        vectorFieldShader.Dispatch(combineKernel, threadSize, threadSize, threadSize);
    }
    
    private void UpdateCombinedNoiseTex(List<ChangedChunk> _changedChunks)
    { 
        int amountTexturesUpdated = 0;
        foreach (var changedChunk in _changedChunks)
        {
            int index = combinedTextures.FindIndex(tex => tex == changedChunk.renderTexture);
            if (index < 0)
            {
                continue;
            }
            //We dont want the bottom 9 squares
            index += 9;

            //We offset directions so that bottomleft is 0, 0, 0
            Vector3 startPosCombinedTex = chunkPositions[index] + new Vector3Int(1, 0, 1);
            startPosCombinedTex *= chunkSize;

            amountTexturesUpdated++;
            vectorFieldShader.SetTexture(updateKernel, "noiseTex", changedChunk.renderTexture);
            vectorFieldShader.SetTexture(updateKernel, "combinedNoiseTex", combinedNoiseTex);
            vectorFieldShader.SetVector("startPosNoiseTex", changedChunk.startPos);
            vectorFieldShader.SetVector("startPosCombinedTex", changedChunk.startPos + startPosCombinedTex);
            vectorFieldShader.Dispatch(updateKernel, changedChunk.dimensions.x, changedChunk.dimensions.y, changedChunk.dimensions.z);
        }
        if (amountTexturesUpdated > 0)
        {
            //GenerateVectorField();
        }
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

