using UnityEngine;

/// <summary>
/// 玩家射击 —— 从 BulletPool 对象池取子弹，避免频繁 Instantiate/Destroy 造成 GC 卡顿
/// 子弹 prefab 统一在 BulletPool 上配置，这里不再单独引用
/// 注意：序列化字段名必须与 Player.prefab 中保存的小写名称完全一致，否则引用会丢失。
/// </summary>
public class PlayerShooting : MonoBehaviour
{
    [Header("Shooting")]
    [Tooltip("射击间隔（秒）")]
    public float fireRate = 0.2f;
    [Tooltip("子弹初速度")]
    public float bulletSpeed = 15f;
    [Tooltip("子弹发射口（一般挂在枪口）")]
    public Transform firePoint;

    private float nextFireTime;

    void Update()
    {
        if (IsShootingHeld() && Time.time >= nextFireTime)
        {
            Shoot();
            nextFireTime = Time.time + fireRate;
        }
    }

    /// <summary>是否按住开火键（左键）</summary>
    bool IsShootingHeld()
    {
        return Input.GetMouseButton(0);
    }

    void Shoot()
    {
        // 依赖 BulletPool 单例提供子弹
        if (BulletPool.Instance == null)
        {
            Debug.LogWarning("BulletPool 单例不存在，无法射击！请确保场景中有 BulletPool 对象");
            return;
        }

        if (firePoint == null)
        {
            Debug.LogWarning("firePoint 没有赋值！");
            return;
        }

        // 从池中取一颗子弹，放到枪口并设置朝向与速度
        GameObject bullet = BulletPool.Instance.GetBullet();
        bullet.transform.SetPositionAndRotation(firePoint.position, firePoint.rotation);

        Rigidbody bulletRb = bullet.GetComponent<Rigidbody>();
        if (bulletRb != null)
        {
            bulletRb.linearVelocity = firePoint.forward * bulletSpeed;
        }
    }
}
