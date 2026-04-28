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

    // 猶予時間の設定（例：0.5秒間入力がなければ連射カウントをリセットする）
    private const float BURST_RESET_DELAY = 0.5f;
    private float timerZ, timerX, timerC, timerV;

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

        // ★ 引数に burstResetTimer を追加
        HandleSkillInput(input.shotZ, ref timerZ, ref burstCountZ, ref burstResetTimerZ, skillData.skillZ);
        HandleSkillInput(input.shotX, ref timerX, ref burstCountX, ref burstResetTimerX, skillData.skillX);
        HandleSkillInput(input.shotC, ref timerC, ref burstCountC, ref burstResetTimerC, skillData.skillC);
        HandleSkillInput(input.shotV, ref timerV, ref burstCountV, ref burstResetTimerV, skillData.skillV);
    }

    private void HandleSkillInput(bool isPressed, ref float timer, ref int currentBurst, ref float resetTimer, PlayerSkillData.SkillSettings settings)
    {
        if (isPressed && timer <= 0)
        {
            // SE再生は Emitter 側で行うため、ここでは Fire を呼ぶだけにする
            emitter.Fire(settings);

            currentBurst++;
            resetTimer = BURST_RESET_DELAY;

            if (settings.maxBurstCount <= 1 || currentBurst >= settings.maxBurstCount)
            {
                timer = settings.cooldown;
                currentBurst = 0;
                resetTimer = 0;
            }
            else
            {
                timer = (settings.burstInterval > 0) ? settings.burstInterval : 0.1f;
            }
        }

        if (!isPressed && timer <= 0 && currentBurst > 0)
        {
            resetTimer -= Time.fixedDeltaTime;
            if (resetTimer <= 0) currentBurst = 0;
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