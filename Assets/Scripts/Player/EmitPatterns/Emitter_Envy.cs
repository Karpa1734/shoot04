using KanKikuchi.AudioManager;
using System.Collections;
using UnityEngine;

public class Emitter_Envy : PlayerDanmakuEmitter
{
    // 🌟 Zスキルの長押し連射中であることを追跡するフラグ
    private bool _isEnvyZHolding = false;

    // 外部（SkillManagerなど）から「現在長押し連射中か？」を安全に参照できるようにするプロパティ
    public bool IsEnvyZHolding => _isEnvyZHolding;

    protected override IEnumerator ExecuteSkillZ(PlayerSkillData.SkillSettings s)
    {
        // 既に長押し連射が走っている場合は多重起動を防ぐ
        if (_isEnvyZHolding) yield break;
        _isEnvyZHolding = true;
        _activeSkillCoroutines++;

        PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();

        // 🌟 修正：各エミッター共通の安全な取得方法で PlayerMove を取得
        PlayerMove myMove = _rootOwner != null ? _rootOwner.GetComponent<PlayerMove>() : GetComponentInParent<PlayerMove>();

        // 🌟 修正：PlayerMove 経由で取得したインスタンスからマナ（currentEnergy）を安全に参照
        if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = s.moveSpeedMultiplier;

        PlayerAnimation pAnim = GetComponentInChildren<PlayerAnimation>();
        if (pAnim == null && _rootOwner != null) pAnim = _rootOwner.GetComponentInChildren<PlayerAnimation>();

        // 💡 規定フレームごとの発射間隔（例: 0.1秒 = 約6フレーム）
        float fireInterval = 0.05f;

        try
        {
            while (true)
            {
                // 1. 射撃許可やステートの確認
                if (!PlayerMove.CanShoot || myStatus == null) break;

                // 2. 🌟 修正：myMove 経由でコスト（currentEnergy）が1発分残っているかチェック
                if (myMove != null && myMove.currentEnergy < s.cost)
                {
                    // コストが尽きたら連射ループを抜ける
                    break;
                }

                // 3. 弾の発射処理
                PlaySkillSE(s.sePath);
                if (pAnim != null) pAnim.TriggerSkillAnimation(s.skillName);

                float targetAngle = GetAngleToTarget(transform.position) + s.angleOffset;

                if (s.count > 1)
                {
                    float wayAngle = s.wideAngle / (s.count - 1);
                    float startWayAngle = targetAngle - (s.wideAngle / 2f);

                    for (int i = 0; i < s.count; i++)
                    {
                        float finalAngle = targetAngle + Random.Range(-16, 16);
                        CreateShot(s.bulletData, transform.position, s.speed, finalAngle, delay: s.delay);
                    }
                }
                else
                {
                    CreateShot(s.bulletData, transform.position, s.speed, targetAngle, delay: s.delay);
                }

                // 🌟 修正：myMove 経由でコスト消費 ＆ アルカナ充填
                if (myMove != null)
                {
                    myMove.currentEnergy -= s.cost;
                    myMove.AddUltimateEnergy(s.ultimateGain * 0.2f);
                }

                // 4. 規定のフレーム/間隔（fireInterval）だけ待機（この間クールタイムは回りません）
                float elapsed = 0f;
                while (elapsed < fireInterval)
                {
                    elapsed += Time.deltaTime;

                    // 5. プレイヤーがボタンを離したかどうかを毎フレーム監視
                    if (!IsZButtonPressed(myStatus))
                    {
                        // 🌟 ボタンを離した瞬間にループを抜け、finallyブロックで正式なクールタイムが開始されます！
                        yield break;
                    }
                    yield return null;
                }
            }
        }
        finally
        {
            // 6. 🧹【重要】：長押し終了（指を離した、またはマナ枯渇）の瞬間にクールタイムを正式にスタートさせる！
            if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = 1.0f;
            _isEnvyZHolding = false;
            _activeSkillCoroutines--;
            if (_activeSkillCoroutines < 0) _activeSkillCoroutines = 0;

            // 💡 SkillManager側のZスキル用クールタイム（timerZ）に正式な長押し後クールタイムを強制注入！
            SkillManager skillMgr = GetComponentInChildren<SkillManager>();
            if (skillMgr == null && _rootOwner != null) skillMgr = _rootOwner.GetComponentInChildren<SkillManager>();
            if (skillMgr != null)
            {
                float cooldownMultiplier = (myStatus != null && myStatus.isOverheated) ? 1.5f : 1.0f;
                skillMgr.timerZ = s.cooldown * cooldownMultiplier;
            }
        }
    }

    /// <summary>
    /// 現在Zボタンが押し続けられているかを判定するヘルパー
    /// </summary>
    private bool IsZButtonPressed(PlayerStatusManager status)
    {
        if (status == null || InputManager.Instance == null) return false;

        var inputSet = (status.playerId == 2) ? InputManager.Instance.player2 : InputManager.Instance.player1;
        if (inputSet.skillZ != null && inputSet.skillZ.action != null)
        {
            return inputSet.skillZ.action.IsPressed();
        }

        return Input.GetKey(KeyCode.L) || Input.GetKey(KeyCode.Z);
    }

    protected override IEnumerator ExecuteSkillX(PlayerSkillData.SkillSettings s) { yield return StartCoroutine(SkillTempleteX(s)); }
    protected override IEnumerator ExecuteSkillC(PlayerSkillData.SkillSettings s) { yield return StartCoroutine(SkillTempleteC(s)); }
    protected override IEnumerator ExecuteSkillV(PlayerSkillData.SkillSettings s) { yield return StartCoroutine(SkillTempleteV(s)); }
    protected override IEnumerator ExecuteSkillEX(PlayerSkillData.SkillSettings s) { yield return StartCoroutine(SkillTempleteEX(s)); }

    protected IEnumerator SkillTempleteX(PlayerSkillData.SkillSettings s) { yield return null; }
    protected IEnumerator SkillTempleteC(PlayerSkillData.SkillSettings s) { yield return null; }
    private IEnumerator SkillTempleteV(PlayerSkillData.SkillSettings s) { yield return null; }
    protected IEnumerator SkillTempleteEX(PlayerSkillData.SkillSettings s) { yield return null; }
}