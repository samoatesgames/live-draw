namespace AntFu7.LiveDraw.Interface
{
    public interface IStrokeHistoryManager
    {
        void PushUndo(StrokesHistoryNode node);
        void PushRedo(StrokesHistoryNode node);

        void Undo();
        void Redo();

        void ClearUndoHistory();
        void ClearRedoHistory();
    }
}