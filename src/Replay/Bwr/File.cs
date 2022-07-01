// <auto-generated>
//  automatically generated by the FlatBuffers compiler, do not modify
// </auto-generated>

namespace Bwr
{

using global::System;
using global::System.Collections.Generic;
using global::FlatBuffers;

public struct File : IFlatbufferObject
{
  private Table __p;
  public ByteBuffer ByteBuffer { get { return __p.bb; } }
  public static void ValidateVersion() { FlatBufferConstants.FLATBUFFERS_1_12_0(); }
  public static File GetRootAsFile(ByteBuffer _bb) { return GetRootAsFile(_bb, new File()); }
  public static File GetRootAsFile(ByteBuffer _bb, File obj) { return (obj.__assign(_bb.GetInt(_bb.Position) + _bb.Position, _bb)); }
  public static bool FileBufferHasIdentifier(ByteBuffer _bb) { return Table.__has_identifier(_bb, "BwRp"); }
  public void __init(int _i, ByteBuffer _bb) { __p = new Table(_i, _bb); }
  public File __assign(int _i, ByteBuffer _bb) { __init(_i, _bb); return this; }

  public Bwr.Metadata? Metadata { get { int o = __p.__offset(4); return o != 0 ? (Bwr.Metadata?)(new Bwr.Metadata()).__assign(__p.__indirect(o + __p.bb_pos), __p.bb) : null; } }
  public Bwr.Level? Levels(int j) { int o = __p.__offset(6); return o != 0 ? (Bwr.Level?)(new Bwr.Level()).__assign(__p.__indirect(__p.__vector(o) + j * 4), __p.bb) : null; }
  public int LevelsLength { get { int o = __p.__offset(6); return o != 0 ? __p.__vector_len(o) : 0; } }

  public static Offset<Bwr.File> CreateFile(FlatBufferBuilder builder,
      Offset<Bwr.Metadata> metadataOffset = default(Offset<Bwr.Metadata>),
      VectorOffset levelsOffset = default(VectorOffset)) {
    builder.StartTable(2);
    File.AddLevels(builder, levelsOffset);
    File.AddMetadata(builder, metadataOffset);
    return File.EndFile(builder);
  }

  public static void StartFile(FlatBufferBuilder builder) { builder.StartTable(2); }
  public static void AddMetadata(FlatBufferBuilder builder, Offset<Bwr.Metadata> metadataOffset) { builder.AddOffset(0, metadataOffset.Value, 0); }
  public static void AddLevels(FlatBufferBuilder builder, VectorOffset levelsOffset) { builder.AddOffset(1, levelsOffset.Value, 0); }
  public static VectorOffset CreateLevelsVector(FlatBufferBuilder builder, Offset<Bwr.Level>[] data) { builder.StartVector(4, data.Length, 4); for (int i = data.Length - 1; i >= 0; i--) builder.AddOffset(data[i].Value); return builder.EndVector(); }
  public static VectorOffset CreateLevelsVectorBlock(FlatBufferBuilder builder, Offset<Bwr.Level>[] data) { builder.StartVector(4, data.Length, 4); builder.Add(data); return builder.EndVector(); }
  public static void StartLevelsVector(FlatBufferBuilder builder, int numElems) { builder.StartVector(4, numElems, 4); }
  public static Offset<Bwr.File> EndFile(FlatBufferBuilder builder) {
    int o = builder.EndTable();
    builder.Required(o, 4);  // metadata
    builder.Required(o, 6);  // levels
    return new Offset<Bwr.File>(o);
  }
  public static void FinishFileBuffer(FlatBufferBuilder builder, Offset<Bwr.File> offset) { builder.Finish(offset.Value, "BwRp"); }
  public static void FinishSizePrefixedFileBuffer(FlatBufferBuilder builder, Offset<Bwr.File> offset) { builder.FinishSizePrefixed(offset.Value, "BwRp"); }
};


}