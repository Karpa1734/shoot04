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
    // 🌟【新設】：スキルを撃ち終わってからマナの自動回復が再開されるまでの「待ち時間タイマー」
    private float _recoveryCooldownTimer = 0f;
    private float CostRegenMultiplier = 0;

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
        // 🎯【修正】：ScriptableObjectの古い手動入力枠(skillData.maxEnergy)を完全パージ！
        // =========================================================================
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
        // 🎯【リファクタリング】：直感的な「秒数指定型」マナ自動回復ディレイ制御
        // =========================================================================
        const float BASE_WAIT_SECONDS = 0.5f;

        if (emitter.IsAnySkillActive)
        {
            float passiveDelayRate = 1.0f;
            if (statusManager != null && statusManager.HasPassiveSkill(PassiveSkillType.GreedReduction))
            {
                passiveDelayRate = 0.7f; // ⚡ 30%短縮
            }

            if (statusManager.isSpellCardActive)
            {
                _recoveryCooldownTimer = (BASE_WAIT_SECONDS * 0.5f) * passiveDelayRate; // ⚡ 領域展開中 + パッシブ
            }
            else if (statusManager.isOverheated)
            {
                _recoveryCooldownTimer = (BASE_WAIT_SECONDS * 2.0f) * passiveDelayRate; // 🚨 焼き切れ中 + パッシブ
            }
            else
            {
                _recoveryCooldownTimer = BASE_WAIT_SECONDS * passiveDelayRate; // 🟢 平常時 + パッシブ
            }
        }
        else
        {
            if (_recoveryCooldownTimer > 0f)
            {
                _recoveryCooldownTimer -= Time.deltaTime;
            }
        }

        // 回復の執行
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

            if (regenMultiplier > 0f)
            {
                playerMove.currentEnergy = Mathf.Min(
                    playerMove.maxEnergy,
                    playerMove.currentEnergy + (playerMove.energyRegenRate * regenMultiplier * Time.deltaTime)
                );
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

        // --- 入力変数フラグ ---
        bool zPressed = false;
        bool xPressed = false;
        bool cPressed = false;
        bool vPressed = false;
        bool exPressed = false;
        bool vjtPressed = false;

        DanmakuAgent agent = GetComponentInParent<DanmakuAgent>();

        // =========================================================================
        // 🤖 A. 敵AIまたはリプレイ再生時の入力抽出
        // =========================================================================
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
        // =========================================================================
        // ⌨️ 🎮 B. 人間操作の入力スキャン（インスペクターのアクションマップへ完全適合）
        // =========================================================================
        else
        {
            if (InputManager.Instance != null)
            {
                var inputSet = (playerMove.playerId == 1) ? InputManager.Instance.player1 : InputManager.Instance.player2;

                // 通常スキルのホールド（押しっぱなし）状態を正確に取得
                zPressed = inputSet.skillZ.action.IsPressed();
                xPressed = inputSet.skillX.action.IsPressed();
                cPressed = inputSet.skillC.action.IsPressed();
                vPressed = inputSet.skillV.action.IsPressed();

                // 💡【バグの根治】: InputSystemの同時押し（Z+X）に WasPressedThisFrame を使うと、
                // 人間の指のわずかなズレで検知漏れします。そのため、ホールドの重複（zPressed && xPressed）を基準にし、
                // 「どちらかのキーが新しく叩かれた瞬間」をトリガーにしてズレを完全に吸収します。
                bool isZX_Combination = (zPressed && xPressed);
                if (isZX_Combination && (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.X)))
                {
                    vjtPressed = true;
                }

                // 💡 パッド用の独立したVJT専用ボタン（RB等）が登録されている場合は、そちらの単発押しも救済検知
                if (!vjtPressed && inputSet.skillVJT != null && inputSet.skillVJT.action != null)
                {
                    if (inputSet.skillVJT.action.WasPressedThisFrame())
                    {
                        vjtPressed = true;
                    }
                }

                // --- 👑 EXスキル（C+V / LB）の判定スキャン ---
                if (inputSet.skillEX != null && inputSet.skillEX.action != null)
                {
                    exPressed = inputSet.skillEX.action.WasPressedThisFrame();
                }
                else
                {
                    // アクションマップが空、またはキーボード同時押し（C+V）のズレ吸収フォールバック
                    if (cPressed && vPressed && (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.V)))
                    {
                        exPressed = true;
                    }
                }
            }
        }

        // =========================================================================
        // 🚀 各種アクションの執行判定ルーチン（完全排他・すり替えなし）
        // =========================================================================
        // 🔮 1. 領域展開（VJT）の執行
        if (vjtPressed)
        {
            // 🔍【VJT入力監査ログ】
            Debug.Log($"<color=orange>🔮 [VJT INPUT SUCCESS] Player {playerMove.playerId} の領域入力（Z+X / パッド）が完全成立！ (現在のアルカナゲージ: {playerMove.ultimateEnergy:F1}%)</color>");
            statusManager.ActivateSpellCard();
            return; // 発動フレームは後続の通常EX処理を完全に遮断
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

        // 通常スキル射出の同期結合 (既存)
        bool isVjtActive = (statusManager != null && statusManager.isSpellCardActive);
        HandleSkillInput(zPressed, ref timerZ, skillData.skillZ, isVjtActive);
        HandleSkillInput(xPressed, ref timerX, skillData.skillX, isVjtActive);
        HandleSkillInput(cPressed, ref timerC, skillData.skillC, isVjtActive);
        HandleSkillInput(vPressed, ref timerV, skillData.skillV, isVjtActive);
    }

    /// <summary>
    /// 各通常スキルの入力・リキャスト・コストを統合評価して射出する中枢サブルーチン
    /// </summary>
    /// <summary>
    /// 各通常スキルの入力・リキャスト・コストを統合評価して射出する中枢サブルーチン
    /// </summary>
    private void HandleSkillInput(bool isPressed, ref float timer, PlayerSkillData.SkillSettings settings, bool isVjtActive)
    {
        // 👑【システム復旧】：領域中であっても、無限化をパージし、純粋に発動コスト以上のマナがあるかチェックします
        bool isCostAllowed = (playerMove.currentEnergy >= settings.cost);

        if (isPressed && timer <= 0 && isCostAllowed)
        {
            _recoveryDelayTimer = 0f;

            // 👑【システム復旧】：領域展開中（VJT）であっても、きっちりスキル設定分のコストを減算消費させます！
            playerMove.currentEnergy -= settings.cost;

            emitter.Fire(settings);

            float cooldownMultiplier = statusManager.isOverheated ? 1.5f : 1.0f;
            timer = settings.cooldown * cooldownMultiplier;
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