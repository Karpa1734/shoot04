// --- BulletData.cs 白単色オーラインフラ版 ---
using UnityEngine;

public enum BulletSize { Large, Medium, Small }

[CreateAssetMenu(fileName = "NewBulletData", menuName = "Danmaku/BulletData")]
public class BulletData : ScriptableObject
{
    [Header("判定・種別設定")]
    public bool isLaser = false;
    public BulletManager.LaserColor laserColor;

    [Header("生成設定")]
    public GameObject bulletPrefab;

    [Header("サイズ・当たり判定")]
    public BulletSize sizeType;
    public float bulletScale = 1.0f;
    public float radius = 0.05f;
    public Vector2 colliderOffset = Vector2.zero;

    [Header("ダメージ設定")]
    public int damage = 10;

    [Header("ビジュアル（本体用）")]
    public Sprite bulletSprite;
    public Sprite[] animationSprites;
    public float animationFPS = 10f;
    public Sprite delaySprite;
    public Color breakColor = Color.white;
    public Material material;
    // 🌟【新設】：Resources.Loadを永久パージするための、オーラ加算マテリアル静的バインド枠
    [Tooltip("ここにオーラ用の加算合成マテリアル(Additive等)を直接アサインしてください")]
    public Material auraMaterial;
    // =========================================================================
    // 🎨【新設：白単色オーラスプライト枠】
    // =========================================================================
    [Header("🌟 オーラ用アセット（白単色・形状一致インフラ）")]
    [Tooltip("ここに、本体の形に対応した【白色（シルエット）の色違いスプライト】を1枚だけ登録してください")]
    public Sprite auraWhiteSprite;
}