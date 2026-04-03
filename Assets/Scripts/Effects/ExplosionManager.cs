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
            // 花びらを生成
            GameObject petal = Instantiate(petalPrefab, transform.position, Quaternion.identity);

            // 花びらのスクリプトを取得
            PetalLogic petalLogic = petal.GetComponent<PetalLogic>();
            if (petalLogic != null)
            {
                // ランダムな3D方向を計算
                Vector3 randomDirection = Random.onUnitSphere; // 球面上のランダムな点（長さ1のベクトル）

                // ランダムな力を掛けて初期速度を設定
                float force = Random.Range(explosionForceRange.x, explosionForceRange.y);
                petalLogic.velocity = randomDirection * force;
            }
        }
    }

    // テスト用：スペースキーを押すと爆発を発生させる
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Explode();
        }
    }
}