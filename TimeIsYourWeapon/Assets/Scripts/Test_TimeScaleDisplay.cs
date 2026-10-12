using TMPro;
using UnityEngine;

public class TimeScaleDisplay : MonoBehaviour
{
    public TMP_Text timeScaleText;

    private void OnEnable()
    {
        TimeScaleManager.OnTimeScaleChange += UpdateTimeScale;
        
        UpdateTimeScale(Time.timeScale);
    }

    private void OnDisable()
    {
        TimeScaleManager.OnTimeScaleChange -= UpdateTimeScale;
    }

    private void UpdateTimeScale(float timeScale)
    {
        timeScaleText.text = $"Time Scale: {timeScale:F2}x";
    }
}