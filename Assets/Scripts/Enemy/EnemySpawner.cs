using UnityEngine;
using System.Collections; //

public class EnemySpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject bossPrefab;

    [Header("Spawn Settings")]
    public Vector3 bossSpawnPosition = new Vector3(0, 2, 0);

    [Header("Practice Result Settings")]
    [Tooltip("流用するポーズ画面のキャンバスオブジェクト")]
    public GameObject pauseCanvas;
    [Tooltip("撃破からメニューが出るまでの待ち時間")]
    public float postDeathDelay = 2.5f;

    private GameObject spawnedBossInstance;
    private bool isMonitoring = false;

    void Start()
    {
        Invoke("SpawnBoss", 1f);
    }

    public void SpawnBoss()
    {
        GameObject prefabToSpawn = bossPrefab;

        // 練習モードならマネージャーに登録されたボスを使う
        if (BossPracticeManager.IsPracticeMode && BossPracticeManager.SelectedBossPrefab != null)
        {
            prefabToSpawn = BossPracticeManager.SelectedBossPrefab;
        }

        if (prefabToSpawn != null)
        {
            spawnedBossInstance = Instantiate(prefabToSpawn, bossSpawnPosition, Quaternion.identity);

            // 練習モード中のみ監視を開始
            if (BossPracticeManager.IsPracticeMode)
            {
                isMonitoring = true;
            }
        }
    }

    void Update()
    {
        // ボスが撃破・撤退して Destroy された（nullになった）瞬間を検知
        if (isMonitoring && spawnedBossInstance == null)
        {
            isMonitoring = false;
            StartCoroutine(ShowResultMenuRoutine());
        }
    }

    private IEnumerator ShowResultMenuRoutine()
    {
        // 指定秒数待機
        yield return new WaitForSeconds(postDeathDelay);

        // シーン内の PauseManager を探してリザルトモードを起動
        PauseManager pm = Object.FindFirstObjectByType<PauseManager>();
        if (pm != null)
        {
            pm.SetPracticeResultMode(true,true);
        }
        else
        {
            Debug.LogWarning("PauseManagerが見つかりません。");
        }
    }
}