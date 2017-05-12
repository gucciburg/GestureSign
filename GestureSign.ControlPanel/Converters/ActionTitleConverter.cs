using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Input;
using GestureSign.Common.Applications;
using MahApps.Metro.Controls;
using ManagedWinapi.Hooks;

namespace GestureSign.ControlPanel.Converters
{
    [ValueConversion(typeof(IAction), typeof(string))]
    public class ActionTitleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var action = (IAction)value;
            if (action == null) return null;

            var hotKeyString = action.Hotkey != null ? "(" + new HotKey(KeyInterop.KeyFromVirtualKey(action.Hotkey.KeyCode), (ModifierKeys)action.Hotkey.ModifierKeys).ToString() + ")" : string.Empty;
            var mouseString = action.MouseHotkey == MouseActions.None ? string.Empty : "(" + ViewModel.MouseActionDescription.DescriptionDict[action.MouseHotkey] + ")";
            return $"{action.Name} {hotKeyString} {mouseString}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}