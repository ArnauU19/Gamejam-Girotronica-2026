using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum Language { English = 0, Catalan = 1, Spanish = 2 }

/// <summary>
/// Game texts in English, Catalan and Spanish. English is the default; the choice is saved in PlayerPrefs.
///  - Loc.Get("play") returns the text in the current language.
///  - Every UI Text of a loaded scene whose content is one of the original Catalan texts gets a
///    LocalizedText automatically (no scene has to be edited), so it follows the selected language.
///  - The game title ("Arregla'l") is never translated: it is simply not in the table.
/// </summary>
public static class Loc
{
    public const string PrefsKey = "Language";
    public static readonly Language[] Order = { Language.English, Language.Catalan, Language.Spanish };

    /// <summary>Raised after the language changes. LocalizedText listens to it.</summary>
    public static event Action Changed;

    static bool loaded;
    static Language current;

    public static Language Current
    {
        get
        {
            if (!loaded)
            {
                loaded = true;
                int saved = PlayerPrefs.GetInt(PrefsKey, (int)Language.English);
                current = Enum.IsDefined(typeof(Language), saved) ? (Language)saved : Language.English;
            }
            return current;
        }
        set
        {
            if (loaded && value == current) return;
            loaded = true;
            current = value;
            PlayerPrefs.SetInt(PrefsKey, (int)value);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }

    /// <summary>Moves the language carousel left (-1) or right (+1).</summary>
    public static void Cycle(int direction)
    {
        int i = Array.IndexOf(Order, Current);
        int n = Order.Length;
        Current = Order[((i + direction) % n + n) % n];
    }

    /// <summary>Name of a language written in that same language (the way language selectors show them).</summary>
    public static string LanguageName(Language l) => l switch
    {
        Language.Catalan => "Català",
        Language.Spanish => "Castellano",
        _ => "English",
    };

    // ------------------------------------------------------------------ table
    // key -> { English, Catalan, Spanish }
    static readonly Dictionary<string, string[]> table = new Dictionary<string, string[]>
    {
        // main menu
        { "play",          new[] { "Play", "Jugar", "Jugar" } },
        { "options",       new[] { "Options", "Opcions", "Opciones" } },
        { "quit",          new[] { "Quit", "Sortir", "Salir" } },
        { "back",          new[] { "Back", "Tornar", "Volver" } },
        // options
        { "master",        new[] { "Master volume", "Volum general", "Volumen general" } },
        { "music",         new[] { "Music", "Música", "Música" } },
        { "sfx",           new[] { "Sound effects", "Efectes de so", "Efectos de sonido" } },
        { "language",      new[] { "Language", "Idioma", "Idioma" } },
        // pause
        { "pause",         new[] { "Pause", "Pausa", "Pausa" } },
        { "resume",        new[] { "Resume", "Tornar al joc", "Volver al juego" } },
        { "quitToMenu",    new[] { "Main menu", "Sortir al menú", "Salir al menú" } },
        { "pressEsc",      new[] { "Press Esc to continue", "Prem Esc per continuar", "Pulsa Esc para continuar" } },
        // level HUD / end
        { "level",         new[] { "Level {0}", "Nivell {0}", "Nivel {0}" } },
        { "thanks",        new[] { "Thanks for\nplaying!", "Gràcies\nper jugar", "Gracias\npor jugar" } },
        { "backToMenu",    new[] { "Back to menu", "Tornar al menú", "Volver al menú" } },
        // help pop-up
        { "helpTitle",     new[] { "How to play", "Com es juga", "Cómo se juega" } },
        { "helpGrab",      new[] { "Hold LEFT CLICK on a piece to pick it up and drag it.",
                                   "Mantén el CLIC ESQUERRE sobre una peça per agafar-la i arrossegar-la.",
                                   "Mantén el CLIC IZQUIERDO sobre una pieza para cogerla y arrastrarla." } },
        { "helpRotate",    new[] { "While you hold a piece, RIGHT CLICK to rotate it.",
                                   "Mentre tens una peça agafada, fes CLIC DRET per girar-la.",
                                   "Mientras tienes una pieza cogida, haz CLIC DERECHO para girarla." } },
        { "helpBridge",    new[] { "Some pieces, like the ruler, are bridges: she can walk over them. Each piece is walked in its own way!",
                                   "Algunes peces, com el regle, fan de pont: ella hi pot caminar per sobre. Cada peça es recorre a la seva manera!",
                                   "Algunas piezas, como la regla, hacen de puente: ella puede caminar por encima. ¡Cada pieza se recorre a su manera!" } },
        { "helpBounce",    new[] { "Other pieces have their own tricks: the eraser makes her bounce back!",
                                   "Altres peces tenen els seus trucs: la goma la fa rebotar!",
                                   "Otras piezas tienen sus propios trucos: ¡la goma la hace rebotar!" } },
        { "helpGotIt",     new[] { "Got it!", "Entesos!", "¡Entendido!" } },
    };

    /// <summary>Text of a key in the current language (the key itself if it is missing).</summary>
    public static string Get(string key)
    {
        if (key != null && table.TryGetValue(key, out var t))
        {
            int i = (int)Current;
            return i < t.Length && !string.IsNullOrEmpty(t[i]) ? t[i] : t[0];
        }
        return key;
    }

    public static string Format(string key, params object[] args) => string.Format(Get(key), args);

    // ------------------------------------------------------------------ automatic scene texts

    // Original (Catalan) texts of the scenes / prefabs -> key
    static Dictionary<string, string> sourceToKey;
    static readonly Regex levelRegex = new Regex(@"^\s*Nivell\s+(\d+)\s*$");

    static string Normalize(string s) => s.Replace("\r", "").Trim();

    /// <summary>Finds the key of an original scene text (Catalan). Returns false if it is not a translatable text.</summary>
    public static bool TryFindKey(string sourceText, out string key, out string arg)
    {
        key = arg = null;
        if (string.IsNullOrEmpty(sourceText)) return false;
        if (sourceToKey == null)
        {
            sourceToKey = new Dictionary<string, string>();
            foreach (var kv in table)
                foreach (var value in kv.Value)        // any language matches (Catalan scenes, or a scene already in English)
                    if (!value.Contains("{0}") && !sourceToKey.ContainsKey(Normalize(value)))
                        sourceToKey[Normalize(value)] = kv.Key;
        }
        string s = Normalize(sourceText);
        if (sourceToKey.TryGetValue(s, out key)) return true;
        var m = levelRegex.Match(s);
        if (m.Success) { key = "level"; arg = m.Groups[1].Value; return true; }
        return false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++) LocalizeScene(SceneManager.GetSceneAt(i));
        SceneManager.sceneLoaded += (scene, mode) => LocalizeScene(scene);
    }

    /// <summary>Adds a LocalizedText to every translatable Text of the scene (inactive ones too).</summary>
    public static void LocalizeScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;
        foreach (var root in scene.GetRootGameObjects())
            LocalizeHierarchy(root);
    }

    public static void LocalizeHierarchy(GameObject root)
    {
        foreach (var text in root.GetComponentsInChildren<Text>(true))
        {
            if (text.GetComponent<LocalizedText>() != null) continue;
            if (!TryFindKey(text.text, out string key, out string arg)) continue;
            var lt = text.gameObject.AddComponent<LocalizedText>();
            lt.key = key;
            lt.arg = arg;
            lt.Apply();
        }
    }
}
