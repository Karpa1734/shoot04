using UnityEngine;
using System.Collections.Generic;

public class BulletPool : MonoBehaviour
{
    public static BulletPool Instance;

    [Header("📊 プール上限セーフティインフラ")]
    [Tooltip("非アクティブ含む、メモリ上に存在していい弾幕オブジェクトの最大限界数")]
    [SerializeField] private int _maxPoolLimit = 1000;

    private Dictionary<GameObject, Stack<GameObject>> poolDict = new Dictionary<GameObject, Stack<GameObject>>();

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    /// <summary>
    /// プールから指定されたプレハブの弾を引き出します。
    /// メモリ上の総数が最大値(1000)に達している場合は、新規のInstantiateを強制ロックします。
    /// </summary>
    public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;

        if (!poolDict.ContainsKey(prefab))
        {
            poolDict[prefab] = new Stack<GameObject>();
        }

        // 眠っている待機弾があれば、上限に関係なく最優先で叩き起こしてリサイクル
        if (poolDict[prefab].Count > 0)
        {
            GameObject obj = poolDict[prefab].Pop();

            if (obj == null) return Get(prefab, position, rotation);

            obj.transform.position = position;
            obj.transform.rotation = rotation;
            obj.SetActive(true);

            Rigidbody2D rb = obj.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.simulated = true;
                rb.linearVelocity = Vector2.zero;
            }
            return obj;
        }

        // 🚨【最大数1000個制限の厳密なる監査】：
        // プールが空で、新しく生成（Instantiate）する必要がある場合のみ、現在の総数をスキャンします。
        int activeCount, totalCount;
        GetPoolStatus(out activeCount, out totalCount);

        if (totalCount >= _maxPoolLimit)
        {
            // 🔥 最大数に達している場合は、新規生成を完全に拒絶（ロック）してヌルリターンします！
            //    これにより、弾幕の異常増殖によるメモリパンクを未然に防ぎます。
            Debug.LogWarning($"<color=red>⚠️ [BULLET POOL LIMIT] 弾幕の総数が最大制限数（{_maxPoolLimit}個）に達したため、新規生成をロックしました！</color>");
            return null;
        }

        // 上限未満であれば、安全に新しい実体を生成
        GameObject newObj = Instantiate(prefab, position, rotation);
        return newObj;
    }

    public void Release(GameObject prefab, GameObject obj)
    {
        if (prefab == null || obj == null) return;

        obj.SetActive(false);

        // 🎯 常に統一された絶対元本キーのスタックへ安全に返却
        if (!poolDict.ContainsKey(prefab))
        {
            poolDict[prefab] = new Stack<GameObject>();
        }

        if (!poolDict[prefab].Contains(obj))
        {
            poolDict[prefab].Push(obj);
        }
    }

    // =========================================================================
    // 📊【デバッグ＆リミッター連動窓口】
    // =========================================================================
    public void GetPoolStatus(out int activeCount, out int totalCount)
    {
        activeCount = 0;
        totalCount = 0;

        DanmakuBullet[] allActiveBullets = Object.FindObjectsByType<DanmakuBullet>(FindObjectsSortMode.None);
        if (allActiveBullets != null)
        {
            activeCount = allActiveBullets.Length;
        }

        int inactiveCount = 0;
        foreach (var kvp in poolDict)
        {
            if (kvp.Value != null)
            {
                inactiveCount += kvp.Value.Count;
            }
        }

        totalCount = activeCount + inactiveCount;
    }
}