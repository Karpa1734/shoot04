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
    private float _speed; // ★ 修正：速さ

    [Header("Behavior Settings")]
    [SerializeField] private float _stayDuration = 1.0f;    // 目的地での待機時間
    [SerializeField] private float _scaleDuration = 0.3f;   // 出現・消滅の拡縮にかかる時間
    [SerializeField] private float _rotationSpeed = 720f;   // 1秒間の回転角度

    [Header("Afterimage Settings")]
    [SerializeField] private bool _enableAfterimage = true;     // 残像を有効にするか
    [SerializeField] private float _afterimageInterval = 0.05f; // 残像を生成する間隔（秒）
    [SerializeField] private float _afterimageFadeTime = 0.3f;  // 残像が消えるまでの時間
    [SerializeField] private float _afterimageStartAlpha = 0.5f; // 残像の初期透明度
    private float _afterimageTimer = 0f;

    [Header("Sub Bullet Settings")]
    [SerializeField] private BulletData _subDanmakuData;
    [SerializeField] private Transform _muzzleTransform;
    [SerializeField] private float _subBulletMaxSpeed = 5f; // ★ 目標とする最高速度
    [SerializeField] private float _subBulletAccel = 0.1f;    // ★ 1フレームあたりの加速度
    [SerializeField] private int _subBulletCount = 8;
    /// <summary>
    /// 初期化。PlayerDanmakuEmitterから呼ばれる。
    /// </summary>
    public void Initialize(Transform owner, Transform target, BulletData data, float speed, PlayerDanmakuEmitter emitter)
    {
        _owner = owner;
        _subBulletData = data;
        _speed = speed; // ★ 速さを代入
        _ownerEmitter = emitter;
        _sr = GetComponent<SpriteRenderer>();

        // 生成時は無（LocalScale 0）
        transform.localScale = Vector3.zero;

        if (target != null)
        {
            _fixedTargetPos = target.position;
        }
        else
        {
            _fixedTargetPos = transform.position + transform.right * 5f;
        }

        var rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        GetComponent<Collider2D>().isTrigger = true;

        StartCoroutine(BoomerangRoutine());
    }

    private void Update()
    {
        // 常に高速回転
        transform.Rotate(0, 0, _rotationSpeed * Time.deltaTime);
    }

    private IEnumerator BoomerangRoutine()
    {
        // --- A. 出現演出：無から拡大（その場に停止） ---
        yield return StartCoroutine(ScaleRoutine(0, 1));

        // --- B. 往路（目的地へ速さベースで移動） ---
        Vector3 startPos = transform.position;
        float distanceToTarget = Vector3.Distance(startPos, _fixedTargetPos);

        // 距離と速さから移動にかかる時間を計算
        float moveTimeForward = (distanceToTarget > 0) ? distanceToTarget / _speed : 0.1f;
        float elapsed = 0;

        while (elapsed < moveTimeForward)
        {
            if (!PlayerMove.CanShoot) break; // ラウンド終了時は移動中断

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / moveTimeForward);
            // 滑らかな加減速を適用しつつ、ターゲットへ移動
            transform.position = Vector3.Lerp(startPos, _fixedTargetPos, Mathf.SmoothStep(0, 1, t));

            UpdateAfterimage();
            if (Time.frameCount % 2 == 0) FireSubDanmaku();
            yield return null;
        }

        // --- C. 目的地ステイ（1秒間停止） ---
        if (PlayerMove.CanShoot)
        {
            float stayElapsed = 0;
            while (stayElapsed < _stayDuration)
            {
                if (!PlayerMove.CanShoot) break; // ラウンド終了時はステイ中断
                UpdateAfterimage();
                stayElapsed += Time.deltaTime;
                if (Time.frameCount % 2 == 0) FireSubDanmaku();
                yield return null;
            }
        }

        // --- D. 復路（動いている自機に戻る） ---
        if (PlayerMove.CanShoot)
        {
            elapsed = 0;
            // 復路開始時の位置を記憶
            Vector3 peakPos = transform.position;

            // 常に自機を追いかけるため、毎フレーム距離から所要時間を計算し直すのではなく、
            // 開始時の距離からベースとなる移動時間を決定します。
            float distanceToOwner = Vector3.Distance(peakPos, _owner != null ? _owner.position : peakPos);
            float moveTimeReturn = (distanceToOwner > 0) ? distanceToOwner / _speed : 0.1f;

            while (elapsed < moveTimeReturn)
            {
                if (!PlayerMove.CanShoot) break; // ラウンド終了時は移動中断

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / moveTimeReturn);

                if (_owner != null)
                {
                    // 滑らかに戻る。Lerpの第2引数を常に _owner.position にすることで追従。
                    transform.position = Vector3.Lerp(peakPos, _owner.position, Mathf.SmoothStep(0, 1, t));
                }

                UpdateAfterimage();
                if (Time.frameCount % 2 == 0) FireSubDanmaku();
                yield return null;
            }
        }

        // --- E. 消滅演出：縮小して消える（その場に停止） ---
        yield return StartCoroutine(ScaleRoutine(1, 0));

        Destroy(gameObject); //
    }

    /// <summary>
    /// その場に停止してサイズを変更するコルーチン
    /// </summary>
    private IEnumerator ScaleRoutine(float start, float end)
    {
        float elapsed = 0;
        while (elapsed < _scaleDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / _scaleDuration;
            transform.localScale = Vector3.one * Mathf.SmoothStep(start, end, t);
            yield return null;
        }
        transform.localScale = Vector3.one * end;
    }

    private void UpdateAfterimage()
    {
        if (!_enableAfterimage || _sr == null) return;
        _afterimageTimer += Time.deltaTime;
        if (_afterimageTimer >= _afterimageInterval)
        {
            _afterimageTimer = 0;
            SpawnAfterimage();
        }
    }

    private void SpawnAfterimage()
    {
        GameObject go = new GameObject("BoomerangAfterimage");
        go.transform.SetPositionAndRotation(transform.position, transform.rotation);
        go.transform.localScale = transform.localScale;

        SpriteRenderer trailSR = go.AddComponent<SpriteRenderer>();
        trailSR.sprite = _sr.sprite;
        trailSR.color = _sr.color;
        trailSR.sortingOrder = _sr.sortingOrder - 1;

        BoomerangAfterimage fader = go.AddComponent<BoomerangAfterimage>();
        fader.Setup(_afterimageFadeTime, _afterimageStartAlpha);
    }

    private void FireSubDanmaku()
    {
        BulletData dataToUse = (_subDanmakuData != null) ? _subDanmakuData : _subBulletData;
        if (_ownerEmitter == null || dataToUse == null) return;

        Vector3 firePos = (_muzzleTransform != null) ? _muzzleTransform.position : transform.position;

        // ★ 修正：現在のビットの回転角度を取得して基準にする
        float baseAngle = transform.eulerAngles.z;
        float angleStep = 360f / _subBulletCount;

        for (int i = 0; i < _subBulletCount; i++)
        {
            // ★ 修正：初速を 0、加速度と最高速度を渡すように拡張メソッドを呼び出す
            _ownerEmitter.ExecuteSubShot(
                dataToUse,
                firePos,
                0f,                     // 初速は 0
                baseAngle + (i * angleStep), // 親の角度 + 円周オフセット
                _subBulletAccel,         // 加速度
                _subBulletMaxSpeed,      // 最高速度
                gameObject.tag,
                gameObject.layer
            );
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (_owner == null) return;
        if (collision.CompareTag("Player") && collision.gameObject != _owner.gameObject)
        {
            collision.SendMessage("OnHit", 20, SendMessageOptions.DontRequireReceiver);
        }
    }

    private class BoomerangAfterimage : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private float _fadeDuration;
        private float _startAlpha;
        private float _elapsed = 0;

        public void Setup(float duration, float startAlpha)
        {
            _fadeDuration = duration;
            _startAlpha = startAlpha;
            _sr = GetComponent<SpriteRenderer>();
            SetAlpha(_sr, _startAlpha);
        }

        private void Update()
        {
            if (_sr == null) return;
            _elapsed += Time.deltaTime;
            float t = _elapsed / _fadeDuration;
            SetAlpha(_sr, Mathf.Lerp(_startAlpha, 0f, t));
            if (_elapsed >= _fadeDuration) Destroy(gameObject);
        }

        private void SetAlpha(SpriteRenderer sr, float alpha)
        {
            Color c = sr.color;
            c.a = alpha;
            sr.color = c;
        }
    }
}