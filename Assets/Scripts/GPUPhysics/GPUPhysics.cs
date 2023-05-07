using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class GPUPhysics
{
    private static ComputeShader gpuPhysicsShader;
    private static ComputeBuffer resultBuffer;
    private static ComputeBuffer countBuffer;
    private static ComputeBuffer intersectBuffer;
    static GPUPhysics()
    {
        gpuPhysicsShader = Resources.Load<ComputeShader>("GPUPhysicsShader");
        resultBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Structured);
        countBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Structured);
        intersectBuffer = new ComputeBuffer(20, sizeof(float) * 6, ComputeBufferType.Append);
    }

    public static bool AreColliding(GraphicsBuffer _vertexBuffer, GraphicsBuffer _indexBuffer, Vector3 _sdfPos, Vector4 _circle)
    {
        resultBuffer.SetData(new int[1]);
        
        gpuPhysicsShader.GetKernelThreadGroupSizes(0, out uint threadGroupSizeX, out uint _, out uint _);

        int amountTriangles = _indexBuffer.count / 3;
        Vector3 threadGroupSize = Vector3.one;
        threadGroupSize.x = Mathf.CeilToInt(amountTriangles / (float)threadGroupSizeX);
        
        gpuPhysicsShader.SetBuffer(0, "vertexBuffer", _vertexBuffer);
        gpuPhysicsShader.SetBuffer(0, "indexBuffer", _indexBuffer);
        gpuPhysicsShader.SetVector("circle", _circle);
        gpuPhysicsShader.SetBuffer(0, "output", resultBuffer);
        
        gpuPhysicsShader.Dispatch(0, (int)threadGroupSize.x, (int)threadGroupSize.y, (int)threadGroupSize.z);
        
        int[] results = new int[1];
        resultBuffer.GetData(results);
        int result = results[0];
        return result > 0;
    }
    
    public static bool RayIntersectMesh(GraphicsBuffer _vertexBuffer, GraphicsBuffer _indexBuffer, Vector3 _meshPos, Vector3 _rayOrigin, Vector3 _rayDirection, out RayOutput _rayOutput)
    {
        countBuffer.SetData(new int[1]);
        intersectBuffer.SetCounterValue(0);
        _rayOutput = new RayOutput();

        gpuPhysicsShader.GetKernelThreadGroupSizes(1, out uint threadGroupSizeX, out uint _, out uint _);

        int amountTriangles = _indexBuffer.count / 3;
        Vector3 threadGroupSize = Vector3.one;
        threadGroupSize.x = Mathf.CeilToInt(amountTriangles / (float)threadGroupSizeX);

        gpuPhysicsShader.SetBuffer(1, "vertexBuffer", _vertexBuffer);
        gpuPhysicsShader.SetBuffer(1, "indexBuffer", _indexBuffer);
        gpuPhysicsShader.SetBuffer(1, "countBuffer", countBuffer);
        gpuPhysicsShader.SetBuffer(1, "intersectBuffer", intersectBuffer);
        gpuPhysicsShader.SetVector("rayOrigin", _rayOrigin);
        gpuPhysicsShader.SetVector("rayDirection", _rayDirection);
        gpuPhysicsShader.SetVector("meshOffset", _meshPos);
        
        gpuPhysicsShader.Dispatch(1, (int)threadGroupSize.x, (int)threadGroupSize.y, (int)threadGroupSize.z);
        
        int[] counters = new int[1];
        countBuffer.GetData(counters);
        int counter = counters[0];

        if (counter <= 0)
        {
            return false;
        }
        
        RayOutput[] intersections = new RayOutput[counter];
        intersectBuffer.GetData(intersections);
        
        float lowestT = float.MaxValue;
        Vector3 point = new Vector3(0, -1000, 0);
        Vector3 normal = new Vector3(0, -1000, 0);
        
        for (var i = 0; i < counter; i++)
        {
            var intersect = intersections[i];
            
            if (Vector3.Dot(_rayDirection.normalized, intersect.position - _rayOrigin) < 0)
            {
                continue;
            }
            
            if (Vector3.Distance(_rayOrigin, intersect.position) < lowestT)
            {
                lowestT = Vector3.Distance(_rayOrigin, intersect.position);
                point = intersect.position;
                normal = intersect.normal;
            }
        }

        if (point.y < 0)
        {
            return false;
        }

        _rayOutput.position = point;
        _rayOutput.normal = normal;

        return true;
    }
}

public struct RayOutput
{
    public Vector3 position;
    public Vector3 normal;
}
