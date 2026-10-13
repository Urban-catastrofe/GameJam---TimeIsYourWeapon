using System;
using System.Collections;
using UnityEngine;

public class TimeManager : MonoBehaviour
{
    public float secondsRemaining = 120f;
    #region Events

    public static event Action<float> OnTimeScaleChange;
    public static event Action<float> OnTimeRemainingChange;
    public static event Action<float> OnTimerModified;

    #endregion
    
    private float originalFixedDeltaTime;

    void Awake()
    {
        originalFixedDeltaTime = Time.fixedDeltaTime;
        
        OnTimeScaleChange?.Invoke(1.0f);
        OnTimeRemainingChange?.Invoke(secondsRemaining);
    }

    void Start()
    {
        StartCoroutine(StartCountdown());
    }

    private IEnumerator StartCountdown()
    {
        while (secondsRemaining > 0f)
        {
            yield return new WaitForSeconds(0.1f);
            
            secondsRemaining -= 0.1f;
            OnTimeRemainingChange?.Invoke(secondsRemaining);
        }
    }

    public void AddSeconds(float seconds)
    {
        secondsRemaining += seconds;
        OnTimeRemainingChange?.Invoke(secondsRemaining);
        OnTimerModified?.Invoke(seconds);
    }

    public void RemoveSeconds(float seconds) => AddSeconds(-seconds);
    
    public void ChangeTimeScale(float timeScale)
    {
        // Source: https://docs.unity3d.com/6000.6/Documentation/ScriptReference/Time-timeScale.html
        Time.timeScale = timeScale;
        Time.fixedDeltaTime = originalFixedDeltaTime * timeScale;
        
        OnTimeScaleChange?.Invoke(timeScale);
    }
}
