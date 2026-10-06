using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SlothUltDomain : MonoBehaviour
{
    private GameObject _shooter;
    private string _targetTag;
    private int _damagePerTick;
    private float _tickInterval = 0.1f; // 0.1秒おきの持続ダメージ
    private float _duration = 2.0f;
    private List<SlothMagicCircle> _linkedCircles = new List<SlothMagicCircle>();

    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private PolygonCollider2D _polyCollider;

    void Awake()
    {
        _meshFilter = gameObject.GetComponent<MeshFilter>();
        if (_meshFilter == null) _meshFilter = gameObject.AddComponent<MeshFilter>();

        _meshRenderer = gameObject.GetComponent<MeshRenderer>();
        if (_meshRenderer == null) _meshRenderer = gameObject.AddComponent<MeshRenderer>();

        _meshRenderer.material = new Material(Shader.Find("Sprites/Default"));
        _meshRenderer.sortingOrder = 1000;

        _polyCollider = gameObject.GetComponent<PolygonCollider2D>();
        if (_polyCollider == null) _polyCollider = gameObject.AddComponent<PolygonCollider2D>();
        _polyCollider.isTrigger = true;
    }

    public void Initialize(GameObject shooter, string targetTag, int damage, List<SlothMagicCircle> circles, float duration)
    {
        _shooter = shooter;
        _targetTag = targetTag;
        _damagePerTick = damage;
        _linkedCircles = new List<SlothMagicCircle>(circles);
        _duration = duration;

        StartCoroutine(DomainRoutine());
    }

    private IEnumerator DomainRoutine()
    {
        float elapsed = 0f;
        float tickTimer = 0f;

        while (elapsed < _duration)
        {
            if (Mathf.Approximately(Time.timeScale, 0f)) { yield return null; continue; }

            elapsed += Time.deltaTime;
            tickTimer += Time.deltaTime;

            UpdateDomainShape();
            CheckBulletsInsideDomain();

            // 一定間隔（tickInterval）ごとに持続ダメージを適用
            if (tickTimer >= _tickInterval)
            {
                tickTimer = 0f;
                ApplyDomainDamage();
            }

            yield return null;
        }

        Destroy(gameObject);
    }

    private void UpdateDomainShape()
    {
        _linkedCircles.RemoveAll(c => c == null);
        if (_linkedCircles.Count < 3)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 center = Vector3.zero;
        foreach (var c in _linkedCircles) center += c.transform.position;
        center /= _linkedCircles.Count;
        transform.position = center;

        List<Vector3> worldPoints = new List<Vector3>();
        foreach (var c in _linkedCircles) worldPoints.Add(c.transform.position);

        worldPoints.Sort((a, b) => {
            float angleA = Mathf.Atan2(a.y - center.y, a.x - center.x);
            float angleB = Mathf.Atan2(b.y - center.y, b.x - center.x);
            return angleA.CompareTo(angleB);
        });

        Vector2[] localPoints = new Vector2[worldPoints.Count];
        for (int i = 0; i < worldPoints.Count; i++)
        {
            Vector2 rawPoint = worldPoints[i] - center;
            localPoints[i] = rawPoint * 0.92f; // 魔方陣との干渉を防ぐための微小縮小
        }

        _polyCollider.points = localPoints;

        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[worldPoints.Count];
        for (int i = 0; i < worldPoints.Count; i++) vertices[i] = worldPoints[i] - center;

        int vertexCount = worldPoints.Count;
        int[] triangles = new int[(vertexCount - 2) * 3];
        for (int i = 0; i < vertexCount - 2; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        _meshFilter.mesh = mesh;

        _meshRenderer.material.color = new Color(0.2f, 0.6f, 1.0f, 0.5f);
    }

    private void CheckBulletsInsideDomain()
    {
        if (_polyCollider == null) return;

        DanmakuBullet[] pBullets = Object.FindObjectsByType<DanmakuBullet>(FindObjectsSortMode.None);
        foreach (var bullet in pBullets)
        {
            if (bullet != null && bullet.gameObject.activeInHierarchy)
            {
                if (_polyCollider.OverlapPoint(bullet.transform.position))
                {
                    bullet.Deactivate(true);
                }
            }
        }

        EnemyBullet[] eBullets = Object.FindObjectsByType<EnemyBullet>(FindObjectsSortMode.None);
        foreach (var bullet in eBullets)
        {
            if (bullet != null && bullet.gameObject.activeInHierarchy)
            {
                if (_polyCollider.OverlapPoint(bullet.transform.position))
                {
                    bullet.Deactivate(true);
                }
            }
        }
    }

    /// <summary>
    /// 🌟 PlayerHitHandler の無敵・ヒット制限をバイパスし、PlayerStatusManager.ApplyDamage を直接叩いて持続ダメージを与える
    /// </summary>
    private void ApplyDomainDamage()
    {
        foreach (var p in PlayerMove.AllPlayers)
        {
            if (p == null || p.gameObject == _shooter) continue;

            if (p.CompareTag(_targetTag) || _shooter != p.gameObject)
            {
                PlayerStatusManager statusMgr = p.GetComponent<PlayerStatusManager>();
                if (statusMgr == null) statusMgr = p.GetComponentInChildren<PlayerStatusManager>();

                if (statusMgr != null && _polyCollider.OverlapPoint(p.transform.position))
                {
                    // 💡 通常被弾のインターバルを無視して直接HP/バリアを削る
                    statusMgr.ApplyDamage(_damagePerTick);
                }
            }
        }
    }
}