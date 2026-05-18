// --- PlayerSkillData.cs 修正版 ---
using UnityEngine;

public enum SkillPatternType
{
    Standard, nWay, Round, Polygon, Line, Custom,
    MovingArc, // ★ 追加：動く弾源パターン // ★ 追加：円弧状に弾源を設置するパターン
    RandomRound, // ★ 追加：ランダム位置からの全方位弾
    Boomerang,
    DefensiveField,
    ChainRandomAim,
    RotatingAllWayLaser,
    RotatingAccelRound,
    GreedTaxPossession
}

[CreateAssetMenu(fileName = "NewPlayerSkillData", menuName = "Danmaku/PlayerSkillData")]
public class PlayerSkillData : ScriptableObject
{
    [Header("Character Info")]
    public string characterName = "キャラクター名";
    public Color imageColor = Color.white;
    [Header("Character Energy Settings")]
    public float maxEnergy = 100f;        // 最大コスト
    public float energyRegenRate = 15f;   // 1秒あたりの回復量（キャラごとに差をつける）
    [System.Serializable]
    public struct SkillSettings
    {
        public string skillName;
        public Sprite skillIcon;
        public SkillPatternType patternType;
        public BulletData bulletData;
        public float cooldown; // ★ これが「次の射撃までの待ち時間」になります
        public string sePath;

        [Header("Pattern Parameters")]
        public int count;
        public float speed;
        public float angleOffset;
        public float wideAngle;

        public float moveSpeedMultiplier;
        [Header("Effect Parameters")]
        public float delay;

        // ★ maxBurstCount と burstInterval はコスト制への移行に伴い削除
        public float cost;
        // --- PlayerSkillData.cs (想定) の SkillSettings 構造体内に追記 ---
        public float ultimateGain; // このスキルを使用した時に溜まるゲージ量
    }


    [Header("Skill Definitions")]
    public SkillSettings skillZ;
    public SkillSettings skillX;
    public SkillSettings skillC;
    public SkillSettings skillV;
}