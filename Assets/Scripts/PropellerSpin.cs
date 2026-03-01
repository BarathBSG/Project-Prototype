using UnityEngine;

public class PropellerSpin : MonoBehaviour
{
    [Header("Propeller Speed")]
    public float minRotationSpeed = 0f;      // Rotation speed when stopped
    public float maxRotationSpeed = 3600f;   // Rotation speed at max speed (degrees/sec)

    [Header("Aircraft Speed Scaling Range (m/s)")]
    public float minAircraftSpeed = 0f;      // m/s
    public float maxAircraftSpeed = 30f;     // m/s

    [Header("Aircraft Rigidbody (to get speed)")]
    public Rigidbody aircraftRigidbody;      // Aircraft rigidbody needed here

    private float currentRotationSpeed;

    void Start()
    {
        if (aircraftRigidbody == null)
        {
            // Try to find rigidbody in parent hierarchy
            aircraftRigidbody = GetComponentInParent<Rigidbody>();

            if (aircraftRigidbody == null)
            {
                Debug.LogError("PropellerSpin: No Rigidbody found! Please assign the aircraft's Rigidbody.");
            }
        }
    }

    void Update()
    {
        if (aircraftRigidbody != null)
        {
            // Get aircraft speed
            float aircraftSpeed = aircraftRigidbody.linearVelocity.magnitude;

            // Map speed to rotation speed
            float speedRatio = Mathf.Clamp01(aircraftSpeed / maxAircraftSpeed);
            currentRotationSpeed = Mathf.Lerp(minRotationSpeed, maxRotationSpeed, speedRatio);

            // Rotate the propeller
            transform.Rotate(0f, currentRotationSpeed * Time.deltaTime, 0f, Space.Self);
        }
    }
}
