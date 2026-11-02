using UnityEngine;

/// <summary>
/// 子弹逻辑 —— 碰撞后检测 IDamageable 目标并归还对象池
/// 对象池复用要点：超时回收计时必须放在 OnEnable，每次从池中取出都重新启动；
/// Start 只在对象首次创建时执行一次，无法满足复用场景。
/// </summary>
public class Bullet : MonoBehaviour
{
    [Header("Bullet Settings")]
    public float lifeTime = 3f;
    public float damage = 25f;

    // 每次被激活（含从池中取出）都重新启动超时回收
    void OnEnable()
    {
        CancelInvoke();
        Invoke(nameof(ReturnToPool), lifeTime);
    }

    // 归还对象池时取消尚未触发的回调，避免回收后又被误触发
    void OnDisable()
    {
        CancelInvoke();
    }

    void OnCollisionEnter(Collision collision)
    {
        ApplyHit(collision.collider);
        ReturnToPool();
    }

    // 敌人碰撞体为触发器：子弹（刚体）穿过触发器时由本回调完成命中判定
    void OnTriggerEnter(Collider other)
    {
        ApplyHit(other);
        ReturnToPool();
    }

    /// <summary>对命中的可伤害目标造成伤害；跳过玩家自身。</summary>
    void ApplyHit(Collider hitCollider)
    {
        if (hitCollider == null) return;

        // 不伤害玩家自己（通过组件识别，比 Tag 更可靠）
        if (hitCollider.GetComponent<Player>() != null) return;

        // 尝试对碰撞目标造成伤害（敌人/箱子可能挂在子节点上，沿父级向上查找一层）
        IDamageable target = hitCollider.GetComponent<IDamageable>();
        if (target == null && hitCollider.transform.parent != null)
            target = hitCollider.transform.parent.GetComponent<IDamageable>();

        if (target != null)
        {
            target.TakeDamage(damage);
            Debug.Log($"子弹命中，造成 {damage} 点伤害");
        }
    }

    void ReturnToPool()
    {
        if (BulletPool.Instance != null)
            BulletPool.Instance.ReturnBullet(gameObject);
        else
            Destroy(gameObject);
    }
}
