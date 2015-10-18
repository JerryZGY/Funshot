using System;
using System.Windows.Controls;

namespace Funshot.Pages
{
    public partial class Page_Start : UserControl, IPage
    {
        public Page_Start()
        {
            InitializeComponent();
        }

        public void InitializeProperty()
        {
            Grid_Main.Opacity = 0;
            Img_Title.Opacity = 0;
            Switcher.pageSwitcher.kinect.setKinectRegionBinding(ref Reg);
        }

        public void EnterStory()
        {
            StoryHandler.Begin(this, "Enter", () => Btn_Start.IsHitTestVisible = true);
        }

        public void ExitStory(Action callback)
        {
            StoryHandler.Begin(this, "Exit", () => callback());
        }

        private void Page_Start_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            InitializeProperty();
            EnterStory();
        }

        private void Btn_Start_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Btn_Start.IsHitTestVisible = false;
            Switcher.Switch(new Page_Main());
        }
    }
}