// --- PlayerSkillData.cs 【スキルごとの同時使用許可フラグ搭載版】 ---
using UnityEngine;

// 💡 6段階のステータスランクを完全定義
public enum StatusRank { E, D, C, B, A, EX }

public enum SkillPatternType
{
    Standard, nWay, Round, Polygon, Line, Custom,
    MovingArc,
    RandomRound,
    Wrath_Skill_C,
    Wrath_Skill_V,
    Greed_Skill_Z,
    Greed_Skill_C,
    Greed_Skill_X,
    Greed_Skill_V,
    Wrath_Skill_Z,
    Wrath_Skill_X,
    Saiki,
    Lust_Skill_X,
    Lust_Skill_C
}

public enum PassiveSkillType
{
    None,
    WrathCounter,     // ⚔️ 逆境の咆哮：被弾時に8秒間、攻撃力が1.3倍に上昇
    GreedReduction,  // ⚡ 術式最適化：マナ回復開始までのディレイ（待ち時間）が0.8倍に短縮
    LustSmall,          // 🛡️ 零式光学迷彩：常時、自身の当たり判定を0.8倍に縮小
    JealousyAtkBoost,     // 👁️ 嫉妬の相剋：相手のアルカナゲージが高いほど攻撃力上昇（最大1.5倍）
    GluttonyRegen,        // 🍰 暴食の超再生：毎秒アルカナゲージを消費し体力を回復（領域中は消費ゼロ）
    SlothStandStillBoost, // 🦥 怠惰の停滞：自機停止時(移動速度0)にコスト回復力＆リキャスト速度1.3倍
    PrideStatusSteal,     // 👑 傲慢の超越：相手の低ステータストップ2をスキャンし、自身の該当ステータスを1ランク上昇
    NihilityFieldCancel   // 🌌 虚無の境界：相手が展開する領域（VJT）のデバフ効果を一切受け付けない
}

public enum VJTEffectType
{
    None,
    WrathBurn,       // 🔷 憤怒：【命の摩耗】（時間経過でじわじわスリップダメージ）
    LustHit,        // 🔶 色欲：【肉体の無防備化】（相手の当たり判定を巨大化させる）
    GreedCast,      // 🪙 強欲：【行動への重税】（相手が攻撃スキルを撃つたびに自傷ダメージ）
    JealousyFog,    // 👁️ 嫉妬：【目隠し霧】（視界を遮る霧を展開）
    GluttonyPull,
    SlothStagnation,
    PrideInversion,
    NihilityField,
}


[System.Serializable]
public struct PassiveSkillSlot
{
    public PassiveSkillType skillType;
    [Tooltip("パッシブスキルの名前")] public string passiveName;
    [Tooltip("フレーバーテキスト（説明文）")] public string description;
}

[CreateAssetMenu(fileName = "NewPlayerSkillData", menuName = "Danmaku/PlayerSkillData")]
public class PlayerSkillData : ScriptableObject
{
    // =========================================================================
    // 📊【修復・完全溶接】：6大パラメーター・ランクインフラ
    // =========================================================================
    [Header("📊 キャラクター基礎ステータス評価（E ～ EX）")]
    [Tooltip("体力(最大HP)の高さ評価")]
    public StatusRank rankHP = StatusRank.C;

    [Tooltip("魔力(最大マナ)の高さ評価")]
    public StatusRank rankMP = StatusRank.C;

    [Tooltip("攻撃(弾幕の基礎攻撃力倍率)の高さ評価")]
    public StatusRank rankAttack = StatusRank.C;

    [Tooltip("敏捷(高速移動速度の速さ倍率)評価")]
    public StatusRank rankAgility = StatusRank.C;

    [Tooltip("マナ再生(マナゲージの自動回復速度)評価")]
    public StatusRank rankMMPRegen = StatusRank.C;

    [Tooltip("領域(聖少女領域の最大持続時間)評価")]
    public StatusRank rankSpellZone = StatusRank.C;


    [Header("Character Info")]
    public string characterName = "キャラクター名";
    public Color imageColor = Color.white;
    [Tooltip("キャラクター選択画面で表示する立ち絵Sprite")]
    public Sprite characterSprite;
    [Header("🌟 Character Specific VJT Settings")]
    [Tooltip("このキャラクター独自の聖少女領域（VJT）の技名を記入してください")]
    public string spellCardName = "〇符「〇〇〇〇」";
    [Header("🌟 特性名・領域名の直接指定テキスト")]
    [Tooltip("画像のような形式で表示するパッシブスキル名（例: 燃え上がる怒り）")]
    public string customPassiveName = "燃え上がる怒り";
    [Tooltip("パッシブスキルの詳細説明文")]
    [TextArea(2, 4)] public string customPassiveDescription = "被弾時に攻撃力が上昇する";

    [Tooltip("聖少女領域（VJT）の固有名称（例: フラメルの賢者石）")]
    public string customSpellCardDisplayName = "フラメルの賢者石";
    [Tooltip("聖少女領域の詳細説明文")]
    [TextArea(2, 4)] public string customSpellCardDescription = "領域展開中の特殊効果説明";

    [Tooltip("このキャラクターがVJT発動時に足元に展開する魔法陣の画像を登録してください")]
    public Sprite spellCircleSprite;

    [Header("🌟 Character Animation Settings")]
    [Tooltip("このキャラクター専用の Animator Controller をここに登録してください")]
    public RuntimeAnimatorController characterAnimatorController;

    [Header("--- VJT Overheat Settings ---")]
    [Tooltip("このキャラクターがVJTを解除・破砕された後の【術式焼き切れ（冷却期間）】の持続時間（秒）")]
    public float characterOverheatDuration = 20f;

    [Header("--- Character Specific Spell BG Settings ---")]
    public Sprite characterSpellBGBase;
    public Sprite characterSpellBGAdditive;

    [Header("--- VJT BG Animation Toggles ---")]
    public bool isBaseScrollActive = false;
    public bool isAdditiveRotateActive = true;
    public bool isAdditiveScrollActive = true;

    [Header("--- VJT BG Speed Settings ---")]
    public Vector2 baseScrollSpeed = new Vector2(0f, -0.4f);
    public float additiveRotateSpeed = 25f;
    public Vector2 additiveScrollSpeed = new Vector2(0.2f, 0.2f);

    [Header("🧬 Passive Skills List")]
    public System.Collections.Generic.List<PassiveSkillSlot> passiveSkills;

    [Header("--- VJT Spell Field Effects ---")]
    public VJTEffectType vjtEffectType = VJTEffectType.None;

    [System.Serializable]
    public struct SkillSettings
    {
        public string skillName;
        public Sprite skillIcon;
        [TextArea(3, 5)]
        public string skillDescription;
        public SkillPatternType patternType;
        public BulletData bulletData;
        [Tooltip("跡引き（トレイル）に別種の弾を使いたい場合はここに別のアセットを登録してください（未設定ならメイン弾と同じになります）")]
        public BulletData trailBulletData;
        public float cooldown;

        [Tooltip("ボタン長押しによる引き絞り/溜めチャージ系スキルの場合はチェックを入れてください")]
        public bool isChargeSkill;

        // 🌟【新規追加】：このスキルを使用中も、他のスキルの同時使用（並列実行）を許可するかどうか
        [Tooltip("チェックを入れると、このスキルの持続中であっても他のスキルを同時に使用できるようになります")]
        public bool isConcurrentAllowed;

        public string sePath;

        [Header("Pattern Parameters")]
        public int count;
        public float speed;
        public float angleOffset;
        public float wideAngle;

        public float moveSpeedMultiplier;
        [Header("Effect Parameters")]
        public float delay;

        public float cost;
        public float ultimateGain;
    }

    [Header("Normal Skills")]
    public SkillSettings skillZ;
    public SkillSettings skillX;
    public SkillSettings skillC;
    public SkillSettings skillV;

    [Header("★ Ultimate Skills (Gauge 100% Base)")]
    public SkillSettings skillEX;
}