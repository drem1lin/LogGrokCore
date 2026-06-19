using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace LogGrokCore.Controls.ListControls
{
    public abstract class BaseListView : System.Windows.Controls.ListView
    {
        protected BaseListView()
        {
            // Publish ourselves as the inherited owner so descendant cells/panels can
            // reach this ListView without a (transiently failing) FindAncestor binding.
            ListViewOwner.SetOwner(this, this);

            void CanCopy(object _, CanExecuteRoutedEventArgs args)
            {
                args.CanExecute = GetSelectedIndices().Any();
                args.Handled = true;
            }

            CommandBindings.Add(new CommandBinding(RoutedCommands.CopyToClipboard,
                (_, args) =>
                {
                    Trace.TraceInformation("CopyToClipboard.Execute");
                    CopySelectedItemsToClipboard(formatJson: false);
                    args.Handled = true;
                },
                CanCopy));

            CommandBindings.Add(new CommandBinding(RoutedCommands.CopyToClipboardFormatted,
                (_, args) =>
                {
                    Trace.TraceInformation("CopyToClipboardFormatted.Execute");
                    CopySelectedItemsToClipboard(formatJson: true);
                    args.Handled = true;
                },
                CanCopy));
        }

        private void CopySelectedItemsToClipboard(bool formatJson)
        {
            var indices = GetSelectedIndices();

            var items =
                indices
                    .OrderBy(i => i)
                    .Select(i => Items[i]);

            var  text = new StringBuilder();
            foreach (var line in items)
            {
                var lineText = (line.ToString() ?? string.Empty).Replace("\0", string.Empty).TrimEnd();
                if (formatJson)
                    lineText = TextOperations.FormatJson(lineText);
                _ = text.Append(lineText);
                _ = text.Append("\r\n");
            }

            TextCopy.ClipboardService.SetText(text.ToString());
        }

        protected abstract IEnumerable<int> GetSelectedIndices();
    }
}