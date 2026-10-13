using UnityEngine;
using UnityEngine.VFX;

public class Projectile : MonoBehaviour
{
    [Header("Default Projectile Settings")]
    [SerializeField] private float speed = 20f;
    [SerializeField] private float damage = 25f;
    [SerializeField] private float lifetime = 5f;

    [Header("Collision")]
    [SerializeField] private LayerMask hitLayers = ~0;

    [Header("Impact Effect")]
    [SerializeField] private VisualEffect impactEffectPrefab;
    [SerializeField] private string impactEventName = "Impact";

    private Vector3 direction;
    private float age;

    private float currentSpeed;
    private float currentDamage;
    private float currentLifetime;

    private void Awake()
    {
        currentSpeed = speed;
        currentDamage = damage;
        currentLifetime = lifetime;
    }

    // Used by the player's WeaponController.
    public void Initialize(Vector3 shootDirection)
    {
        Initialize(shootDirection, speed, damage, lifetime);
    }

    // Used by EnemyAI to supply its own weapon settings.
    public void Initialize(
        Vector3 shootDirection,
        float projectileSpeed,
        float projectileDamage,
        float projectileLifetime)
    {
        direction = shootDirection.normalized;

        currentSpeed = projectileSpeed;
        currentDamage = projectileDamage;
        currentLifetime = projectileLifetime;

        transform.forward = direction;
    }

    private void Update()
    {
        age += Time.deltaTime;

        if (age >= currentLifetime)
        {
            Destroy(gameObject);
            return;
        }

        float distance = currentSpeed * Time.deltaTime;

        if (distance <= 0f)
            return;

        if (Physics.Raycast(
            transform.position,
            direction,
            out RaycastHit hit,
            distance,
            hitLayers,
            QueryTriggerInteraction.Ignore))
        {
            HandleHit(hit);
            return;
        }

        transform.position += direction * distance;
    }

    private void HandleHit(RaycastHit hit)
    {
        Debug.Log("Hit " + hit.collider.gameObject.name);
        // Support colliders placed on child objects of an enemy.
        EnemyAI enemy = hit.collider.GetComponentInParent<EnemyAI>();
        PlayerController pc = hit.collider.GetComponentInParent<PlayerController>();

        if (enemy != null && !enemy.IsDead)
        {
            enemy.TakeHit(hit.collider);
        }
        else if (pc != null)
        {
            pc.OnPlayerDamaged();
        }
        else
        {
            IDamageable target =
                hit.collider.GetComponentInParent<IDamageable>();

            if (target != null)
                target.TakeDamage(currentDamage);
        }

        SpawnImpact(hit.point, hit.normal);
        Destroy(gameObject);
    }

    private void SpawnImpact(Vector3 position, Vector3 normal)
    {
        if (impactEffectPrefab == null)
            return;

        VisualEffect impact = Instantiate(
            impactEffectPrefab,
            position,
            Quaternion.LookRotation(normal));

        impact.SendEvent(impactEventName);
        Destroy(impact.gameObject, 2f);
    }
}