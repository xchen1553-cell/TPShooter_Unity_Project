using System.IO;
using UnityEngine;

/// <summary>
/// 临时生命周期诊断脚本：把 Awake/Start/Update 结果写入磁盘标记文件，绕开 Console 过滤问题。定位后删除。
/// </summary>
public class TestLife : MonoBehaviour
{
    private const string MarkPath = @"C:\UnityTmp\awake_fired.txt";
    private int frame;

    static void Write(string stage)
    {
        try { File.AppendAllText(MarkPath, stage + "  " + System.DateTime.Now.ToString("HH:mm:ss.fff") + "\n"); }
        catch { }
    }

    void Awake()
    {
        Write("[TestLife] Awake fired");
    }

    void Start()
    {
        Write("[TestLife] Start fired");
    }

    void Update()
    {
        if (frame < 3)
        {
            frame++;
            Write("[TestLife] Update frame " + frame);
        }
    }
}
