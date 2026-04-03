using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(CanvasGroup))]
public class SpellBackgroundController : MonoBehaviour
{
    private Canvas canvas;
    private CanvasGroup canvasGroup;
    [SerializeField] private float fadeDuration = 0.5f;

    void Awake()
    {
        canvas = GetComponent<Canvas>();
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f; // 最初は透明
    }

    void Start()
    {
        // カメラを探してアタッチする
        AssignCamera();
    }

    private void AssignCamera()
    {
        // 1. まず標準的な MainCamera タグで探す
        Camera targetCamera = Camera.main;

        // 2. 見つからなければシーン内の「どれでもいいから」カメラを1つ持ってくる
        if (targetCamera == null)
        {
            targetCamera = Object.FindAnyObjectByType<Camera>();
        }

        if (targetCamera != null)
        {
            canvas.worldCamera = targetCamera;
            // 自機や弾よりも奥（50〜100程度）に表示されるように距離を設定
            canvas.planeDistance = 50f;
        }
        else
        {
            Debug.LogError("背景カメラが見つかりません！シーンにCameraがあるか確認してください。");
        }
    }

    public void FadeIn()
    {
        StopAllCoroutines();
        StartCoroutine(FadeRoutine(1f));
    }

    public void FadeOutAndDestroy()
    {
        StopAllCoroutines();
        StartCoroutine(FadeOutRoutine());
    }

    private IEnumerator FadeRoutine(float targetAlpha)
    {
        float duration = Mathf.Max(0.01f, fadeDuration);
        while (!Mathf.Approximately(canvasGroup.alpha, targetAlpha))
        {
            // ポーズやスローに影響されない unscaledDeltaTime を使用
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.unscaledDeltaTime / duration);
            yield return null;
        }
        canvasGroup.alpha = targetAlpha;
    }

    private IEnumerator FadeOutRoutine()
    {
        yield return StartCoroutine(FadeRoutine(0f));
        Destroy(gameObject);
    }
}