using UnityEngine;

/// <summary>
/// Root of a comic panel. Groups its children:
///  - Frame     : LineRenderer with the border of the panel.
///  - Floor     : FloorPath (EdgeCollider2D + LineRenderer).
///  - PanelArea : PanelTrigger, the trigger tagged "panel" that tells the character it is inside.
/// </summary>
public class Panel : MonoBehaviour
{
}
