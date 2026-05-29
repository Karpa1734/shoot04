using KanKikuchi.AudioManager;
using TMPro; // 数字表示に必要
using UnityEngine;

public class ItemEffectHandler : MonoBehaviour
{
    [Header("Score Settings (POC)")]
    [SerializeField] private long maxScoreValue = 10000; // 上部回収ラインでの点数
    [SerializeField] private long minScoreValue = 1000;  // 画面最下部での最低点
    [SerializeField] private float bottomY = -5.5f;      // 画面の下端
    [SerializeField] private long powerToScoreValue = 2000;

    [Header("UI Reference")]
    [SerializeField] private GameObject floatingScorePrefab; // スコアを表示する3Dテキストプレハブ
    private PlayerStatusManager _status;

    void Awake()
    {
        _status = GetComponent<PlayerStatusManager>();
    }
    public void HandleItemCollision(Collider2D collision)
    {
        ItemController item = collision.GetComponent<ItemController>();
        if (item == null) return;

        // Instance ではなく Awake で取得した _status を使用
        if (_status == null) return;

        ItemController.ITEM_TYPE type = item.GetItemType();
        float itemY = collision.transform.position.y;
        long finalScore = 0;

        switch (type)
        {
            case ItemController.ITEM_TYPE.SCORE_UP:
                finalScore = CalculateScore(itemY, item.CollectLineY);
                AddScore(finalScore);
                ShowFloatingScore(finalScore, collision.transform.position);
                SEManager.Instance.Play(SEPath.SE_SCORE, 0.5f);
                break;
                /*
            case ItemController.ITEM_TYPE.POWER01:
                // Instance ではなく _status を使用
                if (!_status.AddPower(1))
                {
                    finalScore = powerToScoreValue;
                    AddScore(finalScore);
                    ShowFloatingScore(finalScore, collision.transform.position);
                }
                break;
                */

        }

        Destroy(collision.gameObject);
    }

    private long CalculateScore(float y, float lineY)
    {
        if (y >= lineY) return maxScoreValue;
        float t = Mathf.Clamp01((y - bottomY) / (lineY - bottomY));
        long score = minScoreValue + (long)((maxScoreValue - minScoreValue) * t);
        return (score / 10) * 10; // 10点単位に切り捨て
    }

    private void ShowFloatingScore(long amount, Vector3 pos)
    {
        if (floatingScorePrefab == null) return;
        GameObject textObj = Instantiate(floatingScorePrefab, pos, Quaternion.identity);
        var tm = textObj.GetComponentInChildren<TextMeshPro>();
        if (tm != null)
        {
            tm.text = amount.ToString();
            // 上部回収（最大点）なら黄色にする演出
            if (amount >= maxScoreValue) tm.color = Color.yellow;
        }
    }

    private void AddScore(long amount)
    {
        ScoreManager.Instance?.AddScore(amount); //
    }
}