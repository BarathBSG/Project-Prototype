using UnityEngine;

public class HorizonLockedCamera : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0, 5, -10);
    public float smoothSpeed = 5f;

    void LateUpdate()
    {
        // Follow plane position
        Vector3 desiredPosition = target.position + target.forward * offset.z + Vector3.up * offset.y;

        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

        // Look at plane, stay upright
        Vector3 lookDirection = target.position - transform.position;
        transform.rotation = Quaternion.LookRotation(lookDirection, Vector3.up);
    }
}