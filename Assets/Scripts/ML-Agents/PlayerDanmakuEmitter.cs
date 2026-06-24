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
    protected bool _isXLineReversed; // ⚔️ カリン専用Xの往復切り替え用フラグ
    // スキル使用中（コルーチンが1つ以上動いている）かどうかを返すプロパティ
    public bool IsAnySkillActive => _activeSkillCoroutines > 0;
    // 🎯【共用一本化】：EXスキル（ULT）が現在絶賛稼働中であることを示す唯一の絶対フラグ
    protected bool _isEXSkillActive = false;

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

        // 💡 固有スキルパターン以外の汎用SE再生
        if (s.patternType != SkillPatternType.MovingArc &&
            s.patternType != SkillPatternType.RandomRound)
        {
            // ※防御フィールド等の固有消音ガードは子クラス側で制御されるため、通常はここでSEを鳴らします
            PlaySkillSE(s.sePath);
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
        var myStatusMgr = GetComponentInParent<PlayerStatusManager>();
        if (myStatusMgr != null && myStatusMgr.characterData != null)
        {
            var data = myStatusMgr.characterData; 
            
            // 🛠️ 修正：ただの StartCoroutine ではなく「this.StartCoroutine」に固定することで、
            //    自分が Emitter_Greed なら Greed の、Emitter_Wrath なら Wrath の上書き関数を100%正確に呼び出します。
            if (s.skillName == data.skillZ.skillName) { this.StartCoroutine(this.ExecuteSkillZ(s)); return; }
            if (s.skillName == data.skillX.skillName) { this.StartCoroutine(this.ExecuteSkillX(s)); return; }
            if (s.skillName == data.skillC.skillName) { this.StartCoroutine(this.ExecuteSkillC(s)); return; }
            if (s.skillName == data.skillV.skillName) { StartCoroutine(ExecuteSkillV(s)); return; }
        }

        // 💡 共通の Standard や nWay などの一般枠はそのままフォールバック実行
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
            case SkillPatternType.Custom:
                ExecuteConvergePattern(s, pos, baseAngle);
                break;
            case SkillPatternType.MovingArc:
                StartCoroutine(MovingArcRoutine(s));
                break;
            case SkillPatternType.RandomRound:
                StartCoroutine(ExecuteRandomRoundRoutine(s));
                break;
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

    protected void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursive(child.gameObject, layer);
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
    protected void CreateShot(BulletData data, Vector3 pos, float speed, float angle, float delay, bool isConverge = false)
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
    protected void PlaySkillSE(string path)
    {
        string clip = string.IsNullOrEmpty(path) ? SEPath.SHOT1 : path;
        if (SEManager.Instance != null)
            SEManager.Instance.Play(clip, 0.4f);
    }
}