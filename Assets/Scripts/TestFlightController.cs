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

    [Header("G-Force Limiting")]
    public float maxGForce = 9f;           // G-force limiter so you cant pull way too hard
    public float gForceSmoothing = 0.1f;

    private float currentGForce = 1f;      //base g-force (atmospheric)
    private Vector3 lastVelocity;

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
        CalculateGForce();
        ApplyThrust();
        ApplyLift();
        ApplyAerodynamicDrag();
        ApplyControlInputs();
    }

    void ApplyThrust()
    {
        rb.AddForce(transform.forward * maxThrust * throttleInput);
    }

    void ApplyLift()
    {
        float speed = rb.linearVelocity.magnitude;
        Vector3 lift = transform.up * speed * liftPower;

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
        float gLimiter = Mathf.Clamp01(1f - ((currentGForce - maxGForce) / maxGForce));
        gLimiter = Mathf.Max(gLimiter, 0.3f); // always allow at least 30% control (but might change to introduce black-out or red-out simulation)

        // pitch (g-limited)
        rb.AddRelativeTorque(Vector3.right * moveInput.y * pitchTorque * gLimiter);

        // roll (g-limited)
        rb.AddRelativeTorque(Vector3.forward * -moveInput.x * rollTorque * gLimiter);

        // yaw (not g-limited)
        rb.AddRelativeTorque(Vector3.up * yawInput * yawTorque);
    }

    void CalculateGForce()
    {
        // calculateacceleration
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
        GUI.Label(new Rect(10, 10, 200, 20), $"Speed: {rb.linearVelocity.magnitude:F1} m/s");
        GUI.Label(new Rect(10, 30, 200, 20), $"G-Force: {currentGForce:F2}G");

        // Warning when approaching G-limit
        if (currentGForce > maxGForce * 0.8f)
        {
            GUI.color = Color.red;
            GUI.Label(new Rect(10, 50, 200, 20), "HIGH G - CONTROLS LIMITED!");
        }
    }
}