using System.Windows.Input;

namespace LogGrokCore.Localization
{
    /// <summary>One entry in the Language menu.</summary>
    public sealed class LanguageViewModel : ViewModelBase
    {
        private bool _isSelected;

        public LanguageViewModel(LanguageInfo info, ICommand selectCommand)
        {
            Code = info.Code;
            DisplayName = info.DisplayName;
            SelectCommand = selectCommand;
        }

        public string Code { get; }

        public string DisplayName { get; }

        public ICommand SelectCommand { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetAndRaiseIfChanged(ref _isSelected, value);
        }
    }
}
