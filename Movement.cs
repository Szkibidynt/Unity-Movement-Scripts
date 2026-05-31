using System.Collections;
using UnityEngine;

public class Movement : MonoBehaviour
{
    [Header("Movement")]
    private float horizontalInput;
    private float verticalInput;
    public float MoveSpeed => moveSpeed;
    private float moveSpeed;
    private float walkSpeed = 5f;
    private float maxSpeed = 15f;
	public float groundDrag = 5f;
    public float jumpForce = 8f;
    private float jumpCooldown = 0.15f;
    private float jumpCooldownTimer;
    public float dbljumpMult = 1.2f;
    private bool readyToJump = true;
    private float airMultiplier =.5f;
    public bool wallRunning;
    public bool dbljump;
    public bool restricted;
    public bool freeze;
    public bool dblJumpUnlocked;
	private Coroutine speedLerpRoutine;

	[Header("Slope Handling")]
    public float maxSlopeAngle = 100f;
    public RaycastHit slopeHit;
    public float slideSpeed = 5f;
    private float desiredMoveSpeed;
    private float lastDesiredMoveSpeed;
    public bool sliding;

    [Header("Vaulting")]
    private Vector3 lastMoveDir;
    public float vaultHeight = 1.5f;

	[Header("Ground Check")]
    public float playerHeight = 2f;
    public LayerMask whatIsGround;
    public bool grounded;

    [Header("Jump Buffer / Coyote")]
    public float coyoteTime = 0.15f;
    private float coyoteCounter;

	private float boostLockTimer;
	private float boostLockTime = .3f;

	[Header("Clutch Ledge")]
	public float clutchLookAhead = 2f;          // jak daleko przed graczem sprawdzamy
	public float clutchTopCheckHeight = 1.4f;     // z jakiej wysokości szukamy topu
	public float clutchAssistUp = 4f;           // jak mocno pomaga w pionie
	public float clutchAssistDuration = 0.12f;    // jak długo pomaga

	public float clutchMinMissHeight = 0.05f;     // minimalny "brak" wysokości
	public float clutchMaxMissHeight = 0.5f;     // maksymalny "brak" wysokości
	public float clutchMaxHorizontalOffset = 0.45f;
	private bool isClutching;
	public float clutchDuration = 0.12f;//aaaa

	[Header("Do przypisywania")]
    public Transform orientation;
    public PlayerInputHandler input;
    private Vector3 moveDirection;
    public Rigidbody rb;
    public MovementState state;
    public enum MovementState
    {
        walking,
        air,
        sliding,
        wallrunning,
        restricted,
        vaulting,
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        readyToJump = true;
        dbljump = true;
		rb.useGravity = true;
		moveSpeed = walkSpeed;
        state = MovementState.walking;

        input = GetComponent<PlayerInputHandler>();
    }

    private void Update()
    {
		grounded = Physics.SphereCast(
		transform.position + Vector3.up * 0.1f,
		0.3f,
		Vector3.down,
		out _,
		playerHeight * 0.5f + 0.25f,
		whatIsGround
		);

		Inputs();
        SpeedControl();
        StateHandler();

        rb.linearDamping = grounded ? groundDrag : 0;

        if (jumpCooldownTimer>0)
            jumpCooldownTimer -= Time.deltaTime;

        Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

        if (flatVel.magnitude > 0.1f)
            lastMoveDir = flatVel.normalized;

        if (grounded && rb.linearVelocity.y <= 0.02f)
        {
            coyoteCounter = coyoteTime;
            readyToJump = true;
        }

        else
            coyoteCounter -= Time.deltaTime;

        if (dbljump == false && grounded)
            dbljump = true;

		if (!grounded && state == MovementState.air && !isClutching)
			TryClutchLedge();
	}

    private void FixedUpdate()
    {
        MovePlayer();
		if (grounded && OnSlope() && !sliding)
		{
			bool noInput = horizontalInput == 0 && verticalInput == 0;

			if (noInput)
			{
				Vector3 slopeGravity = Vector3.ProjectOnPlane(Physics.gravity, slopeHit.normal);
				rb.AddForce(-slopeGravity, ForceMode.Acceleration);
			}
		}
	}

    private void Inputs()
    {
        horizontalInput = input.MoveInput.x;
        verticalInput = input.MoveInput.y;

        if (restricted || freeze) return;
        if (state == MovementState.vaulting) return;
        if (input.JumpPressed && jumpCooldownTimer <= 0f)
        {
            if (readyToJump && coyoteCounter > 0f)
            {
                readyToJump = false;
                coyoteCounter = 0f;
                print("jump");
                Jump();
                return;
            }
            else if (dbljump && state == MovementState.air && dblJumpUnlocked)
            {
                dbljump = false;
                Jump(dbljumpMult);
            }

        }
    }

	private void MovePlayer()
	{
		if (restricted || freeze || sliding || state == MovementState.vaulting) return;
		if (boostLockTimer > 0)
		{
			boostLockTimer -= Time.fixedDeltaTime;
			return;
		}

		moveDirection = orientation.forward * verticalInput + orientation.right * horizontalInput;
		moveDirection.Normalize();

		if (grounded && OnSlope())
		{
			Vector3 slopeMoveDir = Vector3.ProjectOnPlane(moveDirection, slopeHit.normal).normalized;
			rb.AddForce(slopeMoveDir * moveSpeed * 10f, ForceMode.Force);
		}
		else if (grounded)
		{
			rb.AddForce(moveDirection * moveSpeed * 10f, ForceMode.Force);
		}
		else
		{
			Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

			float currentSpeed = flatVel.magnitude;
			float maxAirSpeed = maxSpeed * 0.6f;

			float controlFactor = 1f;

			if (currentSpeed > maxAirSpeed)
				controlFactor = 0.2f;

			Vector3 targetVel = moveDirection * maxAirSpeed;
			Vector3 velocityChange = targetVel - flatVel;
			velocityChange = Vector3.ClampMagnitude(velocityChange, airMultiplier);

			velocityChange = Vector3.ClampMagnitude(
				velocityChange,
				airMultiplier * controlFactor
			);


			rb.AddForce(velocityChange, ForceMode.VelocityChange);
		}
	}

	private void SpeedControl()
	{
		if (sliding) return;

		float decelerationRate = 3.5f; // 🔥 kontrola jak szybko traci speed

		if (OnSlope())
		{
			Vector3 slopeVel = Vector3.ProjectOnPlane(rb.linearVelocity, slopeHit.normal);
			float speed = slopeVel.magnitude;

			if (speed > maxSpeed)
			{
				float excess = 0.5f * (speed - maxSpeed);

				float reduce = decelerationRate * Time.deltaTime;
				float newSpeed = speed - Mathf.Min(excess, reduce);

				Vector3 newVel = slopeVel.normalized * newSpeed;
				Vector3 normalVel = Vector3.Project(rb.linearVelocity, slopeHit.normal);

				rb.linearVelocity = newVel + normalVel;
			}
		}
		else
		{
			Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
			float speed = flatVel.magnitude;

			if (speed > maxSpeed)
			{
				float excess = 0.5f * (speed - maxSpeed);

				float reduce = decelerationRate * Time.deltaTime;
				float newSpeed = speed - Mathf.Min(excess, reduce);

				Vector3 newFlat = flatVel.normalized * newSpeed;

				rb.linearVelocity = new Vector3(
					newFlat.x,
					rb.linearVelocity.y,
					newFlat.z
				);
			}
		}
	}

	private void Jump(float multiplier = 1f)
	{
		if (restricted || freeze) return;

		jumpCooldownTimer = jumpCooldown;
		grounded = false;
		readyToJump = false;
		rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

		rb.AddForce(transform.up * jumpForce * multiplier, ForceMode.Impulse);
	}

	public bool OnSlope()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out slopeHit, playerHeight * 0.5f + 0.5f))
        {
            float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
            return angle > 0f && angle <= maxSlopeAngle;
        }
        return false;
    }

    public Vector3 GetSlopeMoveDirection(Vector3 direction)
    {
        return Vector3.ProjectOnPlane(direction, slopeHit.normal).normalized;
    }

    private void StateHandler()
    {
        if (state == MovementState.vaulting) return;
        if (restricted || freeze) return;
        else if (wallRunning)
        {
            state = MovementState.wallrunning;
        }
        else if (sliding)
        {
            state = MovementState.sliding;
            if (OnSlope() && rb.linearVelocity.y < 0.1f)
                desiredMoveSpeed = slideSpeed;
            else
                desiredMoveSpeed = moveSpeed;
        }
        else if (grounded)
        {
            state = MovementState.walking;
            desiredMoveSpeed = walkSpeed;
        }
        else
            state = MovementState.air;

		if (Mathf.Abs(desiredMoveSpeed - lastDesiredMoveSpeed) > 4 && moveSpeed != 0)
		{
			if (speedLerpRoutine != null)
				StopCoroutine(speedLerpRoutine);

			speedLerpRoutine = StartCoroutine(SmoothlyLerpMoveSpeed());
		}
		else
			moveSpeed = desiredMoveSpeed;

		lastDesiredMoveSpeed = desiredMoveSpeed;
    }
    private IEnumerator SmoothlyLerpMoveSpeed()
    {
        float time = 0f;
        float duration = .12f; // czas przejścia, możesz ustawić np. 0.25f

        float startValue = moveSpeed;

        while (time < duration)
        {
            moveSpeed = Mathf.Lerp(startValue, desiredMoveSpeed, time / duration);
            time += Time.deltaTime;
            yield return null;
        }

        moveSpeed = desiredMoveSpeed;
    }

	void TryClutchLedge()
	{
		if (grounded) return;
		if (state != MovementState.air) return;
		if (isClutching) return;
		if (rb.linearVelocity.y > 0.2f) return;
		if (rb.linearVelocity.y < -10f) return;

		Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
		float speed = flatVel.magnitude;
		if (speed < 2f) return;

		Vector3 dir = flatVel.normalized;

		float checkDistance = clutchLookAhead + speed * Time.fixedDeltaTime * 1.5f;
		float sphereRadius = 0.25f;  // dopasowany do collidera gracza

		Vector3 start = transform.position + Vector3.up * 0.1f + dir * sphereRadius;

		if (!Physics.SphereCast(start, sphereRadius, dir, out RaycastHit wallHit, checkDistance, whatIsGround))
			return;

		if (Vector3.Angle(Vector3.up, wallHit.normal) < 45f) return;

		Vector3 topCheckOrigin = wallHit.point + dir * 0.1f + Vector3.up * clutchTopCheckHeight;
		if (!Physics.Raycast(topCheckOrigin, Vector3.down, out RaycastHit topHit, clutchTopCheckHeight + 1.5f, whatIsGround))
			return;

		Vector3 bodyCheckOrigin = topHit.point + Vector3.up * (playerHeight * 0.5f + 0.05f);
		if (Physics.CheckCapsule(
			bodyCheckOrigin + Vector3.up * 0.2f,
			bodyCheckOrigin + Vector3.up * (playerHeight - 0.2f),
			0.28f,
			whatIsGround))
			return;

		float playerFeetY = transform.position.y - playerHeight * 0.5f;
		float missingHeight = topHit.point.y - playerFeetY;
		if (missingHeight < clutchMinMissHeight || missingHeight > clutchMaxMissHeight) return;

		Vector3 flatOffset = new Vector3(topHit.point.x - transform.position.x, 0f, topHit.point.z - transform.position.z);
		if (Vector3.ProjectOnPlane(flatOffset, dir).magnitude > clutchMaxHorizontalOffset) return;

		StartCoroutine(ClutchRoutine(topHit.point.y));
	}

	IEnumerator ClutchRoutine(float targetTopY)
	{
		isClutching = true;

		float elapsed = 0f;

		while (elapsed < clutchAssistDuration)
		{
			Vector3 vel = rb.linearVelocity;

			float feetY = transform.position.y - (playerHeight * 0.5f);
			float missingHeight = targetTopY - feetY;

			// jeśli już nie brakuje - kończymy
			if (missingHeight <= 0.02f)
				break;

			float assist01 = Mathf.InverseLerp(clutchMinMissHeight, clutchMaxMissHeight, missingHeight);
			float boost = Mathf.Lerp(0.5f, clutchAssistUp, assist01);

			// delikatne "podciągnięcie" tylko w pionie
			float targetY = Mathf.Max(vel.y, boost);
			float newY = Mathf.Lerp(vel.y, targetY, 0.3f);

			rb.linearVelocity = new Vector3(
				vel.x,
				newY,
				vel.z
			);

			elapsed += Time.fixedDeltaTime;
			yield return new WaitForFixedUpdate();
		}

		isClutching = false;
	}

	public void ApplyBoost(Vector3 direction, float force, bool resetY = false)
    {
        if (resetY)
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

		boostLockTimer = boostLockTime;
		state = MovementState.air;
		grounded = false;

		rb.AddForce(direction.normalized * force, ForceMode.VelocityChange);
    }

    public Vector3 GetMoveDirection()
    {
        return lastMoveDir.sqrMagnitude > 0.01f ? lastMoveDir : orientation.forward;
    }

	void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("BreakableVent"))
        {
            VentBreak vent = collision.collider.GetComponent<VentBreak>();
            if (vent != null)
                vent.Break();
        }
    }
}