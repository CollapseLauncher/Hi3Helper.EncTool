using Hi3Helper.UABT.Binary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Hi3Helper.EncTool.Parser.AssetMetadata
{
    public sealed class KianaAudioManifest
    {
        private const    byte                         RsaKeyL = 0x80;
        private const    byte                         AesKeyL = 0x20;
        private const    byte                         AesIvL  = 0x10;
        private readonly int[]                        _gameVersion;
        private          List<ManifestAudioPatchInfo> _audioPatches;

        public int[]                   ManifestVersion { get; private set; }
        public List<ManifestAssetInfo> AudioAssets     { get; set; }

        public KianaAudioManifest(string filePath, string key, int[] gameVersion)
        {
            _gameVersion = gameVersion;
            using FileStream fs = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            Initialize(fs, key);
        }

        public KianaAudioManifest(Stream stream, string key, int[] gameVersion, bool disposeStream = false)
        {
            _gameVersion = gameVersion;
            Initialize(stream, key);

            if (disposeStream)
            {
                stream.Dispose();
            }
        }

        public KianaAudioManifest(Stream stream, int[] gameVersion, bool disposeStream = false)
        {
            _gameVersion = gameVersion;
            Initialize(stream);

            if (disposeStream)
            {
                stream.Dispose();
            }
        }

        private void Initialize(Stream stream)
        {
            // Initialize the Raw Stream and reader
            EndianBinaryReader reader = new(stream);

            // Start deserializing
            DeserializeManifest(reader);
        }

        private void Initialize(Stream stream, string key)
        {
            // Initialize the CryptoStream and reader
            Stream cryptStream = GetCryptoStream(stream, key);
            EndianBinaryReader reader = new(cryptStream);

            // Start deserializing
            DeserializeManifest(reader);
        }

        private static Stream GetCryptoStream(Stream stream, string key)
        {
            // Initialize endian reader
            EndianBinaryReader endianReader = new(stream);

            // Get ICryptoTransform instance
            ICryptoTransform transform = GetAesInstance(endianReader, key);

            // Return CryptoStream instance
            return new CryptoStream(stream, transform, CryptoStreamMode.Read);
        }

        private static ICryptoTransform GetAesInstance(EndianBinaryReader reader, string key)
        {
            // Get the key length
            short keyLength = reader.ReadInt16();

            // Check if the key length is the same as RSA key length. If not, then throw.
            if (keyLength != RsaKeyL)
            {
                throw new FormatException($"This file is not a valid KianaManifest file! Expecting magic: {RsaKeyL} but got {keyLength} instead.");
            }

            // Get the key data
            byte[] keyData = reader.ReadBytes(keyLength);

            // Initialize RSA crypto and import the key
            RSA rsaDec = RSA.Create();
            rsaDec.FromXmlString(key);

            // Decrypt the key with Pkcs1 padding then assign the AES Key and IV
            byte[] aesData = rsaDec.Decrypt(keyData, RSAEncryptionPadding.Pkcs1);
            byte[] aesKey = new byte[AesKeyL];
            byte[] aesIv = new byte[AesIvL];

            // Copy the key buffer into its own Key and IV
            Array.Copy(aesData, aesKey, AesKeyL);
            Array.Copy(aesData, AesKeyL, aesIv, 0, AesIvL);

            // Create AES instance and return the ICryptoTransform
            Aes ret = Aes.Create();
            ret.Key = aesKey;
            ret.IV = aesIv;
            return ret.CreateDecryptor();
        }

        private void DeserializeManifest(EndianBinaryReader reader)
        {
            // Get manifest version
            ManifestVersion =
            [
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32()
            ];

            // Check if the game version and manifest version is match. If not, then throw
            if (!ManifestVersion.AsSpan()[..2].SequenceEqual(_gameVersion.AsSpan()[..2]))
            {
                throw new FormatException($"Manifest version is not the same as game version! Game Version: {string.Join('.', _gameVersion)} <--> Manifest Version: {string.Join('.', ManifestVersion)}");
            }

            // Deserialize Assets
            AudioAssets = DeserializeAsset(reader);
            _audioPatches = DeserializePatchesInfo(reader);

            // Assign for the PatchInfo in ManifestAssetInfo
            TryAssignPatchInfo();
        }

        private static List<ManifestAssetInfo> DeserializeAsset(EndianBinaryReader reader)
        {
            // Get assets count
            uint assetsCount = reader.ReadUInt32();

#if DEBUG
            // Print the assets count
            Console.WriteLine($"Audio assets count: {assetsCount}");
#endif

            // Initialize the List return
            List<ManifestAssetInfo> ret = [];

            // Iterate the ManifestAssetInfo List
            for (uint i = 0; i < assetsCount; i++)
            {
                // Read ManifestAssetInfo and add it
                ret.Add(ReadAssetInfo(reader));
            }

            // Return the List
            return ret;
        }

        private static ManifestAssetInfo ReadAssetInfo(EndianBinaryReader reader)
        {
            // Read data
            string name = reader.ReadString();
            string path = reader.ReadString();

            byte[] hash = XMFBlock.TryReadMD5HashOrOther64(reader);

            int               size        = reader.ReadInt32();
            int               languageInt = reader.ReadInt32();
            int               pckTypeInt  = reader.ReadInt32();
            AudioLanguageType language    = (AudioLanguageType)languageInt;
            AudioPCKType      pckType     = (AudioPCKType)pckTypeInt;
            bool              needMap     = reader.ReadBoolean();

#if DEBUG
            // Print the asset info
            Console.WriteLine($"    Asset: {path} -> [S: {size}] [L: {language} ({languageInt})] [T: {pckType} ({pckTypeInt})] [NeedMap?: {needMap}]");
#endif

            // Return the value
            return new ManifestAssetInfo
            {
                Name     = name,
                Path     = path,
                Hash     = hash,
                Size     = size,
                Language = language,
                PckType  = pckType,
                NeedMap  = needMap
            };
        }

        private static List<ManifestAudioPatchInfo> DeserializePatchesInfo(EndianBinaryReader reader)
        {
            // Get patches count
            uint patchesCount = reader.ReadUInt32();

#if DEBUG
            // Print the patches count
            Console.WriteLine($"Audio patches count: {patchesCount}");
#endif

            // Initialize the List return
            List<ManifestAudioPatchInfo> ret = [];

            // Iterate the ManifestAudioPatchInfo List
            for (uint i = 0; i < patchesCount; i++)
            {
                // Read ManifestAudioPatchInfo and add it
                ret.Add(ReadPatchInfo(reader));
            }

            // Return the List
            return ret;
        }

        private static ManifestAudioPatchInfo ReadPatchInfo(EndianBinaryReader reader)
        {
            // Read data
            string name          = reader.ReadString();
            string fileMd5       = reader.ReadString();
            string newFileMd5    = reader.ReadString();
            string patchFileMd5  = reader.ReadString();
            uint   patchFileSize = reader.ReadUInt32();

#if DEBUG
            // Print the patch info
            Console.WriteLine($"    Asset: {name}");
            Console.WriteLine($"        Asset Hash: {fileMd5}");
            Console.WriteLine($"        New Patched Asset Hash: {newFileMd5}");
            Console.WriteLine($"        Patch Filename (also as Hash): {patchFileMd5}.patch");
            Console.WriteLine($"        Patch File Size: {patchFileSize}");
#endif

            // Return the value
            return new ManifestAudioPatchInfo(name, fileMd5, newFileMd5, patchFileMd5, patchFileSize);
        }

        private void TryAssignPatchInfo()
        {
            // Get the AudioAssets List count
            int le = AudioAssets.Count;

            // Iterate the AudioAssets
            for (int i = 0; i < le; i++)
            {
                // Sidenote 2025/10/21:
                // Starting from version 8.x, the patch info has multiple entries with the same name.
                // The order is from the oldest to newest revision. For example:
                // 
                // Let's say that your game was updated from 8.4.0.0 to 8.5.0.0, and the audio have multiple patches
                // which consists of these revisions:
                //     - 8.4.0.0 -> 8.5.0.0
                //     - 8.5.0.0 -> 8.5.0.2
                //     - 8.5.0.2 -> 8.5.0.3
                // 
                // On PatchInfo field, the first revision will be selected.
                // But, on AllPatchInfo field, all the revisions will be stored in List form.
                // 
                // PatchInfo field will be preserved for backward compatibility with Old mechanism.
                // While on New mechanism, after the hash of the current file has been calculated, you need to lookup
                // which old hash is matching from AllPatchInfo, then apply the patch from that entry and
                // replace the value inside PatchInfo field with that entry.

                // Get the name and try to get the ManifestAudioPatchInfo
                string name = AudioAssets[i].Name + ".pck";
                ManifestAudioPatchInfo info = _audioPatches.FirstOrDefault(x => x.AudioFilename.Equals(name, StringComparison.OrdinalIgnoreCase));
                AudioAssets[i].AddPatchInfo(info?.AudioFilename == null ? null : info);
                AudioAssets[i].AllPatchInfo.AddRange(_audioPatches.Where(x => x.AudioFilename.Equals(name, StringComparison.OrdinalIgnoreCase)));
            }

            // Clean-up PatchInfo
            _audioPatches.Clear();
            _audioPatches = null;
        }
    }
}
