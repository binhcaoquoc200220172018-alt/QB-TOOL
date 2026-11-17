using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace InfraBIM.ManholeTool.Views
{
    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return b ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility v)
            {
                return v != Visibility.Visible;
            }
            return false;
        }
    }

    public class WarningToColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush RedBrush = new(Color.FromRgb(239, 68, 68)); // #EF4444
        private static readonly SolidColorBrush GreenBrush = new(Color.FromRgb(74, 222, 128)); // #4ADE80
        private static readonly SolidColorBrush NormalTextBrush = new(Color.FromRgb(255, 255, 255));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool hasWarning && hasWarning)
            {
                return RedBrush;
            }
            return NormalTextBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BindingProxy : Freezable
    {
        protected override Freezable CreateInstanceCore()
        {
            return new BindingProxy();
        }

        public object Data
        {
            get => GetValue(DataProperty);
            set => SetValue(DataProperty, value);
        }

        public static readonly DependencyProperty DataProperty =
            DependencyProperty.Register("Data", typeof(object), typeof(BindingProxy), new UIPropertyMetadata(null));
    }
}
