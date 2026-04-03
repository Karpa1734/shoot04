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

    public void HandleItemCollision(Collider2D collision)
    {
        Debug.Log($"[ItemCheck] 衝突を検知: {collision.name}"); // ←追加1

        ItemController item = collision.GetComponent<ItemController>();
        if (item == null)
        {
            Debug.LogWarning("[ItemCheck] ItemControllerが見つかりません"); // ←追加2
            return;
        }

        if (PlayerStatusManager.Instance == null)
        {
            Debug.LogWarning("[ItemCheck] PlayerStatusManager.Instance が null です"); // ←追加3
            return;
        }

        ItemController.ITEM_TYPE type = item.GetItemType();
        Debug.Log($"[ItemCheck] アイテム取得: {type}"); // ←追加4
        float itemY = collision.transform.position.y;
        long finalScore = 0;

        switch (type)
        {
            case ItemController.ITEM_TYPE.SCORE_UP:
                // ★高さに応じてスコアを計算
                finalScore = CalculateScore(itemY, item.CollectLineY);
                AddScore(finalScore);
                ShowFloatingScore(finalScore, collision.transform.position);
                SEManager.Instance.Play(SEPath.SE_SCORE, 0.5f);
                break;
            case ItemController.ITEM_TYPE.SCORE00:
                AddScore(100);
                SEManager.Instance.Play(SEPath.SE_SCORE, 0.5f);
                break;
            case ItemController.ITEM_TYPE.POWER01:
                if (!PlayerStatusManager.Instance.AddPower(1)) //
                {
                    finalScore = powerToScoreValue;
                    AddScore(finalScore);
                    ShowFloatingScore(finalScore, collision.transform.position);
                }
                break;
            // --- ★追加：残機エクステンド ---
            case ItemController.ITEM_TYPE.LIFE_UP01:
            //case ItemController.ITEM_TYPE.LIFE_UP02:
                PlayerStatusManager.Instance.AddLife(1);
                SEManager.Instance.Play(SEPath.SE_EXTEND2); // エクステンド音
                break;

            // --- ★追加：ボムアップ ---
            case ItemController.ITEM_TYPE.BOMB_UP01:
            //case ItemController.ITEM_TYPE.BOMB_UP02:
                PlayerStatusManager.Instance.AddBomb(1);
                SEManager.Instance.Play(SEPath.GETSPELLCARD);
                break;
            // ... 他のリソース系は以前のコードと同様
            case ItemController.ITEM_TYPE.LIFE_UP02: // 残機のかけら
                PlayerStatusManager.Instance.AddLifePiece(1);
                break;

            case ItemController.ITEM_TYPE.BOMB_UP02: // ボムのかけら
                PlayerStatusManager.Instance.AddBombPiece(1);
                break;
        }

        Destroy(collision.gameObject); // ハンドラー側で確実に消す
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