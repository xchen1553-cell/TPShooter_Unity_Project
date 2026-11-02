using UnityEngine;

/// <summary>
/// 可受伤接口 —— 任何能受伤的物体（角色、箱子、桶）都实现它
/// </summary>
public interface IDamageable
{
    void TakeDamage(float amount);
    float CurrentHealth { get; }
}
