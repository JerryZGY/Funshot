using System.Windows.Controls;
using Microsoft.Kinect.Toolkit.Input;
using Microsoft.Kinect.Wpf.Controls;

namespace Funshot
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