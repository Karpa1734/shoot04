using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UltimateGaugeUI : MonoBehaviour
{
    [SerializeField] private Slider _slider;
    [SerializeField] private TextMeshProUGUI _stockText;

    private PlayerMove _playerMove;

    public void Initialize(PlayerMove playerMove)
    {
        _playerMove = playerMove;
        if (_slider != null) _slider.maxValue = 1.0f; // バーは常に 0〜1 で制御
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
        if (_slider != null) _slider.value = fillAmount;
    }
}