using UnityEngine;

public class MissionTrigger : MonoBehaviour
{
    public int reward = 10;
    private bool activated = false;

    private void OnTriggerEnter(Collider other)
    {
        if (activated) return;

        if (other.CompareTag("Player"))
        {
            activated = true;

            Debug.Log("Player entered trigger! +" + reward + " coins");

            if (CoinManager.Instance != null)
            {
                CoinManager.Instance.AddCoins(reward);
            }

            gameObject.SetActive(false);
        }
    }
}