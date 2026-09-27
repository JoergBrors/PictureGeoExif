using System.IO;

namespace PictureExifclone.Services;

public static class AtomicFile
{
    public static void Write(string path, byte[] bytes, bool overwrite = true)
    {
        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        string temporary = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes); stream.Flush(true); }
            if (overwrite && File.Exists(full)) File.Replace(temporary, full, null);
            else File.Move(temporary, full);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static string ExportPath(string folder, string name)
    {
        var directory = Path.Combine(folder, DateTime.Now.ToString("yyyyMMdd"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(name)}_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}{Path.GetExtension(name)}");
    }
}
