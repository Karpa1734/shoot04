using UnityEngine;

public class GrazeEffect : MonoBehaviour
{
    [Header("Settings")]
    public float duration = 0.35f; // 30フレーム(約0.5秒)より少し短くしてキレを出す
    private float elapsed = 0f;
    private Vector3 moveDir;
    private SpriteRenderer sr;
    private Color initialColor;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        initialColor = sr.color;

        // 1. 移動方向をランダムに決定 (0~360度)
        float angleDeg = Random.Range(0f, 360f);

        // --- 修正ポイント：角度をラジアンに変換 ---
        // UnityのMathf.Cos/Sinはラジアンを引数に取るため、Deg2Radを掛けます
        float angleRad = angleDeg * Mathf.Deg2Rad;

        // 2. 速度を決定 (DNHのrand(2,4)を参考に設定)
        float speed = Random.Range(1.5f, 3.5f);
        moveDir = new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0) * speed;

        // 3. 向きの修正
        // 画像の「先端」が移動方向を向くように回転させます
        // eff_splash.png が横長なら angleDeg、縦長なら angleDeg + 90 など調整
        transform.rotation = Quaternion.Euler(0, 0, angleDeg);

        // 4. 初期サイズ (少し大きく発生して消えていく)
        transform.localScale = Vector3.one * 0.6f;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = elapsed / duration;

        // 1. 移動：慣性をのせて飛ばす
        transform.position += moveDir * Time.deltaTime;

        // 2. 縮小：徐々に小さくする
        transform.localScale = Vector3.Lerp(Vector3.one * 0.6f, Vector3.zero, t);

        // 3. アルファ減衰：透明度を下げていく (重要！)
        // Effect.txt の Alpha -= 0.75/30 の挙動を再現
        float alpha = Mathf.Lerp(0.75f, 0f, t);
        sr.color = new Color(initialColor.r, initialColor.g, initialColor.b, alpha);

        if (t >= 1f) Destroy(gameObject);
    }
}