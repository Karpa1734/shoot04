using System.Collections;
using System.Collections.Generic;
using System.Linq; // シャッフル用
using UnityEngine;
using UnityEngine.UI; // UI操作のために追加
using KanKikuchi.AudioManager;
public class SpellManager : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject sealPrefab;
    public GameObject shockwavePrefab;

    [Header("Single Sprites")]
    public Sprite sealSprite;
    public Sprite shockwaveSprite;

    [Header("UI Elements")]
    // --- 追加：Canvas内の暗転用画像オブジェクト（DarkOverlay等） ---
    public GameObject darkOverlay;
    public SpellCardUI spellUI; // 追加：UIスクリプトの参照
    public EnemyStatus bossStatus;
    private bool isOnSpell = false;

    // --- SpellManager.cs ---

    void Update()
    {
        if (Time.timeScale <= 0) return;
        if (Input.GetKeyDown(KeyCode.X) && !isOnSpell)
        {// 所持している場合のみ発動
            if (PlayerStatusManager.Instance.UseSpell())
            {
                PlayerHitHandler hitHandler = PlayerMove.Instance.GetComponentInChildren<PlayerHitHandler>();

                if (hitHandler != null)
                {
                    if (hitHandler.currentState == PlayerHitHandler.PlayerState.Normal ||
                        hitHandler.currentState == PlayerHitHandler.PlayerState.DeathBomb)
                    {
                        EnemyStatus boss = Object.FindFirstObjectByType<EnemyStatus>();
                        if (boss != null)
                        {
                            boss.FailSpell();
                        }


                        StartCoroutine(ExecuteFantasySeal());
                    }
                }
            }
        }
    }

    IEnumerator ExecuteFantasySeal()
    {
        isOnSpell = true;
        SEManager.Instance.Play(SEPath.SLASH, 0.5f);

        SEManager.Instance.Play(SEPath.LASER7,0.5f);

        float invincibilityDuration = 360f / 60f; // 5.33秒
        if (spellUI != null)
        {
            spellUI.gameObject.SetActive(true); // UIオブジェクト本体をアクティブにする
            spellUI.DisplaySpell("霊符「夢想封印」", invincibilityDuration);
        }
        // --- 追加：背景の暗転を開始 ---
        if (darkOverlay != null) darkOverlay.SetActive(true);

        // 無敵時間を設定（285フレーム相当） [cite: 7]
        PlayerMove.Instance.SetInvincible(360f / 60f);
        /*
        if (shockwavePrefab != null)
        {
            GameObject shock = Instantiate(shockwavePrefab, transform.position, Quaternion.identity);
            Shockwave logic = shock.GetComponent<Shockwave>();
            if (logic != null)
            {
                // 初期衝撃波は白（デフォルト）のままとします
               // logic.InitializeWithCustomScale(shockwaveSprite, Color.white, 1.0f, 0.05f);
            }
        }
        */
        // --- ホーミング順序のランダム化 ---
        int[] homingOrders = { 0, 1, 2, 3, 4, 5, 6, 7 };
        Shuffle(homingOrders); // 順番をバラバラにする

        // 8つの封印弾を一斉に生成 
        for (int i = 0; i < 8; i++)
        {
            float startAngle = i * 45f;

            // 虹色＋白を順番に割り当てる
            Color c = GetSealColor(i);

            // 生成は同時だが、内部に「ホーミングの順番(homingOrders[i])」を渡す
            SpawnSealImmediate(startAngle, c, homingOrders[i]);
        }

        // スペル持続時間（255フレーム相当）の待機 [cite: 7]
        yield return new WaitForSeconds(360f / 60f);

        // --- 追加：背景の暗転を終了 ---
        if (darkOverlay != null) darkOverlay.SetActive(false);

        SEManager.Instance.Play(SEPath.POWER36, 0.5f);
        isOnSpell = false;
    }

    // 配列をシャッフルするヘルパー
    void Shuffle(int[] array)
    {
        for (int i = array.Length - 1; i > 0; i--)
        {
            int r = Random.Range(0, i + 1);
            int tmp = array[i];
            array[i] = array[r];
            array[r] = tmp;
        }
    }

    void SpawnSealImmediate(float angle, Color color, int order)
    {
        GameObject seal = Instantiate(sealPrefab, transform.position, Quaternion.identity);
        SealOrb logic = seal.GetComponent<SealOrb>();
        if (logic != null)
        {
            logic.Initialize(sealSprite, shockwaveSprite, angle, order, color, bossStatus, shockwavePrefab);
        }
    }

    // 虹色＋白を返すメソッド
    private Color GetSealColor(int index)
    {
        // 色の配列：白、赤、橙、黄、緑、青、藍、紫
        Color[] rainbowPlusWhite = new Color[]
        {
            Color.white,                     // 0: 白
            new Color(1f, 0f, 0f),           // 1: 赤
            new Color(1f, 0.5f, 0f),         // 2: 橙
            new Color(1f, 1f, 0f),           // 3: 黄
            new Color(0f, 1f, 0f),           // 4: 緑
            new Color(0f, 1f, 1f),           // 5: 青
            new Color(0f, 0f, 1f),     // 6: 藍
            new Color(1f, 0f, 1f)          // 7: 紫
        };

        return rainbowPlusWhite[index % 8];
    }
}