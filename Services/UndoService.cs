using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace PictureExifclone.Services
{
    public class UndoService : IDisposable
    {
        private readonly LinkedList<string> undoFiles = new LinkedList<string>();
        private const int MAX_UNDO_STEPS = 10;
        private bool disposed = false;

        public int Count => undoFiles.Count;

        public event EventHandler? UndoStackChanged;

        public void SaveState(string sourceFilePath)
        {
            if (!File.Exists(sourceFilePath))
                return;

            try
            {
                string undoFile = Path.Combine(
                    Path.GetTempPath(),
                    $"undo_{Guid.NewGuid()}{Path.GetExtension(sourceFilePath)}");

                File.Copy(sourceFilePath, undoFile, true);
                undoFiles.AddLast(undoFile);

                TrimOldestFiles();

                Debug.WriteLine($"[UNDO] State saved, stack size: {undoFiles.Count}");
                UndoStackChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UNDO] Error saving state: {ex.Message}");
            }
        }

        public bool TryRestore(string targetFilePath)
        {
            if (undoFiles.Count == 0)
                return false;

            try
            {
                var last = undoFiles.Last;
                if (last == null)
                    return false;

                var undoFile = last.Value;
                undoFiles.RemoveLast();

                Debug.WriteLine($"[UNDO] Restoring from: {undoFile}");

                if (File.Exists(undoFile))
                {
                    File.Copy(undoFile, targetFilePath, true);
                    
                    try
                    {
                        File.Delete(undoFile);
                    }
                    catch
                    {
                        // Best effort cleanup
                    }

                    UndoStackChanged?.Invoke(this, EventArgs.Empty);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UNDO] Error restoring: {ex.Message}");
                return false;
            }
        }

        private void TrimOldestFiles()
        {
            while (undoFiles.Count > MAX_UNDO_STEPS)
            {
                var oldest = undoFiles.First;
                if (oldest != null)
                {
                    undoFiles.RemoveFirst();
                    try
                    {
                        if (File.Exists(oldest.Value))
                        {
                            File.Delete(oldest.Value);
                            Debug.WriteLine($"[UNDO] Deleted old undo file: {oldest.Value}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[UNDO] Error deleting old file: {ex.Message}");
                    }
                }
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;

            Debug.WriteLine("[UNDO] Cleaning up undo files...");

            foreach (var undoFile in undoFiles)
            {
                try
                {
                    if (File.Exists(undoFile))
                    {
                        File.Delete(undoFile);
                        Debug.WriteLine($"[UNDO] Cleaned up: {undoFile}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[UNDO] Error cleaning up {undoFile}: {ex.Message}");
                }
            }

            undoFiles.Clear();
            Debug.WriteLine("[UNDO] Cleanup complete");
        }
    }
}
