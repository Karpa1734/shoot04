using UnityEngine;
using UnityEngine.UI;

public class VJTSpellBackgroundManager2D : MonoBehaviour
{
    public static VJTSpellBackgroundManager2D Instance;

    [System.Serializable]
    public class SpellLayer2D
    {
        public RawImage rawImage; // 2D画像スプライト

        [HideInInspector] public Vector2 runtimeScrollSpeed;
        [HideInInspector] public float runtimeRotateSpeed;
        [HideInInspector] public Vector2 currentOffset;
        [HideInInspector] public float maxAlpha;              // スクリプト側から個別に設定されるアルファ上限
    }

    [Header("2D Spell Background Layers (UI RawImage Version)")]
    [Tooltip("Layer 0 (土台背景) は Element 0、Layer 1 (加算上画像) は Element 1 に登録してください")]
    public SpellLayer2D[] spellLayers;

    [Header("Fade Settings")]
    public GameObject spellBGGroup;
    public float fadeSpeed = 4f;

    [Header("Camera Sway Settings")]
    public Transform mainCameraTransform;
    public bool useCameraSway = true;
    public float swaySpeed = 2f;
    public float maxYaw = 8f;
    public float maxRoll = -20f;

    private bool isSpellActive = false;
    private float timer = 0f;
    private float currentAlpha = 0f;
    private Quaternion originalCameraRotation;

    // アニメーション制御用フラグ
    private bool currentBaseScroll = false;
    private bool currentAdditiveRotate = true;
    private bool currentAdditiveScroll = true;

    private Texture defaultBaseTexture;
    private Texture defaultAdditiveTexture;
    private bool isDefaultTexturesCached = false;

    void Awake()
    {
        Instance = this;
        if (spellBGGroup != null) spellBGGroup.SetActive(false);
        if (mainCameraTransform != null) originalCameraRotation = mainCameraTransform.localRotation;

        // 🌟【最重要リファクタリング】：インスペクターからの不安定な自動取得を廃止し、
        // 🌟 スクリプト側から確実なアルファ上限値をダイレクトにセット！
        SetupHardcodedMaxAlpha();

        SetLayersAlpha(0f);
        CacheDefaultTextures();
    }

    /// <summary>
    /// 🌟【新規上書き】：土台と加算レイヤーのアルファ上限値をスクリプト側から完全強制固定
    /// </summary>
    private void SetupHardcodedMaxAlpha()
    {
        // --- 🔷 Layer 0 (土台のステージ背景背景) ---
        if (spellLayers.Length > 0)
        {
            // 土台背景は後ろを隠すために「しっかり不透明（1.0f = 255）」に固定
            spellLayers[0].maxAlpha = 1.0f;
        }

        // --- 🔶 Layer 1 (上にかぶせる加算魔法陣) ---
        if (spellLayers.Length > 1)
        {
            // 🚨 高橋さん指定仕様：インスペクターの「32」をデジタル数値に完全一発変換！
            // 32f / 255f = 約 0.125f を上限として強制ロックします。
            spellLayers[1].maxAlpha = 64f / 255f;
        }
    }

    private void CacheDefaultTextures()
    {
        if (isDefaultTexturesCached) return;
        if (spellLayers.Length > 0 && spellLayers[0].rawImage != null) defaultBaseTexture = spellLayers[0].rawImage.texture;
        if (spellLayers.Length > 1 && spellLayers[1].rawImage != null) defaultAdditiveTexture = spellLayers[1].rawImage.texture;
        isDefaultTexturesCached = true;
    }

    public void SetSpellBackgroundActive(bool active, PlayerSkillData charData = null)
    {
        isSpellActive = active;
        if (active)
        {
            timer = 0f;
            CacheDefaultTextures();

            if (charData != null)
            {
                currentBaseScroll = charData.isBaseScrollActive;
                currentAdditiveRotate = charData.isAdditiveRotateActive;
                currentAdditiveScroll = charData.isAdditiveScrollActive;

                if (spellLayers.Length > 0) spellLayers[0].runtimeScrollSpeed = charData.baseScrollSpeed;
                if (spellLayers.Length > 1)
                {
                    spellLayers[1].runtimeRotateSpeed = charData.additiveRotateSpeed;
                    spellLayers[1].runtimeScrollSpeed = charData.additiveScrollSpeed;
                }

                if (spellLayers.Length > 0 && spellLayers[0].rawImage != null)
                {
                    spellLayers[0].rawImage.texture = (charData.characterSpellBGBase != null) ? charData.characterSpellBGBase.texture : defaultBaseTexture;
                }
                if (spellLayers.Length > 1 && spellLayers[1].rawImage != null)
                {
                    spellLayers[1].rawImage.texture = (charData.characterSpellBGAdditive != null) ? charData.characterSpellBGAdditive.texture : defaultAdditiveTexture;
                }
            }
            else
            {
                currentBaseScroll = false;
                currentAdditiveRotate = true;
                currentAdditiveScroll = true;

                if (spellLayers.Length > 0)
                {
                    spellLayers[0].runtimeScrollSpeed = new Vector2(0f, -0.4f);
                    if (spellLayers[0].rawImage != null) spellLayers[0].rawImage.texture = defaultBaseTexture;
                }
                if (spellLayers.Length > 1)
                {
                    spellLayers[1].runtimeRotateSpeed = 25f;
                    spellLayers[1].runtimeScrollSpeed = new Vector2(0.2f, 0.2f);
                    if (spellLayers[1].rawImage != null) spellLayers[1].rawImage.texture = defaultAdditiveTexture;
                }
            }
        }
    }

    void Update()
    {
        float targetAlpha = isSpellActive ? 1f : 0f;
        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);

        if (spellBGGroup != null) spellBGGroup.SetActive(currentAlpha > 0f);
        SetLayersAlpha(currentAlpha);

        if (!isSpellActive && currentAlpha <= 0f)
        {
            if (mainCameraTransform != null && mainCameraTransform.localRotation != originalCameraRotation)
            {
                mainCameraTransform.localRotation = Quaternion.Slerp(mainCameraTransform.localRotation, originalCameraRotation, fadeSpeed * Time.deltaTime);
            }
            return;
        }

        timer += Time.deltaTime;

        // --- 🔷 1. Layer 0：下敷き土台背景のスピード制御 ---
        if (spellLayers.Length > 0 && spellLayers[0].rawImage != null)
        {
            if (currentBaseScroll)
            {
                spellLayers[0].currentOffset += spellLayers[0].runtimeScrollSpeed * Time.deltaTime;
                float offsetX = Mathf.Repeat(spellLayers[0].currentOffset.x, 1f);
                float offsetY = Mathf.Repeat(spellLayers[0].currentOffset.y, 1f);
                spellLayers[0].rawImage.uvRect = new Rect(offsetX, offsetY, 1f, 1f);
            }
        }

        // --- 🔶 2. Layer 1：加算合成の上画像のスピード制御 ---
        if (spellLayers.Length > 1 && spellLayers[1].rawImage != null)
        {
            if (currentAdditiveRotate && !Mathf.Approximately(spellLayers[1].runtimeRotateSpeed, 0f))
            {
                spellLayers[1].rawImage.rectTransform.Rotate(0f, 0f, spellLayers[1].runtimeRotateSpeed * Time.deltaTime);
            }

            if (currentAdditiveScroll)
            {
                spellLayers[1].currentOffset += spellLayers[1].runtimeScrollSpeed * Time.deltaTime;
                float offsetX = Mathf.Repeat(spellLayers[1].currentOffset.x, 1f);
                float offsetY = Mathf.Repeat(spellLayers[1].currentOffset.y, 1f);
                spellLayers[1].rawImage.uvRect = new Rect(offsetX, offsetY, 1f, 1f);
            }
        }

        if (useCameraSway && mainCameraTransform != null)
        {
            float yaw = maxYaw * Mathf.Sin(timer * swaySpeed);
            float roll = maxRoll * Mathf.Sin(timer * swaySpeed);
            mainCameraTransform.localRotation = Quaternion.Euler(originalCameraRotation.eulerAngles.x, yaw, roll);
        }
    }

    private void SetLayersAlpha(float alpha)
    {
        foreach (var layer in spellLayers)
        {
            if (layer.rawImage == null) continue;
            Color c = layer.rawImage.color;

            // 🌟 記憶された固定上限値（Layer 1なら 32/255）に対して、フェードの割合を掛け算！
            c.a = layer.maxAlpha * alpha;

            layer.rawImage.color = c;
        }
    }
}