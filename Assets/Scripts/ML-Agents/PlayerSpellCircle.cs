using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class PlayerSpellCircle : MonoBehaviour
{
    private PlayerStatusManager targetStatus;
    private SpriteRenderer sr;

    [Header("TH14 Rotation Settings")]
    public float spinSpeed = 0.8f;      // 東方輝針城仕様の回転速度
    public float lean = 28f;           // 東方輝針城仕様の3D傾き角度

    private float anglez = 0f;
    private float scale2 = 0f;         // 輝針城仕様の進行度変数
    private bool isRunning = false;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = GetComponent<SpriteRenderer>();

        if (sr != null)
        {
            // 輝針城仕様の不透明度（144/255）を初期値としてセット
            sr.color = new Color(1f, 1f, 1f, 144f / 255f);
            sr.enabled = false;
        }

        transform.localScale = Vector3.zero;
    }

    /// <summary>
    /// 🌟 生成時に PlayerStatusManager からキックされ、画像・色・ターゲットを完全同期する
    /// </summary>
    public void Activate(PlayerStatusManager owner, float timeLimit)
    {
        targetStatus = owner;

        if (targetStatus != null && targetStatus.characterData != null)
        {
            if (sr != null)
            {
                // 🌟【仕様適合】：PlayerDataにアタッチされている固有の魔法陣スプライトを動的ロード
                sr.sprite = targetStatus.characterData.spellCircleSprite;

                // 🌟【カラー同調】：バリアやリングと完全同期。輝針城の透過率(144)を保ちつつ、キャラ固有のイメージカラーへ鮮やかに染める
                Color charColor = targetStatus.characterData.imageColor;
                sr.color = new Color(charColor.r, charColor.g, charColor.b, 144f / 255f);
            }
        }

        scale2 = 0f;
        anglez = 0f;
        isRunning = true;
    }

    void Update()
    {
        // 領域終了検知、またはポーズ中・プレイヤー不在時は処理しない
        if (!isRunning || targetStatus == null || Mathf.Approximately(Time.timeScale, 0f)) return;

        // 🌟【完全追従ロジック】：常に発動した自機（プレイヤーオブジェクト）の座標にピッタリ吸い付かせる
        transform.position = targetStatus.transform.position;

        // 1. ✨【輝針城数式の完全保護】：回転計算 (th14仕様の3D傾き)
        anglez -= spinSpeed;
        float anglex = lean - lean * Mathf.Cos(anglez * Mathf.Deg2Rad);
        float angley = lean - lean * Mathf.Sin(anglez * Mathf.Deg2Rad);
        transform.localRotation = Quaternion.Euler(anglex, angley, anglez);

        // 2. 📈【輝針城数式の完全保護】：スケール・脈動処理
        UpdateScale();

        // 3. 領域終了判定（タイマー全損、またはHPが0以下でダウンした際、あるいはバリア破壊時）
        if (targetStatus.spellTimer <= 0f || !targetStatus.isSpellCardActive || targetStatus.spellHP <= 0f)
        {
            Deactivate();
        }
    }

    void UpdateScale()
    {
        if (sr == null) return;

        if (scale2 < 90f)
        {
            // 1. 出現・拡大フェーズ (60フレームで90度に到達)
            scale2 += 90f / 60f;

            // 90度でジャスト 1.0 になるSin計算
            float finalScale = Mathf.Sin(scale2 * Mathf.Deg2Rad);
            transform.localScale = new Vector3(finalScale, finalScale, 1.0f);
        }
        else
        {
            // 2. 待機・脈動フェーズ
            // 速度調整（5倍遅くする処理）を、角度の加算側で行う
            scale2 += (360f / 120f) / 5f;

            float scale3 = 0.90f;
            float scale1 = 0.10f * Mathf.Sin(scale2 * Mathf.Deg2Rad);

            float finalScale = scale3 + scale1;
            transform.localScale = new Vector3(finalScale, finalScale, 1.0f);
        }

        if (!sr.enabled) sr.enabled = true;
    }

    public void Deactivate()
    {
        isRunning = false;
        if (sr != null) sr.enabled = false;

        // プレハブ動的生成型のため、終了時はメモリリークを起こさず自律物理破棄
        Destroy(gameObject);
    }
}