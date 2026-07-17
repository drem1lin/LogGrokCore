using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace LogGrokCore.Avalonia.ViewModels;

public sealed class MainWindowViewModel
{
    public MainWindowViewModel()
        : this([])
    {
    }

    public MainWindowViewModel(string[] args)
    {
        Documents = new ObservableCollection<string>(
            args.Select(Path.GetFullPath));
    }

    public ObservableCollection<string> Documents { get; }

    public string Title => "LogGrokCore";

    public string StartupStatus =>
        Documents.Count == 0
            ? "Avalonia shell is ready. Open-file and log-view UI will be ported next."
            : $"Queued {Documents.Count} file(s) from the command line.";
}
