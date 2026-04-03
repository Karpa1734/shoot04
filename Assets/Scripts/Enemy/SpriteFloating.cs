using UnityEngine;

public class SpriteFloating : MonoBehaviour
{
    [Header("動かしたい画像（子オブジェクト）")]
    public Transform spriteTransform;

    [Header("ふわふわ設定")]
    public float height = 0.3f; // 上下の幅
    public float speed = 2.0f;  // 速さ

    void Update()
    {
        if (spriteTransform == null) return;

        // 子オブジェクトの「ローカル座標」だけを書き換える
        // これにより、親の座標（弾の発射位置）は一切動きません
        float newY = Mathf.Sin(Time.time * speed) * height;
        spriteTransform.localPosition = new Vector3(0, newY, 0);
    }
}