using System;
using UnityEngine;

public class TimeScaleManager : MonoBehaviour
{
    #region Events

    public static event Action<float> OnTimeScaleChange;

    #endregion
    
    private float originalFixedDeltaTime;

    void Awake()
    {
        originalFixedDeltaTime = Time.fixedDeltaTime;
        
        OnTimeScaleChange?.Invoke(1.0f);
    }
    
    public void ChangeTimescale(float timeScale)
    {
        // Source: https://docs.unity3d.com/6000.6/Documentation/ScriptReference/Time-timeScale.html
        Time.timeScale = timeScale;
        Time.fixedDeltaTime = originalFixedDeltaTime * timeScale;
        
        OnTimeScaleChange?.Invoke(timeScale);
    }
}
