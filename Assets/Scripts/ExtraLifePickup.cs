using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ExtraLifePickup : MonoBehaviour
{
    private bool collected;

    public static ExtraLifePickup CreateAdditionalPickup(ExtraLifePickup template, string pickupName)
    {
        if (template == null)
        {
            Debug.LogError("[DungeonEscape] Cannot create another health pickup without a template.");
            return null;
        }

        GameObject clone = UnityEngine.Object.Instantiate(
            template.gameObject, template.transform.position, template.transform.rotation);
        clone.name = pickupName;
        ExtraLifePickup pickup = clone.GetComponent<ExtraLifePickup>();
        pickup.collected = false;
        return pickup;
    }

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected || !other.CompareTag("Player")) return;
        GameManager manager = GameManager.Instance;
        if (manager == null || !manager.IsPlaying || !manager.TryRestoreLife()) return;

        collected = true;
        Destroy(gameObject);
    }
}
