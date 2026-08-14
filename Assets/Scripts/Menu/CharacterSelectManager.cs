// --- CharacterSelectManager.cs デバッグ機能完全除外・クリーン版 ---
using KanKikuchi.AudioManager;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CharacterSelectManager : MonoBehaviour
{
    [Header("🎯 UIアタッチ枠")]
    public TextMeshProUGUI[] charNameTexts;
    public TextMeshProUGUI randomText;
    public TextMeshProUGUI guideText;

    [Header("✨ 選択状況・GameStart表示用UI")]
    public TextMeshProUGUI p1SelectedNameText;
    public TextMeshProUGUI p2SelectedNameText;
    public TextMeshProUGUI gameStartText;
    public TextMeshProUGUI warningText;

    [Header("🖼️ プレイヤー立ち絵表示枠")]
    public Image p1SelectedCharacterImage;
    public Image p2SelectedCharacterImage;
    public Sprite randomOrDefaultSprite;

    [Header("🏷️ 1P側 通常時パッシブ・領域名表示枠")]
    public TextMeshProUGUI p1PassiveNameText;
    public TextMeshProUGUI p1SpellNameText;

    [Header("🏷️ 2P側 通常時パッシブ・領域名表示枠")]
    public TextMeshProUGUI p2PassiveNameText;
    public TextMeshProUGUI p2SpellNameText;

    [Header("⚔️ 1P側 スキルアイコン表示枠 (4つ)")]
    public Image[] p1SkillIconImages;
    public TextMeshProUGUI p1TitleSkillNameText;
    public TextMeshProUGUI p1TitleSkillDescText;

    [Header("⚔️ 2P側 スキルアイコン表示枠 (4つ)")]
    public Image[] p2SkillIconImages;
    public TextMeshProUGUI p2TitleSkillNameText;
    public TextMeshProUGUI p2TitleSkillDescText;

    [Header("📖 1P側 スキル詳細ポップアップ設定")]
    public GameObject p1SkillDetailCanvas;
    public CanvasGroup p1DetailCanvasGroup;
    public TextMeshProUGUI p1DetailSkillKeyText;
    public TextMeshProUGUI p1DetailSkillNameText;
    public TextMeshProUGUI p1DetailSkillDescText;
    public Image p1DetailSkillIconImage;

    [Header("📖 2P側 スキル詳細ポップアップ設定")]
    public GameObject p2SkillDetailCanvas;
    public CanvasGroup p2DetailCanvasGroup;
    public TextMeshProUGUI p2DetailSkillKeyText;
    public TextMeshProUGUI p2DetailSkillNameText;
    public TextMeshProUGUI p2DetailSkillDescText;
    public Image p2DetailSkillIconImage;

    [Header("📦 キャラクターデータマスター")]
    public List<PlayerSkillData> availableCharacters = new List<PlayerSkillData>();

    [Header("⚙️ 表示設定")]
    public int displayCharacterCount = 7;

    [Header("🎨 カラー表現")]
    public Color selectedColor = Color.yellow;
    public Color unselectedColor = Color.white;

    private int _currentCursor = 0;
    private int _selectableCharacterCount = 0;
    private bool _isP2SelectingPhase = false;
    private bool _isGameStartReadyPhase = false;

    private bool _isP1SkillDetailMode = false;
    private bool _isP2SkillDetailMode = false;
    private int _p1CurrentSkillIndex = 0;
    private int _p2CurrentSkillIndex = 0;

    private int _finalP1CharacterId = -1;
    private int _finalP2CharacterId = -1;
    private float _inputCooldownTimer = 0f;

    // 長押し制御管理タイマー
    private float _cursorKeyHoldTimer = 0f;
    private bool _isCursorFirstScrollDone = false;

    private float _skillKeyHoldTimer = 0f;
    private bool _isSkillFirstScrollDone = false;

    private const float MENU_FIRST_SCROLL_DELAY = 0.4f;
    private const float MENU_REPEAT_SCROLL_SPEED = 0.15f;

    private int _lastConnectedControllersCount = -1;
    public GameObject titleMenuCanvas;

    [Header("⏳ ロード画面・プログレスバー設定")]
    public GameObject loadingScreenCanvas;
    public Slider progressBarSlider;
    public TextMeshProUGUI progressText;

    private bool _isLoadingScene = false;

    void Awake()
    {
        SetDetailCanvasVisible(1, false);
        SetDetailCanvasVisible(2, false);
    }

    void OnEnable()
    {
        _isLoadingScene = false;

        PlayerStatusManager.FromCharacterSelect = true;
        _isP1SkillDetailMode = false;
        _isP2SkillDetailMode = false;
        _p1CurrentSkillIndex = 0;
        _p2CurrentSkillIndex = 0;

        SetDetailCanvasVisible(1, false);
        SetDetailCanvasVisible(2, false);

        bool isCleared = false;
        try
        {
            var saveManagerType = System.Type.GetType("SaveManager");
            if (saveManagerType != null)
            {
                isCleared = SaveManager.Load<bool>("GameCleared");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[SaveManager] 読み込み失敗: {e.Message}");
            isCleared = false;
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

        if (_selectableCharacterCount <= 0)
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
        _cursorKeyHoldTimer = 0f;
        _isCursorFirstScrollDone = false;
        _skillKeyHoldTimer = 0f;
        _isSkillFirstScrollDone = false;
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
        int activePlayer = _isP2SelectingPhase ? 2 : 1;
        bool isDetailOpen = (_isP1SkillDetailMode || _isP2SkillDetailMode);

        bool isLeftHeld = false;
        bool isRightHeld = false;

        if (MenuInputManager.Instance != null)
        {
            Vector2 nav = _isP2SelectingPhase ? MenuInputManager.Instance.navigateP2.action.ReadValue<Vector2>() : MenuInputManager.Instance.navigateP1.action.ReadValue<Vector2>();
            isLeftHeld = nav.x < -0.5f;
            isRightHeld = nav.x > 0.5f;
        }
        else
        {
            isLeftHeld = Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A);
            isRightHeld = Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D);
        }

        bool executeSkillScroll = false;
        if (isLeftHeld || isRightHeld)
        {
            if (_skillKeyHoldTimer == 0f && !_isSkillFirstScrollDone)
            {
                executeSkillScroll = true;
                _isSkillFirstScrollDone = true;
                _skillKeyHoldTimer = MENU_FIRST_SCROLL_DELAY;
            }
            else
            {
                _skillKeyHoldTimer -= Time.deltaTime;
                if (_skillKeyHoldTimer <= 0f)
                {
                    executeSkillScroll = true;
                    _skillKeyHoldTimer = MENU_REPEAT_SCROLL_SPEED;
                }
            }
        }
        else
        {
            _skillKeyHoldTimer = 0f;
            _isSkillFirstScrollDone = false;
        }

        if (executeSkillScroll && !_isGameStartReadyPhase)
        {
            if (isLeftHeld)
            {
                if (!_isP2SelectingPhase) _p1CurrentSkillIndex = (_p1CurrentSkillIndex - 1 + 4) % 4;
                else _p2CurrentSkillIndex = (_p2CurrentSkillIndex - 1 + 4) % 4;
            }
            else if (isRightHeld)
            {
                if (!_isP2SelectingPhase) _p1CurrentSkillIndex = (_p1CurrentSkillIndex + 1) % 4;
                else _p2CurrentSkillIndex = (_p2CurrentSkillIndex + 1) % 4;
            }

            if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.MENUSELECT, 0.5f);
            UpdateSelectionVisuals();
            if (isDetailOpen) UpdateSkillDetailVisuals(activePlayer);
            return;
        }

        if (isDetailOpen)
        {
            bool isClosePressed = Input.GetKeyDown(_isP1SkillDetailMode ? KeyCode.F : KeyCode.R) || Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.Escape);
            if (MenuInputManager.Instance != null)
            {
                if (_isP1SkillDetailMode && MenuInputManager.Instance.cancelP1.action.triggered) isClosePressed = true;
                if (_isP2SkillDetailMode && MenuInputManager.Instance.cancelP2.action.triggered) isClosePressed = true;
            }
            if (isClosePressed)
            {
                ToggleSkillDetailMode(_isP1SkillDetailMode ? 1 : 2);
            }
            return;
        }

        int prevCursor = _currentCursor;
        bool isStoryMode = (GameSelectionData.CurrentMode == GameSelectionData.GameMode.Story);
        int maxIndexLimit = isStoryMode ? (_selectableCharacterCount - 1) : _selectableCharacterCount;

        if (_inputCooldownTimer > 0f) _inputCooldownTimer -= Time.deltaTime;

        int connectedControllers = GetConnectedJoystickCount();
        if (connectedControllers != _lastConnectedControllersCount)
        {
            _lastConnectedControllersCount = connectedControllers;
            UpdateSelectionVisuals();
        }

        if (GameSelectionData.CurrentMode == GameSelectionData.GameMode.VsPlayer && connectedControllers < 2)
        {
            if (warningText != null) { warningText.text = "PleaseConnect2Controller"; warningText.gameObject.SetActive(true); }
            UpdateSelectionVisuals();
            bool isLockCancel = MenuInputManager.Instance != null ? MenuInputManager.Instance.cancelP1.action.triggered : Input.GetKeyDown(KeyCode.X);
            if (isLockCancel) { if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.MENUCANCEL); HandleCancel(); }
            return;
        }
        else
        {
            if (warningText != null && warningText.text == "PleaseConnect2Controller") warningText.gameObject.SetActive(false);
        }

        bool isUpHeld = false;
        bool isDownHeld = false;
        bool isDecidePressed = false;
        bool isCancelPressed = false;

        if (MenuInputManager.Instance != null)
        {
            if (GameSelectionData.CurrentMode == GameSelectionData.GameMode.VsPlayer)
            {
                if (!_isP2SelectingPhase && !_isGameStartReadyPhase)
                {
                    Vector2 nav = MenuInputManager.Instance.navigateP1.action.ReadValue<Vector2>();
                    isUpHeld = nav.y > 0.5f; isDownHeld = nav.y < -0.5f;
                    isDecidePressed = MenuInputManager.Instance.submitP1.action.triggered;
                    isCancelPressed = MenuInputManager.Instance.cancelP1.action.triggered;
                }
                else if (_isP2SelectingPhase && !_isGameStartReadyPhase)
                {
                    Vector2 nav = MenuInputManager.Instance.navigateP2.action.ReadValue<Vector2>();
                    isUpHeld = nav.y > 0.5f; isDownHeld = nav.y < -0.5f;
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
                isUpHeld = nav.y > 0.5f; isDownHeld = nav.y < -0.5f;
                isDecidePressed = MenuInputManager.Instance.submitP1.action.triggered;
                isCancelPressed = MenuInputManager.Instance.cancelP1.action.triggered;
            }
        }
        else
        {
            isUpHeld = Input.GetKey(KeyCode.UpArrow); isDownHeld = Input.GetKey(KeyCode.DownArrow);
            isDecidePressed = Input.GetKeyDown(KeyCode.Z); isCancelPressed = Input.GetKeyDown(KeyCode.X);
        }

        if (_isGameStartReadyPhase)
        {
            if (isDecidePressed && _inputCooldownTimer <= 0f) { if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.MENUDECIDE); LoadGameplayScene(); }
            if (isCancelPressed) { if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.MENUCANCEL); RollbackToP2Selection(); }
            return;
        }

        bool executeCursorScroll = false;
        if (isUpHeld || isDownHeld)
        {
            if (_cursorKeyHoldTimer == 0f && !_isCursorFirstScrollDone)
            {
                executeCursorScroll = true;
                _isCursorFirstScrollDone = true;
                _cursorKeyHoldTimer = MENU_FIRST_SCROLL_DELAY;
            }
            else
            {
                _cursorKeyHoldTimer -= Time.deltaTime;
                if (_cursorKeyHoldTimer <= 0f)
                {
                    executeCursorScroll = true;
                    _cursorKeyHoldTimer = MENU_REPEAT_SCROLL_SPEED;
                }
            }
        }
        else
        {
            _cursorKeyHoldTimer = 0f;
            _isCursorFirstScrollDone = false;
        }

        if (executeCursorScroll && isUpHeld)
        {
            _currentCursor = (_currentCursor - 1 + (maxIndexLimit + 1)) % (maxIndexLimit + 1);
        }
        else if (executeCursorScroll && isDownHeld)
        {
            _currentCursor = (_currentCursor + 1) % (maxIndexLimit + 1);
        }

        if (prevCursor != _currentCursor)
        {
            if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.MENUSELECT);
            UpdateSelectionVisuals();
        }

        if (isDecidePressed && _inputCooldownTimer <= 0f) { if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.MENUDECIDE); ConfirmSelection(); }
        if (isCancelPressed) { if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.MENUCANCEL); HandleCancel(); }
    }

    private void ToggleSkillDetailMode(int playerId)
    {
        if (playerId == 1)
        {
            _isP1SkillDetailMode = !_isP1SkillDetailMode;
            SetDetailCanvasVisible(1, _isP1SkillDetailMode);

            if (_isP1SkillDetailMode)
            {
                if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.MENUDECIDE);
                UpdateSkillDetailVisuals(1);
            }
            else
            {
                if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.MENUCANCEL);
            }
        }
        else if (playerId == 2)
        {
            _isP2SkillDetailMode = !_isP2SkillDetailMode;
            SetDetailCanvasVisible(2, _isP2SkillDetailMode);

            if (_isP2SkillDetailMode)
            {
                if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.MENUDECIDE);
                UpdateSkillDetailVisuals(2);
            }
            else
            {
                if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.MENUCANCEL);
            }
        }
    }

    private void SetDetailCanvasVisible(int playerId, bool visible)
    {
        if (playerId == 1)
        {
            if (p1SkillDetailCanvas != null) p1SkillDetailCanvas.SetActive(true);
            if (p1DetailCanvasGroup != null)
            {
                p1DetailCanvasGroup.alpha = visible ? 1f : 0f;
                p1DetailCanvasGroup.interactable = visible;
                p1DetailCanvasGroup.blocksRaycasts = visible;
            }
            else if (p1SkillDetailCanvas != null) p1SkillDetailCanvas.SetActive(visible);
        }
        else
        {
            if (p2SkillDetailCanvas != null) p2SkillDetailCanvas.SetActive(true);
            if (p2DetailCanvasGroup != null)
            {
                p2DetailCanvasGroup.alpha = visible ? 1f : 0f;
                p2DetailCanvasGroup.interactable = visible;
                p2DetailCanvasGroup.blocksRaycasts = visible;
            }
            else if (p2SkillDetailCanvas != null) p2SkillDetailCanvas.SetActive(visible);
        }
    }

    private void UpdateSkillDetailVisuals(int playerId)
    {
        PlayerSkillData currentData = GetCurrentCharacterDataForPlayer(playerId);
        if (currentData == null) return;

        bool isRandomHover = !_isGameStartReadyPhase && (_currentCursor == _selectableCharacterCount);

        int skillIndex = (playerId == 1) ? _p1CurrentSkillIndex : _p2CurrentSkillIndex;
        PlayerSkillData.SkillSettings targetSkill = GetSkillSettingsByIndex(currentData, skillIndex);

        string keyLabel = "";
        switch (skillIndex)
        {
            case 0: keyLabel = "[ Skill: Z ]"; break;
            case 1: keyLabel = "[ Skill: X ]"; break;
            case 2: keyLabel = "[ Skill: C ]"; break;
            case 3: keyLabel = "[ Skill: V ]"; break;
        }

        if (playerId == 1)
        {
            if (p1DetailSkillKeyText != null) p1DetailSkillKeyText.text = keyLabel;
            if (p1DetailSkillNameText != null) p1DetailSkillNameText.text = (isRandomHover && !_isGameStartReadyPhase) ? "？？？" : targetSkill.skillName;
            if (p1DetailSkillDescText != null) p1DetailSkillDescText.text = (isRandomHover && !_isGameStartReadyPhase) ? "？？？" : targetSkill.skillDescription;
            if (p1DetailSkillIconImage != null)
            {
                if (!isRandomHover && targetSkill.skillIcon != null) { p1DetailSkillIconImage.gameObject.SetActive(true); p1DetailSkillIconImage.sprite = targetSkill.skillIcon; }
                else { p1DetailSkillIconImage.gameObject.SetActive(false); }
            }
        }
        else
        {
            if (p2DetailSkillKeyText != null) p2DetailSkillKeyText.text = keyLabel;
            if (p2DetailSkillNameText != null) p2DetailSkillNameText.text = (isRandomHover && !_isGameStartReadyPhase) ? "？？？" : targetSkill.skillName;
            if (p2DetailSkillDescText != null) p2DetailSkillDescText.text = (isRandomHover && !_isGameStartReadyPhase) ? "？？？" : targetSkill.skillDescription;
            if (p2DetailSkillIconImage != null)
            {
                if (!isRandomHover && targetSkill.skillIcon != null) { p2DetailSkillIconImage.gameObject.SetActive(true); p2DetailSkillIconImage.sprite = targetSkill.skillIcon; }
                else { p2DetailSkillIconImage.gameObject.SetActive(false); }
            }
        }
    }

    private void UpdatePlayerTitleSkillText(int playerId)
    {
        PlayerSkillData currentData = GetCurrentCharacterDataForPlayer(playerId);
        bool isRandomHover = !_isGameStartReadyPhase && (_currentCursor == _selectableCharacterCount);

        if (playerId == 1)
        {
            bool isP1Active = !_isP2SelectingPhase && !_isGameStartReadyPhase;
            bool isP1Ready = _isP2SelectingPhase || _isGameStartReadyPhase;

            if (p1PassiveNameText != null)
            {
                p1PassiveNameText.gameObject.SetActive(isP1Active || isP1Ready);
                string pName = (currentData != null) ? currentData.customPassiveName : "？？？";
                p1PassiveNameText.text = $"パッシブ：「{pName}」";
            }
            if (p1SpellNameText != null)
            {
                p1SpellNameText.gameObject.SetActive(isP1Active || isP1Ready);
                string sName = (currentData != null) ? currentData.customSpellCardDisplayName : "？？？";
                p1SpellNameText.text = $"聖少女領域：「{sName}」";
            }

            if (p1TitleSkillNameText != null)
            {
                p1TitleSkillNameText.gameObject.SetActive(isP1Active || isP1Ready);
                PlayerSkillData.SkillSettings skill = GetSkillSettingsByIndex(currentData, _p1CurrentSkillIndex);
                p1TitleSkillNameText.text = (isRandomHover && !_isGameStartReadyPhase) ? "？？？" : skill.skillName;
            }
            if (p1TitleSkillDescText != null)
            {
                p1TitleSkillDescText.gameObject.SetActive(isP1Active || isP1Ready);
                PlayerSkillData.SkillSettings skill = GetSkillSettingsByIndex(currentData, _p1CurrentSkillIndex);
                p1TitleSkillDescText.text = (isRandomHover && !_isGameStartReadyPhase) ? "？？？" : skill.skillDescription;
            }
        }
        else
        {
            bool isP2Active = _isP2SelectingPhase && !_isGameStartReadyPhase;
            bool isP2Ready = _isGameStartReadyPhase;

            if (p2PassiveNameText != null)
            {
                p2PassiveNameText.gameObject.SetActive(isP2Active || isP2Ready);
                string pName = (currentData != null) ? currentData.customPassiveName : "？？？";
                p2PassiveNameText.text = $"パッシブ：「{pName}」";
            }
            if (p2SpellNameText != null)
            {
                p2SpellNameText.gameObject.SetActive(isP2Active || isP2Ready);
                string sName = (currentData != null) ? currentData.customSpellCardDisplayName : "？？？";
                p2SpellNameText.text = $"聖少女領域：「{sName}」";
            }

            if (p2TitleSkillNameText != null)
            {
                p2TitleSkillNameText.gameObject.SetActive(isP2Active || isP2Ready);
                PlayerSkillData.SkillSettings skill = GetSkillSettingsByIndex(currentData, _p2CurrentSkillIndex);
                p2TitleSkillNameText.text = (isRandomHover && !_isGameStartReadyPhase) ? "？？？" : skill.skillName;
            }
            if (p2TitleSkillDescText != null)
            {
                p2TitleSkillDescText.gameObject.SetActive(isP2Active || isP2Ready);
                PlayerSkillData.SkillSettings skill = GetSkillSettingsByIndex(currentData, _p2CurrentSkillIndex);
                p2TitleSkillDescText.text = (isRandomHover && !_isGameStartReadyPhase) ? "？？？" : skill.skillDescription;
            }
        }
    }

    private PlayerSkillData GetCurrentCharacterDataForPlayer(int playerId)
    {
        if (_isGameStartReadyPhase)
        {
            int charId = (playerId == 1) ? GameSelectionData.SelectedCharacterP1 : GameSelectionData.SelectedCharacterP2;
            if (charId >= 0 && charId < availableCharacters.Count)
            {
                return availableCharacters[charId];
            }
        }

        if (playerId == 1 && _isP2SelectingPhase)
        {
            int charId = GameSelectionData.SelectedCharacterP1;
            if (charId >= 0 && charId < availableCharacters.Count)
            {
                return availableCharacters[charId];
            }
        }

        if (playerId == 1 && !_isP2SelectingPhase)
        {
            return GetCurrentHoveredCharacterData();
        }
        else if (playerId == 2 && _isP2SelectingPhase)
        {
            return GetCurrentHoveredCharacterData();
        }

        return null;
    }

    private PlayerSkillData GetCurrentHoveredCharacterData()
    {
        if (_currentCursor < _selectableCharacterCount && _currentCursor < availableCharacters.Count)
        {
            return availableCharacters[_currentCursor];
        }
        return null;
    }

    private PlayerSkillData.SkillSettings GetSkillSettingsByIndex(PlayerSkillData data, int index)
    {
        if (data == null) return new PlayerSkillData.SkillSettings();

        switch (index)
        {
            case 0: return data.skillZ;
            case 1: return data.skillX;
            case 2: return data.skillC;
            case 3: return data.skillV;
            default: return data.skillZ;
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
        PlayerSkillData hoveredData = GetCurrentHoveredCharacterData();

        if (hoveredData != null)
        {
            hoveringName = hoveredData.characterName;
            hoveringSprite = hoveredData.characterSprite;
        }

        if (_isGameStartReadyPhase)
        {
            PlayerSkillData p1FinalData = GetCurrentCharacterDataForPlayer(1);
            PlayerSkillData p2FinalData = GetCurrentCharacterDataForPlayer(2);

            UpdatePlayerSkillIcons(1, p1FinalData);
            UpdatePlayerTitleSkillText(1);

            UpdatePlayerSkillIcons(2, p2FinalData);
            UpdatePlayerTitleSkillText(2);
        }
        else if (!_isP2SelectingPhase)
        {
            UpdatePlayerSkillIcons(1, hoveredData);
            UpdatePlayerTitleSkillText(1);
            TogglePlayerDisplay(2, false);
        }
        else
        {
            UpdatePlayerSkillIcons(2, hoveredData);
            UpdatePlayerTitleSkillText(2);

            PlayerSkillData p1FinalData = GetCurrentCharacterDataForPlayer(1);
            UpdatePlayerSkillIcons(1, p1FinalData);
            UpdatePlayerTitleSkillText(1);
        }

        if (_isP1SkillDetailMode) UpdateSkillDetailVisuals(1);
        if (_isP2SkillDetailMode) UpdateSkillDetailVisuals(2);

        if (p1SelectedNameText != null)
        {
            string p1Label = "1P: ";

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
                    int realP1Id = GameSelectionData.SelectedCharacterP1;
                    if (realP1Id >= 0 && realP1Id < availableCharacters.Count && availableCharacters[realP1Id] != null)
                    {
                        p1SelectedNameText.text = p1Label + availableCharacters[realP1Id].characterName;
                        SetCharacterImage(p1SelectedCharacterImage, availableCharacters[realP1Id].characterSprite);
                    }
                    else
                    {
                        p1SelectedNameText.text = p1Label + "Random";
                        SetCharacterImage(p1SelectedCharacterImage, randomOrDefaultSprite);
                    }
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
                guideText.text = "PRESS START BUTTON TO BATTLE";
                guideText.color = unselectedColor;
            }
            else
            {
                string baseGuide = "";
                if (_isP2SelectingPhase)
                {
                    baseGuide = (GameSelectionData.CurrentMode == GameSelectionData.GameMode.VsCom) ? "COM SELECT" : "2P SELECT";
                }
                else
                {
                    baseGuide = "1P SELECT";
                }

                guideText.text = baseGuide;
                guideText.color = unselectedColor;
            }
        }
    }

    private void TogglePlayerDisplay(int playerId, bool show)
    {
        if (playerId == 1)
        {
            if (p1PassiveNameText != null) p1PassiveNameText.gameObject.SetActive(show);
            if (p1SpellNameText != null) p1SpellNameText.gameObject.SetActive(show);
            if (p1TitleSkillNameText != null) p1TitleSkillNameText.gameObject.SetActive(show);
            if (p1TitleSkillDescText != null) p1TitleSkillDescText.gameObject.SetActive(show);
            if (p1SkillIconImages != null) foreach (var img in p1SkillIconImages) if (img != null) img.gameObject.SetActive(show);
        }
        else
        {
            if (p2PassiveNameText != null) p2PassiveNameText.gameObject.SetActive(show);
            if (p2SpellNameText != null) p2SpellNameText.gameObject.SetActive(show);
            if (p2TitleSkillNameText != null) p2TitleSkillNameText.gameObject.SetActive(show);
            if (p2TitleSkillDescText != null) p2TitleSkillDescText.gameObject.SetActive(show);
            if (p2SkillIconImages != null) foreach (var img in p2SkillIconImages) if (img != null) img.gameObject.SetActive(show);
        }
    }

    private void UpdatePlayerSkillIcons(int playerId, PlayerSkillData charData)
    {
        Image[] targetIcons = (playerId == 1) ? p1SkillIconImages : p2SkillIconImages;
        if (targetIcons == null || targetIcons.Length == 0) return;

        bool isRandomHover = !_isGameStartReadyPhase && (_currentCursor == _selectableCharacterCount);

        if (playerId == 2 && !_isP2SelectingPhase && !_isGameStartReadyPhase)
        {
            foreach (var icon in targetIcons) if (icon != null) icon.gameObject.SetActive(false);
            return;
        }

        if (charData == null || (isRandomHover && !_isGameStartReadyPhase))
        {
            foreach (var icon in targetIcons) if (icon != null) icon.gameObject.SetActive(false);
            return;
        }

        for (int i = 0; i < targetIcons.Length; i++)
        {
            if (targetIcons[i] == null) continue;

            PlayerSkillData.SkillSettings skill = GetSkillSettingsByIndex(charData, i);

            if (!string.IsNullOrEmpty(skill.skillName) && skill.skillIcon != null)
            {
                targetIcons[i].gameObject.SetActive(true);
                targetIcons[i].sprite = skill.skillIcon;

                int currentIndex = (playerId == 1) ? _p1CurrentSkillIndex : _p2CurrentSkillIndex;
                targetIcons[i].color = (i == currentIndex) ? selectedColor : unselectedColor;
            }
            else
            {
                targetIcons[i].gameObject.SetActive(false);
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

    private void ApplyAspectFit(Image targetImage, Sprite sprite)
    {
        if (targetImage == null || sprite == null) return;
        targetImage.preserveAspect = true;

        RectTransform rectTransform = targetImage.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            float spriteWidth = sprite.rect.width;
            float spriteHeight = sprite.rect.height;

            if (spriteWidth > 0f && spriteHeight > 0f)
            {
                float aspectRatio = spriteWidth / spriteHeight;
                float currentHeight = rectTransform.sizeDelta.y;
                if (currentHeight <= 0f) currentHeight = rectTransform.rect.height;
                if (currentHeight <= 0f) currentHeight = 300f;

                rectTransform.sizeDelta = new Vector2(currentHeight * aspectRatio, currentHeight);
            }
        }
    }

    private void ConfirmSelection()
    {
        if (!_isP2SelectingPhase)
        {
            _finalP1CharacterId = _currentCursor;

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

            _inputCooldownTimer = 0.2f;
            EnterGameStartReady();
        }
    }

    private void EnterGameStartReady()
    {
        _isGameStartReadyPhase = true;

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
        if (_isLoadingScene) return;
        _isLoadingScene = true;

        BGMManager.Instance.FadeOut();
        StartCoroutine(LoadSceneAsyncRoutine("Shoot"));
    }

    private IEnumerator LoadSceneAsyncRoutine(string sceneName)
    {
        if (loadingScreenCanvas != null)
        {
            loadingScreenCanvas.SetActive(true);
        }

        AsyncOperation asyncOp = SceneManager.LoadSceneAsync(sceneName);
        asyncOp.allowSceneActivation = false;

        float fakeProgress = 0f;
        float targetFakeProgress = 0f;
        float timer = 0f;

        while (!asyncOp.isDone)
        {
            float realProgress = Mathf.Clamp01(asyncOp.progress / 0.9f);

            timer -= Time.unscaledDeltaTime;
            if (timer <= 0f)
            {
                timer = Random.Range(0.05f, 0.22f);

                if (fakeProgress < realProgress)
                {
                    float maxNext = Mathf.Min(realProgress, fakeProgress + Random.Range(0.02f, 0.12f));
                    targetFakeProgress = Random.Range(fakeProgress, maxNext);
                }
                else if (realProgress >= 1.0f && fakeProgress < 0.95f)
                {
                    targetFakeProgress = Mathf.MoveTowards(fakeProgress, 1.0f, Random.Range(0.03f, 0.08f));
                }
            }

            fakeProgress = Mathf.MoveTowards(fakeProgress, targetFakeProgress, Time.unscaledDeltaTime * Random.Range(0.6f, 1.5f));

            if (fakeProgress > realProgress && realProgress < 1.0f)
            {
                fakeProgress = realProgress;
            }

            if (progressBarSlider != null)
            {
                progressBarSlider.value = fakeProgress;
            }

            if (progressText != null)
            {
                progressText.text = $"{Mathf.RoundToInt(fakeProgress * 100f)}%";
            }

            if (fakeProgress >= 0.99f && realProgress >= 1.0f)
            {
                if (progressBarSlider != null) progressBarSlider.value = 1.0f;
                if (progressText != null) progressText.text = "100%";

                yield return new WaitForSecondsRealtime(0.25f);

                Time.timeScale = 1.0f;
                PlayerMove.CanInput = true;
                PlayerMove.CanShoot = true;
                PlayerStatusManager.isAnyVJTActive = false;

                asyncOp.allowSceneActivation = true;
            }

            yield return null;
        }
    }
}