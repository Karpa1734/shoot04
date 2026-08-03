// --- PlayerStatusManager.cs 【VJTタイムベース・UIブロック・エラー完全解消版】 ---
using DG.Tweening;
using KanKikuchi.AudioManager;
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerStatusManager : MonoBehaviour
{
    [Header("Player Settings")]
    public int playerId = 1;
    public PlayerSkillData characterData;
    // 🌟【新規アタッチ枠】：全キャラクターの PlayerSkillData アセット(計8体)をインスペクターでここに順番に登録してください
    [Header("📚 キャラクターアセットデータベース")]
    public PlayerSkillData[] allCharacterDataDatabase;
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
    [NonSerialized] public float overheatTimer = 0f;
    public TextMeshProUGUI hpNumericText;

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
    [Header("--- VJT Counter Timing ---")]
    [HideInInspector] public float timeSinceVJTActivated = 0f; // 🌟 領域展開からの経過時間
    // 🎯【新設】：デバッグ中断時のランク永続化を防ぐためのキャッシュ
    private StatusRank _originalCharacterRank;
    private bool _hasCachedRank = false;
    // 構造体 CharacterRankBackup _originalBackup; の下あたりに追加

    // 🕒【新規追加】：AIによる領域返し不発SEの「マシンガン大連射」を防止するためのインターバルタイマー
    private float _failedSpellSoundTimer = 0f;
    // 🎯【デバッグ安全弁】：アセットの永続上書きバグを根絶するためのディープキャッシュ構造体

    [Header("⏳ ロード画面・プログレスバー設定")]
    [Tooltip("ロード中に表示する専用のCanvasやPanel（非同期ロード中のみActiveにする）")]
    public GameObject loadingScreenCanvas;
    [Tooltip("進捗状況を表示するUI Slider（値の範囲は 0.0 ～ 1.0）")]
    public UnityEngine.UI.Slider progressBarSlider;
    [Tooltip("進捗率をパーセンテージ（例: 50%）で表示するテキストUI（任意）")]
    public TextMeshProUGUI progressText;

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
    public static bool FromCharacterSelect = false;
    void Awake()
    {
        _playerMove = GetComponent<PlayerMove>();

        int targetSelectedId = (playerId == 1) ? GameSelectionData.SelectedCharacterP1 : GameSelectionData.SelectedCharacterP2;
        if (playerId == 2 && GameModeManager.IsStoryMode)
        {
            if (StoryModeManager.CurrentActiveRoute != null && StoryModeManager.CurrentActiveRoute.stages.Count > 0)
            {
                int currentStageIdx = StoryModeManager.CurrentStageNumber - 1;
                if (currentStageIdx >= 0 && currentStageIdx < StoryModeManager.CurrentActiveRoute.stages.Count)
                {
                    targetSelectedId = StoryModeManager.CurrentActiveRoute.stages[currentStageIdx].bossCharacterId;
                    Debug.Log($"<color=magenta>🎯 [PlayerStatusManager] StoryMode優先介入！ 2PボスIDを [{targetSelectedId}] に確定更新しました。</color>");
                }
            }
        }
        bool shouldLoadFromDatabase = FromCharacterSelect || GameModeManager.IsStoryMode;

        if (shouldLoadFromDatabase && allCharacterDataDatabase != null && targetSelectedId >= 0 && targetSelectedId < allCharacterDataDatabase.Length)
        {
            characterData = Instantiate(allCharacterDataDatabase[targetSelectedId]);
            Debug.Log($"<color=lime>✅ [PlayerStatusManager] Player {playerId} ➔ データベースから ID [{targetSelectedId}] ({characterData.characterName}) を正常ロードしました。</color>");
        }
        else if (characterData != null)
        {
            // 上記以外の完全単体デバッグ時のみ、インスペクターにアタッチされたデータをコピー
            characterData = Instantiate(characterData);
        }

        if (BossPracticeManager.IsPracticeMode)
        {
            stockLives = 0; life = 0;
        }
        else if (GameModeManager.IsStoryMode)
        {
            if (playerId == 1)
            {
                life = 3;       // 1P（自機）の残機
                stockLives = 3;
            }
            // 2P（ボス）の life は StoryBossPhaseManager がフェーズ数から自動算出するため指定不要！
        }
        else
        {
            life = 0;
            stockLives = 0;
        }

        if (playerCollider == null)
        {
            playerCollider = GetComponentInChildren<Collider2D>();
        }

        if (playerCollider != null)
        {
            originalColliderScale = playerCollider.transform.localScale;

            if (playerCollider is CircleCollider2D circle)
            {
                originalColliderRadius = circle.radius;
            }
        }

        if (hitboxSprite1 != null) originalSprite1Scale = hitboxSprite1.transform.localScale;
        if (hitboxSprite2 != null) originalSprite2Scale = hitboxSprite2.transform.localScale;

        _myOwnCharacterRenderer = GetComponent<SpriteRenderer>();
        if (_myOwnCharacterRenderer == null) _myOwnCharacterRenderer = GetComponentInChildren<SpriteRenderer>();

        if (HasPassiveSkill(PassiveSkillType.LustSmall) && playerCollider != null)
        {
            CircleCollider2D startCircle = playerCollider as CircleCollider2D;
            if (startCircle != null)
            {
                startCircle.radius = originalColliderRadius * 0.8f;
                Debug.Log($"<color=lime>🛡️【パッシブ】SmallHitboxによりコライダー半径を常時0.8倍に縮小しました。</color>");
            }
        }

        // 📊 どのStart()よりも先に、選ばれたキャラの6大ステータス（最大マナや速度）を完全に確定させます
        ApplyCharacterRanks();

        // 🎯【最核心】：確定した正しい characterData に基づき、使用しない大罪Emitterを最速仕分け
        if (characterData != null)
        {
            PlayerDanmakuEmitter[] allEmitters = GetComponentsInChildren<PlayerDanmakuEmitter>(true);
            foreach (var em in allEmitters) em.enabled = false;

            if (characterData.characterName == "Karin")
            {
                var wrath = GetComponentInChildren<Emitter_Wrath>(true);
                if (wrath != null) wrath.enabled = true;
            }
            else if (characterData.characterName == "Charlotte")
            {
                var greed = GetComponentInChildren<Emitter_Greed>(true);
                if (greed != null) greed.enabled = true;
            }
            else if (characterData.characterName == "Linghua")
            {
                var lust = GetComponentInChildren<Emitter_Lust>(true);
                if (lust != null) lust.enabled = true;
            }
            else if (characterData.characterName == "Loociel")
            {
                var pride = GetComponentInChildren<Emitter_Pride>(true);
                if (pride != null) pride.enabled = true;
            }
            else if (characterData.characterName == "Shiori")
            {
                var sloth = GetComponentInChildren<Emitter_Sloth>(true);
                if (sloth != null) sloth.enabled = true;
            }
            else if (characterData.characterName == "Eruru")
            {
                var envy = GetComponentInChildren<Emitter_Envy>(true);
                if (envy != null) envy.enabled = true;
            }
            else if (characterData.characterName == "Anzu")
            {
                var gluttony = GetComponentInChildren<Emitter_Gluttony>(true);
                if (gluttony != null) gluttony.enabled = true;
            }
            else if (characterData.characterName == "Alniel")
            {
                var void_ = GetComponentInChildren<Emitter_Void>(true);
                if (void_ != null) void_.enabled = true;
            }
        }
    }

    void Start()
    {
        // =========================================================================
        // 🛠️【エディタ直接起動セーフティ（フラグ監査版）】
        // 💡 選択画面を通らずにこのシーン単体で直接再生された場合のみ、アタッチされたデータで後追い同期
        // =========================================================================
#if UNITY_EDITOR
        if (!FromCharacterSelect && characterData != null && allCharacterDataDatabase != null)
        {
            for (int i = 0; i < allCharacterDataDatabase.Length; i++)
            {
                if (allCharacterDataDatabase[i] != null && allCharacterDataDatabase[i].characterName == characterData.characterName)
                {
                    if (playerId == 1) GameSelectionData.SelectedCharacterP1 = i;
                    else if (playerId == 2) GameSelectionData.SelectedCharacterP2 = i;

                    // エディタ直接起動の時のみ、ここでアセットをリロードしてパラメータを再適用
                    characterData = Instantiate(allCharacterDataDatabase[i]);
                    ApplyCharacterRanks();
                    break;
                }
            }
            Debug.Log($"<color=yellow>🔧 [DEBUG MODE] シーン直接起動を検知。インスペクターのデバッグ用データ【{characterData.characterName}】を同期しました。</color>");
        }
#endif
        BGMManager.Instance.Play(BGMPath.BATTLE01,1.0f,1.0f);
        // 看板テキストやUIカラー、COM名への流し込みを実行
        ApplyCharacterSettings();

        if (HasPassiveSkill(PassiveSkillType.PrideStatusSteal))
        {
            ExecutePrideStatusSteal();
        }

        DanmakuAgent trainingAgent = GetComponent<DanmakuAgent>();
        if (trainingAgent != null && Unity.MLAgents.Academy.Instance.IsCommunicatorOn)
        {
            maxHP *= 100f;
        }

        currentHP = maxHP;

        // マナ（Energy）の初期値を最大ランク基準に完全同期
        if (_playerMove != null)
        {
            _playerMove.currentEnergy = _playerMove.maxEnergy;
            Debug.Log($"<color=cyan>💧 初期マナを最大ランク基準に完全同期しました。currentEnergy={_playerMove.currentEnergy}, maxEnergy={_playerMove.maxEnergy}</color>");
        }

        // =========================================================================
        // 🛡️【最核心修正】：ラウンドまたぎ時の永久チカチカ ✕ スキル封印バグの根絶パージ
        // 💡 理由：前ラウンドでEX魔槍を撃った状態のまま決着がついた際、メモリに残った
        //          各種バフ・デバフ・タイマーを新ラウンド開始時に強制初期化します。
        // =========================================================================
        isSpellCardActive = false;
        isOverheated = false;
        spellTimer = 0f;
        overheatTimer = 0f;
        invincibleTimer = 0f;

        // 自機のエミッター全体のEXフラグを最速で叩き落とす
        PlayerDanmakuEmitter[] startupEmitters = GetComponentsInChildren<PlayerDanmakuEmitter>(true);
        foreach (var em in startupEmitters)
        {
            if (em != null)
            {
                System.Reflection.FieldInfo exField = typeof(PlayerDanmakuEmitter).GetField("_isEXSkillActive", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (exField != null) exField.SetValue(em, false);
            }
        }

        // アニメーションの点滅も強制停止
        PlayerAnimation startupAnim = GetComponentInChildren<PlayerAnimation>(true);
        if (startupAnim != null)
        {
            startupAnim.isInvincible = false;
        }
        if (GameModeManager.IsStoryMode && playerId == 2)
        {
            // 1. ボス進行マネージャーの自動アタッチ
            StoryBossPhaseManager bossManager = GetComponent<StoryBossPhaseManager>();
            if (bossManager == null) bossManager = gameObject.AddComponent<StoryBossPhaseManager>();
            bossManager.enabled = true;

            // 2. 🔥【新設】：シンプル弾幕専用モジュールの自動アタッチ
            BossDanmakuExecutor bossExecutor = GetComponent<BossDanmakuExecutor>();
            if (bossExecutor == null) bossExecutor = gameObject.AddComponent<BossDanmakuExecutor>();
            bossExecutor.enabled = true;

            Debug.Log($"<color=magenta>👑【Story Mode】Player 2 ({characterData.characterName}) をステージボス＆弾幕モジュール化しました。</color>");
        }

        StartCoroutine(SetupInitialUI());
        StartCoroutine(InitUIWithDelay());
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

        // =========================================================================
        // 🎯【バグ根治】：眠っている別キャラのEmitterを誤認取得する事故を完全全面パージ！
        // 💡 現在有効（enabled == true）になって目覚めている、本物のEmitterだけを動的に抽出します。
        // =========================================================================
        PlayerDanmakuEmitter myEmitter = null;
        PlayerDanmakuEmitter[] allEmitters = GetComponents<PlayerDanmakuEmitter>();
        if (allEmitters == null || allEmitters.Length == 0) allEmitters = GetComponentsInChildren<PlayerDanmakuEmitter>(true);

        foreach (var em in allEmitters)
        {
            if (em != null && em.enabled)
            {
                myEmitter = em;
                break;
            }
        }
        if (myEmitter == null) myEmitter = GetComponentInChildren<PlayerDanmakuEmitter>();


        if (myEmitter != null && myEmitter.IsUltimateSkillActive)
        {
            invincibleTimer = 0.1f;
        }
        else if (invincibleTimer > 0)
        {
            invincibleTimer -= Time.deltaTime;
        }

        if (deathBombTimer > 0) deathBombTimer -= Time.deltaTime;

        // --- 🌟【新規追加】：ライフバー用HP数値テキストの動的更新マトリクス ---
        if (hpNumericText != null)
        {
            if (isSpellCardActive)
            {
                // 🔶 聖少女領域（VJT）展開中：上乗せバリアHPの生数値を可視化（例: 2450.0 / 3000.0）
                // 見やすさのため、バリア中はテキストのカラーをゴールド等に変調させることも可能です
                hpNumericText.text = $"{animatedSpellHP:F1} / {spellMaxHP:F1}";
                hpNumericText.color = new Color(1f, 0.85f, 0f); // ゴールド
            }
            else
            {
                // 🔷 通常状態：本体HPの生数値を可視化（例: 85.3 / 100.0）
                hpNumericText.text = $"{currentHP:F1} / {maxHP:F1}";
                hpNumericText.color = Color.white; // 通常時は白
            }
        }

        // 【VJT実行中のリアルタイム毎フレーム制御】
        if (isSpellCardActive)
        {
            if (MatchTimerUI.Instance != null) MatchTimerUI.Instance.StopTimer();
            timeSinceVJTActivated += Time.deltaTime;

            PlayerHitHandler hitHandler = GetComponentInChildren<PlayerHitHandler>();
            PlayerMove oppMove = _playerMove != null ? _playerMove.Opponent : null;
            PlayerHitHandler oppHitHandler = oppMove != null ? oppMove.GetComponentInChildren<PlayerHitHandler>() : null;

            bool isRoundEnded = (hitHandler != null && hitHandler.currentState == PlayerHitHandler.PlayerState.Down) ||
                                (oppHitHandler != null && oppHitHandler.currentState == PlayerHitHandler.PlayerState.Down);
            if (!isRoundEnded)
            {
                bool isULTActive = (myEmitter != null && myEmitter.IsUltimateSkillActive);

                // 領域の残り時間によるタイマーを進める
                spellTimer -= Time.deltaTime;

                // =========================================================================
                // 🌟【最重要修正】：領域中のアルカナゲージの制御
                // 💡 必殺技（EX）がすでに使用された後、または領域展開中にゲージが0になっている間は、
                //    領域が終了するまでゲージを完全に「0%」で固定し続けます！
                // =========================================================================
                bool hasUsedEXDuringSpell = false;
                PlayerDanmakuEmitter[] activeEmitters = GetComponentsInChildren<PlayerDanmakuEmitter>(true);
                foreach (var em in activeEmitters)
                {
                    if (em != null)
                    {
                        // Emitter側に保持されているEX使用履歴フラグや、現在のULTエネルギーが0に固定されているかを監査
                        System.Reflection.FieldInfo exField = typeof(PlayerDanmakuEmitter).GetField("_isEXSkillActive", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (exField != null && (bool)exField.GetValue(em)) { hasUsedEXDuringSpell = true; break; }
                    }
                }

                Emitter_Lust lustEm = GetComponentInChildren<Emitter_Lust>();
                if (lustEm != null && lustEm.IsEXSpearActive) hasUsedEXDuringSpell = true;

                if (hasUsedEXDuringSpell || _playerMove.ultimateEnergy <= 0.1f)
                {
                    _playerMove.ultimateEnergy = 0f; // 👈 領域終了まで0%に完全固定！
                }
                else if (!isULTActive)
                {
                    // 通常の領域維持中のみ、比率に応じたゲージ減少を許可
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

            // ライフバーのアニメーション追従
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
        else
        {
            timeSinceVJTActivated = 0f; // 領域が展開されていない時はリセット
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
        // =========================================================================
        // 🔍【VJT発動監査ログ】：なぜ領域が展開できないのかを徹底的にジャッジします
        // =========================================================================
        if (isSpellCardActive || isOverheated || _playerMove.ultimateEnergy < 200f)
        {
            string reason = "";
            if (isSpellCardActive) reason += "[すでに自身がVJT展開中] ";
            if (isOverheated) reason += $"[術式焼き切れ・冷却デバフ中 (残り {overheatTimer:F1}秒)] ";
            if (_playerMove.ultimateEnergy < 200f) reason += $"[アルカナゲージ不足 (必要:200% / 現在:{_playerMove.ultimateEnergy:F1}%)] ";

            Debug.LogError($"<color=red>❌ [VJT BLOCK] Player {playerId} の領域発動が手前で拒絶されました。 理由: {reason}</color>");
            return;
        }
        // ⚔️【新システム】：領域返し（カウンターVJT）の割り込みジャッジ
        // ⚔️【新システム】：領域返し（カウンターVJT）の割り込みジャッジ
        if (isAnyVJTActive && !isSpellCardActive)
        {
            PlayerMove oppMove = _playerMove != null ? _playerMove.Opponent : null;
            PlayerStatusManager oppStatus = oppMove != null ? oppMove.gameObject.GetComponent<PlayerStatusManager>() : null;

            if (oppStatus != null && oppStatus.isSpellCardActive)
            {
                // 🌟【新規追加】：相手が領域を発動してから「3秒（3.0秒）」経過していなければ、領域返しはまだ受け付けない！
                if (oppStatus.timeSinceVJTActivated < 3.0f)
                {
                    Debug.Log($"<color=yellow>🛡️ [VJT COUNTER BLOCKED] 相手の領域展開からまだ {oppStatus.timeSinceVJTActivated:F1}秒 です (3.0秒必要)。</color>");
                    return; // 3秒未満はカウンターを不発（あるいは入力を弾く）
                }

                float myProgress = Mathf.InverseLerp(200f, 300f, _playerMove.ultimateEnergy);
                float myExpectedDuration = Mathf.Lerp(minSpellDuration, maxSpellDuration, myProgress);
                float oppRemainingTime = oppStatus.spellTimer;
                float timeDifference = myExpectedDuration - oppRemainingTime;

                Debug.Log($"<color=yellow>⚔️ [VJT COUNTER CHECK] 領域返しジャッジ走査中... 時間差: {timeDifference:F2}秒 (必要: >10.0秒)</color>");

                // 🌟【修正】：領域返しの条件を満たしていない場合の不発判定
                if (timeDifference > 10f)
                {
                    Debug.Log($"<color=red>💥💥【領域返し(カウンターVJT)成立!!】時間差: {timeDifference:F2}秒</color>");
                    oppStatus.DeactivateSpellCard(false);
                    oppMove.ultimateEnergy *= 0.5f;
                    float counterVJTDuration = timeDifference;
                    ExecuteCounterActivationSequence(counterVJTDuration);
                    return;
                }
                else
                {
                    Debug.Log($"<color=yellow>🛡️ [VJT COUNTER FAILED] 領域返しの条件(持続アドバンテージ10秒以上)を満たしていないため、不発判定処理を行います。</color>");

                    // 🔊 音声の連打（マシンガン再生）を確実に防ぐ厳密なガード
                    if (_failedSpellSoundTimer <= 0f)
                    {
                        _failedSpellSoundTimer = 0.5f; // 0.5秒間は再発動しないようにロック
                    }
                    return;
                }
            }
        }

        // --- 以下は通常発動時（誰も領域を展開していない平和な時）の早い者勝ち処理 ---
        if (SpellCardManager.Instance != null && !SpellCardManager.Instance.TryRequestVJT(this))
        {
            Debug.LogError($"<color=red>❌ [VJT BLOCK] SpellCardManager によって発動リクエストが拒否されました。世界ロックの状態に矛盾があります。</color>");
            return;
        }

        Debug.Log($"<color=green>💎 [VJT SUCCESS] 全てのチェックを通過！排他フレームジャッジ（同時押しチェックコルーチン）へ移行します。</color>");

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
  // =========================================================================
    // 🔮【デッドロック完全根治】：同フレーム内の入力要求を安全に集約・執行する
    // =========================================================================
    private IEnumerator ExecuteSpellCardWithFrameCheck()
    {
        // 同一フレーム内の1P・2Pからの入力を集約するため、1フレームだけ安全に待機
        yield return null;

        PlayerStatusManager finalWinner = null;

        // 🚨 1. 【運命の天秤】：1Pと2Pがまったく同じフレームで同時申請していた場合
        if (p1Requester != null && p2Requester != null)
        {
            // 50%の確率でどちらを主役にするか厳密にジャッジ！
            bool isP1Winner = UnityEngine.Random.value < 0.5f;
            finalWinner = isP1Winner ? p1Requester : p2Requester;
            PlayerStatusManager finalLoser = isP1Winner ? p2Requester : p1Requester;

            Debug.Log($"<color=red>⚔️【VJT同時発動】完全同時押しジャッジ！ 勝者: [Player {finalWinner.playerId}]</color>");

            // 敗者側の予約ロックを安全に解放パージ
            if (SpellCardManager.Instance != null)
            {
                SpellCardManager.Instance.ReleaseVJT(finalLoser);
            }

            // 勝者を発動
            finalWinner.ExecuteActivationSequence();
        }
        else
        {
            // 🚨 2. 単独申請の場合：セルフロック誤認を完全に回避して、申請者をそのまま100%安全に発動！
            if (p1Requester != null) p1Requester.ExecuteActivationSequence();
            if (p2Requester != null) p2Requester.ExecuteActivationSequence();
        }

        // 次のフレームでの同時押しの為に、申請バッファをクリーンに初期化
        p1Requester = null;
        p2Requester = null;
        lastRequestFrame = -1;
    }
    private void PlayVJTCutIn()
    {
        if (characterData == null || characterData.characterSprite == null) return;

        // 1. カットイン用のGameObjectを動的生成
        GameObject cutInObj = new GameObject("VJTCutInImage_" + playerId);

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
            cutInObj.transform.SetParent(canvas.transform, false);
        }

        RectTransform cutInRect = cutInObj.AddComponent<RectTransform>();
        cutInRect.anchorMin = new Vector2(0.5f, 0.5f);
        cutInRect.anchorMax = new Vector2(0.5f, 0.5f);
        cutInRect.pivot = new Vector2(0.5f, 0.5f);

        // 🌟【修正】：元の Sprite の縦横比を計算し、高さを基準（例: 600f）にして幅を自動調整
        Sprite sprite = characterData.characterSprite;
        float targetHeight = 1200f; // 立ち絵の基準の高さ（お好みに合わせて変更可能）
        float spriteWidth = sprite.rect.width;
        float spriteHeight = sprite.rect.height;

        if (spriteWidth > 0f && spriteHeight > 0f)
        {
            float aspectRatio = spriteWidth / spriteHeight;
            cutInRect.sizeDelta = new Vector2(targetHeight * aspectRatio, targetHeight);
        }
        else
        {
            cutInRect.sizeDelta = new Vector2(400f, targetHeight); // フォールバック
        }

        UnityEngine.UI.Image cutInImage = cutInObj.AddComponent<UnityEngine.UI.Image>();
        cutInImage.sprite = sprite;
        cutInImage.preserveAspect = true;

        // 2. 1Pは「左端から」、2Pは「右端から」登場させる方向分岐
        float startX = (playerId == 1) ? -1500f : 1500f;
        float endX = 0f;          // 中央で停止
        float exitX = (playerId == 1) ? 1500f : -1500f; // 逆側へ抜ける

        // 初期位置とアルファ値（透明）のセット
        cutInRect.anchoredPosition = new Vector3(startX, 500f, 0f);
        Color c = cutInImage.color;
        c.a = 0f;
        cutInImage.color = c;

        // フェードイン
        cutInImage.DOFade(1f, 0.2f);

        // 3. DOTweenでアニメーション構築
        Sequence seq = DOTween.Sequence();

        // ① 左/右端から中央(0)へ移動
        seq.Append(cutInRect.DOAnchorPos(new Vector2(endX, 0f), 1.5f).SetEase(Ease.OutCubic));

        // ② 中央で少しの間停止（タメ）
        seq.AppendInterval(0.4f);

        // ③ 中央から逆側へ加速して画面外へ退場
        seq.Append(cutInRect.DOAnchorPos(new Vector2(exitX, -500f), 1.5f).SetEase(Ease.InCubic));

        // 退場に合わせてフェードアウト
        seq.Join(cutInImage.DOFade(0f, 0.2f).SetDelay(0.7f));

        // 演出終了後にオブジェクトを完全破棄
        seq.OnComplete(() =>
        {
            if (cutInObj != null)
            {
               Destroy(cutInObj);
            }
        });
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

        PlayVJTCutIn();
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

        // =========================================================================
        // 🌟【最重要修正】：必殺技（EXスキル）の使用による解除か、それ以外の解除かを厳密に判定
        // =========================================================================
        bool isExSkillTriggered = false;
        PlayerDanmakuEmitter[] emitters = GetComponentsInChildren<PlayerDanmakuEmitter>(true);
        foreach (var em in emitters)
        {
            if (em != null)
            {
                System.Reflection.FieldInfo exField = typeof(PlayerDanmakuEmitter).GetField("_isEXSkillActive", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (exField != null && (bool)exField.GetValue(em))
                {
                    isExSkillTriggered = true;
                    break;
                }
            }
        }

        // 🌟【追加対策】：Emitter_Lust などの個別のEX発動フラグや槍の生存状態も合わせてチェック
        Emitter_Lust lustEmitter = GetComponentInChildren<Emitter_Lust>();
        if (lustEmitter != null && lustEmitter.IsEXSpearActive)
        {
            isExSkillTriggered = true;
        }

        // 一時退避用の持ち越し値（もし非EX解除なら半分を保持、EX解除なら0）
        float finalCarryOver = 0f;

        if (isExSkillTriggered)
        {
            if (_playerMove != null)
            {
                _playerMove.ultimateEnergy = 0f;
                Debug.Log("<color=orange>👑【ULTゲージ確定リセット】必殺技（EX）使用による領域解除のため、ULTゲージを完全に 0 に固定します。</color>");
            }
        }
        else if (_playerMove != null)
        {
            finalCarryOver = _playerMove.ultimateEnergy * 0.5f;
            Debug.Log($"<color=cyan>✨【ULTゲージ持ち越し準備】領域解除時の残量 {_playerMove.ultimateEnergy}% の半分である {finalCarryOver}% をキープします。</color>");
        }

        isSpellCardActive = false; // フラグを解除
        isAnyVJTActive = false;    // 世界共有ロックをここで完全解放

        // 🌟 破砕（被弾による終了）時のみ1.0秒間の無敵保護を発動
        if (isDefeatedByDamage)
        {
            SetInvincible(1.0f);
        }

        if (spellBarrier != null)
        {
            spellBarrier.SetBarrierActive(false);
        }

        if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.SPELL_OFF, 0.5f);

        if (SpellCardManager.Instance != null)
        {
            SpellCardManager.Instance.ReleaseVJT(this);
        }

        if (playerCollider != null)
        {
            playerCollider.transform.localScale = originalColliderScale;

            if (playerCollider is CircleCollider2D circle)
            {
                float targetRadius = originalColliderRadius;
                if (HasPassiveSkill(PassiveSkillType.LustSmall))
                {
                    targetRadius *= 0.8f;
                }
                circle.radius = targetRadius;
            }
        }

        // 対戦相手のコライダーやスプライトの復元処理
        if (_playerMove != null && _playerMove.Opponent != null)
        {
            PlayerStatusManager oppStatus = _playerMove.Opponent.GetComponent<PlayerStatusManager>();
            if (oppStatus != null)
            {
                if (oppStatus.playerCollider is CircleCollider2D oppCircle)
                {
                    float oppTargetRadius = oppStatus.originalColliderRadius;
                    if (oppStatus.HasPassiveSkill(PassiveSkillType.LustSmall)) oppTargetRadius *= 0.8f;
                    oppCircle.radius = oppTargetRadius;
                }

                SpriteRenderer oppMainSR = _playerMove.Opponent.GetComponentInChildren<SpriteRenderer>();
                SpriteRenderer[] allOppSRs = _playerMove.Opponent.GetComponentsInChildren<SpriteRenderer>(true);
                foreach (var sr in allOppSRs)
                {
                    if (sr == oppMainSR) continue;
                    if (sr.transform != _playerMove.Opponent.transform)
                    {
                        sr.transform.localScale = Vector3.one;
                    }
                }
            }
        }

        // ライフ還元処理
        if (isDefeatedByDamage)
        {
            currentHP = preSpellHP;
        }
        else
        {
            float spellHpRatio = 0f;
            if (spellMaxHP > 0f)
            {
                spellHpRatio = Mathf.Clamp01(spellHP / spellMaxHP);
            }
            float targetHP = maxHP * spellHpRatio;
            currentHP = Mathf.Max(preSpellHP, targetHP);
            currentHP = Mathf.Min(currentHP, maxHP);
        }

        // 領域内の動的資源パラメータをクリア
        spellHP = 0f;
        spellMaxHP = 0f;
        spellTimer = 0f;
        totalSpellDuration = 0f;
        initialUltimateEnergy = 0f;

        // 🎯【重要】：領域解除ルーチンの最後に、EX解除なら強制0%、非EX解除であれば保持していた半分を再設定
        if (_playerMove != null)
        {
            if (isExSkillTriggered)
            {
                _playerMove.ultimateEnergy = 0f; // 🌟 EX解除時は絶対に 0% に固定
            }
            else
            {
                _playerMove.ultimateEnergy = finalCarryOver;
                if (finalCarryOver > 0f)
                {
                    Debug.Log($"<color=lime>🔋【ULTキャリーオーバー適用】非EX解除のため、次へ持ち越すゲージ {finalCarryOver}% を正しく適用しました。</color>");
                }
            }
        }

        isOverheated = true;
        overheatTimer = (characterData != null) ? characterData.characterOverheatDuration : 20f;

        if (MatchTimerUI.Instance != null)
        {
            MatchTimerUI.Instance.ResumeTimer();
        }

        UpdateUI();
        SyncBarsImmediately();

        if (spawnedRingInstance != null)
        {
            PlayerSpellRing_Line ringScript = spawnedRingInstance.GetComponent<PlayerSpellRing_Line>();
            if (ringScript != null) ringScript.Deactivate();
            Destroy(spawnedRingInstance);
            spawnedRingInstance = null;
        }

        if (spawnedCircleInstance != null)
        {
            PlayerSpellCircle circleScript = spawnedCircleInstance.GetComponent<PlayerSpellCircle>();
            if (circleScript != null) circleScript.Deactivate();
            if (spawnedCircleInstance != null)
            {
                Destroy(spawnedCircleInstance);
                spawnedCircleInstance = null;
            }
        }

        if (EnemySpellCardUI.Instance != null)
        {
            EnemySpellCardUI.Instance.HideSpell();
        }

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

        PlayVJTCutIn();
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

        // 🔮 スペルカード（バリア）展開中の処理
        // 🔮 スペルカード（バリア）展開中の処理
        // 🔮 スペルカード（バリア）展開中の処理
        if (isSpellCardActive)
        {
            spellHP -= amount;
            UpdateUI();

            if (spellHP <= 0)
            {
                spellHP = 0;

                // 🌟【最重要】：ストーリーモードかつボス(Player 2)の場合、
                // バリアを削り切った（＝剥がれた）時点で即座にスペルカード撃破（true）とする！
                bool isStoryBossSpell = GameModeManager.IsStoryMode && playerId == 2;

                DeactivateSpellCard(true);

                if (isStoryBossSpell)
                {
                    return true; // 🎯 これによりバリア剥離 ＝ スペル撃破（次段階への即時移行）が確定します
                }

                return false;
            }
            return false;
        }

        // 通常時のHPダメージ処理
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
                    // =========================================================================
                    // ⚔️【デバッグ機能】：エンドレスモード（Eキーデバッグ）の割り込み
                    // 💡 理由：IsEndlessMode が true の時は、勝ち星を一切増やさず、
                    //          即座に false を返して次のラウンド（バトル継続）へ強制移行させます！
                    // =========================================================================
                    if (GameDifficultyManager.IsEndlessMode)
                    {
                        Debug.Log("<color=orange>🔄【Endless Mode】エンドレスモード稼働中。勝ち星を加算せず、次のラウンドへ進みます。</color>");

                        // 画面上のUIの更新だけは安全に通す
                        oppStatus.UpdateUI();
                        UpdateUI();

                        return false; // ゲームセットさせず、100%次のラウンドへ継続 (false)
                    }

                    // ➔ 通常モード時（エンドレスOFF）は従来通りの2本先取ルールを適用
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

    public void UpdateUI()
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
            // 🔮 領域（VJT）展開中のUI処理
            if (spellHpBar != null)
            {
                // ストーリーモードかどうかに関わらず、VSモードでは確実に表示してバーを連動させる
                spellHpBar.gameObject.SetActive(true);
                spellHpBar.maxValue = spellMaxHP;
                spellHpBar.value = animatedSpellHP; // 🌟 ここでピンク色のバーの長さをアニメーション値に連動！
            }

            if (hpBar != null)
            {
                hpBar.gameObject.SetActive(true);
                hpBar.maxValue = maxHP;
                hpBar.value = preSpellHP;
                SetSliderAlpha(hpBar, 0.3f);
            }
        }
        else
        {
            // 🔷 通常状態のUI処理
            if (hpBar != null)
            {
                hpBar.gameObject.SetActive(true);
                hpBar.maxValue = maxHP;
                hpBar.value = currentHP;
                SetSliderAlpha(hpBar, 1.0f);
            }

            if (spellHpBar != null)
            {
                spellHpBar.gameObject.SetActive(false); // 領域外の時は非表示
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
                            $"PrideSteal: {prideStatusStr}\n" +
                            $"NihilityCancel: {nihilityStatusStr}\n" +
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
            // 🌟 従来の即時シーンロードから、不規則なフェイクプログレスバー付きの非同期ロードへ変更
            StartCoroutine(LoadSceneAsyncRoutine("Title"));
        }
    }

    /// <summary>
    /// ⏳ CharacterSelectManagerやPauseManagerと完全共通の非同期ロードコルーチン
    /// </summary>
    private IEnumerator LoadSceneAsyncRoutine(string sceneName)
    {
        if (loadingScreenCanvas != null)
        {
            loadingScreenCanvas.SetActive(true);
        }

        if (BGMManager.Instance != null)
        {
            BGMManager.Instance.FadeOut();
        }

        AsyncOperation asyncOp = SceneManager.LoadSceneAsync(sceneName);
        asyncOp.allowSceneActivation = false;

        float fakeProgress = 0f;
        float targetFakeProgress = 0f;
        float timer = 0f;

        while (!asyncOp.isDone)
        {
            float realProgress = Mathf.Clamp01(asyncOp.progress / 0.9f);

            timer -= Time.unscaledDeltaTime;
            if (timer <= 0f)
            {
                timer = UnityEngine.Random.Range(0.05f, 0.22f);

                if (fakeProgress < realProgress)
                {
                    float maxNext = Mathf.Min(realProgress, fakeProgress + UnityEngine.Random.Range(0.02f, 0.12f));
                    targetFakeProgress = UnityEngine.Random.Range(fakeProgress, maxNext);
                }
                else if (realProgress >= 1.0f && fakeProgress < 0.95f)
                {
                    targetFakeProgress = Mathf.MoveTowards(fakeProgress, 1.0f, UnityEngine.Random.Range(0.03f, 0.08f));
                }
            }

            fakeProgress = Mathf.MoveTowards(fakeProgress, targetFakeProgress, Time.unscaledDeltaTime * UnityEngine.Random.Range(0.6f, 1.5f));

            if (fakeProgress > realProgress && realProgress < 1.0f)
            {
                fakeProgress = realProgress;
            }

            if (progressBarSlider != null)
            {
                progressBarSlider.value = fakeProgress;
            }

            if (progressText != null)
            {
                progressText.text = $"{Mathf.RoundToInt(fakeProgress * 100f)}%";
            }

            if (fakeProgress >= 0.99f && realProgress >= 1.0f)
            {
                if (progressBarSlider != null) progressBarSlider.value = 1.0f;
                if (progressText != null) progressText.text = "100%";

                yield return new WaitForSecondsRealtime(0.25f);
                asyncOp.allowSceneActivation = true;
            }

            yield return null;
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
            case "HP": characterData.rankHP = GetNextRank(characterData.rankHP); break;
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
        DanmakuBullet[] pBullets = UnityEngine.Object.FindObjectsByType<DanmakuBullet>(FindObjectsSortMode.None);
        // 💡 force: true を渡して、ラウンド終了時は不滅弾も一斉強制消去！
        foreach (var b in pBullets) b.Deactivate(true, force: true);

        EnemyBullet[] eBullets = UnityEngine.Object.FindObjectsByType<EnemyBullet>(FindObjectsSortMode.None);
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
                        Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * 1.8f;
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