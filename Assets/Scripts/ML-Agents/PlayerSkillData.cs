// --- PlayerSkillData.cs 修正版 ---
using UnityEngine;

public enum SkillPatternType { Standard, nWay, Round, Polygon, Line, Custom }

[CreateAssetMenu(fileName = "NewPlayerSkillData", menuName = "Danmaku/PlayerSkillData")]
public class PlayerSkillData : ScriptableObject
{
    [Header("Character Info")]
    public string characterName = "キャラクター名"; // ★追加
    public Color imageColor = Color.white;       // ★追加

    [System.Serializable]
    public struct SkillSettings
    {
        public string skillName;
        public SkillPatternType patternType;
        public BulletData bulletData;
        public float cooldown;
        public string sePath;

        [Header("Pattern Parameters")]
        public int count;
        public float speed;
        public float angleOffset;
        public float wideAngle;

        [Header("Effect Parameters")]
        public float delay;
    }

    [Header("Skill Definitions")]
    public SkillSettings skillZ;
    public SkillSettings skillX;
    public SkillSettings skillC;
    public SkillSettings skillV;
}