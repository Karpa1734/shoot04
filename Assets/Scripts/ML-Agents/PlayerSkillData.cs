// --- PlayerSkillData.cs 修正版 ---
using UnityEngine;

public enum SkillPatternType
{
    Standard, nWay, Round, Polygon, Line, Custom,
    MovingArc, // ★ 追加：動く弾源パターン // ★ 追加：円弧状に弾源を設置するパターン
    RandomRound, // ★ 追加：ランダム位置からの全方位弾
    Boomerang // ★ 追加：ブーメラン型子機
}

[CreateAssetMenu(fileName = "NewPlayerSkillData", menuName = "Danmaku/PlayerSkillData")]
public class PlayerSkillData : ScriptableObject
{
    [Header("Character Info")]
    public string characterName = "キャラクター名";
    public Color imageColor = Color.white;

    [System.Serializable]
    public struct SkillSettings
    {
        public string skillName;
        public Sprite skillIcon; // ★追加：インスペクターで画像をセットする枠
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
        [Header("Burst Settings")]
        public int maxBurstCount;   // ★追加：最大連射数（例：4）
        public float burstInterval; // ★追加：連射中の間隔（例：0.1秒）
    }

    [Header("Skill Definitions")]
    public SkillSettings skillZ;
    public SkillSettings skillX;
    public SkillSettings skillC;
    public SkillSettings skillV;
}