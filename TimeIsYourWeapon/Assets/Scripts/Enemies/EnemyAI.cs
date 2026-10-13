using UnityEngine;
using UnityEngine.AI;

// Enemy-AI met states: Idle/Patrol -> Chase -> Attack -> Dead.
// - Ziet de speler alleen binnen detectRange, binnen zijn kijkhoek en met vrije zichtlijn.
// - Volgt de speler tot attackRange en schiet alleen als hij de speler echt kan zien.
// - One hit KO: roep Die() aan vanuit het kogelscript.   Testtoets: K = alle enemies dood.
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
        if (player != null) playerCollider = player.GetComponentInChildren<Collider>();

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

        if (state != State.Dead)
            anim.SetFloat("Speed", agent.velocity.magnitude);
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
            shootTimer = reactionTime;
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
        if (shootTimer <= 0f)
        {
            anim.SetTrigger("Shoot");
            shootTimer = shootInterval + Random.Range(-shootRandomness, shootRandomness);
        }
    }

    // ---------- Overgangen ----------

    void StartChase()
    {
        state = State.Chase;
        lastSeenTime = Time.time;
        agent.isStopped = false;
    }

    void ReturnToCalm()
    {
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

    // Staat de speler binnen de kijkhoek? (alleen nodig zolang hij de speler nog niet heeft gezien)
    bool InViewCone()
    {
        Vector3 to = player.position - transform.position;
        to.y = 0f;
        return Vector3.Angle(transform.forward, to) <= viewAngle * 0.5f;
    }

    // Binnen bereik en niets tussen het oog en de speler?
    bool CanSeePlayer()
    {
        Vector3 from = eye.position;
        Vector3 dir = PlayerAimPoint() - from;
        float dist = dir.magnitude;
        if (dist > detectRange) return false;

        if (Physics.Raycast(from, dir / dist, out RaycastHit hit, dist + 0.5f, sightMask, QueryTriggerInteraction.Ignore))
            return hit.transform == player || hit.transform.IsChildOf(player);

        return true; // niets geraakt = niets in de weg
    }

    // ---------- Schieten & dood ----------

    // Animation Event op het schietmoment in de Shooting-clip. Hier komt later de kogel.
    public void OnShootFrame()
    {
        if (state == State.Dead) return;
        recoil = 1f;
    }

    // One hit KO: roep dit aan vanuit het kogelscript van de speler.
    public void Die()
    {
        if (state == State.Dead) return;
        state = State.Dead;
        recoil = 0f;

        agent.isStopped = true;
        agent.enabled = false;

        foreach (Collider c in GetComponentsInChildren<Collider>())
            c.enabled = false;

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

        Gizmos.color = Color.yellow;                       // detectie-afstand
        Gizmos.DrawWireSphere(pos, detectRange);

        Gizmos.color = Color.red;                          // schietafstand
        Gizmos.DrawWireSphere(pos, attackRange);

        Gizmos.color = Color.cyan;                         // kijkhoek
        Vector3 left = Quaternion.Euler(0f, -viewAngle * 0.5f, 0f) * transform.forward;
        Vector3 right = Quaternion.Euler(0f, viewAngle * 0.5f, 0f) * transform.forward;
        Gizmos.DrawLine(pos, pos + left * detectRange);
        Gizmos.DrawLine(pos, pos + right * detectRange);

        if (Application.isPlaying && player != null && eye != null)
        {
            Gizmos.color = canSee ? Color.green : Color.gray; // zichtlijn
            Gizmos.DrawLine(eye.position, PlayerAimPoint());
        }
    }
}