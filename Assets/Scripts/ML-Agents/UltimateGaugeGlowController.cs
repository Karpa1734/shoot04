using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 🌟 領域発動可能（200%以上）および領域返し（カウンターVJT）の成立時に、アルカナゲージを動的に発光・強調させるUIコントローラー
/// </summary>
public class UltimateGaugeGlowController : MonoBehaviour
{
    [Header("Target UI Components")]
    [Tooltip("発光・強調させたいアルカナゲージのImageコンポーネント（マスクや外枠、またはバー自体）を登録してください")]
    [SerializeField] private Image _gaugeImage;

    [Header("Glow Color Settings")]
    [Tooltip("通常時（199%以下）の標準カラー")]
    [SerializeField] private Color _normalColor = Color.white;

    [Tooltip("🌟通常VJT発動可能（200%以上）の時の発光カラー")]
    [SerializeField] private Color _vjtReadyColor = new Color(0.3f, 1f, 1f, 1f); // 妖しいシアンブルー

    [Tooltip("💥💥領域返し（カウンターVJT）が完全成立している時の激しい警告発光カラー")]
    [SerializeField] private Color _counterReadyColor = new Color(1f, 0.2f, 0.2f, 1f); // 燃え盛る憤怒レッド

    [Header("Animation Settings")]
    [Tooltip("発光時の明滅（パルス）スピード")]
    [SerializeField] private float _pulseSpeed = 8f;
    [Tooltip("パルスアニメーションの最小輝度値")]
    [SerializeField] private float _minPulseAlpha = 0.5f;

    // 内部キャッシュ参照（🌟[SerializeField]をつけてインスペクターに露出させます）
    [Header("Debug Player References")]
    [SerializeField] private PlayerMove _myPlayerMove;
    [SerializeField] private PlayerStatusManager _myStatus;
    private PlayerStatusManager _oppStatus;

    void Start()
    {
        // =========================================================================
        // 🌟【最重要修正】：手動でアタッチされている場合は自動取得で上書きしない！！
        // =========================================================================
        if (_myPlayerMove == null)
        {
            _myPlayerMove = GetComponentInParent<PlayerMove>();
        }

        if (_myStatus == null)
        {
            _myStatus = GetComponentInParent<PlayerStatusManager>();
        }

        if (_gaugeImage == null)
        {
            _gaugeImage = GetComponent<Image>();
        }
    }

    void Update() { }

    /// <summary>
    /// 🌟【完全同期版インターフェース】：UltimateGaugeUI がスライダーの値を動かした「直後」に、この関数を叩いて色を上書き決定します
    /// </summary>
    public void ManualGlowUpdate()
    {
        if (_myPlayerMove == null || _myStatus == null || _gaugeImage == null) return;

        if (_oppStatus == null && _myPlayerMove.Opponent != null)
        {
            _oppStatus = _myPlayerMove.Opponent.GetComponent<PlayerStatusManager>();
        }

        // =========================================================================
        // 🛡️ 判定フェーズ1：すでにゲームセット（KO）している、または自分がVJT中の場合は発光をパージ
        // =========================================================================
        if (_myStatus.isSpellCardActive || PlayerMove.CanShoot == false)
        {
            _gaugeImage.color = _normalColor;
            return;
        }

        // =========================================================================
        // 🔮 判定フェーズ2：領域返し（カウンターVJT）の完全成立チェック
        // =========================================================================
        bool isCounterVJTReady = false;

        if (PlayerStatusManager.isAnyVJTActive && !_myStatus.isSpellCardActive && !_myStatus.isOverheated && _myPlayerMove.ultimateEnergy >= 200f)
        {
            if (_oppStatus != null && _oppStatus.isSpellCardActive)
            {
                float myProgress = Mathf.InverseLerp(200f, 300f, _myPlayerMove.ultimateEnergy);
                float myExpectedDuration = Mathf.Lerp(_myStatus.minSpellDuration, _myStatus.maxSpellDuration, myProgress);
                float oppRemainingTime = _oppStatus.spellTimer;

                if (myExpectedDuration - oppRemainingTime > 10f)
                {
                    isCounterVJTReady = true;
                }
            }
        }

        // =========================================================================
        // 🎨 表現フェーズ3：ステートに応じたカラーマトリクス変調 ＆ サイン波パルス
        // =========================================================================
        if (isCounterVJTReady)
        {
            float pulse = Mathf.Lerp(_minPulseAlpha, 1f, Mathf.Abs(Mathf.Sin(Time.time * _pulseSpeed * 1.5f)));
            Color targetColor = _counterReadyColor;
            targetColor.a = pulse;
            _gaugeImage.color = targetColor;
        }
        else if (_myPlayerMove.ultimateEnergy >= 200f && !_myStatus.isOverheated && !PlayerStatusManager.isAnyVJTActive)
        {
            float pulse = Mathf.Lerp(_minPulseAlpha, 1f, Mathf.Abs(Mathf.Sin(Time.time * _pulseSpeed)));
            Color targetColor = _vjtReadyColor;
            targetColor.a = pulse;
            _gaugeImage.color = targetColor;
        }
        else
        {
            _gaugeImage.color = _normalColor;
        }
    }
}