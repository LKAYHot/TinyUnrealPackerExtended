using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MahApps.Metro.IconPacks;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows;
using TinyUnrealPackerExtended.Controls;
using System.Windows.Controls;
using System.Windows.Threading;

namespace TinyUnrealPackerExtended.Services
{
    public class GrowlService
    {
        private readonly Canvas _growlContainer;

        public GrowlService(Canvas growlContainer)
        {
            _growlContainer = growlContainer;
        }

        public void Show(string title, string message, PackIconMaterialKind iconKind, Brush iconColor, int duration = 3)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var growl = CreateGrowl(title, message, iconKind, iconColor);
                SetupGrowlAnimation(growl, duration);
                _growlContainer.Children.Add(growl);
            });
        }

        public void Show(string title, string message, PackIconMaterialKind iconKind, string hexColor, int duration = 3)
        {
            Show(title, message, iconKind, ConvertHexToBrush(hexColor), duration);
        }

        public void ShowInfo(string message, string title = "Info", int duration = 3)
            => Show(title, message, PackIconMaterialKind.InformationBoxOutline, "#3870f3", duration);

        public void ShowWarning(string message, string title = "Warning", int duration = 3)
            => Show(title, message, PackIconMaterialKind.AlertOutline, "#e46638", duration);

        public void ShowError(string message, string title = "Error", int duration = 3)
            => Show(title, message, PackIconMaterialKind.CloseCircleOutline, "#e43838", duration);

        public void ShowSuccess(string message, string title = "Success", int duration = 3)
            => Show(title, message, PackIconMaterialKind.CheckCircleOutline, "#46e469", duration);

        private CustomGrowl CreateGrowl(string title, string message, PackIconMaterialKind iconKind, Brush iconColor)
        {
            var growl = new CustomGrowl
            {
                Title = title,
                Message = message,
                IconKind = iconKind,
                IconColor = iconColor,
                RenderTransform = new TranslateTransform()
            };

            Canvas.SetTop(growl, _growlContainer.Children.Count * 80);
            Canvas.SetRight(growl, 10);

            growl.CloseButton.Click += (sender, args) => RemoveGrowlWithAnimation(growl);

            return growl;
        }

        private void SetupGrowlAnimation(CustomGrowl growl, int duration)
        {
            var transform = (TranslateTransform)growl.RenderTransform;
            transform.X = 400;

            growl.Loaded += (sender, args) =>
            {
                var slideInAnimation = CreateAnimation(400, 0, 0.5, 0.2, EasingMode.EaseOut);
                transform.BeginAnimation(TranslateTransform.XProperty, slideInAnimation);

                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(duration) };
                timer.Tick += (o, eventArgs) =>
                {
                    timer.Stop();
                    RemoveGrowlWithAnimation(growl);
                };
                timer.Start();
            };
        }

        private void RemoveGrowlWithAnimation(CustomGrowl growl)
        {
            var transform = (TranslateTransform)growl.RenderTransform;
            var slideOutAnimation = CreateAnimation(0, 400, 0.5, 0, EasingMode.EaseIn);

            slideOutAnimation.Completed += (s, e) =>
            {
                _growlContainer.Children.Remove(growl);
                RaiseRemainingGrowls();
            };

            transform.BeginAnimation(TranslateTransform.XProperty, slideOutAnimation);
        }

        private DoubleAnimation CreateAnimation(double from, double to, double duration, double beginTime, EasingMode easingMode)
        {
            return new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = TimeSpan.FromSeconds(duration),
                BeginTime = TimeSpan.FromSeconds(beginTime),
                EasingFunction = new CubicEase { EasingMode = easingMode }
            };
        }

        private void RaiseRemainingGrowls()
        {
            for (int i = 0; i < _growlContainer.Children.Count; i++)
            {
                if (_growlContainer.Children[i] is FrameworkElement child)
                {
                    var animation = CreateAnimation(Canvas.GetTop(child), i * 80, 0.3, 0, EasingMode.EaseOut);
                    child.BeginAnimation(Canvas.TopProperty, animation);
                }
            }
        }

        private Brush ConvertHexToBrush(string hexColor)
        {
            if (string.IsNullOrWhiteSpace(hexColor))
                throw new ArgumentException("Hex color string cannot be null or empty.");

            try
            {
                return (Brush)new BrushConverter().ConvertFromString(hexColor);
            }
            catch
            {
                throw new FormatException($"Invalid hex color string: {hexColor}");
            }
        }
    }
}
