// --- InputManager.cs コントローラー2台完全排他分離版 ---
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance;

    [Header("Action Asset")]
    [SerializeField] private InputActionAsset _actionAsset;

    [System.Serializable]
    public struct PlayerActionSet
    {
        public InputActionReference move;
        public InputActionReference skillZ;
        public InputActionReference skillX;
        public InputActionReference skillC;
        public InputActionReference skillV;
        public InputActionReference skillEX;
        public InputActionReference skillVJT;
        public InputActionReference slow;
        public InputActionReference barrier;
        public InputActionReference pause;
    }

    [Header("Players Input Sets")]
    public PlayerActionSet player1;
    public PlayerActionSet player2;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.Log($"<color=yellow>⚠️ 古い {gameObject.name} の残存を検知したため差し替えます。</color>");
            Destroy(Instance.gameObject);
            Instance = this;
        }

        _actionAsset.Enable();
        InitializeDeviceAssignments();
    }

    /// <summary>
    /// 🎮 コントローラー2台の接続を検知し、1Pと2Pのアクションマップへ完全に1台ずつ排他割り当てする
    /// </summary>
    private void InitializeDeviceAssignments()
    {
        var allGamepads = Gamepad.all;

        InputActionMap p1Map = _actionAsset.FindActionMap("Player1");
        InputActionMap p2Map = _actionAsset.FindActionMap("Player2");

        if (allGamepads.Count >= 2)
        {
            Debug.Log($"<color=cyan>🎮【InputSystem】コントローラー2台検知。1P = Pad[0] ({allGamepads[0].name}), 2P = Pad[1] ({allGamepads[1].name}) に完全排他分離します。</color>");

            // 🌟【核心】：1Pには「0番目のパッド」だけを強制。キーボードや他のパッドの混線を物理シャットアウト！
            if (p1Map != null)
            {
                p1Map.devices = new InputDevice[] { allGamepads[0] };
            }

            // 🌟【核心】：2Pには「1番目のパッド」だけを強制。1P側のパッドの信号を完全に遮断！
            if (p2Map != null)
            {
                p2Map.devices = new InputDevice[] { allGamepads[1] };
            }
        }
        else if (allGamepads.Count == 1)
        {
            Debug.Log("<color=yellow>🎮【InputSystem】コントローラー1台検知。1P=Pad[0]、2P=キーボードに分離します。</color>");

            if (p1Map != null) p1Map.devices = new InputDevice[] { allGamepads[0] };
            if (p2Map != null) p2Map.devices = new InputDevice[] { Keyboard.current };
        }
        else
        {
            Debug.Log("<color=orange>⌨️【InputSystem】コントローラー未接続。</color>");
            if (p1Map != null) p1Map.devices = null;
            if (p2Map != null) p2Map.devices = null;
        }
    }
}