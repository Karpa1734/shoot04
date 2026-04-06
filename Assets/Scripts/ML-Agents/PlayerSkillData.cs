using UnityEngine;

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

        // 弾の設定アセットを参照（ここを修正しました）
        public BulletData bulletData;

        public float cooldown;               // 連射速度
        public string sePath;                // 効果音

        [Header("Pattern Parameters")]
        public int count;           // 弾数 / Way数 / 頂点数
        public float speed;         // 弾速
        public float angleOffset;   // 角度補正（正面を0度としたズレ）
        public float wideAngle;     // 拡散範囲（Wideの時に使用）
    }

    public SkillSettings skillZ;
    public SkillSettings skillX;
    public SkillSettings skillC;
    public SkillSettings skillV;
}