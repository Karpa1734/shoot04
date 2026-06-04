using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UltimateGaugeUI : MonoBehaviour
{
    [SerializeField] private Slider _slider;
    [SerializeField] private TextMeshProUGUI _stockText;

    private PlayerMove _playerMove;
    // 🌟【新規追加】：同じオブジェクト内にある発光コントローラーをキャッシュする枠
    private UltimateGaugeGlowController _glowController;

    public void Initialize(PlayerMove playerMove)
    {
        _playerMove = playerMove;
        if (_slider != null) _slider.maxValue = 1.0f; // バーは常に 0〜1 で制御

        // 🌟 コンポーネントを安全に事前取得
        _glowController = GetComponent<UltimateGaugeGlowController>();
    }

    void Update()
    {
        if (_playerMove == null) return;

        float current = _playerMove.ultimateEnergy;

        // 1. ストック数の計算 (0, 1, 2, 3)
        int stocks = Mathf.FloorToInt(current / 100f);

        // 2. スライダーの表示量計算
        float fillAmount = (current % 100f) / 100f;

        // ★ 300（最大値）の時の特殊処理
        if (current >= 300f)
        {
            stocks = 3;
            fillAmount = 1.0f; // ゲージを0に戻さず満タンにする
        }

        // UIへの反映
        if (_stockText != null) _stockText.text = stocks.ToString();

        if (_slider != null)
        {
            _slider.value = fillAmount; // 🚨 ここでスライダーが内部の色をリセットしてしまう
        }

        // =========================================================================
        // 🌟【最重要リファクタリング】：Sliderの値書き換えが「完全に終わった直後」に
        // 🌟 発光命令を明示的に呼び出すことで、Sliderの自動色リセットを力技で100%上書き突破します！
        // =========================================================================
        if (_glowController == null)
        {
            _glowController = GetComponent<UltimateGaugeGlowController>();
        }

        if (_glowController != null && _glowController.enabled)
        {
            _glowController.ManualGlowUpdate();
        }
    }
}