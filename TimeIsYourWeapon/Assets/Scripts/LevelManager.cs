using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class LevelManager : MonoBehaviour
{
    [System.Serializable]
    public class EnemyCountEvent : UnityEvent<int, int> { }

    public static LevelManager Instance { get; private set; }

    [Header("Spawn point tags")]
    public string playerSpawnTag = "PlayerSpawnPoint";
    public string enemySpawnTag = "EnemySpawnPoint";
    public string enemyBossSpawnTag = "EnemyBossSpawnPoint";

    [Header("Player")]
    public GameObject playerPrefab;
    public bool teleportExistingPlayer = true;

    [Header("Enemies")]
    public GameObject enemyPrefab;
    public int enemyCount = 5;
    public bool shuffleSpawnPoints = false;
    public float samePointSpread = 0.8f;

    [Header("Boss config")]
    public bool enemyBossEnabled = false;
    public GameObject enemyBossPrefab;
    public bool enemyBossReinforcement = false;
    [Range(4, 30)]
    public float enemyBossReinforcementDelay = 5.0f;
    [Range(1, 10)]
    public int amountOfReinforcementsMin = 1;
    [Range(1, 10)]
    public int amountOfReinforcementsMax = 5;


    [Header("Flow")]
    public bool autoStart = true;

    [Header("Events")]
    public UnityEvent onLevelStart = new UnityEvent();
    public UnityEvent onLevelComplete = new UnityEvent();
    public EnemyCountEvent onEnemyCountChanged = new EnemyCountEvent();

    public bool IsRunning { get; private set; }
    public bool IsComplete { get; private set; }
    public int TotalEnemies { get; private set; }
    
    public int AliveEnemies => enemies.Count;
    public int Kills => TotalEnemies - AliveEnemies;
    private float timeManager;
    private bool isBossDead = false;

    readonly List<GameObject> enemies = new List<GameObject>();

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void Start()
    {
        if (autoStart)
        {
            StartLevel();
        }
    }

    [ContextMenu("Start Level")]
    public void StartLevel()
    {
        if (IsRunning)
        {
            return;
        }
        
        timeManager = 0f;
        isBossDead = false;
        TotalEnemies = 0;
        IsRunning = true;
        IsComplete = false;
        enemies.Clear();

        SpawnPlayer();
        SpawnEnemies(enemyCount);
        
        if(enemyBossEnabled && enemyBossPrefab != null)
        {
            Transform bossPoint = FindFirstTagged(enemyBossSpawnTag);
            if (bossPoint != null)
            {
                GameObject boss = Instantiate(enemyBossPrefab, bossPoint.position, bossPoint.rotation);
                boss.name = $"{enemyBossPrefab.name}_Boss";
                enemies.Add(boss);
                TotalEnemies++;
            }
        }

        onLevelStart?.Invoke();
        Debug.Log($"Level started - enemies: {TotalEnemies}");
    }

    [ContextMenu("Restart Level")]
    public void RestartLevel() => SceneManager.LoadScene(SceneManager.GetActiveScene().name);

    void SpawnPlayer()
    {
        Transform point = FindFirstTagged(playerSpawnTag);
        Instantiate(playerPrefab, point.position, point.rotation);
        
        if (!teleportExistingPlayer) return;

        //GameObject player = GameObject.FindGameObjectWithTag("Player");
        //player.transform.SetPositionAndRotation(point.position, point.rotation);
    }

    void SpawnEnemies(int amount = 0)
    {
        List<Transform> points = FindAllTagged(enemySpawnTag);
        if (shuffleSpawnPoints) Shuffle(points);

        var cachedTotalEnemies = TotalEnemies;
        TotalEnemies = Mathf.Max(0, amount);
        for (int i = 0; i < TotalEnemies; i++)
        {
            Transform point = points[i % points.Count];
            Vector3 position = point.position;

            int lap = i / points.Count;
            if (lap > 0)
            {
                float angle = lap * 137.508f * Mathf.Deg2Rad;
                position += new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (samePointSpread * lap);
            }

            GameObject enemy = Instantiate(enemyPrefab, position, point.rotation);
            enemy.name = $"{enemyPrefab.name}_{i + 1:00}";
            enemies.Add(enemy);
        }
        
        TotalEnemies += cachedTotalEnemies;
        onEnemyCountChanged?.Invoke(AliveEnemies, TotalEnemies);
    }

    public void NotifyEnemyKilled(GameObject enemy)
    {
        if (enemy != null && enemies.Remove(enemy))
        {
            NotifyChanged();
        }
    }

    void Update()
    {
        if (!IsRunning) return;
        
        if(enemyBossReinforcement && !isBossDead)
        {
            timeManager += Time.deltaTime;
            
            if(timeManager >= enemyBossReinforcementDelay)
            {
                Debug.Log("Spawning enemy reinforcements.");
                int reinforcements = Random.Range((int)amountOfReinforcementsMin, (int)amountOfReinforcementsMax + 1);
                SpawnEnemies(reinforcements);
                timeManager = 0f;
            }
        }
        
        if(enemyBossEnabled && !isBossDead)
        {
            bool bossAlive = false;
            foreach(GameObject enemy in enemies)
            {
                if(enemy != null && enemy.CompareTag("EnemyBoss"))
                {
                    bossAlive = true;
                    break;
                }
            }
            isBossDead = !bossAlive;
        }
        
        if (enemies.RemoveAll(e => e == null) > 0)
        {
            NotifyChanged();
        }

        CheckComplete();
    }

    void NotifyChanged()
    {
        onEnemyCountChanged?.Invoke(AliveEnemies, TotalEnemies);
        CheckComplete();
    }

    void CheckComplete()
    {
        if (IsComplete || TotalEnemies <= 0 || AliveEnemies > 0) return;

        IsComplete = true;
        IsRunning = false;
        onLevelComplete?.Invoke();  
        Debug.Log($"[LevelManager] Level complete! All {TotalEnemies} enemies defeated.");
    }

    static Transform FindFirstTagged(string tag)
    {
        GameObject go = GameObject.FindGameObjectWithTag(tag);
        return go != null ? go.transform : null;
    }

    static List<Transform> FindAllTagged(string tag)
    {
        List<Transform> result = new List<Transform>();
        foreach (GameObject go in GameObject.FindGameObjectsWithTag(tag)) result.Add(go.transform);
        result.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return result;
    }

    static void Shuffle(List<Transform> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}