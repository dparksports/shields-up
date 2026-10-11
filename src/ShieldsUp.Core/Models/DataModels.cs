namespace ShieldsUp.Core.Models
{
    public class ScheduledTaskItem
    {
        public string TaskName { get; set; }
        public string TaskPath { get; set; }
        public string State { get; set; }
        public string Action { get; set; }
        public string User { get; set; }
    }

    public class NetworkAdapterItem
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public string MacAddress { get; set; }
        public string InterfaceType { get; set; }
        public string DeviceID { get; set; }
    }

    public class StartupItem
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public string Location { get; set; }
        public string TargetPath { get; set; }
    }

    public class AppPackageItem
    {
        public string Name { get; set; }

        /// <summary>Exact Appx package name (e.g. Microsoft.Todos) — identity used
        /// for user-added custom bloatware entries, unlike the display Name.</summary>
        public string PackageName { get; set; }
        public string FullName { get; set; }
        public string Version { get; set; }
        public string Publisher { get; set; }
        public string InstallLocation { get; set; }
        public string SignatureStatus { get; set; }
        public string SignerCertificate { get; set; }
        public bool IsSystem { get; set; }
    }

    public class FirewallRuleItem : System.ComponentModel.INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string DisplayGroup { get; set; }
        public string Direction { get; set; }
        public string Action { get; set; }
        public string Profile { get; set; }
        public string Program { get; set; }
        public string Protocol { get; set; }
        public string LocalPort { get; set; }
        public string RemotePort { get; set; }
        public string RemoteAddress { get; set; }

        private bool _enabled;
        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled != value)
                {
                    _enabled = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(EnabledDisplay));
                }
            }
        }

        public string EnabledDisplay => Enabled ? "Yes" : "No";

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
        }
    }
}
