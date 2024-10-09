using Hi3Helper.EncTool.Parser.Sleepy.Responses;

namespace Hi3Helper.EncTool.Parser.Sleepy
{
    public class SleepyFileInfoResult
    {
        public string BaseUrl { get; private set; }
        public SleepyFileInfo ReferenceFileInfo { get; private set; }
        public string RevisionStamp { get; private set; }

        internal SleepyFileInfoResult(string baseUrl, SleepyFileInfo referenceFileInfo, string revisionStamp)
        {
            BaseUrl = baseUrl;
            ReferenceFileInfo = referenceFileInfo;
            RevisionStamp = revisionStamp;
        }
    }
}
