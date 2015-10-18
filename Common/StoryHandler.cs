using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace Funshot
{
    public class StoryHandler
    {
        public static void SetChildren(FrameworkElement element, string storyName, params DependencyObject[] children)
        {
            Storyboard story = ((Storyboard)element.Resources[storyName]);
            for (int i = 0; i < children.Length; i++)
            {
                Storyboard.SetTarget(story.Children[i], children[i]);
            }
        }

        public static void Begin(FrameworkElement element, string storyName)
        {
            Storyboard story = ((Storyboard)element.Resources[storyName]);
            story.Begin();
        }

        public static void Begin(FrameworkElement element, string storyName, double beginTime)
        {
            Storyboard story = ((Storyboard)element.Resources[storyName]);
            story.BeginTime = TimeSpan.FromSeconds(beginTime);
            story.Begin();
        }

        public static void Begin(FrameworkElement element, string storyName, double beginTime, Action callback)
        {
            Storyboard story = ((Storyboard)element.Resources[storyName]);
            story.BeginTime = TimeSpan.FromSeconds(beginTime);
            story.Completed += (s, e) => callback();
            story.Begin();
        }

        public static void Begin(FrameworkElement element, string storyName, Action callback)
        {
            Storyboard story = ((Storyboard)element.Resources[storyName]);
            story.Completed += (s, e) => callback();
            story.Begin();
        }

        public static void BeginLoop(FrameworkElement element, params string[] storyNames)
        {
            foreach (var storyName in storyNames)
            {
                Storyboard story = ((Storyboard)element.Resources[storyName]);
                story.Begin();
            }
        }

        public static void Stop(FrameworkElement element, string storyName)
        {
            Storyboard story = ((Storyboard)element.Resources[storyName]);
            story.Stop();
        }
    }
}