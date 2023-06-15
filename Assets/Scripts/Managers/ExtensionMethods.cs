using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class ExtensionMethods
{
    public static float Remap (this float value, float from1, float to1, float from2, float to2) 
    {
        return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
    }
    public static Vector3 Remap (this Vector3 value, Vector3 from1, Vector3 to1, Vector3 from2, Vector3 to2) 
    {
        float x = (value.x - from1.x) / (to1.x - from1.x) * (to2.x - from2.x) + from2.x;
        float y = (value.y - from1.y) / (to1.y - from1.y) * (to2.y - from2.y) + from2.y;
        float z = (value.z - from1.z) / (to1.z - from1.z) * (to2.z - from2.z) + from2.z;
        return new Vector3(x, y, z);
    }
    public static float Remap (this int value, float from1, float to1, float from2, float to2) 
    {
        return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
    }

    public static int GetCounter(this ComputeBuffer _buffer)
    {
        int[] intArray = new int[1];
        _buffer.GetData(intArray);
        _buffer.SetData(new int[1]);
        return intArray[0];
    }
    
    public static int GetCounter(this GraphicsBuffer _buffer)
    {
        int[] intArray = new int[1];
        _buffer.GetData(intArray);
        _buffer.SetData(new int[1]);
        return intArray[0];
    }
    
    public static void Clear(this CustomRenderTexture _rt, bool _clearDepth, bool _clearColor, Color _color)
    {
        Graphics.SetRenderTarget(_rt);
        GL.Clear(_clearDepth, _clearColor, _color);
        Graphics.SetRenderTarget(null);
    }
    public static void Clear(this RenderTexture _rt, bool _clearDepth, bool _clearColor, Color _color)
    {
        Graphics.SetRenderTarget(_rt);
        GL.Clear(_clearDepth, _clearColor, _color);
        Graphics.SetRenderTarget(null);
    }

    public static int GetIndexBufferStride(this Mesh mesh)
    {
        return mesh.indexFormat == IndexFormat.UInt32 ? 4 : 2;
    }
    public static void LogAllVertexAttributes(this Mesh mesh)
    {
        int vertexAttribCount = GetVertexAttribCount(mesh);
        VertexAttributeDescriptor[] meshAttrib = new VertexAttributeDescriptor[vertexAttribCount];
        mesh.GetVertexAttributes(meshAttrib);
        for (int i = 0; i < vertexAttribCount; i++)
        {
            Debug.Log(meshAttrib[i]);
        }
    }
    public static Mesh CopyMesh(this Mesh mesh)
    {
        Mesh newMesh = new Mesh();
        newMesh.name = "copy of " + mesh.name;

        newMesh.indexFormat = mesh.indexFormat;
        newMesh.vertices = mesh.vertices;
        newMesh.normals = mesh.normals;
        newMesh.tangents = mesh.tangents;
        newMesh.bounds = mesh.bounds;
        newMesh.bindposes = mesh.bindposes;
        newMesh.indexBufferTarget = mesh.indexBufferTarget;
        newMesh.colors = mesh.colors;
        newMesh.colors32 = mesh.colors32;
        newMesh.boneWeights = mesh.boneWeights;
        newMesh.uv = mesh.uv;
        newMesh.uv2 = mesh.uv2;
        newMesh.uv3 = mesh.uv3;
        newMesh.uv4 = mesh.uv4;
        newMesh.uv5 = mesh.uv5;
        newMesh.uv6 = mesh.uv6;
        newMesh.uv7 = mesh.uv7;
        newMesh.uv8 = mesh.uv8;
        newMesh.subMeshCount = mesh.subMeshCount;

        for (int i = 0; i < newMesh.subMeshCount; i++)
        {
            newMesh.SetIndices(mesh.GetIndices(i), MeshTopology.Triangles, i, true, 0);
            newMesh.SetSubMesh(i,mesh.GetSubMesh(i));
        }
        return newMesh;
    }

    #region EditVertexAttribute
    public static void EditVertexAttribute(this Mesh mesh, VertexAttribute attrib, int dimension)
    {
        if (mesh.HasVertexAttribute(attrib))
        {
            int vertexAttribCount = GetVertexAttribCount(mesh);
            VertexAttributeDescriptor[] meshAttrib = new VertexAttributeDescriptor[vertexAttribCount];
            mesh.GetVertexAttributes(meshAttrib);

            for (int i = 0; i < vertexAttribCount; i++)
            {
                if (meshAttrib[i].attribute == attrib)
                {
                    meshAttrib[i].dimension = dimension;
                }
            }
        }
        else
        {
            Debug.LogError($"Mesh: {mesh.name} doesnt have vertex attribute {attrib} but you are trying to edit it");
        }
    }
    public static void EditVertexAttribute(this Mesh mesh, VertexAttribute attrib, int dimension, VertexAttributeFormat format)
    {
        if (mesh.HasVertexAttribute(attrib))
        {
            int vertexAttribCount = GetVertexAttribCount(mesh);
            VertexAttributeDescriptor[] meshAttrib = new VertexAttributeDescriptor[vertexAttribCount];
            mesh.GetVertexAttributes(meshAttrib);

            for (int i = 0; i < vertexAttribCount; i++)
            {
                if (meshAttrib[i].attribute == attrib)
                {
                    meshAttrib[i].dimension = dimension;
                    meshAttrib[i].format = format;
                }
            }
        }
        else
        {
            Debug.LogError($"Mesh: {mesh.name} doesnt have vertex attribute {attrib} but you are trying to edit it");
        }
    }
    public static void EditVertexAttribute(this Mesh mesh, VertexAttribute attrib, int dimension, VertexAttributeFormat format, uint stream)
    {
        if (mesh.HasVertexAttribute(attrib))
        {
            int vertexAttribCount = GetVertexAttribCount(mesh);
            VertexAttributeDescriptor[] meshAttrib = new VertexAttributeDescriptor[vertexAttribCount];
            mesh.GetVertexAttributes(meshAttrib);

            for (int i = 0; i < vertexAttribCount; i++)
            {
                if (meshAttrib[i].attribute == attrib)
                {
                    meshAttrib[i].dimension = dimension;
                    meshAttrib[i].format = format;
                    meshAttrib[i].stream = (int)stream;
                }
            }
        }
        else
        {
            Debug.LogError($"Mesh: {mesh.name} doesnt have vertex attribute {attrib} but you are trying to edit it");
        }
    }
    public static void EditVertexAttribute(this Mesh mesh, VertexAttribute attrib, int dimension, uint stream)
    {
        if (mesh.HasVertexAttribute(attrib))
        {
            int vertexAttribCount = GetVertexAttribCount(mesh);
            VertexAttributeDescriptor[] meshAttrib = new VertexAttributeDescriptor[vertexAttribCount];
            mesh.GetVertexAttributes(meshAttrib);

            for (int i = 0; i < vertexAttribCount; i++)
            {
                if (meshAttrib[i].attribute == attrib)
                {
                    meshAttrib[i].dimension = dimension;
                    meshAttrib[i].stream = (int)stream;
                }
            }
        }
        else
        {
            Debug.LogError($"Mesh: {mesh.name} doesnt have vertex attribute {attrib} but you are trying to edit it");
        }
    }
    public static void EditVertexAttribute(this Mesh mesh, VertexAttribute attrib, VertexAttributeFormat format)
    {
        if (mesh.HasVertexAttribute(attrib))
        {
            int vertexAttribCount = GetVertexAttribCount(mesh);
            VertexAttributeDescriptor[] meshAttrib = new VertexAttributeDescriptor[vertexAttribCount];
            mesh.GetVertexAttributes(meshAttrib);

            for (int i = 0; i < vertexAttribCount; i++)
            {
                if (meshAttrib[i].attribute == attrib)
                {
                    meshAttrib[i].format = format;
                }
            }
        }
        else
        {
            Debug.LogError($"Mesh: {mesh.name} doesnt have vertex attribute {attrib} but you are trying to edit it");
        }
    }
    public static void EditVertexAttribute(this Mesh mesh, VertexAttribute attrib, uint stream)
    {
        if (mesh.HasVertexAttribute(attrib))
        {
            int vertexAttribCount = GetVertexAttribCount(mesh);
            VertexAttributeDescriptor[] meshAttrib = new VertexAttributeDescriptor[vertexAttribCount];
            mesh.GetVertexAttributes(meshAttrib);

            for (int i = 0; i < vertexAttribCount; i++)
            {
                if (meshAttrib[i].attribute == attrib)
                {

                    meshAttrib[i].stream = (int)stream;
                }
            }
        }
        else
        {
            Debug.LogError($"Mesh: {mesh.name} doesnt have vertex attribute {attrib} but you are trying to edit it");
        }
    }
    public static void EditVertexAttribute(this Mesh mesh, VertexAttribute attrib, VertexAttributeFormat format, uint stream)
    {
        if (mesh.HasVertexAttribute(attrib))
        {
            int vertexAttribCount = GetVertexAttribCount(mesh);
            VertexAttributeDescriptor[] meshAttrib = new VertexAttributeDescriptor[vertexAttribCount];
            mesh.GetVertexAttributes(meshAttrib);

            for (int i = 0; i < vertexAttribCount; i++)
            {
                if (meshAttrib[i].attribute == attrib)
                {
                    meshAttrib[i].format = format;
                    meshAttrib[i].stream = (int)stream;
                }
            }
        }
        else
        {
            Debug.LogError($"Mesh: {mesh.name} doesnt have vertex attribute {attrib} but you are trying to edit it");
        }
    }
    #endregion

    public static VertexAttributeDescriptor GetVertexAttribute(this Mesh mesh, VertexAttribute attrib)
    {
        if (mesh.HasVertexAttribute(attrib))
        {
            int vertexAttribCount = GetVertexAttribCount(mesh);
            VertexAttributeDescriptor[] meshAttrib = new VertexAttributeDescriptor[vertexAttribCount];
            mesh.GetVertexAttributes(meshAttrib);

            for (int i = 0; i < vertexAttribCount; i++)
            {
                if (meshAttrib[i].attribute == attrib)
                {
                    return meshAttrib[i];
                }
            }
        }
        else
        {
            Debug.LogError($"Mesh: {mesh.name} doesnt have vertex attribute {attrib}");
        }
        return new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, dimension: 3, stream: 0);
    }
    
    public static void AddVertexAttribute(this Mesh mesh, VertexAttributeDescriptor attrib)
    {
        int vertexAttribCount = GetVertexAttribCount(mesh) + 1;
        VertexAttributeDescriptor[] meshAttrib = new VertexAttributeDescriptor[vertexAttribCount];
        mesh.GetVertexAttributes(meshAttrib);
        meshAttrib[^1] = attrib;
        mesh.SetVertexBufferParams(mesh.vertexCount, meshAttrib);
    }

    public static int GetVertexAttribCount(this Mesh mesh)
    {
        int count = 0;
        
        if (mesh.HasVertexAttribute(VertexAttribute.Color))
            count++;
        
        if (mesh.HasVertexAttribute(VertexAttribute.Normal))
            count++;
        
        if (mesh.HasVertexAttribute(VertexAttribute.Position))
            count++;
        
        if (mesh.HasVertexAttribute(VertexAttribute.Tangent))
            count++;
        
        if (mesh.HasVertexAttribute(VertexAttribute.BlendIndices))
            count++;
        
        if (mesh.HasVertexAttribute(VertexAttribute.BlendWeight))
            count++;
        
        if (mesh.HasVertexAttribute(VertexAttribute.TexCoord0))
            count++;
        
        if (mesh.HasVertexAttribute(VertexAttribute.TexCoord1))
            count++;
        
        if (mesh.HasVertexAttribute(VertexAttribute.TexCoord2))
            count++;
        
        if (mesh.HasVertexAttribute(VertexAttribute.TexCoord3))
            count++;
        
        if (mesh.HasVertexAttribute(VertexAttribute.TexCoord4))
            count++;
        
        if (mesh.HasVertexAttribute(VertexAttribute.TexCoord5))
            count++;
        
        if (mesh.HasVertexAttribute(VertexAttribute.TexCoord6))
            count++;
        
        if (mesh.HasVertexAttribute(VertexAttribute.TexCoord7))
            count++;

        return count;
    }
}
