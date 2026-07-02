using TMPro;
using UnityEngine;

/// <summary>
/// 弾幕プールの稼働状況（アクティブ数 / 総オブジェクト数）を、
/// ガベージコレクションを発生させずに軽量に画面表示するデバッグUIスクリプト。
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class BulletCounterDebugger : MonoBehaviour
{
    private TextMeshProUGUI _counterText;
    private float _updateTimer = 0f;
    private const float UPDATE_INTERVAL = 0.1f; // 0.1秒ごとに画面の数値をリフレッシュ

    // 💡 文字列結合によるメモリ負荷（GC）を避けるためのカスタムバッファ文字列インフラ
    private System.Text.StringBuilder _sb = new System.Text.StringBuilder(32);

    void Awake()
    {
        _counterText = GetComponent<TextMeshProUGUI>();
    }

    void Update()
    {
        // 高速シミュレーション（ML-Agents）時の描画負荷を下げるため、インターバルを設けて更新
        _updateTimer += Time.unscaledDeltaTime;
        if (_updateTimer < UPDATE_INTERVAL) return;
        _updateTimer = 0f;

        if (_counterText == null || BulletPool.Instance == null) return;

        // 1. プールから最新の稼働データを安全に吸い出す
        int activeCount;
        int totalCount;
        BulletPool.Instance.GetPoolStatus(out activeCount, out totalCount);

        // 2. StringBuilder を用いて「(アクティブ数/総数)」の文字列を最速構築（GC Alloc: 0）
        _sb.Clear();
        _sb.Append("(");
        _sb.Append(activeCount);
        _sb.Append("/");
        _sb.Append(totalCount);
        _sb.Append(")");

        // 3. UIテキストへ流し込み
        _counterText.text = _sb.ToString();
    }
}