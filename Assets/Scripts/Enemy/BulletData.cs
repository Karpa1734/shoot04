// --- BulletData.cs 修正版 ---
using UnityEngine;

public enum BulletSize { Large, Medium, Small }

[CreateAssetMenu(fileName = "NewBulletData", menuName = "Danmaku/BulletData")]
public class BulletData : ScriptableObject
{
    [Header("生成設定")]
    public GameObject bulletPrefab;

    [Header("サイズ設定")]
    public BulletSize sizeType;

    [Header("弾本体の設定（静止画）")]
    public Sprite bulletSprite;

    [Header("アニメーション設定（複数枚ある場合）")]
    // ★ 追加：アニメーション用のスプライト配列
    public Sprite[] animationSprites;
    // ★ 追加：1秒間に何枚進めるか
    public float animationFPS = 10f;

    public float radius = 0.05f;
    // ★ 追加：コライダーのオフセット（X, Yのズレ）を設定できるようにする
    public Vector2 colliderOffset = Vector2.zero;
    [Header("ダメージ設定")]
    public int damage = 10;
    [Header("エフェクト設定")]
    public Sprite delaySprite;
    public Color breakColor = Color.white;
    public Material material;
}