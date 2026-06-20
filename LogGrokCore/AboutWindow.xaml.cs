using System.Diagnostics;
using System.Reflection;
using System.Windows.Navigation;
using MahApps.Metro.Controls;

namespace LogGrokCore
{
    public partial class AboutWindow : MetroWindow
    {
        public AboutWindow()
        {
            InitializeComponent();

            var assembly = Assembly.GetExecutingAssembly();
            VersionText.Text =
                assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
                ?? assembly.GetName().Version?.ToString()
                ?? "—";
            CommitText.Text =
                assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? string.Empty;
        }

        private void OnRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            using var process = new Process
            {
                StartInfo =
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                }
            };
            process.Start();
            e.Handled = true;
        }
    }
}
