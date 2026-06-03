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

    // =========================================================================
    // 🌟【新規追加】：VJT（聖少女領域）展開中のインゲームUI自動ステルススロット
    // =========================================================================
    [Header("--- VJT Active UI Injection Slots ---")]
    [Tooltip("EnemySpellCardUIオブジェクト、またはその子供の『SpellNameBG』など、自機接近時に隠したいスペカ看板アセットを登録してください")]
    public List<GameObject> vjtSpellCardUIObjects;

    [Tooltip("MatchTimerUIオブジェクトの子供にある『vjtTimerText』のGameObjectをここに登録してください")]
    public GameObject vjtTimerUIObject;

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

        // 🌟 VJT用UIのデフォルトターゲット（通常時は1.0 = 不透明）
        float targetVJTSpellUI = 1.0f;
        float targetVJTTimerUI = 1.0f;

        float hT = horizontalThreshold;
        float vT = verticalThreshold;

        foreach (var p in PlayerMove.AllPlayers)
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

            // =========================================================================
            // 🌟【空間条件判定】：自機が上端（左右問わず）に進入した際のVJT用UIステルス
            // =========================================================================
            if (isTop)
            {
                // 1. スペカ看板UI判定：1P(左上)または2P(右上)の出現位置に自機が張り付いたらピンポイント透過
                if ((p.GetComponent<PlayerStatusManager>()?.playerId == 1 && isLeft) ||
                    (p.GetComponent<PlayerStatusManager>()?.playerId == 2 && isRight))
                {
                    targetVJTSpellUI = dimmedAlpha;
                }

                // 2. VJTツインタイマー判定：中央上部のメインタイマー付近（左右どちらかの上端）に迫ったら連動透過
                if (isLeft || isRight)
                {
                    targetVJTTimerUI = dimmedAlpha;
                }
            }
        }

        // 既存UIグループの不透明度更新
        UpdateGroupAlpha(_topLeft, targetTL);
        UpdateGroupAlpha(_topRight, targetTR);
        UpdateGroupAlpha(_bottomLeft, targetBL);
        UpdateGroupAlpha(_bottomRight, targetBR);
        UpdateGroupAlpha(_topShared, targetTopShared);

        // =========================================================================
        // 🌟【動的インジェクション実行】：新設したスペカUI・タイマーのアルファを適用
        // =========================================================================
        if (vjtSpellCardUIObjects != null)
        {
            foreach (var obj in vjtSpellCardUIObjects)
            {
                UpdateSingleObjectAlpha(obj, targetVJTSpellUI);
            }
        }

        if (vjtTimerUIObject != null)
        {
            UpdateSingleObjectAlpha(vjtTimerUIObject, targetVJTTimerUI);
        }
    }

    private void UpdateGroupAlpha(UIGroup group, float target)
    {
        if (group.objects == null) return;
        foreach (var obj in group.objects)
        {
            UpdateSingleObjectAlpha(obj, target);
        }
    }

    /// <summary>
    /// 🌟【共通化メソッド】：CanvasGroupを最優先とし、Graphic / SpriteRenderer を非破壊で滑らかフェードさせる
    /// </summary>
    private void UpdateSingleObjectAlpha(GameObject obj, float target)
    {
        if (obj == null) return;

        CanvasGroup cg = obj.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = Mathf.MoveTowards(cg.alpha, target, fadeSpeed * Time.deltaTime);
        }
        else
        {
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