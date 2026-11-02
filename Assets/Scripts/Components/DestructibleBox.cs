using UnityEngine;

/// <summary>
/// 可破坏箱子 —— 实现 IDamageable，可被武器/子弹破坏
/// 演示"不是角色也能受伤"的接口思想
/// </summary>
public class DestructibleBox : MonoBehaviour, IDamageable
{
    [Header("Box")]
    public float health = 50f;

    [Header("Score")]
    public int scoreValue = 5;

    private float currentHealth;
    private Material boxMaterial; // 缓存实例化材质，避免每次受伤重复访问 material 产生副本

    void Awake()
    {
        // 箱子 FBX 根同样带大缩放与躺倒旋转。若保留非触发器，挂在根上的 BoxCollider 会被
        // 同步放大、横躺成一块巨型静态方块，玩家出生靠近时会被物理求解器顶飞。
        // 这里只把碰撞体改为触发器：触发器不参与刚体推挤、不会顶飞玩家；子弹对触发器走
        // OnTriggerEnter 命中路径（见 Bullet.cs），照样能打中并破坏箱子。模型外观与位置保持不变。
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
            box.isTrigger = true;
    }

    void Start()
    {
        currentHealth = health;
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
            boxMaterial = renderer.material; // 只在这里实例化一次
    }

    public void TakeDamage(float amount)
    {
        // 血量钳制在 0 以上
        currentHealth = Mathf.Max(0f, currentHealth - amount);
        Debug.Log($"{name} 箱子受到 {amount} 伤害，剩余血量：{currentHealth}");

        // 受伤变红反馈（复用缓存材质，不再反复克隆）
        if (boxMaterial != null)
            boxMaterial.color = Color.red;

        if (currentHealth <= 0)
        {
            Debug.Log($"{name} 箱子被摧毁！");

            // 摧毁箱子加分
            if (GameManager.Instance != null)
                GameManager.Instance.AddScore(scoreValue);

            Destroy(gameObject);
        }
    }

    public float CurrentHealth => currentHealth;
}
