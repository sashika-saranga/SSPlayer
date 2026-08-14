using System.Windows;

namespace Mp3Player
{
    public static class IndicatorHelper
    {
        public static readonly DependencyProperty IsIndicatorOnProperty =
            DependencyProperty.RegisterAttached("IsIndicatorOn", typeof(bool), typeof(IndicatorHelper), new PropertyMetadata(false));

        public static void SetIsIndicatorOn(DependencyObject element, bool value)
        {
            element.SetValue(IsIndicatorOnProperty, value);
        }

        public static bool GetIsIndicatorOn(DependencyObject element)
        {
            return (bool)element.GetValue(IsIndicatorOnProperty);
        }
    }
}
