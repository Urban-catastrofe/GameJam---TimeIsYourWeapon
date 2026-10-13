using UnityEngine;
using UnityEngine.UIElements;

public class TimeCountdown : MonoBehaviour
{
    private int uiVersion = -1;
    private Label timerLabel;
    private Label penaltyLabel;
    private Label timeScaleLabel;

    private void Awake()
    {
        GetComponent<PanelRenderer>().RegisterUIReloadCallback(OnUIReload);
        TimeManager.OnTimeScaleChange += OnTimeScaleChanged;
        TimeManager.OnTimeRemainingChange += UpdateTimer;
        TimeManager.OnTimerModified += ShowTimeModification;
    }

    private void OnDestroy()
    {
        GetComponent<PanelRenderer>().UnregisterUIReloadCallback(OnUIReload);
        TimeManager.OnTimeScaleChange -= OnTimeScaleChanged;
        TimeManager.OnTimeRemainingChange -= UpdateTimer;
        TimeManager.OnTimerModified -= ShowTimeModification;
    }

    private void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
    {
        if (uiVersion == version)
            return;

        uiVersion = version;

        timerLabel = root.Q<Label>("TimerLabel");
        penaltyLabel = root.Q<Label>("PenaltyLabel");
        timeScaleLabel = root.Q<Label>("TimeScaleLabel");

        OnTimeScaleChanged(Time.timeScale);
    }

    private void OnTimeScaleChanged(float timeScale)
    {
        if (timeScaleLabel != null)
            timeScaleLabel.text = $"Time scale: {timeScale:F1}x";
    }

    private void UpdateTimer(float seconds)
    {
        if (timerLabel == null) return;
        
        seconds = Mathf.Max(0, seconds);

        int minutes = Mathf.FloorToInt(seconds / 60);
        float remainingSeconds = seconds % 60;

        timerLabel.text = $"{minutes:00}:{remainingSeconds:00.0}";
    }

    private void ShowTimeModification(float amount)
    {
        if (penaltyLabel == null)
            return;

        var popup = new Label(amount > 0 ? $"+{amount:F1}s" : $"{amount:F1}s");
        popup.AddToClassList("penalty-label");
        popup.style.opacity = 1f;
        if (amount > 0)
        {
            // positive
            popup.style.color = new StyleColor(new Color32(71, 255, 65, 255));
        }
        else
        {
            // negative
            popup.style.color = new StyleColor(new Color32(255, 65, 80, 255));
        }

        popup.style.rotate = new Rotate(
            new Angle(Random.Range(-12f, 12f))
        );

        popup.style.position = Position.Absolute;
        var range = 40f;
        popup.style.left = Random.Range(-range, range);
        popup.style.top = 10f;
        penaltyLabel.parent.Add(popup);

        popup.schedule.Execute(() =>
        {
            popup.style.translate = new Translate(
                0,
                100,
                0
            );
            popup.style.opacity = 0f;
        }).StartingIn(20);

        popup.schedule.Execute(popup.RemoveFromHierarchy).StartingIn(750);
    }
}
