using UnityEngine;

public class CarController2 : MonoBehaviour
{
    private const string HORIZONTAL = "Horizontal";
    private const string VERTICAL = "Vertical";

    private Rigidbody rb;

    private float horizontalInput;
    private float verticalInput;
    private float currentSteerAngle;
    private float currentbreakForce;
    private bool isBreaking;

    [SerializeField] private float motorForce;
    [SerializeField] private float breakForce;
    [SerializeField] private float maxSteerAngle;
    [SerializeField] private float nudgeForce = 4f;
    [SerializeField] private Vector3 centerOfMassOffset = new Vector3(0, -0.5f, 0);

    [Header("Front Axle")]
    [SerializeField] private WheelCollider frontLeftWheelCollider;
    [SerializeField] private WheelCollider frontRightWheelCollider;
    [SerializeField] private Transform frontLeftWheelTransform;
    [SerializeField] private Transform frontRightWheelTransform;

    [Header("Middle Axle (optional)")]
    [SerializeField] private WheelCollider middleLeftWheelCollider;
    [SerializeField] private WheelCollider middleRightWheelCollider;
    [SerializeField] private Transform middleLeftWheelTransform;
    [SerializeField] private Transform middleRightWheelTransform;

    [Header("Rear Axle")]
    [SerializeField] private WheelCollider rearLeftWheelCollider;
    [SerializeField] private WheelCollider rearRightWheelCollider;
    [SerializeField] private Transform rearLeftWheelTransform;
    [SerializeField] private Transform rearRightWheelTransform;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = centerOfMassOffset;
    }

    private void FixedUpdate()
    {
        GetInput();
        HandleMotor();
        HandleSteering();
        UpdateWheels();
    }

    private void GetInput()
    {
        horizontalInput = Input.GetAxis(HORIZONTAL);
        verticalInput   = Input.GetAxis(VERTICAL);
        isBreaking      = Input.GetKey(KeyCode.Space);

        if (Input.GetKeyDown(KeyCode.LeftShift))
            rb.AddForce(Vector3.up * nudgeForce, ForceMode.Impulse);
    }

    private void HandleMotor()
    {
        frontLeftWheelCollider.motorTorque  = verticalInput * motorForce;
        frontRightWheelCollider.motorTorque = verticalInput * motorForce;

        // middle axle: no motor, only braking (support axle)

        currentbreakForce = isBreaking ? breakForce : 0f;
        ApplyBreaking();
    }

    private void ApplyBreaking()
    {
        frontLeftWheelCollider.brakeTorque  = currentbreakForce;
        frontRightWheelCollider.brakeTorque = currentbreakForce;
        rearLeftWheelCollider.brakeTorque   = currentbreakForce;
        rearRightWheelCollider.brakeTorque  = currentbreakForce;

        if (middleLeftWheelCollider != null)  middleLeftWheelCollider.brakeTorque  = currentbreakForce;
        if (middleRightWheelCollider != null) middleRightWheelCollider.brakeTorque = currentbreakForce;
    }

    private void HandleSteering()
    {
        currentSteerAngle = maxSteerAngle * horizontalInput;
        frontLeftWheelCollider.steerAngle  = currentSteerAngle;
        frontRightWheelCollider.steerAngle = currentSteerAngle;
    }

    private void UpdateWheels()
    {
        UpdateSingleWheel(frontLeftWheelCollider,  frontLeftWheelTransform);
        UpdateSingleWheel(frontRightWheelCollider, frontRightWheelTransform);
        UpdateSingleWheel(rearLeftWheelCollider,   rearLeftWheelTransform);
        UpdateSingleWheel(rearRightWheelCollider,  rearRightWheelTransform);

        if (middleLeftWheelTransform != null)
        {
            rearLeftWheelCollider.GetWorldPose(out _, out Quaternion rotL);
            middleLeftWheelTransform.rotation = rotL;
        }
        if (middleRightWheelTransform != null)
        {
            rearRightWheelCollider.GetWorldPose(out _, out Quaternion rotR);
            middleRightWheelTransform.rotation = rotR;
        }
    }

    private void UpdateSingleWheel(WheelCollider col, Transform tr)
    {
        col.GetWorldPose(out Vector3 pos, out Quaternion rot);
        tr.rotation = rot;
        tr.position = pos;
    }
}
