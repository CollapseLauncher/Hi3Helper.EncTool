using Hi3Helper.UABT;
using Hi3Helper.UABT.Binary;
using System;
using System.IO;
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global

namespace Hi3Helper.EncTool.Parser.AssetMetadata
{
    public static class XMFUtility
    {
        /// <summary>
        /// Gets the length of the version that can be received by the XMFParser
        /// </summary>
        public static int XMFVersionLength { get => XMFParser.VersioningLength; }

#nullable enable
        /// <summary>
        /// Get the version from XMF file
        /// </summary>
        /// <param name="xmfFs">The stream of the XMF data</param>
        /// <returns>
        /// If the path doesn't exist or the file is not a valid XMF, then it will return a <c>null</c>.
        /// Otherwise, return the version provided by the XMF file
        /// </returns>
        public static int[]? GetXMFVersion(Stream xmfFs)
        {
            try
            {
                using EndianBinaryReader reader = new(xmfFs, EndianType.LittleEndian, true);
                _ = reader.BaseStream.Read(new byte[XMFParser.SignatureLength + 4]);
                int[] versionXMF = XMFParser.ReadVersion(reader);
                return versionXMF;
            }
            catch (Exception e)
            {
                Console.WriteLine($"error while reading XMF file!\r\n{e}");
                return null;
            }
        }

        private static int[]? GetXMFVersion(string xmfPath)
        {
            FileInfo xmf = new(xmfPath);
            if (!xmf.Exists || xmf is { Exists: true, Length: < 0xFF }) return null;

            using FileStream xmfFS = xmf.OpenRead();
            return GetXMFVersion(xmfFS);
        }
#nullable restore

        /// <summary>
        /// Compares the version given from <c>versionBytes</c> with the one from xmfPath.
        /// </summary>
        /// <param name="xmfPath">The path of the XMF file to compare.</param>
        /// <param name="versionBytes">The <c>int</c> array of version to compare. The array length must be 4.</param>
        /// <param name="use4LengthArrayCompare">Use 4 length array compare instead of 3 length array</param>
        /// <returns>
        /// If the version contained in the XMF file matches the one from <c>versionBytes</c> or there's any fault, then return <c>false</c>.<br/>
        /// If it matches, then return <c>true</c>.
        /// </returns>
        public static (bool, int[]) CheckIfXMFVersionMatches(string xmfPath, ReadOnlySpan<int> versionBytes, bool use4LengthArrayCompare = false)
        {
            if (versionBytes.Length != XMFParser.VersioningLength) return (false, []);
            ReadOnlySpan<int> versionXMF = GetXMFVersion(xmfPath);

            return (versionXMF[0] == versionBytes[0] && versionXMF[1] == versionBytes[1] && versionXMF[2] == versionBytes[2] && (!use4LengthArrayCompare || versionXMF[3] == versionBytes[3]),
                    versionXMF.ToArray());
        }
    }
}
