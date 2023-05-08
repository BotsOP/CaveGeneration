using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CaveTerrainCarver
{
    private CaveChunk[,,] chunks;
    private Vector3[] caveBounds;
    private int amountChunksHorizontal;
    private int amountChunksVertical;
    private ComputeShader caveCarveShader;
    private LayerMask caveMask;
    Vector3 threadGroupSize;

    public CaveTerrainCarver(CaveChunk[,,] _chunks, Vector3[] _caveBounds, int _amountChunksHorizontal, int _amountChunksVertical, int _chunkSize, LayerMask _caveMask)
    {
        chunks = _chunks;
        caveBounds = _caveBounds;
        amountChunksHorizontal = _amountChunksHorizontal;
        amountChunksVertical = _amountChunksVertical;
        caveMask = _caveMask;
        caveCarveShader = Resources.Load<ComputeShader>("SDFCarver");
        
        caveCarveShader.GetKernelThreadGroupSizes(0, out uint threadGroupSizeX, out uint threadGroupSizeY, out uint threadGroupSizeZ);
        
        threadGroupSize.x = Mathf.CeilToInt((float)_chunkSize / threadGroupSizeX);
        threadGroupSize.y = Mathf.CeilToInt((float)_chunkSize / threadGroupSizeY);
        threadGroupSize.z = Mathf.CeilToInt((float)_chunkSize / threadGroupSizeZ);
    }

    //These functions do not yet work with other isolevels
    public void RemoveTerrain(Vector3 _pos, float _carveSize, float _carveSpeed)
    {
        Collider[] chunksHit = Physics.OverlapSphere(_pos, _carveSize, caveMask);
        foreach (var chunkCollider in chunksHit)
        {
            Vector3 chunkIndex = GetChunkIndex(chunkCollider.transform.position);
            CaveChunk chunk = chunks[(int)chunkIndex.x, (int)chunkIndex.y, (int)chunkIndex.z];
            Vector3 carvePos = _pos - chunk.position;
            
            caveCarveShader.SetTexture(0, "noiseTex", chunk.noiseTex);
            caveCarveShader.SetVector("carvePos", carvePos);
            caveCarveShader.SetFloat("carveSize", _carveSize);
            caveCarveShader.SetFloat("carveSpeed", _carveSpeed);
            
            caveCarveShader.Dispatch(0, (int)threadGroupSize.x, (int)threadGroupSize.y, (int)threadGroupSize.z);
            chunk.GenerateMesh();
        }
    }
    
    public void FillTerrain(Vector3 _pos, float _carveSize)
    {
        Collider[] chunksHit = Physics.OverlapSphere(_pos, _carveSize);
        foreach (var chunkCollider in chunksHit)
        {
            Vector3 chunkIndex = GetChunkIndex(chunkCollider.transform.position);
            CaveChunk chunk = chunks[(int)chunkIndex.x, (int)chunkIndex.y, (int)chunkIndex.z];
            Vector3 carvePos = _pos - chunk.position;
            
            caveCarveShader.SetTexture(1, "noiseTex", chunk.noiseTex);
            caveCarveShader.SetVector("carvePos", carvePos);
            caveCarveShader.SetFloat("carveSize", _carveSize);
            
            caveCarveShader.Dispatch(1, (int)threadGroupSize.x, (int)threadGroupSize.y, (int)threadGroupSize.z);
            chunk.GenerateMesh();
        }
    }
    
    private Vector3 GetChunkIndex(Vector3 _playerPos)
    {
        return _playerPos.Remap(caveBounds[0], caveBounds[1], Vector3.zero, 
                                new Vector3(amountChunksHorizontal, amountChunksVertical, amountChunksHorizontal));
    }
}
