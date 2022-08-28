using System.Threading.Tasks;

namespace AntFu7.LiveDraw.Interface
{
    public interface IHideableControl
    {
        Task HideControlAsync();
        Task ShowControlAsync();
    }
}
