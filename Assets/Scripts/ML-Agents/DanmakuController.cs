using System.Collections.Generic;
using UnityEngine;

// --- ���W�b�N�S���N���X�F�������Z��ړ��̎��s��������Ɉڏ� ---
public class DanmakuController : MonoBehaviour
{
    [SerializeField] private float highSpeed = 4.5f;
    [SerializeField] private float lowSpeed = 2.0f;

    [Header("Movement Bounds")]
    public float minX = -4.0f;
    public float maxX = 4.0f;
    public float minY = -4.5f;
    public float maxY = 4.5f;

    private Rigidbody2D rb;
    private PlayerMove shell;
    private PlayerHitHandler hitHandler; // �ǉ�
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        // ����I�u�W�F�N�g���̃V�F���iPlayerMove�j���擾
        shell = GetComponent<PlayerMove>(); 
        hitHandler = GetComponentInChildren<PlayerHitHandler>();
    }

    void FixedUpdate()
    {
        if (shell == null) return;
        // --- �� �ǉ��F�ړ������S�ɋ��ۂ������ ---
        // 1. �J�E���g�_�E����
        if (!PlayerMove.CanInput)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // 2. �X�^�����iNormal��ԈȊO�j
        if (hitHandler != null && hitHandler.currentState != PlayerHitHandler.PlayerState.Normal)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }
        // �V�F���Ɋi�[����Ă���u���݂̃t���[���̓��́v��ǂݎ��
        var input = shell.currentFrameInput;
        Vector2 inputVec = new Vector2(input.h, input.v);
        float speed = input.slow ? lowSpeed : highSpeed;

        // �����ړ��̌v�Z
        Vector2 velocity = inputVec.normalized * speed;
        Vector2 nextPosition = rb.position + velocity * Time.fixedDeltaTime;

        // �ړ��͈͂̃N�����v
        nextPosition.x = Mathf.Clamp(nextPosition.x, minX, maxX);
        nextPosition.y = Mathf.Clamp(nextPosition.y, minY, maxY);
        rb.MovePosition(nextPosition);
    }
}