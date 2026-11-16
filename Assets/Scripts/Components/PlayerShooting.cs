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

    [Header("Muzzle Flash 枪口闪光（教授第4条）")]
    [Tooltip("勾选 = 每次开火在枪口打一次黄色闪光")]
    public bool enableMuzzleFlash = true;
    [Tooltip("闪光颜色")]
    public Color muzzleFlashColor = new Color(1f, 0.85f, 0.3f);
    [Tooltip("闪光点光源强度")]
    public float muzzleFlashLightIntensity = 6f;
    [Tooltip("闪光持续时间（秒）")]
    public float muzzleFlashDuration = 0.09f;

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

        // 位置/朝向/速度一次性交给对象池：它在激活子弹之前就摆好，避免池化子弹带着上一次的
        // 位置和残留速度被唤醒、对旧位置附近的敌人产生一次"莫名命中"
        Vector3 muzzlePosition = firePoint.position;
        Quaternion muzzleRotation = firePoint.rotation;
        Vector3 velocity = firePoint.forward * bulletSpeed;

        GameObject bullet = BulletPool.Instance.GetBullet(muzzlePosition, muzzleRotation, velocity);
        if (bullet == null) return;

        // 枪口离玩家身体很近，让子弹忽略玩家自身的碰撞体，避免刚出膛就打到自己
        Collider myCollider = GetComponent<Collider>();
        if (myCollider != null)
        {
            foreach (Collider bulletCollider in bullet.GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(bulletCollider, myCollider, true);
        }

        // 教授第4条：开火黄色闪光
        if (enableMuzzleFlash)
            MuzzleFlash.Spawn(firePoint, muzzleFlashColor, muzzleFlashLightIntensity, muzzleFlashDuration);
    }
}
