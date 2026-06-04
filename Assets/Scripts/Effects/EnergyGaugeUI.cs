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
        // 🌟 EnergyGaugeUI.cs のUpdate、または表示更新メソッド内の最下部に組み込む処理
        // ※内部で保持している「_playerMove」や「_statusManager」から焼き切れフラグを覗き見します。

        // 🌟 EnergyGaugeUI.cs 内の色彩制御の完全版
        if (this.GetComponent<Slider>() != null)
        {
            Slider energySlider = this.GetComponent<Slider>();
            Image fillImage = energySlider.fillRect != null ? energySlider.fillRect.GetComponent<Image>() : null;

            if (fillImage != null)
            {
                var status = _playerMove != null ? _playerMove.GetComponent<PlayerStatusManager>() : null;

                if (status != null)
                {
                    if (status.isSpellCardActive)
                    {
                        // 🌟 1. 領域展開中（バフ状態）：コストゲージを限界突破の「覚醒ネオンシアン（光輝ブルー）」へ！
                        fillImage.color = new Color(0.8f, 0.9f, 1.0f, 1.0f);
                    }
                    else if (status.isOverheated)
                    {
                        // 🚨 2. 術式焼き切れ中（デバフ状態）：警告の「メタリックオレンジ」へ
                        fillImage.color = new Color(0.2f, 0.5f, 1.0f, 1.0f);
                    }
                    else
                    {
                        // 🟢 3. 通常時：本来の「クリーンなスタンダード水色」へ安全復元
                        fillImage.color = new Color(0.5f, 0.8f, 1.0f, 1.0f);
                    }
                }
            }
        }
    }
}