using UnityEngine;

public class Sliding : MonoBehaviour
{
    [Header("References")]
    public Transform orientation;
    private Rigidbody rb;
    private Movement pm;
    private CapsuleCollider col;
    private PlayerInputHandler input;

    [Header("Sliding")]
    public float slideForce;
    private Vector3 slideDirection;
    public float slideControlMultiplier = 0.15f;
    public float lowProfileSpeedMultiplier;
    public float slideMomentumLoss = 0.85f;

	[Header("Slide Tuning")]
	public float slideFriction = 6f;
	public float slideSteerStrength = 0.08f;
	public float minSlideSpeed = 2f;
	public float slideBufferTime = 0.2f;
	private float slideBufferTimer;
	public float coyoteTime = 0.15f;
	private float coyoteTimer;
	public float slideStartSpeed = 14f;
	public float slideDrag = 8f;
	public float slideTurnSpeed = 3f;
	private float currentSlideSpeed;

	[Header("Collider Heights")]
    public bool lowProfile;
    public float standHeight = 2f;
    private float originalHeight;
    public float slideHeight = 1.1f;

	[Header("Camera")]
	public Transform cameraHolder;
	public float crouchCameraOffset = 0.5f;
	public float cameraSmoothSpeed = 10f;

	private Vector3 camVelocity;
	private Vector3 camStartLocalPos;
	private Vector3 camTargetLocalPos;


	private void Start()
    {
        rb = GetComponent<Rigidbody>();
        pm = GetComponent<Movement>();
        col = GetComponent<CapsuleCollider>();
        input = GetComponent<PlayerInputHandler>();

		camStartLocalPos = cameraHolder.localPosition;
		camTargetLocalPos = camStartLocalPos;

		standHeight = col.height;
        originalHeight = col.height;
    }

	private void Update()
	{
		Vector2 moveInput = input.MoveInput;
		bool isMoving = moveInput.sqrMagnitude > 0.01f;
		bool canAirSlide = rb.linearVelocity.y < 0.1f;

		if (slideBufferTimer > 0f && (coyoteTimer > 0f || canAirSlide))
		{
			if (isMoving)
				StartSlide(moveInput);
			else
				EnterCrouch();

			slideBufferTimer = 0f; // reset
		}

		if (input.SlideReleased && (pm.sliding || lowProfile) ||!pm.grounded)
			TryStandUp();

		if (lowProfile && isMoving && !input.SlideHold && !IsObstacleAbove())
			ExitCrouch();

		if (input.SlidePress)
			slideBufferTimer = slideBufferTime;
		else
			slideBufferTimer -= Time.deltaTime;

		if (pm.grounded)
			coyoteTimer = coyoteTime;
		else
			coyoteTimer -= Time.deltaTime;

		UpdateCameraPosition();
	}

	private void FixedUpdate()
    {
        if (pm.sliding)
            SlidingMovement();
		LimitCrouchSpeed();
    }

	private void StartSlide(Vector2 moveInput)
	{
		pm.sliding = true;
		lowProfile = false;

		SetColliderHeight(slideHeight);

		// Bierzemy faktyczny kierunek ruchu z velocity
		Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);

		if (flatVel.magnitude < minSlideSpeed)
		{
			// Za wolno – nie zaczynaj ślizgu
			pm.sliding = false;
			return;
		}

		slideDirection = flatVel.normalized;
		currentSlideSpeed = flatVel.magnitude;
	}

	private void SlidingMovement()
	{
		// INPUT – ograniczone skręcanie
		Vector2 moveInput = input.MoveInput;

		if (moveInput.sqrMagnitude > 0.01f)
		{
			Vector3 desiredDir =
				(orientation.forward * moveInput.y +
				 orientation.right * moveInput.x).normalized;

			slideDirection = Vector3.Slerp(
				slideDirection,
				desiredDir,
				slideTurnSpeed * Time.fixedDeltaTime
			);
		}

		bool onSlope = pm.OnSlope();

		if (onSlope)
		{
			Vector3 slopeDownDir = Vector3.ProjectOnPlane(Vector3.down, pm.slopeHit.normal).normalized;

			float slopeDot = Vector3.Dot(slideDirection, slopeDownDir);
			float slopeAngle = Vector3.Angle(Vector3.up, pm.slopeHit.normal);
			float slopeFactor = slopeAngle / pm.maxSlopeAngle;

			if (slopeDot > 0f)
			{
				float downhillForce = slopeDot * slopeFactor * 25f;
				currentSlideSpeed += downhillForce * Time.fixedDeltaTime;
			}
			else if (slopeDot < 0f)
			{
				float uphillDrag = Mathf.Abs(slopeDot) * slopeFactor * slideDrag * 3f;
				currentSlideSpeed -= uphillDrag * Time.fixedDeltaTime;
			}
		}
		else
		{
			// FLAT
			currentSlideSpeed -= slideDrag * Time.fixedDeltaTime;
		}

		currentSlideSpeed = Mathf.Max(currentSlideSpeed, 0f);

		// RUCH
		Vector3 newVelocity = slideDirection * currentSlideSpeed;
		Vector3 finalVelocity;

		if (pm.OnSlope())
		{
			// kierunek po powierzchni
			Vector3 slopeDir = Vector3.ProjectOnPlane(slideDirection, pm.slopeHit.normal).normalized;

			// velocity wzdłuż slope
			finalVelocity = slopeDir * currentSlideSpeed;

			//ZERUJEMY velocity prostopadłą do slope
			rb.linearVelocity = finalVelocity;
		}
		else
		{
			finalVelocity = slideDirection * currentSlideSpeed;

			rb.linearVelocity = new Vector3(
				finalVelocity.x,
				rb.linearVelocity.y,
				finalVelocity.z
			);
		}

		// KONIEC tylko przy minimalnej prędkości
		if (currentSlideSpeed <= minSlideSpeed)
			TryStandUp();
	}

	private void TryStandUp()
    {
        if (IsObstacleAbove())
            EnterCrouch();
        else
            ExitCrouch();
    }

    private bool IsObstacleAbove()
    {
        float distance = originalHeight - col.height + 0.05f;
        Vector3 origin = rb.position + Vector3.up * (col.height / 2f);
        
        RaycastHit[] hits = Physics.SphereCastAll(origin, col.radius * 0.9f, Vector3.up, distance);
        foreach (var hit in hits)
        {
            if (!hit.collider.isTrigger && hit.collider.gameObject != gameObject)
                return true;
        }
        return false;
    }
    
    private void LimitCrouchSpeed()
    {
        if (!lowProfile) return;

        Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        float maxSpeed = slideForce * lowProfileSpeedMultiplier;

        if (horizontalVel.magnitude > maxSpeed)
        {
            horizontalVel = horizontalVel.normalized * maxSpeed;
            rb.linearVelocity = new Vector3(horizontalVel.x, rb.linearVelocity.y, horizontalVel.z);
        }
    }

    private void EnterCrouch()
    {
		if (!pm.grounded) return;
		pm.sliding = false;
        lowProfile = true;

        SetColliderHeight(slideHeight);
    }

    private void ExitCrouch()
    {
        pm.sliding = false;
        lowProfile = false;
        SetColliderHeight(originalHeight);
    }

    private void SetColliderHeight(float newHeight)
    {
        float bottomY = rb.position.y - col.height / 2f;
        col.height = newHeight;
        rb.position = new Vector3(rb.position.x, bottomY + newHeight / 2f, rb.position.z);
    }

	private void UpdateCameraPosition()
	{
		if (lowProfile || pm.sliding)
			camTargetLocalPos = camStartLocalPos - new Vector3(0, crouchCameraOffset, 0);
		else
			camTargetLocalPos = camStartLocalPos;

		cameraHolder.localPosition = Vector3.SmoothDamp(
			cameraHolder.localPosition,
			camTargetLocalPos,
			ref camVelocity,
			0.08f
		);
	}
}