namespace Hi3Helper.Preset
{
    public interface IAssetIndexSummary
    {
        string PrintSummary();
        long GetAssetSize();
        string GetRemoteURL();
        void SetRemoteURL(string url);
    }
}
