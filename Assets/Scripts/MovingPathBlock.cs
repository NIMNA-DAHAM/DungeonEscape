using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class MovingPathBlock : MonoBehaviour
{
    [SerializeField, Min(0f)] private float travelDistance = 2f;
    [SerializeField, Min(0f)] private float moveSpeed = 1.5f;
    [SerializeField, Range(0f, 1f)] private float phaseOffset;

    private Rigidbody2D body;
    private Vector2 centerPosition;

    public void Configure(Vector2 center, float travel, float speed, float phase)
    {
        centerPosition = center;
        travelDistance = Mathf.Max(0f, travel);
        moveSpeed = Mathf.Max(0f, speed);
        phaseOffset = Mathf.Clamp01(phase);
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        centerPosition = body.position;
    }

    private void FixedUpdate()
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying)
        {
            return;
        }

        float offset = Mathf.PingPong((Time.fixedTime + phaseOffset) * moveSpeed, travelDistance * 2f)
                       - travelDistance;
        body.MovePosition(centerPosition + Vector2.up * offset);
    }
}
