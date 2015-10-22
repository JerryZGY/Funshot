using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Gma.QrCodeNet.Encoding;
using Gma.QrCodeNet.Encoding.Windows.Render;

namespace Funshot.Pages
{
    public partial class Page_Main : UserControl, IPage
    {
        private const int photoWidth = 800;
        private const int photoHeight = 600;
        private System.Windows.Forms.Timer idleCountdownTimer = new System.Windows.Forms.Timer() { Interval = 1000 };
        private System.Windows.Forms.Timer screenShotCountdownTimer = new System.Windows.Forms.Timer() { Interval = 1000 };
        private int idleCountdownTimes;
        private int screenShotCountdownTimes;
        private bool scrollViewerVisible = false;
        private bool isBackgroundScrollViewer = true;
        private List<Button> backgroundButtonImageSource = new List<Button>();
        private List<Button> iconButtonImageSource = new List<Button>();
        private DraggableElement draggedElement;
        private Point mousePosition;
        private Image resultImage;
        private RemoteHandler remote;

        public Page_Main()
        {
            InitializeComponent();
        }

        public void InitializeProperty()
        {
            remote = new RemoteHandler();
            Img_Removal.Source = Switcher.pageSwitcher.kinect.RemovalImageSource;
            Img_UserView.Source = Switcher.pageSwitcher.kinect.BodyIndexImageSource;
            Grid_Main.Opacity = 0;
            Img_Removal.Opacity = 0;
            Img_UserView.Opacity = 0;
            ProgressRing.Opacity = 0;
            idleCountdownTimer.Tick += idleCountdownTimer_Tick;
            screenShotCountdownTimer.Tick += screenShotCountdownTimer_Tick;
            idleCountdownTimer_Reset();
            foreach (string fname in Directory.GetFileSystemEntries(@"Materials/Backgrounds", "*.png"))
            {
                var button = new Button
                {
                    Tag = fname,
                    Style = (Style)Resources["Btn_Img"],
                    Background = new ImageBrush
                    {
                        ImageSource = new BitmapImage(new Uri(fname, UriKind.Relative)),
                        Stretch = Stretch.Uniform
                    }
                };
                button.Click += Btn_Img_Click;
                backgroundButtonImageSource.Add(button);
            }
            foreach (string fname in Directory.GetFileSystemEntries(@"Materials/Icons", "*.png"))
            {
                var button = new Button
                {
                    Tag = fname,
                    Style = (Style)Resources["Btn_Img"],
                    Background = new ImageBrush
                    {
                        ImageSource = new BitmapImage(new Uri(fname, UriKind.Relative)),
                        Stretch = Stretch.Uniform
                    }
                };
                button.Click += Btn_Icon_Click;
                iconButtonImageSource.Add(button);
            }
            foreach (var button in backgroundButtonImageSource)
            {
                WrapPanel.Children.Add(button);
            }
            Switcher.pageSwitcher.kinect.setKinectRegionBinding(ref Reg);
        }

        public void EnterStory()
        {
            StoryHandler.Begin(this, "Enter", () => IsHitTestVisible = true);
        }

        public void ExitStory(Action callback)
        {
            Grid_Opaque.IsHitTestVisible = true;
            Tbx_IdleCounter.Text = "";
            idleCountdownTimer.Stop();
            StoryHandler.Begin(this, "Exit", () => callback());
        }

        private void Page_Start_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeProperty();
            EnterStory();
        }

        private void Btn_ChangeBG_Click(object sender, RoutedEventArgs e)
        {
            idleCountdownTimer_Reset();
            if (scrollViewerVisible && !isBackgroundScrollViewer)
                setIconPanelVisible(false);
            if (!isBackgroundScrollViewer)
            {
                isBackgroundScrollViewer = true;
                WrapPanel.Children.Clear();
                foreach (var button in backgroundButtonImageSource)
                {
                    WrapPanel.Children.Add(button);
                }
            }
            setBackgroundPanelVisible(!scrollViewerVisible);
        }

        private void Btn_AddIcon_Click(object sender, RoutedEventArgs e)
        {
            idleCountdownTimer_Reset();
            if (scrollViewerVisible && isBackgroundScrollViewer)
            {
                setBackgroundPanelVisible(false);
            }
            if (isBackgroundScrollViewer)
            {
                isBackgroundScrollViewer = false;
                WrapPanel.Children.Clear();
                foreach (var button in iconButtonImageSource)
                {
                    WrapPanel.Children.Add(button);
                }
            }
            setIconPanelVisible(!scrollViewerVisible);
        }

        private void Btn_ClearIcon_Click(object sender, RoutedEventArgs e)
        {
            idleCountdownTimer_Reset();
            Cnv.Children.Clear();
        }

        private void Btn_Img_Click(object sender, RoutedEventArgs e)
        {
            idleCountdownTimer_Reset();
            Button button = (Button)sender;
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(button.Tag as string, UriKind.Relative);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            Img_BG.Source = bmp;
        }

        private void Btn_Icon_Click(object sender, RoutedEventArgs e)
        {
            idleCountdownTimer_Reset();
            Button button = (Button)sender;
            ImageSource source = new BitmapImage(new Uri(button.Tag as string, UriKind.Relative));
            var icon = new DraggableElement();
            icon.Child = new Image { Source = source };
            Canvas.SetLeft(icon, Cnv.ActualWidth / 2 - source.Width / 2);
            Canvas.SetTop(icon, Cnv.ActualHeight / 2 - source.Height / 2);
            Cnv.Children.Add(icon);
        }

        private void Btn_Print_Click(object sender, RoutedEventArgs e)
        {
            new PrintDialog().PrintVisual(resultImage, "Print Funshot Result");
        }

        private void Btn_Again_Click(object sender, RoutedEventArgs e)
        {
            Switcher.Switch(new Page_Start());
        }

        private void Btn_Screenshot_Click(object sender, RoutedEventArgs e)
        {
            if (scrollViewerVisible)
            {
                setBackgroundPanelVisible(false);
                setIconPanelVisible(false);
            }
            idleCountdownTimer_Stop();
            Grid_Opaque.IsHitTestVisible = true;
            Tbx_ShotCounter.Text = "5";
            screenShotCountdownTimes = 4;
            screenShotCountdownTimer.Start();
            StoryHandler.Begin(this, "Countdown");
        }

        private void idleCountdownTimer_Tick(object sender, EventArgs e)
        {
            if (idleCountdownTimes > 0)
            {
                Tbx_IdleCounter.Text = idleCountdownTimes.ToString();
                idleCountdownTimes--;
            }
            else
                Switcher.Switch(new Page_Start());
        }

        private void screenShotCountdownTimer_Tick(object sender, EventArgs e)
        {
            if (screenShotCountdownTimes > 0)
            {
                Tbx_ShotCounter.Text = screenShotCountdownTimes.ToString();
                screenShotCountdownTimes--;
            }
            else if (screenShotCountdownTimes == 0)
            {
                screenShotCountdownTimer.Stop();
                Tbx_ShotCounter.Text = "";
                saveBitmap();
            }
            else if (screenShotCountdownTimes == -1)
            {
                screenShotCountdownTimer.Stop();
                StoryHandler.Begin(this, "ShowResult", () => Grid_Opaque.IsHitTestVisible = false);
            }
        }

        private void idleCountdownTimer_Reset()
        {
            idleCountdownTimer.Enabled = false;
            Tbx_IdleCounter.Text = "90";
            idleCountdownTimes = 90;
            idleCountdownTimer.Enabled = true;
        }

        private void idleCountdownTimer_Stop()
        {
            idleCountdownTimer.Enabled = false;
            Tbx_IdleCounter.Text = "";
        }

        private void saveBitmap()
        {
            resultImage = new Image();
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                var backdropBrush = new VisualBrush(Img_BG);
                dc.DrawRectangle(backdropBrush, null, new Rect(new Point(), new Size(photoWidth, photoHeight)));
                var colorBrush = new VisualBrush(Img_Removal);
                dc.DrawRectangle(colorBrush, null, new Rect(new Point(), new Size(photoWidth, photoHeight)));
                var iconBrush = new VisualBrush(Cnv);
                dc.DrawRectangle(iconBrush, null, new Rect(new Point(), new Size(photoWidth, photoHeight)));
            }
            var renderBitmap = new RenderTargetBitmap(photoWidth, photoHeight, 96.0, 96.0, PixelFormats.Pbgra32);
            renderBitmap.Render(dv);
            resultImage.Source = renderBitmap;
            Img_Result.Source = renderBitmap;
            saveBitmapToLocal(renderBitmap);
            showResult(renderBitmap);
        }

        private async void showResult(RenderTargetBitmap renderBitmap)
        {
            if (await remote.IsRemoteConnected())
            {
                var text = await remote.GetImageUrlAsync(renderBitmap);
                var source = drawQrCode(text);
                Img_QrCode.Source = source;
                StoryHandler.Begin(this, "ShowResult", () => Grid_Opaque.IsHitTestVisible = false);
            }
            else
            {
                screenShotCountdownTimes = -1;
                screenShotCountdownTimer.Start();
            }
        }

        private ImageSource drawQrCode(string text)
        {
            QrEncoder qrEncoder = new QrEncoder(ErrorCorrectionLevel.M);
            QrCode qrCode;
            qrEncoder.TryEncode(text, out qrCode);
            WriteableBitmap wBitmap = new WriteableBitmap(111, 111, 96, 96, PixelFormats.Gray8, null);
            new WriteableBitmapRenderer(new FixedModuleSize(3, QuietZoneModules.Two)).Draw(wBitmap, qrCode.Matrix);
            return wBitmap;
        }

        private void saveBitmapToLocal(RenderTargetBitmap renderBitmap)
        {
            BitmapEncoder encoder = new JpegBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(renderBitmap));
            string time = DateTime.Now.ToString("yyyyMMdd'_'hh'_'mm'_'ss");
            string dirPath = @"Photos";
            string path = Path.Combine(@"Photos/", "FS_" + time + ".jpg");
            if (Directory.Exists(dirPath))
            {
                try
                {
                    
                    using (FileStream fs = new FileStream(path, FileMode.Create))
                    {
                        encoder.Save(fs);
                    }
                }
                catch (IOException)
                {
                    Trace.WriteLine(Properties.Resources.FailedSaveBitmapText);
                }
            }
            else
            {
                Directory.CreateDirectory(dirPath);
                try
                {
                    using (FileStream fs = new FileStream(path, FileMode.Create))
                    {
                        encoder.Save(fs);
                    }
                }
                catch (IOException)
                {
                    Trace.WriteLine(Properties.Resources.FailedSaveBitmapText);
                }
            }
        }

        private void setBackgroundPanelVisible(bool visible)
        {
            DoubleAnimation changeBackgroundAnimation = new DoubleAnimation();
            QuarticEase easingFunction = new QuarticEase();
            Storyboard storyBoard = new Storyboard();
            Storyboard.SetTargetName(changeBackgroundAnimation, "ScrollViewer");
            Storyboard.SetTargetProperty(changeBackgroundAnimation, new PropertyPath(HeightProperty));
            changeBackgroundAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.4));
            changeBackgroundAnimation.EasingFunction = easingFunction;
            easingFunction.EasingMode = EasingMode.EaseOut;
            storyBoard.Children.Add(changeBackgroundAnimation);
            if (!visible)
            {
                changeBackgroundAnimation.From = 200;
                changeBackgroundAnimation.To = 0;
                Btn_ChangeBG.Content = "更換背景";
                scrollViewerVisible = false;
            }
            else
            {
                changeBackgroundAnimation.From = 0;
                changeBackgroundAnimation.To = 200;
                Btn_ChangeBG.Content = "隱藏面板";
                scrollViewerVisible = true;
            }
            storyBoard.Begin(ScrollViewer);
        }

        private void setIconPanelVisible(bool visible)
        {
            DoubleAnimation changeBackgroundAnimation = new DoubleAnimation();
            QuarticEase easingFunction = new QuarticEase();
            Storyboard storyBoard = new Storyboard();
            Storyboard.SetTargetName(changeBackgroundAnimation, "ScrollViewer");
            Storyboard.SetTargetProperty(changeBackgroundAnimation, new PropertyPath(HeightProperty));
            changeBackgroundAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.4));
            changeBackgroundAnimation.EasingFunction = easingFunction;
            easingFunction.EasingMode = EasingMode.EaseOut;
            storyBoard.Children.Add(changeBackgroundAnimation);
            if (!visible)
            {
                changeBackgroundAnimation.From = 200;
                changeBackgroundAnimation.To = 0;
                Btn_AddIcon.Content = "新增貼圖";
                scrollViewerVisible = false;
            }
            else
            {
                changeBackgroundAnimation.From = 0;
                changeBackgroundAnimation.To = 200;
                Btn_AddIcon.Content = "隱藏面板";
                scrollViewerVisible = true;
            }
            storyBoard.Begin(ScrollViewer);
        }

        private void CanvasMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var image = e.Source as Image;
            if (image != null)
            {
                var draggableElement = image.Parent as DraggableElement;
                if (draggableElement != null && Cnv.CaptureMouse())
                {
                    mousePosition = e.GetPosition(Cnv);
                    draggedElement = draggableElement;
                    Panel.SetZIndex(draggedElement, 1);
                    idleCountdownTimer_Reset();
                }
            }
        }

        private void CanvasMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (draggedElement != null)
            {
                Cnv.ReleaseMouseCapture();
                Panel.SetZIndex(draggedElement, 0);
                draggedElement = null;
            }
        }

        private void CanvasMouseMove(object sender, MouseEventArgs e)
        {
            if (draggedElement != null)
            {
                var position = e.GetPosition(Cnv);
                var offset = position - mousePosition;
                mousePosition = position;
                var X = Canvas.GetLeft(draggedElement) + offset.X;
                if (X < 0)
                    X = 0;
                if (X > Cnv.ActualWidth - draggedElement.ActualWidth)
                    X = Cnv.ActualWidth - draggedElement.ActualWidth;
                var Y = Canvas.GetTop(draggedElement) + offset.Y;
                if (Y < 0)
                    Y = 0;
                if (Y > Cnv.ActualHeight - draggedElement.ActualHeight)
                    Y = Cnv.ActualHeight - draggedElement.ActualHeight;
                Canvas.SetLeft(draggedElement, X);
                Canvas.SetTop(draggedElement, Y);
            }
        }
    }
}