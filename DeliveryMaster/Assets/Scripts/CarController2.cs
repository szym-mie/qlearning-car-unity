using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class CarController2 : MonoBehaviour
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
    [SerializeField] private float detectionDistance = 4f;
    [SerializeField] private float rayHeightOffset = 0.5f;
    [SerializeField] private float rayFwdOffset = 3f;
    [Tooltip("Boczne odsunięcie promieni od osi auta (≈ pół szerokości auta).")]
    [SerializeField] private float raySideOffset = 1f;
    [SerializeField] private float rayDirFwd = 25f;
    [SerializeField] private float rayDirSide = 75f;


    [Header("Stuck recovery")]
    [SerializeField] private float stuckSpeedThreshold = 0.5f;
    [SerializeField] private float stuckTimeBeforeReverseMin = 2f;
    [SerializeField] private float stuckTimeBeforeReverseMax = 3f;
    [SerializeField] private float reverseDuration = 1f;
    [SerializeField] private float reverseTorqueMultiplier = 1f;

    [Header("Decision weights")]
    [Tooltip("Wagi wyboru kierunku na skrzyżowaniu. Nie muszą sumować się do 1 — i tak są normalizowane.")]
    [SerializeField] private float straightWeight = 0.60f;
    [SerializeField] private float rightWeight = 0.25f;
    [SerializeField] private float leftWeight = 0.15f;
    [Tooltip("Kąt (st.) w którym kandydat jest klasyfikowany jako 'prosto'. Powyżej tego — prawo/lewo wg znaku.")]
    [SerializeField] private float straightAngleThreshold = 30f;

    [SerializeField] private WheelCollider frontLeftWheelCollider;
    [SerializeField] private WheelCollider frontRightWheelCollider;
    [SerializeField] private WheelCollider rearLeftWheelCollider;
    [SerializeField] private WheelCollider rearRightWheelCollider;

    [SerializeField] private Transform frontLeftWheelTransform;
    [SerializeField] private Transform frontRightWheelTransform;
    [SerializeField] private Transform rearLeftWheelTransform;
    [SerializeField] private Transform rearRightWheelTransform;

    [SerializeField] private bool shouldPrintReward = false;
    [SerializeField] private int printRewardEvery = 10;

    private Waypoint currentTarget;
    private Waypoint previousTarget;
    private float steerInput;
    private float steerAngle;
    private bool obstacleAhead;

    private float stuckTimer;
    private float reverseTimer;
    private bool isReversing;
    private float currentStuckThreshold;

    // agentowe rzeczy
    private QLearner learner;
    private State agentLastState;
    private Observation agentLastObservation;
    private float agentRewardMin;
    private float agentRewardMax;
    private float agentRewardSum;
    private int agentAttemptCount;
    private bool agentEndPreCond = false;
    private bool isAgentStuck;
    private float agentStuckTimer;
    private Action agentAction;
    private bool isAgentActive;
    private Vector2 agentZeroPoint; // punkt zerowy ukladu wsporzednych dla qlearn
    private Vector3 agentInitPosition; // punkt do teleportacji samochodu po probie
    private readonly bool nonMarkovianActions = false; // wygładzone sterowanie

    // tymczasowe
    private Quaternion senseDistFwdLRotation;
    private Quaternion senseDistFwdRRotation;
    private Quaternion senseDistSideLRotation;
    private Quaternion senseDistSideRRotation;
    private Vector3 senseDistCentOffset;
    private Vector3 senseDistFwdLOffset;
    private Vector3 senseDistFwdROffset;
    private Vector3 senseDistSideLOffset;
    private Vector3 senseDistSideROffset;


    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0, -0.5f, 0);

        currentTarget = startWaypoint != null ? startWaypoint : FindNearestWaypoint();
        RerollStuckThreshold();

        // inicjalizacja agenta
        //int[] bins = new int[] { 1, 1, 1, 1, 7, 7, 7, 3, 3 };
        int[] bins = new int[] { 1, 1, 1, 1, 1, 4, 4, 2, 2 };
        int actionCount = Enum.GetNames(typeof(Action)).Length;
        learner = new QLearner(bins, actionCount, 0.2f, 0.98f, 1f, 0.2f, +10f, +5f);
        agentLastState = new State { x = 0, y = 0, dir = 0, vel = 0 };
        agentLastObservation = new Observation { };
        agentRewardMax = 0f;
        agentRewardMin = 500f; // TODO zmien na jakas sensowniejsza wartosc

        senseDistFwdLRotation = Quaternion.AngleAxis(-rayDirFwd, Vector3.up);
        senseDistFwdRRotation = Quaternion.AngleAxis(+rayDirFwd, Vector3.up);
        senseDistSideLRotation = Quaternion.AngleAxis(-rayDirSide, Vector3.up);
        senseDistSideRRotation = Quaternion.AngleAxis(+rayDirSide, Vector3.up);
        senseDistCentOffset = new(0f, rayHeightOffset, rayFwdOffset);
        senseDistFwdLOffset = new(-raySideOffset, rayHeightOffset, rayFwdOffset);
        senseDistFwdROffset = new(+raySideOffset, rayHeightOffset, rayFwdOffset);
        senseDistSideLOffset = new(-raySideOffset, rayHeightOffset, rayFwdOffset);
        senseDistSideROffset = new(+raySideOffset, rayHeightOffset, rayFwdOffset);
    }

    private void RerollStuckThreshold()
    {
        currentStuckThreshold = UnityEngine.Random.Range(stuckTimeBeforeReverseMin, stuckTimeBeforeReverseMax);
    }

    private void FixedUpdate()
    {
        UpdateTarget();
        UpdateSteerInput();
        DetectObstacle();
        UpdateStuckRecovery();
        if (isAgentActive) UpdateAgent();
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
                // start manewru omijania (QLearn)
                Debug.Log("start agent");
                agentInitPosition = transform.position;
                agentZeroPoint.x = transform.position.x;
                agentZeroPoint.y = transform.position.z;
                Debug.Log($"agent zero: {agentZeroPoint}");
                isAgentActive = true;
                stuckTimer = 0f;
                RerollStuckThreshold();
            }
            return;
        }

        if (!isAgentActive)
        {
            if (rb.linearVelocity.magnitude < stuckSpeedThreshold)
            {
                stuckTimer += Time.fixedDeltaTime;
                if (stuckTimer >= currentStuckThreshold)
                {
                    isReversing = true;
                    reverseTimer = reverseDuration;
                    stuckTimer = 0f;
                }
            }
            else
            {
                if (stuckTimer > 0f)
                {
                    RerollStuckThreshold();
                }
                stuckTimer = 0f;
            }
        }
    }

    private Ray senseDistRay = new();
    private RaycastHit senseDistHit = new();

    private float SenseDist(Quaternion offsetRotation, Vector3 offsetLinear, float maxDistance)
    {
        Vector3 direction = offsetRotation * transform.forward;
        senseDistRay.origin = transform.position + transform.rotation * offsetLinear;
        senseDistRay.direction = direction;
        bool isHit = Physics.Raycast(senseDistRay, out senseDistHit, maxDistance, obstacleMask, QueryTriggerInteraction.Ignore);
        float distance = isHit ? senseDistHit.distance: maxDistance;
        Debug.DrawRay(senseDistRay.origin, senseDistRay.direction * distance, Color.purple, 0f, false);
        return distance;
    }

    private Observation MakeObservation() 
    {
        Vector2 worldPosition;
        worldPosition.x = transform.position.x;
        worldPosition.y = transform.position.z;
        Vector2 localPosition = worldPosition - agentZeroPoint;
        float dir = rb.rotation.eulerAngles.y;
        if (dir >= 180f) {
            // normalizacja do zakresu (-180, +180)
            dir -= 360f;
        }
        float vel = rb.linearVelocity.magnitude;

        float distCent = SenseDist(Quaternion.identity, senseDistCentOffset, learner.distFwdMax);
        float distFwdL = SenseDist(senseDistFwdLRotation, senseDistFwdLOffset, learner.distFwdMax);
        float distFwdR = SenseDist(senseDistFwdRRotation, senseDistFwdROffset, learner.distFwdMax);
        float distSideL = SenseDist(senseDistSideLRotation, senseDistSideLOffset, learner.distSideMax);
        float distSideR = SenseDist(senseDistSideRRotation, senseDistSideROffset, learner.distSideMax);

        return new Observation 
        { 
            x = localPosition.x, 
            y = localPosition.y, 
            dir = dir,
            vel = vel, 
            distCent = distCent,
            distFwdL = distFwdL, 
            distFwdR = distFwdR,
            distSideL = distSideL,
            distSideR = distSideR
        };
    }

    private float GetReward1(Action action, Observation observation)
    {
        float x = observation.x;
        // nie wyjezdzaj poza droge
        if (Mathf.Abs(x) > 3)
        {
            return 0f;
        }
        float y = observation.y;
        float dy = y - agentLastObservation.y;
        // wiecej nagrod za poradzenie sobie z przeszkoda
        if (y > 12f) {
            return dy * 100;
        }
        // im blizszy dystans tym gorsza nagroda
        float distL = observation.distFwdL / 5f;
        float distR = observation.distFwdR / 5f;
        float distMin = Mathf.Min(distL, distR);
        // przed przeszkoda dawaj punkty jazde w lewo a po przeszkodzie - w prawo
        float dirMult = 1f;
        float dir = observation.dir;
        if (y < 6f)
        {
            float dirDiff = (dir - 20f) * 4f;
            dirMult += Mathf.Cos(dirDiff) * 0.5f;
        }
        if (y > 8f)
        {
            float dirDiff = (dir + 20f) * 4f;
            dirMult += Mathf.Cos(dirDiff) * 0.5f;
        }
        return dy * distMin * dirMult;
    }

    private float GetReward3(Action action, Observation observation)
    {
        float y = observation.y;
        float dy = y - agentLastObservation.y;
        float dDistCent = observation.distCent - agentLastObservation.distCent;
        float dDistFwdL = observation.distFwdL - agentLastObservation.distFwdL;
        float dDistFwdR = observation.distFwdR - agentLastObservation.distFwdR;
        float dDistSideL = observation.distSideL - agentLastObservation.distSideL;
        float dDistSideR = observation.distSideR - agentLastObservation.distSideR;
        float reward = dy;
        float maxFwdDist = Math.Max(observation.distFwdL, observation.distFwdR);
        float steer = (observation.distFwdR - observation.distFwdL) / maxFwdDist;
        if (steer < -0.01f && action == Action.DRIVE_LEFT) reward *= 2f;
        if (steer > +0.01f && action == Action.DRIVE_RIGHT) reward *= 2f;
        reward += dDistCent;
        reward += dDistFwdL;
        reward += dDistFwdR;
        reward += dDistSideL;
        reward += dDistSideR;
        return reward;
    }

    private float ShouldAgentRepeat(Observation observation)
    {
        // inne testy np. OnCollisionEnter nie powiodly sie
        if (agentEndPreCond) return -100f;
        // kiedy zblizymy sie za bardzo do przeszkody
        if (observation.distFwdL < 1f || observation.distFwdR < 1f) return -100f;
        if (observation.distSideL < 1f || observation.distSideR < 1f) return -100f;
        // TODO kiedy wyjedziemy gdzies poza obszar srodowiska
        // jezeli agent jedzie bardzo wolno, odpal czasomierz
        float vel = observation.vel;
        if (vel < 0.5f && !isAgentStuck)
        {
            isAgentStuck = true;
            agentStuckTimer = 0f;
        }
        // anty-utkniecie dla agenta
        if (isAgentStuck)
        {
            agentStuckTimer += Time.deltaTime;
            // histereza - agent musi przyspieszyc zeby wylaczyc zabezpieczenia
            // to powinno wystarczyc aby uniknac cyklu przyspieszam-hamuje
            if (vel > 2.0f) { 
                isAgentStuck = false;
            }
            //if (vel > 0.5f) { agentStuckTimer -= Time.deltaTime * 2; }
            // jezeli sie slimaczy za dlugo to koniec
            if (agentStuckTimer > 1f) return -100f;
        }
        // TODO kiedy agentowi sie uda :)
        if (observation.y > 20f) return 100f;
        // kiedy wypdanie poza swiat
        if (transform.position.y < -10f) return -100f;
        return 0f;
    }

    private void UpdateAgent()
    {
        Observation newObservation = MakeObservation();
        float endReward = ShouldAgentRepeat(newObservation);
        if (endReward != 0f) 
        {
            agentRewardSum += endReward;
            Debug.Log($"Attempt {agentAttemptCount}: y = {newObservation.y}, reward = {agentRewardSum}, epsilon = {learner.epsilon}");
            UpdateAfterAgentAttempt();
            ReinitAgent();
        }
        else
        {
            float reward = GetReward3(agentAction, newObservation);
            agentRewardSum += reward;
            State newState = learner.Discretise(newObservation);
            learner.UpdateKnowledge(agentLastState, agentAction, newState, reward);
            agentAction = learner.PickAction(agentAction, agentLastState);
            agentLastState = newState;
            agentLastObservation = newObservation;
        }
    }

    private void UpdateAfterAgentAttempt()
    {
        agentAttemptCount++;
        agentRewardMax = Mathf.Max(agentRewardMax, agentRewardSum);
        agentRewardMin = Mathf.Min(agentRewardMin, agentRewardSum);

        if (agentAttemptCount > 10) learner.epsilon *= 0.9f;
        if (learner.epsilon < 0.05) learner.epsilon = 0f;
    }

    private void ReinitAgent()
    {
        agentRewardSum = 0f;
        agentEndPreCond = false;
        transform.position = agentInitPosition;
        float dir = 0f;
        transform.rotation = Quaternion.AngleAxis(dir, Vector3.up);
        rb.linearVelocity = Vector3.zero;
        rb.rotation = Quaternion.identity;
        agentLastObservation = MakeObservation();
        isAgentStuck = false;
        agentStuckTimer = 0f;
    }

    private void OnCollisionEnter(Collision collision)
    {
        agentEndPreCond = true;
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

        Waypoint next = PickWeighted(candidates);
        previousTarget = currentTarget;
        currentTarget = next;
    }

    private Waypoint PickWeighted(List<Waypoint> candidates)
    {
        if (candidates.Count == 1) return candidates[0];

        // Kierunek "do przodu" względem dotychczasowego ruchu auta:
        // wektor poprzedni→bieżący waypoint. Jeśli brak previousTarget, używamy transform.forward.
        Vector3 forward;
        if (previousTarget != null)
        {
            forward = currentTarget.Position - previousTarget.Position;
        }
        else
        {
            forward = transform.forward;
        }
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = transform.forward;
        forward.Normalize();

        var straightOnes = new List<Waypoint>();
        var leftOnes = new List<Waypoint>();
        var rightOnes = new List<Waypoint>();

        foreach (var c in candidates)
        {
            Vector3 toC = c.Position - currentTarget.Position;
            toC.y = 0f;
            if (toC.sqrMagnitude < 0.0001f) { straightOnes.Add(c); continue; }
            float angle = Vector3.SignedAngle(forward, toC.normalized, Vector3.up);
            if (Mathf.Abs(angle) < straightAngleThreshold) straightOnes.Add(c);
            else if (angle > 0f) rightOnes.Add(c);
            else leftOnes.Add(c);
        }

        float ws = straightOnes.Count > 0 ? Mathf.Max(0f, straightWeight) : 0f;
        float wr = rightOnes.Count > 0 ? Mathf.Max(0f, rightWeight) : 0f;
        float wl = leftOnes.Count > 0 ? Mathf.Max(0f, leftWeight) : 0f;
        float total = ws + wr + wl;

        // Fallback: jeśli wszystkie wagi to 0 (np. user wyzerował) — losuj uniformly.
        if (total <= 0f) return candidates[UnityEngine.Random.Range(0, candidates.Count)];

        float roll = UnityEngine.Random.value * total;
        List<Waypoint> bucket;
        if (roll < ws) bucket = straightOnes;
        else if (roll < ws + wr) bucket = rightOnes;
        else bucket = leftOnes;

        return bucket[UnityEngine.Random.Range(0, bucket.Count)];
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
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        Vector3 baseOrigin = transform.position + forward * rayFwdOffset + Vector3.up * rayHeightOffset;
        Vector3 leftOrigin = baseOrigin - right * raySideOffset;
        Vector3 rightOrigin = baseOrigin + right * raySideOffset;

        bool leftHit = Physics.Raycast(leftOrigin, forward, detectionDistance, obstacleMask, QueryTriggerInteraction.Ignore);
        bool rightHit = Physics.Raycast(rightOrigin, forward, detectionDistance, obstacleMask, QueryTriggerInteraction.Ignore);
        obstacleAhead = leftHit || rightHit;

        Debug.DrawRay(leftOrigin, forward * detectionDistance, leftHit ? Color.red : Color.green);
        Debug.DrawRay(rightOrigin, forward * detectionDistance, rightHit ? Color.red : Color.green);
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
        else if (isAgentActive)
        {
            // agent obsluguje pedaly gazu i hamulca
            if (agentAction == Action.DRIVE_LEFT || agentAction == Action.DRIVE_RIGHT || agentAction == Action.DRIVE_FWD)
            {
                torque = motorForce;
                brake = 0f;
            }
            else
            {
                torque = 0f;
                brake = brakeForce;
            }
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
        float targetSteerAngle;
        if (isAgentActive)
        {
            // agent obsluguje kierownice
            float agentSteerInput = 0f;
            if (agentAction == Action.DRIVE_LEFT) agentSteerInput = -1f;
            if (agentAction == Action.DRIVE_RIGHT) agentSteerInput = +1f;
            targetSteerAngle = maxSteerAngle * agentSteerInput;
        }
        else
        {
            float effectiveSteer = isReversing ? 0f : steerInput;
            targetSteerAngle = maxSteerAngle * effectiveSteer;
        }
        if (nonMarkovianActions)
        {
            float epsilon = 1f * Time.fixedDeltaTime;
            float steerAngleRate = 120f * Time.fixedDeltaTime;
            float steerError = targetSteerAngle - steerAngle;
            float steerErrorAbs = math.abs(steerError);
            if (steerError > -epsilon && steerError < +epsilon) steerAngle = targetSteerAngle;
            if (steerError < 0f) steerAngle -= math.min(steerAngleRate, steerErrorAbs);
            if (steerError > 0f) steerAngle += math.min(steerAngleRate, steerErrorAbs);
        }
        else
        {
            steerAngle = targetSteerAngle;
        }
        frontLeftWheelCollider.steerAngle = steerAngle;
        frontRightWheelCollider.steerAngle = steerAngle;
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
