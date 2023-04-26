using UnityEngine;

public static class GPUPhysics
{
    private static ComputeShader gpuPhysicsShader;
    static GPUPhysics()
    {
        gpuPhysicsShader = Resources.Load<ComputeShader>("GPUPhysicsShader");
    }

    public static bool AreColliding(RenderTexture _sdf, Vector3 _sdfPos, float _sdfScale, Vector4 _circle, float _isoLevel)
    {
        Vector3 spherePos = new Vector3(_circle.x, _circle.y, _circle.z);
        Vector3 localSpherePos = spherePos - _sdfPos;
        Vector3 localStartPos = localSpherePos - new Vector3(_circle.w / 2, _circle.w / 2, _circle.w / 2);
        
        gpuPhysicsShader.GetKernelThreadGroupSizes(0, out uint threadGroupSizeX, out uint threadGroupSizeY, out uint threadGroupSizeZ);
        
        float threadGroupSize = Mathf.CeilToInt(_circle.w / threadGroupSizeX);
        
        gpuPhysicsShader.SetTexture(0, "sdf", _sdf);
        gpuPhysicsShader.SetVector("circle", _circle);
        
        
        return false;
    }
}
