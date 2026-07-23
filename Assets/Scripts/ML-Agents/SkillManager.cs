// --- SkillManager.cs 【チャージ解放時コスト消費＆アルカナ充填＆シールド即時パージ完全適合版】 ---
using KanKikuchi.AudioManager;
using TMPro;
using UnityEngine;

public class SkillManager : MonoBehaviour
{
    PlayerSkillData skillData;

    private PlayerMove playerMove;
    private PlayerHitHandler hitHandler;
    private PlayerDanmakuEmitter emitter;
    private PlayerStatusManager statusManager;

    [Header("UI Slots (Normal Skills Only)")]
    public SkillCooldownUI uiZ;
    public SkillCooldownUI uiX;
    public SkillCooldownUI uiC;
    public SkillCooldownUI uiV;

    private int burstCountZ, burstCountX, burstCountC, burstCountV;
    private float burstResetTimerZ, burstResetTimerX, burstResetTimerC, burstResetTimerV;
    private float _recoveryDelayTimer = 0f;
    private const float BURST_RESET_DELAY = 0.5f;
    public float timerZ, timerX, timerC, timerV, timerEX;

    private const float EX_COOLDOWN = 2.5f;

    [Header("Energy UI")]
    public EnergyGaugeUI energyGauge;
    [Header("Ultimate UI")]
    public UltimateGaugeUI ultimateGaugeUI;

    private float _cPressedTimestamp = -100f;
    private float _vPressedTimestamp = -100f;
    private const float INTERACTION_WINDOW = 0.08f;

    private int _cHoldFrame = 0;
    private int _vHoldFrame = 0;
    private const int HOLD_REMAINS_FRAMES = 5;

    private bool _isExExecutedInThisWindow = false;
    private PlayerMove.ReplayFrame _lastInput;
    private float _recoveryCooldownTimer = 0f;
    private float CostRegenMultiplier = 0;

    // 🧬【汎用チャージマネジメントスロット】：各スロットが現在溜め状態にあるかを追跡
    private bool _isZCharging = false;
    // =========================================================================
    // 🌟【新規追加】：コスト生数値および自然回復待機タイマー用のUIアタッチ枠
    // =========================================================================
    [Header("🔧 Cost Debug UI Slots")]
    [Tooltip("コストの現在値と最大値を表示するTMPテキストを登録してください")]
    public TextMeshProUGUI energyNumericText;
    [Tooltip("マナ自然回復が再開するまでの待機硬直タイマーを表示するTMPテキストを登録してください")]
    public TextMeshProUGUI recoveryDelayNumericText;
    void Start()
    {
        playerMove = GetComponentInParent<PlayerMove>();
        hitHandler = GetComponentInParent<PlayerHitHandler>();
        emitter = GetComponentInParent<PlayerDanmakuEmitter>();
        statusManager = GetComponentInParent<PlayerStatusManager>();

        if (ultimateGaugeUI != null && playerMove != null)
        {
            ultimateGaugeUI.Initialize(playerMove);
        }

        if (statusManager != null)
        {
            skillData = statusManager.characterData;
        }

        // =========================================================================
        // 🎯【最核心修正：Start実行順序調停マトリクス】
        // 💡 理由：PlayerStatusManagerでの最大マナ計算（ApplyCharacterRanks）が終わった直後に、
        //          SkillManager側からも強制的にマナの初期状態をMAXへ叩き込みます。
        //          これにより、カウントダウン中からテキストが最小値で取り残されるバグを100%根絶します。
        // =========================================================================
        if (playerMove != null && skillData != null)
        {
            // ここで最速で最大マナをインフラ充填
            playerMove.currentEnergy = playerMove.maxEnergy;

            if (energyGauge != null) energyGauge.Initialize(playerMove);
        }

        if (skillData != null)
        {
            if (uiZ != null) uiZ.SetSkillIcon(skillData.skillZ.skillIcon);
            if (uiX != null) uiX.SetSkillIcon(skillData.skillX.skillIcon);
            if (uiC != null) uiC.SetSkillIcon(skillData.skillC.skillIcon);
            if (uiV != null) uiV.SetSkillIcon(skillData.skillV.skillIcon);
        }

        ResetAllTimers();
        UpdateAllCooldownUI();

        // ⭕ 順序調整：マナが確実にMAX(100)で固定されたこのタイミングでUIテキストを先行起動！
        UpdateCostNumericText();
    }

    void Update()
    {
        if (playerMove == null || skillData == null || statusManager == null) return;
        if (!PlayerMove.CanShoot)
        {
            playerMove.currentEnergy = playerMove.maxEnergy;
        }
        if (Mathf.Approximately(Time.timeScale, 0f))
        {
            if (_isZCharging)
            {
                _isZCharging = false;
            }
            return;
        }

        PlayerDanmakuEmitter activeEmitter = null;
        PlayerDanmakuEmitter[] allEmitters = GetComponents<PlayerDanmakuEmitter>();
        if (allEmitters == null || allEmitters.Length == 0) allEmitters = GetComponentsInChildren<PlayerDanmakuEmitter>(true);

        foreach (var em in allEmitters)
        {
            if (em != null && em.enabled)
            {
                activeEmitter = em;
                break;
            }
        }

        if (activeEmitter == null) activeEmitter = emitter;

        const float BASE_WAIT_SECONDS = 0.5f;

        if (activeEmitter != null && activeEmitter.IsAnySkillActive)
        {
            float passiveDelayRate = 1.0f;
            if (statusManager != null && statusManager.HasPassiveSkill(PassiveSkillType.GreedReduction))
            {
                passiveDelayRate = 0.7f;
            }

            if (statusManager.isSpellCardActive)
            {
                _recoveryCooldownTimer = (BASE_WAIT_SECONDS * 0.5f) * passiveDelayRate;
            }
            else if (statusManager.isOverheated)
            {
                _recoveryCooldownTimer = (BASE_WAIT_SECONDS * 2.0f) * passiveDelayRate;
            }
            else
            {
                _recoveryCooldownTimer = BASE_WAIT_SECONDS * passiveDelayRate;
            }
        }
        else
        {
            if (_recoveryCooldownTimer > 0f)
            {
                _recoveryCooldownTimer -= Time.deltaTime;
            }
        }
        if (_recoveryCooldownTimer <= 0f && PlayerMove.CanShoot)
        {
            float regenMultiplier = 1.0f;
            if (statusManager.isOverheated) regenMultiplier = 0.5f;
            else if (statusManager.isSpellCardActive)
            {
                regenMultiplier = 2.0f;

                if (statusManager.characterData != null && statusManager.characterData.vjtEffectType == VJTEffectType.GreedCast)
                {
                    regenMultiplier *= 1.5f;
                }
            }

            if (playerMove != null && playerMove.Opponent != null)
            {
                PlayerStatusManager oppStatus = playerMove.Opponent.GetComponent<PlayerStatusManager>();
                if (oppStatus != null && oppStatus.isSpellCardActive && oppStatus.characterData != null)
                {
                    if (oppStatus.characterData.vjtEffectType == VJTEffectType.GreedCast)
                    {
                        regenMultiplier *= 0.5f;
                    }
                }
            }

            if (statusManager != null && statusManager.IsSlothRegenBlocked())
            {
                regenMultiplier = 0f;
            }

            if (statusManager != null && statusManager.IsSlothBoostActive())
            {
                regenMultiplier *= 1.5f;
            }

            playerMove.currentEnergy = Mathf.Min(
                playerMove.maxEnergy,
                playerMove.currentEnergy + (playerMove.energyRegenRate * regenMultiplier * Time.deltaTime)
            );
        }

        UpdateTimers();
        UpdateAllCooldownUI();

        // 💡【対策の核心】：画面外でのフリーズを防ぐため、ここで一度数値を最新状態に更新しておく
        UpdateCostNumericText();

        if (!PlayerMove.CanShoot) return;
        if (hitHandler != null && hitHandler.currentState != PlayerHitHandler.PlayerState.Normal) return;

        // 📊【安全圏】：ここから下で初めて各入力を評価するため、カウントダウン中の暴発リスクが完全に0になります。
        bool zPressed = false;
        bool xPressed = false;
        bool cPressed = false;
        bool vPressed = false;
        bool exPressed = false;
        bool vjtPressed = false;

        DanmakuAgent agent = GetComponentInParent<DanmakuAgent>();

        if (agent != null && (agent._useAutoEvadeAI || playerMove.currentMode == PlayerMove.ReplayMode.Playing))
        {
            var input = playerMove.currentFrameInput;
            zPressed = input.shotZ;
            xPressed = input.shotX;
            cPressed = input.shotV ? false : input.shotC;
            vPressed = input.shotV;
            exPressed = input.ultimate;

            // 🎯 領域展開の自動判定（CanShootの二重チェックで絶対安全化）
            vjtPressed = (agent._useAutoEvadeAI && input.ultimate &&
                          playerMove.ultimateEnergy >= 200f && !statusManager.isSpellCardActive);
        }
        else
        {
            if (InputManager.Instance != null)
            {
                var inputSet = (playerMove.playerId == 1) ? InputManager.Instance.player1 : InputManager.Instance.player2;

                zPressed = inputSet.skillZ.action.IsPressed();
                xPressed = inputSet.skillX.action.IsPressed();
                cPressed = inputSet.skillC.action.IsPressed();
                vPressed = inputSet.skillV.action.IsPressed();

                bool isZX_Combination = (zPressed && xPressed);
                if (isZX_Combination && (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.X)))
                {
                    vjtPressed = true;
                }

                if (!vjtPressed && inputSet.skillVJT != null && inputSet.skillVJT.action != null)
                {
                    if (inputSet.skillVJT.action.WasPressedThisFrame())
                    {
                        vjtPressed = true;
                    }
                }

                if (inputSet.skillEX != null && inputSet.skillEX.action != null)
                {
                    exPressed = inputSet.skillEX.action.WasPressedThisFrame();
                }
                else
                {
                    if (cPressed && vPressed && (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.V)))
                    {
                        exPressed = true;
                    }
                }
            }
        }


        // 🔮 領域展開の発動執行（※ ストーリーモード中は自機の領域展開を禁止）
        if (vjtPressed && !GameModeManager.IsStoryMode)
        {
            Debug.Log($"<color=orange>🔮 [VJT INPUT SUCCESS] Player {playerMove.playerId} の領域入力が完全成立！</color>");
            statusManager.ActivateSpellCard();
            return;
        }

        // 👑 超必殺技の発動執行
        if (exPressed)
        {
            if (statusManager.isSpellCardActive)
            {
                if (timerEX <= 0f)
                {
                    playerMove.ultimateEnergy = 0f;
                    activeEmitter.FireEX(skillData.skillEX);
                    timerEX = skillData.skillEX.cooldown > 0f ? skillData.skillEX.cooldown : EX_COOLDOWN;
                }
            }
            else
            {
                if (timerEX <= 0f && playerMove.ultimateEnergy >= 100f)
                {
                    playerMove.ultimateEnergy -= 100f;
                    activeEmitter.FireEX(skillData.skillEX);
                    timerEX = skillData.skillEX.cooldown > 0f ? skillData.skillEX.cooldown : EX_COOLDOWN;
                }
            }
            return;
        }

        if (activeEmitter != null && activeEmitter.IsUltimateSkillActive)
        {
            return;
        }

        bool isMyVjtActive = (statusManager != null && statusManager.isSpellCardActive);
        HandleSkillInput(zPressed, ref timerZ, skillData.skillZ, isMyVjtActive, activeEmitter);
        HandleSkillInput(xPressed, ref timerX, skillData.skillX, isMyVjtActive, activeEmitter);
        HandleSkillInput(cPressed, ref timerC, skillData.skillC, isMyVjtActive, activeEmitter);
        HandleSkillInput(vPressed, ref timerV, skillData.skillV, isMyVjtActive, activeEmitter);

        UpdateCostNumericText();
    }
    // =========================================================================
    // 🌟【新設マトリクス】：コスト数値の整数化 ✕ Regen時完全非表示 ✕ 初期同期インフラ
    // =========================================================================
    private void UpdateCostNumericText()
    {
        if (playerMove == null) return;

        if (energyNumericText != null)
        {
            // 💡 修正：:F1（小数点表示）を廃止し、(int)へキャストして「完全な整数」として描写
            int currentEnergyInt = (int)playerMove.currentEnergy;
            int maxEnergyInt = (int)playerMove.maxEnergy;
            energyNumericText.text = $"{currentEnergyInt} / {maxEnergyInt}";
        }

        if (recoveryDelayNumericText != null)
        {
            // 💡 自然回復がロックされている（タイマー稼働中）間だけ秒数を表示
            if (_recoveryCooldownTimer > 0f)
            {
                recoveryDelayNumericText.text = $"{_recoveryCooldownTimer:F1}s";
                recoveryDelayNumericText.color = new Color(1f, 1f, 1f); // 白色
            }
            else
            {
                // 💡 ご指定：Regen状態（タイマーが0以下）の時は、文字を非表示（空文字）にする
                recoveryDelayNumericText.text = "";
            }
        }
    }

    private void HandleSkillInput(bool isPressed, ref float timer, PlayerSkillData.SkillSettings settings, bool isVjtActive, PlayerDanmakuEmitter activeEmitter)
    {
        bool isCostAllowed = (playerMove.currentEnergy >= settings.cost);

        if (settings.isChargeSkill)
        {
            // 💡 A. チャージ開始（最初の押し込みフレーム）
            if (isPressed && timer <= 0 && isCostAllowed && !_isZCharging)
            {
                _isZCharging = true;
                _recoveryDelayTimer = 0f;
                activeEmitter.Fire(settings); // ➔ 槍のチャージ（インジケーター収束）演出のみを開始
            }

            // 💡 B. チャージ解放（ボタンを離して、実際に槍が戦場へ放たれたジャストの瞬間！）
            // 💡 AIの入力終了フラグ、または「溜めタイマー満了によるAI側の強制リリース」のどちらからでも確実にフックします
            if (!isPressed && _isZCharging)
            {
                _isZCharging = false;

                // 🎯 1. 【コスト消費】：ボタンを離したこの瞬間にマナコストを消費（先払いを防止）
                playerMove.currentEnergy -= settings.cost;

                // 🎯 2. 【アルカナゲージ加算】：チャージ開始時をブロックし、この発射時の「1回だけ」に集約加算！
                if (playerMove != null && statusManager != null)
                {
                    float finalGain = settings.ultimateGain; // アセット設定値（例: 15f など）
                    if (statusManager.isOverheated)
                    {
                        finalGain *= 0.5f; // 術式焼き切れ時は獲得量半減
                    }
                    playerMove.AddUltimateEnergy(finalGain);
                }

                // 🎯 3. 【シールド消滅】：槍が出たこの瞬間に、展開中のシールドを安全・確実に直撃パージ！
                if (activeEmitter is Emitter_Lust lustEmitter && lustEmitter.IsShieldActive)
                {
                    lustEmitter.PurgeActiveShield();
                }

                float cooldownMultiplier = statusManager.isOverheated ? 1.5f : 1.0f;
                timer = settings.cooldown * cooldownMultiplier;
            }
        }
        else
        {
            // 通常の単発スキル（従来通りの一瞬でコスト消費する処理）
            if (isPressed && timer <= 0 && isCostAllowed)
            {
                _recoveryDelayTimer = 0f;
                playerMove.currentEnergy -= settings.cost;
                activeEmitter.Fire(settings);

                float cooldownMultiplier = statusManager.isOverheated ? 1.5f : 1.0f;
                timer = settings.cooldown * cooldownMultiplier;
            }
        }
    }
    private void UpdateTimers()
    {
        float dtMultiplier = 1.0f;
        if (statusManager != null && statusManager.IsSlothBoostActive())
        {
            dtMultiplier = 1.3f;
        }

        float dt = Time.deltaTime * dtMultiplier;

        if (timerZ > 0) timerZ -= dt;
        if (timerX > 0) timerX -= dt;
        if (timerC > 0) timerC -= dt;
        if (timerV > 0) timerV -= dt;
        if (timerEX > 0) timerEX -= dt;
    }

    private void ResetAllTimers()
    {
        timerZ = timerX = timerC = timerV = timerEX = 0;
        burstCountZ = burstCountX = burstCountC = burstCountV = 0;
        burstResetTimerZ = burstResetTimerX = burstResetTimerC = burstResetTimerV = 0;
    }

    private void UpdateAllCooldownUI()
    {
        if (skillData == null) return;
        if (uiZ != null) uiZ.UpdateCooldown(timerZ, skillData.skillZ.cooldown * (statusManager.isOverheated ? 1.5f : 1.0f));
        if (uiX != null) uiX.UpdateCooldown(timerX, skillData.skillX.cooldown * (statusManager.isOverheated ? 1.5f : 1.0f));
        if (uiC != null) uiC.UpdateCooldown(timerC, skillData.skillC.cooldown * (statusManager.isOverheated ? 1.5f : 1.0f));
        if (uiV != null) uiV.UpdateCooldown(timerV, skillData.skillV.cooldown * (statusManager.isOverheated ? 1.5f : 1.0f));
    }

    public void InstantFullRecovery()
    {
        ResetAllTimers();
        if (playerMove != null)
        {
            playerMove.currentEnergy = playerMove.maxEnergy;
        }
        _recoveryDelayTimer = 0f;
        UpdateAllCooldownUI();
    }
}