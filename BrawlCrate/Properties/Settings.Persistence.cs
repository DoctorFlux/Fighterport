using System;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;

namespace BrawlCrate.Properties
{
    internal sealed partial class Settings
    {
        [Browsable(false)]
        public string LastPreferenceSaveError { get; private set; }

        // Cosmetic preferences must not prevent startup, editing or project saving.
        public override void Save()
        {
            try
            {
                base.Save();
                LastPreferenceSaveError = null;
            }
            catch (ConfigurationErrorsException exception) { PreferenceSaveFailed(exception); }
            catch (IOException exception) { PreferenceSaveFailed(exception); }
            catch (UnauthorizedAccessException exception) { PreferenceSaveFailed(exception); }
        }

        private void PreferenceSaveFailed(Exception exception)
        {
            LastPreferenceSaveError = exception.Message;
            // The native provider can clear dirty flags before its final file replacement.
            // Keep failed values eligible for a later save when the folder is writable.
            foreach (SettingsPropertyValue value in PropertyValues)
            {
                if (!value.Property.IsReadOnly && value.Property.Attributes[typeof(UserScopedSettingAttribute)] != null)
                {
                    value.IsDirty = true;
                }
            }

            Trace.TraceWarning("FighterPort Studio V2 preferences remain in memory: " + exception.Message);
        }
    }
}
