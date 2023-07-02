using System;
using System.IO;
using Dalamud.Game.Text.SeStringHandling;
using System.Numerics;
namespace Honorific;

public abstract class AbstractColorPayload : Payload {
    protected byte Red { get; init; }
    protected byte Green { get; init; }
    protected byte Blue { get; init; }
    
    protected override byte[] EncodeImpl() {
        return new byte[] { START_BYTE, ChunkType, 0x05, 0xF6, Red, Green, Blue, END_BYTE };
    }

    protected override void DecodeImpl(BinaryReader reader, long endOfStream) {
        
    }
    public override PayloadType Type => PayloadType.Unknown;

    protected abstract byte ChunkType { get; }

}

public abstract class AbstractColorEndPayload : Payload {
    protected override byte[] EncodeImpl() {
        return new byte[] { START_BYTE, ChunkType, 0x02, 0xEC, END_BYTE };
    }

    protected override void DecodeImpl(BinaryReader reader, long endOfStream) {
        
    }
    public override PayloadType Type => PayloadType.Unknown;

    protected abstract byte ChunkType { get; }
}

public class ColorPayload : AbstractColorPayload {
    protected override byte ChunkType => 0x13;

    public ColorPayload(Vector3 color) {
        Red = Math.Max((byte) 1, (byte) (color.X * 255f));
        Green = Math.Max((byte) 1, (byte) (color.Y * 255f));
        Blue = Math.Max((byte) 1, (byte) (color.Z * 255f));
    }
}

public class ColorEndPayload : AbstractColorEndPayload {
    protected override byte ChunkType => 0x13;
}

public class GlowPayload : AbstractColorPayload {
    protected override byte ChunkType => 0x14;
    
    public GlowPayload(Vector3 color) {
        Red = Math.Max((byte) 1, (byte) (color.X * 255f));
        Green = Math.Max((byte) 1, (byte) (color.Y * 255f));
        Blue = Math.Max((byte) 1, (byte) (color.Z * 255f));
    }
}

public class GlowEndPayload : AbstractColorEndPayload {
    protected override byte ChunkType => 0x14;
}
  
