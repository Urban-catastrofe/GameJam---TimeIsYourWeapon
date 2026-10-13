using System;
using Unity.VisualScripting;
using UnityEngine;
using Random = UnityEngine.Random;

// Simpele first-person speler: WASD lopen, muis kijken, spatie springen.
// Zet dit op de Player (met een CharacterController) en de Main Camera als kind op ooghoogte.
// Esc = muis loslaten, klik in de Game-view = muis weer vastzetten.
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Camera")]
    public Transform cameraTransform;       // leeg laten: pakt de camera onder de Player
    public float mouseSensitivity = 2f;
    public float maxLookAngle = 85f;        // hoe ver je omhoog/omlaag kunt kijken

    [Header("Bewegen")]
    public float walkSpeed = 5f;
    public float jumpHeight = 1.2f;
    public float gravity = -20f;

    [Header("Tijd")]
    [Tooltip("Aan: de speler beweegt op normale snelheid, ook als de tijd vertraagd is (Superhot-gevoel).")]
    public bool ignoreTimeScale = true;
    public float timeRemovedMin = 20f;
    public float timeRemovedMax = 30f;

    [Header("Geraakt-explosie (alleen voor de show, doet geen schade)")]
    public float hitExplosionDistance = 3f;    // hoe ver voor de camera de explosie verschijnt
    public float hitExplosionScale = 0.5f;     // 1 = even groot als bij een dode enemy
    public float hitShakeStrength = 0.7f;      // extra camera-shake bij een treffer

    // Voor het time-scale-script: 0 = staat stil, 1 = beweegt op volle snelheid.
    public float MoveAmount { get; private set; }

    CharacterController controller;
    float pitch;
    float verticalVelocity;

    public static event Action OnPlayerHit;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        if (cameraTransform == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null) cameraTransform = cam.transform;
        }
        LockCursor(true);
    }

    void Update()
    {
        // Muis vast / los
        if (Input.GetKeyDown(KeyCode.Escape)) LockCursor(false);
        if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked) LockCursor(true);

        float dt = ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;

        // ---- Kijken (muisbeweging is al per frame, dus geen deltaTime nodig) ----
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            transform.Rotate(0f, mouseX, 0f);                          // links/rechts: hele speler
            pitch = Mathf.Clamp(pitch - mouseY, -maxLookAngle, maxLookAngle);
            if (cameraTransform != null)
                cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f); // omhoog/omlaag: alleen camera
        }

        // ---- Lopen ----
        float x = Input.GetAxisRaw("Horizontal");   // A/D
        float z = Input.GetAxisRaw("Vertical");     // W/S
        Vector3 input = Vector3.ClampMagnitude(new Vector3(x, 0f, z), 1f);
        Vector3 move = transform.TransformDirection(input) * walkSpeed;

        MoveAmount = input.magnitude;

        // Adjust time scale
        if (Mathf.Approximately(MoveAmount, 1))
        {
            // Player is moving, set timeScale to Moving
            TimeManager.Instance.SetTimeScaleState(TimeManager.TimeScaleState.Moving);
        }
        else if (Mathf.Abs(Input.GetAxis("Mouse X")) > 0.01f ||
                 Mathf.Abs(Input.GetAxis("Mouse Y")) > 0.01f)
        {
            // Player is looking around, set timeScale to Rotating
            TimeManager.Instance.SetTimeScaleState(TimeManager.TimeScaleState.Rotating);
        }
        else
        {
            // Player is not moving or looking around, set timeScale to Standing
            TimeManager.Instance.SetTimeScaleState(TimeManager.TimeScaleState.Standing);
        }

        // ---- Zwaartekracht & springen ----
        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;                 // blijft netjes tegen de grond

        if (controller.isGrounded && Input.GetButtonDown("Jump"))
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        verticalVelocity += gravity * dt;
        move.y = verticalVelocity;

        controller.Move(move * dt);
    }

    void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    public void OnPlayerDamaged()
    {
        TimeManager.Instance.RemoveSeconds(Random.Range(timeRemovedMin, timeRemovedMax));
        SpawnHitExplosion();
        OnPlayerHit?.Invoke();
    }

    // Explosie net voor de camera, zodat je hem ziet zonder dat je hele scherm vol vuur zit
    void SpawnHitExplosion()
    {
        Transform view = cameraTransform != null ? cameraTransform : transform;
        Vector3 forward = Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
        if (forward == Vector3.zero) forward = transform.forward;

        Explosion.Spawn(view.position + forward * hitExplosionDistance, hitExplosionScale);
        CameraShake.Shake(view.position, hitShakeStrength);
    }
}