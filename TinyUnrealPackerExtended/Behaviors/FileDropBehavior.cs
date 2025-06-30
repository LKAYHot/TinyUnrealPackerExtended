using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors;
using System.Windows;

namespace TinyUnrealPackerExtended.Behaviors
{
    public class FileDropBehavior : Behavior<UIElement>
    {
        public static readonly DependencyProperty FileDropCommandProperty =
            DependencyProperty.Register(
                nameof(FileDropCommand),
                typeof(ICommand),
                typeof(FileDropBehavior));

        public ICommand FileDropCommand
        {
            get => (ICommand)GetValue(FileDropCommandProperty);
            set => SetValue(FileDropCommandProperty, value);
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.AllowDrop = true;
            AssociatedObject.DragOver += OnDragOver;
            AssociatedObject.Drop += OnDrop;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.DragOver -= OnDragOver;
            AssociatedObject.Drop -= OnDrop;
            base.OnDetaching();
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                        ? DragDropEffects.Copy
                        : DragDropEffects.None;
            e.Handled = true;
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (FileDropCommand != null && FileDropCommand.CanExecute(files))
                FileDropCommand.Execute(files);

        }
    }
}
