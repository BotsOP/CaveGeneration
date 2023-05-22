using System;
using System.Collections.Generic;
using Managers;
using UnityEngine;
using EventType = Managers.EventType;

public class CaveManager : MonoBehaviour
{
    [SerializeField] private GameObject meshContainer;
    [SerializeField] private GameObject caveDecoration;
    public Transform player;
    [Range(1, 128)] public int chunkSize = 32;
    [Range(0.01f, 1)] public float isoLevel;
    [Range(0.01f, 1)] public float noiseScale;
    [Range(1, 32)] public int amountChunksHorizontal;
    [Range(0.1f, 1)] public float caveScale;
    [Range(0, 10)] public int amountDecorationsPerChunk;
    public LayerMask caveMask;
    [NonSerialized] public int amountChunksVertical = 2;
    [NonSerialized] public CaveChunk[,,] chunks;
    [NonSerialized] public Vector3[] caveBounds;

    private Vector2 caveCenter => new Vector2(caveBounds[0].x + (caveBounds[1].x - caveBounds[0].x), caveBounds[0].z + (caveBounds[1].z - caveBounds[0].z));
    private float caveWidth;
    private int stepSize;

    private float NoiseScale => noiseScale / caveScale;

    private void OnEnable()
    {
        stepSize = (int)(chunkSize * caveScale) - 1;
        
        caveWidth = amountChunksHorizontal * stepSize;

        BoxCollider boxCollider = meshContainer.GetComponent<BoxCollider>();
        boxCollider.center = new Vector3(stepSize / 2f, stepSize / 2f, stepSize / 2f);
        boxCollider.size = new Vector3(stepSize, stepSize, stepSize);

        caveBounds = new Vector3[2];
        caveBounds[0] = transform.position;
        caveBounds[1] = new Vector3(caveWidth, amountChunksVertical * stepSize, caveWidth) + transform.position;

        chunks = new CaveChunk[amountChunksHorizontal, amountChunksVertical, amountChunksHorizontal];
        
        for (int i = 0; i < chunks.GetLength(0); i++)
        for (int j = 0; j < chunks.GetLength(1); j++)
        for (int k = 0; k < chunks.GetLength(2); k++)
        {
            Vector3 index = new Vector3(i, j, k);
            Vector3 pos = index * stepSize + transform.position;
            GameObject meshObject = Instantiate(meshContainer, pos, Quaternion.identity, transform);
            chunks[(int)index.x, (int)index.y, (int)index.z] = new CaveChunk(chunkSize * caveScale, amountChunksVertical, pos, 
                                                                             isoLevel, NoiseScale, amountDecorationsPerChunk, meshObject, caveDecoration);
        }
    }

    private void Start()
    {
        for (int i = 0; i < chunks.GetLength(0); i++)
        for (int j = 0; j < chunks.GetLength(1); j++)
        for (int k = 0; k < chunks.GetLength(2); k++)
        {
            Vector3 index = new Vector3(i, j, k);
            chunks[(int)index.x, (int)index.y, (int)index.z].SpawnDecorations(caveDecoration, caveCenter);
        }
    }

    private void OnDisable()
    {
        for (int i = 0; i < chunks.GetLength(0); i++)
        for (int j = 0; j < chunks.GetLength(1); j++)
        for (int k = 0; k < chunks.GetLength(2); k++)
        {
            if (ReferenceEquals(chunks[i, j, k], null))
                continue;
                        
            chunks[i, j, k].OnDestroy();
        }
    }
    
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            for (int i = 0; i < chunks.GetLength(0); i++)
            for (int j = 0; j < chunks.GetLength(1); j++)
            for (int k = 0; k < chunks.GetLength(2); k++)
            {
                if (ReferenceEquals(chunks[i, j, k], null))
                    continue;
                        
                chunks[i, j, k].GenerateMesh();
            }
        }
        
        PlaceChunksAroundPlayer(player.position);
    }
    private Vector3Int cachedPlayerChunkIndex;
    private void PlaceChunksAroundPlayer(Vector3 _playerPos)
    {
        Vector3 playerChunkIndexTemp = GetChunkIndex(_playerPos);

        Vector3Int playerChunkIndex = new Vector3Int(Mathf.FloorToInt(playerChunkIndexTemp.x), Mathf.FloorToInt(playerChunkIndexTemp.y), Mathf.FloorToInt(playerChunkIndexTemp.z));

        bool shiftedChunks = false;
        if (playerChunkIndex.x > amountChunksHorizontal / 2)
        {
            Debug.Log($"shifted chunks left");
            AddChunksRight();
            shiftedChunks = true;
        }
        else if(playerChunkIndex.x < amountChunksHorizontal / 2)
        {
            Debug.Log($"shifted chunks right");
            AddChunksLeft();
            shiftedChunks = true;
        }
        
        if (playerChunkIndex.z > amountChunksHorizontal / 2)
        {
            Debug.Log($"shifted chunks left");
            AddChunksForward();
            shiftedChunks = true;
        }
        else if(playerChunkIndex.z < amountChunksHorizontal / 2)
        {
            Debug.Log($"shifted chunks right");
            AddChunksBackward();
            shiftedChunks = true;
        }

        if (shiftedChunks)
        {
            EventSystem.RaiseEvent(EventType.GENERATE_VECTORFIELD);
        }
    }

    private Vector3 GetChunkIndex(Vector3 _pos)
    {
        return _pos.Remap(
            caveBounds[0], caveBounds[1], Vector3.zero,
            new Vector3(amountChunksHorizontal, amountChunksVertical, amountChunksHorizontal));
    }

    #region AddChunks

    private void AddChunksLeft()
    {
        caveBounds[0].x -= stepSize;
        caveBounds[1].x -= stepSize;
        Stack<GameObject> discardedObjects = new Stack<GameObject>();
        Stack<MeshFilter> discardedMeshFilters = new Stack<MeshFilter>();
        Stack<List<GameObject>> discardedDecorations = new Stack<List<GameObject>>();

        for (int i = chunks.GetLength(0) - 1; i >= 0; i--)
        for (int j = chunks.GetLength(1) - 1; j >= 0; j--)
        for (int k = chunks.GetLength(2) - 1; k >= 0; k--)
        {
            if (i == 0)
            {
                Vector3 index = new Vector3(i, j, k);
                Vector3 pos = chunks[i, j, k].position;
                pos.x -= stepSize;
                        
                discardedObjects.Peek().transform.position = pos;
                        
                chunks[(int)index.x, (int)index.y, (int)index.z] = 
                    new CaveChunk(chunkSize * caveScale, amountChunksVertical, pos, isoLevel, NoiseScale, 
                                  discardedObjects.Pop(), discardedMeshFilters.Pop(), amountDecorationsPerChunk, caveDecoration, discardedDecorations.Pop());
            }
            else
            {
                if (i == chunks.GetLength(0) - 1)
                {
                    discardedObjects.Push(chunks[i, j, k].gameObject);
                    discardedMeshFilters.Push(chunks[i, j, k].meshFilter);
                    discardedDecorations.Push(chunks[i, j, k].decorations);
                    chunks[i, j, k].OnDestroy();
                }
                chunks[i, j, k] = chunks[i - 1, j, k];
            }
        }
        
        for (int j = chunks.GetLength(1) - 1; j >= 0; j--)
        for (int k = chunks.GetLength(2) - 1; k >= 0; k--)
        {
            Vector3 index = new Vector3(0, j, k);
            chunks[(int)index.x, (int)index.y, (int)index.z].RespawnDecorations(caveDecoration, caveCenter);
        }
    }
    private void AddChunksRight()
    {
        caveBounds[0].x += stepSize;
        caveBounds[1].x += stepSize;
        Stack<GameObject> discardedObjects = new Stack<GameObject>();
        Stack<MeshFilter> discardedMeshFilters = new Stack<MeshFilter>();
        Stack<List<GameObject>> discardedDecorations = new Stack<List<GameObject>>();
        
        for (int i = 0; i < chunks.GetLength(0); i++)
        for (int j = 0; j < chunks.GetLength(1); j++)
        for (int k = 0; k < chunks.GetLength(2); k++)
        {
            if (i == chunks.GetLength(0) - 1)
            {
                Vector3 index = new Vector3(i, j, k);
                Vector3 pos = chunks[i, j, k].position;
                pos.x += stepSize;
                        
                discardedObjects.Peek().transform.position = pos;
                        
                chunks[(int)index.x, (int)index.y, (int)index.z] = 
                    new CaveChunk(chunkSize * caveScale, amountChunksVertical, pos, isoLevel, NoiseScale, 
                                  discardedObjects.Pop(), discardedMeshFilters.Pop(), amountDecorationsPerChunk, caveDecoration, discardedDecorations.Pop());
            }
            else
            {
                if (i == 0)
                {
                    discardedObjects.Push(chunks[i, j, k].gameObject);
                    discardedMeshFilters.Push(chunks[i, j, k].meshFilter);
                    discardedDecorations.Push(chunks[i, j, k].decorations);
                    chunks[i, j, k].OnDestroy();
                }
                chunks[i, j, k] = chunks[i + 1, j, k];
            }
        }
        
        for (int j = chunks.GetLength(1) - 1; j >= 0; j--)
        for (int k = chunks.GetLength(2) - 1; k >= 0; k--)
        {
            Vector3 index = new Vector3(chunks.GetLength(0) - 1, j, k);
            chunks[(int)index.x, (int)index.y, (int)index.z].RespawnDecorations(caveDecoration, caveCenter);
        }
    }
    
    private void AddChunksForward()
    {
        caveBounds[0].z += stepSize;
        caveBounds[1].z += stepSize;
        Stack<GameObject> discardedObjects = new Stack<GameObject>();
        Stack<MeshFilter> discardedMeshFilters = new Stack<MeshFilter>();
        Stack<List<GameObject>> discardedDecorations = new Stack<List<GameObject>>();
        
        for (int i = 0; i < chunks.GetLength(0); i++)
        for (int j = 0; j < chunks.GetLength(1); j++)
        for (int k = 0; k < chunks.GetLength(2); k++)
        {
            if (k == chunks.GetLength(0) - 1)
            {
                Vector3 index = new Vector3(i, j, k);
                Vector3 pos = chunks[i, j, k].position;
                pos.z += stepSize;
                        
                discardedObjects.Peek().transform.position = pos;
                        
                chunks[(int)index.x, (int)index.y, (int)index.z] = 
                    new CaveChunk(chunkSize * caveScale, amountChunksVertical, pos, isoLevel, NoiseScale, 
                                  discardedObjects.Pop(), discardedMeshFilters.Pop(), amountDecorationsPerChunk, caveDecoration, discardedDecorations.Pop());
            }
            else
            {
                if (k == 0)
                {
                    discardedObjects.Push(chunks[i, j, k].gameObject);
                    discardedMeshFilters.Push(chunks[i, j, k].meshFilter);
                    discardedDecorations.Push(chunks[i, j, k].decorations);
                    chunks[i, j, k].OnDestroy();
                }
                chunks[i, j, k] = chunks[i, j, k + 1];
            }
        }
        
        for (int i = chunks.GetLength(0) - 1; i >= 0; i--)
        for (int j = chunks.GetLength(1) - 1; j >= 0; j--)
        {
            Vector3 index = new Vector3(i, j, chunks.GetLength(0) - 1);
            chunks[(int)index.x, (int)index.y, (int)index.z].RespawnDecorations(caveDecoration, caveCenter);
        }
    }
    
    private void AddChunksBackward()
    {
        caveBounds[0].z -= stepSize;
        caveBounds[1].z -= stepSize;
        Stack<GameObject> discardedObjects = new Stack<GameObject>();
        Stack<MeshFilter> discardedMeshFilters = new Stack<MeshFilter>();
        Stack<List<GameObject>> discardedDecorations = new Stack<List<GameObject>>();
        
        for (int i = chunks.GetLength(0) - 1; i >= 0; i--)
        for (int j = chunks.GetLength(1) - 1; j >= 0; j--)
        for (int k = chunks.GetLength(2) - 1; k >= 0; k--)
        {
            if (k == 0)
            {
                Vector3 index = new Vector3(i, j, k);
                Vector3 pos = chunks[i, j, k].position;
                pos.z -= stepSize;
                discardedObjects.Peek().transform.position = pos;
                        
                chunks[(int)index.x, (int)index.y, (int)index.z] = 
                    new CaveChunk(chunkSize * caveScale, amountChunksVertical, pos, isoLevel, NoiseScale, 
                                  discardedObjects.Pop(), discardedMeshFilters.Pop(), amountDecorationsPerChunk, caveDecoration, discardedDecorations.Pop());
            }
            else
            {
                if (k == chunks.GetLength(0) - 1)
                {
                    discardedObjects.Push(chunks[i, j, k].gameObject);
                    discardedMeshFilters.Push(chunks[i, j, k].meshFilter);
                    discardedDecorations.Push(chunks[i, j, k].decorations);
                    chunks[i, j, k].OnDestroy();
                }
                chunks[i, j, k] = chunks[i, j, k - 1];
            }
        }
        
        for (int i = chunks.GetLength(0) - 1; i >= 0; i--)
        for (int j = chunks.GetLength(1) - 1; j >= 0; j--)
        {
            Vector3 index = new Vector3(i, j, 0);
            chunks[(int)index.x, (int)index.y, (int)index.z].RespawnDecorations(caveDecoration, caveCenter);
        }
    }

    #endregion
}
