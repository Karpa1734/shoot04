using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class PlayerStatusUI : MonoBehaviour
{
    public List<Image> icons;
    public TextMeshProUGUI pieceText; // インスペクターで pieceText スロットに TMP をセットしてください

    public void SetCount(int mainCount, int pieceCount, int requiredCount)
    {
        // ★修正：計算ではなく、文字列（テキスト）として代入する
        if (pieceText != null)
        {
            // $を使うことで、変数の中身を文字列として合成できます
            pieceText.text = $"{pieceCount}/{requiredCount}";
        }

        // アイコンの制御
        for (int i = 0; i < icons.Count; i++)
        {
            if (icons[i] == null) continue;
            if (i < mainCount) icons[i].fillAmount = 1.0f;
            else if (i == mainCount) icons[i].fillAmount = (float)pieceCount / requiredCount;
            else icons[i].fillAmount = 0.0f;
        }
    }
}