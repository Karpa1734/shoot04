using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 各自機（主人公）ごとの全6ステージのストーリー進行ルートデータ
/// Projectウィンドウ右クリック > Create > Danmaku > Story Route Data から作成します
/// </summary>
[CreateAssetMenu(fileName = "StoryRoute_Player", menuName = "Danmaku/Story Route Data")]
public class StoryRouteData : ScriptableObject
{
    [Header("主役（自機）のキャラ名")]
    public string playerCharacterName = "Karin";

    [System.Serializable]
    public class BossPhaseData
    {
        public enum PhaseType
        {
            NormalAI,      // ① AIによる自動回避・攻撃
            NormalProgram, // ② プログラム制御による通常攻撃
            SpellCard      // ③ スペルカード
        }

        public PhaseType phaseType = PhaseType.NormalAI;

        // --- 通常攻撃時設定 ---
        public float normalPhaseHP = 100f;
        public NormalAttackPattern normalPatternPrefab;

        // --- スペルカード時設定 ---
        public string spellName = "〇符「〇〇〇〇」";
        public float spellHP = 1500f;
        public float timeLimit = 35f;
        public SpellCardPattern spellPatternPrefab;
    }

    [System.Serializable]
    public class StageBossConfig
    {
        [Tooltip("第何面か (1〜6)")]
        public int stageNumber = 1;

        [Tooltip("登場するボスのキャラクターID (0〜7)")]
        public int bossCharacterId = 0;

        [Header("📖 このステージでのボス行動順")]
        public List<BossPhaseData> bossPhases = new List<BossPhaseData>();
    }

    [Header("📖 各ステージのボス設定 (1面〜6面)")]
    public List<StageBossConfig> stages = new List<StageBossConfig>();
}