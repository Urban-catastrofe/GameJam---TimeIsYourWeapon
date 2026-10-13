using System;
using System.Collections;
using UnityEngine;

public class TimeManager : MonoBehaviour
{
    public float secondsRemaining = 120f;
    public float durationKilledState = 3.0f;
    public float timeScaleStandingModifier = 0.25f;
    public float timeScaleMovingModifier = 1.0f;
    public float timeScaleRotatingModifier = 0.5f;
    public float timeScaleKilledModifier = 0.2f;
    #region Events

    public static event Action<float> OnTimeScaleChange;
    public static event Action<float> OnTimeRemainingChange;
    public static event Action<float> OnTimerModified;
    public static event Action OnTimerEnded; // TODO go to end screen when triggered

    #endregion
    
    private float originalFixedDeltaTime;
    private TimeScaleState state;

    public static TimeManager Instance;

    void Awake()
    {
        Instance = this;
        
        originalFixedDeltaTime = Time.fixedDeltaTime;
    }

    void Start()
    {
        StartCoroutine(StartCountdown());
        
        OnTimeScaleChange?.Invoke(1.0f);
        OnTimeRemainingChange?.Invoke(secondsRemaining);
    }

    private IEnumerator StartCountdown()
    {
        while (secondsRemaining > 0f)
        {
            yield return new WaitForSeconds(0.1f);
            
            secondsRemaining -= 0.1f;
            OnTimeRemainingChange?.Invoke(secondsRemaining);
        }
        
        OnTimerEnded?.Invoke();
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
        if (Mathf.Approximately(timeScale, Time.timeScale)) return;
        
        // Source: https://docs.unity3d.com/6000.6/Documentation/ScriptReference/Time-timeScale.html
        Time.timeScale = timeScale;
        Time.fixedDeltaTime = originalFixedDeltaTime * timeScale;
        
        OnTimeScaleChange?.Invoke(timeScale);
    }

    public void SetTimeScaleState(TimeScaleState state)
    {
        if (this.state == state) return;
        if (this.state == TimeScaleState.Killed) return; // Cannot switch state if state is Killed

        this.state = state;

        switch (state)
        {
            case TimeScaleState.Standing:
                ChangeTimeScale(timeScaleStandingModifier);
                break;
            case TimeScaleState.Moving:
                ChangeTimeScale(timeScaleMovingModifier);
                break;
            case TimeScaleState.Rotating:
                ChangeTimeScale(timeScaleRotatingModifier);
                break;
            case TimeScaleState.Killed:
                ChangeTimeScale(timeScaleKilledModifier);
                StartCoroutine(EndKilledState());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state, null);
        }
    }

    private IEnumerator EndKilledState()
    {
        yield return new WaitForSeconds(durationKilledState * timeScaleKilledModifier);
        state = TimeScaleState.Moving;
        ChangeTimeScale(timeScaleMovingModifier);
    }

    public enum TimeScaleState
    {
        Standing,
        Moving,
        Rotating,
        Killed
    }
}
