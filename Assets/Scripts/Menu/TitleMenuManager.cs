using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using KanKikuchi.AudioManager;

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
    public string gameSceneName = "Shoot"; // ゲーム本編のシーン名
    [Header("Practice Menu")]
    public GameObject practiceSubMenu; // 練習用メニューのUI
    private int selectedIndex = 0;
    [Header("Character Select UI")]
    public GameObject characterSelectSubMenu; // インスペクターでキャラ選択パネルを登録
    // --- TitleMenuManager.cs の修正 ---

    void Start()
    {
        // 初期状態で練習用メニューを非表示にする
        if (practiceSubMenu != null) practiceSubMenu.SetActive(false);

        if (menuTexts == null || menuTexts.Length == 0) return;

        // menuSelectable の初期化ロジックを修正
        if (menuSelectable == null || menuSelectable.Length != menuTexts.Length)
        {
            System.Array.Resize(ref menuSelectable, menuTexts.Length);
            for (int i = 0; i < menuSelectable.Length; i++)
            {
                // インデックス 0(Start), 3(Practice), 9(Exit) などを有効にする設定
                // ここでは簡易的に 0, 3, 9 を true にします。
                menuSelectable[i] = (i == 0 || i == 3 || i == menuTexts.Length - 1);
            }
        }

        // 練習モード中なら、演習メニューへ直行する
        if (BossPracticeManager.IsPracticeMode)
        {
            OpenPracticeMenu();
        }

        selectedIndex = FindNextSelectableIndex(-1, 1);
        UpdateMenuVisuals();
    }

    void UpdateMenuVisuals()
    {
        for (int i = 0; i < menuTexts.Length; i++)
        {
            // 追加：テキスト自体がアサインされていない場合はスキップ
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
        // メニューが再び有効になったときに見た目をリフレッシュする
        UpdateMenuVisuals();
    }
    void HandleMenuNavigation()
    {
        int prevIndex = selectedIndex;

        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            selectedIndex = FindNextSelectableIndex(selectedIndex, -1);
            if (prevIndex != selectedIndex)
            {
                UpdateMenuVisuals();
                SEManager.Instance.Play(SEPath.MENUSELECT, 0.5f);
            }
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            selectedIndex = FindNextSelectableIndex(selectedIndex, 1);
            if (prevIndex != selectedIndex)
            {
                UpdateMenuVisuals();
                SEManager.Instance.Play(SEPath.MENUSELECT, 0.5f);
            }
        }

        if (Input.GetKeyDown(KeyCode.Z))
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
        SEManager.Instance.Play(SEPath.MENUDECIDE, 0.5f);

        switch (selectedIndex)
        {
            case 0: // Story Mode
                OpenCharSelect(GameSelectionData.GameMode.Story);
                break;
            case 1: // Vs Com
                OpenCharSelect(GameSelectionData.GameMode.VsCom);
                break;
            case 2: // Vs Player
                OpenCharSelect(GameSelectionData.GameMode.VsPlayer);
                break;
            case 3: // Vs Network
                OpenCharSelect(GameSelectionData.GameMode.VsNetwork);
                break;
            case 4: // Practice (スクリーンショットの順番に合わせるなら4)
                OpenPracticeMenu();
                break;
            case 9: // Exit
                // ...終了処理...
                break;
        }
    }
    void OpenCharSelect(GameSelectionData.GameMode mode)
    {
        GameSelectionData.CurrentMode = mode;

        // =========================================================================
        // 🎯【ご要望①】：選択内容に応じて GameModeManager の状態を自動同期
        // =========================================================================
        if (mode == GameSelectionData.GameMode.Story)
        {
            GameModeManager.CurrentMode = GameModeManager.Mode.Story; // 1番目 ➔ Story
        }
        else if (mode == GameSelectionData.GameMode.VsCom ||
                 mode == GameSelectionData.GameMode.VsPlayer ||
                 mode == GameSelectionData.GameMode.VsNetwork)
        {
            GameModeManager.CurrentMode = GameModeManager.Mode.Versus; // 2, 3, 4番目 ➔ Versus
        }

        // =========================================================================
        // 🎯【ご要望②】：選択内容に応じて CPU(2P)の自動よけAIのON/OFFフラグをセット
        // =========================================================================
        if (mode == GameSelectionData.GameMode.VsCom)
        {
            GameSelectionData.UseAutoEvadeAI = true;  // 2番目(VsCom) ➔ AIをONにしてCPU戦へ
        }
        else if (mode == GameSelectionData.GameMode.VsPlayer)
        {
            GameSelectionData.UseAutoEvadeAI = false; // 3番目(VsPlayer) ➔ AIをOFFにして対人戦へ
        }

        this.enabled = false; // タイトルメニューの入力を止める
        if (characterSelectSubMenu != null) characterSelectSubMenu.SetActive(true); //
    }
    void OpenPracticeMenu()
    {
        // メインメニューの入力を止めて、練習用サブメニューを表示する
        this.enabled = false;
        if (practiceSubMenu != null) practiceSubMenu.SetActive(true);
    }
}