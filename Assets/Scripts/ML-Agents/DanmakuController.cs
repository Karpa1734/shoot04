using System.Collections.Generic;
using UnityEngine;

// --- ロジック担当クラス：物理演算や移動の実行をこちらに移譲 ---
public class DanmakuController : MonoBehaviour
{
    [SerializeField] private float highSpeed = 4.5f;
    [SerializeField] private float lowSpeed = 2.0f;

    [Header("Movement Bounds")]
    public float minX = -4.0f;
    public float maxX = 4.0f;
    public float minY = -4.5f;
    public float maxY = 4.5f;

    private Rigidbody2D rb;
    private PlayerMove shell;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        // 同一オブジェクト内のシェル（PlayerMove）を取得
        shell = GetComponent<PlayerMove>();
    }

    void FixedUpdate()
    {
        if (shell == null) return;

        // シェルに格納されている「現在のフレームの入力」を読み取る
        var input = shell.currentFrameInput;
        Vector2 inputVec = new Vector2(input.h, input.v);
        float speed = input.slow ? lowSpeed : highSpeed;

        // 物理移動の計算
        Vector2 velocity = inputVec.normalized * speed;
        Vector2 nextPosition = rb.position + velocity * Time.fixedDeltaTime;

        // 移動範囲のクランプ
        nextPosition.x = Mathf.Clamp(nextPosition.x, minX, maxX);
        nextPosition.y = Mathf.Clamp(nextPosition.y, minY, maxY);
        rb.MovePosition(nextPosition);
    }
}