using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CavePhysicsManager
{
    private CaveChunk[,,] chunks;
    private Vector3[] caveBounds;
    private int amountChunksHorizontal;
    private int amountChunksVertical;

    public CavePhysicsManager(CaveChunk[,,] _chunks, Vector3[] _caveBounds, int _amountChunksHorizontal, int _amountChunksVertical)
    {
        chunks = _chunks;
        caveBounds = _caveBounds;
        amountChunksHorizontal = _amountChunksHorizontal;
        amountChunksVertical = _amountChunksVertical;
    }

    public Vector3 Raycast(Vector3 _rayOrigin, Vector3 _rayDirection)
    {
        int index = 0;
        int amountChunksToCheck = 10;
        
        while (true)
        {
            Vector3 chunkIndex = GetChunkIndex(_rayOrigin);
            
            if (chunkIndex.x < 0 || chunkIndex.y < 0 || chunkIndex.z < 0 || 
                chunkIndex.x > amountChunksHorizontal - 1 || chunkIndex.y > amountChunksVertical - 1 || chunkIndex.z > amountChunksHorizontal - 1)
            {
                break;
            }
            
            CaveChunk chunk = chunks[(int)chunkIndex.x, (int)chunkIndex.y, (int)chunkIndex.z];
            Vector3 point = GPUPhysics.RayIntersectMesh(chunk.vertexBuffer, chunk.indexBuffer, chunk.chunkPosition, _rayOrigin, _rayDirection);
            if (point.y >= 0)
            {
                return point;
            }

            RaycastHit hit;
            if (Physics.Raycast(_rayOrigin, _rayDirection, out hit, Mathf.Infinity))
            {
                _rayOrigin = hit.point + _rayDirection.normalized / 10;
            }
            else
            {
                break;
            }
            
            index++;
            if (index > amountChunksToCheck)
            {
                break;
            }
        }
        
        return new Vector3(0, -1000, 0);
    }
    
    public Vector3[] Raycast(List<Ray> _rays)
    {
        int index = 0;
        int amountChunksToCheck = 10;

        List<Ray> ongoingRays = new List<Ray>(_rays);
        Vector3[] finishedRays = new Vector3[_rays.Count];

        while (true)
        {
            Dictionary<Vector3, List<Ray>> rayCollection = new Dictionary<Vector3, List<Ray>>();
            foreach (var ray in ongoingRays)
            {
                Vector3 chunkIndex = GetChunkIndex(ray.origin);

                chunkIndex = new Vector3((int)chunkIndex.x, (int)chunkIndex.y, (int)chunkIndex.z);
                
                if (chunkIndex.x < 0 || chunkIndex.y < 0 || chunkIndex.z < 0 || 
                    chunkIndex.x > amountChunksHorizontal - 1 || chunkIndex.y > amountChunksVertical - 1 || chunkIndex.z > amountChunksHorizontal - 1)
                {
                    continue;
                }
                
                if (!rayCollection.ContainsKey(chunkIndex))
                {
                    rayCollection[chunkIndex] = new List<Ray> { ray };
                    continue;
                }
                
                rayCollection[chunkIndex].Add(ray);
            }
            
            ongoingRays.Clear();

            foreach (var VARIABLE in rayCollection)
            {
                Vector3 chunkIndex = VARIABLE.Key;
                CaveChunk chunk = chunks[(int)chunkIndex.x, (int)chunkIndex.y, (int)chunkIndex.z];
                Vector4[] points = GPUPhysics.RayIntersectMesh(chunk.vertexBuffer, chunk.indexBuffer, chunk.chunkPosition, VARIABLE.Value);

                foreach (var point in points)
                {
                    if (point.y <= 0)
                    {
                        int rayIndex = (int)point.w;
                        Ray ray = _rays[rayIndex];
                        Vector3 origin = ray.origin;
                        Vector3 direction = ray.direction;
                        
                        RaycastHit hit;
                        if (Physics.Raycast(origin, direction, out hit, Mathf.Infinity))
                        {
                            ray.origin = hit.point + direction.normalized / 10;
                        }
                        
                        _rays[rayIndex] = ray;
                        ongoingRays.Add(_rays[rayIndex]);
                        continue;
                    }
                    
                    finishedRays[(int)point.w] = new Vector3(point.x, point.y, point.z);
                }
            }

            index++;
            if (index > amountChunksToCheck)
            {
                break;
            }
        }

        return finishedRays;
    }

    private Vector3 GetChunkIndex(Vector3 _playerPos)
    {
        return _playerPos.Remap(caveBounds[0], caveBounds[1], Vector3.zero, 
                                new Vector3(amountChunksHorizontal, amountChunksVertical, amountChunksHorizontal));
    }
}
