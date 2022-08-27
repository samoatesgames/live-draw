using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using AntFu7.LiveDraw.Interface;

namespace AntFu7.LiveDraw.Tools
{
    public class PenDrawTool : BaseDrawTool
    {
        public override DrawTool ToolType => DrawTool.Pen;

        public PenDrawTool(IStrokeHistoryManager historyManager, InkCanvas canvas, UIElement previewElement)
            : base(historyManager, canvas, previewElement)
        {
        }
        
        public override void SetActive(bool isActive)
        {
            base.SetActive(isActive);
            if (IsActive)
            {
                m_inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
            }
        }
        
        public override void SetActiveColor(SolidColorBrush solidColorBrush)
        {
            if (m_previewElement is not Shape previewShape)
            {
                throw new Exception(
                    $"The preview element for '{nameof(PenDrawTool)}' is not of type '{nameof(Shape)}'");
            }
            previewShape.Fill = solidColorBrush;
        }

        public override void MouseMove(MouseEventArgs mouseEventArgs)
        {
        }
    }
}