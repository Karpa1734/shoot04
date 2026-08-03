// --- TitleMenuManager.cs 独立型コンフィグパネル・Story/Versusモードフラグ完全適合版 ---
using KanKikuchi.AudioManager;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class TitleMenuManager : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI[] menuTexts;

    [Header("Selection Settings")]
    public bool[] menuSelectable;
    [Range(0f, 1f)] public float disabledAlpha = 0.3f;

    [Header("Color Settings")]
    public Color selectedColor = Color.white;
    public Color unselectedColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    [Header("Scene Settings")]
    public string gameSceneName = "Shoot";

    [Header("Practice Menu")]
    public GameObject practiceSubMenu;
    private int selectedIndex = 0;

    [Header("Character Select UI")]
    public GameObject characterSelectSubMenu;

    // =========================================================================
    // 🔧【新設】：独立コンフィグパネル（GameObject）のアタッチ枠
    // =========================================================================
    [Header("🔧 Config SubMenu Panel")]
    [Tooltip("新設した ConfigMenuManager スクリプトが付いているコンフィグパネルを登録してください")]
    public GameObject configSubMenu;

    // ⏳ 長押しスクロール（暴走ガード）用の管理タイマー
    private float _menuKeyHoldTimer = 0f;
    private bool _isMenuFirstScrollDone = false;
    private const float MENU_FIRST_SCROLL_DELAY = 0.4f;
    private const float MENU_REPEAT_SCROLL_SPEED = 0.12f;

    void Start()
    {
        BGMManager.Instance.Play(BGMPath.TITLE,1.0f);
        BossPracticeManager.IsPracticeMode = false;

        if (practiceSubMenu != null) practiceSubMenu.SetActive(false);
        if (configSubMenu != null) configSubMenu.SetActive(false);

        if (menuTexts == null || menuTexts.Length == 0) return;

        // 配列要素 0, 1, 2, 3, 4, 8(Config), 9(Exit) を動的リサイズ対応
        if (menuSelectable == null || menuSelectable.Length != menuTexts.Length)
        {
            System.Array.Resize(ref menuSelectable, menuTexts.Length);
            for (int i = 0; i < menuSelectable.Length; i++)
            {
                menuSelectable[i] = (i == 0 || i == 1 || i == 2 || i == 3 || i == 4 || i == 8 || i == menuTexts.Length - 1);
            }
        }

        selectedIndex = FindNextSelectableIndex(-1, 1);
        UpdateMenuVisuals();
    }

    void UpdateMenuVisuals()
    {
        for (int i = 0; i < menuTexts.Length; i++)
        {
            if (menuTexts[i] == null) continue;

            if (!menuSelectable[i])
            {
                Color c = unselectedColor;
                c.a = disabledAlpha;
                menuTexts[i].color = c;
            }
            else
            {
                menuTexts[i].color = (i == selectedIndex) ? selectedColor : unselectedColor;
            }
        }
    }

    void Update()
    {
        HandleMenuNavigation();
    }

    void OnEnable()
    {
        selectedIndex = FindNextSelectableIndex(-1, 1);
        _menuKeyHoldTimer = 0f;
        _isMenuFirstScrollDone = false;

        UpdateMenuVisuals();
    }

    void HandleMenuNavigation()
    {
        int prevIndex = selectedIndex;

        bool isUpHeld = false;
        bool isDownHeld = false;
        bool isDecidePressed = false;

        if (MenuInputManager.Instance != null)
        {
            Vector2 nav = MenuInputManager.Instance.navigateP1.action.ReadValue<Vector2>();
            isUpHeld = nav.y > 0.5f;
            isDownHeld = nav.y < -0.5f;
            isDecidePressed = MenuInputManager.Instance.submitP1.action.triggered;
        }
        else
        {
            isUpHeld = Input.GetKey(KeyCode.UpArrow);
            isDownHeld = Input.GetKey(KeyCode.DownArrow);
            isDecidePressed = Input.GetKeyDown(KeyCode.Z);
        }

        bool executeScroll = false;

        if (isUpHeld || isDownHeld)
        {
            if (_menuKeyHoldTimer == 0f && !_isMenuFirstScrollDone)
            {
                executeScroll = true;
                _isMenuFirstScrollDone = true;
                _menuKeyHoldTimer = MENU_FIRST_SCROLL_DELAY;
            }
            else
            {
                _menuKeyHoldTimer -= Time.deltaTime;
                if (_menuKeyHoldTimer <= 0f)
                {
                    executeScroll = true;
                    _menuKeyHoldTimer = MENU_REPEAT_SCROLL_SPEED;
                }
            }
        }
        else
        {
            _menuKeyHoldTimer = 0f;
            _isMenuFirstScrollDone = false;
        }

        if (executeScroll && isUpHeld)
        {
            selectedIndex = FindNextSelectableIndex(selectedIndex, -1);
            if (prevIndex != selectedIndex)
            {
                UpdateMenuVisuals();
                if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.MENUSELECT, 0.5f);
            }
        }
        else if (executeScroll && isDownHeld)
        {
            selectedIndex = FindNextSelectableIndex(selectedIndex, 1);
            if (prevIndex != selectedIndex)
            {
                UpdateMenuVisuals();
                if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.MENUSELECT, 0.5f);
            }
        }

        if (isDecidePressed)
        {
            if (menuSelectable[selectedIndex])
            {
                ExecuteSelection();
            }
        }
    }

    int FindNextSelectableIndex(int current, int direction)
    {
        int count = menuTexts.Length;
        int next = current;
        for (int i = 0; i < count; i++)
        {
            next = (next + direction + count) % count;
            if (menuSelectable[next]) return next;
        }
        return (current == -1) ? 0 : current;
    }

    void ExecuteSelection()
    {
        if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.MENUDECIDE, 0.5f);

        switch (selectedIndex)
        {
            case 0: OpenCharSelect(GameSelectionData.GameMode.Story); break; // 🎯 1番目(Index 0)でもキャラセレクトを開く
            case 1: OpenCharSelect(GameSelectionData.GameMode.VsCom); break;
            case 2: OpenCharSelect(GameSelectionData.GameMode.VsPlayer); break;
            case 3: OpenCharSelect(GameSelectionData.GameMode.VsNetwork); break;
            case 4: OpenPracticeMenu(); break;
            case 8: OpenConfigMenu(); break;
            case 9: StartEndGameSequence(); break;
        }
    }

    void OpenConfigMenu()
    {
        this.enabled = false;
        if (configSubMenu != null) configSubMenu.SetActive(true);

        _menuKeyHoldTimer = 0f;
        _isMenuFirstScrollDone = false;
    }

    void StartEndGameSequence()
    {
        this.enabled = false;
        Invoke(nameof(End), 1.0f);
    }

    void End()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // =========================================================================
    // 📖【モード分岐＆キャラセレクト遷移レイヤー】
    // =========================================================================
    void OpenCharSelect(GameSelectionData.GameMode mode)
    {
        // 1. 選択データを共有バッファにセット
        GameSelectionData.CurrentMode = mode; 

        // 2. 🌟 StoryモードかVersusモードかのフラグを厳密に立てる
        if (mode == GameSelectionData.GameMode.Story)
        {
            GameModeManager.CurrentMode = GameModeManager.Mode.Story; 
            GameSelectionData.UseAutoEvadeAI = true; // ストーリー時も相手(2P)はCPU自動回避
            Debug.Log("<color=cyan>📖【Mode Setup】Story Mode フラグ(GameModeManager.IsStoryMode = true)を確立しました。</color>");
        }
        else if (mode == GameSelectionData.GameMode.VsCom ||
                 mode == GameSelectionData.GameMode.VsPlayer ||
                 mode == GameSelectionData.GameMode.VsNetwork)
        {
            GameModeManager.CurrentMode = GameModeManager.Mode.Versus; 
            
            if (mode == GameSelectionData.GameMode.VsCom) GameSelectionData.UseAutoEvadeAI = true; 
            else if (mode == GameSelectionData.GameMode.VsPlayer) GameSelectionData.UseAutoEvadeAI = false; 
        }

        // 3. 自身のスクリプトをOFFにし、キャラ選択画面のCanvasを表示してトランスファー
        this.enabled = false; 
        if (characterSelectSubMenu != null) characterSelectSubMenu.SetActive(true); 

        _menuKeyHoldTimer = 0f; 
        _isMenuFirstScrollDone = false; 
    }

    void OpenPracticeMenu()
    {
        this.enabled = false; 
        if (practiceSubMenu != null) practiceSubMenu.SetActive(true); 
    }
}