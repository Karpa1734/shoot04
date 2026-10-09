using KanKikuchi.AudioManager;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SlothMagicCircle : MonoBehaviour
{
    public static List<SlothMagicCircle> AllCircles = new List<SlothMagicCircle>();

    private GameObject shooter;
    private string targetTag;
    private float fireInterval;
    private float fireTimer = 0f;
    private float lifeTimer = 16f;

    [Header("🌟 通常時の弾幕データ")]
    [SerializeField] private BulletData assignedBulletData;

    [Header("🌟 領域内（スペル中）専用の弾幕データ")]
    [SerializeField] private BulletData domainBulletData;

    [Header("Behavior Settings")]
    [SerializeField] private float _rotationSpeed = 180f;   // 1秒間の回転角度（常時回転）
    [SerializeField] private float _scaleDuration = 0.3f;   // 出現・消滅の拡縮にかかる時間

    private LineRenderer lineRenderer;
    private bool _isDestroying = false;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        }
        lineRenderer.startWidth = 0.03f;
        lineRenderer.endWidth = 0.03f;
        lineRenderer.positionCount = 0;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = new Color(0.4f, 0.7f, 1f, 0.7f);
        lineRenderer.endColor = new Color(0.4f, 0.7f, 1f, 0.7f);
    }

    public void Initialize(GameObject shooter, BulletData bulletData, string targetTag, float interval)
    {
        this.shooter = shooter;
        this.targetTag = targetTag;
        this.fireInterval = interval;
        this.fireTimer = interval;

        if (assignedBulletData == null && bulletData != null)
        {
            assignedBulletData = bulletData;
        }

        AllCircles.Add(this);

        if (AllCircles.Count > 5)
        {
            SlothMagicCircle oldest = AllCircles[0];
            if (oldest != null && oldest != this)
            {
                oldest.StartDestroyRoutine();
            }
        }

        transform.localScale = Vector3.zero;
        StartCoroutine(ScaleRoutine(0f, 1.5f));
    }

    void Update()
    {
        if (Mathf.Approximately(Time.timeScale, 0f)) return;

        transform.Rotate(0f, 0f, _rotationSpeed * Time.deltaTime);

        if (!PlayerMove.CanShoot && !_isDestroying)
        {
            StartDestroyRoutine();
            return;
        }

        if (_isDestroying) return;

        lifeTimer -= UnityEngine.Time.deltaTime;
        if (lifeTimer <= 0f)
        {
            StartDestroyRoutine();
            return;
        }

        fireTimer -= UnityEngine.Time.deltaTime;
        if (fireTimer <= 0f)
        {
            FireCircleBullet();

            int connectedCount = GetConnectedCount();

            PlayerStatusManager statusMgr = shooter != null ? shooter.GetComponent<PlayerStatusManager>() : null;
            if (statusMgr == null && shooter != null) statusMgr = shooter.GetComponentInChildren<PlayerStatusManager>();
            bool isSpellActive = (statusMgr != null && statusMgr.isSpellCardActive);

            float intervalMultiplier = isSpellActive ? 0.8f : 1.0f;

            float enhancedInterval = Mathf.Max(0.1f, (fireInterval - (connectedCount * 0.15f)) * intervalMultiplier);
            fireTimer = enhancedInterval;
        }

        UpdateConnections();
    }

    private IEnumerator ScaleRoutine(float start, float end)
    {
        float elapsed = 0f;
        while (elapsed < _scaleDuration)
        {
            elapsed += UnityEngine.Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _scaleDuration);
            transform.localScale = Vector3.one * Mathf.SmoothStep(start, end, t);
            yield return null;
        }
        transform.localScale = Vector3.one * end;
    }

    public void StartDestroyRoutine()
    {
        if (_isDestroying) return;
        _isDestroying = true;
        StartCoroutine(DestroyRoutine());
    }

    private IEnumerator DestroyRoutine()
    {
        if (AllCircles.Contains(this))
        {
            AllCircles.Remove(this);
        }

        if (lineRenderer != null) lineRenderer.positionCount = 0;

        yield return StartCoroutine(ScaleRoutine(1f, 0f));
        Destroy(gameObject);
    }

    private int GetConnectedCount()
    {
        int count = 0;
        float linkThreshold = 9.0f;

        foreach (var other in AllCircles)
        {
            if (other != null && other != this && !other._isDestroying)
            {
                float dist = Vector3.Distance(transform.position, other.transform.position);
                if (dist <= linkThreshold)
                {
                    count++;
                }
            }
        }
        return count;
    }

    private void UpdateConnections()
    {
        if (lineRenderer == null) return;

        List<Vector3> connectedPositions = new List<Vector3>();
        float linkThreshold = 9.0f;

        foreach (var other in AllCircles)
        {
            if (other != null && other != this && !other._isDestroying)
            {
                float dist = Vector3.Distance(transform.position, other.transform.position);
                if (dist <= linkThreshold)
                {
                    connectedPositions.Add(other.transform.position);
                }
            }
        }

        if (connectedPositions.Count > 0)
        {
            lineRenderer.positionCount = connectedPositions.Count * 2;
            for (int i = 0; i < connectedPositions.Count; i++)
            {
                lineRenderer.SetPosition(i * 2, transform.position);
                lineRenderer.SetPosition(i * 2 + 1, connectedPositions[i]);
            }
        }
        else
        {
            lineRenderer.positionCount = 0;
        }
    }

    private void FireCircleBullet()
    {
        PlayerStatusManager statusMgr = shooter != null ? shooter.GetComponent<PlayerStatusManager>() : null;
        if (statusMgr == null && shooter != null) statusMgr = shooter.GetComponentInChildren<PlayerStatusManager>();
        bool isSpellActive = (statusMgr != null && statusMgr.isSpellCardActive);

        BulletData dataToUse = (isSpellActive && domainBulletData != null) ? domainBulletData : assignedBulletData;
        if (dataToUse == null) return;

        int connectedCount = GetConnectedCount();

        int baseWayCount = 1 + (connectedCount * 2);
        float speed = 3.0f + (connectedCount * 0.5f);

        SlothUltDomain activeDomain = Object.FindAnyObjectByType<SlothUltDomain>();
        bool isUltActive = (activeDomain != null);

        int wayCount;
        if (isUltActive)
        {
            // 🌟 ULT時は天井なしでスケールアップ（1.5倍）
            wayCount = Mathf.RoundToInt(baseWayCount * 1.5f);
            if (wayCount % 2 == 0) wayCount += 1;
        }
        else
        {
            // 🌟 通常時およびスペル時は最大 7way を上限（天井）にする
            wayCount = baseWayCount;
            if (wayCount % 2 == 0) wayCount += 1;
            wayCount = Mathf.Min(wayCount, 7);
        }

        float baseAngle = 0f;
        Transform enemyTarget = null;
        foreach (var p in PlayerMove.AllPlayers)
        {
            if (p != null && p.gameObject != shooter) { enemyTarget = p.transform; break; }
        }
        if (enemyTarget != null)
        {
            Vector3 dir = enemyTarget.position - transform.position;
            baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        }

        PlayerDanmakuEmitter emitter = shooter != null ? shooter.GetComponentInChildren<PlayerDanmakuEmitter>() : null;
        if (emitter == null) return;

        float stepAngle = 360f / wayCount;
        int centerIndex = wayCount / 2;

        for (int i = 0; i < wayCount; i++)
        {
            float finalAngle = baseAngle + ((i - centerIndex) * stepAngle);

            if (activeDomain != null)
            {
                float rad = finalAngle * Mathf.Deg2Rad;
                Vector3 checkPoint = transform.position + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * 0.8f;

                PolygonCollider2D domainCollider = activeDomain.GetComponent<PolygonCollider2D>();
                if (domainCollider != null && domainCollider.OverlapPoint(checkPoint))
                {
                    finalAngle += 25f;
                }
            }

            emitter.ExecuteSubShot(dataToUse, transform.position, speed, finalAngle, 0, 8f, gameObject.tag, gameObject.layer, 1f);
        }

        if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.SHOT1, 0.2f);
    }

    void OnDestroy()
    {
        if (AllCircles.Contains(this))
        {
            AllCircles.Remove(this);
        }
    }
}