using UnityEngine;
using UnityEngine.AI;

// Enemy-AI met states: Idle/Patrol -> Chase -> Attack -> Dead.
// - Ziet de speler alleen binnen detectRange, binnen zijn kijkhoek en met vrije zichtlijn.
// - Volgt de speler tot attackRange en schiet alleen als hij de speler echt kan zien.
// - Telegraphing: de visor licht op vlak voor elk schot.
// - Health: kogels roepen TakeHit(collider) aan (zie BulletDamage.cs). Max Health 1 = one hit KO.
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    public enum State { Idle, Patrol, Chase, Attack, Dead }

    [Header("Status (alleen om mee te kijken tijdens Play)")]
    public State state = State.Idle;

    [Header("Speler")]
    public Transform player;                // leeg laten: zoekt object met tag "Player"

    [Header("Detectie")]
    public float detectRange = 20f;         // hoe ver hij kan kijken
    [Range(0f, 360f)]
    public float viewAngle = 120f;          // kijkhoek zolang hij de speler nog niet heeft gezien
    public float forgetAfter = 10f;         // seconden uit zicht voordat hij opgeeft (0 = nooit)
    public LayerMask sightMask = Physics.DefaultRaycastLayers; // wat de zichtlijn kan blokkeren
    public Transform eye;                   // leeg laten: gebruikt automatisch het hoofdbot

    [Header("Aanvallen")]
    public float attackRange = 8f;          // afstand waarop hij stopt en gaat schieten
    public float shootInterval = 3.5f;      // seconden tussen schoten
    public float shootRandomness = 0.5f;    // +/- willekeur op het interval
    public float reactionTime = 0.6f;       // wachttijd voor het eerste schot
    public float turnSpeed = 8f;            // hoe snel hij naar de speler draait

    [Header("Telegraphing (waarschuwing vlak voor het schot)")]
    public float telegraphTime = 0.5f;      // hoe lang van tevoren de visor oplicht
    public Color telegraphColor = new Color(1f, 0.08f, 0.05f);   // felrood
    public float telegraphIntensity = 8f;   // gloei-sterkte (hoger = feller, werkt met Bloom)
    public string glowMaterialName = "Enemy_Glow";              // materiaal dat oplicht
    public AudioClip telegraphSound;        // optioneel: geluidje bij het oplichten

    [Header("Bewegen")]
    public float patrolSpeed = 1.2f;
    public float chaseSpeed = 2f;
    public Transform[] patrolPoints;        // leeg = blijft stilstaan (Idle)
    public float waitAtPoint = 2f;          // wachttijd bij elk patrouillepunt

    [Header("Recoil (extra terugslag bovenop de animatie)")]
    public float recoilChest = 12f;
    public float recoilArm = 25f;
    public float recoilRecovery = 4f;

    [Header("Health")]
    public int maxHealth = 1;               // 1 = one hit KO (Grunt). Boss bijv. 10
    public int bodyDamage = 1;              // schade per treffer op het lichaam
    public int weakPointDamage = 3;         // schade per treffer op een collider met tag "WeakPoint"

    [Header("Hit-animatie (alleen voor enemies met meer levens, zoals de boss)")]
    public bool playHitAnimation = false;   // aan = trigger "Hit" in de Animator bij een treffer
    public float hitStunTime = 0.6f;        // zo lang staat hij stil na een treffer
    public float hitCooldown = 1.5f;        // minimale tijd tussen twee hit-animaties (voorkomt stunlock)
    public bool interruptAttack = false;    // uit = een lopende zwaardslag wordt niet onderbroken
    public string attackStateName = "Shooting"; // naam van de aanval-state in de Animator

    [Header("Dood")]
    public float destroyAfter = 5f;         // 0 = blijft liggen
    public float timeAddedMin = 2f;
    public float timeAddedMax = 10f;

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

    // Telegraphing
    Renderer glowRenderer;
    int glowIndex = -1;
    MaterialPropertyBlock mpb;
    Color baseGlowColor = Color.white;      // originele kleur van het glow-materiaal
    Color baseGlowEmission = Color.black;   // originele emissie van het glow-materiaal
    bool telegraphing;
    float fireTimeout = -1f;
    float glow;                             // 0 = normaal, 1 = volledig opgelicht

    int currentHealth;
    float stunTimer;
    float nextHitAnimTime;

    public bool IsDead => state == State.Dead;
    public int CurrentHealth => currentHealth;
    public float HealthPercent => maxHealth > 0 ? (float)currentHealth / maxHealth : 0f; // 0..1, handig voor de UI

    // Wordt aangeroepen als de health verandert (voor de UI-klasgenoot): geeft HealthPercent door.
    public event System.Action<float> OnHealthChanged;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animator>();
        agent.stoppingDistance = 0.3f;

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
        agent.SetDestination(player.position);   // NavMesh zoekt zelf een weg om muren heen

        // Binnen bereik EN zichtlijn -> stoppen en aanvallen
        if (canSee && FlatDistanceToPlayer() <= attackRange)
        {
            state = State.Attack;
            shootTimer = Mathf.Max(reactionTime, telegraphTime);
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            return;
        }

        // Te lang uit zicht -> opgeven
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

        // Naar de speler draaien
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
        Debug.LogWarning("[EnemyAI] Geen materiaal met de naam '" + glowMaterialName +
                         "' gevonden op " + name + ". Telegraphing is uitgeschakeld.");
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

        // Opbouwen in telegraphTime, snel uitdoven na het schot
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
        mpb.SetColor("_BaseColor", baseCol);       // URP
        mpb.SetColor("_Color", baseCol);           // Built-in
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

        if (Physics.Raycast(from, dir / dist, out RaycastHit hit, dist + 0.5f, sightMask, QueryTriggerInteraction.Ignore))
            return hit.transform == player || hit.transform.IsChildOf(player);

        return true;
    }

    // ---------- Schieten & dood ----------

    // Animation Event op het schietmoment in de Shooting-clip. Hier komt later de kogel.
    public void OnShootFrame()
    {
        if (state == State.Dead) return;
        recoil = 1f;
        telegraphing = false;   // visor dooft na het schot
        fireTimeout = -1f;
    }

    // Roep dit aan vanuit het kogelscript van de speler, met de collider die geraakt is.
    // Raakt de kogel een collider met tag "WeakPoint", dan doet hij weakPointDamage, anders bodyDamage.
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

        // Van achteren geraakt terwijl hij je nog niet zag? Dan weet hij nu waar je bent.
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
        TimeManager.Instance.AddSeconds(Random.Range(timeAddedMin, timeAddedMax));
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

        // Visor gaat uit als hij dood is
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

        if (Application.isPlaying && player != null && eye != null)
        {
            Gizmos.color = canSee ? Color.green : Color.gray;
            Gizmos.DrawLine(eye.position, PlayerAimPoint());
        }
    }
}
