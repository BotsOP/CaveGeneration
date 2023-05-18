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
    [Range(8, 128)] public int chunkSize = 32;
    [Range(0.01f, 1)] public float isoLevel;
    [Range(0.01f, 1)] public float noiseScale;
    [Range(1, 32)] public int amountChunksHorizontal;
    [Range(0.1f, 1)] public float caveScale;
    public LayerMask caveMask;
    [NonSerialized] public int amountChunksVertical = 2;
    [NonSerialized] public CaveChunk[,,] chunks;
    [NonSerialized] public Vector3[] caveBounds;
    private float caveWidth;
    private int stepSize;

    private float NoiseScale => noiseScale / caveScale;

    private void OnEnable()
    {
        stepSize = (int)(chunkSize * caveScale) - 1;
        
        caveWidth = amountChunksHorizontal * stepSize;

        caveBounds = new Vector3[2];
        caveBounds[0] = new Vector3(0, 0, 0);
        caveBounds[1] = new Vector3(caveWidth, amountChunksVertical * stepSize, caveWidth);

        chunks = new CaveChunk[amountChunksHorizontal, amountChunksVertical, amountChunksHorizontal];

        for (int i = 0; i < chunks.GetLength(0); i++)
        for (int j = 0; j < chunks.GetLength(1); j++)
        for (int k = 0; k < chunks.GetLength(2); k++)
        {
            Vector3 index = new Vector3(i, j, k);
            Vector3 pos = index * stepSize;
            GameObject meshObject = Instantiate(meshContainer, pos, Quaternion.identity, transform);
            chunks[(int)index.x, (int)index.y, (int)index.z] = new CaveChunk(chunkSize * caveScale, amountChunksVertical, pos, 
                                                                             isoLevel, NoiseScale, 1, meshObject, caveDecoration);
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

        // if (Input.GetKeyDown(KeyCode.C))
        // {
        //     AddChunksLeft();
        // }
        // if (Input.GetKeyDown(KeyCode.V))
        // {
        //     AddChunksRight();
        // }
        // if (Input.GetKeyDown(KeyCode.X))
        // {
        //     AddChunksForward();
        // }
        // if (Input.GetKeyDown(KeyCode.Z))
        // {
        //     AddChunksBackward();
        // }
    }
    private void PlaceChunksAroundPlayer(Vector3 _playerPos)
    {
        Vector3 playerChunkIndex = _playerPos.Remap(caveBounds[0], caveBounds[1], Vector3.zero, 
            new Vector3(amountChunksHorizontal, amountChunksVertical, amountChunksHorizontal));
        
        if (Mathf.Abs(playerChunkIndex.x - amountChunksHorizontal / 2f) > 1)
        {
            if (playerChunkIndex.x < amountChunksHorizontal / 2f)
            {
                AddChunksLeft();
                Debug.Log($"shifted chunks left");
                return;
            }
            Debug.Log($"shifted chunks right");
            AddChunksRight();
        }
        if (Mathf.Abs(playerChunkIndex.z - amountChunksHorizontal / 2f) > 1)
        {
            if (playerChunkIndex.z < amountChunksHorizontal / 2f)
            {
                AddChunksBackward();
                Debug.Log($"shifted chunks backward");
                return;
            }
            Debug.Log($"shifted chunks forward");
            AddChunksForward();
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
        {
            for (int j = chunks.GetLength(1) - 1; j >= 0; j--)
            {
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
                                          discardedObjects.Pop(), discardedMeshFilters.Pop(), discardedDecorations.Pop());
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
            }
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
        {
            for (int j = 0; j < chunks.GetLength(1); j++)
            {
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
                                          discardedObjects.Pop(), discardedMeshFilters.Pop(), discardedDecorations.Pop());
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
            }
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
        {
            for (int j = 0; j < chunks.GetLength(1); j++)
            {
                for (int k = 0; k < chunks.GetLength(2); k++)
                {
                    if (k == chunks.GetLength(0) - 1)
                    {
                        Vector3 index = new Vector3(i, j, k);
                        Vector3 pos = chunks[i, j, k].position;
                        pos.z += stepSize;
                        
                        discardedObjects.Peek().transform.position = pos;
                        
                        chunks[(int)index.x, (int)index.y, (int)index.z] = 
                            new CaveChunk(chunkSize * caveScale, amountChunksHorizontal, pos, isoLevel, NoiseScale, 
                                          discardedObjects.Pop(), discardedMeshFilters.Pop(), discardedDecorations.Pop());
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
            }
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
        {
            for (int j = chunks.GetLength(1) - 1; j >= 0; j--)
            {
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
                                          discardedObjects.Pop(), discardedMeshFilters.Pop(), discardedDecorations.Pop());
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
            }
        }
    }

    #endregion
}
