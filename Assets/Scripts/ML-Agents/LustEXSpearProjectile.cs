using KanKikuchi.AudioManager;
using System.Collections;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 色欲EX必殺技：壁または敵プレイヤーに激突すると、その場にLustEXExplosionFieldを生成して
/// 18way×5列の幾何学弾幕を大解放する必殺魔槍プロジェクタイル。
/// 💡【コンポーネント生存調停版】：DanmakuBulletを削除せず生存させ、敵シールドに触れても絶対に消滅しないようにガード。
/// </summary>
public class LustEXSpearProjectile : MonoBehaviour
{
    private GameObject _owner;
    private BulletData _spearData;
    private BulletData _subBulletData;
    private PlayerDanmakuEmitter _emitter;

    private float _speed; // Launch 時の基準弾速
    private bool _isFlying = false;
    private Vector3 _moveDirection;
    private Transform _targetTransform;

    private bool _isHomingMode = false;
    private float _homingTimer = 0f;
    private float _currentAngle = 0f;

    private int _trailFrameCounter = 0;
    private bool _hasExplosionSpawned = false; // 重複・不発防止用の安全弁フラグ

    private float _spearLengthHalf = 0f; // 📐 槍のグラフィックから逆算した先端までの半分の長さ

    private const float WALL_MIN_X = -8.8f;
    private const float WALL_MAX_X = 8.8f;
    private const float WALL_MIN_Y = -4.8f;
    private const float WALL_MAX_Y = 4.8f;

    // =========================================================================
    // 📐 【完全滑らか・距離適応および時間パラメータ設計】
    // =========================================================================
    private const float DIST_FAR = 6.0f;   // 遠距離の基準（これ以上で完全エイム・超高速Uターン）
    private const float DIST_CLOSE = 2.0f; // 至近距離の基準（これ以下で引きつけ開始）

    private const float START_HOMING_TURN_SPEED = 30f;
    private const float LIMIT_HOMING_TURN_SPEED = 1200f;

    private const float TOTAL_HOMING_DURATION = 3.0f;
    private const float TIME_TO_MAX_ACCURACY = TOTAL_HOMING_DURATION * 0.7f;

    public void Launch(GameObject owner, float angle, float speed, BulletData spearData, BulletData subBulletData, PlayerDanmakuEmitter emitter, bool enableHoming)
    {
        _owner = owner;
        _speed = speed * 1.4f; // 快適な弾速をクリアにキープ
        _spearData = spearData;
        _subBulletData = subBulletData;
        _emitter = emitter;
        _currentAngle = angle;
        _hasExplosionSpawned = false;

        foreach (var p in PlayerMove.AllPlayers)
        {
            if (p != null && p.gameObject != owner) { _targetTransform = p.transform; break; }
        }

        _isHomingMode = enableHoming;
        _homingTimer = 0f;

        float rad = _currentAngle * Mathf.Deg2Rad;
        _moveDirection = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f).normalized;

        transform.rotation = Quaternion.Euler(0, 0, _currentAngle - 90f);

        SpriteRenderer spearSR = GetComponentInChildren<SpriteRenderer>();
        if (spearSR != null && spearSR.sprite != null)
        {
            float spearWorldHeight = (spearSR.sprite.rect.height / spearSR.sprite.pixelsPerUnit) * spearSR.transform.lossyScale.y;
            _spearLengthHalf = spearWorldHeight * 0.5f;
        }
        else
        {
            _spearLengthHalf = 1.2f;
        }

        // =========================================================================
        // 🎯【最核心修正】：DanmakuBulletの物理Destroy破壊処理を完全に廃止！！
        // 💡 移動処理の重複喧嘩を防ぐため、コンポーネントを活かしたまま「移動機能だけをサスペンド」します。
        // =========================================================================
        DanmakuBullet oldBulletLogic = GetComponent<DanmakuBullet>();
        if (oldBulletLogic != null)
        {
            oldBulletLogic.isMovementSuspended = true; // 動きをプロジェクタイル側に100%委託
            oldBulletLogic.isIndestructible = true;   // 敵シールドのDestroyをすり抜ける不滅耐性を完全維持！
        }

        Collider2D col = GetComponent<Collider2D>();
        if (col == null) col = gameObject.AddComponent<CircleCollider2D>();

        if (col != null)
        {
            col.enabled = true;
            col.isTrigger = true;
            if (col is CircleCollider2D circle && spearData != null)
            {
                circle.radius = spearData.radius;
                circle.offset = new Vector2(0f, _spearLengthHalf / transform.lossyScale.y);
            }
        }

        _trailFrameCounter = 0;
        _isFlying = true;
    }

    private Vector3 GetSpearTipWorldPosition()
    {
        return transform.position + (_moveDirection * _spearLengthHalf);
    }

    private void FixedUpdate()
    {
        if (!_isFlying) return;

        Vector3 currentTipPos = GetSpearTipWorldPosition();

        bool isInsideStage = (currentTipPos.x >= WALL_MIN_X && currentTipPos.x <= WALL_MAX_X &&
                              currentTipPos.y >= WALL_MIN_Y && currentTipPos.y <= WALL_MAX_Y);

        float currentFrameSpeed = _speed;

        if (_isHomingMode && _targetTransform != null)
        {
            if (isInsideStage)
            {
                _homingTimer += Time.fixedDeltaTime;
            }

            if (_homingTimer <= TOTAL_HOMING_DURATION)
            {
                float distance = Vector3.Distance(currentTipPos, _targetTransform.position);

                float rawRatio = (distance - DIST_CLOSE) / (DIST_FAR - DIST_CLOSE);
                float distFactor = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(rawRatio));

                float progressToMax = Mathf.Clamp01(_homingTimer / TIME_TO_MAX_ACCURACY);
                float expFactor = Mathf.Pow(progressToMax, 3f);

                float actualTimeFactor = Mathf.Lerp(expFactor, 1f, distFactor);

                float currentTurnSpeed = Mathf.Lerp(START_HOMING_TURN_SPEED, LIMIT_HOMING_TURN_SPEED, actualTimeFactor);

                currentFrameSpeed = Mathf.Lerp(_speed * 1.3f, _speed, distFactor);

                float launchFactor = Mathf.Clamp01(_homingTimer / 0.3f);
                currentTurnSpeed *= launchFactor;

                Vector3 targetDir = _targetTransform.position - transform.position;
                float targetAngle = Mathf.Atan2(targetDir.y, targetDir.x) * Mathf.Rad2Deg;

                _currentAngle = Mathf.MoveTowardsAngle(_currentAngle, targetAngle, currentTurnSpeed * Time.fixedDeltaTime);

                float rad = _currentAngle * Mathf.Deg2Rad;
                _moveDirection = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f).normalized;
                transform.rotation = Quaternion.Euler(0, 0, _currentAngle - 90f);
            }
            else
            {
                _isHomingMode = false;
                if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.LASER2, 0.3f);
            }
        }

        transform.position += _moveDirection * currentFrameSpeed * Time.fixedDeltaTime;
        _trailFrameCounter++;

        if (_trailFrameCounter % 3 == 0) SpawnSpearVisualTrail();

        if (!isInsideStage)
        {
            if (_isHomingMode) return;

            _isFlying = false;
            float finalClampedX = Mathf.Clamp(currentTipPos.x, WALL_MIN_X, WALL_MAX_X);
            float finalClampedY = Mathf.Clamp(currentTipPos.y, WALL_MIN_Y, WALL_MAX_Y);
            transform.position = new Vector3(finalClampedX, finalClampedY, 0f) - (_moveDirection * _spearLengthHalf);

            StartCoroutine(StuckAndExplodeRoutine());
        }
    }

    private void SpawnSpearVisualTrail()
    {
        SpriteRenderer spearSR = GetComponentInChildren<SpriteRenderer>();
        if (spearSR == null) return;

        GameObject go = new GameObject("LustEXSpearAfterimage");
        go.transform.SetPositionAndRotation(transform.position, transform.rotation);
        go.transform.localScale = transform.localScale;

        SpriteRenderer trailSR = go.AddComponent<SpriteRenderer>();
        trailSR.sprite = spearSR.sprite;
        trailSR.material = spearSR.material;
        trailSR.sortingLayerName = "Middle";
        trailSR.sortingOrder = spearSR.sortingOrder - 1;

        Color origColor = spearSR.color;
        trailSR.color = new Color(origColor.r, origColor.g * 0.4f, origColor.b, 0.5f);

        SpearTrailFader fader = go.AddComponent<SpearTrailFader>();
        fader.Setup(duration: 0.2f, startAlpha: 0.5f);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!_isFlying || _owner == null || _hasExplosionSpawned) return;
        if (collision.gameObject == _owner || collision.transform.IsChildOf(_owner.transform)) return;

        if (collision.CompareTag("Player"))
        {
            _isFlying = false;

            int damage = (_spearData != null) ? _spearData.damage : 40;
            collision.SendMessage("OnHit", damage, SendMessageOptions.DontRequireReceiver);

            StartCoroutine(StuckAndExplodeRoutine());
        }
    }

    private IEnumerator StuckAndExplodeRoutine()
    {
        if (_hasExplosionSpawned) yield break;
        _hasExplosionSpawned = true;

        if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.STARS, 0.4f);
        yield return new WaitForSeconds(0.15f);

        GameObject fieldObj = new GameObject("LustEXExplosionField");
        fieldObj.transform.position = GetSpearTipWorldPosition();
        fieldObj.tag = gameObject.tag;
        fieldObj.layer = gameObject.layer;

        fieldObj.AddComponent<SpriteRenderer>();
        LustEXExplosionField exField = fieldObj.AddComponent<LustEXExplosionField>();

        exField.Initialize(_owner, _subBulletData, _emitter, duration: 1.0f);

        // =========================================================================
        // 🎯【最核心修正：物理破棄をパージし、プールインフラへの安全無音パージへ統合】
        // 💡 理由：Destroyだとオーラエフェクト等の子オブジェクトが不完全に虚空に残り、
        //          爆発最大化の瞬間に重なって色化け・サイズバグを起こしていました。
        //          Deactivate(false) を通すことで、残骸を出さずに美しくプールへ返却します。
        // =========================================================================
        DanmakuBullet bullet = GetComponent<DanmakuBullet>();
        if (bullet != null)
        {
            // 不滅フラグを強制解除し、システム強制終了（force: true）で無音パージしてプールへ安全返却
            bullet.isIndestructible = false;
            bullet.Deactivate(playBreakEffect: false, force: true);
        }
        else
        {
            // セーフティ：万が一コンポーネントがない場合のみ物理破棄
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (!_hasExplosionSpawned && _owner != null)
        {
            PlayerStatusManager ownerStatus = _owner.GetComponent<PlayerStatusManager>();
            if (ownerStatus == null) ownerStatus = _owner.GetComponentInParent<PlayerStatusManager>();

            if (ownerStatus != null && ownerStatus.isSpellCardActive)
            {
                ownerStatus.DeactivateSpellCard(false);
            }

            if (_emitter != null)
            {
                System.Reflection.FieldInfo exActiveField = typeof(PlayerDanmakuEmitter).GetField("_isEXSkillActive", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (exActiveField != null)
                {
                    exActiveField.SetValue(_emitter, false);
                }
            }
        }
    }

    private class SpearTrailFader : MonoBehaviour
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
            if (_sr != null)
            {
                Color c = _sr.color;
                c.a = startAlpha;
                _sr.color = c;
            }
        }

        private void Update()
        {
            if (_sr == null) return;
            _elapsed += Time.deltaTime;
            float t = _elapsed / _fadeDuration;

            Color c = _sr.color;
            c.a = Mathf.Lerp(_startAlpha, 0f, t);
            _sr.color = c;

            if (_elapsed >= _fadeDuration) Destroy(gameObject);
        }
    }
}