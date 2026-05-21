using UnityEngine;
using TMPro;

[RequireComponent(typeof(Rigidbody))]
public class CrashPenalty : MonoBehaviour
{
    [Header("Wysokość kary")]
    [Tooltip("Bazowa kara za każdą kolizję która przeszła próg.")]
    public int baseCrashPenalty = 1;
    [Tooltip("Dodatkowe monety za każdy m/s prędkości względnej powyżej progu.")]
    public float impactPenaltyPerUnit = 0.25f;
    [Tooltip("Maksymalna kara za pojedyncze uderzenie (clamp).")]
    public int maxPenaltyPerHit = 5;

    [Header("Filtrowanie")]
    [Tooltip("Minimalna prędkość względna (m/s) żeby kolizja była karana — odsiewa dotykanie ziemi/ślizgi i delikatne stuknięcia.")]
    public float minImpactSpeed = 7f;
    [Tooltip("Cooldown między karami (sek). Zapobiega drenażowi monet przy ocieraniu o ścianę.")]
    public float cooldown = 0.5f;

    [Header("UI")]
    [Tooltip("Tekst który pokazuje 'Crash -N' po uderzeniu. Zostaw puste żeby wyłączyć komunikat.")]
    public TextMeshProUGUI crashText;
    public Color crashColor = new Color(1f, 0.3f, 0.2f);
    [Tooltip("Jak długo (sek) komunikat o crashu jest widoczny zanim zniknie.")]
    public float messageDuration = 1.5f;

    [Header("Opcjonalne logowanie")]
    public bool logCrashes = false;

    private float lastPenaltyTime = -999f;
    private float messageTimeLeft;

    private void Start()
    {
        if (crashText == null)
        {
            Debug.LogWarning($"CrashPenalty on '{gameObject.name}': pole Crash Text nie jest podpięte — komunikat 'Crash!' nie pokaże się dla tego obiektu.", this);
            return;
        }

        crashText.text = string.Empty;
        Color c = crashText.color;
        c.a = 0f;
        crashText.color = c;
    }

    private void Update()
    {
        if (crashText == null || messageTimeLeft <= 0f) return;

        messageTimeLeft -= Time.deltaTime;
        Color c = crashText.color;
        c.a = Mathf.Clamp01(messageTimeLeft / messageDuration);
        crashText.color = c;

        if (messageTimeLeft <= 0f)
        {
            crashText.text = string.Empty;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (Time.time - lastPenaltyTime < cooldown) return;

        float impact = collision.relativeVelocity.magnitude;
        if (impact < minImpactSpeed) return;

        int extra = Mathf.FloorToInt((impact - minImpactSpeed) * impactPenaltyPerUnit);
        int penalty = Mathf.Clamp(baseCrashPenalty + extra, 0, maxPenaltyPerHit);
        if (penalty <= 0) return;

        if (CoinManager.Instance != null) CoinManager.Instance.AddCoins(-penalty);
        lastPenaltyTime = Time.time;

        ShowCrashMessage(penalty);

        if (logCrashes)
        {
            Debug.Log($"CrashPenalty: hit {collision.gameObject.name} @ {impact:F1} m/s, -{penalty} coins");
        }
    }

    private void ShowCrashMessage(int amount)
    {
        if (crashText == null) return;
        crashText.text = "Crash!";
        Color c = crashColor;
        c.a = 1f;
        crashText.color = c;
        messageTimeLeft = messageDuration;
    }
}
