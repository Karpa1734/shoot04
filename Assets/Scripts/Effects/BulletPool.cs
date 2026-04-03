using UnityEngine;
using System.Collections.Generic;

public class BulletPool : MonoBehaviour
{
    public static BulletPool Instance;

    private Dictionary<GameObject, Stack<GameObject>> poolDict = new Dictionary<GameObject, Stack<GameObject>>();

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;

        if (!poolDict.ContainsKey(prefab))
        {
            poolDict[prefab] = new Stack<GameObject>();
        }

        GameObject obj;
        if (poolDict[prefab].Count > 0)
        {
            obj = poolDict[prefab].Pop();

            // �|�b�v�����e�����ɔj�󂳂�Ă����ꍇ�̈��S��
            if (obj == null) return Get(prefab, position, rotation);

            obj.transform.position = position;
            obj.transform.rotation = rotation;
            obj.SetActive(true);

            // --- �d�v�F���������̍ď����� ---
            Rigidbody2D rb = obj.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.simulated = true; // �O��� Simulated �I�t�������p���Ȃ��悤�ɋ����I��
                rb.linearVelocity = Vector2.zero;
            }
        }
        else
        {
            obj = Instantiate(prefab, position, rotation);
            EnemyBullet eb = obj.GetComponent<EnemyBullet>();
            if (eb != null) eb.originPrefab = prefab;
        }
        return obj;
    }

    public void Release(GameObject prefab, GameObject obj)
    {
        if (prefab == null || obj == null) return;

        // ���łɔ�A�N�e�B�u�i�v�[���ς݁j�Ȃ牽�����Ȃ��i2�d�����[�X�h�~�j
        if (!obj.activeSelf) return;

        obj.SetActive(false);

        // �L�[���Ȃ��ꍇ�ւ̑Ή�
        if (!poolDict.ContainsKey(prefab))
        {
            poolDict[prefab] = new Stack<GameObject>();
        }

        poolDict[prefab].Push(obj);
    }
}