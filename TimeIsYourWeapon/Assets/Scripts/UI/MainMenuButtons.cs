using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

namespace UI
{
    public class MainMenuButtons : MonoBehaviour
    {
        private VisualElement _root;
        public string gameScene = "level";
        public string settingsScene = "settings";
        public string casinoScene = "casino";
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
            Debug.Log("Not implemented");
            // SceneManager.LoadScene(gameScene);
        }

        public void LoadCasinoScene()
        {
            SceneManager.LoadScene(casinoScene);
        }

        public void LoadSettingsScene()
        {
            Debug.Log("Not implemented");
            // SceneManager.LoadScene(settingsScene);
        }

        public void ExitGame()
        {
            Application.Quit();
        }
    }
}
