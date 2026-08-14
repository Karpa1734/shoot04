using KanKikuchi.AudioManager;
using System.Collections;
using UnityEngine;

public class Emitter_Wrath : PlayerDanmakuEmitter
{

    protected bool _isXLineReversed;
    // ⚔️ カリンのZスキル＝「しの字」アーク一閃！
    protected override IEnumerator ExecuteSkillZ(PlayerSkillData.SkillSettings s)
    {
        yield return StartCoroutine(ExecuteKarinScalesSlashRoutine(s));
    }

    // ⚔️ カリンのXスキル＝空間一閃・双極ブレード！
    protected override IEnumerator ExecuteSkillX(PlayerSkillData.SkillSettings s)
    {
        yield return StartCoroutine(ExecuteKarinCrossSlashRoutine(s));
    }

    // ⚔️ カリンのCスキル＝ブーメラン設置！
    protected override IEnumerator ExecuteSkillC(PlayerSkillData.SkillSettings s)
    {
        yield return StartCoroutine(ShootBoomerangRoutine(s));
    }

    // ⚔️ カリンのVスキル＝防御フィールドチャージ！
    protected override IEnumerator ExecuteSkillV(PlayerSkillData.SkillSettings s)
    {
        yield return StartCoroutine(ChargeAndExecuteDefensiveField(s));
    }

    protected override IEnumerator ExecuteSkillEX(PlayerSkillData.SkillSettings s)
    {
        yield return StartCoroutine(ExecuteKarinKokuZessenEXRoutine(s));
    }

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

    // 🔄【新規追加】：カリンのZスキルの使用回数を数えて奇数/偶数を判定するためのカウンター
    private int _karinZComboCount = 0;

    // =========================================================================
    // 🐉【一本化完結版】：カリン専用Z：「しの字」アーク・ポリモーフィック一閃
    // 💡 技を呼び出す窓口を1つに統合し、通常時（1本）と領域中（3本時差展開）を自動仕分けします。
    // =========================================================================
    protected IEnumerator ExecuteKarinScalesSlashRoutine(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++; // 🟢 マナの自動回復を一時停止
        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        PlayerMove myMove = GetComponentInParent<PlayerMove>();

        if (myMove != null) myMove.skillSpeedMultiplier = s.moveSpeedMultiplier;

        // 🎬【モーション連携】：使用回数をインクリメントし、奇数回なら SkillZ、偶数回なら SkillZ2 を切り替え再生！
        _karinZComboCount++;
        PlayerAnimation pAnim = GetComponentInChildren<PlayerAnimation>();
        if (pAnim != null)
        {
            Animator anim = pAnim.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                // 1回目、3回目、5回目… は "SkillZ"、2回目、4回目、6回目… は "SkillZ2" をトリガー
                string targetTrigger = (_karinZComboCount % 2 != 0) ? "SkillZ" : "SkillZ2";
                anim.SetTrigger(targetTrigger);
            }
        }

        yield return new WaitForSeconds(0.15f);

        // 💡 領域展開（スペルカード）中かどうかのステートを上流インフラから安全に取得
        PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
        bool isSpellActive = (myStatus != null && myStatus.isSpellCardActive);

        // 🎯 1. 自機から敵機を見見据えた、絶対的な基準ターゲット角度を取得
        float absoluteCenterAngle = GetAngleToTarget(transform.position);

        // 🔄 2. 【数理空間の動的変調】：領域中なら3連撃（0度、45度、-45度）、通常時なら正面（0度）のみ！
        float[] tripleOffsets = isSpellActive ? new float[] { 0f, 45f, -45f } : new float[] { 0f };
        int loopCount = tripleOffsets.Length;

        // コントロール用の往復極性を反転ロック
        bool comboBaseDirection = _isArcReversed;
        _isArcReversed = !_isArcReversed;

        for (int i = 0; i < loopCount; i++)
        {
            if (!PlayerMove.CanShoot || (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal))
                break;

            PlayerSkillData.SkillSettings currentSettings = s;
            if (isSpellActive && i == 0)
            {
                currentSettings.count = 1; // 領域中の最初の1本目の変調仕様を踏襲
            }

            // 基準軸から綺麗に変調をかけたターゲット角度を算出
            float customAngle = absoluteCenterAngle + tripleOffsets[i];
            PlaySkillSE(s.sePath);

            // 💡 統合の核心：通常時も領域中も、共通の「しの字」トラック生成サブルーチンへ角度を投げる！
            StartCoroutine(ExecuteSingleScalesSlashTrack(currentSettings, customAngle, comboBaseDirection));

            // ⏳ 領域中の時だけ、次弾発射までの「3フレームの時間差ディレイ」を正確にホールド
            if (isSpellActive && i < loopCount - 1)
            {
                for (int f = 0; f < 3; f++)
                {
                    yield return new WaitForFixedUpdate();
                }
            }
        }

        yield return new WaitForSeconds(s.cooldown);
        if (myMove != null) myMove.skillSpeedMultiplier = 1.0f;
        _activeSkillCoroutines--;
    }

    /// <summary>
    /// トリプル展開・通常共通用：指定された絶対角度に向けて「1wayしの字」の軌跡を1本走らせるサブルーチン（角度完全適合版）
    /// </summary>
    protected IEnumerator ExecuteSingleScalesSlashTrack(PlayerSkillData.SkillSettings s, float targetAngle, bool forcedReverse)
    {
        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();

        float startAngleFromTangent = 27f;
        float totalRotationAmount = 3;
        float baseRadiusX = 2.0f;
        float baseRadiusY = 0.8f;

        bool currentDirectionReversed = forcedReverse;
        float startLocalAngle = currentDirectionReversed ? 152f : -152f;
        float localAngleStep = currentDirectionReversed ? -18f : 18f;

        float baseRad = targetAngle * Mathf.Deg2Rad;
        float cosRot = Mathf.Cos(baseRad);
        float sinRot = Mathf.Sin(baseRad);

        int totalStepsCount = 11;

        float f_localAngRad = startLocalAngle * Mathf.Deg2Rad;
        float f_localX = Mathf.Cos(f_localAngRad) * baseRadiusX * 1.3f;
        float f_localY = Mathf.Sin(f_localAngRad) * baseRadiusY * 1.3f;
        Vector3 firstSpawnPos = transform.position + new Vector3(f_localX * cosRot - f_localY * sinRot, f_localX * sinRot + f_localY * cosRot, 0);

        float f_nextLocalAngRad = (startLocalAngle + (localAngleStep * 0.01f)) * Mathf.Deg2Rad;
        float f_nextLocalX = Mathf.Cos(f_nextLocalAngRad) * baseRadiusX * Mathf.Lerp(1.3f, 0.6f, 0.01f / (totalStepsCount - 1));
        float f_nextLocalY = Mathf.Sin(f_nextLocalAngRad) * baseRadiusY * Mathf.Lerp(1.3f, 0.6f, 0.01f / (totalStepsCount - 1));
        Vector3 firstNextSpawnPos = transform.position + new Vector3(f_nextLocalX * cosRot - f_nextLocalY * sinRot, f_nextLocalX * sinRot + f_nextLocalY * cosRot, 0);

        Vector3 firstTangentDir = firstNextSpawnPos - firstSpawnPos;
        float lockedInitialTangentAngle = Mathf.Atan2(firstTangentDir.y, firstTangentDir.x) * Mathf.Rad2Deg;

        for (int step = 0; step < totalStepsCount; step++)
        {
            if (!PlayerMove.CanShoot || (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal))
                break;

            float t = (float)step / (totalStepsCount - 1);
            float localAngle = startLocalAngle + (localAngleStep * step);
            float radiusModifier = Mathf.Lerp(1.3f, 0.6f, t);

            float localAngleRad = localAngle * Mathf.Deg2Rad;
            float localX = Mathf.Cos(localAngleRad) * baseRadiusX * radiusModifier;
            float localY = Mathf.Sin(localAngleRad) * baseRadiusY * radiusModifier;

            Vector3 worldOffset = new Vector3(localX * cosRot - localY * sinRot, localX * sinRot + localY * cosRot, 0);
            Vector3 spawnPos = transform.position + worldOffset;

            float rotationSign = currentDirectionReversed ? -1f : 1f;
            float baseStartAngle = lockedInitialTangentAngle + (startAngleFromTangent * rotationSign);
            float currentMoveAngle = baseStartAngle + (totalRotationAmount * t * rotationSign);

            float finalBulletAngle = currentMoveAngle + s.angleOffset;

            int layerCount = 2;
            for (int l = 0; l < layerCount; l++)
            {
                float speedPercent = Mathf.Lerp(1.1f, 0.8f, (float)l / (layerCount - 1));
                float randomizedSpeed = s.speed * speedPercent;
                randomizedSpeed = Mathf.Max(1.0f, randomizedSpeed);

                // 💡 s.count が 3 以上の時は、1発の直進ではなく、その座標から広がる扇形（3way）をオート展開
                if (s.count >= 3)
                {
                    float wayAngle = s.wideAngle / (s.count - 1);
                    float startWayAngle = finalBulletAngle - (s.wideAngle / 2f);

                    for (int wCount = 0; wCount < s.count; wCount++)
                    {
                        float final3WayAngle = startWayAngle + (wayAngle * wCount);
                        CreateShot(s.bulletData, spawnPos, randomizedSpeed, final3WayAngle, s.delay);
                    }
                }
                else
                {
                    CreateShot(s.bulletData, spawnPos, randomizedSpeed, finalBulletAngle, s.delay);
                }
            }

            yield return new WaitForFixedUpdate();
        }
    }
    // 🔄【新規追加】：カリンのXスキルの使用回数を数えて奇数/偶数を判定するためのカウンター
    private int _karinXComboCount = 0;

    /// <summary>
    /// ⚔️ カリン専用X：空間一閃・自機外し双極ブレード
    /// 🌟 【領域展開・4wayクアッド変調適合版】：
    /// 🌟 通常時はターゲットを見据えたキレのある2way（左右30度開くツインブレード）。
    /// 🌟 領域展開（スペルカード）中はs.countをインフラ層から検知するか、内部フラグを自動ブレンド。
    /// 🌟 ターゲットの逃げ道を100%遮断する「4way大爆風扇形一閃（左右15度・45度）」へと動的にポリモーフィック進化します！
    /// </summary>
    protected IEnumerator ExecuteKarinCrossSlashRoutine(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;
        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        PlayerMove myMove = GetComponentInParent<PlayerMove>();

        if (myMove != null) myMove.skillSpeedMultiplier = s.moveSpeedMultiplier;

        // 🎬【モーション連携】：使用回数をインクリメントし、奇数回なら SkillX、偶数回なら SkillX2 を切り替え再生！
        _karinXComboCount++;
        PlayerAnimation pAnim = GetComponentInChildren<PlayerAnimation>();
        if (pAnim != null)
        {
            Animator anim = pAnim.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                // 1回目、3回目、5回目… は "SkillX"、2回目、4回目、6回目… は "SkillX2" をトリガー
                string targetTrigger = (_karinXComboCount % 2 != 0) ? "SkillZ" : "SkillZ2";
                anim.SetTrigger(targetTrigger);
            }
        }

        // -------------------------------------------------------------------------
        // 🔮 一直線ラインの空間パラメータ設計
        // -------------------------------------------------------------------------
        float lineLengthY = 4.0f; // 剣跡の上下の長さ
        int totalStepsCount = 12; // 剣跡を構成する弾源の密度

        // 🌟【交互反転制御】：使うたびに真偽値が入れ替わります
        bool currentDirectionReversed = _isXLineReversed;
        _isXLineReversed = !_isXLineReversed;

        // 💡【往復生成の調停】
        // 奇数回目（false）：下から上へ走るライン
        // 偶数回目（true） ：上から下へ走るライン
        float startLocalY = currentDirectionReversed ? lineLengthY : -lineLengthY;
        float endLocalY = currentDirectionReversed ? -lineLengthY : lineLengthY;

        // 自機から見た敵機の絶対ターゲット角度を基準軸としてキャプチャ
        float absoluteCenterAngle = GetAngleToTarget(transform.position);
        float baseRad = absoluteCenterAngle * Mathf.Deg2Rad;
        float cosRot = Mathf.Cos(baseRad);
        float sinRot = Mathf.Sin(baseRad);

        PlaySkillSE(s.sePath);

        // 💡 領域展開中（スペルカードアクティブ）のフラグを動的チェック
        PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
        bool isSpellActive = (myStatus != null && myStatus.isSpellCardActive);

        // 🔄 一閃ライン連射ループ（空間を縦に引き裂くスピード感）
        for (int step = 0; step < totalStepsCount; step++)
        {
            if (!PlayerMove.CanShoot || (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal))
                break;

            float t = (float)step / (totalStepsCount - 1);

            // 💡 ターゲットとの直線に対して「垂直な一直線」上の座標を計算
            float localX = 1.0f; // 自機から少し前方に離れた位置に一閃のラインを生成
            float localY = Mathf.Lerp(startLocalY, endLocalY, t);

            // 🌟【2D回転行列】：敵機の絶対角度に合わせてワールド座標へ展開
            Vector3 worldOffset = new Vector3(
                localX * cosRot - localY * sinRot,
                localX * sinRot + localY * cosRot,
                0
            );
            Vector3 spawnPos = transform.position + worldOffset;

            // 各弾源（spawnPos）から見た「敵機へのリアルタイム角度」の抽出
            float angleToEnemyFromSpawnPoint = GetAngleToTarget(spawnPos);

            // 🌟 多段速度差（ツインブレード仕様）の射出
            int layerCount = 2;
            for (int i = 0; i < layerCount; i++)
            {
                float speedPercent = Mathf.Lerp(1.2f, 0.9f, (float)i / (layerCount - 1));
                float randomizedSpeed = s.speed * speedPercent;
                randomizedSpeed = Mathf.Max(1.0f, randomizedSpeed);

                // =========================================================================
                // 🔮【領域変調】：2way ➔ 4way 動的分岐調停システム
                // =========================================================================
                if (isSpellActive)
                {
                    // 🎯【領域展開中：豪華4way（クアッドブレード）】
                    // 💡 ターゲットの正面（0度）を中心に、均等に広がる美しい4wayの扇形（例：計90度幅、30度ステップ）
                    // 💡 具体角：-45度、-15度、+15度、+45度 の4方向に美しく一斉射出！
                    float wideAngleTotal = 80f;
                    float stepAngle = wideAngleTotal / (4 - 1); // 30度ずつ
                    float startWayAngle = angleToEnemyFromSpawnPoint - (wideAngleTotal / 2f) + s.angleOffset;

                    for (int w = 0; w < 4; w++)
                    {
                        float final4WayAngle = startWayAngle + (stepAngle * w);
                        CreateShot(s.bulletData, spawnPos, randomizedSpeed, final4WayAngle, s.delay);
                    }
                }
                else
                {
                    // 🎯【通常時：キレのある自機外し2way】
                    float fanSize = 60f;
                    float halfFan = fanSize / 2f;

                    float leftWayAngle = angleToEnemyFromSpawnPoint + halfFan + s.angleOffset;
                    float rightWayAngle = angleToEnemyFromSpawnPoint - halfFan + s.angleOffset;

                    // 各弾源から敵を見据えて、左右30度ルートへ射出
                    CreateShot(s.bulletData, spawnPos, randomizedSpeed, leftWayAngle, s.delay);
                    CreateShot(s.bulletData, spawnPos, randomizedSpeed, rightWayAngle, s.delay);
                }
            }

            yield return new WaitForFixedUpdate();
        }
        yield return new WaitForSeconds(s.cooldown);
        if (myMove != null) myMove.skillSpeedMultiplier = 1.0f;
        _activeSkillCoroutines--;
    }



    // =========================================================================
    // 🐉【自律判断・インフラ一元化完結版】：カリン専用究極EX：神速・虚空絶閃
    // =========================================================================
    protected IEnumerator ExecuteKarinKokuZessenEXRoutine(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;
        PlayerMove myMove = _rootOwner.GetComponent<PlayerMove>();
        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
        _isEXSkillActive = true;

        if (myMove == null || myStatus == null)
        {
            _activeSkillCoroutines--;
            yield break;
        }

        myMove.skillSpeedMultiplier = 0f;

        // 🎯 1. ターゲットのリアルタイムな左右座標を精密に観測
        float targetAngle = GetAngleToTarget(transform.position);
        bool isEnemyOnRightSide = (targetAngle > -90f && targetAngle <= 90f);

        float mySideScreenEdgeX = isEnemyOnRightSide ? -8.5f : 8.5f;
        float enemySideScreenEdgeX = isEnemyOnRightSide ? 8.5f : -8.5f;
        bool faceRight = isEnemyOnRightSide;

        float startY = _rootOwner.transform.position.y;

        // 💨 敵機の【反対側の画面端】へ超高速バックステップ
        Vector3 startPos = _rootOwner.transform.position;
        Vector3 backStepTargetPos = new Vector3(mySideScreenEdgeX, startY, startPos.z);

        float bsTimer = 0f;
        float bsDuration = 0.15f;
        while (bsTimer < bsDuration)
        {
            bsTimer += Time.fixedDeltaTime;
            float elapsedPercent = bsTimer / bsDuration;
            _rootOwner.transform.position = Vector3.Lerp(startPos, backStepTargetPos, elapsedPercent);
            yield return new WaitForFixedUpdate();
        }
        _rootOwner.transform.position = backStepTargetPos;

        // 🎬【モーション連携】：バックステップ完了（チャージ開始時）に SkillEX（チャージ用）のアニメーションを再生
        PlayerAnimation pAnim = GetComponentInChildren<PlayerAnimation>();
        Animator anim = pAnim.GetComponentInChildren<Animator>();
        if (pAnim == null && _rootOwner != null) pAnim = _rootOwner.GetComponentInChildren<PlayerAnimation>();
        if (pAnim != null)
        {
            if (anim != null)
            {
                anim.SetTrigger("ULT"); // 🌟 チャージ開始モーションのトリガー
            }
        }

        // ⏳ 抜刀の「タメ」演出
        float chargeTime = 0.4f;
        if (BossEffectManager.Instance != null)
        {
            BossEffectManager.Instance.PlayChargeEffect(chargeTime, s.bulletData.breakColor, _rootOwner.transform.position);
        }
        yield return new WaitForSeconds(chargeTime);

        Vector3 laserStartPos = _rootOwner.transform.position;
        // 🎬【モーション連携】：タメが完了して超高速突撃（発射）する瞬間に SkillEX2（発動用）へ切り替え
        if (anim != null) // ※もしanim変数をスコープ内で使い回す場合は直接取得します
        {
            // 安全のため再取得
            Animator animTarget = (pAnim != null) ? pAnim.GetComponentInChildren<Animator>() : null;
            if (animTarget != null)
            {
                animTarget.SetTrigger("ULT2"); // 🌟 突撃・発射モーションのトリガー
            }
        }

        // ⚡ 刹那一閃・敵陣の画面端まで超高速突撃
        SEManager.Instance.Play(SEPath.SLASH, 0.5f);
        Vector3 d_startPos = _rootOwner.transform.position;
        Vector3 d_targetPos = new Vector3(enemySideScreenEdgeX, startY, startPos.z);

        float dashTimer = 0f;
        float dashDuration = 0.1f;
        while (dashTimer < dashDuration)
        {
            dashTimer += Time.fixedDeltaTime;
            float elapsedPercent = dashTimer / dashDuration;
            _rootOwner.transform.position = Vector3.Lerp(d_startPos, d_targetPos, elapsedPercent);
            yield return new WaitForFixedUpdate();
        }
        _rootOwner.transform.position = d_targetPos;

        // 突撃移動の絶対距離（レーザーの長さ）を正確に計算
        float laserDistance = Vector3.Distance(laserStartPos, _rootOwner.transform.position);

        // 🔮【領域検知】：聖少女領域中なら15本バースト、通常空間ならs.count（5本など）
        bool isZoneActive = (myStatus != null && myStatus.isSpellCardActive);
        int totalLinesCount = isZoneActive ? 15 : Mathf.Max(2, s.count);
        bool isEnhancedLines = (totalLinesCount >= 15);

        if (isEnhancedLines)
        {
            Debug.Log("<color=gold>🔮【自律判断・極大空間破砕】カリン究極EX：聖少女領域を検知したため、15本バーストへ拡張執行！</color>");
        }

        // 幾何学アライメントの完全調停
        float offsetStep = isEnhancedLines ? 1.2f : 1.8f;
        float startYOffset = isEnhancedLines ? (-offsetStep * 7f) : (-offsetStep * (float)(totalLinesCount / 2));

        // 🔮 虚空砕裂：上下中心追従展開マトリクス
        for (int i = 0; i < totalLinesCount; i++)
        {
            float currentYOffset = startYOffset + (offsetStep * i);
            Vector3 finalLaserSpawnPos = new Vector3(laserStartPos.x, laserStartPos.y + currentYOffset, laserStartPos.z);

            int dynamicDelay = isEnhancedLines ? (20 + (i * 1)) : 20;

            EnemyLaserBeam zessenLaser = CreateLaserShot(
                s.bulletData,
                finalLaserSpawnPos,
                s.speed,
                count: Mathf.RoundToInt(laserDistance),
                wideAngle: 0.5f,
                warningFrame: dynamicDelay,
                isSetupB: false
            );

            if (zessenLaser != null)
            {
                SpriteRenderer laserSR = zessenLaser.GetComponentInChildren<SpriteRenderer>();
                bool isCustomSpriteAssigned = (s.bulletData.bulletSprite != null);

                if (laserSR != null && isCustomSpriteAssigned)
                {
                    laserSR.sprite = s.bulletData.bulletSprite;
                    if (s.bulletData.material != null) laserSR.material = s.bulletData.material;
                }

                float laserFacingAngle = faceRight ? 0f : 180f;
                zessenLaser.AddData(new EnemyLaserBeam.LaserTransformData { frame = 0, angle = laserFacingAngle });
                zessenLaser.Fire();

                if (isCustomSpriteAssigned)
                {
                    foreach (Transform child in zessenLaser.transform)
                    {
                        if (child != null && (child.name.Contains("Root") || child.name.Contains("Effect") || child.name.Contains("Source")))
                        {
                            child.gameObject.SetActive(false);
                        }
                    }
                }

                float extendedDuration = s.speed + 1.0f;
                StartCoroutine(KeepInvertingLaserOffsetRoutine(zessenLaser.gameObject, laserDistance, extendedDuration, faceRight));
                StartCoroutine(ForceCloseLaserAfterSeconds(zessenLaser, extendedDuration));
            }

            if (!isEnhancedLines)
            {
                yield return null;
            }
        }

        if (myMove != null) myMove.skillSpeedMultiplier = 1.0f;
        _activeSkillCoroutines--;
    }

    /// <summary>
    /// 💡 毎フレーム判定ボックスをお札のグラフィックスの芯へ完全に吸い付かせ、一体化させる調停ループ
    /// </summary>
    protected IEnumerator KeepInvertingLaserOffsetRoutine(GameObject laserObj, float distance, float duration, bool faceRight)
    {
        float timer = 0f;
        BoxCollider2D col = (laserObj != null) ? laserObj.GetComponent<BoxCollider2D>() : null;

        while (timer < duration && laserObj != null && col != null)
        {
            yield return new WaitForFixedUpdate();

            if (laserObj == null || col == null) yield break;

            // 🎯【判定ズレの完全根治】：
            // 右向き突撃（faceRight=true）の時は、お札画像が右に伸びるのに合わせて、判定コライダーもプラス（1f）方向へ。
            // 左向き突撃（faceRight=false）の時は、お札画像が左に伸びるのに合わせて、判定コライダーもマイナス（-1f）方向へ。
            // これにより、右へ一閃した時も左へ一閃した時も、完璧に画像の真上に判定が密着します！
            float offsetSign = faceRight ? 1f : -1f;

            col.size = new Vector2(0.6f, distance); // 当たり判定の適切な太さのクランプ
            col.offset = new Vector2(0f, distance * 0.5f * offsetSign);

            timer += Time.fixedDeltaTime;
        }
    }

    protected IEnumerator ForceCloseLaserAfterSeconds(EnemyLaserBeam laser, float duration)
    {
        PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
        yield return new WaitForSeconds(duration);
        if (laser != null) laser.ForceClose();
        _isEXSkillActive = false; // 🚨 個別フラグ
        if (myStatus != null && myStatus.isSpellCardActive)
        {
            myStatus.DeactivateSpellCard(false);
        }
    }

    protected IEnumerator ShootBoomerangRoutine(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++; // 実行カウントを増やす

        // 🎬【モーション連携】：ブーメラン投げ発動時にスキルアニメーションを即座に再生
        PlaySkillAnimation(s.skillName);

        // --- 既存の生成ロジック ---
        GameObject bitObj = Instantiate(s.bulletData.bulletPrefab, transform.position, Quaternion.identity);
        var myStatus = GetComponentInParent<PlayerStatusManager>();
        int ownerId = (myStatus != null) ? myStatus.playerId : 1;
        string assignedTag = (ownerId == 1) ? "PlayerBullet" : "EnemyBullet";
        string assignedLayer = (ownerId == 1) ? "Player1Bullet" : "Player2Bullet";
        bitObj.tag = assignedTag;
        bitObj.layer = LayerMask.NameToLayer(assignedLayer);
        SetLayerRecursive(bitObj, LayerMask.NameToLayer(assignedLayer));

        BoomerangObject bit = bitObj.GetComponent<BoomerangObject>();
        if (bit == null) bit = bitObj.AddComponent<BoomerangObject>();

        Transform targetTransform = null;
        foreach (var p in PlayerMove.AllPlayers)
            if (p != null && p.gameObject != _rootOwner) targetTransform = p.transform;

        // ビットの初期化
        bit.Initialize(transform, targetTransform, s.bulletData, 4.0f, this);

        // --- ここがポイント：2秒間待機 ---
        // この間 IsAnySkillActive が true になり、SkillManager 側の回復が止まります
        yield return new WaitForSeconds(2.0f);

        _activeSkillCoroutines--; // 2秒経ったらカウントを減らす
    }
    // --- ★ 追加：防御フィールド専用のチャージ演出ルーチン ---
    // 📄 PlayerDanmakuEmitter.cs 内の防御フィールド制御セクター【領域展開・動的巨大延長版】
    // --- ★ 追加：防御フィールド専用のチャージ演出ルーチン ---
    // 📄 PlayerDanmakuEmitter.cs 内の防御フィールド制御セクター【領域展開・動的巨大延長版】
    private IEnumerator ChargeAndExecuteDefensiveField(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++; //
        PlayerMove myMove = _rootOwner.GetComponent<PlayerMove>(); //

        // 🎬【モーション連携】：チャージ開始時に SkillV アニメーションを即座に再生
        PlaySkillAnimation(s.skillName); // ※これがインスペクター上の skillV 名に一致して "SkillV" トリガーを引きます

        // 💡 1. 領域展開中（スペルカード発動中）であるかステートをチェック
        PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
        bool isSpellActive = (myStatus != null && myStatus.isSpellCardActive);

        // 💡 2. 【高橋さんの指定】：領域中ならサイズと持続時間の変数を動的にブースト！
        float finalFieldDuration = 1.0f; // 通常時の持続秒数
        float finalFieldScale = 2.0f;    // 通常時のDefensiveFieldインスペクター想定スケール

        if (isSpellActive)
        {
            finalFieldDuration = 2.0f;   // 🎯 領域展開中：持続時間を延長
            finalFieldScale = 3.5f;      // 🎯 領域展開中：サイズを巨大化
            Debug.Log($"<color=gold>🔮【領域展開・絶対防壁】防御フィールドを極大化！ Duration: {finalFieldDuration}s, Scale: {finalFieldScale}</color>");
        }

        // チャージ演出
        float chargeTime = 0.2f; //

        // 🛡️【完全静止】：チャージが始まった瞬間から移動速度を 0 にしてその場に完全に固定！
        if (myMove != null)
        {
            myMove.skillSpeedMultiplier = 0f;
        }

        if (BossEffectManager.Instance != null) //
        {
            BossEffectManager.Instance.PlayChargeEffect(chargeTime, s.bulletData.breakColor, transform.position); //
        }
        yield return new WaitForSeconds(chargeTime); //

        if (SEManager.Instance != null)
        {
            SEManager.Instance.Play(SEPath.SLASH, 0.5f); //
        }

        // 🎬【モーション連携】：チャージが完了し、実際にフィールドを発動する瞬間に V2（発動用）へ切り替え
        PlayerAnimation pAnim = GetComponentInChildren<PlayerAnimation>();
        if (pAnim == null && _rootOwner != null) pAnim = _rootOwner.GetComponentInChildren<PlayerAnimation>();
        if (pAnim != null)
        {
            Animator anim = pAnim.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.SetTrigger("SkillV2"); // 🌟 "SkillV2" トリガーを引いて発動モーションへ移行
            }
        }

        // 💡 3. 変調されたサイズと持続時間を手渡しして、スキル本体を実体化！
        ExecuteDefensiveField(s, finalFieldDuration, finalFieldScale);

        // 💡 4. 【完全同期】：シールドの展開時間（expandTime=0.2秒）＋持続時間（finalFieldDuration）＋縮小消滅時間（shrinkTime=0.5秒）の間、移動を完全にロック！
        // ※ DefensiveField.cs の定義に合わせ、消滅演出が完全に終わるまでの時間を正確に同期させます。
        float totalShieldLifeTime = 0.2f + finalFieldDuration + 0.5f;
        yield return new WaitForSeconds(totalShieldLifeTime);

        // 🛡️ シールドが画面上から完全に消滅した瞬間に、移動倍率を 1.0f に戻して自由に行動できるようにする
        if (myMove != null) //
        {
            myMove.skillSpeedMultiplier = 1.0f; //
        }
        _activeSkillCoroutines--; //
    }

    // 🎯【引数拡張】：外部変調パラメータを確実に受け取れるようにオーバーロード調停
    private void ExecuteDefensiveField(PlayerSkillData.SkillSettings s, float duration, float scale)
    {
        GameObject fieldObj = Instantiate(s.bulletData.bulletPrefab, transform.position, Quaternion.identity); //
        var myStatus = GetComponentInParent<PlayerStatusManager>(); //
        int ownerId = (myStatus != null) ? myStatus.playerId : 1; //
        string assignedTag = (ownerId == 1) ? "PlayerBullet" : "EnemyBullet"; //
        int assignedLayer = LayerMask.NameToLayer((ownerId == 1) ? "Player1Bullet" : "Player2Bullet"); //

        var field = fieldObj.GetComponent<DefensiveField>(); //
        if (field == null) field = fieldObj.AddComponent<DefensiveField>(); //

        // 💡 拡張された Initialize 窓口へパラメータを一挙にインジェクション！
        field.Initialize(transform, s.bulletData, duration, assignedTag, assignedLayer, scale);
    }


}
