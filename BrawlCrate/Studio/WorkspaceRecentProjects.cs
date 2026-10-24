using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Web.Script.Serialization;

namespace BrawlCrate.Studio
{
    // Local navigation history only. Never load a native document or inspect assets here.
    internal sealed class WorkspaceRecentProjects
    {
        internal const int Limit = 10;
        internal static WorkspaceRecentProjects Current { get; } = new WorkspaceRecentProjects(DefaultPath());
        private readonly string _path;
        private readonly List<string> _paths = new List<string>();
        internal string LastError { get; private set; }
        internal string[] Paths => _paths.ToArray();

        private static string DefaultPath()
        {
            string capture = Environment.GetEnvironmentVariable("FIGHTERPORT_CAPTURE_CONTROL");
            string folder = capture == null
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Fighterport", "Workspace")
                : Path.Combine(capture, "workspace-state");
            return Path.Combine(folder, "recent-projects.json");
        }

        internal WorkspaceRecentProjects(string path)
        {
            _path = path;
            try
            {
                if (!File.Exists(path)) return;
                if (new FileInfo(path).Length > 65536) throw new InvalidDataException("History is too large.");
                var paths = new JavaScriptSerializer().Deserialize<string[]>(File.ReadAllText(path));
                if (paths == null) throw new InvalidDataException("History is empty.");
                foreach (string value in paths)
                {
                    string full = Normalize(value);
                    if (full != null && !_paths.Contains(full, StringComparer.OrdinalIgnoreCase)) _paths.Add(full);
                    if (_paths.Count == Limit) break;
                }
            }
            catch (Exception error) when (StorageError(error) || error is ArgumentException || error is InvalidOperationException || error is InvalidDataException)
            { LastError = "Could not read recent history. Open a project using its file path or Browse."; }
        }

        private static bool StorageError(Exception error) => error is IOException ||
            error is UnauthorizedAccessException || error is SecurityException || error is NotSupportedException;

        private static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try
            {
                if (!Path.IsPathRooted(path)) return null;
                string full = Path.GetFullPath(path);
                return string.Equals(Path.GetExtension(full), ".fpstudio", StringComparison.OrdinalIgnoreCase) ? full : null;
            }
            catch (Exception error) when (StorageError(error) || error is ArgumentException) { return null; }
        }

        internal void Record(string path)
        {
            string full = Normalize(path);
            if (full == null) return;
            _paths.RemoveAll(p => string.Equals(p, full, StringComparison.OrdinalIgnoreCase));
            _paths.Insert(0, full);
            if (_paths.Count > Limit) _paths.RemoveRange(Limit, _paths.Count - Limit);
            Save();
        }

        internal void Forget(string path)
        {
            _paths.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            Save();
        }

        internal void Clear() { _paths.Clear(); Save(); }

        private void Save()
        {
            string temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                File.WriteAllText(temporary, new JavaScriptSerializer().Serialize(_paths));
                if (File.Exists(_path)) File.Replace(temporary, _path, null);
                else File.Move(temporary, _path);
                LastError = null;
            }
            catch (Exception error) when (StorageError(error) || error is ArgumentException)
            { LastError = "Recent history is available for this session. Could not save it to disk."; }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (Exception error) when (StorageError(error)) { }
            }
        }
    }
}
