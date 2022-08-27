using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AntFu7.LiveDraw.Interface;
using Brush = System.Windows.Media.Brush;
using AntFu7.LiveDraw.Tools;
using AntFu7.LiveDraw.Utilities;

namespace AntFu7.LiveDraw
{
    public partial class MainWindow : IStrokeIgnorable
    {
        private delegate void NoArgDelegate();
        
        private static readonly Mutex SingleApplicationMutex = new(true, "LiveDraw");
        
        private static readonly Duration Duration3 = (Duration)Application.Current.Resources["Duration3"];
        private static readonly Duration Duration4 = (Duration)Application.Current.Resources["Duration4"];
        private static readonly Duration Duration5 = (Duration)Application.Current.Resources["Duration5"];
        private static readonly Duration Duration7 = (Duration)Application.Current.Resources["Duration7"];

        private bool m_saved;
        private ColorPicker m_selectedColor;
        private bool m_inkVisibility = true;
        private bool m_displayDetailPanel;
        private bool m_enable;
        private readonly int[] m_brushSizes = { 3, 5, 8, 13, 20 };
        private int m_brushIndex = 1;
        private bool m_displayOrientation;
        private StrokeCollection m_preLoadStrokes;
        private string m_staticInfo = "";
        private bool m_displayingInfo;
        private bool m_ignoreStrokesChange;
        
        private readonly DragManager m_dragManager;
        private readonly IStrokeHistoryManager m_historyManager;
        
        private readonly IDictionary<DrawTool, BaseDrawTool> m_drawTools = new Dictionary<DrawTool, BaseDrawTool>();
        private DrawTool m_activeTool = DrawTool.Pen;
        private readonly EraserTool m_eraserTool;

        public MainWindow()
        {
            if (SingleApplicationMutex.WaitOne(TimeSpan.Zero, true))
            {
                InitializeComponent();
                
                m_dragManager = new DragManager(Palette);
                m_historyManager = new HistoryManager(this, MainInkCanvas);
                
                m_eraserTool = new EraserTool(MainInkCanvas);

                RegisterTool<PenDrawTool>(DrawToolPreview_Pen);
                RegisterTool<LineDrawTool>(DrawToolPreview_Line);
                RegisterTool<RectangleDrawTool>(DrawToolPreview_Rectangle);
                RegisterTool<EllipseDrawTool>(DrawToolPreview_Ellipse);
                RegisterTool<ArrowDrawTool>(DrawToolPreview_Arrow);
                
                SetColor(DefaultColorPicker);
                SetEnable(true);
                SetTopMost(true);
                SetDetailPanel(true);
                SetBrushSize(m_brushSizes[m_brushIndex]);
                SetTool(DrawTool.Pen);
                
                DetailPanel.Opacity = 0;
                
                MainInkCanvas.PreviewMouseLeftButtonDown += InkCanvas_MouseLeftButtonDown;
                MainInkCanvas.MouseLeftButtonUp += InkCanvas_MouseLeftButtonUp;
                MainInkCanvas.MouseMove += InkCanvas_MouseMove;
                MainInkCanvas.Strokes.StrokesChanged += StrokesChanged;
                MainInkCanvas.MouseWheel += BrushSize;
            }
            else
            {
                Application.Current.Shutdown(0);
            }
        }

        private void InkCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (m_eraserTool.Mode != EraserMode.None)
            {
                return;
            }

            if (m_activeTool != DrawTool.Pen)
            {
                SetIgnoreStrokesChange(true);
            }
            
            m_drawTools[m_activeTool].MouseLeftButtonDown(e);
        }
        
        private void InkCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (m_eraserTool.Mode != EraserMode.None)
            {
                return;
            }
            
            SetIgnoreStrokesChange(false);
            m_drawTools[m_activeTool].MouseLeftButtonUp(e);
        }
        
        private void InkCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            m_dragManager.OnMouseMove();
            
            if (m_eraserTool.Mode != EraserMode.None)
            {
                return;
            }
            
            m_drawTools[m_activeTool].MouseMove(e);
        }

        private void RegisterTool<TToolType>(UIElement previewElement) where TToolType : BaseDrawTool
        {
            if (!(Activator.CreateInstance(typeof(TToolType), m_historyManager, MainInkCanvas, previewElement) is BaseDrawTool newTool))
            {
                throw new Exception($"Failed to create a new tool of type '{(typeof(TToolType))}'");
            }
            
            m_drawTools[newTool.ToolType] = newTool;
        }

        private void Exit(object sender, EventArgs e)
        {
            if (IsUnsaved())
                QuickSave("ExitingAutoSave_");

            Application.Current.Shutdown(0);
        }
        
        private bool IsUnsaved()
        {
            return MainInkCanvas.Strokes.Count != 0 && !m_saved;
        }

        private bool PromptToSave()
        {
            if (!IsUnsaved())
                return true;
            var r = MessageBox.Show("You have unsaved work, do you want to save it now?", "Unsaved data",
                MessageBoxButton.YesNoCancel);
            if (r == MessageBoxResult.Yes || r == MessageBoxResult.OK)
            {
                QuickSave();
                return true;
            }
            if (r == MessageBoxResult.No || r == MessageBoxResult.None)
                return true;
            return false;
        }
        
        private void SetDetailPanel(bool v)
        {
            if (v)
            {
                DetailTogglerRotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(180, Duration5));
                DetailPanel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, Duration4));
            }
            else
            {
                DetailTogglerRotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, Duration5));
                DetailPanel.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, Duration4));
            }
            m_displayDetailPanel = v;
        }
        
        private void SetInkVisibility(bool v)
        {
            MainInkCanvas.BeginAnimation(OpacityProperty,
                v ? new DoubleAnimation(0, 1, Duration3) : new DoubleAnimation(1, 0, Duration3));
            HideButton.IsActived = !v;
            SetEnable(v);
            m_inkVisibility = v;
        }
        
        private void SetEnable(bool b)
        {
            EnableButton.IsActived = !b;
            Background = Application.Current.Resources[b ? "FakeTransparent" : "TrueTransparent"] as Brush;
            m_enable = b;

            if (m_enable)
            {
                SetTool(m_activeTool);
            }
            else
            {
                SetStaticInfo("Locked");
                MainInkCanvas.EditingMode = InkCanvasEditingMode.None; //No inking possible
            }
        }

        private void SetTool(DrawTool newActiveToolType)
        {
            SetStaticInfo($"{newActiveToolType} Mode");
            MainInkCanvas.UseCustomCursor = true;
            
            m_eraserTool.SetEraser(EraserMode.None);
            
            m_activeTool = newActiveToolType;
            foreach (var (toolType, drawTool) in m_drawTools)
            {
                drawTool.SetActive(toolType == newActiveToolType);
            }
        }

        private void SetColor(ColorPicker b)
        {
            if (ReferenceEquals(m_selectedColor, b)) return;
            var solidColorBrush = b.Background as SolidColorBrush;
            if (solidColorBrush == null) return;

            var ani = new ColorAnimation(solidColorBrush.Color, Duration3);

            MainInkCanvas.DefaultDrawingAttributes.Color = solidColorBrush.Color;
            brushPreview.Background.BeginAnimation(SolidColorBrush.ColorProperty, ani);
            b.IsActived = true;
            if (m_selectedColor != null)
                m_selectedColor.IsActived = false;
            m_selectedColor = b;
            
            foreach (var drawTool in m_drawTools.Values)
            {
                drawTool.SetActiveColor(solidColorBrush);
            }
            SetTool(m_activeTool);
        }
        
        private void SetBrushSize(double s)
        {
            if (MainInkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint)
            {
                MainInkCanvas.EditingMode = InkCanvasEditingMode.GestureOnly;
                MainInkCanvas.EraserShape = new EllipseStylusShape(s, s);
                MainInkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
            }
            else
            {
                MainInkCanvas.DefaultDrawingAttributes.Height = s;
                MainInkCanvas.DefaultDrawingAttributes.Width = s;
                brushPreview?.BeginAnimation(HeightProperty, new DoubleAnimation(s, Duration4));
                brushPreview?.BeginAnimation(WidthProperty, new DoubleAnimation(s, Duration4));
            }
        }

        private void SetOrientation(bool v)
        {
            PaletteRotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(v ? -90 : 0, Duration4));
            Palette.BeginAnimation(MinWidthProperty, new DoubleAnimation(v ? 90 : 0, Duration7));
            m_displayOrientation = v;
        }
        
        private void SetTopMost(bool v)
        {
            PinButton.IsActived = v;
            Topmost = v;
        }
        
        private void QuickSave(string filename = "QuickSave_")
        {
            if (!Directory.Exists("Save"))
                Directory.CreateDirectory("Save");
            
            Save(new FileStream("Save\\" + filename + GenerateFileName(), FileMode.OpenOrCreate));
        }
        
        private void Save(Stream fs)
        {
            try
            {
                if (fs == Stream.Null) return;
                MainInkCanvas.Strokes.Save(fs);
                m_saved = true;
                Display("Ink saved");
                fs.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                Display("Fail to save");
            }
        }
        
        private StrokeCollection Load(Stream fs)
        {
            try
            {
                return new StrokeCollection(fs);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                Display("Fail to load");
            }
            return new StrokeCollection();
        }
        
        private void AnimatedReload(StrokeCollection sc)
        {
            m_preLoadStrokes = sc;
            var ani = new DoubleAnimation(0, Duration3);
            ani.Completed += LoadAniCompleted;
            MainInkCanvas.BeginAnimation(OpacityProperty, ani);
        }
        
        private void LoadAniCompleted(object sender, EventArgs e)
        {
            if (m_preLoadStrokes == null) return;
            MainInkCanvas.Strokes = m_preLoadStrokes;
            Display("Ink loaded");
            m_saved = true;
            ClearHistory();
            MainInkCanvas.BeginAnimation(OpacityProperty, new DoubleAnimation(1, Duration3));
        }
        
        private static string GenerateFileName(string fileExt = ".fdw")
        {
            return DateTime.Now.ToString("yyyyMMdd-HHmmss") + fileExt;
        }
        
        private async void Display(string info)
        {
            InfoBox.Text = info;
            m_displayingInfo = true;
            await InfoDisplayTimeUp(new Progress<string>(box => InfoBox.Text = box));
        }
        
        private Task InfoDisplayTimeUp(IProgress<string> box)
        {
            return Task.Run(() =>
            {
                Task.Delay(2000).Wait();
                box.Report(m_staticInfo);
                m_displayingInfo = false;
            });
        }
        
        private void SetStaticInfo(string info)
        {
            m_staticInfo = info;
            if (!m_displayingInfo)
                InfoBox.Text = m_staticInfo;
        }

        private static Stream SaveDialog(string initFileName, string fileExt = ".fdw", string filter = "Free Draw Save (*.fdw)|*fdw")
        {
            if (!Directory.Exists("Save"))
                Directory.CreateDirectory("Save");
            
            var dialog = new Microsoft.Win32.SaveFileDialog()
            {
                DefaultExt = fileExt,
                Filter = filter,
                FileName = initFileName,
                InitialDirectory = Directory.GetCurrentDirectory() + "Save"
            };
            return dialog.ShowDialog() == true ? dialog.OpenFile() : Stream.Null;
        }
        
        private static Stream OpenDialog(string fileExt = ".fdw", string filter = "Free Draw Save (*.fdw)|*fdw")
        {
            var dialog = new Microsoft.Win32.OpenFileDialog()
            {
                DefaultExt = fileExt,
                Filter = filter,
            };
            return dialog.ShowDialog() == true ? dialog.OpenFile() : Stream.Null;
        }
        
        private void StrokesChanged(object sender, StrokeCollectionChangedEventArgs e)
        {
            if (m_ignoreStrokesChange)
            {
                return;
            }
            
            m_saved = false;
            if (e.Added.Count != 0)
            {
                m_historyManager.PushUndo(new StrokesHistoryNode(e.Added, StrokesHistoryNodeType.Added));
            }

            if (e.Removed.Count != 0)
            {
                m_historyManager.PushUndo(new StrokesHistoryNode(e.Removed, StrokesHistoryNodeType.Removed));
            }

            m_historyManager.ClearRedoHistory();
        }

        private void ClearHistory()
        {
            m_historyManager.ClearUndoHistory();
            m_historyManager.ClearRedoHistory();
        }
        
        private void Clear()
        {
            MainInkCanvas.Strokes.Clear();
            ClearHistory();
        }

        private void AnimatedClear()
        {
            var ani = new DoubleAnimation(0, Duration3);
            ani.Completed += ClearAniComplete;
            MainInkCanvas.BeginAnimation(OpacityProperty, ani);
        }
        
        private void ClearAniComplete(object sender, EventArgs e)
        {
            Clear();
            Display("Cleared");
            MainInkCanvas.BeginAnimation(OpacityProperty, new DoubleAnimation(1, Duration3));
        }

        private void DetailToggler_Click(object sender, RoutedEventArgs e)
        {
            SetDetailPanel(!m_displayDetailPanel);
        }
        
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Topmost = false;
            var anim = new DoubleAnimation(0, Duration3);
            anim.Completed += Exit;
            BeginAnimation(OpacityProperty, anim);
        }

        private void ColorPickers_Click(object sender, RoutedEventArgs e)
        {
            var border = sender as ColorPicker;
            if (border == null) return;
            SetColor(border);
        }
        
        private void BrushSize(object sender, MouseWheelEventArgs e)
        {
            int delta = e.Delta;
            if (delta < 0)
                m_brushIndex--;
            else
                m_brushIndex++;

            if (m_brushIndex > m_brushSizes.Count() - 1)
                m_brushIndex = 0;
            else if (m_brushIndex < 0)
                m_brushIndex = m_brushSizes.Count() - 1;

            SetBrushSize(m_brushSizes[m_brushIndex]);
        }

        private void BrushSwitchButton_Click(object sender, RoutedEventArgs e)
        {
            m_brushIndex++;
            if (m_brushIndex > m_brushSizes.Count() - 1) m_brushIndex = 0;
            SetBrushSize(m_brushSizes[m_brushIndex]);
        }
        
        private void DrawToolCombo_OnClick(object sender, RoutedEventArgs e)
        {
            DrawToolPopup.IsOpen = !DrawToolPopup.IsOpen;
            EraserPopup.IsOpen = false;
        }
        
        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            m_historyManager.Undo();
        }
        
        private void RedoButton_Click(object sender, RoutedEventArgs e)
        {
            m_historyManager.Redo();
        }
        
        private void EraserButton_Click(object sender, RoutedEventArgs e)
        {
            EraserPopup.IsOpen = !EraserPopup.IsOpen;
            DrawToolPopup.IsOpen = false;
        }
        
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to clear all your annotations? " +
                                         "This can not be undone."
                , "CLEAR CANVAS?",
                MessageBoxButton.YesNo);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }
            
            AnimatedClear();
        }
        
        private void PinButton_Click(object sender, RoutedEventArgs e)
        {
            SetTopMost(!Topmost);
        }
        
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainInkCanvas.Strokes.Count == 0)
            {
                Display("Nothing to save");
                return;
            }
            QuickSave();
        }
        
        private void SaveButton_RightClick(object sender, MouseButtonEventArgs e)
        {
            if (MainInkCanvas.Strokes.Count == 0)
            {
                Display("Nothing to save");
                return;
            }
            Save(SaveDialog(GenerateFileName()));
        }
        
        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            if (!PromptToSave()) return;
            var s = OpenDialog();
            if (s == Stream.Null) return;
            AnimatedReload(Load(s));
        }
        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainInkCanvas.Strokes.Count == 0)
            {
                Display("Nothing to save");
                return;
            }
            try
            {
                var s = SaveDialog("ImageExport_" + GenerateFileName(".png"), ".png",
                    "Portable Network Graphics (*png)|*png");
                if (s == Stream.Null) return;
                var rtb = new RenderTargetBitmap((int)MainInkCanvas.ActualWidth, (int)MainInkCanvas.ActualHeight, 96d,
                    96d, PixelFormats.Pbgra32);
                rtb.Render(MainInkCanvas);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));
                encoder.Save(s);
                s.Close();
                Display("Image Exported");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                Display("Export failed");
            }
        }

        private void ExportButton_RightClick(object sender, MouseButtonEventArgs e)
        {
            if (MainInkCanvas.Strokes.Count == 0)
            {
                Display("Nothing to save");
                return;
            }
            try
            {
                var s = SaveDialog("ImageExportWithBackground_" + GenerateFileName(".png"), ".png", "Portable Network Graphics (*png)|*png");
                if (s == Stream.Null) return;
                Palette.Opacity = 0;
                Palette.Dispatcher.Invoke(DispatcherPriority.Render, (NoArgDelegate)delegate { });
                Thread.Sleep(100);
                var fromHwnd = Graphics.FromHwnd(IntPtr.Zero);
                var w = (int)(SystemParameters.PrimaryScreenWidth * fromHwnd.DpiX / 96.0);
                var h = (int)(SystemParameters.PrimaryScreenHeight * fromHwnd.DpiY / 96.0);
                var image = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                Graphics.FromImage(image).CopyFromScreen(0, 0, 0, 0, new System.Drawing.Size(w, h), CopyPixelOperation.SourceCopy);
                image.Save(s, ImageFormat.Png);
                Palette.Opacity = 1;
                s.Close();
                Display("Image Exported");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                Display("Export failed");
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }
        
        private void HideButton_Click(object sender, RoutedEventArgs e)
        {
            SetInkVisibility(!m_inkVisibility);
        }
        
        private void EnableButton_Click(object sender, RoutedEventArgs e)
        {
            SetEnable(!m_enable);
        }
        
        private void OrientationButton_Click(object sender, RoutedEventArgs e)
        {
            SetOrientation(!m_displayOrientation);
        }

        private void SetActiveTool_Pen(object sender, RoutedEventArgs e)
        {
            SetTool(DrawTool.Pen);
            DrawToolPopup.IsOpen = false;
        }
        
        private void SetActiveTool_Line(object sender, RoutedEventArgs e)
        {
            SetTool(DrawTool.Line);
            DrawToolPopup.IsOpen = false;
        }
        
        private void SetActiveTool_Rectangle(object sender, RoutedEventArgs e)
        {
            SetTool(DrawTool.Rectangle);
            DrawToolPopup.IsOpen = false;
        }
        
        private void SetActiveTool_Ellipse(object sender, RoutedEventArgs e)
        {
            SetTool(DrawTool.Ellipse);
            DrawToolPopup.IsOpen = false;
        }
        
        private void SetActiveTool_Arrow(object sender, RoutedEventArgs e)
        {
            SetTool(DrawTool.Arrow);
            DrawToolPopup.IsOpen = false;
        }
        
        private void SetActiveEraser_None(object sender, RoutedEventArgs e)
        {
            EraserPopup.IsOpen = false;
            m_eraserTool.SetEraser(EraserMode.None);
            SetTool(m_activeTool);
        }
        
        private void SetActiveEraser_Stroke(object sender, RoutedEventArgs e)
        {
            EraserPopup.IsOpen = false;
            m_eraserTool.SetEraser(EraserMode.ByStroke);
        }
        
        private void SetActiveEraser_Pen(object sender, RoutedEventArgs e)
        {
            EraserPopup.IsOpen = false;
            m_eraserTool.SetEraser(EraserMode.ByPoint);
        }
        
        private void PaletteGrip_MouseDown(object sender, MouseButtonEventArgs e)
        {
            m_dragManager.StartDrag();
            DrawToolPopup.IsOpen = false;
            EraserPopup.IsOpen = false;
        }
        
        private void Palette_MouseMove(object sender, MouseEventArgs e)
        {
            m_dragManager.OnMouseMove();
        }
        
        private void Palette_MouseUp(object sender, MouseButtonEventArgs e)
        {
            m_dragManager.EndDrag();
        }
        
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Insert)
            {
                SetEnable(!m_enable);
            }

            if (!m_enable)
            {
                return;
            }

            switch (e.Key)
            {
                case Key.Z:
                    m_historyManager.Undo();
                    break;
                case Key.Y:
                    m_historyManager.Redo();
                    break;
                case Key.E:
                    m_eraserTool.SetEraser(m_eraserTool.Mode == EraserMode.ByStroke
                        ? EraserMode.ByPoint
                        : EraserMode.ByStroke);
                    break;
                case Key.B:
                    SetTool(DrawTool.Pen);
                    break;
                case Key.L:
                    SetTool(DrawTool.Line);
                    break;
                case Key.R:
                    SetTool(DrawTool.Rectangle);
                    break;
                case Key.O:
                    SetTool(DrawTool.Ellipse);
                    break;
                case Key.A:
                    SetTool(DrawTool.Arrow);
                    break;
                case Key.Add:
                    m_brushIndex++;
                    if (m_brushIndex > m_brushSizes.Count() - 1)
                        m_brushIndex = 0;
                    SetBrushSize(m_brushSizes[m_brushIndex]);
                    break;
                case Key.Subtract:
                    m_brushIndex--;
                    if (m_brushIndex < 0)
                        m_brushIndex = m_brushSizes.Count() - 1;
                    SetBrushSize(m_brushSizes[m_brushIndex]);
                    break;
            }
        }

        public void SetIgnoreStrokesChange(bool ignoreChanges)
        {
            m_ignoreStrokesChange = ignoreChanges;
        }
    }
}
