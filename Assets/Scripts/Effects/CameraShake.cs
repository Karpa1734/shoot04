using UnityEngine;
using System.Collections;

public class CameraShake : MonoBehaviour
{
    // 外部から手軽にアクセスできるようにシングルトンにする
    public static CameraShake Instance { get; private set; }

    private Vector3 originalPos;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        // カメラの初期位置を記憶
        originalPos = transform.localPosition;
    }

    // 外部から呼ばれる、揺れを開始させる関数
    // duration: 揺れる時間（秒）, magnitude: 揺れの強さ
    public void Shake(float duration, float magnitude)
    {
        // すべての揺れコルーチンを一度止めてから新しいのを開始
        StopAllCoroutines();
        StartCoroutine(DoShake(duration, magnitude));
    }

    // 実際に揺らすコルーチン
    IEnumerator DoShake(float duration, float magnitude)
    {
        float elapsed = 0.0f;

        while (elapsed < duration)
        {
            // ランダムなオフセットを計算
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;

            // カメラの位置をずらす (Z軸はそのまま)
            transform.localPosition = new Vector3(x, y, originalPos.z);

            elapsed += Time.deltaTime;
            yield return null; // 1フレーム待機
        }

        // 揺れ終わったら元の位置に確実に戻す
        transform.localPosition = originalPos;
    }
}