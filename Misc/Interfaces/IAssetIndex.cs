namespace Hi3Helper.Preset
{
    public interface IAssetIndexSummary<T> : IAssetIndexSummary, IAssetIndexCloneable<T>;

    public interface IAssetIndexSummary
    {
        string PrintSummary();
        long GetAssetSize();
        string GetRemoteURL();
        void SetRemoteURL(string url);
    }

    public interface IAssetIndexCloneable<T>
    {
        T Clone();
    }
}
