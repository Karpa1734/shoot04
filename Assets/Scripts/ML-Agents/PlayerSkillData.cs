using UnityEngine;

/// <summary>
/// 弾幕パターンの種類
/// </summary>
public enum SkillPatternType
{
    Standard,   // 単発
    nWay,       // 扇状
    Round,      // 全方位
    Polygon,    // 多角形
    Line,       // 直線連弾
    Custom      // 特殊用
}

[CreateAssetMenu(fileName = "NewPlayerSkillData", menuName = "Danmaku/PlayerSkillData")]
public class PlayerSkillData : ScriptableObject
{
    [System.Serializable]
    public struct SkillSettings
    {
        public string skillName;
        public SkillPatternType patternType;

        [Tooltip("使用する弾の設定アセット")]
        public BulletData bulletData;

        public float cooldown;               // 連射速度
        public string sePath;                // 効果音

        [Header("Pattern Parameters")]
        public int count;           // 弾数 / Way数 / 頂点数
        public float speed;         // 弾速
        public float angleOffset;   // 角度補正（正面(上)を0度としたズレ）
        public float wideAngle;     // 拡散範囲（Wideの時に使用）

        [Header("Effect Parameters")]
        [Tooltip("発射されるまでの待機フレーム数 (60 = 約1秒)")]
        public float delay;
    }

    [Header("Skill Definitions")]
    public SkillSettings skillZ;
    public SkillSettings skillX;
    public SkillSettings skillC;
    public SkillSettings skillV;
}