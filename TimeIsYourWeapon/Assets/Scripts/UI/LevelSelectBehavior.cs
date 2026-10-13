using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[Serializable] // idk man chatgpt innit
public class LevelEntry
{
    public string displayName; // text on the button
    public string sceneName;   // Actual name of scene in unity also ADD TO BUILD
}

public class LevelSelectBehavior : MonoBehaviour
{
    public List<LevelEntry> levels;

    private string mainMenu = "MainScene";
    private int _uiVersion = 0;
    private string gridName = "LevelGrid";
    private string buttonName = "ReturnButton";

    private void Awake()
    {
        GetComponent<PanelRenderer>().RegisterUIReloadCallback(OnUIReload);
    }

    private void OnDestroy()
    {
        GetComponent<PanelRenderer>().UnregisterUIReloadCallback(OnUIReload);
    }

    private void OnUIReload(PanelRenderer panelRenderer, VisualElement root, int version)
    {
        // to prevent duplicate events
        if (_uiVersion == version)
            return;

        _uiVersion = version;

        root.Q<Button>(buttonName).clicked += LoadMainMenu;

        // build the grid, one button per level
        var grid = root.Q<VisualElement>(gridName);
        grid.Clear();
        foreach (LevelEntry level in levels)
        {
            var levelButton = new Button(() => LoadLevel(level.sceneName));
            levelButton.AddToClassList("button");
            levelButton.AddToClassList("level-button");
            levelButton.text = level.displayName;
            grid.Add(levelButton);
        }
    }

    private void LoadLevel(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    private void LoadMainMenu()
    {
        SceneManager.LoadScene(mainMenu);
    }
}
