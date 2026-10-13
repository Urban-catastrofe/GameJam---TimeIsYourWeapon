using System.Collections.Generic;
using UnityEngine;


// largely unimplemented obviously. wordt ook nooit implemented lol
[CreateAssetMenu(menuName = "Game/Upgrade Inventory", fileName = "UpgradeInventory")]
public class UpgradeInventory : ScriptableObject
{
    private Dictionary<UpgradeType, int> levels = new Dictionary<UpgradeType, int>();

    private void OnEnable()
    {
        levels = new Dictionary<UpgradeType, int>();
    } // for now, this will do. will override the upgrades lol

    // gameplay scripts can call this to check what the player has ex. inventory.GetLevel(UpgradeType.MoveSpeed) and need this for shop
    public int GetLevel(UpgradeType type) => levels.TryGetValue(type, out int level) ? level : 0;
    // need this for shop
    public bool IsMaxed(UpgradeData upgrade) => GetLevel(upgrade.type) >= upgrade.maxLevel;
    // need this for shop
    public void AddLevel(UpgradeData upgrade)
    {
        int newLevel = GetLevel(upgrade.type) + 1;
        levels[upgrade.type] = newLevel;
    }


    private void ApplyMoveSpeed(int level)
    {
        // todo
    }

    private void ApplyMaxTimeBonus(int level)
    {
        // todo
    }

    private void ApplyDamageReduction(int level)
    {
        // todo
    }
}
