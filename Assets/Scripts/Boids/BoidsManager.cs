using System.Collections;
using System.Collections.Generic;
using Managers;
using UnityEngine;
using UnityEngine.VFX;
using EventType = Managers.EventType;

public class BoidsManager : MonoBehaviour
{
    [Header("Settings")]
    [Range(0, 10)] public float rotationSpeed = 1f;
    [Range(0, 10)] public float boidSpeed = 1f;
    [Range(0, 2)] public float boidRadius = 1f;
    [Range(0, 1)] public float boidDamge = 0.1f;
    [Range(0, 1)] public float gunDamage = 0.1f;
    [Range(0, 100)] public float gunRange = 10f;
    
    [Header("Forces")]
    [Range(0, 3)] public float seperationForce;
    [Range(0, 3)]public float alignmentForce;
    [Range(0, 3)]public float pathFindForce;
    
    [Header("Boid Visual")]
    public Mesh boidMesh;
    public Material boidMaterial;

    [Header("Dev settings")]
    public VisualEffect gunVFX;
    public Transform camTransform;
    public CaveVectorField caveVectorField;
    [Range(0, 3)] public float neighbourDistance = 1f;
    public int boidsCount;
    public float spawnRadius;
    public int floatPrecession;

    private ComputeShader boidShader;
    private ComputeBuffer boidsBuffer;
    private ComputeBuffer boidsVelBuffer;
    private ComputeBuffer playerHitsBuffer;
    private ComputeBuffer boidsHitsBuffer;
    private ComputeBuffer argsBuffer;
    private GraphicsBuffer boidsBeingHitBuffer;
    private GraphicsBuffer boidsBeingHitBufferStructured;
    private GraphicsBuffer amountBoidsBeingHitBuffer;
    private int prepKernel;
    private int moveKernel;
    private int moveAndRaycastKernel;
    private uint[] args = { 0, 0, 0, 0, 0 };
    private Boid[] boidsArray;
    private int groupSizeXPrepBoids;
    private int groupSizeXMoveBoids;
    private int numOfBoids;
    private Bounds bounds;

    void Start()
    {
        boidShader = Resources.Load<ComputeShader>("Boids");
        prepKernel = boidShader.FindKernel("PrepBoids");
        moveKernel = boidShader.FindKernel("MoveBoids");
        moveAndRaycastKernel = boidShader.FindKernel("MoveBoidsAndRaycast");

        uint x;
        boidShader.GetKernelThreadGroupSizes(prepKernel, out x, out _, out _);
        groupSizeXPrepBoids = Mathf.CeilToInt((float)boidsCount / (float)x);
        
        boidShader.GetKernelThreadGroupSizes(moveKernel, out x, out _, out _);
        groupSizeXMoveBoids = Mathf.CeilToInt((float)boidsCount / (float)x);
        numOfBoids = groupSizeXMoveBoids * (int)x;
        
        bounds = new Bounds(Vector3.zero, Vector3.one * 1000);

        InitBoids();
        InitShader();
    }

    private void InitBoids()
    {
        boidsArray = new Boid[numOfBoids];
        float spawnAreaWidth = caveVectorField.chunkSize * 3;

        for (int i = 0; i < numOfBoids; i++)
        {
            Vector3 pos = GetBoidSpawnPos() - new Vector3(spawnAreaWidth / 2, 0 , spawnAreaWidth / 2);
            Quaternion rot = Quaternion.Slerp(transform.rotation, Random.rotation, 0.3f);
            boidsArray[i] = new Boid(pos, rot.eulerAngles, 1);
        }
    }

    private Vector3 GetBoidSpawnPos()
    {
        Vector3 respawnPos;
        int chunkSize = caveVectorField.chunkSize;
        float respawnWidth = 10;
        float randX = Random.Range(0f, 1f);
        float randY = Random.Range(0f, 1f);
        float randZ = Random.Range(0f, 1f);
        
        respawnPos.y = randY * (chunkSize * 2 - respawnWidth * 2) + respawnWidth;

        if(Random.Range(0f, 1f) < 0.5)
        {
            respawnPos.x = randX * chunkSize * 3;
            if(respawnPos.x > respawnWidth && respawnPos.x < chunkSize * 3 - respawnWidth)
            {
                float extraZPos = (int)(randZ * 2) * (chunkSize * 3 - respawnWidth);
                respawnPos.z = randZ * respawnWidth + extraZPos;
                return respawnPos;
            }
            respawnPos.z = randZ * chunkSize * 3;
            return respawnPos;
        }
	
        respawnPos.z = randZ * chunkSize * 3;
        if(respawnPos.z > respawnWidth && respawnPos.z < chunkSize * 3 - respawnWidth)
        {
            float extraZPos = (int)(randX * 2) * (chunkSize * 3 - respawnWidth);
            respawnPos.x = randX * respawnWidth + extraZPos;
            return respawnPos;
        }
        respawnPos.x = randX * chunkSize * 3;
        return respawnPos;
    }

    void InitShader()
    {
        boidsBuffer = new ComputeBuffer(numOfBoids, 7 * sizeof(float));
        boidsBuffer.SetData(boidsArray);

        boidsVelBuffer = new ComputeBuffer(numOfBoids, sizeof(int) * 4, ComputeBufferType.Structured);
        boidsVelBuffer.SetData(new BoidVel[numOfBoids]);

        playerHitsBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Structured);
        boidsHitsBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Structured);

        argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);

        boidsBeingHitBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Append, 1000, sizeof(float) * 3);
        boidsBeingHitBufferStructured = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1000, sizeof(float) * 3);
        amountBoidsBeingHitBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, sizeof(int));
        
        gunVFX.SetGraphicsBuffer("BoidsHit", boidsBeingHitBufferStructured);
        
        if (boidMesh != null)
        {
            args[0] = boidMesh.GetIndexCount(0);
            args[1] = (uint)numOfBoids;
        }
        argsBuffer.SetData(args);

        boidShader.SetBuffer(moveKernel, "boidsBuffer", boidsBuffer);
        boidShader.SetBuffer(prepKernel, "boidsBuffer", boidsBuffer);
        boidShader.SetBuffer(moveAndRaycastKernel, "boidsBuffer", boidsBuffer);
        boidShader.SetBuffer(moveKernel, "boidsVelBuffer", boidsVelBuffer);
        boidShader.SetBuffer(prepKernel, "boidsVelBuffer", boidsVelBuffer);
        boidShader.SetBuffer(moveAndRaycastKernel, "boidsVelBuffer", boidsVelBuffer);
        boidShader.SetBuffer(moveAndRaycastKernel, "playerHitBuffer", playerHitsBuffer);
        boidShader.SetBuffer(moveKernel, "boidsHitBuffer", boidsHitsBuffer);
        boidShader.SetBuffer(moveAndRaycastKernel, "boidsHitBuffer", boidsHitsBuffer);
        boidShader.SetBuffer(moveAndRaycastKernel, "boidsBeingHitBuffer", boidsBeingHitBuffer);
        boidShader.SetBuffer(moveAndRaycastKernel, "amountBoidsBeingHitBuffer", amountBoidsBeingHitBuffer);
        boidShader.SetBuffer(3, "boidsBeingHitStruc", boidsBeingHitBufferStructured);
        boidShader.SetBuffer(3, "boidsBeingHitCons", boidsBeingHitBuffer);
        
        boidShader.SetTexture(moveKernel, "vectorField", caveVectorField.vectorField);
        boidShader.SetTexture(moveAndRaycastKernel, "vectorField", caveVectorField.vectorField);
        
        boidShader.SetInt("floatPrecession", floatPrecession);
        boidShader.SetFloat("rotationSpeed", rotationSpeed);
        boidShader.SetFloat("boidSpeed", boidSpeed);
        //shader.SetFloat("boidSpeedVariation", boidSpeedVariation);
        boidShader.SetFloat("neighbourDistance", neighbourDistance);
        boidShader.SetInt("boidsCount", numOfBoids);

        boidMaterial.SetBuffer("boidsBuffer", boidsBuffer);
    }

    public bool switchBoids;
    void Update()
    {
        if (switchBoids)
        {
            boidShader.Dispatch(prepKernel, groupSizeXPrepBoids, groupSizeXPrepBoids, 1);
            
            float actualGunRange = gunRange;
            if (CavePhysicsManager.instance.Raycast(camTransform.position, camTransform.forward * gunRange, out RayOutput rayOutput))
            {
                float distanceToWall = Vector3.Distance(camTransform.position, rayOutput.position);
                actualGunRange = Mathf.Min(actualGunRange, distanceToWall);
            }
            
            boidShader.SetTexture(moveKernel, "vectorField", caveVectorField.vectorField);
            boidShader.SetTexture(moveAndRaycastKernel, "vectorField", caveVectorField.vectorField);
            boidShader.SetFloat("time", Time.time);
            boidShader.SetFloat("deltaTime", Time.deltaTime);
            boidShader.SetFloat("seperationForce", seperationForce);
            boidShader.SetFloat("alignmentForce", alignmentForce);
            boidShader.SetFloat("pathFindForce", pathFindForce);
            boidShader.SetFloat("rotationSpeed", rotationSpeed);
            boidShader.SetFloat("boidSpeed", boidSpeed);
            boidShader.SetFloat("neighbourDistance", neighbourDistance);
            boidShader.SetVector("playerPos", caveVectorField.player.position);
            boidShader.SetFloat("respawnWidth", 10);
            boidShader.SetFloat("gunDamage", gunDamage);
            boidShader.SetFloat("gunRange", actualGunRange);
            boidShader.SetInt("chunkSize", caveVectorField.chunkSize);
            boidShader.SetVector("bottomLeftVectorFieldCorner", caveVectorField.bottomLeftCorner);

            if (Input.GetKeyDown(KeyCode.F))
            {
                Debug.Break();
            }

            if (Input.GetMouseButton(1))
            {
                boidsBeingHitBuffer.SetCounterValue(0);
                
                boidShader.SetVector("rayOrigin", camTransform.position);
                boidShader.SetVector("rayDirection", camTransform.forward);
                boidShader.SetFloat("boidRadius", boidRadius);
                boidShader.Dispatch(moveAndRaycastKernel, groupSizeXMoveBoids, 1, 1);

                int amountBoidsHit = amountBoidsBeingHitBuffer.GetCounter();
                gunVFX.SetInt("AmountBoidsHit", amountBoidsHit);

                if (amountBoidsHit > 0 && amountBoidsHit < boidsCount)
                {
                    boidShader.Dispatch(3, Mathf.CeilToInt(amountBoidsHit / 128f), 1, 1);
                    Debug.Log(amountBoidsHit);
                }
            }
            else
            {
                boidShader.Dispatch(moveKernel, groupSizeXMoveBoids, 1, 1);
                gunVFX.SetInt("AmountBoidsHit", 0);
            }

            int amountPlayerHits = playerHitsBuffer.GetCounter();
            int amountBoidsHits = boidsHitsBuffer.GetCounter();
            
            if (amountPlayerHits > 0)
            {
                EventSystem<int>.RaiseEvent(EventType.UPDATE_SCORE, amountPlayerHits * 10);
            }
            if (amountBoidsHits > 0)
            {
                EventSystem<float>.RaiseEvent(EventType.UPDATE_HEALTH, amountBoidsHits * boidDamge);
            }
        }
        Graphics.DrawMeshInstancedIndirect(boidMesh, 0, boidMaterial, bounds, argsBuffer);
    }

    void OnDestroy()
    {
        boidsBuffer?.Dispose();
        argsBuffer?.Dispose();
        playerHitsBuffer?.Dispose();
        boidsHitsBuffer?.Dispose();
    }
    
    struct Boid
    {
        public Vector3 position;
        public Vector3 direction;
        public float health;

        public Boid(Vector3 pos, Vector3 dir, float _health)
        {
            position.x = pos.x;
            position.y = pos.y;
            position.z = pos.z;
            direction.x = dir.x;
            direction.y = dir.y;
            direction.z = dir.z;
            health = _health;
        }
    }

    struct BoidVel
    {
        // public int alignmentX;
        // public int alignmentY;
        // public int alignmentZ;
        
        // public int cohesionX;
        // public int cohesionY;
        // public int cohesionZ;
        
        public int separationX;
        public int separationY;
        public int separationZ;

        public int neighbourCount;
    }
}

