using UnityEngine;
using UnityEngine.InputSystem;

public class FirstPersonCamera : MonoBehaviour
{
    [Range(0.1f, 9f)]
    [SerializeField] float sensitivity = 0.2f;
    [Range(0f, 90f)]
    [SerializeField] float yRotationLimit = 90f;

    private Vector2 rotation = Vector2.zero;
    private Vector2 lookInput;
    private CameraControl inputActions;

    void Awake()
    {
        inputActions = new CameraControl();
        inputActions.Camera.Look.performed += ctx =>
            lookInput = ctx.ReadValue<Vector2>();
        inputActions.Camera.Look.canceled += ctx =>
            lookInput = Vector2.zero;
    }

    void Start()
    {
        // Lock and hide cursor (seems to only work when I click on the screen)
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // make camera start level (no rotations)
        rotation = Vector2.zero;
        transform.localRotation = Quaternion.identity;
    }

    void OnEnable()
    {
        inputActions.Enable();
    }

    void OnDisable()
    {
        inputActions.Disable();
    }

    void Update()
    {
        rotation.x += lookInput.x * sensitivity;
        rotation.y += lookInput.y * sensitivity;
        rotation.y = Mathf.Clamp(rotation.y, -yRotationLimit, yRotationLimit);

        var xQuat = Quaternion.AngleAxis(rotation.x, Vector3.up);
        var yQuat = Quaternion.AngleAxis(rotation.y, Vector3.left);

        transform.localRotation = xQuat * yQuat;
    }
}