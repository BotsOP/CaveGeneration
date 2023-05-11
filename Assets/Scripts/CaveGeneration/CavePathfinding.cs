using System;
using System.Collections.Generic;
using UnityEngine;

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

        neighboursBuffer = new ComputeBuffer(27, sizeof(bool), ComputeBufferType.Structured);
    }

    public class Location
    {
        public int X, Y, Z;
        public int G, H;
        public Location Parent;
        public int F => G + H;
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
                    int newX = current.X + i % 3;
                    int newY = current.Y + i / 9;
                    int newZ = current.Z + i / 3;
                    
                    int g = current.G + 1;
                    int h = CalculateHeuristic(newX, newY, newZ, endX, endY, endZ);

                    Location adjacentSquare = new Location { X = newX, Y = newY, Z = newZ, G = g, H = h };

                    if (openList.Count == 0 || g < openList[0].G || g == openList[0].G && h < openList[0].H)
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
        // float[] neighbours = new float[27];
        // Vector3 chunkIndex = GetChunkIndex(_pos);
        // CaveChunk chunk = chunks[(int)chunkIndex.x, (int)chunkIndex.y, (int)chunkIndex.z];
        // Vector3 subChunkPos = new Vector3(current.X - chunk.position.x, current.Y - chunk.position.y,
        //     current.Z - chunk.position.z);
        // if (subChunkPos.x == 0)
        // {
        //         
        // }
        return new float[8];
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