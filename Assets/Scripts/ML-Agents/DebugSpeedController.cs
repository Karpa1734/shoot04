using UnityEngine;

public class DebugSpeedController : MonoBehaviour
{
    [Header("Settings")]
    public float fastForwardSpeed = 2.0f; // 倍速の速さ

    // 現在の演出（K.O.のスローなど）を妨げないようにするためのガード
    private bool IsInNormalTime => Mathf.Approximately(Time.timeScale, 1.0f) || Time.timeScale > 1.0f;

    void Update()
    {// 🧠【強化学習適合化】：シーン内に学習中のAIがいれば、このスクリプトによるブレーキを完全無効化する
        if (FindAnyObjectByType<DanmakuAgent>() != null) 
        {
            //Time.timeScale = 5;
            //return;
        }

        // Spaceキーを押している間だけ倍速にする
        if (Input.GetKey(KeyCode.Space))
        {
            // スロー演出中（0.2fなど）でなければ倍速を適用
            if (IsInNormalTime)
            {
                Time.timeScale = fastForwardSpeed;
            }
        }
        else
        {
            // キーを離した際、倍速状態なら1.0に戻す
            if (Time.timeScale > 1.0f)
            {
                Time.timeScale = 1.0f;
            }
        }


    }
}