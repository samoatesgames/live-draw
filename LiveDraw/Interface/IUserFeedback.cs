using System.Windows;

namespace AntFu7.LiveDraw.Interface
{
    public interface IUserFeedback
    {
        void SetUserMessage(string message);
        void SetTempUserMessage(string message);
        MessageBoxResult ShowDialogMessage(string message, string caption, MessageBoxButton buttons);
    }
}
