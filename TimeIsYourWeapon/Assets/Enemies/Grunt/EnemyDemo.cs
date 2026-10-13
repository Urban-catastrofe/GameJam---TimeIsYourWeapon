using UnityEngine;
using UnityEngine.AI;

// Simpele demo: enemy loopt naar de speler, blijft op afstand staan en "schiet".
// Druk op K om de death-animatie te testen.
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyDemo : MonoBehaviour
{
    public Transform player;            // sleep hier je Player in (of laat leeg: zoekt tag "Player")
    public float stopDistance = 8f;     // afstand waarop hij stopt en gaat schieten
    public float shootInterval = 1.5f;  // seconden tussen schoten

    NavMeshAgent agent;
    Animator anim;
    float shootTimer;
    bool dead;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animator>();
        agent.stoppingDistance = stopDistance;

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
    }

    void Update()
    {
        if (dead || player == null) return;

        agent.SetDestination(player.position);

        // Snelheid naar de Animator sturen (Idle <-> Walk)
        anim.SetFloat("Speed", agent.velocity.magnitude);

        float dist = Vector3.Distance(transform.position, player.position);
        if (dist <= stopDistance)
        {
            // Naar de speler draaien
            Vector3 dir = player.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(dir), 8f * Time.deltaTime);

            shootTimer -= Time.deltaTime;
            if (shootTimer <= 0f)
            {
                anim.SetTrigger("Shoot");
                shootTimer = shootInterval;
                // Hier later: kogel spawnen vanaf het Muzzle-punt van het wapen
            }
        }

        if (Input.GetKeyDown(KeyCode.K)) Die();
    }

    public void Die()
    {
        dead = true;
        agent.isStopped = true;
        anim.SetTrigger("Die");
    }
}
