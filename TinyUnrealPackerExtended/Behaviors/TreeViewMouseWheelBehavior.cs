using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows;

namespace TinyUnrealPackerExtended.Behaviors
{
    public static class TreeViewMouseWheelBehavior
    {
        public static readonly DependencyProperty EnableMouseWheelScrollingProperty =
            DependencyProperty.RegisterAttached(
                "EnableMouseWheelScrolling",
                typeof(bool),
                typeof(TreeViewMouseWheelBehavior),
                new UIPropertyMetadata(false, OnEnableMouseWheelScrollingChanged));

        public static void SetEnableMouseWheelScrolling(DependencyObject element, bool value) =>
            element.SetValue(EnableMouseWheelScrollingProperty, value);

        public static bool GetEnableMouseWheelScrolling(DependencyObject element) =>
            (bool)element.GetValue(EnableMouseWheelScrollingProperty);

        private static void OnEnableMouseWheelScrollingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UIElement ui)
            {
                if ((bool)e.NewValue)
                    ui.PreviewMouseWheel += Ui_PreviewMouseWheel;
                else
                    ui.PreviewMouseWheel -= Ui_PreviewMouseWheel;
            }
        }

        private static void Ui_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Ищем ближайший ScrollViewer
            var src = sender as DependencyObject;
            var scroll = FindScrollViewer(src);
            if (scroll != null)
            {
                scroll.ScrollToVerticalOffset(scroll.VerticalOffset - e.Delta);
                e.Handled = true;
            }
        }

        private static ScrollViewer FindScrollViewer(DependencyObject start)
        {
            // Сначала пробуем внутренний ScrollViewer у TreeView
            if (start is TreeView tv)
            {
                return FindVisualChild<ScrollViewer>(tv);
            }
            // Иначе ищем в родителях
            return FindVisualParent<ScrollViewer>(start);
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T correctlyTyped) return correctlyTyped;
                var desc = FindVisualChild<T>(child);
                if (desc != null) return desc;
            }
            return null;
        }

        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T correctlyTyped) return correctlyTyped;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }
    }
}
