using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class GPUPhysics
{
    private static ComputeShader gpuPhysicsShader;
    private static ComputeBuffer countBuffer;
    private static ComputeBuffer outputBuffer;
    static GPUPhysics()
    {
        gpuPhysicsShader = Resources.Load<ComputeShader>("GPUPhysicsShader");
        countBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Structured);
        outputBuffer = new ComputeBuffer(50, sizeof(float) * 6, ComputeBufferType.Append);
    }

    public static bool SphereIntersectMesh(GraphicsBuffer _vertexBuffer, GraphicsBuffer _indexBuffer, Vector3 _meshPos, 
        Vector3 _spherePos, float _sphereRadius, out RayOutput _closestPoint)
    {
        countBuffer.SetData(new int[1]);
        outputBuffer.SetCounterValue(0);
        _closestPoint = new RayOutput();

        int kernelID = gpuPhysicsShader.FindKernel("MeshOverlapSphere");
        gpuPhysicsShader.GetKernelThreadGroupSizes(kernelID, out uint threadGroupSizeX, out uint _, out uint _);

        int amountTriangles = _indexBuffer.count / 3;
        Vector3 threadGroupSize = Vector3.one;
        threadGroupSize.x = Mathf.CeilToInt(amountTriangles / (float)threadGroupSizeX);

        _spherePos -= _meshPos;
        gpuPhysicsShader.SetBuffer(kernelID, "vertexBuffer", _vertexBuffer);
        gpuPhysicsShader.SetBuffer(kernelID, "indexBuffer", _indexBuffer);
        gpuPhysicsShader.SetVector("spherePos", _spherePos);
        gpuPhysicsShader.SetFloat("sphereRadius", _sphereRadius);
        gpuPhysicsShader.SetBuffer(kernelID, "countBuffer", countBuffer);
        gpuPhysicsShader.SetBuffer(kernelID, "rayOutputBuffer", outputBuffer);
        
        gpuPhysicsShader.Dispatch(kernelID, (int)threadGroupSize.x, (int)threadGroupSize.y, (int)threadGroupSize.z);
        _spherePos += _meshPos;
        
        int[] counters = new int[1];
        countBuffer.GetData(counters);
        int counter = counters[0];

        if (counter == 0)
        {
            return false;
        }
        if (counter > 50)
        {
            counter = 50;
        }
        
        RayOutput[] points = new RayOutput[counter];
        outputBuffer.GetData(points);
        
        //Change this to properly reflect the resolving force
        float lowestDist = float.MaxValue;
        RayOutput closestPoint = new RayOutput();

        for (var i = 0; i < points.Length; i++)
        {
            var point = points[i].position + _meshPos;
            float dist = Vector3.Distance(point, _spherePos);
            if (dist < lowestDist)
            {
                lowestDist = dist;
                closestPoint.position = point;
                closestPoint.normal = points[i].normal;
            }
        }

        _closestPoint = closestPoint;
        
        return true;
    }
    
    public static bool RayIntersectMesh(GraphicsBuffer _vertexBuffer, GraphicsBuffer _indexBuffer, Vector3 _meshPos, Vector3 _rayOrigin, Vector3 _rayDirection, out RayOutput _rayOutput)
    {
        countBuffer.SetData(new int[1]);
        outputBuffer.SetCounterValue(0);
        _rayOutput = new RayOutput();
        float rayLength = _rayDirection.magnitude;

        int kernelID = gpuPhysicsShader.FindKernel("MeshIntersectRay");
        gpuPhysicsShader.GetKernelThreadGroupSizes(kernelID, out uint threadGroupSizeX, out uint _, out uint _);

        int amountTriangles = _indexBuffer.count / 3;
        Vector3 threadGroupSize = Vector3.one;
        threadGroupSize.x = Mathf.CeilToInt(amountTriangles / (float)threadGroupSizeX);

        _rayOrigin -= _meshPos;
        gpuPhysicsShader.SetBuffer(kernelID, "vertexBuffer", _vertexBuffer);
        gpuPhysicsShader.SetBuffer(kernelID, "indexBuffer", _indexBuffer);
        gpuPhysicsShader.SetBuffer(kernelID, "countBuffer", countBuffer);
        gpuPhysicsShader.SetBuffer(kernelID, "rayOutputBuffer", outputBuffer);
        gpuPhysicsShader.SetVector("rayOrigin", _rayOrigin);
        gpuPhysicsShader.SetVector("rayDirection", _rayDirection);
        
        gpuPhysicsShader.Dispatch(kernelID, (int)threadGroupSize.x, (int)threadGroupSize.y, (int)threadGroupSize.z);
        _rayOrigin += _meshPos;
        
        int[] counters = new int[1];
        countBuffer.GetData(counters);
        int counter = counters[0];

        if (counter <= 0)
        {
            return false;
        }
        if (counter > 50)
        {
            counter = 50;
        }

        RayOutput[] intersections = new RayOutput[counter];
        outputBuffer.GetData(intersections);
        
        float lowestT = float.MaxValue;
        Vector3 point = new Vector3(0, -1000, 0);
        Vector3 normal = new Vector3(0, -1000, 0);
        
        for (var i = 0; i < counter; i++)
        {
            var intersect = intersections[i];
            intersect.position += _meshPos;

            float dot = Vector3.Dot(_rayDirection.normalized, (intersect.position - _rayOrigin).normalized);
            if (dot < 0)
            {
                continue;
            }

            float dist = Vector3.Distance(_rayOrigin, intersect.position);
            if (dist > rayLength)
            {
                continue;
            }
            
            if (dist < lowestT)
            {
                lowestT = Vector3.Distance(_rayOrigin, intersect.position);
                point = intersect.position;
                normal = intersect.normal;
            }
        }

        if (point.y < -999)
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
