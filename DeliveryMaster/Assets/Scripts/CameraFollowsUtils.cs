using UnityEngine;
using Cinemachine;

public class VCamRMBLook : MonoBehaviour
{
    [SerializeField] private CinemachineVirtualCamera cam;
    [SerializeField] private Transform target;

    public float sensitivity = 3f;
    private float yaw;
    private float pitch;

    void LateUpdate()
    {

        if (cam == null)
        {
            Debug.LogError("CAM NULL");
            return;
        }

        if (target == null)
        {
            Debug.LogError("TARGET NULL");
            return;
        }

        
        if (Input.GetMouseButton(1))
        {
            yaw += Input.GetAxis("Mouse X") * sensitivity;
            pitch -= Input.GetAxis("Mouse Y") * sensitivity;
            pitch = Mathf.Clamp(pitch, -20f, 60f);
        }

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0);

        Vector3 offset = rot * new Vector3(0, 3, -6);

        cam.transform.position = target.position + offset;
        cam.transform.LookAt(target.position);
    }
}