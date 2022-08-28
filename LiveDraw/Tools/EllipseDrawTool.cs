using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using AntFu7.LiveDraw.Interface;

namespace AntFu7.LiveDraw.Tools
{
    public class EllipseDrawTool : BaseDrawTool
    {
        public override DrawTool ToolType => DrawTool.Ellipse;

        public EllipseDrawTool(IStrokeHistoryManager historyManager, InkCanvas canvas, UIElement previewElement) 
            : base(historyManager, canvas, previewElement)
        {
        }
        
        public override void SetActiveColor(SolidColorBrush solidColorBrush)
        {
            if (m_previewElement is not Shape previewShape)
            {
                throw new Exception(
                    $"The preview element for '{nameof(EllipseDrawTool)}' is not of type '{nameof(Shape)}'");
            }

            var setColorAnimation = new ColorAnimation(solidColorBrush.Color, SetColorDuration);
            previewShape.Stroke.BeginAnimation(SolidColorBrush.ColorProperty, setColorAnimation);
        }
        
        public override void MouseMove(MouseEventArgs e)
        {
            if (!IsActive)
            {
                return;
            }

            if (!m_isMoving)
            {
                return;
            }

            var endPoint = e.GetPosition(m_inkCanvas);

            var newLine = m_inkCanvas.DefaultDrawingAttributes.Clone();
            newLine.StylusTip = StylusTip.Ellipse;
            newLine.IgnorePressure = true;

            var ellipsePoints = new StylusPointCollection();

            var a = 0.5 * (endPoint.X - m_startPoint.X);
            var b = 0.5 * (endPoint.Y - m_startPoint.Y);
            for (var r = 0.0; r <= 2 * Math.PI; r += 0.01)
            {
                ellipsePoints.Add(new StylusPoint(
                    0.5 * (m_startPoint.X + endPoint.X) + a * Math.Cos(r), 
                    0.5 * (m_startPoint.Y + endPoint.Y) + b * Math.Sin(r))
                );
            }

            var stroke = new Stroke(ellipsePoints)
            {
                DrawingAttributes = newLine
            };

            if (m_lastStroke != null)
            {
                m_inkCanvas.Strokes.Remove(m_lastStroke);
            }

            m_inkCanvas.Strokes.Add(stroke);
            m_lastStroke = stroke;
        }
    }
}