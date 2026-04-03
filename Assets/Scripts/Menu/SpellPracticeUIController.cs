using KanKikuchi.AudioManager;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SpellPracticeUIController : MonoBehaviour
{
    public enum SelectionState { BossSelect, PhaseSelect }
    private SelectionState currentState = SelectionState.BossSelect;

    [Header("Data")]
    public List<GameObject> bossPrefabs; // インスペクターでボスプレハブを登録
    public string gameSceneName = "Shoot";

    [Header("UI References")]
    public TextMeshProUGUI bossNameText;      // ボス名用 TMP (例: < ルーミア >)
    public TextMeshProUGUI[] phaseTexts;      // グリッド内の 24 個の TMP

    [Header("Color Settings")]
    public Color selectedColor = Color.white;
    public Color unselectedColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    [Header("Title Settings")]
    public TitleMenuManager titleMenu; // ★追加：タイトルメニューマネージャーへの参照
    private int bossIndex = 0;
    private int phaseIndex = 0;
    private const int GRID_COLUMNS = 4; // 1行4列

    void OnEnable()
    {
        currentState = SelectionState.BossSelect;
        bossIndex = 0;
        phaseIndex = 0;
        UpdateDisplay();
    }
    void Awake()
    {
        // ★追加：もしインスペクターでセットし忘れていても、自動でタイトルメニューを探す
        if (titleMenu == null)
        {
            titleMenu = Object.FindFirstObjectByType<TitleMenuManager>();
        }
    }
    void Update()
    {
        if (currentState == SelectionState.BossSelect) HandleBossNavigation();
        else HandlePhaseNavigation();
    }

    void HandleBossNavigation()
    {
        int prevIndex = bossIndex;
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            SEManager.Instance.Play(SEPath.MENUSELECT, 0.5f);
            bossIndex = (bossIndex - 1 + bossPrefabs.Count) % bossPrefabs.Count;
        }
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            SEManager.Instance.Play(SEPath.MENUSELECT, 0.5f);
            bossIndex = (bossIndex + 1) % bossPrefabs.Count;
        }
        if (prevIndex != bossIndex) UpdateDisplay();

        if (Input.GetKeyDown(KeyCode.Z))
        {
            SEManager.Instance.Play(SEPath.MENUDECIDE, 0.5f);
            currentState = SelectionState.PhaseSelect;
            UpdateDisplay();
        }
        if (Input.GetKeyDown(KeyCode.X))
        {
            // 演習モードのフラグを掃除
            BossPracticeManager.Clear();
            SEManager.Instance.Play(SEPath.MENUCANCEL, 0.5f);

            // ★修正：タイトルメニューを確実に起こす
            if (titleMenu != null)
            {
                titleMenu.enabled = true;
            }
            else
            {
                Debug.LogError("TitleMenuManager が見つからないため、メインメニューを再開できません。");
            }

            this.gameObject.SetActive(false);
        }
    }

    void HandlePhaseNavigation()
    {
        // 安全策1：ボスが登録されていない場合は処理しない
        if (bossPrefabs == null || bossPrefabs.Count == 0) return;

        EnemyStatus currentBoss = bossPrefabs[bossIndex].GetComponent<EnemyStatus>();

        // 安全策2：ボスのPhasesリストが存在しない、または空の場合は処理しない
        if (currentBoss == null || currentBoss.Phases == null || currentBoss.Phases.Count == 0) return;

        int phaseCount = currentBoss.Phases.Count;
        int prevIndex = phaseIndex;
        if (phaseCount > 0)
        {
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                phaseIndex = (phaseIndex + 1) % phaseCount;
                SEManager.Instance.Play(SEPath.MENUSELECT, 0.5f);
            }
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                phaseIndex = (phaseIndex - 1 + phaseCount) % phaseCount;
                SEManager.Instance.Play(SEPath.MENUSELECT, 0.5f);
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                phaseIndex = (phaseIndex + GRID_COLUMNS) % phaseCount;
                SEManager.Instance.Play(SEPath.MENUSELECT, 0.5f);
            }
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                phaseIndex = (phaseIndex - GRID_COLUMNS + phaseCount) % phaseCount;
                SEManager.Instance.Play(SEPath.MENUSELECT, 0.5f);
            }
        }
        if (prevIndex != phaseIndex) UpdateDisplay();

        if (Input.GetKeyDown(KeyCode.Z))
        {
            // ★修正：プレハブとインデックスの両方を渡す
            BossPracticeManager.SetPracticePhase(bossPrefabs[bossIndex], phaseIndex);
            SEManager.Instance.Play(SEPath.MENUDECIDE, 0.5f);
            SceneManager.LoadScene(gameSceneName);
        }
        if (Input.GetKeyDown(KeyCode.X))
        {
            currentState = SelectionState.BossSelect;
            phaseIndex = 0;
            SEManager.Instance.Play(SEPath.MENUCANCEL, 0.5f);
            UpdateDisplay();
        }
    }

    void UpdateDisplay()
    {
        if (bossPrefabs.Count == 0) return;

        // 1. ボス情報の取得
        EnemyStatus status = bossPrefabs[bossIndex].GetComponent<EnemyStatus>();
        bossNameText.text = $" {status.bossName} ";

        // 2. 段階（フェーズ）名の割り当て
        var phases = status.Phases;
        for (int i = 0; i < phaseTexts.Length; i++)
        {
            if (i < phases.Count)
            {
                phaseTexts[i].gameObject.SetActive(true);
                phaseTexts[i].text = phases[i].phaseName;

                // 色の反映（ボス選択中は全消灯、段階選択中のみハイライト）
                if (currentState == SelectionState.PhaseSelect && i == phaseIndex)
                    phaseTexts[i].color = selectedColor;
                else
                    phaseTexts[i].color = unselectedColor;
            }
            else
            {
                // 設定された段階数を超える TMP は非表示にする
                phaseTexts[i].gameObject.SetActive(false);
            }
        }
    }
}