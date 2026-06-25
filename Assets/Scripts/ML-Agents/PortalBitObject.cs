using KanKikuchi.AudioManager;
using System.Collections;
using UnityEngine;

/// <summary>
/// 🌟 【リアルタイム敵位置逆相追従 ＆ 動的ターゲットロックオン版】：背後追従型魔方陣ビット本体
/// 🌟 真横固定射撃をパージし、毎フレーム敵の座標を自動追尾して「自機を挟んだ真逆の背後」へリアルタイムに陣形を旋回。
/// 🌟 銃口の角度および射出ベクトルも、敵の芯をミリ秒単位で完全に捉え続けるホーミングスキャンインフラを確立！
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class PortalBitObject : MonoBehaviour
{
    private Transform _owner;
    private Emitter_Greed _ownerEmitter;
    private SpriteRenderer _sr;

    private float _behindOffsetX; // 💡 距離の絶対値（半径）として流用
    private float _yOffset;
    private float _shootAngle;
    private float _bulletSpeed;
    private float _bulletDelay;

    private float _duration;
    private int _fireIntervalFrames;

    [Header("Sub Bullet Settings (ブーメラン準拠構造)")]
    [Tooltip("魔方陣の銃口から実際に発射したい弾幕（ナイフなど）のデータをここにドラッグ＆ドロップしてください")]
    [SerializeField] private BulletData _subDanmakuData;

    private int _bitIndex = 0;
    private bool _isSpellCardEnhanced = false;

    public void Initialize(Transform owner, PlayerSkillData.SkillSettings s, float behindX, float yOffset, float shootAngle, float duration, int interval, Emitter_Greed emitter)
    {
        _owner = owner;
        _ownerEmitter = emitter;
        _behindOffsetX = Mathf.Abs(behindX); // 💡 カリンからの配置半径（1.2f）として絶対値を取得
        _yOffset = yOffset;
        _shootAngle = shootAngle;
        _duration = duration;
        _fireIntervalFrames = interval;

        _bulletSpeed = s.speed * 1.2f;
        _bulletDelay = s.delay;

        _sr = GetComponent<SpriteRenderer>();

        if (_sr != null && _subDanmakuData != null)
        {
            if (_subDanmakuData.material != null) _sr.material = _subDanmakuData.material;
        }

        if (emitter != null)
        {
            PlayerStatusManager ownerStatus = emitter.GetComponentInParent<PlayerStatusManager>();
            _isSpellCardEnhanced = (ownerStatus != null && ownerStatus.isSpellCardActive);
        }

        if (_isSpellCardEnhanced)
        {
            if (Mathf.Approximately(_yOffset, -2.5f)) _bitIndex = 0;
            else if (Mathf.Approximately(_yOffset, -1.5f)) _bitIndex = 1;
            else if (Mathf.Approximately(_yOffset, -0.5f)) _bitIndex = 2;
            else if (Mathf.Approximately(_yOffset, 0.5f)) _bitIndex = 3;
            else if (Mathf.Approximately(_yOffset, 1.5f)) _bitIndex = 4;
            else _bitIndex = 5;
        }
        else
        {
            if (Mathf.Approximately(_yOffset, -1.5f)) _bitIndex = 0;
            else if (Mathf.Approximately(_yOffset, -0.5f)) _bitIndex = 1;
            else if (Mathf.Approximately(_yOffset, 0.5f)) _bitIndex = 2;
            else _bitIndex = 3;
        }

        float currentMaxMag = _isSpellCardEnhanced ? 2.25f : 1.5f;
        int halfCount = _isSpellCardEnhanced ? 3 : 2;

        float initialSpawnY = (_bitIndex >= halfCount) ? currentMaxMag : -currentMaxMag;

        // 🎯 生まれた瞬間の0フレーム目から敵の逆ベクトルの最端へ初期バインド（ワープ防止）
        if (_owner != null && _ownerEmitter != null)
        {
            float targetAngleDeg = _ownerEmitter.ExecuteGetAngleToTargetBridge();
            float baseRad = targetAngleDeg * Mathf.Deg2Rad;

            // 敵の逆方向ベクトル（背後半径） ＋ クロス高度ベクトル
            Vector3 forwardDir = new Vector3(Mathf.Cos(baseRad), Mathf.Sin(baseRad), 0f);
            Vector3 orthoDir = new Vector3(-Mathf.Sin(baseRad), Mathf.Cos(baseRad), 0f);

            transform.position = _owner.position - (forwardDir * _behindOffsetX) + (orthoDir * initialSpawnY);
        }

        transform.localScale = Vector3.zero;
        transform.rotation = Quaternion.Euler(0f, 0f, _shootAngle);

        StartCoroutine(PortalBitLifeRoutine());
    }

    private IEnumerator PortalBitLifeRoutine()
    {
        float totalTimelineTime = _duration + 0.4f; // 1.5秒(連射) ＋ 出現0.2秒 ＋ 消滅0.2秒 ＝ 1.9秒
        float elapsed = 0f;
        int frameCounter = 0;

        float waveCenterY = 0f;
        float maxWaveMagnitude = _isSpellCardEnhanced ? 2.25f : 1.5f;
        int halfCount = _isSpellCardEnhanced ? 3 : 2;

        float groupSign = (_bitIndex >= halfCount) ? 1f : -1f;
        float timeDelayOffset = (_bitIndex % halfCount) * 0.083f;

        while (elapsed < totalTimelineTime)
        {
            while (Mathf.Approximately(Time.timeScale, 0f)) yield return null;

            if (!PlayerMove.CanShoot || _owner == null || _ownerEmitter == null) break;

            // ① スケールおよびコサイン振幅の滑らかなエンベロープフェード
            float currentScale = 1f;
            float currentWaveMagnitude = maxWaveMagnitude;

            if (elapsed < 0.2f)
            {
                float t = elapsed / 0.2f;
                currentScale = Mathf.SmoothStep(0f, 1f, t);
                currentWaveMagnitude = maxWaveMagnitude * currentScale;
            }
            else if (elapsed > totalTimelineTime - 0.2f)
            {
                float t = (totalTimelineTime - elapsed) / 0.2f;
                currentScale = Mathf.SmoothStep(0f, 1f, t);
                currentWaveMagnitude = maxWaveMagnitude * currentScale;
            }

            transform.localScale = new Vector3(currentScale, currentScale, 1f);

            // ② 進行割合に基づく【ジャスト2周期分（4*PI）】コサインタイマー角の算出
            float shootProgress = Mathf.Clamp01((elapsed - 0.2f) / _duration);
            float baseTimerAngleRad = (shootProgress + timeDelayOffset) * Mathf.PI * 4f;

            // ③ 【可変限界高度フルステージ・上下対称クロス数理エンジン】
            float finalLocalY = waveCenterY + Mathf.Cos(baseTimerAngleRad) * groupSign * currentWaveMagnitude;

            // =========================================================================
            // 🎯【核心処理：リアルタイム逆相極座標トランスフォーム】
            // =========================================================================
            // 1. Emitter（親）から現在のターゲットへの最新リアルタイム角度を取得
            float currentTargetAngleDeg = _ownerEmitter.ExecuteGetAngleToTargetBridge();
            float currentRad = currentTargetAngleDeg * Mathf.Deg2Rad;

            // 2. 敵へ向かう前方ベクトル（Forward）と、それと直交する縦軸ベクトル（Ortho）を毎フレーム更新抽出
            Vector3 forwardVector = new Vector3(Mathf.Cos(currentRad), Mathf.Sin(currentRad), 0f);
            Vector3 orthoVector = new Vector3(-Mathf.Sin(currentRad), Mathf.Cos(currentRad), 0f);

            // 3. 自機位置を原点とし、「敵と真逆の背後方向（-Forward）」へ半径分下がり、そこに「クロス往復（Ortho）」を直交合成！
            Vector3 targetPortalPos = _owner.position - (forwardVector * _behindOffsetX) + (orthoVector * finalLocalY);
            transform.position = targetPortalPos;

            // 4. 銃口を敵の方向へ完全にロックオン！
            transform.rotation = Quaternion.Euler(0f, 0f, currentTargetAngleDeg);

            // -------------------------------------------------------------------------
            // ④ 射撃制御（正味の連射持続時間である 0.2秒〜1.7秒 の間だけ射出）
            // -------------------------------------------------------------------------
            if (elapsed >= 0.2f && elapsed <= totalTimelineTime - 0.2f)
            {
                if (frameCounter % _fireIntervalFrames == 0)
                {
                    BulletData dataToUse = (_subDanmakuData != null) ? _subDanmakuData : _bulletDataToShoot;

                    if (_ownerEmitter != null && dataToUse != null)
                    {
                        if (_bitIndex == 0 && SEManager.Instance != null)
                        {
                            // 4フレーム周期ジャスト同期SE
                            SEManager.Instance.Play(SEPath.SHOT1, 0.35f);
                        }

                        // 🎯【敵へ向けてまっすぐ発射】：固定の_shootAngleをパージし、現在の「currentTargetAngleDeg」を直接叩き込む！
                        _ownerEmitter.ExecuteSubShotFromPortal(dataToUse, transform.position, _bulletSpeed, currentTargetAngleDeg, _bulletDelay);
                    }
                }
                frameCounter++;
            }

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        Destroy(gameObject);
    }

    [HideInInspector] public BulletData _bulletDataToShoot;
}