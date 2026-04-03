using UnityEngine;

public class SpellRingEffect : MonoBehaviour
{
    [Header("References")]
    public EnemyStatus bossStatus;
    private SpriteRenderer sr;

    [Header("Settings")]
    public float maxScale = 5.0f;     // タイマー満了時のサイズ
    public float minScale = 1.0f;     // タイマー0秒時のサイズ
    public float rotateSpeed = 30f;   // 回転速度

    private float initialTimeLimit;
    private bool isActive = false;
    private float currentVisualScale;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        sr.enabled = false;
        transform.localScale = Vector3.zero;
    }

    // スペル開始時に呼ばれる
    public void Activate(float timeLimit)
    {
        initialTimeLimit = timeLimit;
        isActive = true;
        sr.enabled = true;
        // 最初は0からグワッと広がる演出
        currentVisualScale = 0f;
    }

    // 撃破または時間切れで呼ばれる
    public void Deactivate()
    {
        isActive = false;
        sr.enabled = false;
        transform.localScale = Vector3.zero;
    }

    void Update()
    {
        if (!isActive || bossStatus == null) return;

        // 1. 回転
        transform.Rotate(0, 0, rotateSpeed * Time.deltaTime);

        // 2. スケール制御
        if (currentVisualScale < 1.0f)
        {
            // 出現時の拡大（0.5秒くらいで目標サイズへ）
            currentVisualScale += Time.deltaTime * 2f;
        }

        // タイマーの割合（1.0 -> 0.0）を計算
        float progress = Mathf.Clamp01(bossStatus.currentTimer / initialTimeLimit);

        // 割合に応じて minScale ～ maxScale の間で縮小
        float targetSize = Mathf.Lerp(minScale, maxScale, progress) * currentVisualScale;
        transform.localScale = new Vector3(targetSize, targetSize, 1f);

        // 3. 消失判定（タイマーがほぼ0になったら消す）
        if (bossStatus.currentTimer <= 0)
        {
            Deactivate();
        }
    }
}