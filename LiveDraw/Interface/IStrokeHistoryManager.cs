namespace AntFu7.LiveDraw.Interface
{
    public interface IStrokeHistoryManager
    {
        void PushUndo(StrokesHistoryNode node);
        void PushRedo(StrokesHistoryNode node);
    }
}