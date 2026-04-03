using UnityEngine;
using System.Collections;

public class BossAuraManager : MonoBehaviour
{
    [Header("Sprites (Sliced)")]
    public Sprite spriteAuraRise;   // Aura01の左側 [cite: 22-23]
    public Sprite spriteAuraGather; // Aura01の右側 [cite: 27]
    public Sprite spriteAuraBase;   // Aura02全体 [cite: 31]

    [Header("Color Settings")]
    public Color auraColor = Color.white;

    [Header("Layer Settings")]
    public int sortingOrder = 4800;

    private Material sharedAdditiveMaterial;

    void Start()
    {
        // 1. マテリアルを最初に1つだけ作成してキャッシュ（高速化）
        Shader additiveShader = Shader.Find("Legacy Shaders/Particles/Additive");
        if (additiveShader == null) additiveShader = Shader.Find("Sprites/Default");
        sharedAdditiveMaterial = new Material(additiveShader);

        // 各オーラ演出の開始
        StartCoroutine(Aura01_A_Routine()); // 上昇 [cite: 17]
        StartCoroutine(Aura01_B_Routine()); // 収束 [cite: 18]
        StartCoroutine(Aura2_Routine());    // 背景 [cite: 29-33]
    }

    // --- 各コルーチン ---
    IEnumerator Aura01_A_Routine()
    {
        while (true)
        {
            while (Time.timeScale <= 0) yield return null;
            SpawnAuraParticle(1);
            yield return new WaitForSeconds(6f / 60f); // 6フレーム間隔
        }
    }

    IEnumerator Aura01_B_Routine()
    {
        while (true)
        {
            while (Time.timeScale <= 0) yield return null;
            SpawnAuraParticle(2);
            yield return new WaitForSeconds(3f / 60f); // 3フレーム間隔 [cite: 14]
        }
    }

    IEnumerator Aura2_Routine()
    {
        // 1. 生成
        GameObject auraObj = CreateAuraObject("Aura_BaseDistortion", spriteAuraBase);
        SpriteRenderer sr = auraObj.GetComponent<SpriteRenderer>();

        // 生成直後にボスの現在地に合わせる
        auraObj.transform.position = transform.position;

        float time = 20f; // 出現時間
        float scaleMax = 1.4f; // 最大サイズ [cite: 29]
        float targetAlpha = 200f / 255f; // 目標とする透明度 [cite: 30]

        // 2. 出現時の拡大演出 + フェードイン
        for (int i = 0; i < time; i++)
        {
            while (Time.timeScale <= 0) yield return null;
            // 拡大中もボスが動く可能性があるため、毎フレーム位置を更新 [cite: 32]
            auraObj.transform.position = transform.position;

            float progress = (i + 1) / time;
            float s = scaleMax * Mathf.Sin(progress * Mathf.PI / 2f);
            auraObj.transform.localScale = new Vector3(s, s, 1);

            // --- 修正：拡大ループ内でも色（アルファ値）を更新してフェードインさせる ---
            // もし「即座に出したい」場合は progress をかけずに targetAlpha をそのまま代入してください
            sr.color = new Color(auraColor.r, auraColor.g, auraColor.b, targetAlpha * progress);

            yield return null;
        }

        // 3. 維持状態（ボスに追従）
        while (true)
        {
            while (Time.timeScale <= 0) yield return null;
            // ここで常にボスの位置を追いかける [cite: 32]
            auraObj.transform.position = transform.position;
            sr.color = new Color(auraColor.r, auraColor.g, auraColor.b, targetAlpha);
            yield return null;
        }
    }

    void SpawnAuraParticle(int type)
    {
        Sprite targetSprite = (type == 1) ? spriteAuraRise : spriteAuraGather;
        GameObject part = CreateAuraObject("Aura_Particle", targetSprite);

        if (type == 1) StartCoroutine(Aura1_1_Logic(part));
        else StartCoroutine(Aura1_2_Logic(part));
    }

    // --- ロジック ---
    IEnumerator Aura1_1_Logic(GameObject obj)
    {
        SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
        float time = 20f;
        float pX = Random.Range(-0.04f, 0.04f); // [cite: 19]
        float angle = pX * 1.5f; // [cite: 19]
        float scalex3 = Random.Range(1.2f, 1.5f); // [cite: 20]
        float scaley3 = Random.Range(1.2f, 1.5f); // [cite: 21]

        Vector3 startPos = transform.position + new Vector3(pX, 0, 0);
        obj.transform.rotation = Quaternion.Euler(0, 0, angle);

        for (int i = 0; i < time; i++)
        {
            while (Time.timeScale <= 0) yield return null;
            float progress = (i + 1) / time;
            float mY = (21f / time) * (i + 1); // [cite: 23]
            float alpha = Mathf.Sin(progress * Mathf.PI); // [cite: 24]
            float sx = scalex3 * Mathf.Sin(progress * (135f / 180f) * Mathf.PI);
            float sy = scaley3 * Mathf.Sin(progress * (90f / 180f) * Mathf.PI);

            obj.transform.position = startPos + new Vector3(0, mY * 0.05f, 0);
            obj.transform.localScale = new Vector3(sx, sy, 1);
            sr.color = new Color(auraColor.r, auraColor.g, auraColor.b, alpha);
            yield return null;
        }
        Destroy(obj);
    }

    IEnumerator Aura1_2_Logic(GameObject obj)
    {
        SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
        float time = 20f;
        float scale1 = 2.9f; // [cite: 26]
        float scale2 = scale1 - 1.0f; // [cite: 26]
        obj.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0, 360f));

        for (int i = 0; i < time; i++)
        {
            scale1 -= (scale2 / time); // [cite: 28]
            float alpha = (240f / 255f) * ((i + 1) / time); // [cite: 28]

            obj.transform.position = transform.position;
            obj.transform.localScale = new Vector3(scale1, scale1, 1);
            sr.color = new Color(auraColor.r, auraColor.g, auraColor.b, alpha);
            yield return null;
        }
        Destroy(obj);
    }

    // --- 重要：生成と初期化をセットで行うメソッド ---
    GameObject CreateAuraObject(string name, Sprite sprite)
    {
        GameObject obj = new GameObject(name);

        // 生成した瞬間に「親の設定」と「座標のリセット」を最初に行う
        obj.transform.SetParent(this.transform);
        obj.transform.position = transform.position; // 即座に親の現在地に合わせる
        obj.transform.localScale = Vector3.zero;

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();

        // 【重要】描画を一旦オフにする
        sr.enabled = false;

        // 加算合成マテリアルと色をセット
        sr.material = sharedAdditiveMaterial;
        sr.sprite = sprite;
        sr.sortingOrder = sortingOrder;
        sr.color = new Color(auraColor.r, auraColor.g, auraColor.b, 0f);

        // すべての準備が整ったので、描画をオンにする
        sr.enabled = true;

        return obj;
    }
}