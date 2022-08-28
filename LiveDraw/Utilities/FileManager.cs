using AntFu7.LiveDraw.Interface;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace AntFu7.LiveDraw.Utilities
{
    public class FileManager : IFileManager
    {
        private static readonly Duration LoadDuration = (Duration)Application.Current.Resources["Duration3"];

        private readonly IUserFeedback m_userFeedback;
        private readonly IHideableControl m_hideableControl;
        private readonly IStrokeHistoryManager m_historyManager;
        private readonly InkCanvas m_inkCanvas;
        private readonly string m_defaultSaveDirectory;

        private bool m_saved;
        private StrokeCollection m_preLoadStrokes;

        public FileManager(IUserFeedback userFeedback, IHideableControl hideableControl, IStrokeHistoryManager historyManager, InkCanvas inkCanvas)
        {
            m_userFeedback = userFeedback;
            m_hideableControl = hideableControl;
            m_historyManager = historyManager;
            m_inkCanvas = inkCanvas;
            m_defaultSaveDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Save");
            m_saved = true;
        }

        public bool IsUnsaved()
        {
            return m_inkCanvas.Strokes.Count != 0 && !m_saved;
        }

        public void QuickSave()
        {
            var filename = "QuickSave_";
            SaveToFile("Save\\" + filename + GenerateDatedFileName(".fdw"));
        }

        public void UserSave()
        {
            var filePath = SaveDialog(GenerateDatedFileName(".fdw"));
            SaveToFile(filePath);
        }

        public void UserExport(bool includeBackground)
        {
            try
            {
                var filePath = SaveDialog($"ImageExport_{GenerateDatedFileName(".png")}", 
                    ".png",
                    "Portable Network Graphics (*png)|*png");

                if (string.IsNullOrWhiteSpace(filePath))
                {
                    return;
                }

                if (!includeBackground)
                {
                    var rtb = new RenderTargetBitmap((int)m_inkCanvas.ActualWidth, (int)m_inkCanvas.ActualHeight, 96d,
                        96d, PixelFormats.Pbgra32);
                    rtb.Render(m_inkCanvas);

                    using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(rtb));
                    encoder.Save(fileStream);
                }
                else
                {
                    var dispatcher = Dispatcher.CurrentDispatcher;
                    Task.Run(async () =>
                    {
                        await m_hideableControl.HideControlAsync();

                        await dispatcher.InvokeAsync(() =>
                        {
                            var windowHandle = Graphics.FromHwnd(IntPtr.Zero);
                            var w = (int)(SystemParameters.PrimaryScreenWidth * windowHandle.DpiX / 96.0);
                            var h = (int)(SystemParameters.PrimaryScreenHeight * windowHandle.DpiY / 96.0);

                            var image = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                            using (var graphics = Graphics.FromImage(image))
                            {
                                graphics.CopyFromScreen(0, 0, 0, 0, new System.Drawing.Size(w, h), CopyPixelOperation.SourceCopy);
                            }

                            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                            {
                                image.Save(fileStream, ImageFormat.Png);
                            }
                        });

                        await m_hideableControl.ShowControlAsync();
                    });
                }

                m_userFeedback.SetTempUserMessage("Image Exported");
            }
            catch (Exception ex)
            {
                m_userFeedback.ShowDialogMessage(ex.ToString(), "Failed to export", MessageBoxButton.OK);
                m_userFeedback.SetTempUserMessage("Export failed");
            }
        }

        public void ClearDirty()
        {
            m_saved = true;
        }

        public void SetDirty()
        {
            m_saved = false;
        }

        public void UserLoad()
        {
            var filePath = OpenDialog();
            if (filePath == null)
            {
                return;
            }

            var strokeCollection = LoadFileFile(filePath);
            AnimatedReload(strokeCollection);
        }

        private void SaveToFile(string absoluteFilePath)
        {
            if (string.IsNullOrWhiteSpace(absoluteFilePath))
            {
                return;
            }

            try
            {
                EnsureFileDirectory(absoluteFilePath);

                using var fileStream = new FileStream(absoluteFilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                m_inkCanvas.Strokes.Save(fileStream);
                m_saved = true;
                m_userFeedback.SetTempUserMessage("Ink saved");
            }
            catch (Exception ex)
            {
                m_userFeedback.ShowDialogMessage(ex.ToString(), $"Failed to save to file '{absoluteFilePath}'", MessageBoxButton.OK);
                m_userFeedback.SetTempUserMessage("Fail to save");
            }
        }

        private string SaveDialog(string initFileName, string fileExt = ".fdw", string filter = "Free Draw Save (*.fdw)|*fdw")
        {
            EnsureDirectory(m_defaultSaveDirectory);

            var dialog = new Microsoft.Win32.SaveFileDialog()
            {
                DefaultExt = fileExt,
                Filter = filter,
                FileName = initFileName,
                InitialDirectory = m_defaultSaveDirectory
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        private StrokeCollection LoadFileFile(string absoluteFilePath)
        {
            try
            {
                using var fileStream = new FileStream(absoluteFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                return new StrokeCollection(fileStream);
            }
            catch (Exception ex)
            {
                m_userFeedback.ShowDialogMessage(ex.ToString(), $"Failed to load from file '{absoluteFilePath}'", MessageBoxButton.OK);
                m_userFeedback.SetTempUserMessage("Fail to load");
            }
            return new StrokeCollection();
        }

        private string OpenDialog(string fileExt = ".fdw", string filter = "Free Draw Save (*.fdw)|*fdw")
        {
            var dialog = new Microsoft.Win32.OpenFileDialog()
            {
                DefaultExt = fileExt,
                Filter = filter,
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        private string GenerateDatedFileName(string fileExtension)
        {
            return $"{DateTime.Now:yyyyMMdd-HHmmss}{fileExtension}";
        }

        private void EnsureFileDirectory(string absoluteFilePath)
        {
            var directory = Path.GetDirectoryName(absoluteFilePath);
            if (directory == null)
            {
                throw new Exception($"Failed to ensure the directory for '{absoluteFilePath}' exists.");
            }
            EnsureDirectory(directory);
        }

        private void EnsureDirectory(string absoluteDirectoryPath)
        {
            if (!Directory.Exists(absoluteDirectoryPath))
            {
                Directory.CreateDirectory(absoluteDirectoryPath);
            }
        }

        private void AnimatedReload(StrokeCollection sc)
        {
            m_preLoadStrokes = sc;
            var ani = new DoubleAnimation(0, LoadDuration);
            ani.Completed += LoadAniCompleted;
            m_inkCanvas.BeginAnimation(UIElement.OpacityProperty, ani);
        }

        private void LoadAniCompleted(object sender, EventArgs e)
        {
            if (m_preLoadStrokes == null)
            {
                return;
            }

            m_inkCanvas.Strokes = m_preLoadStrokes;
            m_userFeedback.SetTempUserMessage("Ink loaded");

            ClearDirty();
            m_historyManager.ClearRedoHistory();
            m_historyManager.ClearUndoHistory();

            m_inkCanvas.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, LoadDuration));
        }
    }
}
