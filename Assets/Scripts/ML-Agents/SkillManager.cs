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
    [Header("UI Slots")]
    public SkillCooldownUI uiZ;
    public SkillCooldownUI uiX;
    public SkillCooldownUI uiC;
    public SkillCooldownUI uiV;
    // ★追加：各スキルの現在の連射数をカウントする
    private int burstCountZ, burstCountX, burstCountC, burstCountV;
    // ★ 追加：連射カウントを保持する猶予時間タイマー
    private float burstResetTimerZ, burstResetTimerX, burstResetTimerC, burstResetTimerV;
    private float _recoveryDelayTimer = 0f; // 回復開始までの待機タイマー
    // 猶予時間の設定（例：0.5秒間入力がなければ連射カウントをリセットする）
    private const float BURST_RESET_DELAY = 0.5f;
    private float timerZ, timerX, timerC, timerV;
    [Header("Energy UI")]
    public EnergyGaugeUI energyGauge; // ★ 追加：インスペクターでセット
    void Start()
    {
        // 1. 必要なコンポーネントを自身の親や子から取得する（これが抜けていました）
        playerMove = GetComponentInParent<PlayerMove>();
        hitHandler = GetComponentInParent<PlayerHitHandler>();
        emitter = GetComponentInParent<PlayerDanmakuEmitter>();

        // 2. 自分のキャラクターデータを取得
        var status = GetComponentInParent<PlayerStatusManager>();
        if (status != null)
        {
            skillData = status.characterData;
        }
        // 2. 自分のキャラクターデータから最大コストをセット[cite: 10, 12]
        if (playerMove != null && skillData != null)
        {
            playerMove.maxEnergy = skillData.maxEnergy;
            playerMove.currentEnergy = skillData.maxEnergy;

            // 3. エネルギーゲージの初期化
            if (energyGauge != null) energyGauge.Initialize(playerMove);
        }
        // 3. アイコンをUIにセットする
        if (skillData != null)
        {
            if (uiZ != null) uiZ.SetSkillIcon(skillData.skillZ.skillIcon);
            if (uiX != null) uiX.SetSkillIcon(skillData.skillX.skillIcon);
            if (uiC != null) uiC.SetSkillIcon(skillData.skillC.skillIcon);
            if (uiV != null) uiV.SetSkillIcon(skillData.skillV.skillIcon);
        }
        else
        {
            Debug.LogWarning("SkillManager: characterData が見つかりません。");
        }
    }
    void FixedUpdate()
    {
        if (playerMove == null || skillData == null) return;
        // 1. 回復判定：スキル使用中（コルーチン実行中）か、ボタン入力があったらタイマーをリセット
        // ★ 修正：ボタン入力の有無ではなく、スキルが「実行中」かどうかだけをチェックする
        bool isAnySkillActive = emitter.IsAnySkillActive;

        if (isAnySkillActive)
        {
            _recoveryDelayTimer = 0f; // 使用中はタイマーが進まない
        }
        else
        {
            _recoveryDelayTimer += Time.fixedDeltaTime;
        }

        // 2. 「何もしていない状態」が1秒続いたら回復開始
        if (PlayerMove.CanShoot && _recoveryDelayTimer >= 1.0f)
        {
            playerMove.currentEnergy = Mathf.Min(
                skillData.maxEnergy,
                playerMove.currentEnergy + skillData.energyRegenRate * Time.fixedDeltaTime
            );
    }
        if (!PlayerMove.CanShoot)
        {
            ResetAllTimers();
        }
        else
        {
            UpdateTimers();
        }

        UpdateAllCooldownUI();

        if (!PlayerMove.CanShoot) return;
        if (hitHandler != null && hitHandler.currentState != PlayerHitHandler.PlayerState.Normal) return;

        var input = playerMove.currentFrameInput;

        // ★ 引数を変更：コストチェックを組み込む
        HandleSkillInput(input.shotZ, ref timerZ, skillData.skillZ);
        HandleSkillInput(input.shotX, ref timerX, skillData.skillX);
        HandleSkillInput(input.shotC, ref timerC, skillData.skillC);
        HandleSkillInput(input.shotV, ref timerV, skillData.skillV);
    }

    // ★ ロジックを「回数」から「コスト」へ刷新
    private void HandleSkillInput(bool isPressed, ref float timer, PlayerSkillData.SkillSettings settings)
    {
        if (isPressed && timer <= 0 && playerMove.currentEnergy >= settings.cost)
        {
            // コストを消費[cite: 11]
            playerMove.currentEnergy -= settings.cost;

            // 弾を射出[cite: 8]
            emitter.Fire(settings);

            // 次の連射までのインターバルをセット（連射中の制限）[cite: 11]
            timer = (settings.burstInterval > 0) ? settings.burstInterval : 0.1f;
        }
    }

    private void UpdateTimers()
    {
        float dt = Time.fixedDeltaTime;
        if (timerZ > 0) timerZ -= dt;
        if (timerX > 0) timerX -= dt;
        if (timerC > 0) timerC -= dt;
        if (timerV > 0) timerV -= dt;
    }

    private void ResetAllTimers()
    {
        timerZ = timerX = timerC = timerV = 0;
        burstCountZ = burstCountX = burstCountC = burstCountV = 0;
        burstResetTimerZ = burstResetTimerX = burstResetTimerC = burstResetTimerV = 0;
    }
    private void UpdateAllCooldownUI()
    {
        if (skillData == null) return;

        // 前回の回答で作成した SkillCooldownUI.UpdateCooldown を呼び出す
        if (uiZ != null) uiZ.UpdateCooldown(timerZ, skillData.skillZ.cooldown);
        if (uiX != null) uiX.UpdateCooldown(timerX, skillData.skillX.cooldown);
        if (uiC != null) uiC.UpdateCooldown(timerC, skillData.skillC.cooldown);
        if (uiV != null) uiV.UpdateCooldown(timerV, skillData.skillV.cooldown);
    }
   

}