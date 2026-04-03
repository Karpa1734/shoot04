using UnityEngine;
using KanKikuchi.AudioManager; // SE再生用

public class EnemyBit : MonoBehaviour
{
    private Transform boss;
    private float targetRadius, expandTime, orbitSpeed, currentAngle;
    private float currentRadius = 0f, elapsed = 0f;

    public void Setup(Transform boss, float radius, float time, float speed, float startAngle)
    {
        this.boss = boss;
        this.targetRadius = radius;
        this.expandTime = time;
        this.orbitSpeed = speed;
        this.currentAngle = startAngle;
    }

    void Update()
    {
        if (boss == null) return;

        if (elapsed < expandTime)
        {
            elapsed += Time.deltaTime;
            currentRadius = Mathf.Lerp(0, targetRadius, elapsed / expandTime);
        }

        currentAngle += orbitSpeed * Time.deltaTime;
        float rad = currentAngle * Mathf.Deg2Rad;

        Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * currentRadius;
        transform.position = boss.position + offset;

        transform.Rotate(0, 0, 200f * Time.deltaTime);
    }

    // ★オブジェクトが破壊されたときに自動で呼ばれる
    private void OnDestroy()
    {
        // アプリ終了時やシーン遷移時に生成しようとするとエラーになるためのガード
        if (!gameObject.scene.isLoaded) return;

        if (BossEffectManager.Instance != null)
        {
            BossEffectManager.Instance.PlayEnemyDeathEffect(transform.position, Color.white);
        }

    }
}