using UnityEngine;

public enum UpgradeType
{
    MoveSpeed,
    MaxTimeBonus,
    DamageReduction
}

[CreateAssetMenu(menuName = "Game/Upgrade Data", fileName = "UpgradeData")]
public class UpgradeData : ScriptableObject
{
    public UpgradeType type;
    public string displayName = "Wealth";
    public string description = "MASSIVE increasese";

    // costs are in seconds, duh
    public int baseCost = 30;
    public int costPerLevel = 15;
    public int maxLevel = 1;

    public int GetCost(int currentLevel) => baseCost + costPerLevel * currentLevel;
}
