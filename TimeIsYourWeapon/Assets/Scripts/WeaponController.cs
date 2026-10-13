
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.VFX;

public class WeaponController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private Transform muzzle;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private InputActionReference fireAction;
    [SerializeField] private Animator animator;

    [Header("Weapon Settings")]
    [SerializeField] private float fireRate = 4f;
    [SerializeField] private int magazineSize = 12;
    [SerializeField] private float aimRange = 100f;
    
    [Header("Effects")]
    [SerializeField] private VisualEffect muzzleFlash;
    [SerializeField] private string muzzleFlashEvent = "Shoot";
    
    [Header("Aim Collision")]
    [SerializeField] private LayerMask aimLayers = ~0;

    private int currentAmmo;
    private float fireCooldown;

    private void Awake()
    {
        currentAmmo = magazineSize;
    }

    private void Update()
    {
        fireCooldown -= Time.deltaTime;

        if (fireAction == null || fireAction.action == null)
            return;

        if (fireAction.action.WasPressedThisFrame() &&
            fireCooldown <= 0f &&
            currentAmmo > 0)
        {
            Fire();
        }
    }

    private void Fire()
    {
        fireCooldown = 1f / fireRate;
        currentAmmo--;
        
        if (muzzleFlash != null)
        {
            muzzleFlash.SendEvent(muzzleFlashEvent);
        }
        animator.SetTrigger("Fire");

        Ray aimRay = playerCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f));

        Vector3 aimPoint =
            aimRay.origin + aimRay.direction * aimRange;

        if (Physics.Raycast(
            aimRay,
            out RaycastHit aimHit,
            aimRange,
            aimLayers,
            QueryTriggerInteraction.Ignore))
        {
            aimPoint = aimHit.point;
        }

        Vector3 direction =
            (aimPoint - muzzle.position).normalized;

        Projectile projectile = Instantiate(
            projectilePrefab,
            muzzle.position,
            Quaternion.LookRotation(direction));

        projectile.Initialize(direction);
    }
}