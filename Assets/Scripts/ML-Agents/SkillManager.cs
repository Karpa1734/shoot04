// --- SkillManager.cs 【引数バグ修正・領域中コストフリー大連射完全適合版】 ---
using KanKikuchi.AudioManager;
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

        if (playerMove != null && skillData != null) 
        {
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
    }

    void Update()
    {
        if (playerMove == null || skillData == null || statusManager == null) return; 

        // =========================================================================
        // 🛑【核心新設：最上流・世界時間停止監査セキュリティ】
        // =========================================================================
        // 💡 ポーズ画面やULTの演出停止等、Time.timeScale が 0 の時は、
        //    入力スキャンやマナの自動回復を含むすべてのスキル判定を完全にフリーズ（遮断）します。
        if (Mathf.Approximately(Time.timeScale, 0f))
        {
            // 安全弁：万が一チャージ中に時間が止まった場合、チャージ状態を安全にリセットクリア
            if (_isZCharging)
            {
                _isZCharging = false;
            }
            return;
        }

        // =========================================================================
        // 🎯【目覚めているEmitterの動的リアルタイムキャッチインフラ】
        // =========================================================================
        PlayerDanmakuEmitter activeEmitter = null; 
        PlayerDanmakuEmitter[] allEmitters = GetComponents<PlayerDanmakuEmitter>(); 
        foreach (var em in allEmitters) 
        {
            if (em != null && em.enabled) 
            {
                activeEmitter = em; 
                break; 
            }
        }

        if (activeEmitter == null) activeEmitter = emitter; 

        // =========================================================================
        // 🎯【マナ自動回復ディレイ制御セクター】
        // =========================================================================
        const float BASE_WAIT_SECONDS = 0.5f; 

        if (activeEmitter != null && activeEmitter.IsAnySkillActive) 
        {
            float passiveDelayRate = 1.0f; 
            if (statusManager != null && statusManager.HasPassiveSkill(PassiveSkillType.GreedReduction)) 
            {
                passiveDelayRate = 0.7f; // ⚡ 30%短縮
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

        if (_recoveryCooldownTimer <= 0f) 
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

        if (!PlayerMove.CanShoot) return; 
        if (hitHandler != null && hitHandler.currentState != PlayerHitHandler.PlayerState.Normal) return; 

        bool zPressed = false; 
        bool xPressed = false; 
        bool cPressed = false; 
        bool vPressed = false; 
        bool exPressed = false; 
        bool vjtPressed = false; 

        DanmakuAgent agent = GetComponentInParent<DanmakuAgent>(); 

        // 🤖 A. 敵AIまたはリプレイ再生時の入力抽出
        if (agent != null && (agent._useAutoEvadeAI || playerMove.currentMode == PlayerMove.ReplayMode.Playing)) 
        {
            var input = playerMove.currentFrameInput; 
            zPressed = input.shotZ; 
            xPressed = input.shotX; 
            cPressed = input.shotV ? false : input.shotC; 
            vPressed = input.shotV; 
            exPressed = input.ultimate; 

            vjtPressed = (agent._useAutoEvadeAI && playerMove.currentFrameInput.ultimate && 
                          playerMove.ultimateEnergy >= 200f && !statusManager.isSpellCardActive); 
        }
        // ⌨️ 🎮 B. 人間操作の入力スキャン
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

        // 🔮 1. 領域展開（VJT）の執行
        if (vjtPressed)
        {
            Debug.Log($"<color=orange>🔮 [VJT INPUT SUCCESS] Player {playerMove.playerId} の領域入力（Z+X / パッド）が完全成立！ (現在のアルカナゲージ: {playerMove.ultimateEnergy:F1}%)</color>"); 
            statusManager.ActivateSpellCard();
            return; 
        }

        // 👑 2. 必殺技（EX/ULT）の執行
        if (exPressed)
        {
            if (statusManager.isSpellCardActive) 
            {
                if (timerEX <= 0f) 
                {
                    playerMove.ultimateEnergy = 0f; 
                    emitter.FireEX(skillData.skillEX); 
                    timerEX = skillData.skillEX.cooldown > 0f ? skillData.skillEX.cooldown : EX_COOLDOWN; 
                    Debug.Log("<color=magenta>👑 [VJT-ULT] 領域維持必殺技を射出しました！</color>"); 
                }
            }
            else
            {
                if (timerEX <= 0f && playerMove.ultimateEnergy >= 100f) 
                {
                    playerMove.ultimateEnergy -= 100f; 
                    emitter.FireEX(skillData.skillEX); 
                    timerEX = skillData.skillEX.cooldown > 0f ? skillData.skillEX.cooldown : EX_COOLDOWN; 
                    Debug.Log("<color=lime>★★ 1ストック通常Exスキルを発動しました ★★</color>"); 
                }
                else
                {
                    Debug.LogWarning($"⚠️ [EX BLOCK] 必殺条件を満たしていません。 (リキャスト残り: {timerEX:F1}秒 / アルカナ: {playerMove.ultimateEnergy:F1}%)"); 
                }
            }
            return; 
        }
        if (activeEmitter is Emitter_Lust lustEmitter && lustEmitter.IsShieldActive)
        {
            // シールドが展開されている間は、押しっぱなし（zPressed）を強制的に踏み倒して無効化！
            zPressed = false;
        }
        if (activeEmitter != null && activeEmitter.IsUltimateSkillActive) 
        {
            return; 
        }

        bool isVjtActive = (statusManager != null && statusManager.isSpellCardActive); 
        HandleSkillInput(zPressed, ref timerZ, skillData.skillZ, isVjtActive); 
        HandleSkillInput(xPressed, ref timerX, skillData.skillX, isVjtActive); 
        HandleSkillInput(cPressed, ref timerC, skillData.skillC, isVjtActive); 
        HandleSkillInput(vPressed, ref timerV, skillData.skillV, isVjtActive); 
    }

    private void HandleSkillInput(bool isPressed, ref float timer, PlayerSkillData.SkillSettings settings, bool isVjtActive)
    {
        bool isCostAllowed = (playerMove.currentEnergy >= settings.cost); 

        if (settings.isChargeSkill) 
        {
            if (isPressed && timer <= 0 && isCostAllowed && !_isZCharging) 
            {
                _isZCharging = true; 
                _recoveryDelayTimer = 0f; 
                emitter.Fire(settings); 
            }

            if (!isPressed && _isZCharging) 
            {
                _isZCharging = false; 
                playerMove.currentEnergy -= settings.cost; 

                float cooldownMultiplier = statusManager.isOverheated ? 1.5f : 1.0f; 
                timer = settings.cooldown * cooldownMultiplier; 
            }
        }
        else
        {
            if (isPressed && timer <= 0 && isCostAllowed) 
            {
                _recoveryDelayTimer = 0f; 
                playerMove.currentEnergy -= settings.cost; 
                emitter.Fire(settings); 

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