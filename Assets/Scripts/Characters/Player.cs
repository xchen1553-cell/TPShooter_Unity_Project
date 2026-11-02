using UnityEngine;

/// <summary>
/// 玩家 —— 继承 Character 基类
/// 基类提供：浮点血量(maxHealth)、受伤事件(OnDamaged)、死亡事件(OnDeath)、IDamageable 接口
/// 本类只负责：输入读取、相机相对移动、跳跃、掉落死亡、死亡接入 GameManager
/// 字段名与 Player.prefab 序列化保持一致（小写），避免覆盖预制体配置。
/// </summary>
public class Player : Character
{
    [Header("Movement")]
    public float speed = 5f;
    public float jumpForce = 5f;
    public float groundCheckDistance = 0.2f;
    public LayerMask groundMask;

    [Header("Fall")]
    [Tooltip("掉落到该 Y 值以下判定为掉落死亡")]
    public float fallThreshold = -10f;

    [Header("References")]
    [Tooltip("主相机，用于相对相机方向移动；留空自动查找")]
    public Transform cameraTransform;

    private Rigidbody rb;
    private bool isGrounded;
    private float hInput;
    private float vInput;
    private int traceFrame;

    void Awake()
    {
        RtLog.Reset();
        RtLog.Write("Player", "Awake 开始");
        try
        {
            // 1) 先拿到刚体并锁转、加阻尼。必须在调整 Transform 姿态之前完成：
            //    否则第一物理帧物理引擎会用实例化时缓存的躺倒姿态覆盖回 Transform。
            rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.freezeRotation = true;
                // 冻结全部三轴旋转：玩家朝向由相机相对移动决定，不允许物理接触力矩翻转或绕 Y 自转
                rb.constraints = RigidbodyConstraints.FreezeRotation;
                rb.linearDamping = 0.1f;
                rb.angularDamping = 1f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            RtLog.Write("Player", "rb=" + (rb != null) + " cameraTransform=" + (cameraTransform != null));

            // 2) 模型显示与物理根解耦：根归一到单位尺度、单位朝向，胶囊校准为竖直人形
            RtLog.Write("Player", "解耦前 scale=" + transform.localScale + " rot=" + transform.localRotation.eulerAngles);
            SeparateVisualFromPhysics();
            RtLog.Write("Player", "解耦完成 scale=" + transform.localScale);

            // 3) 关键：把刚体内部缓存的姿态/位置同步到转正后的 Transform，
            //    避免物理模拟第一帧用旧的 -90° 躺倒姿态把根重新扳回、导致翻滚弹射。
            if (rb != null)
            {
                rb.MoveRotation(Quaternion.identity);
                rb.MovePosition(transform.position);
            }

            // 4) 未拖相机时自动查找主相机，保证开箱可用
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;
            RtLog.Write("Player", "Awake 完成 worldRot=" + transform.rotation.eulerAngles + " cameraTransform=" + (cameraTransform != null));
        }
        catch (System.Exception e)
        {
            RtLog.Write("Player", "Awake 异常: " + e);
        }
    }

    /// <summary>
    /// 层级解耦：FBX 模型根同时带 100 倍缩放和 -90° 旋转，直接把刚体/胶囊挂在这一层，
    /// 碰撞体世界尺寸会被放大 100 倍、轴线躺倒，出生即与地面巨型重叠被物理求解器弹飞翻滚。
    /// 这里把缩放/旋转下沉到新建的 Model 子物体（只负责显示），本物理根回到单位尺度单位朝向，
    /// 胶囊重建为标准人形尺寸；玩家脚本、刚体、相机/UI/敌人引用仍留在本根上，不受影响。
    /// </summary>
    void SeparateVisualFromPhysics()
    {
        // 1) 保存模型原本的本地缩放与旋转（来自 FBX），稍后交给 Model 子物体，保证外观不变
        Vector3 savedScale = transform.localScale;
        Quaternion savedRot = transform.localRotation;

        // 2) 取出根上的网格资源引用
        MeshFilter mf = GetComponent<MeshFilter>();
        MeshRenderer mr = GetComponent<MeshRenderer>();
        Mesh sharedMesh = mf != null ? mf.sharedMesh : null;
        Material[] sharedMats = mr != null ? mr.sharedMaterials : null;

        // 3) 物理根归一到单位尺度、单位朝向
        transform.localScale = Vector3.one;
        transform.localRotation = Quaternion.identity;
        transform.rotation = Quaternion.identity;
        RtLog.Write("Player", "归一后 localRot=" + transform.localRotation.eulerAngles + " worldRot=" + transform.rotation.eulerAngles);

        // 4) 新建 Model 子物体，承接原缩放与旋转；世界外观与解耦前一致
        GameObject modelGo = new GameObject("Model");
        modelGo.transform.SetParent(transform, false);
        modelGo.transform.localPosition = Vector3.zero;
        modelGo.transform.localRotation = savedRot;
        modelGo.transform.localScale = savedScale;

        // 5) 把网格组件从根迁移到 Model 子物体
        if (mf != null) Destroy(mf);
        if (mr != null) Destroy(mr);
        if (sharedMesh != null)
        {
            MeshFilter modelMf = modelGo.AddComponent<MeshFilter>();
            modelMf.sharedMesh = sharedMesh;
            MeshRenderer modelMr = modelGo.AddComponent<MeshRenderer>();
            modelMr.sharedMaterials = sharedMats;
        }

        // 6) 物理根已是单位尺度，把胶囊校准为标准竖直人形胶囊，中心抬到身体中部，底部贴地
        CapsuleCollider cap = GetComponent<CapsuleCollider>();
        if (cap != null)
        {
            cap.direction = 1; // 1 = Y-Axis（竖直站立），用整数避免不同版本枚举类型名差异
            cap.radius = 0.35f;
            cap.height = 1.6f;
            cap.center = new Vector3(0f, 0.8f, 0f);
        }

        // 7) 枪口也放到物理根标准尺度下，略抬高、位于身体前方
        PlayerShooting shooting = GetComponent<PlayerShooting>();
        if (shooting != null && shooting.firePoint != null)
        {
            shooting.firePoint.localPosition = new Vector3(0f, 1f, 0.5f);
            shooting.firePoint.localRotation = Quaternion.identity;
        }
    }

    void Update()
    {
        // 死亡后冻结输入
        if (isDead) return;

        // 掉落检测：超出阈值视为坠落死亡（走统一死亡入口，触发 OnDeath 事件并标记死亡）
        if (transform.position.y < fallThreshold)
        {
            Kill();
            return;
        }

        // 读取移动输入：在新旧输入并存(activeInputHandler=2)环境下，输入轴(GetAxis)可能残留
        // 摇杆/平滑缓存导致静止时误输出非零、玩家自己乱跑。这里完全只读 W/A/S/D 物理按键，最稳定。
        float h = 0f;
        if (Input.GetKey(KeyCode.D)) h += 1f;
        if (Input.GetKey(KeyCode.A)) h -= 1f;

        float v = 0f;
        if (Input.GetKey(KeyCode.W)) v += 1f;
        if (Input.GetKey(KeyCode.S)) v -= 1f;

        hInput = Mathf.Clamp(h, -1f, 1f);
        vInput = Mathf.Clamp(v, -1f, 1f);

        // 地面检测：从脚底向下打射线
        Vector3 footPos = transform.position + Vector3.down * 0.9f;
        isGrounded = Physics.Raycast(footPos, Vector3.down, groundCheckDistance + 0.1f, groundMask);

        // 跳跃
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }

    void FixedUpdate()
    {
        // 相机引用兜底：若 Awake 时主相机尚未就绪，这里每帧补查，避免输入通道永久断开导致 WASD 失效
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
        if (cameraTransform == null || isDead)
        {
            if (traceFrame < 30) { RtLog.Write("Player", "FixedUpdate 早退 cameraNull=" + (cameraTransform == null) + " isDead=" + isDead); traceFrame++; }
            return;
        }

        // 取相机水平方向（消掉 Y 轴俯仰，避免人物被相机仰角带飞）
        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        // 相对相机的移动方向
        Vector3 moveDir = (camForward * vInput + camRight * hInput).normalized;
        if (traceFrame < 30)
        {
            RtLog.Write("Player", "FixedUpdate #" + traceFrame + " h=" + hInput.ToString("0.00") + " v=" + vInput.ToString("0.00")
                + " pos=" + transform.position + " euler=" + transform.rotation.eulerAngles + " grounded=" + isGrounded
                + " vel=" + rb.linearVelocity);
            traceFrame++;
        }
        Move(moveDir * speed);
    }

    /// <summary>实现基类抽象方法 —— 相对相机方向移动，保留竖直速度以支持重力/跳跃</summary>
    public override void Move(Vector3 direction)
    {
        Vector3 moveVelocity = direction;
        moveVelocity.y = rb.linearVelocity.y; // 只改水平，不干扰 Y 轴
        rb.linearVelocity = moveVelocity;
    }

    /// <summary>实现基类抽象方法 —— 玩家死亡：接入 GameManager 的生命扣减与关卡重启</summary>
    protected override void Die()
    {
        Debug.Log("玩家死亡");
        if (GameManager.Instance != null)
            GameManager.Instance.OnPlayerDeath();
    }
}
