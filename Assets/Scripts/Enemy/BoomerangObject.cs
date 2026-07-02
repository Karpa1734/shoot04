using UnityEngine;
using System.Collections;

/// <summary>
/// 速さベースで移動するブーメラン型ビット。
/// 出現・消滅時の拡縮（速度0）、移動中の残像、目的地での1秒ステイ、ラウンド終了時の自動消滅を実装。
/// </summary>
[RequireComponent(typeof(SpriteRenderer), typeof(Collider2D), typeof(Rigidbody2D))]
public class BoomerangObject : MonoBehaviour
{
    private Transform _owner;
    private Vector3 _fixedTargetPos; // 発射時の敵座標（目的地）
    private BulletData _subBulletData;
    private PlayerDanmakuEmitter _ownerEmitter;

    private SpriteRenderer _sr;
    private float _speed;
    private bool _isSpellCardEnhanced = false;

    [Header("Behavior Settings")]
    [SerializeField] private float _stayDuration = 1.0f;    // 目的地での待機時間
    [SerializeField] private float _scaleDuration = 0.3f;   // 出現・消滅の拡縮にかかる時間
    [SerializeField] private float _rotationSpeed = 720f;   // 1秒間の回転角度

    [Header("⚡ Continuous Hit Settings (多段連続ヒット)")]
    [Tooltip("敵プレイヤーと重なり続けている間、何秒ごとに連続ダメージを発生させるか")]
    [SerializeField] private float _hitInterval = 0.3f;     // 💡 0.3秒間隔でガガガッとスリップダメージ
    private float _nextHitEnableTime = 0f;                  // 次にヒット可能になる絶対時刻

    [Header("Afterimage Settings")]
    [SerializeField] private bool _enableAfterimage = true;     // 残像を有効にするか
    [SerializeField] private float _afterimageInterval = 0.05f; // 残像を生成する間隔（秒）
    [SerializeField] private float _afterimageFadeTime = 0.3f;  // 残像が消えるまでの時間
    [SerializeField] private float _afterimageStartAlpha = 0.5f; // 残像の初期透明度
    private float _afterimageTimer = 0f;

    [Header("Sub Bullet Settings")]
    [SerializeField] private BulletData _subDanmakuData;
    [SerializeField] private Transform _muzzleTransform;
    [SerializeField] private float _subBulletMaxSpeed = 5f;
    [SerializeField] private float _subBulletAccel = 0.1f;

    /// <summary>
    /// 初期化。PlayerDanmakuEmitterから呼ばれる。
    /// </summary>
    public void Initialize(Transform owner, Transform target, BulletData data, float speed, PlayerDanmakuEmitter emitter)
    {
        _owner = owner; //
        _subBulletData = data; //
        _speed = speed; //
        _ownerEmitter = emitter; //
        _sr = GetComponent<SpriteRenderer>(); //

        _nextHitEnableTime = 0f; // タイマー初期化

        if (emitter != null) //
        {
            PlayerStatusManager ownerStatus = emitter.GetComponentInParent<PlayerStatusManager>(); //
            _isSpellCardEnhanced = (ownerStatus != null && ownerStatus.isSpellCardActive); //

            if (_isSpellCardEnhanced) //
            {
                Debug.Log("<color=gold>🔮【領域展開・ビット同調】ブーメランビット：子弾発射インターバルを4フレームへ半減（連射速度2倍）！</color>"); //
            }
        }

        transform.localScale = Vector3.zero; //

        if (target != null) //
        {
            _fixedTargetPos = target.position; //
        }
        else //
        {
            _fixedTargetPos = transform.position + transform.right * 5f; //
        }

        var rb = GetComponent<Rigidbody2D>(); //
        rb.bodyType = RigidbodyType2D.Kinematic; //
        GetComponent<Collider2D>().isTrigger = true; //

        StartCoroutine(BoomerangRoutine()); //
    }

    private void Update()
    {
        transform.Rotate(0, 0, _rotationSpeed * Time.deltaTime); //
    }

    private IEnumerator BoomerangRoutine() //
    {
        yield return StartCoroutine(ScaleRoutine(0, 1)); //

        int fireFrameInterval = _isSpellCardEnhanced ? 2 : 5; //
        Vector3 startPos = transform.position; //
        float distanceToTarget = Vector3.Distance(startPos, _fixedTargetPos); //

        float moveTimeForward = (distanceToTarget > 0) ? distanceToTarget / _speed : 0.1f; //
        float elapsed = 0; //

        while (elapsed < moveTimeForward)
        {
            while (Mathf.Approximately(Time.timeScale, 0f)) yield return null; //
            if (!PlayerMove.CanShoot) break; //

            elapsed += Time.deltaTime; //
            float t = Mathf.Clamp01(elapsed / moveTimeForward); //
            transform.position = Vector3.Lerp(startPos, _fixedTargetPos, Mathf.SmoothStep(0, 1, t)); //

            UpdateAfterimage(); //

            if (Time.frameCount % fireFrameInterval == 0) FireSubDanmaku(); //
            yield return null; //
        }

        if (PlayerMove.CanShoot) //
        {
            float stayElapsed = 0; //
            while (stayElapsed < _stayDuration) //
            {
                while (Mathf.Approximately(Time.timeScale, 0f)) yield return null; //
                if (!PlayerMove.CanShoot) break; //

                UpdateAfterimage(); //
                stayElapsed += Time.deltaTime; //

                if (Time.frameCount % fireFrameInterval == 0) FireSubDanmaku(); //
                yield return null; //
            }
        }

        if (PlayerMove.CanShoot) //
        {
            elapsed = 0; //
            Vector3 peakPos = transform.position; //

            float distanceToOwner = Vector3.Distance(peakPos, _owner != null ? _owner.position : peakPos); //
            float moveTimeReturn = (distanceToOwner > 0) ? distanceToOwner / _speed : 0.1f; //

            while (elapsed < moveTimeReturn)
            {
                while (Mathf.Approximately(Time.timeScale, 0f)) yield return null; //
                if (!PlayerMove.CanShoot) break; //

                elapsed += Time.deltaTime; //
                float t = Mathf.Clamp01(elapsed / moveTimeReturn); //

                if (_owner != null) //
                {
                    transform.position = Vector3.Lerp(peakPos, _owner.position, Mathf.SmoothStep(0, 1, t)); //
                }

                UpdateAfterimage(); //

                if (Time.frameCount % fireFrameInterval == 0) FireSubDanmaku(); //
                yield return null; //
            }
        }

        while (Mathf.Approximately(Time.timeScale, 0f)) yield return null; //
        yield return StartCoroutine(ScaleRoutine(1, 0)); //
        Destroy(gameObject); //
    }

    private IEnumerator ScaleRoutine(float start, float end) //
    {
        float elapsed = 0; //
        while (elapsed < _scaleDuration) //
        {
            elapsed += Time.deltaTime; //
            float t = elapsed / _scaleDuration; //
            transform.localScale = Vector3.one * Mathf.SmoothStep(start, end, t); //
            yield return null; //
        }
        transform.localScale = Vector3.one * end; //
    }

    private void UpdateAfterimage() //
    {
        if (!_enableAfterimage || _sr == null) return; //
        _afterimageTimer += Time.deltaTime; //
        if (_afterimageTimer >= _afterimageInterval) //
        {
            _afterimageTimer = 0; //
            SpawnAfterimage(); //
        }
    }

    private void SpawnAfterimage() //
    {
        GameObject go = new GameObject("BoomerangAfterimage"); //
        go.transform.SetPositionAndRotation(transform.position, transform.rotation); //
        go.transform.localScale = transform.localScale; //

        SpriteRenderer trailSR = go.AddComponent<SpriteRenderer>(); //
        trailSR.sprite = _sr.sprite; //
        trailSR.color = _sr.color; //
        trailSR.sortingOrder = _sr.sortingOrder - 1; //

        BoomerangAfterimage fader = go.AddComponent<BoomerangAfterimage>(); //
        fader.Setup(_afterimageFadeTime, _afterimageStartAlpha); //
    }

    private void FireSubDanmaku()
    {
        BulletData dataToUse = (_subDanmakuData != null) ? _subDanmakuData : _subBulletData;
        if (_ownerEmitter == null || dataToUse == null) return;

        Vector3 firePos = (_muzzleTransform != null) ? _muzzleTransform.position : transform.position;

        int fireway = 1;
        float baseAngle = transform.eulerAngles.z;
        float angleStep = 360f / fireway;

        for (int i = 0; i < fireway; i++)
        {
            // ⭕ 修正の核心：すべての引数に「名前（引数名:）」を完全明示バインド！
            //    Emitter側の定義 (data, pos, speed, angle, accel, maxSpeed, tag, layer) とのズレを物理的に破壊し、
            //    プールから引き出された子弾幕にも100%確実にオーラを溶接させます。
            _ownerEmitter.ExecuteSubShot(
                data: dataToUse,
                pos: firePos,
                speed: 0f,
                angle: baseAngle + (i * angleStep),
                accel: _subBulletAccel,
                maxSpeed: _subBulletMaxSpeed,
                tag: gameObject.tag,
                layer: gameObject.layer
            );
        }
    }

    // =========================================================================
    // 🎯【核心修正】：進入(Enter)と滞在(Stay)の双方から共通の衝突ロジックをキック
    // =========================================================================
    private void OnTriggerEnter2D(Collider2D collision)
    {
        HandleBoomerangCollision(collision);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        HandleBoomerangCollision(collision);
    }

    /// <summary>
    /// ブーメラン本体の多段ヒット・ダメージ処理制御
    /// </summary>
    private void HandleBoomerangCollision(Collider2D collision)
    {
        if (_owner == null) return; //

        // 生成・消滅時の完全極小スケール中は、当たり判定を安全にミュートして暴発防止
        if (transform.localScale.x < 0.1f) return;

        // ⚔️ 敵プレイヤーと接触、かつ内部リキャストタイマー（0.3秒）を超えていれば連続OnHit！
        if (collision.CompareTag("Player") && collision.gameObject != _owner.gameObject) //
        {
            if (Time.time >= _nextHitEnableTime)
            {
                _nextHitEnableTime = Time.time + _hitInterval; // 次回多段ヒット可能時刻を更新
                collision.SendMessage("OnHit", 20, SendMessageOptions.DontRequireReceiver); //
            }
        }
    }

    private class BoomerangAfterimage : MonoBehaviour //
    {
        private SpriteRenderer _sr; //
        private float _fadeDuration; //
        private float _startAlpha; //
        private float _elapsed = 0; //

        public void Setup(float duration, float startAlpha) //
        {
            _fadeDuration = duration; //
            _startAlpha = startAlpha; //
            _sr = GetComponent<SpriteRenderer>(); //
            SetAlpha(_sr, _startAlpha); //
        }

        private void Update() //
        {
            if (_sr == null) return; //
            _elapsed += Time.deltaTime; //
            float t = _elapsed / _fadeDuration; //
            SetAlpha(_sr, Mathf.Lerp(_startAlpha, 0f, t)); //
            if (_elapsed >= _fadeDuration) Destroy(gameObject); //
        }

        private void SetAlpha(SpriteRenderer sr, float alpha) //
        {
            Color c = sr.color; //
            c.a = alpha; //
            sr.color = c; //
        }
    }
}