using System.Collections.Generic;
using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    // ★ 全プレイヤーを管理するリスト
    private static List<PlayerMove> _allPlayers = new List<PlayerMove>();
    public static IReadOnlyList<PlayerMove> AllPlayers => _allPlayers;

    // ★ エラー解消のための「後方互換性」プロパティ
    // これを復活させることで、既存の敵やアイテムのスクリプトが壊れなくなります
    public static PlayerMove Instance => (_allPlayers != null && _allPlayers.Count > 0) ? _allPlayers[0] : null;

    [System.Serializable]
    public struct ReplayFrame
    {
        public float h, v;
        public bool slow, shotZ, shotX, shotC, shotV;
        public bool shot => shotZ;
        public bool bomb => shotX;
    }

    public enum ReplayMode { None, Recording, Playing }
    public ReplayMode currentMode = ReplayMode.None;
    public List<ReplayFrame> replayData = new List<ReplayFrame>();
    private int currentFrame = 0;
    public ReplayFrame currentFrameInput;

    private float invincibleTimer = 0f;
    private float deathBombTimer = 0f;
    public bool IsInvincible => invincibleTimer > 0;
    public bool IsInDeathBombWindow => deathBombTimer > 0;

    private SpriteRenderer sr;

    void Awake() => Time.timeScale = 1f;

    void OnEnable() { if (!_allPlayers.Contains(this)) _allPlayers.Add(this); }
    void OnDisable() { _allPlayers.Remove(this); }

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
    }

    void Update()
    {
        if (invincibleTimer > 0) invincibleTimer -= Time.deltaTime;
        if (deathBombTimer > 0) deathBombTimer -= Time.deltaTime;
        UpdateReplayLogic();
    }

    private void UpdateReplayLogic()
    {
        if (currentMode == ReplayMode.Playing && currentFrame < replayData.Count)
        {
            currentFrameInput = replayData[currentFrame];
            currentFrame++;
        }
        else if (currentMode == ReplayMode.Recording)
        {
            replayData.Add(currentFrameInput);
        }
    }

    void LateUpdate()
    {
        if (IsInvincible) UpdateInvincibleVisual();
        else if (sr != null && sr.color != Color.white) ResetVisual();
    }

    public void SetInvincible(float duration) => invincibleTimer = duration;
    public void StartDeathBombWindow(float duration) { if (!IsInvincible) deathBombTimer = duration; }

    private void UpdateInvincibleVisual()
    {
        if (sr == null) return;
        float pingPong = Mathf.PingPong(Time.time * 20f, 1f);
        sr.color = Color.Lerp(new Color(0.4f, 0.4f, 1f, 0.5f), new Color(1f, 1f, 1f, 0.8f), pingPong);
    }
    private void ResetVisual() { if (sr != null) sr.color = Color.white; }
}