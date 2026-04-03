using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic; // Queueを使うために追加

public class ExtendNotificationUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI notificationText;
    [SerializeField] private float fadeDuration = 0.4f;
    [SerializeField] private float displayDuration = 1.2f;

    // 通知データの構造体
    private struct NotificationData
    {
        public string message;
        public Color color;
    }

    private Queue<NotificationData> notificationQueue = new Queue<NotificationData>();
    private bool isDisplaying = false;

    void Awake()
    {
        if (notificationText != null)
        {
            notificationText.alpha = 0;
            notificationText.gameObject.SetActive(false);
        }
    }

    // ★外部からの呼び出し：キューに追加するだけにする
    public void Show(string message, Color color)
    {
        Debug.Log($"[Notification] 表示リクエストを受信: {message}"); // これが出るか確認
        notificationQueue.Enqueue(new NotificationData { message = message, color = color });

        if (!isDisplaying)
        {
            Debug.Log("[Notification] キューの処理を開始します");
            StartCoroutine(ProcessQueue());
        }
    }

    // ★キューを順番に処理するコルーチン
    private IEnumerator ProcessQueue()
    {
        isDisplaying = true;

        while (notificationQueue.Count > 0)
        {
            NotificationData data = notificationQueue.Dequeue();
            yield return StartCoroutine(FadeSequence(data.message, data.color));

            // 通知の間に少しだけ間隔を空ける（東方っぽい余韻）
            yield return new WaitForSecondsRealtime(0.1f);
        }

        isDisplaying = false;
    }

    private IEnumerator FadeSequence(string message, Color color)
    {
        notificationText.text = message;
        notificationText.color = color;
        notificationText.gameObject.SetActive(true);

        // 1. フェードイン
        float elapsed = 0;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            notificationText.alpha = Mathf.Lerp(0, 1, elapsed / fadeDuration);
            yield return null;
        }

        // 2. 表示維持
        yield return new WaitForSecondsRealtime(displayDuration);

        // 3. フェードアウト
        elapsed = 0;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            notificationText.alpha = Mathf.Lerp(1, 0, elapsed / fadeDuration);
            yield return null;
        }

        notificationText.gameObject.SetActive(false);
    }
}