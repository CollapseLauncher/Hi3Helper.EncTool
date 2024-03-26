// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: StarRailGateway.proto
// </auto-generated>
#pragma warning disable 1591, 0612, 3021, 8981
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace Hi3Helper.EncTool.Proto.StarRail {

  /// <summary>Holder for reflection information generated from StarRailGateway.proto</summary>
  public static partial class StarRailGatewayReflection {

    #region Descriptor
    /// <summary>File descriptor for StarRailGateway.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static StarRailGatewayReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "ChVTdGFyUmFpbEdhdGV3YXkucHJvdG8SIEhpM0hlbHBlci5FbmNUb29sLlBy",
            "b3RvLlN0YXJSYWlsItsBCg9TdGFyUmFpbEdhdGV3YXkSIwobQXNzZXRCdW5k",
            "bGVWZXJzaW9uVXBkYXRlVXJsGAwgASgJEiEKGUx1YUJ1bmRsZVZlcnNpb25V",
            "cGRhdGVVcmwYAyABKAkSFwoPTHVhUGF0Y2hWZXJzaW9uGAQgASgJEigKIERl",
            "c2lnbkRhdGFCdW5kbGVWZXJzaW9uVXBkYXRlVXJsGAYgASgJEiIKGUlGaXhQ",
            "YXRjaFZlcnNpb25VcGRhdGVVcmwYlgEgASgJEhkKEUlGaXhQYXRjaFJldmlz",
            "aW9uGA0gASgJYgZwcm90bzM="));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { },
          new pbr::GeneratedClrTypeInfo(null, null, new pbr::GeneratedClrTypeInfo[] {
            new pbr::GeneratedClrTypeInfo(typeof(global::Hi3Helper.EncTool.Proto.StarRail.StarRailGateway), global::Hi3Helper.EncTool.Proto.StarRail.StarRailGateway.Parser, new[]{ "AssetBundleVersionUpdateUrl", "LuaBundleVersionUpdateUrl", "LuaPatchVersion", "DesignDataBundleVersionUpdateUrl", "IFixPatchVersionUpdateUrl", "IFixPatchRevision" }, null, null, null, null)
          }));
    }
    #endregion

  }
  #region Messages
  [global::System.Diagnostics.DebuggerDisplayAttribute("{ToString(),nq}")]
  public sealed partial class StarRailGateway : pb::IMessage<StarRailGateway>
  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      , pb::IBufferMessage
  #endif
  {
    private static readonly pb::MessageParser<StarRailGateway> _parser = new pb::MessageParser<StarRailGateway>(() => new StarRailGateway());
    private pb::UnknownFieldSet _unknownFields;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pb::MessageParser<StarRailGateway> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::Hi3Helper.EncTool.Proto.StarRail.StarRailGatewayReflection.Descriptor.MessageTypes[0]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public StarRailGateway() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public StarRailGateway(StarRailGateway other) : this() {
      assetBundleVersionUpdateUrl_ = other.assetBundleVersionUpdateUrl_;
      luaBundleVersionUpdateUrl_ = other.luaBundleVersionUpdateUrl_;
      luaPatchVersion_ = other.luaPatchVersion_;
      designDataBundleVersionUpdateUrl_ = other.designDataBundleVersionUpdateUrl_;
      iFixPatchVersionUpdateUrl_ = other.iFixPatchVersionUpdateUrl_;
      iFixPatchRevision_ = other.iFixPatchRevision_;
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public StarRailGateway Clone() {
      return new StarRailGateway(this);
    }

    /// <summary>Field number for the "AssetBundleVersionUpdateUrl" field.</summary>
    public const int AssetBundleVersionUpdateUrlFieldNumber = 12;
    private string assetBundleVersionUpdateUrl_ = "";
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public string AssetBundleVersionUpdateUrl {
      get { return assetBundleVersionUpdateUrl_; }
      set {
        assetBundleVersionUpdateUrl_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }

    /// <summary>Field number for the "LuaBundleVersionUpdateUrl" field.</summary>
    public const int LuaBundleVersionUpdateUrlFieldNumber = 3;
    private string luaBundleVersionUpdateUrl_ = "";
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public string LuaBundleVersionUpdateUrl {
      get { return luaBundleVersionUpdateUrl_; }
      set {
        luaBundleVersionUpdateUrl_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }

    /// <summary>Field number for the "LuaPatchVersion" field.</summary>
    public const int LuaPatchVersionFieldNumber = 4;
    private string luaPatchVersion_ = "";
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public string LuaPatchVersion {
      get { return luaPatchVersion_; }
      set {
        luaPatchVersion_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }

    /// <summary>Field number for the "DesignDataBundleVersionUpdateUrl" field.</summary>
    public const int DesignDataBundleVersionUpdateUrlFieldNumber = 6;
    private string designDataBundleVersionUpdateUrl_ = "";
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public string DesignDataBundleVersionUpdateUrl {
      get { return designDataBundleVersionUpdateUrl_; }
      set {
        designDataBundleVersionUpdateUrl_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }

    /// <summary>Field number for the "IFixPatchVersionUpdateUrl" field.</summary>
    public const int IFixPatchVersionUpdateUrlFieldNumber = 150;
    private string iFixPatchVersionUpdateUrl_ = "";
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public string IFixPatchVersionUpdateUrl {
      get { return iFixPatchVersionUpdateUrl_; }
      set {
        iFixPatchVersionUpdateUrl_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }

    /// <summary>Field number for the "IFixPatchRevision" field.</summary>
    public const int IFixPatchRevisionFieldNumber = 13;
    private string iFixPatchRevision_ = "";
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public string IFixPatchRevision {
      get { return iFixPatchRevision_; }
      set {
        iFixPatchRevision_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override bool Equals(object other) {
      return Equals(other as StarRailGateway);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool Equals(StarRailGateway other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (AssetBundleVersionUpdateUrl != other.AssetBundleVersionUpdateUrl) return false;
      if (LuaBundleVersionUpdateUrl != other.LuaBundleVersionUpdateUrl) return false;
      if (LuaPatchVersion != other.LuaPatchVersion) return false;
      if (DesignDataBundleVersionUpdateUrl != other.DesignDataBundleVersionUpdateUrl) return false;
      if (IFixPatchVersionUpdateUrl != other.IFixPatchVersionUpdateUrl) return false;
      if (IFixPatchRevision != other.IFixPatchRevision) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override int GetHashCode() {
      int hash = 1;
      if (AssetBundleVersionUpdateUrl.Length != 0) hash ^= AssetBundleVersionUpdateUrl.GetHashCode();
      if (LuaBundleVersionUpdateUrl.Length != 0) hash ^= LuaBundleVersionUpdateUrl.GetHashCode();
      if (LuaPatchVersion.Length != 0) hash ^= LuaPatchVersion.GetHashCode();
      if (DesignDataBundleVersionUpdateUrl.Length != 0) hash ^= DesignDataBundleVersionUpdateUrl.GetHashCode();
      if (IFixPatchVersionUpdateUrl.Length != 0) hash ^= IFixPatchVersionUpdateUrl.GetHashCode();
      if (IFixPatchRevision.Length != 0) hash ^= IFixPatchRevision.GetHashCode();
      if (_unknownFields != null) {
        hash ^= _unknownFields.GetHashCode();
      }
      return hash;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override string ToString() {
      return pb::JsonFormatter.ToDiagnosticString(this);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void WriteTo(pb::CodedOutputStream output) {
    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      output.WriteRawMessage(this);
    #else
      if (LuaBundleVersionUpdateUrl.Length != 0) {
        output.WriteRawTag(26);
        output.WriteString(LuaBundleVersionUpdateUrl);
      }
      if (LuaPatchVersion.Length != 0) {
        output.WriteRawTag(34);
        output.WriteString(LuaPatchVersion);
      }
      if (DesignDataBundleVersionUpdateUrl.Length != 0) {
        output.WriteRawTag(50);
        output.WriteString(DesignDataBundleVersionUpdateUrl);
      }
      if (AssetBundleVersionUpdateUrl.Length != 0) {
        output.WriteRawTag(98);
        output.WriteString(AssetBundleVersionUpdateUrl);
      }
      if (IFixPatchRevision.Length != 0) {
        output.WriteRawTag(106);
        output.WriteString(IFixPatchRevision);
      }
      if (IFixPatchVersionUpdateUrl.Length != 0) {
        output.WriteRawTag(178, 9);
        output.WriteString(IFixPatchVersionUpdateUrl);
      }
      if (_unknownFields != null) {
        _unknownFields.WriteTo(output);
      }
    #endif
    }

    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    void pb::IBufferMessage.InternalWriteTo(ref pb::WriteContext output) {
      if (LuaBundleVersionUpdateUrl.Length != 0) {
        output.WriteRawTag(26);
        output.WriteString(LuaBundleVersionUpdateUrl);
      }
      if (LuaPatchVersion.Length != 0) {
        output.WriteRawTag(34);
        output.WriteString(LuaPatchVersion);
      }
      if (DesignDataBundleVersionUpdateUrl.Length != 0) {
        output.WriteRawTag(50);
        output.WriteString(DesignDataBundleVersionUpdateUrl);
      }
      if (AssetBundleVersionUpdateUrl.Length != 0) {
        output.WriteRawTag(98);
        output.WriteString(AssetBundleVersionUpdateUrl);
      }
      if (IFixPatchRevision.Length != 0) {
        output.WriteRawTag(106);
        output.WriteString(IFixPatchRevision);
      }
      if (IFixPatchVersionUpdateUrl.Length != 0) {
        output.WriteRawTag(178, 9);
        output.WriteString(IFixPatchVersionUpdateUrl);
      }
      if (_unknownFields != null) {
        _unknownFields.WriteTo(ref output);
      }
    }
    #endif

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public int CalculateSize() {
      int size = 0;
      if (AssetBundleVersionUpdateUrl.Length != 0) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(AssetBundleVersionUpdateUrl);
      }
      if (LuaBundleVersionUpdateUrl.Length != 0) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(LuaBundleVersionUpdateUrl);
      }
      if (LuaPatchVersion.Length != 0) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(LuaPatchVersion);
      }
      if (DesignDataBundleVersionUpdateUrl.Length != 0) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(DesignDataBundleVersionUpdateUrl);
      }
      if (IFixPatchVersionUpdateUrl.Length != 0) {
        size += 2 + pb::CodedOutputStream.ComputeStringSize(IFixPatchVersionUpdateUrl);
      }
      if (IFixPatchRevision.Length != 0) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(IFixPatchRevision);
      }
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(StarRailGateway other) {
      if (other == null) {
        return;
      }
      if (other.AssetBundleVersionUpdateUrl.Length != 0) {
        AssetBundleVersionUpdateUrl = other.AssetBundleVersionUpdateUrl;
      }
      if (other.LuaBundleVersionUpdateUrl.Length != 0) {
        LuaBundleVersionUpdateUrl = other.LuaBundleVersionUpdateUrl;
      }
      if (other.LuaPatchVersion.Length != 0) {
        LuaPatchVersion = other.LuaPatchVersion;
      }
      if (other.DesignDataBundleVersionUpdateUrl.Length != 0) {
        DesignDataBundleVersionUpdateUrl = other.DesignDataBundleVersionUpdateUrl;
      }
      if (other.IFixPatchVersionUpdateUrl.Length != 0) {
        IFixPatchVersionUpdateUrl = other.IFixPatchVersionUpdateUrl;
      }
      if (other.IFixPatchRevision.Length != 0) {
        IFixPatchRevision = other.IFixPatchRevision;
      }
      _unknownFields = pb::UnknownFieldSet.MergeFrom(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(pb::CodedInputStream input) {
    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      input.ReadRawMessage(this);
    #else
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
        switch(tag) {
          default:
            _unknownFields = pb::UnknownFieldSet.MergeFieldFrom(_unknownFields, input);
            break;
          case 26: {
            LuaBundleVersionUpdateUrl = input.ReadString();
            break;
          }
          case 34: {
            LuaPatchVersion = input.ReadString();
            break;
          }
          case 50: {
            DesignDataBundleVersionUpdateUrl = input.ReadString();
            break;
          }
          case 98: {
            AssetBundleVersionUpdateUrl = input.ReadString();
            break;
          }
          case 106: {
            IFixPatchRevision = input.ReadString();
            break;
          }
          case 1202: {
            IFixPatchVersionUpdateUrl = input.ReadString();
            break;
          }
        }
      }
    #endif
    }

    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    void pb::IBufferMessage.InternalMergeFrom(ref pb::ParseContext input) {
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
        switch(tag) {
          default:
            _unknownFields = pb::UnknownFieldSet.MergeFieldFrom(_unknownFields, ref input);
            break;
          case 26: {
            LuaBundleVersionUpdateUrl = input.ReadString();
            break;
          }
          case 34: {
            LuaPatchVersion = input.ReadString();
            break;
          }
          case 50: {
            DesignDataBundleVersionUpdateUrl = input.ReadString();
            break;
          }
          case 98: {
            AssetBundleVersionUpdateUrl = input.ReadString();
            break;
          }
          case 106: {
            IFixPatchRevision = input.ReadString();
            break;
          }
          case 1202: {
            IFixPatchVersionUpdateUrl = input.ReadString();
            break;
          }
        }
      }
    }
    #endif

  }

  #endregion

}

#endregion Designer generated code
