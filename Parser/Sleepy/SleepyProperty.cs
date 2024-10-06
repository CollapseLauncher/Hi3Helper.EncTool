﻿namespace Hi3Helper.EncTool.Parser.Sleepy
{
    public class SleepyProperty
    {
        internal string VersionString { get; init; }
        internal string ChannelString { get; init; }

        internal string DispatchUrl { get; init; }
        internal string DispatchQuery { get; init; }
        internal string GatewayName { get; init; }
        internal string GatewayQuery { get; init; }

        internal string SeedString { get; init; }
        internal SleepyBuildProperty BuildProperty { get; init; }

        private SleepyProperty(
            string versionString, string channelString, string dispatchUrl,
            string dispatchQuery, string gatewayName, string gatewayQuery,
            string seedString, SleepyBuildProperty buildProperty)
        {
            VersionString = versionString;
            ChannelString = channelString;

            DispatchUrl = dispatchUrl;
            DispatchQuery = dispatchQuery;
            GatewayName = gatewayName;
            GatewayQuery = gatewayQuery;

            SeedString = seedString;
            BuildProperty = buildProperty;
        }

        public static SleepyProperty Create(
            string versionString, string channelString, string dispatchUrl,
            string dispatchQuery, string gatewayName, string gatewayQuery,
            string seedString, SleepyBuildProperty buildProperty)
            => new SleepyProperty(
                versionString, channelString, dispatchUrl,
                dispatchQuery, gatewayName, gatewayQuery,
                seedString, buildProperty);
    }

    public class SleepyBuildProperty
    {
        public string BuildIdentity { get; init; }
        public string BuildArea { get; init; }

        public static SleepyBuildProperty Create(string buildIdentity, string buildArea)
        {
            return new SleepyBuildProperty
            {
                BuildIdentity = buildIdentity,
                BuildArea = buildArea
            };
        }
    }
}
