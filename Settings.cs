using System.ComponentModel;

namespace DAIDialogSim.Properties {
    
    internal sealed partial class Settings {
        
        public Settings() {
            this.PropertyChanged += SettingsPropertyChangedHandler;
        }

        private void SettingsPropertyChangedHandler(object sender, PropertyChangedEventArgs e)
        {
            this.Save();
        }
    }
}
