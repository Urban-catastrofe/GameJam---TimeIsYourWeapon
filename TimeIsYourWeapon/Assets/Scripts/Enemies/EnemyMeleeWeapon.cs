using UnityEngine;

// Melee-aanval voor de boss (zwaardslag).
// Zet dit op hetzelfde object als EnemyAI (het bovenste object van de boss).
// EnemyAI roept Strike() aan op het Animation Event "OnShootFrame" in de slash-animatie.
// Staat de speler op dat moment binnen bereik en voor de boss, dan wordt hij geraakt
// via PlayerController.OnBulletHit(), precies zoals bij een kogel van de Grunt.
public class EnemyMeleeWeapon : MonoBehaviour
{
    [Header("Slag")]
    [SerializeField] private float reach = 7f;              // hoe ver het zwaard reikt (meters, vanaf het midden van de boss)
    [SerializeField, Range(0f, 360f)]
    private float arcAngle = 120f;                          // breedte van de slag voor de boss (graden)
    [SerializeField] private float maxHeightDifference = 4f; // speler mag niet ver boven/onder de boss staan

    [Header("Extra straf (optioneel)")]
    [Tooltip("Extra seconden die er bovenop een normale treffer afgaan. 0 = net zoveel als een kogel.")]
    [SerializeField] private float extraSecondsRemoved = 0f;

    [Header("Testen")]
    [SerializeField] private bool debugLog = false;

    private EnemyAI enemyAI;
    private PlayerController player;

    private void Awake()
    {
        enemyAI = GetComponent<EnemyAI>();
    }

    // De speler wordt pas opgezocht als hij nodig is: de LevelManager kan de boss
    // spawnen voordat de speler bestaat.
    private bool ResolvePlayer()
    {
        if (player != null) return true;

        if (enemyAI != null && enemyAI.player != null)
            player = enemyAI.player.GetComponentInParent<PlayerController>();

        if (player == null)
            player = FindAnyObjectByType<PlayerController>();

        return player != null;
    }

    public void Strike()
    {
        if (enemyAI != null && enemyAI.IsDead) return;

        if (!ResolvePlayer())
        {
            Debug.LogWarning("[EnemyMeleeWeapon] Geen PlayerController gevonden in de scene.", this);
            return;
        }

        Vector3 toPlayer = player.transform.position - transform.position;
        float heightDiff = Mathf.Abs(toPlayer.y);
        toPlayer.y = 0f;

        float distance = toPlayer.magnitude;
        float angle = Vector3.Angle(transform.forward, toPlayer);

        bool hit = distance <= reach
                   && angle <= arcAngle * 0.5f
                   && heightDiff <= maxHeightDifference;

        if (debugLog)
            Debug.Log($"[EnemyMeleeWeapon] Slag: afstand {distance:0.0}/{reach}, hoek {angle:0}/{arcAngle * 0.5f:0}, " +
                      $"hoogte {heightDiff:0.0} -> {(hit ? "RAAK" : "mis")}");

        if (!hit) return;

        // Zelfde effect als een kogel: tijd eraf + OnPlayerHit-event (animatie enz.)
        player.OnBulletHit();

        if (extraSecondsRemoved > 0f && TimeManager.Instance != null)
            TimeManager.Instance.RemoveSeconds(extraSecondsRemoved);
    }

    // Laat het bereik van de slag zien in de Scene-view als de boss geselecteerd is.
    private void OnDrawGizmosSelected()
    {
        Vector3 pos = transform.position + Vector3.up * 0.2f;
        Gizmos.color = new Color(1f, 0.5f, 0f);

        Vector3 left = Quaternion.Euler(0f, -arcAngle * 0.5f, 0f) * transform.forward;
        Vector3 right = Quaternion.Euler(0f, arcAngle * 0.5f, 0f) * transform.forward;
        Gizmos.DrawLine(pos, pos + left * reach);
        Gizmos.DrawLine(pos, pos + right * reach);

        const int segments = 20;
        Vector3 prev = pos + left * reach;
        for (int i = 1; i <= segments; i++)
        {
            float a = -arcAngle * 0.5f + arcAngle * i / segments;
            Vector3 next = pos + (Quaternion.Euler(0f, a, 0f) * transform.forward) * reach;
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}