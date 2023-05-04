using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CavePhysicsManager
{
    private CaveGenerator[,,] chunks;
    
    public CavePhysicsManager(CaveGenerator[,,] _chunks)
    {
        chunks = _chunks;
    }

    public Vector3 Raycast(Vector3 _rayOrigin, Vector3 _rayDirection)
    {
        return Vector3.zero;
    }
}
