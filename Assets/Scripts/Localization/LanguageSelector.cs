using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "&lt;  English  &gt;" carousel of the options panel: the arrows cycle English -> Català -> Castellano.
/// Built by Girotronica > UI > Add Help + Language UI.
/// </summary>
public class LanguageSelector : MonoBehaviour
{
    public Text valueText;
    public Button previousButton;
    public Button nextButton;

    void Awake()
    {
        if (previousButton != null) previousButton.onClick.AddListener(() => Loc.Cycle(-1));
        if (nextButton != null) nextButton.onClick.AddListener(() => Loc.Cycle(1));
    }

    void OnEnable()
    {
        Loc.Changed += Refresh;
        Refresh();
    }

    void OnDisable() => Loc.Changed -= Refresh;

    int baseSize;

    void Refresh()
    {
        if (valueText == null) return;
        if (baseSize == 0) baseSize = valueText.fontSize;
        valueText.text = Loc.LanguageName(Loc.Current);
        // shrink a little if the name does not fit between the arrows
        valueText.fontSize = baseSize;
        float width = valueText.rectTransform.rect.width - 8f;
        while (width > 0f && valueText.fontSize > baseSize * 0.7f && valueText.preferredWidth > width) valueText.fontSize--;
    }
}
