using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class SettingsBehavior : MonoBehaviour
{
    public AudioMixer mixer;
    
    private string mainMenu = "MainMenu";
    private int _uiVersion = 0;
    private string buttonName = "ReturnButton";
    private string sliderName = "MasterAudioSlider";
    private string mainVolume = "MainVolume"; 
    
    
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
        
        // init it right place
        mixer.GetFloat(mainVolume, out float dB);
        root.Q<Slider>(sliderName).value = 100 - dB * 0.8f;
        
        root.Q<Button>(buttonName).clicked += LoadMainMenu;
        root.Q<Slider>(sliderName).RegisterCallback<ChangeEvent<float>>((evt) => { setVolume(evt.newValue); });
    }

    private void LoadMainMenu()
    {
        Debug.Log("werkt");
        
        // SceneManager.LoadScene(mainMenu);
    }


    private void setVolume(float input)
    {
        float dB = Mathf.Min(0.8f * input - 80f, 0f);
        mixer.SetFloat("MainVolume", dB);
        Debug.Log(dB);
        
    }
}
