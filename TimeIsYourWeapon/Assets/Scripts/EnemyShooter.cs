
using UnityEngine;

public class EnemyShooter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private Transform muzzle;
    [SerializeField] private Transform player;

    [Header("Shooting")]
    [SerializeField] private float fireRate = 1f;
    [SerializeField] private float shootingRange = 25f;
    [SerializeField] private float aimHeight = 1f;

    [Header("Accuracy")]
    [SerializeField] private float spreadAngle = 2f;

    [Header("Line of Sight")]
    [SerializeField] private LayerMask obstructionLayers = ~0;

    private float fireCooldown;

    private void Start()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
                player = playerObject.transform;
            else
                Debug.LogWarning("EnemyShooter: No object tagged 'Player' found.", this);
        }
    }

    private void Update()
    {
        if (player == null || projectilePrefab == null || muzzle == null)
            return;

        fireCooldown -= Time.deltaTime;

        Vector3 targetPosition = player.position + Vector3.up * aimHeight;
        Vector3 toPlayer = targetPosition - muzzle.position;

        if (toPlayer.sqrMagnitude > shootingRange * shootingRange)
            return;

        if (!HasLineOfSight(targetPosition))
            return;

        if (fireCooldown <= 0f)
        {
            Fire(targetPosition);
            fireCooldown = 1f / Mathf.Max(fireRate, 0.01f);
        }
    }

    private bool HasLineOfSight(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - muzzle.position;
        float distance = direction.magnitude;

        if (distance <= 0f)
            return true;

        if (Physics.Raycast(
            muzzle.position,
            direction.normalized,
            out RaycastHit hit,
            distance,
            obstructionLayers,
            QueryTriggerInteraction.Ignore))
        {
            return hit.transform == player ||
                   hit.transform.IsChildOf(player);
        }
        
        return true;
    }

    private void Fire(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - muzzle.position).normalized;
        
        direction = Quaternion.Euler(
            Random.Range(-spreadAngle, spreadAngle),
            Random.Range(-spreadAngle, spreadAngle),
            0f
        ) * direction;

        Projectile projectile = Instantiate(
            projectilePrefab,
            muzzle.position,
            Quaternion.LookRotation(direction)
        );

        projectile.Initialize(direction);
    }
}