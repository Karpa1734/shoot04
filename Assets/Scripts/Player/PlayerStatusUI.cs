// --- PlayerStatusUI.cs 階層ズレ完全吸収・自動サルベージ版 ---
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class PlayerStatusUI : MonoBehaviour
{
    [Header("--- 1. モード別親ルートオブジェクト（丸ごと非表示用） ---")]
    [Tooltip("ストーリーモード用のUIルート（空の背景ハートなどもすべて内包した親オブジェクト）")]
    public GameObject storyUiRoot;

    [Tooltip("VSモード用のUIルート（Life_VS など、四角アセットを丸ごと持った最上位オブジェクト）")]
    public GameObject vsUiRoot;

    [Header("--- 2. 点灯・減少制御用の実体オブジェクトリスト ---")]
    [Tooltip("ストーリーモード用のハート（中身のImage）を左から順番に登録")]
    public List<Image> storyHeartIcons;

    [Tooltip("VSモード用の四角スロットを左から順番に登録（親・枠・中身のどれを登録しても自動検知します）")]
    public List<Image> vsSquareContentIcons;

    [Header("--- 3. 共通UI ---")]
    public TextMeshProUGUI pieceText;

    public void SetCountVsVariant(int currentCount, int pieceCount, int requiredCount, bool isVsMode)
    {
        if (pieceText != null)
        {
            if (isVsMode) pieceText.text = "";
            else pieceText.text = $"{pieceCount}/{requiredCount}";
        }

        if (isVsMode)
        {
            if (storyUiRoot != null) storyUiRoot.SetActive(false);
            if (vsUiRoot != null) vsUiRoot.SetActive(true);

            // 中身の四角オブジェクトの点灯制御
            for (int i = 0; i < vsSquareContentIcons.Count; i++)
            {
                if (vsSquareContentIcons[i] == null) continue;

                Transform t = vsSquareContentIcons[i].transform;
                GameObject actualContent = null;

                // ==========================================================
                // 🌟【修正】名前、またはインデックスを指定して「緑中身」を確実に対象にする
                // ==========================================================

                // 方法A：名前でピンポイント検索（名前が "緑中身" や "Green" などの場合。一番安全です）
                Transform greenTransform = t.Find("緑中身"); // 💡実際のUnity上の緑中身のオブジェクト名に書き換えてください

                if (greenTransform != null)
                {
                    actualContent = greenTransform.gameObject;
                }
                // 方法B：名前で見つからない場合、2番目の子要素（インデックス 1）を試す
                else if (t.childCount > 1)
                {
                    // 0番目が黒背景、1番目が緑中身、という階層順に対応
                    actualContent = t.GetChild(1).gameObject;
                }
                // フェールセーフ：子要素が1つしかない場合はそれを対象にする
                else if (t.childCount == 1)
                {
                    actualContent = t.GetChild(0).gameObject;
                }
                else
                {
                    actualContent = t.gameObject;
                }

                // 3. 割り出した「緑中身」だけをON/OFF制御
                if (actualContent != null)
                {
                    actualContent.SetActive(i < currentCount);
                }
            }
        }
        else
        {
            // ==========================================================
            // 🌟【ストーリーモード：VS用UIを完全非表示、ストーリーUIを表示】
            // ==========================================================
            if (storyUiRoot != null) storyUiRoot.SetActive(true);
            if (vsUiRoot != null) vsUiRoot.SetActive(false);

            for (int i = 0; i < storyHeartIcons.Count; i++)
            {
                if (storyHeartIcons[i] == null) continue;

                if (i < currentCount) storyHeartIcons[i].fillAmount = 1.0f;
                else if (i == currentCount) storyHeartIcons[i].fillAmount = (float)pieceCount / requiredCount;
                else storyHeartIcons[i].fillAmount = 0.0f;
            }
        }
    }
}