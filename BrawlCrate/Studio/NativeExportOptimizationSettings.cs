using System;
using System.Globalization;
using System.IO;
using System.Security;
using System.Web.Script.Serialization;

namespace BrawlCrate.Studio
{
    internal enum NativeExportOptimizationPolicy { Exact, ErrorBounded }
    internal enum NativeExportSearchEffort { Standard, Thorough }

    // Export policy belongs to local preferences, never the editable document.
    // Limits are cumulative differences from one finished target master.
    internal sealed class NativeExportOptimizationSettings
    {
        public NativeExportOptimizationPolicy Policy { get; set; } = NativeExportOptimizationPolicy.Exact;
        public NativeExportSearchEffort SearchEffort { get; set; } = NativeExportSearchEffort.Standard;
        public double RotationDegrees { get; set; } = .1;
        public double Translation { get; set; } = .002;
        public double Scale { get; set; } = .0001;
        public double GeometryRelative { get; set; } = .001;
        public double RotationDerivative { get; set; } = .1;
        public double TranslationDerivative { get; set; } = .002;
        public double ScaleDerivative { get; set; } = .0001;
        public bool ExactMandatoryFitJoins => true;

        internal NativeExportOptimizationSettings Clone() => (NativeExportOptimizationSettings)MemberwiseClone();
        internal void Validate()
        {
            if (!Enum.IsDefined(typeof(NativeExportOptimizationPolicy), Policy) || !Enum.IsDefined(typeof(NativeExportSearchEffort), SearchEffort))
                throw new InvalidDataException("Choose Exact or Error-bounded export and a supported search effort.");
            Bound(RotationDegrees, .1, "Rotation"); Bound(Translation, .002, "Translation"); Bound(Scale, .0001, "Scale");
            Bound(GeometryRelative, .001, "Full-model relative error"); Bound(RotationDerivative, .1, "Rotation derivative");
            Bound(TranslationDerivative, .002, "Translation derivative"); Bound(ScaleDerivative, .0001, "Scale derivative");
        }
        private static void Bound(double value, double ceiling, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0 || value > ceiling)
                throw new InvalidDataException(name + " must be positive and no greater than " + ceiling.ToString("G", CultureInfo.InvariantCulture) + ".");
        }
        internal string StableKey => new JavaScriptSerializer().Serialize(this);
        internal bool IsApprovedProfile => RotationDegrees == .1 && Translation == .002 && Scale == .0001 && GeometryRelative == .001 &&
            RotationDerivative == .1 && TranslationDerivative == .002 && ScaleDerivative == .0001;
    }

    internal sealed class NativeExportOptimizationPreferences
    {
        private readonly string _path;
        internal string LastError { get; private set; }
        internal NativeExportOptimizationPreferences(string path = null) { _path = path ?? DefaultPath(); }
        private static string DefaultPath()
        {
            string isolated = Environment.GetEnvironmentVariable("FIGHTERPORT_EXPORT_PREFERENCES_ROOT");
            string capture = Environment.GetEnvironmentVariable("FIGHTERPORT_CAPTURE_CONTROL");
            string folder = isolated ?? (capture == null ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Fighterport", "Workspace") : Path.Combine(capture, "workspace-state"));
            return Path.Combine(folder, "native-export-optimization.json");
        }
        private static bool StorageError(Exception error) => error is IOException || error is UnauthorizedAccessException || error is SecurityException || error is NotSupportedException || error is ArgumentException;
        internal NativeExportOptimizationSettings Read()
        {
            try
            {
                if (!File.Exists(_path)) return new NativeExportOptimizationSettings();
                if (new FileInfo(_path).Length > 16384) throw new InvalidDataException("Export preferences exceed the read limit.");
                var settings = new JavaScriptSerializer { MaxJsonLength = 16384 }.Deserialize<NativeExportOptimizationSettings>(File.ReadAllText(_path));
                if (settings == null) throw new InvalidDataException("Export preferences are empty.");
                settings.Validate(); LastError = null; return settings;
            }
            catch (Exception error) when (StorageError(error) || error is InvalidOperationException || error is InvalidDataException)
            { LastError = "Export preferences could not be read. Exact is selected."; return new NativeExportOptimizationSettings(); }
        }
        internal void Save(NativeExportOptimizationSettings settings)
        {
            settings.Validate(); string temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path)));
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(settings));
                using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { file.Write(bytes, 0, bytes.Length); file.Flush(true); }
                if (File.Exists(_path)) File.Replace(temporary, _path, null); else File.Move(temporary, _path);
                LastError = null;
            }
            catch (Exception error) when (StorageError(error)) { LastError = "Export settings apply to this operation, but could not be saved."; }
            finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch (Exception error) when (StorageError(error)) { } }
        }
    }
}
