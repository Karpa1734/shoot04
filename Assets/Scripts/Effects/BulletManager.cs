using UnityEngine;

public class BulletManager : MonoBehaviour
{
    public static BulletManager Instance;

    [System.Serializable]
    public struct LaserSet
    {
        public Sprite mainSprite;
        public Sprite previewSprite;
        public Sprite sourceEffectSprite; // ★追加：色ごとの弾源スプライト
    }
    public enum LaserColor { RED, ORANGE, YELLOW, GREEN, AQUA, BLUE, PURPLE, WHITE }

    [Header("Special Materials")]
    public Material additiveMaterial; // 加算合成マテリアル

    [Header("Special Prefabs")]
    public GameObject laserStreamHeadPrefab; // ストリーム用司令塔
    public GameObject laserBeamPrefab;       // 設置型（極太）レーザー用
    public GameObject laserSourceEffectPrefab; // ★追加：共通の弾源プレハブ(1つ)
    [Header("Laser Visuals (8 Colors / 16 Sprites)")]
    public LaserSet redLaser;
    public LaserSet orangeLaser;
    public LaserSet yellowLaser;
    public LaserSet greenLaser;
    public LaserSet aquaLaser;
    public LaserSet blueLaser;
    public LaserSet purpleLaser;
    public LaserSet whiteLaser;

    [Header("Bullet Arrays")]
    public BulletData[] RED;
    public BulletData[] ORANGE;
    public BulletData[] YELLOW;
    public BulletData[] GREEN;
    public BulletData[] AQUA;
    public BulletData[] BLUE;
    public BulletData[] PURPLE;
    public BulletData[] WHITE;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public LaserSet GetLaserSet(LaserColor color)
    {
        switch (color)
        {
            case LaserColor.RED: return redLaser;
            case LaserColor.ORANGE: return orangeLaser;
            case LaserColor.YELLOW: return yellowLaser;
            case LaserColor.GREEN: return greenLaser;
            case LaserColor.AQUA: return aquaLaser;
            case LaserColor.BLUE: return blueLaser;
            case LaserColor.PURPLE: return purpleLaser;
            case LaserColor.WHITE: return whiteLaser;
            default: return whiteLaser;
        }
    }
}