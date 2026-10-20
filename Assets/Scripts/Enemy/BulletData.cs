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
    // =========================================================================
    // 🌟【新規追加】：弾の回転設定
    // =========================================================================
    [Header("🌟 弾の回転設定")]
    [Tooltip("有効にすると、弾の進行方向（角度）に合わせてスプライトの向きが回転します。丸弾などの場合はオフにしてください")]
    public bool rotateSpriteWithAngle = true;

    [Header("ダメージ設定")]
    public int damage = 10;

    // =========================================================================
    // 🌟【新規追加】：射程距離・減速停止・透明化設定
    // =========================================================================
    [Header("🌟 射程・減速停止設定")]
    [Tooltip("有効にする場合、発射後一定フレームで減速して停止します")]
    public bool useRangeDeceleration = false;
    [Tooltip("発射後、何フレーム目から減速を開始するか（例: 30フレーム = 0.5秒後）")]
    public int decelerationStartFrame = 30;
    [Tooltip("減速を開始してから完全に停止するまでのフレーム数（例: 20フレーム）")]
    public int decelerationDurationFrames = 20;
    // =========================================================================
    // 🌟【新規追加】：接触継続ダメージ（削りダメージ）設定
    // =========================================================================
    [Header("🌟 接触継続ダメージ設定")]
    [Tooltip("有効にすると、命中時に被弾扱いにならず無敵時間を無視して継続的に体力を削ります")]
    public bool isContinuousDamageBullet = false;

    [Header("ビジュアル（本体用）")]
    public Sprite bulletSprite;
    public Sprite[] animationSprites;
    public float animationFPS = 10f;
    public Sprite delaySprite;
    public Color breakColor = Color.white;
    public Material material;

    [Header("🌟 発射源（光源）のサイズ設定")]
    [Tooltip("レーザーの根元に表示される光源エフェクトの大きさ倍率")]
    public float sourceEffectScale = 1.0f;

    [Tooltip("ここにオーラ用の加算合成マテリアル(Additive等)を直接アサインしてください")]
    public Material auraMaterial;

    [Header("🌟 オーラ用アセット（白単色・形状一致インフラ）")]
    [Tooltip("ここに、本体の形に対応した【白色（シルエット）の色違いスプライト】を1枚だけ登録してください")]
    public Sprite auraWhiteSprite;
}