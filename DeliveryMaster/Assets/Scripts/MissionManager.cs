using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class MissionManager : MonoBehaviour
{
    public static MissionManager Instance;

    [Header("Prefabs")]
    [Tooltip("Prefab Start checkpointa (niebieski). Musi mieć Collider z isTrigger=true i CheckpointTrigger.")]
    public GameObject startPrefab;
    [Tooltip("Prefab End checkpointa (czerwony). Musi mieć Collider z isTrigger=true i CheckpointTrigger.")]
    public GameObject endPrefab;

    [Header("Pool startów")]
    public int targetActiveStarts = 4;
    [Tooltip("Sekundy między próbami spawnu nowego startu (jeśli jest poniżej target).")]
    public float spawnInterval = 8f;
    public float startLifetime = 90f;
    public float startFadeDuration = 2f;

    [Header("Generacja końca")]
    [Tooltip("Minimalna odległość w linii prostej między startem a endem.")]
    public float minEndStraightDistance = 60f;
    public int generationRetries = 30;
    [Tooltip("Wysokość nad waypointem na której pojawia się beacon.")]
    public float spawnHeightOffset = 0.5f;

    [Header("Czas misji")]
    [Tooltip("Sekundy na każdą jednostkę dystansu w linii prostej.")]
    public float secondsPerUnit = 0.4f;
    public float minMissionTime = 15f;
    public float maxMissionTime = 120f;

    [Header("Nagrody")]
    public int baseReward = 50;
    [Tooltip("Próg ratio czasLeft/czasMisji dla tieru Super Fast (x3).")]
    public float superFastRatio = 0.6f;
    [Tooltip("Próg ratio czasLeft/czasMisji dla tieru Fast (x2).")]
    public float fastRatio = 0.35f;

    [Header("UI")]
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI missionStateText;
    public GameObject timerPanel;
    public float warningTime = 10f;
    public Color normalTimerColor = Color.white;
    public Color warningTimerColor = Color.red;
    public float blinkSpeed = 6f;

    private readonly List<CheckpointTrigger> activeStarts = new List<CheckpointTrigger>();
    private CheckpointTrigger currentActiveStart;
    private CheckpointTrigger currentEnd;
    private float spawnCooldown;
    private float currentMissionTime;
    private float timeLeft;
    private bool inRace;

    // edge = para (a,b) z różnych skrzyżowań, w obu są neighborami
    private List<(Waypoint a, Waypoint b)> edges = new List<(Waypoint, Waypoint)>();

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        CacheEdges();
        if (timerPanel != null) timerPanel.SetActive(false);
        SetState("Find a blue marker to start a delivery");
    }

    private void CacheEdges()
    {
        edges.Clear();
        var all = Object.FindObjectsByType<Waypoint>(FindObjectsSortMode.None);
        foreach (var w in all)
        {
            if (w.neighbors == null) continue;
            foreach (var n in w.neighbors)
            {
                if (n == null) continue;
                if (n == w) continue;
                if (w.transform.parent != null && w.transform.parent == n.transform.parent) continue;
                edges.Add((w, n));
            }
        }
        if (edges.Count == 0)
        {
            Debug.LogWarning("MissionManager: nie znaleziono krawędzi między skrzyżowaniami — nie spawnuje checkpointów.");
        }
    }

    private void Update()
    {
        if (!inRace)
        {
            activeStarts.RemoveAll(s => s == null);
            spawnCooldown -= Time.deltaTime;
            if (activeStarts.Count < targetActiveStarts && spawnCooldown <= 0f)
            {
                TrySpawnRace();
                spawnCooldown = spawnInterval;
            }
        }
        else
        {
            timeLeft -= Time.deltaTime;
            if (timeLeft <= 0f)
            {
                timeLeft = 0f;
                FailRace();
            }
            UpdateTimerUI();
        }
    }

    private void TrySpawnRace()
    {
        if (edges.Count == 0 || startPrefab == null || endPrefab == null) return;

        Vector3 startPos = PickRandomEdgePoint(out Vector3 startDir);

        Vector3 endPos = Vector3.zero;
        Vector3 endDir = Vector3.forward;
        bool found = false;
        for (int i = 0; i < generationRetries; i++)
        {
            Vector3 candidate = PickRandomEdgePoint(out Vector3 candidateDir);
            if (Vector3.Distance(candidate, startPos) >= minEndStraightDistance)
            {
                endPos = candidate;
                endDir = candidateDir;
                found = true;
                break;
            }
        }
        if (!found) return;

        Vector3 lift = Vector3.up * spawnHeightOffset;
        Quaternion startRot = startDir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(startDir) : Quaternion.identity;
        Quaternion endRot = endDir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(endDir) : Quaternion.identity;

        GameObject startGO = Instantiate(startPrefab, startPos + lift, startRot);
        GameObject endGO = Instantiate(endPrefab, endPos + lift, endRot);

        var startCT = EnsureTrigger(startGO);
        var endCT = EnsureTrigger(endGO);

        startCT.checkpointType = CheckpointType.Start;
        startCT.lifetime = startLifetime;
        startCT.fadeDuration = startFadeDuration;
        startCT.sibling = endCT;

        endCT.checkpointType = CheckpointType.End;
        endCT.sibling = startCT;

        endGO.SetActive(false);
        activeStarts.Add(startCT);
    }

    private CheckpointTrigger EnsureTrigger(GameObject go)
    {
        var ct = go.GetComponent<CheckpointTrigger>();
        if (ct == null) ct = go.AddComponent<CheckpointTrigger>();
        return ct;
    }

    private Vector3 PickRandomEdgePoint(out Vector3 dir)
    {
        var edge = edges[Random.Range(0, edges.Count)];
        float t = Random.value;
        Vector3 pos = Vector3.Lerp(edge.a.Position, edge.b.Position, t);
        dir = edge.b.Position - edge.a.Position;
        return pos;
    }

    public void OnCheckpointReached(CheckpointTrigger cp)
    {
        if (cp == null) return;
        if (cp.checkpointType == CheckpointType.Start) BeginRace(cp);
        else if (cp.checkpointType == CheckpointType.End) CompleteRace(cp);
    }

    public void OnStartExpired(CheckpointTrigger cp)
    {
        if (cp == null) return;
        if (cp == currentActiveStart) return;
        activeStarts.Remove(cp);
        if (cp.sibling != null) Destroy(cp.sibling.gameObject);
        Destroy(cp.gameObject);
    }

    private void BeginRace(CheckpointTrigger start)
    {
        if (inRace) return;
        if (start.sibling == null) return;

        currentActiveStart = start;
        currentEnd = start.sibling;

        Vector3 startPos = start.transform.position;
        Vector3 endPos = currentEnd.transform.position;

        float dist = Vector3.Distance(startPos, endPos);
        currentMissionTime = Mathf.Clamp(dist * secondsPerUnit, minMissionTime, maxMissionTime);
        timeLeft = currentMissionTime;
        inRace = true;

        foreach (var s in activeStarts)
        {
            if (s == null || s == start) continue;
            s.gameObject.SetActive(false);
            if (s.sibling != null) s.sibling.gameObject.SetActive(false);
        }

        start.gameObject.SetActive(false);
        currentEnd.gameObject.SetActive(true);

        if (timerPanel != null) timerPanel.SetActive(true);
        if (timerText != null) timerText.color = normalTimerColor;
        SetState("Delivery in progress — reach the red marker!");
        UpdateTimerUI();
    }

    private void CompleteRace(CheckpointTrigger end)
    {
        if (!inRace || end != currentEnd) return;

        float ratio = currentMissionTime > 0f ? timeLeft / currentMissionTime : 0f;

        int multiplier;
        string tier;
        if (ratio >= superFastRatio) { multiplier = 3; tier = "Super Fast! Premium x3"; }
        else if (ratio >= fastRatio) { multiplier = 2; tier = "Fast! x2"; }
        else { multiplier = 1; tier = "Delivered"; }

        int reward = baseReward * multiplier;
        if (CoinManager.Instance != null) CoinManager.Instance.AddCoins(reward);
        SetState($"{tier} — +{reward} coins");

        if (currentEnd != null) Destroy(currentEnd.gameObject);
        if (currentActiveStart != null) { activeStarts.Remove(currentActiveStart); Destroy(currentActiveStart.gameObject); }
        currentActiveStart = null;
        currentEnd = null;
        inRace = false;
        if (timerPanel != null) timerPanel.SetActive(false);

        RestoreOtherStarts();
    }

    private void FailRace()
    {
        if (!inRace) return;
        inRace = false;

        if (currentEnd != null) Destroy(currentEnd.gameObject);
        if (currentActiveStart != null) { activeStarts.Remove(currentActiveStart); Destroy(currentActiveStart.gameObject); }
        currentActiveStart = null;
        currentEnd = null;
        if (timerPanel != null) timerPanel.SetActive(false);

        SetState("Time's up — delivery failed!");
        RestoreOtherStarts();
    }

    private void RestoreOtherStarts()
    {
        foreach (var s in activeStarts)
        {
            if (s == null) continue;
            s.gameObject.SetActive(true);
        }
    }

    private void UpdateTimerUI()
    {
        if (timerText == null) return;
        int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(timeLeft));
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);

        if (inRace && timeLeft <= warningTime)
        {
            float blink = Mathf.PingPong(Time.time * blinkSpeed, 1f);
            timerText.color = Color.Lerp(normalTimerColor, warningTimerColor, blink);
        }
        else
        {
            timerText.color = normalTimerColor;
        }
    }

    private void SetState(string text)
    {
        if (missionStateText != null) missionStateText.text = text;
    }
}
