using UnityEngine;

/// <summary>
/// Patrols the enemy back and forth between two world points.
/// Touching the player removes one life.
/// </summary>
public class EnemyMovement : MonoBehaviour
{
    private const float MinimumPatrolSpeed = 3f;

    [Header("Patrol Points")]
    [Tooltip("First patrol position in world space.")]
    [SerializeField] private Transform pointA;

    [Tooltip("Second patrol position in world space.")]
    [SerializeField] private Transform pointB;

    [Header("Movement")]
    [Tooltip("How fast the enemy moves between points.")]
    [SerializeField] private float moveSpeed = 2f;

    [Tooltip("How fast the enemy chases the player after the key is collected.")]
    [SerializeField] private float chaseSpeed = 4.25f;

    [Tooltip("How close the enemy must get before reversing.")]
    [SerializeField] private float arriveDistance = 0.05f;

    private Transform currentTarget;
    private PlayerMovement player;
    private Rigidbody2D body;
    private float movementSpeedMultiplier = 1f;
    private bool circularPatrol;
    private Vector2 circularCenter;
    private float circularRadius;
    private float circularAngle;

    public static EnemyMovement CreateAdditionalEnemy(EnemyMovement template, string enemyName)
    {
        if (template == null)
        {
            Debug.LogError("[DungeonEscape] Cannot create another enemy without a template.");
            return null;
        }

        GameObject clone = UnityEngine.Object.Instantiate(
            template.gameObject, template.transform.position, template.transform.rotation);
        clone.name = enemyName;

        GameObject path = new GameObject(enemyName + "Path");
        Transform newPointA = new GameObject("PointA").transform;
        Transform newPointB = new GameObject("PointB").transform;
        newPointA.SetParent(path.transform, false);
        newPointB.SetParent(path.transform, false);

        EnemyMovement movement = clone.GetComponent<EnemyMovement>();
        movement.SetPatrolPoints(newPointA, newPointB);
        return movement;
    }

    public void SetPatrolPoints(Transform startPoint, Transform endPoint)
    {
        if (startPoint == null || endPoint == null)
        {
            Debug.LogError("[DungeonEscape] An enemy requires two patrol points.", this);
            return;
        }

        pointA = startPoint;
        pointB = endPoint;
        currentTarget = pointB;
    }

    public void SetPatrolPositions(Vector2 startPosition, Vector2 endPosition)
    {
        circularPatrol = false;
        Vector3 start = new Vector3(startPosition.x, startPosition.y, transform.position.z);
        transform.position = start;

        if (pointA == null || pointB == null)
        {
            Debug.LogError("[DungeonEscape] Randomized enemy placement requires two patrol points.", this);
            return;
        }

        pointA.position = start;
        pointB.position = new Vector3(endPosition.x, endPosition.y, transform.position.z);
        currentTarget = pointB;
    }

    public void SetMovementSpeedMultiplier(float multiplier)
    {
        movementSpeedMultiplier = multiplier;
    }

    public void SetCircularPatrol(Vector2 center, float radius, float startAngleDegrees)
    {
        if (radius <= 0f)
        {
            Debug.LogError("[DungeonEscape] A circular patrol requires a positive radius.", this);
            return;
        }

        circularPatrol = true;
        circularCenter = center;
        circularRadius = radius;
        circularAngle = startAngleDegrees;
        SetPositionOnCircle();
    }

    private void Start()
    {
        body = GetComponent<Rigidbody2D>();
        player = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();

        // Start by moving toward point B (or A if B is missing).
        if (pointB != null)
        {
            currentTarget = pointB;
        }
        else if (pointA != null)
        {
            currentTarget = pointA;
        }
    }

    private void FixedUpdate()
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying)
        {
            return;
        }

        if (!circularPatrol && GameManager.Instance != null && GameManager.Instance.HasKey && player != null)
        {
            MoveTowards(player.transform.position, chaseSpeed * movementSpeedMultiplier);
            return;
        }

        if (circularPatrol)
        {
            circularAngle = Mathf.Repeat(
                circularAngle + Mathf.Rad2Deg * Mathf.Max(moveSpeed, MinimumPatrolSpeed) *
                movementSpeedMultiplier /
                circularRadius * Time.fixedDeltaTime,
                360f);
            SetPositionOnCircle();
            return;
        }

        if (currentTarget == null || pointA == null || pointB == null)
        {
            return;
        }

        // Move toward the current target.
        MoveTowards(currentTarget.position,
            Mathf.Max(moveSpeed, MinimumPatrolSpeed) * movementSpeedMultiplier);

        // Reverse when close enough to the target.
        if (Vector3.Distance(transform.position, currentTarget.position) <= arriveDistance)
        {
            currentTarget = currentTarget == pointA ? pointB : pointA;
        }
    }

    private void SetPositionOnCircle()
    {
        float angleRadians = circularAngle * Mathf.Deg2Rad;
        Vector3 position = new Vector3(
            circularCenter.x + Mathf.Cos(angleRadians) * circularRadius,
            circularCenter.y + Mathf.Sin(angleRadians) * circularRadius,
            transform.position.z);
        if (body != null)
        {
            body.MovePosition(position);
        }
        else
        {
            transform.position = position;
        }
    }

    private void MoveTowards(Vector3 target, float speed)
    {
        Vector3 position = Vector3.MoveTowards(
            transform.position, target, speed * Time.fixedDeltaTime);
        if (body != null)
        {
            body.MovePosition(position);
        }
        else
        {
            transform.position = position;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Solid contact removes a life and respawns the player.
        if (collision.collider.CompareTag("Player"))
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.DamagePlayer(collision.collider.GetComponentInParent<PlayerMovement>());
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Also support trigger-based enemies if preferred.
        if (other.CompareTag("Player"))
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.DamagePlayer(other.GetComponentInParent<PlayerMovement>());
            }
        }
    }
}
