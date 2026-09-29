using Il2Cpp;
using UnityEngine;
using System.IO;

public static class PortraitDumper
{
    private static string outDir = @"D:\DoubaoWork\Project_001_WagesPerks\07_开发资产\ProbablyStolen_DevFiles\提取资源\wilde_dumped";

    public static void DumpCurrentDialoguePortrait()
    {
        try
        {
            if (Dialogue.Current == null || Dialogue.Current.portrait == null) return;
            var sprite = Dialogue.Current.portrait;
            Texture2D tex = sprite.texture;
            Rect r = sprite.textureRect;
            // 读像素
            Color32[] pixels = tex.GetPixels32((int)r.x, (int)r.y, (int)r.width, (int)r.height);
            Texture2D newTex = new Texture2D((int)r.width, (int)r.height);
            newTex.SetPixels32(pixels);
            newTex.Apply();
            // 转png
            byte[] png = ImageConversion.EncodeToPNG(newTex);
            Directory.CreateDirectory(outDir);
            string path = Path.Combine(outDir, $"portrait_{System.DateTime.Now:HHmmss}.png");
            File.WriteAllBytes(path, png);
            MelonLoader.MelonLogger.Msg($"[PortraitDumper] 立绘已保存: {path}");
        }
        catch (System.Exception ex)
        {
            MelonLoader.MelonLogger.Error($"[PortraitDumper] 导出失败: {ex.Message}");
        }
    }
}
