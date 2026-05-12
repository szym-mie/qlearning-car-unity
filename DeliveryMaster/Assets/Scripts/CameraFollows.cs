using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0, 3, -6);

    [Header("Follow")]
    [SerializeField] private float smoothTime = 0.2f;
    private Vector3 velocity;

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 5f;

    [Header("Orbit")]
    [SerializeField] private float mouseSensitivity = 3f;
    [SerializeField] private float returnSpeed = 2f;

    private float currentYaw;
    private float currentPitch;

    private float defaultYaw;
    private float defaultPitch;

    private bool isOrbiting;

    private void Start()
    {
        Vector3 angles = transform.eulerAngles;
        currentYaw = defaultYaw = angles.y;
        currentPitch = defaultPitch = angles.x;
    }

    private void LateUpdate()
    {
        HandleInput();
        HandleRotation();
        HandleTranslation();
    }

    private void HandleInput()
    {
        if (Input.GetMouseButton(1)) // PPM
        {

            Debug.Log("RMB działa");

            isOrbiting = true;

            currentYaw += Input.GetAxis("Mouse X") * mouseSensitivity * 100f * Time.deltaTime;
            currentPitch -= Input.GetAxis("Mouse Y") * mouseSensitivity * 100f * Time.deltaTime;

            currentPitch = Mathf.Clamp(currentPitch, -20f, 60f);
        }
        else
        {
            isOrbiting = false;

            // powrót do defaultowej rotacji
            currentYaw = Mathf.Lerp(currentYaw, defaultYaw, returnSpeed * Time.deltaTime);
            currentPitch = Mathf.Lerp(currentPitch, defaultPitch, returnSpeed * Time.deltaTime);
        }
    }

    private void HandleRotation()
    {
        Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0);
        transform.rotation = rotation;
    }

    private void HandleTranslation()
    {
        Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0);
        Vector3 desiredPosition = target.position + rotation * offset;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref velocity,
            smoothTime
        );
    }
}