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

        EnsureAlwaysIncludedShaders();

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

    /// <summary>
    /// 把运行时用 Shader.Find 加载的 shader 写进 GraphicsSettings 的 Always Included 列表。
    /// 不做这一步，URP 会在打包时把没有被任何材质引用的粒子 shader 剥离掉，
    /// 结果就是打包版里雪/雨粒子静默消失（编辑器里却完全正常）。
    /// </summary>
    static void EnsureAlwaysIncludedShaders()
    {
        string[] required =
        {
            "Universal Render Pipeline/Particles/Unlit"
        };

        UnityEngine.Object[] settings =
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");

        if (settings == null || settings.Length == 0)
        {
            Debug.LogWarning("[Build] 找不到 GraphicsSettings.asset，跳过 shader 常驻设置");
            return;
        }

        SerializedObject so = new SerializedObject(settings[0]);
        SerializedProperty list = so.FindProperty("m_AlwaysIncludedShaders");

        if (list == null)
        {
            Debug.LogWarning("[Build] GraphicsSettings 里没有 m_AlwaysIncludedShaders 字段");
            return;
        }

        foreach (string name in required)
        {
            Shader shader = Shader.Find(name);
            if (shader == null)
            {
                Debug.LogWarning("[Build] 找不到 shader: " + name);
                continue;
            }

            bool exists = false;
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                {
                    exists = true;
                    break;
                }
            }

            if (exists)
            {
                Debug.Log("[Build] shader 已在常驻列表: " + name);
                continue;
            }

            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
            Debug.Log("[Build] 加入常驻 shader 列表: " + name);
        }

        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
    }
}
