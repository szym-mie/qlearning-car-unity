using UnityEngine;

public class CheckpointTrigger : MonoBehaviour
{
    public CheckpointType checkpointType;

    private bool triggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (triggered) return;

        if (other.CompareTag("Player"))
        {
            triggered = true;
            MissionManager.Instance.OnCheckpointReached(this);
        }
    }

    public void ResetCheckpoint()
    {
        triggered = false;
    }
}