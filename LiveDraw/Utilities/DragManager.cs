using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AntFu7.LiveDraw.Interface;

namespace AntFu7.LiveDraw.Utilities
{
    public class DragManager : IDragManager
    {
        private readonly UIElement m_draggable;
        private bool m_isDragging;
        private Point m_lastMousePosition;
        
        public DragManager(UIElement draggable)
        {
            m_draggable = draggable;
        }

        public void StartDrag()
        {
            m_lastMousePosition = Mouse.GetPosition(Window.GetWindow(m_draggable));
            m_isDragging = true;
        }

        public void EndDrag()
        {
            m_isDragging = false;
        }

        public void OnMouseMove()
        {
            if (!m_isDragging)
            {
                return;
            }
            
            var currentMousePosition = Mouse.GetPosition(Window.GetWindow(m_draggable));
            var offset = currentMousePosition - m_lastMousePosition;
            Canvas.SetTop(m_draggable, Canvas.GetTop(m_draggable) + offset.Y);
            Canvas.SetLeft(m_draggable, Canvas.GetLeft(m_draggable) + offset.X);

            m_lastMousePosition = currentMousePosition;
        }
    }
}