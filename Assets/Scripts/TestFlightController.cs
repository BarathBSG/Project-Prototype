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
    public float liftPower = 2f;
    public float forwardDrag = 0.01f;      // forward drag
    public float lateralDrag = 5f;         // sideways drag
    public float verticalDrag = 3f;        // up/down drag (need these so plane doesn't rotate but still travel in 1 direction)

    [Header("Stall Settings")]
    public float stallAngle = 15f;          // AoA where stall begins
    public float criticalStallAngle = 20f;  // Full stall angle
    public float minStallSpeed = 20f;       // Minimum speed before stall (m/s)
    public float stallLiftReduction = 0.9f; // How much lift is lost at full stall
    public float stallControlReduction = 0.8f; // Control reduction during stall

    [Header("Ground Detection")]
    public float groundCheckDistance = 2f;  // How far to check for ground
    public LayerMask groundLayer;

    [Header("G-Force Limiting")]
    public float maxGForce = 5f;           // G-force limiter so you cant pull way too hard
    public float gForceSmoothing = 0.1f;

    private float currentGForce = 1f;      //base g-force (atmospheric)
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
            // Reset stall when grounded
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
        // get velocity
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        float speed = rb.linearVelocity.magnitude;

        // forward/backward drag
        float forwardDragForce = localVelocity.z * localVelocity.z * Mathf.Sign(localVelocity.z) * forwardDrag;

        // lateral drag (prevents sliding sideways)
        float lateralDragForce = localVelocity.x * localVelocity.x * Mathf.Sign(localVelocity.x) * lateralDrag;

        // vertical drag (prevents unrealistic vertical movement)
        float verticalDragForce = localVelocity.y * localVelocity.y * Mathf.Sign(localVelocity.y) * verticalDrag;

        // apply forward/back, left/right, up/down drag
        Vector3 dragForce = new Vector3(-lateralDragForce, -verticalDragForce, -forwardDragForce);
        rb.AddRelativeForce(dragForce * speed);
    }

    void ApplyControlInputs()
    {
        // calculate G-force limit (reduces control at high Gs)
        float gLimiter = Mathf.Clamp01(1f - ((currentGForce - (maxGForce * 0.9f)) / (maxGForce * 0.4f)));
        gLimiter = gLimiter * gLimiter * gLimiter;
        gLimiter = Mathf.Max(gLimiter, 0.0f); // always allow at least 1% control (but might change to introduce black-out or red-out simulation)

        // Stall control reduction
        float stallControlMultiplier = isGrounded ? 1f : (1f - (stallFactor * stallControlReduction));

        // pitch (g-limited)
        rb.AddRelativeTorque(Vector3.right * moveInput.y * pitchTorque * gLimiter);

        // roll (g-limited)
        rb.AddRelativeTorque(Vector3.forward * -moveInput.x * rollTorque * gLimiter);

        // yaw (not g-limited)
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

            // Check if velocity is above or below aircraft (for proper AoA sign)
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
            // Calculate how deep into stall we are
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

            // Optional: Add buffeting effect (random shaking)
            if (stallFactor > 0.5f)
            {
                Vector3 buffet = Random.insideUnitSphere * stallFactor * 500f;
                rb.AddRelativeTorque(buffet);
            }
        }
    }

    void CalculateGForce()
    {
        // calculate acceleration
        Vector3 acceleration = (rb.linearVelocity - lastVelocity) / Time.fixedDeltaTime;
        lastVelocity = rb.linearVelocity;

        // calculate G-force (1G = 9.81)
        float instantGForce = (acceleration.magnitude + 9.81f) / 9.81f;

        // smoothen it aka round it out a bit
        currentGForce = Mathf.Lerp(currentGForce, instantGForce, gForceSmoothing);
    }

    // very simple GUI for speed and g-limit, to be removed or replaced later
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

        // stall warning
        if (isStalling && stallFactor >= 0.95f)
        {
            GUI.color = Color.red;
            GUI.Label(new Rect(10, 90, 300, 20), $"*** STALL WARNING *** ({stallFactor * 100:F0}%)");
            GUI.color = Color.white;
        }

        // warning when approaching G-limit
        if (currentGForce > maxGForce * 0.8f)
        {
            GUI.color = Color.red;
            GUI.Label(new Rect(150, 30, 200, 20), "HIGH G - CONTROLS LIMITED!");
        }
    }
}