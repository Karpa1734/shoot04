using UnityEngine;
using KanKikuchi.AudioManager;

public class SkillManager : MonoBehaviour
{
    [Header("Character Skill Data")]
    public PlayerSkillData skillData;

    private PlayerMove playerMove;
    private PlayerHitHandler hitHandler;
    private PlayerDanmakuEmitter emitter;

    private float timerZ, timerX, timerC, timerV;

    void Start()
    {
        playerMove = GetComponent<PlayerMove>();
        hitHandler = GetComponentInChildren<PlayerHitHandler>();
        emitter = GetComponent<PlayerDanmakuEmitter>();

        if (emitter == null) emitter = gameObject.AddComponent<PlayerDanmakuEmitter>();
    }

    void FixedUpdate()
    {
        if (playerMove == null || skillData == null) return;

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