using System;
using UnityEngine;
using UnityEngine.UIElements;
using Random = UnityEngine.Random;

public class HealthHUD : MonoBehaviour
{
    public PlayerData player;
    
    private int _uiVersion;
    private Label timeLabel;
    private VisualElement floatLayer;

    private void Awake()
    {
        GetComponent<PanelRenderer>().RegisterUIReloadCallback(OnUIReload);
        player.TimeChanged += OnHealthChanged;
    }

    private void OnDestroy()
    {
        GetComponent<PanelRenderer>().UnregisterUIReloadCallback(OnUIReload);
        player.TimeChanged -= OnHealthChanged;
    }

    private void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
    {
        if (_uiVersion == version)
            return;
            
        _uiVersion = version;
        
        timeLabel = root.Q<Label>("time-label");
        floatLayer = root.Q<VisualElement>("float-layer");
        RefreshLabel();
    }

    private void OnHealthChanged(int oldValue, int newValue)
    {
        RefreshLabel();
        if (newValue < oldValue)
            SpawnNegativeFloatingNumber(oldValue - newValue);
    }

    private void RefreshLabel()
    {
        if (player.Current < 0)
        {
            // need this bc TimeSpan.FromSeconds cannot do negative numbers
            timeLabel.text = "0:00";
            return;
        }
        string minuteNotation = TimeSpan.FromSeconds(player.Current).ToString(@"m\:ss");
        timeLabel.text = $"{minuteNotation}";
    }

    private void SpawnNegativeFloatingNumber(int amount)
    {
        string minuteNotation = TimeSpan.FromSeconds(amount).ToString(@"m\:ss");
        var floatingLabel = new Label($"-{minuteNotation}");
        floatingLabel.AddToClassList("damage-number");

        Rect time = timeLabel.worldBound;
        Vector2 local = floatLayer.WorldToLocal(new Vector2(time.center.x, time.yMin));
        floatingLabel.style.left = local.x+ Random.Range(-30f, 30f);
        floatingLabel.style.top = local.y - 20f - Random.Range(0f, 20f);

        floatLayer.Add(floatingLabel);

        // had to add slight delay 
        floatingLabel.schedule.Execute(() => floatingLabel.AddToClassList("damage-number--float")).StartingIn(30);
        // after 1s they get DESTROYED
        floatingLabel.schedule.Execute( floatingLabel.RemoveFromHierarchy).StartingIn(1000);
    }
}