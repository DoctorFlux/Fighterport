using BrawlCrate.Studio;
using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        private void ShowBrawlDiscLibrary()
        {
            if (SimpleWorkspaceBusy) throw new InvalidOperationException("Finish or cancel the current operation before adding a game source.");
            var restore = CaptureWorkspaceFocus();
            PauseSourceOperation();
            try
            {
                // Reading saved source choices does not register a path or
                // change the active temporary source catalogue on cancellation.
                var shared = _sharedSourceLibrary ?? new AnimationLibrary();
                var prior = shared.Read().GameLocations.LastOrDefault(source => source.Game == "Brawl" && source.Indexed);
                string image = prior != null && (File.Exists(prior.Path) || Directory.Exists(prior.Path)) ? prior.Path : null;
                using (var dialog = new BrawlDiscDialog(image))
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    IndexLibraryGameSource(this, dialog.Request);
                }
            }
            finally { restore(); }
        }
    }
}
