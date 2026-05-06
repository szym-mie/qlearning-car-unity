using UnityEngine;

public class CameraRig : MonoBehaviour
{
    public Transform target;

    public float distance = 6f;
    public float height = 2f;
    public float sensitivity = 3f;

    [Header("Auto Return")]
    public float returnSpeed = 5f;

    float yaw;
    float pitch = 10f;

    void LateUpdate()
    {
        if (target == null) return;

        if (Input.GetMouseButton(1))
        {
            // ręczne obracanie
            yaw += Input.GetAxis("Mouse X") * sensitivity;
            pitch -= Input.GetAxis("Mouse Y") * sensitivity;
            pitch = Mathf.Clamp(pitch, -20f, 60f);
        }
        else
        {
            // 🔥 auto powrót za auto
            float targetYaw = target.eulerAngles.y;
            yaw = Mathf.LerpAngle(yaw, targetYaw, returnSpeed * Time.deltaTime);

            // opcjonalnie wyrównanie pitch
            pitch = Mathf.Lerp(pitch, 10f, returnSpeed * Time.deltaTime);
        }

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0);
        Vector3 offset = rot * new Vector3(0, height, -distance);

        transform.position = target.position + offset;
        transform.LookAt(target);
    }
}