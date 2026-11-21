using System.Collections.Generic;
using UnityEngine;

public enum Sfx
{
    Grab,
    Release,
    Eat,
    Hit,        // character gets hit (dizzy birds)
    Respawn,
}

[System.Serializable]
public class SfxEntry
{
    public Sfx id;
    public AudioClip[] clips;                       // several variants, one is picked at random
    [Range(0f, 1f)] public float volume = 1f;
    public Vector2 pitchRange = new Vector2(0.95f, 1.05f);
}

[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }
    
    [SerializeField] private AudioClip mainMenuSong;
    [SerializeField] private AudioClip levelSong;

    [Header("SFX")]
    [SerializeField] private SfxEntry[] sfxEntries;
    [SerializeField] private int sfxVoices = 8;     // sounds that can overlap

    // Volume settings (0..1), saved in PlayerPrefs
    public const string MasterVolumeKey = "volume_master";
    public const string MusicVolumeKey = "volume_music";
    public const string SfxVolumeKey = "volume_sfx";

    public float MasterVolume { get; private set; } = 1f;
    public float MusicVolume { get; private set; } = 1f;
    public float SfxVolume { get; private set; } = 1f;

    private AudioLowPassFilter lowCutFilter;
    private AudioSource audioManagerSource;

    private AudioSource[] sfxPool;
    private int nextVoice;
    private Dictionary<Sfx, SfxEntry> sfxLookup;
    private float musicBaseVolume = 1f;

    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return; // the duplicate must not keep running (music + SFX pool)
        }

        audioManagerSource = GetComponent<AudioSource>();
        lowCutFilter = GetComponent<AudioLowPassFilter>();
        musicBaseVolume = audioManagerSource.volume;

        lowCutFilter.enabled = false;

        BuildSfxPool();
        BuildSfxLookup();
        LoadVolumes();

        playLevelSong();
    }

    public void playMainMenuSong()
    {
        if (mainMenuSong == null)
            return;
        
        audioManagerSource.Stop();
        audioManagerSource.clip = mainMenuSong;
        audioManagerSource.Play();
    }

    public void playLevelSong()
    {
        if (levelSong == null)
            return;

        audioManagerSource.Stop();
        audioManagerSource.clip = levelSong;
        audioManagerSource.Play();
    }

    public void ChangeEffect()
    {
        lowCutFilter.enabled = !lowCutFilter.enabled; 
    }

    // Muffles only the music (pause menu). SFX voices are children, so they are not filtered.
    public void SetLowPass(bool on)
    {
        if (lowCutFilter != null)
            lowCutFilter.enabled = on;
    }

    public void PlaySfx(Sfx id)
    {
        if (sfxLookup == null || !sfxLookup.TryGetValue(id, out SfxEntry entry))
            return;
        if (entry.clips == null || entry.clips.Length == 0)
            return;

        AudioClip clip = entry.clips[Random.Range(0, entry.clips.Length)];
        if (clip == null)
            return;

        AudioSource voice = sfxPool[nextVoice];
        nextVoice = (nextVoice + 1) % sfxPool.Length;

        voice.clip = clip;
        voice.volume = entry.volume * SfxVolume;
        voice.pitch = Random.Range(entry.pitchRange.x, entry.pitchRange.y);
        voice.Play();
    }

    // ---------- Volume ----------
    public void SetMasterVolume(float value)
    {
        MasterVolume = Mathf.Clamp01(value);
        AudioListener.volume = MasterVolume;
        PlayerPrefs.SetFloat(MasterVolumeKey, MasterVolume);
    }

    public void SetMusicVolume(float value)
    {
        MusicVolume = Mathf.Clamp01(value);
        if (audioManagerSource != null)
            audioManagerSource.volume = musicBaseVolume * MusicVolume;
        PlayerPrefs.SetFloat(MusicVolumeKey, MusicVolume);
    }

    public void SetSfxVolume(float value)
    {
        SfxVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SfxVolumeKey, SfxVolume);
    }

    private void LoadVolumes()
    {
        SetMasterVolume(PlayerPrefs.GetFloat(MasterVolumeKey, 1f));
        SetMusicVolume(PlayerPrefs.GetFloat(MusicVolumeKey, 1f));
        SetSfxVolume(PlayerPrefs.GetFloat(SfxVolumeKey, 1f));
    }

    // The voices are children of the manager so the low pass filter
    // (which lives on the manager object) only affects the music.
    private void BuildSfxPool()
    {
        sfxPool = new AudioSource[Mathf.Max(1, sfxVoices)];
        for (int i = 0; i < sfxPool.Length; i++)
        {
            GameObject go = new GameObject($"SFX_Voice_{i}");
            go.transform.SetParent(transform);
            AudioSource src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            sfxPool[i] = src;
        }
    }

    private void BuildSfxLookup()
    {
        sfxLookup = new Dictionary<Sfx, SfxEntry>();
        if (sfxEntries == null)
            return;

        foreach (SfxEntry e in sfxEntries)
            sfxLookup[e.id] = e;
    }
}
