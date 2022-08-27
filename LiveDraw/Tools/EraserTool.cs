using System.Windows.Controls;
using System.Windows.Ink;

namespace AntFu7.LiveDraw.Tools
{
    public enum EraserMode
    {
        None,
        ByStroke,
        ByPoint
    }
    
    public class EraserTool
    {
        private readonly InkCanvas m_inkCanvas;
        
        public EraserMode Mode { get; private set; } = EraserMode.None;
        
        public EraserTool(InkCanvas canvas)
        {
            m_inkCanvas = canvas;
        }

        public void SetEraser(EraserMode mode)
        {
            Mode = mode;
            switch (Mode)
            {
                case EraserMode.None:
                    m_inkCanvas.UseCustomCursor = true;
                    m_inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                    break;
                case EraserMode.ByStroke:
                    m_inkCanvas.UseCustomCursor = false;
                    m_inkCanvas.EditingMode = InkCanvasEditingMode.EraseByStroke;
                    break;
                case EraserMode.ByPoint:
                {
                    m_inkCanvas.UseCustomCursor = false;
                    m_inkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
                    var s = m_inkCanvas.EraserShape.Height;
                    m_inkCanvas.EraserShape = new EllipseStylusShape(s, s);
                    break;
                }
            }
        }
    }
}