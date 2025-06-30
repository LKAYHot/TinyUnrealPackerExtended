using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;
using TinyUnrealPackerExtended.ViewModels;
using Microsoft.Xaml.Behaviors;

namespace TinyUnrealPackerExtended.Behaviors
{
    public class BreadcrumbOverflowBehavior : Behavior<ItemsControl>
    {
        protected override void OnAttached()
        {
            AssociatedObject.SizeChanged += OnSizeChanged;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.SizeChanged -= OnSizeChanged;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (AssociatedObject.DataContext is not MainWindowViewModel vm)
                return;

            var gen = AssociatedObject.ItemContainerGenerator;
            var elements = gen.Items
                .Select(item => gen.ContainerFromItem(item) as FrameworkElement)
                .Where(fe => fe != null)
                .Cast<FrameworkElement>()
                .ToList();

            // измеряем каждую кнопку вне ограничений
            var widths = elements
                .Select(el => { el.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); return el.DesiredSize.Width; })
                .ToList();

            double available = AssociatedObject.ActualWidth;
            double total = widths.Sum();
            int maxVisible = widths.Count;

            if (total > available && widths.Count > 2)
            {
                // всегда показываем первый и последний
                double used = widths[0] + widths[^1];
                maxVisible = 2;

                // пробегаем по средним
                for (int i = 1; i < widths.Count - 1; i++)
                {
                    if (used + widths[i] <= available)
                    {
                        used += widths[i];
                        maxVisible++;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            vm.FolderEditorVM.MaxVisible = maxVisible;
        }
    }
}
