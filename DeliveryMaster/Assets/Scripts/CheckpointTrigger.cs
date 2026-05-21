using UnityEngine;

public class CheckpointTrigger : MonoBehaviour
{
    [Tooltip("Ustawiane przez MissionManager przy spawnie. Na prefabie obojętne.")]
    public CheckpointType checkpointType;

    [Tooltip("Powiązanie Start↔End. Ustawiane przez MissionManager przy spawnie.")]
    public CheckpointTrigger sibling;

    [Header("Lifetime (tylko Start)")]
    public float lifetime = 90f;
    public float fadeDuration = 2f;

    private float age;
    private bool triggered;
    private bool fading;
    private Vector3 originalScale;
    private bool active = true;

    private void Awake()
    {
        originalScale = transform.localScale;
    }

    private void Update()
    {
        if (!active) return;
        if (checkpointType != CheckpointType.Start) return;
        if (triggered) return;

        age += Time.deltaTime;

        if (!fading && age >= lifetime - fadeDuration)
        {
            fading = true;
        }

        if (fading)
        {
            float t = Mathf.Clamp01((age - (lifetime - fadeDuration)) / fadeDuration);
            transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, t);

            if (t >= 1f)
            {
                active = false;
                if (MissionManager.Instance != null)
                {
                    MissionManager.Instance.OnStartExpired(this);
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggered || !active) return;
        if (!other.CompareTag("Player")) return;

        triggered = true;
        if (MissionManager.Instance != null)
        {
            MissionManager.Instance.OnCheckpointReached(this);
        }
    }

    public void PauseLifetime()
    {
        active = false;
    }

    public void ResumeLifetime()
    {
        active = true;
    }
}
