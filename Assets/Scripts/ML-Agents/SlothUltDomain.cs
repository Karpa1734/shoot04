using KanKikuchi.AudioManager;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SlothUltDomain : MonoBehaviour
{
    private GameObject _shooter;
    private string _targetTag;
    private int _baseDamagePerTick; // 基準ダメージを保持
    private float _tickInterval = 0.1f;
    private float _duration = 4.0f;
    private List<SlothMagicCircle> _linkedCircles = new List<SlothMagicCircle>();

    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private PolygonCollider2D _polyCollider;

    private float _effectSpawnTimer = 0f;
    private float _effectSpawnInterval = 0.35f;

    private bool _isDestroying = false;

    private class EffectContext
    {
        public GameObject fxObj;
        public MeshRenderer renderer;
        public Mesh mesh;
        public float elapsed;
        public float duration;
        public Vector3 startScale;
        public Vector3 endScale;
    }
    private List<EffectContext> _activeEffects = new List<EffectContext>();

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
        _baseDamagePerTick = damage;
        _linkedCircles = new List<SlothMagicCircle>(circles);

        // 🌟【スペル中持続1.5倍化】：発動者がスペルカード（VJT）展開中であれば、持続時間を1.5倍にする
        PlayerStatusManager myStatus = _shooter != null ? _shooter.GetComponent<PlayerStatusManager>() : null;
        if (myStatus == null && _shooter != null) myStatus = _shooter.GetComponentInChildren<PlayerStatusManager>();

        bool isSpellActive = (myStatus != null && myStatus.isSpellCardActive);
        _duration = isSpellActive ? duration * 1.5f : duration;

        StartCoroutine(DomainRoutine());
    }

    private IEnumerator DomainRoutine()
    {
        float elapsed = 0f;
        float tickTimer = 0f;
        _effectSpawnTimer = 0f;

        while (elapsed < _duration)
        {
            if (Mathf.Approximately(Time.timeScale, 0f)) { yield return null; continue; }

            float dt = Time.deltaTime;
            elapsed += dt;
            tickTimer += dt;
            _effectSpawnTimer += dt;

            UpdateDomainShape();
            CheckBulletsInsideDomain();

            if (_effectSpawnTimer >= _effectSpawnInterval)
            {
                if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.STARS, 0.5f);
                _effectSpawnTimer = 0f;
                SpawnShrinkingEffectMesh();
            }

            if (tickTimer >= _tickInterval)
            {
                tickTimer = 0f;
                ApplyDomainDamage();
            }

            yield return null;
        }

        yield return StartCoroutine(FadeOutAndDestroyDomain());
    }

    private IEnumerator FadeOutAndDestroyDomain()
    {
        if (_isDestroying) yield break;
        _isDestroying = true;

        if (_polyCollider != null) _polyCollider.enabled = false;

        float fadeElapsed = 0f;
        float fadeDuration = 0.5f;
        Color startColor = _meshRenderer != null ? _meshRenderer.material.color : new Color(0.2f, 0.6f, 1.0f, 0.5f);

        List<float> fxStartAlphas = new List<float>();
        foreach (var fx in _activeEffects)
        {
            if (fx.renderer != null && fx.renderer.material != null)
                fxStartAlphas.Add(fx.renderer.material.color.a);
            else
                fxStartAlphas.Add(0f);
        }

        while (fadeElapsed < fadeDuration)
        {
            if (Mathf.Approximately(Time.timeScale, 0f)) { yield return null; continue; }

            fadeElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(fadeElapsed / fadeDuration);

            if (_meshRenderer != null && _meshRenderer.material != null)
            {
                float alpha = Mathf.Lerp(startColor.a, 0f, t);
                _meshRenderer.material.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            }

            for (int i = 0; i < _activeEffects.Count; i++)
            {
                var fx = _activeEffects[i];
                if (fx.fxObj != null && fx.renderer != null && fx.renderer.material != null)
                {
                    float currentA = Mathf.Lerp(fxStartAlphas[i], 0f, t);
                    Color c = fx.renderer.material.color;
                    fx.renderer.material.color = new Color(c.r, c.g, c.b, currentA);
                }
            }

            yield return null;
        }

        foreach (var fx in _activeEffects)
        {
            if (fx.fxObj != null) Destroy(fx.fxObj);
            if (fx.mesh != null) Destroy(fx.mesh);
        }
        _activeEffects.Clear();

        Destroy(gameObject);
    }

    private void UpdateDomainShape()
    {
        if (_isDestroying) return;

        _linkedCircles.RemoveAll(c => c == null);
        if (_linkedCircles.Count < 3)
        {
            StartCoroutine(FadeOutAndDestroyDomain());
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
            localPoints[i] = rawPoint * 0.92f;
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

        if (!_isDestroying)
        {
            _meshRenderer.material.color = new Color(0.2f, 0.6f, 1.0f, 0.5f);
        }
    }

    private void SpawnShrinkingEffectMesh()
    {
        if (_isDestroying || _meshFilter == null || _meshFilter.sharedMesh == null) return;

        GameObject fxObj = new GameObject("UltDomainShrinkFX");
        fxObj.transform.position = transform.position;
        fxObj.transform.rotation = transform.rotation;
        fxObj.transform.localScale = Vector3.one * 3.5f;

        MeshFilter fxFilter = fxObj.AddComponent<MeshFilter>();
        MeshRenderer fxRenderer = fxObj.AddComponent<MeshRenderer>();

        Mesh clonedMesh = Instantiate(_meshFilter.sharedMesh);
        fxFilter.mesh = clonedMesh;

        fxRenderer.material = new Material(_meshRenderer.sharedMaterial);
        fxRenderer.sortingOrder = 999;

        EffectContext context = new EffectContext
        {
            fxObj = fxObj,
            renderer = fxRenderer,
            mesh = clonedMesh,
            elapsed = 0f,
            duration = 3.0f,
            startScale = Vector3.one * 3.5f,
            endScale = Vector3.zero
        };
        _activeEffects.Add(context);

        StartCoroutine(ShrinkEffectRoutine(context));
    }

    private IEnumerator ShrinkEffectRoutine(EffectContext context)
    {
        while (context.elapsed < context.duration && context.fxObj != null)
        {
            if (Mathf.Approximately(Time.timeScale, 0f)) { yield return null; continue; }

            context.elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(context.elapsed / context.duration);

            float smoothT = 1f - Mathf.Pow(1f - t, 3f);

            if (context.fxObj != null)
            {
                context.fxObj.transform.localScale = Vector3.Lerp(context.startScale, context.endScale, smoothT);
            }

            if (!_isDestroying && context.renderer != null && context.renderer.material != null)
            {
                float alpha = Mathf.Sin(t * Mathf.PI) * 0.5f;
                Color c = context.renderer.material.color;
                context.renderer.material.color = new Color(0.4f, 0.7f, 1.0f, alpha);
            }

            yield return null;
        }

        if (context.fxObj != null)
        {
            _activeEffects.Remove(context);
            Destroy(context.fxObj);
        }
        if (context.mesh != null)
        {
            Destroy(context.mesh);
        }
    }

    private void CheckBulletsInsideDomain()
    {
        if (_isDestroying || _polyCollider == null) return;

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

    private void ApplyDomainDamage()
    {
        if (_isDestroying) return;

        // 🌟【スペル中ダメージ2倍化】：発動者がスペルカード展開中であればダメージを2倍にする
        PlayerStatusManager myStatus = _shooter != null ? _shooter.GetComponent<PlayerStatusManager>() : null;
        if (myStatus == null && _shooter != null) myStatus = _shooter.GetComponentInChildren<PlayerStatusManager>();

        bool isSpellActive = (myStatus != null && myStatus.isSpellCardActive);
        int finalDamage = isSpellActive ? _baseDamagePerTick * 2 : _baseDamagePerTick;

        foreach (var p in PlayerMove.AllPlayers)
        {
            if (p == null || p.gameObject == _shooter) continue;

            if (p.CompareTag(_targetTag) || _shooter != p.gameObject)
            {
                PlayerStatusManager statusMgr = p.GetComponent<PlayerStatusManager>();
                if (statusMgr == null) statusMgr = p.GetComponentInChildren<PlayerStatusManager>();

                PlayerHitHandler hitHandler = p.GetComponent<PlayerHitHandler>();
                if (hitHandler == null) hitHandler = p.GetComponentInChildren<PlayerHitHandler>();

                if (statusMgr != null && _polyCollider.OverlapPoint(p.transform.position))
                {
                    if (hitHandler != null)
                    {
                        hitHandler.OnHitIgnoreInvincibility(finalDamage);
                    }
                    else
                    {
                        statusMgr.ApplyDamage(finalDamage);
                    }
                }
            }
        }
    }
}