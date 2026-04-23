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

    private float timerZ, timerX, timerC, timerV;

    void Start()
    {
        playerMove = GetComponent<PlayerMove>();
        hitHandler = GetComponentInChildren<PlayerHitHandler>();
        emitter = GetComponent<PlayerDanmakuEmitter>();

        // PlayerStatusManager から自身のキャラデータを取得
        var status = GetComponentInParent<PlayerStatusManager>();
        if (status != null) skillData = status.characterData;
    }
    void FixedUpdate()
    {
        if (playerMove == null || skillData == null) return;

        // ★ 追加：ショット禁止状態（ラウンド間、スタン中、カウントダウン中）なら
        // 全てのクールタイムを強制的に 0（使用可能状態）にする
        if (!PlayerMove.CanShoot)
        {
            timerZ = 0;
            timerX = 0;
            timerC = 0;
            timerV = 0;
        }
        else
        {
            // ショット可能な時だけ、通常通りクールタイムを減少させる
            UpdateTimers();
        }

        // ★ 重要：タイマーを 0 にしたことを UI に即座に反映させるため
        // CanShoot の状態に関わらず、毎回 UI 更新を呼び出す
        UpdateAllCooldownUI();

        // これ以降の「ボタン入力によるスキル発動」は、ショット可能な時のみ実行する
        if (!PlayerMove.CanShoot) return;
        // 被弾中などは発射制限
        if (hitHandler != null && hitHandler.currentState != PlayerHitHandler.PlayerState.Normal) return;

        var input = playerMove.currentFrameInput;

        // 各ボタンのスキル判定
        HandleSkillInput(input.shotZ, ref timerZ, skillData.skillZ);
        HandleSkillInput(input.shotX, ref timerX, skillData.skillX);
        HandleSkillInput(input.shotC, ref timerC, skillData.skillC);
        HandleSkillInput(input.shotV, ref timerV, skillData.skillV);
        // UIを更新する（現在のタイマー値と最大クールタイムを渡す）
        if (skillData != null)
        {
            if (uiZ != null) uiZ.UpdateCooldown(timerZ, skillData.skillZ.cooldown);
            if (uiX != null) uiX.UpdateCooldown(timerX, skillData.skillX.cooldown);
            if (uiC != null) uiC.UpdateCooldown(timerC, skillData.skillC.cooldown);
            if (uiV != null) uiV.UpdateCooldown(timerV, skillData.skillV.cooldown);
        }
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
    /// <summary>
    /// 誰か一人が撃墜・復帰中かどうかを判定するヘルパー
    /// </summary>
    private bool IsAnyPlayerDeadOrRebirthing()
    {
        // PlayerMove.AllPlayers のリストを走査
        foreach (var p in PlayerMove.AllPlayers)
        {
            if (p == null) continue;
            PlayerHitHandler hh = p.GetComponentInChildren<PlayerHitHandler>();

            // 誰か一人の currentState が Normal 以外（Hit や Rebirth）なら true
            if (hh != null && hh.currentState != PlayerHitHandler.PlayerState.Normal)
            {
                return true;
            }
        }
        return false;
    }
    private void HandleSkillInput(bool isPressed, ref float timer, PlayerSkillData.SkillSettings settings)
    {
        // 修正された bulletData 変数を参照
        if (isPressed && timer <= 0 && settings.bulletData != null)
        {
            emitter.Fire(settings);

            string se = string.IsNullOrEmpty(settings.sePath) ? SEPath.SHOT1 : settings.sePath;
            SEManager.Instance.Play(se, 0.4f);

            timer = settings.cooldown;
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
}