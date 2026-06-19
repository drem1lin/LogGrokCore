using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using DryIoc;
using LogGrokCore.Data;
using LogGrokCore.Diagnostics;
using LogGrokCore.MarkedLines;
using LogGrokCore.Search;
using Splat.DryIoc;

namespace LogGrokCore.Bootstrap
{
    public partial class App
    {
        private readonly Container _container;
        public App()
        {
#if DEBUG
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
#endif
            TracesLogger.Initialize();
            ExceptionsLogger.Initialize();
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            _container = new Container();
            LoggerRegistrationHelper.Register(_container);

            Trace.TraceInformation($"Git info: {ThisAssembly.Git.Branch}:{ThisAssembly.Git.Commit}");

            _container.UseDryIocDependencyResolver();
            RegisterDependencies(_container);
            Trace.TraceInformation("Initialization complete.");
            
            InitializeComponent();
        }

        public void OnNextInstanceStared(IEnumerable<string> commandLine)
        {
            ProcessCommandLine(commandLine);

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (MainWindow == null) throw new InvalidOperationException();
                if (MainWindow.WindowState == WindowState.Minimized)
                {
                    MainWindow.WindowState = WindowState.Normal;
                }

                MainWindow.Activate();
                MainWindow.Topmost = true;
                MainWindow.Topmost = false;
                MainWindow.Focus(); 
            }));
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var mainWindow = _container.Resolve<MainWindow>();
            mainWindow.Show();
            
            ProcessCommandLine(e.Args.Where(item => item != null));
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _container.Resolve<SearchAutocompleteCache>().Save();
            _container.Dispose();
            base.OnExit(e);

            // During process shutdown the OS tears down windows (msctf/TSF ->
            // DestroyWindow -> a managed window procedure) while the CLR is already
            // shutting down, which makes the runtime fail-fast (0xc0000602). This is
            // especially easy to hit when the window is closed quickly during loading.
            // All persistent state is saved above, so terminate the process directly to
            // skip that unstable managed-callback-during-loader-shutdown path.
            Logger.FlushAll();
            TerminateProcess(GetCurrentProcess(), 0);
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

        private void RegisterDependencies(IRegistrator container)
        {
            container.RegisterDelegate(ApplicationSettings.Instance);
            container.Register<MainWindowViewModel>(Reuse.Singleton);
            container.Register<SearchAutocompleteCache>(Reuse.Singleton); 
            container.Register<MarkedLinesViewModel>();
            container.Register<MainWindow>();
        }

        private void ProcessCommandLine(IEnumerable<string> commandLine)
        {            
            var mainVm = _container.Resolve<MainWindowViewModel>();
            foreach (var item in commandLine)
            {
                Dispatcher.BeginInvoke(new Action(() => { mainVm.AddDocument(item); }));
            }
        }
    }
}
