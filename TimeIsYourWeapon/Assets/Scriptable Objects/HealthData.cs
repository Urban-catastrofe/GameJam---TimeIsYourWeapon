using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Player Data", fileName = "PlayerData")]
public class PlayerData : ScriptableObject
{
    public int maxTime = 100; // Starting balance for a new save.

    private const string SaveKey = "Player.TotalTime";
    [NonSerialized] private int time;

    public event Action<int, int> TimeChanged;
    public int Current => time;

    private void OnEnable()
    {
        time = PlayerPrefs.GetInt(SaveKey, maxTime);
    }

    public void SetTime(int value)
    {
        int old = time;
        time = Mathf.Max(0, value);

        PlayerPrefs.SetInt(SaveKey, time);
        PlayerPrefs.Save();

        TimeChanged?.Invoke(old, time);
    }

    public void Damage(int amount) => SetTime(time - amount);
    public void Recover(int amount) => SetTime(time + amount);
}