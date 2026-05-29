using UnityEngine;

/// <summary>
/// 聖少女領域（VJT）発動時に、当たり判定の拡大に合わせて拡縮するバリアエフェクト
/// </summary>
public class SpellBarrierEffect : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Scale Settings")]
    [SerializeField] private float targetScale = 2.5f;       // 🌟判定拡大(2.5倍)に合わせた目標スケール
    [SerializeField] private float expandSpeed = 5.0f;       // 出現時の広がるスピード
    [SerializeField] private float shrinkSpeed = 8.0f;       // 終了時の縮むスピード

    [Header("Alpha Settings")]
    [SerializeField] private float maxAlpha = 0.6f;          // バリアの最大不透明度（まぶしすぎない程度に）
    [SerializeField] private float alphaFadeSpeed = 4.0f;

    private float _currentScale = 0f;
    private float _currentAlpha = 0f;
    private bool _isActive = false;

    void Start()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        // 🌟初期状態は大きさ・透明度ともに 0
        transform.localScale = Vector3.zero;
        if (spriteRenderer != null)
        {
            spriteRenderer.color = new Color(1, 1, 1, 0);
        }
    }

    void Update()
    {
        if (Time.timeScale <= 0) return;

        // 🌟目標値の決定
        float targetS = _isActive ? targetScale : 0f;
        float targetA = _isActive ? maxAlpha : 0f;
        float sSpeed = _isActive ? expandSpeed : shrinkSpeed;

        // 🌟サイズとアルファ（透明度）を滑らかに補間
        _currentScale = Mathf.MoveTowards(_currentScale, targetS, sSpeed * Time.deltaTime);
        _currentAlpha = Mathf.MoveTowards(_currentAlpha, targetA, alphaFadeSpeed * Time.deltaTime);

        // 🌟トランスフォームとカラーに反映
        transform.localScale = new Vector3(_currentScale, _currentScale, 1f);

        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = _currentAlpha;
            spriteRenderer.color = c;
        }

        // 完全に縮みきっていて、非アクティブ状態なら描画オブジェクトを非表示にして負荷軽減
        if (!_isActive && _currentScale <= 0f)
        {
            spriteRenderer.enabled = false;
        }
    }

    /// <summary>
    /// バリアの展開・収縮を外部（PlayerStatusManager）から切り替える窓口
    /// </summary>
    public void SetBarrierActive(bool active)
    {
        _isActive = active;
        if (_isActive && spriteRenderer != null)
        {
            spriteRenderer.enabled = true; // 展開時は即座に描画をONにする
        }
    }
}