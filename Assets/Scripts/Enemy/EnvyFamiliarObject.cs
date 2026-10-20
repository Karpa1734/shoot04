using KanKikuchi.AudioManager;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnvyFamiliarObject : MonoBehaviour
{
    private Transform _owner;
    private PlayerDanmakuEmitter _ownerEmitter;
    private string _targetTag;
    private int _ownerId;

    private List<DanmakuBullet> _spawnedBullets = new List<DanmakuBullet>();
    private Transform _targetOpponent;

    [Header("Behavior Settings")]
    [SerializeField] private float _duration = 8.0f;
    [SerializeField] private float _dropInterval = 0.05f;
    [SerializeField] private float _moveSpeed = 3.0f;

    [Tooltip("寿命の終盤で、弾を出さない猶予時間（秒）")]
    [SerializeField] private float _noFireWindowBeforeEnd = 1.0f;

    private List<BulletData> _bulletDataList = new List<BulletData>();

    [Tooltip("true にすると、設置ごとにリスト内からランダム選択")]
    [SerializeField] private bool _useRandomBulletType = true;

    [SerializeField] private int _selectedBulletIndex = 0;

    private BulletData _fallbackDefaultData;
    private BulletData[] _familiarMultiBulletDatas;

    public void Initialize(
        Transform owner,
        Transform target,
        BulletData defaultData,
        BulletData[] familiarMultiBulletDatas,
        PlayerDanmakuEmitter emitter,
        string targetTag,
        int ownerId)
    {
        _owner = owner;
        _ownerEmitter = emitter;
        _targetTag = targetTag;
        _ownerId = ownerId;
        _targetOpponent = target;
        _fallbackDefaultData = defaultData;
        _familiarMultiBulletDatas = familiarMultiBulletDatas;

        if (_ownerEmitter == null)
        {
            Debug.LogWarning("EnvyFamiliarObject: _ownerEmitter が null です。使い魔を破棄します。");
            Destroy(gameObject);
            return;
        }

        StartCoroutine(FamiliarRoutine());
    }

    private BulletData GetNextBulletData()
    {
        List<BulletData> validList = new List<BulletData>();

        if (_familiarMultiBulletDatas != null)
        {
            foreach (var d in _familiarMultiBulletDatas)
            {
                if (d != null) validList.Add(d);
            }
        }

        if (_bulletDataList != null)
        {
            foreach (var d in _bulletDataList)
            {
                if (d != null) validList.Add(d);
            }
        }

        if (validList.Count == 0)
            return _fallbackDefaultData;

        int index = _useRandomBulletType ? Random.Range(0, validList.Count) : Mathf.Clamp(_selectedBulletIndex, 0, validList.Count - 1);

        if (!_useRandomBulletType)
            _selectedBulletIndex = (index + 1) % validList.Count;

        return validList[index];
    }

    private IEnumerator FamiliarRoutine()
    {
        float elapsed = 0f;
        float dropTimer = 0f;

        while (elapsed < _duration)
        {
            while (Mathf.Approximately(Time.timeScale, 0f)) yield return null;
            if (!PlayerMove.CanShoot) break;

            elapsed += Time.deltaTime;
            dropTimer += Time.deltaTime;

            if (_targetOpponent != null)
            {
                Vector3 toTarget = _targetOpponent.position - transform.position;
                float distance = toTarget.magnitude;

                if (distance > 0.001f)
                {
                    Vector3 dir = toTarget.normalized;

                    // 距離が遠いほど早く接近（最大2倍）
                    // 0〜12unit を 1〜2倍の幅に変換
                    float distanceRatio = Mathf.InverseLerp(0f, 12f, distance);
                    float speedMultiplier = Mathf.Lerp(1f, 2f, distanceRatio);

                    transform.position += dir * _moveSpeed * speedMultiplier * Time.deltaTime;
                }
            }

            // ここが追加ポイント
            // 寿命の終盤（例: 30フレーム前）になったら射撃をストップ
            float remainingTime = _duration - elapsed;
            if (remainingTime <= _noFireWindowBeforeEnd)
            {
                // 終盤では弾を出さない
                yield return null;
                continue;
            }

            if (dropTimer >= _dropInterval)
            {
                dropTimer = 0f;

                if (SEManager.Instance != null)
                {
                    SEManager.Instance.Play(SEPath.SHOT1, 0.4f);
                }

                SpawnFamiliarBullet();
            }

            yield return null;
        }

        foreach (var b in _spawnedBullets)
        {
            if (b != null)
            {
                b.Deactivate(true, force: true);
            }
        }

        Destroy(gameObject);
    }

    private void SpawnFamiliarBullet()
    {
        BulletData targetBulletData = GetNextBulletData();

        if (targetBulletData == null || targetBulletData.bulletPrefab == null || _ownerEmitter == null)
        {
            Debug.LogWarning("EnvyFamiliarObject: 有効な BulletData が見つかりませんでした。");
            return;
        }

        float randomOffsetX = Random.Range(-0.8f, 0.8f);
        float randomOffsetY = Random.Range(-0.8f, 0.8f);
        Vector3 spawnPos = transform.position + new Vector3(randomOffsetX, randomOffsetY, 0f);

        if (targetBulletData.delaySprite != null)
        {
            GameObject delayEffectObj = new GameObject("Familiar_DelayEffect");
            delayEffectObj.transform.position = spawnPos;
            delayEffectObj.transform.localScale = Vector3.one * 0.8f;

            SpriteRenderer dSr = delayEffectObj.AddComponent<SpriteRenderer>();
            dSr.sprite = targetBulletData.delaySprite;
            dSr.sortingOrder = 5000;

            ShotEffect effectLogic = delayEffectObj.AddComponent<ShotEffect>();
            if (effectLogic != null)
            {
                effectLogic.StartCoroutine(effectLogic.PlayDelay(0.6f, targetBulletData.delaySprite, 1.5f));
            }
        }

        StartCoroutine(SpawnRealBulletWithDelay(targetBulletData, spawnPos, 0.6f));
    }

    private IEnumerator SpawnRealBulletWithDelay(BulletData bulletData, Vector3 spawnPos, float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);

        if (bulletData == null || _ownerEmitter == null) yield break;

        DanmakuBullet spawnedBullet = _ownerEmitter.CreateShot(
            data: bulletData,
            pos: spawnPos,
            speed: 0f,
            angle: Random.Range(0f, 360f),
            delay: 1.0f,
            isConverge: false,
            accel: 0f,
            maxSpeed: 3.0f,
            customMaterial: null,
            customScale: 1.0f,
            isIndestructible: false
        );

        if (spawnedBullet != null)
        {
            _spawnedBullets.Add(spawnedBullet);
        }
    }
}