using System;
using System.Collections.Generic;
using UnityEngine;

public class Location
{
    public int X, Y, Z;
    public int G, H;
    public Location Parent;
    public int F => G + H;

    public Vector3 pos => new Vector3(X, Y, Z);
}

public class CavePathfinding
{
    private CaveChunk[,,] chunks;
    private Vector3[] caveBounds;
    private int amountChunksHorizontal;
    private int amountChunksVertical;
    private ComputeShader getSDFInfoShader;
    private int chunkSize;
    private float chunkScale;
    private ComputeBuffer neighboursBuffer;

    public CavePathfinding(CaveChunk[,,] _chunks, Vector3[] _caveBounds, int _amountChunksHorizontal, int _amountChunksVertical, int _chunkSize, float _chunkScale)
    {
        chunks = _chunks;
        caveBounds = _caveBounds;
        amountChunksHorizontal = _amountChunksHorizontal;
        amountChunksVertical = _amountChunksVertical;
        chunkSize = _chunkSize;
        chunkScale = _chunkScale;
        
        getSDFInfoShader = Resources.Load<ComputeShader>("SDFInfo");

        neighboursBuffer = new ComputeBuffer(27, sizeof(float), ComputeBufferType.Structured);
    }

    public List<Location> FindPath(Vector3 _startPos, Vector3 _endPos)
    {
        int endX = (int)_endPos.x;
        int endY = (int)_endPos.y;
        int endZ = (int)_endPos.z;
        List<Location> openList = new List<Location>();
        List<Location> closedList = new List<Location>();

        openList.Add(new Location { X = (int)_startPos.x, Y = (int)_startPos.y, Z = (int)_startPos.z, G = 0, 
            H = CalculateHeuristic((int)_startPos.x, (int)_startPos.y, (int)_startPos.z, endX, endY, endZ) });
        closedList.Add(openList[0]);

        while (openList.Count > 0)
        {
            Location current = openList[0];
            float[] neighbours = GetNeighbours(_startPos);
            
            for (int i = 0; i < 27; i++)
            {
                //0 is wall and 1 is air
                float isAir = neighbours[i];

                if (isAir >= 0)
                {
                    int newX = current.X + i % 3 - 1;
                    int newY = current.Y + i / 9 - 1;
                    int newZ = current.Z + i / 3 - 1;
                    
                    int g = current.G + 1;
                    int h = CalculateHeuristic(newX, newY, newZ, endX, endY, endZ);

                    Location adjacentSquare = new Location { X = newX, Y = newY, Z = newZ, G = g, H = h, Parent = current};

                    if (openList.Count == 0 || g <= openList[0].G && h < openList[0].H)
                    {
                        openList.Insert(0, adjacentSquare);
                    }
                }
            }

            current = openList[0];
            openList.Remove(current);
            closedList.Add(current);
        }

        if (openList.Count == 0)
        {
            return null;
        }

        return ReconstructPath(closedList);
    }

    private float[] GetNeighbours(Vector3 _pos)
    {
        float[] neighbours = new float[27];
        
        Vector3 chunkIndex = GetChunkIndex(_pos);
        CaveChunk chunk = chunks[(int)chunkIndex.x, (int)chunkIndex.y, (int)chunkIndex.z];
        Vector3 subChunkPos = new Vector3(_pos.x - chunk.position.x, _pos.y - chunk.position.y, _pos.z - chunk.position.z);

        bool xTooLeft = subChunkPos.x == 0;
        bool xTooRight = (int)subChunkPos.x == chunkSize - 1;
        bool zTooBackward = subChunkPos.z == 0;
        bool zTooForward = (int)subChunkPos.z == chunkSize - 1;
        bool yTooDownward = subChunkPos.y == 0;
        bool yTooUpward = (int)subChunkPos.y == chunkSize - 1;

        if (!xTooLeft && !xTooRight && !zTooBackward && !zTooForward && !yTooDownward && !yTooUpward)
        {
            getSDFInfoShader.SetTexture(0, "noiseTex", chunk.noiseTex);
            getSDFInfoShader.SetBuffer(0, "neighbours", neighboursBuffer);
            getSDFInfoShader.SetVector("currentPos", subChunkPos);
            getSDFInfoShader.SetInt("chunkSize", chunkSize);
            
            getSDFInfoShader.Dispatch(0, 1, 1, 1);
            
            neighboursBuffer.GetData(neighbours);
            return neighbours;
        }
        
        //all faces
        if ((xTooLeft || xTooRight) && !zTooBackward && !zTooForward && !yTooDownward && !yTooUpward)
        {
            //left face
            if (xTooLeft)
            {
                CaveChunk chunkAdjacent = chunks[(int)chunkIndex.x - 1, (int)chunkIndex.y, (int)chunkIndex.z];
                
                getSDFInfoShader.SetTexture(1, "noiseTex", chunk.noiseTex);
                getSDFInfoShader.SetTexture(1, "noiseTexAdjacent1", chunkAdjacent.noiseTex);
                getSDFInfoShader.SetBuffer(1, "neighbours", neighboursBuffer);
                getSDFInfoShader.SetVector("currentPos", subChunkPos);
                getSDFInfoShader.SetInt("chunkSize", chunkSize);
            
                getSDFInfoShader.Dispatch(1, 1, 1, 1);
            
                neighboursBuffer.GetData(neighbours);
                return neighbours;
            }
            //right face
            if (xTooRight)
            {
                CaveChunk chunkAdjacent = chunks[(int)chunkIndex.x + 1, (int)chunkIndex.y, (int)chunkIndex.z];
                
                getSDFInfoShader.SetTexture(1, "noiseTex", chunk.noiseTex);
                getSDFInfoShader.SetTexture(1, "noiseTexAdjacent1", chunkAdjacent.noiseTex);
                getSDFInfoShader.SetBuffer(1, "neighbours", neighboursBuffer);
                getSDFInfoShader.SetVector("currentPos", subChunkPos);
                getSDFInfoShader.SetInt("chunkSize", chunkSize);
            
                getSDFInfoShader.Dispatch(1, 1, 1, 1);
            
                neighboursBuffer.GetData(neighbours);
                return neighbours;
            }
        }
        else if (!xTooLeft && !xTooRight && (zTooBackward || zTooForward) && !yTooDownward && !yTooUpward)
        {
            //back face
            if (zTooBackward)
            {
                CaveChunk chunkAdjacent = chunks[(int)chunkIndex.x, (int)chunkIndex.y, (int)chunkIndex.z - 1];
                
                getSDFInfoShader.SetTexture(1, "noiseTex", chunk.noiseTex);
                getSDFInfoShader.SetTexture(1, "noiseTexAdjacent1", chunkAdjacent.noiseTex);
                getSDFInfoShader.SetBuffer(1, "neighbours", neighboursBuffer);
                getSDFInfoShader.SetVector("currentPos", subChunkPos);
                getSDFInfoShader.SetInt("chunkSize", chunkSize);
            
                getSDFInfoShader.Dispatch(1, 1, 1, 1);
            
                neighboursBuffer.GetData(neighbours);
                return neighbours;
            }
            //forward face
            if (zTooForward)
            {
                CaveChunk chunkAdjacent = chunks[(int)chunkIndex.x, (int)chunkIndex.y, (int)chunkIndex.z + 1];
                
                getSDFInfoShader.SetTexture(1, "noiseTex", chunk.noiseTex);
                getSDFInfoShader.SetTexture(1, "noiseTexAdjacent1", chunkAdjacent.noiseTex);
                getSDFInfoShader.SetBuffer(1, "neighbours", neighboursBuffer);
                getSDFInfoShader.SetVector("currentPos", subChunkPos);
                getSDFInfoShader.SetInt("chunkSize", chunkSize);
            
                getSDFInfoShader.Dispatch(1, 1, 1, 1);
            
                neighboursBuffer.GetData(neighbours);
                return neighbours;
            }
        }
        else if (!xTooLeft && !xTooRight && !zTooBackward && !zTooForward && (yTooDownward || yTooUpward))
        {
            //down face
            if (yTooDownward)
            {
                CaveChunk chunkAdjacent = chunks[(int)chunkIndex.x, (int)chunkIndex.y - 1, (int)chunkIndex.z];
                
                getSDFInfoShader.SetTexture(1, "noiseTex", chunk.noiseTex);
                getSDFInfoShader.SetTexture(1, "noiseTexAdjacent1", chunkAdjacent.noiseTex);
                getSDFInfoShader.SetBuffer(1, "neighbours", neighboursBuffer);
                getSDFInfoShader.SetVector("currentPos", subChunkPos);
                getSDFInfoShader.SetInt("chunkSize", chunkSize);
            
                getSDFInfoShader.Dispatch(1, 1, 1, 1);
            
                neighboursBuffer.GetData(neighbours);
                return neighbours;
            }
            //up face
            if (yTooUpward)
            {
                CaveChunk chunkAdjacent = chunks[(int)chunkIndex.x, (int)chunkIndex.y + 1, (int)chunkIndex.z];
                
                getSDFInfoShader.SetTexture(1, "noiseTex", chunk.noiseTex);
                getSDFInfoShader.SetTexture(1, "noiseTexAdjacent1", chunkAdjacent.noiseTex);
                getSDFInfoShader.SetBuffer(1, "neighbours", neighboursBuffer);
                getSDFInfoShader.SetVector("currentPos", subChunkPos);
                getSDFInfoShader.SetInt("chunkSize", chunkSize);
            
                getSDFInfoShader.Dispatch(1, 1, 1, 1);
            
                neighboursBuffer.GetData(neighbours);
                return neighbours;
            }
        }
        //all edges
        else if (xTooLeft && yTooDownward && !zTooBackward && !zTooForward)
        {
            CaveChunk chunkAdjacent = chunks[(int)chunkIndex.x - 1, (int)chunkIndex.y, (int)chunkIndex.z];
            CaveChunk chunkAdjacent1 = chunks[(int)chunkIndex.x, (int)chunkIndex.y - 1, (int)chunkIndex.z];
            CaveChunk chunkAdjacent2 = chunks[(int)chunkIndex.x - 1, (int)chunkIndex.y - 1, (int)chunkIndex.z];
                
            getSDFInfoShader.SetTexture(1, "noiseTex", chunk.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent1", chunkAdjacent.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent4", chunkAdjacent1.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent3", chunkAdjacent2.noiseTex);
            getSDFInfoShader.SetBuffer(1, "neighbours", neighboursBuffer);
            getSDFInfoShader.SetVector("currentPos", subChunkPos);
            getSDFInfoShader.SetInt("chunkSize", chunkSize);
            
            getSDFInfoShader.Dispatch(1, 1, 1, 1);
            
            neighboursBuffer.GetData(neighbours);
            return neighbours;
        }
        else if (zTooForward && yTooDownward && !xTooLeft && !xTooRight)
        {
            CaveChunk chunkAdjacent = chunks[(int)chunkIndex.x, (int)chunkIndex.y, (int)chunkIndex.z + 1];
            CaveChunk chunkAdjacent1 = chunks[(int)chunkIndex.x, (int)chunkIndex.y - 1, (int)chunkIndex.z];
            CaveChunk chunkAdjacent2 = chunks[(int)chunkIndex.x, (int)chunkIndex.y - 1, (int)chunkIndex.z + 1];
                
            getSDFInfoShader.SetTexture(1, "noiseTex", chunk.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent1", chunkAdjacent.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent4", chunkAdjacent1.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent3", chunkAdjacent2.noiseTex);
            getSDFInfoShader.SetBuffer(1, "neighbours", neighboursBuffer);
            getSDFInfoShader.SetVector("currentPos", subChunkPos);
            getSDFInfoShader.SetInt("chunkSize", chunkSize);
            
            getSDFInfoShader.Dispatch(1, 1, 1, 1);
            
            neighboursBuffer.GetData(neighbours);
            return neighbours;
        }
        else if (xTooRight && yTooDownward && !zTooBackward && !zTooForward)
        {
            CaveChunk chunkAdjacent = chunks[(int)chunkIndex.x + 1, (int)chunkIndex.y, (int)chunkIndex.z];
            CaveChunk chunkAdjacent1 = chunks[(int)chunkIndex.x, (int)chunkIndex.y - 1, (int)chunkIndex.z];
            CaveChunk chunkAdjacent2 = chunks[(int)chunkIndex.x + 1, (int)chunkIndex.y - 1, (int)chunkIndex.z];
                
            getSDFInfoShader.SetTexture(1, "noiseTex", chunk.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent1", chunkAdjacent.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent4", chunkAdjacent1.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent3", chunkAdjacent2.noiseTex);
            getSDFInfoShader.SetBuffer(1, "neighbours", neighboursBuffer);
            getSDFInfoShader.SetVector("currentPos", subChunkPos);
            getSDFInfoShader.SetInt("chunkSize", chunkSize);
            
            getSDFInfoShader.Dispatch(1, 1, 1, 1);
            
            neighboursBuffer.GetData(neighbours);
            return neighbours;
        }
        else if (zTooBackward && yTooDownward && !xTooLeft && !xTooRight)
        {
            CaveChunk chunkAdjacent = chunks[(int)chunkIndex.x, (int)chunkIndex.y, (int)chunkIndex.z - 1];
            CaveChunk chunkAdjacent1 = chunks[(int)chunkIndex.x, (int)chunkIndex.y - 1, (int)chunkIndex.z];
            CaveChunk chunkAdjacent2 = chunks[(int)chunkIndex.x, (int)chunkIndex.y - 1, (int)chunkIndex.z - 1];
                
            getSDFInfoShader.SetTexture(1, "noiseTex", chunk.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent1", chunkAdjacent.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent4", chunkAdjacent1.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent3", chunkAdjacent2.noiseTex);
            getSDFInfoShader.SetBuffer(1, "neighbours", neighboursBuffer);
            getSDFInfoShader.SetVector("currentPos", subChunkPos);
            getSDFInfoShader.SetInt("chunkSize", chunkSize);
            
            getSDFInfoShader.Dispatch(1, 1, 1, 1);
            
            neighboursBuffer.GetData(neighbours);
            return neighbours;
        }
        else if (xTooLeft && yTooUpward && !zTooBackward && !zTooForward)
        {
            CaveChunk chunkAdjacent = chunks[(int)chunkIndex.x - 1, (int)chunkIndex.y, (int)chunkIndex.z];
            CaveChunk chunkAdjacent1 = chunks[(int)chunkIndex.x, (int)chunkIndex.y + 1, (int)chunkIndex.z];
            CaveChunk chunkAdjacent2 = chunks[(int)chunkIndex.x - 1, (int)chunkIndex.y + 1, (int)chunkIndex.z];
                
            getSDFInfoShader.SetTexture(1, "noiseTex", chunk.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent1", chunkAdjacent.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent4", chunkAdjacent1.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent3", chunkAdjacent2.noiseTex);
            getSDFInfoShader.SetBuffer(1, "neighbours", neighboursBuffer);
            getSDFInfoShader.SetVector("currentPos", subChunkPos);
            getSDFInfoShader.SetInt("chunkSize", chunkSize);
            
            getSDFInfoShader.Dispatch(1, 1, 1, 1);
            
            neighboursBuffer.GetData(neighbours);
            return neighbours;
        }
        else if (zTooForward && yTooUpward && !xTooLeft && !xTooRight)
        {
            CaveChunk chunkAdjacent = chunks[(int)chunkIndex.x, (int)chunkIndex.y, (int)chunkIndex.z + 1];
            CaveChunk chunkAdjacent1 = chunks[(int)chunkIndex.x, (int)chunkIndex.y + 1, (int)chunkIndex.z];
            CaveChunk chunkAdjacent2 = chunks[(int)chunkIndex.x, (int)chunkIndex.y + 1, (int)chunkIndex.z + 1];
                
            getSDFInfoShader.SetTexture(1, "noiseTex", chunk.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent1", chunkAdjacent.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent4", chunkAdjacent1.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent3", chunkAdjacent2.noiseTex);
            getSDFInfoShader.SetBuffer(1, "neighbours", neighboursBuffer);
            getSDFInfoShader.SetVector("currentPos", subChunkPos);
            getSDFInfoShader.SetInt("chunkSize", chunkSize);
            
            getSDFInfoShader.Dispatch(1, 1, 1, 1);
            
            neighboursBuffer.GetData(neighbours);
            return neighbours;
        }
        else if (xTooRight && yTooUpward && !zTooBackward && !zTooForward)
        {
            CaveChunk chunkAdjacent = chunks[(int)chunkIndex.x + 1, (int)chunkIndex.y, (int)chunkIndex.z];
            CaveChunk chunkAdjacent1 = chunks[(int)chunkIndex.x, (int)chunkIndex.y + 1, (int)chunkIndex.z];
            CaveChunk chunkAdjacent2 = chunks[(int)chunkIndex.x + 1, (int)chunkIndex.y + 1, (int)chunkIndex.z];
                
            getSDFInfoShader.SetTexture(1, "noiseTex", chunk.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent1", chunkAdjacent.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent4", chunkAdjacent1.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent3", chunkAdjacent2.noiseTex);
            getSDFInfoShader.SetBuffer(1, "neighbours", neighboursBuffer);
            getSDFInfoShader.SetVector("currentPos", subChunkPos);
            getSDFInfoShader.SetInt("chunkSize", chunkSize);
            
            getSDFInfoShader.Dispatch(1, 1, 1, 1);
            
            neighboursBuffer.GetData(neighbours);
            return neighbours;
        }
        else if (zTooBackward && yTooUpward && !xTooLeft && !xTooRight)
        {
            CaveChunk chunkAdjacent = chunks[(int)chunkIndex.x, (int)chunkIndex.y, (int)chunkIndex.z - 1];
            CaveChunk chunkAdjacent1 = chunks[(int)chunkIndex.x, (int)chunkIndex.y - 1, (int)chunkIndex.z];
            CaveChunk chunkAdjacent2 = chunks[(int)chunkIndex.x, (int)chunkIndex.y - 1, (int)chunkIndex.z - 1];
                
            getSDFInfoShader.SetTexture(1, "noiseTex", chunk.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent1", chunkAdjacent.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent4", chunkAdjacent1.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent3", chunkAdjacent2.noiseTex);
            getSDFInfoShader.SetBuffer(1, "neighbours", neighboursBuffer);
            getSDFInfoShader.SetVector("currentPos", subChunkPos);
            getSDFInfoShader.SetInt("chunkSize", chunkSize);
            
            getSDFInfoShader.Dispatch(1, 1, 1, 1);
            
            neighboursBuffer.GetData(neighbours);
            return neighbours;
        }
        else if (zTooBackward && xTooLeft && !yTooDownward && !yTooUpward)
        {
            CaveChunk chunkAdjacent = chunks[(int)chunkIndex.x, (int)chunkIndex.y, (int)chunkIndex.z - 1];
            CaveChunk chunkAdjacent1 = chunks[(int)chunkIndex.x - 1, (int)chunkIndex.y, (int)chunkIndex.z];
            CaveChunk chunkAdjacent2 = chunks[(int)chunkIndex.x - 1, (int)chunkIndex.y, (int)chunkIndex.z - 1];
                
            getSDFInfoShader.SetTexture(1, "noiseTex", chunk.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent1", chunkAdjacent.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent4", chunkAdjacent1.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent3", chunkAdjacent2.noiseTex);
            getSDFInfoShader.SetBuffer(1, "neighbours", neighboursBuffer);
            getSDFInfoShader.SetVector("currentPos", subChunkPos);
            getSDFInfoShader.SetInt("chunkSize", chunkSize);
            
            getSDFInfoShader.Dispatch(1, 1, 1, 1);
            
            neighboursBuffer.GetData(neighbours);
            return neighbours;
        }
        else if (zTooForward && xTooLeft && !yTooDownward && !yTooUpward)
        {
            CaveChunk chunkAdjacent = chunks[(int)chunkIndex.x, (int)chunkIndex.y, (int)chunkIndex.z + 1];
            CaveChunk chunkAdjacent1 = chunks[(int)chunkIndex.x - 1, (int)chunkIndex.y, (int)chunkIndex.z];
            CaveChunk chunkAdjacent2 = chunks[(int)chunkIndex.x - 1, (int)chunkIndex.y, (int)chunkIndex.z + 1];
                
            getSDFInfoShader.SetTexture(1, "noiseTex", chunk.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent1", chunkAdjacent.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent4", chunkAdjacent1.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent3", chunkAdjacent2.noiseTex);
            getSDFInfoShader.SetBuffer(1, "neighbours", neighboursBuffer);
            getSDFInfoShader.SetVector("currentPos", subChunkPos);
            getSDFInfoShader.SetInt("chunkSize", chunkSize);
            
            getSDFInfoShader.Dispatch(1, 1, 1, 1);
            
            neighboursBuffer.GetData(neighbours);
            return neighbours;
        }
        else if (zTooForward && xTooRight && !yTooDownward && !yTooUpward)
        {
            CaveChunk chunkAdjacent = chunks[(int)chunkIndex.x, (int)chunkIndex.y, (int)chunkIndex.z + 1];
            CaveChunk chunkAdjacent1 = chunks[(int)chunkIndex.x + 1, (int)chunkIndex.y, (int)chunkIndex.z];
            CaveChunk chunkAdjacent2 = chunks[(int)chunkIndex.x + 1, (int)chunkIndex.y, (int)chunkIndex.z + 1];
                
            getSDFInfoShader.SetTexture(1, "noiseTex", chunk.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent1", chunkAdjacent.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent4", chunkAdjacent1.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent3", chunkAdjacent2.noiseTex);
            getSDFInfoShader.SetBuffer(1, "neighbours", neighboursBuffer);
            getSDFInfoShader.SetVector("currentPos", subChunkPos);
            getSDFInfoShader.SetInt("chunkSize", chunkSize);
            
            getSDFInfoShader.Dispatch(1, 1, 1, 1);
            
            neighboursBuffer.GetData(neighbours);
            return neighbours;
        }
        else if (zTooBackward && xTooRight && !yTooDownward && !yTooUpward)
        {
            CaveChunk chunkAdjacent = chunks[(int)chunkIndex.x, (int)chunkIndex.y, (int)chunkIndex.z - 1];
            CaveChunk chunkAdjacent1 = chunks[(int)chunkIndex.x + 1, (int)chunkIndex.y, (int)chunkIndex.z];
            CaveChunk chunkAdjacent2 = chunks[(int)chunkIndex.x + 1, (int)chunkIndex.y, (int)chunkIndex.z - 1];
                
            getSDFInfoShader.SetTexture(1, "noiseTex", chunk.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent1", chunkAdjacent.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent4", chunkAdjacent1.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent3", chunkAdjacent2.noiseTex);
            getSDFInfoShader.SetBuffer(1, "neighbours", neighboursBuffer);
            getSDFInfoShader.SetVector("currentPos", subChunkPos);
            getSDFInfoShader.SetInt("chunkSize", chunkSize);
            
            getSDFInfoShader.Dispatch(1, 1, 1, 1);
            
            neighboursBuffer.GetData(neighbours);
            return neighbours;
        }
        //all corners
        else if (xTooLeft && zTooBackward && yTooDownward)
        {
            CaveChunk chunkAdjacent = chunks[(int)chunkIndex.x - 1, (int)chunkIndex.y, (int)chunkIndex.z];
            CaveChunk chunkAdjacent1 = chunks[(int)chunkIndex.x, (int)chunkIndex.y, (int)chunkIndex.z - 1];
            CaveChunk chunkAdjacent2 = chunks[(int)chunkIndex.x - 1, (int)chunkIndex.y , (int)chunkIndex.z - 1];
            CaveChunk chunkAdjacent3 = chunks[(int)chunkIndex.x - 1, (int)chunkIndex.y - 1, (int)chunkIndex.z];
            CaveChunk chunkAdjacent4 = chunks[(int)chunkIndex.x, (int)chunkIndex.y - 1, (int)chunkIndex.z - 1];
            CaveChunk chunkAdjacent5 = chunks[(int)chunkIndex.x - 1, (int)chunkIndex.y - 1, (int)chunkIndex.z - 1];
            CaveChunk chunkAdjacent6 = chunks[(int)chunkIndex.x, (int)chunkIndex.y - 1, (int)chunkIndex.z];
                
            getSDFInfoShader.SetTexture(1, "noiseTex", chunk.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent1", chunkAdjacent.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent2", chunkAdjacent1.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent3", chunkAdjacent2.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent4", chunkAdjacent3.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent5", chunkAdjacent4.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent6", chunkAdjacent5.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent7", chunkAdjacent6.noiseTex);
            getSDFInfoShader.SetBuffer(1, "neighbours", neighboursBuffer);
            getSDFInfoShader.SetVector("currentPos", subChunkPos);
            getSDFInfoShader.SetInt("chunkSize", chunkSize);
            
            getSDFInfoShader.Dispatch(1, 1, 1, 1);
            
            neighboursBuffer.GetData(neighbours);
            return neighbours;
        }
        else if (xTooLeft && zTooBackward && yTooUpward)
        {
            CaveChunk chunkAdjacent = chunks[(int)chunkIndex.x - 1, (int)chunkIndex.y, (int)chunkIndex.z];
            CaveChunk chunkAdjacent1 = chunks[(int)chunkIndex.x, (int)chunkIndex.y, (int)chunkIndex.z - 1];
            CaveChunk chunkAdjacent2 = chunks[(int)chunkIndex.x - 1, (int)chunkIndex.y , (int)chunkIndex.z - 1];
            CaveChunk chunkAdjacent3 = chunks[(int)chunkIndex.x - 1, (int)chunkIndex.y + 1, (int)chunkIndex.z];
            CaveChunk chunkAdjacent4 = chunks[(int)chunkIndex.x, (int)chunkIndex.y + 1, (int)chunkIndex.z - 1];
            CaveChunk chunkAdjacent5 = chunks[(int)chunkIndex.x - 1, (int)chunkIndex.y + 1, (int)chunkIndex.z - 1];
            CaveChunk chunkAdjacent6 = chunks[(int)chunkIndex.x, (int)chunkIndex.y + 1, (int)chunkIndex.z];
                
            getSDFInfoShader.SetTexture(1, "noiseTex", chunk.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent1", chunkAdjacent.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent2", chunkAdjacent1.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent3", chunkAdjacent2.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent4", chunkAdjacent3.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent5", chunkAdjacent4.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent6", chunkAdjacent5.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent7", chunkAdjacent6.noiseTex);
            getSDFInfoShader.SetBuffer(1, "neighbours", neighboursBuffer);
            getSDFInfoShader.SetVector("currentPos", subChunkPos);
            getSDFInfoShader.SetInt("chunkSize", chunkSize);
            
            getSDFInfoShader.Dispatch(1, 1, 1, 1);
            
            neighboursBuffer.GetData(neighbours);
            return neighbours;
        }
        else if (xTooLeft && zTooForward && yTooUpward)
        {
            CaveChunk chunkAdjacent = chunks[(int)chunkIndex.x - 1, (int)chunkIndex.y, (int)chunkIndex.z];
            CaveChunk chunkAdjacent1 = chunks[(int)chunkIndex.x, (int)chunkIndex.y, (int)chunkIndex.z + 1];
            CaveChunk chunkAdjacent2 = chunks[(int)chunkIndex.x - 1, (int)chunkIndex.y , (int)chunkIndex.z + 1];
            CaveChunk chunkAdjacent3 = chunks[(int)chunkIndex.x - 1, (int)chunkIndex.y + 1, (int)chunkIndex.z];
            CaveChunk chunkAdjacent4 = chunks[(int)chunkIndex.x, (int)chunkIndex.y + 1, (int)chunkIndex.z + 1];
            CaveChunk chunkAdjacent5 = chunks[(int)chunkIndex.x - 1, (int)chunkIndex.y + 1, (int)chunkIndex.z + 1];
            CaveChunk chunkAdjacent6 = chunks[(int)chunkIndex.x, (int)chunkIndex.y + 1, (int)chunkIndex.z];
                
            getSDFInfoShader.SetTexture(1, "noiseTex", chunk.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent1", chunkAdjacent.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent2", chunkAdjacent1.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent3", chunkAdjacent2.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent4", chunkAdjacent3.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent5", chunkAdjacent4.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent6", chunkAdjacent5.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent7", chunkAdjacent6.noiseTex);
            getSDFInfoShader.SetBuffer(1, "neighbours", neighboursBuffer);
            getSDFInfoShader.SetVector("currentPos", subChunkPos);
            getSDFInfoShader.SetInt("chunkSize", chunkSize);
            
            getSDFInfoShader.Dispatch(1, 1, 1, 1);
            
            neighboursBuffer.GetData(neighbours);
            return neighbours;
        }
        else if (xTooLeft && zTooForward && yTooDownward)
        {
            CaveChunk chunkAdjacent = chunks[(int)chunkIndex.x - 1, (int)chunkIndex.y, (int)chunkIndex.z];
            CaveChunk chunkAdjacent1 = chunks[(int)chunkIndex.x, (int)chunkIndex.y, (int)chunkIndex.z + 1];
            CaveChunk chunkAdjacent2 = chunks[(int)chunkIndex.x - 1, (int)chunkIndex.y , (int)chunkIndex.z + 1];
            CaveChunk chunkAdjacent3 = chunks[(int)chunkIndex.x - 1, (int)chunkIndex.y + 1, (int)chunkIndex.z];
            CaveChunk chunkAdjacent4 = chunks[(int)chunkIndex.x, (int)chunkIndex.y + 1, (int)chunkIndex.z + 1];
            CaveChunk chunkAdjacent5 = chunks[(int)chunkIndex.x - 1, (int)chunkIndex.y + 1, (int)chunkIndex.z + 1];
            CaveChunk chunkAdjacent6 = chunks[(int)chunkIndex.x, (int)chunkIndex.y + 1, (int)chunkIndex.z];
                
            getSDFInfoShader.SetTexture(1, "noiseTex", chunk.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent1", chunkAdjacent.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent2", chunkAdjacent1.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent3", chunkAdjacent2.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent4", chunkAdjacent3.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent5", chunkAdjacent4.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent6", chunkAdjacent5.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent7", chunkAdjacent6.noiseTex);
            getSDFInfoShader.SetBuffer(1, "neighbours", neighboursBuffer);
            getSDFInfoShader.SetVector("currentPos", subChunkPos);
            getSDFInfoShader.SetInt("chunkSize", chunkSize);
            
            getSDFInfoShader.Dispatch(1, 1, 1, 1);
            
            neighboursBuffer.GetData(neighbours);
            return neighbours;
        }
        else if (xTooRight && zTooBackward && yTooDownward)
        {
            CaveChunk chunkAdjacent = chunks[(int)chunkIndex.x + 1, (int)chunkIndex.y, (int)chunkIndex.z];
            CaveChunk chunkAdjacent1 = chunks[(int)chunkIndex.x, (int)chunkIndex.y, (int)chunkIndex.z - 1];
            CaveChunk chunkAdjacent2 = chunks[(int)chunkIndex.x + 1, (int)chunkIndex.y , (int)chunkIndex.z - 1];
            CaveChunk chunkAdjacent3 = chunks[(int)chunkIndex.x + 1, (int)chunkIndex.y - 1, (int)chunkIndex.z];
            CaveChunk chunkAdjacent4 = chunks[(int)chunkIndex.x, (int)chunkIndex.y - 1, (int)chunkIndex.z - 1];
            CaveChunk chunkAdjacent5 = chunks[(int)chunkIndex.x + 1, (int)chunkIndex.y - 1, (int)chunkIndex.z - 1];
            CaveChunk chunkAdjacent6 = chunks[(int)chunkIndex.x, (int)chunkIndex.y - 1, (int)chunkIndex.z];
                
            getSDFInfoShader.SetTexture(1, "noiseTex", chunk.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent1", chunkAdjacent.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent2", chunkAdjacent1.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent3", chunkAdjacent2.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent4", chunkAdjacent3.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent5", chunkAdjacent4.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent6", chunkAdjacent5.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent7", chunkAdjacent6.noiseTex);
            getSDFInfoShader.SetBuffer(1, "neighbours", neighboursBuffer);
            getSDFInfoShader.SetVector("currentPos", subChunkPos);
            getSDFInfoShader.SetInt("chunkSize", chunkSize);
            
            getSDFInfoShader.Dispatch(1, 1, 1, 1);
            
            neighboursBuffer.GetData(neighbours);
            return neighbours;
        }
        else if (xTooRight && zTooBackward && yTooUpward)
        {
            CaveChunk chunkAdjacent = chunks[(int)chunkIndex.x + 1, (int)chunkIndex.y, (int)chunkIndex.z];
            CaveChunk chunkAdjacent1 = chunks[(int)chunkIndex.x, (int)chunkIndex.y, (int)chunkIndex.z - 1];
            CaveChunk chunkAdjacent2 = chunks[(int)chunkIndex.x + 1, (int)chunkIndex.y , (int)chunkIndex.z - 1];
            CaveChunk chunkAdjacent3 = chunks[(int)chunkIndex.x + 1, (int)chunkIndex.y + 1, (int)chunkIndex.z];
            CaveChunk chunkAdjacent4 = chunks[(int)chunkIndex.x, (int)chunkIndex.y + 1, (int)chunkIndex.z - 1];
            CaveChunk chunkAdjacent5 = chunks[(int)chunkIndex.x + 1, (int)chunkIndex.y + 1, (int)chunkIndex.z - 1];
            CaveChunk chunkAdjacent6 = chunks[(int)chunkIndex.x, (int)chunkIndex.y + 1, (int)chunkIndex.z];
                
            getSDFInfoShader.SetTexture(1, "noiseTex", chunk.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent1", chunkAdjacent.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent2", chunkAdjacent1.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent3", chunkAdjacent2.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent4", chunkAdjacent3.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent5", chunkAdjacent4.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent6", chunkAdjacent5.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent7", chunkAdjacent6.noiseTex);
            getSDFInfoShader.SetBuffer(1, "neighbours", neighboursBuffer);
            getSDFInfoShader.SetVector("currentPos", subChunkPos);
            getSDFInfoShader.SetInt("chunkSize", chunkSize);
            
            getSDFInfoShader.Dispatch(1, 1, 1, 1);
            
            neighboursBuffer.GetData(neighbours);
            return neighbours;
        }
        else if (xTooRight && zTooForward && yTooUpward)
        {
            CaveChunk chunkAdjacent = chunks[(int)chunkIndex.x + 1, (int)chunkIndex.y, (int)chunkIndex.z];
            CaveChunk chunkAdjacent1 = chunks[(int)chunkIndex.x, (int)chunkIndex.y, (int)chunkIndex.z + 1];
            CaveChunk chunkAdjacent2 = chunks[(int)chunkIndex.x + 1, (int)chunkIndex.y , (int)chunkIndex.z + 1];
            CaveChunk chunkAdjacent3 = chunks[(int)chunkIndex.x + 1, (int)chunkIndex.y + 1, (int)chunkIndex.z];
            CaveChunk chunkAdjacent4 = chunks[(int)chunkIndex.x, (int)chunkIndex.y + 1, (int)chunkIndex.z + 1];
            CaveChunk chunkAdjacent5 = chunks[(int)chunkIndex.x + 1, (int)chunkIndex.y + 1, (int)chunkIndex.z + 1];
            CaveChunk chunkAdjacent6 = chunks[(int)chunkIndex.x, (int)chunkIndex.y + 1, (int)chunkIndex.z];
                
            getSDFInfoShader.SetTexture(1, "noiseTex", chunk.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent1", chunkAdjacent.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent2", chunkAdjacent1.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent3", chunkAdjacent2.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent4", chunkAdjacent3.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent5", chunkAdjacent4.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent6", chunkAdjacent5.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent7", chunkAdjacent6.noiseTex);
            getSDFInfoShader.SetBuffer(1, "neighbours", neighboursBuffer);
            getSDFInfoShader.SetVector("currentPos", subChunkPos);
            getSDFInfoShader.SetInt("chunkSize", chunkSize);
            
            getSDFInfoShader.Dispatch(1, 1, 1, 1);
            
            neighboursBuffer.GetData(neighbours);
            return neighbours;
        }
        else if (xTooRight && zTooForward && yTooDownward)
        {
            CaveChunk chunkAdjacent = chunks[(int)chunkIndex.x + 1, (int)chunkIndex.y, (int)chunkIndex.z];
            CaveChunk chunkAdjacent1 = chunks[(int)chunkIndex.x, (int)chunkIndex.y, (int)chunkIndex.z - 1];
            CaveChunk chunkAdjacent2 = chunks[(int)chunkIndex.x + 1, (int)chunkIndex.y , (int)chunkIndex.z - 1];
            CaveChunk chunkAdjacent3 = chunks[(int)chunkIndex.x + 1, (int)chunkIndex.y - 1, (int)chunkIndex.z];
            CaveChunk chunkAdjacent4 = chunks[(int)chunkIndex.x, (int)chunkIndex.y - 1, (int)chunkIndex.z - 1];
            CaveChunk chunkAdjacent5 = chunks[(int)chunkIndex.x + 1, (int)chunkIndex.y - 1, (int)chunkIndex.z - 1];
            CaveChunk chunkAdjacent6 = chunks[(int)chunkIndex.x, (int)chunkIndex.y - 1, (int)chunkIndex.z];
                
            getSDFInfoShader.SetTexture(1, "noiseTex", chunk.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent1", chunkAdjacent.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent2", chunkAdjacent1.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent3", chunkAdjacent2.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent4", chunkAdjacent3.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent5", chunkAdjacent4.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent6", chunkAdjacent5.noiseTex);
            getSDFInfoShader.SetTexture(1, "noiseTexAdjacent7", chunkAdjacent6.noiseTex);
            getSDFInfoShader.SetBuffer(1, "neighbours", neighboursBuffer);
            getSDFInfoShader.SetVector("currentPos", subChunkPos);
            getSDFInfoShader.SetInt("chunkSize", chunkSize);
            
            getSDFInfoShader.Dispatch(1, 1, 1, 1);
            
            neighboursBuffer.GetData(neighbours);
            return neighbours;
        }

        return neighbours;
    }

    private int CalculateHeuristic(int x1, int y1, int z1, int x2, int y2, int z2)
    {
        int dx = Math.Abs(x2 - x1);
        int dy = Math.Abs(y2 - y1);
        int dz = Math.Abs(z2 - z1);

        return dx + dy + dz;
    }

    private List<Location> ReconstructPath(List<Location> closedList)
    {
        List<Location> path = new List<Location>();

        Location current = closedList[closedList.Count - 1];
        while (current != null)
        {
            path.Add(current);
            current = current.Parent;
        }

        path.Reverse();
        return path;
    }
    
    private Vector3 GetChunkIndex(Vector3 _pos)
    {
        return _pos.Remap(caveBounds[0], caveBounds[1], Vector3.zero, 
            new Vector3(amountChunksHorizontal, amountChunksVertical, amountChunksHorizontal));
    }
}