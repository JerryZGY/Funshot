using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Kinect.Input;
using Microsoft.Kinect.Toolkit.Input;
using Microsoft.Kinect.Wpf.Controls;

namespace 樂拍機
{
    public class DraggableElementController : IKinectManipulatableController
    {
        private ManipulatableModel _inputModel;
        private KinectRegion _kinectRegion;
        private DraggableElement _dragDropElement;
        private bool _disposedValue;

        public DraggableElementController(IInputModel inputModel, KinectRegion kinectRegion)
        {
            _inputModel = inputModel as ManipulatableModel;
            _kinectRegion = kinectRegion;
            _dragDropElement = _inputModel.Element as DraggableElement;
            _inputModel.ManipulationStarted += OnManipulationStarted;
            _inputModel.ManipulationUpdated += OnManipulationUpdated;
            _inputModel.ManipulationCompleted += OnManipulationCompleted;
        }

        private void OnManipulationCompleted(object sender, KinectManipulationCompletedEventArgs kinectManipulationCompletedEventArgs) { }

        private void OnManipulationUpdated(object sender, KinectManipulationUpdatedEventArgs e)
        {
            var parent = _dragDropElement.Parent as Canvas;
            if (parent != null)
            {
                var d = e.Delta.Translation;
                var y = Canvas.GetTop(_dragDropElement);
                var x = Canvas.GetLeft(_dragDropElement);

                if (double.IsNaN(y)) y = 0;
                if (double.IsNaN(x)) x = 0;

                // Delta value is between 0.0 and 1.0 so they need to be scaled within the kinect region.
                var yD = d.Y*_kinectRegion.ActualHeight;
                var xD = d.X*_kinectRegion.ActualWidth;
                var X = x + xD;
                var Y = y + yD;
                if (X < 0)
                    X = 0;
                if (X > 760 - _dragDropElement.ActualWidth)
                    X = 760 - _dragDropElement.ActualWidth;
                if (Y < 0)
                    Y = 0;
                if (Y > 595 - _dragDropElement.ActualHeight)
                    Y = 595 - _dragDropElement.ActualHeight;
                Canvas.SetTop(_dragDropElement, Y);
                Canvas.SetLeft(_dragDropElement, X);
            }
        }

        private void OnManipulationStarted(object sender, KinectManipulationStartedEventArgs e)
        {

        }

        ManipulatableModel IKinectManipulatableController.ManipulatableInputModel
        {
            get { return _inputModel; }
        }

        FrameworkElement IKinectController.Element
        {
            get { return _inputModel.Element as FrameworkElement; }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                _kinectRegion = null;
                _inputModel = null;
                _dragDropElement = null;

                _inputModel.ManipulationStarted -= OnManipulationStarted;
                _inputModel.ManipulationUpdated -= OnManipulationUpdated;
                _inputModel.ManipulationCompleted -= OnManipulationCompleted;

                _disposedValue = true;
            }
        }

        void IDisposable.Dispose()
        {
            Dispose(true);
        }
    }
}