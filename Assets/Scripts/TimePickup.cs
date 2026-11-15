using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class TimePickup : MonoBehaviour
{
    [SerializeField, Min(1f)] private float secondsToAdd = 10f;
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
        manager.AddTime(secondsToAdd);
        Destroy(gameObject);
    }
}
