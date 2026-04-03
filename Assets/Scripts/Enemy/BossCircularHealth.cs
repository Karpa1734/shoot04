using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class BossCircularHealth : MonoBehaviour
{
    // ピンの情報を管理するための内部構造体
    private struct MarkerInfo
    {
        public GameObject obj;
        public float threshold;
    }
    private List<MarkerInfo> activeMarkers = new List<MarkerInfo>(); // 管理用リスト
    [Header("UI References")]
    public Image healthFillImage;
    public RectTransform markerParent;
    public GameObject markerPrefab;
    public CanvasGroup canvasGroup;

    [Header("Target Settings")]
    private EnemyStatus targetEnemy;
    private RectTransform rectTransform;
    private Camera mainCamera;

    [Header("Appearance Settings")]
    public float radius = 60f;
    public Vector3 offset = Vector3.zero;
    public float appearDuration = 1.0f;

    [Header("Marker Settings")]
    public Vector3 markerScale = new Vector3(0.5f, 0.5f, 1f);

    private bool isAppearing = true;



    // 座標同期を共通化
    private void SyncPosition()
    {
        if (targetEnemy == null || mainCamera == null) return;

        // 1. ボスのワールド座標にオフセットを足してスクリーン座標に変換
        Vector2 screenPos = mainCamera.WorldToScreenPoint(targetEnemy.transform.position + offset);

        // 2. Cameraモードの場合、スクリーン座標をキャンバス内のローカル座標に変換する必要がある
        RectTransform canvasRect = transform.parent as RectTransform; // 親のCanvasのRectを取得

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPos,
            GetComponentInParent<Canvas>().worldCamera, // キャンバスに設定されたカメラ
            out Vector2 localPos))
        {
            // 3. ローカル座標を適用
            rectTransform.anchoredPosition = localPos;
        }
    }

    public void Initialize(EnemyStatus enemy, List<float> thresholds)
    {
        targetEnemy = enemy;
        rectTransform = GetComponent<RectTransform>();
        mainCamera = Camera.main;

        // 生成された瞬間に必ず透明＆空にする
        if (healthFillImage != null) healthFillImage.fillAmount = 0f;
        if (canvasGroup != null) canvasGroup.alpha = 0f;

        SyncPosition();
        InitializeMarkers(thresholds);
        markerParent.gameObject.SetActive(false);

        // ステルス状態で座標が確定してから表示
        if (canvasGroup != null) canvasGroup.alpha = 1f;

        StartCoroutine(AppearRoutine());
    }

    IEnumerator AppearRoutine()
    {
        isAppearing = true;
        float elapsed = 0f;

        // 溜まりきる目標値 (通常は1.0)
        float finalRatio = targetEnemy.maxHP > 0 ? targetEnemy.currentHP / targetEnemy.maxHP : 1f;

        while (elapsed < appearDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / appearDuration;
            // イージングをかけるとより東方らしくなります (例: t * t)
            healthFillImage.fillAmount = Mathf.Lerp(0f, finalRatio, t);
            yield return null;
        }

        healthFillImage.fillAmount = finalRatio;
        markerParent.gameObject.SetActive(true);
        isAppearing = false;
    }
    void Update()
    {
        if (targetEnemy == null)
        {
            Destroy(gameObject);
            return;
        }

        if (!isAppearing)
        {
            // --- 修正ポイント：バー全体の比率を取得 ---
            float barMax = targetEnemy.GetBarTotalMaxHP();
            float barCurrent = targetEnemy.GetBarCurrentHP();

            float ratio = barMax > 0 ? barCurrent / barMax : 0;
            healthFillImage.fillAmount = ratio;
            CheckMarkers(ratio);
        }
        SyncPosition();
    }
    // 現在の比率としきい値を比較してピンを消す
    void CheckMarkers(float currentRatio)
    {
        // 逆順でループ（削除操作を行うため）
        for (int i = activeMarkers.Count - 1; i >= 0; i--)
        {
            // 体力比率がピンの位置（しきい値）を下回ったら削除
            if (currentRatio <= activeMarkers[i].threshold)
            {
                // 単に消すだけでなく、ここでエフェクトを出しても良い
                Destroy(activeMarkers[i].obj);
                activeMarkers.RemoveAt(i);
            }
        }
    }
    void InitializeMarkers(List<float> thresholds)
    {
        // 既存のピンをクリア
        foreach (Transform child in markerParent) Destroy(child.gameObject);
        activeMarkers.Clear();

        foreach (float threshold in thresholds)
        {
            GameObject marker = Instantiate(markerPrefab, markerParent);
            RectTransform markerRect = marker.GetComponent<RectTransform>();

            // アンカーとピボットを「中心」に固定
            markerRect.anchorMin = new Vector2(0.5f, 0.5f);
            markerRect.anchorMax = new Vector2(0.5f, 0.5f);
            markerRect.pivot = new Vector2(0.5f, 0.5f);

            // --- 修正ポイント：反時計回り（CCW）の計算に変更 ---
            float angle = threshold * 360f;
            float rad = angle * Mathf.Deg2Rad;

            // X座標を「-Mathf.Sin」にすることで、上から左方向（反時計回り）へ配置します
            float x = -Mathf.Sin(rad) * radius;
            float y = Mathf.Cos(rad) * radius;

            markerRect.anchoredPosition = new Vector2(x, y);

            // --- 修正ポイント：ピンの向きも反時計回りに合わせる ---
            // 符号をプラス（angle）にすることで、反時計回りに回転させます
            markerRect.localRotation = Quaternion.Euler(0, 0, angle);
            markerRect.localScale = markerScale;

            // 管理リストに追加
            activeMarkers.Add(new MarkerInfo { obj = marker, threshold = threshold });
        }
    }
}