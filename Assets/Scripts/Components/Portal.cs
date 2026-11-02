using UnityEngine;

/// <summary>
/// 传送门 —— 支持跨场景切换和同场景瞬移两种模式
/// 新增延迟，解决加载瞬间触发OnTriggerEnter直接跳关
/// </summary>
public class Portal : MonoBehaviour
{
    [Header("Portal Type")]
    [Tooltip("true = 切换到下一关场景；false = 同场景瞬移到 Target 位置")]
    public bool isLevelPortal = true;

    [Header("Teleport Target")]
    [Tooltip("同场景瞬移时的目标位置")]
    public Transform teleportTarget;

    [Header("Delay")]
    [Tooltip("场景加载后多久才能触发传送，防止开局瞬间误触发")]
    public float enableDelay = 0.5f;
    private bool canTeleport;

    void Start()
    {
        canTeleport = false;
        Invoke(nameof(EnablePortal), enableDelay);
    }

    void EnablePortal()
    {
        canTeleport = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!canTeleport) return;

        // 通过组件识别玩家（不依赖 Tag，FBX 模型标签易失效）
        if (other.GetComponent<Player>() == null)
            return;

        if (isLevelPortal)
        {
            Debug.Log("进入传送门 → 加载下一关");
            if (LevelManager.Instance != null)
                LevelManager.Instance.LoadNextLevel();
        }
        else if (teleportTarget != null)
        {
            Debug.Log($"同场景瞬移到：{teleportTarget.position}");
            other.transform.position = teleportTarget.position;
        }
    }
}