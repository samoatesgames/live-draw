using AntFu7.LiveDraw.Interface;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AntFu7.LiveDraw.Utilities
{
    public class DragManager : IDragManager
    {
        private readonly FrameworkElement m_draggable;
        private bool m_isDragging;
        private Point m_lastMousePosition;
        
        public DragManager(FrameworkElement draggable)
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

        public void StoreLocation()
        {
            var x = Canvas.GetLeft(m_draggable);
            var y = Canvas.GetTop(m_draggable);
            using var fileStream = new FileStream("_lastlocation.info", FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            using var writer = new StreamWriter(fileStream);
            writer.WriteLine((int)x);
            writer.WriteLine((int)y);
        }

        public void RestoreLocation()
        {
            if (!File.Exists("_lastlocation.info"))
            {
                return;
            }

            var owner = Window.GetWindow(m_draggable);
            if (owner == null)
            {
                // We don't have a window, so can't ensure we fit on it
                return;
            }

            var maxX = (int)(owner.ActualWidth - m_draggable.ActualWidth);
            var maxY = (int)(owner.ActualHeight - m_draggable.ActualHeight);

            try
            {
                using var fileStream = new FileStream("_lastlocation.info", FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(fileStream);
                var xAsString = reader.ReadLine();
                var yAsString = reader.ReadLine();

                if (!int.TryParse(xAsString, out var x) || !int.TryParse(yAsString, out var y))
                {
                    return;
                }
                
                // Make sure we are on screen
                if (x < 0) { x = 0; }
                if (y < 0) { y = 0; }
                if (x > maxX) { x = maxX; }
                if (y > maxY) { y = maxY; }

                // Set our position
                Canvas.SetLeft(m_draggable, x);
                Canvas.SetTop(m_draggable, y);
            }
            catch
            {
                // ignored
            }
        }
    }
}