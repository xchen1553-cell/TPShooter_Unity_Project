using UnityEngine;

/// <summary>
/// 敌人 —— 巡逻 + 追击型 AI
/// 继承 Character 基类（自带浮点血量、OnDamaged/OnDeath 事件、IDamageable 接口）
/// 行为流程：出生点附近巡逻 → 玩家进入 detectionRange 开始追击 →
///          贴近 attackRange 周期性造成接触伤害 → 玩家逃出 loseRange 后返回出生点
/// 说明：移动直接改 transform，不依赖 Rigidbody 物理，任何挂载此脚本的物体都能跑。
///       敌人没有 Rigidbody 时，碰撞判定用距离实现（TryAttackPlayer），不依赖物理接触。
/// 字段名与旧版 Enemy.prefab 序列化保持一致，避免覆盖预制体配置。
/// </summary>
public class Enemy : Character
{
    [Header("Patrol 巡逻")]
    [Tooltip("巡逻半径：以出生点为中心的游荡范围")]
    public float patrolRadius = 8f;
    [Tooltip("巡逻移动速度")]
    public float patrolSpeed = 2f;
    [Tooltip("到达巡逻点后停留时长（秒）")]
    public float waitTime = 2f;

    [Header("Chase 追击")]
    [Tooltip("发现玩家并开始追击的半径")]
    public float detectionRange = 15f;
    [Tooltip("追击移动速度")]
    public float chaseSpeed = 5f;
    [Tooltip("玩家逃出多远后放弃追击")]
    public float loseRange = 20f;

    [Header("Attack 攻击")]
    [Tooltip("接触伤害生效半径（贴脸判定，不依赖物理碰撞）")]
    public float attackRange = 1.5f;
    [Tooltip("每次接触造成的伤害")]
    public float attackDamage = 10f;
    [Tooltip("攻击冷却（秒），防止每帧重复扣血")]
    public float attackCooldown = 1f;

    [Header("Reward 奖励")]
    [Tooltip("被击杀后奖励分数")]
    public int scoreValue = 10;

    [Header("Reference 引用")]
    [Tooltip("玩家引用：可拖入；留空会自动查找场景中的 Player")]
    public Player player;

    // 出生点：巡逻与返回的参照
    private Vector3 spawnPoint;

    // AI 状态机
    private enum EnemyState { Patrol, Chase, Return }
    private EnemyState state = EnemyState.Patrol;

    // 巡逻目标点与等待计时
    private Vector3 patrolTarget;
    private float waitTimer;

    // 攻击冷却计时
    private float nextAttackTime;

    void Awake()
    {
        // 敌人与玩家共用同一份 FBX 模型：根节点同样带 100 倍缩放与 -90° 躺倒旋转。
        // 若不解耦，挂在根上的胶囊会被放大 100 倍并横躺；无刚体敌人靠 transform 移动时，
        // 这个巨型横躺碰撞体会从侧面把玩家刚体顶飞。这里与玩家做相同的显示/物理隔离。
        SeparateVisualFromPhysics();
    }

    /// <summary>层级解耦：缩放/旋转下沉到 Model 子节点，本根归一为单位尺度单位朝向，胶囊校准为竖直标准尺寸并设为触发器。</summary>
    void SeparateVisualFromPhysics()
    {
        Vector3 savedScale = transform.localScale;
        Quaternion savedRot = transform.localRotation;

        MeshFilter mf = GetComponent<MeshFilter>();
        MeshRenderer mr = GetComponent<MeshRenderer>();
        Mesh sharedMesh = mf != null ? mf.sharedMesh : null;
        Material[] sharedMats = mr != null ? mr.sharedMaterials : null;

        transform.localScale = Vector3.one;
        transform.localRotation = Quaternion.identity;
        transform.rotation = Quaternion.identity;

        GameObject modelGo = new GameObject("Model");
        modelGo.transform.SetParent(transform, false);
        modelGo.transform.localPosition = Vector3.zero;
        modelGo.transform.localRotation = savedRot;
        modelGo.transform.localScale = savedScale;

        if (mf != null) Destroy(mf);
        if (mr != null) Destroy(mr);
        if (sharedMesh != null)
        {
            MeshFilter modelMf = modelGo.AddComponent<MeshFilter>();
            modelMf.sharedMesh = sharedMesh;
            MeshRenderer modelMr = modelGo.AddComponent<MeshRenderer>();
            modelMr.sharedMaterials = sharedMats;
        }

        CapsuleCollider cap = GetComponent<CapsuleCollider>();
        if (cap != null)
        {
            // 触发器：敌人攻击已由 TryAttackPlayer 的距离判定完成，触发器不产生物理推挤，避免把玩家撞飞
            cap.isTrigger = true;
            cap.direction = 1; // 1 = Y-Axis 竖直站立
            cap.radius = 0.4f;
            cap.height = 1.6f;
            cap.center = new Vector3(0f, 0.8f, 0f);
        }
    }

    protected override void Start()
    {
        base.Start(); // 初始化 currentHealth = maxHealth
        try
        {
            spawnPoint = transform.position;
            PickNewPatrolTarget();

            // 玩家引用：优先用拖入的；未拖入时只在 Start 查找一次并缓存，避免每帧全场景扫描
            if (player == null)
                player = FindObjectOfType<Player>();
            RtLog.Write("Enemy", name + " Start 完成 player=" + (player != null) + " spawn=" + spawnPoint);
        }
        catch (System.Exception e)
        {
            RtLog.Write("Enemy", name + " Start 异常: " + e);
        }
    }

    private int traceFrame;

    void Update()
    {
        // 死亡后停止一切行为
        if (isDead) return;

        DecideState();
        ExecuteState();

        // 贴脸攻击（按距离判定，比物理碰撞更稳健）
        TryAttackPlayer();

        if (traceFrame < 2)
        {
            RtLog.Write("Enemy", name + " Update state=" + state + " pos=" + transform.position);
            traceFrame++;
        }
    }

    /// <summary>状态决策：Chase / Return / Patrol</summary>
    void DecideState()
    {
        bool hasPlayer = player != null;
        float distToPlayer = hasPlayer ? Vector3.Distance(transform.position, player.transform.position) : Mathf.Infinity;
        float distToSpawn = Vector3.Distance(transform.position, spawnPoint);

        if (hasPlayer && distToPlayer <= detectionRange)
        {
            state = EnemyState.Chase; // 玩家在侦测范围内 → 追击
        }
        else if (state == EnemyState.Chase && hasPlayer && distToPlayer <= loseRange)
        {
            state = EnemyState.Chase; // 追击中且还没完全丢失 → 保持追击
        }
        else if (distToSpawn > 1f)
        {
            state = EnemyState.Return; // 离开出生点 → 先回老家再巡逻
        }
        else
        {
            state = EnemyState.Patrol;
        }
    }

    /// <summary>按当前状态执行移动</summary>
    void ExecuteState()
    {
        switch (state)
        {
            case EnemyState.Chase:
                if (player != null)
                {
                    Vector3 dir = (player.transform.position - transform.position).normalized;
                    Move(dir * chaseSpeed);
                }
                break;

            case EnemyState.Return:
                Vector3 homeDir = (spawnPoint - transform.position).normalized;
                Move(homeDir * patrolSpeed);
                if (Vector3.Distance(transform.position, spawnPoint) < 0.5f)
                {
                    state = EnemyState.Patrol;
                    PickNewPatrolTarget();
                }
                break;

            default: // Patrol：朝巡逻点移动，到达后等待再换新点
                Move((patrolTarget - transform.position).normalized * patrolSpeed);
                if (Vector3.Distance(transform.position, patrolTarget) < 0.5f)
                {
                    waitTimer -= Time.deltaTime;
                    if (waitTimer <= 0f)
                        PickNewPatrolTarget();
                }
                break;
        }
    }

    /// <summary>实现基类抽象方法 —— 沿方向移动并平滑转向</summary>
    public override void Move(Vector3 direction)
    {
        direction.y = 0f; // 敌人不上下移动
        if (direction.magnitude < 0.01f) return;

        transform.position += direction * Time.deltaTime;

        // 面向移动方向
        Quaternion lookRot = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, 5f * Time.deltaTime);
    }

    /// <summary>在出生点 patrolRadius 内随机挑一个巡逻目标点</summary>
    void PickNewPatrolTarget()
    {
        Vector2 randomDir = Random.insideUnitCircle * patrolRadius;
        patrolTarget = spawnPoint + new Vector3(randomDir.x, 0f, randomDir.y);
        patrolTarget.y = transform.position.y; // 保持在同一平面
        waitTimer = waitTime;
    }

    /// <summary>玩家进入 attackRange 时按冷却周期造成接触伤害</summary>
    void TryAttackPlayer()
    {
        if (player == null || Time.time < nextAttackTime) return;

        float dist = Vector3.Distance(transform.position, player.transform.position);
        if (dist <= attackRange)
        {
            player.TakeDamage(attackDamage);
            nextAttackTime = Time.time + attackCooldown;
        }
    }

    /// <summary>实现基类抽象方法 —— 敌人死亡：加分并销毁</summary>
    protected override void Die()
    {
        Debug.Log($"{name} 敌人死亡，奖励 {scoreValue} 分");
        if (GameManager.Instance != null)
            GameManager.Instance.AddScore(scoreValue);
        Destroy(gameObject);
    }
}
