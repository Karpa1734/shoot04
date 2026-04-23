using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SkillCooldownUI : MonoBehaviour
{
    [Header("UI References")]
    public Image fillImage;        // Fill MethodをRadialに設定したImage
    public TextMeshProUGUI timerText; // カウントダウン用のTMP

    public void UpdateCooldown(float currentTimer, float maxCooldown)
    {
        if (currentTimer > 0)
        {
            // 残り時間の表示 (例: 1.5s)
            timerText.gameObject.SetActive(true);
            timerText.text = currentTimer.ToString("F1") + "s";

            // Fill量を計算 (1.0 -> 0)
            if (fillImage != null && maxCooldown > 0)
            {
                fillImage.fillAmount = currentTimer / maxCooldown;
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