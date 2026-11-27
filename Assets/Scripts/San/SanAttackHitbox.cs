using UnityEngine;

// 挂在怪物攻击用的 2D Trigger 上。每次启用代表一次新攻击。
public class SanAttackHitbox : MonoBehaviour
{
    [SerializeField, Min(1)] private int damage = PartySan.StandardDamage;

    // 多个角色或多个碰撞体命中同一次攻击时，共享 SAN 只扣一次。
    private bool hasHit;

    private void OnEnable()
    {
        hasHit = false;
    }

    // 持续检测覆盖攻击启用时，角色已经站在判定区内的情况。
    private void OnTriggerStay2D(Collider2D other)
    {
        if (!isActiveAndEnabled || hasHit)
        {
            return;
        }

        PlayerSanReceiver target = other.GetComponentInParent<PlayerSanReceiver>();
        if (target == null || !target.isActiveAndEnabled)
        {
            return;
        }

        hasHit = true;
        target.TakeDamage(damage);
    }
}
