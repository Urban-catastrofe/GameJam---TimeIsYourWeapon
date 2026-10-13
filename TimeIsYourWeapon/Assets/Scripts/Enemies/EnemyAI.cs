using UnityEngine;
using UnityEngine.AI;
using UnityEngine.VFX;

// Enemy-AI met states: Idle/Patrol -> Chase -> Attack -> Dead.
// - Ziet de speler alleen binnen detectRange, binnen zijn kijkhoek en met vrije zichtlijn.
// - Volgt de speler tot attackRange en schiet alleen als hij de speler echt kan zien.
// - Telegraphing: de visor licht op vlak voor elk schot (en elke zwaardslag).
// - Attack modes: Ranged (schieten), Melee (zwaard) of Hybrid (schieten, maar zwaard als de speler dichtbij komt).
// - One hit KO: roep Die() aan vanuit het kogelscript.   Testtoets: K = alle enemies dood.
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    public enum State { Idle, Patrol, Chase, Attack, Dead }

    public enum AttackMode
    {
        Ranged,
        Melee
    }

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
    public AttackMode attackMode = AttackMode.Ranged;   // Ranged = oud gedrag
    public float attackRange = 8f;          // afstand waarop hij stopt en gaat schieten
    public float shootInterval = 3.5f;      // seconden tussen schoten
    public float shootRandomness = 0.5f;    // +/- willekeur op het interval
    public float reactionTime = 0.6f;       // wachttijd voor het eerste schot
    public float turnSpeed = 8f;            // hoe snel hij naar de speler draait

    [Header("Sword Combat (Melee / Hybrid)")]
    public float meleeRange = 2f;           // afstand waarop hij stopt en de speler kan raken
    public float meleeDamage = 40f;
    public float meleeInterval = 1.4f;      // seconden tussen zwaardslagen
    public float meleeRandomness = 0.3f;    // +/- willekeur op het interval
    [Range(0f, 360f)]
    public float meleeArc = 100f;           // de speler moet binnen deze hoek voor hem staan
    public string slashTrigger = "Shoot";   // Animator trigger voor de zwaardslag (zelfde als schieten)
    public bool useAnimationEvent = true;   // true = schade komt van het Animation Event in de clip (OnShootFrame of OnSwordHit)
    public float meleeHitDelay = 0.35f;     // alleen als useAnimationEvent uit staat: vertraging tot de hit
    public AudioClip swordSwingSound;       // optioneel
    public AudioClip swordHitSound;         // optioneel

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

    [Header("Dood")]
    public float destroyAfter = 5f;         // 0 = blijft liggen
    
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

    NavMeshAgent agent;
    Animator anim;
    Transform chest, rightUpperArm;
    Collider playerCollider;
    float playerRadius = 0.4f;
    readonly RaycastHit[] sightHits = new RaycastHit[16];

    bool canSee;
    float lastSeenTime;
    float shootTimer;
    float waitTimer;
    int patrolIndex;
    float recoil;

    // Sword
    bool hasSlashParam;
    bool swinging;
    float swingTimer = -1f;

    // Telegraphing
    Renderer glowRenderer;
    int glowIndex = -1;
    MaterialPropertyBlock mpb;
    Color baseGlowColor = Color.white;      // originele kleur van het glow-materiaal
    Color baseGlowEmission = Color.black;   // originele emissie van het glow-materiaal
    bool telegraphing;
    float fireTimeout = -1f;
    float glow;                             // 0 = normaal, 1 = volledig opgelicht

    public bool IsDead => state == State.Dead;

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
        if (player != null)
        {
            playerCollider = player.GetComponentInChildren<Collider>();
            if (playerCollider != null)
                playerRadius = Mathf.Max(playerCollider.bounds.extents.x, playerCollider.bounds.extents.z);
        }

        FindGlowMaterial();
        ReturnToCalm();
    }

    void Update()
    {
        recoil = Mathf.Max(0f, recoil - recoilRecovery * Time.deltaTime);

        if (state == State.Dead) return;
        if (Input.GetKeyDown(KeyCode.K)) { Die(); return; }
        if (player == null) return;

        canSee = CanSeePlayer();
        if (canSee) lastSeenTime = Time.time;

        switch (state)
        {
            case State.Idle:   UpdateIdle();   break;
            case State.Patrol: UpdatePatrol(); break;
            case State.Chase:  UpdateChase();  break;
            case State.Attack: UpdateAttack(); break;
        }

        if (state == State.Dead) return;

        UpdateSwing();

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
        if (canSee && AttackDistance() <= EffectiveAttackRange())
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

        // Speler uit zicht of te ver weg (speling) -> weer achtervolgen
        float margin = attackMode == AttackMode.Melee ? 0.5f : 1f;
        if (!canSee || AttackDistance() > EffectiveAttackRange() + margin)
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

        // Vlak voor de aanval: visor laten oplichten
        if (!telegraphing && shootTimer <= telegraphTime)
            StartTelegraph();

        if (shootTimer <= 0f)
        {
            if (UseMelee())
            {
                StartSwing();
                shootTimer = meleeInterval + Random.Range(-meleeRandomness, meleeRandomness);
            }
            else
            {
                anim.SetTrigger("Shoot");
                shootTimer = shootInterval + Random.Range(-shootRandomness, shootRandomness);
            }
            fireTimeout = 1.5f;   // vangnet als het Animation Event ontbreekt
        }
    }

    // ---------- Attack mode ----------

    // Op welke afstand stopt hij met lopen en begint hij aan te vallen?
    float EffectiveAttackRange()
    {
        return attackMode == AttackMode.Melee ? meleeRange : attackRange;
    }

    // Afstand tussen de BUITENKANTEN van enemy en speler (niet middelpunt tot middelpunt).
    // Zo werkt meleeRange ook voor een grote boss: zijn lichaam (agent.radius) botst eerder
    // tegen de speler dan dat zijn middelpunt in de buurt komt.
    float MeleeDistance()
    {
        float r = agent != null ? agent.radius : 0.5f;
        return Mathf.Max(0f, FlatDistanceToPlayer() - r - playerRadius);
    }

    // Afstand die telt voor "ben ik dicht genoeg om aan te vallen?"
    float AttackDistance()
    {
        return attackMode == AttackMode.Melee ? MeleeDistance() : FlatDistanceToPlayer();
    }

    // Slaat hij nu met het zwaard of schiet hij?
    bool UseMelee()
    {
        switch (attackMode)
        {
            case AttackMode.Melee:  return true;
            default:                return false;
        }
    }

    // ---------- Zwaard ----------

    void StartSwing()
    {
        Debug.Log("StartSwing " +  hasSlashParam);
        swinging = true;
        swingTimer = useAnimationEvent ? -1f : meleeHitDelay;

        anim.SetTrigger("Shoot");
        if (swordSwingSound != null)
            AudioSource.PlayClipAtPoint(swordSwingSound, transform.position);
    }

    void UpdateSwing()
    {
        if (!swinging || swingTimer <= 0f) return;

        swingTimer -= Time.deltaTime;
        if (swingTimer <= 0f) OnSwordHit();
    }

    // Animation Event op het raakmoment van de slag (alleen nodig als useAnimationEvent aan staat).
    // Anders wordt dit automatisch aangeroepen na meleeHitDelay.
    public void OnSwordHit()
    {
        if (state == State.Dead || !swinging) return;

        swinging = false;
        swingTimer = -1f;
        telegraphing = false;
        fireTimeout = -1f;

        // Raak ik de speler? Afstand, hoek en zichtlijn moeten kloppen op het raakmoment.
        Vector3 to = player.position - transform.position;
        to.y = 0f;
        bool inReach = MeleeDistance() <= meleeRange + 0.75f;   // beetje speling voor een stap vooruit
        bool inArc = Vector3.Angle(transform.forward, to) <= meleeArc * 0.5f;

        if (inReach && inArc && canSee)
        {
            ApplyMeleeDamage();
            if (swordHitSound != null)
                AudioSource.PlayClipAtPoint(swordHitSound, player.position);
        }
    }

    void ApplyMeleeDamage()
    {
        // PAS DIT AAN aan het health-script van je speler.
        // Nu wordt "TakeDamage(float)" aangeroepen op de speler (en zijn parents) als die methode bestaat.
        // Voorbeeld met een eigen script:
        //   var health = player.GetComponentInParent<PlayerHealth>();
        //   if (health != null) health.TakeDamage(meleeDamage);
        player.gameObject.SendMessageUpwards("TakeDamage", meleeDamage, SendMessageOptions.DontRequireReceiver);
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
        swinging = false;
        swingTimer = -1f;
        if (anim != null)
        {
            anim.ResetTrigger("Shoot");
            if (hasSlashParam) anim.ResetTrigger(slashTrigger);
        }
    }

    void UpdateGlow()
    {
        if (fireTimeout > 0f)
        {
            fireTimeout -= Time.deltaTime;
            if (fireTimeout <= 0f)
            {
                telegraphing = false;
                swinging = false;
            }
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

        // Negeer de eigen colliders van de enemy: een grote boss heeft meerdere colliders
        // (romp, armen, zwaard) die anders zijn eigen zichtlijn blokkeren.
        int count = Physics.RaycastNonAlloc(from, dir / dist, sightHits, dist + 0.5f, sightMask, QueryTriggerInteraction.Ignore);

        float closest = float.MaxValue;
        Transform closestHit = null;
        for (int i = 0; i < count; i++)
        {
            Transform t = sightHits[i].transform;
            if (t == transform || t.IsChildOf(transform)) continue;   // eigen collider
            if (sightHits[i].distance < closest)
            {
                closest = sightHits[i].distance;
                closestHit = t;
            }
        }

        if (closestHit == null) return true;
        return closestHit == player || closestHit.IsChildOf(player);
    }

    // ---------- Schieten & dood ----------

    // Animation Event op het schietmoment in de Shooting-clip. Hier komt later de kogel.
    public void OnShootFrame()
    {
        if (state == State.Dead)
            return;

        // Zelfde clip/event voor schieten en zwaardslag: bij een slag geen kogel maar een zwaardhit.
        if (swinging)
        {
            OnSwordHit();
            return;
        }

        recoil = 1f;
        telegraphing = false;
        fireTimeout = -1f;

        FireProjectile();
    }

    // One hit KO: roep dit aan vanuit het kogelscript van de speler.
    public void Die()
    {
        if (state == State.Dead) return;
        state = State.Dead;
        recoil = 0f;
        telegraphing = false;
        swinging = false;
        swingTimer = -1f;

        agent.isStopped = true;
        agent.enabled = false;

        foreach (Collider c in GetComponentsInChildren<Collider>())
            c.enabled = false;

        // Visor gaat uit als hij dood is
        SetGlow(baseGlowColor * 0.2f, Color.black);

        anim.SetFloat("Speed", 0f);
        anim.SetTrigger("Die");

        if (destroyAfter > 0f)
            Destroy(gameObject, destroyAfter);
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

        // Zwaardbereik en slagboog
        if (attackMode != AttackMode.Ranged)
        {
            Gizmos.color = Color.magenta;
            NavMeshAgent a = GetComponent<NavMeshAgent>();
            float reach = meleeRange + (a != null ? a.radius : 0f);   // vanaf het middelpunt gemeten
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
        {
            Debug.LogWarning(
                $"[{name}] Cannot shoot: projectile prefab, muzzle, or player is missing.",
                this);
            return;
        }

        // Aim towards the player's collider centre.
        Vector3 targetPosition = PlayerAimPoint();
        Vector3 direction = (targetPosition - muzzle.position).normalized;

        // Add a little inaccuracy.
        direction = Quaternion.Euler(
            Random.Range(-spreadAngle, spreadAngle),
            Random.Range(-spreadAngle, spreadAngle),
            0f
        ) * direction;

        // Play the enemy's own muzzle flash.
        if (muzzleFlash != null)
            muzzleFlash.SendEvent(muzzleFlashEvent);

        // Create the projectile.
        GameObject bullet = Instantiate(
            projectilePrefab,
            muzzle.position,
            Quaternion.LookRotation(direction));

        Projectile projectile = bullet.GetComponent<Projectile>();

        if (projectile == null)
        {
            Debug.LogError(
                $"[{name}] Projectile prefab has no Projectile component on its root.",
                bullet);

            Destroy(bullet);
            return;
        }

        // Configure this projectile for the enemy's weapon.
        projectile.Initialize(
            direction,
            projectileSpeed,
            projectileDamage,
            projectileLifetime);
    }
}