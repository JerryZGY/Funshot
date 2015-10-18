using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Funshot.Pages;

namespace Funshot
{
    public partial class PageSwitcher : Window
    {
        public KinectHandler kinect;

        public PageSwitcher()
        {
            InitializeComponent();
            new MusicHandler(Grid_Main, "Materials/BGM.mp3");
            kinect = new KinectHandler();
            Switcher.pageSwitcher = this;
            Switcher.Switch(new Page_Start());
        }

        public void Navigate(UserControl nextPage)
        {
            GC.Collect();
            var prevPage = Presenter.Content as IPage;
            if (prevPage != null)
                prevPage.ExitStory(() => Presenter.Content = nextPage);
            else
                Presenter.Content = nextPage;
        }

        private void closing(object sender, CancelEventArgs e)
        {
            if (kinect.multiFrameSourceReader != null)
            {
                kinect.multiFrameSourceReader.Dispose();
                kinect.multiFrameSourceReader = null;
            }
            if (kinect.kinectSensor != null)
            {
                kinect.kinectSensor.Close();
                kinect.kinectSensor = null;
            }
        }
    }
}