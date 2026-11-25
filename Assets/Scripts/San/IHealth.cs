// 受击和恢复共用的生命接口，amount 表示要改变的正数点数。
public interface IHealth
{
    void TakeDamage(int amount);
    void Heal(int amount);
}
