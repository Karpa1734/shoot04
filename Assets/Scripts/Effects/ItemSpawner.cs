using UnityEngine;
using System.Collections.Generic;

public class ItemSpawner : MonoBehaviour
{
    public static ItemSpawner Instance;

    [System.Serializable]
    public struct ItemPrefabMap
    {
        public ItemController.ITEM_TYPE type;
        public GameObject prefab;
    }

    [Header("Item Settings")]
    [SerializeField] private List<ItemPrefabMap> itemPrefabs;

    // 内部的な辞書で検索を高速化
    private Dictionary<ItemController.ITEM_TYPE, GameObject> prefabDict = new Dictionary<ItemController.ITEM_TYPE, GameObject>();

    void Awake()
    {
        if (Instance == null) Instance = this;

        // リストを辞書に変換
        foreach (var map in itemPrefabs)
        {
            if (map.prefab != null && !prefabDict.ContainsKey(map.type))
            {
                prefabDict.Add(map.type, map.prefab);
            }
        }
    }

    /// <summary>
    /// 指定した位置にアイテムを1つ生成する
    /// </summary>
    public void SpawnItem(ItemController.ITEM_TYPE type, Vector3 position, bool autoCollect = false)
    {
        if (!prefabDict.ContainsKey(type)) return;

        GameObject itemObj = Instantiate(prefabDict[type], position, Quaternion.identity);

        if (autoCollect)
        {
            ItemController controller = itemObj.GetComponent<ItemController>();
            if (controller != null)
            {
                // 即座に回収フラグを立てる
                controller.SetCollectImmediate();
            }
        }
    }
    /// <summary>
    /// 指定した位置に複数のアイテムを円状・ランダムに散らして生成する
    /// </summary>
    public void SpawnItems(ItemController.ITEM_TYPE type, int count, Vector3 position, float spread = 0.7f)
    {
        for (int i = 0; i < count; i++)
        {
            // 少しだけ位置をずらす
            Vector3 randomOffset = new Vector3(
                Random.Range(-spread, spread),
                Random.Range(-spread, spread),
                0
            );
            SpawnItem(type, position + randomOffset);
        }

    }

    /// <summary>
    /// 敵撃破時のドロップセット（例：パワー2、スコア5）を一括生成する
    /// </summary>
    public void DropItemsOnDeath(Vector3 position, int powerCount, int scoreCount)
    {
        if (powerCount > 0)
            SpawnItems(ItemController.ITEM_TYPE.POWER01, powerCount, position);

        if (scoreCount > 0)
            SpawnItems(ItemController.ITEM_TYPE.SCORE_UP, scoreCount, position);
    }
}