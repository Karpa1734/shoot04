using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 生成されたダメージ数値を浮き上がらせ（または沈ませ）ながら 0 -> 1 -> 0 でフェード表示して自動消滅させるコンポーネント
/// 💡【画面位置適応型】：被弾位置が画面上部なら下へ、下部なら上へポップ方向を自動反転し、視認時間を延長・減速調整。
/// </summary>
[RequireComponent(typeof(TextMeshPro))]
public class DamagePopup : MonoBehaviour
{
    private TextMeshPro _textMesh;
    private Vector3 _moveDirection = Vector3.up; // 🎯 動的に決定される移動方向

    [Header("Animation Settings")]
    [Tooltip("表示演出全体の時間（秒）")]
    public float duration = 1.0f; // 💡 0.6f ➔ 1.0f へ延長し、じっくり読めるように調整
    [Tooltip("移動する速度")]
    public float moveSpeed = 0.8f; // 💡 1.8f ➔ 0.8f への減速で、マイルドで上品な浮動へ変更

    void Awake()
    {
        _textMesh = GetComponent<TextMeshPro>();
    }

    /// <summary>
    /// ダメージ数値をセットし、アニメーションを開始する
    /// </summary>
    public void Setup(int damageAmount)
    {
        if (_textMesh == null) _textMesh = GetComponent<TextMeshPro>();
        _textMesh.text = damageAmount.ToString();

        // =========================================================================
        // 🎯【ポップ方向の動的分岐インフラ】
        // 💡 理由：画面中央(Y=0)を基準とし、上部での被弾なら下（Vector3.down）へ、
        //          下部での被弾なら上（Vector3.up）へ流すことで、画面外見切れを100%防止します。
        // =========================================================================
        if (transform.position.y > 0f)
        {
            _moveDirection = Vector3.down;
        }
        else
        {
            _moveDirection = Vector3.up;
        }

        StartCoroutine(AnimateRoutine());
    }

    private IEnumerator AnimateRoutine()
    {
        Color baseColor = _textMesh.color;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // 時間の進行度を 0.0 ~ 1.0 に正規化
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);

            // 1. 位置の制御：被弾場所に応じて上下へ滑らかに等速移動
            transform.position += _moveDirection * moveSpeed * Time.deltaTime;

            // 2. 透明度（アルファ値）の制御：0 -> 1 -> 0 の放物線カーブを作成
            // progressが0.5（中間の時間）のときに最大（1.0）になり、最後は0になる計算
            float alpha = 1f - Mathf.Pow((progress - 0.5f) * 2f, 2f);
            alpha = Mathf.Clamp01(alpha);

            _textMesh.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

            yield return null;
        }

        // 演出終了後にメモリ解放のため自身を完全に消去
        Destroy(gameObject);
    }
}