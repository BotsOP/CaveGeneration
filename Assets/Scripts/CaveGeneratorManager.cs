using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CaveGeneratorManager : MonoBehaviour
{
    [SerializeField] private GameObject meshContainer;
    [SerializeField] private int chunkSize;
    [SerializeField, Range(0.01f, 1)] private float isoLevel;
    [SerializeField, Range(0.01f, 1)] private float noiseScale;
    [SerializeField, Range(1, 32)] private int amountChunksAroundPlayer;
    [SerializeField, Range(0.1f, 1)] private float caveScale;
    public Transform sphere;
    public List<RenderTexture> renderTextures;
    private CaveGenerator[][][] chunks;
    private int stepSize;

    private float NoiseScale => noiseScale / caveScale;

    private void Start()
    {
        InitiliazeArray();

        stepSize = (int)(chunkSize * caveScale);

        for (int i = 0; i < chunks.Length; i++)
        {
            for (int j = 0; j < chunks[i].Length; j++)
            {
                for (int k = 0; k < chunks[i][j].Length; k++)
                {
                    AddChunk(new Vector3(i, j, k));
                }
            }
        }
    }
    
    private void InitiliazeArray()
    {

        chunks = new CaveGenerator[amountChunksAroundPlayer][][];
        for (int i = 0; i < chunks.Length; i++)
        {
            chunks[i] = new CaveGenerator[amountChunksAroundPlayer][];
            for (int j = 0; j < chunks[i].Length; j++)
            {
                chunks[i][j] = new CaveGenerator[amountChunksAroundPlayer];
            }
        }
    }

    private void OnDisable()
    {
        for (int i = 0; i < chunks.Length; i++)
        {
            for (int j = 0; j < chunks[i].Length; j++)
            {
                for (int k = 0; k < chunks[i][j].Length; k++)
                {
                    if (!ReferenceEquals(chunks[i][j][k], null))
                    {
                        chunks[i][j][k].OnDestroy();
                    }
                }
            }
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            for (int i = 0; i < chunks.Length; i++)
            {
                for (int j = 0; j < chunks[i].Length; j++)
                {
                    for (int k = 0; k < chunks[i][j].Length; k++)
                    {
                        if (ReferenceEquals(chunks[i][j][k], null))
                            continue;
                        
                        chunks[i][j][k].Initialize(NoiseScale);
                        chunks[i][j][k].GenerateMesh(isoLevel);
                    }
                }
            }
        }
        // Vector4 sphereGPU = new Vector4(sphere.position.x, sphere.position.y, sphere.position.z, sphere.lossyScale.x / 2);
        // Debug.Log(GPUPhysics.AreColliding(chunks[0][0][0].vertexBuffer, chunks[0][0][0].indexBuffer, chunks[0][0][0].chunkPosition, sphereGPU));
        //Debug.Log(Camera.main.transform.right);
        Vector3 point = GPUPhysics.AreColliding(chunks[0][0][0].vertexBuffer, chunks[0][0][0].indexBuffer, 
            Camera.main.transform.position, Camera.main.transform.forward * 1000);
        Debug.Log(point);
        Debug.DrawRay(Camera.main.transform.position, Camera.main.transform.forward * 1000);
        //Debug.Log(Camera.main.transform.forward);
        sphere.position = point;
    }

    private void AddChunk(Vector3 index)
    {
        var position = (index * stepSize - index);
        //position = new Vector3(index.x * stepSize - index.x, (int)position.y, (int)position.z);
        GameObject meshObject = Instantiate(meshContainer, position, Quaternion.identity, transform);
        MeshFilter meshFilter = meshObject.GetComponent<MeshFilter>();
        chunks[(int)index.x][(int)index.y][(int)index.z] = new CaveGenerator(chunkSize * caveScale, position, isoLevel, NoiseScale, meshFilter);
        renderTextures.Add(chunks[(int)index.x][(int)index.y][(int)index.z].noiseTex);
    }
}
