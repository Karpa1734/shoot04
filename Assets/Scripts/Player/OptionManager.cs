using UnityEngine;

public class OptionManager : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private GameObject optionPrefab;
    private GameObject[] options = new GameObject[4];

    [Header("Distance Settings (Adjust in Inspector)")]
    public Vector3[] hiOffsets = {
        new Vector3(0.45f, -0.2f, 0),
        new Vector3(-0.45f, -0.2f, 0),
        new Vector3(-0.25f, -0.3f, 0),
        new Vector3(0.25f, -0.3f, 0)
    };

    public Vector3[] lowOffsets = {
        new Vector3(0.25f, 0.3f, 0),
        new Vector3(-0.25f, 0.3f, 0),
        new Vector3(-0.1f, 0.35f, 0),
        new Vector3(0.1f, 0.35f, 0)
    };

    [Range(0, 1)]
    public float lerpFactor = 0.3f;

    // --- 追加したメソッド ---
    /// <summary>
    /// 生成されたオプションの配列を返します。
    /// ショット発射スクリプトなどで使用します。
    /// </summary>
    public GameObject[] GetOptions()
    {
        return options;
    }
    // -----------------------

    void Start()
    {
        for (int i = 0; i < 4; i++)
        {
            options[i] = Instantiate(optionPrefab, player.position, Quaternion.identity);
        }
    }

    void FixedUpdate()
    {// 自機が消えていたら何もしない（安全装置）
        if (player == null) return;
        bool isSlow = Input.GetKey(KeyCode.LeftShift);

        for (int i = 0; i < 4; i++)
        {
            if (options[i] == null) continue;

            Vector3 targetOffset = isSlow ? lowOffsets[i] : hiOffsets[i];
            Vector3 targetPos = player.position + targetOffset;

            options[i].transform.position = Vector3.Lerp(options[i].transform.position, targetPos, lerpFactor);

            options[i].transform.Rotate(0, 0, 360f / 54f);
        }
    }
}