// --- BulletData.cs 修正版 ---
using UnityEngine;

public enum BulletSize { Large, Medium, Small }

[CreateAssetMenu(fileName = "NewBulletData", menuName = "Danmaku/BulletData")]
public class BulletData : ScriptableObject
{
    [Header("判定・種別設定")]
    public bool isLaser = false; // ★ レーザー判定を行うか
    public BulletManager.LaserColor laserColor; // ★ レーザーの色

    [Header("生成設定")]
    public GameObject bulletPrefab; // レーザーの場合は LaserBeamPrefab を指定

    [Header("サイズ・当たり判定")]
    public BulletSize sizeType;
    public float radius = 0.05f; // 弾の場合の半径
    public Vector2 colliderOffset = Vector2.zero;

    [Header("ダメージ設定")]
    public int damage = 10;

    [Header("ビジュアル")]
    public Sprite bulletSprite;
    public Sprite[] animationSprites;
    public float animationFPS = 10f;
    public Sprite delaySprite;
    public Color breakColor = Color.white;
    public Material material;
}