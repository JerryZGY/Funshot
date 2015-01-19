namespace KinectPiPi
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Microsoft.Kinect;
    using Microsoft.Kinect.Toolkit.BackgroundRemoval;

    /// <summary>
    /// 可追蹤的去背使用者類別
    /// </summary>
    internal class TrackableUser : IDisposable
    {
        /// <summary>
        /// 無效的追蹤編號
        /// </summary>
        public const int InvalidTrackingId = 0;

        /// <summary>
        /// 暫存的追蹤編號
        /// </summary>
        private int trackingId = InvalidTrackingId;

        /// <summary>
        /// 使用中的Kinect感測器
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// 此物件所用的去背影像串流
        /// </summary>
        private BackgroundRemovedColorStream backgroundRemovedColorStream;

        /// <summary>
        /// 由感測器所接收到的骨架陣列
        /// </summary>
        private Skeleton[] skeletonsNew;

        /// <summary>
        /// 追蹤中的骨架陣列
        /// </summary>
        private Skeleton[] skeletonsTracked;

        /// <summary>
        /// 圖片控制項
        /// </summary>
        private Image imageControl;

        /// <summary>
        /// 是否已處理結束
        /// </summary>
        private bool disposed;

        /// <summary>
        /// 實體化 <see cref="TrackableUser"/> 類別
        /// </summary>
        /// <param name="imageControl">去背影像所呈現之圖片控制項</param>
        public TrackableUser (Image imageControl)
        {
            this.imageControl = imageControl;
        }

        /// <summary>
        /// 解構化 <see cref="TrackableUser"/> 類別
        /// 當處理結束函式被呼叫時才進行解構化
        /// </summary>
        ~TrackableUser ()
        {
            this.Dispose(false);
        }

        /// <summary>
        /// 是否已追蹤此物件
        /// </summary>
        public bool IsTracked
        {
            get
            {
                return InvalidTrackingId != this.TrackingId;
            }
        }

        /// <summary>
        /// 設置追蹤編號，若無追蹤中的使用者，編號將設為無效
        /// </summary>
        public int TrackingId
        {
            get
            {
                return this.trackingId;
            }
            set
            {
                if (value != this.trackingId)
                {
                    if (null != this.backgroundRemovedColorStream)
                    {
                        if (InvalidTrackingId != value)
                        {
                            this.backgroundRemovedColorStream.SetTrackedPlayer(value);
                            this.Timestamp = DateTime.UtcNow;
                        }
                        else
                        {
                            this.imageControl.Visibility = Visibility.Hidden;
                            this.Timestamp = DateTime.MinValue;
                        }
                    }
                    this.trackingId = value;
                }
            }
        }

        /// <summary>
        /// 設置時間戳記，若無追蹤中的使用者，時間戳記將設為無效
        /// </summary>
        public DateTime Timestamp { get; private set; }

        /// <summary>
        /// 處理結束函式，將釋放此物件的所有資源
        /// </summary>
        public void Dispose ()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Kinect感測器改變函式，將設置所有應用參數並註冊事件
        /// </summary>
        /// <param name="oldSensor">上個Kinect感測器</param>
        /// <param name="newSensor">新的Kinect感測器</param>
        public void OnKinectSensorChanged (KinectSensor oldSensor, KinectSensor newSensor)
        {
            if (null != oldSensor)
            {
                oldSensor.AllFramesReady -= this.SensorAllFramesReady;
                this.backgroundRemovedColorStream.BackgroundRemovedFrameReady -= this.BackgroundRemovedFrameReadyHandler;
                this.backgroundRemovedColorStream.Dispose();
                this.backgroundRemovedColorStream = null;
                this.TrackingId = InvalidTrackingId;
            }
            this.sensor = newSensor;
            if (null != newSensor)
            {
                this.backgroundRemovedColorStream = new BackgroundRemovedColorStream(newSensor);
                this.backgroundRemovedColorStream.BackgroundRemovedFrameReady += this.BackgroundRemovedFrameReadyHandler;
                this.backgroundRemovedColorStream.Enable(newSensor.ColorStream.Format, newSensor.DepthStream.Format);
                newSensor.AllFramesReady += this.SensorAllFramesReady;
            }
        }

        /// <summary>
        /// 處理結束檢查函式，根據是否已處理結束來決定要不要釋放此物件的所有資源
        /// </summary>
        /// <param name="disposing">是否已處理結束</param>
        protected virtual void Dispose (bool disposing)
        {
            if (!this.disposed)
            {
                if (null != this.backgroundRemovedColorStream)
                {
                    this.backgroundRemovedColorStream.Dispose();
                }
                this.disposed = true;
            }
        }

        /// <summary>
        /// 複製骨架函式，從骨架影像中複製骨架
        /// </summary>
        /// <param name="skeletonFrame">骨架影像來源</param>
        private void CopyDataFromSkeletonFrame (SkeletonFrame skeletonFrame)
        {
            if (null == this.skeletonsNew)
            {
                this.skeletonsNew = new Skeleton[skeletonFrame.SkeletonArrayLength];
            }
            skeletonFrame.CopySkeletonDataTo(this.skeletonsNew);
        }

        /// <summary>
        /// 更新骨架函式，更新追蹤中使用者的骨架陣列
        /// </summary>
        /// <returns>如果追蹤中的使用者存在將回傳True，反之則回傳False</returns>
        private bool UpdateTrackedSkeletonsArray ()
        {
            bool isUserPresent = false;
            foreach (var skeleton in this.skeletonsNew)
            {
                if (skeleton.TrackingId == this.TrackingId)
                {
                    isUserPresent = true;
                    if (skeleton.TrackingState == SkeletonTrackingState.Tracked)
                    {
                        var temp = this.skeletonsTracked;
                        this.skeletonsTracked = this.skeletonsNew;
                        this.skeletonsNew = temp;
                    }
                    break;
                }
            }
            if (!isUserPresent)
            {
                this.TrackingId = TrackableUser.InvalidTrackingId;
            }
            return isUserPresent;
        }

        /// <summary>
        /// Kinect感測器影像已初始化事件
        /// </summary>
        /// <param name="sender">事件發送對象的物件</param>
        /// <param name="e">事件參數</param>
        private void SensorAllFramesReady (object sender, AllFramesReadyEventArgs e)
        {
            if (null == this.sensor || this.sensor != sender)
            {
                return;
            }
            try
            {
                if (this.IsTracked)
                {
                    using (var depthFrame = e.OpenDepthImageFrame())
                    {
                        if (null != depthFrame)
                        {
                            this.backgroundRemovedColorStream.ProcessDepth(depthFrame.GetRawPixelData(), depthFrame.Timestamp);
                        }
                    }
                    using (var colorFrame = e.OpenColorImageFrame())
                    {
                        if (null != colorFrame)
                        {
                            this.backgroundRemovedColorStream.ProcessColor(colorFrame.GetRawPixelData(), colorFrame.Timestamp);
                        }
                    }
                    using (var skeletonFrame = e.OpenSkeletonFrame())
                    {
                        if (null != skeletonFrame)
                        {
                            this.CopyDataFromSkeletonFrame(skeletonFrame);
                            bool isUserPresent = this.UpdateTrackedSkeletonsArray();
                            if (isUserPresent && null != this.skeletonsTracked)
                            {
                                this.backgroundRemovedColorStream.ProcessSkeleton(this.skeletonsTracked, skeletonFrame.Timestamp);
                            }
                        }
                    }
                }
            }
            catch (InvalidOperationException) { }
        }

        /// <summary>
        /// 去背影像已初始化事件，將去背後的影像串流以BGRA的格式繪製於圖片控制項中
        /// </summary>
        /// <param name="sender">事件發送對象的物件</param>
        /// <param name="e">事件參數</param>
        private void BackgroundRemovedFrameReadyHandler (object sender, BackgroundRemovedColorFrameReadyEventArgs e)
        {
            using (var backgroundRemovedFrame = e.OpenBackgroundRemovedColorFrame())
            {
                if (null != backgroundRemovedFrame && this.IsTracked)
                {
                    int width = backgroundRemovedFrame.Width;
                    int height = backgroundRemovedFrame.Height;
                    WriteableBitmap foregroundBitmap = this.imageControl.Source as WriteableBitmap;
                    if (null == foregroundBitmap || foregroundBitmap.PixelWidth != width || foregroundBitmap.PixelHeight != height)
                    {
                        foregroundBitmap = new WriteableBitmap(width, height, 96.0, 96.0, PixelFormats.Bgra32, null);
                        this.imageControl.Source = foregroundBitmap;
                    }
                    foregroundBitmap.WritePixels(new Int32Rect(0, 0, width, height), backgroundRemovedFrame.GetRawPixelData(), foregroundBitmap.PixelWidth * sizeof(int), 0);
                    this.imageControl.Visibility = Visibility.Visible;
                }
            }
        }
    }
}