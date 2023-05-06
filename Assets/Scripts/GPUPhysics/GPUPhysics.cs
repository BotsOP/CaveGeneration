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
        intersectBuffer = new ComputeBuffer(100, sizeof(float) * 4, ComputeBufferType.Append);
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
    
    public static Vector3 RayIntersectMesh(GraphicsBuffer _vertexBuffer, GraphicsBuffer _indexBuffer, Vector3 _meshPos, Vector3 _rayOrigin, Vector3 _rayDirection)
    {
        countBuffer.SetData(new int[1]);
        intersectBuffer.SetCounterValue(0);

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
            return new Vector3(0, -1000, 0);
        }
        
        Vector4[] intersections = new Vector4[counter];
        intersectBuffer.GetData(intersections);
        
        float lowestT = float.MaxValue;
        Vector3 point = new Vector3(0, -1000, 0);
        
        for (var i = 0; i < counter; i++)
        {
            var intersect = intersections[i];
            
            Vector3 pos = new Vector3(intersect.x, intersect.y, intersect.z);
            
            if (Vector3.Dot(_rayDirection.normalized, pos - _rayOrigin) < 0)
            {
                continue;
            }
            
            if (Vector3.Distance(_rayOrigin, pos) < lowestT)
            {
                lowestT = Vector3.Distance(_rayOrigin, pos);
                point = pos;
            }
        }

        return point;
    }

    public static Vector4[] RayIntersectMesh(GraphicsBuffer _vertexBuffer, GraphicsBuffer _indexBuffer, Vector3 _meshPos, List<Ray> _rays)
    {
        countBuffer.SetData(new int[1]);
        intersectBuffer.SetCounterValue(0);

        gpuPhysicsShader.GetKernelThreadGroupSizes(2, out uint threadGroupSizeX, out uint _, out uint _);

        int amountTriangles = _indexBuffer.count / 3;
        Vector3 threadGroupSize = Vector3.one;
        threadGroupSize.x = Mathf.CeilToInt(amountTriangles / (float)threadGroupSizeX);

        int amountRays = _rays.Count;
        ComputeBuffer rays = new ComputeBuffer(amountRays, sizeof(float) * 6 + sizeof(int), ComputeBufferType.Structured);
        rays.SetData(_rays);

        gpuPhysicsShader.SetBuffer(2, "vertexBuffer", _vertexBuffer);
        gpuPhysicsShader.SetBuffer(2, "indexBuffer", _indexBuffer);
        gpuPhysicsShader.SetBuffer(2, "countBuffer", countBuffer);
        gpuPhysicsShader.SetBuffer(2, "intersectBuffer", intersectBuffer);
        gpuPhysicsShader.SetBuffer(2, "rays", rays);
        gpuPhysicsShader.SetVector("meshOffset", _meshPos);
        gpuPhysicsShader.SetInt("amountRays", _rays.Count - 1);

        gpuPhysicsShader.Dispatch(2, (int)threadGroupSize.x, (int)threadGroupSize.y, (int)threadGroupSize.z);
        
        rays.Dispose();

        int[] counters = new int[1];
        countBuffer.GetData(counters);
        int counter = counters[0];

        if (counter <= 0)
        {
            return Array.Empty<Vector4>();
        }

        Vector4[] intersections = new Vector4[counter];
        intersectBuffer.GetData(intersections);

        float[] lowestDist = new float[amountRays];
        Array.Fill(lowestDist, float.MaxValue);

        Vector4[] points = new Vector4[amountRays];
        for (int i = 0; i < amountRays; i++)
        {
            points[i] = new Vector4(0, -1000, 0, _rays[i].index);
        }

        for (int i = 0; i < counter; i++)
        {
            int rayIndex = (int)intersections[i].w;
            Vector3 pos = new Vector3(intersections[i].x, intersections[i].y, intersections[i].z);
            
            if (Vector3.Dot(_rays[rayIndex].direction.normalized, pos - _rays[rayIndex].origin) < 0)
            {
                continue;
            }
            
            float dist = Vector3.Distance(_rays[rayIndex].origin, pos);
            if (Vector3.Distance(_rays[rayIndex].origin, pos) < lowestDist[rayIndex])
            {
                lowestDist[rayIndex] = dist;
                points[rayIndex] = new Vector4(pos.x, pos.y, pos.z, _rays[rayIndex].index);
            }
        }

        return points;
    }
}
