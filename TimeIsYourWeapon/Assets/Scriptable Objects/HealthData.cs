using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Player Data", fileName = "PlayerData")]
public class PlayerData : ScriptableObject
{
    public int maxTime = 100;

    // NonSerialized so playmode changes dont carry over
    [NonSerialized] private int time;

    public event Action<int, int> TimeChanged; // old, and current time amount
    public int Current => time;

    private void OnEnable() => time = maxTime; // for now, this will do

    public void SetTime(int value)
    {
        int old = time;
        time = value;
        TimeChanged?.Invoke(old, time);
    }

    public void Damage(int damage) => SetTime(time - damage);
    public void Recover(int heal) => SetTime(time + heal);
}