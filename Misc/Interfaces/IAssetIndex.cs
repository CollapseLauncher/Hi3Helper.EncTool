namespace Hi3Helper.Preset
{
    public interface IAssetIndexSummary<out T> : IAssetIndexSummary, IAssetIndexCloneable<T>;

    public interface IAssetIndexSummary
    {
        string PrintSummary();
        long GetAssetSize();
        string GetRemoteURL();
        void SetRemoteURL(string url);
    }

    public interface IAssetIndexCloneable<out T>
    {
        T Clone();
    }
}
