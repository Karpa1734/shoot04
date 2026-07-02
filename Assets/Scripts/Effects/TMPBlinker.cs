using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 指定されたTextMeshProUGUIを、fontMaterial を用いて【確定黒縁】を維持したまま
/// ゆっくりと半透明往復点滅させるUI演出スクリプト。
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class TMPBlinker : MonoBehaviour
{
    [Header("✨ 点滅設定")]
    [Tooltip("点滅の速さ（数値が大きいほど高速に往復します）")]
    [SerializeField] private float _blinkSpeed = 2.5f;

    [Tooltip("最も薄くなった（透明になった）瞬間のアルファ値 (0.0 ～ 1.0)")]
    [SerializeField] private float _minAlpha = 0.15f;

    [Tooltip("最も濃くなった瞬間のアルファ値 (0.0 ～ 1.0)")]
    [SerializeField] private float _maxAlpha = 0.6f;

    private TextMeshProUGUI _tmpText;
    private Coroutine _blinkCoroutine;
    private Material _uniqueMaterial;
    private Color _baseFaceColor;

    void Awake()
    {
        _tmpText = GetComponent<TextMeshProUGUI>();
        if (_tmpText != null)
        {
            _tmpText.text = "DemoPLAY";

            // 🎯【fontMaterial による縁色のコントロール】：
            // fontMaterial にアクセスすることで、このオブジェクト専用にマテリアルを複製（インスタンス化）します。
            _uniqueMaterial = _tmpText.fontMaterial;

            if (_uniqueMaterial != null)
            {
                // 💡 1. 縁の色（Outline Color）を強制的に【不透明な黒】に染め上げる
                _uniqueMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 1f));

                // 💡 2. 縁の太さ（Outline Width）をしっかり認識できる太さ（0.3f）に固定
                _uniqueMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.3f);
            }

            // 元の文字のベースカラー（中身の色）をキープ
            _baseFaceColor = _tmpText.color;
        }
    }

    void OnEnable()
    {
        if (_blinkCoroutine != null) StopCoroutine(_blinkCoroutine);
        _blinkCoroutine = StartCoroutine(ExecuteBlinkRoutine());
    }

    void OnDisable()
    {
        if (_blinkCoroutine != null)
        {
            StopCoroutine(_blinkCoroutine);
            _blinkCoroutine = null;
        }
    }

    private IEnumerator ExecuteBlinkRoutine()
    {
        float timeAccumulator = 0f;

        while (true)
        {
            timeAccumulator += Time.unscaledDeltaTime * _blinkSpeed;

            float rawSin = Mathf.Sin(timeAccumulator);
            float normalizedT = (rawSin + 1f) * 0.5f;
            float targetAlpha = Mathf.Lerp(_minAlpha, _maxAlpha, normalizedT);

            if (_tmpText != null)
            {
                // 🎯 縁（アウトライン）の黒をくっきり残すため、TMP全体のAlphaをいじるのではなく、
                //    文字の「中身の色（Face Color）」の透明度だけを狙い撃ちでフェードさせます。
                Color faceColor = _baseFaceColor;
                faceColor.a = targetAlpha;
                _tmpText.color = faceColor;
            }

            yield return null;
        }
    }
}