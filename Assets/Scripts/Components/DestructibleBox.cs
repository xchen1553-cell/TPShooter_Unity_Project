using UnityEngine;
using System.Collections;

/// <summary>
/// 场景箱子 —— 默认是**纯障碍物**（打不掉、挡路）；把 destructible 勾上才变回可破坏箱子。
///
/// 为什么默认不可破坏：箱子根节点是 FBX 模型，带大缩放（约 100 倍）且躺倒旋转，
/// 原始 BoxCollider（size 1）会变成一块巨型方块把玩家顶飞。旧代码只好把它改成触发器，
/// 代价是玩家能直接穿过去、而子弹又能把它打碎 —— 这不符合"障碍物"的定位。
/// 现在改成：按模型真实尺寸校正碰撞体后设为实体碰撞，子弹打上去只闪一下、不扣血不销毁。
/// </summary>
public class DestructibleBox : MonoBehaviour, IDamageable
{
    [Header("Box")]
    public float health = 50f;

    [Header("Score")]
    public int scoreValue = 5;

    [Header("Behavior 行为")]
    [Tooltip("是否可以被打掉。false（默认）= 纯障碍物：子弹打上去不掉血、不消失")]
    public bool destructible = false;

    [Tooltip("是否阻挡移动。true（默认）= 实体碰撞，玩家和敌人都撞不穿")]
    public bool solidObstacle = true;

    private float currentHealth;
    private Material boxMaterial;   // 缓存实例化材质，避免每次受伤重复访问 material 产生副本
    private Renderer boxRenderer;
    private bool flashing;

    void Awake()
    {
        boxRenderer = GetComponent<Renderer>();

        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
        {
            if (solidObstacle)
            {
                // 实体障碍物：先把碰撞体校正成模型真实尺寸，否则 100 倍缩放会变成巨型方块
                box.isTrigger = false;
                FitColliderToModel(box);
            }
            else
            {
                // 只当"可被子弹检测"的触发器，不参与物理推挤（旧行为）
                box.isTrigger = true;
            }
        }
    }

    /// <summary>按渲染器自身模型的局部包围盒校正 BoxCollider 的 size / center</summary>
    void FitColliderToModel(BoxCollider box)
    {
        if (boxRenderer == null) return;

        Bounds local = boxRenderer.localBounds;
        if (local.size.sqrMagnitude < 0.000001f) return;

        box.size = local.size;
        box.center = local.center;

        Debug.Log($"[DestructibleBox] {name} 碰撞体已校正：size={box.size} center={box.center} lossyScale={transform.lossyScale}");
    }

    void Start()
    {
        currentHealth = health;
        if (boxRenderer != null)
            boxMaterial = boxRenderer.material; // 只在这里实例化一次
    }

    public void TakeDamage(float amount)
    {
        if (!destructible)
        {
            // 纯障碍物：不扣血、不销毁，只闪一下作为"打上去了"的反馈
            if (!flashing) StartCoroutine(HitFlash());
            return;
        }

        // 血量钳制在 0 以上
        currentHealth = Mathf.Max(0f, currentHealth - amount);
        Debug.Log($"{name} 箱子受到 {amount} 伤害，剩余血量：{currentHealth}");

        // 受伤变红反馈（复用缓存材质，不再反复克隆）
        SetBaseColor(Color.red);

        if (currentHealth <= 0)
        {
            Debug.Log($"{name} 箱子被摧毁！");

            // 摧毁箱子加分
            if (GameManager.Instance != null)
                GameManager.Instance.AddScore(scoreValue);

            Destroy(gameObject);
        }
    }

    /// <summary>受击闪白，0.08 秒后恢复原色</summary>
    IEnumerator HitFlash()
    {
        if (boxMaterial == null) yield break;

        flashing = true;
        Color original = GetBaseColor();
        SetBaseColor(Color.white);
        yield return new WaitForSeconds(0.08f);
        SetBaseColor(original);
        flashing = false;
    }

    void SetBaseColor(Color c)
    {
        if (boxMaterial == null) return;
        if (boxMaterial.HasProperty("_BaseColor")) boxMaterial.SetColor("_BaseColor", c);
        if (boxMaterial.HasProperty("_Color")) boxMaterial.SetColor("_Color", c);
    }

    Color GetBaseColor()
    {
        if (boxMaterial == null) return Color.white;
        if (boxMaterial.HasProperty("_BaseColor")) return boxMaterial.GetColor("_BaseColor");
        if (boxMaterial.HasProperty("_Color")) return boxMaterial.GetColor("_Color");
        return Color.white;
    }

    public float CurrentHealth => currentHealth;
}
