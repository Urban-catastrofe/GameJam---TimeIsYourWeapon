using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.VFX;

[RequireComponent(typeof(AudioSource))]
public class WeaponController : MonoBehaviour
{
    private AudioSource shotSound;
    private AudioClip shotSoundClip;
    
    [Header("References")]
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private Transform muzzle;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private InputActionReference fireAction;
    [SerializeField] private Animator animator;
    [SerializeField] private InputActionReference reloadAction; 

    [Header("Weapon Settings")]
    [SerializeField] private float fireRate = 4f;
    [SerializeField] private int magazineSize = 12;
    [SerializeField] private float aimRange = 100f;
    [SerializeField] private float reloadTimePenalty = 1f;
    
    [Header("Effects")]
    [SerializeField] private VisualEffect muzzleFlash;
    [SerializeField] private string muzzleFlashEvent = "Shoot";
    
    [Header("Aim Collision")]
    [SerializeField] private LayerMask aimLayers = ~0;
    
    [Header("Recoil")]
    [SerializeField] private Transform recoilTarget;
    [SerializeField] private Vector3 recoilPosition = new Vector3(0f, 0.01f, -0.06f);
    [SerializeField] private Vector3 recoilRotation = new Vector3(-6f, 0f, 0f);
    [SerializeField, Min(0.01f)] private float recoilRecovery = 8f;

    private int currentAmmo;
    private float fireCooldown;
    private Vector3 recoilRestPosition;
    private Quaternion recoilRestRotation;
    private float recoil;

    private void Awake()
    {
        shotSound = GetComponent<AudioSource>();
        if (shotSound == null)
        {
            shotSound = gameObject.AddComponent<AudioSource>();
        }

        shotSoundClip = Resources.Load<AudioClip>("Audio/PlayerShoot");
        if (shotSoundClip == null)
        {
            Debug.LogWarning("[WeaponController] Kan 'PlayerShoot' niet vinden in de Resources-map!");
        }

        currentAmmo = magazineSize;
        
        if (recoilTarget != null)
        {
            recoilRestPosition = recoilTarget.localPosition;
            recoilRestRotation = recoilTarget.localRotation;
        }
    }
    
    private void OnDisable()
    {
        recoil = 0f;

        if (recoilTarget == null) return;

        recoilTarget.localPosition = recoilRestPosition;
        recoilTarget.localRotation = recoilRestRotation;
    }

    private void Update()
    {
        fireCooldown -= Time.deltaTime;

        if (fireAction == null || fireAction.action == null)
            return;

        bool reloadPressed = reloadAction != null && reloadAction.action != null
            ? reloadAction.action.WasPressedThisFrame()
            : Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;

        bool firePressed = fireAction.action.WasPressedThisFrame();
        if (reloadPressed)
        {
            Reload();
            return;
        }

        if (firePressed && fireCooldown <= 0f && currentAmmo > 0)
        {
            Fire();
        }
    }

    private void Fire()
    {
        if (shotSound != null && shotSoundClip != null)
        {
            shotSound.PlayOneShot(shotSoundClip);
        }
        
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
        recoil = 1f;
    }
    
    private void Reload()
    {
        if (currentAmmo >= magazineSize)
            return;
        
        animator.SetTrigger("Fire");
        
        var bulletsUsed = magazineSize - currentAmmo;
        currentAmmo = magazineSize;

        if (TimeManager.Instance != null)
            TimeManager.Instance.RemoveSeconds(bulletsUsed * reloadTimePenalty);
    }
    
    private void LateUpdate()
    {
        if (recoilTarget == null) return;

        // Sharp kick, easing back into the resting pose.
        float strength = recoil * recoil;

        recoilTarget.localPosition =
            recoilRestPosition + recoilPosition * strength;

        recoilTarget.localRotation =
            recoilRestRotation *
            Quaternion.Euler(recoilRotation * strength);

        // Recover even while the world is slowed or frozen.
        recoil = Mathf.MoveTowards(
            recoil, 0f, recoilRecovery * Time.unscaledDeltaTime);
    }
}
