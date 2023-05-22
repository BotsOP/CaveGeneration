using Managers;
using UnityEngine;
using EventType = Managers.EventType;

public class PlayerPhysics : MonoBehaviour
{
	[SerializeField] private Transform camTransform;
	[SerializeField, Range(0f, 100f)]
	float maxSpeed = 10f;
	[SerializeField, Range(0f, 1000f)]
	float maxAcceleration = 10f, maxAirAcceleration = 1f;
	[SerializeField, Range(0f, 50f)] private float jumpHeight = 2f;
	[SerializeField] private float collisionSolverMultiplier = 1;
	[SerializeField, Range(0, 5)]
	int maxAirJumps = 0;
	[SerializeField, Range(0, 90)]
	float maxGroundAngle = 25f, maxStairsAngle = 50f;
	[SerializeField, Range(0f, 100f)]
	float maxSnapSpeed = 100f;
	[SerializeField, Min(0f)]
	float probeDistance = 1f;
	
	Rigidbody body;
	Vector3 velocity, desiredVelocity;
	bool desiredJump;
	Vector3 contactNormal, steepNormal;
	int groundContactCount, steepContactCount;
	bool OnGround => groundContactCount > 0;
	bool OnSteep => steepContactCount > 0;
	int jumpPhase;
	float minGroundDotProduct, minStairsDotProduct;
	int stepsSinceLastGrounded, stepsSinceLastJump;
	private bool nextFrameReset;

	void OnValidate () {
		minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
		minStairsDotProduct = Mathf.Cos(maxStairsAngle * Mathf.Deg2Rad);
	}

	void Awake () {
		body = GetComponent<Rigidbody>();
		OnValidate();
	}

	void Update () {
		Vector2 playerInput;
		playerInput.x = Input.GetAxis("Horizontal");
		playerInput.y = Input.GetAxis("Vertical");
		playerInput = Vector2.ClampMagnitude(playerInput, 1f);

		var forward = camTransform.forward;
		var right = camTransform.right;
		desiredVelocity = (playerInput.x * right + playerInput.y * forward) * maxSpeed;
		desiredVelocity = new Vector3(desiredVelocity.x, 0, desiredVelocity.z);

		desiredJump |= Input.GetButtonDown("Jump");
	}

	void FixedUpdate () {

		UpdateState();
		AdjustVelocity();
		
		SolveCollisions();

		if (desiredJump) 
		{
			desiredJump = false;
			Jump();
		}

		body.velocity = velocity;
		ClearState();
	}
	private void SolveCollisions()
	{
		int counter = 0;
		while (CavePhysicsManager.instance.Sphere(transform.position, transform.lossyScale.x / 2, out RayOutput closestPoint) && counter < 10)
		{
			Vector3 pos = closestPoint.position;
			Vector3 normal = closestPoint.normal;

			float magnitude = transform.lossyScale.x / 2 - Vector3.Distance(transform.position, pos);
			Vector3 dir = (transform.position - pos).normalized;
			transform.Translate(dir * (magnitude * collisionSolverMultiplier));
			velocity += dir * magnitude;

			if (normal.y >= minGroundDotProduct)
			{
				groundContactCount += 1;
				contactNormal += normal;
			}
			else if (normal.y > -0.01f)
			{
				steepContactCount += 1;
				steepNormal += normal;
			}

			counter++;
		}
	}

	void ClearState () {
		groundContactCount = steepContactCount = 0;
		contactNormal = steepNormal = Vector3.zero;
	}

	void UpdateState () {
		stepsSinceLastGrounded += 1;
		stepsSinceLastJump += 1;
		velocity = body.velocity;
		if (OnGround || SnapToGround() || CheckSteepContacts()) {
			stepsSinceLastGrounded = 0;
			if (stepsSinceLastJump > 1) {
				jumpPhase = 0;
			}
			if (groundContactCount > 1) {
				contactNormal.Normalize();
			}
		}
		else {
			contactNormal = Vector3.up;
		}
	}

	bool SnapToGround () {
		if (stepsSinceLastGrounded > 1 || stepsSinceLastJump <= 2) {
			return false;
		}
		float speed = velocity.magnitude;
		if (speed > maxSnapSpeed) {
			return false;
		}
		if (!CavePhysicsManager.instance.Raycast(
			body.position, Vector3.down * probeDistance, out RayOutput hit)) {
			return false;
		}
		if (hit.normal.y < minGroundDotProduct) {
			return false;
		}

		groundContactCount = 1;
		contactNormal = hit.normal;
		float dot = Vector3.Dot(velocity, hit.normal);
		if (dot > 0f) {
			velocity = (velocity - Vector3.up * dot).normalized * speed;
		}
		return true;
	}

	bool CheckSteepContacts () {
		if (steepContactCount > 1) {
			steepNormal.Normalize();
			if (steepNormal.y >= minGroundDotProduct) {
				steepContactCount = 0;
				groundContactCount = 1;
				contactNormal = steepNormal;
				return true;
			}
		}
		return false;
	}

	void AdjustVelocity () {
		Vector3 xAxis = ProjectOnContactPlane(Vector3.right).normalized;
		Vector3 zAxis = ProjectOnContactPlane(Vector3.forward).normalized;

		float currentX = Vector3.Dot(velocity, xAxis);
		float currentZ = Vector3.Dot(velocity, zAxis);

		float acceleration = OnGround ? maxAcceleration : maxAirAcceleration;
		float maxSpeedChange = acceleration * Time.deltaTime;

		float newX =
			Mathf.MoveTowards(currentX, desiredVelocity.x, maxSpeedChange);
		float newZ =
			Mathf.MoveTowards(currentZ, desiredVelocity.z, maxSpeedChange);

		velocity += xAxis * (newX - currentX) + zAxis * (newZ - currentZ);
	}

	void Jump () {
		Vector3 jumpDirection;
		if (OnGround) {
			jumpDirection = contactNormal;
		}
		else if (OnSteep) {
			jumpDirection = steepNormal;
			jumpPhase = 0;
		}
		else if (maxAirJumps > 0 && jumpPhase <= maxAirJumps) {
			if (jumpPhase == 0) {
				jumpPhase = 1;
			}
			jumpDirection = contactNormal;
		}
		else {
			return;
		}

		stepsSinceLastJump = 0;
		jumpPhase += 1;
		float jumpSpeed = Mathf.Sqrt(-2f * Physics.gravity.y * jumpHeight);
		jumpDirection = (jumpDirection + Vector3.up).normalized;
		float alignedSpeed = Vector3.Dot(velocity, jumpDirection);
		if (alignedSpeed > 0f) {
			jumpSpeed = Mathf.Max(jumpSpeed - alignedSpeed, 0f);
		}
		velocity += jumpDirection * jumpSpeed;
	}

	Vector3 ProjectOnContactPlane (Vector3 vector) {
		return vector - contactNormal * Vector3.Dot(vector, contactNormal);
	}
}

