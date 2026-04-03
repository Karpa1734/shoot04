using KanKikuchi.AudioManager;
using System.Collections;
using UnityEngine;

public class ItemController : MonoBehaviour
{
    public enum ITEM_TYPE { SCORE_UP, LIFE_UP01, LIFE_UP02, BOMB_UP01, BOMB_UP02, SCORE00, POWER01 }

    [SerializeField] private ITEM_TYPE itemType = ITEM_TYPE.SCORE_UP;

    [Header("Physics Settings")]
    [SerializeField] private float initialVelocity = 4.0f; // 上方向への初速
    [SerializeField] private float gravity = 8.0f;          // 重力の強さ
    [SerializeField] private float maxFallSpeed = 3.5f;    // 最大落下速度

    [Header("Auto Collect Setting")]
    [SerializeField] private float collectLineY = 2.0f;
    public float CollectLineY => collectLineY; // ハンドラーから見えるようにする

    private float verticalVelocity; // 現在の垂直速度
    private bool isCollecting = false;
    private float collectSpeed = 5.0f;
    public void SetCollectImmediate() { isCollecting = true; }
    void Start()
    {
        verticalVelocity = initialVelocity; //

        if (itemType == ITEM_TYPE.LIFE_UP01 || itemType == ITEM_TYPE.BOMB_UP01 || itemType == ITEM_TYPE.LIFE_UP02 || itemType == ITEM_TYPE.BOMB_UP02)
        {
            SEManager.Instance.Play(SEPath.ONE07); //
        }
    }

    public ITEM_TYPE GetItemType()
    {
        return itemType; //
    }

    void Update()
    {
        if (Time.timeScale <= 0) return;

        if (transform.position.y < -5.5f) { Destroy(gameObject); return; }

        // 回収ライン判定
        if (!isCollecting && PlayerMove.Instance != null)
        {
            if (PlayerMove.Instance.transform.position.y >= collectLineY)
            {
                isCollecting = true;
            }
        }

        // --- 修正：回収中であっても、上昇中なら物理（投げ上げ）を優先する ---
        if (isCollecting && verticalVelocity <= 0 && PlayerMove.Instance != null)
        {
            // A. 自機へ吸い込み
            collectSpeed += 15f * Time.deltaTime;
            transform.position = Vector3.MoveTowards(
                transform.position,
                PlayerMove.Instance.transform.position,
                collectSpeed * Time.deltaTime
            );

            // 衝突判定は ItemEffectHandler 側に任せているため、ここでは Destroy しない
        }
        else
        {
            // B. 鉛直投げ上げ・自由落下処理
            // 重力加速度 $g$ を用いた速度変化：$v = v_0 - gt$
            verticalVelocity -= gravity * Time.deltaTime;
            if (verticalVelocity < -maxFallSpeed) verticalVelocity = -maxFallSpeed;

            transform.Translate(Vector3.up * verticalVelocity * Time.deltaTime, Space.World);

            // 上昇中のみ回転させる演出
            if (verticalVelocity > 0)
            {
                transform.Rotate(0, 0, 360f * Time.deltaTime);
            }
            else
            {
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.identity, 10f * Time.deltaTime);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("CollectArea"))
        {
            isCollecting = true; //
        }
    }
}