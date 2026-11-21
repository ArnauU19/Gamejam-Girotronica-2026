using UnityEngine;

/// <summary>
/// A comic panel. Its Enter/Exit triggers (PanelTrigger children) report here.
/// Later this is the place to change the character when entering/leaving a panel.
/// </summary>
public class Panel : MonoBehaviour
{
    public void PlayerEntered(SurfaceWalker walker)
    {
        Debug.Log($"[Panel] Player ENTERED panel '{name}'", this);
        walker.OnEnteredPanel(this);
    }

    public void PlayerExited(SurfaceWalker walker)
    {
        Debug.Log($"[Panel] Player EXITED panel '{name}'", this);
        walker.OnExitedPanel(this);
    }
}
