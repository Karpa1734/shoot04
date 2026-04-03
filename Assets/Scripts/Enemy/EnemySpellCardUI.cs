using KanKikuchi.AudioManager;
using System.Collections;
using TMPro;
using UnityEngine;

public class EnemySpellCardUI : MonoBehaviour
{
    public static EnemySpellCardUI Instance;

    [Header("UI Components")]
    public CanvasGroup canvasGroup;
    public RectTransform rectTransform;
    public TextMeshProUGUI spellNameText;
    public TextMeshProUGUI bonusText;
    public TextMeshProUGUI historyText;

    [Header("Position Settings")]
    public Vector2 startPos = new Vector2(400, -450);
    public Vector2 targetPos = new Vector2(400, 400);

    [Header("Result UI Elements")]
    public GameObject resultRoot;
    public CanvasGroup resultCanvasGroup; // 追加：リザルト用のCanvasGroup
    public TextMeshProUGUI resultHeaderText; // "GET SPELL CARD BONUS!!" または "BONUS FAILED"
    public TextMeshProUGUI resultScoreText;
    public TextMeshProUGUI clearTimeText;
    public TextMeshProUGUI realTimeText;
    // リザルト表示用コルーチンの参照を保持
    private Coroutine resultCoroutine;
    // --- 水色のカラーコード設定 ---
    private readonly string cyanColorTag = "<color=#00FFFF>";
    private readonly string colorEndTag = "</color>";
    private bool isExiting = false; // 退場アニメーション中かどうかのフラグ
    private Coroutine currentAnimation;

    void Awake()
    {
        Instance = this;
        // 初期状態は透明にしておく
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    // スペルカード開始時に EnemyStatus.cs から呼ばれる
    public void DisplaySpell(string spellName, int getCount, int challengeCount, float initialBonus, bool isFailed)
    {
        gameObject.SetActive(true);
        isExiting = false; // 出現時はフラグをリセット

        if (currentAnimation != null) StopCoroutine(currentAnimation);
        currentAnimation = StartCoroutine(SpellInRoutine(spellName, getCount, challengeCount, initialBonus, isFailed));

        SEManager.Instance.Play(SEPath.CARDCALL, 0.5f);
    }

    public void HideSpell()
    {
        // 修正：既に退場中、またはオブジェクトが非アクティブ、または既に消えている場合は何もしない
        if (!gameObject.activeInHierarchy || isExiting || (canvasGroup != null && canvasGroup.alpha <= 0))
        {
            return;
        }

        if (currentAnimation != null) StopCoroutine(currentAnimation);
        isExiting = true; // 退場開始
        currentAnimation = StartCoroutine(SpellOutRoutine());
    }

    // ボーナス値を外部（EnemyStatus の Update 等）から更新する用
    public void UpdateBonusText(int currentBonus, bool isFailed = false)
    {
        if (isFailed)
        {
            // Failed の時は <mspace> を外して表示する
            // これで文字の間隔が自然な見た目になります
            // 右端を合わせたい場合は、先頭に半角スペースを入れて調整します
            bonusText.text = $"{cyanColorTag}Bonus{colorEndTag}  Failed";
        }
        else
        {
            // 数値の時は <mspace> を使い、数字の幅を固定してガタつきを防ぎます
            // 前回の「6文字分」という指定に合わせて PadLeft(6) にしています
            string scoreStr = currentBonus.ToString().PadLeft(6, ' ');
            bonusText.text = $"{cyanColorTag}Bonus{colorEndTag}  <mspace=0.5em>{scoreStr}</mspace>";
        }
    }
    // 引数に initialBonus を追加
    IEnumerator SpellInRoutine(string name, int get, int challenge, float initialBonus, bool isFailed)
    {
        spellNameText.text = name;
        historyText.text = $"{cyanColorTag}History{colorEndTag}  {get:D3}/{challenge:D3}";

        // --- 修正：常に false ではなく、現在の状態を表示する ---
        UpdateBonusText((int)initialBonus, isFailed);
        // --- 修正ポイント：出現・移動中はボーナスと履歴を隠す ---
        bonusText.gameObject.SetActive(false);
        historyText.gameObject.SetActive(false);
        // 1. 右下に出現
        rectTransform.anchoredPosition = startPos;
        rectTransform.localScale = Vector3.one * 1.5f;
        canvasGroup.alpha = 0f;

        float elapsed = 0f;
        while (elapsed < 0.33f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 0.33f;
            rectTransform.localScale = Vector3.Lerp(Vector3.one * 1.5f, Vector3.one, t);
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
            yield return null;
        }

        // 2. 右上へ移動
        yield return new WaitForSeconds(0.6f);
        elapsed = 0f;
        while (elapsed < 0.67f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 0.67f;
            float easedT = Mathf.Sin(t * Mathf.PI * 0.5f);
            rectTransform.anchoredPosition = Vector2.Lerp(startPos, targetPos, easedT);
            yield return null;
        }
        bonusText.gameObject.SetActive(true);
        historyText.gameObject.SetActive(true);
    }

    IEnumerator SpellOutRoutine()
    {
        float elapsed = 0f;
        Vector2 startPosition = rectTransform.anchoredPosition; // 現在の座標から開始
        Vector2 exitPos = targetPos + new Vector2(600f, 0f);

        // 現在のアルファ値を保持（途中からでも滑らかに消えるようにする）
        float startAlpha = canvasGroup.alpha;

        while (elapsed < 0.33f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 0.33f;
            // targetPos固定ではなく、現在の位置(startPosition)から逃げるように変更するとより滑らかです
            rectTransform.anchoredPosition = Vector2.Lerp(startPosition, exitPos, t);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        // isExiting はリセットせず、次の DisplaySpell まで維持することで2重呼び出しを防ぎます

        CheckAndDisableAll();
    
    }

    // 第5引数に isTimeUp を追加（デフォルトは false）
    public void ShowSpellResult(int bonus, float clearTime, float realTime, bool isSuccess, bool isTimeUp = false)
    {// ★ 追加：コルーチンを開始するために、まずオブジェクトをアクティブにする
        gameObject.SetActive(true);
        if (resultCoroutine != null) StopCoroutine(resultCoroutine);

        resultRoot.SetActive(true);
        if (resultCanvasGroup != null) resultCanvasGroup.alpha = 1f;

        if (isSuccess)
        {
            resultHeaderText.text = "<color=#00FFFF>GET SPELL CARD BONUS!!</color>";
            resultScoreText.text = bonus.ToString("N0");
            resultScoreText.gameObject.SetActive(true);
            // 取得成功時のSE
            SEManager.Instance.Play(SEPath.GETSPELLCARD, 0.6f);
        }
        else
        {
            // --- 修正：失敗理由（タイムアップかそれ以外か）で音を出し分ける ---
            if (isTimeUp)
            {
                // タイムアップ失敗時のSE（環境に合わせて SEPath を調整してください）
                SEManager.Instance.Play(SEPath.FAIL, 0.6f);
            }
            else
            {
                // 被弾・ボムでの失敗時のSE
                SEManager.Instance.Play(SEPath.SHOT1, 0.6f);
            }

            resultHeaderText.text = "<color=#808080>BONUS FAILED...</color>";
            resultScoreText.gameObject.SetActive(false);
        }

        clearTimeText.text = $"撃破時間  {clearTime:F2}s";
        // ここで渡された実時間を表示
        realTimeText.text = $"実時間    {realTime:F2}s";

        resultCoroutine = StartCoroutine(ResultDisplayRoutine());
    }

    IEnumerator ResultDisplayRoutine()
    {
        // 1. 3秒間は不透明なまま表示を維持
        yield return new WaitForSeconds(3.0f);

        // 2. 1秒かけてフェードアウト
        if (resultCanvasGroup != null)
        {
            float fadeDuration = 1.0f;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                // 1.0 から 0.0 へ緩やかに変化
                resultCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                yield return null;
            }
            resultCanvasGroup.alpha = 0f;
        }

        Debug.Log("Result Hidden!");
        resultRoot.SetActive(false);
        resultCoroutine = null;

        CheckAndDisableAll();
    }
    // 両方の演出が終わっている場合のみ、オブジェクト全体をオフにする
    private void CheckAndDisableAll()
    {
        // 1. スペル名表示が消えている（alphaが0）
        // 2. リザルト表示が消えている（resultRootが非アクティブ）
        // この2つの条件が揃ったときだけ全体を非アクティブにする
        if (canvasGroup.alpha <= 0 && !resultRoot.activeInHierarchy)
        {
            gameObject.SetActive(false);
        }
    }

}