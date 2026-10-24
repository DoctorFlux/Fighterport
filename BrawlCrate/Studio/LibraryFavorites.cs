using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Web.Script.Serialization;

namespace BrawlCrate.Studio
{
    // Presentation preferences belong to one library, never to its catalogue/Undo.
    internal sealed class LibraryFavorites
    {
        private const int MaximumBytes = 2 * 1024 * 1024;
        private readonly object _gate = new object();
        private readonly string _path;
        private readonly HashSet<string> _ids = new HashSet<string>(StringComparer.Ordinal);
        internal string LastError { get; private set; }
        public sealed class Data { public int Schema { get; set; } public string[] Favorites { get; set; } }
        internal LibraryFavorites(string directory)
        {
            _path = Path.Combine(directory, "preferences.json");
            try
            {
                AnimationLibrary.NoLinks(_path);
                if (!File.Exists(_path)) return;
                using (var file = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (file.Length > MaximumBytes) throw new InvalidDataException("Preferences exceed the read bound.");
                    using (var reader = new StreamReader(file, Encoding.UTF8))
                    {
                        var data = new JavaScriptSerializer().Deserialize<Data>(reader.ReadToEnd());
                        if (data == null || data.Schema != 1 || data.Favorites == null || data.Favorites.Length > AnimationLibrary.MaximumClips)
                            throw new InvalidDataException("Unsupported preferences.");
                        foreach (string id in data.Favorites) if (ValidId(id)) _ids.Add(id);
                    }
                }
            }
            catch (Exception error) when (StorageError(error) || error is ArgumentException || error is InvalidOperationException)
            { _ids.Clear(); LastError = "Could not read favorites. Browsing remains available; new favorites are available for this session."; }
        }
        private static bool StorageError(Exception error) => error is IOException || error is InvalidDataException || error is UnauthorizedAccessException || error is SecurityException || error is NotSupportedException;
        private static bool ValidId(string id) => id != null && Guid.TryParseExact(id, "N", out _);
        internal string[] Ids { get { lock (_gate) return _ids.ToArray(); } }
        internal bool Contains(string id) { lock (_gate) return id != null && _ids.Contains(id); }
        internal void Set(string id, bool favorite)
        {
            if (!ValidId(id)) throw new ArgumentException("Choose an exact library clip identity.");
            lock (_gate)
            {
                if (favorite && !_ids.Contains(id) && _ids.Count >= AnimationLibrary.MaximumClips)
                    throw new InvalidOperationException("The favorite limit has been reached.");
                if (favorite) _ids.Add(id); else _ids.Remove(id);
                string temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    AnimationLibrary.NoLinks(_path);
                    Directory.CreateDirectory(Path.GetDirectoryName(_path));
                    byte[] bytes = Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(new Data { Schema = 1, Favorites = _ids.OrderBy(x => x, StringComparer.Ordinal).ToArray() }));
                    if (bytes.Length > MaximumBytes) throw new InvalidDataException("Preferences exceed the write bound.");
                    using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    { file.Write(bytes, 0, bytes.Length); file.Flush(true); }
                    if (File.Exists(_path)) File.Replace(temporary, _path, null); else File.Move(temporary, _path);
                    LastError = null;
                }
                catch (Exception error) when (StorageError(error) || error is ArgumentException)
                { LastError = "Favorites are available for this session. Could not save library preferences to disk."; }
                finally
                {
                    try { if (File.Exists(temporary)) File.Delete(temporary); }
                    catch (Exception error) when (StorageError(error)) { }
                }
            }
        }
    }
}
