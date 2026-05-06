using System.Collections.Generic;
using UnityEngine;

public class CarNPC : MonoBehaviour
{
    private Rigidbody rb;

    private float currentSteerAngle;

    [SerializeField] private float motorForce = 1500f;
    [SerializeField] private float maxSteerAngle = 30f;
    [SerializeField] private float brakeForce = 3000f;

    [Header("NPC settings")]
    [SerializeField] private float constantForwardInput = 1f;
    [SerializeField] private float maxSpeed = 10f;

    [Header("Waypoint navigation")]
    [SerializeField] private Waypoint startWaypoint;
    [Tooltip("Promień, w którym waypoint uznajemy za osiągnięty.")]
    [SerializeField] private float arriveRadius = 3f;
    [Tooltip("Kąt (st.) przy którym sterowanie jest na maksa. Im mniejszy, tym ostrzejsze reakcje.")]
    [SerializeField] private float steerSensitivityAngle = 25f;

    [Header("Obstacle detection")]
    [SerializeField] private LayerMask obstacleMask = ~0;
    [SerializeField] private float detectionDistance = 6f;
    [SerializeField] private float rayHeightOffset = 0.5f;
    [SerializeField] private float rayForwardOffset = 1.5f;

    [Header("Stuck recovery")]
    [SerializeField] private float stuckSpeedThreshold = 0.5f;
    [SerializeField] private float stuckTimeBeforeReverse = 2.5f;
    [SerializeField] private float reverseDuration = 1f;
    [SerializeField] private float reverseTorqueMultiplier = 1f;

    [SerializeField] private WheelCollider frontLeftWheelCollider;
    [SerializeField] private WheelCollider frontRightWheelCollider;
    [SerializeField] private WheelCollider rearLeftWheelCollider;
    [SerializeField] private WheelCollider rearRightWheelCollider;

    [SerializeField] private Transform frontLeftWheelTransform;
    [SerializeField] private Transform frontRightWheelTransform;
    [SerializeField] private Transform rearLeftWheelTransform;
    [SerializeField] private Transform rearRightWheelTransform;

    private Waypoint currentTarget;
    private Waypoint previousTarget;
    private float steerInput;
    private bool obstacleAhead;

    private float stuckTimer;
    private float reverseTimer;
    private bool isReversing;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0, -0.5f, 0);

        currentTarget = startWaypoint != null ? startWaypoint : FindNearestWaypoint();
    }

    private void FixedUpdate()
    {
        UpdateTarget();
        UpdateSteerInput();
        DetectObstacle();
        UpdateStuckRecovery();
        HandleMotor();
        HandleSteering();
        LimitSpeed();
        UpdateWheels();
    }

    private void UpdateStuckRecovery()
    {
        if (isReversing)
        {
            reverseTimer -= Time.fixedDeltaTime;
            if (reverseTimer <= 0f)
            {
                isReversing = false;
                stuckTimer = 0f;
            }
            return;
        }

        if (rb.linearVelocity.magnitude < stuckSpeedThreshold)
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer >= stuckTimeBeforeReverse)
            {
                isReversing = true;
                reverseTimer = reverseDuration;
                stuckTimer = 0f;
            }
        }
        else
        {
            stuckTimer = 0f;
        }
    }

    private void UpdateTarget()
    {
        if (currentTarget == null) return;

        Vector3 flatPos = transform.position; flatPos.y = 0;
        Vector3 flatTarget = currentTarget.Position; flatTarget.y = 0;
        if (Vector3.Distance(flatPos, flatTarget) > arriveRadius) return;

        var neighbors = currentTarget.neighbors;
        if (neighbors == null || neighbors.Count == 0) return;

        List<Waypoint> candidates = new List<Waypoint>(neighbors.Count);
        foreach (var n in neighbors)
        {
            if (n == null) continue;
            if (n == previousTarget) continue;
            candidates.Add(n);
        }
        if (candidates.Count == 0)
        {
            foreach (var n in neighbors) if (n != null) candidates.Add(n);
        }
        if (candidates.Count == 0) return;

        previousTarget = currentTarget;
        currentTarget = candidates[Random.Range(0, candidates.Count)];
    }

    private void UpdateSteerInput()
    {
        if (currentTarget == null) { steerInput = 0f; return; }

        Vector3 toTarget = currentTarget.Position - transform.position;
        toTarget.y = 0f;
        Vector3 forward = transform.forward; forward.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f) { steerInput = 0f; return; }

        float angle = Vector3.SignedAngle(forward, toTarget, Vector3.up);
        steerInput = Mathf.Clamp(angle / steerSensitivityAngle, -1f, 1f);
    }

    private void DetectObstacle()
    {
        Vector3 origin = transform.position + transform.forward * rayForwardOffset + Vector3.up * rayHeightOffset;
        obstacleAhead = Physics.Raycast(origin, transform.forward, detectionDistance, obstacleMask, QueryTriggerInteraction.Ignore);
        Debug.DrawRay(origin, transform.forward * detectionDistance, obstacleAhead ? Color.red : Color.green);
    }

    private void HandleMotor()
    {
        float torque;
        float brake;
        if (isReversing)
        {
            torque = -motorForce * reverseTorqueMultiplier;
            brake = 0f;
        }
        else if (obstacleAhead)
        {
            torque = 0f;
            brake = brakeForce;
        }
        else
        {
            torque = constantForwardInput * motorForce;
            brake = 0f;
        }

        frontLeftWheelCollider.motorTorque = torque;
        frontRightWheelCollider.motorTorque = torque;

        frontLeftWheelCollider.brakeTorque = brake;
        frontRightWheelCollider.brakeTorque = brake;
        rearLeftWheelCollider.brakeTorque = brake;
        rearRightWheelCollider.brakeTorque = brake;
    }

    private void HandleSteering()
    {
        float effectiveSteer = isReversing ? 0f : steerInput;
        currentSteerAngle = maxSteerAngle * effectiveSteer;
        frontLeftWheelCollider.steerAngle = currentSteerAngle;
        frontRightWheelCollider.steerAngle = currentSteerAngle;
    }

    private void LimitSpeed()
    {
        if (rb.linearVelocity.magnitude > maxSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
        }
    }

    private void UpdateWheels()
    {
        UpdateSingleWheel(frontLeftWheelCollider, frontLeftWheelTransform);
        UpdateSingleWheel(frontRightWheelCollider, frontRightWheelTransform);
        UpdateSingleWheel(rearRightWheelCollider, rearRightWheelTransform);
        UpdateSingleWheel(rearLeftWheelCollider, rearLeftWheelTransform);
    }

    private void UpdateSingleWheel(WheelCollider wheelCollider, Transform wheelTransform)
    {
        wheelCollider.GetWorldPose(out Vector3 pos, out Quaternion rot);
        wheelTransform.position = pos;
        wheelTransform.rotation = rot;
    }

    private Waypoint FindNearestWaypoint()
    {
        Waypoint[] all = FindObjectsByType<Waypoint>(FindObjectsSortMode.None);
        Waypoint best = null;
        float bestDist = float.MaxValue;
        foreach (var w in all)
        {
            float d = (w.Position - transform.position).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = w; }
        }
        return best;
    }
}
