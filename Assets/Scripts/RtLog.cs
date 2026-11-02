using System.IO;
using System.Text;
using UnityEngine;

/// <summary>临时运行时诊断：把关键状态写入磁盘，绕开 Console 显示问题。定位后连同 TestLife 一起删除。</summary>
public static class RtLog
{
    private const string Path = @"C:\UnityTmp\runtime_trace.txt";

    public static void Write(string tag, string msg)
    {
        try
        {
            File.AppendAllText(Path, tag + " | " + msg + " | " + System.DateTime.Now.ToString("HH:mm:ss.fff") + "\n");
        }
        catch { }
    }

    public static void Reset()
    {
        try { File.Delete(Path); } catch { }
    }
}
