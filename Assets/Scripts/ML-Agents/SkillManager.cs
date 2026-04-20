using UnityEngine;
using KanKikuchi.AudioManager;

public class SkillManager : MonoBehaviour
{
    PlayerSkillData skillData;

    private PlayerMove playerMove;
    private PlayerHitHandler hitHandler;
    private PlayerDanmakuEmitter emitter;

    private float timerZ, timerX, timerC, timerV;

    void Start()
    {
        playerMove = GetComponent<PlayerMove>();
        hitHandler = GetComponentInChildren<PlayerHitHandler>();
        emitter = GetComponent<PlayerDanmakuEmitter>();

        // ★ 自分で持たず、隣の PlayerStatusManager からデータを貰ってくる
        var status = GetComponent<PlayerStatusManager>();
        if (status != null)
        {
            skillData = status.characterData;
        }
    }
    void FixedUpdate()
    {
        if (playerMove == null || skillData == null) return;
        // ★ 修正：自分または相手が死んでいる（Normal状態でない）間は、全ての入力を無視する
        if (IsAnyPlayerDeadOrRebirthing()) return;
        UpdateTimers();

        // 被弾中などは発射制限
        if (hitHandler != null && hitHandler.currentState != PlayerHitHandler.PlayerState.Normal) return;

        var input = playerMove.currentFrameInput;

        // 各ボタンのスキル判定
        HandleSkillInput(input.shotZ, ref timerZ, skillData.skillZ);
        HandleSkillInput(input.shotX, ref timerX, skillData.skillX);
        HandleSkillInput(input.shotC, ref timerC, skillData.skillC);
        HandleSkillInput(input.shotV, ref timerV, skillData.skillV);
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