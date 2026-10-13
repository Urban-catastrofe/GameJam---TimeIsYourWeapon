using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class CasinoBehavior : MonoBehaviour
{
    public PlayerData player;
    public UpgradeInventory inventory;
    public List<UpgradeData> upgrades;

    private string mainMenu = "MainScene";
    private int minTimeLeft = 1; // you cant spend or gamble yourself to death in the casino at least

    private int _uiVersion = 0;
    private Label timeLabel;
    private Label stakeLabel;
    private Label resultLabel;
    private SliderInt stakeSlider;
    private Button coinFlipButton;
    private Button diceButton;
    private ScrollView shopList;

    private void Awake()
    {
        GetComponent<PanelRenderer>().RegisterUIReloadCallback(OnUIReload);
        player.TimeChanged += OnTimeChanged;
    }

    private void OnDestroy()
    {
        GetComponent<PanelRenderer>().UnregisterUIReloadCallback(OnUIReload);
        player.TimeChanged -= OnTimeChanged;
    }

    // Has to be this way bc of UIToolkit
    private void OnUIReload(PanelRenderer panelRenderer, VisualElement root, int version)
    {
        // to prevent duplicate events
        if (_uiVersion == version)
            return;

        _uiVersion = version;

        timeLabel = root.Q<Label>("TimeLabel");
        stakeLabel = root.Q<Label>("StakeLabel");
        resultLabel = root.Q<Label>("ResultLabel");
        stakeSlider = root.Q<SliderInt>("StakeSlider");
        coinFlipButton = root.Q<Button>("CoinFlipButton");
        diceButton = root.Q<Button>("DiceButton");
        shopList = root.Q<ScrollView>("ShopList");

        root.Q<Button>("ReturnButton").clicked += LoadMainMenu;
        coinFlipButton.clicked += GambleCoinFlip;
        diceButton.clicked += GambleDiceRoll;
        stakeSlider.RegisterCallback<ChangeEvent<int>>((evt) => { UpdateStakeLabel(evt.newValue); });

        resultLabel.text = "";
        RefreshAll();
    }

    private void OnTimeChanged(int oldValue, int newValue)
    {
        // happens before the UI is loaded the first time
        if (timeLabel == null)
            return;

        RefreshAll();
    }


    private void GambleCoinFlip()
    {
        Gamble(CasinoGambling.CoinFlip(stakeSlider.value), "Coin flip");
    }

    private void GambleDiceRoll()
    {
        Gamble(CasinoGambling.DiceRoll(stakeSlider.value), "Dice roll");
    }

    private void Gamble(GambleResult result, string gameName)
    {
        if (result.change > 0)
        {
            player.Recover(result.change);
            resultLabel.text = $"{gameName}: {result.description} You WIN! {FormatTime(result.change)}!!!";
        }
        else if (result.change < 0)
        {
            player.Damage(-result.change);
            resultLabel.text = $"{gameName}: {result.description} You lose {FormatTime(-result.change)}...";
        }
        else
        {
            resultLabel.text = $"{gameName}: {result.description} Nobody wins. boo";
        }
    }


    private void BuyUpgrade(UpgradeData upgrade)
    {
        int cost = upgrade.GetCost(inventory.GetLevel(upgrade.type));

        if (inventory.IsMaxed(upgrade) || !CanAfford(cost))
            return;

        // level first bc paying starts the time changed event which redraws the shop
        inventory.AddLevel(upgrade);
        player.Damage(cost);
        resultLabel.text = $"Bought {upgrade.displayName} for {FormatTime(cost)}.";
    }

    private VisualElement BuildShopRow(UpgradeData upgrade)
    {
        // we make this during runtime bc that allows for easy expension (as if that will happen lol (it is cool tho))
        int level = inventory.GetLevel(upgrade.type);
        bool maxed = inventory.IsMaxed(upgrade);
        int cost = upgrade.GetCost(level);

        var row = new VisualElement();
        row.AddToClassList("shop-row");

        var info = new VisualElement();
        info.AddToClassList("shop-info");

        var nameLabel = new Label($"{upgrade.displayName}  ({level}/{upgrade.maxLevel})");
        nameLabel.AddToClassList("shop-name");
        info.Add(nameLabel);

        var descriptionLabel = new Label(upgrade.description);
        descriptionLabel.AddToClassList("shop-description");
        info.Add(descriptionLabel);

        row.Add(info);

        var buyButton = new Button(() => BuyUpgrade(upgrade));
        buyButton.AddToClassList("button");
        buyButton.AddToClassList("casino-button");
        buyButton.AddToClassList("shop-buy");
        buyButton.text = maxed ? "Owned" : FormatTime(cost);
        buyButton.SetEnabled(!maxed && CanAfford(cost));
        row.Add(buyButton);

        return row;
    }


    private void RefreshAll()
    {
        timeLabel.text = FormatTime(player.Current);

        shopList.contentContainer.Clear();
        foreach (UpgradeData upgrade in upgrades)
            shopList.Add(BuildShopRow(upgrade));

        // slider can only go as high as max time; updates every gamba
        int maxStake = Mathf.Max(1, player.Current - minTimeLeft);
        stakeSlider.highValue = maxStake;
        stakeSlider.value = Mathf.Clamp(stakeSlider.value, 1, maxStake);
        UpdateStakeLabel(stakeSlider.value);

        bool canGamble = CanAfford(1);
        coinFlipButton.SetEnabled(canGamble);
        diceButton.SetEnabled(canGamble);
    }

    private void UpdateStakeLabel(int stake)
    {
        stakeLabel.text = $"Stake: {FormatTime(stake)}";
    }

    private bool CanAfford(int amount)
    {
        return player.Current - amount >= minTimeLeft;
    }

    private static string FormatTime(int seconds)
    {
        return TimeSpan.FromSeconds(Mathf.Max(0, seconds)).ToString(@"m\:ss");
    }

    private void LoadMainMenu()
    {
        SceneManager.LoadScene(mainMenu);
    }
}
