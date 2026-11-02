using UnityEngine;

/// <summary>
/// 第三人称跟随相机 —— 水平环绕 + 垂直俯仰，鼠标控制视角
/// Esc 释放鼠标便于调试，点击左键重新锁定。
/// </summary>
public class CameraFollow : MonoBehaviour
{
    public Transform target; // 要跟随的目标（拖入 player）
    public Vector3 offset = new Vector3(0, 3, -5); // 相机相对主角的偏移
    public float smoothSpeed = 0.125f; // 跟随平滑度

    [Header("Mouse Look")]
    [Tooltip("鼠标灵敏度，控制水平环绕与垂直俯仰")]
    public float mouseSensitivity = 3f;
    [Tooltip("初始水平朝向角（度），0 表示相机从主角正后方")]
    public float startYaw = 0f;
    [Tooltip("俯仰角下限（度，相机压低）")]
    public float minPitch = -15f;
    [Tooltip("俯仰角上限（度，相机抬高）")]
    public float maxPitch = 55f;

    private float yaw;
    private float pitch = 15f;

    void Start()
    {
        yaw = startYaw;
        RtLog.Write("CameraFollow", "Start target=" + (target != null) + " targetName=" + (target != null ? target.name : "null"));
        LockCursor();
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Esc 释放鼠标；左键重新锁定
        if (Input.GetKeyDown(KeyCode.Escape))
            UnlockCursor();
        else if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
            LockCursor();

        // 鼠标水平 → 环绕；鼠标垂直 → 俯仰（限制在上下限之间，避免翻转）
        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // 目标位置 = 主角位置 + 绕主角旋转后的偏移（同时含水平与俯仰）
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desiredPosition = target.position + rot * offset;

        // 平滑插值移动
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        // 始终看向主角
        transform.LookAt(target);
    }

    void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
