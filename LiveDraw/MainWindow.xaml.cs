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
using Point = System.Windows.Point;
using AntFu7.LiveDraw.Tools;

namespace AntFu7.LiveDraw
{
    public partial class MainWindow : IStrokeHistoryManager
    {
        private static readonly Mutex SingleApplicationMutex = new(true, "LiveDraw");
        
        private static readonly Duration Duration3 = (Duration)Application.Current.Resources["Duration3"];
        private static readonly Duration Duration4 = (Duration)Application.Current.Resources["Duration4"];
        private static readonly Duration Duration5 = (Duration)Application.Current.Resources["Duration5"];
        private static readonly Duration Duration7 = (Duration)Application.Current.Resources["Duration7"];

        private readonly IDictionary<DrawTool, BaseDrawTool> m_drawTools = new Dictionary<DrawTool, BaseDrawTool>();
        private DrawTool m_activeTool = DrawTool.Pen;
        private readonly EraserTool m_eraserTool;

        #region /---------Lifetime---------/

        public MainWindow()
        {
            if (SingleApplicationMutex.WaitOne(TimeSpan.Zero, true))
            {
                _history = new Stack<StrokesHistoryNode>();
                _redoHistory = new Stack<StrokesHistoryNode>();

                InitializeComponent();
                
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
                SetBrushSize(_brushSizes[_brushIndex]);
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
            
            m_drawTools[m_activeTool].MouseLeftButtonDown(e);
            _ignoreStrokesChange = true;
        }
        
        private void InkCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (m_eraserTool.Mode != EraserMode.None)
            {
                return;
            }
            
            m_drawTools[m_activeTool].MouseLeftButtonUp(e);
            _ignoreStrokesChange = false;
        }
        
        private void InkCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (m_eraserTool.Mode != EraserMode.None)
            {
                return;
            }
            
            m_drawTools[m_activeTool].MouseMove(e);
        }

        private void RegisterTool<TToolType>(UIElement previewElement) where TToolType : BaseDrawTool
        {
            if (!(Activator.CreateInstance(typeof(TToolType), this, MainInkCanvas, previewElement) is BaseDrawTool newTool))
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

        #endregion


        #region /---------Judge--------/

        private bool _saved;

        private bool IsUnsaved()
        {
            return MainInkCanvas.Strokes.Count != 0 && !_saved;
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

        #endregion


        #region /---------Setter---------/

        private ColorPicker _selectedColor;
        private bool _inkVisibility = true;
        private bool _displayDetailPanel;
        private bool _enable;
        private readonly int[] _brushSizes = { 3, 5, 8, 13, 20 };
        private int _brushIndex = 1;
        private bool _displayOrientation;

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
            _displayDetailPanel = v;
        }
        private void SetInkVisibility(bool v)
        {
            MainInkCanvas.BeginAnimation(OpacityProperty,
                v ? new DoubleAnimation(0, 1, Duration3) : new DoubleAnimation(1, 0, Duration3));
            HideButton.IsActived = !v;
            SetEnable(v);
            _inkVisibility = v;
        }
        private void SetEnable(bool b)
        {
            EnableButton.IsActived = !b;
            Background = Application.Current.Resources[b ? "FakeTransparent" : "TrueTransparent"] as Brush;
            _enable = b;

            if (_enable)
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
            if (ReferenceEquals(_selectedColor, b)) return;
            var solidColorBrush = b.Background as SolidColorBrush;
            if (solidColorBrush == null) return;

            var ani = new ColorAnimation(solidColorBrush.Color, Duration3);

            MainInkCanvas.DefaultDrawingAttributes.Color = solidColorBrush.Color;
            brushPreview.Background.BeginAnimation(SolidColorBrush.ColorProperty, ani);
            b.IsActived = true;
            if (_selectedColor != null)
                _selectedColor.IsActived = false;
            _selectedColor = b;
            
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
            _displayOrientation = v;
        }
        private void SetTopMost(bool v)
        {
            PinButton.IsActived = v;
            Topmost = v;
        }
        #endregion


        #region /---------IO---------/
        private StrokeCollection _preLoadStrokes = null;
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
                _saved = true;
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
            _preLoadStrokes = sc;
            var ani = new DoubleAnimation(0, Duration3);
            ani.Completed += LoadAniCompleted;
            MainInkCanvas.BeginAnimation(OpacityProperty, ani);
        }
        private void LoadAniCompleted(object sender, EventArgs e)
        {
            if (_preLoadStrokes == null) return;
            MainInkCanvas.Strokes = _preLoadStrokes;
            Display("Ink loaded");
            _saved = true;
            ClearHistory();
            MainInkCanvas.BeginAnimation(OpacityProperty, new DoubleAnimation(1, Duration3));
        }
        
        private static string GetFileNameFromPath(string path)
        {
            return Path.GetFileName(path);
        }
        #endregion


        #region /---------Generator---------/
        private static string GenerateFileName(string fileExt = ".fdw")
        {
            return DateTime.Now.ToString("yyyyMMdd-HHmmss") + fileExt;
        }
        #endregion


        #region /---------Helper---------/
        private string _staticInfo = "";
        private bool _displayingInfo;

        private async void Display(string info)
        {
            InfoBox.Text = info;
            _displayingInfo = true;
            await InfoDisplayTimeUp(new Progress<string>(box => InfoBox.Text = box));
        }
        private Task InfoDisplayTimeUp(IProgress<string> box)
        {
            return Task.Run(() =>
            {
                Task.Delay(2000).Wait();
                box.Report(_staticInfo);
                _displayingInfo = false;
            });
        }
        private void SetStaticInfo(string info)
        {
            _staticInfo = info;
            if (!_displayingInfo)
                InfoBox.Text = _staticInfo;
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

        #endregion


        #region /---------Ink---------/
        private readonly Stack<StrokesHistoryNode> _history;
        private readonly Stack<StrokesHistoryNode> _redoHistory;
        private bool _ignoreStrokesChange;

        private void Undo()
        {
            if (!CanUndo()) return;
            var last = Pop(_history);
            _ignoreStrokesChange = true;
            if (last.Type == StrokesHistoryNodeType.Added)
                MainInkCanvas.Strokes.Remove(last.Strokes);
            else
                MainInkCanvas.Strokes.Add(last.Strokes);
            _ignoreStrokesChange = false;
            PushRedo(last);
        }
        private void Redo()
        {
            if (!CanRedo()) return;
            var last = Pop(_redoHistory);
            _ignoreStrokesChange = true;
            if (last.Type == StrokesHistoryNodeType.Removed)
                MainInkCanvas.Strokes.Remove(last.Strokes);
            else
                MainInkCanvas.Strokes.Add(last.Strokes);
            _ignoreStrokesChange = false;
            PushUndo(last);
        }

        public void PushUndo(StrokesHistoryNode node)
        {
            _history.Push(node);
        }
        
        public void PushRedo(StrokesHistoryNode node)
        {
            _redoHistory.Push(node);
        }
        
        private static StrokesHistoryNode Pop(Stack<StrokesHistoryNode> collection)
        {
            return collection.Count == 0 ? null : collection.Pop();
        }
        private bool CanUndo()
        {
            return _history.Count != 0;
        }
        private bool CanRedo()
        {
            return _redoHistory.Count != 0;
        }
        private void StrokesChanged(object sender, StrokeCollectionChangedEventArgs e)
        {
            if (_ignoreStrokesChange) return;
            _saved = false;
            if (e.Added.Count != 0)
                PushUndo(new StrokesHistoryNode(e.Added, StrokesHistoryNodeType.Added));
            if (e.Removed.Count != 0)
                PushUndo(new StrokesHistoryNode(e.Removed, StrokesHistoryNodeType.Removed));
            ClearHistory(_redoHistory);
        }

        private void ClearHistory()
        {
            ClearHistory(_history);
            ClearHistory(_redoHistory);
        }
        private static void ClearHistory(Stack<StrokesHistoryNode> collection)
        {
            collection?.Clear();
        }
        private void Clear()
        {
            MainInkCanvas.Strokes.Clear();
            ClearHistory();
        }

        private void AnimatedClear()
        {
            var ani = new DoubleAnimation(0, Duration3);
            ani.Completed += ClearAniComplete; ;
            MainInkCanvas.BeginAnimation(OpacityProperty, ani);
        }
        private void ClearAniComplete(object sender, EventArgs e)
        {
            Clear();
            Display("Cleared");
            MainInkCanvas.BeginAnimation(OpacityProperty, new DoubleAnimation(1, Duration3));
        }
        #endregion


        #region /---------UI---------/
        private void DetailToggler_Click(object sender, RoutedEventArgs e)
        {
            SetDetailPanel(!_displayDetailPanel);
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
                _brushIndex--;
            else
                _brushIndex++;

            if (_brushIndex > _brushSizes.Count() - 1)
                _brushIndex = 0;
            else if (_brushIndex < 0)
                _brushIndex = _brushSizes.Count() - 1;

            SetBrushSize(_brushSizes[_brushIndex]);
        }

        private void BrushSwitchButton_Click(object sender, RoutedEventArgs e)
        {
            _brushIndex++;
            if (_brushIndex > _brushSizes.Count() - 1) _brushIndex = 0;
            SetBrushSize(_brushSizes[_brushIndex]);
        }
        private void DrawToolCombo_OnClick(object sender, RoutedEventArgs e)
        {
            DrawToolPopup.IsOpen = !DrawToolPopup.IsOpen;
            EraserPopup.IsOpen = false;
        }
        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            Undo();
        }
        private void RedoButton_Click(object sender, RoutedEventArgs e)
        {
            Redo();
        }
        private void EraserButton_Click(object sender, RoutedEventArgs e)
        {
            EraserPopup.IsOpen = !EraserPopup.IsOpen;
            DrawToolPopup.IsOpen = false;
        }
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            AnimatedClear(); //Warning! to missclick erasermode (confirmation click?)
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
        private delegate void NoArgDelegate();
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
            this.WindowState = WindowState.Minimized;
        }
        private void HideButton_Click(object sender, RoutedEventArgs e)
        {
            SetInkVisibility(!_inkVisibility);
        }
        private void EnableButton_Click(object sender, RoutedEventArgs e)
        {
            SetEnable(!_enable);
            // if (_eraserMode)
            // {
            //     SetEraserMode(!_eraserMode);
            //     EraserButton.ToolTip = "Toggle eraser mode (E)";
            //     EraseByPoint_Flag = (int)erase_mode.NONE;
            // }
        }
        private void OrientationButton_Click(object sender, RoutedEventArgs e)
        {
            SetOrientation(!_displayOrientation);
        }
        #endregion


        #region /------ Tool Mode ------/
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
        #endregion


        #region  /---------Docking---------/

        enum DockingDirection
        {
            None,
            Top,
            Left,
            Right
        }
        private int _dockingEdgeThreshold = 30;
        private int _dockingAwaitTime = 10000;
        private int _dockingSideIndent = 290;
        private void AnimatedCanvasMoving(UIElement ctr, Point to, Duration dur)
        {
            ctr.BeginAnimation(Canvas.TopProperty, new DoubleAnimation(Canvas.GetTop(ctr), to.Y, dur));
            ctr.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(Canvas.GetLeft(ctr), to.X, dur));
        }

        private DockingDirection CheckDocking()
        {
            var left = Canvas.GetLeft(Palette);
            var right = Canvas.GetRight(Palette);
            var top = Canvas.GetTop(Palette);

            if (left > 0 && left < _dockingEdgeThreshold)
                return DockingDirection.Left;
            if (right > 0 && right < _dockingEdgeThreshold)
                return DockingDirection.Right;
            if (top > 0 && top < _dockingEdgeThreshold)
                return DockingDirection.Top;
            return DockingDirection.None;
        }

        private void RightDocking()
        {
            AnimatedCanvasMoving(Palette, new Point(ActualWidth + _dockingSideIndent, Canvas.GetTop(Palette)), Duration5);
        }
        private void LeftDocking()
        {
            AnimatedCanvasMoving(Palette, new Point(0 - _dockingSideIndent, Canvas.GetTop(Palette)), Duration5);
        }
        private void TopDocking()
        {

        }

        private async void AwaitDocking()
        {

            await Docking();
        }

        private Task Docking()
        {
            return Task.Run(() =>
            {
                Thread.Sleep(_dockingAwaitTime);
                var direction = CheckDocking();
                if (direction == DockingDirection.Left) LeftDocking();
                if (direction == DockingDirection.Right) RightDocking();
                if (direction == DockingDirection.Top) TopDocking();
            });
        }
        #endregion


        #region /---------Dragging---------/
        private Point _lastMousePosition;
        private bool _isDraging;
        private bool _tempEnable;

        private void StartDrag()
        {
            _lastMousePosition = Mouse.GetPosition(this);
            _isDraging = true;
            Palette.Background = new SolidColorBrush(Colors.Transparent);
            _tempEnable = _enable;
            SetEnable(true);

            DrawToolPopup.IsOpen = false;
            EraserPopup.IsOpen = false;
        }
        private void EndDrag()
        {
            if (_isDraging == true)
            {
                SetEnable(_tempEnable);
            }
            _isDraging = false;
            Palette.Background = null;
        }
        private void PaletteGrip_MouseDown(object sender, MouseButtonEventArgs e)
        {
            StartDrag();
        }
        private void Palette_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDraging) return;
            var currentMousePosition = Mouse.GetPosition(this);
            var offset = currentMousePosition - _lastMousePosition;

            Canvas.SetTop(Palette, Canvas.GetTop(Palette) + offset.Y);
            Canvas.SetLeft(Palette, Canvas.GetLeft(Palette) + offset.X);

            _lastMousePosition = currentMousePosition;
        }
        private void Palette_MouseUp(object sender, MouseButtonEventArgs e)
        {
            EndDrag();
        }
        private void Palette_MouseLeave(object sender, MouseEventArgs e)
        {
            EndDrag();
        }
        #endregion

        #region /--------- Shortcuts --------/
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Insert)
            {
                SetEnable(!_enable);
            }

            if (!_enable)
            {
                return;
            }

            switch (e.Key)
            {
                case Key.Z:
                    Undo();
                    break;
                case Key.Y:
                    Redo();
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
                    _brushIndex++;
                    if (_brushIndex > _brushSizes.Count() - 1)
                        _brushIndex = 0;
                    SetBrushSize(_brushSizes[_brushIndex]);
                    break;
                case Key.Subtract:
                    _brushIndex--;
                    if (_brushIndex < 0)
                        _brushIndex = _brushSizes.Count() - 1;
                    SetBrushSize(_brushSizes[_brushIndex]);
                    break;
            }
        }
        #endregion

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
    }
}
