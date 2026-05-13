using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance;

    [Header("Action Asset")]
    [SerializeField] private InputActionAsset _actionAsset;

    // --- ★ プレイヤーごとの入力を構造体で定義 ---
    [System.Serializable]
    public struct PlayerActionSet
    {
        public InputActionReference move;
        public InputActionReference skillZ;
        public InputActionReference skillX;
        public InputActionReference skillC;
        public InputActionReference skillV;
        public InputActionReference slow;
        public InputActionReference barrier;
    }

    [Header("Players Input Sets")]
    public PlayerActionSet player1; // 1P用 (WASD / Pad1)
    public PlayerActionSet player2; // 2P用 (Arrows / Pad2)

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        _actionAsset.Enable();
    }
}