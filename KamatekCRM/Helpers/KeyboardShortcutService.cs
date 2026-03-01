using System.Windows;
using System.Windows.Input;

namespace KamatekCrm.Helpers
{
    /// <summary>
    /// Global klavye kısayolları servisi.
    /// MainWindow'a bağlanır, tüm modüllerde geçerli kısayolları yönetir.
    ///
    /// Kısayollar:
    ///   Ctrl+N  → Yeni kayıt
    ///   Ctrl+S  → Kaydet
    ///   Ctrl+F  → Arama odağı
    ///   F5      → Yenile
    ///   Escape  → Popup/Dialog kapat
    ///   Ctrl+E  → Excel Export
    ///   Ctrl+P  → PDF Export / Yazdır
    ///   Ctrl+D  → Dashboard'a git
    ///   Ctrl+1-9 → Modül hızlı geçiş
    /// </summary>
    public class KeyboardShortcutService
    {
        private readonly Dictionary<KeyCombination, Action> _shortcuts = new();
        private Window? _window;

        /// <summary>Ana pencereye bağla</summary>
        public void Attach(Window window)
        {
            _window = window;
            _window.PreviewKeyDown += OnPreviewKeyDown;
        }

        /// <summary>Bağlantıyı kaldır</summary>
        public void Detach()
        {
            if (_window != null)
            {
                _window.PreviewKeyDown -= OnPreviewKeyDown;
                _window = null;
            }
        }

        /// <summary>Kısayol kaydet</summary>
        public void Register(Key key, ModifierKeys modifiers, Action action)
        {
            _shortcuts[new KeyCombination(key, modifiers)] = action;
        }

        /// <summary>Kısayol kaydet (modifier yok)</summary>
        public void Register(Key key, Action action)
        {
            Register(key, ModifierKeys.None, action);
        }

        /// <summary>Kısayol sil</summary>
        public void Unregister(Key key, ModifierKeys modifiers = ModifierKeys.None)
        {
            _shortcuts.Remove(new KeyCombination(key, modifiers));
        }

        /// <summary>Tüm kısayolları temizle</summary>
        public void ClearAll() => _shortcuts.Clear();

        /// <summary>Kayıtlı kısayolları listele (UI'da göstermek için)</summary>
        public IReadOnlyDictionary<KeyCombination, Action> GetRegisteredShortcuts()
            => _shortcuts.AsReadOnly();

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            // TextBox'larda yazarken kısayolları engelleme (modifier ile beraber çalışır)
            if (e.OriginalSource is System.Windows.Controls.TextBox && Keyboard.Modifiers == ModifierKeys.None)
                return;

            var combo = new KeyCombination(e.Key, Keyboard.Modifiers);
            if (_shortcuts.TryGetValue(combo, out var action))
            {
                action.Invoke();
                e.Handled = true;
            }
        }

        /// <summary>Varsayılan global kısayolları kaydet</summary>
        public void RegisterDefaults(
            Action? onNew = null,
            Action? onSave = null,
            Action? onSearch = null,
            Action? onRefresh = null,
            Action? onEscape = null,
            Action? onExcel = null,
            Action? onPrint = null)
        {
            if (onNew != null) Register(Key.N, ModifierKeys.Control, onNew);
            if (onSave != null) Register(Key.S, ModifierKeys.Control, onSave);
            if (onSearch != null) Register(Key.F, ModifierKeys.Control, onSearch);
            if (onRefresh != null) Register(Key.F5, onRefresh);
            if (onEscape != null) Register(Key.Escape, onEscape);
            if (onExcel != null) Register(Key.E, ModifierKeys.Control, onExcel);
            if (onPrint != null) Register(Key.P, ModifierKeys.Control, onPrint);
        }
    }

    /// <summary>
    /// Tuş kombinasyonu (hash + equals)
    /// </summary>
    public record KeyCombination(Key Key, ModifierKeys Modifiers)
    {
        public string DisplayText => Modifiers != ModifierKeys.None
            ? $"{ModifierText}+{KeyText}"
            : KeyText;

        private string ModifierText => Modifiers switch
        {
            ModifierKeys.Control => "Ctrl",
            ModifierKeys.Alt => "Alt",
            ModifierKeys.Shift => "Shift",
            ModifierKeys.Control | ModifierKeys.Shift => "Ctrl+Shift",
            ModifierKeys.Control | ModifierKeys.Alt => "Ctrl+Alt",
            _ => Modifiers.ToString()
        };

        private string KeyText => Key switch
        {
            Key.F5 => "F5",
            Key.Escape => "Esc",
            _ => Key.ToString()
        };
    }
}
