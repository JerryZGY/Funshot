using System;
using System.Windows;
using System.Windows.Controls;

namespace Funshot
{
    public class MusicHandler
    {
        public MediaElement Music;

        public MusicHandler(Grid grid, string uri)
        {
            Music = new MediaElement()
            {
                Visibility = Visibility.Collapsed,
                LoadedBehavior = MediaState.Manual,
                Volume = .5
            };
            Music.Source = new Uri(uri, UriKind.Relative);
            Music.Loaded += loaded;
            Music.MediaEnded += ended;
            grid.Children.Add(Music);
        }

        private void loaded(object sender, RoutedEventArgs e)
        {
            Music.Play();
        }

        private void ended(object sender, RoutedEventArgs e)
        {
            Music.Position = TimeSpan.Zero;
            Music.Play();
        }
    }
}