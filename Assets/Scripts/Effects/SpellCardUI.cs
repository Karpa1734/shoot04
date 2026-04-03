using UnityEngine;
using TMPro; // TextMeshProを使用するために必要
using System.Collections;

public class SpellCardUI : MonoBehaviour
{
    [Header("UI Components")]
    public TextMeshProUGUI spellNameText; // TextMeshProUGUIに変更
    public TextMeshProUGUI timerText;     // TextMeshProUGUIに変更
    public CanvasGroup canvasGroup;
    public RectTransform rectTransform;

    private float remainingTime;
    private bool isCounting = false;

    private readonly string cyanColorTag = "<color=#00FFFF>";
    private readonly string colorEndTag = "</color>";
    // 座標設定（Canvasの解像度に合わせてインスペクターで調整してください）
    [Header("Position Settings")]
    public Vector2 centerLeftPos = new Vector2(-400, 0);
    public Vector2 bottomLeftPos = new Vector2(-400, -400);

    public void DisplaySpell(string spellName, float duration)
    {
        spellNameText.text = spellName;
        remainingTime = duration;
        isCounting = true;

        StopAllCoroutines();
        StartCoroutine(SpellUIAnimationRoutine());
    }

    void Update()
    {
        if (isCounting && remainingTime > 0)
        {
            remainingTime -= Time.deltaTime;
            if (remainingTime < 0) remainingTime = 0;

            // image_96b826.png の形式を再現
            timerText.text = $"{cyanColorTag}Spell Time{colorEndTag}  {remainingTime:F2} [s]";
        }
    }

    IEnumerator SpellUIAnimationRoutine()
    {
        // 1. 初期状態設定
        canvasGroup.alpha = 0f;
        rectTransform.anchoredPosition = centerLeftPos;
        rectTransform.localScale = Vector3.one * 5f;

        // 2. 縮小フェードイン
        float elapsed = 0f;
        while (elapsed < 0.3f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 0.3f;
            rectTransform.localScale = Vector3.Lerp(Vector3.one * 5f, Vector3.one, t);
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
            yield return null;
        }

        // 3. 左下へスライド移動
        yield return new WaitForSeconds(0.3f);
        elapsed = 0f;
        Vector2 startPos = rectTransform.anchoredPosition;
        while (elapsed < 0.4f)
        {
            elapsed += Time.deltaTime;
            rectTransform.anchoredPosition = Vector2.Lerp(startPos, bottomLeftPos, elapsed / 0.4f);
            yield return null;
        }

        // 4. カウントダウン終了まで待機
        while (remainingTime > 0)
        {
            yield return null;
        }

        // 5. フェードアウト演出
        yield return new WaitForSeconds(0.5f);
        elapsed = 0f;
        Vector2 finalPos = rectTransform.anchoredPosition;
        while (elapsed < 0.5f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 0.5f;
            rectTransform.anchoredPosition = Vector2.Lerp(finalPos, finalPos + Vector2.left * 750f, t);
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
            yield return null;
        }

        // --- 修正：すべての演出が終わったら非アクティブにする ---
        isCounting = false;
        this.gameObject.SetActive(false);
    }
}