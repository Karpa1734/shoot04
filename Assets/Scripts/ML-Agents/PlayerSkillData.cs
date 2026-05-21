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
    GreedTaxPossession
}

[CreateAssetMenu(fileName = "NewPlayerSkillData", menuName = "Danmaku/PlayerSkillData")]
public class PlayerSkillData : ScriptableObject
{
    [Header("Character Info")]
    public string characterName = "キャラクター名";
    public Color imageColor = Color.white;

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
    public SkillSettings skillEX; // ★追加：インスペクターで完全に独立して設定可能に
}