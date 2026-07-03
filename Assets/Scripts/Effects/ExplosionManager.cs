using UnityEngine;
using System.Collections;

public class ExplosionManager : MonoBehaviour
{
    [Header("エフェクト設定")]
    [Tooltip("花びらのプレハブ（PetalLogicスクリプトが付いたもの）")]
    public GameObject petalPrefab;
    [Tooltip("一度に生成する花びらの数")]
    public int petalCount = 30;
    [Tooltip("花びらが飛び散る力（速度）の範囲")]
    public Vector2 explosionForceRange = new Vector2(3f, 8f);

    private void Start()
    {
        Explode(); // 爆発を開始
    }

    // 爆発を発生させる関数（トリガーとして呼び出す）
    public void Explode()
    {
        for (int i = 0; i < petalCount; i++)
        {
            // 花びらを生成[cite: 27]
            GameObject petal = Instantiate(petalPrefab, transform.position, Quaternion.identity); //[cite: 27]

            // 花びらのスクリプトを取得[cite: 27]
            PetalLogic petalLogic = petal.GetComponent<PetalLogic>(); //[cite: 27]
            if (petalLogic != null) //[cite: 27]
            {
                // ランダムな3D方向を計算[cite: 27]
                Vector3 randomDirection = Random.onUnitSphere; // 球面上のランダムな点（長さ1のベクトル）[cite: 27]

                // ランダムな力を掛けて初期速度を設定[cite: 27]
                float force = Random.Range(explosionForceRange.x, explosionForceRange.y); //[cite: 27]
                petalLogic.velocity = randomDirection * force; //[cite: 27]
            }
        }

        // =========================================================================
        // 🎯【最核心修正：セルフデスライフサイクルインフラ】
        // 💡 理由：花びらを一斉にバラまき終えたら、このエミッター（親）自体の役割は
        //          100%終了しているため、次の物理フレームまたは数秒後に自身を完全物理削除します。
        //          これにより、ヒエラルキーにManagerが無限に溜まるリークを完全に根絶します。
        // =========================================================================
        // ※もし花びら以外の消滅SEなどの鳴り終わりを待ちたい場合は、Destroy(gameObject, 2.0f); のようにディレイ秒数を指定してください
        Destroy(gameObject);
    }

    // テスト用：スペースキーを押すと爆発を発生させる[cite: 27]
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space)) //[cite: 27]
        {
            Explode(); //[cite: 27]
        }
    }
}