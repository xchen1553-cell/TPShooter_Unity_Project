using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 子弹对象池 —— 避免反复 Instantiate/Destroy 导致 GC 卡顿
/// </summary>
public class BulletPool : MonoBehaviour
{
    /// <summary>全局唯一实例（讲义标准单例）</summary>
    public static BulletPool Instance { get; private set; }

    [Header("Pool Settings")]
    public GameObject bulletPrefab;
    public int poolSize = 20;

    private Queue<GameObject> pool = new Queue<GameObject>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        RtLog.Write("BulletPool", "Awake bulletPrefab=" + (bulletPrefab != null));
        // 预创建子弹（Awake 中完成，保证其他脚本 Start 即可用）
        try
        {
            for (int i = 0; i < poolSize; i++)
            {
                GameObject bullet = Instantiate(bulletPrefab);
                bullet.SetActive(false);
                pool.Enqueue(bullet);
            }
            RtLog.Write("BulletPool", "子弹池初始化完成，数量：" + poolSize);
        }
        catch (System.Exception e)
        {
            RtLog.Write("BulletPool", "初始化异常: " + e);
        }
    }

    // 场景卸载时若本对象就是单例，清空静态引用；否则下一关新建的池会误判"已有实例"而被销毁，
    // 导致静态 Instance 指向已销毁对象，射击时触发 MissingReferenceException。
    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// 从池中取一颗子弹
    /// </summary>
    public GameObject GetBullet()
    {
        GameObject bullet;

        if (pool.Count == 0)
        {
            // 池空了，动态扩容
            bullet = Instantiate(bulletPrefab);
            Debug.LogWarning("子弹池已空，动态创建新子弹");
        }
        else
        {
            bullet = pool.Dequeue();
        }

        bullet.SetActive(true);
        return bullet;
    }

    /// <summary>
    /// 将子弹归还池中
    /// </summary>
    public void ReturnBullet(GameObject bullet)
    {
        if (bullet == null) return;
        if (pool.Contains(bullet)) return; // 防止同一颗子弹被重复归还导致队列出现副本
        bullet.SetActive(false);
        pool.Enqueue(bullet);
    }
}
