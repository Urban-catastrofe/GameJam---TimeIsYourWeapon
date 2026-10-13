using UnityEngine;
using UnityEngine.AI;

// Enemy-demo: loopt naar de speler, schiet op afstand, met recoil.
// One hit KO: één treffer = Die().  Testtoets: K = doodgaan.
[RequireComponent(typeof(AudioSource))] // Zorgt dat de enemy automatisch een AudioSource krijgt
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyDemo : MonoBehaviour
{
    [Header("Gedrag")]
    public Transform player;            // leeg laten: zoekt object met tag "Player"
    public float stopDistance = 8f;     // afstand waarop hij stopt en gaat schieten
    public float shootInterval = 3.5f;  // seconden tussen schoten

    [Header("Recoil (extra terugslag bovenop de animatie)")]
    public float recoilChest = 12f;     // graden dat de borst naar achteren klapt
    public float recoilArm = 25f;       // graden dat de schietarm omhoog klapt
    public float recoilRecovery = 4f;   // hoe snel hij terugveert (hoger = sneller)

    [Header("Dood")]
    public float destroyAfter = 5f;     // seconden na de dood tot het object verdwijnt (0 = blijft liggen)

    // --- AUDIO TOEGEVOEGD ---
    [Header("Audio")]
    public float timeBetweenSteps = 0.35f;  
    private float stepTimer = 0f;
    private AudioClip shootSoundClip; 
    private AudioClip[] footstepSounds = new AudioClip[6];
    private AudioSource audioSource;
    // ------------------------

    NavMeshAgent agent;
    Animator anim;
    Transform chest, rightUpperArm;
    float shootTimer;
    float recoil;                       // 1 = net geschoten, 0 = rust
    bool dead;

    public bool IsDead => dead;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animator>();
        agent.stoppingDistance = stopDistance;

        // --- AUDIO INLADEN ---
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        
        shootSoundClip = Resources.Load<AudioClip>("Audio/EnemyShoot");
        for (int i = 0; i < 6; i++)
        {
            footstepSounds[i] = Resources.Load<AudioClip>("Audio/Footstep" + (i + 1));
        }
        // ---------------------

        // Botten opzoeken via de Humanoid-avatar (werkt voor elk Mixamo-model)
        chest = anim.GetBoneTransform(HumanBodyBones.UpperChest);
        if (chest == null) chest = anim.GetBoneTransform(HumanBodyBones.Chest);
        rightUpperArm = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.K)) Die();

        recoil = Mathf.Max(0f, recoil - recoilRecovery * Time.deltaTime);

        if (dead || player == null) return;

        agent.SetDestination(player.position);
        anim.SetFloat("Speed", agent.velocity.magnitude);

        // Alleen horizontale afstand meten
        Vector3 flat = player.position - transform.position;
        flat.y = 0f;
        float dist = flat.magnitude;

        if (dist <= stopDistance + 0.5f)
        {
            if (flat.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(flat), 8f * Time.deltaTime);

            shootTimer -= Time.deltaTime;
            if (shootTimer <= 0f)
            {
                anim.SetTrigger("Shoot");
                shootTimer = shootInterval;
            }
        }

        // --- VOETSTAPPEN LOGICA ---
        if (agent.velocity.magnitude > 0.1f)
        {
            stepTimer -= Time.deltaTime;
            if (stepTimer <= 0f)
            {
                PlayFootstep();
                stepTimer = timeBetweenSteps; 
            }
        }
        else
        {
            stepTimer = 0f; 
        }
        // --------------------------
    }

    // Wordt aangeroepen door een Animation Event op het schietmoment in de Shooting-clip.
    // Hier komt later ook het spawnen van de kogel.
    public void OnShootFrame()
    {
        if (dead) return;
        recoil = 1f;

        // --- AUDIO SCHIETEN ---
        if (audioSource != null && shootSoundClip != null)
        {
            audioSource.pitch = 1f; // Terugzetten op normale hoogte na eventuele random voetstappen
            audioSource.PlayOneShot(shootSoundClip);
        }
        // ----------------------
    }

    // Roep dit aan vanuit het kogelscript van de speler. One hit KO.
    public void Die()
    {
        if (dead) return;
        dead = true;
        recoil = 0f;

        agent.isStopped = true;
        agent.enabled = false;                  // geen navigatie meer

        // Collider uit, zodat kogels er niet meer tegenaan botsen
        foreach (Collider c in GetComponentsInChildren<Collider>())
            c.enabled = false;

        anim.SetTrigger("Die");

        if (destroyAfter > 0f)
            Destroy(gameObject, destroyAfter);
    }

    // LateUpdate draait NA de animatie, dus deze rotaties komen bovenop de animatie.
    void LateUpdate()
    {
        if (dead) return;

        float r = recoil * recoil;              // ease-out: snelle klap, rustig terugveren
        if (r <= 0f) return;

        if (chest != null)
            chest.rotation = Quaternion.AngleAxis(-recoilChest * r, transform.right) * chest.rotation;

        if (rightUpperArm != null)
            rightUpperArm.rotation = Quaternion.AngleAxis(-recoilArm * r, transform.right) * rightUpperArm.rotation;
    }

    // --- AUDIO HULPFUNCTIE ---
    private void PlayFootstep()
    {
        if (footstepSounds != null && footstepSounds.Length > 0 && audioSource != null)
        {
            int randomIndex = Random.Range(0, footstepSounds.Length);
            AudioClip clip = footstepSounds[randomIndex];

            if (clip != null)
            {
                audioSource.pitch = Random.Range(0.85f, 1.15f); 
                audioSource.PlayOneShot(clip, 0.4f); // Volume van de voetstap
            }
        }
    }
}