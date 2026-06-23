using KanKikuchi.AudioManager;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.AppUI.UI;
using UnityEngine;
// 🔥【これを追加】：このファイル内で単に「Random」と書いたらUnity側を優先する、という絶対命令
using Random = UnityEngine.Random;
/// <summary>
/// プレイヤーのスキル設定に基づき、実際に弾幕を生成・射出するクラス
/// 1vs1対戦対応：奇数弾は自機狙い、偶数弾は自機外しを自動計算
/// </summary>
public class PlayerDanmakuEmitter : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("攻撃対象（相手）のタグ")]
    public string targetTag = "Player";

    private GameObject _rootOwner;
    private bool _isArcReversed = false;
    // 現在アクティブなコルーチンの数をカウント[cite: 7]
    private int _activeSkillCoroutines = 0;
    private bool _isXLineReversed; // ⚔️ カリン専用Xの往復切り替え用フラグ
    // スキル使用中（コルーチンが1つ以上動いている）かどうかを返すプロパティ
    public bool IsAnySkillActive => _activeSkillCoroutines > 0;
    // 🎯【共用一本化】：EXスキル（ULT）が現在絶賛稼働中であることを示す唯一の絶対フラグ
    private bool _isEXSkillActive = false;

    // 🎯【外部公開用プロパティ】：PlayerStatusManagerが無敵やタイマーストップを判定するために、この共用フラグを公開します
    public bool IsUltimateSkillActive => _isEXSkillActive;
    public bool IsSyaruBitEXActive => _isEXSkillActive; // 他の通常スキルが参照している場合の互換性維持

    // 📊【新設】：サイズごとのソーティングオーダー分配・ループ管理カウンター
    private static int _smallOrderCounter = 5000;
    private static int _mediumOrderCounter = 10000;
    private static int _largeOrderCounter = 15000;

    /// <summary>
    /// 💡 弾のサイズデータに基づいて次のソーティングオーダーを安全に算出してループさせます
    /// </summary>
    private int AllocateNextSortingOrder(BulletSize size)
    {
        switch (size)
        {
            case BulletSize.Small:
                _smallOrderCounter++;
                if (_smallOrderCounter > 20000) _smallOrderCounter = 15000;
                return _smallOrderCounter;

            case BulletSize.Medium:
                _mediumOrderCounter++;
                if (_mediumOrderCounter > 15000) _mediumOrderCounter = 10000;
                return _mediumOrderCounter;

            case BulletSize.Large:
                _largeOrderCounter++;
                if (_largeOrderCounter > 10000) _largeOrderCounter = 5000;
                return _largeOrderCounter;

            default:
                return 1000;
        }
    }

    private void Awake()
    {
        _rootOwner = transform.root.gameObject;
    }

    private float GetAngleToTarget()
    {
        Transform target = null;
        foreach (var p in PlayerMove.AllPlayers)
        {
            if (p != null && p.gameObject != _rootOwner)
            {
                target = p.transform;
                break;
            }
        }
        if (target != null)
        {
            Vector3 dir = target.position - transform.position;
            return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        }
        var myStatus = GetComponentInParent<PlayerStatusManager>();
        if (myStatus != null && myStatus.playerId == 2) return 180f;
        return 0f;
    }

    private float GetAngleToTarget(Vector3 fromPos)
    {
        Transform target = null;
        foreach (var p in PlayerMove.AllPlayers)
        {
            if (p != null && p.gameObject != _rootOwner)
            {
                target = p.transform;
                break;
            }
        }
        if (target != null)
        {
            Vector3 dir = target.position - fromPos;
            return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        }
        var myStatus = GetComponentInParent<PlayerStatusManager>();
        if (myStatus != null && myStatus.playerId == 2) return 180f;
        return 0f;
    }

    /// <summary>
    /// スキル設定に基づき、弾幕を生成・射出するメインエントランス
    /// </summary>
    public void Fire(PlayerSkillData.SkillSettings s)
    {
        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        if (!PlayerMove.CanShoot) return;
        if (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal) return;
        if (s.bulletData == null || s.bulletData.bulletPrefab == null) return;
        if (_isEXSkillActive && s.patternType != SkillPatternType.Line) return;
        PlayerMove myMove = _rootOwner.GetComponent<PlayerMove>();
        if (myMove != null)
        {
            float finalGain = s.ultimateGain;
            PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
            if (myStatus != null && myStatus.isOverheated)
            {
                finalGain *= 0.5f;
            }
            myMove.AddUltimateEnergy(finalGain);
        }

        if (s.patternType != SkillPatternType.MovingArc &&
            s.patternType != SkillPatternType.RandomRound &&
            s.patternType != SkillPatternType.DefensiveField)
        {
            PlaySkillSE(s.sePath);
        }

        // 🎯【速度干渉ガード】：魔方陣EXの発動中は、通常スキルの TemporarySlow の起動自体を完全カット！
        if (s.patternType != SkillPatternType.DefensiveField &&
            s.patternType != SkillPatternType.MovingArc &&
            !_isEXSkillActive && // 💡魔方陣EXが動いていない平和な時だけ通常減速を許可
            s.moveSpeedMultiplier < 1.0f)
        {
            StartCoroutine(TemporarySlow(s.moveSpeedMultiplier, 0.2f));
        }

        float targetAngle = GetAngleToTarget();
        float baseAngle = targetAngle + s.angleOffset;
        Vector3 pos = transform.position;

        PlayerStatusManager emitterStatus = GetComponentInParent<PlayerStatusManager>();
        if (emitterStatus != null && emitterStatus.isSpellCardActive)
        {
            PlayerSkillData.SkillSettings enhancedSettings = s;
            if (enhancedSettings.patternType == SkillPatternType.KarinScalesSlash)
            {
                enhancedSettings.count = 1;
                s = enhancedSettings;
                StartCoroutine(ExecuteKarinTripleScalesSlashRoutine(s));
                return;
            }
            else if (enhancedSettings.patternType == SkillPatternType.KarinFireSlash)
            {
                // 🔥【大修復】：領域展開中（KarinFireSlash）の時も、本来の一閃コルーチンを確実にキックして即returnさせる！
                StartCoroutine(ExecuteKarinCrossSlashRoutine(s));
                return;
            }
            else
            {
                enhancedSettings.speed = s.speed * 1.3f;
                s = enhancedSettings;
            }
        }

        switch (s.patternType)
        {
            case SkillPatternType.Standard:
                if (s.bulletData.isLaser) StartCoroutine(LaserRoutine(s, false));
                else CreateShot(s.bulletData, pos, s.speed, baseAngle, s.delay);
                break;
            case SkillPatternType.nWay:
                ExecuteNWay(s, pos, baseAngle);
                break;
            case SkillPatternType.Round:
                ExecuteRound(s, pos, baseAngle);
                break;
            case SkillPatternType.Polygon:
                ExecutePolygon(s, pos, baseAngle);
                break;
            case SkillPatternType.Line:
                break;
            case SkillPatternType.Custom:
                ExecuteConvergePattern(s, pos, baseAngle);
                break;
            case SkillPatternType.MovingArc:
                StartCoroutine(MovingArcRoutine(s));
                break;
            case SkillPatternType.RandomRound:
                StartCoroutine(ExecuteRandomRoundRoutine(s));
                break;
            case SkillPatternType.Boomerang:
                StartCoroutine(ShootBoomerangRoutine(s));
                break;
            case SkillPatternType.DefensiveField:
                StartCoroutine(ChargeAndExecuteDefensiveField(s));
                break;
            case SkillPatternType.ChainRandomAim:
                StartCoroutine(ChainRandomAimRoutine(s));
                break;
            case SkillPatternType.RotatingAllWayLaser:
                StartCoroutine(RotatingAllWayLaserRoutine(s));
                break;
            case SkillPatternType.RotatingAccelRound:
                StartCoroutine(RotatingAccelRoundRoutine(s));
                break;
            case SkillPatternType.GreedTaxPossession:
                StartCoroutine(GreedTaxPossessionRoutine(s));
                break;
            case SkillPatternType.KarinScalesSlash:
                StartCoroutine(ExecuteKarinScalesSlashRoutine(s));
                break;
            case SkillPatternType.KarinFireSlash:
                StartCoroutine(ExecuteKarinCrossSlashRoutine(s));
                break;
            case SkillPatternType.Saiki:
                StartCoroutine(ExecuteExpandingRingZeroSpeedRoutine(s));
                break;
            // (既存の case 達の下に追加)
            case SkillPatternType.KunaiCage: // 必要に応じて新しいSkillPatternTypeをenumに定義するか、既存のテスト用枠に流し込んでください
                StartCoroutine(ExecuteEnemyEnclosureShrinkingRingRoutine(s));
                break;
            case SkillPatternType.HeartRef: // テスト用にCustom枠で起動するか、新enumをケースにしてください
                StartCoroutine(ExecuteBouncingTrailShotRoutine(s));
                break;
        }
    }

    /// <summary>
    /// 独立したEX枠のデータを受け取り、パターン（s.patternType）に応じて固有の必殺技をキックする
    /// </summary>
    public void FireEX(PlayerSkillData.SkillSettings s)
    {
        if (!PlayerMove.CanShoot) return;

        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        if (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal) return;

        // 🎯【高橋さんの指定：魔方陣EX限定・重ね撃ち完全拒絶ロック】：
        // 💡 この大技自身が現在すでに稼働中（_isSyaruBitEXActive = true）なら、EXボタン連打の多重入力を完全に遮断！
        if (s.patternType == SkillPatternType.Line && _isEXSkillActive) return;

        // 🌟 共通インフラ（硬直制御・例外安全ライフサイクル）を開始
        StartCoroutine(ExecuteEXInfrastructureRoutine(s));
    }



    // =========================================================================
    // 🔮【完全復旧版】：固定弾源・再帰的半径拡大 ✕ 10秒/半径30終期収束ライフサイクルコルーチン
    // =========================================================================
    private IEnumerator ExecuteExpandingRingZeroSpeedRoutine(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++; // 🟢 マナの自動回復を一時停止

        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        PlayerMove myMove = GetComponentInParent<PlayerMove>();

        if (myMove != null && !_isEXSkillActive)
        {
            myMove.skillSpeedMultiplier = s.moveSpeedMultiplier;
        }

        // --- 🛡️ try-finally インフラにより、どんな異常中断が起きてもマナ回復を絶対保証 🛡️ ---
        try
        {
            // --- パラメータの設定と初期化 ---
            int wayCount = Mathf.Max(1, s.count);   // インスペクターの Count 枠を「way数」として直撃バインド
            float currentRadius = 0.5f;             // 開始時の初期半径（ここからスタート）
            float radiusStep = 0.1f;                // 1波ごとに外側へ広がる半径の拡張幅

            float angularVelocityStep = s.angleOffset;

            // 💡 弾自体の寿命秒数：インスペクターの Speed 枠の数値をそのまま流用（設定がなければ3.0秒）
            float bulletLifeTime = (s.speed > 0f) ? s.speed : 3.0f;

            float targetCenterAngle = GetAngleToTarget(transform.position);
            Vector3 centerOriginPos = transform.position; // 発射を開始した瞬間の自機の中心座標を固定（弾源の核）

            float elapsedTimer = 0f; // 弾源の持続時間計測タイマー
            int waveCount = 0;       // ウェーブ数のカウント

            // ⏳ 弾源の寿命監査：30秒経過するか、半径が30を超えたら自動で完全消滅
            while (elapsedTimer < 30f && currentRadius <= 30f)
            {
                // 💡 被弾等でループをブレーク（中断）しても、finallyブロックにジャンプして安全にMPが回復し始めます！
                if (!PlayerMove.CanShoot || (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal))
                    break;

                PlaySkillSE(s.sePath);

                float currentRotationOffset = angularVelocityStep * waveCount;
                float baseAngle = targetCenterAngle + currentRotationOffset;

                float startAngle;
                float stepAngle;

                if (s.wideAngle <= 0f || s.wideAngle >= 360f)
                {
                    stepAngle = 360f / wayCount;
                    startAngle = baseAngle;
                }
                else
                {
                    stepAngle = (wayCount > 1) ? s.wideAngle / (wayCount - 1) : 0f;
                    startAngle = baseAngle - (s.wideAngle / 2f);
                }

                for (int i = 0; i < wayCount; i++)
                {
                    float finalPlacementAngle = startAngle + (stepAngle * i);
                    float rad = finalPlacementAngle * Mathf.Deg2Rad;

                    Vector3 spawnPos = centerOriginPos + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * currentRadius;

                    // 1. 通常通りクローンデータを生成して実体化
                    BulletData runtimeData = Instantiate(s.bulletData);

                    // 🛡️ エディタ用永続化パージを溶接
                    runtimeData.hideFlags = HideFlags.DontSave;

                    // 大元の所有者バフ計算ルーチン
                    PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
                    if (myStatus == null && _rootOwner != null) myStatus = _rootOwner.GetComponent<PlayerStatusManager>();
                    int ownerId = (myStatus != null) ? myStatus.playerId : 1;

                    if (myStatus != null && myStatus.characterData != null)
                    {
                        float atkMultiplier = 1.0f;
                        switch (myStatus.characterData.rankAttack)
                        {
                            case StatusRank.E: atkMultiplier = 0.8f; break;
                            case StatusRank.D: atkMultiplier = 0.9f; break;
                            case StatusRank.C: atkMultiplier = 1.0f; break;
                            case StatusRank.B: atkMultiplier = 1.1f; break;
                            case StatusRank.A: atkMultiplier = 1.2f; break;
                            case StatusRank.EX: atkMultiplier = 1.3f; break;
                        }
                        if (myStatus.IsAttackBoostActive) atkMultiplier *= 1.3f;
                        atkMultiplier *= myStatus.GetJealousyMultiplier();
                        runtimeData.damage = Mathf.RoundToInt(runtimeData.damage * atkMultiplier);
                    }

                    // 2. オブジェクトプール、または新規生成からオブジェクトを取得
                    GameObject obj = (BulletPool.Instance != null && runtimeData.bulletPrefab != null)
                        ? BulletPool.Instance.Get(runtimeData.bulletPrefab, spawnPos, Quaternion.identity)
                        : Instantiate(runtimeData.bulletPrefab, spawnPos, Quaternion.identity);

                    // オーラインフラの自動付製（既存を踏襲）
                    SpriteRenderer mainSR = obj.GetComponentInChildren<SpriteRenderer>();
                    if (mainSR != null && obj.transform.Find("PureColorAuraObject") == null)
                    {
                        GameObject auraChild = new GameObject("PureColorAuraObject");
                        auraChild.transform.SetParent(obj.transform);
                        auraChild.transform.localPosition = Vector3.zero;
                        auraChild.transform.localRotation = Quaternion.identity;
                        auraChild.transform.localScale = new Vector3(1.4f, 1.4f, 1.0f);
                        SpriteRenderer auraSR = auraChild.AddComponent<SpriteRenderer>();
                        auraSR.sortingLayerID = mainSR.sortingLayerID;
                        auraSR.sortingOrder = mainSR.sortingOrder - 1;

                        if (runtimeData.auraMaterial != null) auraSR.material = runtimeData.auraMaterial;
                        else
                        {
                            Material dynMaterial = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
                            dynMaterial.hideFlags = HideFlags.DontSave; // 🛡️ アサーションエラー完全対策
                            auraSR.material = dynMaterial;
                        }

                        auraSR.sprite = (runtimeData.auraWhiteSprite != null) ? runtimeData.auraWhiteSprite : mainSR.sprite;
                        if (myStatus != null && myStatus.characterData != null) { Color c = myStatus.characterData.imageColor; c.a = 1.0f; auraSR.color = c; }
                        else { Color c = (ownerId == 1) ? Color.cyan : Color.red; c.a = 1.0f; auraSR.color = c; }
                    }

                    string assignedTag = (ownerId == 1) ? "PlayerBullet" : "EnemyBullet";
                    obj.tag = assignedTag;
                    int assignedLayer = LayerMask.NameToLayer((ownerId == 1) ? "Player1Bullet" : "Player2Bullet");
                    obj.layer = assignedLayer;
                    SetLayerRecursive(obj, assignedLayer);

                    DanmakuBullet bullet = obj.GetComponent<DanmakuBullet>();
                    if (bullet != null)
                    {
                        bullet.Initialize(_rootOwner, targetTag, 0f, finalPlacementAngle, 0, 0f, 0, s.delay, runtimeData, false);
                        bullet.StartSelfDestructTimer(bulletLifeTime); // 💡弾独自の自爆タイマー
                    }
                }

                currentRadius += radiusStep;
                waveCount++;

                const float intervalDuration = 1f / 60f;
                yield return new WaitForSeconds(intervalDuration);
                elapsedTimer += intervalDuration;
            }

            // 🟢 ループが正常に終了した後のスキルクールダウン待機
            yield return new WaitForSeconds(s.cooldown);
        }
        finally
        {
            // =========================================================================
            // 🚨【絶対復旧インフラ】：どのような経路でコルーチンが終了・破棄されても、
            //                        100%確実にカウントを引き下げてコスト自動回復を即座に再開！
            // =========================================================================
            if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = 1.0f;
            _activeSkillCoroutines--; // 🔄 これでコストが完全回復するようになります！
        }
    }
    private List<DanmakuBullet> _activeCageBullets = new List<DanmakuBullet>();
    // =========================================================================
    // 🔮【確定版】：位置固定・早期コスト解放 ✕ 再設置時古い檻強制クリア型コルーチン
    // =========================================================================
    private IEnumerator ExecuteEnemyEnclosureShrinkingRingRoutine(PlayerSkillData.SkillSettings s)
    {
        // 🚨【再設置セーフティ】：もし前回の檻の弾幕がまだ画面上に残っている場合、
        //                          新しい檻を展開する前に、古い弾をその場ですべて一斉に美しく破壊・回収します！
        if (_activeCageBullets != null && _activeCageBullets.Count > 0)
        {
            for (int b = _activeCageBullets.Count - 1; b >= 0; b--)
            {
                DanmakuBullet oldBullet = _activeCageBullets[b];
                // まだプールに戻らず生きている弾だけをピンポイント爆破
                if (oldBullet != null && oldBullet.gameObject.activeSelf)
                {
                    oldBullet.Deactivate(true); // ガラス破砕エフェクトを伴ってプールへ強制返却
                }
            }
            _activeCageBullets.Clear(); // 古いリストを更地にする
        }

        _activeSkillCoroutines++; // 🟢 檻の「生成・設置中」のみマナの自動回復を一時停止

        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        PlayerMove myMove = GetComponentInParent<PlayerMove>();

        if (myMove != null && !_isEXSkillActive)
        {
            myMove.skillSpeedMultiplier = s.moveSpeedMultiplier;
        }

        // 💡 リアルタイムに公転同期させるための動的マトリクスレイヤー
        List<List<Tuple<Transform, float>>> layersBulletMatrix = new List<List<Tuple<Transform, float>>>();
        bool isGenerationFinished = false; // 5層すべての敷設が完了したかを示す内部フラグ

        try
        {
            // 🎯 1. 攻撃対象（敵プレイヤー）の発動瞬間の現在地をロックオン
            Transform targetEnemyTransform = null;
            foreach (var p in PlayerMove.AllPlayers)
            {
                if (p != null && p.gameObject != _rootOwner)
                {
                    targetEnemyTransform = p.transform;
                    break;
                }
            }
            Vector3 enemyCenterPos = (targetEnemyTransform != null) ? targetEnemyTransform.position : Vector3.zero;

            int wayCount = Mathf.Max(4, s.count);
            float[] layersRadius = new float[] { 2.0f, 2.5f, 3.0f, 3.5f, 4.0f };
            int totalLayers = layersRadius.Length;

            float bulletLifeTime = (s.speed > 0f) ? s.speed : 5.0f;
            float baseRotateSpeed = (s.angleOffset != 0f) ? s.angleOffset : 60f;

            float currentElapsed = 0f;
            int nextLayerToSpawn = 0;
            float spawnTimer = 0f;
            float spawnInterval = 5f / 60f;        // 5フレームおき

            // =========================================================================
            // 🔄 統合リアルタイムメイン駆動ループ
            // =========================================================================
            while (currentElapsed < bulletLifeTime || layersBulletMatrix.Count < totalLayers)
            {
                yield return new WaitForFixedUpdate();
                float dt = Time.fixedDeltaTime;
                currentElapsed += dt;
                spawnTimer += dt;

                // 被弾スタン時などの安全ブレーク
                if (!PlayerMove.CanShoot || (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal))
                    break;

                // 🚀【5フレームごとの段階発生処理】
                if (nextLayerToSpawn < totalLayers && (nextLayerToSpawn == 0 || spawnTimer >= spawnInterval))
                {
                    spawnTimer = 0f;
                    int layer = nextLayerToSpawn;
                    float radius = layersRadius[layer];
                    float stepAngle = 360f / wayCount;
                    float layerStartAngleOffset = layer * (stepAngle * 0.3f);

                    List<Tuple<Transform, float>> currentLayerList = new List<Tuple<Transform, float>>();
                    PlaySkillSE(s.sePath);

                    for (int i = 0; i < wayCount; i++)
                    {
                        float initPlacementAngle = (stepAngle * i) + layerStartAngleOffset;
                        float rad = initPlacementAngle * Mathf.Deg2Rad;

                        Vector3 spawnPos = enemyCenterPos + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * radius;
                        float faceCenterAngle = initPlacementAngle + 180f;

                        BulletData runtimeData = Instantiate(s.bulletData);
                        runtimeData.hideFlags = HideFlags.DontSave;

                        PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
                        if (myStatus == null && _rootOwner != null) myStatus = _rootOwner.GetComponent<PlayerStatusManager>();
                        int ownerId = (myStatus != null) ? myStatus.playerId : 1;

                        if (myStatus != null && myStatus.characterData != null)
                        {
                            float atkMultiplier = 1.0f;
                            switch (myStatus.characterData.rankAttack)
                            {
                                case StatusRank.E: atkMultiplier = 0.8f; break;
                                case StatusRank.D: atkMultiplier = 0.9f; break;
                                case StatusRank.C: atkMultiplier = 1.0f; break;
                                case StatusRank.B: atkMultiplier = 1.1f; break;
                                case StatusRank.A: atkMultiplier = 1.2f; break;
                                case StatusRank.EX: atkMultiplier = 1.3f; break;
                            }
                            if (myStatus.IsAttackBoostActive) atkMultiplier *= 1.3f;
                            runtimeData.damage = Mathf.RoundToInt(runtimeData.damage * atkMultiplier * myStatus.GetJealousyMultiplier());
                        }

                        GameObject obj = (BulletPool.Instance != null && runtimeData.bulletPrefab != null)
                            ? BulletPool.Instance.Get(runtimeData.bulletPrefab, spawnPos, Quaternion.identity)
                            : Instantiate(runtimeData.bulletPrefab, spawnPos, Quaternion.identity);
                        // 📊 ★【追加】：檻のクナイもサイズを読み取ってレイヤー自動バインド！
                        SpriteRenderer mainSR = obj.GetComponentInChildren<SpriteRenderer>();

                        if (mainSR != null)
                        {
                            Transform auraChildTransform = obj.transform.Find("PureColorAuraObject");
                            GameObject auraChildObj; SpriteRenderer auraSR;
                            if (auraChildTransform == null)
                            {
                                auraChildObj = new GameObject("PureColorAuraObject"); auraChildObj.transform.SetParent(obj.transform);
                                auraChildObj.transform.localPosition = Vector3.zero; auraChildObj.transform.localScale = new Vector3(1.4f, 1.4f, 1.0f);
                                auraSR = auraChildObj.AddComponent<SpriteRenderer>();
                            }
                            else
                            {
                                auraChildObj = auraChildTransform.gameObject; auraSR = auraChildObj.GetComponent<SpriteRenderer>();
                            }
                            auraSR.sortingLayerID = mainSR.sortingLayerID; auraSR.sortingOrder = mainSR.sortingOrder - 1;
                            if (runtimeData.auraMaterial != null) auraSR.material = runtimeData.auraMaterial;
                            else { Material dm = new Material(Shader.Find("Legacy Shaders/Particles/Additive")); dm.hideFlags = HideFlags.DontSave; auraSR.material = dm; }
                            auraSR.sprite = (runtimeData.auraWhiteSprite != null) ? runtimeData.auraWhiteSprite : mainSR.sprite;
                            Color c = (myStatus != null && myStatus.characterData != null) ? myStatus.characterData.imageColor : ((ownerId == 1) ? Color.cyan : Color.red);
                            c.a = 1.0f; auraSR.color = c;
                        }

                        obj.tag = (ownerId == 1) ? "PlayerBullet" : "EnemyBullet";
                        int assignedLayer = LayerMask.NameToLayer((ownerId == 1) ? "Player1Bullet" : "Player2Bullet");
                        obj.layer = assignedLayer; SetLayerRecursive(obj, assignedLayer);

                        DanmakuBullet bullet = obj.GetComponent<DanmakuBullet>();
                        if (bullet != null)
                        {
                            bullet.Initialize(_rootOwner, targetTag, 0f, faceCenterAngle, 0, 0f, 0, s.delay, runtimeData, false);
                            bullet.isMovementSuspended = true;
                            bullet.StartSelfDestructTimer(bulletLifeTime);

                            // 🎯【重要】：新しく生まれた弾幕を、リアルタイム追跡リストに登録
                            _activeCageBullets.Add(bullet);
                        }

                        currentLayerList.Add(new Tuple<Transform, float>(obj.transform, initPlacementAngle));
                    }

                    layersBulletMatrix.Add(currentLayerList);
                    nextLayerToSpawn++;

                    // 🔥【核心】：5層すべての設置が完了した瞬間を検知！
                    if (nextLayerToSpawn >= totalLayers)
                    {
                        isGenerationFinished = true;
                        // 🔓 檻が完成した瞬間、消滅を待たずにコスト自動回復のロックを即座に全面解除！
                        _activeSkillCoroutines--;
                        if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = 1.0f;
                        Debug.Log("<color=lime>🔓【檻設置完了】自機の行動制限およびマナ回復ロックを早期解放しました！</color>");
                    }
                }

                // 🔄【リアルタイム公転スピン同期】
                for (int layerIndex = 0; layerIndex < layersBulletMatrix.Count; layerIndex++)
                {
                    float rotDirection = (layerIndex % 2 == 0) ? 1.0f : -1.0f;
                    float deltaRotation = baseRotateSpeed * currentElapsed * rotDirection;

                    var currentLayerBullets = layersBulletMatrix[layerIndex];
                    float radius = layersRadius[layerIndex];

                    for (int i = 0; i < currentLayerBullets.Count; i++)
                    {
                        var bulletTuple = currentLayerBullets[i];
                        Transform bulletTx = bulletTuple.Item1;

                        if (bulletTx == null || !bulletTx.gameObject.activeSelf) continue;

                        float updatedPlacementAngle = bulletTuple.Item2 + deltaRotation;
                        float rad = updatedPlacementAngle * Mathf.Deg2Rad;

                        Vector3 newPosition = enemyCenterPos + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * radius;
                        bulletTx.position = newPosition;

                        float updatedFaceCenterAngle = updatedPlacementAngle + 180f;
                        bulletTx.rotation = Quaternion.Euler(0, 0, updatedFaceCenterAngle - 90f);
                    }
                }
            }

            // スキル全体のクールダウンホールド
            yield return new WaitForSeconds(s.cooldown);
        }
        finally
        {
            // 🚨 もし5層を出し切る前に被弾等で「中断（break）」した場合のみ、ここで確実にコストロックを解除
            if (!isGenerationFinished)
            {
                if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = 1.0f;
                _activeSkillCoroutines--;
            }
        }
    }
    private IEnumerator ChainRandomAimRoutine(PlayerSkillData.SkillSettings s)
    {
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
            float targetAngle = GetAngleToTarget(spawnPos) + Random.Range(-1.5f,1.5f);
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
    // --- ★ 追加：防御フィールド専用のチャージ演出ルーチン ---
    // 📄 PlayerDanmakuEmitter.cs 内の防御フィールド制御セクター【領域展開・動的巨大延長版】
    private IEnumerator ChargeAndExecuteDefensiveField(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++; //
        PlayerMove myMove = _rootOwner.GetComponent<PlayerMove>(); //

        if (myMove != null) //
        {
            myMove.skillSpeedMultiplier = s.moveSpeedMultiplier; //
        }

        // 💡 1. 領域展開中（スペルカード発動中）であるかステートをチェック
        PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
        bool isSpellActive = (myStatus != null && myStatus.isSpellCardActive);

        // 💡 2. 【高橋さんの指定】：領域中ならサイズと持続時間の変数を動的にブースト！
        float finalFieldDuration = 1.0f; // 通常時の持続秒数
        float finalFieldScale = 2.0f;    // 通常時のDefensiveFieldインスペクター想定スケール

        if (isSpellActive)
        {
            finalFieldDuration = 2.0f;   // 🎯 領域展開中：持続時間を「3.0秒」へ延長（2倍）
            finalFieldScale = 3.5f;      // 🎯 領域展開中：サイズ（最大スケール）を「3.5倍」へ巨大化
            Debug.Log($"<color=gold>🔮【領域展開・絶対防壁】防御フィールドを極大化！ Duration: {finalFieldDuration}s, Scale: {finalFieldScale}</color>");
        }

        // チャージ演出
        float chargeTime = 0.3f; //
        if (BossEffectManager.Instance != null) //
        {
            BossEffectManager.Instance.PlayChargeEffect(chargeTime, s.bulletData.breakColor, transform.position); //
        }
        yield return new WaitForSeconds(chargeTime + 0.2f); //

        if (SEManager.Instance != null)
        {
            SEManager.Instance.Play(SEPath.SLASH, 0.5f); //
        }

        // 💡 3. 変調されたサイズと持続時間を手渡しして、スキル本体を実体化！
        ExecuteDefensiveField(s, finalFieldDuration, finalFieldScale);

        // 💡 4. 【インフラ完全同期】：スキル終了まで待機（引き伸ばされた動的持続時間に正確に合わせる）
        yield return new WaitForSeconds(finalFieldDuration);

        // 倍率を戻す
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

    private IEnumerator MovingArcRoutine(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++; //
        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>(); //
        float radiusX = 1.5f; //
        float radiusY = 0.4f; //
        int wayCount = 3; //
        bool currentDirectionReversed = _isArcReversed; //
        _isArcReversed = !_isArcReversed; //
        float startOffset = currentDirectionReversed ? 90f : -90f; //
        float endOffset = currentDirectionReversed ? -90f : 90f; //
        float step = currentDirectionReversed ? -20f : 20f; //
        float centerTargetAngle = GetAngleToTarget(transform.position); //
        PlayerMove myMove = GetComponentInParent<PlayerMove>(); //
        if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = s.moveSpeedMultiplier;

        for (float offset = startOffset;
             (step > 0 ? offset <= endOffset : offset >= endOffset);
             offset += step)
        {
            // ★ 修正：yield break ではなく break にしてループの下（クリーンアップ処理）へ流す
            if (!PlayerMove.CanShoot || (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal))
                break;

            float spawnAngleRad = (centerTargetAngle + offset) * Mathf.Deg2Rad; //
            Vector3 ellipseOffset = new Vector3(Mathf.Cos(spawnAngleRad) * radiusX, Mathf.Sin(spawnAngleRad) * radiusY, 0); //
            Vector3 spawnPos = transform.position + ellipseOffset; //
            float realAimAngle = GetAngleToTarget(spawnPos) + s.angleOffset; //
            float currentWideAngle = 60f; //
            float startAngle = realAimAngle - (currentWideAngle / 2f); //
            float stepAngle = (wayCount > 1) ? currentWideAngle / (wayCount - 1) : 0; //
            PlaySkillSE(s.sePath); //
            for (int i = 0; i < wayCount; i++) //
            {
                CreateShot(s.bulletData, spawnPos, s.speed, startAngle + (stepAngle * i), s.delay); //
            }
            for (int f = 0; f < 2; f++) yield return new WaitForFixedUpdate(); //
        }

        // これでガード句に引っかかった際も、確実にここを通ってリセットされます
        if (myMove != null) myMove.skillSpeedMultiplier = 1.0f; //
        _activeSkillCoroutines--; //
    }

    private IEnumerator ExecuteRandomRoundRoutine(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++; // 実行中カウントを増やす（コスト回復を止める）
        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        PlayerMove myMove = GetComponentInParent<PlayerMove>();
        int wayCount = 12;

        // 1. スキル使用中の減速を適用
        if (myMove != null)
        {
            myMove.skillSpeedMultiplier = s.moveSpeedMultiplier;
        }

        // --- 単発分（1セット）の弾幕生成ロジック ---
        if (PlayerMove.CanShoot && (myHH == null || myHH.currentState == PlayerHitHandler.PlayerState.Normal))
        {
            Vector3 randomOffset = new Vector3(Random.Range(-1f, 1f), Random.Range(-1.5f, 1.5f), 0);
            Vector3 spawnPos = transform.position + randomOffset;

            float targetAngle = GetAngleToTarget(spawnPos);
            float baseAngle = targetAngle + s.angleOffset;
            float step = 360f / wayCount;
            float rotationOffset = step / 2f;

            // 弾幕の速度をランダム化
            float randomizedBulletSpeed = s.speed + Random.Range(-1.0f, 1.0f);
            randomizedBulletSpeed = Mathf.Max(0.5f, randomizedBulletSpeed);

            PlaySkillSE(s.sePath);

            for (int i = 0; i < wayCount; i++)
            {
                float finalAngle = baseAngle + rotationOffset + (step * i);
                CreateShot(s.bulletData, spawnPos, randomizedBulletSpeed, finalAngle, s.delay);
            }
        }

        yield return null;
        // 2. ★ 重要：次の射撃が可能になるまで（cooldown秒間）状態を維持する
        // これにより、連射中に「速度制限」と「コスト回復停止」が継続します
        float waitTime = Mathf.Max(0.1f, 0.4f);
        yield return new WaitForSeconds(waitTime);

        // 3. 速度制限を解除し、実行中カウントを減らす
        if (myMove != null) myMove.skillSpeedMultiplier = 1.0f;
        _activeSkillCoroutines--;
    }
    // ★ void から IEnumerator に変更
    private IEnumerator ShootBoomerangRoutine(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++; // 実行カウントを増やす

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

    private void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    // 📄 PlayerDanmakuEmitter.cs 内の ExecuteSubShot メソッド【攻撃ランク・最下流溶接版】
    public void ExecuteSubShot(BulletData data, Vector3 pos, float speed, float angle, float accel, float maxSpeed, string tag, int layer)
    {
        if (data == null || data.bulletPrefab == null) return;

        if (SEManager.Instance != null)
        {
            SEManager.Instance.Play(SEPath.SHOT2, 0.2f);
        }

        // 💡 ブーメラン独自の物理と攻撃力をコンテキスト共有するため、ランタイムクローンを生成
        BulletData runtimeData = Instantiate(data);

        // 大元の所有者（PlayerStatusManager）の精密探索
        PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
        if (myStatus == null && _rootOwner != null)
        {
            myStatus = _rootOwner.GetComponent<PlayerStatusManager>();
        }

        // =========================================================================
        // 🎯【大正法】：ブーメラン子弾射出の瞬間にも攻撃ランク倍率を動的結合！
        // =========================================================================
        if (myStatus != null && myStatus.characterData != null)
        {
            float atkMultiplier = 1.0f;
            switch (myStatus.characterData.rankAttack)
            {
                case StatusRank.E: atkMultiplier = 0.8f; break;
                case StatusRank.D: atkMultiplier = 0.9f; break;
                case StatusRank.C: atkMultiplier = 1.0f; break;
                case StatusRank.B: atkMultiplier = 1.1f; break;
                case StatusRank.A: atkMultiplier = 1.2f; break;
                case StatusRank.EX: atkMultiplier = 1.3f; break;
            }

            // 🧬【パッシブスキル割り込み】：被弾時攻撃力強化バフが有効なら、さらに1.3倍
            if (myStatus.IsAttackBoostActive)
            {
                atkMultiplier *= 1.3f;
            }
            // 👁️【新規追加パッシブ】：相手のゲージ量に応じた嫉妬倍率を動的に乗算
            atkMultiplier *= myStatus.GetJealousyMultiplier();
            runtimeData.damage = Mathf.RoundToInt(runtimeData.damage * atkMultiplier);
        }

        // プレハブをその場で実体化
        GameObject obj = Instantiate(runtimeData.bulletPrefab, pos, Quaternion.identity);

        obj.tag = tag;
        obj.layer = layer;
        SetLayerRecursive(obj, layer);

        DanmakuBullet bullet = obj.GetComponent<DanmakuBullet>();
        if (bullet != null)
        {
            bullet.Initialize(_rootOwner, targetTag, speed, angle, accel, maxSpeed, 0f, 0f, runtimeData, false);
        }

        // =========================================================================
        // 🔮【白アセットカラー着色型・非混色加算オーラ生成インフラ】
        // =========================================================================
        SpriteRenderer mainSR = obj.GetComponentInChildren<SpriteRenderer>();
        if (mainSR != null)
        {
            GameObject auraChild = new GameObject("PureColorAuraObject");
            auraChild.transform.SetParent(obj.transform);

            auraChild.transform.localPosition = Vector3.zero;
            auraChild.transform.localRotation = Quaternion.identity;
            auraChild.transform.localScale = new Vector3(1.4f, 1.4f, 1.0f);

            SpriteRenderer auraSR = auraChild.AddComponent<SpriteRenderer>();

            auraSR.sortingLayerID = mainSR.sortingLayerID;
            auraSR.sortingOrder = mainSR.sortingOrder - 1;

            if (runtimeData.auraMaterial != null) auraSR.material = runtimeData.auraMaterial;
            else auraSR.material = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));

            if (runtimeData.auraWhiteSprite != null) auraSR.sprite = runtimeData.auraWhiteSprite;
            else auraSR.sprite = mainSR.sprite;

            if (myStatus != null && myStatus.characterData != null)
            {
                Color charImageColor = myStatus.characterData.imageColor;
                charImageColor.a = 0.7f;
                auraSR.color = charImageColor;
            }
            else
            {
                int ownerId = (myStatus != null) ? myStatus.playerId : 1;
                Color defaultColor = (ownerId == 1) ? Color.cyan : Color.red;
                defaultColor.a = 0.8f;
                auraSR.color = defaultColor;
            }
        }
    }


    private void ExecuteNWay(PlayerSkillData.SkillSettings s, Vector3 pos, float baseAngle)
    {
        int count = Mathf.Max(1, s.count);
        if (count == 1)
        {
            CreateShot(s.bulletData, pos, s.speed, baseAngle, s.delay);
            return;
        }
        float wayAngle;
        float startAngle;
        if (count % 2 == 0)
        {
            wayAngle = s.wideAngle / count;
            startAngle = baseAngle - (s.wideAngle / 2f) + (wayAngle / 2f);
        }
        else
        {
            wayAngle = s.wideAngle / (count - 1);
            startAngle = baseAngle - (s.wideAngle / 2f);
        }
        for (int i = 0; i < count; i++)
            CreateShot(s.bulletData, pos, s.speed, startAngle + (wayAngle * i), s.delay);
    }

    private void ExecuteRound(PlayerSkillData.SkillSettings s, Vector3 pos, float baseAngle)
    {
        int count = Mathf.Max(1, s.count);
        float step = 360f / count;
        float rotationOffset = (count % 2 == 0) ? (step / 2f) : 0f;
        for (int i = 0; i < count; i++)
            CreateShot(s.bulletData, pos, s.speed, baseAngle + rotationOffset + (step * i), s.delay);
    }

    private void ExecuteConvergePattern(PlayerSkillData.SkillSettings s, Vector3 pos, float baseAngle)
    {
        int count = Mathf.Max(1, s.count);
        float step = 360f / count;
        float spawnDistance =3.5f;
        float rotationOffset = (count % 2 == 0) ? (step / 2f) : 0f;
        for (int i = 0; i < count; i++)
        {
            float angle = baseAngle + rotationOffset + (step * i);
            float rad = angle * Mathf.Deg2Rad;
            Vector3 spawnPos = pos + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * spawnDistance;
            CreateShot(s.bulletData, spawnPos, s.speed, angle, s.delay, true);
        }
    }

    /// <summary>
    /// 設置型または追従型の極太レーザーを実行する
    /// </summary>


    // 📄 PlayerDanmakuEmitter.cs 内の CreateShot メソッド【攻撃ランク・最下流溶接版】
    private void CreateShot(BulletData data, Vector3 pos, float speed, float angle, float delay, bool isConverge = false)
    {
        // 💡 修正の核心：プレハブを実体化する前に、この弾幕データの「純粋なクローン（複製）」を作ります。
        // 💡 これにより、元の ScriptableObject(アセット)のダメージ設定値を永久に汚すことなく、
        // 💡 このフレームで生まれる弾だけの攻撃力倍率を安全に書き換えることができます！
        BulletData runtimeData = Instantiate(data);

        // 大元の所有者（PlayerStatusManager）の精密探索コンテキスト
        PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
        if (myStatus == null && _rootOwner != null)
        {
            myStatus = _rootOwner.GetComponent<PlayerStatusManager>();
        }

        int ownerId = (myStatus != null) ? myStatus.playerId : 1;

        // =========================================================================
        // 🎯【大正法】：弾幕射出の瞬間にキャラクターの攻撃ランク倍率を動的結合！
        // =========================================================================
        if (myStatus != null && myStatus.characterData != null)
        {
            float atkMultiplier = 1.0f;
            switch (myStatus.characterData.rankAttack)
            {
                case StatusRank.E: atkMultiplier = 0.8f; break;
                case StatusRank.D: atkMultiplier = 0.9f; break;
                case StatusRank.C: atkMultiplier = 1.0f; break;
                case StatusRank.B: atkMultiplier = 1.1f; break;
                case StatusRank.A: atkMultiplier = 1.2f; break;
                case StatusRank.EX: atkMultiplier = 1.3f; break;
            }

            // 🧬【パッシブスキル割り込み】：被弾時攻撃力強化バフが有効なら、さらに1.3倍
            if (myStatus.IsAttackBoostActive)
            {
                atkMultiplier *= 1.3f;
            }

            // 👁️【新規追加パッシブ】：相手のゲージ量に応じた嫉妬倍率を動的に乗算
            atkMultiplier *= myStatus.GetJealousyMultiplier();

            // 💡 キャラクター固有の攻撃ランク倍率 ＆ パッシブ倍率を、生まれたての弾幕にダイレクト乗算！
            runtimeData.damage = Mathf.RoundToInt(runtimeData.damage * atkMultiplier);
        }

        // 1. スプライト本来の弾幕プレハブを実体化（上書きされた runtimeData を使用）
        GameObject obj = Instantiate(runtimeData.bulletPrefab, pos, Quaternion.identity);

        // =========================================================================
        // 📊 ★【追加】：発射された弾のサイズ評価を検出し、指定範囲レイヤーへ強制バインド！
        // =========================================================================
        SpriteRenderer mainSR = obj.GetComponentInChildren<SpriteRenderer>();

        if (mainSR != null)
        {
            GameObject auraChild = new GameObject("PureColorAuraObject");
            auraChild.transform.SetParent(obj.transform);

            auraChild.transform.localPosition = Vector3.zero;
            auraChild.transform.localRotation = Quaternion.identity;
            auraChild.transform.localScale = new Vector3(1.4f, 1.4f, 1.0f);

            SpriteRenderer auraSR = auraChild.AddComponent<SpriteRenderer>();

            auraSR.sortingLayerID = mainSR.sortingLayerID;
            auraSR.sortingOrder = mainSR.sortingOrder - 1;

            if (runtimeData.auraMaterial != null) auraSR.material = runtimeData.auraMaterial;
            else auraSR.material = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));

            if (runtimeData.auraWhiteSprite != null) auraSR.sprite = runtimeData.auraWhiteSprite;
            else auraSR.sprite = mainSR.sprite;

            if (myStatus != null && myStatus.characterData != null)
            {
                Color charImageColor = myStatus.characterData.imageColor;
                charImageColor.a = 1.0f;
                auraSR.color = charImageColor;
            }
            else
            {
                Color defaultColor = (ownerId == 1) ? Color.cyan : Color.red;
                defaultColor.a = 1.0f;
                auraSR.color = defaultColor;
            }
        }

        // --- 以下、既存のチーム識別タグ・レイヤー設定、および Initialize インフラへ過不足なく完全結合 ---
        string assignedTag = (ownerId == 1) ? "PlayerBullet" : "EnemyBullet";
        obj.tag = assignedTag;

        int assignedLayer = LayerMask.NameToLayer((ownerId == 1) ? "Player1Bullet" : "Player2Bullet");
        obj.layer = assignedLayer;
        SetLayerRecursive(obj, assignedLayer);

        DanmakuBullet bullet = obj.GetComponent<DanmakuBullet>();
        if (bullet != null)
            bullet.Initialize(_rootOwner, targetTag, speed, angle, 0, speed, 0, delay, runtimeData, isConverge);
    }
    private void ExecutePolygon(PlayerSkillData.SkillSettings s, Vector3 pos, float startAngle)
    {
        int edges = Mathf.Max(3, s.count);
        int bulletCount = 32;
        float segmentAngle = 360f / edges;
        float rotationOffset = (edges % 2 == 0) ? (segmentAngle / 2f) : 0f;
        float finalStartAngle = startAngle + rotationOffset;
        for (int i = 0; i < bulletCount; i++)
        {
            float angleDeg = i * (360f / bulletCount) + finalStartAngle;
            float relativeAngle = ((angleDeg - finalStartAngle) % segmentAngle) - (segmentAngle / 2f);
            float speedMult = 1f / Mathf.Cos(relativeAngle * Mathf.Deg2Rad);
            CreateShot(s.bulletData, pos, s.speed * speedMult, angleDeg, s.delay);
        }
    }
    /// <summary>
    /// レーザーの生成とパラメータ設定を行うコルーチン
    /// </summary>
    private IEnumerator LaserRoutine(PlayerSkillData.SkillSettings s, bool isFollow)
    {
        _activeSkillCoroutines++;

        if (BulletManager.Instance == null)
        {
            _activeSkillCoroutines--;
            yield break;
        }

        BulletManager.LaserColor color = s.bulletData.laserColor;
        var laserSet = BulletManager.Instance.GetLaserSet(color);

        // 生成
        GameObject laserObj = Instantiate(BulletManager.Instance.laserBeamPrefab, transform.position, Quaternion.identity);
        EnemyLaserBeam laser = laserObj.GetComponent<EnemyLaserBeam>();

        if (laser != null)
        {
            // ★修正：targetTag と damage を渡す
            if (isFollow)
            {
                // SetupBの実装も同様に修正が必要
            }
            else
            {
                // =========================================================================
                // ⚔️【最核心修正】：通常レーザーへの攻撃ランク＆憤怒・嫉妬バフの動的結合
                // =========================================================================
                int finalNormalLaserDamage = s.bulletData.damage;
                PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
                if (myStatus != null && myStatus.characterData != null)
                {
                    float atkMultiplier = 1.0f;
                    switch (myStatus.characterData.rankAttack)
                    {
                        case StatusRank.E: atkMultiplier = 0.8f; break;
                        case StatusRank.D: atkMultiplier = 0.9f; break;
                        case StatusRank.C: atkMultiplier = 1.0f; break;
                        case StatusRank.B: atkMultiplier = 1.1f; break;
                        case StatusRank.A: atkMultiplier = 1.2f; break;
                        case StatusRank.EX: atkMultiplier = 1.3f; break;
                    }

                    // 🧬 憤怒パッシブの1.3倍を乗算
                    if (myStatus.IsAttackBoostActive) atkMultiplier *= 1.3f;
                    // 👁️ 嫉妬パッシブを乗算
                    atkMultiplier *= myStatus.GetJealousyMultiplier();

                    finalNormalLaserDamage = Mathf.RoundToInt(finalNormalLaserDamage * atkMultiplier);
                }

                laser.SetupA(_rootOwner, targetTag, finalNormalLaserDamage, // 💡計算済みの実数値を代入
                             transform.position.x, transform.position.y, s.count, s.wideAngle,
                             color, (int)s.delay, BulletManager.Instance.laserSourceEffectPrefab, laserSet.sourceEffectSprite, s.bulletData);
            }

            // 角度設定（1セット目は現在のターゲット方向）
            float angle = GetAngleToTarget() + s.angleOffset;
            laser.AddData(new EnemyLaserBeam.LaserTransformData { frame = 0, angle = angle });
            laser.Fire();

            // 持続時間（Cooldown）待機
            yield return new WaitForSeconds(s.speed);

            // 消滅命令
            if (laser != null) laser.ForceClose();
        }

        _activeSkillCoroutines--;
    }
    // --- PlayerDanmakuEmitter.cs 修正版ルーチン ---

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

        // --- 設定パラメータ ---
        int laserCount = Mathf.Max(1, LaserWay);
        float radius = 1.6f;             // 弾源の半径
        int stopFrame = 40;              // 回転が止まり始めるフレーム
        int warningFrame = stopFrame + 60; // 完全に止まってから実線化するまでの「タメ」

        // ★ 回転方向をランダムに決定
        float rotDir = (Random.value < 0.5f) ? 1.0f : -1.0f;
        float initialRotSpeed = 5.0f * rotDir;

        // ★ 追加：回転中にかける「ズレ」の総量
        float totalDriftAngle = 30f * rotDir;
        float driftVelocity = totalDriftAngle / stopFrame;

        // ★ 修正：停止位置（目標角度）をランダムに決定
        float targetAngle = Random.Range(0f, 360f);

        // 停止位置から逆算して、開始時のベース角度を求める
        float estimatedRotation = 245f * rotDir;
        float baseAngle = targetAngle - estimatedRotation;

        BulletManager.LaserColor color = s.bulletData.laserColor;
        var laserSet = BulletManager.Instance.GetLaserSet(color);

        // =========================================================================
        // ⚔️【最核心修正】：ループ突入前に、攻撃ランク＆憤怒・嫉妬バフの倍率を動的合算
        // =========================================================================
        int finalLaserDamage = s.bulletData.damage;
        if (myStatus != null && myStatus.characterData != null)
        {
            float atkMultiplier = 1.0f;
            switch (myStatus.characterData.rankAttack)
            {
                case StatusRank.E: atkMultiplier = 0.8f; break;
                case StatusRank.D: atkMultiplier = 0.9f; break;
                case StatusRank.C: atkMultiplier = 1.0f; break;
                case StatusRank.B: atkMultiplier = 1.1f; break;
                case StatusRank.A: atkMultiplier = 1.2f; break;
                case StatusRank.EX: atkMultiplier = 1.3f; break;
            }

            // 🧬【憤怒パッシブ】：被弾時バフ（IsAttackBoostActive）が有効なら1.3倍を直撃乗算！
            if (myStatus.IsAttackBoostActive)
            {
                atkMultiplier *= 1.3f;
            }
            // 👁️【嫉妬パッシブ】：相手のゲージ量に応じた倍率を同期乗算
            atkMultiplier *= myStatus.GetJealousyMultiplier();

            finalLaserDamage = Mathf.RoundToInt(finalLaserDamage * atkMultiplier);
        }

        for (int i = 0; i < laserCount; i++)
        {
            GameObject laserObj = Instantiate(BulletManager.Instance.laserBeamPrefab, transform.position, Quaternion.identity);
            EnemyLaserBeam laser = laserObj.GetComponent<EnemyLaserBeam>();

            if (laser != null)
            {
                spawnedLasers.Add(laser);

                // 💡 修正：s.bulletData.damage の生データではなく、バフ計算を終えた finalLaserDamage をインジェクション！
                laser.SetupB(_rootOwner, targetTag, finalLaserDamage,
                             transform.position.x, transform.position.y,
                             s.count, s.wideAngle, color, warningFrame,
                             BulletManager.Instance.laserSourceEffectPrefab, laserSet.sourceEffectSprite, s.bulletData);

                float currentStartAngle = baseAngle + (360f / laserCount * i);

                // 初期オフセット（120度は渦を巻くような大きな曲がり）
                float aimOffset = 120f * rotDir;
                float initialLaserAngle = currentStartAngle + aimOffset;

                // データ1：回転開始
                laser.AddData(new EnemyLaserBeam.LaserTransformData
                {
                    frame = 0,
                    dist = radius,                // 半径分離す
                    distAngle = currentStartAngle, // 弾源の公転角
                    laserAngle = initialLaserAngle, // レーザー自体の向き（曲げる）
                    distAngleVel = initialRotSpeed,
                    laserAngleVel = initialRotSpeed + driftVelocity, // 徐々にズレるように自転速度を微調整
                    isSmooth = true
                });

                // データ2：停止
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

        // 照射終了まで待機
        yield return new WaitForSeconds((warningFrame / 60f) + s.speed);

        // 全て消去
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
        // 💡 大元の所有者から現在の領域展開（スペルカード）ステートをチェック
        PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
        bool isSpellActive = (myStatus != null && myStatus.isSpellCardActive);

        // 💡 高橋さんの指定：領域展開中であればベースの数（s.count）から「4」を動的に加算！
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
            for (int i = 0; i < bulletCount; i++)
            {
                // ベース角 + 全方位分割角 + wによる回転量を加算
                float finalAngle = baseAngle + (step * i) + (angleIncrement * w); //

                // 💡 すでに完成しているCreateShotインフラを通るため、
                // 💡 4発増量された全弾の真ろに、混色ゼロの美しい白シルエット加算オーラが自動で溶接されます！
                CreateShot(s.bulletData, pos, currentSpeed, finalAngle, s.delay); //
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



    /// <summary>
    /// 【キャラA専用EX】陰陽オーブ公転・ホーミング追従アサルト（公転終了時に自機の硬直速度制限を最速解除する最適化版）
    /// </summary>
    private IEnumerator CharA_SealOrbEXPattern(PlayerSkillData.SkillSettings s, PlayerHitHandler myHH, bool isZoneActive)
    {
        int totalOrbs = s.count > 0 ? s.count : 6;
        if (isZoneActive) totalOrbs = Mathf.RoundToInt(totalOrbs * 1.5f);

        List<ExOrbTrackData> activeOrbs = new List<ExOrbTrackData>();
        float baseAngleStep = 360f / totalOrbs;

        // --- パラメータ定義（旧SealOrb.csの定数を完全再現） ---
        const float CONST_SPREAD_SPEED = 0.02f;
        const float CONST_ROTATION_SPEED = 4f;

        float enemyHomingSpeed = isZoneActive ? s.speed * 1.0f : s.speed;
        float playerReturnSpeed = enemyHomingSpeed * 0.8f;
        SEManager.Instance.Play(SEPath.SLASH, 0.3f);
        SEManager.Instance.Play(SEPath.LASER7, 0.3f);

        // =========================================================================
        // --- 段階1：オーブを一斉に実体化（クッキリ光る加算合成・赤維持） ---
        // =========================================================================
        for (int i = 0; i < totalOrbs; i++)
        {
            float startAngle = s.angleOffset + (baseAngleStep * i);
            GameObject bulletObj = Instantiate(s.bulletData.bulletPrefab, transform.position, Quaternion.identity);

            SpriteRenderer orbSR = bulletObj.GetComponent<SpriteRenderer>();
            if (orbSR == null) orbSR = bulletObj.GetComponentInChildren<SpriteRenderer>();

            if (orbSR != null && s.bulletData != null)
            {
                if (s.bulletData.bulletSprite != null) orbSR.sprite = s.bulletData.bulletSprite;

                Color baseColor = orbSR.color;
                baseColor.a = 1.0f;

                if (s.bulletData.material != null)
                {
                    orbSR.material = s.bulletData.material;
                }
                else
                {
                    Shader additiveShader = Shader.Find("Legacy Shaders/Particles/Additive");
                    if (additiveShader != null)
                    {
                        orbSR.material = new Material(additiveShader);
                    }
                }
            }

            var myStatus = GetComponentInParent<PlayerStatusManager>();
            int ownerId = (myStatus != null) ? myStatus.playerId : 1;
            string assignedTag = (ownerId == 1) ? "PlayerBullet" : "EnemyBullet";
            int assignedLayer = LayerMask.NameToLayer((ownerId == 1) ? "Player1Bullet" : "Player2Bullet");
            bulletObj.tag = assignedTag;
            bulletObj.layer = assignedLayer;
            SetLayerRecursive(bulletObj, assignedLayer);

            activeOrbs.Add(new ExOrbTrackData
            {
                tx = bulletObj.transform,
                angle = startAngle,
                radius = 0.2f,
                currentSpeed = 0f
            });

            if (i % 2 == 0) PlaySkillSE(s.sePath);
            yield return new WaitForFixedUpdate();
        }

        // =========================================================================
        // --- 段階2：自機の周りを公転しながらじわじわと外に広がる（60フレーム） ---
        // =========================================================================
        int orbitDurationFrames = 60;

        for (int f = 0; f < orbitDurationFrames; f++)
        {
            Vector3 playerPos = transform.position;
            for (int i = activeOrbs.Count - 1; i >= 0; i--)
            {
                var orb = activeOrbs[i];
                if (orb.tx == null) { activeOrbs.RemoveAt(i); continue; }

                Vector3 posBefore = orb.tx.position;

                orb.angle += CONST_ROTATION_SPEED;
                orb.radius += CONST_SPREAD_SPEED;

                float rad = orb.angle * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * orb.radius;
                orb.tx.position = playerPos + offset;

                if (Time.fixedDeltaTime > 0)
                {
                    orb.currentSpeed = (orb.tx.position - posBefore).magnitude / Time.fixedDeltaTime;
                }

                orb.tx.rotation = Quaternion.Euler(0, 0, orb.angle + 90f);
            }

            yield return new WaitForFixedUpdate();
        }

        // =========================================================================
        // 🌟 修正の核心：公転展開フェーズが終了した瞬間に、自機の移動速度倍率を最速解放！！
        // =========================================================================
        PlayerMove myMove = GetComponentInParent<PlayerMove>();
        if (myMove != null)
        {
            // インfra層の終了を待たず、この瞬間に1.0倍（等速通常巡航スピード）に強制復旧！
            myMove.skillSpeedMultiplier = 1.0f;
            Debug.Log("<color=green>⚡【EXインフラ最適化】公転フェーズ終了。自機のタメ硬直を即座に全面解除しました！</color>");
        }

        // =========================================================================
        // --- 段階3：【完全再現】回転の慣性ベクトルを維持したまま、滑らかに追尾へ遷移 ---
        // =========================================================================
        Debug.Log("<color=cyan>【EXスキル】ロックオン完了！ 公転ベクトルを引き継いで滑らかな追尾を開始します！</color>");

        for (int i = 0; i < activeOrbs.Count; i++)
        {
            var orb = activeOrbs[i];
            if (orb.tx == null) continue;

            DanmakuBullet bullet = orb.tx.GetComponent<DanmakuBullet>();
            if (bullet == null) bullet = orb.tx.gameObject.AddComponent<DanmakuBullet>();

            float currentHomingAngle = GetAngleToTarget(orb.tx.position) + s.angleOffset;

            bullet.Initialize(_rootOwner, targetTag, enemyHomingSpeed, currentHomingAngle, 0f, enemyHomingSpeed, 0f, 0f, s.bulletData, true);
            bullet.isMovementSuspended = true;

            orb.angle += 90f;
        }

        PlaySkillSE(s.sePath);

        float homingTimer = 0;
        float maxHomingTime = 180f;

        while (homingTimer < maxHomingTime && activeOrbs.Count > 0)
        {
            if (!PlayerMove.CanShoot) yield break;

            float dt = Time.fixedDeltaTime;

            for (int i = activeOrbs.Count - 1; i >= 0; i--)
            {
                var orb = activeOrbs[i];
                if (orb.tx == null) { activeOrbs.RemoveAt(i); continue; }

                Vector3 destination;
                float homingDamp;
                float targetSpeed;

                PlayerMove targetPlayer = null;
                foreach (var p in PlayerMove.AllPlayers)
                {
                    if (p != null && p.gameObject != _rootOwner)
                    {
                        targetPlayer = p;
                        break;
                    }
                }

                if (targetPlayer != null)
                {
                    destination = targetPlayer.transform.position;
                    homingDamp = 24f;
                    targetSpeed = enemyHomingSpeed;
                }
                else
                {
                    destination = _rootOwner != null ? _rootOwner.transform.position : transform.position;
                    homingDamp = 12f;
                    targetSpeed = playerReturnSpeed;
                }

                Vector3 diff = destination - orb.tx.position;
                float targetAngleRad = Mathf.Atan2(diff.y, diff.x);
                float judgangle = Mathf.Sin(targetAngleRad - (orb.angle * Mathf.Deg2Rad));

                if (Mathf.Abs(judgangle) > 0.05f)
                    orb.angle += Mathf.Asin(judgangle) * Mathf.Rad2Deg / homingDamp;
                else
                    orb.angle = targetAngleRad * Mathf.Rad2Deg;

                orb.currentSpeed = Mathf.Lerp(orb.currentSpeed, targetSpeed, 0.15f);

                orb.tx.rotation = Quaternion.Euler(0, 0, orb.angle);
                float rad = orb.angle * Mathf.Deg2Rad;
                orb.tx.position += new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * orb.currentSpeed * dt;
            }

            homingTimer++;
            yield return new WaitForFixedUpdate();
        }

        // =========================================================================
        // --- 段階4：時間切れ慣性直進フェーズ ---
        // =========================================================================
        Debug.Log("<color=gray>[EXスキル] 追尾時間が終了しました。残存オーブを慣性直進へ解放します。</color>");

        while (activeOrbs.Count > 0)
        {
            if (!PlayerMove.CanShoot) yield break;

            float dt = Time.fixedDeltaTime;

            for (int i = activeOrbs.Count - 1; i >= 0; i--)
            {
                var orb = activeOrbs[i];
                if (orb.tx == null) { activeOrbs.RemoveAt(i); continue; }

                float rad = orb.angle * Mathf.Deg2Rad;
                orb.tx.position += new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * orb.currentSpeed * dt;

                if (Mathf.Abs(orb.tx.position.x) > 10.0f || Mathf.Abs(orb.tx.position.y) > 10.0f)
                {
                    DanmakuBullet bullet = orb.tx.GetComponent<DanmakuBullet>();
                    if (bullet != null)
                    {
                        bullet.isMovementSuspended = false;
                        bullet.Deactivate(false);
                    }
                    else
                    {
                        Destroy(orb.tx.gameObject);
                    }
                    activeOrbs.RemoveAt(i);
                }
            }

            yield return new WaitForFixedUpdate();
        }
        activeOrbs.Clear();
    }
    private IEnumerator CharB_KnifeEXPattern(PlayerSkillData.SkillSettings s, PlayerHitHandler myHH, bool isZoneActive)
    {
        // -------------------------------------------------------------------------
        // 🌟 パラメータ設定（ここを変えるだけで弾数や回転数を自由に変更可能）
        // -------------------------------------------------------------------------
        int totalWaves = 15;          // 連射するサイクル数（対角2Wayなので計60発）
        float angleIncrement = 16f;    // 1ウェーブごとの発射角の傾き（渦巻きの密度）
        float stopDelayTime = 0.5f;   // 💡発射されてから空間停止するまでの時間（0.5秒）

        float initialSpeed = 5f;      // 弾幕データ初期速度
        float dashSpeed = isZoneActive ? (initialSpeed * 1.5f) * 1.3f : initialSpeed * 3.5f; // 突撃時は1.5倍速
        // 🌟 追加：現在の発射主の PlayerMove から、共通インフラが設定してくれた EX移動倍率をスタック
        PlayerMove myMove = GetComponentInParent<PlayerMove>();
        float currentAngle = UnityEngine.Random.Range(1f, 360f);
        SEManager.Instance.Play(SEPath.SLASH, 0.3f);
        SEManager.Instance.Play(SEPath.LASER7, 0.3f);
        Debug.Log("<color=pink>【EXスキル】キャラB：インフラ活用型・対角2Wayナイフアサルトを開始</color>");

        // =========================================================================
        // --- 段階1：対角2Wayで射出（DanmakuBulletの標準直進をそのまま利用） ---
        // =========================================================================
        for (int i = 0; i < totalWaves; i++)
        {
            //if (!PlayerMove.CanShoot || (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal)) yield break;
            // 💡 リアルタイム減速：連射のループ中、常にインスペクターから指定された低速倍率（例: 0.2倍）を上書き固定ホールド
            if (myMove != null) myMove.skillSpeedMultiplier = s.moveSpeedMultiplier;
            for (int j = 0; j < 2; j++)
            {
                float finalAngle = currentAngle + (j * 180f);
                Vector3 spawnPos = transform.position;

                GameObject bulletObj = Instantiate(s.bulletData.bulletPrefab, spawnPos, Quaternion.identity);

                // チームの所属タグ・レイヤーの動的自動結合
                var myStatus = GetComponentInParent<PlayerStatusManager>();
                int ownerId = (myStatus != null) ? myStatus.playerId : 1;
                string assignedTag = (ownerId == 1) ? "PlayerBullet" : "EnemyBullet";
                int assignedLayer = LayerMask.NameToLayer((ownerId == 1) ? "Player1Bullet" : "Player2Bullet");
                bulletObj.tag = assignedTag;
                bulletObj.layer = assignedLayer;
                SetLayerRecursive(bulletObj, assignedLayer);

                DanmakuBullet bullet = bulletObj.GetComponent<DanmakuBullet>();
                if (bullet == null) bullet = bulletObj.AddComponent<DanmakuBullet>();

                // 🌟 核心：isMovementSuspended を使わず、普通に初期物理を直進させます！
                // 🌟 これにより、生まれた瞬間から initialSpeed で綺麗に前進を始めます。
                bullet.isMovementSuspended = false;
                bullet.Initialize(_rootOwner, targetTag, initialSpeed, finalAngle, 0f, initialSpeed, 0f, 0f, s.bulletData, false);

                // 【画像そのまま】アセット指定のビジュアルを完全維持
                SpriteRenderer knifeSR = bulletObj.GetComponent<SpriteRenderer>();
                if (knifeSR == null) knifeSR = bulletObj.GetComponentInChildren<SpriteRenderer>();
                if (knifeSR != null && s.bulletData != null)
                {
                    if (s.bulletData.bulletSprite != null) knifeSR.sprite = s.bulletData.bulletSprite;
                    if (s.bulletData.material != null) knifeSR.material = s.bulletData.material;
                }

                // 🌟【非同期タイマーコルーチンを弾単体に個別に持たせる】
                // これにより、このEmitter側のメインループは2フレーム間隔の連射だけに100%集中できます！
                StartCoroutine(IndividualKnifeRoutine(bullet, bulletObj.GetComponent<CircleCollider2D>(), finalAngle, stopDelayTime, dashSpeed, s.bulletData));
            }

            currentAngle += angleIncrement;
            PlaySkillSE(s.sePath);

            // 正確に2フレーム待機して次の2Wayを放つ
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
        }

        yield return null;
    }

    /// <summary>
    /// 💡 新設：生まれた1発1発のナイフが、自分自身の時間軸で勝手に「0.5秒進んで止まる➔回る➔突撃」を行う独立AIルーチン
    /// </summary>
    private IEnumerator IndividualKnifeRoutine(DanmakuBullet bullet, CircleCollider2D col, float spawnAngle, float delay, float speed2, BulletData data)
    {
        // 1. 【発射➔0.5秒待機フェーズ】
        // 弾は内部のFixedUpdateで勝手に初速5で前に進んでいるので、ここではただ0.5秒待つだけ！
        yield return new WaitForSeconds(delay);
        if (bullet == null) yield break;

        // 2. 【0.5秒後にその場でピタッと停止 ➔ 当たり判定OFF】
        // 移動処理のみをサスペンド(ON)に切り替えて自動前進をロックし、判定を消す
        bullet.isMovementSuspended = true;
        if (col != null) col.enabled = false;

        // 3. 【その場で360度カチカチと回転スピン】
        // 過去コードの「for (int i = 0; i < 360; i += 12)」を完全再現
        for (int rotationDelta = 0; rotationDelta < 360; rotationDelta += 12)
        {
            // 一時停止や時間停止を考慮した、ゲーム全体のFixedUpdateフレーム同期
            yield return new WaitForFixedUpdate();
            if (bullet == null) yield break;

            float currentRotAngle = spawnAngle + rotationDelta;
            bullet.transform.rotation = Quaternion.Euler(0, 0, currentRotAngle - 90f);
        }

        // 4. 【ロックオン ➔ 判定復旧 ➔ 1.5倍速一直線突撃】
        // 最も近い敵を探索
        float finalAimAngle = spawnAngle;
        Transform nearestEnemy = null;
        float minDistance = Mathf.Infinity;

        foreach (var p in PlayerMove.AllPlayers)
        {
            if (p != null && p.gameObject != _rootOwner)
            {
                float dist = Vector3.Distance(bullet.transform.position, p.transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    nearestEnemy = p.transform;
                }
            }
        }

        if (nearestEnemy != null)
        {
            Vector3 dir = nearestEnemy.position - bullet.transform.position;
            finalAimAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        }

        // 刃先をターゲットへ向け、判定をONに戻す
        bullet.transform.rotation = Quaternion.Euler(0, 0, finalAimAngle - 90f);
        if (col != null) col.enabled = true;

        // 🌟 物理の完全解放：サスペンドを解除し、新しい「突撃角度」と「1.5倍の超高速」で直進物理をリスタート！
        bullet.isMovementSuspended = false;
        bullet.Initialize(_rootOwner, targetTag, speed2, finalAimAngle, 0f, speed2, 0f, 0f, data, false);


    }
    /// <summary>
    /// 🌟【領域展開専用】：トリプル一閃（中央1本：通常完全同調・自機狙い確定版）
    /// 💡【数理修復】：子スレッドへ渡すオフセット配列と、軌道回転行列の結合バグを完全根治しました。
    /// </summary>
    private IEnumerator ExecuteKarinTripleScalesSlashRoutine(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;

        // 1. 現在の自機の位置から敵機を見据えた、絶対的な基準ターゲット角度を取得
        float absoluteCenterAngle = GetAngleToTarget(transform.position);

        // 🎯 ターゲット正面を中心に、指定のオフセットで扇形に展開する3連撃配列
        // 💡 1本目（i=0）➔ 敵機のど真ん中（0度）
        // 💡 2本目（i=1）➔ 敵機の右上（+120度）
        // 💡 3本目（i=2）➔ 敵機の左下（-120度）
        float[] tripleOffsets = new float[] { 0f, 45f, -45f };

        // 3連コンボ開始時の往復フラグをローカルにロック
        bool comboBaseDirection = _isArcReversed;
        _isArcReversed = !_isArcReversed;

        for (int i = 0; i < tripleOffsets.Length; i++)
        {
            if (!PlayerMove.CanShoot) break;

            // 💡 基準となる自機狙い軸から、オフセット分綺麗に変調をかけた絶対ターゲット角度を算出！
            float customAngle = absoluteCenterAngle + tripleOffsets[i];

            PlaySkillSE(s.sePath);

            // 💡 散らした角度（customAngle）をサブルーチンへ確実にパッシングして射出
            StartCoroutine(ExecuteSingleScalesSlashTrack(s, customAngle, comboBaseDirection));

            // 🎯 ご指定の「3フレームの時間差ディレイ」を正確にホールド
            for (int f = 0; f < 3; f++)
            {
                yield return new WaitForFixedUpdate();
            }
        }

        yield return new WaitForSeconds(s.cooldown);
        _activeSkillCoroutines--;
    }

    /// <summary>
    /// トリプル展開用：指定された絶対角度に向けて「1wayしの字」の軌跡を1本走らせるサブルーチン（角度完全適合版）
    /// </summary>
    private IEnumerator ExecuteSingleScalesSlashTrack(PlayerSkillData.SkillSettings s, float targetAngle, bool forcedReverse)
    {
        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();

        float startAngleFromTangent = 27f;
        float totalRotationAmount = 3;

        float baseRadiusX = 2.0f;
        float baseRadiusY = 0.8f;

        // 親から引き継いだ往復極性をカプセル化
        bool currentDirectionReversed = forcedReverse;

        float startLocalAngle = currentDirectionReversed ? 152f : -152f;
        float localAngleStep = currentDirectionReversed ? -18f : 18f;

        // =========================================================================
        // 🌟【大修復】：親から受け取った引数 `targetAngle` を回転行列のベースに直撃結合！
        // =========================================================================
        // 💡 理由：GetAngleToTarget をここで再計算してしまうと、オフセットが0度に戻ってしまいます。
        //          引き渡された targetAngle をラジアン化することで、指定方向に綺麗な扇形として直交展開されます。
        float baseRad = targetAngle * Mathf.Deg2Rad;
        float cosRot = Mathf.Cos(baseRad);
        float sinRot = Mathf.Sin(baseRad);

        int totalStepsCount = 11;

        // 1発目のローカル接線の先読み計算
        float f_localAngRad = startLocalAngle * Mathf.Deg2Rad;
        float f_localX = Mathf.Cos(f_localAngRad) * baseRadiusX * 1.3f;
        float f_localY = Mathf.Sin(f_localAngRad) * baseRadiusY * 1.3f;
        Vector3 firstSpawnPos = transform.position + new Vector3(f_localX * cosRot - f_localY * sinRot, f_localX * sinRot + f_localY * cosRot, 0);

        float f_nextLocalAngRad = (startLocalAngle + (localAngleStep * 0.01f)) * Mathf.Deg2Rad;
        float f_nextLocalX = Mathf.Cos(f_nextLocalAngRad) * baseRadiusX * Mathf.Lerp(1.3f, 0.6f, 0.01f / (totalStepsCount - 1));
        float f_nextLocalY = Mathf.Sin(f_nextLocalAngRad) * baseRadiusY * Mathf.Lerp(1.3f, 0.6f, 0.01f / (totalStepsCount - 1));
        Vector3 firstNextSpawnPos = transform.position + new Vector3(f_nextLocalX * cosRot - f_nextLocalY * sinRot, f_nextLocalX * sinRot + f_nextLocalY * cosRot, 0);

        // 初期接線方向ベクトルをロック
        Vector3 firstTangentDir = firstNextSpawnPos - firstSpawnPos;
        float lockedInitialTangentAngle = Mathf.Atan2(firstTangentDir.y, firstTangentDir.x) * Mathf.Rad2Deg;

        // 「しの字」一閃ラインの生成ループ
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

            // 歪みのない直交行列により、指定角度（targetAngle）の空間へ完全に変換！
            Vector3 worldOffset = new Vector3(localX * cosRot - localY * sinRot, localX * sinRot + localY * cosRot, 0);
            Vector3 spawnPos = transform.position + worldOffset;

            float rotationSign = currentDirectionReversed ? -1f : 1f;
            float baseStartAngle = lockedInitialTangentAngle + (startAngleFromTangent * rotationSign);
            float currentMoveAngle = baseStartAngle + (totalRotationAmount * t * rotationSign);

            // 💡 弾自体の進行方向角度（finalBulletAngle）も、s.angleOffset を乗算ブレンドして美しく同期
            float finalBulletAngle = currentMoveAngle + s.angleOffset;

            // 高速弾・低速残響弾のツインブレードレイヤー射出
            int layerCount = 2;
            for (int l = 0; l < layerCount; l++)
            {
                float speedPercent = Mathf.Lerp(1.1f, 0.8f, (float)l / (layerCount - 1));
                float randomizedSpeed = s.speed * speedPercent;
                randomizedSpeed = Mathf.Max(1.0f, randomizedSpeed);

                // 鋭い「1way」として完璧なアライメントで射出！
                CreateShot(s.bulletData, spawnPos, randomizedSpeed, finalBulletAngle, s.delay);
            }

            yield return new WaitForFixedUpdate();
        }
    }
    /// <summary>
    /// 🐉 カリン専用Z：「しの字」アークの【最も盛り上がっている部分】が完璧に敵機正面を捉える完全対称化アルゴリズム
    /// 🌟 【仕様確定版】：2連装速度差（高速・低速ペア）にスリム化し、キレのある二連斬撃を表現。
    /// </summary>
    private IEnumerator ExecuteKarinScalesSlashRoutine(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;
        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        PlayerMove myMove = GetComponentInParent<PlayerMove>();

        if (myMove != null) myMove.skillSpeedMultiplier = s.moveSpeedMultiplier;

        float startAngleFromTangent = 27f;
        float totalRotationAmount = 3;

        float baseRadiusX = 2.0f;
        float baseRadiusY = 0.8f;

        bool currentDirectionReversed = _isArcReversed;
        _isArcReversed = !_isArcReversed;

        float startLocalAngle = currentDirectionReversed ? 152f : -152f;
        float localAngleStep = currentDirectionReversed ? -18f : 18f;

        float absoluteCenterAngle = GetAngleToTarget(transform.position);
        float baseRad = absoluteCenterAngle * Mathf.Deg2Rad;
        float cosRot = Mathf.Cos(baseRad);
        float sinRot = Mathf.Sin(baseRad);

        int totalStepsCount = 11;

        // 1発目の接線角度（ベースの向き）をローカル座標から先読みしてロック
        float f_t = 0f;
        float f_radiusMod = Mathf.Lerp(1.3f, 0.6f, f_t);
        float f_localAngRad = startLocalAngle * Mathf.Deg2Rad;

        float f_localX = Mathf.Cos(f_localAngRad) * baseRadiusX * f_radiusMod;
        float f_localY = Mathf.Sin(f_localAngRad) * baseRadiusY * f_radiusMod;

        Vector3 firstSpawnPos = transform.position + new Vector3(
            f_localX * cosRot - f_localY * sinRot,
            f_localX * sinRot + f_localY * cosRot,
            0
        );

        float f_nextLocalAngRad = (startLocalAngle + (localAngleStep * 0.01f)) * Mathf.Deg2Rad;
        float f_nextRadiusMod = Mathf.Lerp(1.3f, 0.6f, 0.01f / (totalStepsCount - 1));
        float f_nextLocalX = Mathf.Cos(f_nextLocalAngRad) * baseRadiusX * f_nextRadiusMod;
        float f_nextLocalY = Mathf.Sin(f_nextLocalAngRad) * baseRadiusY * f_nextRadiusMod;
        Vector3 firstNextSpawnPos = transform.position + new Vector3(
            f_nextLocalX * cosRot - f_nextLocalY * sinRot,
            f_nextLocalX * sinRot + f_nextLocalY * cosRot,
            0
        );

        Vector3 firstTangentDir = firstNextSpawnPos - firstSpawnPos;
        float lockedInitialTangentAngle = Mathf.Atan2(firstTangentDir.y, firstTangentDir.x) * Mathf.Rad2Deg;

        PlaySkillSE(s.sePath);

        // 🔄 頂点完全固定・双方向往復「しの字」連射ループ
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

            Vector3 worldOffset = new Vector3(
                localX * cosRot - localY * sinRot,
                localX * sinRot + localY * cosRot,
                0
            );
            Vector3 spawnPos = transform.position + worldOffset;

            float rotationSign = currentDirectionReversed ? -1f : 1f;
            float baseStartAngle = lockedInitialTangentAngle + (startAngleFromTangent * rotationSign);
            float currentMoveAngle = baseStartAngle + (totalRotationAmount * t * rotationSign);
            float finalBulletAngle = currentMoveAngle + s.angleOffset;

            // =========================================================================
            // 🔮【核心機能】：領域展開連動型・ポリモーフィック多段射出システム
            // =========================================================================
            int layerCount = 2; // 高速・低速ペア
            for (int l = 0; l < layerCount; l++)
            {
                float speedPercent = Mathf.Lerp(1.1f, 0.9f, (float)l / (layerCount - 1));
                float randomizedSpeed = s.speed * speedPercent;
                randomizedSpeed = Mathf.Max(1.0f, randomizedSpeed);

                // 💡 s.count が 3 以上の時は、1発の直進ではなく、その座標から広がる扇形（3way）をオート展開！
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
                    // 💡 通常時（1way）は、従来通りの完璧な1対のペアブレードをストレート射出
                    CreateShot(s.bulletData, spawnPos, randomizedSpeed, finalBulletAngle, s.delay);
                }
            }

            yield return new WaitForFixedUpdate();
        }

        yield return new WaitForSeconds(s.cooldown);
        if (myMove != null) myMove.skillSpeedMultiplier = 1.0f;
        _activeSkillCoroutines--;
    }
    /// <summary>
    /// ⚔️ カリン専用X：空間一閃・自機外し双極ブレード
    /// 🌟 【領域展開・4wayクアッド変調適合版】：
    /// 🌟 通常時はターゲットを見据えたキレのある2way（左右30度開くツインブレード）。
    /// 🌟 領域展開（スペルカード）中はs.countをインフラ層から検知するか、内部フラグを自動ブレンド。
    /// 🌟 ターゲットの逃げ道を100%遮断する「4way大爆風扇形一閃（左右15度・45度）」へと動的にポリモーフィック進化します！
    /// </summary>
    private IEnumerator ExecuteKarinCrossSlashRoutine(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;
        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        PlayerMove myMove = GetComponentInParent<PlayerMove>();

        if (myMove != null) myMove.skillSpeedMultiplier = s.moveSpeedMultiplier;

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

    /// <summary>
    /// EX/超必殺の共通インフラ（器）
    /// 🌟【通常5本・領域15本完全分離調停版】：
    /// 🌟 通常時はインスペクターの設定をそのまま活かした「正統な5本」を流下。
    /// 🌟 領域展開中のみ、ベース11本＋上下拡張4本＝「極大15本」へと上流で鮮やかにオーバーライドします。
    /// </summary>
    private IEnumerator ExecuteEXInfrastructureRoutine(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;
        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        PlayerMove myMove = GetComponentInParent<PlayerMove>();

        if (myMove != null) myMove.skillSpeedMultiplier = s.moveSpeedMultiplier;
        PlaySkillSE(s.sePath);

        try
        {
            PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
            bool isZoneActive = (myStatus != null && myStatus.isSpellCardActive);

            // =========================================================================
            // 🔮【EXインフラ変調レイヤー】：通常時と領域中の本数を完全に独立制御
            // =========================================================================
            PlayerSkillData.SkillSettings enhancedEXSettings = s;

            if (enhancedEXSettings.patternType == SkillPatternType.RotatingAccelRound)
            {
                if (isZoneActive)
                {
                    // 🎯 領域展開中：ベース11本 ＋ 上下外側拡張4本 ＝ 【15本】
                    enhancedEXSettings.count = 15;
                    Debug.Log("<color=gold>🔮【領域展開・極大空間破砕】カリン究極EX：全画面飽和15本バーストへ！</color>");
                }
                else
                {
                    // 🎯 通常時：インスペクターに設定された元の数（5本）をピュアに維持！
                    // 💡 もしデータが0の場合は安全のためデフォルトの「5本」を代入します
                    enhancedEXSettings.count = (s.count <= 0) ? 5 : s.count;
                    Debug.Log($"<color=cyan>⚔️【通常EX・正統回帰】カリン究極EX：元のスマートな【{enhancedEXSettings.count}本】へデータを調停。</color>");
                }
                s = enhancedEXSettings;
            }
            else if (isZoneActive)
            {
                enhancedEXSettings.speed = s.speed * 1.3f;
                s = enhancedEXSettings;
            }

            switch (s.patternType)
            {
                case SkillPatternType.Custom:
                    yield return StartCoroutine(CharA_SealOrbEXPattern(s, myHH, isZoneActive));
                    break;
                case SkillPatternType.Line:
                case SkillPatternType.GreedTaxPossession:
                    //yield return StartCoroutine(CharB_KnifeEXPattern(s, myHH, isZoneActive));
                    yield return StartCoroutine(ExecuteSyaruBackFormationSlashEXRoutine(s));
                    break;
                case SkillPatternType.Standard:
                    yield return StartCoroutine(CharA_SealOrbEXPattern(s, myHH, isZoneActive));
                    break;
                case SkillPatternType.RotatingAccelRound:
                    yield return StartCoroutine(ExecuteKarinKokuZessenEXRoutine(s));
                    break;
                default:
                    Debug.LogWarning($"[FireEX] 未実装のEXパターンタイプです: {s.patternType}");
                    break;
            }
        }
        finally
        {
            if (myMove != null) myMove.skillSpeedMultiplier = 1.0f;
            _activeSkillCoroutines--;
        }
    }

    /// <summary>
    /// 🐉 カリン専用究極EX：神速・虚空絶閃（全画面空間破砕アサルト）
    /// 🌟 【自機追従・通常5本／領域15本両立決定版】：
    /// 🌟 通常時（5本）は、お札の上下間隔を「2.0f」に維持し、カリンを中心に「-4.0f〜+4.0f」に美しく並べます。
    /// 🌟 領域中（15本）は、間隔を「0.8f」へスリーム化し、開始位置を「-5.6f」へシフトさせて上下端まで完全圧殺。
    /// 🌟 どちらのモードであっても、中央の斬撃ラインの軸はカリンの現在地に100%完璧に吸い付きます！
    /// </summary>
    private IEnumerator ExecuteKarinKokuZessenEXRoutine(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;
        PlayerMove myMove = _rootOwner.GetComponent<PlayerMove>();
        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
        _isEXSkillActive = true; // 🎯 共用フラグON
        if (myMove == null || myStatus == null)
        {
            _activeSkillCoroutines--;
            yield break;
        }
        // =========================================================================
        // 🎯【ゲージ消費の完全執行】：
        // 💡 シャウル側のタイムラインと同様に、カリン究極EXが正式にキックされた瞬間、
        // 💡 アルカナゲージ残量を確実に「0%」へとリセットクランプします！
        // =========================================================================
        myMove.skillSpeedMultiplier = 0f;

        // 🎯 1. ターゲットのリアルタイムな左右座標を精密に観測
        float targetAngle = GetAngleToTarget(transform.position);
        bool isEnemyOnRightSide = (targetAngle > -90f && targetAngle <= 90f);

        float mySideScreenEdgeX = isEnemyOnRightSide ? -8.5f : 8.5f;
        float enemySideScreenEdgeX = isEnemyOnRightSide ? 8.5f : -8.5f;
        bool faceRight = isEnemyOnRightSide;

        float startY = _rootOwner.transform.position.y;

        // =========================================================================
        // 💨 1. 敵機の【反対側の画面端】へ超高速バックステップ
        // =========================================================================
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

        // =========================================================================
        // ⏳ 2. 抜刀の「タメ」演出
        // =========================================================================
        float chargeTime = 0.4f;
        if (BossEffectManager.Instance != null)
        {
            BossEffectManager.Instance.PlayChargeEffect(chargeTime, s.bulletData.breakColor, _rootOwner.transform.position);
        }
        yield return new WaitForSeconds(chargeTime);

        Vector3 laserStartPos = _rootOwner.transform.position;

        // =========================================================================
        // ⚡ 3. 刹那一閃・敵陣の画面端まで超高速突撃
        // =========================================================================
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

        // 突撃移動の絶対距離（長さ）を正確に計算
        float laserDistance = Vector3.Distance(laserStartPos, _rootOwner.transform.position);

        // =========================================================================
        // 🔮 4. 虚空砕裂：【通常5本／領域15本】上下カリン中心追従展開マトリクス
        // =========================================================================
        if (BulletManager.Instance != null)
        {
            BulletManager.LaserColor color = s.bulletData.laserColor;
            var laserSet = BulletManager.Instance.GetLaserSet(color);

            int totalLinesCount = Mathf.Max(2, s.count);
            bool isEnhancedLines = (totalLinesCount >= 15);

            // 🎯【幾何学アライメントの完全調停】：
            // 💡 通常時（5本）➔ 間隔は広めの「2.0f」。カリンの位置から上下に2本分（-4.0f）スライドして開始。
            // 💡 領域中（15本）➔ 間隔は超密の「0.8f」。カリンの位置から上下に7本分（-5.6f）スライドして、画面外まで圧殺。
            float offsetStep = isEnhancedLines ? 1.2f : 1.8f;

            // 🎯【中心軸の絶対カリン同期】：
            // 通常時：i = 2 の時に currentYOffset がジャスト 0f になり、カリンの位置に完全一致！
            // 領域中：i = 7 の時に currentYOffset がジャスト 0f になり、カリンの位置に完全一致！
            float startYOffset = isEnhancedLines ? (-offsetStep * 7f) : (-offsetStep * 2f);

            for (int i = 0; i < totalLinesCount; i++)
            {
                float currentYOffset = startYOffset + (offsetStep * i);
                Vector3 finalLaserSpawnPos = new Vector3(laserStartPos.x, laserStartPos.y + currentYOffset, laserStartPos.z);

                GameObject laserObj = Instantiate(BulletManager.Instance.laserBeamPrefab, finalLaserSpawnPos, Quaternion.identity);
                EnemyLaserBeam zessenLaser = laserObj.GetComponent<EnemyLaserBeam>();

                if (zessenLaser != null)
                {
                    // 重なり防止の時差タイマーグラデーション
                    int dynamicDelay = isEnhancedLines ? (20 + (i * 1)) : 20;

                    // =========================================================================
                    // ⚔️【最核心修正】：究極EX斬撃レーザーへの攻撃ランク＆憤怒・嫉妬バフの動的結合
                    // =========================================================================
                    int finalLaserDamage = s.bulletData.damage; // ベース（一閃固有の2倍化）
                    if (myStatus != null && myStatus.characterData != null)
                    {
                        float atkMultiplier = 1.0f;
                        switch (myStatus.characterData.rankAttack)
                        {
                            case StatusRank.E: atkMultiplier = 0.8f; break;
                            case StatusRank.D: atkMultiplier = 0.9f; break;
                            case StatusRank.C: atkMultiplier = 1.0f; break;
                            case StatusRank.B: atkMultiplier = 1.1f; break;
                            case StatusRank.A: atkMultiplier = 1.2f; break;
                            case StatusRank.EX: atkMultiplier = 1.3f; break;
                        }

                        // 🧬【憤怒パッシブ割り込み】：被弾時バフ（IsAttackBoostActive）が有効なら1.3倍を直撃乗算！
                        if (myStatus.IsAttackBoostActive)
                        {
                            atkMultiplier *= 1.3f;
                        }
                        // 👁️【嫉妬パッシブ】：相手のゲージ量に応じた倍率を同期乗算
                        atkMultiplier *= myStatus.GetJealousyMultiplier();

                        finalLaserDamage = Mathf.RoundToInt(finalLaserDamage * atkMultiplier);
                    }

                    zessenLaser.SetupA(_rootOwner, targetTag, finalLaserDamage, // 💡計算済みの実数値を代入
                                     finalLaserSpawnPos.x, finalLaserSpawnPos.y,
                                     laserDistance, 0.5f,
                                     color, dynamicDelay, BulletManager.Instance.laserSourceEffectPrefab, laserSet.sourceEffectSprite, s.bulletData);
                    SpriteRenderer laserSR = laserObj.GetComponentInChildren<SpriteRenderer>();
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
                        foreach (Transform child in laserObj.transform)
                        {
                            if (child != null && (child.name.Contains("Root") || child.name.Contains("Effect") || child.name.Contains("Source")))
                            {
                                child.gameObject.SetActive(false);
                            }
                        }
                    }

                    float extendedDuration = s.speed + 1.0f;
                    StartCoroutine(KeepInvertingLaserOffsetRoutine(laserObj, laserDistance, extendedDuration, faceRight));
                    StartCoroutine(ForceCloseLaserAfterSeconds(zessenLaser, extendedDuration));
                }

                // 💡 通常時（5本）のみ、心地いいパラパラ感を残すために1フレームウェイトを適用
                if (!isEnhancedLines)
                {
                    yield return null;
                }
            }
        }

        if (myMove != null) myMove.skillSpeedMultiplier = 1.0f;
        _activeSkillCoroutines--;

    }
    /// <summary>
    /// 💡 毎フレーム判定ボックスをお札のグラフィックスの芯へ完全に吸い付かせ、一体化させる調停ループ
    /// </summary>
    private IEnumerator KeepInvertingLaserOffsetRoutine(GameObject laserObj, float distance, float duration, bool faceRight)
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

    private IEnumerator ForceCloseLaserAfterSeconds(EnemyLaserBeam laser, float duration)
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
    /// <summary>
    /// 🎯【新設】：PortalBitObjectが毎フレーム自機の最新ターゲット角度を逆算抽出するためのインフラブリッジ関数
    /// </summary>
    public float ExecuteGetAngleToTargetBridge()
    {
        return GetAngleToTarget(transform.position);
    }
    public void ExecuteSubShotFromPortal(BulletData data, Vector3 pos, float speed, float angle, float delay)
    {
        CreateShot(data, pos, speed, angle, delay);
    }

    /// <summary>
    /// 🎯【新設】：魔方陣EXが動いていない安全なコンテキストの時のみ、等速（1.0f）へと復旧させるインフラ関数
    /// </summary>
    private void RestoreSpeedSafety(PlayerMove myMove)
    {
        if (myMove == null) return;

        // 🛡️ 核心ガード：魔方陣EXが絶賛稼働中（_isEXSkillActive == true）の時は、
        // 他の通常スキルが終了した際の一律等速リセット命令を「100%完全に無視（遮断）」します！
        if (_isEXSkillActive) return;

        myMove.skillSpeedMultiplier = 1.0f;
    }

    // 📄 PlayerDanmakuEmitter.cs 内の TemporarySlow コルーチン【参照完全分離版】
    private IEnumerator TemporarySlow(float multiplier, float duration)
    {
        // 🔄 修正前：PlayerMove myMove = GetComponentInParent<PlayerMove>();
        // 🎯 修正後：Awakeで確定ロックした「自分自身の_rootOwner」から直接引っ張ることで、
        // 💡 対戦相手のPlayerMoveのポインタを誤って掴んでしまう事故を100%物理的にパージします！
        PlayerMove myMove = (_rootOwner != null) ? _rootOwner.GetComponent<PlayerMove>() : GetComponentInParent<PlayerMove>();

        if (myMove != null) myMove.skillSpeedMultiplier = multiplier; //
        yield return new WaitForSeconds(duration); //

        RestoreSpeedSafety(myMove); //
    }

    // 拡張した内部データ管理クラス
    private class ExOrbTrackData
    {
        public Transform tx;
        public float angle;
        public float radius;
        public float currentSpeed; // 慣性等速ホーミング用の速度スタック
    }


    // =========================================================================
    // 🔮【修正確定版】：三転反射 ✕ サブ弾ランダム四散トレイル（発射後2秒コスト解放ホールド版）
    // =========================================================================
    // =========================================================================
    // 🔮【完全クリーン版】：三転反射 ✕ サブ弾ランダム四散トレイル（レイヤー完全バインド適合）
    // =========================================================================
    private IEnumerator ExecuteBouncingTrailShotRoutine(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++; // 🟢 マナの自動回復を一時停止

        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        PlayerMove myMove = GetComponentInParent<PlayerMove>();

        if (myMove != null && !_isEXSkillActive)
        {
            myMove.skillSpeedMultiplier = s.moveSpeedMultiplier;
        }

        List<BouncingBulletTrack> trackingBullets = new List<BouncingBulletTrack>();
        bool isCostReleased = false; // 🔓 コストが解放されたかを追跡するフラグ

        try
        {
            // --- ⚙️ メイン弾のパラメータ設定 ---
            int shotCount = Mathf.Max(1, s.count);
            float mainBulletSpeed = (s.speed > 0f) ? s.speed : 5.0f;
            float targetAngle = GetAngleToTarget();
            float baseAngle = targetAngle + s.angleOffset;

            Vector3 spawnOrigin = transform.position;
            PlaySkillSE(s.sePath);

            // 🎯 1. 最初のメイン弾（n-Way）の初期角度・ステップ幅計算（※順序バグを完全修復）
            float startAngle = baseAngle;
            float stepAngle = 0f;
            if (shotCount > 1)
            {
                float spread = (s.wideAngle > 0f) ? s.wideAngle : 45f;
                startAngle = baseAngle - (spread / 2f);
                stepAngle = spread / (shotCount - 1);
            }

            for (int i = 0; i < shotCount; i++)
            {
                // 正しい定義順序で射角を確定
                float finalAngle = startAngle + (stepAngle * i);

                BulletData runtimeData = Instantiate(s.bulletData);
                runtimeData.hideFlags = HideFlags.DontSave;

                GameObject obj = (BulletPool.Instance != null && runtimeData.bulletPrefab != null)
                    ? BulletPool.Instance.Get(runtimeData.bulletPrefab, spawnOrigin, Quaternion.identity)
                    : Instantiate(runtimeData.bulletPrefab, spawnOrigin, Quaternion.identity);

                obj.transform.localScale = Vector3.one;

                DanmakuBullet bullet = obj.GetComponent<DanmakuBullet>();
                if (bullet != null)
                {
                    // 💡 弾側の Initialize が内部でサイズ別のオーダーとオーラアライメントを 100% 完璧に自動執行します！
                    bullet.Initialize(_rootOwner, targetTag, 0f, finalAngle, 0, 0f, 0, s.delay, runtimeData, false);
                    bullet.isMovementSuspended = true;

                    trackingBullets.Add(new BouncingBulletTrack(obj.transform, finalAngle, mainBulletSpeed, bullet));
                }
            }

            // =========================================================================
            // 🔄 2. メインリアルタイム移動 ✕ 跳ね返り ✕ 跡引き設置ループ
            // =========================================================================
            float currentElapsed = 0f;
            int frameCounter = 0;

            const float wallMinX = -8.8f;
            const float wallMaxX = 8.8f;
            const float wallMaxY = 4.8f;
            const float wallMinY = -4.8f;

            float myTrailSpeed = 0.15f;
            float myTrailLifeTime = 1.0f;
            float myTrailAccel = 0;

            BulletData trailBaseAsset = (s.trailBulletData != null) ? s.trailBulletData : s.bulletData;
            BulletData trailData = Instantiate(trailBaseAsset);
            trailData.hideFlags = HideFlags.DontSave;

            while (trackingBullets.Count > 0)
            {
                yield return new WaitForFixedUpdate();
                float dt = Time.fixedDeltaTime;
                currentElapsed += dt;
                frameCounter++;

                if (!PlayerMove.CanShoot || (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal))
                    break;

                if (!isCostReleased && currentElapsed >= 2.0f)
                {
                    isCostReleased = true;
                    _activeSkillCoroutines--;
                    if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = 1.0f;
                    Debug.Log("<color=lime>🔓【反射トレイル】発射後2秒経過。早期解放しました！</color>");
                }

                bool shouldLeaveTrailThisFrame = (frameCounter % 4 == 0);

                for (int i = trackingBullets.Count - 1; i >= 0; i--)
                {
                    BouncingBulletTrack b = trackingBullets[i];

                    if (b.tx == null || !b.tx.gameObject.activeSelf || b.bulletLogic == null)
                    {
                        trackingBullets.RemoveAt(i);
                        continue;
                    }

                    if (b.bulletLogic.DelayFrames > 0)
                    {
                        continue;
                    }

                    float rad = b.currentAngle * Mathf.Deg2Rad;
                    Vector3 moveStep = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * b.speed * dt;
                    b.tx.position += moveStep;

                    b.tx.rotation = Quaternion.Euler(0, 0, b.currentAngle - 90f);

                    Vector3 currentPos = b.tx.position;
                    bool bouncedThisFrame = false;

                    if (b.bounceCount < 6)
                    {
                        if (currentPos.x <= wallMinX && Mathf.Cos(rad) < 0f)
                        {
                            b.currentAngle = 180f - b.currentAngle;
                            b.bounceCount++;
                            bouncedThisFrame = true;
                        }
                        else if (currentPos.x >= wallMaxX && Mathf.Cos(rad) > 0f)
                        {
                            b.currentAngle = 180f - b.currentAngle;
                            b.bounceCount++;
                            bouncedThisFrame = true;
                        }

                        rad = b.currentAngle * Mathf.Deg2Rad;
                        if (currentPos.y <= wallMinY && Mathf.Sin(rad) < 0f)
                        {
                            b.currentAngle = -b.currentAngle;
                            b.bounceCount++;
                            bouncedThisFrame = true;
                        }
                        else if (currentPos.y >= wallMaxY && Mathf.Sin(rad) > 0f)
                        {
                            b.currentAngle = -b.currentAngle;
                            b.bounceCount++;
                            bouncedThisFrame = true;
                        }

                        if (bouncedThisFrame)
                        {
                            b.currentAngle = (b.currentAngle + 360f) % 360f;
                            if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.SHOT2, 0.15f);
                        }
                    }
                    else
                    {
                        if (Mathf.Abs(currentPos.x) > 10.0f || Mathf.Abs(currentPos.y) > 6.0f)
                        {
                            b.bulletLogic.Deactivate(false);
                            trackingBullets.RemoveAt(i);
                            continue;
                        }
                    }

                    if (shouldLeaveTrailThisFrame && !bouncedThisFrame)
                    {
                        for (int v = 0; v < 3; v++)
                        {
                            float randomAngle = UnityEngine.Random.Range(0f, 360f);

                            Vector3 positionNoise = new Vector3(
                                UnityEngine.Random.Range(-0.05f, 0.05f),
                                UnityEngine.Random.Range(-0.05f, 0.05f),
                                0f
                            );
                            Vector3 scatterSpawnPos = currentPos + positionNoise;

                            GameObject trailObj = (BulletPool.Instance != null && trailData.bulletPrefab != null)
                                ? BulletPool.Instance.Get(trailData.bulletPrefab, scatterSpawnPos, Quaternion.identity)
                                : Instantiate(trailData.bulletPrefab, scatterSpawnPos, Quaternion.identity);

                            trailObj.transform.localScale = Vector3.one;

                            string assTag = b.tx.tag;
                            trailObj.tag = assTag;
                            trailObj.layer = b.tx.gameObject.layer;
                            SetLayerRecursive(trailObj, trailObj.layer);

                            DanmakuBullet trailLogic = trailObj.GetComponent<DanmakuBullet>();
                            if (trailLogic != null)
                            {
                                // 💡 移設完了した弾側の Initialize へ完全パス回し！出撃の瞬間に 5000〜20000 のサイズ別レイヤーが100%安全に刷り直されます。
                                trailLogic.Initialize(_rootOwner, targetTag, myTrailSpeed, randomAngle, myTrailAccel, 0f, 0f, 0f, trailData, false);
                                trailLogic.StartSelfDestructTimer(myTrailLifeTime);
                            }
                        }
                    }
                }
            }

            yield return new WaitForSeconds(s.cooldown);
        }
        finally
        {
            if (!isCostReleased)
            {
                if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = 1.0f;
                _activeSkillCoroutines--;
            }
        }
    }
    private void RefreshBulletAuraInfrastructure(GameObject obj, BulletData runtimeData)
    {
        SpriteRenderer mainSR = obj.GetComponentInChildren<SpriteRenderer>();
        if (mainSR == null) return;

        Transform auraChildTransform = obj.transform.Find("PureColorAuraObject");
        GameObject auraChildObj;
        SpriteRenderer auraSR;

        if (auraChildTransform == null)
        {
            auraChildObj = new GameObject("PureColorAuraObject");
            auraChildObj.transform.SetParent(obj.transform);
            auraChildObj.transform.localPosition = Vector3.zero;
            auraChildObj.transform.localScale = new Vector3(1.4f, 1.4f, 1.0f);
            auraSR = auraChildObj.AddComponent<SpriteRenderer>();
        }
        else
        {
            auraChildObj = auraChildTransform.gameObject;
            auraSR = auraChildObj.GetComponent<SpriteRenderer>();
        }

        auraSR.sortingLayerID = mainSR.sortingLayerID;
        auraSR.sortingOrder = mainSR.sortingOrder - 1;

        if (runtimeData.auraMaterial != null) auraSR.material = runtimeData.auraMaterial;
        else
        {
            Material dm = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
            dm.hideFlags = HideFlags.DontSave;
            auraSR.material = dm;
        }

        auraSR.sprite = (runtimeData.auraWhiteSprite != null) ? runtimeData.auraWhiteSprite : mainSR.sprite;

        PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
        if (myStatus == null && _rootOwner != null) myStatus = _rootOwner.GetComponent<PlayerStatusManager>();
        int ownerId = (myStatus != null) ? myStatus.playerId : 1;

        Color c = (myStatus != null && myStatus.characterData != null) ? myStatus.characterData.imageColor : ((ownerId == 1) ? Color.cyan : Color.red);
        c.a = 1.0f;
        auraSR.color = c;
    }

    // 💡 反射移動の状態をカプセル化して内部維持するシミュレーション構造体
    private class BouncingBulletTrack
    {
        public Transform tx;
        public float currentAngle;
        public float speed;
        public int bounceCount;
        public DanmakuBullet bulletLogic;

        public BouncingBulletTrack(Transform t, float angle, float spd, DanmakuBullet logic)
        {
            this.tx = t;
            this.currentAngle = angle;
            this.speed = spd;
            this.bounceCount = 0;
            this.bulletLogic = logic;
        }
    }
    private void PlaySkillSE(string path)
    {
        string clip = string.IsNullOrEmpty(path) ? SEPath.SHOT1 : path;
        if (SEManager.Instance != null)
            SEManager.Instance.Play(clip, 0.4f);
    }
}