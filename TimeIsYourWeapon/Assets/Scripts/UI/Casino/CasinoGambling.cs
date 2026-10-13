using UnityEngine;

public struct GambleResult
{
    public int change; // actual change
    public string description; // msg to inform
}

public static class CasinoGambling
{
    // 50/50, double or nuthing; we always call heads
    public static GambleResult CoinFlip(int stake)
    {
        bool heads = Random.value < 0.5f;

        return new GambleResult
        {
            change = heads ? stake : -stake,
            description = heads ? "HEADS!" : "Tails..."
        };
    }

    public static GambleResult DiceRoll(int stake)
    {
        int playerRoll = Random.Range(1, 7);
        int houseRoll = Random.Range(1, 7);

        int change = 0;
        if (playerRoll > houseRoll) change = stake;
        else if (playerRoll < houseRoll) change = -stake;

        return new GambleResult
        {
            change = change,
            description = $"You rolled {playerRoll}, the house rolled {houseRoll}."
        };
    }
}
