using UnityEngine;

public class BulletHitEffect : MonoBehaviour
{
    private SpriteRenderer sr;
    private float speed;
    private float angle;
    private float alpha = 0.75f; // 
    private float fadeSpeed = 2.0f; // 消える速さ

    public void Initialize(Vector3 pos, float bulletSpeed, float bulletAngle, Sprite effectSprite, Material addMaterial)
    {
        transform.position = pos;
        speed = bulletSpeed / 8f; //  速度を落として慣性を表現
        angle = bulletAngle;

        sr = GetComponent<SpriteRenderer>();
        sr.sprite = effectSprite;
        sr.material = addMaterial; // BLEND_ADD_RGB 相当 
        sr.color = new Color(1, 1, 1, alpha);

        // 進行方向に合わせる
        transform.rotation = Quaternion.Euler(0, 0, angle - 90f);

        // 短時間で自動削除
        Destroy(gameObject, 0.5f);
    }

    void Update()
    {
        // 慣性移動 [cite: 6]
        float rad = angle * Mathf.Deg2Rad;
        transform.position += new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * speed * Time.deltaTime;

        // フェードアウト 
        alpha -= fadeSpeed * Time.deltaTime;
        sr.color = new Color(1, 1, 1, Mathf.Max(0, alpha));
    }
}