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

    private void OnTriggerEnter2D(Collider2D collision) //
    {
        if (!_isInitialized || string.IsNullOrEmpty(_targetBulletTag)) return; //

        if (collision.CompareTag(_targetBulletTag)) //
        {
            DanmakuBullet enemyBullet = collision.GetComponent<DanmakuBullet>(); //
            if (enemyBullet != null) //
            {
                Vector3 spawnPos = collision.transform.position; //
                enemyBullet.Deactivate(false); //

                if (_playerMove != null) _playerMove.AddUltimateEnergy(_energyGainPerBullet); //

                if (_knifeBulletData != null && _knifeBulletData.bulletPrefab != null) //
                {
                    // =========================================================================
                    // 🎯【強欲カウンター攻撃ランク変調】：アセットを汚さないランタイムクローン
                    // =========================================================================
                    BulletData runtimeKnifeData = Instantiate(_knifeBulletData);

                    PlayerStatusManager myStatus = _owner.GetComponent<PlayerStatusManager>();
                    if (myStatus == null) myStatus = _owner.GetComponentInParent<PlayerStatusManager>();

                    if (myStatus != null && myStatus.characterData != null)
                    {
                        float atkMultiplier = 1.0f;
                        switch (myStatus.characterData.rankAttack)
                        {
                            case StatusRank.E: atkMultiplier = 0.6f; break;
                            case StatusRank.D: atkMultiplier = 0.8f; break;
                            case StatusRank.C: atkMultiplier = 1.0f; break;
                            case StatusRank.B: atkMultiplier = 1.2f; break;
                            case StatusRank.A: atkMultiplier = 1.4f; break;
                            case StatusRank.EX: atkMultiplier = 1.6f; break;
                        }
                        runtimeKnifeData.damage = Mathf.RoundToInt(runtimeKnifeData.damage * atkMultiplier);
                    }

                    // 複製されたデータ基準でプレハブを実体化
                    GameObject knifeObj = Instantiate(runtimeKnifeData.bulletPrefab, spawnPos, Quaternion.identity);

                    knifeObj.tag = gameObject.tag; //
                    knifeObj.layer = gameObject.layer; //
                    foreach (Transform child in knifeObj.GetComponentsInChildren<Transform>()) //
                        child.gameObject.layer = gameObject.layer; //

                    DanmakuBullet bulletLogic = knifeObj.GetComponent<DanmakuBullet>(); //
                    if (bulletLogic != null) //
                    {
                        // 拡張された runtimeKnifeData を安全に結合！
                        bulletLogic.InitializeKnifeCounter(_owner, _targetTag, _knifeShootSpeed, _knifeDelayDuration, runtimeKnifeData); //
                    }

                    knifeObj.transform.localScale = runtimeKnifeData.bulletPrefab.transform.localScale * _knifeScaleMultiplier; //
                }
            }
        }

        // =========================================================================
        // 🎯【修正：自機レーザーの絶対保護 ＆ 敵レーザーのみの狙撃判定】
        // =========================================================================
        if (collision.CompareTag("Laser")) //
        {
            EnemyLaserBeam laser = collision.GetComponent<EnemyLaserBeam>(); //
            if (laser != null) //
            {
                // 💡 核心チェック：レーザーオブジェクトの「レイヤー（所属）」をスキャン！
                // 💡 もしレーザーの所属レイヤーが「自分自身（このシールドオブジェクト）の属する弾レイヤー」と
                // 💡 【一致している場合】は、自分が撃ったレーザーなので消去を100%完全スキップ（保護）します！
                if (collision.gameObject.layer == gameObject.layer)
                {
                    return;
                }

                // 💡 所属レイヤーが異なっている（＝敵が撃ったレーザー）場合のみ、ForceCloseを執行して吸収！
                laser.ForceClose(); //
                if (_playerMove != null) _playerMove.AddUltimateEnergy(_energyGainPerBullet * 4f); //
            }
        }



    }
}