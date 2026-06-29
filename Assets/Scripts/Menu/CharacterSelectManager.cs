// --- CharacterSelectManager.cs キャラの選択・左右名表示・GameStartロック・ランダム決定後ネーム同期版 ---
using KanKikuchi.AudioManager;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
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

    // 1P/2Pが最終決定した「カーソル位置」のインデックス保存用（RandomならRandomのインデックスを保持）
    private int _finalP1CharacterId = -1;
    private int _finalP2CharacterId = -1;
    // 🌟【新設】：フェーズ切り替え時やシーン遷移直後の決定キー暴発（突き抜け）を防ぐ入力冷却タイマー
    private float _inputCooldownTimer = 0f;
    // 🕒 長押し（ホールド）スクロール管理用タイマー
    private float _keyHoldTimer = 0f;
    private bool _isFirstScrollDone = false;

    private const float FIRST_SCROLL_DELAY = 0.4f;
    private const float REPEAT_SCROLL_SPEED = 0.08f;

    public GameObject titleMenuCanvas;

    void OnEnable()
    {
        // 🎯【核心連動】：選択画面が起動したため、ゲームシーン側の直接デバッグ上書きを永久ロック（禁止）します
        PlayerStatusManager.FromCharacterSelect = true;
        // =========================================================================
        // 💾【セーブシステム統合】：独自Bitセーブインフラからクリアフラグをデコード
        // =========================================================================
        bool isCleared = false;
        try
        {
            isCleared = SaveManager.Load<bool>("GameCleared");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[SaveManager] GameClearedの読み込みに失敗、または未定義です。デフォルト(false)を適用します: {e.Message}");
        }

        // 基本はデータ全件。ただし未クリアなら最大7件に絞るなどの調整
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

        // 🎯 画面が開いた直後の0.2秒間は入力を受け付けない（暴発ガード）
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
        // 🎯 毎フレーム、入力冷却タイマーを安全に減算カウントダウン
        if (_inputCooldownTimer > 0f)
        {
            _inputCooldownTimer -= Time.deltaTime;
        }
        // GameStart準備完了フェーズ（カーソルロック状態）
        if (_isGameStartReadyPhase)
        {
            if (Input.GetKeyDown(KeyCode.Z))
            {
                if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.MENUDECIDE);
                LoadGameplayScene();
            }

            if (Input.GetKeyDown(KeyCode.X))
            {
                if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.MENUCANCEL);
                RollbackToP2Selection();
            }
            return;
        }

        // 上下入力処理
        bool isUpPressed = Input.GetKey(KeyCode.UpArrow);
        bool isDownPressed = Input.GetKey(KeyCode.DownArrow);

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

        if (Input.GetKeyDown(KeyCode.Z) && _inputCooldownTimer <= 0f)
        {
            if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.MENUDECIDE);
            ConfirmSelection();
        }

        if (Input.GetKeyDown(KeyCode.X))
        {
            if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.MENUCANCEL);
            HandleCancel();
        }
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

        // 左右の表示名テキスト用バッファを取得（選択移動中の仮表示名）
        string hoveringName = "Random";
        if (_currentCursor < _selectableCharacterCount && _currentCursor < availableCharacters.Count && availableCharacters[_currentCursor] != null)
        {
            hoveringName = availableCharacters[_currentCursor].characterName;
        }

        // 💡 1P側のテキスト表示更新
        if (p1SelectedNameText != null)
        {
            if (_isGameStartReadyPhase)
            {
                // 🔥【修正点】最終決定状態(GameStart表示中)なら、抽選で決定済みの実際のキャラ名を表示
                int p1RealId = GameSelectionData.SelectedCharacterP1;
                if (p1RealId >= 0 && p1RealId < availableCharacters.Count && availableCharacters[p1RealId] != null)
                {
                    string randomPrefix = (_finalP1CharacterId == _selectableCharacterCount) ? "" : "";
                    p1SelectedNameText.text = "1P: " + randomPrefix + availableCharacters[p1RealId].characterName;
                }
            }
            else if (_isP2SelectingPhase && _finalP1CharacterId >= 0)
            {
                // 2P選択中（1P決定済み、かつGameStart前）は、元がRandomなら「Random」のままにする
                string p1Name = (_finalP1CharacterId == _selectableCharacterCount) ? "Random" : availableCharacters[_finalP1CharacterId].characterName;
                p1SelectedNameText.text = "1P: " + p1Name;
            }
            else
            {
                // 1P選択中
                p1SelectedNameText.text = "1P: " + hoveringName;
            }
        }

        // 💡 2P側のテキスト表示更新
        if (p2SelectedNameText != null)
        {
            if (_isGameStartReadyPhase)
            {
                // 🔥【修正点】最終決定状態(GameStart表示中)なら、抽選で決定済みの実際のキャラ名を表示
                int p2RealId = GameSelectionData.SelectedCharacterP2;
                if (p2RealId >= 0 && p2RealId < availableCharacters.Count && availableCharacters[p2RealId] != null)
                {
                    string randomPrefix = (_finalP2CharacterId == _selectableCharacterCount) ? "" : "";
                    p2SelectedNameText.text = "2P: " + randomPrefix + availableCharacters[p2RealId].characterName;
                }
            }
            else if (_isP2SelectingPhase)
            {
                // 2P選択中
                p2SelectedNameText.text = "2P: " + hoveringName;
            }
            else
            {
                // 1P選択中
                p2SelectedNameText.text = "2P: Selecting...";
            }
        }

        if (guideText != null)
        {
            if (_isGameStartReadyPhase) guideText.text = "PRESS Z TO START";
            else guideText.text = _isP2SelectingPhase ? "2P SELECT" : "1P SELECT";
        }
    }

    private void ConfirmSelection()
    {
        // 1P選択フェーズ
        if (!_isP2SelectingPhase)
        {
            _finalP1CharacterId = _currentCursor;

            if (GameSelectionData.CurrentMode == GameSelectionData.GameMode.VsCom ||
                GameSelectionData.CurrentMode == GameSelectionData.GameMode.VsPlayer)
            {
                _isP2SelectingPhase = true;
                _currentCursor = 0;

                // 🎯【最核心】：1P決定の瞬間に 0.2秒 の冷却時間をチャージし、2Pの即時自動決定を100%阻止！
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
        // 2P選択フェーズ
        else
        {
            _finalP2CharacterId = _currentCursor;
            _inputCooldownTimer = 0.2f; // 2P決定時からGameStart画面への移行時も安全にガード
            EnterGameStartReady();
        }
    }

    private void EnterGameStartReady()
    {
        _isGameStartReadyPhase = true;

        // 🎲 1Pランダム抽選実行
        if (_finalP1CharacterId == _selectableCharacterCount)
        {
            GameSelectionData.SelectedCharacterP1 = Random.Range(0, _selectableCharacterCount);
        }
        else
        {
            GameSelectionData.SelectedCharacterP1 = _finalP1CharacterId;
        }

        // 🎲 2Pランダム抽選実行
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

        // 確定した実際のキャラクター名を左右UIに強制反映させる
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