using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider2D))]
public class DraggableObject : MonoBehaviour
{
    public void OnGrab()
    {
        Debug.Log("Me tienes");
    }

    public void OnRelease()
    {
        Debug.Log("Me sueltas");
    }
}
