using UnityEngine;

public class TeamSpawnPoint : MonoBehaviour
{
    public Team team;

    private void OnDrawGizmos()
    {
        Gizmos.color = team == Team.Red ? Color.red : Color.blue;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward);
    }
}
