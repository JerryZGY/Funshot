using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Gma.QrCodeNet.Encoding;
using Gma.QrCodeNet.Encoding.Windows.Render;
using Microsoft.Kinect;
using Microsoft.Kinect.Toolkit;
using Microsoft.Kinect.Toolkit.Controls;

namespace KinectPiPi
{
    /// <summary>
    /// MainWindow.xaml 的互動邏輯
    /// </summary>
    public partial class MainWindow : Window, IDisposable
    {
        public static readonly DependencyProperty PageLeftEnabledProperty = DependencyProperty.Register(
            "PageLeftEnabled", typeof(bool), typeof(MainWindow), new PropertyMetadata(false));

        public static readonly DependencyProperty PageRightEnabledProperty = DependencyProperty.Register(
            "PageRightEnabled", typeof(bool), typeof(MainWindow), new PropertyMetadata(false));

        /// <summary>
        /// CLR Property Wrappers for PageLeftEnabledProperty
        /// </summary>
        public bool PageLeftEnabled
        {
            get { return (bool)GetValue(PageLeftEnabledProperty); }
            set { this.SetValue(PageLeftEnabledProperty, value); }
        }

        /// <summary>
        /// CLR Property Wrappers for PageRightEnabledProperty
        /// </summary>
        public bool PageRightEnabled
        {
            get { return (bool)GetValue(PageRightEnabledProperty); }
            set { this.SetValue(PageRightEnabledProperty, value); }
        }

        private const double scrollErrorMargin = 0.001;

        /// <summary>
        /// 捲軸選單是否可見
        /// </summary>
        private bool scrollViewerVisible = false;

        /// <summary>
        /// 同時追蹤使用者上限
        /// </summary>
        private const int maxUsers = 4;

        /// <summary>
        /// 追蹤中的骨架編號陣列
        /// </summary>
        private readonly int[] trackingIds =
        {
            TrackableUser.InvalidTrackingId,
            TrackableUser.InvalidTrackingId
        };

        /// <summary>
        /// 使用者識別區色碼索引
        /// </summary>
        private readonly uint[] userColors =
        {
            0xff000000, // 黑 (背景)
            0xffff0000, // 紅
            0xffff00ff, // 洋紅
            0xff0000ff, // 藍
            0xff00ffff, // 青
            0xff00ff00, // 綠
            0xffffff00, // 黃
            0xff000000  // 黑 (未使用)
        };

        /// <summary>
        /// 可追蹤的去背使用者物件陣列
        /// </summary>
        private TrackableUser[] trackableUsers = new TrackableUser[maxUsers];

        /// <summary>
        /// Kinect感測器選擇器
        /// </summary>
        private KinectSensorChooser sensorChooser;

        /// <summary>
        /// 深度影像緩衝區
        /// </summary>
        private DepthImagePixel[] depthData;

        /// <summary>
        /// 骨架影像緩衝區
        /// </summary>
        private Skeleton[] skeletons;

        /// <summary>
        /// 使用者識別區圖片物件
        /// </summary>
        private WriteableBitmap userViewBitmap;

        /// <summary>
        /// 下一位使用者索引，用來選擇下一位使用者之骨架
        /// </summary>
        private int nextUserIndex = 0;

        /// <summary>
        /// 是否已處理結束
        /// </summary>
        private bool disposed;

        public MainWindow ()
        {
            InitializeComponent();
            this.sensorChooser = new KinectSensorChooser();
            SensorChooserUi.KinectSensorChooser = this.sensorChooser;
            this.sensorChooser.KinectChanged += sensorChooserOnKinectChanged;
            this.sensorChooser.Start();
            var regionSensorBinding = new Binding("Kinect") { Source = this.sensorChooser };
            BindingOperations.SetBinding(this.KinectRegion, KinectRegion.KinectSensorProperty, regionSensorBinding);
            for (var index = 0; index < 14; index++)
            {
                var button = new KinectTileButton {
                    Label = index,
                    Style = (Style)this.Resources["ImageButtonStyle"],
                    Background = new ImageBrush {
                        ImageSource = new BitmapImage(new Uri(@"Resources/CYCU" + index + ".jpg", UriKind.Relative)),
                        Stretch = Stretch.Uniform
                    }
                };
                button.Click += Button_Image_Click;
                WrapPanel.Children.Add(button);
            }
            this.updatePagingButtonState();
            ScrollViewer.ScrollChanged += (o, e) => this.updatePagingButtonState();
            for (int i = 0; i < maxUsers; ++i)
            {
                Image image = new Image();
                Grid_BackgroundRemoved.Children.Add(image);
                this.trackableUsers[i] = new TrackableUser(image);
            }
        }

        ~MainWindow ()
        {
            this.Dispose(false);
        }

        /// <summary>
        /// 處理結束函式，將釋放此物件的所有資源
        /// </summary>
        public void Dispose ()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 處理結束檢查函式，根據是否已處理結束來決定要不要釋放此物件的所有資源
        /// </summary>
        /// <param name="disposing">是否已處理結束</param>
        protected virtual void Dispose (bool disposing)
        {
            if (!this.disposed)
            {
                foreach (var user in this.trackableUsers)
                {
                    user.Dispose();
                }
                this.disposed = true;
            }
        }

        /// <summary>
        /// 根據捲軸選單來改變其中按鈕之座標
        /// </summary>
        private void updatePagingButtonState ()
        {
            this.PageLeftEnabled = ScrollViewer.HorizontalOffset > scrollErrorMargin;
            this.PageRightEnabled = ScrollViewer.HorizontalOffset < ScrollViewer.ScrollableWidth - scrollErrorMargin;
        }

        /// <summary>
        /// 自動選擇追蹤中的使用者，先追蹤的使用者擁有優先權
        /// </summary>
        private void selectTrackedUsers ()
        {
            foreach (var skeleton in this.skeletons.Where(s => SkeletonTrackingState.NotTracked != s.TrackingState))
            {
                if (this.trackableUsers.Where(u => u.IsTracked).Count() == maxUsers)
                {
                    break;
                }
                if (this.trackableUsers.Where(u => u.TrackingId == skeleton.TrackingId).Count() == 0)
                {
                    toggleUserTracking(skeleton.TrackingId);
                }
            }
        }

        /// <summary>
        /// 處理視窗關閉執行緒
        /// </summary>
        /// <param name="sender">事件發送對象的物件</param>
        /// <param name="e">事件參數</param>
        private void windowClosing (object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.sensorChooser.Stop();
            this.sensorChooser = null;
        }

        /// <summary>
        /// 根據追蹤編號來切換追蹤中的使用者
        /// </summary>
        /// <param name="trackingId">欲追蹤的使用者編號</param>
        private void toggleUserTracking (int trackingId)
        {
            if (TrackableUser.InvalidTrackingId != trackingId)
            {
                DateTime minTimestamp = DateTime.MaxValue;
                TrackableUser trackedUser = null;
                TrackableUser staleUser = null;
                foreach (var user in this.trackableUsers)
                {
                    if (user.TrackingId == trackingId)
                    {
                        trackedUser = user;
                    }
                    if (user.Timestamp < minTimestamp)
                    {
                        staleUser = user;
                        minTimestamp = user.Timestamp;
                    }
                }
                if (null != trackedUser)
                {
                    trackedUser.TrackingId = TrackableUser.InvalidTrackingId;
                }
                else
                {
                    staleUser.TrackingId = trackingId;
                }
            }
        }

        /// <summary>
        /// 指示骨架影像來追蹤特定的骨架
        /// </summary>
        private void updateChosenSkeletons ()
        {
            KinectSensor sensor = this.sensorChooser.Kinect;
            if (null != sensor)
            {
                int trackedUserCount = 0;
                for (int i = 0; i < maxUsers && trackedUserCount < this.trackingIds.Length; ++i)
                {
                    var trackableUser = this.trackableUsers[this.nextUserIndex];
                    if (trackableUser.IsTracked)
                    {
                        this.trackingIds[trackedUserCount++] = trackableUser.TrackingId;
                    }
                    this.nextUserIndex = ( this.nextUserIndex + 1 ) % maxUsers;
                }
                for (int i = trackedUserCount; i < this.trackingIds.Length; ++i)
                {
                    this.trackingIds[i] = TrackableUser.InvalidTrackingId;
                }
                sensor.SkeletonStream.ChooseSkeletons(this.trackingIds[0], this.trackingIds[1]);
            }
        }

        /// <summary>
        /// 繪製使用者識別區圖像
        /// </summary>
        /// <param name="depthFrame">新的深度影像</param>
        private void updateUserView (DepthImageFrame depthFrame)
        {
            if (null == this.depthData || this.depthData.Length != depthFrame.PixelDataLength)
            {
                this.depthData = new DepthImagePixel[depthFrame.PixelDataLength];
            }
            depthFrame.CopyDepthImagePixelDataTo(this.depthData);
            int width = depthFrame.Width;
            int height = depthFrame.Height;
            if (null == this.userViewBitmap || this.userViewBitmap.PixelWidth != width || this.userViewBitmap.PixelHeight != height)
            {
                this.userViewBitmap = new WriteableBitmap(width, height, 96.0, 96.0, PixelFormats.Bgra32, null);
                this.Image_UserView.Source = this.userViewBitmap;
            }
            this.userViewBitmap.Lock();
            unsafe
            {
                uint* userViewBits = (uint*)this.userViewBitmap.BackBuffer;
                fixed (uint* userColors = &this.userColors[0])
                {
                    fixed (DepthImagePixel* depthData = &this.depthData[0])
                    {
                        DepthImagePixel* depthPixel = depthData;
                        DepthImagePixel* depthPixelEnd = depthPixel + this.depthData.Length;
                        while (depthPixel < depthPixelEnd)
                        {
                            *( userViewBits++ ) = *( userColors + ( depthPixel++ )->PlayerIndex );
                        }
                    }
                }
            }
            this.userViewBitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
            this.userViewBitmap.Unlock();
        }

        /// <summary>
        /// Kinect感測器改變函式，將設置所有應用參數並註冊事件
        /// </summary>
        /// <param name="oldSensor">上個Kinect感測器</param>
        /// <param name="newSensor">新的Kinect感測器</param>
        private void sensorChooserOnKinectChanged (object sender, KinectChangedEventArgs args)
        {
            if (null != args.OldSensor)
            {
                try
                {
                    args.OldSensor.AllFramesReady -= this.sensorAllFramesReady;
                    args.OldSensor.DepthStream.Disable();
                    args.OldSensor.ColorStream.Disable();
                    args.OldSensor.SkeletonStream.Disable();
                }
                catch (InvalidOperationException) { }
            }

            if (null != args.NewSensor)
            {
                try
                {
                    args.NewSensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
                    args.NewSensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
                    args.NewSensor.SkeletonStream.Enable();
                    args.NewSensor.SkeletonStream.AppChoosesSkeletons = true;
                    args.NewSensor.SkeletonStream.EnableTrackingInNearRange = true;
                    if (null == this.skeletons)
                    {
                        this.skeletons = new Skeleton[args.NewSensor.SkeletonStream.FrameSkeletonArrayLength];
                    }
                    args.NewSensor.AllFramesReady += this.sensorAllFramesReady;
                    args.NewSensor.DepthStream.Range = DepthRange.Default;
                }
                catch (InvalidOperationException) { }
            }
            foreach (var user in this.trackableUsers)
            {
                user.OnKinectSensorChanged(args.OldSensor, args.NewSensor);
            }
        }

        /// <summary>
        /// Kinect感測器影像初始化事件
        /// </summary>
        /// <param name="sender">事件發送對象的物件</param>
        /// <param name="e">事件參數</param>
        private void sensorAllFramesReady (object sender, AllFramesReadyEventArgs e)
        {
            if (null == this.sensorChooser || null == this.sensorChooser.Kinect || this.sensorChooser.Kinect != sender)
            {
                return;
            }
            try
            {
                using (var depthFrame = e.OpenDepthImageFrame())
                {
                    if (null != depthFrame)
                    {
                        this.updateUserView(depthFrame);
                    }
                }
                using (var skeletonFrame = e.OpenSkeletonFrame())
                {
                    if (null != skeletonFrame)
                    {
                        skeletonFrame.CopySkeletonDataTo(this.skeletons);
                        selectTrackedUsers();
                        this.updateChosenSkeletons();
                    }
                }
            }
            catch (InvalidOperationException) { }
        }

        private void Button_Screenshot_Click (object sender, RoutedEventArgs e)
        {
            if (scrollViewerVisible)
                setBackgroundPanelVisible(false);
            Grid_Opaque.IsHitTestVisible = true;
            Label_Counter.Content = "5";
            beginCountdownStoryboard();
            Timer countdownTimer = new Timer(1000);
            var count = 4;
            countdownTimer.Elapsed += (s, ev) =>
            {
                countdownTimer.Stop();
                if (count == 0)
                {
                    countdownTimer.Enabled = false;
                    countdownTimer.Stop();
                    Dispatcher.BeginInvoke(new Action<string>(CounterUpdate), "");
                    Dispatcher.BeginInvoke(new Action(saveBitmap));
                }
                else
                {
                    Dispatcher.BeginInvoke(new Action<string>(CounterUpdate), count.ToString());
                    count--;
                    countdownTimer.Start();
                }
            };
            countdownTimer.Enabled = true;
        }

        private void setBackgroundPanelVisible (bool visible)
        {
            DoubleAnimation changeBackgroundAnimation = new DoubleAnimation();
            QuarticEase easingFunction = new QuarticEase();
            Storyboard storyBoard = new Storyboard();
            Storyboard.SetTargetName(changeBackgroundAnimation, "ScrollViewer");
            Storyboard.SetTargetProperty(changeBackgroundAnimation, new PropertyPath(KinectScrollViewer.HeightProperty));
            changeBackgroundAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.4));
            changeBackgroundAnimation.EasingFunction = easingFunction;
            easingFunction.EasingMode = EasingMode.EaseOut;
            storyBoard.Children.Add(changeBackgroundAnimation);
            if (!visible)
            {
                changeBackgroundAnimation.From = 200;
                changeBackgroundAnimation.To = 0;
                Button_ChangeBackground.Content = "更換背景";
                scrollViewerVisible = false;
            }
            else
            {
                changeBackgroundAnimation.From = 0;
                changeBackgroundAnimation.To = 200;
                Button_ChangeBackground.Content = "隱藏面板";
                scrollViewerVisible = true;
            }
            storyBoard.Begin(ScrollViewer);
        }

        private void beginCountdownStoryboard ()
        {
            Storyboard storyBoard = (Storyboard)this.Resources["CountdownStoryboard"];
            Storyboard.SetTarget(storyBoard.Children.ElementAt(0) as ColorAnimation, Grid_Opaque);
            Storyboard.SetTarget(storyBoard.Children.ElementAt(1) as ColorAnimation, Grid_Opaque);
            Storyboard.SetTarget(storyBoard.Children.ElementAt(2) as ColorAnimation, Grid_Opaque);
            storyBoard.Completed += (se, ev) => { ProgressRing.IsActive = true; };
            storyBoard.Begin();
        }

        private void CounterUpdate (string count)
        {
            Label_Counter.Content = count;
        }

        private async void saveBitmap ()
        {
            BitmapSource source = Image_Background.Source as BitmapSource;
            int colorWidth = Convert.ToInt32(Image_Background.ActualWidth);
            int colorHeight = Convert.ToInt32(Image_Background.ActualHeight);
            var renderBitmap = new RenderTargetBitmap(colorWidth, colorHeight, 96.0, 96.0, PixelFormats.Pbgra32);
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                var backdropBrush = new VisualBrush(Image_Background);
                dc.DrawRectangle(backdropBrush, null, new Rect(new Point(), new Size(colorWidth, colorHeight)));
                var colorBrush = new VisualBrush(Grid_BackgroundRemoved);
                dc.DrawRectangle(colorBrush, null, new Rect(new Point(), new Size(colorWidth, colorHeight)));
            }
            renderBitmap.Render(dv);
            Image_Result.Source = renderBitmap;
            drawQrCode(await getImageUrlAsync(await postImageHttpWebRequsetAsync(convertImageToByte(renderBitmap))));
            beginShowResultStoryboard();
        }

        private void drawQrCode (string text)
        {
            QrEncoder qrEncoder = new QrEncoder(ErrorCorrectionLevel.M);
            QrCode qrCode;
            qrEncoder.TryEncode(text, out qrCode);
            WriteableBitmap wBitmap = new WriteableBitmap(180, 180, 96, 96, PixelFormats.Gray8, null);
            new WriteableBitmapRenderer(new FixedModuleSize(4, QuietZoneModules.Two)).Draw(wBitmap, qrCode.Matrix);
            Image_QrCode.Source = wBitmap;
        }

        private async Task<HttpWebRequest> postImageHttpWebRequsetAsync (byte[] image)
        {
            HttpWebRequest req = (HttpWebRequest)HttpWebRequest.Create("http://140.135.113.20/kinect/get.php");
            byte[] bs = Encoding.ASCII.GetBytes(@"img=data:image/jpeg;base64," + Convert.ToBase64String(image));
            req.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
            req.Method = "POST";
            req.ContentLength = bs.Length;
            Stream reqStream = await req.GetRequestStreamAsync();
            reqStream.Write(bs, 0, bs.Length);
            return req;
        }

        private async Task<string> getImageUrlAsync (HttpWebRequest req)
        {
            WebResponse response = await req.GetResponseAsync();
            string imageAddress = @"http://140.135.113.20/kinect/index.php?img=" + new StreamReader(response.GetResponseStream()).ReadToEnd();
            return imageAddress;
        }

        private byte[] convertImageToByte (RenderTargetBitmap image)
        {
            BitmapEncoder encoder = new JpegBitmapEncoder();
            MemoryStream ms = new MemoryStream();
            encoder.Frames.Add(BitmapFrame.Create(image));
            encoder.Save(ms);
            return ms.ToArray();
        }

        private void beginShowResultStoryboard ()
        {
            Storyboard storyBoard = (Storyboard)this.Resources["ShowResultStoryboard"];
            Storyboard.SetTarget(storyBoard.Children.ElementAt(0) as ColorAnimation, Grid_Opaque);
            Storyboard.SetTarget(storyBoard.Children.ElementAt(1) as DoubleAnimation, Button_Screenshot);
            Storyboard.SetTarget(storyBoard.Children.ElementAt(2) as DoubleAnimation, Button_ChangeBackground);
            Storyboard.SetTarget(storyBoard.Children.ElementAt(3) as DoubleAnimation, Button_Restart);
            Storyboard.SetTarget(storyBoard.Children.ElementAt(4) as DoubleAnimation, Image_QrCode);
            storyBoard.Completed += (se, ev) => { ProgressRing.IsActive = false; Grid_Opaque.IsHitTestVisible = false; };
            storyBoard.Begin();
        }

        private void Button_ChangeBackground_Click (object sender, RoutedEventArgs e)
        {
            if (scrollViewerVisible)
            {
                setBackgroundPanelVisible(false);
            }
            else
            {
                setBackgroundPanelVisible(true);
            }
        }

        private void Button_Restart_Click (object sender, RoutedEventArgs e)
        {
            Storyboard storyBoard = (Storyboard)this.Resources["RestartStoryboard"];
            Storyboard.SetTarget(storyBoard.Children.ElementAt(0) as DoubleAnimation, Button_Restart);
            Storyboard.SetTarget(storyBoard.Children.ElementAt(1) as DoubleAnimation, Image_QrCode);
            Storyboard.SetTarget(storyBoard.Children.ElementAt(2) as DoubleAnimation, Button_Screenshot);
            Storyboard.SetTarget(storyBoard.Children.ElementAt(3) as DoubleAnimation, Button_ChangeBackground);
            storyBoard.Begin();
            Image_Result.Source = null;
        }

        private void Button_Image_Click (object sender, RoutedEventArgs e)
        {
            KinectTileButton button = (KinectTileButton)sender;
            Image_Background.Source = new BitmapImage(new Uri("Resources/CYCU" + button.Label as string + ".jpg", UriKind.Relative));
        }
    }
}
