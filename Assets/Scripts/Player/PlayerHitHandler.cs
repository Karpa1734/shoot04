using KanKikuchi.AudioManager;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// プレイヤーの被弾、食らいボム、復活処理を管理するクラス（横STG対戦用）
/// </summary>
public class PlayerHitHandler : MonoBehaviour
{
    public enum PlayerState { Normal, DeathBomb, Hit, Down, Rebirth }
    public PlayerState currentState = PlayerState.Normal;

    [Header("Settings")]
    public float deathBombWindow = 0.15f;
    public float invincibilityTime = 2.0f;
    public float downTime = 0.8f;
    public float stunTime = 2.0f; // スタン時間（2秒）

    [Header("--- Hit Cap Settings (VJT Density Counter) ---")]
    [Tooltip("1フレーム内に同時にヒットしていい弾の最大数。全方位弾などの一瞬の全壊を防ぎます")]
    public int maxHitsPerFrame = 2;
    private int currentHitsInThisFrame = 0;
    private int lastProcessedFrame = -1;
    // 🌟 処理の二重実行を防ぐためのロック用フラグ
    private bool _isHandlingExplosion = false;
    [Header("References")]
    public GameObject explosionEffectPrefab;
    public PlayerAnimation playerAnim;
    public PlayerMove playerMove;
    public GameObject bulletClearPrefab;

    [Header("Multiplayer Support")]
    public PlayerStatusManager myStatusManager;

    // 🌟【新規管理フラグ】：時間切れによる強制爆発コルーチン呼び出しであるかを判別する
    [HideInInspector] public bool isTriggeredByTimeUp = false;
    [Tooltip("作成したDamagePopupのプレハブを登録してください")]
    public GameObject damagePopupPrefab; // 🌟新規追加
    private SpriteRenderer characterRenderer;
    private ItemEffectHandler itemHandler;

    void Awake()
    {
        if (playerMove == null) playerMove = GetComponentInParent<PlayerMove>();
        if (playerAnim == null) playerAnim = GetComponentInParent<PlayerAnimation>();

        itemHandler = GetComponent<ItemEffectHandler>();
        characterRenderer = GetComponentInParent<SpriteRenderer>();

        if (characterRenderer == null)
        {
            characterRenderer = transform.parent.GetComponentInChildren<SpriteRenderer>();
        }
    }

    void Update()
    {
     
    }

    void LateUpdate()
    {
        if (Time.frameCount != lastProcessedFrame)
        {
            currentHitsInThisFrame = 0;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Item"))
        {
            if (itemHandler != null) itemHandler.HandleItemCollision(collision);
            return;
        }
    }

    public void OnHit(int damage)
    {
        Vector3 hitPos = transform.position;

        if (playerMove.IsInvincible || currentState == PlayerState.Down || currentState == PlayerState.Rebirth) return;

        if (myStatusManager != null && myStatusManager.isOverheated && !myStatusManager.isSpellCardActive && Time.frameCount == lastProcessedFrame)
        {
            return;
        }

        if (Time.frameCount == lastProcessedFrame)
        {
            currentHitsInThisFrame++;

            if (currentHitsInThisFrame > maxHitsPerFrame)
            {
                return;
            }
        }
        else
        {
            lastProcessedFrame = Time.frameCount;
            currentHitsInThisFrame = 1;
        }

        bool isDown = false;

        // 事前に今回のダメージで通常HPが全損（0以下）するか、あるいはスペルカード破砕かをシミュレート先読み
        bool willSpellCardEnd = myStatusManager != null && myStatusManager.isSpellCardActive && (myStatusManager.spellHP - damage <= 0);
        bool isLastHitOnNormalHP = myStatusManager != null && !myStatusManager.isSpellCardActive && (myStatusManager.currentHP - damage <= 0);


        if (myStatusManager != null)
        {
            DanmakuAgent agent = GetComponentInParent<DanmakuAgent>();
            if (agent != null)
            {
                agent.GiveHitPenalty();
            }

            // 🌟【最重要】：バリア展開中であったかを事前に記録
            bool wasSpellActive = myStatusManager.isSpellCardActive;

            // ダメージの実際の適用（バリアが削り切られたらここで true が返ります）
            isDown = myStatusManager.ApplyDamage(damage);

            // 🌟 ストーリーボスのスペルバリアが剥がれた（削り切られた）瞬間である場合、
            // 通常HPの残りに関わらず、即座にダウン（フェーズ進行）を確定させる！
            if (wasSpellActive && !myStatusManager.isSpellCardActive)
            {
                if (GameModeManager.IsStoryMode && myStatusManager.playerId == 2)
                {
                    isDown = true;
                }
            }

            // 🌟【新規追加】：ダメージポップアッププレハブを動的生成
            if (damagePopupPrefab != null)
            {
                Vector3 spawnPos = hitPos + new Vector3(0f, 0.5f, 0f);
                GameObject popupGo = Instantiate(damagePopupPrefab, spawnPos, Quaternion.identity);

                DamagePopup popupScript = popupGo.GetComponent<DamagePopup>();
                if (popupScript != null)
                {
                    popupScript.Setup(damage);
                }
            }
        }

        // 【音響条件3】：スペルカード（VJT）が被弾ダメージによって破砕終了した瞬間は、被弾音を100%カットして早期リターン
        if (willSpellCardEnd)
        {
            return;
        }

        // VJTバリア持続中の通常ガード音（アーマー耐久音）
        if (myStatusManager != null && myStatusManager.isSpellCardActive)
        {
            if (SEManager.Instance != null)
            {
                SEManager.Instance.Play(SEPath.SE_DAMAGE00, 0.5f);
            }
            return;
        }

        // 【音響条件2】：この被弾が「ゲームセットが決まる最後の一発（2勝先取）」の時は、BOSS_END_ENDを最優先させるため被弾音（SE_PLAYER_COLLISION）を強制ミュート！
        bool isMatchGameOverPreCheck = false;
        if (myStatusManager != null && !GameModeManager.IsStoryMode && playerMove != null && playerMove.Opponent != null)
        {
            PlayerStatusManager oppStatus = playerMove.Opponent.GetComponent<PlayerStatusManager>();
            if (oppStatus != null && oppStatus.life >= 1 && isLastHitOnNormalHP)
            {
                isMatchGameOverPreCheck = true;
            }
        }

        if (explosionEffectPrefab != null) Instantiate(explosionEffectPrefab, hitPos, Quaternion.identity);

        // 最後の一発でなければ通常の被弾音を綺麗に再生
        if (!isMatchGameOverPreCheck)
        {
            if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.SE_PLAYER_COLLISION, 0.3f);
        }

        if (isDown)
        {
            isTriggeredByTimeUp = false;
            currentState = PlayerState.Hit;
            StartCoroutine(ExplosionAndStunRoutine());
        }
        else
        {
            StartCoroutine(DamageStunRoutine());
        }
    }

    IEnumerator DamageStunRoutine()
    {
        currentState = PlayerState.Hit;

        if (playerMove != null) playerMove.enabled = false;
        playerMove.SetInvincible(1.0f);

        yield return new WaitForSeconds(0.4f);

        if (playerMove != null) playerMove.enabled = true;
        currentState = PlayerState.Normal;
    }

    IEnumerator ExplosionAndStunRoutine()
    {

        Vector3 hitPos = transform.position;

        if (playerMove.IsInvincible) yield break;

        currentState = PlayerState.Down;
        // =========================================================================
        // 🌟【最重要追加】：対戦終了・撃墜の瞬間に、タイマーの駆動フラグ自体を完全停止させる！
        // =========================================================================
        if (MatchTimerUI.Instance != null)
        {
            MatchTimerUI.Instance.StopTimer();
            MatchTimerUI.Instance.StopMatch(); // 👈 試合終了フラグ（isMatchStarted = false）をここで確定！
        }
        // =========================================================================
        // 🧠【強化学習専用：爆速超高速リセットインフラ】
        // =========================================================================
        // 💡 目的：誰かが撃墜された瞬間、演出・スローモーションを100%全カットして即死即リセット。
        DanmakuAgent myAgent = GetComponentInParent<DanmakuAgent>();
        bool isTrainingMode = (myAgent != null && Unity.MLAgents.Academy.Instance.IsCommunicatorOn);
        if (playerMove != null && playerMove.Opponent != null && !isTrainingMode)
        {
            var oppAgent = playerMove.Opponent.GetComponentInChildren<DanmakuAgent>();
            if (oppAgent != null && Unity.MLAgents.Academy.Instance.IsCommunicatorOn) isTrainingMode = true;
        }

        if (isTrainingMode)
        {
            // 1. 全弾幕・レーザーを即座に消去して画面をクリーンにする
            ClearAllBullets(true);

            // 2. スローモーションを発生させず、完全に等速（1.0f）を維持
            Time.timeScale = 1.0f;

            // 3. 領域展開（VJT）が残っていれば即座に完全消去
            foreach (var p in PlayerMove.AllPlayers)
            {
                if (p == null) continue;
                PlayerStatusManager status = p.GetComponent<PlayerStatusManager>();
                if (status != null && status.isSpellCardActive)
                {
                    status.DeactivateSpellCard(false);
                }
            }

            // 4. お互いのエージェントに「この試合は終わり（EndEpisode）」を通達して脳の学習を1区切りさせる
            foreach (var p in PlayerMove.AllPlayers)
            {
                if (p == null) continue;
                DanmakuAgent agent = p.GetComponentInChildren<DanmakuAgent>();
                if (agent != null)
                {
                    // 被弾した本人にはすでにOnHitでGiveHitPenalty()が入っているので、ここで一気にエピソードを締めくくります
                    agent.EndEpisode();
                }

                // 体力やリソース、タイマーを全快にして初期化
                PlayerStatusManager status = p.GetComponent<PlayerStatusManager>();
                if (status != null) status.currentHP = status.maxHP; // HP全快

                SkillManager sm = p.GetComponentInChildren<SkillManager>();
                if (sm != null) sm.InstantFullRecovery(); // マナ・リキャスト全快
            }

            // 5. 1.8秒の滑らか移動（巡航）を完全無視し、お互いを一瞬で初期配置（±3.5）に強制ワープ
            foreach (var p in PlayerMove.AllPlayers)
            {
                if (p == null) continue;
                PlayerStatusManager ps = p.GetComponent<PlayerStatusManager>();
                PlayerHitHandler hh = p.GetComponentInChildren<PlayerHitHandler>();
                if (ps != null)
                {
                    float targetX = (ps.playerId == 2) ? 3.5f : -3.5f;
                    p.transform.position = new Vector3(targetX, 0f, 0f);
                }
                if (hh != null)
                {
                    hh.SetPlayerActiveState(true);
                    hh.currentState = PlayerState.Normal;
                }
            }

            // 6. カウントダウン演出もスキップして、即座に次の試合の追跡入力を開始
            PlayerMove.CanInput = true;
            PlayerMove.CanShoot = true;

            if (MatchTimerUI.Instance != null) MatchTimerUI.Instance.ResetRoundTimer(99f);

            isTriggeredByTimeUp = false;
            currentState = PlayerState.Normal;

            yield break; // 💡 コルーチンをここで即座に脱出し、以下の格ゲー演出ルートに絶対に進ませない！
        }

        if (MatchTimerUI.Instance != null) MatchTimerUI.Instance.StopTimer();
        Time.timeScale = 0.3f; // 🌟スローモーション開始

        // =========================================================================
        // 🌟【新規追加】：決着の瞬間に、展開されているVJT領域を強制終了（通常背景へ復帰）
        // =========================================================================
        // 画面内の全プレイヤー（1P, 2P両方）のStatusManagerに対して、領域終了を即座に命令
        foreach (var p in PlayerMove.AllPlayers)
        {
            if (p == null) continue;
            PlayerStatusManager status = p.GetComponent<PlayerStatusManager>();
            if (status != null && status.isSpellCardActive)
            {
                // falseを渡すことで、自然消滅と同じ扱いで安全に魔法陣・バリア・専用2D背景をパージします
                status.DeactivateSpellCard(false);
            }
        }

        PlayerMove.CanInput = true;
        PlayerMove.CanShoot = false;

        // 💡 タイムアップによって外部（EvaluateTimeUpVictory）から呼ばれた場合は、
        // すでにHPが残っている状態でフラグがONになっているため、ここのHP0化によるデータ破壊をスキップ！
        if (!isTriggeredByTimeUp && myStatusManager != null && myStatusManager.currentHP > 0f)
        {
            isTriggeredByTimeUp = true;
        }

        if (myStatusManager != null)
        {
            // 🎯【修正】：ストーリーモードのボス（Player 2）撃破時は、次段階への移行のために
            // 強制的にcurrentHPを0にする処理をバイパス（または除外）する！
            bool isStoryBossDefeat = GameModeManager.IsStoryMode && myStatusManager.playerId == 2;

            if (!isTriggeredByTimeUp && !isStoryBossDefeat)
            {
                myStatusManager.currentHP = 0;
            }
            myStatusManager.SendMessage("UpdateUI", SendMessageOptions.DontRequireReceiver);
        }

        // 星の計算の事前評価
        bool isMatchGameOver = false;
        if (myStatusManager != null && !GameModeManager.IsStoryMode && playerMove != null && playerMove.Opponent != null)
        {
            PlayerStatusManager oppStatus = playerMove.Opponent.GetComponent<PlayerStatusManager>();
            if (oppStatus != null && oppStatus.life >= 1)
            {
                isMatchGameOver = true;
            }
        }

        if (myStatusManager != null)
        {
            myStatusManager.SubtractLifeAndCheckRebirth();
        }
        ClearAllBullets(true);

        // 2勝決着時のみ、この爆散の瞬間にSEを最優先再生
        if (isMatchGameOver)
        {
            if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.BOSS_END_END, 0.6f);
        }

        if (explosionEffectPrefab != null) Instantiate(explosionEffectPrefab, hitPos, Quaternion.identity);

        foreach (var p in PlayerMove.AllPlayers)
        {
            if (p == null) continue;
            SkillManager sm = p.GetComponentInChildren<SkillManager>();
            if (sm != null)
            {
                sm.InstantFullRecovery();
            }
        }

        yield return null;

        if (GameModeManager.IsStoryMode)
        {
            currentState = PlayerState.Hit;
            if (playerMove != null) playerMove.enabled = false;

            bool isHumanPlayer = (myStatusManager != null && myStatusManager.playerId == 1);

            if (isHumanPlayer)
            {
                // =========================================================================
                // 🔷 1. 自機（1P）がやられた時：自機のみ全快 ➔ カウントダウン後再開（ボスHPそのまま）
                // =========================================================================
                yield return new WaitForSecondsRealtime(2.0f);
                Time.timeScale = 1.0f;
                yield return new WaitForSeconds(1.0f);

                if (myStatusManager != null) yield return StartCoroutine(myStatusManager.GradualHealthRecovery(1.0f));

                currentState = PlayerState.Normal;
                if (playerMove != null) playerMove.enabled = true;

                playerMove.SetInvincible(invincibilityTime);

                PlayerMove.CanShoot = false;
                if (GameStartCountdown.Instance != null)
                {
                    GameStartCountdown.Instance.StartCountdown(); // 🎯 カウントダウンあり！
                }
                else
                {
                    PlayerMove.CanShoot = true;
                    if (MatchTimerUI.Instance != null) MatchTimerUI.Instance.ResumeTimer();
                }
            }
            else
            {
                // =========================================================================
                // 🔶 2. ストーリーボスの被弾・バリア破壊時の演出
                // (※フェーズ進行やクリア判定は StoryBossPhaseManager 側に完全に一元化しています)
                // =========================================================================
                Time.timeScale = 1.0f;

                // 画面上の弾を綺麗にクリア
                ClearAllBullets(true);

                StoryBossPhaseManager phaseMgr = GetComponentInParent<StoryBossPhaseManager>();
                if (phaseMgr == null) phaseMgr = GetComponent<StoryBossPhaseManager>();

                bool hasMorePhases = (phaseMgr != null && phaseMgr.HasRemainingPhases());

                if (hasMorePhases)
                {
                    // 🔮 中間フェーズ・スペル切替時：爆発エフェクトを再生して即座に通常状態へ復帰
                    if (explosionEffectPrefab != null)
                    {
                        Instantiate(explosionEffectPrefab, hitPos, Quaternion.identity);
                    }

                    _isHandlingExplosion = false;
                    currentState = PlayerState.Normal;
                    isTriggeredByTimeUp = false;

                    // 🌟【最重要修正】：中間フェーズ切替時は、ボスの移動・入力・射撃を完全に再有効化する！
                    if (playerMove != null)
                    {
                        playerMove.enabled = true;
                    }
                    PlayerMove.CanInput = true;
                    PlayerMove.CanShoot = true;
                    SetPlayerActiveState(true);

                    yield break;
                }
                else
                {
                    // 💥 最終段階（真の最終撃破）時：VSモード同様に爆発して姿を消す
                    PlayerMove.CanShoot = false;
                    PlayerMove.CanInput = false;

                    if (explosionEffectPrefab != null)
                    {
                        Instantiate(explosionEffectPrefab, hitPos, Quaternion.identity);
                    }

                    SetPlayerActiveState(false);

                    yield return new WaitForSecondsRealtime(0.8f);

                    _isHandlingExplosion = false;
                    currentState = PlayerState.Normal;
                    isTriggeredByTimeUp = false;
                    yield break;
                }
            }
        }
        else
        {
            // 🛑 ここは「完全なVSモード（対戦モード）」の時だけ通るようにする
            if (isMatchGameOver)
            {
                SetPlayerActiveState(false);
            }
            else
            {
                if (playerMove != null) playerMove.enabled = false;
            }

            yield return StartCoroutine(PerformKORoundEndRoutine(isMatchGameOver));
        }
    }
    

    public IEnumerator TriggerDrawSequence()
    {
        if (myStatusManager != null && myStatusManager.countdownText != null)
        {
            myStatusManager.countdownText.text = "DRAW";
            myStatusManager.countdownText.color = Color.white;
            myStatusManager.countdownText.gameObject.SetActive(true);
        }

        yield return new WaitForSeconds(2.0f);

        if (myStatusManager != null && myStatusManager.countdownText != null)
        {
            myStatusManager.countdownText.gameObject.SetActive(false);
        }

        yield return StartCoroutine(RoundResetSequence());
    }
    /// <summary>
    /// ⏳【タイムアップ勝敗判定判定インフラ】
    /// 1Pと2Pの残りHP割合（現在HP / 最大HP）を比較し、勝敗またはドローを決定して適切なシーエンスをキックします。
    /// </summary>
    public void EvaluateTimeUpVictory()
    {
        if (PlayerMove.AllPlayers == null || PlayerMove.AllPlayers.Count < 2) return;

        PlayerMove p1 = PlayerMove.AllPlayers[0];
        PlayerMove p2 = PlayerMove.AllPlayers[1];

        if (p1 == null || p2 == null) return;

        PlayerStatusManager s1 = p1.GetComponent<PlayerStatusManager>();
        PlayerStatusManager s2 = p2.GetComponent<PlayerStatusManager>();

        if (s1 == null || s2 == null) return;

        // --- 🧪 1. 両者の現在値と最大値の生データを安全に抽出 ---
        float p1Current = s1.isSpellCardActive ? s1.spellHP : s1.currentHP;
        float p1Max = s1.isSpellCardActive ? s1.spellMaxHP : s1.maxHP;

        float p2Current = s2.isSpellCardActive ? s2.spellHP : s2.currentHP;
        float p2Max = s2.isSpellCardActive ? s2.spellMaxHP : s2.maxHP;

        // --- 🛡️ 2. 【初期化フレームバグ・ランク格差の絶対肉壁ガード】 ---
        // 💡 理由：コルーチンの1フレーム待ちラグにより、ゲーム開始直後は初期値(100)に対してデータがズレる事象を回避します。
        // 現在のHPが最大HP以上、または最大HPとの差が 0.5f 未満（ほぼ無傷）なら、強制的に割合を「1.0 (100%満タン)」として扱います。
        float p1Ratio = 0f;
        if (p1Current >= p1Max || Mathf.Abs(p1Max - p1Current) < 0.5f)
        {
            p1Ratio = 1.0f; // 確定で満タン扱い
        }
        else
        {
            p1Ratio = p1Max > 0f ? p1Current / p1Max : 0f;
        }

        float p2Ratio = 0f;
        if (p2Current >= p2Max || Mathf.Abs(p2Max - p2Current) < 0.5f)
        {
            p2Ratio = 1.0f; // 確定で満タン扱い
        }
        else
        {
            p2Ratio = p2Max > 0f ? p2Current / p2Max : 0f;
        }

        Debug.Log($"<color=cyan>⏳ [Time Up Exact Check] 1P(ID:{s1.playerId}): {p1Ratio * 100f:F2}% (HP:{p1Current}/{p1Max}) | 2P(ID:{s2.playerId}): {p2Ratio * 100f:F2}% (HP:{p2Current}/{p2Max})</color>");

        // --- ⚔️ 3. 最終ジャッジ ---
        // 💡 差分が 0.0001f（0.01%未満）の極小の計算誤差範囲であれば、文句なしの完全ドロー（引き分け）にします。
        if (Mathf.Abs(p1Ratio - p2Ratio) < 0.0001f)
        {
            // 🛑 完全なる引き分け（ドロー）
            StartCoroutine(TriggerDrawSequence());
        }
        else
        {
            // ⚔️ 割合が低い方のプレイヤー（敗者）の爆散ルーチンを起動
            PlayerHitHandler loserHandler = (p1Ratio < p2Ratio)
                ? p1.GetComponentInChildren<PlayerHitHandler>()
                : p2.GetComponentInChildren<PlayerHitHandler>();

            if (loserHandler != null)
            {
                // 時間切れによる敗北フラグを立てて、通常の撃墜演出（スローモーション等）へ安全に流し込む
                loserHandler.isTriggeredByTimeUp = true;
                loserHandler.currentState = PlayerState.Hit;
                loserHandler.StartCoroutine(loserHandler.ExplosionAndStunRoutine());
            }
        }
    }

    /// <summary>
    /// 🌟【人間操作100%完全除外】：AI操作のキャラクターのみを初期位置へ自動巡航させます。
    /// 人間が操作しているキャラクターは自動移動も、最終フィックス（ワープ）も完全に「ノータッチ」にします。
    /// </summary>
   // =========================================================================
    // 🌟【同一シーン連動型リセットインフラ】：槍EX等によるデータ残存・チカチカを100%パージ
    // =========================================================================
    IEnumerator RoundResetSequence()
    {
        // 🌟【スローモーション解除の確実化】：自動移動開始のファーストフレームで確実に等倍復帰
        Time.timeScale = 1.0f; 

        // 全員の当たり判定やスプライトを復元
        foreach (var p in PlayerMove.AllPlayers) 
        {
            if (p == null) continue; 
            PlayerHitHandler hh = p.GetComponentInChildren<PlayerHitHandler>(); 
            if (hh != null) 
            {
                hh.SetPlayerActiveState(true); 
                hh.currentState = PlayerState.Normal; 
            }

            PlayerStatusManager ps = p.GetComponent<PlayerStatusManager>(); 
            if (ps != null) 
            {
                StartCoroutine(ps.GradualHealthRecovery(1.0f)); 

                // =========================================================================
                // 🛡️【最核心】：次ラウンドのスキル封印 ＆ UIチカチカバグの完全パージ
                // 💡 理由：同一シーン維持リセットのため、強制停止したEXコルーチンの残存データを
                //          ここで物理的に上書き初期化し、次ラウンドへの持ち込みを遮断します。
                // =========================================================================
                ps.isSpellCardActive = false; // 領域強制OFF
                ps.isOverheated = false;      // 冷却デバフ強制OFF[cite: 11, 16, 32]
                ps.spellTimer = 0f; 
                ps.overheatTimer = 0f; 
                ps.invincibleTimer = 0f; 
                
                // ゲージアニメーションフラグを叩き落としてチカチカを根治
                System.Reflection.FieldInfo animBarField = typeof(PlayerStatusManager).GetField("isAnimatingSpellBar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (animBarField != null) animBarField.SetValue(ps, false); 

                // 確定したマナ上限を最新の状態で再同期
                ps.SyncBarsImmediately(); 
            }

            // 🎯 各プレイヤーに付いている全エミッターのEX発動中ロックを完全解除
            PlayerDanmakuEmitter[] allEmitters = p.GetComponentsInChildren<PlayerDanmakuEmitter>(true); 
            foreach (var emitter in allEmitters) 
            {
                if (emitter != null)
                {
                    System.Reflection.FieldInfo exActiveField = typeof(PlayerDanmakuEmitter).GetField("_isEXSkillActive", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (exActiveField != null)
                    {
                        exActiveField.SetValue(emitter, false); // 硬直ロックを強制パージ[cite: 6, 19]
                    }
                }
            }

            // 無敵点滅アニメーションも確定OFF
            PlayerAnimation pAnim = p.GetComponentInChildren<PlayerAnimation>(true); 
            if (pAnim != null)
            {
                pAnim.isInvincible = false; 
            }
        }

        float moveDuration = 1.8f; 
        float elapsed = 0f; 

        System.Collections.Generic.Dictionary<GameObject, Vector3> startPositions = new System.Collections.Generic.Dictionary<GameObject, Vector3>(); 
        System.Collections.Generic.Dictionary<GameObject, Vector3> targetPositions = new System.Collections.Generic.Dictionary<GameObject, Vector3>(); 

        foreach (var p in PlayerMove.AllPlayers) 
        {
            if (p == null) continue; 
            PlayerStatusManager ps = p.GetComponent<PlayerStatusManager>(); 
            if (ps != null) 
            {
                startPositions[p.gameObject] = p.transform.position; 
                float targetX = (ps.playerId == 2) ? 3.5f : -3.5f; 
                targetPositions[p.gameObject] = new Vector3(targetX, 0f, 0f); 
            }
        }

        while (elapsed < moveDuration) 
        {
            elapsed += Time.deltaTime; 
            float rawPercent = Mathf.Clamp01(elapsed / moveDuration); 
            float smoothPercent = rawPercent * rawPercent * (3f - 2f * rawPercent); 

            foreach (var p in PlayerMove.AllPlayers) 
            {
                if (p == null || !startPositions.ContainsKey(p.gameObject)) continue; 

                bool isAIControlled = false; 
                if (GameModeManager.IsStoryMode) 
                {
                    PlayerStatusManager currentPs = p.GetComponent<PlayerStatusManager>(); 
                    if (currentPs != null && currentPs.playerId == 2) 
                    {
                        isAIControlled = true; 
                    }
                    else if (p.name.Contains("AI") || p.name.Contains("Enemy") || p.name.Contains("CPU")) 
                    {
                        isAIControlled = true; 
                    }
                }

                if (isAIControlled) 
                {
                    Vector3 nextPos = Vector3.Lerp(startPositions[p.gameObject], targetPositions[p.gameObject], smoothPercent); 
                    p.transform.position = nextPos; 

                    float clampedX = Mathf.Clamp(p.transform.position.x, -8.5f, 8.5f); 
                    float clampedY = Mathf.Clamp(p.transform.position.y, -4.5f, 4.5f); 
                    p.transform.position = new Vector3(clampedX, clampedY, 0f); 
                }
            }
            yield return null; 
        }

        foreach (var p in PlayerMove.AllPlayers) 
        {
            if (p == null) continue; 

            bool isAIControlled = false; 
            if (GameModeManager.IsStoryMode) 
            {
                PlayerStatusManager ps = p.GetComponent<PlayerStatusManager>(); 
                if (ps != null && ps.playerId == 2) isAIControlled = true; 
                else if (p.name.Contains("AI") || p.name.Contains("Enemy") || p.name.Contains("CPU")) isAIControlled = true; 
            }

            if (isAIControlled) 
            {
                PlayerStatusManager ps = p.GetComponent<PlayerStatusManager>(); 
                float targetX = (ps != null && ps.playerId == 2) ? 3.5f : -3.5f; 
                p.transform.position = new Vector3(targetX, 0f, 0f); 
            }
        }

        if (MatchTimerUI.Instance != null) 
        {
            MatchTimerUI.Instance.ResetRoundTimer(99f); 
        }

        if (GameStartCountdown.Instance != null) 
        {
            GameStartCountdown.Instance.StartCountdown(); 
        }
        else 
        {
            PlayerMove.CanInput = true; 
        }

        isTriggeredByTimeUp = false; 
        yield return null; 
    }

    /// <summary>
    /// 🌟【大修正】：スローモーションの持続時間を格ゲー準拠の心地いいタイムラインに調整
    /// </summary>
    IEnumerator PerformKORoundEndRoutine(bool isMatchGameOver)
    {
        if (myStatusManager != null && myStatusManager.koText != null)
        {
            myStatusManager.koText.text = isMatchGameOver ? "Game Set !!" : "Down !!";
            yield return myStatusManager.StartCoroutine(myStatusManager.PlayKOAnimation());
        }

        if (isMatchGameOver)
        {
            // 👑 1. 決着時：1.5秒のスローモーション演出
            yield return new WaitForSecondsRealtime(1.5f);
            Time.timeScale = 1.0f;

            // 「Game Set !!」の文字をフェードアウト
            if (myStatusManager != null && myStatusManager.koText != null)
            {
                yield return myStatusManager.StartCoroutine(myStatusManager.FadeOutKOAnimation(0.4f));
            }

            yield return new WaitForSeconds(0.2f);

            // =========================================================================
            // 🎯【演出改善①】：会話が始まる「直前」のタイミングで勝者メッセージをパッと表示！
            // =========================================================================
            ShowWinMessage();

            yield return new WaitForSeconds(1.0f);
            // =========================================================================
            // 👑【勝敗キャラ自動検知 ✕ 2通りCSV動的分岐インフラ】
            // =========================================================================
            NovelSystem.NovelDialogManager dialogManager = UnityEngine.Object.FindAnyObjectByType<NovelSystem.NovelDialogManager>();
            if (dialogManager != null)
            {
                // シーン上の全プレイヤーから 1P と 2P の名前を純粋にスキャン抽出
                string p1Name = "Player1";
                string p2Name = "Player2";
                int winnerPlayerId = 1;

                if (PlayerMove.AllPlayers != null && PlayerMove.AllPlayers.Count >= 2)
                {
                    PlayerStatusManager s1 = PlayerMove.AllPlayers[0].GetComponent<PlayerStatusManager>();
                    PlayerStatusManager s2 = PlayerMove.AllPlayers[1].GetComponent<PlayerStatusManager>();

                    if (s1 != null && s1.characterData != null) p1Name = s1.characterData.characterName;
                    if (s2 != null && s2.characterData != null) p2Name = s2.characterData.characterName;

                    if (playerMove.Opponent != null)
                    {
                        PlayerStatusManager oppStatus = playerMove.Opponent.GetComponent<PlayerStatusManager>();
                        if (oppStatus != null) winnerPlayerId = oppStatus.playerId;
                    }
                }

                // 引数を再定義して会話システムをキック！
                dialogManager.StartVictoryDialogue(p1Name, p2Name, winnerPlayerId);

                // 会話（3秒自動進行 or Zキー）が終わるまで、勝者テキストを出したままホールド待機
                while (NovelSystem.NovelDialogManager.isTalking)
                {
                    yield return null;
                }

                // =========================================================================
                // 🎯【演出改善②】：会話が終了した瞬間（Canvas非表示後）、勝者メッセージを非表示に
                // =========================================================================
                if (myStatusManager != null && myStatusManager.winText != null)
                {
                    myStatusManager.winText.gameObject.SetActive(false);
                }

                // 前のステップで実装した、会話終了後の心地いい余韻タイム（1秒間の静寂ウェイト）
                yield return new WaitForSecondsRealtime(1.0f);
            }
            else
            {
                // 会話マネージャーがシーンにいない場合のセーフティフォールバック
                yield return new WaitForSecondsRealtime(3.5f);
                if (myStatusManager != null && myStatusManager.winText != null)
                {
                    myStatusManager.winText.gameObject.SetActive(false);
                }
            }

            // 👑 4. すべての演出と1秒の余韻を完全に消化しきったら、満を持してポーズ画面（リザルトメニュー）へ！
            if (myStatusManager != null) myStatusManager.TriggerGameOver();
        }
        else
        {
            // 🔷 1本目のダウン時：
            // 🌟【スローモーションフリーズの根治】：
            // ダウンしたその場で「1.2秒間」だけリアルタイム基準で余韻を見せたら、
            // 文字のフェードアウトを待たずに、ここで即座にタイムスケールを等速（1.0f）に叩き戻します！
            yield return new WaitForSecondsRealtime(1.2f);
            Time.timeScale = 1.0f; // 🌟ここで素早くスローモーションを解除！

            if (myStatusManager != null && myStatusManager.koText != null)
            {
                // 等速の心地いいスピードの中で「Down !!」の文字がサラサラと消えていきます
                yield return myStatusManager.StartCoroutine(myStatusManager.FadeOutKOAnimation(0.4f));
            }

            yield return new WaitForSeconds(0.1f);

            if (myStatusManager != null && myStatusManager.winText != null)
            {
                myStatusManager.winText.gameObject.SetActive(false);
            }

            // 自動リセットコルーチン（この内部はもう等速で快適に動きます）へ進行
            yield return StartCoroutine(RoundResetSequence());
        }
    }

    public void SetPlayerActiveState(bool active)
    {
        Renderer[] renderers = transform.parent.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers) r.enabled = active;

        Collider2D[] colliders = transform.parent.GetComponentsInChildren<Collider2D>();
        foreach (var c in colliders) c.enabled = active;

        if (playerMove != null) playerMove.enabled = active;
    }

    // =========================================================================
    // ⭕ 修正後：ラウンド決着時、画面上の弾だけでなく自機のEX状態・チカチカ点滅も完全初期化
    // =========================================================================
    private void ClearAllBullets(bool force)
    {
        DanmakuBullet[] playerBullets = Object.FindObjectsByType<DanmakuBullet>(FindObjectsSortMode.None);
        foreach (var b in playerBullets)
        {
            b.Deactivate(true, force: force);
        }

        EnemyBullet[] enemyBullets = Object.FindObjectsByType<EnemyBullet>(FindObjectsSortMode.None);
        foreach (var b in enemyBullets)
        {
            b.Deactivate(true);
        }

        // =========================================================================
        // 🛡️【最核心修正】：次ラウンドのスキル封印 ＆ 永久チカチカ点滅バグの根治パージ
        // =========================================================================
        if (force)
        {
            // 1. 自機にアタッチされているすべての Emitter のEXスキル稼働フラグを強制的に false へリセット
            PlayerDanmakuEmitter[] allEmitters = transform.root.GetComponentsInChildren<PlayerDanmakuEmitter>(true);
            foreach (var emitter in allEmitters)
            {
                if (emitter != null)
                {
                    System.Reflection.FieldInfo exActiveField = typeof(PlayerDanmakuEmitter).GetField("_isEXSkillActive", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (exActiveField != null)
                    {
                        exActiveField.SetValue(emitter, false);
                    }
                }
            }

            // 2. 自機のアニメーションコンポーネントの無敵チカチカフラグを強制的に OFF にリセット
            PlayerAnimation anim = transform.root.GetComponentInChildren<PlayerAnimation>(true);
            if (anim != null)
            {
                anim.isInvincible = false;
            }

            Debug.Log("<color=lime>✨ [ROUND CLEANUP SUCCESS] 次ラウンドへの移行を検知。すべてのEX硬直ロックとチカチカ無敵状態を完全リセットしました！</color>");
        }
    }

    public void StartRebirthFromContinue()
    {
        StartCoroutine(RebirthRoutine());
    }

    private IEnumerator RebirthRoutine()
    {
        currentState = PlayerState.Rebirth;
        PlayerMove.CanShoot = false;

        float spawnX = (myStatusManager != null && myStatusManager.playerId == 2) ? 8.0f : -8.0f;
        float targetX = (myStatusManager != null && myStatusManager.playerId == 2) ? 3.5f : -3.5f;

        transform.parent.position = new Vector3(spawnX, 0, 0);

        SetPlayerActiveState(true);

        float elapsed = 0;
        Vector3 startPos = transform.parent.position;
        Vector3 targetPos = new Vector3(targetX, 0, 0);

        while (elapsed < 0.6f)
        {
            transform.parent.position = Vector3.Lerp(startPos, targetPos, elapsed / 0.6f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        currentState = PlayerState.Normal;
        if (playerMove != null) playerMove.SetInvincible(invincibilityTime);

        if (MatchTimerUI.Instance != null)
        {
            MatchTimerUI.Instance.StopTimer();
        }

        if (GameStartCountdown.Instance != null)
        {
            GameStartCountdown.Instance.StartCountdown();
        }
        else
        {
            PlayerMove.CanShoot = true;
            if (MatchTimerUI.Instance != null) MatchTimerUI.Instance.ResumeTimer();
        }
    }
    private void ShowWinMessage()
    {
        PlayerMove winner = playerMove.Opponent;

        if (winner == null)
        {
            foreach (var p in PlayerMove.AllPlayers)
            {
                if (p != null && p != playerMove)
                {
                    winner = p;
                    break;
                }
            }
        }

        if (winner != null && myStatusManager.winText != null)
        {
            PlayerStatusManager winnerStatus = winner.GetComponent<PlayerStatusManager>();

            string winnerName = (winnerStatus != null && winnerStatus.characterData != null)
                ? winnerStatus.characterData.characterName
                : "Player";

            myStatusManager.winText.text = winnerName + " Wins!";
            myStatusManager.winText.gameObject.SetActive(true);

            if (winnerStatus != null && winnerStatus.characterData != null)
            {
                myStatusManager.winText.color = winnerStatus.characterData.imageColor;
            }
        }
        else
        {
            Debug.LogWarning("Winner could not be identified.");
        }
    }
}