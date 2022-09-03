using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AntFu7.LiveDraw.Dialog
{
    public partial class StyledMessageBox : Window
    {
        private MessageBoxResult m_result = MessageBoxResult.None;

        private StyledMessageBox()
        {
            InitializeComponent();
            DragBar.PreviewMouseLeftButtonDown += DragBarOnMouseDown;
        }
        
        private void DragBarOnMouseDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        public static MessageBoxResult Show(Window owner, string message, string title, MessageBoxButton buttons)
        {
            var dialog = new StyledMessageBox
            {
                MessageTitleText =
                {
                    Text = title
                },
                MessageContentText =
                {
                    Text = message
                },
                Owner = owner
            };

            switch (buttons)
            {
                case MessageBoxButton.OK:
                    AddButton(dialog, MessageBoxResult.OK);
                    break;
                case MessageBoxButton.OKCancel:
                    AddButton(dialog, MessageBoxResult.OK);
                    AddButton(dialog, MessageBoxResult.Cancel);
                    break;
                case MessageBoxButton.YesNoCancel:
                    AddButton(dialog, MessageBoxResult.Yes);
                    AddButton(dialog, MessageBoxResult.No);
                    AddButton(dialog, MessageBoxResult.Cancel);
                    break;
                case MessageBoxButton.YesNo:
                    AddButton(dialog, MessageBoxResult.Yes);
                    AddButton(dialog, MessageBoxResult.No);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(buttons), buttons, null);
            }

            dialog.ShowDialog();
            return dialog.m_result;
        }

        private static void AddButton(StyledMessageBox dialog, MessageBoxResult buttonType)
        {
            var button = new ActivableButton
            {
                Tag = buttonType
            };
            button.Click += OnButtonClick;

            switch (buttonType)
            {
                case MessageBoxResult.None:
                    return;
                case MessageBoxResult.OK:
                    button.Content = "Ok";
                    break;
                case MessageBoxResult.Cancel:
                    button.Content = "Cancel";
                    break;
                case MessageBoxResult.Yes:
                    button.Content = "Yes";
                    break;
                case MessageBoxResult.No:
                    button.Content = "No";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(buttonType), buttonType, null);
            }
            
            dialog.ButtonHolder.Children.Add(button);
        }

        private static void OnButtonClick(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button))
            {
                return;
            }

            var result = (MessageBoxResult)button.Tag;
            var window = GetWindow(button) as StyledMessageBox;
            window?.CloseWithResult(result);
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = null;
            CloseWithResult(MessageBoxResult.Cancel);
        }

        private void CloseWithResult(MessageBoxResult result)
        {
            m_result = result;
            Close();
        }
    }
}