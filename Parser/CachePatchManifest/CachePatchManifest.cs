using Hi3Helper.Data;
using Hi3Helper.UABT;
using Hi3Helper.UABT.Binary;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
#pragma warning disable IDE0130

// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
// ReSharper disable UnusedMember.Global
// ReSharper disable CollectionNeverQueried.Global

#nullable enable
namespace Hi3Helper.EncTool.Parser.AssetMetadata;

[StructLayout(LayoutKind.Sequential, Pack = 2)]
public struct CachePatchInfo
{
    public        string? OldFileName;
    private fixed byte    _oldFileHashSHA1[20];
    public unsafe Span<byte> OldFileHashSHA1
    {
        get
        {
            fixed (byte* p = _oldFileHashSHA1)
                return new Span<byte>(p, 20);
        }
    }

    public        string? NewFileName;
    private fixed byte    _newFileHashSHA1[20];
    private fixed byte    _newFileHashMD5[16];
    public unsafe Span<byte> NewFileHashSHA1
    {
        get
        {
            fixed (byte* p = _newFileHashSHA1)
                return new Span<byte>(p, 20);
        }
    }
    public unsafe Span<byte> NewFileHashMD5
    {
        get
        {
            fixed (byte* p = _newFileHashMD5)
                return new Span<byte>(p, 16);
        }
    }

    public        int  PatchFileSize;
    private fixed byte _patchFileHashMD5[16];
    public unsafe Span<byte> PatchFileHashMD5
    {
        get
        {
            fixed (byte* p = _patchFileHashMD5)
                return new Span<byte>(p, 16);
        }
    }

    public string PatchName => $"{HexTool.BytesToHexUnsafe(PatchFileHashMD5)}.patch";

    public CachePatchInfo(string oldFileName,
                          string newFileName,
                          string newFileHashMD5,
                          string patchFileHashMD5,
                          int    patchFileSize)
    {
        OldFileName = oldFileName;
        ReadOnlySpan<char> oldFileHashSHA1 = oldFileName.GetSplit(1, '_');
        HexTool.TryHexToBytesUnsafe(oldFileHashSHA1, OldFileHashSHA1);

        NewFileName = newFileName;
        ReadOnlySpan<char> newFileHashSHA1 = newFileName.GetSplit(1, '_');
        HexTool.TryHexToBytesUnsafe(newFileHashSHA1, NewFileHashSHA1);
        HexTool.TryHexToBytesUnsafe(newFileHashMD5,  NewFileHashMD5);

        PatchFileSize = patchFileSize;
        HexTool.TryHexToBytesUnsafe(patchFileHashMD5, PatchFileHashMD5);
    }
}

public class CachePatchManifest : IReadOnlyCollection<CachePatchInfo>
{
    public List<CachePatchInfo> PatchAsset { get; } = [];

    public CachePatchManifest(string filePath)
    {
        using FileStream fs = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        Initialize(fs);
    }

    public CachePatchManifest(Stream stream, bool disposeStream = false)
    {
        Initialize(stream);

        if (disposeStream)
        {
            stream.Dispose();
        }
    }

    private void Initialize(Stream stream)
    {
        // Initialize patch asset
        PatchAsset.Clear();

        // Initialize the stream into the endian reader
        using EndianBinaryReader reader = new(stream, EndianType.BigEndian, true);

        // Start deserializing
        DeserializeManifest(reader);
    }

    private void DeserializeManifest(EndianBinaryReader reader)
    {
        // Get asset count
        int count = reader.ReadInt32();
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 0);

        // Iterate the count and read the asset property
        for (uint i = 0; i < count; i++)
        {
            // Read values
            string oldName    = reader.ReadString();
            string newName    = reader.ReadString();
            string newHashMd5 = reader.ReadString();
            string patchMd5   = reader.ReadString();
            int    patchSize  = reader.ReadInt32();
            ArgumentOutOfRangeException.ThrowIfLessThan(patchSize, 0);

            // Add it to list
            PatchAsset.Add(new CachePatchInfo(oldName, newName, newHashMd5, patchMd5, patchSize));
        }
    }

    public IEnumerator<CachePatchInfo> GetEnumerator() => PatchAsset.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public int Count => PatchAsset.Count;
}
