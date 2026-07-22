using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.CompilerServices;
using LogGrokCore.MarkedLines;
using LogGrokCore.Search;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokCore.Tests
{
    [TestClass]
    public class MainWindowViewModelLifecycleTests
    {
        [TestMethod]
        public void Dispose_LiveDocumentsCollectionDoesNotKeepViewModelAlive()
        {
            var (viewModelReference, documents) = CreateAndDisposeViewModel();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.IsFalse(viewModelReference.IsAlive,
                "Documents.CollectionChanged still holds the disposed MainWindowViewModel.");

            // The collection must remain strongly reachable until after the assertion; otherwise
            // the test would pass even if its event still referenced the view model.
            GC.KeepAlive(documents);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static (WeakReference viewModelReference,
            ObservableCollection<DocumentViewModel> documents) CreateAndDisposeViewModel()
        {
            var missingSettingsPath = Path.Combine(Path.GetTempPath(),
                "loggrok-lifecycle-" + Guid.NewGuid().ToString("N"), "missing.yaml");
            var settings = ApplicationSettings.BuildFromFile(missingSettingsPath,
                error => Assert.Fail(error.ToString()), out _);
            var viewModel = new MainWindowViewModel(
                settings,
                new SearchAutocompleteCache(),
                documents => new MarkedLinesViewModel(documents));
            var documents = viewModel.Documents;

            viewModel.Dispose();
            return (new WeakReference(viewModel), documents);
        }
    }
}
