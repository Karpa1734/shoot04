// --- SkillManager.cs 【Skill_VJTインプット完全溶接版】 ---
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

        // 1. エネルギーの自然回復処理（焼き切れデバフ連動）
        if (emitter.IsAnySkillActive)
        {
            _recoveryDelayTimer = 0f;
        }
        else
        {
            _recoveryDelayTimer += Time.deltaTime;
        }

        if (_recoveryDelayTimer >= 0.5f)
        {
            float regenMultiplier = statusManager.isOverheated ? 0.5f : 1.0f;

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
        bool vjtPressed = false; // 🌟 追加：VJT発動ボタンフラグ

        DanmakuAgent agent = GetComponentInParent<DanmakuAgent>();

        if (agent != null && (agent._useAutoEvadeAI || playerMove.currentMode == PlayerMove.ReplayMode.Playing))
        {
            var input = playerMove.currentFrameInput;
            zPressed = input.shotZ;
            xPressed = input.shotX;
            cPressed = input.shotC;
            vPressed = input.shotV;
            exPressed = input.ultimate;
            // ※必要に応じて ReplayFrame 構造体に vjt 項目の追加拡張が可能ですが、
            // 現在は手動・AI双方から安全にインターセプトできるよう、以下で共通ボタン検知を走らせます
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

                // 🌟【新設】InputManager に追加した Skill_VJT の入力をフレーム検知
                if (inputSet.skillVJT != null && inputSet.skillVJT.action != null)
                {
                    vjtPressed = inputSet.skillVJT.action.WasPressedThisFrame();
                }
                else
                {
                    // フォールバック（念のためのZ+Xキーボード直押し同時入力判定）
                    vjtPressed = (Input.GetKeyDown(KeyCode.Z) && Input.GetKey(KeyCode.X)) || (Input.GetKeyDown(KeyCode.X) && Input.GetKey(KeyCode.Z));
                }
            }
        }

        // =========================================================================
        // 🌟 ① 【Skill_VJT 結合】：聖少女領域（VJT）発動処理の実行ジャッジ
        // =========================================================================
        if (vjtPressed && !statusManager.isSpellCardActive)
        {
            // PlayerStatusManager 内部の「アルカナゲージ200%以上チェック」および
            // 「相手が発動していないかどうかの排他処理（早い者勝ちルール）」を実行して領域を展開！
            statusManager.ActivateSpellCard();

            // 発動フレームは他の通常スキル入力をカットして即座にリターン
            return;
        }

        // =========================================================================
        // ② EXスキル ＆ アルティメットスキル（ULT）の排他制御
        // =========================================================================
        if (exPressed)
        {
            if (statusManager.isSpellCardActive)
            {
                // 🚨【領域展開中】➔ 完全無敵のアルティメットスキル（ULT）発動！
                if (timerEX <= 0f)
                {
                    statusManager.SetInvincible(2.0f);
                    emitter.FireEX(skillData.skillEX);

                    playerMove.ultimateEnergy = 0f;
                    statusManager.DeactivateSpellCard(false);

                    timerEX = skillData.skillEX.cooldown > 0f ? skillData.skillEX.cooldown : EX_COOLDOWN;
                    Debug.Log("<color=magenta>👑【アルティメットスキル(ULT)】領域を強制解除し、必殺技を放ちました！</color>");
                }
            }
            else
            {
                // 通常時の通常Exボム（1ストック消費）
                if (timerEX <= 0f && playerMove.ultimateEnergy >= 100f)
                {
                    // アルカナゲージを1ストック(100f)消費して
                    playerMove.ultimateEnergy -= 100f;

                    // 🌟【修正】：statusManager.UseSpell(); の行を完全に削除しました

                    // 弾幕エミッターから技を射出
                    emitter.FireEX(skillData.skillEX);

                    timerEX = skillData.skillEX.cooldown > 0f ? skillData.skillEX.cooldown : EX_COOLDOWN;
                    Debug.Log("<color=orange>★★ 1ストック通常Exスキルを発動しました ★★</color>");
                }
            }

            return;
        }

        // =========================================================================
        // ③ 通常スキルの実行判定
        // =========================================================================
        HandleSkillInput(zPressed, ref timerZ, skillData.skillZ);
        HandleSkillInput(xPressed, ref timerX, skillData.skillX);

        // 術式焼き切れ（isOverheated）中のC/V封印処理
        if (statusManager.isOverheated)
        {
            cPressed = false;
            vPressed = false;
        }

        HandleSkillInput(cPressed, ref timerC, skillData.skillC);
        HandleSkillInput(vPressed, ref timerV, skillData.skillV);
    }

    private void HandleSkillInput(bool isPressed, ref float timer, PlayerSkillData.SkillSettings settings)
    {
        if (isPressed && timer <= 0 && playerMove.currentEnergy >= settings.cost)
        {
            _recoveryDelayTimer = 0f;
            playerMove.currentEnergy -= settings.cost;
            emitter.Fire(settings);
            timer = settings.cooldown;
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
        if (uiZ != null) uiZ.UpdateCooldown(timerZ, skillData.skillZ.cooldown);
        if (uiX != null) uiX.UpdateCooldown(timerX, skillData.skillX.cooldown);
        if (uiC != null) uiC.UpdateCooldown(timerC, skillData.skillC.cooldown);
        if (uiV != null) uiV.UpdateCooldown(timerV, skillData.skillV.cooldown);
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