using KanKikuchi.AudioManager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PowerPointSlideMenuManager : MonoBehaviour
{
    [Header("Parent Transition Reference")]
    [Tooltip("タイトルメニューの親オブジェクト（キャンセル時に復帰用）")]
    public GameObject titleMenuCanvas;

    [Header("Navigation UI (Tabs & Sins)")]
    [Tooltip("上部のタブ（1, 2, 3）のUIオブジェクト配列（必ず左から順に3つ割り当て）")]
    public GameObject[] tabIndicators;

    [Tooltip("左側の8つの大罪メニューのテキスト配列（Wrath, Greed, Lust, Pride, Sloth, Envy, Gluttony, Void の順に8つ割り当て）")]
    public TextMeshProUGUI[] sinMenuTexts;

    [Header("Slide Image Display")]
    [Tooltip("パワポのスライド画像を表示するためのUI Imageコンポーネント")]
    public Image slideScreenImage;

    // 🖼️ 1つの大罪につき「タブ1, 2, 3に対応する3枚のスライド画像」をまとめた構造体
    [System.Serializable]
    public struct SinSlideSet
    {
        [Tooltip("大罪名（例: Wrath, Greed 等の確認用ラベル）")]
        public string sinLabel;

        [Tooltip("タブ1枚目の画像（例: スライド2：ステータス・レーダー等）")]
        public Sprite slideTab1;

        [Tooltip("タブ2枚目の画像（例: スライド3：保有スキル一覧等）")]
        public Sprite slideTab2;

        [Tooltip("タブ3枚目の画像（例: スライド4：キャラクター詳細等）")]
        public Sprite slideTab3;
    }

    [Header("PowerPoint Slide Database (8 Sins)")]
    [Tooltip("上から順に 0:Wrath, 1:Greed, 2:Lust, 3:Pride, 4:Sloth, 5:Envy, 6:Gluttony, 7:Void のスライドセットを登録してください（計8つ）")]
    public SinSlideSet[] sinDatabase = new SinSlideSet[8];

    [Header("Color Settings")]
    public Color selectedColor = Color.yellow;
    public Color unselectedColor = Color.white;

    // 🎮 カーソル管理用インデックス
    private int _currentTabIndex = 0; // 0: Tab1, 1: Tab2, 2: Tab3 (横入力で切替：厳格に3枚でループ)
    private int _currentSinIndex = 0; // 0 ~ 7 (縦入力で大罪切替: Wrath ~ Void)
    private float _inputCooldownTimer = 0f;

    void OnEnable()
    {
        _inputCooldownTimer = 0.2f; // 開いた瞬間の入力暴発ガード
        UpdateSlideVisuals();
    }

    void Update()
    {
        if (_inputCooldownTimer > 0f)
        {
            _inputCooldownTimer -= Time.deltaTime;
        }

        HandleMenuNavigation();
    }

    /// <summary>
    /// 🔮 パワポスライドメニュー：左右で3枚のタブ切替 ✕ 上下で8大罪切替 ✕ キャンセルでタイトル復帰
    /// </summary>
    private void HandleMenuNavigation()
    {
        bool isUpPressed = false;
        bool isDownPressed = false;
        bool isLeftPressed = false;
        bool isRightPressed = false;
        bool isCancelPressed = false;

        if (MenuInputManager.Instance != null)
        {
            Vector2 nav = MenuInputManager.Instance.navigateP1.action.ReadValue<Vector2>();
            isUpPressed = MenuInputManager.Instance.navigateP1.action.WasPressedThisFrame() && nav.y > 0.5f;
            isDownPressed = MenuInputManager.Instance.navigateP1.action.WasPressedThisFrame() && nav.y < -0.5f;
            isLeftPressed = MenuInputManager.Instance.navigateP1.action.WasPressedThisFrame() && nav.x < -0.5f;
            isRightPressed = MenuInputManager.Instance.navigateP1.action.WasPressedThisFrame() && nav.x > 0.5f;
            isCancelPressed = MenuInputManager.Instance.cancelP1.action.triggered;
        }
        else
        {
            isUpPressed = Input.GetKeyDown(KeyCode.UpArrow);
            isDownPressed = Input.GetKeyDown(KeyCode.DownArrow);
            isLeftPressed = Input.GetKeyDown(KeyCode.LeftArrow);
            isRightPressed = Input.GetKeyDown(KeyCode.RightArrow);
            isCancelPressed = Input.GetKeyDown(KeyCode.X);
        }

        // 🔄 1. 左右キーでタブ（1, 2, 3 の「厳格な3枚」）をループ切り替え
        int maxTabCount = 3;
        if (isLeftPressed)
        {
            _currentTabIndex = (_currentTabIndex - 1 + maxTabCount) % maxTabCount;
            if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.MENUSELECT, 0.5f);
            UpdateSlideVisuals();
        }
        else if (isRightPressed)
        {
            _currentTabIndex = (_currentTabIndex + 1) % maxTabCount;
            if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.MENUSELECT, 0.5f);
            UpdateSlideVisuals();
        }

        // ↕️ 2. 上下キーで大罪メニュー（Wrath ~ Void の最大8種類）をループ切り替え
        int maxSinCount = (sinMenuTexts != null && sinMenuTexts.Length > 0) ? sinMenuTexts.Length : 8;
        if (isUpPressed)
        {
            _currentSinIndex = (_currentSinIndex - 1 + maxSinCount) % maxSinCount;
            if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.MENUSELECT, 0.5f);
            UpdateSlideVisuals();
        }
        else if (isDownPressed)
        {
            _currentSinIndex = (_currentSinIndex + 1) % maxSinCount;
            if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.MENUSELECT, 0.5f);
            UpdateSlideVisuals();
        }

        // ❌ 3. キャンセルキー（戻るボタン）でタイトル画面へ戻る
        if (isCancelPressed && _inputCooldownTimer <= 0f)
        {
            if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.MENUCANCEL, 0.5f);

            if (titleMenuCanvas != null)
            {
                TitleMenuManager titleMenu = titleMenuCanvas.GetComponent<TitleMenuManager>();
                if (titleMenu != null)
                {
                    titleMenu.enabled = true;
                }
            }
            this.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 🎨 選択位置に応じてタブや大罪テキストの色を変え、対応するパワポ画像を画面に反映する
    /// </summary>
    private void UpdateSlideVisuals()
    {
        // 1. 上部タブ (1, 2, 3) の選択カラー強調
        if (tabIndicators != null)
        {
            for (int i = 0; i < tabIndicators.Length; i++)
            {
                if (tabIndicators[i] != null)
                {
                    Image img = tabIndicators[i].GetComponent<Image>();
                    if (img != null)
                    {
                        img.color = (i == _currentTabIndex) ? selectedColor : unselectedColor;
                    }
                }
            }
        }

        // 2. 左側の大罪メニュー (Wrath, Greed...) の選択カラー強調
        if (sinMenuTexts != null)
        {
            for (int i = 0; i < sinMenuTexts.Length; i++)
            {
                if (sinMenuTexts[i] != null)
                {
                    sinMenuTexts[i].color = (i == _currentSinIndex) ? selectedColor : unselectedColor;
                }
            }
        }

        // 3. データベースから現在の「大罪」と「タブ（3枚のうちの1枚）」に一致するスライド画像を取得して表示
        if (sinDatabase != null && sinDatabase.Length > _currentSinIndex && _currentSinIndex >= 0)
        {
            SinSlideSet currentSet = sinDatabase[_currentSinIndex];
            Sprite targetSprite = null;

            switch (_currentTabIndex)
            {
                case 0: targetSprite = currentSet.slideTab1; break; // タブ1画像 (スライド2形式)
                case 1: targetSprite = currentSet.slideTab2; break; // タブ2画像 (スライド3形式)
                case 2: targetSprite = currentSet.slideTab3; break; // タブ3画像 (スライド4形式)
            }

            if (slideScreenImage != null)
            {
                if (targetSprite != null)
                {
                    slideScreenImage.gameObject.SetActive(true);
                    slideScreenImage.sprite = targetSprite;
                }
                else
                {
                    // 画像が未設定のスライド枠
                    slideScreenImage.gameObject.SetActive(false);
                }
            }
        }
    }
}