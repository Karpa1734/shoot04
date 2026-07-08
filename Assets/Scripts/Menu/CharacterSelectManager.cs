// --- CharacterSelectManager.cs キャラの選択・左右名表示・左右立ち絵リアルタイム反映・GameStartロック・ランダム決定後ネーム＆グラフィック同期版 ---
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
    private int _lastConnectedControllersCount = -1; // 💡【追加】前フレームの接続数を保持する変数
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
        // 💡【追加】初回起動時の接続数を記録しておく
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
        int maxIndex = _selectableCharacterCount;

        if (_inputCooldownTimer > 0f)
        {
            _inputCooldownTimer -= Time.deltaTime;
        }

        int connectedControllers = GetConnectedJoystickCount();
        // 💡【追加】コントローラーの抜き差し（接続数の変化）を検知した瞬間にUIを即座に更新
        if (connectedControllers != _lastConnectedControllersCount)
        {
            _lastConnectedControllersCount = connectedControllers;
            UpdateSelectionVisuals();
        }
        // 👑【VsPlayer専用の鉄壁接続チェックガード】
        if (GameSelectionData.CurrentMode == GameSelectionData.GameMode.VsPlayer && connectedControllers < 2)
        {
            if (warningText != null)
            {
                // 🚨 2つのコントローラーが確認できない場合はメッセージを表示
                warningText.text = "PleaseConnect2Controller";
                warningText.gameObject.SetActive(true);
            }
            // 💡【追加】ロック中もガイドテキストの表記を「接続要求」に同期させる
            UpdateSelectionVisuals();
            // ロック中も、1Pのキャンセル操作（戻る）だけは受け付ける
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

            // ⚠️ コントローラーが足りない場合はここで処理を強制終了！
            // これにより、これ以降に記述されているキーボード操作や決定処理が一切実行されなくなります。
            return;
        }
        else
        {
            // コントローラーが2つ以上繋がっていれば警告を消して先に進める
            if (warningText != null && warningText.text == "PleaseConnect2Controller")
            {
                warningText.gameObject.SetActive(false);
            }
        }

        // 🎮【新インプットシステム排他分離レイヤー】（※ここから先はコントローラーが2つある時だけ走る）
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
                    // 1P選択中：2Pデバイス入力を完全シャットアウト
                    Vector2 nav = MenuInputManager.Instance.navigateP1.action.ReadValue<Vector2>();
                    isUpPressed = nav.y > 0.5f;
                    isDownPressed = nav.y < -0.5f;
                    isDecidePressed = MenuInputManager.Instance.submitP1.action.triggered;
                    isCancelPressed = MenuInputManager.Instance.cancelP1.action.triggered;
                }
                else if (_isP2SelectingPhase && !_isGameStartReadyPhase)
                {
                    // 2P選択中：1Pデバイス入力を完全シャットアウト
                    Vector2 nav = MenuInputManager.Instance.navigateP2.action.ReadValue<Vector2>();
                    isUpPressed = nav.y > 0.5f;
                    isDownPressed = nav.y < -0.5f;
                    isDecidePressed = MenuInputManager.Instance.submitP2.action.triggered;
                    isCancelPressed = MenuInputManager.Instance.cancelP2.action.triggered;
                }
                else if (_isGameStartReadyPhase)
                {
                    // 準備完了：どちらのデバイスからでもOK
                    isDecidePressed = MenuInputManager.Instance.submitP1.action.triggered || MenuInputManager.Instance.submitP2.action.triggered;
                    isCancelPressed = MenuInputManager.Instance.cancelP1.action.triggered || MenuInputManager.Instance.cancelP2.action.triggered;
                }
            }
            else
            {
                // 通常モード（VsComやStory）➔ 1Pの入力デバイスですべて操作
                Vector2 nav = MenuInputManager.Instance.navigateP1.action.ReadValue<Vector2>();
                isUpPressed = nav.y > 0.5f;
                isDownPressed = nav.y < -0.5f;
                isDecidePressed = MenuInputManager.Instance.submitP1.action.triggered;
                isCancelPressed = MenuInputManager.Instance.cancelP1.action.triggered;
            }
        }
        else
        {
            // 旧システムフォールバック
            isUpPressed = Input.GetKey(KeyCode.UpArrow);
            isDownPressed = Input.GetKey(KeyCode.DownArrow);
            isDecidePressed = Input.GetKeyDown(KeyCode.Z);
            isCancelPressed = Input.GetKeyDown(KeyCode.X);
        }

        // --- 以下、既存のスクロール・決定・キャンセル処理 ---
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
                p1SelectedNameText.text = "1P: " + hoveringName;
                SetCharacterImage(p1SelectedCharacterImage, hoveringSprite);
            }
        }

        // 💡 2P / COM側のテキスト＆グラフィックリアルタイム更新
        if (p2SelectedNameText != null)
        {
            // モードに応じてプレフィックスを「2P」か「COM」に切り替える
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

        // 💡 画面中央下のガイドテキスト更新
        if (guideText != null)
        {
            if (GameSelectionData.CurrentMode == GameSelectionData.GameMode.VsPlayer && GetConnectedJoystickCount() < 2)
            {
                guideText.text = "PLEASE CONNECT 2 CONTROLLERS TO VS PLAYER";
                guideText.color = Color.red;
            }
            else if (_isGameStartReadyPhase)
            {
                guideText.text = "PRESS START BUTTON TO BATTLE";
                guideText.color = unselectedColor;
            }
            else
            {
                if (_isP2SelectingPhase)
                {
                    // VsCom の時は「COM SELECT」、VsPlayer の時は「2P SELECT」
                    guideText.text = (GameSelectionData.CurrentMode == GameSelectionData.GameMode.VsCom) ? "COM SELECT" : "2P SELECT";
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
        }
        else
        {
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