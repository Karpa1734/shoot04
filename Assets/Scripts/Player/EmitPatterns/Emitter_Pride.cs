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
        float baseAngle = targetAngle + s.angleOffset + evenWayOffset + Random.Range(-6f,6f);


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
    // 🌟【新規追加】：Cスキルのチャージ（長押し回転）中であるかを追跡するフラグ
    private bool _isPrideTrapCharging = false;
    private List<DanmakuBullet> _chargingTrapBullets = new List<DanmakuBullet>();

    private IEnumerator ExecutePrideZoneTrap(PlayerSkillData.SkillSettings s)
    {
        if (_isPrideTrapCharging) yield break;
        _isPrideTrapCharging = true;

        _activeSkillCoroutines++;
        PlaySkillSE(s.sePath);
        PlaySkillAnimation(s.skillName);

        PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
        PlayerMove myMove = _rootOwner != null ? _rootOwner.GetComponent<PlayerMove>() : GetComponentInParent<PlayerMove>();

        float chargeMoveSpeed = (s.moveSpeedMultiplier > 0f) ? s.moveSpeedMultiplier : 0.5f;
        if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = chargeMoveSpeed;

        Vector3 pos = transform.position;
        int wayCount = 3; // 3way固定
        List<DanmakuBullet> spawnedBullets = new List<DanmakuBullet>();

        try
        {
            // 1. 自機中心から3wayの罠弾を生成（それぞれのインデックスに対応した個別の弾データを取得）
            for (int i = 0; i < wayCount; i++)
            {
                float initialAngle = 120f * i;
                float shootSpeed = s.speed > 0f ? s.speed : 4f;

                // 🌟【個別弾データの選択】：multiBulletDatas に個別に設定があればそれを優先、なければ共通の bulletData を使用
                BulletData targetBulletData = s.bulletData;
                if (s.multiBulletDatas != null && s.multiBulletDatas.Length > i && s.multiBulletDatas[i] != null)
                {
                    targetBulletData = s.multiBulletDatas[i];
                }

                DanmakuBullet bullet = ExecuteSubShot02_Returnable(
                    targetBulletData, // 👈 個別に指定された弾データを渡す
                    pos,
                    shootSpeed,
                    initialAngle,
                    0f,
                    shootSpeed,
                    targetTag,
                    gameObject.layer,
                    angularVelocity: 0f,
                    maxRotationLimit: 0f
                );

                if (bullet != null)
                {
                    spawnedBullets.Add(bullet);
                }
            }

            _chargingTrapBullets = spawnedBullets;

            UnityEngine.InputSystem.InputAction cAction = null;
            if (InputManager.Instance != null && myStatus != null)
            {
                var inputSet = (myStatus.playerId == 2) ? InputManager.Instance.player2 : InputManager.Instance.player1;
                if (inputSet.skillC != null) cAction = inputSet.skillC.action;
            }

            float maxOrbitRadius = 1.5f;
            float expandDuration = 0.5f;
            float chargeElapsed = 0f;
            float[] currentOrbitAngles = new float[spawnedBullets.Count];

            for (int i = 0; i < spawnedBullets.Count; i++)
            {
                currentOrbitAngles[i] = (120f * i) * Mathf.Deg2Rad;
            }

            // =========================================================================
            // 🔄【フェーズ1：長押しループ】ボタンを押し続けている間、回転＆半径拡大
            // =========================================================================
            bool isKeyReleased = false;
            while (!isKeyReleased)
            {
                if (!PlayerMove.CanShoot)
                {
                    isKeyReleased = true;
                    break;
                }

                if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = chargeMoveSpeed;

                chargeElapsed += Time.fixedDeltaTime;
                float angularVelocity = 200f * Mathf.Deg2Rad;
                float currentRadius = Mathf.Min(maxOrbitRadius, (chargeElapsed / expandDuration) * maxOrbitRadius);

                for (int i = 0; i < spawnedBullets.Count; i++)
                {
                    if (spawnedBullets[i] != null)
                    {
                        currentOrbitAngles[i] += angularVelocity * Time.fixedDeltaTime;

                        float targetX = transform.position.x + Mathf.Cos(currentOrbitAngles[i]) * currentRadius;
                        float targetY = transform.position.y + Mathf.Sin(currentOrbitAngles[i]) * currentRadius;

                        spawnedBullets[i].transform.position = new Vector3(targetX, targetY, 0f);

                        float facingAngleDeg = (currentOrbitAngles[i] * Mathf.Rad2Deg) + 90f;
                        spawnedBullets[i].SetAngle(facingAngleDeg);
                    }
                }

                yield return new WaitForFixedUpdate();

                DanmakuAgent agent = GetComponentInChildren<DanmakuAgent>();
                if (agent == null) agent = GetComponentInParent<DanmakuAgent>();

                if (agent != null && agent._useAutoEvadeAI)
                {
                    if (chargeElapsed >= 1.5f) isKeyReleased = true;
                }
                else
                {
                    if (cAction != null && !cAction.IsPressed()) isKeyReleased = true;
                    else if (cAction == null && !Input.GetKey(KeyCode.C)) isKeyReleased = true;
                    else if (chargeElapsed >= 5.0f) isKeyReleased = true;
                }
            }

            // =========================================================================
            // 🚀【フェーズ2：ボタン解放後】ホーミング突撃発射
            // =========================================================================

            // =========================================================================
            // 🚀【フェーズ2：ボタン解放後】ホーミング突撃発射 ＋ 弾限定の残像トレイル処理
            // =========================================================================

            // ボタンを離してホーミングに移行した瞬間に、自機の移動速度を通常（1.0f）に戻す
            if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = 1.0f;

            PlaySkillSE(s.sePath);
            float homingDuration = 1.0f;
            float homingTimer = 0f;
            float homingSpeed = (s.speed > 0f ? s.speed : 4f) * 1.0f;

            // 🌟【残像用タイマー管理】
            float trailSpawnTimer = 0f;
            float trailInterval = 0.05f; // 残像を残す間隔（秒）

            while (homingTimer < homingDuration)
            {
                float dt = Time.deltaTime;
                homingTimer += dt;
                trailSpawnTimer += dt;

                // 一定間隔ごとに、生存している各弾の現在地に「半透明の残像スプライト」をその場に残す
                bool shouldLeaveTrail = (trailSpawnTimer >= trailInterval);
                if (shouldLeaveTrail)
                {
                    trailSpawnTimer = 0f;
                }

                for (int i = spawnedBullets.Count - 1; i >= 0; i--)
                {
                    DanmakuBullet b = spawnedBullets[i];
                    if (b != null)
                    {
                        // 1. ホーミング処理
                        float targetAngleToEnemy = GetAngleToTarget(b.transform.position);
                        float currentAngle = b.Angle;
                        float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngleToEnemy, 300f * dt);

                        b.SetAngle(newAngle);
                        b.SetSpeed(homingSpeed);

                        // 2. 🌟【この弾だけの限定的な残像処理】
                        if (shouldLeaveTrail && b.gameObject != null)
                        {
                            // 弾自身のスプライト情報から一時的な残像ゴーストオブジェクトを生成
                            SpriteRenderer parentSR = b.GetComponentInChildren<SpriteRenderer>();
                            if (parentSR != null && parentSR.sprite != null)
                            {
                                GameObject ghostObj = new GameObject("BulletTrailGhost");
                                ghostObj.transform.position = b.transform.position;
                                ghostObj.transform.rotation = b.transform.rotation;
                                ghostObj.transform.localScale = b.transform.localScale;

                                SpriteRenderer ghostSR = ghostObj.AddComponent<SpriteRenderer>();
                                ghostSR.sprite = parentSR.sprite;
                                ghostSR.material = parentSR.material;
                                ghostSR.sortingLayerID = parentSR.sortingLayerID;
                                ghostSR.sortingOrder = parentSR.sortingOrder - 1; // 弾より少し手前に描画
                                ghostSR.color = new Color(0.8f, 0.9f, 1f, 0.5f); // 薄い青白い半透明

                                // 0.2秒かけてシュッと消えながら消滅する残像コンポーネントをインラインで付与
                                ghostObj.AddComponent<BulletGhostFade>();
                            }
                        }
                    }
                    else
                    {
                        spawnedBullets.RemoveAt(i);
                    }
                }
                yield return null;
            }

            yield return new WaitForSeconds(1.0f);
        }
        finally
        {
            if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = 1.0f;
            _isPrideTrapCharging = false;
            _chargingTrapBullets.Clear();

            _activeSkillCoroutines--;
            if (_activeSkillCoroutines < 0) _activeSkillCoroutines = 0;
        }
    }
    /// <summary>
    /// 🛡️ Vスキル: 傲慢のディフェンス・ダッシュ（一定時間無敵化・半透明化・残像残し・移動速度ブースト）
    /// </summary>
    private IEnumerator ExecuteFormChangeV(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;
        PlaySkillSE(s.sePath);
        PlaySkillAnimation(s.skillName);

        PlayerMove myMove = _rootOwner != null ? _rootOwner.GetComponent<PlayerMove>() : GetComponentInParent<PlayerMove>();

        // 1. プレイヤーの移動スクリプト側が持つ無敵付与機能を利用して一定時間無敵にする（例: 1.0秒間）
        float skillDuration = s.cooldown > 0f ? Mathf.Min(s.cooldown, 2.5f) : 1.0f;
        if (myMove != null)
        {
            myMove.SetInvincible(skillDuration);
        }

        // 2. 移動速度をアップ（例: 設定倍率が1.0以上ならそのまま、未設定なら1.6倍など）
        float boostSpeedMultiplier = (s.moveSpeedMultiplier > 1.0f) ? s.moveSpeedMultiplier : 1.6f;
        if (myMove != null && !_isEXSkillActive)
        {
            myMove.skillSpeedMultiplier = boostSpeedMultiplier;
        }

        // 3. 自機のスプライトを半透明にする（無敵・ステルス感を演出）
        SpriteRenderer[] bodyRenderers = GetComponentsInChildren<SpriteRenderer>();
        if (bodyRenderers.Length == 0 && _rootOwner != null)
        {
            bodyRenderers = _rootOwner.GetComponentsInChildren<SpriteRenderer>();
        }

        Dictionary<SpriteRenderer, Color> originalColors = new Dictionary<SpriteRenderer, Color>();
        foreach (var sr in bodyRenderers)
        {
            if (sr != null)
            {
                originalColors[sr] = sr.color;
                Color c = sr.color;
                c.a = 0.4f; // 半透明化
                sr.color = c;
            }
        }

        // 4. 一定時間の持続（無敵・半透明・残像ダッシュ状態の維持）
        float elapsed = 0f;
        float ghostSpawnTimer = 0f;
        float ghostInterval = 0.04f; // 残像を残す間隔

        try
        {
            while (elapsed < skillDuration)
            {
                float dt = Time.deltaTime;
                elapsed += dt;
                ghostSpawnTimer += dt;

                // 一定間隔ごとに自機の残像（ゴースト）を生成して配置
                if (ghostSpawnTimer >= ghostInterval)
                {
                    ghostSpawnTimer = 0f;
                    if (_rootOwner != null)
                    {
                        foreach (var sr in bodyRenderers)
                        {
                            if (sr != null && sr.sprite != null && sr.enabled)
                            {
                                GameObject ghostObj = new GameObject("PlayerGhostTrail");
                                ghostObj.transform.position = sr.transform.position;
                                ghostObj.transform.rotation = sr.transform.rotation;
                                ghostObj.transform.localScale = sr.transform.lossyScale;

                                SpriteRenderer ghostSR = ghostObj.AddComponent<SpriteRenderer>();
                                ghostSR.sprite = sr.sprite;
                                ghostSR.material = sr.material;
                                ghostSR.sortingLayerID = sr.sortingLayerID;
                                ghostSR.sortingOrder = sr.sortingOrder - 1;
                                ghostSR.color = new Color(0.7f, 0.85f, 1f, 0.4f); // 青白い半透明残像

                                ghostObj.AddComponent<BulletGhostFade>(); // 既存のフェード用ヘルパーを流用
                            }
                        }
                    }
                }

                yield return null;
            }
        }
        finally
        {
            // 5. 終了時の完全復元（速度・不透明度を元に戻す。無敵時間は playerMove 側で自動管理されます）
            if (myMove != null && !_isEXSkillActive)
            {
                myMove.skillSpeedMultiplier = 1.0f;
            }

            foreach (var kvp in originalColors)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.color = kvp.Value;
                }
            }

            _activeSkillCoroutines--;
            if (_activeSkillCoroutines < 0) _activeSkillCoroutines = 0;
        }
    }

    private IEnumerator ExecutePrideUltimateEX(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;
        _isEXSkillActive = true;
        PlaySkillSE(SEPath.LASER2);
        PlayEXAnimation(); // EX用アニメーションの再生

        PlayerMove myMove = _rootOwner != null ? _rootOwner.GetComponent<PlayerMove>() : GetComponentInParent<PlayerMove>();
        PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();

        // 詠唱・発射中は移動を大きく制限する
        if (myMove != null) myMove.skillSpeedMultiplier = 0.1f;

        // 🎯【方向固定の核心】：発動した瞬間の敵の方向を一度だけ取得して完全にロック！
        float fixedAimAngle = GetAngleToTarget(transform.position) + s.angleOffset - 60;

        // 1. 【フェーズ1：固定された予告線プレビューの生成】
        GameObject lineObj = new GameObject("EXLaserPreviewLine");
        LineRenderer previewLine = lineObj.AddComponent<LineRenderer>();
        previewLine.material = new Material(Shader.Find("Sprites/Default"));
        previewLine.startColor = new Color(1f, 0.3f, 1f, 0.7f); // 細い赤い予告線
        previewLine.endColor = new Color(1f, 0.3f, 1f, 0.7f);
        previewLine.startWidth = 0.04f;
        previewLine.endWidth = 0.04f;
        previewLine.positionCount = 2;

        float warningDuration = 0.8f; // 予告線を出す時間（秒）
        float warningElapsed = 0f;

        try
        {
            while (warningElapsed < warningDuration)
            {
                warningElapsed += Time.deltaTime;

                if (previewLine != null)
                {
                    previewLine.SetPosition(0, transform.position);
                    float rad = fixedAimAngle * Mathf.Deg2Rad;
                    Vector3 endpoint = transform.position + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * 30f;
                    previewLine.SetPosition(1, endpoint);
                }
                yield return null;
            }

            // 予告線オブジェクトを破棄
            if (lineObj != null) Destroy(lineObj);

            // 2. 【フェーズ2：固定された方向へ極太レーザーの実体化】
            PlaySkillSE(SEPath.LASER7); // 発射音

            bool isSpellActive = (myStatus != null && myStatus.isSpellCardActive);

            // 領域中なら左右に15度ずつ開いた状態（合計30度）からスタートし、最終的に中央（fixedAimAngle）へ収束する
            float[] startAngles = isSpellActive ? new float[] { fixedAimAngle - 15f, fixedAimAngle + 15f } : new float[] { fixedAimAngle };
            float[] finalAngles = isSpellActive ? new float[] { fixedAimAngle, fixedAimAngle } : new float[] { fixedAimAngle };

            for (int i = 0; i < startAngles.Length; i++)
            {
                GameObject beamObj = null;
                if (s.bulletData != null && s.bulletData.bulletPrefab != null)
                {
                    beamObj = Instantiate(s.bulletData.bulletPrefab, transform.position, Quaternion.identity);

                    PrideUltimateLaser laserScript = beamObj.GetComponent<PrideUltimateLaser>();
                    if (laserScript == null)
                    {
                        laserScript = beamObj.AddComponent<PrideUltimateLaser>();
                    }

                    float targetWidth = s.bulletData.bulletScale > 0f ? s.bulletData.bulletScale * 9.0f : 2.5f;
                    int laserDmg = s.bulletData.damage > 0 ? s.bulletData.damage : 30;

                    if (isSpellActive)
                    {
                        // 🌟 領域中：少しずつ閉まっていく（収束）専用の初期化を呼び出す（閉じるのにかかる時間は1.2秒に設定）
                        laserScript.InitializeWithConvergence(startAngles[i], finalAngles[i], targetWidth, transform, targetTag, laserDmg, 120.0f);
                    }
                    else
                    {
                        // 通常時：通常の初期化
                        laserScript.Initialize(startAngles[i], targetWidth, transform, targetTag, laserDmg);
                    }
                }
                else
                {
                    beamObj = new GameObject("DefaultEXBeam");
                    beamObj.transform.position = transform.position;
                }

                // 所属チームのタグとレイヤーを確実に適用
                int ownerId = (myStatus != null) ? myStatus.playerId : 1;
                string assignedTag = (ownerId == 1) ? "PlayerBullet" : "EnemyBullet";
                int assignedLayer = LayerMask.NameToLayer((ownerId == 1) ? "Player1Bullet" : "Player2Bullet");
                beamObj.tag = assignedTag;
                beamObj.layer = assignedLayer;
                SetLayerRecursive(beamObj, assignedLayer);
            }

            // 3. 【フェーズ3：レーザー発射中（約2.5秒＝150フレーム）画面を揺らしながらレーザー消滅を待つ】
            float laserDuration = 2.5f;
            float laserElapsed = 0f;

            while (laserElapsed < laserDuration)
            {
                laserElapsed += Time.deltaTime;

                if (CameraShake.Instance != null)
                {
                    CameraShake.Instance.Shake(0.15f, 0.15f);
                }

                yield return null;
            }

            // 4. クールダウン待機
            yield return new WaitForSeconds(s.cooldown);
        }
        finally
        {
            if (lineObj != null) Destroy(lineObj);

            _isEXSkillActive = false;
            if (myMove != null) myMove.skillSpeedMultiplier = 1.0f;
            _activeSkillCoroutines--;

            if (myStatus != null && myStatus.isSpellCardActive)
            {
                myStatus.DeactivateSpellCard(false);
            }
        }
    }
}