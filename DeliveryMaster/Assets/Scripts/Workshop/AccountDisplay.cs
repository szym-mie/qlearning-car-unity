using UnityEngine;
using TMPro;

public class AccountDisplay : MonoBehaviour
{
    public static AccountDisplay Instance { get; private set; }

    [SerializeField] private TextMeshProUGUI accountText;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (ShopDataManager.Instance == null) return;
        accountText.text = $"{ShopDataManager.Instance.UserProfile.account:N0}";
    }
}
