using UnityEngine;

public class VJTSpellBackgroundManager2D : MonoBehaviour
{
    public static VJTSpellBackgroundManager2D Instance;

    [System.Serializable]
    public class SpellLayer2D
    {
        public SpriteRenderer spriteRenderer; // 2D画像スプライト

        [HideInInspector] public Vector2 runtimeScrollSpeed;  // キャラごとに実行時上書きされるスクロール速度
        [HideInInspector] public float runtimeRotateSpeed;    // キャラごとに実行時上書きされる回転速度
        [HideInInspector] public Vector2 currentOffset;
    }

    [Header("2D Spell Background Layers")]
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

    private Sprite defaultBaseSprite;
    private Sprite defaultAdditiveSprite;
    private bool isDefaultSpritesCached = false;

    void Awake()
    {
        Instance = this;
        if (spellBGGroup != null) spellBGGroup.SetActive(false);
        if (mainCameraTransform != null) originalCameraRotation = mainCameraTransform.localRotation;
        SetLayersAlpha(0f);

        // 🌟【最速記憶】：Awakeの時点でインスペクターの初期スプライトを即座に記憶してNone化を予防
        CacheDefaultSprites();
    }

    private void CacheDefaultSprites()
    {
        if (isDefaultSpritesCached) return;

        // インスペクターのコンポーネントにアタッチされている初期画像をがっちりバックアップ
        if (spellLayers.Length > 0 && spellLayers[0].spriteRenderer != null)
            defaultBaseSprite = spellLayers[0].spriteRenderer.sprite;

        if (spellLayers.Length > 1 && spellLayers[1].spriteRenderer != null)
            defaultAdditiveSprite = spellLayers[1].spriteRenderer.sprite;

        isDefaultSpritesCached = true;
    }

    /// <summary>
    /// 🌟【鉄壁ガード版】：データが万が一 null で飛んできても、Noneによる上書きを完全にシャットアウト
    /// </summary>
    public void SetSpellBackgroundActive(bool active, PlayerSkillData charData = null)
    {
        isSpellActive = active;
        if (active)
        {
            timer = 0f;
            CacheDefaultSprites();

            if (charData != null)
            {
                // 1. アニメーションON/OFFトグルの同期
                currentBaseScroll = charData.isBaseScrollActive;
                currentAdditiveRotate = charData.isAdditiveRotateActive;
                currentAdditiveScroll = charData.isAdditiveScrollActive;

                // 2. 速度パラメータの動的インジェクション
                if (spellLayers.Length > 0) spellLayers[0].runtimeScrollSpeed = charData.baseScrollSpeed;
                if (spellLayers.Length > 1)
                {
                    spellLayers[1].runtimeRotateSpeed = charData.additiveRotateSpeed;
                    spellLayers[1].runtimeScrollSpeed = charData.additiveScrollSpeed;
                }

                // 3. スプライトアセットの動的バインド（Data側が未設定なら初期画像をフォールバック維持）
                if (spellLayers.Length > 0 && spellLayers[0].spriteRenderer != null)
                {
                    spellLayers[0].spriteRenderer.sprite = (charData.characterSpellBGBase != null) ? charData.characterSpellBGBase : defaultBaseSprite;
                }
                if (spellLayers.Length > 1 && spellLayers[1].spriteRenderer != null)
                {
                    spellLayers[1].spriteRenderer.sprite = (charData.characterSpellBGAdditive != null) ? charData.characterSpellBGAdditive : defaultAdditiveSprite;
                }
            }
            else
            {
                // 🚨 外部からデータなし（null）で呼ばれた場合でも、インスペクターに元々貼ってある画像を死守！！
                currentBaseScroll = false;
                currentAdditiveRotate = true;
                currentAdditiveScroll = true;

                if (spellLayers.Length > 0)
                {
                    spellLayers[0].runtimeScrollSpeed = new Vector2(0f, -0.4f);
                    if (spellLayers[0].spriteRenderer != null) spellLayers[0].spriteRenderer.sprite = defaultBaseSprite;
                }
                if (spellLayers.Length > 1)
                {
                    spellLayers[1].runtimeRotateSpeed = 25f;
                    spellLayers[1].runtimeScrollSpeed = new Vector2(0.2f, 0.2f);
                    if (spellLayers[1].spriteRenderer != null) spellLayers[1].spriteRenderer.sprite = defaultAdditiveSprite;
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

        // --- Layer 0：下敷き土台背景のスピード制御 ---
        if (spellLayers.Length > 0 && spellLayers[0].spriteRenderer != null)
        {
            if (currentBaseScroll && spellLayers[0].spriteRenderer.material != null)
            {
                spellLayers[0].currentOffset += spellLayers[0].runtimeScrollSpeed * Time.deltaTime;
                spellLayers[0].spriteRenderer.material.mainTextureOffset = spellLayers[0].currentOffset;
            }
        }

        // --- Layer 1：加算合成の上画像のスピード制御 ---
        if (spellLayers.Length > 1 && spellLayers[1].spriteRenderer != null)
        {
            if (currentAdditiveRotate && !Mathf.Approximately(spellLayers[1].runtimeRotateSpeed, 0f))
            {
                spellLayers[1].spriteRenderer.transform.Rotate(0f, 0f, spellLayers[1].runtimeRotateSpeed * Time.deltaTime);
            }

            if (currentAdditiveScroll && spellLayers[1].spriteRenderer.material != null)
            {
                spellLayers[1].currentOffset += spellLayers[1].runtimeScrollSpeed * Time.deltaTime;
                spellLayers[1].spriteRenderer.material.mainTextureOffset = spellLayers[1].currentOffset;
            }
        }

        // カメラ揺れ演出
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
            if (layer.spriteRenderer == null) continue;
            Color c = layer.spriteRenderer.color;
            c.a = alpha;
            layer.spriteRenderer.color = c;
        }
    }
}