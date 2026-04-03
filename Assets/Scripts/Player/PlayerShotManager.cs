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

        // --- 修正：GetComponent ではなく GetComponentInChildren を使う ---
        hitHandler = GetComponentInChildren<PlayerHitHandler>();

        // デバッグ用：見つからなかったらエラーを出す
        if (hitHandler == null) Debug.LogError("PlayerHitHandler が子オブジェクトに見つかりません！");
    }
    void Update()
    {
        if (Time.timeScale <= 0) return;
        if (hitHandler == null || hitHandler.currentState != PlayerHitHandler.PlayerState.Normal)
        {
            mainTimer = 0;
            subTimer = 0;
            return;
        }
        if (Input.GetKey(KeyCode.Z))
        {
            mainTimer -= Time.deltaTime;
            subTimer -= Time.deltaTime;

            if (mainTimer <= 0)
            {
                FireMainShot();
                mainTimer = mainShotInterval;
            }

            if (subTimer <= 0)
            {
                bool isSlow = Input.GetKey(KeyCode.LeftShift);
                FireSubShot(isSlow);
                subTimer = isSlow ? needleInterval : homingInterval;
            }
        }
        else
        {
            mainTimer = 0;
            subTimer = 0;
        }
    }

    void FireMainShot()
    {

        SEManager.Instance.Play(SEPath.SE_PLST00,0.5f);
        // Shot.txtのMainShot座標再現 (±8) [cite: 1, 2]
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
                // 針弾
                Spawn(needlePrefab, options[i].transform.position + new Vector3(-0.08f, 0, 0));
                Spawn(needlePrefab, options[i].transform.position + new Vector3(0.08f, 0, 0));
            }
            else
            {
                // ホーミング弾
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