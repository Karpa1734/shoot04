// --- ConfigMenuManager.cs コンフィグ専用独立コントロールパネルクラス ---
using KanKikuchi.AudioManager;
using TMPro;
using UnityEngine;

public class ConfigMenuManager : MonoBehaviour
{
    [Header("UI Slots")]
    [Tooltip("現在の難易度（< EASY > 等）を表示するTMPテキスト")]
    public TextMeshProUGUI configDifficultyText;

    [Header("Parent Transition Reference")]
    [Tooltip("タイトルメニューの親オブジェクト（復帰のキック用）")]
    public GameObject titleMenuCanvas;

    private float _inputCooldownTimer = 0f;

    void OnEnable()
    {
        _inputCooldownTimer = 0.2f; // 開いた瞬間の暴発ガード
        UpdateConfigTextVisuals();
    }

    void Update()
    {
        if (_inputCooldownTimer > 0f)
        {
            _inputCooldownTimer -= Time.deltaTime;
        }

        HandleConfigMenuNavigation();
    }

    /// <summary>
    /// 🔮 コンフィグパネル独立：左右の難易度トグル ✕ キャンセル復帰インフラ
    /// </summary>
    private void HandleConfigMenuNavigation()
    {
        bool isLeftPressed = false;
        bool isRightPressed = false;
        bool isCancelPressed = false;

        if (MenuInputManager.Instance != null)
        {
            Vector2 nav = MenuInputManager.Instance.navigateP1.action.ReadValue<Vector2>();
            isLeftPressed = MenuInputManager.Instance.navigateP1.action.WasPressedThisFrame() && nav.x < -0.5f;
            isRightPressed = MenuInputManager.Instance.navigateP1.action.WasPressedThisFrame() && nav.x > 0.5f;
            isCancelPressed = MenuInputManager.Instance.cancelP1.action.triggered;
        }
        else
        {
            isLeftPressed = Input.GetKeyDown(KeyCode.LeftArrow);
            isRightPressed = Input.GetKeyDown(KeyCode.RightArrow);
            isCancelPressed = Input.GetKeyDown(KeyCode.X);
        }

        // 🔄 1. 左右キーによる難易度トグル循環
        if (isLeftPressed)
        {
            int diffIndex = (int)GameDifficultyManager.CurrentDifficulty;
            diffIndex = (diffIndex - 1 + 4) % 4; // 左ループ
            GameDifficultyManager.CurrentDifficulty = (GameDifficulty)diffIndex;

            if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.MENUSELECT, 0.5f);
            UpdateConfigTextVisuals();
        }
        else if (isRightPressed)
        {
            int diffIndex = (int)GameDifficultyManager.CurrentDifficulty;
            diffIndex = (diffIndex + 1) % 4; // 右ループ
            GameDifficultyManager.CurrentDifficulty = (GameDifficulty)diffIndex;

            if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.MENUSELECT, 0.5f);
            UpdateConfigTextVisuals();
        }

        // ❌ 2. キャンセルキー（戻るボタン）によるタイトル画面への逆トランスファー
        if (isCancelPressed && _inputCooldownTimer <= 0f)
        {
            if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.MENUCANCEL, 0.5f);

            // キャラ選択画面（CharacterSelectManager.cs）と全く同じクリーンな復帰シーケンス
            if (titleMenuCanvas != null)
            {
                TitleMenuManager titleMenu = titleMenuCanvas.GetComponent<TitleMenuManager>();
                if (titleMenu != null)
                {
                    titleMenu.enabled = true; // 1P側のタイトルメニューの知性をONに戻す
                }
            }
            this.gameObject.SetActive(false); // コンフィグパネル自身をパージして閉じる
        }
    }

    /// <summary>
    /// 現在の難易度をテキストに直撃描写
    /// </summary>
    public void UpdateConfigTextVisuals()
    {
        if (configDifficultyText == null) return;

        switch (GameDifficultyManager.CurrentDifficulty)
        {
            case GameDifficulty.Easy: configDifficultyText.text = "< EASY >"; break;
            case GameDifficulty.Normal: configDifficultyText.text = "< NORMAL >"; break;
            case GameDifficulty.Hard: configDifficultyText.text = "< HARD >"; break;
            case GameDifficulty.Lunatic: configDifficultyText.text = "< LUNATIC >"; break;
        }
    }
}