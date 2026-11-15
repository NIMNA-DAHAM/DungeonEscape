using UnityEngine;

/// <summary>Optional treasure bonus. Collecting the chest does not unlock the exit.</summary>
[RequireComponent(typeof(Collider2D))]
public class TreasureChestCollect : MonoBehaviour
{
    [SerializeField, Min(1)] private int scoreValue = 20;
    private bool collected;

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected || !other.CompareTag("Player")) return;
        GameManager manager = GameManager.Instance;
        if (manager == null || !manager.IsPlaying) return;

        collected = true;
        manager.AddScore(scoreValue);
        Destroy(gameObject);
    }
}
