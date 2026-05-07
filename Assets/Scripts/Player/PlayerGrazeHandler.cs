using UnityEngine;
using KanKikuchi.AudioManager;
using System.Collections.Generic;

public class PlayerGrazeHandler : MonoBehaviour
{
    [Header("References")]
    public GameObject grazeEffectPrefab; // 生成するエフェクトのプレハブ

    private Dictionary<Collider2D, int> laserGrazeFrames = new Dictionary<Collider2D, int>();
    private PlayerMove _playerMove;
    private PlayerHitHandler _hitHandler;
    private string _targetBulletTag; // 自分が避けるべき敵弾のタグ
    private string _targetLaserTag;  // 自分が避けるべき敵レーザーのタグ（必要であれば）
    private void Start()
    {
        _playerMove = GetComponentInParent<PlayerMove>();
        _hitHandler = GetComponentInParent<PlayerHitHandler>();

        // ★ 追加：自分のPlayerIDを確認し、相手の弾のタグを決定する
        var myStatus = GetComponentInParent<PlayerStatusManager>();
        int id = (myStatus != null) ? myStatus.playerId : 1;

        // P1ならP2の弾(EnemyBullet)を、P2ならP1の弾(PlayerBullet)をグレイズ対象にする
        _targetBulletTag = (id == 1) ? "EnemyBullet" : "PlayerBullet";

        // レーザーも同様に分ける場合は設定（現在は"Laser"固定の想定）
        _targetLaserTag = "Laser";
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag(_targetBulletTag))
        {
            // ★ 修正：EnemyBullet ではなく DanmakuBullet を取得する
            DanmakuBullet bullet = collision.GetComponent<DanmakuBullet>();

            if (bullet != null && bullet.TryGraze())
            {
                DoGraze(collision.transform.position);
            }
        }
    }

    // --- PlayerGrazeHandler.cs 修正版 ---

    private void OnTriggerStay2D(Collider2D collision)
    {
        // タグが一致しているか確認
        if (collision.CompareTag(_targetBulletTag))
        {
            // ★ 修正ポイント：多段ヒットさせたいオブジェクトかどうかを判定する
            // レーザー(Laser)や防御フィールド(DefensiveField)などが付いている場合のみ多段処理を行う
            bool isMultiHitObject = collision.GetComponent<DefensiveField>() != null ||
                                    collision.CompareTag(_targetLaserTag); // "Laser"タグも一応残しておく

            if (isMultiHitObject)
            {
                if (!laserGrazeFrames.ContainsKey(collision) || Time.frameCount - laserGrazeFrames[collision] >= 3)
                {
                    Vector3 closestPoint = collision.ClosestPoint(transform.position);
                    DoGraze(closestPoint);
                    laserGrazeFrames[collision] = Time.frameCount;
                }
            }
            // 普通の弾(DanmakuBulletのみ)の場合は、OnTriggerStayでは何もしない
            // これにより Enter 時の 1回きりになります
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag(_targetBulletTag))
        {
            laserGrazeFrames.Remove(collision);
        }
    }

    private void DoGraze(Vector3 targetPos)
    {
        // ラウンド終了時や被弾中（Normal以外）は判定を行わない
        if (!PlayerMove.CanShoot) return;
        if (_hitHandler != null && _hitHandler.currentState != PlayerHitHandler.PlayerState.Normal) return;

        // --- 1. 音の演出 (SFX) ---
        if (SEManager.Instance != null)
            SEManager.Instance.Play(SEPath.SE_GRAZE, 0.4f);

        // --- 2. 判定の通知 (ScoreManagerへの加算等) ---
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.AddGraze();
        }
        if (_playerMove != null)
        {
            // 直接代入から関数呼び出しに変更
            _playerMove.AddUltimateEnergy(1f); // グレイズ1回につき5%
        }
        // --- 3. 視覚演出 (VFX) ---
        if (grazeEffectPrefab != null)
        {
            // 自機と弾幕の中間地点にエフェクトを生成
            Vector3 grazePos = (transform.position + targetPos) / 2f;
            Instantiate(grazeEffectPrefab, grazePos, Quaternion.identity);
        }

        // ★ 将来的にここで「エネルギー上昇」などの処理を追加します
        Debug.Log("Graze Detected!");
    }
}