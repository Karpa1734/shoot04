using UnityEngine;

public class BackgroundController : MonoBehaviour
{
    [System.Serializable]
    public class ScrollingLayer
    {
        public MeshRenderer renderer;
        public Vector2 scrollSpeed;
        [HideInInspector] public Vector2 currentOffset;
    }

    [Header("Normal Background Layers")]
    public ScrollingLayer ground;
    public ScrollingLayer fog1;
    public ScrollingLayer fog2;
    public ScrollingLayer clouds;
    public ScrollingLayer clouds2;

    [Header("Spell Card Settings")]
    public GameObject spellBGGroup; // スペル背景の親オブジェクト
    public CanvasGroup spellCanvasGroup; // フェード用
    public bool isSpellActive;
    public float fadeSpeed = 4f;

    [Header("Camera Sway")]
    public Transform cameraTransform;
    private float timer = 0f;

    void Update()
    {
        timer += Time.deltaTime;

        // 1. UVスクロール処理 (dnhの pos += 2 相当)
        UpdateLayerOffset(ground);
        UpdateLayerOffset(fog1);
        UpdateLayerOffset(fog2);
        UpdateLayerOffset(clouds);
        UpdateLayerOffset(clouds2);

        // 2. カメラの揺れ (dnhの SetCameraYaw/Roll 相当)
        if (cameraTransform != null)
        {
            float yaw = 10f * Mathf.Sin(timer * 2f); // 係数はdnhのccに合わせて調整
            float roll = -25f * Mathf.Sin(timer * 2f);
            cameraTransform.localRotation = Quaternion.Euler(15f, yaw, roll);
        }

        // 3. スペルカード背景のフェード (dnhの alpha += 4 相当)
        if (spellCanvasGroup != null)
        {
            float targetAlpha = isSpellActive ? 1f : 0f;
            spellCanvasGroup.alpha = Mathf.MoveTowards(spellCanvasGroup.alpha, targetAlpha, fadeSpeed * Time.deltaTime);
            spellBGGroup.SetActive(spellCanvasGroup.alpha > 0);
        }
    }

    void UpdateLayerOffset(ScrollingLayer layer)
    {
        if (layer.renderer == null) return;
        layer.currentOffset += layer.scrollSpeed * Time.deltaTime;
        layer.renderer.material.mainTextureOffset = layer.currentOffset;
    }
}