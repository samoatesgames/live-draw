using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using AntFu7.LiveDraw.Interface;

namespace AntFu7.LiveDraw.Tools
{
    public class LineDrawTool : BaseDrawTool
    {
        public override DrawTool ToolType => DrawTool.Line;

        public LineDrawTool(IStrokeHistoryManager historyManager, InkCanvas canvas, UIElement previewElement) 
            : base(historyManager, canvas, previewElement)
        {
        }
        
        public override void SetActiveColor(SolidColorBrush solidColorBrush)
        {
            if (m_previewElement is not Shape previewShape)
            {
                throw new Exception(
                    $"The preview element for '{nameof(LineDrawTool)}' is not of type '{nameof(Shape)}'");
            }
            previewShape.Stroke = solidColorBrush;
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

            var pointsOfLine = new List<Point>
            {
                m_startPoint,
                endPoint,
            };
            
            var points = new StylusPointCollection(pointsOfLine);
            var stroke = new Stroke(points) { DrawingAttributes = newLine };

            if (m_lastStroke != null)
            {
                m_inkCanvas.Strokes.Remove(m_lastStroke);
            }

            m_inkCanvas.Strokes.Add(stroke);
            m_lastStroke = stroke;
        }
    }
}