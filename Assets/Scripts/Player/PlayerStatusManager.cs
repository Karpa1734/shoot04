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

    [Header("--- VJT Overheat Settings ---")]
    [Tooltip("このキャラクターがVJTを解除・破砕された後の【術式焼き切れ（冷却期間）】の持続時間（秒）")]
    public float characterOverheatDuration = 20f; // 🚨 デフォルトを20秒に設定
    // 🌟【新規追加】：SEの重複再生を防止し、条件達成の「瞬間」だけを捉えるためのステート記憶フラグ
    private bool _wasVJTReadyLastFrame = false;
    private bool _wasCounterReadyLastFrame = false;
    public bool IsInvincible => invincibleTimer > 0;
    public bool IsDeathBombWindow => deathBombTimer > 0;

    public TextMeshProUGUI characterNameText;
    public TextMeshProUGUI winText;
    public TextMeshProUGUI koText;
    public UnityEngine.UI.Slider hpBar;
    public UnityEngine.UI.Slider orangeBar;
    public UnityEngine.UI.Slider spellHpBar;
    public float lerpSpeed = 2.0f;
    // 🌟【新規追加】：スリップダメージの小数を毎フレーム蓄積しておくためのプール
    private float _hpDrainAccumulator = 0f;
    private float _actionTaxAccumulator = 0f; // 🪙 強欲の重税用プール
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


    private float appearanceElapsed = 0f;
    private float animatedSpellHP = 0f;
    private bool isAnimatingSpellBar = false;
    private const float SPELL_BAR_ANIM_DURATION = 0.4f;

    // =========================================================================
    // 🎯【インスペクターアタッチ拡張】：コライダーと2つのスプライトを直接登録
    // =========================================================================
    [Header("🎯 Hitbox & Sprite Assignments (Inspector)")]
    [Tooltip("子オブジェクトにある当たり判定用コライダーをここにドラッグ＆ドロップしてください")]
    public Collider2D playerCollider;

    [Tooltip("当たり判定を視覚化している1つ目のスプライト（例：コア画像など）")]
    public SpriteRenderer hitboxSprite1;

    [Tooltip("当たり判定を視覚化している2つ目のスプライト（例：外枠・オーラ画像など）")]
    public SpriteRenderer hitboxSprite2;

    // 内部キャッシュ用変数
    [HideInInspector] public Vector3 originalColliderScale;
    private float originalColliderRadius = 0.2f;

    // スプライトそれぞれの初期等倍サイズを正確に記憶するための変数
    private Vector3 originalSprite1Scale = Vector3.one;
    private Vector3 originalSprite2Scale = Vector3.one;

    // =========================================================================
    // 🌟【排他制御・同時発動防止インフラ】：世界で一度に展開できるのは1人まで
    // =========================================================================
    // 現在ゲーム内でいずれかのプレイヤーが聖少女領域（VJT）を展開中であるか
    public static bool isAnyVJTActive = false;

    // 1フレーム内での完全同時発動を検知・処理するための静的ワークアセット
    private static int lastRequestFrame = -1;
    private static PlayerStatusManager p1Requester = null;
    private static PlayerStatusManager p2Requester = null;

    [Header("👁️ Jealousy Field Settings (Internal)")]
    [Tooltip("JealousyFogEffectスクリプトと画像がセットされた【黒い霧のプレハブ】をここに登録してください")]
    public GameObject jealousyFogPrefab;
    private float _fogSpawnTimer = 0f; // 霧の発生間隔を数える内部タイマー

    // =========================================================================
    // 🧬【新規拡張】：パッシブスキル状態管理用モニタータイマー
    // =========================================================================
    private float _passiveAtkBoostTimer = 0f; // 被弾時攻撃力アップの残り維持タイマー
    public bool IsAttackBoostActive => _passiveAtkBoostTimer > 0f; // 外部（Emitter等）からカンニングされるバフ点灯フラグ
    // 🚨【バグ根治用追加】：他人のスプライトを誤認取得しないための、自分専用の純粋な自機レンダラーキャッシュ
    private SpriteRenderer _myOwnCharacterRenderer;
    // =========================================================================
    // 🔍【新設】：デバッグ数値リアルタイム可視化TMP枠
    // =========================================================================
    [Header("🔧 Debug UI Slots")]
    [Tooltip("HP/MP/アルカナの生数値を小数点第一位まで表示するデバッグ用Textアタッチ枠")]
    public TextMeshProUGUI debugStatusText;

    // 🎯【新設】：デバッグ中断時のランク永続化を防ぐためのキャッシュ
    private StatusRank _originalCharacterRank;
    private bool _hasCachedRank = false;
    // 構造体 CharacterRankBackup _originalBackup; の下あたりに追加

    // 🕒【新規追加】：AIによる領域返し不発SEの「マシンガン大連射」を防止するためのインターバルタイマー
    private float _failedSpellSoundTimer = 0f;
    // 🎯【デバッグ安全弁】：アセットの永続上書きバグを根絶するためのディープキャッシュ構造体
    private struct CharacterRankBackup
    {
        public StatusRank hp;
        public StatusRank mp;
        public StatusRank attack; // ⚔️【修復】：不足していた攻撃ランクのバックアップポケットを完全溶接！
        public StatusRank agility;
        public StatusRank mmpRegen;
        public StatusRank spellZone;
    }
    private CharacterRankBackup _originalBackup;


    void Awake()
    {
        _playerMove = GetComponent<PlayerMove>(); //

        if (characterData != null)
        {
            characterData = Instantiate(characterData);
        }

        if (BossPracticeManager.IsPracticeMode) //
        { //
            stockLives = 0; life = 0; //
        } //
        else if (GameModeManager.IsStoryMode) //
        { //
            life = 3; //
            stockLives = 3; //
        } //
        else //
        { //
            life = 0; //
            stockLives = 0; //
        } //

        if (playerCollider == null) //
        { //
            playerCollider = GetComponentInChildren<Collider2D>(); //
        } //

        if (playerCollider != null) //
        { //
            originalColliderScale = playerCollider.transform.localScale; //

            if (playerCollider is CircleCollider2D circle) //
            { //
                originalColliderRadius = circle.radius; //
            } //
        } //

        if (hitboxSprite1 != null) originalSprite1Scale = hitboxSprite1.transform.localScale; //
        if (hitboxSprite2 != null) originalSprite2Scale = hitboxSprite2.transform.localScale; //

        _myOwnCharacterRenderer = GetComponent<SpriteRenderer>(); //
        if (_myOwnCharacterRenderer == null) _myOwnCharacterRenderer = GetComponentInChildren<SpriteRenderer>(); //

        if (HasPassiveSkill(PassiveSkillType.LustSmall) && playerCollider != null) //
        { //
            CircleCollider2D startCircle = playerCollider as CircleCollider2D; //
            if (startCircle != null) //
            { //
                startCircle.radius = originalColliderRadius * 0.8f; //
                Debug.Log($"<color=lime>🛡️【パッシブ】SmallHitboxによりコライダー半径およびスプライト2種を常時0.8倍に縮小しました。</color>"); //
            } //
        } //

        // 🌟 核心：初期ランクデコードをメソッドへ一本化
        ApplyCharacterRanks();
    }

    /// <summary>
    /// 📊 6大パラメーター・ランクインフラの動的展開マトリクス
    /// </summary>
    private void ApplyCharacterRanks()
    {
        if (characterData == null) return;

        // 🟥 ① 体力 (最大HP) の反映
        switch (characterData.rankHP) //
        { //
            case StatusRank.E: maxHP = 70f; break; //
            case StatusRank.D: maxHP = 85f; break; //
            case StatusRank.C: maxHP = 100f; break; //
            case StatusRank.B: maxHP = 115f; break; //
            case StatusRank.A: maxHP = 130f; break; //
            case StatusRank.EX: maxHP = 145f; break; //
        } //

        // 🟦 ② 魔力 (最大マナの高さ rankMP)
        float convertedMaxEnergy = 100f; //
        switch (characterData.rankMP) //
        { //
            case StatusRank.E: convertedMaxEnergy = 70f; break; //
            case StatusRank.D: convertedMaxEnergy = 85f; break; //
            case StatusRank.C: convertedMaxEnergy = 100f; break; //
            case StatusRank.B: convertedMaxEnergy = 115f; break; //
            case StatusRank.A: convertedMaxEnergy = 130f; break; //
            case StatusRank.EX: convertedMaxEnergy = 145f; break; //
        } //
        if (_playerMove != null) _playerMove.maxEnergy = convertedMaxEnergy; //

        // 🟨 ③ 敏捷 (高速移動時の移動速度 rankAgility)
        if (_playerMove != null) //
        { //
            float calculatedAgilitySpeed = 5.0f; //
            switch (characterData.rankAgility) //
            { //
                case StatusRank.E: calculatedAgilitySpeed = 3.8f; break; //
                case StatusRank.D: calculatedAgilitySpeed = 4.4f; break; //
                case StatusRank.C: calculatedAgilitySpeed = 5.0f; break; //
                case StatusRank.B: calculatedAgilitySpeed = 5.6f; break; //
                case StatusRank.A: calculatedAgilitySpeed = 6.2f; break; //
                case StatusRank.EX: calculatedAgilitySpeed = 6.8f; break; //
            } //
            _playerMove.SetSpeedFromRank(calculatedAgilitySpeed); //
        } //

        // 🟩 ④ マナ再生 (マナゲージ再生の速さ rankMMPRegen)
        if (_playerMove != null) //
        { //
            switch (characterData.rankMMPRegen) //
            { //
                case StatusRank.E: _playerMove.energyRegenRate = 50f; break; //
                case StatusRank.D: _playerMove.energyRegenRate = 60f; break; //
                case StatusRank.C: _playerMove.energyRegenRate = 70f; break; //
                case StatusRank.B: _playerMove.energyRegenRate = 80f; break; //
                case StatusRank.A: _playerMove.energyRegenRate = 90f; break; //
                case StatusRank.EX: _playerMove.energyRegenRate = 100f; break; //
            } //
        } //

        // 🔮 ⑤ 領域維持時間 (rankSpellZone)
        switch (characterData.rankSpellZone) //
        { //
            case StatusRank.E: maxSpellDuration = 20f; characterOverheatDuration = maxSpellDuration * 0.8f; break; //
            case StatusRank.D: maxSpellDuration = 25f; characterOverheatDuration = maxSpellDuration * 0.8f; break; //
            case StatusRank.C: maxSpellDuration = 30f; characterOverheatDuration = maxSpellDuration * 0.8f; break; //
            case StatusRank.B: maxSpellDuration = 35f; characterOverheatDuration = maxSpellDuration * 0.8f; break; //
            case StatusRank.A: maxSpellDuration = 40f; characterOverheatDuration = maxSpellDuration * 0.8f; break; //
            case StatusRank.EX: maxSpellDuration = 45f; characterOverheatDuration = maxSpellDuration * 0.8f; break; //
        } //
        minSpellDuration = maxSpellDuration * 0.6f; //
    }

    // 📄 PlayerStatusManager.cs 内の Start メソッドおよび拡張インフラ層
    // 📄 PlayerStatusManager.cs 内の Start メソッド（6大パラメーター完全適合版）
    void Start()
    {
        // 🚨【新規追加】：お互いのAwakeデータ展開が完全に完了したこの瞬間に、
        // 🚨                 相手の弱点をサーチして自身のランクをハッキング・引き上げます！
        if (HasPassiveSkill(PassiveSkillType.PrideStatusSteal))
        {
            ExecutePrideStatusSteal();
        }

        // --- 既存のUI初期化インフラへ完全結合 ---
        currentHP = maxHP; //
        ApplyCharacterSettings(); //
        StartCoroutine(SetupInitialUI()); //
        StartCoroutine(InitUIWithDelay()); //
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
        // --- 🧬 パッシブ：攻撃力アップバフの制限時間カウントダウン ---
        if (_passiveAtkBoostTimer > 0f)
        {
            _passiveAtkBoostTimer -= Time.deltaTime;
        }
        // 🕒【新規加算】：連続不発SE防止タイマーを進める
        if (_failedSpellSoundTimer > 0f)
        {
            _failedSpellSoundTimer -= Time.deltaTime;
        }
        // =========================================================================
        // 🍰【新規実装】暴食：【生命への超還元】（通常時：毎秒アルカナ0.5%消費➔体力0.5%自動回復 / 領域中：消費なしで再生力10倍）
        // =========================================================================
        if (HasPassiveSkill(PassiveSkillType.GluttonyRegen) && currentHP > 0f)
        {
            // すでに通常HP（または領域バー）が満タンの場合はゲージを消費せず処理をスキップ
            float currentLimitHP = isSpellCardActive ? spellMaxHP : maxHP;
            float currentCheckHP = isSpellCardActive ? spellHP : currentHP;

            if (currentCheckHP < currentLimitHP)
            {
                // 💡 ベースとなる回復量：毎秒0.5%（Time.deltaTime を掛けて毎フレーム滑らかに加算）
                float baseRegenAmount = maxHP * 0.005f * Time.deltaTime;

                if (isSpellCardActive)
                {
                    // 🔶 聖少女領域（VJT）展開中：【コスト消費なし】で再生力が15倍（毎秒7.5%回復）に超ブースト！
                    float areaRegenAmount = baseRegenAmount * 30f;
                    spellHP = Mathf.Min(spellHP + areaRegenAmount, spellMaxHP);
                }
                else
                {
                    // 🔷 通常時：アルカナゲージ（ultimateEnergy）が消費量（毎秒0.5% = 毎秒0.5f）以上あるかチェック
                    //    ※ 1秒間に 0.5f 消費するため、毎フレームの必要量は 0.5f * Time.deltaTime
                    float requiredEnergy = 0.5f * Time.deltaTime;

                    if (_playerMove != null && _playerMove.ultimateEnergy >= requiredEnergy)
                    {
                        // 資源を消費して、本体のHPを安全に回復
                        _playerMove.ultimateEnergy -= requiredEnergy;
                        currentHP = Mathf.Min(currentHP + baseRegenAmount, maxHP);
                    }
                }
            }
        }


        PlayerDanmakuEmitter myEmitter = GetComponentInChildren<PlayerDanmakuEmitter>();
        if (myEmitter != null && myEmitter.IsSyaruBitEXActive)
        {
            invincibleTimer = 0.1f;
        }
        else if (invincibleTimer > 0)
        {
            invincibleTimer -= Time.deltaTime;
        }

        if (deathBombTimer > 0) deathBombTimer -= Time.deltaTime;

        // 【VJT実行中のリアルタイム毎フレーム制御】
        // 【VJT実行中のリアルタイム毎フレーム制御】
        if (isSpellCardActive)
        {
            if (MatchTimerUI.Instance != null) MatchTimerUI.Instance.StopTimer(); 

            // =========================================================================
            // 🌟【新規追加】：決着（KO）時はアルカナゲージおよび維持タイマーをその場で完全停止！
            // =========================================================================
            PlayerHitHandler hitHandler = GetComponentInChildren<PlayerHitHandler>();
            PlayerMove oppMove = _playerMove != null ? _playerMove.Opponent : null;
            PlayerHitHandler oppHitHandler = oppMove != null ? oppMove.GetComponentInChildren<PlayerHitHandler>() : null;

            // 自分、または対戦相手のどちらかが「通常状態（Normal）」でなくなった＝決着演出が始まったら、
            // このフレームのタイマーおよびゲージの減少計算を完全スキップ（フリーズ）させます。
            bool isRoundEnded = (hitHandler != null && hitHandler.currentState != PlayerHitHandler.PlayerState.Normal) ||
                                (oppHitHandler != null && oppHitHandler.currentState != PlayerHitHandler.PlayerState.Normal);

            if (!isRoundEnded)
            {

                bool isULTActive = (myEmitter != null && myEmitter.IsSyaruBitEXActive);
                
                if (!isULTActive)
                {
                    spellTimer -= Time.deltaTime; 
                    float timeRatio = Mathf.Clamp01(spellTimer / totalSpellDuration); 
                    _playerMove.ultimateEnergy = initialUltimateEnergy * timeRatio; 
                }

                // キャラクター固有の領域効果（フィールド・デバフ）の執行
                ExecuteFieldEffectToOpponent(); 

                if (spellTimer <= 0f) 
                {
                    spellTimer = 0f;
                    _playerMove.ultimateEnergy = 0f; 
                    DeactivateSpellCard(false); 
                }
            }

            // ライフバーのアニメーションはフリーズ中も滑らかに追従させるため、ifの外側に配置
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
        // =========================================================================
        // 🌟【新機能】：領域発動可能 ＆ 領域返し成立の「瞬間」限定SEトリガーシステム
        // =========================================================================
        if (_playerMove != null && !isSpellCardActive && PlayerMove.CanShoot)
        {
            // --- 🔮 1. 領域返し（カウンターVJT）の成立条件リアルタイム先読み ---
            bool isCounterCurrentlyReady = false;

            // 相手が領域展開中、自分が焼き切れ状態でなく、200%以上のエネルギーを保持している場合
            if (isAnyVJTActive && !isOverheated && _playerMove.ultimateEnergy >= 200f)
            {
                PlayerMove oppMove = _playerMove.Opponent;
                PlayerStatusManager oppStatus = oppMove != null ? oppMove.GetComponent<PlayerStatusManager>() : null;

                if (oppStatus != null && oppStatus.isSpellCardActive)
                {
                    // 領域返しの持続時間差分をシミュレート計算
                    float myProgress = Mathf.InverseLerp(200f, 300f, _playerMove.ultimateEnergy);
                    float myExpectedDuration = Mathf.Lerp(minSpellDuration, maxSpellDuration, myProgress);
                    float oppRemainingTime = oppStatus.spellTimer;

                    if (myExpectedDuration - oppRemainingTime > 10f)
                    {
                        isCounterCurrentlyReady = true; // 領域返し条件完全成立！
                    }
                }
            }

            // --- 🔷 2. 通常VJTの発動可能条件 ---
            // 条件：200%以上、かつ焼き切れデバフ中でなく、世界に誰も領域を展開していない平和な時
            bool isVJTCurrentlyReady = _playerMove.ultimateEnergy >= 200f && !isOverheated && !isAnyVJTActive;


            // --- 🔊 3. 領域返し可能になった【瞬間】のSE再生 ---
            if (isCounterCurrentlyReady && !_wasCounterReadyLastFrame)
            {
                // 🚨 ここにお好みの「カウンター成立警告音」のSEパスを指定してください！
                // 例として、より緊迫感のある高音や警告系のSEを割り当てると最高に盛り上がります。
                if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.GETSPELLCARD, 1.0f);
                Debug.Log("<color=red>🔔【VJT UI】💥領域返し（カウンターVJT）が完全成立しました！チャンス音再生！</color>");
            }

            // --- 🔊 4. 通常VJT発動可能になった【瞬間】のSE再生 ---
            // 💡 領域返し可能時はそちらの警告音を最優先させるため、!isCounterCurrentlyReady を挟みます
            if (isVJTCurrentlyReady && !_wasVJTReadyLastFrame && !isCounterCurrentlyReady)
            {
                // 🚨 ここにお好みの「通常発動可能音（チャージ完了音）」のSEパスを指定してください！
                if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.GETSPELLCARD, 1.0f);
                Debug.Log("<color=cyan>🔔【VJT UI】🔮通常領域（VJT）が発動可能になりました！チャージ音再生！</color>");
            }

            // --- 💾 5. 次のフレームのために現在のステートを厳密にバックアップ記憶 ---
            _wasCounterReadyLastFrame = isCounterCurrentlyReady;
            _wasVJTReadyLastFrame = isVJTCurrentlyReady;
        }
        else
        {
            // 自分がVJTを発動した、あるいはゲームセット時はフラグをクリーンにリセットし、次回に備える
            _wasCounterReadyLastFrame = false;
            _wasVJTReadyLastFrame = false;
        }




        UpdateUI(); // 👈 元々あったこの行の直前に割り込ませる形になります
    }

    public void ActivateSpellCard()
    {
        // 自分が既に展開中、または自分が焼き切れ(冷却デバフ)中、最低発動ゲージ（200%）未満なら鉄壁ガード
        if (isSpellCardActive || isOverheated || _playerMove.ultimateEnergy < 200f) return; 

        // =========================================================================
        // ⚔️【新システム】：領域返し（カウンターVJT）の割り込みジャッジ
        // =========================================================================
        if (isAnyVJTActive && !isSpellCardActive) 
        {
            PlayerMove oppMove = _playerMove != null ? _playerMove.Opponent : null; 
            PlayerStatusManager oppStatus = oppMove != null ? oppMove.GetComponent<PlayerStatusManager>() : null; 

            // 相手が本物のVJT主導権を握っているか確認
            if (oppStatus != null && oppStatus.isSpellCardActive) 
            {
                // ① 自分の現在のゲージから予定持続時間を算出（既存の発動時数式と完全同期）
                float myProgress = Mathf.InverseLerp(200f, 300f, _playerMove.ultimateEnergy); 
                float myExpectedDuration = Mathf.Lerp(minSpellDuration, maxSpellDuration, myProgress); 

                // ② 相手の残り持続時間を取得
                float oppRemainingTime = oppStatus.spellTimer; 

                // 🚨 条件評価：「自分の予定時間 - 相手の残り時間 > 10.0秒」か
                float timeDifference = myExpectedDuration - oppRemainingTime;

                if (timeDifference > 10f)
                {
                    // ➔ 【領域返し成立！】
                    Debug.Log($"<color=red>💥💥【領域返し(カウンターVJT)成立!!】時間差: {timeDifference:F2}秒</color>");

                    // 1. 相手のペナルティ：領域を即座に強制シャットダウン（自然消滅扱いで安全パージ）
                    oppStatus.DeactivateSpellCard(false); 

                    // 2. 相手のアルカナゲージ残量を現在の半分（50%）にカットして叩き割る！
                    oppMove.ultimateEnergy *= 0.5f;

                    // 3. 自分のリターン：超過した時間（timeDifference - 10秒）のみを持続時間として上書きして発動！
                    float counterVJTDuration = timeDifference;

                    ExecuteCounterActivationSequence(counterVJTDuration);
                    return;
                }
                else
                {
                    // =========================================================================
                    // 🛡️【核心修正】：AIの領域返し連打による不発SE（SPELL_OFF）のマシンガン連射の根治
                    // =========================================================================
                    // 💡 理由：AIがフラグを押しっぱなしにした際、毎フレームSEが重複再生されるのを防ぐため、
                    //          0.5秒の再再生インターバルを設け、人間の耳に心地いい警告スパンへクランプ調停します。
                    Debug.Log($"<color=yellow>🛡️【領域返し不発】... </color>");

                    if (_failedSpellSoundTimer <= 0f)
                    {
                        if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.SPELL_OFF, 0.4f);
                        _failedSpellSoundTimer = 0.5f; // 💡 0.5秒間は連続不発音の発生をがっちりフリーズブロック！
                    }
                    return;
                }
            }
        }

        // --- 以下は通常発動時（誰も領域を展開していない平和な時）の早い者勝ち処理 ---
        if (SpellCardManager.Instance != null && !SpellCardManager.Instance.TryRequestVJT(this)) 
        {
            return; 
        }

        if (Time.frameCount != lastRequestFrame) 
        {
            lastRequestFrame = Time.frameCount; 
            p1Requester = (playerId == 1) ? this : null; 
            p2Requester = (playerId == 2) ? this : null; 
            StartCoroutine(ExecuteSpellCardWithFrameCheck()); 
        }
        else 
        {
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
        ClearAllBulletsOnField();
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
            playerCollider.transform.localScale = originalColliderScale; // 通常サイズに復元

            // 🎯【新規追加】：領域終了に伴い、コライダーの半径をパッシブを考慮した適正値へリセット
            if (playerCollider is CircleCollider2D circle)
            {
                float targetRadius = originalColliderRadius;
                if (HasPassiveSkill(PassiveSkillType.LustSmall))
                {
                    targetRadius *= 0.8f; // パッシブ持ちなら0.8倍、無いなら等倍
                }
                circle.radius = targetRadius;
            }
        }

        // 🎯【修正】：対戦相手のコライダー半径、スプライト色、および「子オブジェクトの当たり判定演出」を通常状態へ解放リセット
        if (_playerMove != null && _playerMove.Opponent != null)
        {
            PlayerStatusManager oppStatus = _playerMove.Opponent.GetComponent<PlayerStatusManager>();
            if (oppStatus != null)
            {
                // 1. 相手の判定コライダーの半径を元に戻す
                if (oppStatus.playerCollider is CircleCollider2D oppCircle)
                {
                    float oppTargetRadius = oppStatus.originalColliderRadius;
                    if (oppStatus.HasPassiveSkill(PassiveSkillType.LustSmall)) oppTargetRadius *= 0.8f;
                    oppCircle.radius = oppTargetRadius;
                }

                // 2. 相手本体の色調を標準（白）に戻す（本体のスケールは一切触らない）
                SpriteRenderer oppMainSR = _playerMove.Opponent.GetComponentInChildren<SpriteRenderer>();
                if (oppMainSR != null)
                {
                    //oppMainSR.color = Color.white;
                }

                // 3. ✨【大修正】：子オブジェクトにある「当たり判定スプライト2種」のスケールを等倍に完全復元！
                // 相手オブジェクトの配下（ComponentsInChildren）から、全てのSpriteRendererを走査
                SpriteRenderer[] allOppSRs = _playerMove.Opponent.GetComponentsInChildren<SpriteRenderer>(true);
                foreach (var sr in allOppSRs)
                {
                    // 💡 相手本体（親またはメイングラフィック）のスプライトはスキップする
                    if (sr == oppMainSR) continue;

                    // 💡 オプション：もし当たり判定オブジェクトの名前が決まっている場合（例: "Hitbox" や "Atari"）は
                    // if (sr.gameObject.name.Contains("Hitbox")) の条件を挟むとより安全です。
                    // ここでは「子オブジェクトにあるスプライト＝当たり判定表示用」としてスケールを等倍(Vector3.one)に戻します。
                    if (sr.transform != _playerMove.Opponent.transform)
                    {
                        sr.transform.localScale = Vector3.one;
                        // もし「パッシブ：SmallHitbox」の時に当たり判定スプライトの見た目も小さくしていた場合は、
                        // ここを「oppStatus.HasPassiveSkill(PassiveSkillType.SmallHitbox) ? Vector3.one * 0.8f : Vector3.one;」にすると完全に一致します。
                    }
                }
            }
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
            float targetHP = maxHP * spellHpRatio;

            // 3. 🌟【条件適合】：発動時の体力が目標値（79%など）以上なら元の体力をキープ、以下なら目標値まで引き上げて回復！
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

        // 🌟【修正】：キャラクターデータアセット（Data）から固有の時間を引き抜いて適用！未設定なら20秒でフォールバック。
        overheatTimer = (characterData != null) ? characterData.characterOverheatDuration : 20f;

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

    /// <summary>
    /// 🌟【新規追加】：領域返し成功者のみがトリガーする、制限時間変調型・領域展開シークエンス
    /// </summary>
    private void ExecuteCounterActivationSequence(float overrideDuration)
    {
        ClearAllBulletsOnField(); 
        
        // 領域返し専用のド派手なシステムアナウンスログ
        Debug.Log($"<color=lime>👑【COUNTER VJT FLUSH】超過時間 {overrideDuration:F2} 秒で世界を再定義します！</color>");

        if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.CARDCALL, 1.2f); // やや強めに再生

        isSpellCardActive = true; 
        isAnyVJTActive = true;    // 世界ロックを即座に再取得
        isOverheated = false; 

        initialUltimateEnergy = _playerMove.ultimateEnergy; 
        preSpellHP = currentHP; 

        float fullArmorHP = maxHP * 30f; 

        // 🌟【ここが核心】：通常計算を無視し、引数で渡された「超過時間」を今回の総持続時間として強制適用！
        totalSpellDuration = overrideDuration;
        spellTimer = totalSpellDuration; 

        // バリア体力は自分の現在のゲージ割合（進行度）に応じて適正配分
        float progress = Mathf.InverseLerp(200f, 300f, initialUltimateEnergy); 
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

        // バリアのカラー同調適用
        if (spellBarrier != null) 
        {
            Color charColor = (characterData != null) ? characterData.imageColor : Color.white; 
            spellBarrier.SetBarrierActive(true); 
            Renderer[] barrierRenderers = spellBarrier.GetComponentsInChildren<Renderer>(true); 
            foreach (var r in barrierRenderers) 
            {
                if (r is SpriteRenderer sr) sr.color = charColor; 
                else if (r is LineRenderer lr) { lr.startColor = charColor; lr.endColor = charColor; }
                
                else if (r.material != null) r.material.color = charColor; 
            }
        }

        UpdateUI(); 
        SyncBarsImmediately(); 

        // 各種魔法陣・看板UI・2D専用背景の動的バインド処理（通常シーエンスと完全同期）
        if (spellRingPrefab != null && spawnedRingInstance == null) 
        {
            spawnedRingInstance = Instantiate(spellRingPrefab, transform.position, Quaternion.identity); 
            PlayerSpellRing_Line ringScript = spawnedRingInstance.GetComponent<PlayerSpellRing_Line>(); 
            if (ringScript != null) { ringScript.targetStatus = this; ringScript.Activate(totalSpellDuration); }
            
        }
        if (spellCirclePrefab != null && spawnedCircleInstance == null) 
        {
            spawnedCircleInstance = Instantiate(spellCirclePrefab, transform.position, Quaternion.identity); 
            PlayerSpellCircle circleScript = spawnedCircleInstance.GetComponent<PlayerSpellCircle>(); 
            if (circleScript != null) circleScript.Activate(this, totalSpellDuration); 
        }
        if (EnemySpellCardUI.Instance != null && characterData != null) 
        {
            string displayName = string.IsNullOrEmpty(characterData.spellCardName) ? characterData.characterName : characterData.spellCardName; 
            EnemySpellCardUI.Instance.DisplaySpell(displayName, 0, 0, 1000000f, false, this.playerId); 
        }
        if (VJTSpellBackgroundManager2D.Instance != null) 
        {
            VJTSpellBackgroundManager2D.Instance.SetSpellBackgroundActive(true, this.characterData); 
        }
    }

    public bool ApplyDamage(int amount)
    {
        // 🚨 ダメージを受けた瞬間にパッシブ「被弾時攻撃力強化」を持っているかスキャン
        if (HasPassiveSkill(PassiveSkillType.WrathCounter))
        {
            _passiveAtkBoostTimer = 8.0f; // 8秒間持続バフ点灯！
            Debug.Log($"<color=orange>⚔️【パッシブ発動】被弾をトリガーに8秒間、攻撃力1.3倍バフが起動しました！</color>");
        }
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
    /// 🧬 キャラクターが特定のパッシブスキルを習得しているかを安全にスキャンします
    /// 💡【虚無領域適合】：対戦相手が「虚無の領域（NihilityField）」を展開している場合、すべてのパッシブは強制的に無効化(false)されます。
    /// </summary>
    public bool HasPassiveSkill(PassiveSkillType type)
    {
        if (characterData == null || characterData.passiveSkills == null) return false; //

        // 🌌【核心：虚無領域によるパッシブ完全無効化割り込みマトリクス】
        // 💡 理由：自分がパッシブを評価しようとした際、相手が「虚無」の聖少女領域を展開中であれば、
        //          パッシブスキルを1つも持っていないものとして扱い、強制的にfalseを突き返します。
        if (_playerMove != null && _playerMove.Opponent != null)
        {
            PlayerStatusManager oppStatus = _playerMove.Opponent.GetComponent<PlayerStatusManager>();
            if (oppStatus != null && oppStatus.isSpellCardActive && oppStatus.characterData != null)
            {
                if (oppStatus.characterData.vjtEffectType == VJTEffectType.NihilityField)
                {
                    // 🚨 例外救済盾（虚無 VS 虚無）：
                    // 自分が領域デバフ完全無効化パッシブ「NihilityFieldCancel」のチェックを行っている場合だけは、
                    // 相手のパッシブ消去デバフそのものを無効化してすり抜ける必要があるため、判定のジャミング（false化）をスキップします。
                    if (type != PassiveSkillType.NihilityFieldCancel)
                    {
                        // 自身が領域無効化パッシブ（NihilityFieldCancel）を真に持っているなら、相手の虚無領域を突っぱねて通常通りパッシブを有効化！
                        // 持っていない（デバフが直撃している）なら、ここでパッシブを完全消失（falseリターン）させます。
                        if (!HasPassiveSkill(PassiveSkillType.NihilityFieldCancel))
                        {
                            return false;
                        }
                    }
                }
            }
        }

        // --- 📊 通常通りのパッシブ配列スキャン（デバフを喰らっていない、または耐え切った時のみ到達） ---
        foreach (var slot in characterData.passiveSkills) //
        { //
            if (slot.skillType == type) return true; //
        } //
        return false; //
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
        if (debugStatusText != null)
        {
            float displayCurrentHP = isSpellCardActive ? spellHP : currentHP;
            float displayMaxHP = isSpellCardActive ? spellMaxHP : maxHP;
            string hpLabel = isSpellCardActive ? "<color=gold>SP-HP</color>" : "HP";

            float displayCurrentMP = (_playerMove != null) ? _playerMove.currentEnergy : 0f;
            float displayMaxMP = (_playerMove != null) ? _playerMove.maxEnergy : 100f;
            float arcanaPercentage = (_playerMove != null) ? _playerMove.ultimateEnergy : 0f;

            string rHP = characterData != null ? characterData.rankHP.ToString() : "C";
            string rMP = characterData != null ? characterData.rankMP.ToString() : "C";
            string rAtk = characterData != null ? characterData.rankAttack.ToString() : "C";
            string rAgi = characterData != null ? characterData.rankAgility.ToString() : "C";
            string rReg = characterData != null ? characterData.rankMMPRegen.ToString() : "C";
            string rSpl = characterData != null ? characterData.rankSpellZone.ToString() : "C";

            float atkMult = 1.0f;
            if (characterData != null)
            {
                switch (characterData.rankAttack)
                {
                    case StatusRank.E: atkMult = 0.8f; break;
                    case StatusRank.D: atkMult = 0.9f; break;
                    case StatusRank.C: atkMult = 1.0f; break;
                    case StatusRank.B: atkMult = 1.1f; break;
                    case StatusRank.A: atkMult = 1.2f; break;
                    case StatusRank.EX: atkMult = 1.3f; break;
                }
            }

            float speedAgi = (_playerMove != null) ? _playerMove.normalSpeed : 5.0f;
            float speedFoc = (_playerMove != null) ? _playerMove.focusSpeed : 2.0f;

            float regenRate = (_playerMove != null) ? _playerMove.energyRegenRate : 15f;

            string passiveStatusStr = "<color=gray>Inactive</color>";
            float finalAtkMult = atkMult;

            if (IsAttackBoostActive)
            {
                finalAtkMult = atkMult * 1.3f;
                passiveStatusStr = $"<color=gold>ACTIVE ({_passiveAtkBoostTimer:F1}s)</color>";
            }

            // 🌟【新規追加】：嫉妬パッシブによる動的な上乗せ倍率を取得
            float jealousyMult = GetJealousyMultiplier();
            finalAtkMult *= jealousyMult; // 最終攻撃倍率に嫉妬倍率を乗算結合

            // 🍰【新規追加】：暴食パッシブのステータス文字列を動的に生成
            string gluttonyStatusStr = "<color=gray>OFF</color>";
            if (HasPassiveSkill(PassiveSkillType.GluttonyRegen))
            {
                float currentLimitHP = isSpellCardActive ? spellMaxHP : maxHP;
                float currentCheckHP = isSpellCardActive ? spellHP : currentHP;

                if (currentCheckHP >= currentLimitHP)
                {
                    gluttonyStatusStr = "<color=green>FULL (Idle)</color>";
                }
                else if (isSpellCardActive)
                {
                    gluttonyStatusStr = "<color=cyan>VJT FREE REGEN (+1%/s)</color>"; // 領域中無料モード
                }
                else
                {
                    float requiredEnergy = 1.0f * Time.deltaTime;
                    if (_playerMove != null && _playerMove.ultimateEnergy >= requiredEnergy)
                        gluttonyStatusStr = "<color=lime>CONSUMING REGEN (+1%/s)</color>"; // 通常消費モード
                    else
                        gluttonyStatusStr = "<color=red>NO ENERGY (Paused)</color>"; // ゲージ不足モード
                }
            }

            // 既存の slothStatusStr の下あたりに追記
            string prideStatusStr = "<color=gray>OFF</color>";
            if (HasPassiveSkill(PassiveSkillType.PrideStatusSteal))
            {
                prideStatusStr = "<color=gold>ACTIVE (Transcendence)</color>";
            }

            // 🍰 暴食パッシブのステータス表示の下あたりに追記
            string slothStatusStr = "<color=gray>OFF</color>";
            if (HasPassiveSkill(PassiveSkillType.SlothStandStillBoost))
            {
                slothStatusStr = IsSlothBoostActive() ? "<color=lime>ACTIVE (x1.5)</color>" : "<color=yellow>MOVING (Idle)</color>";
            }

            // 既存の prideStatusStr の下あたりに追記
            string nihilityStatusStr = "<color=gray>OFF</color>";
            if (HasPassiveSkill(PassiveSkillType.NihilityFieldCancel))
            {
                // 自分が虚無持ちで、かつ相手が領域を展開しているなら「BLOCKING」と点灯させる
                bool isOpponentVJTActive = (_playerMove != null && _playerMove.Opponent != null &&
                                            _playerMove.Opponent.GetComponent<PlayerStatusManager>() != null &&
                                            _playerMove.Opponent.GetComponent<PlayerStatusManager>().isSpellCardActive);

                nihilityStatusStr = isOpponentVJTActive ? "<color=cyan>ABSORB (Blocking!)</color>" : "<color=lime>ON (Ready)</color>";
            }

            float currentRadius = 0f;
            if (playerCollider is CircleCollider2D circle)
            {
                currentRadius = circle.radius;
            }

            string debugInfo =
                            $"<b>== REALTIME RESOURCE ==</b>\n" + //
                            $"{hpLabel}: {displayCurrentHP:F1} / {displayMaxHP:F1}\n" + //
                            $"MP: {displayCurrentMP:F1} / {displayMaxMP:F1}\n" + //
                            $"ARCANA: {arcanaPercentage:F1}%\n\n" + //
                            $"<b>==🧬 PASSIVE SKILL STATUS ==</b>\n" + //
                            $"AtkBoostOnHit: {passiveStatusStr}\n" + //
                            $"SmallHitbox(0.8x): {(HasPassiveSkill(PassiveSkillType.LustSmall) ? "<color=lime>ON</color>" : "<color=gray>OFF</color>")}\n" + //
                            $"JealousyBoost(Max1.5x): {(HasPassiveSkill(PassiveSkillType.JealousyAtkBoost) ? $"<color=orange>x{jealousyMult:F2}</color>" : "<color=gray>OFF</color>")}\n" + //
                            $"GluttonyRegen(1%/s): {gluttonyStatusStr}\n" + //
                            $"SlothBoost(1.3x): {slothStatusStr}\n" + //
                            $"PrideSteal: {prideStatusStr}\n" + // 👑 ここに追記
                            $"NihilityCancel: {nihilityStatusStr}\n" + // 🌌 ここに追記
                            $"[判定半径] Hitbox Radius: <color=cyan>{currentRadius:F3}</color> (Base: {originalColliderRadius:F2})\n\n" + //
                            $"<b>== 6-STATUS RANKS & VALUE ==</b>\n" + //
                            $"[体力] HP_Max: {maxHP:F1} ({rHP})\n" +
                $"[魔力] MP_Max: {displayMaxMP:F1} ({rMP})\n" +
                $"[攻撃] ATK_Mult: x{finalAtkMult:F2} (Base: x{atkMult:F1}) ({rAtk})\n" +
                $"[敏捷] SPD_High: {speedAgi:F1} / Low: {speedFoc:F1} ({rAgi})\n" +
                $"[再生] MP_Regen: {regenRate:F1}/s ({rReg})\n" +
                $"[領域] VJT_Time: {maxSpellDuration:F1}s / Min: {minSpellDuration:F1}s ({rSpl})";

            debugStatusText.text = debugInfo;
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
    /// <summary>
    /// 👁️【嫉妬の相剋】：対戦相手のアルカナゲージ残量（0% 〜 300%）に比例した攻撃倍率を動的に算出します。
    /// 💡【仕様拡張】：自身または対戦相手が聖少女領域（VJT）を展開中の場合、ゲージ量に関わらず『固定で1.5倍（最大値）』にロックされます。
    /// </summary>
    public float GetJealousyMultiplier()
    {
        // 自身がこのパッシブ（JealousyAtkBoost）を所持していない場合は等倍(1.0f)で安全リターン
        if (!HasPassiveSkill(PassiveSkillType.JealousyAtkBoost))
        {
            return 1.0f;
        }

        // 🌟【領域中固定1.5倍マックスロックガード】
        // 自分自身がVJT展開中（isSpellCardActive）であるか、
        // または世界で誰かがVJT展開中（isAnyVJTActive）で、かつ相手がVJT展開中の場合、無条件で1.5倍を確定させます。
        if (isSpellCardActive)
        {
            return 1.5f;
        }

        if (_playerMove != null && _playerMove.Opponent != null)
        {
            PlayerStatusManager oppStatus = _playerMove.Opponent.GetComponent<PlayerStatusManager>();
            if (oppStatus != null && oppStatus.isSpellCardActive)
            {
                return 1.5f; // 相手が領域展開している場合も、嫉妬の炎が最大化して固定1.5倍
            }

            // --- 📊 通常時のアルカナゲージ比例計算（0%〜300%） ---
            PlayerMove oppMove = _playerMove.Opponent.GetComponent<PlayerMove>();
            if (oppMove != null)
            {
                // アルカナゲージ（ultimateEnergy）は 0 〜 300 の範囲で蓄積
                float oppGauge = Mathf.Clamp(oppMove.ultimateEnergy, 0f, 300f);

                // ゲージ0で 0、ゲージ300で 1.0 になる割合を算出
                float gaugeRatio = oppGauge / 300f;

                // 1.0f から 最大 1.5f までの線形補間をかけて倍率を動的算出
                return Mathf.Lerp(1.0f, 1.5f, gaugeRatio);
            }
        }

        return 1.0f;
    }

    /// <summary>
    /// 🦥【怠惰の停滞】：自機が現在完全に停止している（方向キー・パッド・AIの移動入力が完全にゼロである）かつ、
    /// パッシブスキル（SlothStandStillBoost）を習得しているかをフレーム単位で精密に判定します。
    /// </summary>
    public bool IsSlothBoostActive()
    {
        // 自身がこのパッシブを所持していない、または移動コンポーネントがない場合は即座に遮断
        if (!HasPassiveSkill(PassiveSkillType.SlothStandStillBoost) || _playerMove == null)
        {
            return false;
        }

        // 🚨【値型(struct)構造完全適合アルゴリズム】
        // 💡 理由：currentFrameInput は参照型(class)ではなく値型(struct)のため、nullチェックを行うとコンパイルエラーになります。
        //          そのため、_playerMove自体の生存チェックのみを行い、入力データ(h, v)の絶対値をダイレクトに評価します。

        // 水平入力(h)または垂直入力(v)のどちらかに少しでも数値が入っていれば「移動中」と判定
        bool isMovingInputActive = Mathf.Abs(_playerMove.currentFrameInput.h) > 0.001f ||
                                   Mathf.Abs(_playerMove.currentFrameInput.v) > 0.001f;

        // 「移動入力が上下左右ともに一切ない（false）」の時だけ、真の怠惰状態として true を返します！
        return !isMovingInputActive;
    }

    // =========================================================================
    // 👑【新規拡張】傲慢パッシブ：ステータススキャン＆ランクアップマトリクス
    // =========================================================================
    private class StatusEvaluator
    {
        public string Name;
        public StatusRank Rank;
        public int Order; // 同率時のプライオリティ（上からの順：HP=0, MP=1, Agility=2, MMPRegen=3, SpellZone=4）
    }

    /// <summary>
    /// 👑【傲慢の超越】：対戦相手の【全6大基礎ステータス】をリアルタイムに一斉スキャンし、
    /// 最も低いステータス第1位・第2位を特定して自身のランクを1段階引き上げ、パラメーターを再展開します。
    /// </summary>
    private void ExecutePrideStatusSteal()
    {
        if (!HasPassiveSkill(PassiveSkillType.PrideStatusSteal) || _playerMove == null || _playerMove.Opponent == null) return;

        PlayerStatusManager oppStatus = _playerMove.Opponent.GetComponent<PlayerStatusManager>();
        if (oppStatus == null || oppStatus.characterData == null) return;

        // 🚨【安全弁】：上書き前のピュアな初期6大ランクを構造体キャッシュに一括完全ロック記憶！
        if (characterData != null && !_hasCachedRank)
        {
            _originalBackup.hp = characterData.rankHP;
            _originalBackup.mp = characterData.rankMP;
            _originalBackup.attack = characterData.rankAttack; // ⚔️ 構造体へ綺麗に格納
            _originalBackup.agility = characterData.rankAgility;
            _originalBackup.mmpRegen = characterData.rankMMPRegen;
            _originalBackup.spellZone = characterData.rankSpellZone;
            _hasCachedRank = true;
        }

        // 1. 完全な6大ステータスを評価用リストに格納（インスペクターの並び順に準拠）
        var oppStats = new System.Collections.Generic.List<StatusEvaluator>
        {
            new StatusEvaluator { Name = "HP",        Rank = oppStatus.characterData.rankHP,        Order = 0 },
            new StatusEvaluator { Name = "MP",        Rank = oppStatus.characterData.rankMP,        Order = 1 },
            new StatusEvaluator { Name = "Attack",    Rank = oppStatus.characterData.rankAttack,    Order = 2 },
            new StatusEvaluator { Name = "Agility",   Rank = oppStatus.characterData.rankAgility,   Order = 3 },
            new StatusEvaluator { Name = "MMPRegen",  Rank = oppStatus.characterData.rankMMPRegen,  Order = 4 },
            new StatusEvaluator { Name = "SpellZone", Rank = oppStatus.characterData.rankSpellZone, Order = 5 }
        };

        // 2. ランクが低い順（E->EX）でソート。同率の場合は Order が小さい順に安定ソート
        oppStats.Sort((a, b) =>
        {
            if (a.Rank != b.Rank) return a.Rank.CompareTo(b.Rank);
            return a.Order.CompareTo(b.Order);
        });

        // 3. 1番目と2番目に低いステータスを抽出し、自身の該当ステータスを1ランクアップ
        Debug.Log($"<color=gold>👑【傲慢のスキャン】相手({oppStatus.characterData.characterName})の低スペック上位: 1位 {oppStats[0].Name}({oppStats[0].Rank}), 2位 {oppStats[1].Name}({oppStats[1].Rank})</color>");

        for (int i = 0; i < 2; i++)
        {
            UpgradeTargetStatus(oppStats[i].Name);
        }

        // 4. 自身のパラメーター評価マトリクスを一から再計算して適用
        ApplyCharacterRanks();
    }

    private void UpgradeTargetStatus(string statName)
    {
        if (characterData == null) return;

        switch (statName)
        {
            case "HP":        characterData.rankHP = GetNextRank(characterData.rankHP); break;
            case "MP": characterData.rankMP = GetNextRank(characterData.rankMP); break;
            case "Attack": characterData.rankAttack = GetNextRank(characterData.rankAttack); break; // ⚔️ ランクアップ対象
            case "Agility": characterData.rankAgility = GetNextRank(characterData.rankAgility); break;
            case "MMPRegen": characterData.rankMMPRegen = GetNextRank(characterData.rankMMPRegen); break;
            case "SpellZone": characterData.rankSpellZone = GetNextRank(characterData.rankSpellZone); break;
        }
    }

    /// <summary>
    /// 🎯 戦闘終了時やデバッグ中断時に、構造体から元のランクを完全復元するアセット保護安全弁
    /// </summary>
    public void RestoreOriginalRank()
    {
        if (_hasCachedRank && characterData != null)
        {
            characterData.rankHP = _originalBackup.hp;
            characterData.rankMP = _originalBackup.mp;
            characterData.rankAttack = _originalBackup.attack; // ⚔️ 構造体から美しく一括復元！
            characterData.rankAgility = _originalBackup.agility;
            characterData.rankMMPRegen = _originalBackup.mmpRegen;
            characterData.rankSpellZone = _originalBackup.spellZone;

            Debug.Log($"<color=cyan>🔄【デバッグ安全弁】{characterData.characterName} の6大ステータスランクを初期状態へ完全復元しました。</color>");
        }
    }
    /// <summary>
    /// 🦥【怠惰の牢獄】：自身が現在、対戦相手の展開する「怠惰領域」に囚われており、
    /// かつ自身が上下左右に移動中（移動入力がON）であるためマナ自動回復がフリーズされる状態かを判定します。
    /// </summary>
    public bool IsSlothRegenBlocked()
    {
        // 相手が無事に存在しており、かつその相手が聖少女領域（VJT）を展開中かチェック
        if (_playerMove != null && _playerMove.Opponent != null)
        {
            PlayerStatusManager oppStatus = _playerMove.Opponent.GetComponent<PlayerStatusManager>();
            if (oppStatus != null && oppStatus.isSpellCardActive && oppStatus.characterData != null)
            {
                // 相手の領域効果が「怠惰（SlothStagnation）」である場合
                if (oppStatus.characterData.vjtEffectType == VJTEffectType.SlothStagnation)
                {
                    // 🚨 自分が「虚無パッシブ（NihilityFieldCancel）」を持っているなら、この領域デバフごと完全に踏み倒して無効化！
                    if (HasPassiveSkill(PassiveSkillType.NihilityFieldCancel)) return false;

                    // 自身が現在、キーボードやAIによって「上下左右に移動中」であるかをチェック
                    bool isCurrentlyMoving = Mathf.Abs(_playerMove.currentFrameInput.h) > 0.001f ||
                                             Mathf.Abs(_playerMove.currentFrameInput.v) > 0.001f;

                    // 「移動中」であれば、マナ回復をフリーズさせるために true を返します
                    return isCurrentlyMoving;
                }
            }
        }
        return false;
    }

    void OnDestroy()
    {
        // 🚨【重要】エディタで再生を途中で停止した場合も、このOnDestroyは高確率で走ります。
        // ここで元のランクに戻すことで、アセットの書き換わりを物理的にガードします。
        RestoreOriginalRank();
    }

    private StatusRank GetNextRank(StatusRank current)
    {
        if (current == StatusRank.EX) return StatusRank.EX;
        return (StatusRank)((int)current + 1);
    }

    public void SetInvincible(float duration)
    {
        invincibleTimer = duration;
        deathBombTimer = 0;
        if (_playerMove != null) _playerMove.SetInvincible(duration);
    }
    /// <summary>
    /// 🌟 画面上に存在しているすべてのプレイヤー弾および敵弾をスキャンして安全に完全パージする
    /// </summary>
    private void ClearAllBulletsOnField()
    {
        DanmakuBullet[] pBullets = Object.FindObjectsByType<DanmakuBullet>(FindObjectsSortMode.None);
        foreach (var b in pBullets) b.Deactivate(true);

        EnemyBullet[] eBullets = Object.FindObjectsByType<EnemyBullet>(FindObjectsSortMode.None);
        foreach (var b in eBullets) b.Deactivate(true);
    }
    /// <summary>
    /// 🌟【コンパイルエラー修正版】：領域を展開している側から、対戦相手に対して固有のデバフを毎フレーム流し込む
    /// </summary>
    private void ExecuteFieldEffectToOpponent()
    {
        // 🚨【修正】：_playerMove.Opponent の型に適合させるため、直接 null チェックを行います
        if (characterData == null || _playerMove == null || _playerMove.Opponent == null) return;

        // 🚨【修正】：Opponent (PlayerMove) から .gameObject を経由して安全にコンポーネントを相互参照します
        PlayerMove oppMove = _playerMove.Opponent;
        GameObject oppObj = oppMove.gameObject;
        PlayerStatusManager oppStatus = oppObj.GetComponent<PlayerStatusManager>();

        if (oppStatus == null) return;

        // 💡 相手が無敵状態（IsInvincible）なら領域効果を完全ガード
        if (oppStatus.IsInvincible) return;

        // =========================================================================
        // 🌌【新規追加：虚無の境界デバフ無効化】
        // =========================================================================
        // 💡 核心：デバフを受けようとしている対戦相手（oppStatus）がパッシブ「虚用」を所持している場合、
        //          これ以降の憤怒（スリップダメ）、色欲（判定巨大化）、強欲（自傷）のスイッチ処理を100%完全遮断します！
        if (oppStatus.HasPassiveSkill(PassiveSkillType.NihilityFieldCancel))
        {
            return;
        }

        float value_W = 1.0f;
        float value_L = 1.5f;

        switch (characterData.vjtEffectType)
        {
            // =========================================================================
            // 🔥 1. 憤怒：【命の摩耗】（小数点蓄積型スリップダメージ）
            // =========================================================================
            case VJTEffectType.WrathBurn:
                // 1秒間に value 分だけ削る純粋な小数ダメージ（例: 20 * 0.016 = 0.32）をプールに加算
                _hpDrainAccumulator += value_W * Time.deltaTime;

                // プールされたダメージが「1.0（1ダメージ分）」を超えたかジャッジ
                if (_hpDrainAccumulator >= 1f)
                {
                    // 貯まった分から整数部分（1や2など）を安全に引っこ抜く
                    int damageToApply = Mathf.FloorToInt(_hpDrainAccumulator);

                    // 整数ダメージを確定させて相手に与える！
                    oppStatus.ApplyDamage(damageToApply);

                    // 適用した分の数値をプールから減算し、余った小数（0.32など）は次のフレームへキャリーオーバー
                    _hpDrainAccumulator -= damageToApply;
                }
                break;

            // =========================================================================
            // ❤️ 2. 色欲：【肉体の無防備化】（コライダー半径の巨大化 ＆ 魂の蒼色同調のみを執行）
            // =========================================================================
            case VJTEffectType.LustHit:
                if (oppStatus.playerCollider is CircleCollider2D oppCircle)
                {
                    // 💡 A. 当たり判定（半径）の巨大化：
                    //    相手がパッシブを持っていれば縮小された（0.8倍）状態の半径から、領域倍率（value = 1.5fなど）を正確に乗算結合！
                    float currentBaseRadius = oppStatus.HasPassiveSkill(PassiveSkillType.LustSmall) ? oppStatus.originalColliderRadius * 0.8f : oppStatus.originalColliderRadius;
                    oppCircle.radius = currentBaseRadius * value_L;
                }
                break;
            // =========================================================================
            // 🪙 3. 強欲：【行動への重税】（攻撃フレーム持続型・確定徴税システム＋コスト領域）
            // =========================================================================
            case VJTEffectType.GreedCast:
                SkillManager oppSkill = oppObj.GetComponentInChildren<SkillManager>();
                if (oppSkill != null)
                {
                    // 💡【設計変更ノート】：
                    // コスト回復率の半減（相手）および1.5倍（自分）の制御は、
                    // 領域フラグをトリガーとして SkillManager 側のマナ自動回復システム内で
                    // 安全に一元計算（毎フレーム自動判定）されるため、ここではダメージ徴税のみを執行します。

                }
                break;
            // =========================================================================
            // 👁️ 5. 嫉妬：【視界の剥奪】（超高速・超高密度目隠し結界マトリクス）
            // =========================================================================
            case VJTEffectType.JealousyFog:
                if (jealousyFogPrefab == null) return;

                // 💡【内部固定設計】：出る間隔をさらに短縮！「0.05秒(毎秒20回)」の極小スパンで大連射を执行
                _fogSpawnTimer += Time.deltaTime;
                if (_fogSpawnTimer >= 0.01f)
                {
                    _fogSpawnTimer = 0f;

                    // 💡【出る個数を多く】：1回のトリガーにつき「12個」の霧を同時に実体化！
                    for (int i = 0; i < 12; i++)
                    {
                        // 相手の現在地を中心に、半径 1.8 ユニット以内の広範囲にばらつかせて画面を覆い尽くします
                        Vector2 randomOffset = Random.insideUnitCircle * 1.8f;
                        Vector3 spawnPosition = oppObj.transform.position + new Vector3(randomOffset.x, randomOffset.y, 0f);

                        // 霧オブジェクトを生成
                        GameObject fogInstance = Instantiate(jealousyFogPrefab, spawnPosition, Quaternion.identity);

                        // 相手が巨大化しているなら、目隠しの霧のベースサイズもその大きさに自動追従拡張
                        if (oppStatus.playerCollider != null)
                        {
                            fogInstance.transform.localScale = oppStatus.playerCollider.transform.localScale;
                        }
                    }
                }
                break;
            // =========================================================================
            // 🍰 6. 暴食：【無限の重力圏】（移動ベクトル干渉型・抵抗可能引力システム）
            // =========================================================================
            case VJTEffectType.GluttonyPull:
                // 💡【パワーバランス調停】：相手がキー入力でギリギリ抵抗できる適正値（1.5f）に調整
                //    相手の通常の最高敏捷速度（3.8f〜6.8f）より低く設定することで、キーを入れれば脱出可能にします。
                const float PULL_FORCE = 1.0f;

                Vector2 myPosition = transform.position;
                Vector2 oppPosition = oppObj.transform.position;

                // 相手から自分（使用者）へ向かう引力の正規化ベクトルを計算
                Vector2 pullDir = (myPosition - oppPosition).normalized;

                if ((myPosition - oppPosition).sqrMagnitude > 0.01f)
                {
                    // 🚨 座標を直接上書きするのを完全に廃止！
                    // 💡 相手のPlayerMoveコンポーネントに対し、「毎秒 PULL_FORCE の速度で引き寄せるベクトル」をパッシング注入します。
                    oppMove.AddExternalPull(pullDir * PULL_FORCE);
                }
                break;
            // =========================================================================
            // 🦥 7. 怠惰：【停滞の牢獄】（移動速度0.9倍化デバフの執行）
            // =========================================================================
            case VJTEffectType.SlothStagnation:
                // 💡【内部固定設計】：相手の移動速度倍率を常時「0.9倍」にデバフ変調クランプ！
                //    ※ 固定値 1.0f に戻る処理は、領域終了時の既存のリセットインフラ層が自動で通常等速へと安全ケアします。
                oppMove.skillSpeedMultiplier = 0.9f;
                break;
        }
    }
}