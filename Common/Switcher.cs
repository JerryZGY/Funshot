namespace Funshot
{
    public static class Switcher
    {
        public static PageSwitcher pageSwitcher;

        public static void Switch(System.Windows.Controls.UserControl newPage)
        {
            pageSwitcher.Navigate(newPage);
        }
    }
}