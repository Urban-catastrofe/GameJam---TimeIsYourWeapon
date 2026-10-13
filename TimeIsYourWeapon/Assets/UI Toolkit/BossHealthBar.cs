using UnityEngine;
using UnityEngine.UIElements;

public class BossHealthBar : MonoBehaviour
{
    public string bossName = "MegaClanker";
    public float hideAfterDeath = 2f;

    private int uiVersion = 0;
    private VisualElement root;
    private VisualElement fill;
    private VisualElement trail;
    private Label nameLabel;

    private EnemyAI boss;
    private float percent = 1f;

    private void Awake()
    {
        GetComponent<PanelRenderer>().RegisterUIReloadCallback(OnUIReload);
    }

    private void OnDestroy()
    {
        GetComponent<PanelRenderer>().UnregisterUIReloadCallback(OnUIReload);
        if (boss != null)
            boss.OnHealthChanged -= OnHealthChanged;
    }

    
    private void Update()
    {
        // in update bc might not be set frame 1
        if (boss != null || LevelManager.Instance == null) return;

        GameObject bossObject = LevelManager.Instance.BossInstance;
        if (bossObject == null) return;

        boss = bossObject.GetComponent<EnemyAI>();
        if (boss == null) return;

        boss.OnHealthChanged += OnHealthChanged;
        percent = boss.HealthPercent;
        Refresh();
    }

    private void OnUIReload(PanelRenderer renderer, VisualElement uiRoot, int version)
    {
        if (uiVersion == version)
            return;

        uiVersion = version;

        root = uiRoot.Q<VisualElement>("BossHUDRoot");
        fill = uiRoot.Q<VisualElement>("BossBarFill");
        trail = uiRoot.Q<VisualElement>("BossBarTrail");
        nameLabel = uiRoot.Q<Label>("BossNameLabel");

        Refresh();
    }

    private void OnHealthChanged(float healthPercent)
    {
        percent = healthPercent;
        Refresh();

        if (percent <= 0f)
        {
            boss.OnHealthChanged -= OnHealthChanged;
            root?.schedule.Execute(() => root.RemoveFromClassList("is-visible"))
                .StartingIn((long)(hideAfterDeath * 1000f));
        }
    }

    private void Refresh()
    {
        nameLabel.text = bossName;

        Length width = Length.Percent(Mathf.Clamp01(percent) * 100f);
        if (fill != null) fill.style.width = width;
        if (trail != null) trail.style.width = width;

        // Only show the bar while there's a living boss
        if (boss != null && percent > 0f)
            root.AddToClassList("is-visible");
    }
}
