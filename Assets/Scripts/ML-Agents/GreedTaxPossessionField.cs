// --- GreedTaxPossessionField.cs 最終開通版（自機レーザー保護＆魔方陣回転実装） ---
using UnityEngine;

public class GreedTaxPossessionField : MonoBehaviour
{
    [Header("Greed Counter Settings")]
    [SerializeField] private BulletData _knifeBulletData; //
    [SerializeField] private float _knifeShootSpeed = 4.5f; //
    [SerializeField] private float _knifeDelayDuration = 0.5f; //
    [SerializeField] private float _knifeScaleMultiplier = 1.0f; //

    [Header("Field Settings")]
    [SerializeField] private float _duration = 1.5f; //
    [SerializeField] private float _energyGainPerBullet = 1.5f; //

    // =========================================================================
    // 🎨【新設：魔方陣回転エフェクト設定】
    // =========================================================================
    [Header("🌟 Visual Settings")]
    [Tooltip("魔方陣の1秒間あたりの回転速度（度数）。マイナス値で逆回転")]
    [SerializeField] private float _fieldRotationSpeed = 45f; // 💡 1秒間に45度回転（程よい高級感のある速度）

    private Transform _playerTransform; //
    private GameObject _owner; //
    private PlayerDanmakuEmitter _ownerEmitter; //
    private PlayerMove _playerMove; //

    private string _targetBulletTag; //
    private string _targetTag; //
    private float _timer = 0f; //
    private bool _isInitialized = false; //
    private Vector3 _originalFieldScale; //

    void Awake()
    {
        _originalFieldScale = transform.localScale; //
        if (_originalFieldScale == Vector3.zero) _originalFieldScale = Vector3.one; //
    }

    public void Initialize(Transform playerTransform, GameObject shooter, string targetTag, PlayerDanmakuEmitter emitter,
                           float overrideDuration = -1f, float overrideScaleMult = -1f, float overrideKnifeSpeed = -1f, float overrideEnergyGain = -1f)
    {
        this._playerTransform = playerTransform; //
        this._owner = shooter; //
        this._targetTag = targetTag; //
        this._ownerEmitter = emitter; //
        this._playerMove = shooter.GetComponent<PlayerMove>(); //

        var myStatus = shooter.GetComponentInParent<PlayerStatusManager>(); //
        int id = (myStatus != null) ? myStatus.playerId : 1; //
        this._targetBulletTag = (id == 1) ? "EnemyBullet" : "PlayerBullet"; //

        // 設置型にするため、初期化時のプレイヤー座標を自身の位置として完全に固定する
        transform.position = playerTransform.position; //

        // 外部からの領域変調命令のオーバーライド適用
        if (overrideDuration > 0f) _duration = overrideDuration;
        if (overrideScaleMult > 0f) _originalFieldScale *= overrideScaleMult;
        if (overrideKnifeSpeed > 0f) _knifeShootSpeed = overrideKnifeSpeed;
        if (overrideEnergyGain > 0f) _energyGainPerBullet = overrideEnergyGain;

        this._isInitialized = true; //
        this._timer = 0f; //
        transform.localScale = Vector3.zero; //
    }

    void FixedUpdate()
    {
        if (!_isInitialized) return; //

        if (!PlayerMove.CanShoot) //
        {
            Destroy(gameObject); //
            return; //
        }

        _timer += Time.fixedDeltaTime; //

        // =========================================================================
        // 🎯【新設：魔方陣定速回転スレッド】
        // =========================================================================
        // 💡 設置された座標を中心軸として、毎フレーム等速でぐるぐる綺麗に回転させます！
        transform.Rotate(0f, 0f, _fieldRotationSpeed * Time.fixedDeltaTime);

        // 展開・縮小演出（固定された座標基準で綺麗に拡縮します）
        if (_timer < 0.1f) //
        {
            transform.localScale = Vector3.Lerp(Vector3.zero, _originalFieldScale, _timer / 0.1f); //
        }
        else if (_timer >= _duration) //
        {
            float t = (_timer - _duration) / 0.1f; //
            transform.localScale = Vector3.Lerp(_originalFieldScale, Vector3.zero, t); //
            if (_timer >= _duration + 0.1f) Destroy(gameObject); //
        }
        else //
        {
            transform.localScale = _originalFieldScale; //
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!_isInitialized || string.IsNullOrEmpty(_targetBulletTag)) return;

        // =========================================================================
        // 🎯 1. 敵の通常弾幕（Bullet）を検知してカウンターナイフを生成・射出する処理
        // =========================================================================
        if (collision.CompareTag(_targetBulletTag))
        {
            DanmakuBullet enemyBullet = collision.GetComponent<DanmakuBullet>();
            if (enemyBullet != null)
            {
                // ⭕ 修正の核心：消えない弾幕（不滅フラグ）が立っている場合は、100%完全スルーして形状を維持！
                if (enemyBullet.isIndestructible) return;
                Vector3 spawnPos = collision.transform.position;

                // 敵弾を画面からパージしてプールへ回収（システム回収命令は force: true）
                enemyBullet.Deactivate(false, force: true);

                if (_playerMove != null) _playerMove.AddUltimateEnergy(_energyGainPerBullet);

                if (_knifeBulletData != null && _knifeBulletData.bulletPrefab != null)
                {
                    // 📊 攻撃ランクの乗算計算
                    float atkMultiplier = 1.0f;
                    PlayerStatusManager myStatus = _owner != null ? _owner.GetComponent<PlayerStatusManager>() : null;
                    if (myStatus == null && _owner != null) myStatus = _owner.GetComponentInParent<PlayerStatusManager>();

                    if (myStatus != null && myStatus.characterData != null)
                    {
                        switch (myStatus.characterData.rankAttack)
                        {
                            case StatusRank.E: atkMultiplier = 0.6f; break;
                            case StatusRank.D: atkMultiplier = 0.8f; break;
                            case StatusRank.C: atkMultiplier = 1.0f; break;
                            case StatusRank.B: atkMultiplier = 1.2f; break;
                            case StatusRank.A: atkMultiplier = 1.4f; break;
                            case StatusRank.EX: atkMultiplier = 1.6f; break;
                        }
                    }

                    // -----------------------------------------------------------------
                    // ⭕ プール上限1000発ロック対応＆キーの絶対元本一本化インフラ
                    // -----------------------------------------------------------------
                    GameObject knifeObj = null;

                    // 🎯【最核心修正】：Instantiateを完全破棄！大元のプレハブ元本キーでプールからGetします。
                    if (BulletPool.Instance != null)
                    {
                        knifeObj = BulletPool.Instance.Get(_knifeBulletData.bulletPrefab, spawnPos, Quaternion.identity);
                    }
                    else
                    {
                        knifeObj = Instantiate(_knifeBulletData.bulletPrefab, spawnPos, Quaternion.identity);
                    }

                    // 💡 セーフティ：総数が1000発に達してプールが新規生成を拒絶(Null)した場合は安全にスルーします
                    if (knifeObj != null)
                    {
                        // チーム所属に応じたタグとレイヤーの厳密なパッシング同期
                        knifeObj.tag = gameObject.tag;
                        knifeObj.layer = gameObject.layer;

                        // 子オブジェクト（コライダー等）のレイヤーも再帰的に同期
                        SetLayerRecursiveInfrastrucure(knifeObj, gameObject.layer);

                        // アセットを汚さないためにランタイムクローンを作成し、ダメージ計算を反映
                        BulletData runtimeKnifeData = Instantiate(_knifeBulletData);
                        runtimeKnifeData.damage = Mathf.RoundToInt(runtimeKnifeData.damage * atkMultiplier);

                        DanmakuBullet bulletLogic = knifeObj.GetComponent<DanmakuBullet>();
                        if (bulletLogic != null)
                        {
                            // 拡張された runtimeKnifeData（originPrefabが記録される側）を安全にインジェクション
                            bulletLogic.InitializeKnifeCounter(_owner, _targetTag, _knifeShootSpeed, _knifeDelayDuration, runtimeKnifeData);
                        }

                        // 元本のプレハブスケールに基づき、カスタムサイズを算出適用
                        knifeObj.transform.localScale = _knifeBulletData.bulletPrefab.transform.localScale * _knifeScaleMultiplier;
                    }
                }
            }
        }

        // =========================================================================
        // 🎯 2. 自機レーザーの絶対保護 ＆ 敵レーザーのみの狙撃・吸収判定
        // =========================================================================
        if (collision.CompareTag("Laser"))
        {
            EnemyLaserBeam laser = collision.GetComponent<EnemyLaserBeam>();
            if (laser != null)
            {
                // 💡 核心チェック：同じレイヤー（味方チーム）のレーザーであれば消去を100%完全スキップ
                if (collision.gameObject.layer == gameObject.layer)
                {
                    return;
                }

                // 敵のレーザーのみを強制遮断・吸収
                laser.ForceClose();
                if (_playerMove != null) _playerMove.AddUltimateEnergy(_energyGainPerBullet * 4f);
            }
        }
    }

    /// <summary>
    /// 🛡️ 生成された子オブジェクトすべてのレイヤーを安全にチーム用レイヤーへ上書きするヘルパー関数
    /// </summary>
    private void SetLayerRecursiveInfrastrucure(GameObject obj, int layer)
    {
        if (obj == null) return;
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursiveInfrastrucure(child.gameObject, layer);
        }
    }
}