using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Kinect;
using Microsoft.Kinect.Wpf.Controls;

namespace Funshot
{
    public class KinectHandler
    {
        public KinectSensor kinectSensor;
        public MultiSourceFrameReader multiFrameSourceReader;
        public ImageSource RemovalImageSource { get { return removalBitmap; } }
        public ImageSource BodyIndexImageSource { get { return bodyIndexBitmap; } }

        private static readonly uint[] BodyColor =
        {
            0x0000FF00,
            0x00FF0000,
            0xFFFF4000,
            0x40FFFF00,
            0xFF40FF00,
            0xFF808000,
        };
        private FrameDescription bodyIndexFrameDescription;
        private uint[] bodyIndexPixels;
        private const int BytesPerPixel = 4;
        private readonly int bytesPerPixel = (PixelFormats.Bgr32.BitsPerPixel + 7) / 8;
        private CoordinateMapper coordinateMapper;
        private uint bitmapBackBufferSize = 0;
        private DepthSpacePoint[] colorMappedToDepthPoints;
        private WriteableBitmap removalBitmap;
        private WriteableBitmap bodyIndexBitmap;

        public KinectHandler()
        {
            kinectSensor = KinectSensor.GetDefault();
            multiFrameSourceReader = kinectSensor.OpenMultiSourceFrameReader(FrameSourceTypes.Depth | FrameSourceTypes.Color | FrameSourceTypes.BodyIndex);
            multiFrameSourceReader.MultiSourceFrameArrived += Reader_MultiSourceFrameArrived;
            coordinateMapper = kinectSensor.CoordinateMapper;
            FrameDescription depthFrameDescription = kinectSensor.DepthFrameSource.FrameDescription;
            var depthWidth = depthFrameDescription.Width;
            var depthHeight = depthFrameDescription.Height;
            FrameDescription colorFrameDescription = kinectSensor.ColorFrameSource.FrameDescription;
            var colorWidth = colorFrameDescription.Width;
            var colorHeight = colorFrameDescription.Height;
            colorMappedToDepthPoints = new DepthSpacePoint[colorWidth * colorHeight];
            removalBitmap = new WriteableBitmap(colorWidth, colorHeight, 96.0, 96.0, PixelFormats.Bgra32, null);
            bodyIndexFrameDescription = kinectSensor.BodyIndexFrameSource.FrameDescription;
            bodyIndexPixels = new uint[bodyIndexFrameDescription.Width * bodyIndexFrameDescription.Height];
            bodyIndexBitmap = new WriteableBitmap(bodyIndexFrameDescription.Width, bodyIndexFrameDescription.Height, 96.0, 96.0, PixelFormats.Bgr32, null);
            bitmapBackBufferSize = (uint)((removalBitmap.BackBufferStride * (removalBitmap.PixelHeight - 1)) + (removalBitmap.PixelWidth * bytesPerPixel));
            kinectSensor.Open();
        }

        public void setKinectRegionBinding(ref KinectRegion reg)
        {
            BindingOperations.SetBinding(reg, KinectRegion.KinectSensorProperty, new Binding("Kinect") { Source = kinectSensor });
        }

        private void Reader_MultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            var depthWidth = 0;
            var depthHeight = 0;
            DepthFrame depthFrame = null;
            ColorFrame colorFrame = null;
            BodyIndexFrame bodyIndexFrame = null;
            bool isBitmapLocked = false;
            bool bodyIndexFrameProcessed = false;
            MultiSourceFrame multiSourceFrame = e.FrameReference.AcquireFrame();
            if (multiSourceFrame == null)
                return;
            try
            {
                depthFrame = multiSourceFrame.DepthFrameReference.AcquireFrame();
                colorFrame = multiSourceFrame.ColorFrameReference.AcquireFrame();
                bodyIndexFrame = multiSourceFrame.BodyIndexFrameReference.AcquireFrame();
                if ((depthFrame == null) || (colorFrame == null) || (bodyIndexFrame == null))
                    return;
                FrameDescription depthFrameDescription = depthFrame.FrameDescription;
                depthWidth = depthFrameDescription.Width;
                depthHeight = depthFrameDescription.Height;
                using (KinectBuffer depthFrameData = depthFrame.LockImageBuffer())
                {
                    coordinateMapper.MapColorFrameToDepthSpaceUsingIntPtr(
                        depthFrameData.UnderlyingBuffer,
                        depthFrameData.Size,
                        colorMappedToDepthPoints);
                }
                depthFrame.Dispose();
                depthFrame = null;
                removalBitmap.Lock();
                isBitmapLocked = true;
                colorFrame.CopyConvertedFrameDataToIntPtr(removalBitmap.BackBuffer, bitmapBackBufferSize, ColorImageFormat.Bgra);
                colorFrame.Dispose();
                colorFrame = null;
                using (KinectBuffer bodyIndexData = bodyIndexFrame.LockImageBuffer())
                {
                    if (((bodyIndexFrameDescription.Width * bodyIndexFrameDescription.Height) == bodyIndexData.Size) &&
                            (bodyIndexFrameDescription.Width == bodyIndexBitmap.PixelWidth) && (bodyIndexFrameDescription.Height == bodyIndexBitmap.PixelHeight))
                    {
                        ProcessBodyIndexFrameData(bodyIndexData.UnderlyingBuffer, bodyIndexData.Size);
                        bodyIndexFrameProcessed = true;
                    }
                    unsafe
                    {
                        byte* bodyIndexDataPointer = (byte*)bodyIndexData.UnderlyingBuffer;
                        int colorMappedToDepthPointCount = colorMappedToDepthPoints.Length;
                        fixed (DepthSpacePoint* colorMappedToDepthPointsPointer = colorMappedToDepthPoints)
                        {
                            var bitmapPixelsPointer = (uint*)removalBitmap.BackBuffer;
                            for (int colorIndex = 0; colorIndex < colorMappedToDepthPointCount; ++colorIndex)
                            {
                                var colorMappedToDepthX = colorMappedToDepthPointsPointer[colorIndex].X;
                                var colorMappedToDepthY = colorMappedToDepthPointsPointer[colorIndex].Y;
                                if (!float.IsNegativeInfinity(colorMappedToDepthX) && !float.IsNegativeInfinity(colorMappedToDepthY))
                                {
                                    var depthX = (int)(colorMappedToDepthX + 0.5f);
                                    var depthY = (int)(colorMappedToDepthY + 0.5f);
                                    if ((depthX >= 0) && (depthX < depthWidth) && (depthY >= 0) && (depthY < depthHeight))
                                    {
                                        var depthIndex = (depthY * depthWidth) + depthX;
                                        if (bodyIndexDataPointer[depthIndex] != 0xff)
                                            continue;
                                    }
                                }
                                bitmapPixelsPointer[colorIndex] = 0;
                            }
                        }
                        removalBitmap.AddDirtyRect(new Int32Rect(0, 0, removalBitmap.PixelWidth, removalBitmap.PixelHeight));
                    }
                }
            }
            finally
            {
                if (isBitmapLocked)
                {
                    removalBitmap.Unlock();
                }
                if (bodyIndexFrameProcessed)
                {
                    RenderBodyIndexPixels();
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

        private void RenderBodyIndexPixels()
        {
            bodyIndexBitmap.WritePixels(
                new Int32Rect(0, 0, bodyIndexBitmap.PixelWidth, bodyIndexBitmap.PixelHeight),
                bodyIndexPixels,
                bodyIndexBitmap.PixelWidth * BytesPerPixel,
                0);
        }

        private unsafe void ProcessBodyIndexFrameData(IntPtr bodyIndexFrameData, uint bodyIndexFrameDataSize)
        {
            byte* frameData = (byte*)bodyIndexFrameData;
            for (int i = 0; i < (int)bodyIndexFrameDataSize; ++i)
            {
                bodyIndexPixels[i] = (frameData[i] < BodyColor.Length) ? BodyColor[frameData[i]] : 0x00000000;
            }
        }
    }
}
