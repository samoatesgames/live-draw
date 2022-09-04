using AntFu7.LiveDraw.Interface;
using AntFu7.LiveDraw.Tools;
using AntFu7.LiveDraw.Utilities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AntFu7.LiveDraw.Dialog;
using Brush = System.Windows.Media.Brush;

namespace AntFu7.LiveDraw
{
    public partial class MainWindow : IStrokeIgnorable, IUserFeedback, IHideableControl
    {
        private static readonly Mutex SingleApplicationMutex = new(true, "LiveDraw");
        
        private static readonly Duration Duration3 = (Duration)Application.Current.Resources["Duration3"];
        private static readonly Duration Duration4 = (Duration)Application.Current.Resources["Duration4"];
        private static readonly Duration Duration5 = (Duration)Application.Current.Resources["Duration5"];
        private static readonly Duration Duration7 = (Duration)Application.Current.Resources["Duration7"];
        
        private ColorPicker m_selectedColor;
        private bool m_inkVisibility = true;
        private bool m_displayDetailPanel;
        private bool m_enable;
        private readonly int[] m_brushSizes = { 3, 5, 8, 13, 20 };
        private int m_brushIndex = 1;
        private bool m_displayOrientation;
        private string m_staticInfo = "";
        private bool m_displayingInfo;
        private bool m_ignoreStrokesChange;
        
        private readonly IDragManager m_dragManager;
        private readonly IStrokeHistoryManager m_historyManager;
        private readonly IFileManager m_fileManager;

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
                m_fileManager = new FileManager(this, this, m_historyManager, MainInkCanvas);

                m_eraserTool = new EraserTool(MainInkCanvas);

                RegisterTool<PenDrawTool>(DrawToolPreview_Pen);
                RegisterTool<LineDrawTool>(DrawToolPreview_Line);
                RegisterTool<RectangleDrawTool>(DrawToolPreview_Rectangle);
                RegisterTool<EllipseDrawTool>(DrawToolPreview_Ellipse);
                RegisterTool<ArrowDrawTool>(DrawToolPreview_Arrow);
                
                SetColor(ColorPicker6);
                SetEnable(true);
                SetTopMost(true);
                SetDetailPanel(true);
                SetBrushSize(m_brushSizes[m_brushIndex]);
                SetTool(DrawTool.Pen);

                EraserTool_Icon.Visibility = Visibility.Visible;
                EraserTool_Icon.Fill = Brushes.DarkGray;
                EraserTool_PointIcon.Visibility = Visibility.Hidden;

                foreach (var monitor in GetMonitors())
                {
                    MonitorItems.Items.Add(monitor);
                }

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

        private void Palette_OnLoaded(object sender, RoutedEventArgs e)
        {
            m_dragManager.RestoreLocation();
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

        private IReadOnlyCollection<MonitorDescription> GetMonitors()
        {
            return new[] { new MonitorDescription(1, "Monitor One") };
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
            if (m_fileManager.IsUnsaved())
            {
                m_fileManager.QuickSave();
            }

            m_dragManager.StoreLocation();
            Application.Current.Shutdown(0);
        }
        
        private bool PromptToSave()
        {
            if (!m_fileManager.IsUnsaved())
            {
                return true;
            }

            var r = ShowDialogMessage(
                "You have unsaved work, do you want to save it now?", 
                "Unsaved data",
                MessageBoxButton.YesNoCancel);
            
            if (r == MessageBoxResult.Yes)
            {
                m_fileManager.QuickSave();
                return true;
            }

            if (r == MessageBoxResult.No)
            {
                return true;
            }

            return false; // Cancel
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
                SetUserMessage("Locked");
                MainInkCanvas.EditingMode = InkCanvasEditingMode.None; //No inking possible
            }
        }

        private void SetTool(DrawTool newActiveToolType)
        {
            SetUserMessage($"{newActiveToolType} Mode");
            MainInkCanvas.UseCustomCursor = true;
            
            m_eraserTool.SetEraser(EraserMode.None);
            EraserTool_Icon.Visibility = Visibility.Visible;
            EraserTool_Icon.Fill = Brushes.DarkGray;
            EraserTool_PointIcon.Visibility = Visibility.Hidden;
            
            m_activeTool = newActiveToolType;
            foreach (var (toolType, drawTool) in m_drawTools)
            {
                drawTool.SetActive(toolType == newActiveToolType);
            }
        }

        private void SetColor(ColorPicker b)
        {
            if (ReferenceEquals(m_selectedColor, b)) return;
            if (b.Background is not SolidColorBrush solidColorBrush) return;

            MainInkCanvas.DefaultDrawingAttributes.Color = solidColorBrush.Color;

            var setColorAnimation = new ColorAnimation(solidColorBrush.Color, Duration3);
            brushPreview.Background.BeginAnimation(SolidColorBrush.ColorProperty, setColorAnimation);

            if (m_selectedColor != null)
            {
                m_selectedColor.IsActived = false;
            }
            
            b.IsActived = true;
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
            IOPopup.IsOpen = false;
            DrawToolPopup.IsOpen = false;
            EraserPopup.IsOpen = false;
            
            PaletteRotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(v ? -90 : 0, Duration4));
            Palette.BeginAnimation(MinWidthProperty, new DoubleAnimation(v ? 90 : 0, Duration7));
            m_displayOrientation = v;
        }
        
        private void SetTopMost(bool v)
        {
            PinButton.IsActived = v;
            Topmost = v;
        }

        private void StrokesChanged(object sender, StrokeCollectionChangedEventArgs e)
        {
            if (m_ignoreStrokesChange)
            {
                return;
            }
            
            m_fileManager.SetDirty();
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

        private void Clear()
        {
            MainInkCanvas.Strokes.Clear();
            m_historyManager.ClearUndoHistory();
            m_historyManager.ClearRedoHistory();
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
            SetTempUserMessage("Cleared");
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
            if (sender is not ColorPicker border) return;
            SetColor(border);
        }
        
        private void BrushSize(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta < 0)
            {
                m_brushIndex--;
            }
            else
            {
                m_brushIndex++; 

            }

            if (m_brushIndex > m_brushSizes.Length - 1)
            {
                m_brushIndex = 0;
            }
            else if (m_brushIndex < 0)
            {
                m_brushIndex = m_brushSizes.Length - 1;
            }

            SetBrushSize(m_brushSizes[m_brushIndex]);
        }

        private void BrushSwitchButton_Click(object sender, RoutedEventArgs e)
        {
            BrushSizePopup.IsOpen = !BrushSizePopup.IsOpen;
        }
        
        private void DrawToolCombo_OnClick(object sender, RoutedEventArgs e)
        {
            DrawToolPopup.IsOpen = !DrawToolPopup.IsOpen;
            EraserPopup.IsOpen = false;
            IOPopup.IsOpen = false;
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
            IOPopup.IsOpen = false;
            MonitorPopup.IsOpen = false;
        }

        private void IOButton_Click(object sender, RoutedEventArgs e)
        {
            IOPopup.IsOpen = !IOPopup.IsOpen;
            DrawToolPopup.IsOpen = false;
            EraserPopup.IsOpen = false;
            MonitorPopup.IsOpen = false;
        }

        private void MonitorButton_Click(object sender, RoutedEventArgs e)
        {
            MonitorPopup.IsOpen = !MonitorPopup.IsOpen;
            IOPopup.IsOpen = false;
            DrawToolPopup.IsOpen = false;
            EraserPopup.IsOpen = false;
        }
        
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            var result = ShowDialogMessage(
                "Are you sure you want to clear all your annotations? This can not be undone.",
                "Clear Canvas?",
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
            IOPopup.IsOpen = false;
            
            if (MainInkCanvas.Strokes.Count == 0)
            {
                SetTempUserMessage("Nothing to save");
                return;
            }
            m_fileManager.QuickSave();
        }
        
        private void SaveButton_RightClick(object sender, MouseButtonEventArgs e)
        {
            IOPopup.IsOpen = false;
            
            if (MainInkCanvas.Strokes.Count == 0)
            {
                SetTempUserMessage("Nothing to save");
                return;
            }
            m_fileManager.UserSave();
        }
        
        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            IOPopup.IsOpen = false;
            
            if (!PromptToSave())
            {
                return;
            }
            m_fileManager.UserLoad();
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            IOPopup.IsOpen = false;
            
            if (MainInkCanvas.Strokes.Count == 0)
            {
                SetTempUserMessage("Nothing to export");
                return;
            }

            m_fileManager.UserExport(false);
        }

        private void ExportButton_RightClick(object sender, MouseButtonEventArgs e)
        {
            IOPopup.IsOpen = false;
            
            if (MainInkCanvas.Strokes.Count == 0)
            {
                SetTempUserMessage("Nothing to export");
                return;
            }

            m_fileManager.UserExport(true);
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
            EraserTool_Icon.Visibility = Visibility.Visible;
            EraserTool_Icon.Fill = Brushes.White;
            EraserTool_PointIcon.Visibility = Visibility.Hidden;
        }
        
        private void SetActiveEraser_Pen(object sender, RoutedEventArgs e)
        {
            EraserPopup.IsOpen = false;
            m_eraserTool.SetEraser(EraserMode.ByPoint);
            EraserTool_Icon.Visibility = Visibility.Hidden;
            EraserTool_PointIcon.Visibility = Visibility.Visible;
        }
        
        private void PaletteGrip_MouseDown(object sender, MouseButtonEventArgs e)
        {
            m_dragManager.StartDrag();
            DrawToolPopup.IsOpen = false;
            EraserPopup.IsOpen = false;
            IOPopup.IsOpen = false;
            MonitorPopup.IsOpen = false;
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
                    if (m_brushIndex > m_brushSizes.Length - 1)
                    {
                        m_brushIndex = 0;
                    }
                    SetBrushSize(m_brushSizes[m_brushIndex]);
                    break;
                case Key.Subtract:
                    m_brushIndex--;
                    if (m_brushIndex < 0)
                    {
                        m_brushIndex = m_brushSizes.Length - 1;
                    }
                    SetBrushSize(m_brushSizes[m_brushIndex]);
                    break;
                case Key.F1:
                case Key.F2:
                case Key.F3:
                case Key.F4:
                case Key.F5:
                    m_brushIndex = e.Key - Key.F1;
                    SetBrushSize(m_brushSizes[m_brushIndex]);
                    break;
                case Key.D1:
                case Key.NumPad1:
                    SetColor(ColorPicker1);
                    break;
                case Key.D2:
                case Key.NumPad2:
                    SetColor(ColorPicker2);
                    break;
                case Key.D3:
                case Key.NumPad3:
                    SetColor(ColorPicker3);
                    break;
                case Key.D4:
                case Key.NumPad4:
                    SetColor(ColorPicker4);
                    break;
                case Key.D5:
                case Key.NumPad5:
                    SetColor(ColorPicker5);
                    break;
                case Key.D6:
                case Key.NumPad6:
                    SetColor(ColorPicker6);
                    break;
                case Key.D7:
                case Key.NumPad7:
                    SetColor(ColorPicker7);
                    break;
                case Key.D8:
                case Key.NumPad8:
                    SetColor(ColorPicker8);
                    break;
                case Key.D9:
                case Key.NumPad9:
                    SetColor(ColorPicker9);
                    break;
                case Key.D0:
                case Key.NumPad0:
                    SetColor(ColorPicker10);
                    break;
                case Key.OemMinus:
                case Key.Divide:
                    SetColor(ColorPicker11);
                    break;
                case Key.OemPlus:
                case Key.Multiply:
                    SetColor(ColorPicker12);
                    break;
            }
        }

        public void SetIgnoreStrokesChange(bool ignoreChanges)
        {
            m_ignoreStrokesChange = ignoreChanges;
        }

        public void SetUserMessage(string message)
        {
            m_staticInfo = message;
            if (!m_displayingInfo)
            {
                InfoBox.Text = m_staticInfo;
            }
        }

        public void SetTempUserMessage(string message)
        {
            InfoBox.Text = message;
            m_displayingInfo = true;
            
            Task.Run(async() =>
            {
                await Task.Delay(2000);
                await Dispatcher.InvokeAsync(() =>
                {
                    InfoBox.Text = m_staticInfo;
                    m_displayingInfo = false;
                });
            });
        }

        public MessageBoxResult ShowDialogMessage(string message, string caption, MessageBoxButton buttons)
        {
            return StyledMessageBox.Show(this, message, caption, buttons);
        }
        
        public async Task HideControlAsync()
        {
            await Dispatcher.InvokeAsync(() =>
            {
                Palette.Visibility = Visibility.Hidden;
            });
            await Task.Delay(100);
        }

        public async Task ShowControlAsync()
        {
            await Dispatcher.InvokeAsync(() =>
            {
                Palette.Visibility = Visibility.Visible;
            });
        }

        private void SetBrushSize_Click(object sender, RoutedEventArgs e)
        {
            BrushSizePopup.IsOpen = false;

            if (!(sender is Button button))
            {
                return;
            }

            if (!(button.Tag is string sizeString))
            {
                return;
            }

            if (!double.TryParse(sizeString, out var size))
            {
                return;
            }

            m_brushIndex = Array.IndexOf(m_brushSizes, size);
            SetBrushSize(size);
        }

        private void SetMonitorButton_Click(object sender, RoutedEventArgs e)
        {
            MonitorPopup.IsOpen = false;
            
            if (!(sender is Button button))
            {
                return;
            }

            if (!(button.DataContext is MonitorDescription monitor))
            {
                return;
            }
            
            // TODO: Set the active monitor based on the description
            ShowDialogMessage("Changing monitor is not currently supported.", "Can not change monitor",
                MessageBoxButton.OK);
        }
    }
}
