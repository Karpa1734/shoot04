#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(StoryRouteData.BossPhaseData))]
public class BossPhaseDataDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // 各プロパティの参照を取得
        SerializedProperty phaseTypeProp = property.FindPropertyRelative("phaseType");
        SerializedProperty normalHPProp = property.FindPropertyRelative("normalPhaseHP");
        SerializedProperty normalPrefabProp = property.FindPropertyRelative("normalPatternPrefab");
        SerializedProperty spellNameProp = property.FindPropertyRelative("spellName");
        SerializedProperty spellHPProp = property.FindPropertyRelative("spellHP");
        SerializedProperty timeLimitProp = property.FindPropertyRelative("timeLimit");
        SerializedProperty spellPrefabProp = property.FindPropertyRelative("spellPatternPrefab");

        // 1行の高さを定義
        float lineHeight = EditorGUIUtility.singleLineHeight;
        float lineSpacing = EditorGUIUtility.standardVerticalSpacing;

        Rect currentRect = new Rect(position.x, position.y, position.width, lineHeight);

        // 1. フェーズ種別のドロップダウン描画
        EditorGUI.PropertyField(currentRect, phaseTypeProp, new GUIContent("フェーズ種別 (Phase Type)"));
        currentRect.y += lineHeight + lineSpacing;

        StoryRouteData.BossPhaseData.PhaseType phaseType = (StoryRouteData.BossPhaseData.PhaseType)phaseTypeProp.enumValueIndex;

        // 2. フェーズ種別に応じた項目の動的切り替え表示
        if (phaseType == StoryRouteData.BossPhaseData.PhaseType.NormalAI)
        {
            EditorGUI.PropertyField(currentRect, normalHPProp, new GUIContent("通常HP (Normal HP)"));
        }
        else if (phaseType == StoryRouteData.BossPhaseData.PhaseType.NormalProgram)
        {
            EditorGUI.PropertyField(currentRect, normalHPProp, new GUIContent("通常HP (Normal HP)"));
            currentRect.y += lineHeight + lineSpacing;
            EditorGUI.PropertyField(currentRect, normalPrefabProp, new GUIContent("通常パターン (Normal Prefab)"));
        }
        else if (phaseType == StoryRouteData.BossPhaseData.PhaseType.SpellCard)
        {
            EditorGUI.PropertyField(currentRect, spellNameProp, new GUIContent("スペルカード名 (Spell Name)"));
            currentRect.y += lineHeight + lineSpacing;
            EditorGUI.PropertyField(currentRect, spellHPProp, new GUIContent("スペルHP (Spell HP)"));
            currentRect.y += lineHeight + lineSpacing;
            EditorGUI.PropertyField(currentRect, timeLimitProp, new GUIContent("制限時間 (Time Limit)"));
            currentRect.y += lineHeight + lineSpacing;
            EditorGUI.PropertyField(currentRect, spellPrefabProp, new GUIContent("スペルパターン (Spell Prefab)"));
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        SerializedProperty phaseTypeProp = property.FindPropertyRelative("phaseType");
        StoryRouteData.BossPhaseData.PhaseType phaseType = (StoryRouteData.BossPhaseData.PhaseType)phaseTypeProp.enumValueIndex;

        float lineHeight = EditorGUIUtility.singleLineHeight;
        float lineSpacing = EditorGUIUtility.standardVerticalSpacing;

        // 選択されたTypeに応じて描画領域の高さ（行数）を自動計算
        int lineCount = 1; // PhaseTypeの分

        if (phaseType == StoryRouteData.BossPhaseData.PhaseType.NormalAI)
        {
            lineCount += 1; // normalHP
        }
        else if (phaseType == StoryRouteData.BossPhaseData.PhaseType.NormalProgram)
        {
            lineCount += 2; // normalHP + normalPrefab
        }
        else if (phaseType == StoryRouteData.BossPhaseData.PhaseType.SpellCard)
        {
            lineCount += 4; // spellName + spellHP + timeLimit + spellPrefab
        }

        return (lineHeight * lineCount) + (lineSpacing * (lineCount - 1));
    }
}
#endif