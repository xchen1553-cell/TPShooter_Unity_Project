using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 子弹对象池 —— 避免反复 Instantiate/Destroy 导致 GC 卡顿
///
/// 【重要】本组件挂在 GlobalManagers 上，而同一物体上的 GameManager / LevelManager 调用了
/// DontDestroyOnLoad —— 整个根节点跨场景常驻，所以这个池也常驻。但池里的子弹是**当前场景**的
/// 普通对象，切场景时会被销毁。若不处理，队列里就会留下一堆已销毁的子弹引用，
/// 下次开火时访问它们会抛 MissingReferenceException。
/// 因此这里做了两件事：取子弹时跳过已销毁的引用；场景加载后整池重建。
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
        FillPool();

        // 常驻对象：每次切场景都要重建池（旧子弹已随上一个场景销毁）
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // 场景卸载时若本对象就是单例，清空静态引用
    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this)
            Instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 丢弃旧场景里已经销毁的引用，重新造满一池子弹
        pool.Clear();
        FillPool();
        RtLog.Write("BulletPool", "场景 " + scene.name + " 加载完成，子弹池重建：" + pool.Count);
    }

    /// <summary>补满对象池</summary>
    void FillPool()
    {
        if (bulletPrefab == null)
        {
            Debug.LogWarning("[BulletPool] bulletPrefab 未赋值，无法生成子弹池");
            return;
        }

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

    /// <summary>从队列取一颗仍然存活的子弹；已销毁的直接丢弃</summary>
    GameObject TakeFromPool()
    {
        while (pool.Count > 0)
        {
            GameObject bullet = pool.Dequeue();
            if (bullet != null) return bullet;   // 切场景后被销毁的引用，跳过
        }
        return null;
    }

    /// <summary>
    /// 从池中取一颗子弹（兼容旧调用：取出来自己摆位置）
    /// </summary>
    public GameObject GetBullet()
    {
        GameObject bullet = TakeFromPool();

        if (bullet == null)
        {
            // 池空了，动态扩容
            bullet = Instantiate(bulletPrefab);
            Debug.LogWarning("子弹池已空，动态创建新子弹");
        }

        bullet.SetActive(true);
        return bullet;
    }

    /// <summary>
    /// 从池中取一颗子弹，并在**激活之前**摆好位置与朝向、清掉上一发的速度。
    /// 之前是在激活后设置位置/速度，池里的子弹会带着"上一次命中点"的位置和残留速度被唤醒，
    /// 容易在那一帧对旧位置附近的敌人产生一次莫名其妙的命中。
    /// </summary>
    public GameObject GetBullet(Vector3 position, Quaternion rotation, Vector3 velocity)
    {
        GameObject bullet = TakeFromPool();

        if (bullet == null)
        {
            bullet = Instantiate(bulletPrefab);
            Debug.LogWarning("子弹池已空，动态创建新子弹");
        }

        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        bullet.transform.SetPositionAndRotation(position, rotation);
        bullet.SetActive(true);

        if (rb != null)
            rb.linearVelocity = velocity;

        return bullet;
    }

    /// <summary>
    /// 将子弹归还池中
    /// </summary>
    public void ReturnBullet(GameObject bullet)
    {
        if (bullet == null) return;          // 已被销毁（切场景）就直接丢弃
        if (pool.Contains(bullet)) return;   // 防止同一颗子弹被重复归还导致队列出现副本
        bullet.SetActive(false);
        pool.Enqueue(bullet);
    }
}
