using UnityEngine;
using KanKikuchi.AudioManager;
using System.Collections.Generic;

public class PlayerGrazeHandler : MonoBehaviour
{
    [Header("References")]
    public GameObject grazeEffectPrefab;

    // ★追加：各レーザー（Collider）が最後にグレイズを発生させたフレームを記録
    private Dictionary<Collider2D, int> laserGrazeFrames = new Dictionary<Collider2D, int>();

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 1. 通常弾（EnemyBullet）の処理
        if (collision.CompareTag("EnemyBullet"))
        {
            EnemyBullet bullet = collision.GetComponent<EnemyBullet>();

            // まだグレイズされていない弾かチェック（弾側のフラグを使用）
            if (bullet != null && bullet.TryGraze())
            {
                DoGraze(collision.transform.position);
            }
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        // 2. レーザー（Laser）の処理
        if (collision.CompareTag("Laser"))
        {
            // 前回のグレイズから3フレーム以上経過しているかチェック
            if (!laserGrazeFrames.ContainsKey(collision) || Time.frameCount - laserGrazeFrames[collision] >= 3)
            {
                // レーザーは長いので、自機に最も近い点を接触位置として取得
                Vector3 closestPoint = collision.ClosestPoint(transform.position);
                DoGraze(closestPoint);

                // 現在のフレームを記録
                laserGrazeFrames[collision] = Time.frameCount;
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        // レーザーの範囲から出たら記録を削除
        if (collision.CompareTag("Laser"))
        {
            laserGrazeFrames.Remove(collision);
        }
    }

    private void DoGraze(Vector3 targetPos)
    {
        // SE再生
        if (SEManager.Instance != null)
            SEManager.Instance.Play(SEPath.SE_GRAZE, 0.4f);

        // スコア加算
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.AddGraze();
        }

        // エフェクト生成
        if (grazeEffectPrefab != null)
        {
            // 自機とレーザー（または弾）の中間地点にエフェクトを配置
            Vector3 grazePos = (transform.position + targetPos) / 2f;
            Instantiate(grazeEffectPrefab, grazePos, Quaternion.identity);
        }
    }
}