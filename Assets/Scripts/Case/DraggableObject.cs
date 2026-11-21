using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider2D))]
public class DraggableObject : MonoBehaviour
{
    [SerializeField] private SnapTip[] tips;
    public SnapTip[] Tips => tips;

    private readonly List<SnapZone> usedZones = new();
    
    public void OccupyZone(SnapZone zone)
    {
        zone.Occupant = this;
        usedZones.Add(zone);
    }

    public void ReleaseZones()
    {
        foreach (var z in usedZones) z.Occupant = null;
        usedZones.Clear();
    }
    
    public void OnGrab()
    {
        Debug.Log("Me tienes");
    }

    public void OnRelease()
    {
        Debug.Log("Me sueltas");
    }

    public void RotateRightClick()
    {
        transform.Rotate(0, 0, 90);
    }
}
