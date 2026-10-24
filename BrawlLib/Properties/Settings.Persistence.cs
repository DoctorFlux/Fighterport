using System;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;

namespace BrawlLib.Properties
{
    public sealed partial class Settings
    {
        [Browsable(false)]
        public string LastPreferenceSaveError { get; private set; }

        // Preference storage is independent of native resource and fighter project saves.
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
            foreach (SettingsPropertyValue value in PropertyValues)
            {
                if (!value.Property.IsReadOnly && value.Property.Attributes[typeof(UserScopedSettingAttribute)] != null)
                {
                    value.IsDirty = true;
                }
            }

            Trace.TraceWarning("FighterPort Studio V2 editor preferences remain in memory: " + exception.Message);
        }
    }
}
