using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class PlayerSpellRing_Line : MonoBehaviour
{
    [Header("References")]
    [Tooltip("追従対象となるプレイヤーのPlayerStatusManager")]
    public PlayerStatusManager targetStatus;
    private LineRenderer line;

    [Header("Shape Settings")]
    public int segments = 90;          // 円の滑らかさ
    public float maxRadius = 3.5f;     // 開始時の最大半径（自機サイズに合わせて調整可）
    public float minRadius = 0.8f;     // 終了直前の最小半径
    public float ringWidth = 0.25f;    // リングの線の太さ

    [Header("Texture Settings")]
    [Tooltip("画像が円一周で何回繰り返されるか。数値を小さくすると1つ1つの画像が大きくなります")]
    public float textureTiling = 6.0f;
    public float scrollSpeed = 1.5f;   // テクスチャがぐるぐる回る流れる速度

    private float initialTimeLimit;
    private bool isActive = false;
    private float appearanceProgress = 0f;

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.positionCount = segments + 1;
        line.loop = true;
        line.enabled = false;

        // 🌟【重要】：ローカル座標系を使用
        // これにより、(x, y)の計算結果が「このオブジェクトの中心（＝自機の中心）」からの距離になります
        line.useWorldSpace = false;
        line.textureMode = LineTextureMode.Tile;
    }

    void Update()
    {
        // 領域がアクティブではない、またはプレイヤー情報がアタッチされていない場合は処理しない
        if (!isActive || targetStatus == null) return;

        // 🌟【完全追従ロジック】：常に親となる自機（プレイヤーオブジェクト）の座標にピッタリ吸い付かせる
        transform.position = targetStatus.transform.position;

        // 1. UVテクスチャのスクロールアニメーション制御
        float offset = -Time.time * scrollSpeed;
        line.material.mainTextureScale = new Vector2(textureTiling, 1);
        line.material.mainTextureOffset = new Vector2(offset, 0);

        // 2. 出現時の演出（発動瞬間に0からフワッと目標サイズへ広がる）
        if (appearanceProgress < 1.0f)
        {
            appearanceProgress += Time.deltaTime * 2.0f; // 約0.5秒で展開完了
        }

        // 3. 🌟【中枢：アルカナタイマー同期減衰計算】
        // PlayerStatusManagerが内包する今回の総維持時間（totalSpellDuration）と、
        // 毎フレームリアルタイムに減衰している残り時間（spellTimer）を直接参照して割合（1.0 -> 0.0）を算出！
        float timeRate = 0f;
        if (targetStatus.totalSpellDuration > 0f)
        {
            timeRate = Mathf.Clamp01(targetStatus.spellTimer / targetStatus.totalSpellDuration);
        }

        // 4. 残り時間の割合（timeRate）に応じて最大半径から最小半径へと滑らかにイージング縮小
        float currentRadius = Mathf.Lerp(minRadius, maxRadius, timeRate) * appearanceProgress;

        // 円の再描画
        DrawCircle(currentRadius);

        // 5. 領域終了判定（タイマー全損、またはHPが0以下でダウンした際、あるいはバリア破壊時）
        if (targetStatus.spellTimer <= 0f || !targetStatus.isSpellCardActive || targetStatus.spellHP <= 0f)
        {
            Deactivate();
        }
    }

    /// <summary>
    /// 🌟 PlayerStatusManager.cs の ActivateSpellCard() の中から呼び出すための起動エントリーポイント
    /// </summary>
    public void Activate(float timeLimit)
    {
        initialTimeLimit = timeLimit;
        isActive = true;
        line.enabled = true;
        appearanceProgress = 0f;

        // 線の太さをリセット
        line.startWidth = ringWidth;
        line.endWidth = ringWidth;
    }

    /// <summary>
    /// 領域終了時、または撃墜時に安全に非表示化する
    /// </summary>
    public void Deactivate()
    {
        isActive = false;
        line.enabled = false;
    }

    /// <summary>
    /// 三角関数を用いて滑らかなローカル円形の座標配列を LineRenderer にインジェクションする
    /// </summary>
    void DrawCircle(float radius)
    {
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * (360f / segments) * Mathf.Deg2Rad;
            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius;
            line.SetPosition(i, new Vector3(x, y, 0));
        }
    }
}