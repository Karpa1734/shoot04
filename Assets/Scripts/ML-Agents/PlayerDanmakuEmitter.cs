using KanKikuchi.AudioManager;
using System.Collections;
using System.Collections.Generic;
using Unity.AppUI.UI;
using UnityEngine;

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
    private IEnumerator ChainRandomAimRoutine(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++; // 実行中カウントを増やす（エネルギー回復停止）

        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        PlayerMove myMove = GetComponentInParent<PlayerMove>();

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
            float targetAngle = GetAngleToTarget(spawnPos) + Random.Range(-3.0f,3.0f);
            float baseAngle = targetAngle + s.angleOffset;

            // 規定回数（6回）を連射
            int burstCount = 6;
            for (int i = 0; i < burstCount; i++)
            {
                // --- N-way（扇形）の生成ロジック ---
                int wayCount = Mathf.Max(1, s.count); // 3way, 5wayなど
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

        // 次のセットまでの待機
        yield return new WaitForSeconds(s.cooldown);

        // 状態を戻す
        if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = 1.0f;
        _activeSkillCoroutines--;
    }
    // --- ★ 追加：防御フィールド専用のチャージ演出ルーチン ---
    private IEnumerator ChargeAndExecuteDefensiveField(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;
        // ★ 修正：_rootOwner から確実に PlayerMove を取得する
        PlayerMove myMove = _rootOwner.GetComponent<PlayerMove>();

        if (myMove != null)
        {
            // デバッグログ：現在の設定値をコンソールに表示して確認
            Debug.Log($"Charge Start: Multiplier set to {s.moveSpeedMultiplier}");
            myMove.skillSpeedMultiplier = s.moveSpeedMultiplier;
    }
        else
        {
            Debug.LogError("PlayerMove could not be found on _rootOwner!");
        }

        // チャージ演出
        float chargeTime = 0.3f;
        if (BossEffectManager.Instance != null)
        {
            BossEffectManager.Instance.PlayChargeEffect(chargeTime, s.bulletData.breakColor, transform.position);
    }
        yield return new WaitForSeconds(chargeTime);

        SEManager.Instance.Play(SEPath.SHOT1, 0.2f);
        // スキル本体の生成
        ExecuteDefensiveField(s);

        // スキル終了まで待機（DefensiveFieldの持続時間に合わせる）
        yield return new WaitForSeconds(1.5f);

        // 倍率を戻す
        if (myMove != null)
        {
            Debug.Log("Charge End: Multiplier reset to 1.0");
            myMove.skillSpeedMultiplier = 1.0f; 
    }
        _activeSkillCoroutines--;
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

        // 2. ★ 重要：次の射撃が可能になるまで（cooldown秒間）状態を維持する
        // これにより、連射中に「速度制限」と「コスト回復停止」が継続します
        float waitTime = Mathf.Max(0.1f, s.cooldown);
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

    public void ExecuteSubShot(BulletData data, Vector3 pos, float speed, float angle, float accel, float maxSpeed, string tag, int layer)
    {
        GameObject obj = Instantiate(data.bulletPrefab, pos, Quaternion.identity);
        obj.tag = tag;
        obj.layer = layer;
        SEManager.Instance.Play(SEPath.SHOT2, 0.2f);
        DanmakuBullet bullet = obj.GetComponent<DanmakuBullet>();
        if (bullet != null)
            bullet.Initialize(_rootOwner, targetTag, speed, angle, accel, maxSpeed, 0, 0, data);
    }

    private void ExecuteDefensiveField(PlayerSkillData.SkillSettings s)
    {
        GameObject fieldObj = Instantiate(s.bulletData.bulletPrefab, transform.position, Quaternion.identity);
        var myStatus = GetComponentInParent<PlayerStatusManager>();
        int ownerId = (myStatus != null) ? myStatus.playerId : 1;
        string assignedTag = (ownerId == 1) ? "PlayerBullet" : "EnemyBullet";
        int assignedLayer = LayerMask.NameToLayer((ownerId == 1) ? "Player1Bullet" : "Player2Bullet");
        var field = fieldObj.GetComponent<DefensiveField>();
        if (field == null) field = fieldObj.AddComponent<DefensiveField>();
        field.Initialize(transform, s.bulletData, 1.5f, assignedTag, assignedLayer);
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


    private void CreateShot(BulletData data, Vector3 pos, float speed, float angle, float delay, bool isConverge = false)
    {
        // 1. スプライト本来の弾幕プレハブを実体化（本体はクッキリ最前面に描画）
        GameObject obj = Instantiate(data.bulletPrefab, pos, Quaternion.identity);

        // 大元の所有者（PlayerStatusManager）の精密探索コンテキスト
        PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
        if (myStatus == null && _rootOwner != null)
        {
            myStatus = _rootOwner.GetComponent<PlayerStatusManager>();
        }

        int ownerId = (myStatus != null) ? myStatus.playerId : 1;

        // =========================================================================
        // 🔮【Resources完全撤廃型：白アセットカラー着色・非混色加算オーラシステム】
        // =========================================================================
        SpriteRenderer mainSR = obj.GetComponentInChildren<SpriteRenderer>();
        if (mainSR != null && data != null)
        {
            // 💡 1. オーラ専用の子供オブジェクトを生成してバインド
            GameObject auraChild = new GameObject("PureColorAuraObject");
            auraChild.transform.SetParent(obj.transform);

            // 💡 2. 位置・回転を本体と100%完全同調させ、サイズを一回り大きく拡張！
            auraChild.transform.localPosition = Vector3.zero;
            auraChild.transform.localRotation = Quaternion.identity;
            auraChild.transform.localScale = new Vector3(1.4f, 1.4f, 1.0f);

            SpriteRenderer auraSR = auraChild.AddComponent<SpriteRenderer>();

            // 💡 3. レイヤー順（SortingOrder）を、本体スプライトの「真後ろ（-1）」へ潜り込ませる
            auraSR.sortingLayerID = mainSR.sortingLayerID;
            auraSR.sortingOrder = mainSR.sortingOrder - 1;

            // =========================================================================
            // 🎯【ノンリソース化の核心：静的データバインド調停】
            // =========================================================================
            // 💡 Resources.Loadを100%完全パージ！
            // 💡 BulletDataのインスペクターに直接登録されたマテリアルから最速でデータ共有。
            if (data.auraMaterial != null)
            {
                auraSR.material = data.auraMaterial;
            }
            else
            {
                // 🛡️ 安全弁：もしアセット側で入れ忘れていた場合のみ、標準の粒子加算シェーダーをバックアップビルド
                auraSR.material = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
            }

            // 💡 5. 白スプライトをアサインし、プレイヤーカラーで着色
            if (data.auraWhiteSprite != null)
            {
                auraSR.sprite = data.auraWhiteSprite;
            }
            else
            {
                auraSR.sprite = mainSR.sprite;
            }

            // 💡 6. 大元の持ち主のカラー（imageColor）を精密抽出してオーラへ着色インジェクション！
            if (myStatus != null && myStatus.characterData != null)
            {
                Color charImageColor = myStatus.characterData.imageColor;

                // 加算合成マテリアルの不透明度（アルファ）を一番綺麗に輝く 0.7f 前後に調整して注入
                charImageColor.a = 1.0f;
                auraSR.color = charImageColor;
            }
            else
            {
                // セーフティ安全弁
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
            bullet.Initialize(_rootOwner, targetTag, speed, angle, 0, speed, 0, delay, data, isConverge);
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
                laser.SetupA(_rootOwner, targetTag, s.bulletData.damage,
                             transform.position.x, transform.position.y, s.count, s.wideAngle,
                             color, (int)s.delay, BulletManager.Instance.laserSourceEffectPrefab, laserSet.sourceEffectSprite);
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

        // --- 設定パラメータ ---
        int laserCount = Mathf.Max(1, 32); // 18本
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

        for (int i = 0; i < laserCount; i++)
        {
            GameObject laserObj = Instantiate(BulletManager.Instance.laserBeamPrefab, transform.position, Quaternion.identity);
            EnemyLaserBeam laser = laserObj.GetComponent<EnemyLaserBeam>();

            if (laser != null)
            {
                spawnedLasers.Add(laser);

                // ★ 発射時の自機座標を centerPos として固定する SetupB を使用
                laser.SetupB(_rootOwner, targetTag, s.bulletData.damage,
                             transform.position.x, transform.position.y,
                             s.count, s.wideAngle, color, warningFrame,
                             BulletManager.Instance.laserSourceEffectPrefab, laserSet.sourceEffectSprite);

                float currentStartAngle = baseAngle + (360f / laserCount * i);

                // 初期オフセット（150度は渦を巻くような大きな曲がり）
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
                    laserAngleVel = initialRotSpeed + driftVelocity, // ★徐々にズレるように自転速度を微調整
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
    private IEnumerator RotatingAccelRoundRoutine(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++; // 実行中カウント（MP回復停止）
        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>(); //
        PlayerMove myMove = GetComponentInParent<PlayerMove>(); //

        if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = s.moveSpeedMultiplier;

        Vector3 pos = transform.position; //

        // 1. 1波あたりの弾数を設定（インスペクターのCountを使用）
        int bulletCount = Mathf.Max(2, s.count); //
        if (bulletCount % 2 != 0) bulletCount++; //

        float step = 360f / bulletCount; //
        float evenWayOffset = step / 2f; //

        // 2. 連射設定と ★回転方向の交互反転ロジック
        int waveLoops = 12; //
        float currentSpeed = s.speed; // 初速（インスペクターのSpeed）

        // ★ 現在の状態を取得し、フラグを反転させて次回に備える
        bool currentRotReversed = _isRoundRotReversed;
        _isRoundRotReversed = !_isRoundRotReversed;

        // フラグに応じて回転方向を 1.0 または -1.0 にする
        float rotDirection = currentRotReversed ? -1f : 1f;
        float angleIncrement = 13f * rotDirection; // ★ 1波ごとの回転角の向きを決定

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
                CreateShot(s.bulletData, pos, currentSpeed, finalAngle, s.delay); //
            }

            // 次の波の弾速を上げる（段階的加速）
            currentSpeed += 0.5f; //

            // 波と波の間の時間差（1フレーム待機）
            for (int f = 0; f < 3; f++) //
            {
                yield return new WaitForFixedUpdate(); //
            }
        }

        // 次のキャストまでのクールタイム待機
        yield return new WaitForSeconds(s.cooldown); //

        if (myMove != null && ! _isEXSkillActive) myMove.skillSpeedMultiplier = 1.0f; //
        _activeSkillCoroutines--; //
    }
    /// <summary>
    /// 強欲：グリード・タックス＆ポゼッション
    /// 敵弾をかき消して必殺ゲージに変え、その場に一回転するカウンターナイフを生成する防御フィールドを展開
    /// </summary>
    private IEnumerator GreedTaxPossessionRoutine(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;

        PlayerMove myMove = _rootOwner.GetComponent<PlayerMove>();
        if (myMove != null && !_isEXSkillActive && s.moveSpeedMultiplier < 1.0f)
        {
            myMove.skillSpeedMultiplier = s.moveSpeedMultiplier;
        }

        PlaySkillSE(s.sePath);

        // 1. スキルデータに登録された「フィールドプレハブ」を生成
        GameObject fieldObj = Instantiate(s.bulletData.bulletPrefab, transform.position, Quaternion.identity);

        // 2. 所属チームに応じたタグとレイヤーを生成の瞬間に割り当てる
        var myStatus = GetComponentInParent<PlayerStatusManager>();
        int ownerId = (myStatus != null) ? myStatus.playerId : 1;

        string assignedTag = (ownerId == 1) ? "PlayerBullet" : "EnemyBullet";
        int assignedLayer = LayerMask.NameToLayer((ownerId == 1) ? "Player1Bullet" : "Player2Bullet");

        fieldObj.tag = assignedTag;
        fieldObj.layer = assignedLayer;
        SetLayerRecursive(fieldObj, assignedLayer);

        // 3. プレハブにあらかじめ付いている GreedTaxPossessionField コンポーネントを取得
        GreedTaxPossessionField fieldLogic = fieldObj.GetComponent<GreedTaxPossessionField>();

        if (fieldLogic != null)
        {
            // ★ ブーメランビットと同様、アタッチされたコンポーネントに必要な参照を渡して初期化
            fieldLogic.Initialize(transform, _rootOwner, targetTag, this);
        }
        else
        {
            Debug.LogError("フィールド用プレハブに GreedTaxPossessionField が付いていません！");
        }

        // 4. フィールドの有効持続時間分、Emitter側も安全に同期待機
        yield return new WaitForSeconds(3.0f + 0.2f);

        if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = 1.0f;
        _activeSkillCoroutines--;
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
    /// 🌟【領域展開専用】：45度差トリプル一閃（中央1本：通常完全同調・自機狙い確定版）
    /// 🌟 【仕様変更】：120度分散をパージし、ターゲット正面を中心に左右45度（0度、+45度、-45度）に開く扇形3連撃へ調停。
    /// 🌟 基準角の計算を「自機の現在地」に復旧させたことで、中央の1本目が通常時と1ミリの狂いもなく完璧に敵の芯を捉えます。
    /// </summary>
    private IEnumerator ExecuteKarinTripleScalesSlashRoutine(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;
        
        // 🎯【自機狙い完全復旧】：
        // 💡 ステージ中央固定ではなく、現在の「自機の座標（transform.position）」から敵機を見据えた
        // 💡 正規のターゲット角度を取得することで、通常時と100%同一のジャストミート自機狙い軸を確立します！
        float absoluteCenterAngle = GetAngleToTarget(transform.position);

        // 🎯【仕様変更】：ターゲットの正面を中心に、左右45度ずつ扇形に開くトリプルオフセット配列
        // 💡 1本目（i=0）➔ 敵機のど真ん中（0度）
        // 💡 2本目（i=1）➔ 敵機の右上（+45度）
        // 💡 3本目（i=2）➔ 敵機の左下（-45度）
        float[] tripleOffsets = new float[] { 0f, 120f, -120f };

        // 3連コンボ開始時の往復フラグをローカルにロック
        bool comboBaseDirection = _isArcReversed;
        _isArcReversed = !_isArcReversed;

        for (int i = 0; i < tripleOffsets.Length; i++)
        {
            if (!PlayerMove.CanShoot) break;

            // 💡 基準となる自機狙い軸から、45度ずつ綺麗に変調をかけます
            float customAngle = absoluteCenterAngle + tripleOffsets[i];

            PlaySkillSE(s.sePath);
            // 💡 各スレッドの極性を完全にカプセル化して非同期射出
            StartCoroutine(ExecuteSingleScalesSlashTrack(s, customAngle, comboBaseDirection));
            
            // 🎯 ご指定の「3フレームの時間差ディレイ」を正確にホールド
            for (int f = 0; f < 3; f++)
            {
                yield return new WaitForFixedUpdate();
            }
        }

        _activeSkillCoroutines--;
    }

    /// <summary>
    /// トリプル展開用：指定された絶対角度に向けて「1wayしの字」の軌跡を1本走らせるサブルーチン（自機狙い完全同期版）
    /// </summary>
    private IEnumerator ExecuteSingleScalesSlashTrack(PlayerSkillData.SkillSettings s, float targetAngle, bool forcedReverse)
    {
        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        
        float startAngleFromTangent = 10f;
        float totalRotationAmount = 20f;
        float baseRadiusX = 2.5f;
        float baseRadiusY = 0.8f;

        // 親から手渡しされた不変のローカルフラグをバインド（スレッド安全）
        bool currentDirectionReversed = forcedReverse;

        float startLocalAngle = currentDirectionReversed ? 150f : -150f;
        float localAngleStep = currentDirectionReversed ? -15f : 15f;

        // 🎯 補正された自機狙い軸（0度、+45度、-45度）を元に回転マトリクスの基底ベクトルを構築
        float baseRad = targetAngle * Mathf.Deg2Rad;
        float cosRot = Mathf.Cos(baseRad);
        float sinRot = Mathf.Sin(baseRad);

        int totalStepsCount = 13;

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

            // 回転行列により、自機狙い基準の扇形空間へ座標を歪みなく100%直交変換
            Vector3 worldOffset = new Vector3(localX * cosRot - localY * sinRot, localX * sinRot + localY * cosRot, 0);
            Vector3 spawnPos = transform.position + worldOffset;

            float rotationSign = currentDirectionReversed ? -1f : 1f;
            float baseStartAngle = lockedInitialTangentAngle + (startAngleFromTangent * rotationSign);
            float currentMoveAngle = baseStartAngle + (totalRotationAmount * t * rotationSign);
            float finalBulletAngle = currentMoveAngle + s.angleOffset;

            // 高速弾・低速残響弾のツインブレードレイヤー射出
            int layerCount = 2;
            for (int l = 0; l < layerCount; l++)
            {
                float speedPercent = Mathf.Lerp(1.1f, 0.8f, (float)l / (layerCount - 1));
                float randomizedSpeed = s.speed * speedPercent;
                randomizedSpeed = Mathf.Max(1.0f, randomizedSpeed);

                // 鋭い「1way」として完璧なアライメントで射出
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

        float startAngleFromTangent = 10f;
        float totalRotationAmount = 20f;

        float baseRadiusX = 2.5f;
        float baseRadiusY = 0.8f;

        bool currentDirectionReversed = _isArcReversed;
        _isArcReversed = !_isArcReversed;

        float startLocalAngle = currentDirectionReversed ? 150f : -150f;
        float localAngleStep = currentDirectionReversed ? -15f : 15f;

        float absoluteCenterAngle = GetAngleToTarget(transform.position);
        float baseRad = absoluteCenterAngle * Mathf.Deg2Rad;
        float cosRot = Mathf.Cos(baseRad);
        float sinRot = Mathf.Sin(baseRad);

        int totalStepsCount = 13;

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
                float speedPercent = Mathf.Lerp(1.1f, 0.8f, (float)l / (layerCount - 1));
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

                    zessenLaser.SetupA(_rootOwner, targetTag, s.bulletData.damage * 2,
                                     finalLaserSpawnPos.x, finalLaserSpawnPos.y,
                                     laserDistance, 0.5f,
                                     color, dynamicDelay, BulletManager.Instance.laserSourceEffectPrefab, laserSet.sourceEffectSprite);

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

    private IEnumerator TemporarySlow(float multiplier, float duration)
    {
        PlayerMove myMove = GetComponentInParent<PlayerMove>();
        if (myMove != null) myMove.skillSpeedMultiplier = multiplier;
        yield return new WaitForSeconds(duration);

        // 🔄 修正前：myMove.skillSpeedMultiplier = 1.0f;
        RestoreSpeedSafety(myMove); // ✨ 修正後：安全関数経由にしてガード！
    }

    // 拡張した内部データ管理クラス
    private class ExOrbTrackData
    {
        public Transform tx;
        public float angle;
        public float radius;
        public float currentSpeed; // 慣性等速ホーミング用の速度スタック
    }

    private void PlaySkillSE(string path)
    {
        string clip = string.IsNullOrEmpty(path) ? SEPath.SHOT1 : path;
        if (SEManager.Instance != null)
            SEManager.Instance.Play(clip, 0.4f);
    }
}