using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class TestFlightController : MonoBehaviour
{
    private FlightControls controls;

    private Rigidbody rb;

    private Vector2 moveInput;
    private float yawInput;
    private float throttleInput;

    public float maxThrust = 20000f;
    public float pitchTorque = 5000f;
    public float rollTorque = 5000f;
    public float yawTorque = 3000f;

    void Awake()
    {
        controls = new FlightControls();
        rb = GetComponent<Rigidbody>();

        rb.centerOfMass = new Vector3(0f, -0.3f, 0f);
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
        float speed = rb.linearVelocity.magnitude;

        // Thrust
        rb.AddForce(transform.forward * maxThrust * throttleInput);

        Vector3 lift = transform.up * speed * 2f;

        // Apply lift slightly behind center to prevent nose dive
        Vector3 liftPoint = transform.position - transform.forward * 2.0f;

        rb.AddForceAtPosition(lift, liftPoint);

        // Pitch
        rb.AddRelativeTorque(Vector3.right * moveInput.y * pitchTorque);

        // Roll
        rb.AddRelativeTorque(Vector3.forward * -moveInput.x * rollTorque);

        // Yaw
        rb.AddRelativeTorque(Vector3.up * yawInput * yawTorque);
    }

}