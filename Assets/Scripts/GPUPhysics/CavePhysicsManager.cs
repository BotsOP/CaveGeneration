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
        bool shouldRun = true;
        int index = 0;
        int amountChunksToCheck = 10;
        
        while (shouldRun)
        {
            Vector3 chunkIndex = GetChunkIndex(_rayOrigin);
            if (chunkIndex.x < 0 || chunkIndex.y < 0 || chunkIndex.z < 0 || chunkIndex.x > amountChunksHorizontal - 1 || chunkIndex.y > amountChunksVertical - 1 || chunkIndex.z > amountChunksHorizontal - 1)
            {
                shouldRun = false;
            }
            CaveChunk chunk = chunks[(int)chunkIndex.x, (int)chunkIndex.y, (int)chunkIndex.z];
            
            Vector3 point = GPUPhysics.RayIntersectMesh(chunk.vertexBuffer, chunk.indexBuffer, chunk.chunkPosition, _rayOrigin, _rayDirection);
            if (point.y >= 0)
            {
                return point;
            }

            RaycastHit hit;
            // Does the ray intersect any objects excluding the player layer
            if (Physics.Raycast(_rayOrigin, _rayDirection, out hit, Mathf.Infinity))
            {
                _rayOrigin = hit.point + _rayDirection.normalized / 10;
            }
            else
            {
                Debug.LogWarning($"Ray did not hit anything");
                shouldRun = false;
            }
            
            index++;
            if (index > amountChunksToCheck)
            {
                shouldRun = false;
            }
        }
        
        return new Vector3(0, -1000, 0);
    }

    // private Vector3 GetSquareExitPoint(Vector3 _rayOrigin, Vector3 _rayDirection, Vector3 _leftBottomBack, Vector3 _rightTopForward)
    // {
    //     Vector3 rightBottomBack = new Vector3(_rightTopForward.x, _leftBottomBack.y, _leftBottomBack.z);
    //     Vector3 leftTopBack = new Vector3(_leftBottomBack.x, _rightTopForward.y, _leftBottomBack.z);
    //     Vector3 rightTopBack = new Vector3(_rightTopForward.x, _rightTopForward.y, _leftBottomBack.z);
    //
    //     Vector3 leftBottomForward = new Vector3(_leftBottomBack.x, _leftBottomBack.y, _rightTopForward.z);
    //     Vector3 rightBottomForward = new Vector3(_rightTopForward.x, _leftBottomBack.y, _rightTopForward.z);
    //     Vector3 leftTopForward = new Vector3(_leftBottomBack.x, _rightTopForward.y, _rightTopForward.z);
    //     
    //     Vector3 result = GetPlaneIntersection(_rayOrigin, _rayDirection, _leftBottomBack, rightTopBack);
    //     if (result.y >= 0)
    //     {
    //         return result;
    //     }
    //     
    //     result = GetPlaneIntersection(_rayOrigin, _rayDirection, rightBottomBack, _rightTopForward);
    //     if (result.y >= 0)
    //     {
    //         return result;
    //     }
    //     
    //     result = GetPlaneIntersection(_rayOrigin, _rayDirection, leftBottomForward, _rightTopForward);
    //     if (result.y >= 0)
    //     {
    //         return result;
    //     }
    //     
    //     result = GetPlaneIntersection(_rayOrigin, _rayDirection, _leftBottomBack, leftTopForward);
    //     if (result.y >= 0)
    //     {
    //         return result;
    //     }
    //     
    //     result = GetPlaneIntersection(_rayOrigin, _rayDirection, leftTopBack, _rightTopForward);
    //     if (result.y >= 0)
    //     {
    //         return result;
    //     }
    //     
    //     result = GetPlaneIntersection(_rayOrigin, _rayDirection, _leftBottomBack, rightBottomForward);
    //     if (result.y >= 0)
    //     {
    //         return result;
    //     }
    //     
    //     Debug.LogWarning("Didnt intersect with any sides");
    //     return Vector3.zero;
    // }
    //
    // private Vector3 GetPlaneIntersection(Vector3 _rayOrigin, Vector3 _rayDirection, Vector3 _bottomLeft, Vector3 _topRight)
    // {
    //     Vector3 planeNormal = _topRight - _bottomLeft;
    //     float denominator = Vector3.Dot(planeNormal, _rayDirection);
    //
    //     if (Math.Abs(denominator) < 1e-6f)
    //     {
    //         // The line is parallel to the plane, no intersection or infinite intersections
    //         return new Vector3(0, -1000, 0);
    //     }
    //
    //     float t = (Vector3.Dot(_bottomLeft, planeNormal) - Vector3.Dot(_rayOrigin, planeNormal)) / denominator;
    //     Vector3 intersection = _rayOrigin + t * _rayDirection;
    //
    //     return intersection;
    // }
    
    private Vector3 GetChunkIndex(Vector3 _playerPos)
    {
        return _playerPos.Remap(caveBounds[0], caveBounds[1], Vector3.zero, 
                                new Vector3(amountChunksHorizontal, amountChunksVertical, amountChunksHorizontal));
    }
}
