using UnityEngine;
using UnityEngine.Rendering.Universal;

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
    [Tooltip("速度倍率，由天气系统写入（雪地会降低），1 = 正常")]
    public float speedMultiplier = 1f;
    [Tooltip("地面抓地力：1 = 立即响应（原手感），越小越滑（雨天湿滑地面），由天气系统写入")]
    [Range(0.05f, 1f)] public float grip = 1f;
    public float jumpForce = 5f;
    public float groundCheckDistance = 0.2f;
    public LayerMask groundMask;

    [Header("Auto Rotation 移动自动朝向（教授第5条）")]
    [Tooltip("勾选 = 移动时人物自动转向前进方向（纯视觉，不影响移动方向）")]
    public bool autoRotate = true;
    [Tooltip("转向平滑速度；越大转得越快，8~15 比较自然")]
    public float rotateSpeed = 12f;
    [Tooltip("模型朝向修正角（度）：如果人物跑起来是侧着/背对着跑，改这里，±90 或 180")]
    public float modelYawOffset = 0f;

    [Header("Fall")]
    [Tooltip("掉落到该 Y 值以下判定为掉落死亡")]
    public float fallThreshold = -10f;

    [Header("Visual")]
    [Tooltip("玩家本体颜色（基础区分色，绿色）；未绑定材质时用此颜色染副本")]
    public Color bodyColor = new Color(0.2f, 0.8f, 0.2f);
    [Tooltip("玩家固化材质（绿色）；绑定后优先使用，留空则按 bodyColor 运行时染色")]
    public Material bodyMaterial;

    [Header("Glow 发光")]
    [Tooltip("开启自发光；配合场景 Bloom 产生辉光（教授要求的 glowing effect）")]
    public bool enableGlow = true;
    [Tooltip("自发光强度；大于 1 才会被 Bloom 拾取")]
    public float glowIntensity = 2.2f;
    [Tooltip("额外挂一盏同色点光源，让辉光洒到地面")]
    public bool addGlowLight = true;

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

            // 5) 打开相机后处理：Bloom 需要它，否则自发光只有本体亮度、没有辉光
            EnableCameraPostProcessing();

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

        // 8) 给显示模型套用玩家区分色（实例化材质副本，不污染共享 FBX 材质）
        ApplyBodyColor(modelGo);
    }

    /// <summary>给模型套用区分色与自发光：每个渲染器生成材质实例，绑定固化材质时以它为底，否则用 FBX 内嵌材质</summary>
    void ApplyBodyColor(GameObject modelGo)
    {
        if (modelGo == null) return;

        foreach (Renderer r in modelGo.GetComponentsInChildren<Renderer>())
        {
            Material src = bodyMaterial != null ? bodyMaterial : r.sharedMaterial;
            if (src == null) continue;

            // 实例化：运行时的颜色/发光改动不会写回 .mat 资源
            Material inst = new Material(src);
            inst.color = bodyColor;

            if (enableGlow)
            {
                inst.EnableKeyword("_EMISSION");
                inst.SetColor("_EmissionColor", bodyColor * glowIntensity);
            }
            else
            {
                inst.DisableKeyword("_EMISSION");
                inst.SetColor("_EmissionColor", Color.black);
            }

            r.material = inst;
        }

        if (addGlowLight)
            EnsureGlowLight();
    }

    /// <summary>
    /// 在物理根下挂一盏同色点光源，让辉光也能照亮周围地面。
    /// 注意挂在根上而不是 Model 子物体：Model 带 100 倍缩放，挂上去光源位置会被放大。
    /// </summary>
    void EnsureGlowLight()
    {
        if (transform.Find("GlowLight") != null) return;

        GameObject go = new GameObject("GlowLight");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0.9f, 0f);

        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = bodyColor;
        l.range = 4f;
        l.intensity = 1.2f;
        l.shadows = LightShadows.None;
    }

    /// <summary>
    /// 确保主相机开启后处理：场景里 Level1 的相机该开关是关的，Level2 甚至没有
    /// UniversalAdditionalCameraData 组件。这里在运行时统一补齐，保证 Bloom 生效。
    /// </summary>
    void EnableCameraPostProcessing()
    {
        Camera cam = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : Camera.main;
        if (cam == null) return;

        UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
        if (data != null)
            data.renderPostProcessing = true;
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
        // 移动自动朝向：让根节点平滑转到前进方向。
        // 刚体已 FreezeRotation，物理引擎不会与这里的直接赋角打架；胶囊是回转体，转向不影响碰撞。
        if (autoRotate && moveDir.sqrMagnitude > 0.0001f)
        {
            Quaternion target = Quaternion.LookRotation(Quaternion.Euler(0f, modelYawOffset, 0f) * moveDir, Vector3.up);
            // 指数平滑：与帧率无关，rotateSpeed 越大越跟手
            float t = 1f - Mathf.Exp(-rotateSpeed * Time.fixedDeltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, t);
        }

        Move(moveDir * speed * Mathf.Max(0.05f, speedMultiplier));
    }

    /// <summary>实现基类抽象方法 —— 相对相机方向移动，保留竖直速度以支持重力/跳跃</summary>
    public override void Move(Vector3 direction)
    {
        // 水平方向按 grip 做"抓地力"混合：grip = 1 时等于直接赋值（原手感），
        // grip 越小越滑，松开按键后还会顺着惯性滑一段（雨天湿滑地面的规则）
        Vector3 targetHorizontal = new Vector3(direction.x, 0f, direction.z);

        Vector3 velocity = rb.linearVelocity;
        Vector3 currentHorizontal = new Vector3(velocity.x, 0f, velocity.z);

        float g = Mathf.Clamp01(grip);
        Vector3 blendedHorizontal = Vector3.Lerp(currentHorizontal, targetHorizontal, g);

        velocity.x = blendedHorizontal.x;
        velocity.z = blendedHorizontal.z;
        // Y 轴保持原样，不干扰重力/跳跃
        rb.linearVelocity = velocity;
    }

    /// <summary>实现基类抽象方法 —— 玩家死亡：接入 GameManager 的生命扣减与关卡重启</summary>
    protected override void Die()
    {
        Debug.Log("玩家死亡");
        if (GameManager.Instance != null)
            GameManager.Instance.OnPlayerDeath();
    }
}
