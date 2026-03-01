using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class TestFlightController : MonoBehaviour
{
    private FlightControls controls;
    private Rigidbody rb;
    private Vector2 moveInput;
    private float yawInput;
    private float throttleInput;

    [Header("Thrust")]
    public float maxThrust = 20000f;

    [Header("Control Torques")]
    public float pitchTorque = 5000f;
    public float rollTorque = 5000f;
    public float yawTorque = 3000f;

    [Header("Aerodynamics")]
    public float liftPower = 1f;
    public float forwardDrag = 0.1f;      // Forward drag
    public float lateralDrag = 5f;         // Sideways drag
    public float verticalDrag = 3f;        // Up/down drag (need these so plane doesn't rotate but still travel in 1 direction)

    [Header("Stall Settings")]
    public float stallAngle = 15f;          // Angle of attack where stall begins
    public float criticalStallAngle = 20f;  // 100% stall angle
    public float minStallSpeed = 20f;       // Minimum speed before stall (m/s)
    public float stallLiftReduction = 0.9f; // How much lift is lost at full stall
    public float stallControlReduction = 0.8f; // Control reduction during stall

    [Header("Ground Detection")]
    public float groundCheckDistance = 2f;  // How far to check for ground
    public LayerMask groundLayer;

    [Header("G-Force Limiting")]
    public float maxGForce = 5f;           // G-force limiter so you cant pull way too hard
    public float gForceSmoothing = 0.1f;

    private float currentGForce = 1f;      // Base g-force (atmospheric = 1G)
    private Vector3 lastVelocity;

    // Stall variables
    private float currentAoA = 0f;
    private float stallFactor = 0f;  // 0 = no stall, 1 = full stall
    private bool isStalling = false;

    // Ground detection
    private bool isGrounded = false;

    void Awake()
    {
        controls = new FlightControls();
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0f, -0.5f, -1.0f);
        lastVelocity = rb.linearVelocity;
    }

    void OnEnable()
    {
        controls.Flight.Enable();
    }

    void OnDisable()
    {
        controls.Flight.Disable();
    }

    void Update()
    {
        moveInput = controls.Flight.Move.ReadValue<Vector2>();
        yawInput = controls.Flight.Yaw.ReadValue<float>();
        throttleInput = controls.Flight.Throttle.ReadValue<float>();
    }

    void FixedUpdate()
    {
        CheckGroundStatus();
        CalculateGForce();

        // Only calculate stall if airborne
        if (!isGrounded)
        {
            CalculateStall();
        }
        else
        {
            // Reset stall and make it impossible when grounded
            stallFactor = 0f;
            isStalling = false;
            currentAoA = 0f;
        }

        ApplyThrust();
        ApplyLift();
        ApplyAerodynamicDrag();
        ApplyControlInputs();

        // Only apply stall effects if airborne
        if (!isGrounded)
        {
            ApplyStallEffects();
        }
    }

    void CheckGroundStatus()
    {
        // Raycast downward from aircraft to check for ground
        RaycastHit hit;
        if (Physics.Raycast(transform.position, -transform.up, out hit, groundCheckDistance, groundLayer))
        {
            isGrounded = true;
        }
        else
        {
            isGrounded = false;
        }
    }

    void ApplyThrust()
    {
        rb.AddForce(transform.forward * maxThrust * throttleInput);
    }

    void ApplyLift()
    {
        if (isGrounded) return;

        float speed = rb.linearVelocity.magnitude;

        // Reduce lift based on stall factor
        float liftMultiplier = 1f - (stallFactor * stallLiftReduction);
        Vector3 lift = transform.up * speed * liftPower * liftMultiplier;

        // Apply lift slightly behind center to prevent nose dive
        Vector3 liftPoint = transform.position - transform.forward * 2.0f;
        rb.AddForceAtPosition(lift, liftPoint);
    }

    void ApplyAerodynamicDrag()
    {
        // Get velocity
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        float speed = rb.linearVelocity.magnitude;

        // Forward/backward drag
        float forwardDragForce = localVelocity.z * localVelocity.z * Mathf.Sign(localVelocity.z) * forwardDrag;

        // Lateral drag (prevents sliding sideways)
        float lateralDragForce = localVelocity.x * localVelocity.x * Mathf.Sign(localVelocity.x) * lateralDrag;

        // Vertical drag (prevents unrealistic vertical movement)
        float verticalDragForce = localVelocity.y * localVelocity.y * Mathf.Sign(localVelocity.y) * verticalDrag;

        // Apply forward/back, left/right, up/down drag
        Vector3 dragForce = new Vector3(-lateralDragForce, -verticalDragForce, -forwardDragForce);
        rb.AddRelativeForce(dragForce * speed);
    }

    void ApplyControlInputs()
    {
        // Calculate G-force limit (reduces control at high Gs)
        float gLimiter = Mathf.Clamp01(1f - ((currentGForce - (maxGForce * 0.9f)) / (maxGForce * 0.4f)));
        gLimiter = gLimiter * gLimiter * gLimiter;
        gLimiter = Mathf.Max(gLimiter, 0.0f); // always allow at least 1% control (but might change to introduce black-out or red-out simulation)

        // Stall control reduction
        float stallControlMultiplier = isGrounded ? 1f : (1f - (stallFactor * stallControlReduction));

        // Pitch (g-limited)
        rb.AddRelativeTorque(Vector3.right * moveInput.y * pitchTorque * gLimiter);

        // Roll (g-limited)
        rb.AddRelativeTorque(Vector3.forward * -moveInput.x * rollTorque * gLimiter);

        // Yaw (not g-limited) (yaw control not actually implemented yet so this doesn't matter)
        rb.AddRelativeTorque(Vector3.up * yawInput * yawTorque);
    }

    void CalculateStall()
    {
        float speed = rb.linearVelocity.magnitude;

        // Calculate Angle of Attack (AoA)
        if (speed > 0.1f)
        {
            Vector3 velocityDirection = rb.linearVelocity.normalized;
            Vector3 forward = transform.forward;

            // Angle between aircraft nose and velocity direction
            currentAoA = Vector3.Angle(forward, velocityDirection);

            // Check if velocity is pointed above or below aircraft (for proper AoA sign)
            float verticalComponent = Vector3.Dot(transform.up, velocityDirection);
            if (verticalComponent < 0)
                currentAoA = -currentAoA;
        }
        else
        {
            currentAoA = 0f;
        }

        // Calculate stall factor based on AoA and speed
        if (Mathf.Abs(currentAoA) > stallAngle || speed < minStallSpeed)
        {
            // Calculate how deep into stall the plane is
            float aoaStallFactor = Mathf.InverseLerp(stallAngle, criticalStallAngle, Mathf.Abs(currentAoA));
            float speedStallFactor = Mathf.InverseLerp(minStallSpeed, minStallSpeed * 0.5f, speed);

            // Use the worse of the two
            stallFactor = Mathf.Max(aoaStallFactor, speedStallFactor);
            stallFactor = Mathf.Clamp01(stallFactor);

            isStalling = stallFactor > 0.1f;
        }
        else
        {
            stallFactor = 0f;
            isStalling = false;
        }
    }

    void ApplyStallEffects()
    {
        if (isStalling)
        {
            // Nose drop during stall (realistic stall behavior)
            float noseDropTorque = stallFactor * pitchTorque * 0.5f;
            rb.AddRelativeTorque(Vector3.right * noseDropTorque);

            // Buffeting effect (doesn't work, not sure why, need to debug later)
            if (stallFactor > 0.5f)
            {
                Vector3 buffet = Random.insideUnitSphere * stallFactor * 500f;
                rb.AddRelativeTorque(buffet);
            }
        }
    }

    void CalculateGForce()
    {
        // Calculate acceleration
        Vector3 acceleration = (rb.linearVelocity - lastVelocity) / Time.fixedDeltaTime;
        lastVelocity = rb.linearVelocity;

        // Calculate G-force (1G = 9.81)
        float instantGForce = (acceleration.magnitude + 9.81f) / 9.81f;

        // Smoothen it aka round it out a bit
        currentGForce = Mathf.Lerp(currentGForce, instantGForce, gForceSmoothing);
    }

    // Very simple GUI for speed and g-limit, to be removed or replaced later
    void OnGUI()
    {
        //GUI.Label(new Rect(10, 10, 200, 20), $"Speed: {rb.linearVelocity.magnitude:F1} m/s");
        GUI.Label(new Rect(10, 10, 200, 20), $"Speed: {rb.linearVelocity.magnitude*2.37:F1} mph");
        GUI.Label(new Rect(10, 30, 200, 20), $"G-Force: {currentGForce:F2}G");
        GUI.Label(new Rect(10, 50, 200, 20), $"AoA: {currentAoA:F1}°");

        // Ground status
        GUI.color = isGrounded ? Color.green : Color.white;
        GUI.Label(new Rect(10, 70, 200, 20), isGrounded ? "GROUNDED" : "AIRBORNE");
        GUI.color = Color.white;

        // Stall warning
        if (isStalling && stallFactor >= 0.95f)
        {
            GUI.color = Color.red;
            GUI.Label(new Rect(10, 90, 300, 20), $"*** STALL WARNING *** ({stallFactor * 100:F0}%)");
            GUI.color = Color.white;
        }

        // Warning when approaching (80%) of G-limit
        if (currentGForce > maxGForce * 0.8f)
        {
            GUI.color = Color.red;
            GUI.Label(new Rect(150, 30, 200, 20), "HIGH G - CONTROLS LIMITED!");
        }
    }
}