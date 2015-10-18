namespace Funshot
{
    public interface IPage
    {
        void InitializeProperty();
        void EnterStory();
        void ExitStory(System.Action callback);
    }
}