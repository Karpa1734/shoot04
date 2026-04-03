using UnityEngine;

public class FrameRateManager : MonoBehaviour
{
    [SerializeField] private int targetFPS = 60;

    void Awake()
    {
        // V-Sync（垂直同期）をオフにする
        // これが1以上だと、モニタの周波数に強制同期されてしまいます
        QualitySettings.vSyncCount = 0;

        // 目標のフレームレートを設定
        Application.targetFrameRate = targetFPS;
    }
}