using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows;

namespace TinyUnrealPackerExtended.Helpers
{
    public class FullscreenHelper
    {
        private readonly Window _window;
        private Panel _container;
        private bool _areResizeGripsAttached = false;

        private Rect _preFullscreenBounds;
        private WindowState _previousState;
        private bool _isFullscreen = false;
        private bool _wasFullscreenBeforeMinimized = false;
        private Rect _preMaximizedBounds;
        private bool _isMaximizedIntercepted = false;

        private const int WM_NCLBUTTONDOWN = 0x00A1;


        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        public FullscreenHelper(Window window)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _window.Loaded += OnWindowLoaded;
            _window.StateChanged += OnWindowStateChanged;
        }


        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            EnsureContainer();
            AttachResizeGrips();
        }


        private void EnsureContainer()
        {
            if (_window.Content is Panel panel)
            {
                _container = panel;
            }
            else
            {
                var grid = new Grid();
                if (_window.Content is UIElement originalContent)
                {
                    _window.Content = null;
                    grid.Children.Add(originalContent);
                }
                _window.Content = grid;
                _container = grid;
            }
        }


        private void AttachResizeGrips()
        {
            if (_window.ResizeMode == ResizeMode.NoResize)
                return;

            if (_areResizeGripsAttached || _container == null)
                return;

            // Границы
            CreateResizeGrip("ResizeLeft", HorizontalAlignment.Left, VerticalAlignment.Stretch,
                width: 5, height: double.NaN, cursor: Cursors.SizeWE, directionTag: 10);
            CreateResizeGrip("ResizeRight", HorizontalAlignment.Right, VerticalAlignment.Stretch,
                width: 5, height: double.NaN, cursor: Cursors.SizeWE, directionTag: 11);
            CreateResizeGrip("ResizeTop", HorizontalAlignment.Stretch, VerticalAlignment.Top,
                width: double.NaN, height: 5, cursor: Cursors.SizeNS, directionTag: 12);
            CreateResizeGrip("ResizeBottom", HorizontalAlignment.Stretch, VerticalAlignment.Bottom,
                width: double.NaN, height: 5, cursor: Cursors.SizeNS, directionTag: 15);

            // Углы
            CreateResizeGrip("ResizeTopLeft", HorizontalAlignment.Left, VerticalAlignment.Top,
                width: 10, height: 10, cursor: Cursors.SizeNWSE, directionTag: 13);
            CreateResizeGrip("ResizeTopRight", HorizontalAlignment.Right, VerticalAlignment.Top,
                width: 10, height: 10, cursor: Cursors.SizeNESW, directionTag: 14);
            CreateResizeGrip("ResizeBottomLeft", HorizontalAlignment.Left, VerticalAlignment.Bottom,
                width: 10, height: 10, cursor: Cursors.SizeNESW, directionTag: 16);
            CreateResizeGrip("ResizeBottomRight", HorizontalAlignment.Right, VerticalAlignment.Bottom,
                width: 10, height: 10, cursor: Cursors.SizeNWSE, directionTag: 17);

            _areResizeGripsAttached = true;
        }

        private void CreateResizeGrip(string name, HorizontalAlignment horizontalAlignment, VerticalAlignment verticalAlignment,
            double width, double height, Cursor cursor, int directionTag)
        {
            var border = new Border
            {
                Name = name,
                Background = Brushes.Transparent,
                HorizontalAlignment = horizontalAlignment,
                VerticalAlignment = verticalAlignment,
                Cursor = cursor,
                Tag = directionTag
            };

            if (!double.IsNaN(width))
                border.Width = width;
            if (!double.IsNaN(height))
                border.Height = height;

            border.MouseLeftButtonDown += Resize_MouseLeftButtonDown;

            _container.Children.Add(border);
        }

        private void Resize_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isFullscreen)
                return; // не разрешаем изменение размера, если окно в fullscreen

            if (sender is FrameworkElement element && element.Tag != null)
            {
                if (int.TryParse(element.Tag.ToString(), out int direction))
                {
                    ReleaseCapture();
                    IntPtr hwnd = new WindowInteropHelper(_window).Handle;
                    SendMessage(hwnd, WM_NCLBUTTONDOWN, (IntPtr)direction, IntPtr.Zero);
                }
            }
        }

        private void OnWindowStateChanged(object sender, EventArgs e)
        {
            switch (_window.WindowState)
            {
                case WindowState.Maximized when !_isFullscreen:
                    _preMaximizedBounds = new Rect(_window.Left, _window.Top, _window.Width, _window.Height);
                    _isMaximizedIntercepted = true;
                    _window.WindowState = WindowState.Normal;
                    EnterFullscreen();
                    break;

                case WindowState.Minimized:
                    _wasFullscreenBeforeMinimized = _isFullscreen;
                    break;

                case WindowState.Normal:
                    if (_wasFullscreenBeforeMinimized)
                    {
                        EnterFullscreen();
                        _wasFullscreenBeforeMinimized = false;
                    }
                    else if (_isFullscreen)
                    {
                        ExitFullscreen();
                    }
                    RestoreOpacity();
                    break;
            }
        }

        private void EnterFullscreen()
        {
            if (!_wasFullscreenBeforeMinimized)
            {
                _preFullscreenBounds = new Rect(_window.Left, _window.Top, _window.Width, _window.Height);
            }
            _previousState = _window.WindowState;

            Rect workArea = SystemParameters.WorkArea;
            _window.Left = workArea.Left;
            _window.Top = workArea.Top;
            _window.Width = workArea.Width;
            _window.Height = workArea.Height;

            _isFullscreen = true;
            SetResizeGripsVisibility(Visibility.Collapsed);
        }

        private void ExitFullscreen()
        {
            _window.Left = _preFullscreenBounds.Left;
            _window.Top = _preFullscreenBounds.Top;
            _window.Width = _preFullscreenBounds.Width;
            _window.Height = _preFullscreenBounds.Height;

            _window.Dispatcher.Invoke(() =>
            {
                _window.WindowState = _previousState;
            });

            _isFullscreen = false;
            SetResizeGripsVisibility(Visibility.Visible);

            if (_isMaximizedIntercepted)
            {
                _window.Left = _preMaximizedBounds.Left;
                _window.Top = _preMaximizedBounds.Top;
                _window.Width = _preMaximizedBounds.Width;
                _window.Height = _preMaximizedBounds.Height;
                _isMaximizedIntercepted = false;
            }
        }

        private void SetResizeGripsVisibility(Visibility visibility)
        {
            if (_container == null)
                return;

            foreach (var child in _container.Children)
            {
                if (child is Border border && border.Tag != null)
                {
                    border.Visibility = visibility;
                }
            }
        }

        public void ToggleFullscreen()
        {
            if (_isFullscreen)
                ExitFullscreen();
            else
                EnterFullscreen();
        }

        public void HandleDragMove()
        {
            if (!_isFullscreen && _window.WindowState != WindowState.Maximized)
            {
                _window.DragMove();
            }
        }

        public void ToggleMinimizeScreen()
        {
            if (_window.WindowState == WindowState.Normal || _isFullscreen)
            {
                AnimateMinimize();
            }
            else if (_window.WindowState == WindowState.Minimized)
            {
                _window.WindowState = WindowState.Normal;
                RestoreOpacity();
            }
        }

        private void AnimateMinimize()
        {
            var animation = new System.Windows.Media.Animation.DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromSeconds(0.1)
            };

            animation.Completed += (s, e) => _window.WindowState = WindowState.Minimized;
            _window.BeginAnimation(UIElement.OpacityProperty, animation);
        }

        private void RestoreOpacity()
        {
            var animation = new System.Windows.Media.Animation.DoubleAnimation
            {
                To = 1,
                Duration = TimeSpan.FromSeconds(0.1)
            };

            _window.BeginAnimation(UIElement.OpacityProperty, animation);
        }

        public void CloseWindow()
        {
            _window?.Close();
        }
    }
}
