namespace AntFu7.LiveDraw.Interface
{
    public interface IDragManager
    {
        void StartDrag();
        void EndDrag();
        void OnMouseMove();

        void StoreLocation();
        void RestoreLocation();
    }
}
