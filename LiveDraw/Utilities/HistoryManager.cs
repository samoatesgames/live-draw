using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using AntFu7.LiveDraw.Interface;

namespace AntFu7.LiveDraw.Utilities
{
    public class HistoryManager : IStrokeHistoryManager
    {
        private readonly Stack<StrokesHistoryNode> m_history = new();
        private readonly Stack<StrokesHistoryNode> m_redoHistory = new();

        private readonly IStrokeIgnorable m_strokeIgnorable;
        private readonly InkCanvas m_inkCanvas;

        public HistoryManager(IStrokeIgnorable strokeIgnorable, InkCanvas inkCanvas)
        {
            m_strokeIgnorable = strokeIgnorable;
            m_inkCanvas = inkCanvas;
        }
        
        public void PushUndo(StrokesHistoryNode node)
        {
            m_history.Push(node);
        }

        public void PushRedo(StrokesHistoryNode node)
        {
            m_redoHistory.Push(node);
        }

        public void Undo()
        {
            if (!CanUndo())
            {
                return;
            }
            
            var last = Pop(m_history);
            m_strokeIgnorable.SetIgnoreStrokesChange(true);
            if (last.Type == StrokesHistoryNodeType.Added)
            {
                m_inkCanvas.Strokes.Remove(last.Strokes);
            }
            else
            {
                m_inkCanvas.Strokes.Add(last.Strokes);
            }
            m_strokeIgnorable.SetIgnoreStrokesChange(false);
            PushRedo(last);
        }

        public void Redo()
        {
            if (!CanRedo())
            {
                return;
            }
            
            var last = Pop(m_redoHistory);
            m_strokeIgnorable.SetIgnoreStrokesChange(true);
            if (last.Type == StrokesHistoryNodeType.Removed)
            {
                m_inkCanvas.Strokes.Remove(last.Strokes);
            }
            else
            {
                m_inkCanvas.Strokes.Add(last.Strokes);
            }
            m_strokeIgnorable.SetIgnoreStrokesChange(false);
            PushUndo(last);
        }

        public void ClearUndoHistory()
        {
            ClearHistory(m_history);
        }

        public void ClearRedoHistory()
        {
            ClearHistory(m_redoHistory);
        }

        private bool CanUndo()
        {
            return m_history.Any();
        }
        
        private bool CanRedo()
        {
            return m_redoHistory.Any();
        }
        
        private void ClearHistory(Stack<StrokesHistoryNode> collection)
        {
            collection?.Clear();
        }
        
        private StrokesHistoryNode Pop(Stack<StrokesHistoryNode> collection)
        {
            return collection.Any() ? collection.Pop() : null;
        }

    }
}