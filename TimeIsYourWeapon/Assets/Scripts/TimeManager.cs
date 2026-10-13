using System;
using System.Collections;
using UnityEngine;

public class TimeManager : MonoBehaviour
{
    public float secondsRemaining = 120f;
    public float durationKilledState = 3.0f;
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
                ChangeTimeScale(0);
                break;
            case TimeScaleState.Moving:
                ChangeTimeScale(1.0f);
                break;
            case TimeScaleState.Rotating:
                ChangeTimeScale(0.5f);
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
        ChangeTimeScale(1.0f);
    }

    public enum TimeScaleState
    {
        Standing,
        Moving,
        Rotating,
        Killed
    }
}
