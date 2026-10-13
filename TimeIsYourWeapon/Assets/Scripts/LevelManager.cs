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
    
    [Header("Level results")]
    [SerializeField] private PlayerData playerData;
    [SerializeField] private string menuSceneName = "MainScene";

    [Header("Events")]
    public UnityEvent onLevelStart      = new UnityEvent();
    public UnityEvent onLevelComplete   = new UnityEvent();
    public UnityEvent onLevelFailed     = new UnityEvent();   // <-- NEW
    public EnemyCountEvent onEnemyCountChanged = new EnemyCountEvent();

    public bool IsRunning { get; private set; }
    public bool IsComplete { get; private set; }
    public int TotalEnemies { get; private set; }
    
    public int AliveEnemies => enemies.Count;
    public int Kills => TotalEnemies - AliveEnemies;
    public GameObject BossInstance => bossInstance;
    private float timeManager;
    private GameObject bossInstance;

    readonly List<GameObject> enemies = new List<GameObject>();

    void Awake() => Instance = this;
    
    void OnEnable()
    {
        TimeManager.OnTimerEnded += HandleTimerEnded;
    }

    void OnDisable()
    {
        TimeManager.OnTimerEnded -= HandleTimerEnded;
    }

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
        bossInstance = null;
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
                bossInstance = Instantiate(enemyBossPrefab, bossPoint.position, bossPoint.rotation);
                bossInstance.name = $"{enemyBossPrefab.name}_Boss";
                enemies.Add(bossInstance);
                TotalEnemies++;
            }
        }

        onLevelStart?.Invoke();
        onEnemyCountChanged?.Invoke(AliveEnemies, TotalEnemies);
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
        int amountToSpawn = Mathf.Max(0, amount);
        if (amountToSpawn == 0) return;

        if (enemyPrefab == null)
        {
            Debug.LogWarning("[LevelManager] Cannot spawn enemies: no enemy prefab assigned.", this);
            return;
        }

        List<Transform> points = FindAllTagged(enemySpawnTag);
        if (points.Count == 0)
        {
            Debug.LogWarning($"[LevelManager] Cannot spawn enemies: no points tagged '{enemySpawnTag}'.", this);
            return;
        }

        if (shuffleSpawnPoints) Shuffle(points);

        for (int i = 0; i < amountToSpawn; i++)
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
            TotalEnemies++;
        }
    }

    public void NotifyEnemyKilled(GameObject enemy)
    {
        // Removing the registered instance also makes repeated death reports harmless.
        if (enemy == null || !enemies.Remove(enemy)) return;

        if (enemy == bossInstance)
        {
            bossInstance = null;
            timeManager = 0f;
        }

        NotifyChanged();
    }

    void Update()
    {
        if (!IsRunning) return;

        // Fallback for enemies removed without going through their death handler.
        if (enemies.RemoveAll(e => e == null) > 0)
        {
            NotifyChanged();
            if (!IsRunning) return;
        }

        if (enemyBossEnabled && enemyBossReinforcement && bossInstance != null)
        {
            timeManager += Time.deltaTime;
            
            if(timeManager >= enemyBossReinforcementDelay)
            {
                Debug.Log("Spawning enemy reinforcements.");
                int reinforcements = Random.Range((int)amountOfReinforcementsMin, (int)amountOfReinforcementsMax + 1);
                SpawnEnemies(reinforcements);
                timeManager = 0f;
                NotifyChanged();
            }
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
        if (!IsRunning || IsComplete || TotalEnemies <= 0 ||
            AliveEnemies > 0 || (enemyBossEnabled && bossInstance != null))
        {
            return;
        }

        EndLevel(true);
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
    
    void HandleTimerEnded()
    {
        EndLevel(false);
    }
    
    void EndLevel(bool won)
    {
        if (!IsRunning) return;

        IsRunning = false;
        IsComplete = won;

        TimeManager timer = TimeManager.Instance;
        int reward = 0;

        if (timer != null)
        {
            if (won)
            {
                reward = Mathf.FloorToInt(Mathf.Max(0f, timer.secondsRemaining));
            }

            timer.StopAllCoroutines(); // Stop the countdown.
            timer.ChangeTimeScale(1f); // Restore time and physics speed.
        }
        else
        {
            Time.timeScale = 1f;
        }
        
        if (reward > 0)
        {
            if (playerData != null)
            {
                playerData.Recover(reward);
            }
        }

        if (won)
        {
            onLevelComplete?.Invoke();
        }
        else
        {
            onLevelFailed?.Invoke();
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SceneManager.LoadScene(menuSceneName);
    }
}
