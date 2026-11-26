#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace KamatekCrm.Diagnostics
{
    /// <summary>
    /// Yalnızca DEBUG modunda çalışan, UI Dispatcher üzerinde VisualTree taraması
    /// ve etkileşim (click/type/select/press_key) yürüten yardımcı sınıf.
    /// </summary>
    internal sealed class DebugUiAutomationHelper
    {
        private readonly Dictionary<string, WeakReference<DependencyObject>> _elementRefs =
            new(StringComparer.OrdinalIgnoreCase);

        public static Window? GetActiveTargetWindow()
        {
            var app = Application.Current;
            if (app == null)
            {
                return null;
            }

            var visibleWindows = app.Windows
                .OfType<Window>()
                .Where(w => w.IsVisible && !IsVisualStudioDiagnosticWindow(w))
                .ToList();
            if (visibleWindows.Count == 0)
            {
                return app.MainWindow;
            }

            var activeModal = visibleWindows.LastOrDefault(w => w != app.MainWindow && w.IsActive);
            if (activeModal != null)
            {
                return activeModal;
            }

            var topModal = visibleWindows.LastOrDefault(w => w != app.MainWindow);
            if (topModal != null)
            {
                return topModal;
            }

            return visibleWindows.FirstOrDefault(w => w.IsActive) ?? app.MainWindow ?? visibleWindows[0];
        }

        private static bool IsVisualStudioDiagnosticWindow(Window window)
        {
            string fullName = window.GetType().FullName ?? string.Empty;
            return fullName.StartsWith("Microsoft.VisualStudio.", StringComparison.Ordinal) ||
                   string.Equals(window.GetType().Name, "AdornerWindow", StringComparison.Ordinal);
        }

        public static string DescribeWindow(Window? window)
        {
            if (window == null)
            {
                return "None";
            }

            return string.IsNullOrWhiteSpace(window.Title)
                ? window.GetType().Name
                : $"{window.GetType().Name} ({window.Title})";
        }

        public List<DebugUiNodeDto> InspectUiTree(int maxNodes)
        {
            int limit = Math.Clamp(maxNodes <= 0 ? 120 : maxNodes, 1, 500);
            _elementRefs.Clear();

            var results = new List<DebugUiNodeDto>(Math.Min(limit, 128));
            var rootWindow = GetActiveTargetWindow();
            if (rootWindow == null)
            {
                return results;
            }

            var stack = new Stack<(DependencyObject Element, bool InsideScrollBar)>();
            stack.Push((rootWindow, false));

            while (stack.Count > 0 && results.Count < limit)
            {
                var (current, insideScrollBar) = stack.Pop();

                if (current is UIElement uie && !uie.IsVisible)
                {
                    continue;
                }

                bool nowInsideScrollBar = insideScrollBar || current is ScrollBar;
                bool skipChildren = false;

                if (!nowInsideScrollBar && TryBuildNode(current, results.Count + 1, out var dto, out skipChildren))
                {
                    _elementRefs[dto.RefId] = new WeakReference<DependencyObject>(current);
                    results.Add(dto);
                    if (results.Count >= limit)
                    {
                        break;
                    }
                }

                if (skipChildren)
                {
                    continue;
                }

                int childCount = VisualTreeHelper.GetChildrenCount(current);
                for (int i = childCount - 1; i >= 0; i--)
                {
                    var child = VisualTreeHelper.GetChild(current, i);
                    if (child != null)
                    {
                        stack.Push((child, nowInsideScrollBar));
                    }
                }
            }

            return results;
        }

        public DebugActionResult ExecuteAction(string? target, string? action, string? value)
        {
            string normalizedAction = (action ?? string.Empty).Trim().ToLowerInvariant();
            if (normalizedAction == "press_key")
            {
                string keySpec = !string.IsNullOrWhiteSpace(value) ? value! : (target ?? string.Empty);
                return ExecutePressKey(keySpec);
            }

            if (string.IsNullOrWhiteSpace(target))
            {
                return new DebugActionResult(false, normalizedAction, string.Empty, null, "Hedef (target) belirtilmedi.");
            }

            var resolved = ResolveTarget(target.Trim());
            if (resolved == null)
            {
                return new DebugActionResult(false, normalizedAction, target, null, $"Görünür öğe bulunamadı: '{target}'.");
            }

            string resolvedType = resolved.GetType().Name;

            return normalizedAction switch
            {
                "click" => ExecuteClick(resolved, target, resolvedType),
                "type" => ExecuteType(resolved, target, resolvedType, value ?? string.Empty),
                "select" => ExecuteSelect(resolved, target, resolvedType, value ?? string.Empty),
                _ => new DebugActionResult(false, normalizedAction, target, resolvedType, $"Desteklenmeyen aksiyon: '{action}'.")
            };
        }

        private static bool TryBuildNode(
            DependencyObject element,
            int nextIndex,
            out DebugUiNodeDto dto,
            out bool skipChildren)
        {
            dto = default!;
            skipChildren = false;

            string refId = $"e{nextIndex}";
            string automationId = GetAutomationIdOrName(element);
            bool isEnabled = (element as UIElement)?.IsEnabled ?? true;

            switch (element)
            {
                case CheckBox cb:
                    skipChildren = true;
                    dto = new DebugUiNodeDto(refId, automationId, "CheckBox", ExtractElementLabel(cb), isEnabled, cb.IsChecked, cb.IsChecked?.ToString());
                    return true;

                case RadioButton rb:
                    skipChildren = true;
                    dto = new DebugUiNodeDto(refId, automationId, "RadioButton", ExtractElementLabel(rb), isEnabled, rb.IsChecked, rb.IsChecked?.ToString());
                    return true;

                case ButtonBase btn when btn is not RepeatButton:
                    skipChildren = true;
                    bool? toggleState = (btn as ToggleButton)?.IsChecked;
                    string btnType = btn is ToggleButton ? "ToggleButton" : "Button";
                    dto = new DebugUiNodeDto(refId, automationId, btnType, ExtractElementLabel(btn), isEnabled, toggleState, null);
                    return true;

                case TextBox tb:
                    skipChildren = true;
                    dto = new DebugUiNodeDto(refId, automationId, "TextBox", ExtractElementLabel(tb), isEnabled, null, tb.Text);
                    return true;

                case PasswordBox pb:
                    skipChildren = true;
                    string masked = pb.Password.Length > 0 ? "***" : string.Empty;
                    dto = new DebugUiNodeDto(refId, automationId, "PasswordBox", ExtractElementLabel(pb), isEnabled, null, masked);
                    return true;

                case ComboBox combo:
                    skipChildren = true;
                    dto = new DebugUiNodeDto(refId, automationId, "ComboBox", ExtractElementLabel(combo), isEnabled, null, GetComboSelectedValue(combo));
                    return true;

                case TabItem tabItem:
                    skipChildren = true;
                    string tabText = ExtractObjectText(tabItem.Header);
                    if (string.IsNullOrWhiteSpace(tabText))
                    {
                        tabText = ExtractElementLabel(tabItem);
                    }
                    dto = new DebugUiNodeDto(refId, automationId, "TabItem", tabText, isEnabled, tabItem.IsSelected, tabItem.IsSelected ? "selected" : "unselected");
                    return true;

                case DataGrid dg:
                    skipChildren = false;
                    dto = new DebugUiNodeDto(refId, automationId, "DataGrid", ExtractElementLabel(dg), isEnabled, null, $"rows:{dg.Items.Count},selected:{dg.SelectedIndex}");
                    return true;

                case TextBlock textBlock when IsBannerTextBlock(textBlock, automationId):
                    skipChildren = true;
                    string bannerText = textBlock.Text.Trim();
                    dto = new DebugUiNodeDto(refId, automationId, "TextBlock", bannerText, isEnabled, null, bannerText);
                    return true;

                default:
                    return false;
            }
        }

        private static bool IsBannerTextBlock(TextBlock tb, string automationId)
        {
            if (string.IsNullOrWhiteSpace(tb.Text))
            {
                return false;
            }

            string text = tb.Text.Trim();
            if (text.Length > 300)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(automationId) &&
                (automationId.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
                 automationId.Contains("Warn", StringComparison.OrdinalIgnoreCase) ||
                 automationId.Contains("Alert", StringComparison.OrdinalIgnoreCase) ||
                 automationId.Contains("Banner", StringComparison.OrdinalIgnoreCase) ||
                 automationId.Contains("Status", StringComparison.OrdinalIgnoreCase) ||
                 automationId.Contains("Validation", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (tb.Foreground is SolidColorBrush scb &&
                scb.Color.A > 120 &&
                scb.Color.R >= 180 &&
                scb.Color.G < 140 &&
                scb.Color.B < 100)
            {
                return true;
            }

            return text.Contains("hata", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("uyarı", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("başarısız", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("zorunlu", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("geçersiz", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("bulunamadı", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("yetki", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("⚠", StringComparison.Ordinal) ||
                   text.Contains("❌", StringComparison.Ordinal);
        }

        private DependencyObject? ResolveTarget(string target)
        {
            if (_elementRefs.TryGetValue(target, out var weakRef) &&
                weakRef.TryGetTarget(out var cached) &&
                cached is UIElement cachedUi &&
                cachedUi.IsVisible)
            {
                return cached;
            }

            var window = GetActiveTargetWindow();
            if (window == null)
            {
                return null;
            }

            DependencyObject? partialTextMatch = null;
            var stack = new Stack<DependencyObject>();
            stack.Push(window);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (current is UIElement uie && !uie.IsVisible)
                {
                    continue;
                }

                if (IsActionableElement(current))
                {
                    string autoId = AutomationProperties.GetAutomationId(current);
                    string feName = (current as FrameworkElement)?.Name ?? string.Empty;
                    if (string.Equals(autoId, target, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(feName, target, StringComparison.OrdinalIgnoreCase))
                    {
                        return current;
                    }

                    string label = current is TabItem ti
                        ? ExtractObjectText(ti.Header)
                        : ExtractElementLabel(current);

                    if (string.Equals(label, target, StringComparison.OrdinalIgnoreCase))
                    {
                        return current;
                    }

                    if (partialTextMatch == null &&
                        !string.IsNullOrWhiteSpace(label) &&
                        label.Contains(target, StringComparison.OrdinalIgnoreCase))
                    {
                        partialTextMatch = current;
                    }
                }

                int count = VisualTreeHelper.GetChildrenCount(current);
                for (int i = count - 1; i >= 0; i--)
                {
                    stack.Push(VisualTreeHelper.GetChild(current, i));
                }
            }

            return partialTextMatch;
        }

        private static bool IsActionableElement(DependencyObject d) =>
            d is ButtonBase or TextBox or PasswordBox or ComboBox or TabItem or DataGrid;

        private static DebugActionResult ExecuteClick(DependencyObject element, string target, string resolvedType)
        {
            if (element is UIElement uie && !uie.IsEnabled)
            {
                return new DebugActionResult(false, "click", target, resolvedType, "Öğe devre dışı (IsEnabled=false).");
            }

            switch (element)
            {
                case TabItem tabItem:
                    tabItem.IsSelected = true;
                    tabItem.Focus();
                    return new DebugActionResult(true, "click", target, resolvedType, "Sekme seçildi.");

                case CheckBox checkBox:
                    checkBox.Focus();
                    checkBox.IsChecked = !(checkBox.IsChecked ?? false);
                    ExecuteButtonCommandIfCan(checkBox);
                    return new DebugActionResult(true, "click", target, resolvedType, $"CheckBox={checkBox.IsChecked}");

                case RadioButton radioButton:
                    radioButton.Focus();
                    radioButton.IsChecked = true;
                    ExecuteButtonCommandIfCan(radioButton);
                    return new DebugActionResult(true, "click", target, resolvedType, "RadioButton seçildi.");

                case Button button:
                    button.Focus();
                    IInvokeProvider invokeProvider = new ButtonAutomationPeer(button);
                    invokeProvider.Invoke();
                    return new DebugActionResult(true, "click", target, resolvedType, "Button tetiklendi.");

                case ButtonBase buttonBase:
                    buttonBase.Focus();
                    if (buttonBase is ToggleButton toggle)
                    {
                        toggle.IsChecked = !(toggle.IsChecked ?? false);
                    }
                    if (!ExecuteButtonCommandIfCan(buttonBase))
                    {
                        buttonBase.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                    }
                    return new DebugActionResult(true, "click", target, resolvedType, "ButtonBase tetiklendi.");

                default:
                    return new DebugActionResult(false, "click", target, resolvedType, "Bu öğe türü tıklamayı desteklemiyor.");
            }
        }

        private static bool ExecuteButtonCommandIfCan(ButtonBase btn)
        {
            if (btn.Command != null && btn.Command.CanExecute(btn.CommandParameter))
            {
                btn.Command.Execute(btn.CommandParameter);
                return true;
            }

            return false;
        }

        private static DebugActionResult ExecuteType(DependencyObject element, string target, string resolvedType, string value)
        {
            switch (element)
            {
                case TextBox tb:
                    tb.Focus();
                    tb.Text = value;
                    tb.CaretIndex = value.Length;
                    tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                    return new DebugActionResult(true, "type", target, resolvedType, "TextBox metni güncellendi.");

                case PasswordBox pb:
                    pb.Focus();
                    pb.Password = value;
                    return new DebugActionResult(true, "type", target, resolvedType, "PasswordBox değeri güncellendi.");

                case ComboBox cb when cb.IsEditable:
                    cb.Focus();
                    cb.Text = value;
                    cb.GetBindingExpression(ComboBox.TextProperty)?.UpdateSource();
                    return new DebugActionResult(true, "type", target, resolvedType, "ComboBox metni güncellendi.");

                default:
                    return new DebugActionResult(false, "type", target, resolvedType, "Hedef öğe metin girişini desteklemiyor.");
            }
        }

        private static DebugActionResult ExecuteSelect(DependencyObject element, string target, string resolvedType, string value)
        {
            if (element is ComboBox combo)
            {
                int matchedIndex = -1;
                for (int i = 0; i < combo.Items.Count; i++)
                {
                    string itemText = FormatComboItem(combo, combo.Items[i]);
                    if (string.Equals(itemText, value, StringComparison.OrdinalIgnoreCase))
                    {
                        matchedIndex = i;
                        break;
                    }

                    if (matchedIndex < 0 && itemText.Contains(value, StringComparison.OrdinalIgnoreCase))
                    {
                        matchedIndex = i;
                    }
                }

                if (matchedIndex < 0 && int.TryParse(value, out int numericIndex) &&
                    numericIndex >= 0 && numericIndex < combo.Items.Count)
                {
                    matchedIndex = numericIndex;
                }

                if (matchedIndex < 0)
                {
                    return new DebugActionResult(false, "select", target, resolvedType, $"ComboBox içinde '{value}' eşleşmedi.");
                }

                combo.SelectedIndex = matchedIndex;
                combo.GetBindingExpression(Selector.SelectedItemProperty)?.UpdateSource();
                combo.GetBindingExpression(Selector.SelectedValueProperty)?.UpdateSource();
                combo.GetBindingExpression(Selector.SelectedIndexProperty)?.UpdateSource();
                return new DebugActionResult(true, "select", target, resolvedType, $"SelectedIndex={matchedIndex}");
            }

            if (element is DataGrid dg && int.TryParse(value, out int rowIdx) && rowIdx >= 0 && rowIdx < dg.Items.Count)
            {
                dg.SelectedIndex = rowIdx;
                return new DebugActionResult(true, "select", target, resolvedType, $"DataGrid satır seçildi: {rowIdx}");
            }

            return new DebugActionResult(false, "select", target, resolvedType, "Hedef öğe seçim işlemini desteklemiyor.");
        }

        private static DebugActionResult ExecutePressKey(string keySpec)
        {
            var window = GetActiveTargetWindow();
            if (window == null)
            {
                return new DebugActionResult(false, "press_key", keySpec, null, "Aktif pencere yok.");
            }

            if (!TryParseKeyCombo(keySpec, out var key, out var modifiers))
            {
                return new DebugActionResult(false, "press_key", keySpec, window.GetType().Name, $"Geçersiz kısayol: '{keySpec}'.");
            }

            var targetElement = Keyboard.FocusedElement as UIElement ?? window;

            for (DependencyObject? cursor = targetElement; cursor != null; cursor = VisualTreeHelper.GetParent(cursor))
            {
                if (cursor is UIElement uiCursor && TryInvokeInputBinding(uiCursor, key, modifiers))
                {
                    return new DebugActionResult(true, "press_key", keySpec, window.GetType().Name, "KeyBinding komutu çalıştırıldı.");
                }
            }

            if (TryInvokeInputBinding(window, key, modifiers))
            {
                return new DebugActionResult(true, "press_key", keySpec, window.GetType().Name, "Window KeyBinding komutu çalıştırıldı.");
            }

            var source = PresentationSource.FromVisual(window);
            if (source != null)
            {
                var previewArgs = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key)
                {
                    RoutedEvent = Keyboard.PreviewKeyDownEvent
                };
                targetElement.RaiseEvent(previewArgs);

                if (!previewArgs.Handled)
                {
                    var keyDownArgs = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key)
                    {
                        RoutedEvent = Keyboard.KeyDownEvent
                    };
                    targetElement.RaiseEvent(keyDownArgs);
                }
            }

            if (modifiers == ModifierKeys.None && key == Key.Escape && window != Application.Current?.MainWindow)
            {
                window.Close();
                return new DebugActionResult(true, "press_key", keySpec, window.GetType().Name, "Modal pencere kapatıldı.");
            }

            return new DebugActionResult(true, "press_key", keySpec, window.GetType().Name, $"Tuş iletildi: {modifiers}+{key}");
        }

        private static bool TryInvokeInputBinding(UIElement element, Key key, ModifierKeys modifiers)
        {
            foreach (InputBinding binding in element.InputBindings)
            {
                if (binding is KeyBinding kb && kb.Key == key && kb.Modifiers == modifiers &&
                    kb.Command != null && kb.Command.CanExecute(kb.CommandParameter))
                {
                    kb.Command.Execute(kb.CommandParameter);
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseKeyCombo(string spec, out Key key, out ModifierKeys modifiers)
        {
            key = Key.None;
            modifiers = ModifierKeys.None;
            if (string.IsNullOrWhiteSpace(spec))
            {
                return false;
            }

            string[] tokens = spec.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (string token in tokens)
            {
                switch (token.ToUpperInvariant())
                {
                    case "CTRL":
                    case "CONTROL":
                        modifiers |= ModifierKeys.Control;
                        continue;
                    case "ALT":
                        modifiers |= ModifierKeys.Alt;
                        continue;
                    case "SHIFT":
                        modifiers |= ModifierKeys.Shift;
                        continue;
                    case "ESC":
                    case "ESCAPE":
                        key = Key.Escape;
                        continue;
                    case "ENTER":
                    case "RETURN":
                        key = Key.Return;
                        continue;
                    case "LEFT":
                        key = Key.Left;
                        continue;
                    case "RIGHT":
                        key = Key.Right;
                        continue;
                    case "UP":
                        key = Key.Up;
                        continue;
                    case "DOWN":
                        key = Key.Down;
                        continue;
                    default:
                        if (Enum.TryParse(token, ignoreCase: true, out Key parsed))
                        {
                            key = parsed;
                        }
                        break;
                }
            }

            return key != Key.None;
        }

        private static string GetAutomationIdOrName(DependencyObject element)
        {
            string autoId = AutomationProperties.GetAutomationId(element);
            if (!string.IsNullOrWhiteSpace(autoId))
            {
                return autoId;
            }

            return (element as FrameworkElement)?.Name ?? string.Empty;
        }

        private static string ExtractElementLabel(DependencyObject element)
        {
            string autoName = AutomationProperties.GetName(element);
            if (!string.IsNullOrWhiteSpace(autoName))
            {
                return autoName.Trim();
            }

            if (element is ContentControl cc)
            {
                string contentText = ExtractObjectText(cc.Content);
                if (!string.IsNullOrWhiteSpace(contentText))
                {
                    return contentText;
                }

                string visualText = CollectInnerTextBlocks(cc);
                if (!string.IsNullOrWhiteSpace(visualText))
                {
                    return visualText;
                }
            }

            if (element is FrameworkElement fe)
            {
                if (fe.Tag is string tagStr && !string.IsNullOrWhiteSpace(tagStr))
                {
                    return tagStr.Trim();
                }

                if (fe.ToolTip is string tipStr && !string.IsNullOrWhiteSpace(tipStr))
                {
                    return tipStr.Trim();
                }

                if (!string.IsNullOrWhiteSpace(fe.Name))
                {
                    return fe.Name;
                }
            }

            return string.Empty;
        }

        private static string ExtractObjectText(object? content) => content switch
        {
            null => string.Empty,
            string s => s.Trim(),
            TextBlock tb => tb.Text?.Trim() ?? string.Empty,
            DependencyObject d => CollectInnerTextBlocks(d),
            _ => content.ToString()?.Trim() ?? string.Empty
        };

        private static string CollectInnerTextBlocks(DependencyObject root)
        {
            var sb = new StringBuilder();
            var stack = new Stack<DependencyObject>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                if (cur is UIElement uie && !uie.IsVisible)
                {
                    continue;
                }

                if (cur is TextBlock tb && !string.IsNullOrWhiteSpace(tb.Text))
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(' ');
                    }
                    sb.Append(tb.Text.Trim());
                    if (sb.Length >= 120)
                    {
                        break;
                    }
                    continue;
                }

                int children = VisualTreeHelper.GetChildrenCount(cur);
                for (int i = children - 1; i >= 0; i--)
                {
                    stack.Push(VisualTreeHelper.GetChild(cur, i));
                }
            }

            return sb.ToString();
        }

        private static string GetComboSelectedValue(ComboBox combo)
        {
            if (combo.SelectedItem == null)
            {
                return combo.Text ?? string.Empty;
            }

            return FormatComboItem(combo, combo.SelectedItem);
        }

        private static string FormatComboItem(ComboBox combo, object? item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            if (item is ComboBoxItem cbi)
            {
                return ExtractObjectText(cbi.Content);
            }

            if (!string.IsNullOrWhiteSpace(combo.DisplayMemberPath))
            {
                var prop = item.GetType().GetProperty(combo.DisplayMemberPath);
                if (prop != null)
                {
                    return prop.GetValue(item)?.ToString() ?? string.Empty;
                }
            }

            return item.ToString() ?? string.Empty;
        }
    }

    public sealed record DebugUiNodeDto(
        [property: JsonPropertyName("refId")] string RefId,
        [property: JsonPropertyName("automationId")] string AutomationId,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("isEnabled")] bool IsEnabled,
        [property: JsonPropertyName("isChecked")] bool? IsChecked,
        [property: JsonPropertyName("value")] string? Value);

    internal sealed record DebugActionResult(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("action")] string Action,
        [property: JsonPropertyName("target")] string Target,
        [property: JsonPropertyName("resolvedType")] string? ResolvedType,
        [property: JsonPropertyName("message")] string Message);
}
#endif
