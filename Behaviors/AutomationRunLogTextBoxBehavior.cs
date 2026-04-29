#nullable enable

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace Gamepad_Mapping.Behaviors;

public static class AutomationRunLogTextBoxBehavior
{
    private sealed class Bridge
    {
        public ObservableCollection<string>? Source;
        public NotifyCollectionChangedEventHandler? Handler;
    }

    private static readonly ConditionalWeakTable<TextBox, Bridge> Bridges = new();

    public static readonly DependencyProperty LinesSourceProperty =
        DependencyProperty.RegisterAttached(
            "LinesSource",
            typeof(ObservableCollection<string>),
            typeof(AutomationRunLogTextBoxBehavior),
            new PropertyMetadata(null, OnLinesSourceChanged));

    public static void SetLinesSource(TextBox element, ObservableCollection<string>? value) =>
        element.SetValue(LinesSourceProperty, value);

    public static ObservableCollection<string>? GetLinesSource(TextBox element) =>
        (ObservableCollection<string>?)element.GetValue(LinesSourceProperty);

    private static void OnLinesSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox textBox)
            return;

        Detach(textBox);
        if (e.NewValue is ObservableCollection<string> lines)
            Attach(textBox, lines);
        else
            textBox.Clear();
    }

    private static void Attach(TextBox textBox, ObservableCollection<string> lines)
    {
        if (Bridges.TryGetValue(textBox, out _))
            Detach(textBox);

        var bridge = new Bridge { Source = lines };

        void OnCollectionChanged(object? _, NotifyCollectionChangedEventArgs args)
        {
            switch (args.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (args.NewItems?.Count == 1 &&
                        args.NewStartingIndex == lines.Count - 1 &&
                        args.NewItems[0] is string line)
                    {
                        if (textBox.Text.Length == 0)
                            textBox.AppendText(line);
                        else
                            textBox.AppendText(Environment.NewLine + line);
                        ScrollLogToCaret(textBox);
                        return;
                    }
                    break;

                case NotifyCollectionChangedAction.Reset:
                    textBox.Clear();
                    if (lines.Count > 0)
                        FullResync(textBox, lines);
                    return;
            }

            FullResync(textBox, lines);
        }

        bridge.Handler = OnCollectionChanged;
        Bridges.Add(textBox, bridge);
        lines.CollectionChanged += OnCollectionChanged;
        FullResync(textBox, lines);
    }

    private static void Detach(TextBox textBox)
    {
        if (!Bridges.TryGetValue(textBox, out var bridge))
            return;

        if (bridge.Handler is not null && bridge.Source is not null)
            bridge.Source.CollectionChanged -= bridge.Handler;

        Bridges.Remove(textBox);
    }

    private static void FullResync(TextBox textBox, ObservableCollection<string> lines)
    {
        textBox.Text = lines.Count == 0 ? "" : string.Join(Environment.NewLine, lines);
        ScrollLogToCaret(textBox);
    }

    private static void ScrollLogToCaret(TextBox textBox)
    {
        textBox.CaretIndex = textBox.Text.Length;
        textBox.SelectionLength = 0;
    }
}
