    using System.Threading.Tasks;

namespace rcopy_gui
{
    public interface IDialogService
    {
        Task<string?> PickFolderAsync(string? initialPath = null, string? description = null);
    }
}