using KanKikuchi.AudioManager;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Emitter_Greed : PlayerDanmakuEmitter
{

    // ⚔️ カリンのZスキル＝「しの字」アーク一閃！
    protected override IEnumerator ExecuteSkillZ(PlayerSkillData.SkillSettings s)
    {
        Debug.Log("Executing Skill Z1");
        yield return StartCoroutine(ChainRandomAimRoutine(s));
    }

    // ⚔️ カリンのXスキル＝空間一閃・双極ブレード！
    protected override IEnumerator ExecuteSkillX(PlayerSkillData.SkillSettings s)
    {
        yield return StartCoroutine(RotatingAccelRoundRoutine(s));
    }

    // ⚔️ カリンのCスキル＝ブーメラン設置！
    protected override IEnumerator ExecuteSkillC(PlayerSkillData.SkillSettings s)
    {
        yield return StartCoroutine(RotatingAllWayLaserRoutine(s));
    }

    // ⚔️ カリンのVスキル＝防御フィールドチャージ！
    protected override IEnumerator ExecuteSkillV(PlayerSkillData.SkillSettings s)
    {
        yield return StartCoroutine(GreedTaxPossessionRoutine(s));
    }

    protected override IEnumerator ExecuteSkillEX(PlayerSkillData.SkillSettings s)
    {
        yield return StartCoroutine(ExecuteSyaruBackFormationSlashEXRoutine(s));
    }

    private IEnumerator ChainRandomAimRoutine(PlayerSkillData.SkillSettings s)
    {
        Debug.Log("Executing Skill Z2");
        _activeSkillCoroutines++; // 実行中カウントを増やす（エネルギー回復停止）

        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        PlayerMove myMove = GetComponentInParent<PlayerMove>();


        int burstCount = 6;
        int knivesway = s.count;

        PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
        bool isSpellActive = (myStatus != null && myStatus.isSpellCardActive);
        if (isSpellActive)
        {
            knivesway += 2;
        }
        // 1. スキル使用中の減速を適用
        if (myMove != null && !_isEXSkillActive) { myMove.skillSpeedMultiplier = s.moveSpeedMultiplier; }

        if (PlayerMove.CanShoot && (myHH == null || myHH.currentState == PlayerHitHandler.PlayerState.Normal))
        {
            // --- セット開始時の初期化 ---
            // 自機周辺のランダムな位置を弾源に設定
            float radius = 1.8f;
            Vector2 randomCircle = Random.insideUnitCircle.normalized * radius;
            Vector3 spawnPos = transform.position + new Vector3(randomCircle.x, randomCircle.y, 0);

            // ★ セット内で角度を固定：弾源から敵機への基本角度を一度だけ計算
            float targetAngle = GetAngleToTarget(spawnPos) + Random.Range(-1.5f, 1.5f);
            float baseAngle = targetAngle + s.angleOffset;

            // 規定回数（6回）を連射
            for (int i = 0; i < burstCount; i++)
            {
                // --- N-way（扇形）の生成ロジック ---
                int wayCount = Mathf.Max(1, knivesway); // 3way, 5wayなど
                float spread = s.wideAngle;

                if (wayCount <= 1)
                {
                    // 1-wayの場合は正面のみ
                    CreateShot(s.bulletData, spawnPos, s.speed, baseAngle, s.delay);
                }
                else
                {
                    // 複数wayの場合は扇形に展開
                    float startAngle = baseAngle - (spread / 2f);
                    float stepAngle = spread / (wayCount - 1);

                    for (int j = 0; j < wayCount; j++)
                    {
                        float finalAngle = startAngle + (stepAngle * j);
                        CreateShot(s.bulletData, spawnPos, s.speed, finalAngle, s.delay);
                    }
                }


                PlaySkillSE(s.sePath);
                // 2フレーム待機 (FixedUpdate 2回分)
                for (int j = 0; j < 7; j++)
                {
                    yield return new WaitForFixedUpdate();
                }
                // 被弾中断チェック
                if (!PlayerMove.CanShoot || (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal)) break;
            }
        }

        yield return new WaitForSeconds(s.cooldown);
        // 状態を戻す
        if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = 1.0f;
        _activeSkillCoroutines--;
    }


    // =========================================================================
    // 🪙 シャウル専用C：ローテーティング・オールウェイ結界レーザー
    // 💡 独自のInstantiateおよびバフの重複計算をパージし、中央EXインフラへ中継一本化！
    // =========================================================================
    private IEnumerator RotatingAllWayLaserRoutine(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;
        if (BulletManager.Instance == null) { _activeSkillCoroutines--; yield break; }

        List<EnemyLaserBeam> spawnedLasers = new List<EnemyLaserBeam>();
        int LaserWay = 24;
        PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
        bool isSpellActive = (myStatus != null && myStatus.isSpellCardActive);
        if (isSpellActive)
        {
            LaserWay = 48;
        }

        int laserCount = Mathf.Max(1, LaserWay);
        float radius = 1.6f;
        int stopFrame = 40;
        int warningFrame = stopFrame + 60;

        float rotDir = (Random.value < 0.5f) ? 1.0f : -1.0f;
        float initialRotSpeed = 5.0f * rotDir;
        float totalDriftAngle = 30f * rotDir;
        float driftVelocity = totalDriftAngle / stopFrame;
        float targetAngle = Random.Range(0f, 360f);

        float estimatedRotation = 245f * rotDir;
        float baseAngle = targetAngle - estimatedRotation;

        PlaySkillSE(s.sePath);
        // 🔄 24連/48連レーザーの一斉召喚インフラ
        for (int i = 0; i < laserCount; i++)
        {
            // 🛠️ 変更の核心：独自の Instantiate ✕ SetupB ✕ チーム・バフ処理を1行のコアインフラへ完全委託！
            EnemyLaserBeam laser = CreateLaserShot(s.bulletData, transform.position, s.speed, s.count, s.wideAngle, warningFrame, isSetupB: true);

            if (laser != null)
            {
                spawnedLasers.Add(laser);

                float currentStartAngle = baseAngle + (360f / laserCount * i);
                float aimOffset = 120f * rotDir;
                float initialLaserAngle = currentStartAngle + aimOffset;

                // データ1：段階的公転加速スピン開始
                laser.AddData(new EnemyLaserBeam.LaserTransformData
                {
                    frame = 0,
                    dist = radius,
                    distAngle = currentStartAngle,
                    laserAngle = initialLaserAngle,
                    distAngleVel = initialRotSpeed,
                    laserAngleVel = initialRotSpeed + driftVelocity,
                    isSmooth = true
                });

                // データ2：ジャストフレーム完全ロック静止
                laser.AddData(new EnemyLaserBeam.LaserTransformData
                {
                    frame = stopFrame,
                    distAngleVel = 0f,
                    laserAngleVel = 0f,
                    isSmooth = true
                });

                laser.Fire();
            }
        }

        yield return new WaitForSeconds((warningFrame / 60f) + s.speed);

        foreach (var laser in spawnedLasers)
        {
            if (laser != null) laser.ForceClose();
        }

        _activeSkillCoroutines--;
    }

    private bool _isRoundRotReversed = false; // ★ 追加：全方位弾の回転方向反転用フラグ
    /// <summary>
    /// 自機外し全方位弾を、射角を回転させ、段階的に弾速を上げながら連射する
    /// 1回使うごとに回転方向が交互に反転する
    /// </summary>
    // 📄 PlayerDanmakuEmitter.cs 内の RotatingAccelRoundRoutine メソッド【領域展開・弾数4増量変調版】
    // 📄 PlayerDanmakuEmitter.cs 内の RotatingAccelRoundRoutine メソッド【領域展開・弾数4増量・オーラ完全溶接版】
    private IEnumerator RotatingAccelRoundRoutine(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++; // 実行中カウント（MP回復停止）
        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>(); //
        PlayerMove myMove = GetComponentInParent<PlayerMove>(); //
        float addan = 12;
        if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = s.moveSpeedMultiplier; //

        Vector3 pos = transform.position; //

        // =========================================================================
        // 🔮【新設：領域展開連動・4極アレイ拡張マトリクス】
        // =========================================================================
        // 大元の所有者から現在の領域展開（スペルカード）ステートをチェック
        PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
        bool isSpellActive = (myStatus != null && myStatus.isSpellCardActive);

        // 領域展開中であればベースの数（s.count）から「4」を動的に加算！
        int baseBulletCount = s.count;
        if (isSpellActive)
        {
            baseBulletCount += 4;
        }

        // 1. 1波あたりの弾数を設定（偶数丸め処理）
        int bulletCount = Mathf.Max(2, baseBulletCount); //
        if (bulletCount % 2 != 0) bulletCount++; //

        float step = 360f / bulletCount; //
        float evenWayOffset = step / 2f; //

        // 2. 連射設定と ★回転方向の交互反転ロジック
        int waveLoops = 12; //
        float currentSpeed = s.speed; // 初速（インスペクターのSpeed）

        // ★ 現在の状態を取得し、フラグを反転させて次回に備える
        bool currentRotReversed = _isRoundRotReversed; //
        _isRoundRotReversed = !_isRoundRotReversed; //

        // フラグに応じて回転方向を 1.0 または -1.0 にする
        float rotDirection = currentRotReversed ? -1f : 1f; //
        float angleIncrement = addan * rotDirection; // ★ 1波ごとの回転角の向きを決定

        // 射撃開始時のターゲットへの基本角度を算出
        float targetAngle = GetAngleToTarget(); //
        float baseAngle = targetAngle + s.angleOffset + evenWayOffset; //

        // 3. バースト連射ループ
        for (int w = 0; w < waveLoops; w++)
        {
            // 被弾時やラウンド終了時の安全ガード
            if (!PlayerMove.CanShoot || (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal)) break; //

            PlaySkillSE(s.sePath); //

            // 1波分（全方位）の弾を生成
            // 1波分（全方位）の弾を生成
            for (int i = 0; i < bulletCount; i++)
            {
                // ベース角 + 全方位分割角 + wによる回転量を加算
                float finalAngle = baseAngle + (step * i) + (angleIncrement * w); //

                // =========================================================================
                // 🎯【バグ修正】：固定値 1.0f だった delay を本来の設定値である s.delay に修正
                // =========================================================================
                CreateShot(s.bulletData, pos, currentSpeed, finalAngle, delay: s.delay); //[cite: 18]
            }

            // 次の波の弾速を上げる（段階的加速）
            currentSpeed += 0.5f; //

            // 波と波の間の時間差（3フレーム待機）
            for (int f = 0; f < 3; f++) //
            {
                yield return new WaitForFixedUpdate(); //
            }
        }

        yield return new WaitForSeconds(s.cooldown);
        if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = 1.0f; //
        _activeSkillCoroutines--; //
    }


    // 📄 PlayerDanmakuEmitter.cs 内の強欲カウンター制御セクター【領域展開・性能4冠ブースト版】
    private IEnumerator GreedTaxPossessionRoutine(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++; //

        PlayerMove myMove = _rootOwner.GetComponent<PlayerMove>(); //
        if (myMove != null && !_isEXSkillActive && s.moveSpeedMultiplier < 1.0f) //
        {
            myMove.skillSpeedMultiplier = s.moveSpeedMultiplier; //
        }

        PlaySkillSE(s.sePath); //

        // 1. スキルデータに登録された「フィールドプレハブ」を生成
        GameObject fieldObj = Instantiate(s.bulletData.bulletPrefab, transform.position, Quaternion.identity); //

        // 2. 所属チームに応じたタグとレイヤーを生成の瞬間に割り当てる
        var myStatus = GetComponentInParent<PlayerStatusManager>(); //
        int ownerId = (myStatus != null) ? myStatus.playerId : 1; //

        string assignedTag = (ownerId == 1) ? "PlayerBullet" : "EnemyBullet"; //
        int assignedLayer = LayerMask.NameToLayer((ownerId == 1) ? "Player1Bullet" : "Player2Bullet"); //

        fieldObj.tag = assignedTag; //
        fieldObj.layer = assignedLayer; //
        SetLayerRecursive(fieldObj, assignedLayer); //

        // 3. プレハブにあらかじめ付いている GreedTaxPossessionField コンポーネントを取得
        GreedTaxPossessionField fieldLogic = fieldObj.GetComponent<GreedTaxPossessionField>(); //

        if (fieldLogic != null)
        {
            // 💡 4. 領域展開中（スペルカードアクティブ）のフラグを上流インフラから安全に取得
            bool isSpellActive = (myStatus != null && myStatus.isSpellCardActive);

            // 💡 5. 【高橋さんの指定】：通常値と領域展開中のパラメータを完全に仕分け
            float targetDuration = 1.5f;     // 通常時の持続時間（秒）
            float targetScaleMultiplier = 1f; // 通常時のスケール等倍
            float targetKnifeSpeed = 6f;   // 通常時の反射カウンター弾速
            float targetEnergyGain = 1.5f;   // 通常時の1発あたりゲージ回復量

            if (isSpellActive)
            {
                // 🎯 領域展開中：性能4冠の一挙極大ブーストを執行！
                targetDuration = 3.0f;        // ⏰ 持続時間を「3.0秒」へ延長（2倍）
                targetScaleMultiplier = 1.3f; // 📐 フィールドの大きさを「1.8倍」へ巨大化
                targetKnifeSpeed = 9.0f;      // ⚡ 反射カウンター弾速を「7.0f」へ高速化
                targetEnergyGain = 0f;      // 🪙 ゲージ回復量を「3.0f」へ倍増
                Debug.Log($"<color=orange>🪙【領域展開・強欲の重税】魔方陣フィールド強化：Duration:{targetDuration}s, Scale:{targetScaleMultiplier}x, KnifeSpeed:{targetKnifeSpeed}, EnergyGain:{targetEnergyGain}</color>");
            }

            // 💡 6. 拡張された窓口へ変調パラメータを安全にインジェクション！
            fieldLogic.Initialize(transform, _rootOwner, targetTag, this, targetDuration, targetScaleMultiplier, targetKnifeSpeed, targetEnergyGain);

            // 💡 7. 【タイムライン完全同期】：フィールドの稼働時間（持続秒数 ＋ 拡縮演出0.2秒）に正確に一致させてEmitter側も待機！
            yield return new WaitForSeconds(targetDuration + 0.2f);
        }
        else
        {
            Debug.LogError("フィールド用プレハブに GreedTaxPossessionField が付いていません！"); //
            yield return new WaitForSeconds(1.5f + 0.2f);
        }

        if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = 1.0f; //
        _activeSkillCoroutines--; //
    }
    // =========================================================================
    // 🔮【新設・カリン／シャウル専用EX】：使い魔ビット魔方陣・アタッチメント召喚エンジン
    // =========================================================================
    /// <summary>
    /// ⚔️ 新EXスキル：背後追従型・四連/六連魔方陣・使い魔ビット独立アサルト
    /// 🌟 【領域展開・6個変調＆1.5倍振幅適合版】：
    /// 🌟 通常時は4枚の魔方陣が背後を美しく2往復対称クロススライド。
    /// 🌟 領域展開（スペルカードアクティブ）中は、自動で【6枚アレイ仕様】へとポリモーフィック進化！
    /// 🌟 縦の敷設オフセットも6本仕様に自動拡張し、子弾ビットへ領域コンテキストを安全インジェクションします。
    /// </summary>
    public IEnumerator ExecuteSyaruBackFormationSlashEXRoutine(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;
        PlayerMove myMove = _rootOwner.GetComponent<PlayerMove>();

        // 🎯 術式発動の瞬間、自機の移動速度を確実に「30% (0.3f)」へクランプロック！
        _isEXSkillActive = true;
        if (myMove != null) myMove.skillSpeedMultiplier = 0.3f;

        // 🎯 1. ターゲットの左右極性を精密測定
        float targetAngle = GetAngleToTarget(transform.position);
        bool isEnemyOnRightSide = (targetAngle > -90f && targetAngle <= 90f);

        float shootAngle = isEnemyOnRightSide ? 0f : 180f;
        float behindOffsetX = isEnemyOnRightSide ? -1.2f : 1.2f;

        // 💡 領域展開中（スペルカードアクティブ）のステートを上流インフラから安全に取得
        PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
        bool isZoneActive = (myStatus != null && myStatus.isSpellCardActive);

        // =========================================================================
        // 🎯【数理空間の動的ポリモーフィズム】：通常時4本 ➔ 領域展開中6本へのアレイ拡張
        // =========================================================================
        // 💡 領域展開中は、画面の上下限界をさらに制圧するために高度を「-2.5f 〜 +2.5f」の6本仕様マトリクスへ自動増設！
        float[] formationYOffsets = isZoneActive
            ? new float[] { -2.5f, -1.5f, -0.5f, 0.5f, 1.5f, 2.5f }
            : new float[] { -1.5f, -0.5f, 0.5f, 1.5f };

        // 💡 技の開幕演出の迫力を引き立てるSEを重奏
        SEManager.Instance.Play(SEPath.SLASH, 0.3f);
        SEManager.Instance.Play(SEPath.LASER7, 0.3f);

        int ownerId = (myStatus != null) ? myStatus.playerId : 1;

        // =========================================================================
        // 🔮【データ駆動実体化】：s.bulletData.bulletPrefab（共通魔方陣）を一斉召喚！
        // =========================================================================
        for (int i = 0; i < formationYOffsets.Length; i++)
        {
            Vector3 spawnPos = transform.position + new Vector3(behindOffsetX, formationYOffsets[i], 0f);

            GameObject portalBitObj = Instantiate(s.bulletData.bulletPrefab, spawnPos, Quaternion.identity);

            string assignedTag = (ownerId == 1) ? "PlayerBullet" : "EnemyBullet";
            string assignedLayer = (ownerId == 1) ? "Player1Bullet" : "Player2Bullet";

            portalBitObj.tag = assignedTag;
            portalBitObj.layer = LayerMask.NameToLayer(assignedLayer);
            SetLayerRecursive(portalBitObj, LayerMask.NameToLayer(assignedLayer));

            PortalBitObject bitLogic = portalBitObj.GetComponent<PortalBitObject>();
            if (bitLogic == null) bitLogic = portalBitObj.AddComponent<PortalBitObject>();

            // 💡 連射持続時間は2.5秒を維持ホールド
            bitLogic.Initialize(transform, s, behindOffsetX, formationYOffsets[i], shootAngle, 2.5f, 4, this);
        }

        // =========================================================================
        // ⏳ タイムライン完全同期ホールド（2.9秒）
        // =========================================================================
        yield return new WaitForSeconds(2.9f);

        _isEXSkillActive = false;
        if (myMove != null) myMove.skillSpeedMultiplier = 1.0f;
        _activeSkillCoroutines--;

        if (myStatus != null && myStatus.isSpellCardActive)
        {
            myStatus.DeactivateSpellCard(false);
        }

    }
    public float ExecuteGetAngleToTargetBridge()
    {
        return GetAngleToTarget(transform.position);
    }
    public void ExecuteSubShotFromPortal(BulletData data, Vector3 pos, float speed, float angle, float delay)
    {
        CreateShot(data, pos, speed, angle, delay);
    }
}
