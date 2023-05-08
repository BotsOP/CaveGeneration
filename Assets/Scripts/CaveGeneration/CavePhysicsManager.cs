using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CavePhysicsManager
{
    public static CavePhysicsManager instance;
    private CaveChunk[,,] chunks;
    private Vector3[] caveBounds;
    private int amountChunksHorizontal;
    private int amountChunksVertical;
    private LayerMask caveMask;

    public CavePhysicsManager(CaveChunk[,,] _chunks, Vector3[] _caveBounds, int _amountChunksHorizontal, int _amountChunksVertical, LayerMask _caveMask)
    {
        chunks = _chunks;
        caveBounds = _caveBounds;
        amountChunksHorizontal = _amountChunksHorizontal;
        amountChunksVertical = _amountChunksVertical;
        caveMask = _caveMask;
        instance = this;
    }

    public bool Sphere(Vector3 _spherePos, float _sphereRadius, out Vector3 _resolvingForce)
    {
        _resolvingForce = new Vector3();
        
        Collider[] chunksHit = Physics.OverlapSphere(_spherePos, _sphereRadius, caveMask);
        int amountChunksHit = chunksHit.Length;

        if (amountChunksHit == 0)
        {
            return false;
        }
        
        foreach (var chunkCollider in chunksHit)
        {
            Vector3 chunkIndex = GetChunkIndex(chunkCollider.transform.position);
            CaveChunk chunk = chunks[(int)chunkIndex.x, (int)chunkIndex.y, (int)chunkIndex.z];

            if (GPUPhysics.SphereIntersectMesh(chunk.vertexBuffer, chunk.indexBuffer, chunk.position, _spherePos,
                    _sphereRadius, out Vector3 resolvingForce))
            {
                Debug.Log(resolvingForce);
                _resolvingForce += resolvingForce;
            }
        }

        if (_resolvingForce == Vector3.zero)
        {
            return false;
        }

        _resolvingForce /= amountChunksHit;
        
        return true;
    }

    public bool Raycast(Vector3 _rayOrigin, Vector3 _rayDirection, out RayOutput _rayOutput)
    {
        _rayOutput = new RayOutput();
        int index = 0;
        int amountChunksToCheck = 10;
        
        Vector3 localRayOrigin = _rayOrigin;
        while (true)
        {
            Vector3 chunkIndex = GetChunkIndex(localRayOrigin);
            chunkIndex = new Vector3((int)chunkIndex.x, (int)chunkIndex.y, (int)chunkIndex.z);
            
            if (chunkIndex.x < 0 || chunkIndex.y < 0 || chunkIndex.z < 0 || 
                chunkIndex.x > amountChunksHorizontal - 1 || chunkIndex.y > amountChunksVertical - 1 || chunkIndex.z > amountChunksHorizontal - 1)
            {
                break;
            }
            
            CaveChunk chunk = chunks[(int)chunkIndex.x, (int)chunkIndex.y, (int)chunkIndex.z];
            if (GPUPhysics.RayIntersectMesh(chunk.vertexBuffer, chunk.indexBuffer, chunk.position, _rayOrigin, _rayDirection, out var rayOutput))
            {
                _rayOutput = rayOutput;
                return true;
            }
            
            RaycastHit hit;
            if (Physics.Raycast(localRayOrigin, _rayDirection, out hit, Mathf.Infinity))
            {
                localRayOrigin = hit.point + _rayDirection.normalized / 10;
            }
            else
            {
                return false;
            }
            
            index++;
            if (index > amountChunksToCheck)
            {
                return false;
            }
        }

        return false;
    }

    private Vector3 GetChunkIndex(Vector3 _playerPos)
    {
        return _playerPos.Remap(caveBounds[0], caveBounds[1], Vector3.zero, 
                                new Vector3(amountChunksHorizontal, amountChunksVertical, amountChunksHorizontal));
    }
}
