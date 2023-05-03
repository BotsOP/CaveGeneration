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
    
    public static Vector3 AreColliding(GraphicsBuffer _vertexBuffer, GraphicsBuffer _indexBuffer, Vector3 _rayOrigin, Vector3 _rayDirection)
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
        
        gpuPhysicsShader.Dispatch(1, (int)threadGroupSize.x, (int)threadGroupSize.y, (int)threadGroupSize.z);
        
        int[] counters = new int[1];
        resultBuffer.GetData(counters);
        int counter = counters[0];

        if (counter == 0)
        {
            return new Vector3(0, -1000, 0);
        }

        Vector4[] intersections = new Vector4[100];
        intersectBuffer.GetData(intersections);
        float lowestT = float.MaxValue;
        Vector3 point = new Vector3(0, -1000, 0);
        foreach (var intersect in intersections)
        {
            if (intersect.w < lowestT)
            {
                lowestT = intersect.w;
                point = intersect;
            }
        }
        return point;
    }
}
