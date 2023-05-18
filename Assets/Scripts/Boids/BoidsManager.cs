using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoidsManager : MonoBehaviour
{
    public CaveVectorField caveVectorField;
    public float rotationSpeed = 1f;
    public float boidSpeed = 1f;
    public float neighbourDistance = 1f;
    public float boidSpeedVariation = 1f;
    public Mesh boidMesh;
    public Material boidMaterial;
    public int boidsCount;
    public float spawnRadius;
    public Transform target;
    public float seperationForce;
    public float alignmentForce;
    public float cohesionForce;
    public float pathFindForce;
    public int floatPrecession;

    private ComputeShader shader;
    ComputeBuffer boidsBuffer;
    ComputeBuffer boidsVelBuffer;
    ComputeBuffer argsBuffer;
    int moveKernel;
    int prepKernel;
    uint[] args = { 0, 0, 0, 0, 0 };
    Boid[] boidsArray;
    int groupSizeXPrepBoids;
    int groupSizeXMoveBoids;
    int numOfBoids;
    Bounds bounds;

    void Start()
    {
        shader = Resources.Load<ComputeShader>("Boids");
        prepKernel = shader.FindKernel("PrepBoids");
        moveKernel = shader.FindKernel("MoveBoids");

        uint x;
        shader.GetKernelThreadGroupSizes(prepKernel, out x, out _, out _);
        groupSizeXPrepBoids = Mathf.CeilToInt((float)boidsCount / (float)x);
        
        shader.GetKernelThreadGroupSizes(moveKernel, out x, out _, out _);
        groupSizeXMoveBoids = Mathf.CeilToInt((float)boidsCount / (float)x);
        numOfBoids = groupSizeXMoveBoids * (int)x;
        
        bounds = new Bounds(Vector3.zero, Vector3.one * 1000);

        InitBoids();
        InitShader();
    }

    private void InitBoids()
    {
        boidsArray = new Boid[numOfBoids];

        for (int i = 0; i < numOfBoids; i++)
        {
            Vector3 pos = transform.position + Random.insideUnitSphere * spawnRadius;
            Quaternion rot = Quaternion.Slerp(transform.rotation, Random.rotation, 0.3f);
            boidsArray[i] = new Boid(pos, rot.eulerAngles);
        }
    }

    void InitShader()
    {
        boidsBuffer = new ComputeBuffer(numOfBoids, 6 * sizeof(float));
        boidsBuffer.SetData(boidsArray);

        //boidsVelBuffer = new ComputeBuffer(numOfBoids, sizeof(int) * 10, ComputeBufferType.Structured);
        boidsVelBuffer = new ComputeBuffer(numOfBoids, sizeof(int) * 7, ComputeBufferType.Structured);
        boidsVelBuffer.SetData(new BoidVel[numOfBoids]);

        argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        if (boidMesh != null)
        {
            args[0] = boidMesh.GetIndexCount(0);
            args[1] = (uint)numOfBoids;
        }
        argsBuffer.SetData(args);

        shader.SetBuffer(moveKernel, "boidsBuffer", boidsBuffer);
        shader.SetBuffer(prepKernel, "boidsBuffer", boidsBuffer);
        shader.SetBuffer(moveKernel, "boidsVelBuffer", boidsVelBuffer);
        shader.SetBuffer(prepKernel, "boidsVelBuffer", boidsVelBuffer);
        shader.SetTexture(moveKernel, "vectorField", caveVectorField.vectorField);
        shader.SetInt("floatPrecession", floatPrecession);
        shader.SetFloat("rotationSpeed", rotationSpeed);
        shader.SetFloat("boidSpeed", boidSpeed);
        shader.SetFloat("boidSpeedVariation", boidSpeedVariation);
        shader.SetVector("flockPosition", target.transform.position);
        shader.SetFloat("neighbourDistance", neighbourDistance);
        shader.SetInt("boidsCount", numOfBoids);

        boidMaterial.SetBuffer("boidsBuffer", boidsBuffer);
    }

    public bool switchBoids;
    void Update()
    {
        if (switchBoids)
        {
            shader.Dispatch(prepKernel, groupSizeXPrepBoids, groupSizeXPrepBoids, 1);

            // BoidVel[] boidVelArray = new BoidVel[numOfBoids];
            // boidsVelBuffer.GetData(boidVelArray);
            // Boid[] boidArray = new Boid[numOfBoids];
            // boidsBuffer.GetData(boidArray);
            
            shader.SetFloat("time", Time.time);
            shader.SetFloat("deltaTime", Time.deltaTime);
            shader.SetFloat("seperationForce", seperationForce);
            shader.SetFloat("alignmentForce", alignmentForce);
            shader.SetFloat("cohesionForce", cohesionForce);
            shader.SetFloat("pathFindForce", pathFindForce);
            shader.SetFloat("rotationSpeed", rotationSpeed);
            shader.SetFloat("boidSpeed", boidSpeed);
            shader.SetFloat("neighbourDistance", neighbourDistance);
            shader.SetVector("flockPosition", target.transform.position);
            shader.SetVector("bottomLeftVectorFieldCorner", caveVectorField.bottomLeftCorner);

            shader.Dispatch(moveKernel, groupSizeXMoveBoids, 1, 1);

            //
            // BoidVel[] boidVelArray2 = new BoidVel[numOfBoids];
            // boidsVelBuffer.GetData(boidVelArray2);
        }
        Graphics.DrawMeshInstancedIndirect(boidMesh, 0, boidMaterial, bounds, argsBuffer);
    }

    void OnDestroy()
    {
        boidsBuffer?.Dispose();
        boidsVelBuffer?.Dispose();
        argsBuffer?.Dispose();
    }
    
    struct Boid
    {
        public Vector3 position;
        public Vector3 direction;

        public Boid(Vector3 pos, Vector3 dir)
        {
            position.x = pos.x;
            position.y = pos.y;
            position.z = pos.z;
            direction.x = dir.x;
            direction.y = dir.y;
            direction.z = dir.z;
        }
    }

    struct BoidVel
    {
        public int alignmentX;
        public int alignmentY;
        public int alignmentZ;
        
        // public int cohesionX;
        // public int cohesionY;
        // public int cohesionZ;
        
        public int separationX;
        public int separationY;
        public int separationZ;

        public int neighbourCount;
    }
}

