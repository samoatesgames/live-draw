using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AntFu7.LiveDraw.Utilities
{
    public class DragManager
    {
        private readonly UIElement m_dragable;
        private bool m_isDraging;
        private Point m_lastMousePosition;
        
        public DragManager(UIElement dragable)
        {
            m_dragable = dragable;
        }

        public void StartDrag()
        {
            m_lastMousePosition = Mouse.GetPosition(Window.GetWindow(m_dragable));
            m_isDraging = true;
        }

        public void EndDrag()
        {
            m_isDraging = false;
        }

        public void OnMouseMove()
        {
            if (!m_isDraging)
            {
                return;
            }
            
            var currentMousePosition = Mouse.GetPosition(Window.GetWindow(m_dragable));
            var offset = currentMousePosition - m_lastMousePosition;
            Canvas.SetTop(m_dragable, Canvas.GetTop(m_dragable) + offset.Y);
            Canvas.SetLeft(m_dragable, Canvas.GetLeft(m_dragable) + offset.X);

            m_lastMousePosition = currentMousePosition;
        }
    }
}