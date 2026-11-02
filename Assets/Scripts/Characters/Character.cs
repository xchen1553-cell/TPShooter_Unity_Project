using UnityEngine;

/// <summary>
/// 角色抽象基类 —— 玩家和敌人共享血量与受伤逻辑
/// </summary>
public abstract class Character : MonoBehaviour, IDamageable
{
    [Header("Health")]
    public float maxHealth = 100f;

    protected float currentHealth;
    protected bool isDead = false;

    // 事件 —— 让 UI/音效/特效订阅，玩家类本身不依赖它们
    public event System.Action<float> OnDamaged;
    public event System.Action OnDeath;

    protected virtual void Start()
    {
        currentHealth = maxHealth;
    }

    /// <summary>
    /// 受伤逻辑 —— 子类可重写（如加护盾、无敌帧）
    /// </summary>
    public virtual void TakeDamage(float amount)
    {
        if (isDead) return;

        // 血量钳制在 0 以上，避免出现负值导致 UI 血条反向填充
        currentHealth = Mathf.Max(0f, currentHealth - amount);
        OnDamaged?.Invoke(amount);

        if (currentHealth <= 0)
            Kill();
    }

    /// <summary>
    /// 统一死亡入口：标记死亡 → 通知 OnDeath 订阅者（UI/特效）→ 执行子类 Die()
    /// 血量归零、坠落等任何死亡路径都应走这里，保证事件与状态一致。
    /// </summary>
    protected void Kill()
    {
        if (isDead) return;
        isDead = true;
        OnDeath?.Invoke();
        Die();
    }

    /// <summary>
    /// 死亡 —— 子类必须实现
    /// </summary>
    protected abstract void Die();

    /// <summary>
    /// 移动 —— 子类必须实现
    /// </summary>
    public abstract void Move(Vector3 direction);

    // 接口属性实现
    public float CurrentHealth => currentHealth;
}
