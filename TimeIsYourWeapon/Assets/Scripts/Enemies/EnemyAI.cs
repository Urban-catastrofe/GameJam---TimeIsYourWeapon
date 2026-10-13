using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(AudioSource))]
// Enemy-AI met states: Idle/Patrol -> Chase -> Attack -> Dead.
// - Ziet de speler alleen binnen detectRange, binnen zijn kijkhoek en met vrije zichtlijn.
// - Volgt de speler tot attackRange en schiet alleen als hij de speler echt kan zien.
// - Telegraphing: de visor licht op vlak voor elk schot.
// - Health: kogels roepen TakeHit(collider) aan (zie BulletDamage.cs). Max Health 1 = one hit KO.
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    public enum State { Idle, Patrol, Chase, Attack, Dead }
    public enum AttackMode { Ranged, Melee }


    [Header("Status (alleen om mee te kijken tijdens Play)")]
    public State state = State.Idle;

    [Header("Speler")]
    public Transform player;

    [Header("Detectie")]
    public float detectRange = 20f;
    [Range(0f, 360f)]
    public float viewAngle = 120f;
    public float forgetAfter = 10f;
    public LayerMask sightMask = Physics.DefaultRaycastLayers;
    public Transform eye;

    [Header("Aanvallen")]
    public AttackMode attackMode = AttackMode.Ranged;
    public float attackRange = 8f;
    public float shootInterval = 3.5f;
    public float shootRandomness = 0.5f;
    public float reactionTime = 0.6f;
    public float turnSpeed = 8f;

    [Header("Sword Combat (Melee / Hybrid)")]
    public float meleeRange = 2f;
    public float meleeDamage = 40f;
    public float meleeInterval = 1.4f;
    public float meleeRandomness = 0.3f;
    [Range(0f, 360f)]
    public float meleeArc = 100f;
    public string slashTrigger = "Shoot";
    public bool useAnimationEvent = true;
    public float meleeHitDelay = 0.35f;
    public AudioClip swordHitSound;

    public float attackRange = 8f;          // afstand waarop hij stopt en gaat schieten
    public float shootInterval = 3.5f;      // seconden tussen schoten
    public float shootRandomness = 0.5f;    // +/- willekeur op het interval
    public float reactionTime = 0.6f;       // wachttijd voor het eerste schot
    public float turnSpeed = 8f;            // hoe snel hij naar de speler draait

    [Header("Telegraphing (waarschuwing vlak voor het schot)")]
    public float telegraphTime = 0.5f;
    public Color telegraphColor = new Color(1f, 0.08f, 0.05f);
    public float telegraphIntensity = 8f;
    public string glowMaterialName = "Enemy_Glow";
    public AudioClip telegraphSound;

    [Header("Bewegen")]
    public float patrolSpeed = 1.2f;
    public float chaseSpeed = 2f;
    public Transform[] patrolPoints;
    public float waitAtPoint = 2f;

    [Header("Recoil (extra terugslag)")]
    public float recoilChest = 12f;
    public float recoilArm = 25f;
    public float recoilRecovery = 4f;

    [Header("Health")]
    public int maxHealth = 1;
    public int bodyDamage = 1;
    public int weakPointDamage = 3;

    [Header("Hit-animatie (alleen voor enemies met meer levens, zoals de boss)")]
    public bool playHitAnimation = false;   // aan = trigger "Hit" in de Animator bij een treffer
    public float hitStunTime = 0.6f;        // zo lang staat hij stil na een treffer
    public float hitCooldown = 1.5f;        // minimale tijd tussen twee hit-animaties (voorkomt stunlock)
    public bool interruptAttack = false;    // uit = een lopende zwaardslag wordt niet onderbroken
    public string attackStateName = "Shooting"; // naam van de aanval-state in de Animator

    [Header("Dood")]
    public float destroyAfter = 5f;
    
    [Header("Enemy Weapon")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform muzzle;
    [SerializeField] private float projectileSpeed = 20f;
    [SerializeField] private float projectileDamage = 25f;
    [SerializeField] private float projectileLifetime = 5f;
    [SerializeField] private float spreadAngle = 2f;

    [Header("Weapon Effects")]
    [SerializeField] private VisualEffect muzzleFlash;
    [SerializeField] private string muzzleFlashEvent = "Shoot";

    // --- AUDIO VARIABELEN ---
    [Header("Audio")]
    public float maxAudioDistance = 25f; 
    public float timeBetweenSteps = 0.35f;  
    private float stepTimer = 0f;
    private AudioClip shootSoundClip; 
    private AudioClip swordSwingSound; 
    private AudioClip[] footstepSounds = new AudioClip[6];
    private AudioSource audioSource;
    // ------------------------
    public float destroyAfter = 5f;         // 0 = blijft liggen

    NavMeshAgent agent;
    Animator anim;
    Transform chest, rightUpperArm;
    Collider playerCollider;

    bool canSee;
    float lastSeenTime;
    float shootTimer;
    float waitTimer;
    int patrolIndex;
    float recoil;

    bool hasSlashParam;
    bool swinging;
    float swingTimer = -1f;

    // Telegraphing
    Renderer glowRenderer;
    int glowIndex = -1;
    MaterialPropertyBlock mpb;
    Color baseGlowColor = Color.white;
    Color baseGlowEmission = Color.black;
    bool telegraphing;
    float fireTimeout = -1f;
    float glow;

    int currentHealth;
    float stunTimer;
    float nextHitAnimTime;

    public bool IsDead => state == State.Dead;
    public int CurrentHealth => currentHealth;
    public float HealthPercent => maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;

    public event System.Action<float> OnHealthChanged;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animator>();
        agent.stoppingDistance = 0.3f;
        
        // --- AUDIO COMPONENT INSTELLEN ---
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) 
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Wij laten Unity vanaf nu de afstanden regelen!
        audioSource.spatialBlend = 1f; // 100% 3D Geluid
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = 2f;  // Tot 2 meter hoor je hem op vol volume
        audioSource.maxDistance = maxAudioDistance; // Nu aanpasbaar in de inspector!

        // Geluiden laden
        shootSoundClip = Resources.Load<AudioClip>("Audio/EnemyShoot");
        swordSwingSound = Resources.Load<AudioClip>("Audio/SwordSwing"); 
        
        for (int i = 0; i < 6; i++)
        {
            footstepSounds[i] = Resources.Load<AudioClip>("Audio/Footstep" + (i + 1));
        }
        // ---------------------------------

        if (eye == null) eye = anim.GetBoneTransform(HumanBodyBones.Head);
        if (eye == null) eye = transform;

        chest = anim.GetBoneTransform(HumanBodyBones.UpperChest);
        if (chest == null) chest = anim.GetBoneTransform(HumanBodyBones.Chest);
        rightUpperArm = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        if (player != null) playerCollider = player.GetComponentInChildren<Collider>();

        currentHealth = Mathf.Max(1, maxHealth);

        FindGlowMaterial();
        ReturnToCalm();
    }

    void Update()
    {
        recoil = Mathf.Max(0f, recoil - recoilRecovery * Time.deltaTime);

        if (state == State.Dead) return;
        if (Input.GetKeyDown(KeyCode.K)) { Die(); return; }          
        if (Input.GetKeyDown(KeyCode.H)) { TakeDamage(bodyDamage); } 
        if (state == State.Dead) return;
        if (player == null) return;

        canSee = CanSeePlayer();
        if (canSee) lastSeenTime = Time.time;

        // Even stilstaan tijdens de hit-animatie
        if (stunTimer > 0f)
        {
            stunTimer -= Time.deltaTime;
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            anim.SetFloat("Speed", 0f);
            UpdateGlow();
            return;
        }

        switch (state)
        {
            case State.Idle:   UpdateIdle();   break;
            case State.Patrol: UpdatePatrol(); break;
            case State.Chase:  UpdateChase();  break;
            case State.Attack: UpdateAttack(); break;
        }

        if (state == State.Dead) return;

        anim.SetFloat("Speed", agent.velocity.magnitude);
        UpdateGlow();
        
        // --- VOETSTAPPEN (PUUR CODE) ---
        // Speelt altijd af als hij rent, BEHALVE als hij een zwaard (Melee) heeft!
        if (attackMode != AttackMode.Melee && agent.velocity.magnitude > 0.1f)
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
        // -------------------------------
    }

    // ---------- States ----------

    void UpdateIdle()
    {
        agent.isStopped = true;
        if (canSee && InViewCone()) StartChase();
    }

    void UpdatePatrol()
    {
        agent.isStopped = false;
        agent.speed = patrolSpeed;

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                GoToPatrolPoint();
            }
        }

        if (canSee && InViewCone()) StartChase();
    }

    void UpdateChase()
    {
        agent.isStopped = false;
        agent.speed = chaseSpeed;
        agent.SetDestination(player.position);   

        if (canSee && AttackDistance() <= EffectiveAttackRange())
        // Binnen bereik EN zichtlijn -> stoppen en aanvallen
        if (canSee && FlatDistanceToPlayer() <= attackRange)
        {
            state = State.Attack;
            shootTimer = Mathf.Max(reactionTime, telegraphTime);
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            return;
        }

        if (forgetAfter > 0f && Time.time - lastSeenTime > forgetAfter)
            ReturnToCalm();
    }

    void UpdateAttack()
    {
        agent.isStopped = true;


        // Speler uit zicht of te ver weg (+1 m speling) -> weer achtervolgen
        if (!canSee || FlatDistanceToPlayer() > attackRange + 1f)
        {
            StartChase();
            return;
        }

        Vector3 flat = player.position - transform.position;
        flat.y = 0f;
        if (flat.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(flat), turnSpeed * Time.deltaTime);

        shootTimer -= Time.deltaTime;

        // Vlak voor het schot: visor laten oplichten
        if (!telegraphing && shootTimer <= telegraphTime)
            StartTelegraph();

        if (shootTimer <= 0f)
        {
            anim.SetTrigger("Shoot");
            shootTimer = shootInterval + Random.Range(-shootRandomness, shootRandomness);
            fireTimeout = 1.5f;   // vangnet als het Animation Event ontbreekt
        }
    }

    // ---------- Overgangen ----------

    void StartChase()
    {
        state = State.Chase;
        lastSeenTime = Time.time;
        agent.isStopped = false;
        CancelTelegraph();
    }

    void ReturnToCalm()
    {
        CancelTelegraph();
        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            state = State.Patrol;
            GoToPatrolPoint();
        }
        else
        {
            state = State.Idle;
            agent.isStopped = true;
        }
    }

    void GoToPatrolPoint()
    {
        Transform p = patrolPoints[patrolIndex];
        if (p != null) agent.SetDestination(p.position);
        waitTimer = waitAtPoint;
    }

    // ---------- Telegraphing ----------

    void FindGlowMaterial()
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null && mats[i].name.Contains(glowMaterialName))
                {
                    glowRenderer = r;
                    glowIndex = i;
                    if (mats[i].HasProperty("_BaseColor")) baseGlowColor = mats[i].GetColor("_BaseColor");
                    else if (mats[i].HasProperty("_Color")) baseGlowColor = mats[i].GetColor("_Color");
                    if (mats[i].HasProperty("_EmissionColor")) baseGlowEmission = mats[i].GetColor("_EmissionColor");
                    mpb = new MaterialPropertyBlock();
                    return;
                }
            }
        }
    }

    void StartTelegraph()
    {
        telegraphing = true;
        if (telegraphSound != null)
            AudioSource.PlayClipAtPoint(telegraphSound, eye.position);
    }

    void CancelTelegraph()
    {
        telegraphing = false;
        fireTimeout = -1f;
        if (anim != null) anim.ResetTrigger("Shoot");
    }

    void UpdateGlow()
    {
        if (fireTimeout > 0f)
        {
            fireTimeout -= Time.deltaTime;
            if (fireTimeout <= 0f) telegraphing = false;
        }

        if (telegraphing)
            glow = Mathf.MoveTowards(glow, 1f, Time.deltaTime / Mathf.Max(0.01f, telegraphTime));
        else
            glow = Mathf.MoveTowards(glow, 0f, Time.deltaTime * 6f);

        Color emission = Color.Lerp(baseGlowEmission, telegraphColor * telegraphIntensity, glow * glow);
        Color baseCol = Color.Lerp(baseGlowColor, telegraphColor, glow);
        SetGlow(baseCol, emission);
    }

    void SetGlow(Color baseCol, Color emission)
    {
        if (glowRenderer == null) return;
        glowRenderer.GetPropertyBlock(mpb, glowIndex);
        mpb.SetColor("_BaseColor", baseCol);       
        mpb.SetColor("_Color", baseCol);           
        mpb.SetColor("_EmissionColor", emission);
        glowRenderer.SetPropertyBlock(mpb, glowIndex);
    }

    // ---------- Zien ----------

    Vector3 PlayerAimPoint()
    {
        return playerCollider != null ? playerCollider.bounds.center : player.position + Vector3.up;
    }

    float FlatDistanceToPlayer()
    {
        Vector3 d = player.position - transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    bool InViewCone()
    {
        Vector3 to = player.position - transform.position;
        to.y = 0f;
        return Vector3.Angle(transform.forward, to) <= viewAngle * 0.5f;
    }

    bool CanSeePlayer()
    {
        Vector3 from = eye.position;
        Vector3 dir = PlayerAimPoint() - from;
        float dist = dir.magnitude;
        if (dist > detectRange) return false;

        int count = Physics.RaycastNonAlloc(from, dir / dist, sightHits, dist + 0.5f, sightMask, QueryTriggerInteraction.Ignore);

        float closest = float.MaxValue;
        Transform closestHit = null;
        for (int i = 0; i < count; i++)
        {
            Transform t = sightHits[i].transform;
            if (t == transform || t.IsChildOf(transform)) continue;   
            if (sightHits[i].distance < closest)
            {
                closest = sightHits[i].distance;
                closestHit = t;
            }
        }

        if (closestHit == null) return true;
        return closestHit == player || closestHit.IsChildOf(player);
        if (Physics.Raycast(from, dir / dist, out RaycastHit hit, dist + 0.5f, sightMask, QueryTriggerInteraction.Ignore))
            return hit.transform == player || hit.transform.IsChildOf(player);

        return true;
    }

    // ---------- Schieten & dood ----------

    public void OnShootFrame()
    {
        if (state == State.Dead)
            return;

        if (swinging)
        {
            OnSwordHit();
            return;
        }

        if (state == State.Dead) return;
        recoil = 1f;
        telegraphing = false;   // visor dooft na het schot
        fireTimeout = -1f;
    }

    public void TakeHit(Collider hitCollider)
    {
        bool weak = hitCollider != null && hitCollider.CompareTag("WeakPoint");
        TakeDamage(weak ? weakPointDamage : bodyDamage);
    }

    public void TakeDamage(int amount)
    {
        if (state == State.Dead || amount <= 0) return;

        currentHealth = Mathf.Max(0, currentHealth - amount);
        OnHealthChanged?.Invoke(HealthPercent);

        if (currentHealth <= 0)
        {
            Die();
            return;
        }

        if (state == State.Idle || state == State.Patrol)
            StartChase();

        PlayHitReaction();
    }

    void PlayHitReaction()
    {
        if (!playHitAnimation || Time.time < nextHitAnimTime) return;

        bool attacking = anim.GetCurrentAnimatorStateInfo(0).IsName(attackStateName)
                         || anim.GetNextAnimatorStateInfo(0).IsName(attackStateName);
        if (attacking && !interruptAttack) return;

        nextHitAnimTime = Time.time + hitCooldown;
        stunTimer = hitStunTime;

        // Een lopende waarschuwing/aanval afbreken en na de hit opnieuw beginnen
        CancelTelegraph();
        if (state == State.Attack)
            shootTimer = Mathf.Max(shootTimer, telegraphTime + 0.2f);

        anim.SetTrigger("Hit");
    }

    public void Die()
    {
        if (state == State.Dead) return;
        state = State.Dead;
        recoil = 0f;
        if (currentHealth != 0)
        {
            currentHealth = 0;
            OnHealthChanged?.Invoke(0f);
        }
        telegraphing = false;

        agent.isStopped = true;
        agent.enabled = false;

        foreach (Collider c in GetComponentsInChildren<Collider>())
            c.enabled = false;

        SetGlow(baseGlowColor * 0.2f, Color.black);

        stunTimer = 0f;
        if (playHitAnimation) anim.ResetTrigger("Hit");

        anim.SetFloat("Speed", 0f);
        anim.SetTrigger("Die");

        if (destroyAfter > 0f)
            Destroy(gameObject, destroyAfter);

        // Tel de kill meteen, ook als het lichaam nog blijft liggen.
        if (LevelManager.Instance != null)
            LevelManager.Instance.NotifyEnemyKilled(gameObject);
    }

    // ---------- Recoil (na de animatie) ----------

    void LateUpdate()
    {
        if (state == State.Dead) return;

        float r = recoil * recoil;
        if (r <= 0f) return;

        if (chest != null)
            chest.rotation = Quaternion.AngleAxis(-recoilChest * r, transform.right) * chest.rotation;
        if (rightUpperArm != null)
            rightUpperArm.rotation = Quaternion.AngleAxis(-recoilArm * r, transform.right) * rightUpperArm.rotation;
    }

    // ---------- Hulplijnen in de Scene-view ----------

    void OnDrawGizmosSelected()
    {
        Vector3 pos = transform.position + Vector3.up * 0.1f;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(pos, detectRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(pos, attackRange);

        Gizmos.color = Color.cyan;
        Vector3 left = Quaternion.Euler(0f, -viewAngle * 0.5f, 0f) * transform.forward;
        Vector3 right = Quaternion.Euler(0f, viewAngle * 0.5f, 0f) * transform.forward;
        Gizmos.DrawLine(pos, pos + left * detectRange);
        Gizmos.DrawLine(pos, pos + right * detectRange);

        if (attackMode != AttackMode.Ranged)
        {
            Gizmos.color = Color.magenta;
            NavMeshAgent a = GetComponent<NavMeshAgent>();
            float reach = meleeRange + (a != null ? a.radius : 0f);   
            Gizmos.DrawWireSphere(pos, reach);
            Vector3 mLeft = Quaternion.Euler(0f, -meleeArc * 0.5f, 0f) * transform.forward;
            Vector3 mRight = Quaternion.Euler(0f, meleeArc * 0.5f, 0f) * transform.forward;
            Gizmos.DrawLine(pos, pos + mLeft * reach);
            Gizmos.DrawLine(pos, pos + mRight * reach);
        }

        if (Application.isPlaying && player != null && eye != null)
        {
            Gizmos.color = canSee ? Color.green : Color.gray;
            Gizmos.DrawLine(eye.position, PlayerAimPoint());
        }
    }
    
    private void FireProjectile()
    {
        if (projectilePrefab == null || muzzle == null || player == null)
            return;
        
        // --- AUDIO SCHIETEN ---
        if (audioSource != null && shootSoundClip != null)
        {
            audioSource.pitch = Random.Range(0.9f, 1.1f); // Beetje variatie in schietgeluid!
            audioSource.PlayOneShot(shootSoundClip, 0.8f);
        }
        // ----------------------

        Vector3 targetPosition = PlayerAimPoint();
        Vector3 direction = (targetPosition - muzzle.position).normalized;

        direction = Quaternion.Euler(
            Random.Range(-spreadAngle, spreadAngle),
            Random.Range(-spreadAngle, spreadAngle),
            0f
        ) * direction;

        if (muzzleFlash != null)
            muzzleFlash.SendEvent(muzzleFlashEvent);

        GameObject bullet = Instantiate(
            projectilePrefab,
            muzzle.position,
            Quaternion.LookRotation(direction));

        Projectile projectile = bullet.GetComponent<Projectile>();

        if (projectile == null)
        {
            Destroy(bullet);
            return;
        }

        projectile.Initialize(direction, projectileSpeed, projectileDamage, projectileLifetime);
    }

    // --- AUDIO HULPFUNCTIE VOOR DE STAPPEN ---
    private void PlayFootstep()
    {
        if (footstepSounds != null && footstepSounds.Length > 0 && audioSource != null)
        {
            int randomIndex = Random.Range(0, footstepSounds.Length);
            AudioClip clip = footstepSounds[randomIndex];

            if (clip != null)
            {
                audioSource.pitch = Random.Range(0.85f, 1.15f); 
                audioSource.PlayOneShot(clip, 0.35f); 
            }
        }
    }
}

