using UnityEngine;

public class BossController : MonoBehaviour
{
    [Header("References")]
    public EnemyStatus status;
    public SpriteRenderer bossSpriteRenderer;

    [Header("Sprite Settings")]
    public Sprite idleSprite;
    public Sprite movingSprite;
    public bool faceLeftByDefault = true;

    private bool isMoving = false;
    private Vector3 lastPosition;

    void Start()
    {
        if (status == null) status = GetComponent<EnemyStatus>();
        if (bossSpriteRenderer == null) bossSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        lastPosition = transform.position;
    }

    // 外部（パターンプレハブ）から移動状態をセットしてもらう
    public void SetMoving(bool moving) => isMoving = moving;

    void Update()
    {
        if (bossSpriteRenderer == null) return;

        // スプライト切り替え
        bossSpriteRenderer.sprite = isMoving && movingSprite != null ? movingSprite : idleSprite;

        // 左右反転
        float deltaX = transform.position.x - lastPosition.x;
        if (Mathf.Abs(deltaX) > 0.001f)
            bossSpriteRenderer.flipX = deltaX > 0 ? faceLeftByDefault : !faceLeftByDefault;

        lastPosition = transform.position;
    }
}