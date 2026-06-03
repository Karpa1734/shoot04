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

    // 🌟【名前修正】：インスペクター上で分かりやすいように spellNameBG に変更
    [Header("--- VJT Mirror Visual Settings ---")]
    [Tooltip("Textの背景にある『SpellNameBG』の RectTransform をここにアタッチしてください")]
    public RectTransform spellNameBG;

    [Header("Position Settings (Base For Right Side / 2P)")]
    public Vector2 startPos = new Vector2(400, -450);
    public Vector2 targetPos = new Vector2(400, 400);

    [Header("Result UI Elements")]
    public GameObject resultRoot;
    public CanvasGroup resultCanvasGroup;
    public TextMeshProUGUI resultHeaderText;
    public TextMeshProUGUI resultScoreText;
    public TextMeshProUGUI clearTimeText;
    public TextMeshProUGUI realTimeText;

    private Coroutine resultCoroutine;
    private readonly string cyanColorTag = "<color=#00FFFF>";
    private readonly string colorEndTag = "</color>";
    private bool isExiting = false;
    private Coroutine currentAnimation;

    private int currentActivePlayerId = 1;
    private Vector2 actualStartPos;
    private Vector2 actualTargetPos;

    void Awake()
    {
        Instance = this;
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    /// <summary>
    /// 🌟【仕様完全適合】：スペカ発動時に呼び出され、1P/2Pに応じて背景の反転とテキストの左右詰めを自動判定
    /// </summary>
    public void DisplaySpell(string spellName, int getCount, int challengeCount, float initialBonus, bool isFailed, int playerId)
    {
        gameObject.SetActive(true);
        isExiting = false;
        currentActivePlayerId = playerId;

        // 🌟【1P (左側プレイヤー) 発動時の処理】
        if (currentActivePlayerId == 1)
        {
            // 1. 座標を画面左側へミラー反転
            actualStartPos = new Vector2(-Mathf.Abs(startPos.x), startPos.y);
            actualTargetPos = new Vector2(-Mathf.Abs(targetPos.x), targetPos.y);

            // 2. テキスト表示を「左詰め（Left）」にカチッと統一
            spellNameText.alignment = TextAlignmentOptions.Left;
            bonusText.alignment = TextAlignmentOptions.Left;
            historyText.alignment = TextAlignmentOptions.Left;

            // 3. 🌟【反転処理】：背景（SpellNameBG）のXスケールを「-1」にして左側用に反転！
            if (spellNameBG != null)
            {
                spellNameBG.localScale = new Vector3(-3.5f, 3.5f, 1f);
            }
        }
        // 🌟【2P (右側プレイヤー / ストーリーボス) 発動時の通常処理】
        else
        {
            // 1. 座標を従来通り画面右側へ配置
            actualStartPos = new Vector2(Mathf.Abs(startPos.x), startPos.y);
            actualTargetPos = new Vector2(Mathf.Abs(targetPos.x), targetPos.y);

            // 2. テキスト表示を従来通り「右詰め（Right）」に統一
            spellNameText.alignment = TextAlignmentOptions.Right;
            bonusText.alignment = TextAlignmentOptions.Right;
            historyText.alignment = TextAlignmentOptions.Right;

            // 3. 🌟【通常復帰】：背景（SpellNameBG）のスケールを通常の「1」に戻す
            if (spellNameBG != null)
            {
                spellNameBG.localScale = new Vector3(3.5f, 3.5f, 1f);
            }
        }

        if (currentAnimation != null) StopCoroutine(currentAnimation);
        currentAnimation = StartCoroutine(SpellInRoutine(spellName, getCount, challengeCount, initialBonus, isFailed));

        if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.CARDCALL, 0.5f);
    }

    public void DisplaySpell(string spellName, int getCount, int challengeCount, float initialBonus, bool isFailed)
    {
        DisplaySpell(spellName, getCount, challengeCount, initialBonus, isFailed, 2);
    }

    public void HideSpell()
    {
        if (!gameObject.activeInHierarchy || isExiting || (canvasGroup != null && canvasGroup.alpha <= 0))
        {
            return;
        }

        if (currentAnimation != null) StopCoroutine(currentAnimation);
        isExiting = true;
        currentAnimation = StartCoroutine(SpellOutRoutine());
    }

    public void UpdateBonusText(int currentBonus, bool isFailed = false)
    {
        if (isFailed)
        {
            bonusText.text = $"{cyanColorTag}Bonus{colorEndTag}  Failed";
        }
        else
        {
            string scoreStr = currentBonus.ToString().PadLeft(6, ' ');

            // 1P(左詰め)の時、mspaceの右側に寄ってしまうガタつきを防ぐ調整
            if (currentActivePlayerId == 1)
            {
                string scoreStrLeft = currentBonus.ToString();
                bonusText.text = $"{cyanColorTag}Bonus{colorEndTag}  <mspace=0.5em>{scoreStrLeft}</mspace>";
            }
            else
            {
                bonusText.text = $"{cyanColorTag}Bonus{colorEndTag}  <mspace=0.5em>{scoreStr}</mspace>";
            }
        }
    }

    IEnumerator SpellInRoutine(string name, int get, int challenge, float initialBonus, bool isFailed)
    {
        spellNameText.text = name;
        historyText.text = $"{cyanColorTag}History{colorEndTag}  {get:D3}/{challenge:D3}";

        UpdateBonusText((int)initialBonus, isFailed);
        bonusText.gameObject.SetActive(false);
        historyText.gameObject.SetActive(false);

        rectTransform.anchoredPosition = actualStartPos;
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

        yield return new WaitForSeconds(0.6f);
        elapsed = 0f;
        while (elapsed < 0.67f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 0.67f;
            float easedT = Mathf.Sin(t * Mathf.PI * 0.5f);

            rectTransform.anchoredPosition = Vector2.Lerp(actualStartPos, actualTargetPos, easedT);
            yield return null;
        }
        //bonusText.gameObject.SetActive(true);
        //historyText.gameObject.SetActive(true);
    }

    IEnumerator SpellOutRoutine()
    {
        float elapsed = 0f;
        Vector2 startPosition = rectTransform.anchoredPosition;

        float exitXOffset = (currentActivePlayerId == 1) ? -600f : 600f;
        Vector2 exitPos = actualTargetPos + new Vector2(exitXOffset, 0f);

        float startAlpha = canvasGroup.alpha;

        while (elapsed < 0.33f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 0.33f;
            rectTransform.anchoredPosition = Vector2.Lerp(startPosition, exitPos, t);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        CheckAndDisableAll();
    }

    public void ShowSpellResult(int bonus, float clearTime, float realTime, bool isSuccess, bool isTimeUp = false)
    {
        gameObject.SetActive(true);
        if (resultCoroutine != null) StopCoroutine(resultCoroutine);

        resultRoot.SetActive(true);
        if (resultCanvasGroup != null) resultCanvasGroup.alpha = 1f;

        RectTransform resultRect = resultRoot.GetComponent<RectTransform>();
        if (resultRect != null)
        {
            float resultX = (currentActivePlayerId == 1) ? -Mathf.Abs(targetPos.x) : Mathf.Abs(targetPos.x);
            resultRect.anchoredPosition = new Vector2(resultX, resultRect.anchoredPosition.y);

            if (currentActivePlayerId == 1)
            {
                resultHeaderText.alignment = TextAlignmentOptions.Left;
                clearTimeText.alignment = TextAlignmentOptions.Left;
                realTimeText.alignment = TextAlignmentOptions.Left;
            }
            else
            {
                resultHeaderText.alignment = TextAlignmentOptions.Right;
                clearTimeText.alignment = TextAlignmentOptions.Right;
                realTimeText.alignment = TextAlignmentOptions.Right;
            }
        }

        if (isSuccess)
        {
            resultHeaderText.text = "<color=#00FFFF>GET SPELL CARD BONUS!!</color>";
            resultScoreText.text = bonus.ToString("N0");
            resultScoreText.gameObject.SetActive(true);
            if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.GETSPELLCARD, 0.6f);
        }
        else
        {
            if (isTimeUp)
            {
                if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.FAIL, 0.6f);
            }
            else
            {
                if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.SHOT1, 0.6f);
            }

            resultHeaderText.text = "<color=#808080>BONUS FAILED...</color>";
            resultScoreText.gameObject.SetActive(false);
        }

        clearTimeText.text = $"撃破時間  {clearTime:F2}s";
        realTimeText.text = $"実時間    {realTime:F2}s";

        resultCoroutine = StartCoroutine(ResultDisplayRoutine());
    }

    IEnumerator ResultDisplayRoutine()
    {
        yield return new WaitForSeconds(3.0f);

        if (resultCanvasGroup != null)
        {
            float fadeDuration = 1.0f;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
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

    private void CheckAndDisableAll()
    {
        if (canvasGroup.alpha <= 0 && !resultRoot.activeInHierarchy)
        {
            gameObject.SetActive(false);
        }
    }
}