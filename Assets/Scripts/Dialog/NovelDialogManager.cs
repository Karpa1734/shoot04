using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace NovelSystem
{
    public enum NovelFocusType
    {
        LeftFocus = 0,  // 左が前に出る
        RightFocus = 1, // 右が前に出る
        BothFocus = 2   // 両方が前に出る
    }

    [System.Serializable]
    public class DialogueRow
    {
        public string text;
        public int emotionLeft;    // 左側（1P）の表情、または勝者の表情
        public int emotionRight;   // 右側（2P）の表情、または敗者の表情
        public Color textColor;
        public string rawColorStr;
        public NovelFocusType focus; // ストーリーモード用の直接指定フォーカス
    }

    public class NovelDialogManager : MonoBehaviour
    {
        [System.Serializable]
        public struct CharacterNovelAsset
        {
            public string characterName;
            [Tooltip("ストーリーモード用：全表情Spriteのリスト（インデックス指定用）")]
            public List<Sprite> emotionSprites;
            [Tooltip("対戦モード用：通常時（勝者側）の立ち絵1枚")]
            public Sprite normalSprite;
            [Tooltip("対戦モード用：ボロ絵（敗者側）の立ち絵1枚")]
            public Sprite damagedSprite;
            [Tooltip("キャラ固有色の頭文字（R, B, G, Y, A, P, O, W）")]
            public string colorLetter;
        }

        // =========================================================================
        // ⭕【エラー原因の根治】：1マッチに2つのCSV（P1Win / P2Win）を美しく内包する決定版構造体
        // =========================================================================
        [System.Serializable]
        public struct CSVPair
        {
            [Tooltip("対戦カード名を 1Pキャラ名_vs_2Pキャラ名 の形式で入力（例: Karin_vs_Charlotte）")]
            public string matchCombinationName;
            [Tooltip("Player1（左のキャラ）が勝利した場合の勝利セリフCSV")]
            public TextAsset p1WinCSV;
            [Tooltip("Player2（右のキャラ）が勝利した場合の勝利セリフCSV")]
            public TextAsset p2WinCSV;
        }

        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI dialogueText;
        [SerializeField] private GameObject textBoxObject;
        [SerializeField] private GameObject talkingCanvas;

        [Header("Character UI Images")]
        [SerializeField] private Image leftCharacterImage;  // 左立ち絵（常に1P）
        [SerializeField] private Image rightCharacterImage; // 右立ち絵（常に2P・自動反転）

        [Header("Character Asset Database (Dynamic Mapping)")]
        [Tooltip("全キャラクターのデータ（名前・表情リスト・通常/ボロ絵・固有色）を登録")]
        [SerializeField] private List<CharacterNovelAsset> characterDatabase = new List<CharacterNovelAsset>();

        [Header("📋 CSV File Database (For Match Mode)")]
        [Tooltip("対戦モード用のCSVファイルをここにセットで登録")]
        [SerializeField] private List<CSVPair> csvDatabase = new List<CSVPair>();

        [Header("📘 Story Mode CSV Slot (Direct Override)")]
        [Tooltip("ストーリーモード等、対戦結果に関係なく特定のCSVを直接流し込みたい場合はここにセットしてStartDialogue()を呼ぶ")]
        [SerializeField] private TextAsset csvFile;

        [Header("Text & Advance Settings")]
        [SerializeField] private float textSpeed = 0.03f;
        [SerializeField] private float autoAdvanceDelay = 3.0f;

        // 現在適用されている左右のキャラのデータバッファ
        private CharacterNovelAsset _leftCharAsset;
        private CharacterNovelAsset _rightCharAsset;

        // =========================================================================
        // ⭕【エラー原因の根治】：不足していた話し手自動逆算ロジック用のプライベート変数
        // =========================================================================
        private int _winnerPlayerId = 1;
        private string _p1ColorLetter = "W";
        private string _p2ColorLetter = "W";
        private bool _isMatchMode = true; // CSVの列数から自動判別するフラグ

        private List<DialogueRow> _dialogueDataList = new List<DialogueRow>();
        private int _currentLineIndex = 0;
        private bool _isTyping = false;
        private string _currentCompleteText = "";

        private Coroutine _typingCoroutine;
        private Coroutine _autoAdvanceCoroutine;

        private Vector3 _leftBasePosition;
        private Vector3 _rightBasePosition;
        private Vector3 _leftBaseScale;
        private Vector3 _rightBaseScale;

        public static bool isTalking { get; private set; } = false;

        private void Awake()
        {
            if (leftCharacterImage != null)
            {
                _leftBasePosition = leftCharacterImage.rectTransform.localPosition;
                _leftBaseScale = leftCharacterImage.rectTransform.localScale;
            }
            if (rightCharacterImage != null)
            {
                _rightBasePosition = rightCharacterImage.rectTransform.localPosition;
                _rightBaseScale = rightCharacterImage.rectTransform.localScale;
            }
        }

        private void Start()
        {
            if (talkingCanvas != null) talkingCanvas.SetActive(false);
        }

        private void Update()
        {
            if (!isTalking) return;
            if (Input.GetKeyDown(KeyCode.Z) || Input.GetMouseButtonDown(0)) OnNextInput();
        }

        /// <summary>
        /// 👑【対戦モード用エントランス】：1マッチ2CSV動的選択 ✕ 双方向ミラーリング対応
        /// </summary>
        public void StartVictoryDialogue(string p1Name, string p2Name, int winnerPlayerId)
        {
            _isMatchMode = true;
            _winnerPlayerId = winnerPlayerId;

            // 1. 🎯【対戦カード名の生成】：通常の並び（1P_vs_2P）
            string currentMatchKey = $"{p1Name}_vs_{p2Name}";
            // 🔄 反転した並び（2P_vs_1P）のキーも用意
            string reversedMatchKey = $"{p2Name}_vs_{p1Name}";

            TextAsset matchedCSV = null;

            // 🌟 インスペクターの登録枠から最適なCSVを検索
            foreach (var pair in csvDatabase)
            {
                // 🔹 パターンA: 1P・2Pの立ち位置が登録通り（例: Karin vs Charlotte）
                if (pair.matchCombinationName == currentMatchKey)
                {
                    matchedCSV = (winnerPlayerId == 1) ? pair.p1WinCSV : pair.p2WinCSV;
                    break;
                }
                // 🔹 パターンB: 1P・2Pの立ち位置が登録と逆（例: Charlotte vs Karin）
                // 💡 1つの枠だけで両側から使い回せるように、勝敗判定を内部で反転させて辻褄を合わせます！
                else if (pair.matchCombinationName == reversedMatchKey)
                {
                    matchedCSV = (winnerPlayerId == 1) ? pair.p2WinCSV : pair.p1WinCSV;
                    break;
                }
            }

            if (matchedCSV != null)
            {
                csvFile = matchedCSV;
            }
            else
            {
                Debug.LogWarning($"❌ Csv Database 内に対戦カード [{currentMatchKey}] または [{reversedMatchKey}] の登録が見つかりません。");
            }

            // =========================================================================
            // ⭕【エラー原因の根治】：不足していた固有色文字の抽出とキャッシュ
            // =========================================================================
            _p1ColorLetter = GetColorLetterFromDatabase(p1Name);
            _p2ColorLetter = GetColorLetterFromDatabase(p2Name);

            // データベースからキャラデータを取得して保持
            _leftCharAsset = GetAssetFromDatabase(p1Name);
            _rightCharAsset = GetAssetFromDatabase(p2Name);

            LoadDialogueFromCSV();
            SetupInitialImages();
            StartDialogue();
        }

        /// <summary>
        /// 📘【ストーリーモード用エントランス】
        /// </summary>
        public void StartStoryDialogue(string p1Name, string p2Name, TextAsset storyCSV)
        {
            _isMatchMode = false;
            if (storyCSV != null) csvFile = storyCSV;

            _leftCharAsset = GetAssetFromDatabase(p1Name);
            _rightCharAsset = GetAssetFromDatabase(p2Name);

            LoadDialogueFromCSV();
            SetupInitialImages();
            StartDialogue();
        }

        // =========================================================================
        // ⭕【エラー原因の根治】：不足していた固有色文字取得関数の実装
        // =========================================================================
        private string GetColorLetterFromDatabase(string charName)
        {
            foreach (var asset in characterDatabase)
            {
                if (asset.characterName == charName) return asset.colorLetter.ToUpper();
            }
            return "W";
        }

        private CharacterNovelAsset GetAssetFromDatabase(string charName)
        {
            foreach (var asset in characterDatabase)
            {
                if (asset.characterName == charName) return asset;
            }
            Debug.LogError($"❌ キャラクター [{charName}] が登録されていません！");
            return new CharacterNovelAsset();
        }

        private void SetupInitialImages()
        {
            // 右側の自動反転
            if (rightCharacterImage != null) rightCharacterImage.rectTransform.localEulerAngles = new Vector3(0f, 180f, 0f);
            if (leftCharacterImage != null) leftCharacterImage.rectTransform.localEulerAngles = Vector3.zero;

            // 対戦モードの場合、ここで勝敗に応じて「通常絵」「ボロ絵」をガチッと固定
            if (_isMatchMode)
            {
                if (leftCharacterImage != null)
                {
                    leftCharacterImage.sprite = (_winnerPlayerId == 1) ? _leftCharAsset.normalSprite : _leftCharAsset.damagedSprite;
                    ApplyNovelAspectFit(leftCharacterImage, leftCharacterImage.sprite);
                }
                if (rightCharacterImage != null)
                {
                    rightCharacterImage.sprite = (_winnerPlayerId == 2) ? _rightCharAsset.normalSprite : _rightCharAsset.damagedSprite;
                    ApplyNovelAspectFit(rightCharacterImage, rightCharacterImage.sprite);
                }
            }
        }

        private void LoadDialogueFromCSV()
        {
            if (csvFile == null) return;
            _dialogueDataList.Clear();

            using (StringReader reader = new StringReader(csvFile.text))
            {
                string line;
                bool isHeader = true;

                while ((line = reader.ReadLine()) != null)
                {
                    if (isHeader) { isHeader = false; continue; }
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    string[] fields = line.Split(',');
                    if (fields.Length < 2) continue;

                    DialogueRow row = new DialogueRow();
                    row.text = fields[0].Replace("<>", "\n");

                    // 🌟【ハイブリッドパース】：列数（フィールド数）でモード別の読み込みを自動スイッチ！
                    if (fields.Length >= 5)
                    {
                        // 📘 ストーリーモード仕様（5列構成：Text, Emotion1P, Emotion2P, Color, Focus）
                        int.TryParse(fields[1], out row.emotionLeft);
                        int.TryParse(fields[2], out row.emotionRight);
                        row.rawColorStr = fields[3].Trim().ToUpper();

                        int.TryParse(fields[4], out int focusId);
                        row.focus = (NovelFocusType)Mathf.Clamp(focusId, 0, 2);
                    }
                    else
                    {
                        // 👑 対戦モード仕様（2〜4列構成：Text, WinnerEmotion(任意), LoserEmotion(任意), Color）
                        if (fields.Length > 2) int.TryParse(fields[1], out row.emotionLeft);
                        if (fields.Length > 3) int.TryParse(fields[2], out row.emotionRight);

                        // 色指定の取得（最後の列が色になるケースを想定）
                        row.rawColorStr = fields[fields.Length - 1].Trim().ToUpper();
                        if (row.rawColorStr.Length != 1) row.rawColorStr = "W";
                    }

                    row.textColor = ParseColorName(row.rawColorStr);
                    _dialogueDataList.Add(row);
                }
            }
        }

        public void StartDialogue()
        {
            if (_dialogueDataList.Count == 0) return;
            if (talkingCanvas != null) talkingCanvas.SetActive(true);
            isTalking = true;
            _currentLineIndex = 0;
            if (textBoxObject != null) textBoxObject.SetActive(true);

            // 最初はアルファ0から登場
            if (leftCharacterImage != null)
            {
                leftCharacterImage.gameObject.SetActive(true);
                leftCharacterImage.color = new Color(1f, 1f, 1f, 0f);
                leftCharacterImage.rectTransform.localPosition = _leftBasePosition - new Vector3(50f, 0f, 0f);
            }
            if (rightCharacterImage != null)
            {
                rightCharacterImage.gameObject.SetActive(true);
                rightCharacterImage.color = new Color(1f, 1f, 1f, 0f);
                rightCharacterImage.rectTransform.localPosition = _rightBasePosition + new Vector3(50f, 0f, 0f);
            }

            ShowLine(_currentLineIndex);
        }

        private void ShowLine(int index)
        {
            if (index < 0 || index >= _dialogueDataList.Count) { EndDialogue(); return; }

            DialogueRow row = _dialogueDataList[index];
            dialogueText.color = row.textColor;

            NovelFocusType targetFocus = NovelFocusType.BothFocus;

            if (_isMatchMode)
            {
                // 👑 対戦モード時の挙動：立ち絵は固定のまま、色からフォーカスのみを動的に逆算
                string p1Color = string.IsNullOrEmpty(_leftCharAsset.colorLetter) ? "W" : _leftCharAsset.colorLetter.ToUpper();
                string p2Color = string.IsNullOrEmpty(_rightCharAsset.colorLetter) ? "W" : _rightCharAsset.colorLetter.ToUpper();

                if (row.rawColorStr == p1Color) targetFocus = NovelFocusType.LeftFocus;
                else if (row.rawColorStr == p2Color) targetFocus = NovelFocusType.RightFocus;
            }
            else
            {
                // 📘 ストーリーモード時の挙動：毎行CSVの指示に従う
                if (_leftCharAsset.emotionSprites != null && _leftCharAsset.emotionSprites.Count > 0)
                {
                    int l = Mathf.Clamp(row.emotionLeft, 0, _leftCharAsset.emotionSprites.Count - 1);
                    if (leftCharacterImage != null)
                    {
                        leftCharacterImage.sprite = _leftCharAsset.emotionSprites[l];
                        ApplyNovelAspectFit(leftCharacterImage, leftCharacterImage.sprite);
                    }
                }
                if (_rightCharAsset.emotionSprites != null && _rightCharAsset.emotionSprites.Count > 0)
                {
                    int r = Mathf.Clamp(row.emotionRight, 0, _rightCharAsset.emotionSprites.Count - 1);
                    if (rightCharacterImage != null)
                    {
                        rightCharacterImage.sprite = _rightCharAsset.emotionSprites[r];
                        ApplyNovelAspectFit(rightCharacterImage, rightCharacterImage.sprite);
                    }
                }
                targetFocus = row.focus;
            }

            ApplyTouhouFocusVisuals(targetFocus);

            _currentCompleteText = row.text;
            if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
            _typingCoroutine = StartCoroutine(TypeTextRoutine(row.text));
        }
        /// <summary>
        /// 🖼️ 立ち絵Spriteの元サイズ（縦横比）を基準に、ImageのRectTransformの大きさを綺麗にフィットさせるヘルパー
        /// </summary>
        private void ApplyNovelAspectFit(Image targetImage, Sprite sprite)
        {
            if (targetImage == null || sprite == null) return;

            // 比例を維持する機能をON
            targetImage.preserveAspect = true;

            RectTransform rectTransform = targetImage.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                float spriteWidth = sprite.rect.width;
                float spriteHeight = sprite.rect.height;

                if (spriteWidth > 0f && spriteHeight > 0f)
                {
                    float aspectRatio = spriteWidth / spriteHeight;

                    // 現在の高さを基準にして、比率に合わせたサイズへ補正
                    float currentHeight = rectTransform.sizeDelta.y;
                    if (currentHeight <= 0f) currentHeight = rectTransform.rect.height;
                    if (currentHeight <= 0f) currentHeight = 600f; // フォールバック用基準高さ

                    rectTransform.sizeDelta = new Vector2(currentHeight * aspectRatio, currentHeight);
                }
            }
        }
        private void OnNextInput()
        {
            if (_isTyping)
            {
                if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
                dialogueText.text = _currentCompleteText;
                _isTyping = false;
                StartAutoAdvanceTimer();
            }
            else
            {
                _currentLineIndex++;
                ShowLine(_currentLineIndex);
            }
        }

        private void ApplyTouhouFocusVisuals(NovelFocusType focusType)
        {
            float duration = 0.3f;
            float focusAlpha = 1.0f;
            float dimAlpha = 0.45f;
            float slideOffset = 35f;

            if (focusType == NovelFocusType.LeftFocus)
            {
                leftCharacterImage?.DOFade(focusAlpha, duration);
                leftCharacterImage?.DOColor(new Color(focusAlpha, focusAlpha, focusAlpha), duration);
                leftCharacterImage?.rectTransform.DOScale(_leftBaseScale * 1.04f, duration);
                leftCharacterImage?.rectTransform.DOLocalMove(new Vector3(_leftBasePosition.x + slideOffset, _leftBasePosition.y, 0f), duration).SetEase(Ease.OutQuad);

                rightCharacterImage?.DOFade(dimAlpha, duration);
                rightCharacterImage?.DOColor(new Color(dimAlpha, dimAlpha, dimAlpha), duration);
                rightCharacterImage?.rectTransform.DOScale(_rightBaseScale * 1.00f, duration);
                rightCharacterImage?.rectTransform.DOLocalMove(_rightBasePosition, duration).SetEase(Ease.OutQuad);
            }
            else if (focusType == NovelFocusType.RightFocus)
            {
                leftCharacterImage?.DOFade(dimAlpha, duration);
                leftCharacterImage?.DOColor(new Color(dimAlpha, dimAlpha, dimAlpha), duration);
                leftCharacterImage?.rectTransform.DOScale(_leftBaseScale * 1.00f, duration);
                leftCharacterImage?.rectTransform.DOLocalMove(_leftBasePosition, duration).SetEase(Ease.OutQuad);

                rightCharacterImage?.DOFade(focusAlpha, duration);
                rightCharacterImage?.DOColor(new Color(focusAlpha, focusAlpha, focusAlpha), duration);
                rightCharacterImage?.rectTransform.DOScale(_rightBaseScale * 1.04f, duration);
                rightCharacterImage?.rectTransform.DOLocalMove(new Vector3(_rightBasePosition.x - slideOffset, _rightBasePosition.y, 0f), duration).SetEase(Ease.OutQuad);
            }
            else if (focusType == NovelFocusType.BothFocus)
            {
                leftCharacterImage?.DOFade(focusAlpha, duration);
                leftCharacterImage?.DOColor(new Color(focusAlpha, focusAlpha, focusAlpha), duration);
                leftCharacterImage?.rectTransform.DOScale(_leftBaseScale * 1.04f, duration);
                leftCharacterImage?.rectTransform.DOLocalMove(new Vector3(_leftBasePosition.x + slideOffset, _leftBasePosition.y, 0f), duration).SetEase(Ease.OutQuad);

                rightCharacterImage?.DOFade(focusAlpha, duration);
                rightCharacterImage?.DOColor(new Color(focusAlpha, focusAlpha, focusAlpha), duration);
                rightCharacterImage?.rectTransform.DOScale(_rightBaseScale * 1.04f, duration);
                rightCharacterImage?.rectTransform.DOLocalMove(new Vector3(_rightBasePosition.x - slideOffset, _rightBasePosition.y, 0f), duration).SetEase(Ease.OutQuad);
            }
        }

        private IEnumerator TypeTextRoutine(string targetText)
        {
            _isTyping = true;
            dialogueText.text = "";
            foreach (char letter in targetText.ToCharArray())
            {
                dialogueText.text += letter;
                yield return new WaitForSeconds(textSpeed);
            }
            _isTyping = false;
            StartAutoAdvanceTimer();
        }

        private void StartAutoAdvanceTimer()
        {
            if (_autoAdvanceCoroutine != null) StopCoroutine(_autoAdvanceCoroutine);
            _autoAdvanceCoroutine = StartCoroutine(AutoAdvanceRoutine());
        }

        private IEnumerator AutoAdvanceRoutine()
        {
            yield return new WaitForSecondsRealtime(autoAdvanceDelay);
            _currentLineIndex++;
            ShowLine(_currentLineIndex);
        }

        private Color ParseColorName(string colorLetter)
        {
            switch (colorLetter.ToUpper())
            {
                case "R": return new Color(1.0f, 0.35f, 0.35f);
                case "B": return new Color(0.35f, 0.6f, 1.0f);
                case "G": return new Color(0.35f, 1.0f, 0.45f);
                case "Y": return new Color(1.0f, 0.95f, 0.35f);
                case "A": return new Color(0.35f, 1.0f, 1.0f);
                case "P": return new Color(0.75f, 0.45f, 1.0f);
                case "O": return new Color(1.0f, 0.65f, 0.2f);
                case "W":
                default:
                    return Color.white;
            }
        }

        public void EndDialogue()
        {
            isTalking = false;
            if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
            if (_autoAdvanceCoroutine != null) StopCoroutine(_autoAdvanceCoroutine);

            float fadeDuration = 0.5f;
            leftCharacterImage?.DOFade(0f, fadeDuration);
            leftCharacterImage?.rectTransform.DOLocalMove(_leftBasePosition - new Vector3(50f, 0f, 0f), fadeDuration);

            rightCharacterImage?.DOFade(0f, fadeDuration);
            rightCharacterImage?.rectTransform.DOLocalMove(_rightBasePosition + new Vector3(50f, 0f, 0f), fadeDuration).OnComplete(() =>
            {
                if (textBoxObject != null) textBoxObject.SetActive(false);
                dialogueText.text = "";
                leftCharacterImage.gameObject.SetActive(false);
                rightCharacterImage.gameObject.SetActive(false);
                if (talkingCanvas != null) talkingCanvas.SetActive(false);
            });
        }
    }
}