// --- SkillManager.cs 【同時押し残像結合・エラー完全解消版】 ---
using KanKikuchi.AudioManager;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

public class SkillManager : MonoBehaviour
{
    PlayerSkillData skillData;

    private PlayerMove playerMove;
    private PlayerHitHandler hitHandler;
    private PlayerDanmakuEmitter emitter;

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

    // ★★★ 【Input System 適合】同時押しを成立させるためのリアルタイム・タイムスタンプ変数 ★★★
    private float _cPressedTimestamp = -100f;
    private float _vPressedTimestamp = -100f;
    private const float INTERACTION_WINDOW = 0.08f; // 同時押しとして許容する猶予時間（0.08秒 ＝ 約5フレーム分）

    // ★ 物理キーボードの跳ね返りによる入力寸断を救済する残像カウンター
    private int _cHoldFrame = 0;
    private int _vHoldFrame = 0;
    private const int HOLD_REMAINS_FRAMES = 5; // 離されてから5物理フレームの間は「まだ押されている」と脳内に残像を残す

    private bool _isExExecutedInThisWindow = false;
    private PlayerMove.ReplayFrame _lastInput;

    void Start()
    {
        playerMove = GetComponentInParent<PlayerMove>();
        hitHandler = GetComponentInParent<PlayerHitHandler>();
        emitter = GetComponentInParent<PlayerDanmakuEmitter>();

        if (ultimateGaugeUI != null && playerMove != null)
        {
            ultimateGaugeUI.Initialize(playerMove);
        }

        var status = GetComponentInParent<PlayerStatusManager>();
        if (status != null)
        {
            skillData = status.characterData;
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

    // --- SkillManager.cs 【バグ完全修正・C,V単押し最速解放版】Update 内 ---

    // --- SkillManager.cs 【排他モディファイア（Exclusive Modifier）適合版】Update 内 ---

    // --- SkillManager.cs 【バケツリレー撤廃・Inputアセットダイレクトバインド版】Update 内 ---

    void Update()
    {
        if (playerMove == null || skillData == null) return;

        // 1. エネルギーの自然回復処理
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
            playerMove.currentEnergy = Mathf.Min(
                skillData.maxEnergy,
                playerMove.currentEnergy + skillData.energyRegenRate * Time.deltaTime
            );
        }

        // 2. タイマー更新
        UpdateTimers();
        UpdateAllCooldownUI();

        if (!PlayerMove.CanShoot) return;
        if (hitHandler != null && hitHandler.currentState != PlayerHitHandler.PlayerState.Normal) return;

        // --- ★★★ 核心：AI思考と人間デバッグ操作の入力ソース完全溶接 ★★★ ---
        bool zPressed = false;
        bool xPressed = false;
        bool cPressed = false;
        bool vPressed = false;
        bool exPressed = false;

        DanmakuAgent agent = GetComponentInParent<DanmakuAgent>();

        // 🌟 修正の核心：
        // 🌟 エージェントコンポーネントが存在し、かつ「自動回避AIモード(_useAutoEvadeAI)」がONになっている時は、
        // 🌟 人間のアセット入力をバイパスし、AIが毎フレーム吐き出す高精度パケット(currentFrameInput)を完全適用する！
        if (agent != null && agent._useAutoEvadeAI)
        {
            // AIの頭脳モデルが走っている時、または自律行動デバッグ時はパケットから完全デコード
            var input = playerMove.currentFrameInput;
            zPressed = input.shotZ;
            xPressed = input.shotX;
            cPressed = input.shotC;
            vPressed = input.shotV;
            exPressed = input.ultimate;
        }
        else
        {
            // 🌟 人間が手動でキーボード操作デバッグしている時は、InputManagerアセットから直接ポーリング！
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
            }
        }

        // ① 【最優先：EX必殺技（アクション5番）の実行判定】
        // AIの戦術的ぶっ放し、または人間の同時押しアセットシグナルをダイレクトに直撃させます
        if (exPressed)
        {
            if (timerEX <= 0f && playerMove.ultimateEnergy >= 100f)
            {
                playerMove.ultimateEnergy -= 100f; // 100%消費
                emitter.FireEX(skillData.skillEX); // 独立EX枠の射出

                // 内部タイマーをScriptableObjectアセットの固有値から自動同期！
                timerEX = skillData.skillEX.cooldown > 0f ? skillData.skillEX.cooldown : EX_COOLDOWN;

                Debug.Log("<color=orange>★★★★★ 【究極大成功】二重バケツリレーを撤廃し、AIシグナルとInputアセットの双方からEX弾幕が完全覚醒しました！ ★★★★★</color>");
            }

            // 同時押しActionが走っているフレームは、下層の通常単押しC・Vのトランザクションを100%排他して処理を抜ける
            return;
        }

        // ② 【通常スキルの最速実行】
        // 同時押し（EX）がONになっていないプレーンなフレームの時だけ、通常技がレイテンシ（遅延）ゼロで即座に実行されます！
        HandleSkillInput(zPressed, ref timerZ, skillData.skillZ);
        HandleSkillInput(xPressed, ref timerX, skillData.skillX);
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