using System.Collections.Generic;
using UnityEngine;

public class Waypoint : MonoBehaviour
{
    [Tooltip("Sąsiednie waypointy do których auto może pojechać z tego punktu.")]
    public List<Waypoint> neighbors = new List<Waypoint>();

    public Vector3 Position => transform.position;

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(transform.position, 0.5f);

        Gizmos.color = Color.yellow;
        if (neighbors == null) return;
        foreach (var n in neighbors)
        {
            if (n == null) continue;
            Gizmos.DrawLine(transform.position, n.transform.position);
        }
    }
}
