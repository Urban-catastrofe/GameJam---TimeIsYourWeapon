using UnityEngine;
using UnityEngine.VFX;

public class EnemyGunWeapon : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private Transform muzzle;
    [SerializeField] private Transform target;

    [Header("Projectile")]
    [SerializeField] private float projectileSpeed = 20f;
    [SerializeField] private float projectileDamage = 25f;
    [SerializeField] private float projectileLifetime = 5f;

    [Header("Effects")]
    [SerializeField] private VisualEffect muzzleFlash;
    [SerializeField] private string muzzleFlashEvent = "Shoot";

    private EnemyAI enemyAI;

    private void Awake()
    {
        enemyAI = GetComponent<EnemyAI>();

        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");

            if (player != null)
                target = player.transform;
        }
    }

    public void Fire()
    {
        if (enemyAI != null && enemyAI.IsDead)
            return;

        if (projectilePrefab == null)
        {
            Debug.LogError("[EnemyGunWeapon] Projectile prefab is not assigned.", this);
            return;
        }

        if (muzzle == null)
        {
            Debug.LogError("[EnemyGunWeapon] Muzzle is not assigned.", this);
            return;
        }

        if (target == null)
        {
            Debug.LogError("[EnemyGunWeapon] Target/player not found.", this);
            return;
        }

        Vector3 targetPosition = target.position + Vector3.up;
        Vector3 direction = (targetPosition - muzzle.position).normalized;

        Projectile projectile = Instantiate(
            projectilePrefab,
            muzzle.position,
            Quaternion.LookRotation(direction)
        );

        projectile.Initialize(
            direction,
            projectileSpeed,
            projectileDamage,
            projectileLifetime
        );

        if (muzzleFlash != null)
            muzzleFlash.SendEvent(muzzleFlashEvent);
    }
}