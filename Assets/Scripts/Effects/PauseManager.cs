using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using KanKikuchi.AudioManager;
using UnityEngine.UI;
using System.Collections;

public class PauseManager : MonoBehaviour
{

    [Header("UI Elements")]
    public GameObject pauseCanvas;
    public TextMeshProUGUI[] menuTexts;

    [Header("Confirmation UI")]
    public GameObject confirmPanel; // 確認ダイアログの親オブジェクト
    public TextMeshProUGUI confirmYesText;
    public TextMeshProUGUI confirmNoText;

    [Header("Selection Settings")]
    public bool[] menuSelectable;
    [Range(0f, 1f)] public float disabledAlpha = 0.3f;

    [Header("Color Settings")]
    public Color selectedColor = Color.white;
    public Color unselectedColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    private bool isPaused = false;
    private int selectedIndex = 0;
    private bool isGameOverMode = false;

    // --- 追加：状態管理用 ---
    private enum PauseState { Main, ConfirmExit, ConfirmRestart }
    private PauseState currentState = PauseState.Main;
    private int confirmIndex = 1; // 0: Yes, 1: No (初期位置をNoに)
    private bool isPracticeResultMode = false; // ★追加：演習リザルト中かどうかのフラグ]]

    // 💡【追加】長押しスクロール制御用のタイマー変数
    private float _keyHoldTimer = 0f;
    private bool _isFirstScrollDone = false;
    private const float FIRST_SCROLL_DELAY = 0.4f;
    private const float REPEAT_SCROLL_SPEED = 0.08f;
    [Header("⏳ ロード画面・プログレスバー設定")]
    [Tooltip("ロード中に表示する専用のCanvasやPanel（非同期ロード中のみActiveにする）")]
    public GameObject loadingScreenCanvas;
    [Tooltip("進捗状況を表示するUI Slider（値の範囲は 0.0 ～ 1.0）")]
    public Slider progressBarSlider;
    [Tooltip("進捗率をパーセンテージ（例: 50%）で表示するテキストUI（任意）")]
    public TextMeshProUGUI progressText;

    void Start()
    {
        if (pauseCanvas != null) pauseCanvas.SetActive(false);
        if (confirmPanel != null) confirmPanel.SetActive(false);

        if (menuSelectable == null || menuSelectable.Length != menuTexts.Length)
        {
            System.Array.Resize(ref menuSelectable, menuTexts.Length);
            for (int i = 0; i < menuSelectable.Length; i++) menuSelectable[i] = true;
        }
    }

    void Update()
    {
        // ★練習モード中かつUnityエディタ上のみ、Backspaceキーで即座にリトライ（デバッグ用）
#if UNITY_EDITOR
        if (BossPracticeManager.IsPracticeMode && !isPaused && Input.GetKeyDown(KeyCode.Backspace))
        {
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            return;
        }
#endif

        // 🎮【New Input System完全統合】ポーズボタンの入力検知
        bool isPauseTriggered = false;

        if (InputManager.Instance != null)
        {
            // 💡 1Pまたは2Pのどちらかが「ポーズボタン（スタートボタン等）」を押した瞬間をフック
            bool p1Pause = InputManager.Instance.player1.pause != null && InputManager.Instance.player1.pause.action.triggered;
            bool p2Pause = InputManager.Instance.player2.pause != null && InputManager.Instance.player2.pause.action.triggered;

            isPauseTriggered = p1Pause || p2Pause;
        }
        else
        {
            // フォールバック用の従来キー（万が一InputManagerがない時用）
            isPauseTriggered = Input.GetKeyDown(KeyCode.Escape);
        }

        // 🕒 ポーズ開閉の執行ジャッジ
        if (isPauseTriggered)
        {
            if (isPaused)
            {
                // 演習リザルト画面、またはゲームオーバー画面の時はポーズ強制解除を無効化（誤操作防止）
                if (isPracticeResultMode || isGameOverMode) return;

                // 確認ダイアログ（本当にタイトルに戻る？等）を開いている時は、1個前のメインポーズメニューに戻る
                if (currentState != PauseState.Main) CancelConfirmation();
                else ResumeGame(); // 通常ポーズ中ならゲーム再開
            }
            else
            {
                // 🚨 カウントダウン中など（自機の移動・入力が許可されていないタイミング）はポーズを開かない
                if (!PlayerMove.CanInput) return;

                PauseGame(); // ポーズ画面を開く
            }
        }

        // 🔔 ポーズ中のメニュー操作ナビゲーション
        if (isPaused)
        {
            if (currentState == PauseState.Main) HandleMenuNavigation();
            else HandleConfirmNavigation(); // 確認画面用
        }
    }

    public void PauseGame()
    {
        isPaused = true;
        pauseCanvas.SetActive(true);
        confirmPanel.SetActive(false);
        currentState = PauseState.Main;
        Time.timeScale = 0f;
        selectedIndex = FindNextSelectableIndex(-1, 1);
        UpdateMenuVisuals();
        SEManager.Instance.Play(SEPath.PAUSE, 0.5f);
        BGMManager.Instance.Pause();
    }

    public void ResumeGame()
    {
        isPaused = false;
        pauseCanvas.SetActive(false);
        confirmPanel.SetActive(false);

        // 💡【追加】長押しスクロール用のタイマーと判定を完全にリセットしてゲームに戻る
        _keyHoldTimer = 0f;
        _isFirstScrollDone = false;

        Time.timeScale = 1f;
        SEManager.Instance.Play(SEPath.MENUCANCEL, 0.5f);
        BGMManager.Instance.UnPause();
    }

    void HandleMenuNavigation()
    {
        int prevIndex = selectedIndex;

        // 🎮【新インプットシステム排他分離レイヤー】の適用
        bool isUpPressed = false;
        bool isDownPressed = false;
        bool isDecidePressed = false;

        if (MenuInputManager.Instance != null)
        {
            // 通常ポーズ時は1Pのみ、ゲームオーバー時は1P・2Pどちらからでも操作可能にするガード
            Vector2 nav = Vector2.zero;
            if (isGameOverMode)
            {
                Vector2 navP1 = MenuInputManager.Instance.navigateP1.action.ReadValue<Vector2>();
                Vector2 navP2 = MenuInputManager.Instance.navigateP2.action.ReadValue<Vector2>();
                nav = navP1.sqrMagnitude > navP2.sqrMagnitude ? navP1 : navP2;

                isDecidePressed = MenuInputManager.Instance.submitP1.action.triggered ||
                                  MenuInputManager.Instance.submitP2.action.triggered;
            }
            else
            {
                nav = MenuInputManager.Instance.navigateP1.action.ReadValue<Vector2>();
                isDecidePressed = MenuInputManager.Instance.submitP1.action.triggered;
            }

            isUpPressed = nav.y > 0.5f;
            isDownPressed = nav.y < -0.5f;
        }
        else
        {
            // 旧システムフォールバック
            isUpPressed = Input.GetKey(KeyCode.UpArrow);
            isDownPressed = Input.GetKey(KeyCode.DownArrow);
            isDecidePressed = Input.GetKeyDown(KeyCode.Z);
        }

        // キャラ選択画面と完全に同一の滑らかな長押しスクロールアルゴリズム
        if (isUpPressed || isDownPressed)
        {
            if (_keyHoldTimer == 0f && !_isFirstScrollDone)
            {
                if (isUpPressed) selectedIndex = FindNextSelectableIndex(selectedIndex, -1);
                if (isDownPressed) selectedIndex = FindNextSelectableIndex(selectedIndex, 1);

                _isFirstScrollDone = true;
                _keyHoldTimer = FIRST_SCROLL_DELAY;
            }
            else
            {
                _keyHoldTimer -= Time.unscaledDeltaTime; // 💡 ポーズ中はTime.timeScaleが0なため、unscaledを使用
                if (_keyHoldTimer <= 0f)
                {
                    if (isUpPressed) selectedIndex = FindNextSelectableIndex(selectedIndex, -1);
                    if (isDownPressed) selectedIndex = FindNextSelectableIndex(selectedIndex, 1);

                    _keyHoldTimer = REPEAT_SCROLL_SPEED;
                }
            }
        }
        else
        {
            _keyHoldTimer = 0f;
            _isFirstScrollDone = false;
        }

        if (prevIndex != selectedIndex)
        {
            UpdateMenuVisuals();
            SEManager.Instance.Play(SEPath.MENUSELECT, 0.5f);
        }

        if (isDecidePressed)
        {
            if (menuSelectable[selectedIndex]) ExecuteSelection();
        }
    }

    // --- 追加：確認画面のナビゲーション ---
    void HandleConfirmNavigation()
    {
        int prev = confirmIndex;
        bool isLeftRightPressed = false;
        bool isDecidePressed = false;
        bool isCancelPressed = false;

        if (MenuInputManager.Instance != null)
        {
            Vector2 nav = isGameOverMode ?
                MenuInputManager.Instance.navigateP1.action.ReadValue<Vector2>() + MenuInputManager.Instance.navigateP2.action.ReadValue<Vector2>() :
                MenuInputManager.Instance.navigateP1.action.ReadValue<Vector2>();

            // スティックが左右上下どちらに倒れてもトグルが切り替わるように判定
            if (_keyHoldTimer <= 0f)
            {
                if (Mathf.Abs(nav.x) > 0.5f || Mathf.Abs(nav.y) > 0.5f)
                {
                    isLeftRightPressed = true;
                    _keyHoldTimer = FIRST_SCROLL_DELAY; // 連続トグル防止ウェイト
                }
            }
            else
            {
                _keyHoldTimer -= Time.unscaledDeltaTime;
                if (Mathf.Abs(nav.x) < 0.1f && Mathf.Abs(nav.y) < 0.1f) _keyHoldTimer = 0f; // 指を離したら即座にリセット
            }

            isDecidePressed = isGameOverMode ?
                (MenuInputManager.Instance.submitP1.action.triggered || MenuInputManager.Instance.submitP2.action.triggered) :
                MenuInputManager.Instance.submitP1.action.triggered;

            isCancelPressed = isGameOverMode ?
                (MenuInputManager.Instance.cancelP1.action.triggered || MenuInputManager.Instance.cancelP2.action.triggered) :
                MenuInputManager.Instance.cancelP1.action.triggered;
        }
        else
        {
            isLeftRightPressed = Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow) ||
                                 Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow);
            isDecidePressed = Input.GetKeyDown(KeyCode.Z);
            isCancelPressed = Input.GetKeyDown(KeyCode.X);
        }

        if (isLeftRightPressed)
        {
            confirmIndex = (confirmIndex == 0) ? 1 : 0;
            if (prev != confirmIndex) { UpdateConfirmVisuals(); SEManager.Instance.Play(SEPath.MENUSELECT, 0.5f); }
        }

        if (isDecidePressed)
        {
            if (confirmIndex == 0) ExecuteConfirmedAction(); // Yes
            else CancelConfirmation(); // No
        }

        if (isCancelPressed) CancelConfirmation();
    }

    void ExecuteSelection()
    {
        switch (selectedIndex)
        {
            case 0: // 再開 / コンティニュー
                SEManager.Instance.Play(SEPath.MENUDECIDE, 0.5f);
                if (isGameOverMode)
                {
                    // ★修正：全プレイヤーをコンティニューさせる
                    foreach (var player in PlayerMove.AllPlayers)
                    {
                        var status = player.GetComponent<PlayerStatusManager>();
                        if (status != null) status.PerformContinue();
                    }
                }
                isGameOverMode = false;
                ResumeGame();
                break;

            case 1: // タイトルへ / ゲームを終了
                if (isPracticeResultMode)
                {
                    // ★追加：撃破後なら確認なしで即実行
                    currentState = PauseState.ConfirmExit;
                    ExecuteConfirmedAction();
                }
                else
                {
                    // 通常時は確認画面を開く
                    OpenConfirmation(PauseState.ConfirmExit);
                }
                break;

            case 4: // 最初からやり直す
                if (isPracticeResultMode)
                {
                    // ★追加：撃破後なら確認なしで即実行
                    currentState = PauseState.ConfirmRestart;
                    ExecuteConfirmedAction();
                }
                else
                {
                    // 通常時は確認画面を開く
                    OpenConfirmation(PauseState.ConfirmRestart);
                }
                break;

            default:
                // その他の項目（操作説明など）
                SEManager.Instance.Play(SEPath.MENUDECIDE, 0.5f);
                break;
        }
    }
    void OpenConfirmation(PauseState state)
    {
        SEManager.Instance.Play(SEPath.MENUDECIDE, 0.5f);
        currentState = state;
        confirmIndex = 1; // 初期位置を No に設定
        confirmPanel.SetActive(true);
        UpdateConfirmVisuals();
    }

    void CancelConfirmation()
    {
        SEManager.Instance.Play(SEPath.MENUCANCEL, 0.5f);
        currentState = PauseState.Main;
        confirmPanel.SetActive(false);
    }

    void ExecuteConfirmedAction()
    {
        SEManager.Instance.Play(SEPath.MENUDECIDE, 0.5f);

        if (ScoreManager.Instance != null) ScoreManager.Instance.SaveHighScore();

        foreach (var player in PlayerMove.AllPlayers)
        {
            var status = player.GetComponent<PlayerStatusManager>();
            if (status != null) status.ResetContinueCount();
        }

        // ❌ 修正前：ここで Time.timeScale = 1.0f に戻していたため、裏で時間が進んでしまっていました。
        // ⭕ 修正：時間は 0f（停止状態）のまま維持し、バトルが一切進まないようにします！

        if (currentState == PauseState.ConfirmExit)
        {
            // 🌟 タイトルに戻る際は時間を止めたままロード画面を挟んで非同期ロードを実行
            StartCoroutine(LoadSceneAsyncRoutine("Title"));
        }
        else if (currentState == PauseState.ConfirmRestart)
        {
            // 🌟 リトライ時も同様に時間を止めたまま現在のシーンを非同期ロード
            StartCoroutine(LoadSceneAsyncRoutine(SceneManager.GetActiveScene().name));
        }
    }
    private IEnumerator LoadSceneAsyncRoutine(string sceneName)
    {
        if (pauseCanvas != null) pauseCanvas.SetActive(false);
        if (confirmPanel != null) confirmPanel.SetActive(false);

        if (loadingScreenCanvas != null)
        {
            loadingScreenCanvas.SetActive(true);
        }

        if (BGMManager.Instance != null)
        {
            BGMManager.Instance.FadeOut();
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

                // =========================================================================
                // 🌟【最重要修正】：新しいシーンへ移行する直前に、必ず時間停止（Time.timeScale）を解除する！
                // =========================================================================
                Time.timeScale = 1.0f;
                PlayerMove.CanInput = true;
                PlayerMove.CanShoot = true;
                PlayerStatusManager.isAnyVJTActive = false;

                asyncOp.allowSceneActivation = true;
            }

            yield return null;
        }
    }
    void UpdateConfirmVisuals()
    {
        confirmYesText.color = (confirmIndex == 0) ? selectedColor : unselectedColor;
        confirmNoText.color = (confirmIndex == 1) ? selectedColor : unselectedColor;
    }


    // --- 追加：次に選択可能なインデックスを探索する ---
    int FindNextSelectableIndex(int current, int direction)
    {
        int count = menuTexts.Length;
        if (count == 0) return 0;

        int next = current;
        for (int i = 0; i < count; i++)
        {
            next = (next + direction + count) % count;
            if (menuSelectable[next]) return next;
        }
        return (current == -1) ? 0 : current;
    }

    void UpdateMenuVisuals()
    {
        if (menuTexts == null) return;

        for (int i = 0; i < menuTexts.Length; i++)
        {
            // 追加：インスペクターで None が混ざっている場合の対策
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
    // ★追加：演習リザルト（撃破後）用のモード設定
    // ★修正：第2引数 isWin を追加
    public void SetPracticeResultMode(bool active, bool isWin)
    {
        isPracticeResultMode = active;
        isPaused = active;

        if (active)
        {
            pauseCanvas.SetActive(true);
            Time.timeScale = 0f;

            // 1. 「一時停止を解除 (index 0)」を非表示・選択不可にする
            menuSelectable[0] = false;
            if (menuTexts[0] != null) menuTexts[0].gameObject.SetActive(false);

            // 2. ★結果に応じて初期カーソルを変更
            if (isWin)
            {
                // 勝利時：タイトルに戻る (index 1) を選択
                selectedIndex = 1;
            }
            else
            {
                // 敗北時：最初からやり直す (index 4) を選択
                selectedIndex = 4;
            }

            UpdateMenuVisuals();
            SEManager.Instance.Play(SEPath.PAUSE, 0.5f);
        }
        else
        {
            // リセット処理
            menuSelectable[0] = true;
            if (menuTexts[0] != null) menuTexts[0].gameObject.SetActive(true);
        }
    }
    // 外部からモードを切り替えるメソッド
    public void SetGameOverMode(bool active)
    {
        isGameOverMode = active;
        if (active)
        {
            menuTexts[0].text = "コンティニューする"; // 文言を変更
            menuSelectable[0] = true; // ゲームオーバー時は選べるようにする
        }
        else
        {
            menuTexts[0].text = "一時停止を解除";
        }
    }
}