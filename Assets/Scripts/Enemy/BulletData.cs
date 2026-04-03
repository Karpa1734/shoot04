using UnityEngine;

// 弾のサイズ定義
public enum BulletSize { Large, Medium, Small }

[CreateAssetMenu(fileName = "NewBulletData", menuName = "Danmaku/BulletData")]
public class BulletData : ScriptableObject
{
    [Header("生成設定")]
    // --- 追加箇所：このデータが使用するベースとなる弾のプレハブ ---
    public GameObject bulletPrefab;

    [Header("サイズ設定")]
    public BulletSize sizeType; // 大・中・小を選択

    [Header("弾本体の設定")]
    public Sprite bulletSprite;
    public float radius = 0.05f;

    [Header("エフェクト設定")]
    public Sprite delaySprite;
    public Color breakColor = Color.white;
    public Material material;
}