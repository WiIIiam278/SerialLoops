﻿using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Layout;
using ReactiveUI;
using SerialLoops.Lib.Script;
using SerialLoops.Utility;

namespace SerialLoops.Models
{
    public class ScriptCommandTreeItem : ITreeItem, IViewFor<ScriptItemCommand>
    {
        private Image _image = new()
        {
            Width = 24, Height = 24
        };
        private TextBlock _textBlock = new();
        StackPanel _panel = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 3,
            Margin = new(2),
        };

        public string Text { get; set; }
        public Avalonia.Svg.Svg Icon { get; set; } = null;
        public ObservableCollection<ITreeItem> Children { get; set; } = null;
        public bool IsExpanded { get; set; } = false;

        public ScriptCommandTreeItem(ScriptItemCommand command)
        {
            ViewModel = command;
            this.OneWayBind(ViewModel, vm => vm.Display, v => v._textBlock.Text);
            this.OneWayBind(ViewModel, vm => vm.Color, v => v._textBlock.Foreground);
            this.OneWayBind(ViewModel, vm => vm.Image, v => v._image.Source,
                vmToViewConverterOverride: new SKBitmapToAvaloniaConverter());
            _panel.Children.Add(_image);
            _panel.Children.Add(_textBlock);
        }

        public Control GetDisplay()
        {
            return _panel;
        }

        object IViewFor.ViewModel
        {
            get => ViewModel;
            set => ViewModel = (ScriptItemCommand)value;
        }

        public ScriptItemCommand ViewModel { get; set; }

    }
}