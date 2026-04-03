using KanKikuchi.AudioManager;
using System.Collections;
using TMPro;
using UnityEngine;

public class BossTimerUI : MonoBehaviour
{
    public static BossTimerUI Instance;
    [Header("References")]
    public EnemyStatus targetStatus;
    public TextMeshProUGUI timerText;
    public CanvasGroup canvasGroup;

    [Header("Color Settings")]
    private Color normalColor = Color.white;
    private Color warningColor = new Color(1f, 128f / 255f, 128f / 255f);
    private Color dangerColor = new Color(1f, 64f / 255f, 64f / 255f);

    // --- 追加：SE用の設定 ---
    [Header("SE Settings")]
    public AudioSource audioSource;
    public AudioClip countNormalSE; // 10秒〜5秒用
    public AudioClip countDangerSE; // 4秒以下用

    [Header("Position Settings")]
    public float worldCenterX = -2.0f;

    public float normalTopOffset = 50f;   // 通常時
    public float spellTopOffset = 100f;  // スペルカード時
    private float currentTargetOffset;

    [Header("Animation (th13 Slide)")]
    private float t_count = 0;
    private RectTransform rectTransform;
    private Camera mainCamera;

    private int lastIntSecond = -1;
    private Vector3 originalScale;

    void Awake()
    {
        if (Instance == null) Instance = this;
        rectTransform = GetComponent<RectTransform>();
        mainCamera = Camera.main;
        canvasGroup.alpha = 0f;
        originalScale = rectTransform.localScale;
        currentTargetOffset = normalTopOffset;

        rectTransform.anchorMin = new Vector2(0.5f, 1f);
        rectTransform.anchorMax = new Vector2(0.5f, 1f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }

    public void SetPhaseType(PhaseType type)
    {
        if (type == PhaseType.SpellCard || type == PhaseType.Endurance)
            currentTargetOffset = spellTopOffset;
        else
            currentTargetOffset = normalTopOffset;
    }

    void Update()
    {
        if (targetStatus == null)
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 0f, Time.deltaTime * 2f);
            t_count = 0;
            lastIntSecond = -1;
            return;
        }

        if (canvasGroup.alpha < 1f && targetStatus.currentTimer > 0f)
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 1f, Time.deltaTime * 3f);
        }

        Vector3 worldPos = new Vector3(worldCenterX, 0, 0);
        Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);
        float uiPosX = screenPos.x - (Screen.width / 2f);

        if (t_count < 90f) t_count += Time.deltaTime * 120f;
        float py = -40f * Mathf.Sin(t_count * Mathf.Deg2Rad) + 40f;

        float smoothedOffset = Mathf.Lerp(rectTransform.anchoredPosition.y, -currentTargetOffset + py, Time.deltaTime * 3f);
        rectTransform.anchoredPosition = new Vector2(uiPosX, smoothedOffset);

        UpdateUI(targetStatus.currentTimer);

        // --- 復活：10秒以下の特殊演出（Pop & SE） ---
        int currentIntSecond = Mathf.FloorToInt(targetStatus.currentTimer);
        // 秒数が切り替わった瞬間だけ実行
        if (targetStatus.currentTimer <= 10.5f && currentIntSecond != lastIntSecond && targetStatus.currentTimer > 0)
        {
            if (currentIntSecond < 10)
            {
                StartCoroutine(PopRoutine());
                // SE再生処理を呼び出し
                PlayCountSE(currentIntSecond);
            }
            lastIntSecond = currentIntSecond;
        }
    }

    // --- 追加：SE再生メソッド ---
    void PlayCountSE(int sec)
    {
        // SEManager 自体が AudioSource を管理するため、このクラスに AudioSource は不要です。
        // そのため、↓の 1行を削除してください。
        // if (audioSource == null) return; 

        // 4秒以下で音が変わる仕様
        string clipPath = (sec > 4) ? SEPath.TIMER1 : SEPath.TIMER2;

        // 直接 SEManager を呼び出す
        SEManager.Instance.Play(clipPath, 0.5f);
    }

    IEnumerator PopRoutine()
    {
        float duration = 0.15f;
        float elapsed = 0f;
        Vector3 popScale = originalScale * 1.3f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            rectTransform.localScale = Vector3.Lerp(originalScale, popScale, elapsed / duration);
            yield return null;
        }
        rectTransform.localScale = originalScale;
    }

    void UpdateUI(float time)
    {
        if (time > 99f) time = 99.99f;
        int sec = Mathf.FloorToInt(time);
        int ms = Mathf.FloorToInt((time * 100f) % 100f);
        timerText.text = string.Format("{0:00}<size=70%>.{1:00}</size>", sec, ms);

        if (time < 5f) timerText.color = dangerColor;
        else if (time < 10f) timerText.color = warningColor;
        else timerText.color = normalColor;
    }
}