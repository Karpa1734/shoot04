using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SkillCooldownUI : MonoBehaviour
{
    [Header("UI References")]
    public Image iconImage;        // スキルアイコン
    public Image fillImage;        // クールタイム用の塗りつぶしImage
    public TextMeshProUGUI timerText;

    [Header("Transparency Settings")]
    [Range(0, 1)]
    public float cooldownFillAlpha = 0.5f; // ★ 半透明の度合い（デフォルトは50%）

    public void SetSkillIcon(Sprite sprite)
    {
        if (iconImage != null)
        {
            iconImage.sprite = sprite;
        }
    }

    public void UpdateCooldown(float currentTimer, float maxCooldown)
    {
        if (currentTimer > 0)
        {
            // 残り時間の表示
            timerText.gameObject.SetActive(true);
            timerText.text = currentTimer.ToString("F1") + "s";

            // Fill量を計算し、半透明を適用
            if (fillImage != null && maxCooldown > 0)
            {
                fillImage.fillAmount = currentTimer / maxCooldown;

                // ★ 修正：色のアルファ値を設定した値に更新する
                Color c = fillImage.color;
                c.a = cooldownFillAlpha;
                fillImage.color = c;
            }
        }
        else
        {
            // クールタイム終了時
            timerText.gameObject.SetActive(false);
            if (fillImage != null) fillImage.fillAmount = 0;
        }
    }
}