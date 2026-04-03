using UnityEngine;

public class BackgroundRotator : MonoBehaviour
{
    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 30f; // 1•bŠÔ‚É‰ñ“]‚·‚é“x”

    void Update()
    {
        // ’†S‚ğ²‚É‰ñ“]
        transform.Rotate(0, 0, rotationSpeed * Time.unscaledDeltaTime);
    }
}