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
    public class RectangleDrawTool : BaseDrawTool
    {
        public override DrawTool ToolType => DrawTool.Rectangle;

        public RectangleDrawTool(IStrokeHistoryManager historyManager, InkCanvas canvas, UIElement previewElement)
            : base(historyManager, canvas, previewElement)
        {
        }
        
        public override void SetActiveColor(SolidColorBrush solidColorBrush)
        {
            if (m_previewElement is not Shape previewShape)
            {
                throw new Exception(
                    $"The preview element for '{nameof(RectangleDrawTool)}' is not of type '{nameof(Shape)}'");
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
            newLine.FitToCurve = false;

            var rectanglePoints = new StylusPointCollection
            {
                new StylusPoint(m_startPoint.X, m_startPoint.Y),
                new StylusPoint(m_startPoint.X, endPoint.Y),
                new StylusPoint(endPoint.X, endPoint.Y),
                new StylusPoint(endPoint.X, m_startPoint.Y),
                new StylusPoint(m_startPoint.X, m_startPoint.Y)
            };

            var stroke = new Stroke(rectanglePoints)
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