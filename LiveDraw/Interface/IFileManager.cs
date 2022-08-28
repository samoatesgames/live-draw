namespace AntFu7.LiveDraw.Interface
{
    public interface IFileManager
    {
        bool IsUnsaved();
        void QuickSave();
        void UserSave();
        void UserLoad();

        void UserExport(bool includeBackground);

        void ClearDirty();
        void SetDirty();
    }
}
