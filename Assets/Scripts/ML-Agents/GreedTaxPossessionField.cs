// --- GreedTaxPossessionField.cs 修正版 ---
using UnityEngine;

public class GreedTaxPossessionField : MonoBehaviour
{
    [Header("Greed Counter Settings")]
    [SerializeField] private BulletData _knifeBulletData;
    [SerializeField] private float _knifeShootSpeed = 4.5f;
    [SerializeField] private float _knifeDelayDuration = 0.5f;
    [SerializeField] private float _knifeScaleMultiplier = 1.0f;

    [Header("Field Settings")]
    [SerializeField] private float _duration = 1.5f;
    [SerializeField] private float _energyGainPerBullet = 1.5f;

    private Transform _playerTransform;
    private GameObject _owner;
    private PlayerDanmakuEmitter _ownerEmitter;
    private PlayerMove _playerMove;

    private string _targetBulletTag;
    private string _targetTag;
    private float _timer = 0f;
    private bool _isInitialized = false;
    private Vector3 _originalFieldScale;

    void Awake()
    {
        _originalFieldScale = transform.localScale;
        if (_originalFieldScale == Vector3.zero) _originalFieldScale = Vector3.one;
    }

    public void Initialize(Transform playerTransform, GameObject shooter, string targetTag, PlayerDanmakuEmitter emitter)
    {
        this._playerTransform = playerTransform;
        this._owner = shooter;
        this._targetTag = targetTag;
        this._ownerEmitter = emitter;
        this._playerMove = shooter.GetComponent<PlayerMove>();

        var myStatus = shooter.GetComponentInParent<PlayerStatusManager>();
        int id = (myStatus != null) ? myStatus.playerId : 1;
        this._targetBulletTag = (id == 1) ? "EnemyBullet" : "PlayerBullet";

        // ★ 設置型にするため、初期化時のプレイヤー座標を自身の位置として完全に固定する
        transform.position = playerTransform.position;

        this._isInitialized = true;
        this._timer = 0f;
        transform.localScale = Vector3.zero;
    }

    void FixedUpdate()
    {
        if (!_isInitialized) return;

        if (!PlayerMove.CanShoot)
        {
            Destroy(gameObject);
            return;
        }

        // ★ 変更点：毎フレームの自機への位置追従処理（transform.position = _playerTransform.position;）を削除
        // これにより、出現した座標に完全に固定（静的配置）されます。

        _timer += Time.fixedDeltaTime;

        // 展開・縮小演出（固定された座標基準で綺麗に拡縮します）
        if (_timer < 0.1f)
        {
            transform.localScale = Vector3.Lerp(Vector3.zero, _originalFieldScale, _timer / 0.1f);
        }
        else if (_timer >= _duration)
        {
            float t = (_timer - _duration) / 0.1f;
            transform.localScale = Vector3.Lerp(_originalFieldScale, Vector3.zero, t);
            if (_timer >= _duration + 0.1f) Destroy(gameObject);
        }
        else
        {
            transform.localScale = _originalFieldScale;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!_isInitialized || string.IsNullOrEmpty(_targetBulletTag)) return;

        // 敵の通常弾を検知
        if (collision.CompareTag(_targetBulletTag))
        {
            DanmakuBullet enemyBullet = collision.GetComponent<DanmakuBullet>();
            if (enemyBullet != null)
            {
                Vector3 spawnPos = collision.transform.position;
                enemyBullet.Deactivate(false);

                if (_playerMove != null) _playerMove.AddUltimateEnergy(_energyGainPerBullet);

                if (_knifeBulletData != null && _knifeBulletData.bulletPrefab != null)
                {
                    GameObject knifeObj = Instantiate(_knifeBulletData.bulletPrefab, spawnPos, Quaternion.identity);

                    knifeObj.tag = gameObject.tag;
                    knifeObj.layer = gameObject.layer;
                    foreach (Transform child in knifeObj.GetComponentsInChildren<Transform>())
                        child.gameObject.layer = gameObject.layer;

                    DanmakuBullet bulletLogic = knifeObj.GetComponent<DanmakuBullet>();
                    if (bulletLogic != null)
                    {
                        bulletLogic.InitializeKnifeCounter(_owner, _targetTag, _knifeShootSpeed, _knifeDelayDuration, _knifeBulletData);
                    }

                    knifeObj.transform.localScale = _knifeBulletData.bulletPrefab.transform.localScale * _knifeScaleMultiplier;
                }
            }
        }

        // 敵のレーザーを検知
        if (collision.CompareTag("Laser"))
        {
            EnemyLaserBeam laser = collision.GetComponent<EnemyLaserBeam>();
            if (laser != null)
            {
                laser.ForceClose(); //
                if (_playerMove != null) _playerMove.AddUltimateEnergy(_energyGainPerBullet * 4f);
            }
        }
    }
}