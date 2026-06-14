// --- SkillManager.cs 【アセット入力完全パージ・PlayerMove一元化適合版】 ---
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
        // 💡 ライフサイクルの壁を越えて、PlayerStatusManagerがランクから算出した
        // 💡 確定初期値（playerMove.maxEnergy）をそのまま満タンとして初期化に用います！
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
        // 💡 通常時の「スキル使用後にマナ回復が始まるまでの待ち時間」をここで直感的に秒数指定！
        const float BASE_WAIT_SECONDS = 0.5f;

        if (emitter.IsAnySkillActive)
        {
            // 💡 パッシブ「回復ディレイ短縮」を持っている場合は0.8倍、持っていなければ1.0倍
            float passiveDelayRate = 1.0f;
            if (statusManager != null && statusManager.HasPassiveSkill(PassiveSkillType.GreedReduction))
            {
                passiveDelayRate = 0.7f; // ⚡ 30%短縮
            }

            // 💡 現在いずれかのスキルを撃ちまくっている最中は、常に待ち時間を最大値でロックホールド！
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
            // 💡 スキルの硬直が解け、何もしていない平和な状態（アイドル時）であれば、
            //    目標の0秒に向かってタイマーを純粋にカウントダウン減算していきます。
            if (_recoveryCooldownTimer > 0f)
            {
                _recoveryCooldownTimer -= Time.deltaTime;
            }
        }

        // 💡 3.【回復の執行】：待ち時間タイマーが完全に0秒以下（消化完了）になった場合のみ、
        //    PlayerMove側に集約されたランク実数値のスピードでマナが自動回復を執行します！
        // 💡 3.【回復の執行】：待ち時間タイマーが完全に0秒以下（消化完了）になった場合のみ、
        // 💡 3.【回復の執行】：待ち時間タイマーが完全に0秒以下（消化完了）になった場合のみ、
        if (_recoveryCooldownTimer <= 0f)
        {
            // 自身のリスク・リターン状態によるベース倍率の決定
            float regenMultiplier = 1.0f;
            if (statusManager.isOverheated) regenMultiplier = 0.5f;
            else if (statusManager.isSpellCardActive)
            {

                regenMultiplier = 2.0f; // 他の領域なら従来通りの2倍

                // 💡【強欲強化】：自身が「強欲(ActionTax)」の領域を展開しているなら3.0倍、それ以外の通常領域なら2.0倍
                if (statusManager.characterData != null && statusManager.characterData.vjtEffectType == VJTEffectType.GreedCast)
                {
                    regenMultiplier *= 1.5f;
                }

            }

            // 🌀【対戦相手からの強欲領域デバフ検知インフラ】
            // 相手が現在領域展開中で、かつその効果が「強欲（ActionTax）」だった場合、自分の回復倍率をさらに半分（x0.5）に叩き落とす
            if (playerMove != null && playerMove.Opponent != null)
            {
                PlayerStatusManager oppStatus = playerMove.Opponent.GetComponent<PlayerStatusManager>();
                if (oppStatus != null && oppStatus.isSpellCardActive && oppStatus.characterData != null)
                {
                    if (oppStatus.characterData.vjtEffectType == VJTEffectType.GreedCast)
                    {
                        regenMultiplier *= 0.5f; // 回復量を強制的に半減
                    }
                }
            }

            // =========================================================================
            // 🦥【新規追加：怠惰領域による移動中マナ回復フリーズ割り込み】
            // =========================================================================
            // 💡 核心：自分が現在「怠惰領域」に捕まっており、かつ「移動中」であるならば、
            //          マナ回復の加算計算を丸ごとスキップさせて回復量を【完全ゼロ】へと叩き落とします！
            if (statusManager != null && statusManager.IsSlothRegenBlocked())
            {
                regenMultiplier = 0f;
            }

            // ランク逆算ベースのクリーンな一元化プロパティを参照してマナを加算
            if (regenMultiplier > 0f) // 0の時は加算を行わないガード
            {
                playerMove.currentEnergy = Mathf.Min(
                    playerMove.maxEnergy,
                    playerMove.currentEnergy + (playerMove.energyRegenRate * regenMultiplier * Time.deltaTime)
                ); //
            }

            // 🦥【新規追加：怠惰のコスト回復力1.3倍ブースト】
            if (statusManager != null && statusManager.IsSlothBoostActive())
            {
                regenMultiplier *= 1.5f; // 停止時マナ回復スピードを1.3倍に加速
            }


            // ランク逆算ベースのクリーンな一元化プロパティを参照してマナを加算
            playerMove.currentEnergy = Mathf.Min(
                playerMove.maxEnergy,
                playerMove.currentEnergy + (playerMove.energyRegenRate * regenMultiplier * Time.deltaTime)
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

        // =========================================================================
        // 🤖 A. 敵AI（自動回避・自律駆動モード）またはリプレイ再生時の入力完全抽出
        // =========================================================================
        if (agent != null && (agent._useAutoEvadeAI || playerMove.currentMode == PlayerMove.ReplayMode.Playing))
        {
            var input = playerMove.currentFrameInput;

            // 💡 核心修正：AIが脳内で選んだスキル（Z,X,C,V,ULT）を1ミリの上書きもなくダイレクトに結合！
            zPressed = input.shotZ;
            xPressed = input.shotX;
            cPressed = input.shotC;
            vPressed = input.shotV;
            exPressed = input.ultimate;

            // AIの戦術思考が領域展開を要求しており、かつ自分がまだ領域を展開していない場合
            vjtPressed = (agent._useAutoEvadeAI && playerMove.currentFrameInput.ultimate &&
                          playerMove.ultimateEnergy >= 200f && !statusManager.isSpellCardActive);
        }
        // =========================================================================
        // ⌨️ B. 人間操作（キーボード・パッドデバイス）の入力スキャン
        // =========================================================================
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
        // 🚀 各種アクションの執行判定ルーチン（ここからはAI・人間共通で稼働）
        // =========================================================================
        if (vjtPressed && !statusManager.isSpellCardActive)
        {
            statusManager.ActivateSpellCard();
            return;
        }

        if (exPressed)
        {
            if (statusManager.isSpellCardActive)
            {
                if (timerEX <= 0f)
                {
                    playerMove.ultimateEnergy = 0f;
                    emitter.FireEX(skillData.skillEX);

                    timerEX = skillData.skillEX.cooldown > 0f ? skillData.skillEX.cooldown : EX_COOLDOWN;
                    Debug.Log("<color=magenta>👑【AI/Player ULT】領域を維持したまま、究極必殺技をキックしました！</color>");
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

            float cooldownMultiplier = statusManager.isOverheated ? 1.5f : 1.0f;
            timer = settings.cooldown * cooldownMultiplier;
        }
    }

    private void UpdateTimers()
    {
        // 🦥 怠惰パッシブ発動中で停止していれば、クールダウン消費速度を1.3倍にブースト！
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

    // =========================================================================
    // 🎯【修正】：ScriptableObjectのパラメータ削除に伴い、
    // 🎯          ここも playerMove 一元化プロパティを参照するように変更
    // =========================================================================
    public void InstantFullRecovery()
    {
        ResetAllTimers();
        if (playerMove != null)
        {
            playerMove.currentEnergy = playerMove.maxEnergy; // 💡変更
        }
        _recoveryDelayTimer = 0f;
        UpdateAllCooldownUI();
    }
}