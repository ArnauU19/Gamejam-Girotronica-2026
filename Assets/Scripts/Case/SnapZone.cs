using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public class SnapZone : MonoBehaviour
{
    public DraggableObject Occupant { get; set; }
    public bool isFree => Occupant == null;
    public Vector3 Position => transform.position;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.TryGetComponent(out SnapTip tip) && !tip.ZonesInside.Contains(this))
            tip.ZonesInside.Add(this);
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.TryGetComponent(out SnapTip tip))
            tip.ZonesInside.Remove(this);
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(transform.position, GetComponent<CircleCollider2D>().radius);
    }
}
