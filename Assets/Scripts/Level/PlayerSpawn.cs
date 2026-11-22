using UnityEngine;

/// <summary>
/// Where the character starts (and respawns). It comes inside the start panel prefab (Panel_Start),
/// so the character can be anywhere in the scene: at the start it is moved here.
/// </summary>
public class PlayerSpawn : MonoBehaviour
{
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.35f);
        Gizmos.DrawLine(transform.position, transform.position + transform.right * 0.8f);
    }
}
