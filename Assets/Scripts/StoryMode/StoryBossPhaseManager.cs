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
    // フェーズ遷移中の重複実行を防ぐための制御用フラグ
    private bool _isTransitioning = false;
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
        if (!GameModeManager.IsStoryMode || _statusManager == null || _activePhaseList.Count == 0 || _isTransitioning) return;

        // 🌟 バリアのHP切れ、または【スペルカード自体の制限時間切れ（spellTimer <= 0f）】を監視して次段階へ移行！
        if (_statusManager.isSpellCardActive)
        {
            if (_statusManager.spellHP <= 0f || _statusManager.spellTimer <= 0f)
            {
                // 時間切れの場合、バリアを強制0にしてから次へ進む
                _statusManager.spellHP = 0f;
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

    // 外部、および Update から呼ばれる唯一の進行窓口
    public void AdvanceToNextPhase()
    {
        if (_isTransitioning) return;
        StartCoroutine(TransitionToNextPhaseRoutine());
    }

    private IEnumerator TransitionToNextPhaseRoutine()
    {
        _isTransitioning = true;

        // 1. 爆発エフェクトなどの演出が走る時間を少しだけ待機
        yield return new WaitForSecondsRealtime(0.4f);

        _currentPhaseIndex++;
        yield return StartCoroutine(StartPhaseRoutine(_currentPhaseIndex));

        _isTransitioning = false;
    }

    public bool HasRemainingPhases()
    {
        // 現在のフェーズインデックスが、最後のフェーズ未満であれば true（＝まだ次がある）
        return _currentPhaseIndex < _activePhaseList.Count - 1;
    }
    // StartPhase をコルーチン化
    private void StartPhase(int index)
    {
        StartCoroutine(StartPhaseRoutine(index));
    }

    private IEnumerator StartPhaseRoutine(int index)
    {
        // 🌟【安全ガード】：フェーズ切替の瞬間は、前フェーズのVJT領域フラグ、バリア、コライダーを確実にリセット
        _statusManager.isSpellCardActive = false;
        _statusManager.spellHP = 0f;
        _statusManager.spellMaxHP = 0f;

        if (_statusManager.spellBarrier != null)
        {
            _statusManager.spellBarrier.SetBarrierActive(false);
        }

        if (_statusManager.playerCollider != null)
        {
            _statusManager.playerCollider.transform.localScale = _statusManager.originalColliderScale;
        }


        if (index >= _activePhaseList.Count)
        {
            Debug.Log("<color=gold>🏆【Story Boss】全フェーズ撃破！ステージクリアへ移行します。</color>");
            _statusManager.ApplyDamage((int)_statusManager.currentHP + 9999);

            if (StoryModeManager.Instance != null)
            {
                StoryModeManager.Instance.OnStageCleared();
            }
            yield break;
        }

        // 前のフェーズで動いていたインスタンスの後処理
        ClearPreviousInstances();

        // 🌟【安全ガード】：フェーズ切替の瞬間は、前フェーズのVJT領域フラグを確実にオフにしてリセット
        _statusManager.isSpellCardActive = false;
        _statusManager.spellHP = 0f;
        _statusManager.spellMaxHP = 0f;

        StoryRouteData.BossPhaseData currentPhase = _activePhaseList[index];

        int remainingPhasesAsLives = _activePhaseList.Count - index - 1;
        _statusManager.life = Mathf.Max(0, remainingPhasesAsLives);

        if (currentPhase.phaseType == StoryRouteData.BossPhaseData.PhaseType.NormalAI)
        {
            Debug.Log($"<color=cyan>⚔️【Story Boss】フェーズ {index + 1}/{_activePhaseList.Count}: 通常AI戦 開始</color>");
            if (_aiAgent != null) _aiAgent._useAutoEvadeAI = true;

            _statusManager.currentHP = currentPhase.normalPhaseHP;
            _statusManager.maxHP = currentPhase.normalPhaseHP;
            _statusManager.UpdateUI();

            // 🎯 修正：MatchTimerUI.defaultTimeLimit の代わりに直接 99f などを指定するか、ResetRoundTimerを使用
            if (MatchTimerUI.Instance != null)
            {
                MatchTimerUI.Instance.ResetRoundTimer(99f); // 通常の制限時間（99秒）にリセットして再開
                MatchTimerUI.Instance.ResumeTimer();
            }
        }
        // ---------------------------------------------------------------------
        // 🟦 2. 通常プログラム攻撃
        // ---------------------------------------------------------------------
        else if (currentPhase.phaseType == StoryRouteData.BossPhaseData.PhaseType.NormalProgram)
        {
            Debug.Log($"<color=blue>⚔️【Story Boss】フェーズ {index + 1}/{_activePhaseList.Count}: プログラム通常攻撃 開始</color>");
            if (_aiAgent != null) _aiAgent._useAutoEvadeAI = false;

            _statusManager.currentHP = currentPhase.normalPhaseHP;
            _statusManager.maxHP = currentPhase.normalPhaseHP;
            _statusManager.UpdateUI();

            if (currentPhase.normalPatternPrefab != null)
            {
                _currentActiveNormalInstance = Instantiate(currentPhase.normalPatternPrefab, transform);
                _currentActiveNormalInstance.Initialize(_statusManager);
                StartCoroutine(_currentActiveNormalInstance.ExecutePatternRoutine());
            }

            // 🎯 修正
            if (MatchTimerUI.Instance != null)
            {
                MatchTimerUI.Instance.ResetRoundTimer(99f);
                MatchTimerUI.Instance.ResumeTimer();
            }
        }
        // ---------------------------------------------------------------------
        // 🔮 3. スペルカード攻撃
        // ---------------------------------------------------------------------
        else if (currentPhase.phaseType == StoryRouteData.BossPhaseData.PhaseType.SpellCard)
        {
            Debug.Log($"<color=magenta>🔮【Story Boss】フェーズ {index + 1}/{_activePhaseList.Count}: スペルカード展開準備... [{currentPhase.spellName}]</color>");
            if (_aiAgent != null) _aiAgent._useAutoEvadeAI = false;

            yield return new WaitForSeconds(1.0f);

            Debug.Log($"<color=magenta>🔮【Story Boss】スペルカード本番展開！ [{currentPhase.spellName}]</color>");

            if (currentPhase.spellPatternPrefab != null)
            {
                _currentActiveSpellInstance = Instantiate(currentPhase.spellPatternPrefab, transform);
                _currentActiveSpellInstance.Initialize(_statusManager);
                StartCoroutine(_currentActiveSpellInstance.ExecutePatternRoutine());
            }

            SetupSpellCardUIAndHP(currentPhase);

            // 🎯 修正：スペルカード固有の制限時間（currentPhase.timeLimit）をメインタイマーにセットして走らせる
            if (MatchTimerUI.Instance != null)
            {
                MatchTimerUI.Instance.ResetRoundTimer(currentPhase.timeLimit);
                MatchTimerUI.Instance.ResumeTimer();
            }
        }
    }

    private void SetupSpellCardUIAndHP(StoryRouteData.BossPhaseData phase)
    {
        DanmakuBullet[] pBullets = FindObjectsByType<DanmakuBullet>(FindObjectsSortMode.None);
        foreach (var b in pBullets) b.Deactivate(true, force: true);

        // =========================================================================
        // 💖【最重要修正】：スペルカード（バリア）突入時は、本体HPも必ず満タン（全快）にリセットする！
        // =========================================================================
        _statusManager.currentHP = _statusManager.maxHP;
        _statusManager.UpdateUI();

        // 1. ステータス側のVJT（領域）フラグとHP・タイマーをセット
        _statusManager.isSpellCardActive = true;
        _statusManager.spellMaxHP = phase.spellHP;
        _statusManager.spellHP = phase.spellHP;
        _statusManager.totalSpellDuration = phase.timeLimit;
        _statusManager.spellTimer = phase.timeLimit;

        // 2. 当たり判定（コライダー）の肥大化
        if (_statusManager.playerCollider != null)
        {
            _statusManager.playerCollider.transform.localScale = _statusManager.originalColliderScale * 30f;
        }

        // 3. バリアビジュアル（SpellBarrierEffect）を展開＆カラー同調
        if (_statusManager.spellBarrier != null)
        {
            Color charColor = (_statusManager.characterData != null) ? _statusManager.characterData.imageColor : Color.white;
            _statusManager.spellBarrier.SetBarrierActive(true);

            Renderer[] barrierRenderers = _statusManager.spellBarrier.GetComponentsInChildren<Renderer>(true);
            foreach (var r in barrierRenderers)
            {
                if (r is SpriteRenderer sr) sr.color = charColor;
                else if (r is LineRenderer lr) { lr.startColor = charColor; lr.endColor = charColor; }
                else if (r.material != null) r.material.color = charColor;
            }

            ParticleSystem[] barrierParticles = _statusManager.spellBarrier.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in barrierParticles)
            {
                var mainModule = ps.main;
                mainModule.startColor = charColor;
            }
        }

        // 4. 看板UI（EnemySpellCardUI）の表示
        if (EnemySpellCardUI.Instance != null)
        {
            EnemySpellCardUI.Instance.DisplaySpell(
                phase.spellName,
                0, 0, 1000000f, false, _statusManager.playerId
            );
        }

        // 5. 専用2D背景の展開
        if (VJTSpellBackgroundManager2D.Instance != null)
        {
            VJTSpellBackgroundManager2D.Instance.SetSpellBackgroundActive(true, _statusManager.characterData);
        }

        // 6. リング（PlayerSpellRing_Line）の動的生成
        if (_statusManager.spellRingPrefab != null)
        {
            System.Reflection.FieldInfo ringField = typeof(PlayerStatusManager).GetField("spawnedRingInstance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            GameObject spawnedRing = ringField != null ? (GameObject)ringField.GetValue(_statusManager) : null;

            if (spawnedRing == null)
            {
                spawnedRing = Instantiate(_statusManager.spellRingPrefab, _statusManager.transform.position, Quaternion.identity);
                PlayerSpellRing_Line ringScript = spawnedRing.GetComponent<PlayerSpellRing_Line>();
                if (ringScript != null)
                {
                    ringScript.targetStatus = _statusManager;
                    ringScript.Activate(phase.timeLimit);
                }
                if (ringField != null) ringField.SetValue(_statusManager, spawnedRing);
            }
        }

        // 7. 魔法陣（PlayerSpellCircle）の動的生成
        if (_statusManager.spellCirclePrefab != null)
        {
            System.Reflection.FieldInfo circleField = typeof(PlayerStatusManager).GetField("spawnedCircleInstance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            GameObject spawnedCircle = circleField != null ? (GameObject)circleField.GetValue(_statusManager) : null;

            if (spawnedCircle == null)
            {
                spawnedCircle = Instantiate(_statusManager.spellCirclePrefab, _statusManager.transform.position, Quaternion.identity);
                PlayerSpellCircle circleScript = spawnedCircle.GetComponent<PlayerSpellCircle>();
                if (circleScript != null)
                {
                    circleScript.Activate(_statusManager, phase.timeLimit);
                }
                if (circleField != null) circleField.SetValue(_statusManager, spawnedCircle);
            }
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


}