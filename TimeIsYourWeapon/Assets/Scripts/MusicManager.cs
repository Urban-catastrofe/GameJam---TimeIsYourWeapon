using UnityEngine;
using UnityEngine.SceneManagement;

// Plays background music that keeps going across scene loads.
// Menu scenes (main menu, level select, settings, casino) share the menu theme without restarting it;
// the level scenes switch to the levels theme. The track only restarts when the clip actually changes.
// Put one in the main menu. Extra copies (e.g. in a level for playtesting) remove themselves.
[RequireComponent(typeof(AudioSource))]
public class MusicManager : MonoBehaviour
{
    [SerializeField] AudioClip menuTheme;
    [SerializeField] AudioClip levelsTheme;
    [SerializeField] string[] levelScenes = { "Level 1", "Level 2", "BossArena" };
    [SerializeField, Range(0f, 1f)] float volume = 0.4f;

    static MusicManager instance;
    AudioSource source;

    void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        source = GetComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = volume;

        SceneManager.sceneLoaded += OnSceneLoaded;
        PlayFor(SceneManager.GetActiveScene().name);
    }

    void OnDestroy()
    {
        if (instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => PlayFor(scene.name);

    void PlayFor(string sceneName)
    {
        AudioClip clip = System.Array.IndexOf(levelScenes, sceneName) >= 0 ? levelsTheme : menuTheme;
        if (clip == null || (source.clip == clip && source.isPlaying)) return;

        source.clip = clip;
        source.Play();
    }
}
