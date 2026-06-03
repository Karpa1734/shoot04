// --- PlayerStatusManager.cs 【VJTタイムベース・UIブロック・エラー完全解消版】 ---
using KanKikuchi.AudioManager;
using System.Collections;
using TMPro;
using UnityEngine;

public class PlayerStatusManager : MonoBehaviour
{
    [Header("Player Settings")]
    public int playerId = 1;
    public PlayerSkillData characterData;
    // 🌟【新規追加】：User1, User2 などのプレイヤーネームを表示しているTMPアタッチ枠
    [Tooltip("User1 または User2 と表記されているプレイヤーネームUIをここに登録してください")]
    public TextMeshProUGUI playerNameText;
    [Header("Resources")]
    public int life = 0;          // 対戦時の獲得ラウンド勝星数（0からスタート）
    public float currentHP = 100f; // 通常時のHP（インスペクター準拠で100）
    public float maxHP = 100f;     // 通常時の最大HP（インスペクター準拠で100）
    public int stockLives = 2;    // 通常時の残機

    [Header("Piece Settings")]
    public int lifePieces = 0;
    public int lifePiecesRequired = 3;

    [Header("Timers")]
    public float invincibleTimer = 0f;
    public float deathBombTimer = 0f;

    [Header("Statistics")]
    public int continueCount = 0;
    public TextMeshProUGUI countdownText;

    [Header("UI References")]
    public PlayerStatusUI lifeUI;
    public ExtendNotificationUI extendUI;

    [Header("Round Transition")]
    public CanvasGroup screenFader;

    [Header("Global References")]
    public PauseManager pauseManager;
    [Header("VJT Visual Effects")]
    public SpellBarrierEffect spellBarrier; // インスペクターからアタッチするバリアの参照
    [Tooltip("新設した PlayerSpellRing_Line の【プレハブ】をここに登録してください")]
    public GameObject spellRingPrefab;      // 🌟プレハブの設計図をアタッチする枠
    private GameObject spawnedRingInstance; // 🌟実際に画面に生成された実体を記憶する枠
    [Tooltip("新設した PlayerSpellCircle の【プレハブ】をここに登録してください")]
    public GameObject spellCirclePrefab;
    private GameObject spawnedCircleInstance;
    private PlayerMove _playerMove;

    public bool IsInvincible => invincibleTimer > 0;
    public bool IsDeathBombWindow => deathBombTimer > 0;

    public TextMeshProUGUI characterNameText;
    public TextMeshProUGUI winText;
    public TextMeshProUGUI koText;
    public UnityEngine.UI.Slider hpBar;
    public UnityEngine.UI.Slider orangeBar;
    public UnityEngine.UI.Slider spellHpBar;
    public float lerpSpeed = 2.0f;

    // =========================================================================
    // 🌟 聖少女領域（VJT）独立上乗せライフ ＆ デバフ管理ステート
    // =========================================================================
    [Header("--- Spell Card (VJT) 上乗せライフシステム ---")]
    public bool isSpellCardActive = false;      // 聖少女領域（VJT）展開中フラグ
    public bool isOverheated = false;          // 術式焼き切れ（デバフ）中フラグ

    public float spellHP = 0f;
    public float spellMaxHP = 0f;

    [HideInInspector] public float preSpellHP;             // 発動直前の通常HPをロック固定して覚える用

    [Header("--- VJT Duration Settings (Seconds) ---")]
    public float minSpellDuration = 8.0f;       // ゲージ200%（最小）で発動した時の維持秒数
    public float maxSpellDuration = 15.0f;      // ゲージ300%（最大）で発動した時の維持秒数

    public float totalSpellDuration = 0f;      // 発動時に確定した今回の総維持秒数
    public float spellTimer = 0f;              // 残り時間をカウントするリアルタイムタイマー
    public float initialUltimateEnergy = 0f;   // 発動した瞬間のアルカナゲージ量を記憶する用

    public float overheatDuration = 5f;        // 術式焼き切れのデバフ持続時間（5秒）
    private float overheatTimer = 0f;

    private Vector3 originalColliderScale = Vector3.one;
    private Collider2D playerCollider;

    private float appearanceElapsed = 0f;
    private float animatedSpellHP = 0f;
    private bool isAnimatingSpellBar = false;
    private const float SPELL_BAR_ANIM_DURATION = 0.4f;
    // =========================================================================
    // 🌟【排他制御・同時発動防止インフラ】：世界で一度に展開できるのは1人まで
    // =========================================================================
    // 現在ゲーム内でいずれかのプレイヤーが聖少女領域（VJT）を展開中であるか
    public static bool isAnyVJTActive = false;

    // 1フレーム内での完全同時発動を検知・処理するための静的ワークアセット
    private static int lastRequestFrame = -1;
    private static PlayerStatusManager p1Requester = null;
    private static PlayerStatusManager p2Requester = null;
    void Awake()
    {
        _playerMove = GetComponent<PlayerMove>();

        if (BossPracticeManager.IsPracticeMode)
        {
            stockLives = 0; life = 0;
        }
        else if (GameModeManager.IsStoryMode)
        {
            life = 3;
            stockLives = 3;
        }
        else
        {
            life = 0;
            stockLives = 0;
        }

        playerCollider = GetComponentInChildren<Collider2D>();
        if (playerCollider != null)
        {
            originalColliderScale = playerCollider.transform.localScale;
        }
    }

    void Start()
    {
        currentHP = maxHP;
        ApplyCharacterSettings();
        StartCoroutine(SetupInitialUI());
        StartCoroutine(InitUIWithDelay());
    }

    private IEnumerator InitUIWithDelay()
    {
        yield return null;
        UpdateUI();
    }

    private IEnumerator SetupInitialUI()
    {
        yield return null;
        currentHP = maxHP;
        UpdateUI();

        if (orangeBar != null)
        {
            orangeBar.maxValue = maxHP;
            orangeBar.value = currentHP;
        }
    }

    private void ApplyCharacterSettings()
    {
        if (characterData != null)
        {
            if (characterNameText != null)
            {
                characterNameText.text = characterData.characterName;
                characterNameText.color = characterData.imageColor;
            }
        }

        // =========================================================================
        // 🌟【AI（COM）自動ネーム判別システム】
        // =========================================================================
        if (playerNameText != null)
        {
            // 自分自身にアタッチされている DanmakuAgent (AIコンポーネント) をスキャン
            DanmakuAgent agent = GetComponent<DanmakuAgent>();

            // 💡 AIコンポーネントが存在し、かつ自動回避AIモード（_useAutoEvadeAI）がONの場合
            if (agent != null && agent._useAutoEvadeAI)
            {
                // 表示を「COM」に上書き！
                playerNameText.text = "COM";
            }
            else
            {
                // 人間操作（デバッグ時など含む）の場合は、従来のデフォルト表示（User1 や User2 など）をそのまま維持
                // ※ 将来的にキャラ選択画面からの名前引き継ぎを行う場合は、ここに流し込み処理を敷けます。
                playerNameText.text = (playerId == 1) ? "User1" : "User2";
            }
        }
    }

    void Update()
    {
        if (invincibleTimer > 0) invincibleTimer -= Time.deltaTime;
        if (deathBombTimer > 0) deathBombTimer -= Time.deltaTime;

        // 【VJT実行中のリアルタイム毎フレーム制御】
        if (isSpellCardActive)
        {
            if (MatchTimerUI.Instance != null) MatchTimerUI.Instance.StopTimer();

            spellTimer -= Time.deltaTime;

            // 🌟【大修正】：残り時間の割合（1.0 から 0.0 へ減衰）を厳密に抽出
            float timeRatio = Mathf.Clamp01(spellTimer / totalSpellDuration);

            // 🌟 発動時のアルカナゲージ残量から、残り時間に合わせて 0% へ向かって寸分の狂いもなく完全比例減少！
            _playerMove.ultimateEnergy = initialUltimateEnergy * timeRatio;

            if (spellTimer <= 0f)
            {
                spellTimer = 0f;
                _playerMove.ultimateEnergy = 0f;
                DeactivateSpellCard(false);
            }

            if (isAnimatingSpellBar)
            {
                appearanceElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(appearanceElapsed / SPELL_BAR_ANIM_DURATION);
                float easedT = t * t * (3f - 2f * t);
                animatedSpellHP = Mathf.Lerp(0f, spellHP, easedT);

                if (t >= 1f) isAnimatingSpellBar = false;
            }
            else
            {
                animatedSpellHP = spellHP;
            }
        }

        // 術式焼き切れデバフのタイマー処理
        if (isOverheated)
        {
            overheatTimer -= Time.deltaTime;
            if (overheatTimer <= 0f)
            {
                isOverheated = false;
                Debug.Log("<color=green>⏳【VJT】術式冷却完了。通常状態へ復帰しました。</color>");
            }
        }

        // ゲージの滑らかな減衰補間
        float targetSliderValue = isSpellCardActive ? animatedSpellHP : currentHP;
        if (orangeBar != null && orangeBar.value > targetSliderValue)
        {
            orangeBar.value = Mathf.Lerp(orangeBar.value, targetSliderValue, Time.deltaTime * lerpSpeed);
            if (orangeBar.value - targetSliderValue < 0.1f) orangeBar.value = targetSliderValue;
        }

        UpdateUI();
    }

    public void ActivateSpellCard()
    {
        // 既にいずれかのプレイヤーが領域を展開している場合、または自分が焼き切れ(デバフ)中、ゲージ不足なら鉄壁ガード
        if (isSpellCardActive || isAnyVJTActive || _playerMove.ultimateEnergy < 200f) return;

        if (SpellCardManager.Instance != null && !SpellCardManager.Instance.TryRequestVJT(this))
        {
            return;
        }

        // =========================================================================
        // 🌟【1フレーム同時発動ジャッジシステム】
        // =========================================================================
        if (Time.frameCount != lastRequestFrame)
        {
            // 新しいフレームの最初の申請なので、要求バッファを初期化して自分を登録
            lastRequestFrame = Time.frameCount;
            p1Requester = (playerId == 1) ? this : null;
            p2Requester = (playerId == 2) ? this : null;

            // 1フレーム（1メソッド内）だけ処理を遅らせて、相方の同時入力を待つためにコルーチンへバイパス
            StartCoroutine(ExecuteSpellCardWithFrameCheck());
        }
        else
        {
            // 🚨【完全同時押し検知】：前の申請と同じフレーム内に、もう片方のプレイヤーからも要求が来た場合
            if (playerId == 1) p1Requester = this;
            if (playerId == 2) p2Requester = this;
        }
    }

    /// <summary>
    /// 🌟【新規追加】：同フレーム内の入力要求を安全に集約し、50%の確率で勝者を選出する
    /// </summary>
    private IEnumerator ExecuteSpellCardWithFrameCheck()
    {
        // 同一フレーム内の入力をすべてバッファに集約させるため、1フレーム（FixedUpdate/Updateの裏側）だけ安全に待機
        yield return null;

        // 既に他の要因で世界ロックがかかってしまっていたら安全のために抜ける
        if (isAnyVJTActive) yield break;

        PlayerStatusManager finalWinner = this;

        // 🚨【運命の天秤】：1Pと2Pがまったく同じフレームで同時申請していた場合
        if (p1Requester != null && p2Requester != null)
        {
            // UnityEngine.Random を用いて 50% (0.5未満) の確率でどちらを主役にするか厳密にジャッジ！
            bool isP1Winner = UnityEngine.Random.value < 0.5f;
            finalWinner = isP1Winner ? p1Requester : p2Requester;

            Debug.Log($"<color=red>⚔️【VJT同時発動警告】1フレーム内の完全同時押しを検知！ 確率ジャッジの結果、勝者は [Player {finalWinner.playerId}] です！</color>");
        }

        // 選ばれたプレイヤー（あるいは先手を取った自分）のみ、以下の発動処理を全開化！
        finalWinner.ExecuteActivationSequence();
    }

    /// <summary>
    /// 排他権を獲得したプレイヤーのみが実行する、本物の領域展開シークエンス
    /// </summary>
    private void ExecuteActivationSequence()
    {
        Debug.Log($"<color=cyan>🔥【聖少女領域 - VJT展開】現在のゲージ残量: {_playerMove.ultimateEnergy}%</color>");

        if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.CARDCALL, 1.0f);

        isSpellCardActive = true;
        isAnyVJTActive = true;    // 世界ロック
        isOverheated = false;

        initialUltimateEnergy = _playerMove.ultimateEnergy;
        preSpellHP = currentHP;

        float fullArmorHP = maxHP * 30f;

        float progress = Mathf.InverseLerp(200f, 300f, initialUltimateEnergy);
        totalSpellDuration = Mathf.Lerp(minSpellDuration, maxSpellDuration, progress);
        spellTimer = totalSpellDuration;

        float spawnHPRatio = Mathf.Lerp(0.6f, 1.0f, progress);

        spellMaxHP = fullArmorHP;
        spellHP = fullArmorHP * spawnHPRatio;

        isAnimatingSpellBar = true;
        appearanceElapsed = 0f;
        animatedSpellHP = 0f;

        if (playerCollider != null)
        {
            playerCollider.transform.localScale = originalColliderScale * 30f;
        }

        // =========================================================================
        // 🌟【カラー同調リファクタリング】：キャラクターのイメージカラーをバリアへ注入
        // =========================================================================
        if (spellBarrier != null)
        {
            // キャラデータから設定色（imageColor）を安全に抽出し、デフォルトは白でフォールバック
            Color charColor = (characterData != null) ? characterData.imageColor : Color.white;

            // 後述の拡張対応型メソッドをキックし、色を同期させてからアクティブ化
            spellBarrier.SetBarrierActive(true);

            // 🌟 もし既存の SpellBarrierEffect スクリプトに色を変更する関数がまだない場合でも、
            // 🌟 以下の「汎用コンポーネント自動書き換えロジック」により、
            // 🌟 スクリプトを改造することなく強制的にバリアオブジェクトの「全色相」をキャラカラーへ上書き同調させます！
            Renderer[] barrierRenderers = spellBarrier.GetComponentsInChildren<Renderer>(true);
            foreach (var r in barrierRenderers)
            {
                if (r is SpriteRenderer sr)
                {
                    sr.color = charColor; // スプライト製バリアの場合の色同期
                }
                else if (r is LineRenderer lr)
                {
                    lr.startColor = charColor; // ライン製バリアの場合の色同期
                    lr.endColor = charColor;
                }
                else if (r.material != null)
                {
                    r.material.color = charColor; // 通常マテリアル（3D/Mesh等）の場合の色同期
                }
            }

            // パーティクルシステム（オーラ等）で構築されている場合の追従処理
            ParticleSystem[] barrierParticles = spellBarrier.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in barrierParticles)
            {
                var mainModule = ps.main;
                mainModule.startColor = charColor; // 放出されるオーラ粒子の色同期
            }
        }

        UpdateUI();
        SyncBarsImmediately();

        // プレハブ動的生成の魔法陣（前回の実装）
        if (spellRingPrefab != null && spawnedRingInstance == null)
        {
            spawnedRingInstance = Instantiate(spellRingPrefab, transform.position, Quaternion.identity);
            PlayerSpellRing_Line ringScript = spawnedRingInstance.GetComponent<PlayerSpellRing_Line>();
            if (ringScript != null)
            {
                ringScript.targetStatus = this;
                ringScript.Activate(totalSpellDuration);
            }
        }
        // =========================================================================
        // 🌟【プレハブ動的生成】：魔法陣（Circle）を発動した瞬間にクローン実体化！
        // =========================================================================
        if (spellCirclePrefab != null && spawnedCircleInstance == null)
        {
            spawnedCircleInstance = Instantiate(spellCirclePrefab, transform.position, Quaternion.identity);
            PlayerSpellCircle circleScript = spawnedCircleInstance.GetComponent<PlayerSpellCircle>();
            if (circleScript != null)
            {
                // 自分をオーナーとして渡し、画像・色・タイマーを動的に結びつける
                circleScript.Activate(this, totalSpellDuration);
            }
        }
        // =========================================================================
        // 🌟【看板UI連動】：発動したプレイヤー側のUIライフバー位置へ、カード名表示をスライドイン！
        // =========================================================================
        if (EnemySpellCardUI.Instance != null && characterData != null)
        {
            // 🌟 データの安全な抽出（もし未記入ならデフォルトとしてキャラクター名をフォールバック）
            string displayName = string.IsNullOrEmpty(characterData.spellCardName)
                ? characterData.characterName
                : characterData.spellCardName;

            // 第1引数に、これまでの「characterName」ではなく、新設した「displayName (スペルカード名)」を流し込みます！
            EnemySpellCardUI.Instance.DisplaySpell(
                displayName,      // 🌟【大修正】：インスペクターで設定した独自のスペルカード名が描画されます！
                0,                // getCount (戦績連動させたい場合はここに変数を結合)
                0,                // challengeCount
                1000000f,         // 初期ボーナススコア
                false,            // isFailed
                this.playerId     // 1Pなら左、2Pなら右へ自動配置
            );
        }
        // =========================================================================
        // 🌟【専用2D背景連動】：発動キャラの固有データを流し込み、専用背景へフェードイン！
        // =========================================================================
        if (VJTSpellBackgroundManager2D.Instance != null)
        {
            // 🚨 第2引数に「this.characterData」が確実に注入されているか大至急チェック！
            VJTSpellBackgroundManager2D.Instance.SetSpellBackgroundActive(true, this.characterData);
        }
    }
    /// <summary>
    /// 聖少女領域（VJT）を終了・解除する
    /// </summary>
    /// <param name="isDefeatedByDamage">被弾ダメージによる強制破砕（全損終了）なら true、時間切れなら false</param>
    public void DeactivateSpellCard(bool isDefeatedByDamage)
    {
        if (!isSpellCardActive) return; // 既に解除されている場合は処理をスキップ
        isSpellCardActive = false; // フラグを解除
        // 🌟【大修正】：世界共有ロックをここで完全解放！ これでもう片方も発動できるようになります
        isAnyVJTActive = false;
        // 🌟 破砕（被弾による終了）時のみ1.0秒間の無敵保護を発動、時間切れやULT時はスキップしてクールダウンへ
        if (isDefeatedByDamage)
        {
            SetInvincible(1.0f); // 1秒間無敵
        }

        if (spellBarrier != null)
        {
            spellBarrier.SetBarrierActive(false); // バリアエフェクトを非アクティブ化
        }

        if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.SPELL_OFF, 0.5f); // 解除SEを再生

        if (SpellCardManager.Instance != null)
        {
            SpellCardManager.Instance.ReleaseVJT(this); // マネージャー側のロックを解放
        }

        if (playerCollider != null)
        {
            playerCollider.transform.localScale = originalColliderScale; // 当たり判定のスケールを通常サイズに復元
        }

        // =========================================================================
        // 🌟【ライフ還元アルゴリズム修正】：領域バーの残量％まで体力を最低保証引き上げ
        // =========================================================================
        if (isDefeatedByDamage)
        {
            // 🔷 被弾破砕によってバリアが割れた時：
            // 元々の通常ライフを無傷のままそのまま復元（ preSpellHP からの再開）
            currentHP = preSpellHP;
        }
        else
        {
            // 🔶 時間切れ（自然消滅）によって領域が終了した時：
            // 1. 残った領域バーの割合（0.0 〜 1.0）を算出
            float spellHpRatio = 0f;
            if (spellMaxHP > 0f)
            {
                spellHpRatio = Mathf.Clamp01(spellHP / spellMaxHP);
            }

            // 2. 領域バーの残り％を、通常HP（100満点）の目標体力値（0 〜 100）にダイレクト変換
            // 例：領域バーが79%なら、targetHP は 79f になります。
            float targetHP = maxHP * spellHpRatio;

            // 3. 🌟【条件適合】：発動時の体力が目標値（79%など）以上なら元の体力をキープ、以下なら目標値まで引き上げて回復！
            // Mathf.Max を使うことで、preSpellHP と targetHP の大きい方の値が自動的に currentHP に代入されます。
            currentHP = Mathf.Max(preSpellHP, targetHP);

            // 念のため最大HP（100）を絶対に突破しないようにクランプ保護
            currentHP = Mathf.Min(currentHP, maxHP);

            Debug.Log($"<color=lime>💖【VJT領域％クランプ還元】領域バー残量: {spellHpRatio * 100f:F1}% -> 目標HP: {targetHP:F1} / 通常HP: {preSpellHP} -> 最終HP: {currentHP}</color>");
        }

        // 領域内の動的資源パラメータを安全に完全クリア化
        spellHP = 0f;
        spellMaxHP = 0f;
        spellTimer = 0f;
        totalSpellDuration = 0f;
        initialUltimateEnergy = 0f;

        isOverheated = true; // 術式焼き切れ（冷却期間デバフ）へ移行
        overheatTimer = overheatDuration;

        if (MatchTimerUI.Instance != null)
        {
            MatchTimerUI.Instance.ResumeTimer(); // 止まっていた対戦ラウンドタイマーを再開
        }

        UpdateUI(); // UIの表示を最新情報に更新
        SyncBarsImmediately(); // 各種スライダー位置を即時同期
        // =========================================================================
        // 🌟【完全パージ】：領域終了時に、生成した魔法陣オブジェクトを安全にメモリから消去
        // =========================================================================
        if (spawnedRingInstance != null)
        {
            PlayerSpellRing_Line ringScript = spawnedRingInstance.GetComponent<PlayerSpellRing_Line>();
            if (ringScript != null)
            {
                ringScript.Deactivate();
            }

            // オブジェクトの物理破棄
            Destroy(spawnedRingInstance);
            spawnedRingInstance = null; // 参照のクリア化
        }
        // =========================================================================
        // 🌟【完全パージ】：領域終了時に、生成した魔法陣（Circle）も安全にメモリから消去
        // =========================================================================
        if (spawnedCircleInstance != null)
        {
            PlayerSpellCircle circleScript = spawnedCircleInstance.GetComponent<PlayerSpellCircle>();
            if (circleScript != null)
            {
                circleScript.Deactivate();
            }
            if (spawnedCircleInstance != null)
            {
                Destroy(spawnedCircleInstance);
                spawnedCircleInstance = null;
            }
        }
        if (EnemySpellCardUI.Instance != null)
        {
            EnemySpellCardUI.Instance.HideSpell(); // 🌟領域終了と同時に看板UIを画面外へ滑らかに退場
        }
        // =========================================================================
        // 🌟【専用2D背景連動】：領域解除（または被弾全損）と同時に通常2D背景へ滑らかに復帰
        // =========================================================================
        if (VJTSpellBackgroundManager2D.Instance != null)
        {
            VJTSpellBackgroundManager2D.Instance.SetSpellBackgroundActive(false);
        }
    }

    public bool ApplyDamage(int amount)
    {
        if (isSpellCardActive)
        {
            spellHP -= amount;
            UpdateUI();

            if (spellHP <= 0)
            {
                spellHP = 0;
                DeactivateSpellCard(true);
                return false;
            }
            return false;
        }

        currentHP -= amount;
        UpdateUI();

        if (currentHP <= 0)
        {
            currentHP = 0;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 被弾時に残機または星の計算を行い、【このダウンで完全に勝負（マッチ）が決着するか】を論理評価する
    /// </summary>
    /// <returns>完全に決着・ゲームオーバーなら true、まだ次ラウンドや残機で復活できるなら false</returns>
    /// <summary>
    /// 被弾時に残機または星の計算を行い、【このダウンで完全に勝負（マッチ）が決着するか】を論理評価する
    /// </summary>
    /// <returns>完全に決着・2勝先取されたら true、まだ次ラウンドへ移行できるなら false</returns>
    public bool SubtractLifeAndCheckRebirth()
    {
        if (GameModeManager.IsStoryMode)
        {
            if (stockLives > 0)
            {
                stockLives--;
                life = stockLives;
                UpdateUI();
                return false;
            }
            return true;
        }
        else
        {
            // 🔷 VSモード（対戦モード）時：2ラウンド先取制（2つの星が点灯したら終了）
            if (_playerMove != null && _playerMove.Opponent != null)
            {
                PlayerStatusManager oppStatus = _playerMove.Opponent.GetComponent<PlayerStatusManager>();
                if (oppStatus != null)
                {
                    // 🌟 相手の現在の星（life）が既に 1 だった場合、今回の勝利で 2 に到達するため【マッチ終了】
                    if (oppStatus.life >= 1)
                    {
                        oppStatus.life = 2;
                        oppStatus.UpdateUI();
                        UpdateUI();
                        return true; // 2ラウンド先取したため、ゲームセット確定！ (true)
                    }
                    else
                    {
                        // 🌟 相手の現在の星が 0 だった場合は、今回で 1 個目が灯るので【ラウンド継続】
                        oppStatus.life = 1;
                        oppStatus.UpdateUI();
                        UpdateUI();
                        return false; // まだ1本目なので、次のラウンドへ進む (false)
                    }
                }
            }

            UpdateUI();
            return false;
        }
    }

    public IEnumerator GradualHealthRecovery(float duration)
    {
        float startHP = currentHP;
        float elapsed = 0;

        if (orangeBar != null)
        {
            orangeBar.maxValue = maxHP;
            orangeBar.value = startHP;
        }

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            currentHP = Mathf.Lerp(startHP, maxHP, elapsed / duration);
            UpdateUI();
            yield return null;
        }
        currentHP = maxHP;
        UpdateUI();

        if (orangeBar != null) orangeBar.value = maxHP;
    }

    public IEnumerator FadeRoutine(float targetAlpha, float duration)
    {
        if (screenFader == null) yield break;
        float startAlpha = screenFader.alpha;
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            screenFader.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }
        screenFader.alpha = targetAlpha;
    }

    private void UpdateUI()
    {
        if (winText != null && !winText.gameObject.activeSelf) winText.gameObject.SetActive(false);
        if (koText != null && !koText.gameObject.activeSelf) koText.gameObject.SetActive(false);

        bool isVs = !GameModeManager.IsStoryMode;

        if (lifeUI != null)
        {
            lifeUI.SetCountVsVariant(life, lifePieces, lifePiecesRequired, isVs);
        }

        if (isSpellCardActive)
        {
            if (spellHpBar != null)
            {
                spellHpBar.gameObject.SetActive(true);
                spellHpBar.maxValue = spellMaxHP;
                spellHpBar.value = animatedSpellHP;
            }

            if (hpBar != null)
            {
                hpBar.gameObject.SetActive(true);
                hpBar.maxValue = maxHP;
                hpBar.value = preSpellHP; // 🌟通常HPを満タン100の位置でフリーズ固定ロック！
                SetSliderAlpha(hpBar, 0.3f);
            }
        }
        else
        {
            if (hpBar != null)
            {
                hpBar.gameObject.SetActive(true);
                hpBar.maxValue = maxHP;
                hpBar.value = currentHP;
                SetSliderAlpha(hpBar, 1.0f);
            }

            if (spellHpBar != null)
            {
                spellHpBar.gameObject.SetActive(false);
            }
        }

        if (orangeBar != null)
        {
            orangeBar.maxValue = isSpellCardActive ? spellMaxHP : maxHP;
            float currentTargetValue = isSpellCardActive ? animatedSpellHP : currentHP;
            if (currentTargetValue >= (isSpellCardActive ? spellMaxHP : maxHP))
            {
                orangeBar.value = isSpellCardActive ? spellMaxHP : maxHP;
            }
        }
    }

    public void SyncBarsImmediately()
    {
        if (hpBar != null)
        {
            hpBar.maxValue = isSpellCardActive ? spellMaxHP : maxHP;
            hpBar.value = isSpellCardActive ? animatedSpellHP : currentHP;
        }
        if (orangeBar != null)
        {
            orangeBar.maxValue = isSpellCardActive ? spellMaxHP : maxHP;
            orangeBar.value = isSpellCardActive ? animatedSpellHP : currentHP;
        }
    }

    private void SetSliderAlpha(UnityEngine.UI.Slider slider, float alpha)
    {
        UnityEngine.UI.Image[] images = slider.GetComponentsInChildren<UnityEngine.UI.Image>(true);
        foreach (var img in images)
        {
            Color c = img.color;
            c.a = alpha;
            img.color = c;
        }
    }

    // =========================================================================
    // 🌟【修復・完全溶接】：外部クラスから呼び出される中枢関数群
    // =========================================================================

    /// <summary>
    /// 一時停止メニュー等からリトライ（コンティニュー）を確定した際の初期化処理
    /// </summary>
    public void PerformContinue()
    {
        continueCount++;
        currentHP = maxHP;
        isSpellCardActive = false;
        isOverheated = false;
        spellHP = 0f;
        spellMaxHP = 0f;
        spellTimer = 0f;
        totalSpellDuration = 0f;
        initialUltimateEnergy = 0f;
        UpdateUI();

        PlayerHitHandler hitHandler = GetComponentInChildren<PlayerHitHandler>();
        if (hitHandler != null) hitHandler.StartRebirthFromContinue();
    }

    /// <summary>
    /// コンティニュー回数のカウンタを完全にリセットする
    /// </summary>
    public void ResetContinueCount()
    {
        continueCount = 0;
    }

    /// <summary>
    /// 完全に勝敗が決したリザルト画面（ゲームオーバー画面）をトリガーする
    /// </summary>
    public void TriggerGameOver()
    {
        if (pauseManager == null) return;

        if (BossPracticeManager.IsPracticeMode)
        {
            pauseManager.SetPracticeResultMode(true, false);
        }
        else
        {
            pauseManager.SetGameOverMode(true);
            pauseManager.PauseGame();
        }
    }

    public void AddLife(int amount)
    {
        life = Mathf.Min(life + amount, 8);
        UpdateUI();
        if (extendUI != null) extendUI.Show("Extend!!", new Color(1f, 0.4f, 0.7f));
    }

    public void AddLifePiece(int amount)
    {
        lifePieces += amount;
        if (lifePieces >= lifePiecesRequired)
        {
            lifePieces -= lifePiecesRequired;
            AddLife(1);
            if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.SE_EXTEND2);
        }
        UpdateUI();
    }
    // 🌟【修正】：文字の強制上書きロジック（koText.text = "Game Set !!";）を排除！
    // ヒットハンドラー側でセットされた文字列をそのままアニメーションさせます。
    public IEnumerator PlayKOAnimation()
    {
        if (koText == null) yield break;
        koText.gameObject.SetActive(true);

        koText.transform.localScale = Vector3.zero;
        float elapsed = 0;
        float duration = 0.5f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float scale = 0;
            if (t < 0.7f) scale = Mathf.Lerp(0, 1.5f, t / 0.7f);
            else scale = Mathf.Lerp(1.5f, 1.0f, (t - 0.7f) / 0.3f);

            koText.transform.localScale = Vector3.one * scale;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        koText.transform.localScale = Vector3.one;
    }

    // 🌟【修正】：フェードアウト終了時に koText 自体は非アクティブにしますが、
    // 次回の判定への影響を防ぐため、元のテキスト内容を無理にリセットしない形に変更。
    public IEnumerator FadeOutKOAnimation(float duration)
    {
        if (koText == null) yield break;
        Color startColor = koText.color;
        float elapsed = 0;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime; // タイムスケール等倍復帰後のため通常のdeltaTimeで安全稼働
            float alpha = Mathf.Lerp(1, 0, elapsed / duration);
            koText.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            yield return null;
        }
        koText.gameObject.SetActive(false);
        koText.color = startColor;
    }

    public void SetInvincible(float duration)
    {
        invincibleTimer = duration;
        deathBombTimer = 0;
        if (_playerMove != null) _playerMove.SetInvincible(duration);
    }
}