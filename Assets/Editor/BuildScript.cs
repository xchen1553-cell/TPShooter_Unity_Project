using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 命令行 / 菜单构建脚本
/// 用法（Unity 关闭后，在终端执行）：
///   "E:\unity\6000.6.3f1\Editor\Unity.exe" -quit -batchmode -projectPath "D:\存放编程\My project" ^
///        -executeMethod BuildScript.BuildWindows -logFile "D:\存放编程\My project\build_windows.log"
/// 也可在编辑器里用菜单 TPShooter → Build Windows 直接构建。
/// </summary>
public static class BuildScript
{
    // 输出目录与文件名（交付用）
    const string OutputDir = @"D:\存放编程\Backup_Build\TPShooter_win64";

    [MenuItem("TPShooter/Build Windows (交付)")]
    public static void BuildWindowsMenu()
    {
        BuildWindows();
    }

    public static void BuildWindows()
    {
        // 确保新加的脚本已导入、序列化已落盘
        AssetDatabase.Refresh();
        AssetDatabase.SaveAssets();

        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            Debug.LogError("[Build] Build Settings 里没有启用的场景，构建中止");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        Directory.CreateDirectory(OutputDir);

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(OutputDir, "TPShooter.exe"),
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None
        };

        Debug.Log("[Build] 场景列表: " + string.Join(", ", scenes));
        Debug.Log("[Build] 输出: " + options.locationPathName);

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        Debug.Log($"[Build] 结果={summary.result}  大小={summary.totalSize} bytes  错误={summary.totalErrors}  耗时={summary.totalTime}");

        if (Application.isBatchMode)
            EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
