using KanKikuchi.AudioManager;
using System.Collections;
using UnityEngine;


public class Emitter_Pride : PlayerDanmakuEmitter
{
    // 👑 傲慢のステート管理（デフォルトはディフェンスモードから開始）
    private bool IsAttackmode = false;

    protected override IEnumerator ExecuteSkillZ(PlayerSkillData.SkillSettings s)
    {
        if (IsAttackmode)
        {
            yield return StartCoroutine(ExecuteIcicleRay(s)); // 💥 アタック：アイシクルレイ
        }
        else
        {
            yield return StartCoroutine(ExecuteHollowSphere(s)); // 🛡️ ディフェンス：ホロウスフィア
        }
    }

    protected override IEnumerator ExecuteSkillX(PlayerSkillData.SkillSettings s)
    {
        if (IsAttackmode)
        {
            yield return StartCoroutine(ExecuteDarkPulsar(s)); // 💥 アタック：ダークパルサー
        }
        else
        {
            yield return StartCoroutine(ExecuteDischarge(s)); // 🛡️ ディフェンス：ディスチャージ
        }
    }

    protected override IEnumerator ExecuteSkillC(PlayerSkillData.SkillSettings s)
    {
        // Cスキル：共通の全方位魔方陣トラップ（アセット指定がある場合はsをそのまま処理）
        yield return StartCoroutine(ExecutePrideZoneTrap(s));
    }

    protected override IEnumerator ExecuteSkillV(PlayerSkillData.SkillSettings s)
    {
        // フォームチェンジ（Vボタン）
        yield return StartCoroutine(ExecuteFormChangeV(s));
    }

    protected override IEnumerator ExecuteSkillEX(PlayerSkillData.SkillSettings s)
    {
        // 傲慢のEX究極術式
        yield return StartCoroutine(ExecutePrideUltimateEX(s));
    }

    // =========================================================================
    // ⚔️ 【Zスキル】：アタック（アイシクルレイ） ✕ ディフェンス（ホロウスフィア）
    // =========================================================================

    /// <summary>
    /// 💥 Z-Attack: アイシクルレイ
    /// 敵の座標へ向けて、高速かつ攻撃力ランクの高い直線氷刃弾を3つ時間差で高速スナイプ連射する
    /// </summary>
    private IEnumerator ExecuteIcicleRay(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;
        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        PlayerMove myMove = GetComponentInParent<PlayerMove>();

        // アタックモード時は前傾姿勢（減速をゆるくして攻めやすく）
        if (myMove != null) myMove.skillSpeedMultiplier = Mathf.Max(s.moveSpeedMultiplier, 0.8f);

        int burstCount = Mathf.Max(1, s.count); // 通常3連射想定
        PlaySkillSE(s.sePath);

        for (int i = 0; i < burstCount; i++)
        {
            if (!PlayerMove.CanShoot || (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal)) break;

            // 毎波ターゲットへの角度を精密追従計算
            float targetAngle = GetAngleToTarget(transform.position) + s.angleOffset;

            // 正面へ直線スナイプ弾を射出
            CreateShot(s.bulletData, transform.position, s.speed, targetAngle, s.delay);

            // 4フレームの時間差時差連射ディレイ
            for (int f = 0; f < 4; f++) yield return new WaitForFixedUpdate();
        }

        yield return new WaitForSeconds(s.cooldown);
        if (myMove != null) myMove.skillSpeedMultiplier = 1.0f;
        _activeSkillCoroutines--;
    }

    /// <summary>
    /// 🛡️ Z-Defense: ホロウスフィア
    /// 自身の周囲に低速で公転・防御する巨大な球体弾（お札）を環状に展開し、盾として身に纏う
    /// </summary>
    private IEnumerator ExecuteHollowSphere(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;
        PlayerMove myMove = GetComponentInParent<PlayerMove>();

        // ディフェンス時はしっかり身を固めて低速精密移動
        if (myMove != null) myMove.skillSpeedMultiplier = Mathf.Min(s.moveSpeedMultiplier, 0.5f);
        PlaySkillSE(s.sePath);

        int sphereCount = Mathf.Max(4, s.count); // 盾の枚数
        float currentTargetAngle = GetAngleToTarget(transform.position);

        for (int i = 0; i < sphereCount; i++)
        {
            // 自身の周囲360度へ環状均等に配置
            float placementAngle = currentTargetAngle + (360f / sphereCount * i) + s.angleOffset;

            // s.speed を 0f にして、自機の周りに留まらせる（公転や移動は弾のアセット側または低速直進で適合）
            CreateShot(s.bulletData, transform.position, s.speed, placementAngle, s.delay);
        }

        yield return new WaitForSeconds(s.cooldown);
        if (myMove != null) myMove.skillSpeedMultiplier = 1.0f;
        _activeSkillCoroutines--;
    }

    // =========================================================================
    // ⚔️ 【Xスキル】：アタック（ダークパルサー） ✕ ディフェンス（ディスチャージ）
    // =========================================================================

    /// <summary>
    /// 💥 X-Attack: ダークパルサー
    /// 前方の空間へ向けて、徐々に横幅が広がりながら敵を飲み込んでいく、不吉なV字型の闇の波動（扇形弾幕）を放つ
    /// </summary>
    private IEnumerator ExecuteDarkPulsar(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;
        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();

        PlaySkillSE(s.sePath);
        float centerAngle = GetAngleToTarget(transform.position) + s.angleOffset;

        int wayCount = Mathf.Max(3, s.count); // 5wayなどの扇形
        float spread = s.wideAngle > 0 ? s.wideAngle : 45f;

        float startAngle = centerAngle - (spread / 2f);
        float stepAngle = spread / (wayCount - 1);

        // 弾速差をつけた2層のパルスを同時射出（時間差ハメ防止用）
        for (int layer = 0; layer < 2; layer++)
        {
            if (!PlayerMove.CanShoot || (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal)) break;

            float layerSpeed = s.speed * (layer == 0 ? 1.0f : 0.75f);

            for (int i = 0; i < wayCount; i++)
            {
                float finalAngle = startAngle + (stepAngle * i);
                CreateShot(s.bulletData, transform.position, layerSpeed, finalAngle, s.delay);
            }

            // 2フレームだけずらして厚みを持たせる
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
        }

        yield return new WaitForSeconds(s.cooldown);
        _activeSkillCoroutines--;
    }

    /// <summary>
    /// 🛡️ X-Defense: ディスチャージ
    /// 自身の足元から、迫りくる敵弾を押し返すかのような全方位24wayの衝撃波リングを円状に一斉放射する
    /// </summary>
    private IEnumerator ExecuteDischarge(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;
        PlaySkillSE(s.sePath);

        int allWayCount = 24; // 鉄壁の全方位弾幕
        float stepAngle = 360f / allWayCount;
        float randomBaseOffset = Random.Range(0f, 360f);

        for (int i = 0; i < allWayCount; i++)
        {
            float finalAngle = randomBaseOffset + (stepAngle * i) + s.angleOffset;

            // ディスチャージ用の波を四方八方へ一斉射出
            CreateShot(s.bulletData, transform.position, s.speed, finalAngle, s.delay);
        }

        yield return new WaitForSeconds(s.cooldown);
        _activeSkillCoroutines--;
    }

    // =========================================================================
    // ⚔️ 【C・V・EXスキル】：インフラ管理 ＆ モード切り替え窓口
    // =========================================================================

    /// <summary>
    /// 🪙 C共通スキル: プライドゾーン・トラップ
    /// フォームに関係なく、現在の座標に一定時間残留して罠となる結界弾幕を敷設する
    /// </summary>
    private IEnumerator ExecutePrideZoneTrap(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;
        PlaySkillSE(s.sePath);

        // 自機の現在地にトラップを設置（弾速0、寿命や追加弾幕はアセット側に委託）
        CreateShot(s.bulletData, transform.position, 0f, 0f, s.delay);

        yield return new WaitForSeconds(s.cooldown);
        _activeSkillCoroutines--;
    }

    /// <summary>
    /// 👑 Vスキル: 傲慢のフォームチェンジ（Attack ⇔ Defense）
    /// 使うたびに極性が完全反転。さらに焼き切れ状態でなければ、モード変更のSEとログを美しく出力。
    /// </summary>
    private IEnumerator ExecuteFormChangeV(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;

        // モードを反転
        IsAttackmode = !IsAttackmode; 

        // モード変更の切り替え効果音を再生
        if (SEManager.Instance != null)
        {
            SEManager.Instance.Play(SEPath.MENUSELECT, 0.7f);
        }

        if (IsAttackmode)
        {
            Debug.Log("<color=red>⚔️【傲慢：Change_AtkMode!!】攻撃特化形態へ移行。アイシクルレイ ＆ ダークパルサーが解禁！</color>");

        }
        else
        {
            Debug.Log("<color=cyan>🛡️【傲慢：Change_DefMode!!】絶対防御形態へ移行。ホロウスフィア ＆ ディスチャージが解禁！</color>"); 
        }

        // フォームチェンジ自体の硬直（ごくわずか）
        yield return new WaitForSeconds(Mathf.Max(0.1f, s.cooldown));
        _activeSkillCoroutines--;
    }

    /// <summary>
    /// 😈 EX究極スキル: 傲慢なる王の審判（ヴァニティ・カルマ）
    /// 現在アタックモードなら極大の15本レーザーによる殲滅、ディフェンスモードなら全画面を覆う絶対結界を展開
    /// </summary>
    private IEnumerator ExecutePrideUltimateEX(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;
        _isEXSkillActive = true;
        PlaySkillSE(s.sePath);

        PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
        float currentTargetAngle = GetAngleToTarget(transform.position);

        if (IsAttackmode)
        {
            // 💥 アタック中の必殺：前方を消し飛ばす極大レーザーアレイを3本並行照射
            Debug.Log("<color=red>👑【EX：傲慢王の一撃】審判の極大レーザーアレイを執行！</color>");
            for (int i = -1; i <= 1; i++)
            {
                Vector3 laserOffsetPos = transform.position + transform.right * (i * 0.8f);
                CreateLaserShot(s.bulletData, laserOffsetPos, s.speed, count: 12, wideAngle: 0.8f, warningFrame: 25, isSetupB: false);
            }
        }
        else
        {
            // 🛡️ ディフェンス中の必殺：自身の周囲全方位（12条）に防壁レーザーの魔方陣を一斉に咲かせる
            Debug.Log("<color=cyan>👑【EX：傲慢の絶対結界】十二天柱の防壁結界を展開！</color>");
            float stepAngle = 360f / 12;
            for (int i = 0; i < 12; i++)
            {
                float finalAngle = (stepAngle * i) + currentTargetAngle;
                CreateLaserShot(s.bulletData, transform.position, s.speed, count: 10, wideAngle: 0.4f, warningFrame: 30, isSetupB: true);
            }
        }

        // 必殺の硬直
        yield return new WaitForSeconds(s.cooldown);

        _isEXSkillActive = false;
        _activeSkillCoroutines--;

        // EX終了時に領域が展開されていれば自動クローズさせて足並みを揃える
        if (myStatus != null && myStatus.isSpellCardActive)
        {
            myStatus.DeactivateSpellCard(false);
        }
    }
}