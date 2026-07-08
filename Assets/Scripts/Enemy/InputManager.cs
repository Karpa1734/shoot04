// --- InputManager.cs 修正完全同期版（「ゲームの作り方」様・アセット分離適合版） ---
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance;

    [Header("Action Asset")]
    [SerializeField] private InputActionAsset _actionAsset;

    // --- プレイヤーごとの入力を構造体で定義 ---
    [System.Serializable]
    public struct PlayerActionSet
    {
        public InputActionReference move;
        public InputActionReference skillZ;
        public InputActionReference skillX;
        public InputActionReference skillC;
        public InputActionReference skillV;
        public InputActionReference skillEX; // 1ストック通常EX
        public InputActionReference skillVJT; // 聖少女領域（VJT）用のリファレンス枠
        public InputActionReference slow;
        public InputActionReference barrier;
        public InputActionReference pause;
    }

    [Header("Players Input Sets")]
    public PlayerActionSet player1; // 1P用 (矢印キー / Pad1)
    public PlayerActionSet player2; // 2P用 (WASDキー / Pad2)

    void Awake()
    {
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

        // 🌟 アセット全体の有効化とデバイスの初期化を実行
        _actionAsset.Enable();
        InitializeDeviceAssignments();
    }
    /// <summary>
    /// 接続されているコントローラーの数に応じて、1P（Pad1+矢印）と2P（Pad2+WASD）の入力をアセットレベルで完全に切り分ける
    /// </summary>
    private void InitializeDeviceAssignments()
    {
        // 現在PCに認識されているすべてのゲームパッド（コントローラー）を取得
        var allGamepads = Gamepad.all;

        // アセット内からPlayer1とPlayer2のアクションマップを直接検索取得
        InputActionMap p1Map = _actionAsset.FindActionMap("Player1");
        InputActionMap p2Map = _actionAsset.FindActionMap("Player2");

        if (allGamepads.Count >= 2)
        {
            Debug.Log($"<color=cyan>🎮【InputSystem】コントローラー2個検知。1P=Pad1+キーボード、2P=Pad2のみに完全排他分離。総数: {allGamepads.Count}</color>");

            // 1Pのアクションマップに「1台目のゲームパッド」と「キーボード全体」のみを処理デバイスとして登録
            if (p1Map != null)
            {
                p1Map.devices = new InputDevice[] { allGamepads[0], Keyboard.current };
            }

            // 2Pのアクションマップに「2台目のゲームパッドのみ」を登録（キーボードや1台目パッドからの干渉を物理遮断！）
            if (p2Map != null)
            {
                p2Map.devices = new InputDevice[] { allGamepads[1] };
            }
        }
        else if (allGamepads.Count == 1)
        {
            Debug.Log("<color=yellow>🎮【InputSystem】コントローラー1個検知。1P=ゲームパッド1台目+キーボード(矢印)、2P=キーボード(WASD)専用に完全隔離します。</color>");

            // 1Pが1台目のパッドを独占（キーボードの矢印でも操作可能）
            if (p1Map != null)
            {
                p1Map.devices = new InputDevice[] { allGamepads[0], Keyboard.current };
            }

            // 2Pはコントローラーの信号を1ミリ秒も受け取らないよう、「キーボード(Keyboard)デバイスのみ」に制限を強制！
            if (p2Map != null)
            {
                p2Map.devices = new InputDevice[] { Keyboard.current };
            }
        }
        else
        {
            Debug.Log("<color=orange>⌨️【InputSystem】コントローラー未接続。1P=矢印、2P=WASDのデフォルトキーボード配置で駆動します。</color>");

            // コントローラーがない場合はアセット全体でキーボード入力を共有（デフォルト状態を死守）
            if (p1Map != null) p1Map.devices = new InputDevice[] { Keyboard.current };
            if (p2Map != null) p2Map.devices = new InputDevice[] { Keyboard.current };
        }
    }
}