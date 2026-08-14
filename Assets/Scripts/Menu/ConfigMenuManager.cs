// --- ConfigMenuManager.cs 画面サイズ変更対応版 ---
using KanKikuchi.AudioManager;
using TMPro;
using UnityEngine;

public class ConfigMenuManager : MonoBehaviour
{
    [Header("UI Slots")]
    [Tooltip("現在の難易度（< EASY > 等）を表示するTMPテキスト")]
    public TextMeshProUGUI configDifficultyText; 

    [Tooltip("現在の画面解像度（< 1920x1080 > 等）を表示するTMPテキスト")]
    public TextMeshProUGUI configResolutionText; // 🌟【新規追加】画面サイズUI枠

    [Header("Color Settings")]
    public Color selectedColor = Color.yellow;
    public Color unselectedColor = Color.white;

    [Header("Parent Transition Reference")]
    [Tooltip("タイトルメニューの親オブジェクト（復帰のキック用）")]
    public GameObject titleMenuCanvas; 

    // 画面サイズのプリセット構造体
    private struct ResolutionOption
    {
        public string label;
        public int width;
        public int height;
        public FullScreenMode screenMode;

        public ResolutionOption(string label, int width, int height, FullScreenMode screenMode)
        {
            this.label = label;
            this.width = width;
            this.height = height;
            this.screenMode = screenMode;
        }
    }

    // ご要望の5種類の解像度リスト
    private readonly ResolutionOption[] _resolutions = new ResolutionOption[]
    {
        new ResolutionOption("< FULL SCREEN >", 1920, 1080, FullScreenMode.FullScreenWindow),
        new ResolutionOption("< 1920 x 1080 >", 1920, 1080, FullScreenMode.Windowed),
        new ResolutionOption("< 1280 x 720 >",  1280, 720,  FullScreenMode.Windowed),
        new ResolutionOption("< 960 x 540 >",   960,  540,  FullScreenMode.Windowed),
        new ResolutionOption("< 640 x 360 >",   640,  360,  FullScreenMode.Windowed)
    };

    private int _selectedMenuIndex = 0; // 0: 難易度, 1: 画面解像度
    private int _currentResolutionIndex = 1; // デフォルトは1920x1080(ウィンドウ)
    private float _inputCooldownTimer = 0f; 

    void OnEnable()
    {
        _inputCooldownTimer = 0.2f; // 開いた瞬間の暴発ガード
        _selectedMenuIndex = 0;

        // 現在の画面状態から最も近い解像度インデックスを初期選択
        DetectCurrentResolutionIndex();
        UpdateConfigTextVisuals();
    }

    private void DetectCurrentResolutionIndex()
    {
        if (Screen.fullScreenMode == FullScreenMode.FullScreenWindow || Screen.fullScreen)
        {
            _currentResolutionIndex = 0; // フルスクリーン
            return;
        }

        int w = Screen.width;
        int h = Screen.height;

        for (int i = 1; i < _resolutions.Length; i++)
        {
            if (_resolutions[i].width == w && _resolutions[i].height == h)
            {
                _currentResolutionIndex = i;
                return;
            }
        }
        _currentResolutionIndex = 1; // 該当がなければ1920x1080へ
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
    /// 🔮 コンフィグパネル：上下で項目移動 ✕ 左右で値変更 ✕ キャンセル復帰
    /// </summary>
    private void HandleConfigMenuNavigation()
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

        // ↕️ 1. 上下キーで「難易度」と「画面解像度」を選択切替
        if (isUpPressed || isDownPressed)
        {
            _selectedMenuIndex = (_selectedMenuIndex == 0) ? 1 : 0;
            if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.MENUSELECT, 0.5f);
            UpdateConfigTextVisuals();
            return;
        }

        // 🔄 2. 左右キーで数値を切り替え
        if (isLeftPressed) 
        {
            if (_selectedMenuIndex == 0)
            {
                // 難易度変更（左ループ）
                int diffIndex = (int)GameDifficultyManager.CurrentDifficulty; 
                diffIndex = (diffIndex - 1 + 4) % 4; 
                GameDifficultyManager.CurrentDifficulty = (GameDifficulty)diffIndex; 
            }
            else
            {
                // 解像度変更（左ループ）
                _currentResolutionIndex = (_currentResolutionIndex - 1 + _resolutions.Length) % _resolutions.Length;
                ApplyScreenResolution();
            }

            if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.MENUSELECT, 0.5f); 
            UpdateConfigTextVisuals(); 
        }
        else if (isRightPressed) 
        {
            if (_selectedMenuIndex == 0)
            {
                // 難易度変更（右ループ）
                int diffIndex = (int)GameDifficultyManager.CurrentDifficulty; 
                diffIndex = (diffIndex + 1) % 4; 
                GameDifficultyManager.CurrentDifficulty = (GameDifficulty)diffIndex; 
            }
            else
            {
                // 解像度変更（右ループ）
                _currentResolutionIndex = (_currentResolutionIndex + 1) % _resolutions.Length;
                ApplyScreenResolution();
            }

            if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.MENUSELECT, 0.5f); 
            UpdateConfigTextVisuals(); 
        }

        // ❌ 3. キャンセルキー（戻るボタン）で閉じる
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
    /// 選択された画面解像度をUnityシステムへ適用
    /// </summary>
    private void ApplyScreenResolution()
    {
        ResolutionOption opt = _resolutions[_currentResolutionIndex];
        Screen.SetResolution(opt.width, opt.height, opt.screenMode);
        Debug.Log($"🖥️ 解像度を変更しました: {opt.label} ({opt.width}x{opt.height}, Mode:{opt.screenMode})");
    }

    /// <summary>
    /// 各項目のテキスト表示 ＆ 選択中テキストの色強調
    /// </summary>
    public void UpdateConfigTextVisuals()
    {
        // 1. 難易度テキスト描画
        if (configDifficultyText != null) 
        {
            switch (GameDifficultyManager.CurrentDifficulty)
            {
                case GameDifficulty.Easy: configDifficultyText.text = "< EASY >"; break; 
                case GameDifficulty.Normal: configDifficultyText.text = "< NORMAL >"; break; 
                case GameDifficulty.Hard: configDifficultyText.text = "< HARD >"; break; 
                case GameDifficulty.Lunatic: configDifficultyText.text = "< LUNATIC >"; break; 
            }
            configDifficultyText.color = (_selectedMenuIndex == 0) ? selectedColor : unselectedColor;
        }

        // 2. 画面解像度テキスト描画
        if (configResolutionText != null)
        {
            configResolutionText.text = _resolutions[_currentResolutionIndex].label;
            configResolutionText.color = (_selectedMenuIndex == 1) ? selectedColor : unselectedColor;
        }
    }
}