using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StoryBossPhaseManager : MonoBehaviour
{
    public StoryRouteData currentRouteData;
    public int currentStageNumber = 1;

    private int _currentPhaseIndex = 0;
    private List<StoryRouteData.BossPhaseData> _activePhaseList = new List<StoryRouteData.BossPhaseData>();

    private PlayerStatusManager _statusManager;
    private DanmakuAgent _aiAgent;

    // 現在稼働中のプログラムインスタンス
    private SpellCardPattern _currentActiveSpellInstance;
    private NormalAttackPattern _currentActiveNormalInstance;

    void Awake()
    {
        _statusManager = GetComponent<PlayerStatusManager>();
        _aiAgent = GetComponent<DanmakuAgent>();
    }

    void Start()
    {
        if (!GameModeManager.IsStoryMode || (_statusManager != null && _statusManager.playerId != 2))
        {
            this.enabled = false;
            return;
        }

        // 🌟 StoryModeManager から「現在進行中の自機ルート」と「現在の面数」を自動引き継ぎ！
        if (StoryModeManager.Instance != null && StoryModeManager.CurrentActiveRoute != null)
        {
            currentRouteData = StoryModeManager.CurrentActiveRoute;
            currentStageNumber = StoryModeManager.CurrentStageNumber;
        }

        LoadCurrentStagePhases();

        if (_activePhaseList.Count > 0)
        {
            StartPhase(_currentPhaseIndex);
        }
    }

    private void LoadCurrentStagePhases()
    {
        if (currentRouteData == null) return;

        foreach (var stage in currentRouteData.stages)
        {
            if (stage.stageNumber == currentStageNumber)
            {
                _activePhaseList = stage.bossPhases;
                return;
            }
        }
    }

    void Update()
    {
        if (!GameModeManager.IsStoryMode || _statusManager == null || _activePhaseList.Count == 0) return;

        // フェーズ撃破・時間切れ判定
        if (_statusManager.isSpellCardActive)
        {
            if (_statusManager.spellHP <= 0f || _statusManager.spellTimer <= 0f)
            {
                AdvanceToNextPhase();
            }
        }
        else
        {
            if (_statusManager.currentHP <= 0f)
            {
                AdvanceToNextPhase();
            }
        }
    }

    private void StartPhase(int index)
    {
        if (index >= _activePhaseList.Count)
        {
            Debug.Log("<color=gold>🏆【Story Boss】全フェーズ撃破！ステージクリアへ移行します。</color>");
            _statusManager.ApplyDamage((int)_statusManager.currentHP + 9999);

            if (StoryModeManager.Instance != null)
            {
                StoryModeManager.Instance.OnStageCleared();
            }
            return;
        }

        // 前のフェーズで動いていたインスタンスの後処理
        ClearPreviousInstances();

        StoryRouteData.BossPhaseData currentPhase = _activePhaseList[index];

        // 🌟【最核心】：残りのフェーズ数からボスUI用のライフ（星の数）を自動算出！
        // 例：全4フェーズの場合
        // フェーズ1 (index:0) ➔ life = 4 - 0 - 1 = 3 個
        // フェーズ2 (index:1) ➔ life = 4 - 1 - 1 = 2 個
        // フェーズ3 (index:2) ➔ life = 4 - 2 - 1 = 1 個
        // フェーズ4 (index:3 = 最終フェーズ) ➔ life = 4 - 3 - 1 = 0 個（星表示なし！）
        int remainingPhasesAsLives = _activePhaseList.Count - index - 1;
        _statusManager.life = Mathf.Max(0, remainingPhasesAsLives);

        // ---------------------------------------------------------------------
        // 🟢 1. 通常AI攻撃（AIによる思考・移動）
        // ---------------------------------------------------------------------
        if (currentPhase.phaseType == StoryRouteData.BossPhaseData.PhaseType.NormalAI)
        {
            Debug.Log($"<color=cyan>⚔️【Story Boss】フェーズ {index + 1}/{_activePhaseList.Count}: 通常AI戦 開始 (残りライフUI: {_statusManager.life})</color>");
            if (_aiAgent != null) _aiAgent._useAutoEvadeAI = true;

            if (_statusManager.isSpellCardActive) _statusManager.DeactivateSpellCard(false);

            _statusManager.currentHP = currentPhase.normalPhaseHP;
            _statusManager.maxHP = currentPhase.normalPhaseHP;
            _statusManager.UpdateUI();
        }
        // ---------------------------------------------------------------------
        // 🟦 2. 通常プログラム攻撃（確定パターン通常攻撃）
        // ---------------------------------------------------------------------
        else if (currentPhase.phaseType == StoryRouteData.BossPhaseData.PhaseType.NormalProgram)
        {
            Debug.Log($"<color=blue>⚔️【Story Boss】フェーズ {index + 1}/{_activePhaseList.Count}: プログラム通常攻撃 開始 (残りライフUI: {_statusManager.life})</color>");
            if (_aiAgent != null) _aiAgent._useAutoEvadeAI = false;

            if (_statusManager.isSpellCardActive) _statusManager.DeactivateSpellCard(false);

            _statusManager.currentHP = currentPhase.normalPhaseHP;
            _statusManager.maxHP = currentPhase.normalPhaseHP;
            _statusManager.UpdateUI();

            if (currentPhase.normalPatternPrefab != null)
            {
                _currentActiveNormalInstance = Instantiate(currentPhase.normalPatternPrefab, transform);
                _currentActiveNormalInstance.Initialize(_statusManager);
                StartCoroutine(_currentActiveNormalInstance.ExecutePatternRoutine());
            }
        }
        // ---------------------------------------------------------------------
        // 🔮 3. スペルカード攻撃（専用UI＋時間制限＋確定パターン弾幕）
        // ---------------------------------------------------------------------
        else if (currentPhase.phaseType == StoryRouteData.BossPhaseData.PhaseType.SpellCard)
        {
            Debug.Log($"<color=magenta>🔮【Story Boss】フェーズ {index + 1}/{_activePhaseList.Count}: スペルカード展開！ [{currentPhase.spellName}] (残りライフUI: {_statusManager.life})</color>");
            if (_aiAgent != null) _aiAgent._useAutoEvadeAI = false;

            if (currentPhase.spellPatternPrefab != null)
            {
                _currentActiveSpellInstance = Instantiate(currentPhase.spellPatternPrefab, transform);
                _currentActiveSpellInstance.Initialize(_statusManager);
                StartCoroutine(_currentActiveSpellInstance.ExecutePatternRoutine());
            }

            SetupSpellCardUIAndHP(currentPhase);
        }
    }

    private void SetupSpellCardUIAndHP(StoryRouteData.BossPhaseData phase)
    {
        DanmakuBullet[] pBullets = FindObjectsByType<DanmakuBullet>(FindObjectsSortMode.None);
        foreach (var b in pBullets) b.Deactivate(true, force: true);

        _statusManager.isSpellCardActive = true;
        _statusManager.spellMaxHP = phase.spellHP;
        _statusManager.spellHP = phase.spellHP;
        _statusManager.totalSpellDuration = phase.timeLimit;
        _statusManager.spellTimer = phase.timeLimit;

        if (EnemySpellCardUI.Instance != null)
        {
            EnemySpellCardUI.Instance.DisplaySpell(
                phase.spellName,
                0, 0, 1000000f, false, _statusManager.playerId
            );
        }
    }

    private void ClearPreviousInstances()
    {
        if (_currentActiveNormalInstance != null)
        {
            _currentActiveNormalInstance.OnAttackEnd();
            Destroy(_currentActiveNormalInstance.gameObject);
        }

        if (_currentActiveSpellInstance != null)
        {
            _currentActiveSpellInstance.OnSpellEnd();
            Destroy(_currentActiveSpellInstance.gameObject);
        }

        if (EnemySpellCardUI.Instance != null)
        {
            EnemySpellCardUI.Instance.HideSpell();
        }
    }

    public void AdvanceToNextPhase()
    {
        _currentPhaseIndex++;
        StartPhase(_currentPhaseIndex);
    }
}