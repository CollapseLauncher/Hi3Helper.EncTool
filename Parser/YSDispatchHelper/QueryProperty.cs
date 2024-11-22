using Hi3Helper.EncTool.Parser.AssetIndex;
using System.Collections.Generic;

#nullable enable
namespace Hi3Helper.EncTool.Parser.YSDispatchHelper
{
    public class QueryProperty
    {
        public string? GameServerName { get; set; }
        public string? ClientGameResURL { get; set; }
        public string? ClientDesignDataURL { get; set; }
        public string? ClientDesignDataSilURL { get; set; }
        public string? ClientAudioAssetsURL { get; set; }
        public uint AudioRevisionNum { get; set; }
        public uint DataRevisionNum { get; set; }
        public uint ResRevisionNum { get; set; }
        public uint SilenceRevisionNum { get; set; }
        public string? GameVersion { get; set; }
        public string? ChannelName { get; set; }
        public IEnumerable<PkgVersionProperties?>? ClientGameRes { get; set; }
        public PkgVersionProperties? ClientDesignData { get; set; }
        public PkgVersionProperties? ClientDesignDataSil { get; set; }
    }
}
