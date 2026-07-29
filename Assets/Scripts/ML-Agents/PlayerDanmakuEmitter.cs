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

    protected GameObject _rootOwner;
    protected bool _isArcReversed = false;
    // 現在アクティブなコルーチンの数をカウント[cite: 7]
    protected int _activeSkillCoroutines = 0;
    public bool IsAnySkillActive => _activeSkillCoroutines > 0;
    // 🎯【共用一本化】：EXスキル（ULT）が現在絶賛稼働中であることを示す唯一の絶対フラグ
    protected bool _isEXSkillActive = false;

    // 🎯【外部公開用プロパティ】：PlayerStatusManagerが無敵やタイマーストップを判定するために、この共用フラグを公開します
    public bool IsUltimateSkillActive => _isEXSkillActive;

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

    protected float GetAngleToTarget()
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

    protected float GetAngleToTarget(Vector3 fromPos)
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
    /// スキル設定に基づき、弾幕を生成・射出するメインエントランス（完全オブジェクト指向新調版）
    /// </summary>
    public void Fire(PlayerSkillData.SkillSettings s)
    {
        if (!enabled)
        {
            // 🛠️ 修正：transform.parent をパージし、同じオブジェクトから安全に全Emitterを全抽出！
            PlayerDanmakuEmitter[] allEmitters = GetComponents<PlayerDanmakuEmitter>();
            foreach (var emitter in allEmitters)
            {
                if (emitter != null && emitter.enabled)
                {
                    emitter.Fire(s);
                    return;
                }
            }
            return;
        }


        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        if (!PlayerMove.CanShoot) return;
        if (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal) return;
        if (s.bulletData == null || s.bulletData.bulletPrefab == null) return;
        if (_isEXSkillActive && s.patternType != SkillPatternType.Line) return;

        var myStatusMgr = GetComponentInParent<PlayerStatusManager>();
        PlayerMove myMove = _rootOwner.GetComponent<PlayerMove>();

        // 🌟【修正】：自身が現在領域展開中（isSpellCardActive）でない場合のみ、スキル使用時のULTゲージ増加を有効にする
        bool isMyVjtActive = (myStatusMgr != null && myStatusMgr.isSpellCardActive);

        if (myMove != null && myStatusMgr != null && !isMyVjtActive && s.skillName != myStatusMgr.characterData.skillZ.skillName)
        {
            float finalGain = s.ultimateGain;
            PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
            if (myStatus != null && myStatus.isOverheated)
            {
                finalGain *= 0.5f;
            }
            myMove.AddUltimateEnergy(finalGain);
        }

        // 🎯【速度干渉ガード】：魔方陣EXの発動中は通常スキルの移動デバフを完全カット
        if (!_isEXSkillActive && s.moveSpeedMultiplier < 1.0f)
        {
            StartCoroutine(TemporarySlow(s.moveSpeedMultiplier, 0.2f));
        }

        float targetAngle = GetAngleToTarget();
        float baseAngle = targetAngle + s.angleOffset;
        Vector3 pos = transform.position;

        // 📊【上流インフラ】：領域展開中の弾速ブースト共通処理
        PlayerStatusManager emitterStatus = GetComponentInParent<PlayerStatusManager>();
        if (emitterStatus != null && emitterStatus.isSpellCardActive)
        {
            PlayerSkillData.SkillSettings enhancedSettings = s;
            enhancedSettings.speed = s.speed * 1.3f;
            s = enhancedSettings;
        }

        // =========================================================================
        // 🔮【大罪仕分けパージ】：技名によるポリモーフィック自動中継インフラ
        // =========================================================================

        if (myStatusMgr != null && myStatusMgr.characterData != null)
        {
            var data = myStatusMgr.characterData; 
            
            // 🛠️ 修正：ただの StartCoroutine ではなく「this.StartCoroutine」に固定することで、
            //    自分が Emitter_Greed なら Greed の、Emitter_Wrath なら Wrath の上書き関数を100%正確に呼び出します。
            if (s.skillName == data.skillZ.skillName) { this.StartCoroutine(this.ExecuteSkillZ(s)); return; }
            if (s.skillName == data.skillX.skillName) { this.StartCoroutine(this.ExecuteSkillX(s)); return; }
            if (s.skillName == data.skillC.skillName) { this.StartCoroutine(this.ExecuteSkillC(s)); return; }
            if (s.skillName == data.skillV.skillName) { this.StartCoroutine(this.ExecuteSkillV(s)); return; }
        }


    }

    /// <summary>
    /// 独立したEX枠のデータを受け取り、固有の必殺技をキックする
    /// </summary>
    public void FireEX(PlayerSkillData.SkillSettings s)
    {
        // 🛡️ EX用アクティブバトン中継
        if (!enabled)
        {
            // 🛠️ 修正：ここも同様に GetComponents<PlayerDanmakuEmitter>() に修正します
            PlayerDanmakuEmitter[] allEmitters = GetComponents<PlayerDanmakuEmitter>();
            foreach (var emitter in allEmitters)
            {
                if (emitter != null && emitter.enabled)
                {
                    emitter.FireEX(s);
                    return;
                }
            }
            return;
        }

        if (!PlayerMove.CanShoot) return;

        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        if (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal) return;

        // 🎯【多重入力防止】：EXスキルがすでに稼働中ならボタン連打を遮断
        if (_isEXSkillActive) return;

        // 🌟 共通インフラ（硬直制御・例外安全ライフサイクル）を開始
        StartCoroutine(ExecuteEXInfrastructureRoutine(s));
    }

    /// <summary>
    /// EX/超必殺の共通インフラ（器）
    /// 💡 スイッチ判定を完全撤廃！子クラスがオーバーライドした固有技（ExecuteSkillEX）を直接キックします。
    /// </summary>
    protected IEnumerator ExecuteEXInfrastructureRoutine(PlayerSkillData.SkillSettings s)
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

            PlayerSkillData.SkillSettings enhancedEXSettings = s;

            // 🔮 領域展開中の弾速1.3倍ブースト共通インフラ処理
            if (isZoneActive)
            {
                enhancedEXSettings.speed = s.speed * 1.3f;
                s = enhancedEXSettings;
            }

            // =========================================================================
            // 🎯【EX中継インフラ】：子クラス側の ExecuteSkillEX が自身の時間軸で強化版を自律制御します
            // =========================================================================
            yield return StartCoroutine(ExecuteSkillEX(s));
        }
        finally
        {
            if (myMove != null) myMove.skillSpeedMultiplier = 1.0f;
            _activeSkillCoroutines--;
        }
        yield return null;
    }

    // =========================================================================
    // 📊 キャラクター固有技のオーバーライド用バーチャルスロット（土台）
    // =========================================================================
    protected virtual IEnumerator ExecuteSkillZ(PlayerSkillData.SkillSettings s) { yield return null; }
    protected virtual IEnumerator ExecuteSkillX(PlayerSkillData.SkillSettings s) { yield return null; }
    protected virtual IEnumerator ExecuteSkillC(PlayerSkillData.SkillSettings s) { yield return null; }
    protected virtual IEnumerator ExecuteSkillV(PlayerSkillData.SkillSettings s) { yield return null; }
    protected virtual IEnumerator ExecuteSkillEX(PlayerSkillData.SkillSettings s) { yield return null; }

    // =========================================================================
    // 🛠️【リファクタリング】：独自のInstantiateをパージし、CreateShotへ完全統合！
    // =========================================================================
    // =========================================================================
    // 🛠️【リファクタリング】：独自のInstantiateをパージし、CreateShotへ完全統合！
    // =========================================================================
    // =========================================================================
    // 🛠️【リファクタリング】：SubShot生成時の引数混線・オーラ消失を完全破砕
    // =========================================================================
    public void ExecuteSubShot(BulletData data, Vector3 pos, float speed, float angle, float accel, float maxSpeed, string tag, int layer, float delay_ = 0f)
    {
        if (data == null || data.bulletPrefab == null) return;

        if (delay_ < 1) { delay_ = 1; }

        if (SEManager.Instance != null)
        {
            SEManager.Instance.Play(SEPath.SHOT2, 0.1f);
        }

        // ⭕ 修正：すべての引数に「名前（引数名:）」を明示的に指定して、順番のねじれを完全にロック！
        //          これにより、子弾幕(SubShot)としてプールから目覚めた際も、100%確実に即座にオーラが生成されます。
        CreateShot(
            data: data,
            pos: pos,
            speed: speed,
            angle: angle,
            delay: delay_,
            isConverge: false,
            accel: accel,
            maxSpeed: maxSpeed,
            customMaterial: null,
            customScale: 1.0f,
            isIndestructible: false
        );
    }
    // =========================================================================
    // 🛠️【リファクタリング】：SubShot02生成時の引数・角速度拡張版
    // =========================================================================
    public void ExecuteSubShot02(BulletData data, Vector3 pos, float speed, float angle, float accel, float maxSpeed, string tag, int layer, float angularVelocity = 0f, float maxRotationLimit = 0f)
    {
        if (data == null || data.bulletPrefab == null) return;

        if (SEManager.Instance != null)
        {
            SEManager.Instance.Play(SEPath.SHOT2, 0.1f);
        }

        // ⭕ 拡張：名前付き引数で角速度と最大回転制限をCreateShotへ安全にパッシング
        CreateShot(
            data: data,
            pos: pos,
            speed: speed,
            angle: angle,
            delay: 0,
            isConverge: false,
            accel: accel,
            maxSpeed: maxSpeed,
            customMaterial: null,
            customScale: 1.0f,
            isIndestructible: true,
            angularVelocity: angularVelocity,
            maxRotationLimit: maxRotationLimit
        );
    }
    // =========================================================================
    // 🔮【超軽量化インフラ】：生成したオブジェクトを直接返却するファクトリ中継窓口
    // 💡 理由：これを経由することで、ExplosionField側での重いOverlapCircleAllを完全パージします
    // =========================================================================
    public DanmakuBullet ExecuteSubShot02_Returnable(BulletData data, Vector3 pos, float speed, float angle, float accel, float maxSpeed, string tag, int layer, float angularVelocity = 0f, float maxRotationLimit = 0f)
    {
        if (data == null || data.bulletPrefab == null) return null;

        if (SEManager.Instance != null)
        {
            SEManager.Instance.Play(SEPath.SHOT2, 0.1f);
        }

        // CreateShotは元々 DanmakuBullet を返す構造になっているため、そのまま直接 return します
        return CreateShot(
            data: data,
            pos: pos,
            speed: speed,
            angle: angle,
            delay: 0f,
            isConverge: false,
            accel: accel,
            maxSpeed: maxSpeed,
            customMaterial: null,
            customScale: 1.0f,
            isIndestructible: true,
            angularVelocity: angularVelocity,
            maxRotationLimit: maxRotationLimit
        );
    }

    protected void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursive(child.gameObject, layer);
        }
    }
    // =========================================================================
    // 🔮【一般化インフラ】：全員共用・愛の設置射出型ストリームレーザー共通インフラ
    // 💡【弾源の空間固定化】：発動した瞬間の座標に弾源（起点）を完全固定ロック！
    //    自機がどこへダッシュして移動しようとも、レーザーの根本はその場に居座り続けます。
    // =========================================================================
    protected IEnumerator ExecuteStreamLaserInfrastructure(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;
        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        PlayerMove myMove = _rootOwner.GetComponent<PlayerMove>();

        if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = s.moveSpeedMultiplier;

        try
        {
            int laserIntervalFrames = 1;
            int totalBulletSegments = Mathf.Max(10, s.count);
            float laserWidthScale = (s.wideAngle > 0f) ? s.wideAngle : 1.0f;

            PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
            bool isSpellActive = (myStatus != null && myStatus.isSpellCardActive);

            int wayCount = isSpellActive ? 5 : 3;
            float spreadAngle = 24f;

            Material additiveMaterial = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
            additiveMaterial.hideFlags = HideFlags.DontSave;

            // =========================================================================
            // 🎯【最核心修正】：発動した瞬間の自機の座標を「世界の起点」として完全固定
            // =========================================================================
            Vector3 spawnOrigin = transform.position; // 👈 ループの外で現在の座標をスナップショット記録

            for (int f = 0; f < totalBulletSegments; f++)
            {
                if (!PlayerMove.CanShoot || (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal))
                    break;

                if (f % 4 == 0) PlaySkillSE(s.sePath);

                // 🎯【設置型ロックオン】：固定された弾源座標（spawnOrigin）から、現在の敵機への最新角度を毎フレーム逆算！
                float currentTargetAngle = GetAngleToTarget(spawnOrigin);
                float currentBaseAngle = currentTargetAngle + s.angleOffset;

                float startAngle = currentBaseAngle - (spreadAngle / 2f);
                float stepAngle = spreadAngle / (wayCount - 1);

                for (int w = 0; w < wayCount; w++)
                {
                    float finalLaserAngle = startAngle + (stepAngle * w);

                    // 🛠️ 生成位置を transform.position ➔ 固定された「spawnOrigin」へ変更！
                    CreateShot(s.bulletData, spawnOrigin, s.speed, finalLaserAngle, delay: 0f,
                               isConverge: false, accel: 0f, maxSpeed: 0f,
                               customMaterial: additiveMaterial, customScale: laserWidthScale);
                }

                for (int i = 0; i < laserIntervalFrames; i++)
                {
                    yield return new WaitForFixedUpdate();
                }
            }

            yield return new WaitForSeconds(s.cooldown);
        }
        finally
        {
            if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = 1.0f;
            _activeSkillCoroutines--;
        }
    }

    // =========================================================================
    // 🎯【完全一本化・ファクトリ最高拡張版】：すべての弾幕・子弾・特殊軌跡の生成を集約
    // 💡 引数の末尾に `bool isIndestructible = false` を新規完全ドッキング！
    // =========================================================================
    // =========================================================================
    // 🎯【ファクトリ最高拡張版】：角速度 (angularVelocity) と最大回転角 (maxRotationLimit) をドッキング
    // =========================================================================
    // 🎯【根本バグ修正版】：オーラ消失・レイヤーねじれを完全破砕する超先行初期化ファクトリ
    protected DanmakuBullet CreateShot(BulletData data, Vector3 pos, float speed, float angle, float delay,
                                       bool isConverge = false, float accel = 0f, float maxSpeed = 0f,
                                       Material customMaterial = null, float customScale = 1.0f,
                                       bool isIndestructible = false,
                                       float angularVelocity = 0f, float maxRotationLimit = 0f)
    {
        if (data == null || data.bulletPrefab == null) return null;

        if (delay < 1.0f) { delay = 1.0f; }

        BulletData runtimeData = Instantiate(data);
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
        bool isSpear = (runtimeData != null && (runtimeData.name.Contains("Spear") || runtimeData.bulletPrefab.name.Contains("Spear")));

        Quaternion initialRotation = isSpear && delay > 0f
            ? Quaternion.Euler(90f, 0f, angle - 90f)
            : Quaternion.Euler(0f, 0f, angle - 90f);

        // =========================================================================
        // 🌟 1. プレハブの実体化（ここが世界の起点）
        // =========================================================================
        GameObject obj = null;
        if (BulletPool.Instance != null)
        {
            obj = BulletPool.Instance.Get(data.bulletPrefab, pos, initialRotation);
        }
        else
        {
            obj = Instantiate(data.bulletPrefab, pos, initialRotation);
        }

        if (obj == null) return null;

        // =========================================================================
        // 🎯【最核心の修正】：何よりも先に、生成直後の「最初の1行」で Initialize を叩き込む！
        // =========================================================================
        // 💡 理由：タグやレイヤーのループ処理、マテリアル変更を先に行うと、
        //    その重い処理の間に子オブジェクトのオーラ描画システムが一瞬だけ「データ未注入」の状態で
        //    フレームを跨いでしまい、描画がパージ（消失）されてしまいます。
        //    生成直後に脳直でデータを直撃同期させることで、オーラ消失を根本から100%防ぎます。
        DanmakuBullet bullet = obj.GetComponent<DanmakuBullet>();
        if (bullet != null)
        {
            float finalMaxSpeed = (maxSpeed == 0f) ? speed : maxSpeed;

            bullet.Initialize(
                shooter: _rootOwner,
                target: targetTag,
                speed: speed,
                angle: angle,
                accel: accel,
                maxSpeed: finalMaxSpeed,
                angVel: angularVelocity,
                delay: delay,
                data: runtimeData,
                converge: isConverge
            );

            bullet.isIndestructible = isIndestructible;
        }

        // =========================================================================
        // 🔒 2. 同期完了後に、タグ・レイヤーなどの空間パラメーターを安全に上書き
        // =========================================================================
        string assignedTag = (ownerId == 1) ? "PlayerBullet" : "EnemyBullet";
        obj.tag = assignedTag;

        int assignedLayer = LayerMask.NameToLayer((ownerId == 1) ? "Player1Bullet" : "Player2Bullet");
        obj.layer = assignedLayer;
        SetLayerRecursive(obj, assignedLayer);

        float finalBulletScale = runtimeData.bulletScale * customScale;
        obj.transform.localScale = new Vector3(finalBulletScale, finalBulletScale, 1.0f);

        if (customMaterial != null)
        {
            SpriteRenderer mainSR = obj.GetComponentInChildren<SpriteRenderer>();
            if (mainSR != null) mainSR.material = customMaterial;
        }

        obj.transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);

        return bullet;
    }

    // =========================================================================
    // 🔮【新設・レーザー一本化コアインフラ】：すべての通常・設置・公転型レーザーの生成を集約
    // =========================================================================
    protected EnemyLaserBeam CreateLaserShot(BulletData data, Vector3 pos, float speed, int count, float wideAngle, int warningFrame, bool isSetupB = false)
    {
        if (BulletManager.Instance == null || data == null || data.bulletPrefab == null) return null;

        BulletData runtimeData = Instantiate(data);
        BulletManager.LaserColor color = runtimeData.laserColor;
        var laserSet = BulletManager.Instance.GetLaserSet(color);

        PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
        if (myStatus == null && _rootOwner != null) myStatus = _rootOwner.GetComponent<PlayerStatusManager>();
        int ownerId = (myStatus != null) ? myStatus.playerId : 1;

        // ⚔️ レーザー射出の瞬間における攻撃ランク ＆ 憤怒・嫉妬パッシブバフの動的完全結合
        int finalLaserDamage = runtimeData.damage;
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
            finalLaserDamage = Mathf.RoundToInt(finalLaserDamage * atkMultiplier);
        }

        // 1. レーザーオブジェクトの共通実体化
        GameObject laserObj = Instantiate(BulletManager.Instance.laserBeamPrefab, pos, Quaternion.identity);

        // 2. チームに応じたタグ・レイヤーの厳密なパッシング
        string assignedTag = (ownerId == 1) ? "PlayerBullet" : "EnemyBullet";
        laserObj.tag = assignedTag;

        int assignedLayer = LayerMask.NameToLayer((ownerId == 1) ? "Player1Bullet" : "Player2Bullet");
        laserObj.layer = assignedLayer;
        SetLayerRecursive(laserObj, assignedLayer);

        EnemyLaserBeam laser = laserObj.GetComponent<EnemyLaserBeam>();
        if (laser != null)
        {
            // 3. 呼び出し元の要求形式（SetupA = 通常直線 / SetupB = 強欲設置回転）に応じて自動マッピング初期化
            if (isSetupB)
            {
                laser.SetupB(_rootOwner, targetTag, finalLaserDamage, pos.x, pos.y, count, wideAngle, color, warningFrame,
                             BulletManager.Instance.laserSourceEffectPrefab, laserSet.sourceEffectSprite, runtimeData);
            }
            else
            {
                laser.SetupA(_rootOwner, targetTag, finalLaserDamage, pos.x, pos.y, count, wideAngle, color, warningFrame,
                             BulletManager.Instance.laserSourceEffectPrefab, laserSet.sourceEffectSprite, runtimeData);
            }
        }

        return laser;
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


    protected void PlaySkillSE(string path)
    {
        string clip = string.IsNullOrEmpty(path) ? SEPath.SHOT1 : path;
        if (SEManager.Instance != null)
            SEManager.Instance.Play(clip, 0.4f);
    }
}