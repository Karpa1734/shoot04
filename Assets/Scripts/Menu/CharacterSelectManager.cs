// --- CharacterSelectManager.cs 指定キャラ数表示拡張・エラー完全解消版 ---
using KanKikuchi.AudioManager;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class CharacterSelectManager : MonoBehaviour
{
    [Header("🎯 UIアタッチ枠")]
    [Tooltip("画面上に存在するキャラクター名テキストUIを順番に登録（最大数分）")]
    public TextMeshProUGUI[] charNameTexts;
    public TextMeshProUGUI randomText;
    public TextMeshProUGUI guideText;

    [Header("✨ 選択状況・GameStart表示用UI")]
    [Tooltip("画面左側に配置する、1Pが選択中/選択したキャラクター名テキスト")]
    public TextMeshProUGUI p1SelectedNameText;
    [Tooltip("画面右側に配置する、2Pが選択中/選択したキャラクター名テキスト")]
    public TextMeshProUGUI p2SelectedNameText;
    [Tooltip("2P選択完了時に表示する「GameStart」テキストUI")]
    public TextMeshProUGUI gameStartText;
    [Tooltip("コントローラー未接続時や準備未完了時に警告を表示するテキストUI")]
    public TextMeshProUGUI warningText;

    [Header("🖼️ プレイヤー立ち絵表示枠")]
    [Tooltip("画面左側に配置する、1Pの立ち絵表示用Imageコンポーネント")]
    public Image p1SelectedCharacterImage;
    [Tooltip("画面右側に配置する、2Pの立ち絵表示用Imageコンポーネント")]
    public Image p2SelectedCharacterImage;

    [Tooltip("ランダムカーソルホバー時や、未選択時に表示させるデフォルト/シルエット用Sprite（任意）")]
    public Sprite randomOrDefaultSprite;

    [Header("📦 キャラクターデータマスター")]
    [Tooltip("ゲームに登場させる全キャラクターの PlayerSkillData をインスペクターでここに登録してください")]
    public List<PlayerSkillData> availableCharacters = new List<PlayerSkillData>();

    [Header("⚙️ 表示設定")]
    [Tooltip("選択肢として画面に表示させるキャラクターの数（例: 3を指定すると上から3キャラ＋ランダムのみ表示されます）")]
    public int displayCharacterCount = 7;

    [Header("🎨 カラー表現")]
    public Color selectedColor = Color.yellow;
    public Color unselectedColor = Color.white;

    private int _currentCursor = 0;
    private int _selectableCharacterCount = 0;
    private bool _isP2SelectingPhase = false;
    private bool _isGameStartReadyPhase = false;

    private int _finalP1CharacterId = -1;
    private int _finalP2CharacterId = -1;
    private float _inputCooldownTimer = 0f;
    private float _keyHoldTimer = 0f;
    private bool _isFirstScrollDone = false;

    private const float FIRST_SCROLL_DELAY = 0.4f;
    private const float REPEAT_SCROLL_SPEED = 0.08f;
    private int _lastConnectedControllersCount = -1;
    public GameObject titleMenuCanvas;

    // =========================================================================
    // 🔧【デモ専用新設】：ワーク用デバッグステート
    // =========================================================================
    private bool _p1DebugAutoAiToggle = false; // 1P自機のCPU自動操縦
    private bool _endlessModeToggle = false;     // エンドレスモードフラグ

    void OnEnable()
    {
        PlayerStatusManager.FromCharacterSelect = true;
        _p1DebugAutoAiToggle = false;
        _endlessModeToggle = false;

        bool isCleared = false;
        try
        {
            isCleared = SaveManager.Load<bool>("GameCleared");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[SaveManager] GameClearedの読み込みに失敗、または未定義です。デフォルト(false)を適用します: {e.Message}");
        }

        int actualDataCount = availableCharacters.Count;
        int targetLimit = Mathf.Min(displayCharacterCount, actualDataCount);

        if (!isCleared && targetLimit > 7)
        {
            _selectableCharacterCount = 7;
        }
        else
        {
            _selectableCharacterCount = targetLimit;
        }

        _isP2SelectingPhase = false;
        _isGameStartReadyPhase = false;
        _currentCursor = 0;

        _finalP1CharacterId = -1;
        _finalP2CharacterId = -1;

        if (gameStartText != null) gameStartText.gameObject.SetActive(false);
        if (warningText != null) warningText.gameObject.SetActive(false);

        _inputCooldownTimer = 0.2f;
        _lastConnectedControllersCount = GetConnectedJoystickCount();
        InitializeCharacterSelectUI();
        UpdateSelectionVisuals();
    }

    private void InitializeCharacterSelectUI()
    {
        for (int i = 0; i < charNameTexts.Length; i++)
        {
            if (charNameTexts[i] == null) continue;

            if (i < _selectableCharacterCount && i < availableCharacters.Count)
            {
                charNameTexts[i].gameObject.SetActive(true);
                if (availableCharacters[i] != null)
                {
                    charNameTexts[i].text = availableCharacters[i].characterName;
                }
            }
            else
            {
                charNameTexts[i].gameObject.SetActive(false);
            }
        }
    }

    void Update()
    {
        int prevCursor = _currentCursor;

        bool isStoryMode = (GameSelectionData.CurrentMode == GameSelectionData.GameMode.Story);
        int maxIndexLimit = isStoryMode ? (_selectableCharacterCount - 1) : _selectableCharacterCount;

        if (_inputCooldownTimer > 0f)
        {
            _inputCooldownTimer -= Time.deltaTime;
        }

        if (GameSelectionData.CurrentMode == GameSelectionData.GameMode.VsCom)
        {
            if (Input.GetKeyDown(KeyCode.C))
            {
                _p1DebugAutoAiToggle = !_p1DebugAutoAiToggle;
                if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.MENUSELECT, 0.6f);
                Debug.Log($"<color=lime>🛠️【Debugトグル】1P自機のAI自動操縦状態を変更 ➔ ACTIVE: {_p1DebugAutoAiToggle}</color>");
                UpdateSelectionVisuals();
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                _endlessModeToggle = !_endlessModeToggle;
                if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.CARDCALL, 0.6f);
                Debug.Log($"<color=orange>⚔️【Debugトグル】エンドレスモード（勝ち星カウント停止）状態を変更 ➔ ACTIVE: {_endlessModeToggle}</color>");
                UpdateSelectionVisuals();
            }
        }

        int connectedControllers = GetConnectedJoystickCount();
        if (connectedControllers != _lastConnectedControllersCount)
        {
            _lastConnectedControllersCount = connectedControllers;
            UpdateSelectionVisuals();
        }

        if (GameSelectionData.CurrentMode == GameSelectionData.GameMode.VsPlayer && connectedControllers < 2)
        {
            if (warningText != null)
            {
                warningText.text = "PleaseConnect2Controller";
                warningText.gameObject.SetActive(true);
            }
            UpdateSelectionVisuals();
            bool isLockCancel = false;
            if (MenuInputManager.Instance != null)
            {
                isLockCancel = MenuInputManager.Instance.cancelP1.action.triggered;
            }
            else
            {
                isLockCancel = Input.GetKeyDown(KeyCode.X);
            }

            if (isLockCancel)
            {
                if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.MENUCANCEL);
                HandleCancel();
            }
            return;
        }
        else
        {
            if (warningText != null && warningText.text == "PleaseConnect2Controller")
            {
                warningText.gameObject.SetActive(false);
            }
        }

        bool isUpPressed = false;
        bool isDownPressed = false;
        bool isDecidePressed = false;
        bool isCancelPressed = false;

        if (MenuInputManager.Instance != null)
        {
            if (GameSelectionData.CurrentMode == GameSelectionData.GameMode.VsPlayer)
            {
                if (!_isP2SelectingPhase && !_isGameStartReadyPhase)
                {
                    Vector2 nav = MenuInputManager.Instance.navigateP1.action.ReadValue<Vector2>();
                    isUpPressed = nav.y > 0.5f;
                    isDownPressed = nav.y < -0.5f;
                    isDecidePressed = MenuInputManager.Instance.submitP1.action.triggered;
                    isCancelPressed = MenuInputManager.Instance.cancelP1.action.triggered;
                }
                else if (_isP2SelectingPhase && !_isGameStartReadyPhase)
                {
                    Vector2 nav = MenuInputManager.Instance.navigateP2.action.ReadValue<Vector2>();
                    isUpPressed = nav.y > 0.5f;
                    isDownPressed = nav.y < -0.5f;
                    isDecidePressed = MenuInputManager.Instance.submitP2.action.triggered;
                    isCancelPressed = MenuInputManager.Instance.cancelP2.action.triggered;
                }
                else if (_isGameStartReadyPhase)
                {
                    isDecidePressed = MenuInputManager.Instance.submitP1.action.triggered || MenuInputManager.Instance.submitP2.action.triggered;
                    isCancelPressed = MenuInputManager.Instance.cancelP1.action.triggered || MenuInputManager.Instance.cancelP2.action.triggered;
                }
            }
            else
            {
                Vector2 nav = MenuInputManager.Instance.navigateP1.action.ReadValue<Vector2>();
                isUpPressed = nav.y > 0.5f;
                isDownPressed = nav.y < -0.5f;
                isDecidePressed = MenuInputManager.Instance.submitP1.action.triggered;
                isCancelPressed = MenuInputManager.Instance.cancelP1.action.triggered;
            }
        }
        else
        {
            isUpPressed = Input.GetKey(KeyCode.UpArrow);
            isDownPressed = Input.GetKey(KeyCode.DownArrow);
            isDecidePressed = Input.GetKeyDown(KeyCode.Z);
            isCancelPressed = Input.GetKeyDown(KeyCode.X);
        }

        if (_isGameStartReadyPhase)
        {
            if (isDecidePressed && _inputCooldownTimer <= 0f)
            {
                if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.MENUDECIDE);
                LoadGameplayScene();
            }
            if (isCancelPressed)
            {
                if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.MENUCANCEL);
                RollbackToP2Selection();
            }
            return;
        }

        if (isUpPressed || isDownPressed)
        {
            if (_keyHoldTimer == 0f && !_isFirstScrollDone)
            {
                if (isUpPressed) _currentCursor = (_currentCursor - 1 + (maxIndexLimit + 1)) % (maxIndexLimit + 1);
                if (isDownPressed) _currentCursor = (_currentCursor + 1) % (maxIndexLimit + 1);

                _isFirstScrollDone = true;
                _keyHoldTimer = FIRST_SCROLL_DELAY;
            }
            else
            {
                _keyHoldTimer -= Time.deltaTime;
                if (_keyHoldTimer <= 0f)
                {
                    if (isUpPressed) _currentCursor = (_currentCursor - 1 + (maxIndexLimit + 1)) % (maxIndexLimit + 1);
                    if (isDownPressed) _currentCursor = (_currentCursor + 1) % (maxIndexLimit + 1);

                    _keyHoldTimer = REPEAT_SCROLL_SPEED;
                }
            }
        }
        else
        {
            _keyHoldTimer = 0f;
            _isFirstScrollDone = false;
        }

        if (prevCursor != _currentCursor)
        {
            if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.MENUSELECT);
            UpdateSelectionVisuals();
        }

        if (isDecidePressed && _inputCooldownTimer <= 0f)
        {
            if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.MENUDECIDE);
            ConfirmSelection();
        }

        if (isCancelPressed)
        {
            if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.MENUCANCEL);
            HandleCancel();
        }
    }

    private int GetConnectedJoystickCount()
    {
        string[] joysticks = Input.GetJoystickNames();
        int count = 0;
        foreach (string joy in joysticks)
        {
            if (!string.IsNullOrEmpty(joy) && !string.IsNullOrEmpty(joy.Trim())) count++;
        }
        return count;
    }

    private void UpdateSelectionVisuals()
    {
        bool isStory = (GameSelectionData.CurrentMode == GameSelectionData.GameMode.Story);

        for (int i = 0; i < charNameTexts.Length; i++)
        {
            if (i >= charNameTexts.Length || charNameTexts[i] == null) continue;

            if (i < _selectableCharacterCount)
            {
                charNameTexts[i].gameObject.SetActive(true);
                charNameTexts[i].color = (i == _currentCursor && !_isGameStartReadyPhase) ? selectedColor : unselectedColor;
            }
            else
            {
                charNameTexts[i].gameObject.SetActive(false);
            }
        }

        if (randomText != null)
        {
            if (isStory)
            {
                randomText.gameObject.SetActive(false);
            }
            else
            {
                randomText.gameObject.SetActive(true);
                randomText.color = (_currentCursor == _selectableCharacterCount && !_isGameStartReadyPhase) ? selectedColor : unselectedColor;
            }
        }

        string hoveringName = "Random";
        Sprite hoveringSprite = randomOrDefaultSprite;

        if (_currentCursor < _selectableCharacterCount && _currentCursor < availableCharacters.Count && availableCharacters[_currentCursor] != null)
        {
            hoveringName = availableCharacters[_currentCursor].characterName;
            hoveringSprite = availableCharacters[_currentCursor].characterSprite;
        }

        if (p1SelectedNameText != null)
        {
            string p1Label = _p1DebugAutoAiToggle ? "1P(COM): " : "1P: ";

            if (_isGameStartReadyPhase)
            {
                int p1RealId = GameSelectionData.SelectedCharacterP1;
                if (p1RealId >= 0 && p1RealId < availableCharacters.Count && availableCharacters[p1RealId] != null)
                {
                    p1SelectedNameText.text = p1Label + availableCharacters[p1RealId].characterName;
                    SetCharacterImage(p1SelectedCharacterImage, availableCharacters[p1RealId].characterSprite);
                }
            }
            else if (_isP2SelectingPhase && _finalP1CharacterId >= 0)
            {
                if (_finalP1CharacterId == _selectableCharacterCount)
                {
                    p1SelectedNameText.text = p1Label + "Random";
                    SetCharacterImage(p1SelectedCharacterImage, randomOrDefaultSprite);
                }
                else
                {
                    p1SelectedNameText.text = p1Label + availableCharacters[_finalP1CharacterId].characterName;
                    SetCharacterImage(p1SelectedCharacterImage, availableCharacters[_finalP1CharacterId].characterSprite);
                }
            }
            else
            {
                p1SelectedNameText.text = p1Label + hoveringName;
                SetCharacterImage(p1SelectedCharacterImage, hoveringSprite);
            }
        }

        if (p2SelectedNameText != null)
        {
            if (isStory)
            {
                p2SelectedNameText.gameObject.SetActive(false);
            }
            else
            {
                p2SelectedNameText.gameObject.SetActive(true);
                string sideLabel = (GameSelectionData.CurrentMode == GameSelectionData.GameMode.VsCom) ? "COM" : "2P";

                if (_isGameStartReadyPhase)
                {
                    int p2RealId = GameSelectionData.SelectedCharacterP2;
                    if (p2RealId >= 0 && p2RealId < availableCharacters.Count && availableCharacters[p2RealId] != null)
                    {
                        p2SelectedNameText.text = $"{sideLabel}: " + availableCharacters[p2RealId].characterName;
                        SetCharacterImage(p2SelectedCharacterImage, availableCharacters[p2RealId].characterSprite);
                    }
                }
                else if (_isP2SelectingPhase)
                {
                    p2SelectedNameText.text = $"{sideLabel}: " + hoveringName;
                    SetCharacterImage(p2SelectedCharacterImage, hoveringSprite);
                }
                else
                {
                    p2SelectedNameText.text = $"{sideLabel}: Selecting...";
                    SetCharacterImage(p2SelectedCharacterImage, randomOrDefaultSprite);
                }
            }
        }

        if (p2SelectedCharacterImage != null)
        {
            if (isStory)
            {
                p2SelectedCharacterImage.gameObject.SetActive(false);
            }
        }

        if (guideText != null)
        {
            if (GameSelectionData.CurrentMode == GameSelectionData.GameMode.VsPlayer && GetConnectedJoystickCount() < 2)
            {
                guideText.text = "PLEASE CONNECT 2 CONTROLLERS TO VS PLAYER";
                guideText.color = Color.red;
            }
            else if (_isGameStartReadyPhase)
            {
                if (_endlessModeToggle)
                {
                    guideText.text = "PRESS START BUTTON [DEBUG: ENDLESS MODE ON]";
                    guideText.color = Color.cyan;
                }
                else
                {
                    guideText.text = "PRESS START BUTTON TO BATTLE";
                }
            }
            else
            {
                string debugSuffix = "";
                if (_p1DebugAutoAiToggle) debugSuffix += "[1P:AI] ";
                if (_endlessModeToggle) debugSuffix += "[ENDLESS] ";

                if (!string.IsNullOrEmpty(debugSuffix))
                {
                    guideText.text = $"1P SELECT <color=yellow>{debugSuffix}</color>";
                }
                else
                {
                    guideText.text = "1P SELECT";
                }
                guideText.color = unselectedColor;
            }
        }
    }

    private void SetCharacterImage(Image targetImage, Sprite sprite)
    {
        if (targetImage == null) return;

        if (sprite != null)
        {
            targetImage.gameObject.SetActive(true);
            targetImage.sprite = sprite;

            // 🌟【追加】：画像の縦横比を維持したまま、Imageのサイズ（RectTransform）を自動調整する
            ApplyAspectFit(targetImage, sprite);
        }
        else
        {
            if (randomOrDefaultSprite != null)
            {
                targetImage.gameObject.SetActive(true);
                targetImage.sprite = randomOrDefaultSprite;
                ApplyAspectFit(targetImage, randomOrDefaultSprite);
            }
            else
            {
                targetImage.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 🖼️ Spriteの元サイズ（縦横比）を基準に、ImageのRectTransformの大きさを綺麗にフィットさせるヘルパーメソッド
    /// </summary>
    private void ApplyAspectFit(Image targetImage, Sprite sprite)
    {
        if (targetImage == null || sprite == null) return;

        // Image自体の設定として、元画像の比率を維持するプロパティをONにする
        targetImage.preserveAspect = true;

        // もし「Imageコンポーネントがアタッチされている枠の大きさ自体を、画像の比率に合わせて自動変形させたい」場合：
        RectTransform rectTransform = targetImage.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            float spriteWidth = sprite.rect.width;
            float spriteHeight = sprite.rect.height;

            if (spriteWidth > 0f && spriteHeight > 0f)
            {
                float aspectRatio = spriteWidth / spriteHeight;

                // 現在の高さ（あるいは幅）を基準にして、比率に合わせたサイズへ補正
                // ※ここでは高さを基準に幅を自動調整する例です
                float currentHeight = rectTransform.sizeDelta.y;
                if (currentHeight <= 0f) currentHeight = rectTransform.rect.height;
                if (currentHeight <= 0f) currentHeight = 300f; // フォールバック値

                rectTransform.sizeDelta = new Vector2(currentHeight * aspectRatio, currentHeight);
            }
        }
    }
    private void ConfirmSelection()
    {
        if (!_isP2SelectingPhase)
        {
            _finalP1CharacterId = _currentCursor;

            if (GameSelectionData.CurrentMode == GameSelectionData.GameMode.Story)
            {
                _finalP2CharacterId = 7;
                _inputCooldownTimer = 0.2f;
                EnterGameStartReady();
            }
            else if (GameSelectionData.CurrentMode == GameSelectionData.GameMode.VsCom ||
                     GameSelectionData.CurrentMode == GameSelectionData.GameMode.VsPlayer)
            {
                _isP2SelectingPhase = true;
                _currentCursor = 0;
                _inputCooldownTimer = 0.2f;

                UpdateSelectionVisuals();
            }
        }
        else
        {
            _finalP2CharacterId = _currentCursor;
            _inputCooldownTimer = 0.2f;
            EnterGameStartReady();
        }
    }

    private void EnterGameStartReady()
    {
        _isGameStartReadyPhase = true;

        if (_finalP1CharacterId == _selectableCharacterCount)
        {
            GameSelectionData.SelectedCharacterP1 = Random.Range(0, _selectableCharacterCount);
        }
        else
        {
            GameSelectionData.SelectedCharacterP1 = _finalP1CharacterId;
        }

        if (GameSelectionData.CurrentMode == GameSelectionData.GameMode.Story)
        {
            GameSelectionData.SelectedCharacterP2 = 7;
        }
        else
        {
            if (_finalP2CharacterId == _selectableCharacterCount)
            {
                GameSelectionData.SelectedCharacterP2 = Random.Range(0, _selectableCharacterCount);
                if (GameSelectionData.CurrentMode == GameSelectionData.GameMode.VsCom && _selectableCharacterCount > 1)
                {
                    while (GameSelectionData.SelectedCharacterP2 == GameSelectionData.SelectedCharacterP1)
                    {
                        GameSelectionData.SelectedCharacterP2 = Random.Range(0, _selectableCharacterCount);
                    }
                }
            }
            else
            {
                GameSelectionData.SelectedCharacterP2 = _finalP2CharacterId;
            }
        }

        // 🌟【修正】：誤字（gameStartTest）を正しい変数名（gameStartText）に修正
        if (gameStartText != null)
        {
            gameStartText.gameObject.SetActive(true);
            gameStartText.color = selectedColor;
        }

        GameDifficultyManager.IsP1AutoAiDebugMode = _p1DebugAutoAiToggle;
        GameDifficultyManager.IsEndlessMode = _endlessModeToggle;

        UpdateSelectionVisuals();
    }

    private void RollbackToP2Selection()
    {
        _isGameStartReadyPhase = false;
        _finalP2CharacterId = -1;

        if (gameStartText != null) gameStartText.gameObject.SetActive(false);

        if (GameSelectionData.CurrentMode == GameSelectionData.GameMode.Story)
        {
            _isP2SelectingPhase = false;
            _finalP1CharacterId = -1;
            _currentCursor = 0;
        }
        else
        {
            _isP2SelectingPhase = true;
            _currentCursor = 0;
        }

        UpdateSelectionVisuals();
    }

    private void HandleCancel()
    {
        if (_isP2SelectingPhase)
        {
            _selectableCharacterCount = Mathf.Min(displayCharacterCount, availableCharacters.Count);
            _isP2SelectingPhase = false;
            _finalP1CharacterId = -1;
            _currentCursor = 0;
            UpdateSelectionVisuals();
        }
        else
        {
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

    private void LoadGameplayScene()
    {
        SceneManager.LoadScene("Shoot");
    }
}