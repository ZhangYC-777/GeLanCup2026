using UnityEngine;

// 挂在角色根物体上，把命中这个角色的生命变化转交给队伍。
[DisallowMultipleComponent]
public class PlayerSanReceiver : MonoBehaviour, IHealth
{
    [SerializeField] private PartySan partySan;

    public void TakeDamage(int amount)
    {
        if (partySan != null)
        {
            partySan.TakeDamage(amount);
        }
    }

    public void Heal(int amount)
    {
        if (partySan != null)
        {
            partySan.Heal(amount);
        }
    }
}
