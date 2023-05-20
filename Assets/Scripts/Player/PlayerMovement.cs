using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerMovement : MonoBehaviour
{
    [Header("Ground settings")]
    [SerializeField, Range(0f, 100f)]
    float maxSpeed = 10f;
    [SerializeField, Range(0f, 100f)] 
    private float maxAcceleration = 100f, maxAirAcceleration = 50f;
    [SerializeField, Range(0f, 10f)]
    float jumpHeight = 2f;
    [SerializeField, Range(0, 5)]
    int maxAirJumps = 2;
    
    [Header("Physics settings")]
    [SerializeField, Range(0f, 90f)]
    float maxGroundAngle = 25f, maxStairsAngle = 50f;
    [SerializeField, Range(0f, 100f)]
    float maxSnapSpeed = 100f;
    [SerializeField, Min(0f)]
    float probeDistance = 1f;
    [SerializeField]
    LayerMask probeMask = -1, stairsMask = -1, waterMask = 0;

    [Header("Mouse settings")]
    [SerializeField]
    private Transform cameraTransform;
    [SerializeField]
    float mouseSensitivity;

    Vector3 velocity, desiredVelocity, connectionVelocity;
    Vector3 contactNormal, steepNormal;
    Vector3 connectionWorldPosition, connectionLocalPosition;
    private Vector3 oldRot, connectedRot;
    bool desiredJump, waterSuit;
    int groundContactCount, steepContactCount;
    int stepsSinceLastGrounded, stepsSinceLastJump;
    bool OnGround => groundContactCount > 0;
    bool OnSteep => steepContactCount > 0;
    bool InWater => submergence > 0.9f;
    float submergence;
    float xRotation = 0f;
    private float maxSpeedPrev, jumpHeightPrev;
    private int maxAirJumpsPrev;
    int jumpPhase;
    float minGroundDotProduct, minStairsDotProduct;
    Rigidbody body, connectedBody, previousConnectedBody;

    void OnValidate () 
    {
        minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
        minStairsDotProduct = Mathf.Cos(maxStairsAngle * Mathf.Deg2Rad);
    }
    
    void Awake () 
    {
        body = GetComponent<Rigidbody>();
        Cursor.lockState = CursorLockMode.Locked;
        maxSpeedPrev = maxSpeed;
        maxAirJumpsPrev = maxAirJumps;
        jumpHeightPrev = jumpHeight;
        OnValidate();
    }

    void Update () 
    {
        Vector2 playerInput;
        playerInput.x = Input.GetAxis("Horizontal");
        playerInput.y = Input.GetAxis("Vertical");
        playerInput = Vector2.ClampMagnitude(playerInput, 1f);

        var forward = cameraTransform.forward;
        var right = cameraTransform.right;
        desiredVelocity = (playerInput.x * right + playerInput.y * forward) * maxSpeed;
        desiredVelocity = new Vector3(desiredVelocity.x, 0, desiredVelocity.z);
        
        desiredJump |= Input.GetButtonDown("Jump");
    }
    

    
    void FixedUpdate () 
    {
        UpdateState();

        AdjustVelocity();
        
        if (desiredJump && !InWater) 
        {
            desiredJump = false;
            Jump();
        }

        body.velocity = velocity;

        ClearState();
    }

    void UpdateState () 
    {
        stepsSinceLastGrounded += 1;
        stepsSinceLastJump += 1;
        velocity = body.velocity;
        if (OnGround || SnapToGround() || CheckSteepContacts()) 
        {
            stepsSinceLastGrounded = 0;
            if (stepsSinceLastJump > 1) 
            {
                jumpPhase = 0;
            }
            //if someting break put jumphase = 0 here
            if (groundContactCount > 1) 
            {
                contactNormal.Normalize();
            }
        }
        else 
        {
            contactNormal = Vector3.up;
        }
        
        if (connectedBody) 
        {
            if (connectedBody.isKinematic || connectedBody.mass >= body.mass) 
            {
                UpdateConnectionState();
            }
        }
    }
    
    void UpdateConnectionState ()
    {
        connectedRot = connectedBody.transform.eulerAngles;
        connectedRot -= oldRot;
        if (oldRot != Vector3.zero)
        {
            transform.Rotate(connectedRot);
        }
        oldRot = connectedBody.transform.eulerAngles;
        

        if (connectedBody == previousConnectedBody) 
        {
            Vector3 connectionMovement = connectedBody.transform.TransformPoint(connectionLocalPosition) - connectionWorldPosition;
            connectionVelocity = connectionMovement / Time.deltaTime;
        }
        
        connectionWorldPosition = body.position;
        connectionLocalPosition = connectedBody.transform.InverseTransformPoint(connectionWorldPosition);
    }
    
    void ClearState () 
    {
        groundContactCount = steepContactCount = 0;
        contactNormal = steepNormal = connectionVelocity = Vector3.zero;
        previousConnectedBody = connectedBody;
    }
    
    bool SnapToGround () 
    {
        if (stepsSinceLastGrounded > 1 || stepsSinceLastJump <= 2) 
        {
            return false;
        }
        float speed = velocity.magnitude;
        if (speed > maxSnapSpeed) 
        {
            return false;
        }
        if (!Physics.Raycast(body.position, Vector3.down, out RaycastHit hit, probeDistance, probeMask, QueryTriggerInteraction.Ignore)) 
        {
            return false;
        }
        if (hit.normal.y < GetMinDot(hit.collider.gameObject.layer)) 
        {
            return false;
        }
        groundContactCount = 1;
        contactNormal = hit.normal;
        float dot = Vector3.Dot(velocity, hit.normal);
        if (dot > 0f) {
            velocity = (velocity - hit.normal * dot).normalized * speed;
        }
        connectedBody = hit.rigidbody;
        return true;
    }
    
    void Jump () {
        Vector3 jumpDirection;
        if (OnGround) 
        {
            jumpDirection = contactNormal;
        }
        else if (OnSteep) 
        {
            jumpDirection = steepNormal;
            jumpPhase = 0;
        }
        else if (maxAirJumps > 0 && jumpPhase <= maxAirJumps) 
        {
            if (jumpPhase == 0) {
                jumpPhase = 1;
            }
            jumpDirection = contactNormal;
        }
        else {
            return;
        }
        
        jumpPhase += 1;
        float jumpSpeed = Mathf.Sqrt(-2f * Physics.gravity.y * jumpHeight);
        jumpDirection = (jumpDirection + Vector3.up).normalized;
        float alignedSpeed = Vector3.Dot(velocity, jumpDirection);
        
        if (alignedSpeed > 0f) 
        {
            jumpSpeed = Mathf.Max(jumpSpeed - alignedSpeed, 0f);
        }
        
        velocity += jumpDirection * jumpSpeed;
        
    }

    void OnCollisionEnter (Collision collision) 
    {
        EvaluateCollision(collision);
    }

    void OnCollisionStay (Collision collision) 
    {
        EvaluateCollision(collision);
    }

    void EvaluateCollision (Collision collision) 
    {
        float minDot = GetMinDot(collision.gameObject.layer);
        for (int i = 0; i < collision.contactCount; i++) 
        {
            Vector3 normal = collision.GetContact(i).normal;
            if (normal.y >= minDot) 
            {
                groundContactCount += 1;
                contactNormal += normal;
                connectedBody = collision.rigidbody;
            }
            else if (normal.y > -0.01f) 
            {
                steepContactCount += 1;
                steepNormal += normal;
                if (groundContactCount == 0) {
                    connectedBody = collision.rigidbody;
                }
            }
        }
    }
    
    Vector3 ProjectOnContactPlane (Vector3 vector) 
    {
        return vector - contactNormal * Vector3.Dot(vector, contactNormal);
    }
    
    void AdjustVelocity () 
    {
        Vector3 xAxis = ProjectOnContactPlane(Vector3.right).normalized;
        Vector3 zAxis = ProjectOnContactPlane(Vector3.forward).normalized;
        
        Vector3 relativeVelocity = velocity - connectionVelocity;
        float currentX = Vector3.Dot(relativeVelocity, xAxis);
        float currentZ = Vector3.Dot(relativeVelocity, zAxis);
        
        float acceleration = OnGround ? maxAcceleration : maxAirAcceleration;
        float maxSpeedChange = acceleration * Time.deltaTime;

        float newX =
            Mathf.MoveTowards(currentX, desiredVelocity.x, maxSpeedChange);
        float newZ =
            Mathf.MoveTowards(currentZ, desiredVelocity.z, maxSpeedChange);
        
        velocity += xAxis * (newX - currentX) + zAxis * (newZ - currentZ);
    }
    
    float GetMinDot (int layer) 
    {
       return (stairsMask & (1 << layer)) == 0 ? minGroundDotProduct : minStairsDotProduct;
    }
    
    bool CheckSteepContacts () 
    {
        if (steepContactCount > 1) 
        {
            steepNormal.Normalize();
            if (steepNormal.y >= minGroundDotProduct) 
            {
                groundContactCount = 1;
                contactNormal = steepNormal;
                return true;
            }
        }
        return false;
    }
}
