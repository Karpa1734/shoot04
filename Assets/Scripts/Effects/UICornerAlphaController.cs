using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UICornerAlphaController : MonoBehaviour
{
    [System.Serializable]
    public struct UIGroup
    {
        public List<GameObject> objects;
    }

    [Header("UI Groups")]
    [SerializeField] private UIGroup _topLeft;
    [SerializeField] private UIGroup _topRight;
    [SerializeField] private UIGroup _bottomLeft;
    [SerializeField] private UIGroup _bottomRight;
    [SerializeField] private UIGroup _topShared; // タイマーなどの共通UI用

    [Header("Alpha Settings")]
    [Range(0, 1)] public float dimmedAlpha = 0.3f;
    public float fadeSpeed = 5.0f;

    [Header("Threshold Settings")]
    [Range(0, 0.5f)] public float horizontalThreshold = 0.28f;
    [Range(0, 0.5f)] public float verticalThreshold = 0.28f;

    private Camera _mainCam;

    void Start() => _mainCam = Camera.main;

    void Update()
    {
        float targetTL = 1.0f, targetTR = 1.0f, targetBL = 1.0f, targetBR = 1.0f, targetTopShared = 1.0f;
        float hT = horizontalThreshold;
        float vT = verticalThreshold;

        foreach (var p in PlayerMove.AllPlayers) //
        {
            if (p == null) continue;
            Vector3 vPos = _mainCam.WorldToViewportPoint(p.transform.position);
            if (vPos.z < 0) continue;

            bool isLeft = vPos.x < hT;
            bool isRight = vPos.x > (1f - hT);
            bool isTop = vPos.y > (1f - vT);
            bool isBottom = vPos.y < vT;

            if (isLeft && isTop) targetTL = dimmedAlpha;
            if (isRight && isTop) targetTR = dimmedAlpha;
            if (isLeft && isBottom) targetBL = dimmedAlpha;
            if (isRight && isBottom) targetBR = dimmedAlpha;

            // タイマー用：左右どちらかの上端に誰かがいたら透過
            if (isTop && (isLeft || isRight)) targetTopShared = dimmedAlpha;
        }

        UpdateGroupAlpha(_topLeft, targetTL);
        UpdateGroupAlpha(_topRight, targetTR);
        UpdateGroupAlpha(_bottomLeft, targetBL);
        UpdateGroupAlpha(_bottomRight, targetBR);
        UpdateGroupAlpha(_topShared, targetTopShared);
    }

    private void UpdateGroupAlpha(UIGroup group, float target)
    {
        if (group.objects == null) return;
        foreach (var obj in group.objects)
        {
            if (obj == null) continue;

            // ★ 修正：まず CanvasGroup がついているか確認する
            CanvasGroup cg = obj.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                // CanvasGroup があれば、その alpha を操作（これが一番確実で競合しません）
                cg.alpha = Mathf.MoveTowards(cg.alpha, target, fadeSpeed * Time.deltaTime);
            }
            else
            {
                // CanvasGroup がない場合のみ、内部の Graphic や Sprite を個別に操作
                var graphics = obj.GetComponentsInChildren<Graphic>();
                foreach (var g in graphics)
                {
                    Color c = g.color;
                    c.a = Mathf.MoveTowards(c.a, target, fadeSpeed * Time.deltaTime);
                    g.color = c;
                }
                var sprites = obj.GetComponentsInChildren<SpriteRenderer>();
                foreach (var s in sprites)
                {
                    Color c = s.color;
                    c.a = Mathf.MoveTowards(c.a, target, fadeSpeed * Time.deltaTime);
                    s.color = c;
                }
            }
        }
    }
}