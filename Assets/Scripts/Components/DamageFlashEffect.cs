using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 受伤闪白特效 —— 监听角色 OnDamaged 事件
/// 演示事件驱动的解耦设计
/// </summary>
public class DamageFlashEffect : MonoBehaviour
{
    [Header("References")]
    public Character target;
    public Image flashImage; // 全屏白色 Image

    [Header("Settings")]
    public float flashDuration = 0.15f;

    void Start()
    {
        if (target != null)
        {
            target.OnDamaged += Flash;
        }

        if (flashImage != null)
            flashImage.color = new Color(1, 1, 1, 0); // 初始透明
    }

    void Flash(float damage)
    {
        if (flashImage != null)
            StartCoroutine(DoFlash());
    }

    System.Collections.IEnumerator DoFlash()
    {
        flashImage.color = new Color(1, 1, 1, 0.5f);
        float elapsed = 0f;

        while (elapsed < flashDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(0.5f, 0f, elapsed / flashDuration);
            flashImage.color = new Color(1, 1, 1, alpha);
            yield return null;
        }

        flashImage.color = new Color(1, 1, 1, 0);
    }
}
