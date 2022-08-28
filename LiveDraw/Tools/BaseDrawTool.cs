using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using AntFu7.LiveDraw.Interface;

namespace AntFu7.LiveDraw.Tools
{
    public abstract class BaseDrawTool
    {
        protected static readonly Duration SetColorDuration = (Duration)Application.Current.Resources["Duration3"];

        protected IStrokeHistoryManager m_historyManager;
        protected InkCanvas m_inkCanvas;
        protected UIElement m_previewElement;
        
        protected bool m_isMoving;
        protected Point m_startPoint;
        protected Stroke m_lastStroke;
        
        public bool IsActive { get; protected set; }
        public abstract DrawTool ToolType { get; }
        
        protected BaseDrawTool(IStrokeHistoryManager historyManager, InkCanvas canvas, UIElement previewElement)
        {
            m_historyManager = historyManager;
            m_inkCanvas = canvas;
            m_previewElement = previewElement;
        }

        public virtual void SetActive(bool isActive)
        {
            IsActive = isActive;
            m_previewElement.Visibility = IsActive ? Visibility.Visible : Visibility.Hidden;

            if (IsActive)
            {
                m_inkCanvas.EditingMode = InkCanvasEditingMode.None;
            }
        }

        public abstract void SetActiveColor(SolidColorBrush solidColorBrush);

        public virtual void MouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (!IsActive)
            {
                return;
            }
            
            m_isMoving = true;
            m_startPoint = e.GetPosition(m_inkCanvas);
            m_lastStroke = null;
        }

        public virtual void MouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (!IsActive)
            {
                return;
            }
            
            if (m_isMoving)
            {
                if (m_lastStroke != null)
                {
                    var collection = new StrokeCollection { m_lastStroke };
                    m_historyManager.PushUndo(new StrokesHistoryNode(collection, StrokesHistoryNodeType.Added));
                }
            }
            
            m_isMoving = false;
        }

        public abstract void MouseMove(MouseEventArgs e);
    }
}