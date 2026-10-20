using UnityEngine;

public class Blind : MonoBehaviour
{
    private Transform myTransform;

    public void InitSpot(float spotscale)
    {
        // InitSpotが呼び出されるたびにtransformを確実に取得
        myTransform = transform;

        // nullチェックを追加して安全性を高める
        if (myTransform == null)
        {
            Debug.LogError("Transform component not found!");
            return;
        }

        // スケールを安全に変更
        myTransform.localScale = Vector3.one * spotscale;
    }

    // Startメソッドは不要になるため削除
    // void Start() { }

    void Update()
    {
        // ... 他のUpdate()ロジック
    }
}