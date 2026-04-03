using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class BossLifeCountUI : MonoBehaviour
{
    public static BossLifeCountUI Instance;

    [Header("References")]
    public TextMeshProUGUI nameText;
    public Transform starParent;
    public GameObject starPrefab;
    public CanvasGroup canvasGroup;

    [Header("Position Settings")]
    public float worldCenterX = -2.0f;
    public float leftOffset = -180f;
    public float topOffset = 20f;

    private EnemyStatus targetStatus;
    private RectTransform rectTransform;
    private Camera mainCamera;
    private List<BossStarItem> activeStars = new List<BossStarItem>();

    // --- 追加：非表示フラグ ---
    private bool isHiding = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        rectTransform = GetComponent<RectTransform>();
        mainCamera = Camera.main;
        canvasGroup.alpha = 0f;
    }

    public void Initialize(EnemyStatus status)
    {
        targetStatus = status;
        isHiding = false; // 初期化時にフラグをリセット
        nameText.text = status.bossName;
        nameText.color = new Color(nameText.color.r, nameText.color.g, nameText.color.b, 0);

        StartCoroutine(FadeInText());
        UpdateStars();
    }

    // --- 追加：外部から非表示を指示するメソッド ---
    public void Hide()
    {
        isHiding = true;
    }

    void Update()
    {
        // --- 修正ポイント：ターゲットがいない、または非表示フラグが立っている場合はフェードアウト ---
        if (targetStatus == null || isHiding)
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 0f, Time.deltaTime * 3f);
            return;
        }

        // ターゲットがいる場合は表示する
        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 1f, Time.deltaTime * 3f);

        // 位置の同期
        Vector3 worldPos = new Vector3(worldCenterX, 0, 0);
        Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);
        float uiPosX = (screenPos.x - Screen.width / 2f) + leftOffset;
        rectTransform.anchoredPosition = new Vector2(uiPosX, -topOffset);

        UpdateStars();
    }

    void UpdateStars()
    {
        if (targetStatus == null) return;

        int currentLife = targetStatus.GetRemainingLifeCount();

        while (activeStars.Count > currentLife)
        {
            BossStarItem lastStar = activeStars[activeStars.Count - 1];
            activeStars.RemoveAt(activeStars.Count - 1);
            lastStar.Break();
        }

        while (activeStars.Count < currentLife)
        {
            GameObject obj = Instantiate(starPrefab, starParent);
            BossStarItem item = obj.GetComponent<BossStarItem>();
            activeStars.Add(item);
            item.FadeIn();
        }
    }

    IEnumerator FadeInText()
    {
        float elapsed = 0f;
        while (elapsed < 0.5f)
        {
            elapsed += Time.deltaTime;
            nameText.color = new Color(nameText.color.r, nameText.color.g, nameText.color.b, elapsed / 0.5f);
            yield return null;
        }
        nameText.color = new Color(nameText.color.r, nameText.color.g, nameText.color.b, 1f);
    }
}