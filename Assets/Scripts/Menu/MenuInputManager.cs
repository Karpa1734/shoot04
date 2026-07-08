// --- MenuInputManager.cs タイトル・キャラ選択画面専用の入力デバイス動的分配クラス（最終決定版） ---
using UnityEngine;
using UnityEngine.InputSystem;

public class MenuInputManager : MonoBehaviour
{
    public static MenuInputManager Instance;

    [Header("Action Asset")]
    [SerializeField] private InputActionAsset _actionAsset;

    [Header("Menu Actions Reference (1P)")]
    public InputActionReference navigateP1; // UIPl1/Navigate
    public InputActionReference submitP1;   // UIPl1/Submit
    public InputActionReference cancelP1;   // UIPl1/Cancel

    [Header("Menu Actions Reference (2P)")]
    public InputActionReference navigateP2; // UIPl2/Navigate
    public InputActionReference submitP2;   // UIPl2/Submit
    public InputActionReference cancelP2;   // UIPl2/Cancel

    void Awake()
    {
        // 🌟 タイトルから選択画面を跨いでも破棄されないようにシングルトン化
        if (Instance == null)
        {
            Instance = this;
            // もしこのマネージャー自体を永続化させたい場合はここで設定
            // DontDestroyOnLoad(gameObject); 
        }
        else
        {
            // 💡【核心の修正】：古いインスタンス（Instance）がすでに存在していた場合、
            // その古い方のゲームオブジェクトを物理的に破壊し、今回の新しい方を最優先で登録し直します。
            Debug.Log($"<color=yellow>⚠️ 古い {gameObject.name} の残存を検知したため、古い方を破棄して現在のシーンの新しい方に差し替えます。</color>");

            Destroy(Instance.gameObject);
            Instance = this;
        }

        // アセット全体の入力を有効化
        _actionAsset.Enable();

        // 🎯 起動時にコントローラーの接続数に合わせて、デバイスを1Pと2Pに動的分配
        InitializeMenuDeviceAssignments();
    }

    void OnEnable()
    {
        // コントローラーが途中で抜き差しされたイベントを検知して自動で再分配するセーフティを結合
        InputSystem.onDeviceChange += OnDeviceChangeDetected;
    }

    void OnDisable()
    {
        InputSystem.onDeviceChange -= OnDeviceChangeDetected;
    }

    /// <summary>
    /// 接続されているコントローラーの数に応じて、UI操作デバイスを 1P(UIPl1) と 2P(UIPl2) へリアルタイム動的排他分離する
    /// </summary>
    public void InitializeMenuDeviceAssignments()
    {
        if (_actionAsset == null) return;

        // 現在PCに認識されているすべてのゲームパッド（コントローラー）を取得
        var allGamepads = Gamepad.all;

        // アセット内のメニュー用アクションマップ「UIPl1」と「UIPl2」を検索
        InputActionMap p1MenuMap = _actionAsset.FindActionMap("UIPl1");
        InputActionMap p2MenuMap = _actionAsset.FindActionMap("UIPl2");

        if (allGamepads.Count >= 2)
        {
            Debug.Log($"<color=cyan>🎮【MenuInput】コントローラーを2個以上検知。1P=1台目パッド+キーボード、2P=2台目パッドのみに【動的排他分離】します。（接続数: {allGamepads.Count}）</color>");

            // 1P = 1台目ゲームパッド ＋ キーボード全体
            if (p1MenuMap != null)
            {
                p1MenuMap.devices = new InputDevice[] { allGamepads[0], Keyboard.current };
            }

            // 2P = 2台目ゲームパッドのみ（1P側のパッドやキーボードからの干渉信号を物理遮断！）
            if (p2MenuMap != null)
            {
                p2MenuMap.devices = new InputDevice[] { allGamepads[1] };
            }
        }
        else if (allGamepads.Count == 1)
        {
            Debug.Log("<color=yellow>🎮【MenuInput】コントローラーを1個検知。1P=1台目パッド+キーボード、2P=キーボード専用に【動的排他分離】します。</color>");

            // 1P = 1台目ゲームパッド ＋ キーボード全体（矢印キーで動く）
            if (p1MenuMap != null)
            {
                p1MenuMap.devices = new InputDevice[] { allGamepads[0], Keyboard.current };
            }

            // 2P = キーボードのみ（アセット側のW,A,S,D設定のみに反応させる）
            if (p2MenuMap != null)
            {
                p2MenuMap.devices = new InputDevice[] { Keyboard.current };
            }
        }
        else
        {
            Debug.Log("<color=orange>⌨️【MenuInput】コントローラー未接続。1P(矢印キー)・2P(WASDキー)ともにキーボード信号から抽出します。</color>");

            // コントローラーがない場合はキーボード全体を両マップに共有し、アセット側のキー縄張り（矢印とWASD）に制御を委ねます
            if (p1MenuMap != null) p1MenuMap.devices = new InputDevice[] { Keyboard.current };
            if (p2MenuMap != null) p2MenuMap.devices = new InputDevice[] { Keyboard.current };
        }
    }

    /// <summary>
    /// ゲーム中にデバイスの抜き差しが発生した際に走るUnityイベントコールバック
    /// </summary>
    private void OnDeviceChangeDetected(InputDevice device, InputDeviceChange change)
    {
        if (change == InputDeviceChange.Added || change == InputDeviceChange.Removed)
        {
            Debug.Log($"<color=lime>🔄【MenuInput】デバイスの変化を検知（{change}）。操作権を再割り当てします。</color>");
            InitializeMenuDeviceAssignments();
        }
    }

    public void RefreshMenuDevices()
    {
        InitializeMenuDeviceAssignments();
    }
}