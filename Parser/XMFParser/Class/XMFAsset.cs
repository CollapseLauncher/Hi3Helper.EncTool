using Hi3Helper.UABT;
using Hi3Helper.UABT.Binary;
using System;
using System.IO;
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global

namespace Hi3Helper.EncTool.Parser
{
    public class XMFAsset
    {
        private XMFBlock Parent { get; }

        /// <summary>
        /// This class contains some information about the asset inside the block file,
        /// including the size, name and offsets.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="end"></param>
        /// <param name="start"></param>
        /// <param name="parent"></param>
        internal XMFAsset(string name, uint end, uint start, XMFBlock parent)
        {
            Parent      = parent;
            Name        = name;
            OffsetEnd   = end;
            OffsetStart = start;
        }

        /// <summary>
        /// Gets a <c>ChunkStream</c> of the asset file with Read-only mode.<br/>
        /// This will give you direct access of Read operation within offsets of the asset file inside
        /// its block file.
        /// </summary>
        /// <param name="skipHeaderCheck">Skip UnityFS header check (default: false)</param>
        /// <returns>Stream of the asset file with Read-only mode.</returns>
        public ChunkStream GetAssetStreamOpenRead(bool skipHeaderCheck = false)
        {
            CheckParentFileAndThrow();

            if (!skipHeaderCheck) IsUnityFsHeaderValid();
            return new ChunkStream(Parent.GetBlockStream(FileAccess.Read), OffsetStart, OffsetEnd);
        }

        /// <summary>
        /// Gets a <c>ChunkStream</c> of the asset file with Write-only mode.<br/>
        /// This will give you direct access of Write operation within offsets of the asset file inside
        /// its block file.<br/><br/>
        /// However, you can't write data more than the given size.
        /// </summary>
        /// <param name="skipHeaderCheck">Skip UnityFS header check (default: false)</param>
        /// <returns>Stream of the asset file with Write-only mode.</returns>
        public ChunkStream GetAssetStreamOpenWrite(bool skipHeaderCheck = false)
        {
            CheckParentFileAndThrow();

            if (!skipHeaderCheck) IsUnityFsHeaderValid();
            return new ChunkStream(Parent.GetBlockStream(FileAccess.Write), OffsetStart, OffsetEnd);
        }

        /// <summary>
        /// Gets a <c>ChunkStream</c> of the asset file with Read/Write mode.<br/>
        /// This will give you direct access of Write operation within offsets of the asset file inside
        /// its block file.<br/><br/>
        /// However, you can't write data more than the given size.
        /// </summary>
        /// <param name="skipHeaderCheck">Skip UnityFS header check (default: false)</param>
        /// <returns>Stream of the asset file with Read/Write mode.</returns>
        public ChunkStream GetAssetStreamOpenReadWrite(bool skipHeaderCheck = false)
        {
            CheckParentFileAndThrow();

            if (!skipHeaderCheck) IsUnityFsHeaderValid();
            return new ChunkStream(Parent.GetBlockStream(FileAccess.ReadWrite), OffsetStart, OffsetEnd);
        }

        internal void SetOffsetEnd(long offsetEnd) => OffsetEnd = offsetEnd;

        private void CheckParentFileAndThrow()
        {
            if (!Parent.IsExist)
            {
                throw new FileNotFoundException($"Cannot create a stream because parent block file: {Parent.BlockName} doesn't exist!");
            }
            if (OffsetStart > Parent.Size || Parent.Size < OffsetEnd)
            {
                throw new FileLoadException($"Cannot create a stream because parent block file with hash: " +
                                            $"{Parent.BlockName} doesn't have a correct size or maybe corrupted!");
            }
        }

        private void IsUnityFsHeaderValid(long exSig = 0x53467974696E55)
        {
            using Stream             fs  = Parent.GetBlockStream(FileAccess.ReadWrite);
            using Stream             cs  = new ChunkStream(fs, OffsetStart, OffsetEnd);
            using EndianBinaryReader s   = new(cs, EndianType.LittleEndian);
            long                     sig = s.ReadInt64();
            if (sig != exSig)
            {
                throw new FormatException($"Asset file doesn't seem to be a valid UnityFS file! " +
                                          $"(Expecting: {exSig} but get: {sig} instead)");
            }
        }

        /// <summary>
        /// Gets a name of the asset file
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets an End Offset/Location of the asset file inside the block file.
        /// </summary>
        public long OffsetEnd { get; private set; }

        /// <summary>
        /// Gets a Start Offset/Location of the asset file inside the block file.
        /// </summary>
        public long OffsetStart { get; }

        /// <summary>
        /// Gets the size of the asset file inside the block file.
        /// </summary>
        public long Size { get => OffsetEnd - OffsetStart; }
    }
}
