using UnityEngine;
using UnityEngine.UI;

public class EnemyMarker : MonoBehaviour
{
    // --- 変更点：シングルトン(Instance)を削除 ---

    [Header("References")]
    public EnemyStatus targetStatus;
    public Image markerImage;
    public CanvasGroup canvasGroup;

    [Header("Display Settings")]
    public float bottomOffset = 20f;

    private RectTransform rectTransform;
    private Camera mainCamera;
    private float flickerTimer = 0f;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        mainCamera = Camera.main;
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    public void SetTarget(EnemyStatus status) => targetStatus = status;

    void Update()
    {
        // --- 変更点：ターゲットがいなくなったら、UIオブジェクト自体を削除する ---
        if (targetStatus == null)
        {
            Destroy(gameObject);
            return;
        }

        // 1. 位置の同期
        Vector3 worldPos = targetStatus.transform.position;
        Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);
        float uiPosX = screenPos.x - (Screen.width / 2f);
        rectTransform.anchoredPosition = new Vector2(uiPosX, bottomOffset);

        // 2. 表示範囲判定（プレイエリア外なら隠す）
        if (worldPos.x < -6f || worldPos.x > 2f)
        {
            canvasGroup.alpha = 0f;
            return;
        }
        else
        {
            canvasGroup.alpha = 1f;
        }

        // 3. 透明度の計算
        float alpha = 0.6f;
        if (PlayerMove.Instance != null)
        {
            float distToPlayer = Mathf.Abs(worldPos.x - PlayerMove.Instance.transform.position.x);
            alpha = (144f + distToPlayer * 10f) / 255f;
        }
        alpha = Mathf.Clamp(alpha, 0.4f, 1.0f);

        // 4. 点滅処理（2割以下でパルス）
        Color finalColor = Color.white;
        if (targetStatus.currentHP < targetStatus.flickerLifeThreshold)
        {
            float flickerSpeed = 5f + (1f - targetStatus.currentHP / targetStatus.flickerLifeThreshold) * 10f;
            flickerTimer += Time.deltaTime * flickerSpeed;

            float wave = (Mathf.Sin(flickerTimer * Mathf.PI) + 1f) / 2f;
            finalColor = Color.Lerp(new Color(1f, 0.4f, 0.4f), Color.white, wave);
        }

        if (markerImage != null)
        {
            markerImage.color = new Color(finalColor.r, finalColor.g, finalColor.b, alpha);
        }
    }
}