using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;

namespace rcopy_gui
{
    public class DialogService : IDialogService
    {
        public Task<string?> PickFolderAsync(string? initialPath = null, string? description = null)
        {
            var tcs = new TaskCompletionSource<string?>();
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                using var dlg = new FolderBrowserDialog
                {
                    Description = description ?? string.Empty,
                    UseDescriptionForTitle = true,
                    SelectedPath = initialPath ?? string.Empty,
                    ShowNewFolderButton = true
                };

                var result = dlg.ShowDialog();
                if (result == DialogResult.OK)
                {
                    tcs.SetResult(dlg.SelectedPath);
                }
                else
                {
                    tcs.SetResult(null);
                }
            });

            return tcs.Task;
        }
    }
}