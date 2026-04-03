using UnityEngine;

public class Shockwave : MonoBehaviour
{
    private SpriteRenderer sr;
    private CircleCollider2D col;
    private float expandSpeed;
    private int damage = 20; // 衝撃波のダメージ

    [Header("Screen Shake Settings")]
    [SerializeField] private float shakeDuration = 0.2f;
    [SerializeField] private float shakeMagnitude = 0.15f;

    // --- 修正：引数を4つ (Color shockColor を追加) に変更 ---
    public void InitializeWithCustomScale(Sprite sprite, Color shockColor, float startScale, float speed ,bool isShake = false)
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<CircleCollider2D>();

        sr.sprite = sprite;
        sr.color = shockColor; // 渡された弾の色を適用
        transform.localScale = Vector3.one * startScale;
        expandSpeed = speed;

        if (col != null) col.isTrigger = true;

        // 加算合成
        sr.material = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));

        // 画面を揺らす
        if (CameraShake.Instance != null && isShake)
        {
            CameraShake.Instance.Shake(shakeDuration, shakeMagnitude);
        }

        Destroy(gameObject, 1.0f);
    }

    void Update()
    {
        float currentScale = transform.localScale.x + expandSpeed * Time.deltaTime * 60f;
        transform.localScale = Vector3.one * currentScale;

        Color c = sr.color;
        c.a -= 0.02f * Time.deltaTime * 60f;
        sr.color = c;

        if (c.a <= 0) Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 何かに当たったこと自体を確認
        //Debug.Log("衝撃波が何かに接触: " + collision.gameObject.name);

        if (collision.CompareTag("EnemyBullet"))
        {
            //Debug.Log("弾のタグを検知！");
            EnemyBullet bullet = collision.GetComponent<EnemyBullet>();
            if (bullet != null)
            {
                //Debug.Log("弾の消滅処理を実行します");
                bullet.Deactivate(true); //
            }
        }
        // 2. ★レーザーの先頭（EnemyLaserStream）の検知と消滅処理
        // レーザーは EnemyBullet ではないため、別途コンポーネントをチェックします
        EnemyLaserStream laser = collision.GetComponent<EnemyLaserStream>();
        if (laser != null)
        {
            // Debug.Log("レーザーのヘッドを検知！一括消去します");
            laser.ClearLaser(); // 先頭を破壊し、繋がっている「節」もすべて消去する
        }
        EnemyStatus enemy = collision.GetComponent<EnemyStatus>();
        if (enemy != null) enemy.TakeDamage(damage, true); //
    }
}