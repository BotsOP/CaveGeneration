using System;
using System.Collections.Generic;
using UnityEngine;

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

    public List<Vector3Int> AStarPathfinding(Vector3 _start, Vector3 _goal)
    {
        Vector3Int start = new Vector3Int((int)_start.x, (int)_start.y, (int)_start.z);
        Vector3Int goal = new Vector3Int((int)_goal.x, (int)_goal.y, (int)_goal.z);

        List<Node> openList = new List<Node>();
        HashSet<Vector3Int> closedList = new HashSet<Vector3Int>();
        Dictionary<Vector3Int, Node> nodeLookup = new Dictionary<Vector3Int, Node>();

        Node startNode = new Node(start, null, 0, EuclideanHeuristic(start, goal));
        openList.Add(startNode);
        nodeLookup.Add(start, startNode);

        int count = 0;
        
        while (openList.Count > 0)
        {
            Node current = GetNodeWithLowestFScore(openList);
            openList.Remove(current);

            if (current.position == goal)
            {
                return ConstructPath(current);
            }

            closedList.Add(current.position);

            float[] neighbours = GetNeighbours(current.position);

            for (int i = 0; i < 27; i++)
            {
                //0 is wall and 1 is air
                float isAir = neighbours[i];

                int newX = i % 3 - 1;
                int newY = i / 9 - 1;
                int newZ = i % 9 / 3 - 1;

                Vector3Int neighborPosition = current.position + new Vector3Int(newX, newY, newZ);
                
                if (closedList.Contains(neighborPosition))
                {
                    continue;
                }
                if (isAir < 0.5f)
                {
                    continue;
                }

                float gScore = current.gScore + Vector3Int.Distance(current.position, neighborPosition);
                float hScore = EuclideanHeuristic(neighborPosition, goal);
                float fScore = gScore + hScore;

                if (!nodeLookup.TryGetValue(neighborPosition, out Node neighborNode))
                {
                    neighborNode = new Node(neighborPosition, current, gScore, hScore);
                    nodeLookup.Add(neighborPosition, neighborNode);
                    openList.Add(neighborNode);
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
                break;
            }
        }

        return null;// No path found
    }

    public Node GetNodeWithLowestFScore(List<Node> nodeList)
    {
        Node lowestNode = null;
        float lowestFScore = float.MaxValue;

        foreach (Node node in nodeList)
        {
            if (node.fScore < lowestFScore)
            {
                lowestNode = node;
                lowestFScore = node.fScore;
            }
        }

        return lowestNode;
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

        int kernelID;
        
        if (!xTooLeft && !xTooRight && !zTooBackward && !zTooForward && !yTooDownward && !yTooUpward)
        {
            kernelID = getSDFInfoShader.FindKernel("GetNeighbours1");
            getSDFInfoShader.SetTexture(kernelID, "noiseTex", chunk.noiseTex);
            getSDFInfoShader.SetBuffer(kernelID, "neighbours", neighboursBuffer);
            getSDFInfoShader.SetVector("currentPos", subChunkPos);
            getSDFInfoShader.SetInt("chunkSize", chunkSize);

            getSDFInfoShader.Dispatch(kernelID, 32, 1, 1);

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
