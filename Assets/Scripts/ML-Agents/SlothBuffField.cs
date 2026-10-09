using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SlothBuffField : MonoBehaviour
{
    [Header("Field Settings")]
    [SerializeField] private float _duration = 3.0f;

    [Header("Visual Settings")]
    [SerializeField] private float _fieldRotationSpeed = 45f;

    private GameObject _owner;
    private PlayerStatusManager _statusManager;
    private bool _isBuffActive = false;
    private float _timer = 0f;
    private bool _isInitialized = false;
    private Vector3 _originalFieldScale;

    // 🌟 接触中であるかを保持するフラグ
    private bool _isCollidingWithOwner = false;

    void Awake()
    {
        _originalFieldScale = transform.localScale;
        if (_originalFieldScale == Vector3.zero) _originalFieldScale = Vector3.one;

        // 🌟【修正】：コライダーを削除せず、トリガーとして機能させる
        PolygonCollider2D poly = GetComponent<PolygonCollider2D>();
        if (poly != null)
        {
            poly.isTrigger = true;
        }
        else
        {
            // もしポリゴンコライダーがなければ、サークルコライダー等を付与するか設定してください
            CircleCollider2D circle = gameObject.AddComponent<CircleCollider2D>();
            circle.isTrigger = true;
            circle.radius = 1.5f; // 必要に応じて調整
        }
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

        bool isSpellActive = (_statusManager != null && _statusManager.isSpellCardActive);
        if (isSpellActive)
        {
            _originalFieldScale *= 1.5f;
        }

        _timer = 0f;
        _isInitialized = true;
        transform.localScale = Vector3.zero;

        // 💡 以前はここで即座にONにしていましたが、接触判定に変更するため最初はOFFまたは接触時のみにします
        _isBuffActive = false;
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

        // 🌟【修正の核心】：コライダー接触中（_isCollidingWithOwner）であるかに基づいてバフを制御
        if (_statusManager != null)
        {
            if (_isCollidingWithOwner && !_isBuffActive)
            {
                _isBuffActive = true;
                _statusManager.SetSlothUltBuffActive(true);
            }
            else if (!_isCollidingWithOwner && _isBuffActive)
            {
                _isBuffActive = false;
                _statusManager.SetSlothUltBuffActive(false);
            }
        }
    }

    // =========================================================================
    // 🛡️ 2Dコライダーの接触判定コールバック
    // =========================================================================
    void OnTriggerEnter2D(Collider2D collision)
    {
        if (_owner != null && (collision.gameObject == _owner || collision.transform.root == _owner.transform.root))
        {
            _isCollidingWithOwner = true;
        }
    }

    void OnTriggerExit2D(Collider2D collision)
    {
        if (_owner != null && (collision.gameObject == _owner || collision.transform.root == _owner.transform.root))
        {
            _isCollidingWithOwner = false;
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