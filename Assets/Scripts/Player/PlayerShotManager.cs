using KanKikuchi.AudioManager;
using UnityEngine;

public class PlayerShotManager : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject mainShotPrefab;
    public GameObject needlePrefab;
    public GameObject homingPrefab;

    [Header("Interval Settings")]
    public float mainShotInterval = 0.05f;
    public float needleInterval = 0.08f;
    public float homingInterval = 0.12f;

    private float mainTimer;
    private float subTimer;
    private OptionManager optionManager;
    private float[] homingInitialAngles = { -20f, 20f, 10f, -10f };

    private PlayerHitHandler hitHandler;

    void Start()
    {
        optionManager = GetComponent<OptionManager>();
        hitHandler = GetComponentInChildren<PlayerHitHandler>();

        if (hitHandler == null) Debug.LogError("PlayerHitHandler が子オブジェクトに見つかりません！");
    }

    // --- Update は削除、または入力を受け取らないように空にする ---
    void Update()
    {
        // リプレイ実装時はここで Input を直接参照せず、
        // すべて FixedUpdate 内で PlayerMove.Instance.currentFrameInput を通して処理します。
    }

    void FixedUpdate()
    {
        // タイムスケール停止中や、被弾中などは発射しない
        if (Time.timeScale <= 0) return;
        if (hitHandler == null || hitHandler.currentState != PlayerHitHandler.PlayerState.Normal)
        {
            mainTimer = 0;
            subTimer = 0;
            return;
        }

        // --- リプレイ・操作共通の入力参照 ---
        // PlayerMove.cs で記録・再現されている入力データを使用する
        bool isShooting = PlayerMove.Instance.currentFrameInput.shot;
        bool isSlow = PlayerMove.Instance.currentFrameInput.slow;

        if (isShooting)
        {
            // FixedUpdate 内なので、必ず fixedDeltaTime を使用する
            mainTimer -= Time.fixedDeltaTime;
            subTimer -= Time.fixedDeltaTime;

            if (mainTimer <= 0)
            {
                FireMainShot();
                // インターバルをリセット
                mainTimer = mainShotInterval;
            }

            if (subTimer <= 0)
            {
                FireSubShot(isSlow);
                subTimer = isSlow ? needleInterval : homingInterval;
            }
        }
        else
        {
            // 撃っていない時はタイマーをリセット（即座に撃ち始められるように 0 にする）
            mainTimer = 0;
            subTimer = 0;
        }
    }

    void FireMainShot()
    {
        SEManager.Instance.Play(SEPath.SE_PLST00, 0.5f);
        Spawn(mainShotPrefab, transform.position + new Vector3(-0.18f, 0, 0));
        Spawn(mainShotPrefab, transform.position + new Vector3(0.18f, 0, 0));
    }

    void FireSubShot(bool isSlow)
    {
        GameObject[] options = optionManager.GetOptions();
        for (int i = 0; i < options.Length; i++)
        {
            if (options[i] == null) continue;

            if (isSlow)
            {
                Spawn(needlePrefab, options[i].transform.position + new Vector3(-0.08f, 0, 0));
                Spawn(needlePrefab, options[i].transform.position + new Vector3(0.08f, 0, 0));
            }
            else
            {
                Quaternion rot = Quaternion.Euler(0, 0, homingInitialAngles[i]);
                Instantiate(homingPrefab, options[i].transform.position, rot);
            }
        }
    }

    void Spawn(GameObject prefab, Vector3 pos)
    {
        Instantiate(prefab, pos, Quaternion.identity);
    }
}