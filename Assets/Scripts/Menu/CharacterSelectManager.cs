// --- CharacterSelectManager.cs キャラの選択・左右名表示・左右立ち絵リアルタイム反映・GameStartロック・ランダム決定後ネーム＆グラフィック同期版 ---
using KanKikuchi.AudioManager;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI; // 🌟 Imageコンポーネント制御のために追加
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

    [Header("🖼️ プレイヤー立ち絵表示枠（新設）")]
    [Tooltip("画面左側に配置する、1Pの立ち絵表示用Imageコンポーネント")]
    public Image p1SelectedCharacterImage;
    [Tooltip("画面右側に配置する、2Pの立ち絵表示用Imageコンポーネント")]
    public Image p2SelectedCharacterImage;

    [Tooltip("ランダムカーソルホバー時や、未選択時に表示させるデフォルト/シルエット用Sprite（任意）")]
    public Sprite randomOrDefaultSprite;

    [Header("📦 キャラクターデータマスター")]
    [Tooltip("ゲームに登場させる全キャラクターの PlayerSkillData をインスペクターでここに登録してください")]
    public List<PlayerSkillData> availableCharacters = new List<PlayerSkillData>();

    [Header("🎨 カラー表現")]
    public Color selectedColor = Color.yellow;
    public Color unselectedColor = Color.white;

    private int _currentCursor = 0;
    private int _selectableCharacterCount = 0;
    private bool _isP2SelectingPhase = false;
    private bool _isGameStartReadyPhase = false; // GameStart待ち状態フラグ

    private int _finalP1CharacterId = -1;
    private int _finalP2CharacterId = -1;
    private float _inputCooldownTimer = 0f;
    private float _keyHoldTimer = 0f;
    private bool _isFirstScrollDone = false;

    private const float FIRST_SCROLL_DELAY = 0.4f;
    private const float REPEAT_SCROLL_SPEED = 0.08f;

    public GameObject titleMenuCanvas;

    void OnEnable()
    {
        PlayerStatusManager.FromCharacterSelect = true;
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
        if (!isCleared && actualDataCount > 7)
        {
            _selectableCharacterCount = 7;
        }
        else
        {
            _selectableCharacterCount = actualDataCount;
        }

        _isP2SelectingPhase = false;
        _isGameStartReadyPhase = false;
        _currentCursor = 0;

        _finalP1CharacterId = -1;
        _finalP2CharacterId = -1;

        if (gameStartText != null) gameStartText.gameObject.SetActive(false);
        if (warningText != null) warningText.gameObject.SetActive(false);

        _inputCooldownTimer = 0.2f;
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
        int maxIndex = _selectableCharacterCount;

        if (_inputCooldownTimer > 0f)
        {
            _inputCooldownTimer -= Time.deltaTime;
        }

        int connectedControllers = GetConnectedJoystickCount();

        if (GameSelectionData.CurrentMode == GameSelectionData.GameMode.VsPlayer && connectedControllers < 2)
        {
            if (warningText != null)
            {
                warningText.text = "⚠️ コントローラーが2つ接続されていません！\n(1P・2P双方のコントローラーが必要です)";
                warningText.gameObject.SetActive(true);
            }

            bool isLockCancel = false;
            //if (MenuInputManager.Instance != null)
            //{
             //   isLockCancel = MenuInputManager.Instance.cancelP1.action.triggered;
            //}
            //else
            //{
                isLockCancel = Input.GetKeyDown(KeyCode.X);
            //}

            if (isLockCancel)
            {
                if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.MENUCANCEL);
                HandleCancel();
            }
            return;
        }
        else
        {
            if (warningText != null && warningText.text.Contains("接続されていません"))
                warningText.gameObject.SetActive(false);
        }

        bool isUpPressed = false;
        bool isDownPressed = false;
        bool isDecidePressed = false;
        bool isCancelPressed = false;

        /*if (MenuInputManager.Instance != null)
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
        {*/
            isUpPressed = Input.GetKey(KeyCode.UpArrow);
            isDownPressed = Input.GetKey(KeyCode.DownArrow);
            isDecidePressed = Input.GetKeyDown(KeyCode.Z);
            isCancelPressed = Input.GetKeyDown(KeyCode.X);
        //}

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
                if (isUpPressed) _currentCursor = (_currentCursor - 1 + (maxIndex + 1)) % (maxIndex + 1);
                if (isDownPressed) _currentCursor = (_currentCursor + 1) % (maxIndex + 1);

                _isFirstScrollDone = true;
                _keyHoldTimer = FIRST_SCROLL_DELAY;
            }
            else
            {
                _keyHoldTimer -= Time.deltaTime;
                if (_keyHoldTimer <= 0f)
                {
                    if (isUpPressed) _currentCursor = (_currentCursor - 1 + (maxIndex + 1)) % (maxIndex + 1);
                    if (isDownPressed) _currentCursor = (_currentCursor + 1) % (maxIndex + 1);

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
        for (int i = 0; i < _selectableCharacterCount; i++)
        {
            if (i >= charNameTexts.Length || charNameTexts[i] == null) continue;
            charNameTexts[i].color = (i == _currentCursor && !_isGameStartReadyPhase) ? selectedColor : unselectedColor;
        }

        if (randomText != null)
        {
            randomText.color = (_currentCursor == _selectableCharacterCount && !_isGameStartReadyPhase) ? selectedColor : unselectedColor;
        }

        string hoveringName = "Random";
        Sprite hoveringSprite = randomOrDefaultSprite;

        if (_currentCursor < _selectableCharacterCount && _currentCursor < availableCharacters.Count && availableCharacters[_currentCursor] != null)
        {
            hoveringName = availableCharacters[_currentCursor].characterName;

            // 💡【アセット連動】：PlayerSkillData または CharacterData 内に定義されている立ち絵（Sprite）を安全に抽出
            // ※ もし変数名が別（例：characterSprite, normalSprite）なら、以下をその変数名に書き換えてください。
            hoveringSprite = availableCharacters[_currentCursor].characterSprite;
        }

        // 💡 1P側のテキスト＆グラフィックリアルタイム更新
        if (p1SelectedNameText != null)
        {
            if (_isGameStartReadyPhase)
            {
                int p1RealId = GameSelectionData.SelectedCharacterP1;
                if (p1RealId >= 0 && p1RealId < availableCharacters.Count && availableCharacters[p1RealId] != null)
                {
                    p1SelectedNameText.text = "1P: " + availableCharacters[p1RealId].characterName;
                    SetCharacterImage(p1SelectedCharacterImage, availableCharacters[p1RealId].characterSprite);
                }
            }
            else if (_isP2SelectingPhase && _finalP1CharacterId >= 0)
            {
                // 2P選択中、1Pは確定立ち絵でホールド
                if (_finalP1CharacterId == _selectableCharacterCount)
                {
                    p1SelectedNameText.text = "1P: Random";
                    SetCharacterImage(p1SelectedCharacterImage, randomOrDefaultSprite);
                }
                else
                {
                    p1SelectedNameText.text = "1P: " + availableCharacters[_finalP1CharacterId].characterName;
                    SetCharacterImage(p1SelectedCharacterImage, availableCharacters[_finalP1CharacterId].characterSprite);
                }
            }
            else
            {
                // 1Pカーソル移動中：現在ホバーしているキャラの立ち絵をリアルタイム表示
                p1SelectedNameText.text = "1P: " + hoveringName;
                SetCharacterImage(p1SelectedCharacterImage, hoveringSprite);
            }
        }

        // 💡 2P側のテキスト＆グラフィックリアルタイム更新
        if (p2SelectedNameText != null)
        {
            if (_isGameStartReadyPhase)
            {
                int p2RealId = GameSelectionData.SelectedCharacterP2;
                if (p2RealId >= 0 && p2RealId < availableCharacters.Count && availableCharacters[p2RealId] != null)
                {
                    p2SelectedNameText.text = "2P: " + availableCharacters[p2RealId].characterName;
                    SetCharacterImage(p2SelectedCharacterImage, availableCharacters[p2RealId].characterSprite);
                }
            }
            else if (_isP2SelectingPhase)
            {
                // 2Pカーソル移動中：現在ホバーしているキャラの立ち絵をリアルタイム表示
                p2SelectedNameText.text = "2P: " + hoveringName;
                SetCharacterImage(p2SelectedCharacterImage, hoveringSprite);
            }
            else
            {
                // 1P選択中：2Pはまだ「Selecting...」およびデフォルト表示
                p2SelectedNameText.text = "2P: Selecting...";
                SetCharacterImage(p2SelectedCharacterImage, randomOrDefaultSprite);
            }
        }

        if (guideText != null)
        {
            if (_isGameStartReadyPhase) guideText.text = "PRESS START BUTTON TO BATTLE";
            else guideText.text = _isP2SelectingPhase ? "2P SELECT (PLAYER 2 INPUT ONLY)" : "1P SELECT";
        }
    }

    /// <summary>
    /// 🖼️ Imageコンポーネントに対して安全にSpriteを流し込むための補助メソッド
    /// </summary>
    private void SetCharacterImage(Image targetImage, Sprite sprite)
    {
        if (targetImage == null) return;

        if (sprite != null)
        {
            targetImage.gameObject.SetActive(true);
            targetImage.sprite = sprite;
        }
        else
        {
            // Spriteが登録されていない、またはRandomホバー時は非表示にするか、デフォルトにするガード
            if (randomOrDefaultSprite != null)
            {
                targetImage.gameObject.SetActive(true);
                targetImage.sprite = randomOrDefaultSprite;
            }
            else
            {
                targetImage.gameObject.SetActive(false);
            }
        }
    }

    private void ConfirmSelection()
    {
        if (!_isP2SelectingPhase)
        {
            _finalP1CharacterId = _currentCursor;

            if (GameSelectionData.CurrentMode == GameSelectionData.GameMode.VsCom ||
                GameSelectionData.CurrentMode == GameSelectionData.GameMode.VsPlayer)
            {
                _isP2SelectingPhase = true;
                _currentCursor = 0;
                _inputCooldownTimer = 0.2f;

                UpdateSelectionVisuals();
            }
            else if (GameSelectionData.CurrentMode == GameSelectionData.GameMode.Story)
            {
                _finalP2CharacterId = 7;
                _inputCooldownTimer = 0.2f;
                EnterGameStartReady();
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

        if (gameStartText != null)
        {
            gameStartText.gameObject.SetActive(true);
            gameStartText.color = selectedColor;
        }

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