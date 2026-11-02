using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 管理器 —— 显示分数、生命值、血条
/// 通过 GameManager 单例读取数据，通过事件订阅角色受伤
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("UI References")]
    public Text scoreText;
    public Text livesText;
    public Image healthBar;
    public GameObject gameOverPanel;

    [Header("Player Reference")]
    public Character player;

    void Start()
    {
        // 订阅玩家受伤/死亡事件
        if (player != null)
        {
            player.OnDamaged += UpdateHealthBar;
            player.OnDeath += ShowGameOver;
        }

        // 初始隐藏 GameOver 面板
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        // 开局先刷新一次血条，保证初始填充比例正确
        UpdateHealthBar(0f);
    }

    void Update()
    {
        // 实时更新分数和生命
        if (GameManager.Instance != null)
        {
            if (scoreText != null)
                scoreText.text = $"Score: {GameManager.Instance.score}";

            if (livesText != null)
                livesText.text = $"Lives: {GameManager.Instance.lives}";
        }
    }

    /// <summary>
    /// 更新血条（由事件触发）
    /// </summary>
    void UpdateHealthBar(float damage)
    {
        if (healthBar != null && player != null && player.maxHealth > 0f)
        {
            // Clamp01 防止血量为负或超满时血条反向 / 溢出填充
            healthBar.fillAmount = Mathf.Clamp01(player.CurrentHealth / player.maxHealth);
        }
    }

    /// <summary>
    /// 显示 GameOver 面板
    /// </summary>
    void ShowGameOver()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);
    }
}
