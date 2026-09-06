using KanKikuchi.AudioManager;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class Emitter_Pride : PlayerDanmakuEmitter
{
    // 👑 傲慢のステート管理（デフォルトはディフェンスモードから開始）
    private bool IsAttackmode = false;

    // 🌟 共通でアニメーションを安全に取得・実行するためのヘルパーメソッド
    private void PlaySkillAnimation(string skillName)
    {
        PlayerAnimation pAnim = GetComponentInChildren<PlayerAnimation>();
        if (pAnim == null && _rootOwner != null) pAnim = _rootOwner.GetComponentInChildren<PlayerAnimation>();

        if (pAnim != null)
        {
            pAnim.TriggerSkillAnimation(skillName);
        }
    }

    private void PlayEXAnimation()
    {
        PlayerAnimation pAnim = GetComponentInChildren<PlayerAnimation>();
        if (pAnim == null && _rootOwner != null) pAnim = _rootOwner.GetComponentInChildren<PlayerAnimation>();

        if (pAnim != null)
        {
            pAnim.TriggerEXSkillAnimation();
        }
    }

    protected override IEnumerator ExecuteSkillZ(PlayerSkillData.SkillSettings s)
    {
        yield return StartCoroutine(ExecuteIcicleRay(s)); // 💥 アタック：アイシクルレイ
    }

    protected override IEnumerator ExecuteSkillX(PlayerSkillData.SkillSettings s)
    {
        yield return StartCoroutine(ExecuteDarkPulsar(s)); // 💥 アタック：ダークパルサー

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


    // 🌟 同時展開しているアクティブなレーザーのセットを追跡するリスト
    private List<List<EnemyLaserBeam>> _activeIcicleLaserSets = new List<List<EnemyLaserBeam>>();
    private const int MAX_ICICLE_SETS = 3; // 最大3セットまで同時展開可能

    // 💡 外部（PlayerDanmakuEmitterのCanFireなど）から上限に達しているか安全に確認するためのヘルパー
    public bool HasReachedMaxIcicleLasers()
    {
        if (_activeIcicleLaserSets != null)
        {
            _activeIcicleLaserSets.RemoveAll(set => set == null || set.TrueForAll(l => l == null));
            return _activeIcicleLaserSets.Count >= MAX_ICICLE_SETS;
        }
        return false;
    }

    /// <summary>
    /// 🧊 予告線を敵機方向に向かわせ、指定フレーム後に実線化して発射するアイスレーザー（即時完了・連射対応版）
    /// </summary>
    private IEnumerator ExecuteIcicleRay(PlayerSkillData.SkillSettings s)
    {
        if (BulletManager.Instance == null) yield break;

        // 🛡️ 最大数チェック
        if (_activeIcicleLaserSets != null)
        {
            _activeIcicleLaserSets.RemoveAll(set => set == null || set.TrueForAll(l => l == null));
            if (_activeIcicleLaserSets.Count >= MAX_ICICLE_SETS)
            {
                yield break;
            }
        }

        PlaySkillSE(s.sePath);
        PlaySkillAnimation(s.skillName);

        int warningFrame = 30; // 予告フレーム (0.5秒)
        float targetAngle = GetAngleToTarget(transform.position) + s.angleOffset;

        List<EnemyLaserBeam> currentSetLasers = new List<EnemyLaserBeam>();

 
            EnemyLaserBeam laser = CreateLaserShot(
                s.bulletData,
                transform.position,
                s.speed,
                s.count,
                s.wideAngle,
                warningFrame,
                isSetupB: true
            );

        if (laser != null)
        {
            currentSetLasers.Add(laser);

            // 予告線が敵機方向を向くようにデータを登録（少しずつ角度を散らす）
            laser.AddData(new EnemyLaserBeam.LaserTransformData
            {
                frame = 0,
                dist = 0f,
                distAngle = 0f,
                laserAngle = targetAngle,
                    distAngleVel = 0f,
                laserAngleVel = 0f,
                isSmooth = true
            });

            // 予告時間後の実線化ロックデータ
            laser.AddData(new EnemyLaserBeam.LaserTransformData
            {
                frame = warningFrame,
                laserAngleVel = 0f,
                isSmooth = true
            });

            laser.Fire();

        }

        if (currentSetLasers.Count > 0)
        {
            _activeIcicleLaserSets.Add(currentSetLasers);

            // 💡 スキル本体は即座に終了しますが、レーザーが消えるまでの間だけ
            // マナの自然回復を止めるためにライフタイム管理コルーチンを裏で独立して走らせます
            StartCoroutine(ManageIcicleSetLifetime(currentSetLasers, (warningFrame / 60f) + 1.0f));
        }

        // 🎯 予告線を出した瞬間にスキル発射処理としては完了（即座に別のスキルや連射が可能に！）
        yield break;
    }

    /// <summary>
    /// 生成されたレーザーセットのライフタイムを監視し、終了時にクローズして管理リストから外す
    /// </summary>
    private IEnumerator ManageIcicleSetLifetime(List<EnemyLaserBeam> laserSet, float duration)
    {
        // レーザーが生存している間だけマナ自然回復をブロック
        _activeSkillCoroutines++;

        yield return new WaitForSeconds(duration);

        foreach (var laser in laserSet)
        {
            if (laser != null)
            {
                laser.ForceClose();
            }
        }

        if (_activeIcicleLaserSets != null)
        {
            _activeIcicleLaserSets.Remove(laserSet);
        }

        if (_activeSkillCoroutines > 0)
        {
            _activeSkillCoroutines--;
        }
    }
    // 🌟 全方位弾の回転方向反転用フラグ（Xスキル用）
    private bool _isDarkPulsarRotReversed = false;

    /// <summary>
    /// 💥 X-Attack: ダークパルサー
    /// 自機外し全方位弾を、射角を回転させながら段階的に弾速を上げつつ連続発射する弾幕ルーチン
    /// </summary>
    private IEnumerator ExecuteDarkPulsar(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;
        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        PlayerMove myMove = GetComponentInParent<PlayerMove>();

        if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = s.moveSpeedMultiplier;

        PlaySkillSE(s.sePath);
        PlaySkillAnimation(s.skillName);

        Vector3 pos = transform.position;

        // 1. 1波あたりの弾数を設定（偶数丸め処理）
        int baseBulletCount = s.count > 0 ? s.count : 16;
        if (baseBulletCount % 2 != 0) baseBulletCount++;
        int bulletCount = Mathf.Max(4, baseBulletCount);

        float step = 360f / bulletCount;
        float evenWayOffset = step / 2f;

        float currentSpeed = 6f; // 初速

        // 使うたびに回転方向が反転
        bool currentRotReversed = _isDarkPulsarRotReversed;
        _isDarkPulsarRotReversed = !_isDarkPulsarRotReversed;

        float rotDirection = currentRotReversed ? -1f : 1f;
        float angleIncrement = 12f * rotDirection; // 1波ごとの回転角

        float targetAngle = GetAngleToTarget();
        float baseAngle = targetAngle + s.angleOffset + evenWayOffset + Random.Range(-3f,3f);


        PlaySkillSE(s.sePath);

        // 1波分の全方位弾を生成
        for (int i = 0; i < bulletCount; i++)
        {
            float finalAngle = baseAngle + (step * i);
            CreateShot(s.bulletData, pos, currentSpeed, finalAngle, delay: s.delay);
        }



        yield return new WaitForSeconds(s.cooldown);
        if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = 1.0f;
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
    /// <summary>
    /// 👑 Vスキル: 傲慢のフォームチェンジ（Attack ⇔ Defense）
    /// 使うたびに極性が完全反転。切り替え時のアニメーション、SE、詳細なステータスログを出力します。
    /// </summary>
    private IEnumerator ExecuteFormChangeV(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;

        // 1. モード変数を反転（False: ディフェンス ⇄ True: アタック）
        IsAttackmode = !IsAttackmode;

        // 2. フォームチェンジ用のアニメーションを再生
        PlaySkillAnimation(s.skillName);

        // 3. 切り替え効果音を再生
        if (SEManager.Instance != null)
        {
            SEManager.Instance.Play(SEPath.MENUSELECT, 0.7f); // 必要に応じて専用のSEパスに変更可能
        }

        // 4. 現在のモードに応じた詳細なデバッグログ（変数状態の可視化）を出力
        if (IsAttackmode)
        {
            Debug.Log($"<color=red>⚔️【傲慢 (Pride) フォームチェンジ】アタックモード起動！ (IsAttackmode = {IsAttackmode}) ➔ [アイシクルレイ ＆ ダークパルサー] が解放されました。</color>");

            // 💡 補足：もしアタックモード時に機体カラーやオーラを変化させたい場合はここに処理を追加できます
        }
        else
        {
            Debug.Log($"<color=cyan>🛡️【傲慢 (Pride) フォームチェンジ】ディフェンスモード起動！ (IsAttackmode = {IsAttackmode}) ➔ [ホロウスフィア ＆ ディスチャージ] が解放されました。</color>");

            // 💡 補足：ディフェンスモード時の見た目の切り替え処理などもここに記述可能です
        }

        // 5. フォームチェンジ自体の硬直（ごくわずかなウェイト）
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