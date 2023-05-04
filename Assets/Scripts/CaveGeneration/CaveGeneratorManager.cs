using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CaveGeneratorManager : MonoBehaviour
{
    [SerializeField] private Transform playerTransform;
    [SerializeField] private GameObject meshContainer;
    [SerializeField] private int chunkSize;
    [SerializeField, Range(0.01f, 1)] private float isoLevel;
    [SerializeField, Range(0.01f, 1)] private float noiseScale;
    [SerializeField, Range(1, 32)] private int amountChunksHorizontal;
    [SerializeField, Range(1, 32)] private int amountChunksVertical;
    [SerializeField, Range(0.1f, 1)] private float caveScale;
    public Transform sphere;
    private CaveGenerator[,,] chunks;
    private CavePhysicsManager physicsManager;
    private Vector3[] caveBounds;
    private float caveWidth;
    private int stepSize;

    private float NoiseScale => noiseScale / caveScale;

    private void Start()
    {
        // chunks = InitiliazeArray();
        chunks = new CaveGenerator[amountChunksHorizontal, amountChunksVertical, amountChunksHorizontal];
        physicsManager = new CavePhysicsManager(chunks);

        stepSize = (int)(chunkSize * caveScale) - 1;

        for (int i = 0; i < chunks.GetLength(0); i++)
        {
            for (int j = 0; j < chunks.GetLength(1); j++)
            {
                for (int k = 0; k < chunks.GetLength(2); k++)
                {
                    Vector3 index = new Vector3(i, j, k);
                    AddChunk(index, index * stepSize);
                }
            }
        }

        caveWidth = amountChunksHorizontal * stepSize;

        caveBounds = new Vector3[2];
        caveBounds[0] = new Vector3(0, 0, 0);
        caveBounds[1] = new Vector3(caveWidth, amountChunksVertical * stepSize, caveWidth);
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
                        
                        chunks[i, j, k].Initialize(NoiseScale);
                        chunks[i, j, k].GenerateMesh(isoLevel);
                    }
                }
            }
        }
        Vector3 playerChunkIndex = GetChunkIndex(playerTransform.position);
        PlaceChunksAroundPlayer(playerChunkIndex);
        
        if (Input.GetKeyDown(KeyCode.C))
        {
            AddChunksLeft();
        }
        if (Input.GetKeyDown(KeyCode.V))
        {
            AddChunksRight();
        }
        if (Input.GetKeyDown(KeyCode.X))
        {
            AddChunksForward();
        }
        if (Input.GetKeyDown(KeyCode.Z))
        {
            AddChunksBackward();
        }
        // Vector4 sphereGPU = new Vector4(sphere.position.x, sphere.position.y, sphere.position.z, sphere.lossyScale.x / 2);
        // Debug.Log(GPUPhysics.AreColliding(chunks[0][0][0].vertexBuffer, chunks[0][0][0].indexBuffer, chunks[0][0][0].chunkPosition, sphereGPU));
        Vector3 point = GPUPhysics.AreColliding(chunks[0, 0, 0].vertexBuffer, chunks[0, 0, 0].indexBuffer, playerTransform.position, playerTransform.forward * 1000);
        Debug.DrawRay(playerTransform.position, playerTransform.forward * 1000);
        sphere.position = point;
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
                        AddChunk(index, pos);
                    }
                    else
                    {
                        if (i == chunks.GetLength(0) - 1)
                        {
                            Destroy(chunks[i, j, k].gameObject);
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
                        AddChunk(index, pos);
                    }
                    else
                    {
                        if (i == 0)
                        {
                            Destroy(chunks[i, j, k].gameObject);
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
                        AddChunk(index, pos);
                    }
                    else
                    {
                        if (k == 0)
                        {
                            Destroy(chunks[i, j, k].gameObject);
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
                        AddChunk(index, pos);
                    }
                    else
                    {
                        if (k == chunks.GetLength(0) - 1)
                        {
                            Destroy(chunks[i, j, k].gameObject);
                            chunks[i, j, k].OnDestroy();
                        }
                        chunks[i, j, k] = chunks[i, j, k - 1];
                    }
                }
            }
        }
    }

    #endregion

    private void AddChunk(Vector3 _index, Vector3 pos)
    {
        GameObject meshObject = Instantiate(meshContainer, pos, Quaternion.identity, transform);
        MeshFilter meshFilter = meshObject.GetComponent<MeshFilter>();
        chunks[(int)_index.x, (int)_index.y, (int)_index.z] = new CaveGenerator(chunkSize * caveScale, pos, isoLevel, NoiseScale, meshFilter);
    }
}
