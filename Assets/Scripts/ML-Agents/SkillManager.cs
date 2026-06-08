// --- SkillManager.cs 【Skill_VJTインプット完全溶接版・リキャスト1.5倍型ペナルティ適合版】 ---
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
            playerMove.maxEnergy = skillData.maxEnergy;
            playerMove.currentEnergy = skillData.maxEnergy;
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

        // 1. エネルギーの自然回復処理（焼き切れデバフ・領域バフ連動型）
        if (emitter.IsAnySkillActive)
        {
            _recoveryDelayTimer = 0f;
        }
        else
        {
            // =========================================================================
            // ⏳【新機能】：術式焼き切れ中は次の回復が始まるまでのディレイ消化スピードを「半分」に遅延！
            // =========================================================================
            float delaySpeedMultiplier = 1.0f;

            if (statusManager.isSpellCardActive)
            {
                delaySpeedMultiplier = 2.0f; // 🟢 領域展開中：2倍の速さでディレイが解ける（実質0.25秒で回復開始）
            }
            else if (statusManager.isOverheated)
            {
                delaySpeedMultiplier = 0.5f; // 🚨 術式焼き切れ中：1/2の遅さでしかディレイが溜まらない（実質1.0秒待つまで回復が始まらない！）
            }

            _recoveryDelayTimer += Time.deltaTime * delaySpeedMultiplier;
        }

        // 💡 通常は0.5秒の猶予。上記の消化スピード変調により、焼き切れ中はきっちり「1.0秒」の完全な硬直に変化します
        if (_recoveryDelayTimer >= 1.0f)
        {
            // 🌟【回復速度マルチプライヤー】：通常1.0f、焼き切れ0.5f、領域バフ2.0f
            float regenMultiplier = 1.0f;
            if (statusManager.isOverheated) regenMultiplier = 0.5f;
            else if (statusManager.isSpellCardActive) regenMultiplier = 2.0f;

            playerMove.currentEnergy = Mathf.Min(
                skillData.maxEnergy,
                playerMove.currentEnergy + (skillData.energyRegenRate * regenMultiplier * Time.deltaTime)
            );
        }

        // 2. タイマー更新
        UpdateTimers();
        UpdateAllCooldownUI();

        if (!PlayerMove.CanShoot) return;
        if (hitHandler != null && hitHandler.currentState != PlayerHitHandler.PlayerState.Normal) return;

        // --- 入力ソースの同期デコード ---
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
            cPressed = input.shotC;
            vPressed = input.shotV;
            exPressed = input.ultimate;
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

                if (inputSet.skillEX != null && inputSet.skillEX.action != null)
                {
                    exPressed = inputSet.skillEX.action.IsPressed();
                }
                else
                {
                    exPressed = (cPressed && vPressed);
                }

                if (inputSet.skillVJT != null && inputSet.skillVJT.action != null)
                {
                    vjtPressed = inputSet.skillVJT.action.WasPressedThisFrame();
                }
                else
                {
                    vjtPressed = (Input.GetKeyDown(KeyCode.Z) && Input.GetKey(KeyCode.X)) || (Input.GetKeyDown(KeyCode.X) && Input.GetKey(KeyCode.Z));
                }
            }
        }

        // =========================================================================
        // 🌟 ① 【Skill_VJT 結合】：聖少女領域（VJT）発動処理の実行ジャッジ
        // =========================================================================
        if (vjtPressed && !statusManager.isSpellCardActive)
        {
            statusManager.ActivateSpellCard();
            return;
        }

        // =========================================================================
        // ② EXスキル ＆ アルティメットスキル（ULT）の排他制御
        // =========================================================================
        if (exPressed)
        {
            if (statusManager.isSpellCardActive)
            {
                if (timerEX <= 0f)
                {
                    // 🎯【ULTインフラの始動】：発動の瞬間にアルカナゲージ（アルティメットゲージ）を0%にリセットしつつ、
                    // 🚨 DeactivateSpellCard をパージすることで、領域を破棄させずに引き伸ばします！
                    playerMove.ultimateEnergy = 0f;
                    emitter.FireEX(skillData.skillEX);

                    timerEX = skillData.skillEX.cooldown > 0f ? skillData.skillEX.cooldown : EX_COOLDOWN;
                    Debug.Log("<color=magenta>👑【アルティメットスキル(ULT)】領域を維持したまま、究極必殺技をキックしました！</color>");
                }
            }
            else
            {
                if (timerEX <= 0f && playerMove.ultimateEnergy >= 100f)
                {
                    playerMove.ultimateEnergy -= 100f;
                    emitter.FireEX(skillData.skillEX);
                    timerEX = skillData.skillEX.cooldown > 0f ? skillData.skillEX.cooldown : EX_COOLDOWN;
                    Debug.Log("<color=orange>★★ 1ストック通常Exスキルを発動しました ★★</color>");
                }
            }

            return;
        }

        // =========================================================================
        // ③ 通常スキルの実行判定（🚨古いC/V封印のif文を撤廃し、全スキルを等価に解放！）
        // =========================================================================
        HandleSkillInput(zPressed, ref timerZ, skillData.skillZ);
        HandleSkillInput(xPressed, ref timerX, skillData.skillX);
        HandleSkillInput(cPressed, ref timerC, skillData.skillC);
        HandleSkillInput(vPressed, ref timerV, skillData.skillV);
    }

    /// <summary>
    /// 🌟【最新仕様溶接】：スキル発動時に術式焼き切れ状態をフックし、リキャストを1.5倍に延長する
    /// </summary>
    private void HandleSkillInput(bool isPressed, ref float timer, PlayerSkillData.SkillSettings settings)
    {
        if (isPressed && timer <= 0 && playerMove.currentEnergy >= settings.cost)
        {
            _recoveryDelayTimer = 0f;
            playerMove.currentEnergy -= settings.cost;
            emitter.Fire(settings);

            // 🚨 仕様適合：術式焼き切れ（isOverheated）中に放ったスキルは、クールダウンを「1.5倍」へ強制遅延ペナルティ加算！
            float cooldownMultiplier = statusManager.isOverheated ? 1.5f : 1.0f;
            timer = settings.cooldown * cooldownMultiplier;
        }
    }

    private void UpdateTimers()
    {
        float dt = Time.deltaTime;
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
        if (playerMove != null && skillData != null)
        {
            playerMove.currentEnergy = skillData.maxEnergy;
        }
        _recoveryDelayTimer = 0f;
        UpdateAllCooldownUI();
    }
}