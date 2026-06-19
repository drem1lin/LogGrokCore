using System.Windows;
using WpfListView = System.Windows.Controls.ListView;

namespace LogGrokCore.Controls.ListControls
{
    /// <summary>
    /// Inherited attached property that carries the owning ListView down to every
    /// descendant (item panels, cells). Templates use it instead of
    /// {RelativeSource FindAncestor, AncestorType=ListView}: that FindAncestor binding
    /// fails and logs "Cannot find source for binding" each time a virtualized container
    /// is momentarily detached during recycling, whereas reading an inherited property
    /// via {RelativeSource Self} always resolves (it is simply null until attached).
    /// </summary>
    public static class ListViewOwner
    {
        public static readonly DependencyProperty OwnerProperty =
            DependencyProperty.RegisterAttached(
                "Owner",
                typeof(WpfListView),
                typeof(ListViewOwner),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));

        public static void SetOwner(DependencyObject element, WpfListView? value) =>
            element.SetValue(OwnerProperty, value);

        public static WpfListView? GetOwner(DependencyObject element) =>
            (WpfListView?)element.GetValue(OwnerProperty);
    }
}
