using UnityEngine;
using TMPro; // TextMeshProを使用するために必要

public class FPSCounter : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI fpsText; // 表示用のUIテキスト

    private int frameCount = 0;
    private float prevTime = 0f;

    void Start()
    {
        prevTime = Time.realtimeSinceStartup;
    }

    void Update()
    {
        frameCount++;

        // Time.realtimeSinceStartup で、ゲーム開始からの経過時間を取得
        float currentTime = Time.realtimeSinceStartup;
        float deltaTime = currentTime - prevTime;

        // 1秒経過したら更新
        if (deltaTime >= 1.0f)
        {
            float fps = frameCount / deltaTime;

            // "F1" で小数点第1位まで表示 (60.0fps形式)
            fpsText.text = fps.ToString("F1") + "fps";

            // カウンターをリセット
            frameCount = 0;
            prevTime = currentTime;
        }
    }
}