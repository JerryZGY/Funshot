using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Microsoft.Kinect.Toolkit.Input;
using Microsoft.Kinect.Wpf.Controls;
using MS.Internal.PresentationFramework;
using System;
using System.Windows.Automation.Peers;
using System.Windows.Media;

namespace 樂拍機
{
    public class DraggableElement : Decorator, IKinectControl
    {
        public IKinectController CreateController(IInputModel inputModel, KinectRegion kinectRegion)
        {
            return new DraggableElementController(inputModel, kinectRegion);
        }

        public bool IsManipulatable
        {
            get { return true; }
        }

        public bool IsPressable
        {
            get { return false; }
        }
    }
}
