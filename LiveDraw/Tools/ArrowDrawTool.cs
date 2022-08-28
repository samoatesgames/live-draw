using System;
using System.Collections.Generic;
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
    public class ArrowDrawTool : BaseDrawTool
    {
        public override DrawTool ToolType => DrawTool.Arrow;

        public ArrowDrawTool(IStrokeHistoryManager historyManager, InkCanvas canvas, UIElement previewElement) 
            : base(historyManager, canvas, previewElement)
        {
        }
        
        public override void SetActiveColor(SolidColorBrush solidColorBrush)
        {
            if (m_previewElement is not Shape previewShape)
            {
                throw new Exception(
                    $"The preview element for '{nameof(ArrowDrawTool)}' is not of type '{nameof(Shape)}'");
            }

            var setColorAnimation = new ColorAnimation(solidColorBrush.Color, SetColorDuration);
            previewShape.Fill.BeginAnimation(SolidColorBrush.ColorProperty, setColorAnimation);
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

            var newLine = m_inkCanvas.DefaultDrawingAttributes.Clone();
            newLine.StylusTip = StylusTip.Ellipse;
            newLine.IgnorePressure = true;
            newLine.FitToCurve = false;
            
            var endPoint = e.GetPosition(m_inkCanvas);
            var direction = endPoint - m_startPoint;
            if (Math.Abs(direction.Length) < double.Epsilon)
            {
                return;
            }

            var headSize = Math.Min(direction.Length / 10, 25) * (newLine.Width * 0.25);
            
            direction.Normalize();
            var head1 = RotateVector(direction * -headSize, 0.5);
            var head2 = RotateVector(direction * -headSize, -0.5);
            
            var pointsForArrow = new List<Point>
            {
                m_startPoint,
                endPoint,
                new(endPoint.X + head1.X, endPoint.Y + head1.Y),
                new(endPoint.X, endPoint.Y),
                new(endPoint.X + head2.X, endPoint.Y + head2.Y)
            };

            var point = new StylusPointCollection(pointsForArrow);
            var stroke = new Stroke(point) { DrawingAttributes = newLine };

            if (m_lastStroke != null)
            {
                m_inkCanvas.Strokes.Remove(m_lastStroke);
            }

            m_inkCanvas.Strokes.Add(stroke);
            m_lastStroke = stroke;
        }
        
        private Vector RotateVector(Vector v, double radians)
        {
            var ca = Math.Cos(radians);
            var sa = Math.Sin(radians);
            return new Vector(ca * v.X - sa * v.Y, sa * v.X + ca * v.Y);
        }
    }
}