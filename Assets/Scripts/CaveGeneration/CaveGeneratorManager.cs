using System;
using System.Collections;
using System.Collections.Generic;
using Managers;
using UnityEngine;
using EventType = Managers.EventType;

public class CaveGeneratorManager : MonoBehaviour
{
    [SerializeField] private Transform playerTransform;
    [SerializeField] private GameObject meshContainer;
    [SerializeField] private GameObject caveDecoration;
    [SerializeField] private int chunkSize;
    [SerializeField, Range(0.01f, 1)] private float isoLevel;
    [SerializeField, Range(0.01f, 1)] private float noiseScale;
    [SerializeField, Range(1, 32)] private int amountChunksHorizontal;
    [SerializeField, Range(1, 32)] private int amountChunksVertical;
    [SerializeField, Range(0.1f, 1)] private float caveScale;
    [SerializeField] private LayerMask caveMask;
    public Transform sphere;
    private CaveChunk[,,] chunks;
    private CavePhysicsManager physicsManager;
    private CaveTerrainCarver terrainCarver;
    private Vector3[] caveBounds;
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
        physicsManager = new CavePhysicsManager(chunks, caveBounds, amountChunksHorizontal, amountChunksVertical);
        terrainCarver = new CaveTerrainCarver(chunks, caveBounds, amountChunksHorizontal, amountChunksVertical, chunkSize, caveMask);

        for (int i = 0; i < chunks.GetLength(0); i++)
        {
            for (int j = 0; j < chunks.GetLength(1); j++)
            {
                for (int k = 0; k < chunks.GetLength(2); k++)
                {
                    Vector3 index = new Vector3(i, j, k);
                    Vector3 pos = index * stepSize;
                    GameObject meshObject = Instantiate(meshContainer, pos, Quaternion.identity, transform);
                    chunks[(int)index.x, (int)index.y, (int)index.z] = new CaveChunk(chunkSize * caveScale, pos, 
                        isoLevel, NoiseScale, 1, meshObject, caveDecoration);
                }
            }
        }
        
        EventSystem<MyRay, float, float>.Subscribe(EventType.CARVE_TERRAIN, CarveTerrain);
    }

    private void OnDisable()
    {
        for (int i = 0; i < chunks.GetLength(0); i++)
        {
            for (int j = 0; j < chunks.GetLength(1); j++)
            {
                for (int k = 0; k < chunks.GetLength(2); k++)
                {
                    if (ReferenceEquals(chunks[i, j, k], null))
                        continue;
                        
                    chunks[i, j, k].OnDestroy();
                }
            }
        }
        
        EventSystem<MyRay, float, float>.Unsubscribe(EventType.CARVE_TERRAIN, CarveTerrain);
    }

    private void CarveTerrain(MyRay _ray, float _carveSize, float _carveSpeed)
    {
        if (physicsManager.Raycast(_ray.origin, _ray.direction, out var rayOutput))
        {
            Debug.DrawRay(playerTransform.position, playerTransform.forward * 1000);
            sphere.position = rayOutput.position;
            sphere.rotation = Quaternion.LookRotation(rayOutput.normal);
            terrainCarver.RemoveTerrain(rayOutput.position, _carveSize, _carveSpeed);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            for (int i = 0; i < chunks.GetLength(0); i++)
            {
                for (int j = 0; j < chunks.GetLength(1); j++)
                {
                    for (int k = 0; k < chunks.GetLength(2); k++)
                    {
                        if (ReferenceEquals(chunks[i, j, k], null))
                            continue;
                        
                        chunks[i, j, k].GenerateMesh(isoLevel);
                    }
                }
            }
        }
        
        Vector3 playerChunkIndex = GetChunkIndex(playerTransform.position);
        PlaceChunksAroundPlayer(playerChunkIndex);
        
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
    private void PlaceChunksAroundPlayer(Vector3 playerChunkIndex)
    {
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

    private Vector3 GetChunkIndex(Vector3 _playerPos)
    {
        return _playerPos.Remap(caveBounds[0], caveBounds[1], Vector3.zero, 
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
                        Vector3 pos = chunks[i, j, k].chunkPosition;
                        pos.x -= stepSize;
                        
                        discardedObjects.Peek().transform.position = pos;
                        
                        chunks[(int)index.x, (int)index.y, (int)index.z] = 
                            new CaveChunk(chunkSize * caveScale, pos, isoLevel, NoiseScale, 
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
                        Vector3 pos = chunks[i, j, k].chunkPosition;
                        pos.x += stepSize;
                        
                        discardedObjects.Peek().transform.position = pos;
                        
                        chunks[(int)index.x, (int)index.y, (int)index.z] = 
                            new CaveChunk(chunkSize * caveScale, pos, isoLevel, NoiseScale, 
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
                        Vector3 pos = chunks[i, j, k].chunkPosition;
                        pos.z += stepSize;
                        
                        discardedObjects.Peek().transform.position = pos;
                        
                        chunks[(int)index.x, (int)index.y, (int)index.z] = 
                            new CaveChunk(chunkSize * caveScale, pos, isoLevel, NoiseScale, 
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
                        Vector3 pos = chunks[i, j, k].chunkPosition;
                        pos.z -= stepSize;

                        discardedObjects.Peek().transform.position = pos;
                        
                        chunks[(int)index.x, (int)index.y, (int)index.z] = 
                            new CaveChunk(chunkSize * caveScale, pos, isoLevel, NoiseScale, 
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
