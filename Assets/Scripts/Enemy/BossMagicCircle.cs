using UnityEngine;

public class BossMagicCircle : MonoBehaviour
{
    [Header("References")]
    public SpriteRenderer sr;

    [Header("Rotation Settings")]
    public float spinSpeed = 0.8f;

    [Header("TH14 Lean Settings")]
    public float lean = 28f;

    private float anglez = 0f;
    private float scale2 = 0f; // 輝針城仕様の進行度変数
    private bool isRunning = false;

    void Start()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();

        if (sr != null)
        {
            // Built-inで光らせるための設定
            //Shader additiveShader = Shader.Find("Legacy Shaders/Particles/Additive");
           // if (additiveShader == null) additiveShader = Shader.Find("Sprites/Default");
           // sr.material = new Material(additiveShader);
            sr.color = new Color(1f, 1f, 1f, 144f / 255f);

            // 最初は描画をオフにして、初期化によるチラつきを防ぐ
            sr.enabled = false;
        }

        transform.localScale = Vector3.zero;
        StartAppearance();
    }

    void Update()
    {
        if (!isRunning || Mathf.Approximately(Time.timeScale, 0f)) return;

        // 1. 回転計算 (th14仕様の3D傾き)
        anglez -= spinSpeed;
        float anglex = lean - lean * Mathf.Cos(anglez * Mathf.Deg2Rad);
        float angley = lean - lean * Mathf.Sin(anglez * Mathf.Deg2Rad);
        transform.localRotation = Quaternion.Euler(anglex, angley, anglez);

        // 2. スケール処理 (th14の数式を完全再現)
        UpdateScale();
    }

    void UpdateScale()
    {
        if (scale2 < 90f)
        {
            // 1. 出現・拡大フェーズ (60フレームで90度に到達)
            scale2 += 90f / 60f;

            // 全体 = (0.9 + 0.1) * Sin(scale2) と同じ意味になり、90度でジャスト 1.0 になる
            float finalScale = Mathf.Sin(scale2 * Mathf.Deg2Rad);
            transform.localScale = new Vector3(finalScale, finalScale, 1.0f);
        }
        else
        {
            // 2. 待機・脈動フェーズ
            // 速度調整（5倍遅くする処理）を、角度の加算側で行う
            scale2 += (360f / 120f) / 5f;

            // 計算式から「/ 5」を消去する
            // これにより、scale2が90の瞬間（Sin=1）から、滑らかに脈動が開始される
            float scale3 = 0.90f;
            float scale1 = 0.10f * Mathf.Sin(scale2 * Mathf.Deg2Rad);

            float finalScale = scale3 + scale1;
            transform.localScale = new Vector3(finalScale, finalScale, 1.0f);
        }

        if (!sr.enabled) sr.enabled = true;
    }
    public void StartAppearance()
    {
        scale2 = 0f;
        isRunning = true;
    }
}