using UnityEngine;
using TMPro;

public class MissionManager : MonoBehaviour
{
    public static MissionManager Instance;

    [Header("Checkpoints")]
    public GameObject startCheckpoint;
    public GameObject targetCheckpoint;
    public GameObject finishCheckpoint;

    [Header("UI")]
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI missionStateText;

    [Header("Settings")]
    public float missionTime = 60f;
    public int finishReward = 50;

    [Header("Timer Warning")]
    public float warningTime = 10f;
    public Color normalTimerColor = Color.white;
    public Color warningTimerColor = Color.red;
    public float blinkSpeed = 6f;

    private float timeLeft;
    private bool timerRunning = false;

    public GameObject timerPanel;

    private enum MissionState
    {
        WaitingForStart,
        GoingToTarget,
        GoingToFinish,
        Completed,
        Failed
    }

    private MissionState currentState = MissionState.WaitingForStart;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        startCheckpoint.SetActive(true);
        targetCheckpoint.SetActive(false);
        finishCheckpoint.SetActive(false);

        timeLeft = missionTime;

        if (timerText != null)
        {
            timerPanel.SetActive(false);
        }

        UpdateStateUI("Start the mission: drive into the blue checkpoint");
    }

    private void Update()
    {
        if (!timerRunning) return;

        timeLeft -= Time.deltaTime;

        if (timeLeft <= 0f)
        {
            timeLeft = 0f;
            FailMission();
        }

        UpdateTimerUI();
    }

    public void OnCheckpointReached(CheckpointTrigger checkpoint)
    {
        if (checkpoint.checkpointType == CheckpointType.Start)
        {
            TryStartMission();
        }
        else if (checkpoint.checkpointType == CheckpointType.Target)
        {
            TryReachTarget();
        }
        else if (checkpoint.checkpointType == CheckpointType.Finish)
        {
            TryFinishMission();
        }
    }

    private void TryStartMission()
    {
        if (currentState != MissionState.WaitingForStart) return;

        currentState = MissionState.GoingToTarget;

        timeLeft = missionTime;
        timerRunning = true;

        if (timerText != null)
        {
            timerPanel.SetActive(true);
            timerText.color = normalTimerColor;
        }

        startCheckpoint.SetActive(false);
        targetCheckpoint.SetActive(true);
        finishCheckpoint.SetActive(false);

        ResetCheckpoint(targetCheckpoint);

        UpdateStateUI("Drive to the red checkpoint!");
        UpdateTimerUI();
    }

    private void TryReachTarget()
    {
        if (currentState != MissionState.GoingToTarget) return;

        currentState = MissionState.GoingToFinish;

        timerRunning = false;

        if (timerText != null)
        {
            timerText.color = normalTimerColor;
            timerPanel.SetActive(false);
        }

        targetCheckpoint.SetActive(false);
        finishCheckpoint.SetActive(true);

        ResetCheckpoint(finishCheckpoint);

        UpdateStateUI("Target reached! Drive to the green finish");
    }

    private void TryFinishMission()
    {
        if (currentState != MissionState.GoingToFinish) return;

        currentState = MissionState.Completed;

        finishCheckpoint.SetActive(false);

        if (CoinManager.Instance != null)
        {
            CoinManager.Instance.AddCoins(finishReward);
        }

        UpdateStateUI("Mission completed! +" + finishReward + " coins");
    }

    private void FailMission()
    {
        if (currentState != MissionState.GoingToTarget) return;

        currentState = MissionState.Failed;
        timerRunning = false;

        if (timerText != null)
        {
            timerText.color = normalTimerColor;
            timerPanel.SetActive(false);
        }

        targetCheckpoint.SetActive(false);
        finishCheckpoint.SetActive(false);
        startCheckpoint.SetActive(true);

        ResetCheckpoint(startCheckpoint);

        UpdateStateUI("Time is up! Drive into the blue checkpoint to try again");

        currentState = MissionState.WaitingForStart;
    }
    private void ResetCheckpoint(GameObject checkpointObject)
    {
        CheckpointTrigger trigger = checkpointObject.GetComponentInChildren<CheckpointTrigger>();

        if (trigger != null)
        {
            trigger.ResetCheckpoint();
        }
    }

    private void UpdateTimerUI()
    {
        if (timerText == null) return;

        int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(timeLeft));

        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);

        if (timerRunning && timeLeft <= warningTime)
        {
            float blink = Mathf.PingPong(Time.time * blinkSpeed, 1f);
            timerText.color = Color.Lerp(normalTimerColor, warningTimerColor, blink);
        }
        else
        {
            timerText.color = normalTimerColor;
        }
    }

    private void UpdateStateUI(string text)
    {
        if (missionStateText != null)
        {
            missionStateText.text = text;
        }
    }
}