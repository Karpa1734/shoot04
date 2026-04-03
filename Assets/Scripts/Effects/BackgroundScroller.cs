using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class BackgroundScroller : MonoBehaviour
{
    [Header("Scroll Settings")]
    [SerializeField] private Vector2 scrollSpeed = new Vector2(0.1f, 0.1f);

    private RawImage rawImage;
    private Vector2 currentOffset = Vector2.zero;

    void Awake()
    {
        rawImage = GetComponent<RawImage>();
    }

    void Update()
    {
        // 1. フレームごとにオフセットを加算
        currentOffset += scrollSpeed * Time.unscaledDeltaTime;

        // 2. UV座標（uvRect）をずらすことで、画像が流れているように見せる
        rawImage.uvRect = new Rect(currentOffset, Vector2.one);
    }
}