using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class HealthClickTester : MonoBehaviour
{
    [SerializeField] private PlayerData player;
    [SerializeField] private int minDamage = 5;
    [SerializeField] private int maxDamage = 20;
    [SerializeField] private bool resetWhenDead = true;

    private void Update()
    {
        if (LeftClicked())
        {
            player.Damage(Random.Range(minDamage, maxDamage + 1));
        }
    }

    private static bool LeftClicked()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }
}