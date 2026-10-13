using UnityEngine;
using UnityEngine.VFX;

public class Projectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private float speed = 20f;
    [SerializeField] private float damage = 25f;
    [SerializeField] private float lifetime = 5f;

    [Header("Collision")]
    [SerializeField] private LayerMask hitLayers = ~0;
    
    [Header("Impact Effect")]
    [SerializeField] private VisualEffect impactEffectPrefab;
    [SerializeField] private string impactEventName = "Impact";
    [SerializeField] private float surfaceOffset = 0.03f;
    [SerializeField] private float impactLifetime = 2f;

    private Vector3 direction;
    private float age;

    public void Initialize(Vector3 shootDirection)
    {
        direction = shootDirection.normalized;
        transform.forward = -direction;
    }

    private void Update()
    {

        if (gameObject == null)
        {
            return;
        }
            
        float delta = Time.deltaTime;

        age += delta;

        if (age >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        float distance = speed * delta;

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
            if (hit.collider.TryGetComponent<IDamageable>(
                    out IDamageable target))
            {
                target.TakeDamage(damage);
            }

            SpawnImpact(hit.point, hit.normal, hit.transform);

            Destroy(gameObject);
            return;
        }

        transform.position += direction * distance;
    }
    
    private void SpawnImpact(Vector3 position, Vector3 normal, Transform hitTransform)
    {
        if (impactEffectPrefab == null)
            return;

        Vector3 spawnPos = position + normal * surfaceOffset;
        Quaternion rotation = Quaternion.LookRotation(normal);

        VisualEffect impact = Instantiate(impactEffectPrefab, spawnPos, rotation);

        impact.SendEvent(impactEventName);

        Destroy(impact.gameObject, 2f);
    }
}