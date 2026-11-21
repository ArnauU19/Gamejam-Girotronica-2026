using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Options panel: master / music / SFX volume sliders (0..1).
/// Values live in the AudioManager (and PlayerPrefs). If there is no AudioManager in the scene,
/// the values are only saved to PlayerPrefs and applied the next time one is loaded.
/// </summary>
public class OptionsMenu : MonoBehaviour
{
    [Header("Sliders")]
    public Slider masterSlider;
    public Slider musicSlider;
    public Slider sfxSlider;

    [Header("Value labels (optional)")]
    public Text masterValueText;
    public Text musicValueText;
    public Text sfxValueText;

    private bool listenersAdded;

    private void Awake()
    {
        AddListeners();
    }

    private void OnEnable()
    {
        AddListeners();
        Refresh();
    }

    private void AddListeners()
    {
        if (listenersAdded) return;
        listenersAdded = true;

        if (masterSlider != null) masterSlider.onValueChanged.AddListener(OnMasterChanged);
        if (musicSlider != null) musicSlider.onValueChanged.AddListener(OnMusicChanged);
        if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(OnSfxChanged);
    }

    // Shows the current values without triggering the callbacks
    private void Refresh()
    {
        AudioManager am = AudioManager.Instance;

        float master = am != null ? am.MasterVolume : PlayerPrefs.GetFloat(AudioManager.MasterVolumeKey, 1f);
        float music = am != null ? am.MusicVolume : PlayerPrefs.GetFloat(AudioManager.MusicVolumeKey, 1f);
        float sfx = am != null ? am.SfxVolume : PlayerPrefs.GetFloat(AudioManager.SfxVolumeKey, 1f);

        if (masterSlider != null) masterSlider.SetValueWithoutNotify(master);
        if (musicSlider != null) musicSlider.SetValueWithoutNotify(music);
        if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(sfx);

        SetLabel(masterValueText, master);
        SetLabel(musicValueText, music);
        SetLabel(sfxValueText, sfx);
    }

    private void OnMasterChanged(float value)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.SetMasterVolume(value);
        else PlayerPrefs.SetFloat(AudioManager.MasterVolumeKey, value);
        SetLabel(masterValueText, value);
    }

    private void OnMusicChanged(float value)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.SetMusicVolume(value);
        else PlayerPrefs.SetFloat(AudioManager.MusicVolumeKey, value);
        SetLabel(musicValueText, value);
    }

    private void OnSfxChanged(float value)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.SetSfxVolume(value);
        else PlayerPrefs.SetFloat(AudioManager.SfxVolumeKey, value);
        SetLabel(sfxValueText, value);
    }

    private static void SetLabel(Text label, float value)
    {
        if (label != null) label.text = Mathf.RoundToInt(value * 100f) + "%";
    }
}
