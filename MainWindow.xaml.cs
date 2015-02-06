using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
using Facebook;
using Gma.QrCodeNet.Encoding;
using Gma.QrCodeNet.Encoding.Windows.Render;
using Microsoft.Kinect;
using Microsoft.Kinect.Wpf.Controls;

namespace KinectPiPi
{
    /// <summary>
    /// MainWindow.xaml 的互動邏輯
    /// </summary>
    public partial class MainWindow : Window
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

        /// <summary>
        /// Gets the bitmap to display
        /// </summary>
        public ImageSource ImageSource
        {
            get
            {
                return this.bitmap;
            }
        }

        /// <summary>
        /// Gets the bitmap to display
        /// </summary>
        public ImageSource BodyIndexImageSource
        {
            get
            {
                return this.bodyIndexBitmap;
            }
        }

        /// <summary>
        /// Collection of colors to be used to display the BodyIndexFrame data.
        /// </summary>
        private static readonly uint[] BodyColor =
        {
            0x0000FF00,
            0x00FF0000,
            0xFFFF4000,
            0x40FFFF00,
            0xFF40FF00,
            0xFF808000,
        };

        /// <summary>
        /// Description of the data contained in the body index frame
        /// </summary>
        private FrameDescription bodyIndexFrameDescription = null;

        /// <summary>
        /// Intermediate storage for frame data converted to color
        /// </summary>
        private uint[] bodyIndexPixels = null;

        /// <summary>
        /// Size of the RGB pixel in the bitmap
        /// </summary>
        private const int BytesPerPixel = 4;

        /// <summary>
        /// Bitmap to display
        /// </summary>
        private WriteableBitmap bodyIndexBitmap = null;

        private const double scrollErrorMargin = 0.001;

        private DraggableElement draggedElement;

        private Point mousePosition;

        private List<Button> backgroundButtonImageSource = new List<Button>();

        private List<Button> iconButtonImageSource = new List<Button>();

        /// <summary>
        /// 捲軸選單是否可見
        /// </summary>
        private bool scrollViewerVisible = false;

        private bool isBackgroundScrollViewer = true;

        /// <summary>
        /// Size of the RGB pixel in the bitmap
        /// </summary>
        private readonly int bytesPerPixel = ( PixelFormats.Bgr32.BitsPerPixel + 7 ) / 8;

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor kinectSensor = null;

        /// <summary>
        /// Coordinate mapper to map one type of point to another
        /// </summary>
        private CoordinateMapper coordinateMapper = null;

        /// <summary>
        /// Reader for depth/color/body index frames
        /// </summary>
        private MultiSourceFrameReader multiFrameSourceReader = null;

        /// <summary>
        /// The size in bytes of the bitmap back buffer
        /// </summary>
        private uint bitmapBackBufferSize = 0;

        /// <summary>
        /// Intermediate storage for the color to depth mapping
        /// </summary>
        private DepthSpacePoint[] colorMappedToDepthPoints = null;

        /// <summary>
        /// Bitmap to display
        /// </summary>
        private WriteableBitmap bitmap = null;

        public MainWindow ()
        {
            /*WebClient wc = new WebClient();
            //因為access_token會有過期失效問題，所以每次都重新取得access_token
            string result = wc.DownloadString("https://graph.facebook.com/oauth/access_token?client_id=330042973859060&client_secret=25cb17666efcaf603ae18eb46abc5950&scope=manage_notifications,manage_pages,publish_actions");
            string access_token = result.Split('=')[1];
            var fb = new FacebookClient();
            dynamic result = fb.Get("oauth/access_token", new
            {
                client_id = "330042973859060",
                client_secret = "25cb17666efcaf603ae18eb46abc5950",
                grant_type = "client_credentials"
            });
            fb.AccessToken = result.access_token;
            dynamic resultt = fb.Get("812796125441443");
            var access_token = resultt.access_token;*/

            this.kinectSensor = KinectSensor.GetDefault();

            this.multiFrameSourceReader = this.kinectSensor.OpenMultiSourceFrameReader(FrameSourceTypes.Depth | FrameSourceTypes.Color | FrameSourceTypes.BodyIndex);

            this.multiFrameSourceReader.MultiSourceFrameArrived += this.Reader_MultiSourceFrameArrived;

            this.coordinateMapper = this.kinectSensor.CoordinateMapper;

            FrameDescription depthFrameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;

            int depthWidth = depthFrameDescription.Width;
            int depthHeight = depthFrameDescription.Height;

            FrameDescription colorFrameDescription = this.kinectSensor.ColorFrameSource.FrameDescription;

            int colorWidth = colorFrameDescription.Width;
            int colorHeight = colorFrameDescription.Height;

            this.colorMappedToDepthPoints = new DepthSpacePoint[colorWidth * colorHeight];

            this.bitmap = new WriteableBitmap(colorWidth, colorHeight, 96.0, 96.0, PixelFormats.Bgra32, null);

            this.bodyIndexFrameDescription = this.kinectSensor.BodyIndexFrameSource.FrameDescription;
            // allocate space to put the pixels being converted
            this.bodyIndexPixels = new uint[this.bodyIndexFrameDescription.Width * this.bodyIndexFrameDescription.Height];
            // create the bitmap to display
            this.bodyIndexBitmap = new WriteableBitmap(this.bodyIndexFrameDescription.Width, this.bodyIndexFrameDescription.Height, 96.0, 96.0, PixelFormats.Bgr32, null);
            // Calculate the WriteableBitmap back buffer size
            this.bitmapBackBufferSize = (uint)( ( this.bitmap.BackBufferStride * ( this.bitmap.PixelHeight - 1 ) ) + ( this.bitmap.PixelWidth * this.bytesPerPixel ) );

            this.kinectSensor.Open();
            this.DataContext = this;

            var regionSensorBinding = new Binding("Kinect") { Source = this.kinectSensor };
            InitializeComponent();
            foreach (string fname in System.IO.Directory.GetFileSystemEntries(@"Resources/Background", "*.jpg"))
            {
                var button = new Button
                {
                    Tag = fname,
                    Style = (Style)this.Resources["ImageButtonStyle"],
                    Background = new ImageBrush
                    {
                        ImageSource = new BitmapImage(new Uri(fname, UriKind.Relative)),
                        Stretch = Stretch.Uniform
                    }
                };
                button.Click += Button_Image_Click;
                backgroundButtonImageSource.Add(button);
            }
            foreach (string fname in System.IO.Directory.GetFileSystemEntries(@"Resources/Icon", "*.png"))
            {
                var button = new Button
                {
                    Tag = fname,
                    Style = (Style)this.Resources["ImageButtonStyle"],
                    Background = new ImageBrush
                    {
                        ImageSource = new BitmapImage(new Uri(fname, UriKind.Relative)),
                        Stretch = Stretch.Uniform
                    }
                };
                button.Click += Button_Icon_Click;
                iconButtonImageSource.Add(button);
            }
            BindingOperations.SetBinding(this.KinectRegion, KinectRegion.KinectSensorProperty, regionSensorBinding);
            this.updatePagingButtonState();
            ScrollViewer.ScrollChanged += (o, ev) => this.updatePagingButtonState();
            beginStartStoryboard();
        }

        /// <summary>
        /// Handles the depth/color/body index frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_MultiSourceFrameArrived (object sender, MultiSourceFrameArrivedEventArgs e)
        {
            int depthWidth = 0;
            int depthHeight = 0;

            DepthFrame depthFrame = null;
            ColorFrame colorFrame = null;
            BodyIndexFrame bodyIndexFrame = null;
            bool isBitmapLocked = false;
            bool bodyIndexFrameProcessed = false;
            MultiSourceFrame multiSourceFrame = e.FrameReference.AcquireFrame();
            // If the Frame has expired by the time we process this event, return.
            if (multiSourceFrame == null)
            {
                return;
            }
            // We use a try/finally to ensure that we clean up before we exit the function.  
            // This includes calling Dispose on any Frame objects that we may have and unlocking the bitmap back buffer.
            try
            {
                depthFrame = multiSourceFrame.DepthFrameReference.AcquireFrame();
                colorFrame = multiSourceFrame.ColorFrameReference.AcquireFrame();
                bodyIndexFrame = multiSourceFrame.BodyIndexFrameReference.AcquireFrame();
                // If any frame has expired by the time we process this event, return.
                // The "finally" statement will Dispose any that are not null.
                if (( depthFrame == null ) || ( colorFrame == null ) || ( bodyIndexFrame == null ))
                {
                    return;
                }
                // Process Depth
                FrameDescription depthFrameDescription = depthFrame.FrameDescription;
                depthWidth = depthFrameDescription.Width;
                depthHeight = depthFrameDescription.Height;
                // Access the depth frame data directly via LockImageBuffer to avoid making a copy
                using (KinectBuffer depthFrameData = depthFrame.LockImageBuffer())
                {
                    this.coordinateMapper.MapColorFrameToDepthSpaceUsingIntPtr(
                        depthFrameData.UnderlyingBuffer,
                        depthFrameData.Size,
                        this.colorMappedToDepthPoints);
                }
                // We're done with the DepthFrame 
                depthFrame.Dispose();
                depthFrame = null;
                // Process Color
                // Lock the bitmap for writing
                this.bitmap.Lock();
                isBitmapLocked = true;
                colorFrame.CopyConvertedFrameDataToIntPtr(this.bitmap.BackBuffer, this.bitmapBackBufferSize, ColorImageFormat.Bgra);
                // We're done with the ColorFrame 
                colorFrame.Dispose();
                colorFrame = null;
                // We'll access the body index data directly to avoid a copy
                using (KinectBuffer bodyIndexData = bodyIndexFrame.LockImageBuffer())
                {
                    if (( ( this.bodyIndexFrameDescription.Width * this.bodyIndexFrameDescription.Height ) == bodyIndexData.Size ) &&
                            ( this.bodyIndexFrameDescription.Width == this.bodyIndexBitmap.PixelWidth ) && ( this.bodyIndexFrameDescription.Height == this.bodyIndexBitmap.PixelHeight ))
                    {
                        this.ProcessBodyIndexFrameData(bodyIndexData.UnderlyingBuffer, bodyIndexData.Size);
                        bodyIndexFrameProcessed = true;
                    }
                    unsafe
                    {
                        byte* bodyIndexDataPointer = (byte*)bodyIndexData.UnderlyingBuffer;
                        int colorMappedToDepthPointCount = this.colorMappedToDepthPoints.Length;
                        fixed (DepthSpacePoint* colorMappedToDepthPointsPointer = this.colorMappedToDepthPoints)
                        {
                            // Treat the color data as 4-byte pixels
                            uint* bitmapPixelsPointer = (uint*)this.bitmap.BackBuffer;
                            // Loop over each row and column of the color image
                            // Zero out any pixels that don't correspond to a body index
                            for (int colorIndex = 0; colorIndex < colorMappedToDepthPointCount; ++colorIndex)
                            {
                                float colorMappedToDepthX = colorMappedToDepthPointsPointer[colorIndex].X;
                                float colorMappedToDepthY = colorMappedToDepthPointsPointer[colorIndex].Y;
                                // The sentinel value is -inf, -inf, meaning that no depth pixel corresponds to this color pixel.
                                if (!float.IsNegativeInfinity(colorMappedToDepthX) &&
                                    !float.IsNegativeInfinity(colorMappedToDepthY))
                                {
                                    // Make sure the depth pixel maps to a valid point in color space
                                    int depthX = (int)( colorMappedToDepthX + 0.5f );
                                    int depthY = (int)( colorMappedToDepthY + 0.5f );
                                    // If the point is not valid, there is no body index there.
                                    if (( depthX >= 0 ) && ( depthX < depthWidth ) && ( depthY >= 0 ) && ( depthY < depthHeight ))
                                    {
                                        int depthIndex = ( depthY * depthWidth ) + depthX;
                                        // If we are tracking a body for the current pixel, do not zero out the pixel
                                        if (bodyIndexDataPointer[depthIndex] != 0xff)
                                        {
                                            continue;
                                        }
                                    }
                                }
                                bitmapPixelsPointer[colorIndex] = 0;
                            }
                        }
                        this.bitmap.AddDirtyRect(new Int32Rect(0, 0, this.bitmap.PixelWidth, this.bitmap.PixelHeight));
                    }
                }
            }
            finally
            {
                if (isBitmapLocked)
                {
                    this.bitmap.Unlock();
                }
                if (bodyIndexFrameProcessed)
                {
                    this.RenderBodyIndexPixels();
                }
                if (depthFrame != null)
                {
                    depthFrame.Dispose();
                }
                if (colorFrame != null)
                {
                    colorFrame.Dispose();
                }
                if (bodyIndexFrame != null)
                {
                    bodyIndexFrame.Dispose();
                }
            }
        }

        /// <summary>
        /// Directly accesses the underlying image buffer of the BodyIndexFrame to 
        /// create a displayable bitmap.
        /// This function requires the /unsafe compiler option as we make use of direct
        /// access to the native memory pointed to by the bodyIndexFrameData pointer.
        /// </summary>
        /// <param name="bodyIndexFrameData">Pointer to the BodyIndexFrame image data</param>
        /// <param name="bodyIndexFrameDataSize">Size of the BodyIndexFrame image data</param>
        private unsafe void ProcessBodyIndexFrameData (IntPtr bodyIndexFrameData, uint bodyIndexFrameDataSize)
        {
            byte* frameData = (byte*)bodyIndexFrameData;

            // convert body index to a visual representation
            for (int i = 0; i < (int)bodyIndexFrameDataSize; ++i)
            {
                // the BodyColor array has been sized to match
                // BodyFrameSource.BodyCount
                if (frameData[i] < BodyColor.Length)
                {
                    // this pixel is part of a player,
                    // display the appropriate color
                    this.bodyIndexPixels[i] = BodyColor[frameData[i]];
                }
                else
                {
                    // this pixel is not part of a player
                    // display black
                    this.bodyIndexPixels[i] = 0x00000000;
                }
            }
        }

        /// <summary>
        /// Renders color pixels into the writeableBitmap.
        /// </summary>
        private void RenderBodyIndexPixels ()
        {
            this.bodyIndexBitmap.WritePixels(
                new Int32Rect(0, 0, this.bodyIndexBitmap.PixelWidth, this.bodyIndexBitmap.PixelHeight),
                this.bodyIndexPixels,
                this.bodyIndexBitmap.PixelWidth * (int)BytesPerPixel,
                0);
        }

        private void postToFacebook (byte[] filebytes)
        {
            var access_token = "CAAEsLB43rPQBABmSsrP2XfrGGMYvdQIpM5F0QhNN79VW4CJhsGQL43oQjvSrldM3xZClS0amynVDUGe3zSx50cIOOnEJFbnZBeuiuNhYGS4OqZCpFHNqGZCW37aDm1GhV07dXYX12a7jhf2QxgOAZAfdxbmPH458RPk1lkZBoO54pudvhVIqCB";
            FacebookClient fb = new FacebookClient(access_token);
            FacebookMediaObject media = new FacebookMediaObject();
            media.ContentType = "image/jpeg";
            media.FileName = "test.jpg";
            media.SetValue(filebytes);
            Dictionary<string, object> upload = new Dictionary<string, object>();
            upload.Add("message", "Kinect拍拍熱騰騰的相片哦！");
            upload.Add("no_story", "1");
            upload.Add("access_token", access_token);
            upload.Add("@file.jpg", media);
            try
            {
                fb.Post("/764952110260957" + "/photos", upload);
            }
            catch (Exception) { }
        }

        private void beginStartStoryboard ()
        {
            Storyboard storyBoard = (Storyboard)this.Resources["StartStoryboard"];
            Storyboard.SetTarget(storyBoard.Children.ElementAt(0) as DoubleAnimation, Grid_StartPage);
            Storyboard.SetTarget(storyBoard.Children.ElementAt(1) as DoubleAnimation, Grid_StartTitle);
            Storyboard.SetTarget(storyBoard.Children.ElementAt(2) as DoubleAnimation, Grid_StartTitle);
            Storyboard.SetTarget(storyBoard.Children.ElementAt(3) as DoubleAnimation, Button_Start);
            storyBoard.Completed += storyBoard_Completed;
            storyBoard.Begin();
        }

        private void storyBoard_Completed (object sender, EventArgs e)
        {
            foreach (var button in backgroundButtonImageSource)
            {
                WrapPanel.Children.Add(button);
            }
            Button_Start.IsHitTestVisible = true;
        }

        private void Button_Start_Click (object sender, RoutedEventArgs e)
        {
            scrollViewerVisible = false;
            Grid_MainPage.Visibility = Visibility.Visible;
            Grid_MainPage.IsHitTestVisible = true;
            Storyboard storyBoard = (Storyboard)this.Resources["EnterMainPageStoryboard"];
            Storyboard.SetTarget(storyBoard.Children.ElementAt(0) as DoubleAnimation, Grid_StartPage);
            Storyboard.SetTarget(storyBoard.Children.ElementAt(1) as DoubleAnimation, Grid_MainPage);
            storyBoard.Completed += (se, ev) =>
            {
                Grid_StartPage.Visibility = Visibility.Collapsed;
                Image_BackgroundRemoval.Visibility = Visibility.Visible;
                Image_UserView.Visibility = Visibility.Visible;
            };
            storyBoard.Begin();
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
        /// 處理視窗關閉執行緒
        /// </summary>
        /// <param name="sender">事件發送對象的物件</param>
        /// <param name="e">事件參數</param>
        private void windowClosing (object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (this.multiFrameSourceReader != null)
            {
                this.multiFrameSourceReader.Dispose();
                this.multiFrameSourceReader = null;
            }
            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }
        }

        private void Button_Screenshot_Click (object sender, RoutedEventArgs e)
        {
            if (scrollViewerVisible)
            {
                setBackgroundPanelVisible(false);
                setIconPanelVisible(false);
            }
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
            Storyboard.SetTargetProperty(changeBackgroundAnimation, new PropertyPath(ScrollViewer.HeightProperty));
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

        private void setIconPanelVisible (bool visible)
        {
            DoubleAnimation changeBackgroundAnimation = new DoubleAnimation();
            QuarticEase easingFunction = new QuarticEase();
            Storyboard storyBoard = new Storyboard();
            Storyboard.SetTargetName(changeBackgroundAnimation, "ScrollViewer");
            Storyboard.SetTargetProperty(changeBackgroundAnimation, new PropertyPath(ScrollViewer.HeightProperty));
            changeBackgroundAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.4));
            changeBackgroundAnimation.EasingFunction = easingFunction;
            easingFunction.EasingMode = EasingMode.EaseOut;
            storyBoard.Children.Add(changeBackgroundAnimation);
            if (!visible)
            {
                changeBackgroundAnimation.From = 200;
                changeBackgroundAnimation.To = 0;
                Button_AddIcon.Content = "新增貼圖";
                scrollViewerVisible = false;
            }
            else
            {
                changeBackgroundAnimation.From = 0;
                changeBackgroundAnimation.To = 200;
                Button_AddIcon.Content = "隱藏面板";
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
                var colorBrush = new VisualBrush(Image_BackgroundRemoval);
                dc.DrawRectangle(colorBrush, null, new Rect(new Point(), new Size(colorWidth, colorHeight)));
                var iconBrush = new VisualBrush(canvas);
                dc.DrawRectangle(iconBrush, null, new Rect(new Point(), new Size(colorWidth, colorHeight)));
            }
            renderBitmap.Render(dv);
            Image_Result.Source = renderBitmap;
            saveBitmapToLocal(renderBitmap);
            var resultBitmap = convertImageToByte(renderBitmap);
            drawQrCode("https://www.facebook.com/pages/Kinect%E6%8B%8D%E6%8B%8D/764952110260957?sk=photos_stream", Image_FBQrCode);
            await Task.Run(() => postToFacebook(resultBitmap));
            //drawQrCode(await getImageUrlAsync(await postImageHttpWebRequsetAsync(convertImageToByte(renderBitmap))), Image_QrCode);
            beginShowResultStoryboard();
        }

        private void saveBitmapToLocal (RenderTargetBitmap renderBitmap)
        {
            BitmapEncoder encoder = new JpegBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(renderBitmap));
            string time = System.DateTime.Now.ToString("yyyy'-'MM'-'dd'-'hh'-'mm'-'ss");
            string dirPath = @"Photo";
            string path = System.IO.Path.Combine(@"Photo/", "KinectPiPi-" + time + ".jpg");
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
                    string.Format(Properties.Resources.FailedSaveBitmapText, path);
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
                    string.Format(Properties.Resources.FailedSaveBitmapText, path);
                }
            }  
        }

        private void drawQrCode (string text, Image image)
        {
            QrEncoder qrEncoder = new QrEncoder(ErrorCorrectionLevel.M);
            QrCode qrCode;
            qrEncoder.TryEncode(text, out qrCode);
            WriteableBitmap wBitmap = new WriteableBitmap(120, 120, 96, 96, PixelFormats.Gray8, null);
            new WriteableBitmapRenderer(new FixedModuleSize(2, QuietZoneModules.Two)).Draw(wBitmap, qrCode.Matrix);
            image.Source = wBitmap;
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
            Storyboard.SetTarget(storyBoard.Children.ElementAt(2) as DoubleAnimation, Button_AddIcon);
            Storyboard.SetTarget(storyBoard.Children.ElementAt(3) as DoubleAnimation, Button_ChangeBackground);
            Storyboard.SetTarget(storyBoard.Children.ElementAt(4) as DoubleAnimation, Button_ClearIcon);
            Storyboard.SetTarget(storyBoard.Children.ElementAt(5) as DoubleAnimation, Image_QrCode);
            Storyboard.SetTarget(storyBoard.Children.ElementAt(6) as DoubleAnimation, Image_FBQrCode);
            Storyboard.SetTarget(storyBoard.Children.ElementAt(7) as DoubleAnimation, Button_BrowseFanPage);
            Storyboard.SetTarget(storyBoard.Children.ElementAt(8) as DoubleAnimation, Button_Again);
            storyBoard.Completed += (se, ev) => { ProgressRing.IsActive = false; Grid_Opaque.IsHitTestVisible = false; };
            storyBoard.Begin();
        }

        private void Button_ChangeBackground_Click (object sender, RoutedEventArgs e)
        {
            if (scrollViewerVisible && !isBackgroundScrollViewer)
            {
                setIconPanelVisible(false);
            }
            if (!isBackgroundScrollViewer)
            {
                isBackgroundScrollViewer = true;
                WrapPanel.Children.Clear();
                foreach (var button in backgroundButtonImageSource)
                {
                    WrapPanel.Children.Add(button);
                }
            }
            if (scrollViewerVisible)
                setBackgroundPanelVisible(false);
            else
                setBackgroundPanelVisible(true);
        }

        private void Button_Again_Click(object sender, RoutedEventArgs e)
        {
            Storyboard storyBoard = (Storyboard)this.Resources["AgainStoryboard"];
            Storyboard.SetTarget(storyBoard.Children.ElementAt(0) as DoubleAnimation, Button_Again);
            Storyboard.SetTarget(storyBoard.Children.ElementAt(1) as DoubleAnimation, Button_BrowseFanPage);
            Storyboard.SetTarget(storyBoard.Children.ElementAt(2) as DoubleAnimation, Image_QrCode);
            Storyboard.SetTarget(storyBoard.Children.ElementAt(3) as DoubleAnimation, Image_FBQrCode);
            Storyboard.SetTarget(storyBoard.Children.ElementAt(4) as DoubleAnimation, Button_Screenshot);
            Storyboard.SetTarget(storyBoard.Children.ElementAt(5) as DoubleAnimation, Button_AddIcon);
            Storyboard.SetTarget(storyBoard.Children.ElementAt(6) as DoubleAnimation, Button_ClearIcon);
            Storyboard.SetTarget(storyBoard.Children.ElementAt(7) as DoubleAnimation, Button_ChangeBackground);
            storyBoard.Begin();
            Image_Result.Source = null;
            canvas.Children.Clear();
        }

        private void Button_Image_Click (object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            Image_Background.Source = new BitmapImage(new Uri(button.Tag as string, UriKind.Relative));
        }

        private void Button_AddIcon_Click (object sender, RoutedEventArgs e)
        {
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
            if (scrollViewerVisible)
                setIconPanelVisible(false);
            else
                setIconPanelVisible(true);
        }

        private void Button_Icon_Click (object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            ImageSource source = new BitmapImage(new Uri(button.Tag as string, UriKind.Relative));
            var icon = new DraggableElement();
            icon.Child = new Image { Source = source };
            Canvas.SetLeft(icon, canvas.ActualWidth / 2 - source.Width / 2);
            Canvas.SetTop(icon, canvas.ActualHeight / 2 - source.Height / 2);
            canvas.Children.Add(icon);
        }

        private void CanvasMouseLeftButtonDown (object sender, MouseButtonEventArgs e)
        {
            var image = e.Source as Image;
            if (image != null)
            {
                var draggableElement = image.Parent as DraggableElement;
                if (draggableElement != null && canvas.CaptureMouse())
                {
                    mousePosition = e.GetPosition(canvas);
                    draggedElement = draggableElement;
                    Panel.SetZIndex(draggedElement, 1);
                }
            }
        }

        private void CanvasMouseLeftButtonUp (object sender, MouseButtonEventArgs e)
        {
            if (draggedElement != null)
            {
                canvas.ReleaseMouseCapture();
                Panel.SetZIndex(draggedElement, 0);
                draggedElement = null;
            }
        }

        private void CanvasMouseMove (object sender, MouseEventArgs e)
        {
            if (draggedElement != null)
            {
                var position = e.GetPosition(canvas);
                var offset = position - mousePosition;
                mousePosition = position;
                var X = Canvas.GetLeft(draggedElement) + offset.X;
                if (X < 0)
                    X = 0;
                if (X > canvas.ActualWidth - draggedElement.ActualWidth)
                    X = canvas.ActualWidth - draggedElement.ActualWidth;
                var Y = Canvas.GetTop(draggedElement) + offset.Y;
                if (Y < 0)
                    Y = 0;
                if (Y > canvas.ActualHeight - draggedElement.ActualHeight)
                    Y = canvas.ActualHeight - draggedElement.ActualHeight;
                Canvas.SetLeft(draggedElement, X);
                Canvas.SetTop(draggedElement, Y);
            }
        }

        private void Button_ClearIcon_Click (object sender, RoutedEventArgs e)
        {
            canvas.Children.Clear();
        }

        private void Button_BrowseFanPage_Click (object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://www.facebook.com/pages/Kinect%E6%8B%8D%E6%8B%8D/764952110260957?sk=photos_stream"));
            e.Handled = true;
        }
    }
}
