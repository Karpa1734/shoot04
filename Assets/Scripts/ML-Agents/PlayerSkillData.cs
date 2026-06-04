// --- PlayerSkillData.cs 修正完全版 ---
using UnityEngine;

public enum SkillPatternType
{
    Standard, nWay, Round, Polygon, Line, Custom,
    MovingArc,
    RandomRound,
    Boomerang,
    DefensiveField,
    ChainRandomAim,
    RotatingAllWayLaser,
    RotatingAccelRound,
    GreedTaxPossession,
    KarinScalesSlash,
    KarinFireSlash
}
public enum VJTEffectType
{
    None,
    HpDrain,       // 🔷 憤怒：【命の摩耗】（時間経過でじわじわスリップダメージ）
    SlowDown,      // 🟢 相手の移動速度を低下させる（既存の鈍化用）
    SizeUp,        // 🔶 色欲：【肉体の無防備化】（相手の当たり判定を巨大化させる）
    ActionTax      // 🪙 強欲：【行動への重税】（相手が攻撃スキルを撃つたびに自傷ダメージ）
}

[CreateAssetMenu(fileName = "NewPlayerSkillData", menuName = "Danmaku/PlayerSkillData")]
public class PlayerSkillData : ScriptableObject
{
    [Header("Character Info")]
    public string characterName = "キャラクター名";
    public Color imageColor = Color.white;

    [Header("🌟 Character Specific VJT Settings")]
    [Tooltip("このキャラクター独自の聖少女領域（VJT）の技名を記入してください")]
    public string spellCardName = "〇符「〇〇〇〇」";
    [Tooltip("このキャラクターがVJT発動時に足元に展開する魔法陣の画像を登録してください")]
    public Sprite spellCircleSprite;

    // =========================================================================
    // 🌟【エラー根治】：PlayerStatusManagerがアクセスする固有の術式冷却持続時間
    // =========================================================================
    [Header("--- VJT Overheat Settings ---")]
    [Tooltip("このキャラクターがVJTを解除・破砕された後の【術式焼き切れ（冷却期間）】の持続時間（秒）")]
    public float characterOverheatDuration = 20f; // 🚨 デフォルト値を20秒に完全固定

    [Header("--- Character Specific Spell BG Settings ---")]
    [Tooltip("手法1：背景の土台としてそのままループスクロールさせるスプライトを登録（未設定なら共通の既定画像を使用）")]
    public Sprite characterSpellBGBase;
    [Tooltip("手法2：土台の上で『加算合成しながらぐるぐる回転スクロール』させる幾何学模様などのスプライトを登録")]
    public Sprite characterSpellBGAdditive;

    [Header("--- VJT BG Animation Toggles ---")]
    [Tooltip("【下敷き背景】をスクロールさせますか？（チェックを外すと完全停止します。デフォルト：OFF）")]
    public bool isBaseScrollActive = false;
    [Tooltip("【加算上画像】を回転させますか？（デフォルト：ON）")]
    public bool isAdditiveRotateActive = true;
    [Tooltip("【加算上画像】をスクロールさせますか？（デフォルト：ON）")]
    public bool isAdditiveScrollActive = true;

    [Header("--- VJT BG Speed Settings ---")]
    [Tooltip("【下敷き背景】のスクロール速度（X, Y）を設定します")]
    public Vector2 baseScrollSpeed = new Vector2(0f, -0.4f);
    [Tooltip("【加算上画像】の回転速度（1秒間に回転する度数。マイナスで逆回転）")]
    public float additiveRotateSpeed = 25f;
    [Tooltip("【加算上画像】のスクロール速度（X, Y）を設定します")]
    public Vector2 additiveScrollSpeed = new Vector2(0.2f, 0.2f);

    [Header("--- VJT Spell Field Effects ---")]
    [Tooltip("このキャラクターがVJTを展開した際に相手に与える領域効果の種類")]
    public VJTEffectType vjtEffectType = VJTEffectType.None;
    [Tooltip("効果の強度（例：HpDrainなら1秒間のダメージ、SizeUpならコライダースケール倍率）")]
    public float vjtEffectValue = 10f;

    [Header("Character Energy Settings")]
    public float maxEnergy = 100f;
    public float energyRegenRate = 15f;

    [System.Serializable]
    public struct SkillSettings
    {
        public string skillName;
        public Sprite skillIcon;
        public SkillPatternType patternType;
        public BulletData bulletData;
        public float cooldown;
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