using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

public class GameOverScreen : MonoBehaviour
{
    public static GameOverScreen Instance { get; private set; }

    public string mainMenuScene = "MainScene";

    private int uiVersion = 0;
    private VisualElement root;
    private Label statsLabel;
    private bool isShown;

    private void Awake()
    {
        Instance = this;
        GetComponent<PanelRenderer>().RegisterUIReloadCallback(OnUIReload);
    }

    private void OnDestroy()
    {
        GetComponent<PanelRenderer>().UnregisterUIReloadCallback(OnUIReload);
        if (Instance == this)
            Instance = null;
    }

    private void OnUIReload(PanelRenderer renderer, VisualElement uiRoot, int version)
    {
        // to prevent duplicate events
        if (uiVersion == version)
            return;

        uiVersion = version;

        root = uiRoot.Q<VisualElement>("GameOverRoot");
        statsLabel = uiRoot.Q<Label>("GameOverStats");

        var button = uiRoot.Q<Button>("MainMenuButton");
        if (button != null)
            button.clicked += LoadMainMenu;
        else
            Debug.LogError("Button not found: MainMenuButton");

        Refresh();
    }

    public void Show()
    {
        if (isShown) return;
        isShown = true;

        // Stop the player from moving, looking around or shooting behind the screen
        var player = FindAnyObjectByType<PlayerController>();
        if (player != null)
        {
            player.enabled = false;
            foreach (var weapon in player.GetComponentsInChildren<WeaponController>())
                weapon.enabled = false;
        }

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Refresh();
    }

    private void Refresh()
    {
        if (root == null) return;

        if (statsLabel != null && LevelManager.Instance != null)
            statsLabel.text = $"Enemies killed: {LevelManager.Instance.Kills} / {LevelManager.Instance.TotalEnemies}";

        if (isShown)
            root.AddToClassList("is-visible");
        else
            root.RemoveFromClassList("is-visible");
    }

    private void LoadMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuScene);
    }
}
