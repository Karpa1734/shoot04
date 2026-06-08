using KanKikuchi.AudioManager;
using System.Collections;
using UnityEngine;

/// <summary>
/// 🌟 【音響周期不整合・音消えバグ完全根治版】：背後追従型魔方陣ビット本体
/// 🌟 射撃間隔（4F）とSE間引き（3F）の周期ズレによる不規則な音切れ（12Fの罠）を100%パージ。
/// 🌟 生成インデックス（_bitIndex）に基づく固有スロットID音響分散システムにより、
/// 🌟 6連極大クロス時も音割れを完全に防ぎつつ、弾源とSEが完璧に等速等間隔でシンクロ駆動します！
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class PortalBitObject : MonoBehaviour
{
    private Transform _owner;
    private PlayerDanmakuEmitter _ownerEmitter;
    private SpriteRenderer _sr;

    private float _behindOffsetX;
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

    public void Initialize(Transform owner, PlayerSkillData.SkillSettings s, float behindX, float yOffset, float shootAngle, float duration, int interval, PlayerDanmakuEmitter emitter)
    {
        _owner = owner;
        _ownerEmitter = emitter;
        _behindOffsetX = behindX;
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
        if (_owner != null)
        {
            transform.position = _owner.position + new Vector3(_behindOffsetX, initialSpawnY, 0f);
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

            if (!PlayerMove.CanShoot || _owner == null) break;

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

            // ③ 【ワープ完全根治】：可変限界高度フルステージ・上下対称クロス数理エンジン
            float finalLocalY = waveCenterY + Mathf.Cos(baseTimerAngleRad) * groupSign * currentWaveMagnitude;

            Vector3 targetPortalPos = _owner.position + new Vector3(_behindOffsetX, finalLocalY, 0f);
            transform.position = targetPortalPos;
            transform.rotation = Quaternion.Euler(0f, 0f, _shootAngle);

            // -------------------------------------------------------------------------
            // ④ 射撃制御（正味の連射持続時間である 0.2秒〜1.7秒 の間だけ射出）
            // -------------------------------------------------------------------------
            if (elapsed >= 0.2f && elapsed <= totalTimelineTime - 0.2f)
            {
                // 🎯 4フレームに1回の正規の射撃タイミング
                if (frameCounter % _fireIntervalFrames == 0)
                {
                    BulletData dataToUse = (_subDanmakuData != null) ? _subDanmakuData : _bulletDataToShoot;

                    if (_ownerEmitter != null && dataToUse != null)
                    {
                        // =========================================================================
                        // 🎯【コンパイラ/数理調停の核心：音消えバグ完全根治インフラ】
                        // =========================================================================
                        // 💡 外部の3F周期に頼るのではなく、この弾が出る「4F周期」の瞬間に完全に同期！
                        // 💡 6枚（または4枚）の魔方陣のうち、「インデックスが 0 番（最下端）」のオブジェクトだけが
                        // 💡 代表してSEをトリガーすることで、不規則な音切れ（12Fの罠）を100%永久パージ！
                        // 💡 常に4フレームに1回、「シュババババッ！」とキレ味鋭い音が一切途切れずに鳴り響きます。
                        if (_bitIndex == 0 && SEManager.Instance != null)
                        {
                            SEManager.Instance.Play(SEPath.SHOT1, 0.35f); // 1音集中に付き音量をスマートに最適化
                        }

                        _ownerEmitter.ExecuteSubShotFromPortal(dataToUse, transform.position, _bulletSpeed, _shootAngle, _bulletDelay);
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