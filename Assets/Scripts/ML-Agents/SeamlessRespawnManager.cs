using System.Collections;
using UnityEngine;

public class SeamlessRespawnManager : MonoBehaviour
{
    [SerializeField] private GameObject player;
    [SerializeField] private float invincibilityDuration = 2.0f; // 無敵時間
    [SerializeField] private float respawnMoveDelay = 0.5f;     // リスポーン地点への移動演出時間

    private bool isInvincible = false;

    // プレイヤーが被弾した時に呼ばれる
    public void OnPlayerHit()
    {
        StartCoroutine(RespawnSequence());
    }

    private IEnumerator RespawnSequence()
    {
        // 1. ゲーム内タイマーを停止（敵の弾やギミックも止まる）
        Time.timeScale = 0f;

        // 2. 被弾演出（爆発エフェクトや自機の非表示など）
        Debug.Log("Player Down: Timer Paused");
        player.SetActive(false);

        // 実時間で少し待機（即リスポーンだと状況が掴めないため）
        yield return new WaitForSecondsRealtime(respawnMoveDelay);

        // 3. リスポーン位置へ移動し、自機を再表示
        ResetPlayerPosition();
        player.SetActive(true);
        SetInvincibility(true);

        // 4. 自機が動けるようになるまで待機（ここで復帰アニメーションなどを入れる）
        // ここでは「動けるようになる準備時間」として0.5秒ほど設定
        yield return new WaitForSecondsRealtime(0.5f);

        // 5. タイマー再開（シームレスにラウンド再開）
        Debug.Log("Resume: Timer Started");
        Time.timeScale = 1.0f;

        // 6. 無敵時間が終わるまで待機
        yield return new WaitForSeconds(invincibilityDuration);

        // 7. 無敵解除
        SetInvincibility(false);
        Debug.Log("Invincibility Ended");
    }

    private void ResetPlayerPosition()
    {
        // 初期位置、もしくはチェックポイントへ戻す
        player.transform.position = new Vector3(0, -4, 0);
    }

    private void SetInvincibility(bool state)
    {
        isInvincible = state;
        // 以前作成したPlayerHitHandlerの当たり判定フラグや、
        // スプライトの点滅処理をここで制御する
        var renderer = player.GetComponent<SpriteRenderer>();
        if (state)
        {
            // 点滅開始などのコルーチンを別途呼ぶ
        }
    }
}