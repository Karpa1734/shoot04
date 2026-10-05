using System.Collections;
using UnityEngine;

public class PrideUltimateLaser : MonoBehaviour
{
    private float currentLaserAngle;
    private float targetConvergenceAngle; // 収束先の角度
    private bool isConverging = false;
    private float convergenceTimer = 0f;
    private float convergenceDuration = 1.0f; // 閉まりきるのにかかる時間（秒）

    private float currentWidth = 0.0f;
    private float targetMaxWidth = 2.5f;
    private float laserLength = 40.0f; // 縦の長さ

    private Transform ownerTransform;
    private string targetTag;
    private int laserDamage = 10;
    private float _hitTimer = 0f;

    private int frame = 0;
    private int durationFrames = 150; // 全体の生存フレーム数（約2.5秒）
    private bool isClosing = false;
    private float closingStartWidth = 0f;
    private int closingFrames = 0;

    [Header("🌟 追加エフェクト設定")]
    [Tooltip("レーザー発射中、数フレームおきに同じ方向へ射出する波動エフェクトプレハブ")]
    public GameObject waveEffectPrefab;
    [Tooltip("射出する波動エフェクトの移動スピード")]
    public float waveSpeed = 15.0f;
    [Tooltip("何フレームおきにエフェクトを発射するか（例: 8フレームごと）")]
    public int waveIntervalFrames = 8;

    /// <summary>
    /// 外部から角度、最大幅、所有者、ターゲットタグ、ダメージを指定して初期化
    /// </summary>
    public void Initialize(float angle, float maxWidth, Transform owner, string target, int damage)
    {
        currentLaserAngle = angle;
        targetConvergenceAngle = angle; // 収束しない場合はそのまま
        targetMaxWidth = maxWidth;
        ownerTransform = owner;
        targetTag = target;
        laserDamage = damage;
        _hitTimer = 0f;

        UpdateRotation();
        transform.localScale = new Vector3(currentWidth, laserLength, 1.0f);

        if (waveEffectPrefab != null)
        {
            StartCoroutine(SpawnWaveRoutine());
        }
    }

    /// <summary>
    /// 🌟 2wayなどで「徐々に角度を閉じていきたい場合」に呼び出す拡張初期化
    /// </summary>
    public void InitializeWithConvergence(float startAngle, float finalAngle, float maxWidth, Transform owner, string target, int damage, float duration)
    {
        currentLaserAngle = startAngle;
        targetConvergenceAngle = finalAngle;
        isConverging = true;
        convergenceTimer = 0f;
        convergenceDuration = duration;

        targetMaxWidth = maxWidth;
        ownerTransform = owner;
        targetTag = target;
        laserDamage = damage;
        _hitTimer = 0f;

        UpdateRotation();
        transform.localScale = new Vector3(currentWidth, laserLength, 1.0f);

        if (waveEffectPrefab != null)
        {
            StartCoroutine(SpawnWaveRoutine());
        }
    }

    public void ForceClose()
    {
        if (isClosing) return;
        closingStartWidth = currentWidth;
        isClosing = true;
        closingFrames = 0;
    }

    private IEnumerator SpawnWaveRoutine()
    {
        while (frame < 150 && !isClosing)
        {
            // 飛んでいくエフェクトにも現在のレーザーの向きを反映させる
            Quaternion waveRot = Quaternion.Euler(0f, 0f, currentLaserAngle - 90f);
            GameObject waveObj = Instantiate(waveEffectPrefab, transform.position, waveRot);

            WaveEffectMover mover = waveObj.GetComponent<WaveEffectMover>();
            if (mover == null) mover = waveObj.AddComponent<WaveEffectMover>();
            mover.Initialize(currentLaserAngle, waveSpeed);

            for (int i = 0; i < waveIntervalFrames; i++)
            {
                yield return null;
            }
        }
    }

    private void Update()
    {
        if (Mathf.Approximately(Time.timeScale, 0f)) return;

        if (!PlayerMove.CanShoot && !isClosing)
        {
            ForceClose();
        }

        if (_hitTimer > 0f)
        {
            _hitTimer -= Time.deltaTime;
        }

        // 🌟 【角度の収束処理（少しずつ閉まっていく）】
        if (isConverging && !isClosing)
        {
            convergenceTimer += Time.deltaTime;
            float t = Mathf.Clamp01(convergenceTimer / convergenceDuration);
            // 滑らかに目標の角度へ補間する
            currentLaserAngle = Mathf.LerpAngle(currentLaserAngle, targetConvergenceAngle, t);
        }

        if (isClosing)
        {
            closingFrames++;
            int shrinkDuration = 15;
            float t = (float)closingFrames / shrinkDuration;
            currentWidth = Mathf.Lerp(closingStartWidth, 0f, t);

            if (closingFrames >= shrinkDuration)
            {
                Destroy(gameObject);
                return;
            }
        }
        else
        {
            frame++;

            if (ownerTransform != null)
            {
                transform.position = ownerTransform.position;
            }

            if (frame > 0 && frame <= 15)
            {
                currentWidth += (targetMaxWidth / 15f);
            }

            if (frame > 135 && frame <= 150)
            {
                currentWidth -= (targetMaxWidth / 15f);
            }

            if (frame >= 150)
            {
                Destroy(gameObject);
                return;
            }
        }

        transform.localScale = new Vector3(Mathf.Max(0f, currentWidth), laserLength, 1.0f);
        UpdateRotation();
    }

    private void UpdateRotation()
    {
        var angles = transform.localEulerAngles;
        angles.z = currentLaserAngle - 90;
        transform.localEulerAngles = angles;
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (isClosing) return;
        if (ownerTransform != null && (collision.gameObject == ownerTransform.gameObject || collision.transform.IsChildOf(ownerTransform)))
            return;

        if (!string.IsNullOrEmpty(targetTag) && collision.CompareTag(targetTag) && _hitTimer <= 0f)
        {
            collision.SendMessage("OnHit", laserDamage, SendMessageOptions.DontRequireReceiver);
            _hitTimer = 0.1f;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isClosing) return;
        if (ownerTransform != null && (collision.gameObject == ownerTransform.gameObject || collision.transform.IsChildOf(ownerTransform)))
            return;

        if (!string.IsNullOrEmpty(targetTag) && collision.CompareTag(targetTag) && _hitTimer <= 0f)
        {
            collision.SendMessage("OnHit", laserDamage, SendMessageOptions.DontRequireReceiver);
            _hitTimer = 0.1f;
        }
    }
}
/// <summary>
/// 🌊 射出された波動エフェクトを指定された角度・スピードで前方に直進させ、
/// 最初はXスケール0から元のスケールへ滑らかに拡大させながら一定時間で消滅させるヘルパー
/// </summary>
public class WaveEffectMover : MonoBehaviour
{
    private float moveAngle;
    private float speed;
    private float lifeTimer = 1.5f; // 寿命（秒）

    private Vector3 originalScale; // プレハブが元々持っているスケールを記憶
    private float scaleExpandDuration = 0.15f; // 0から元のスケールになるまでの時間（秒）
    private float scaleTimer = 0f;

    public void Initialize(float angle, float moveSpeed)
    {
        moveAngle = angle;
        speed = moveSpeed;

        // プレハブの元のスケールを保持しておく
        originalScale = transform.localScale;

        // Xスケールを0にしてスタート
        transform.localScale = new Vector3(0f, originalScale.y, originalScale.z);
    }

    void Update()
    {
        if (Mathf.Approximately(Time.timeScale, 0f)) return;

        // 🌟 波動側もマッチ終了時は即座に消滅させる
        if (!PlayerMove.CanShoot)
        {
            Destroy(gameObject);
            return;
        }

        // 1. スケールを 0 から元のサイズ（originalScale.x）へ向かって拡大させる処理
        if (scaleTimer < scaleExpandDuration)
        {
            scaleTimer += Time.deltaTime;
            float t = Mathf.Clamp01(scaleTimer / scaleExpandDuration);
            float currentX = Mathf.Lerp(0f, originalScale.x, t);
            transform.localScale = new Vector3(currentX, originalScale.y, originalScale.z);
        }

        // 2. 移動処理
        float rad = moveAngle * Mathf.Deg2Rad;
        Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
        transform.position += dir * speed * Time.deltaTime;

        // 3. 寿命・画面外チェック
        lifeTimer -= Time.deltaTime;
        if (lifeTimer <= 0f || Mathf.Abs(transform.position.x) > 12f || Mathf.Abs(transform.position.y) > 8f)
        {
            Destroy(gameObject);
        }
    }
}