using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

namespace UI
{
    public class MainMenuButtons : MonoBehaviour
    {
        private VisualElement _root;
        private string levelSelectScene = "Level Select";
        private string settingsScene = "Settings";
        private string casinoScene = "Casino";
        private int _uiVersion = 0;
        
        void Awake()
        {
            GetComponent<PanelRenderer>().RegisterUIReloadCallback(OnUIReload);
        }

        void OnDestroy()
        {
            GetComponent<PanelRenderer>().UnregisterUIReloadCallback(OnUIReload);
        }

        // Has to be this way bc of UIToolkit
        private void OnUIReload(PanelRenderer panelRenderer, VisualElement root, int version)
        {
            // to prevent duplicate events
            if (_uiVersion == version)
                return;
            
            _uiVersion = version;
            
            
            BindButton(root, "StartButton", LoadGameScene);
            BindButton(root, "QuitButton", ExitGame);
            BindButton(root, "SettingsButton", LoadSettingsScene);
            BindButton(root, "CasinoButton", LoadCasinoScene);
        }

        private void BindButton(
            VisualElement root,
            string buttonName,
            System.Action action)
        {
            var button = root.Q<Button>(buttonName);

            if (button == null)
            {
                Debug.LogError("Button not found: " + buttonName);
                return;
            }
            button.clicked += action;
        }
        
        public void LoadGameScene()
        {
            SceneManager.LoadScene(levelSelectScene);
        }

        public void LoadCasinoScene()
        {
            SceneManager.LoadScene(casinoScene);
        }

        public void LoadSettingsScene()
        {
            SceneManager.LoadScene(settingsScene);
        }

        public void ExitGame()
        {
            Application.Quit();
        }
    }
}
