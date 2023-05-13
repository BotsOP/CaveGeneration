using System;
using System.Collections.Generic;
using UnityEngine;
using Utils;

public class CavePathfinding
{
    public class Node
    {
        public Vector3Int position;
        public Node parent;
        public float gScore;
        public float hScore;
        public float fScore;

        public Node(Vector3Int position, Node parent, float gScore, float hScore)
        {
            this.position = position;
            this.parent = parent;
            this.gScore = gScore;
            this.hScore = hScore;
            fScore = gScore + hScore;
        }
    }

    public static CavePathfinding instance;
    private CaveChunk[,,] chunks;
    private Vector3[] caveBounds;
    private int amountChunksHorizontal;
    private int amountChunksVertical;
    private ComputeShader getSDFInfoShader;
    private int chunkSize;
    private ComputeBuffer neighboursBuffer;
    private float isoLevel;

    public CavePathfinding(CaveChunk[,,] _chunks, Vector3[] _caveBounds, int _amountChunksHorizontal, int _amountChunksVertical, int _chunkSize, float _isoLevel)
    {
        chunks = _chunks;
        caveBounds = _caveBounds;
        amountChunksHorizontal = _amountChunksHorizontal;
        amountChunksVertical = _amountChunksVertical;
        chunkSize = _chunkSize;
        isoLevel = _isoLevel;

        getSDFInfoShader = Resources.Load<ComputeShader>("SDFInfo");

        neighboursBuffer = new ComputeBuffer(27, sizeof(float), ComputeBufferType.Structured);

        instance = this;
    }

    public void OnDestroy()
    {
        neighboursBuffer.Dispose();
        neighboursBuffer = null;
    }

    public List<Vector3Int> AStarPathfinding(Vector3 _start, Vector3 _goal)
    {
        float startTerrainValue = GetTerrainValue(_start);
        float endTerrainValue = GetTerrainValue(_goal);
        if (!(startTerrainValue > isoLevel && startTerrainValue < isoLevel + 0.4f))
        {
            Debug.LogWarning($"Start pos is not in a valid position, {startTerrainValue}");
            return null;
        }
        if (!(endTerrainValue > isoLevel && endTerrainValue < isoLevel + 0.4f))
        {
            Debug.LogWarning($"End pos is not in a valid position, {endTerrainValue}");
            return null;
        }
        
        Vector3Int start = new Vector3Int(Mathf.RoundToInt(_start.x), Mathf.RoundToInt(_start.y), Mathf.RoundToInt(_start.z));
        Vector3Int goal = new Vector3Int(Mathf.RoundToInt(_goal.x), Mathf.RoundToInt(_goal.y), Mathf.RoundToInt(_goal.z));

        PriorityQueue<Node, float> openList = new PriorityQueue<Node, float>();
        HashSet<Vector3Int> closedList = new HashSet<Vector3Int>();
        Dictionary<Vector3Int, Node> nodeLookup = new Dictionary<Vector3Int, Node>();

        Node startNode = new Node(start, null, 0, EuclideanHeuristic(start, goal));
        openList.Enqueue(startNode, startNode.fScore);
        nodeLookup.Add(start, startNode);

        int count = 0;
        
        while (openList.Count > 0)
        {
            Node current = openList.Dequeue();

            if (Vector3Int.Distance(current.position, goal) < 2)
            {
                Debug.Log(count);
                return ConstructPath(current);
            }

            closedList.Add(current.position);

            float[] neighbours = GetNeighbours(current.position);

            for (int i = 0; i < 27; i++)
            {
                //0 is wall and 1 is air
                float terrainValue = neighbours[i];
                bool isAir = terrainValue > isoLevel && terrainValue < isoLevel + 0.4f;
                
                int newX = i % 3 - 1;
                int newY = i / 9 - 1;
                int newZ = i % 9 / 3 - 1;

                Vector3Int neighborPosition = current.position + new Vector3Int(newX, newY, newZ);
                
                if (closedList.Contains(neighborPosition))
                {
                    continue;
                }
                if (!isAir)
                {
                    continue;
                }

                terrainValue = (terrainValue - isoLevel) * 4;
                float gScore = current.gScore + Vector3Int.Distance(current.position, neighborPosition) + terrainValue;
                float hScore = EuclideanHeuristic(neighborPosition, goal);
                float fScore = gScore + hScore;

                if (!nodeLookup.TryGetValue(neighborPosition, out Node neighborNode))
                {
                    neighborNode = new Node(neighborPosition, current, gScore, hScore);
                    nodeLookup.Add(neighborPosition, neighborNode);
                    openList.Enqueue(neighborNode, neighborNode.fScore);
                }
                else if (gScore < neighborNode.gScore)
                {
                    neighborNode.parent = current;
                    neighborNode.gScore = gScore;
                    neighborNode.fScore = fScore;
                }
            }

            count++;
            if (count > 100000)
            {
                Debug.LogWarning("couldnt find path after 100000 cycles");
                break;
            }
        }
        
        return null;// No path found
    }

    public List<Vector3Int> ConstructPath(Node goal)
    {
        List<Vector3Int> path = new List<Vector3Int>();
        Node currentNode = goal;

        while (currentNode != null)
        {
            path.Add(currentNode.position);
            currentNode = currentNode.parent;
        }

        path.Reverse();
        return path;
    }

    public float EuclideanHeuristic(Vector3Int a, Vector3Int b)
    {
        return Vector3Int.Distance(a, b);
    }

    private Vector3 GetChunkIndex(Vector3 _pos)
    {
        return _pos.Remap(
            caveBounds[0], caveBounds[1], Vector3.zero,
            new Vector3(amountChunksHorizontal, amountChunksVertical, amountChunksHorizontal));
    }

    public float GetTerrainValue(Vector3 _worldPos)
    {
        float[] corners = new float[27];
        
        Vector3 chunkIndex = GetChunkIndex(_worldPos);
        CaveChunk chunk = chunks[(int)chunkIndex.x, (int)chunkIndex.y, (int)chunkIndex.z];
        Vector3 subChunkPos = new Vector3((int)_worldPos.x - chunk.position.x, (int)_worldPos.y - chunk.position.y, (int)_worldPos.z - chunk.position.z);
        
        getSDFInfoShader.SetTexture(4, "noiseTex", chunk.noiseTex);
        getSDFInfoShader.SetBuffer(4, "neighbours", neighboursBuffer);
        getSDFInfoShader.SetVector("currentPos", subChunkPos);

        getSDFInfoShader.Dispatch(4, 1, 1, 1);

        neighboursBuffer.GetData(corners);
        
        Vector3 posInsideSubChunk = _worldPos - chunk.position - subChunkPos;
        posInsideSubChunk = new Vector3(Mathf.Abs(posInsideSubChunk.x), Mathf.Abs(posInsideSubChunk.y), Mathf.Abs(posInsideSubChunk.z));
        
        float c00 = corners[0] * (1 - posInsideSubChunk.x) + corners[1] * posInsideSubChunk.x;
        float c01 = corners[2] * (1 - posInsideSubChunk.x) + corners[3] * posInsideSubChunk.x;
        float c10 = corners[4] * (1 - posInsideSubChunk.x) + corners[5] * posInsideSubChunk.x;
        float c11 = corners[6] * (1 - posInsideSubChunk.x) + corners[7] * posInsideSubChunk.x;

        float c0 = c00 * (1 - posInsideSubChunk.y) + c01 * posInsideSubChunk.y;
        float c1 = c10 * (1 - posInsideSubChunk.y) + c11 * posInsideSubChunk.y;

        float result = c0 * (1 - posInsideSubChunk.z) + c1 * posInsideSubChunk.z;
        
        return result;
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
            CaveChunk chunkAdjacent2 = chunks[(int)chunkIndex.x - 1, (int)chunkIndex.y, (int)chunkIndex.z - 1];
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
            CaveChunk chunkAdjacent2 = chunks[(int)chunkIndex.x - 1, (int)chunkIndex.y, (int)chunkIndex.z - 1];
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
            CaveChunk chunkAdjacent2 = chunks[(int)chunkIndex.x - 1, (int)chunkIndex.y, (int)chunkIndex.z + 1];
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
            CaveChunk chunkAdjacent2 = chunks[(int)chunkIndex.x - 1, (int)chunkIndex.y, (int)chunkIndex.z + 1];
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
            CaveChunk chunkAdjacent2 = chunks[(int)chunkIndex.x + 1, (int)chunkIndex.y, (int)chunkIndex.z - 1];
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
            CaveChunk chunkAdjacent2 = chunks[(int)chunkIndex.x + 1, (int)chunkIndex.y, (int)chunkIndex.z - 1];
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
            CaveChunk chunkAdjacent2 = chunks[(int)chunkIndex.x + 1, (int)chunkIndex.y, (int)chunkIndex.z + 1];
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
            CaveChunk chunkAdjacent2 = chunks[(int)chunkIndex.x + 1, (int)chunkIndex.y, (int)chunkIndex.z - 1];
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

}
