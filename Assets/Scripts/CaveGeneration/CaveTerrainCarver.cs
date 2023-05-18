using System;
using System.Collections;
using System.Collections.Generic;
using Managers;
using UnityEngine;
using EventType = Managers.EventType;

[RequireComponent(typeof(CaveManager)), RequireComponent(typeof(CavePhysicsManager))]
public class CaveTerrainCarver : MonoBehaviour
{
    private ComputeShader caveCarveShader;
    private Vector3 threadGroupSize;
    private CaveManager caveManager;
    private CavePhysicsManager physicsManager;
    private CaveChunk[,,] chunks => caveManager.chunks;
    private Vector3[] caveBounds => caveManager.caveBounds;
    private int amountChunksHorizontal => caveManager.amountChunksHorizontal;
    private int amountChunksVertical => caveManager.amountChunksVertical;
    private LayerMask caveMask => caveManager.caveMask;
    private int chunkSize => caveManager.chunkSize;

    private bool vectorFieldEnabled;

    private void OnEnable()
    {
        caveManager = GetComponent<CaveManager>();
        physicsManager = GetComponent<CavePhysicsManager>();
            
        caveCarveShader = Resources.Load<ComputeShader>("SDFCarver");
        caveCarveShader.GetKernelThreadGroupSizes(0, out uint threadGroupSizeX, out uint threadGroupSizeY, out uint threadGroupSizeZ);
        
        threadGroupSize.x = Mathf.CeilToInt((float)chunkSize / threadGroupSizeX);
        threadGroupSize.y = Mathf.CeilToInt((float)chunkSize / threadGroupSizeY);
        threadGroupSize.z = Mathf.CeilToInt((float)chunkSize / threadGroupSizeZ);

        vectorFieldEnabled = GetComponent<CaveVectorField>() != null;
        
        EventSystem<MyRay, float, float>.Subscribe(Managers.EventType.CARVE_TERRAIN, CarveTerrain);
    }

    private void OnDisable()
    {
        EventSystem<MyRay, float, float>.Unsubscribe(Managers.EventType.CARVE_TERRAIN, CarveTerrain);
    }

    private void CarveTerrain(MyRay _ray, float _carveSize, float _carveSpeed)
    {
        if (physicsManager.Raycast(_ray.origin, _ray.direction, out var rayOutput))
        {
            // raycastCursor.position = rayOutput.position;
            // raycastCursor.rotation = Quaternion.LookRotation(rayOutput.normal);
            RemoveTerrain(rayOutput.position, _carveSize, _carveSpeed);
            if (vectorFieldEnabled)
            {
                EventSystem<bool>.RaiseEvent(EventType.UPDATE_VECTORFIELD, true);
            }
        }
    }

    //These functions do not yet work with other isolevels
    public void RemoveTerrain(Vector3 _pos, float _carveSize, float _carveSpeed)
    {
        Collider[] chunksHit = Physics.OverlapSphere(_pos, _carveSize, caveMask);
        foreach (var chunkCollider in chunksHit)
        {
            Vector3 chunkIndex = GetChunkIndex(chunkCollider.transform.position);
            CaveChunk chunk = chunks[(int)chunkIndex.x, (int)chunkIndex.y, (int)chunkIndex.z];
            Vector3 carvePos = _pos - chunk.position;
            
            caveCarveShader.SetTexture(0, "noiseTex", chunk.noiseTex);
            caveCarveShader.SetVector("carvePos", carvePos);
            caveCarveShader.SetFloat("carveSize", _carveSize);
            caveCarveShader.SetFloat("carveSpeed", _carveSpeed);
            
            caveCarveShader.Dispatch(0, (int)threadGroupSize.x, (int)threadGroupSize.y, (int)threadGroupSize.z);
            chunk.GenerateMesh();
        }
    }
    
    public void FillTerrain(Vector3 _pos, float _carveSize)
    {
        Collider[] chunksHit = Physics.OverlapSphere(_pos, _carveSize);
        foreach (var chunkCollider in chunksHit)
        {
            Vector3 chunkIndex = GetChunkIndex(chunkCollider.transform.position);
            CaveChunk chunk = chunks[(int)chunkIndex.x, (int)chunkIndex.y, (int)chunkIndex.z];
            Vector3 carvePos = _pos - chunk.position;
            
            caveCarveShader.SetTexture(1, "noiseTex", chunk.noiseTex);
            caveCarveShader.SetVector("carvePos", carvePos);
            caveCarveShader.SetFloat("carveSize", _carveSize);
            
            caveCarveShader.Dispatch(1, (int)threadGroupSize.x, (int)threadGroupSize.y, (int)threadGroupSize.z);
            chunk.GenerateMesh();
        }
    }
    
    private Vector3 GetChunkIndex(Vector3 _playerPos)
    {
        return _playerPos.Remap(caveBounds[0], caveBounds[1], Vector3.zero, 
                                new Vector3(amountChunksHorizontal, amountChunksVertical, amountChunksHorizontal));
    }
}
