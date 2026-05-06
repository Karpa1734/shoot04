// --- EnergyGaugeUI.cs 修正版 ---

using UnityEngine;
using UnityEngine.UI;

public class EnergyGaugeUI : MonoBehaviour
{
    [SerializeField] private Slider _energySlider; // シンプルなSlider1本のみ[cite: 10]

    private PlayerMove _playerMove;

    public void Initialize(PlayerMove playerMove)
    {
        _playerMove = playerMove;
        if (_energySlider != null)
        {
            _energySlider.maxValue = playerMove.maxEnergy; 
            _energySlider.value = playerMove.currentEnergy;
        }
    }

    void Update()
    {
        if (_playerMove != null && _energySlider != null)
        {
            // 現在の値をダイレクトに反映（ Lerp などを使わず即座に動かすのが今の意図に合います）
            _energySlider.value = _playerMove.currentEnergy; 
        }
    }
}