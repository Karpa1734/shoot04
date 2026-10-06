using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SlothBuffField : MonoBehaviour
{
    [Header("Field Settings")]
    [SerializeField] private float _duration = 3.0f;
    [Tooltip("バフが有効になる魔方陣からの距離（この範囲内に自機がいればバフ適用。0なら常時適用）")]
    [SerializeField] private float _buffRadius = 2.5f;

    [Header("Visual Settings")]
    [SerializeField] private float _fieldRotationSpeed = 45f;

    private GameObject _owner;
    private PlayerStatusManager _statusManager;
    private bool _isBuffActive = false;
    private float _timer = 0f;
    private bool _isInitialized = false;
    private Vector3 _originalFieldScale;

    void Awake()
    {
        _originalFieldScale = transform.localScale;
        if (_originalFieldScale == Vector3.zero) _originalFieldScale = Vector3.one;

        // ポリゴンコライダーは使用しないため、アタッチされていれば自動で削除または無効化します
        PolygonCollider2D poly = GetComponent<PolygonCollider2D>();
        if (poly != null) Destroy(poly);
    }

    public void Initialize(GameObject owner, float overrideDuration = -1f)
    {
        _owner = owner;
        if (_owner != null)
        {
            _statusManager = _owner.GetComponent<PlayerStatusManager>();
            if (_statusManager == null) _statusManager = _owner.GetComponentInChildren<PlayerStatusManager>();
        }

        if (overrideDuration > 0f) _duration = overrideDuration;

        _timer = 0f;
        _isInitialized = true;
        transform.localScale = Vector3.zero;

        // 生成された瞬間にバフを即座にONにする（もし「魔方陣を出している間ずっとバフ」にしたい場合）
        if (_statusManager != null)
        {
            _isBuffActive = true;
            _statusManager.SetSlothUltBuffActive(true);
        }
    }

    void FixedUpdate()
    {
        if (!_isInitialized) return;

        if (!PlayerMove.CanShoot)
        {
            CleanupAndDestroy();
            return;
        }

        _timer += Time.fixedDeltaTime;
        transform.Rotate(0f, 0f, _fieldRotationSpeed * Time.fixedDeltaTime);

        // 展開・縮小アニメーション
        if (_timer < 0.1f)
        {
            transform.localScale = Vector3.Lerp(Vector3.zero, _originalFieldScale, _timer / 0.1f);
        }
        else if (_timer >= _duration)
        {
            float t = (_timer - _duration) / 0.1f;
            transform.localScale = Vector3.Lerp(_originalFieldScale, Vector3.zero, t);
            if (_timer >= _duration + 0.1f)
            {
                CleanupAndDestroy();
                return;
            }
        }
        else
        {
            transform.localScale = _originalFieldScale;
        }

        // 💡 距離による判定（必要に応じて有効化。今回は魔方陣が展開されている間は確実にバフが乗る設計にしています）
        if (_owner != null && _statusManager != null)
        {
            float dist = Vector3.Distance(transform.position, _owner.transform.position);
            bool isNear = (dist <= _buffRadius);

            // 範囲内にいる（または常時適用）かつ、まだバフが有効でなければON
            if (isNear && !_isBuffActive)
            {
                _isBuffActive = true;
                _statusManager.SetSlothUltBuffActive(true);
            }
            else if (!isNear && _isBuffActive)
            {
                _isBuffActive = false;
                _statusManager.SetSlothUltBuffActive(false);
            }
        }
    }

    private void CleanupAndDestroy()
    {
        if (_isBuffActive && _statusManager != null)
        {
            _statusManager.SetSlothUltBuffActive(false);
            _isBuffActive = false;
        }
        _isInitialized = false;
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (_isBuffActive && _statusManager != null)
        {
            _statusManager.SetSlothUltBuffActive(false);
            _isBuffActive = false;
        }
    }
}