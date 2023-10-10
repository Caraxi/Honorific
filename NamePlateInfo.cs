using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.System.String;

namespace Honorific; 

// TODO: Move back to ClientStructs when IsRedrawRequested is Available.

[StructLayout(LayoutKind.Explicit, Size = 0x248)]
public struct NamePlateInfo {
    [FieldOffset(0x00)] public GameObjectID ObjectID;
    [FieldOffset(0x30)] public Utf8String Name;
    [FieldOffset(0x98)] public Utf8String FcName;
    [FieldOffset(0x100)] public Utf8String Title;
    [FieldOffset(0x168)] public Utf8String DisplayTitle;
    [FieldOffset(0x1D0)] public Utf8String LevelText;
    [FieldOffset(0x240)] public int Flags;
    [FieldOffset(0x244)] private byte isRedrawRequested;

    public bool IsRedrawRequested {
        get => isRedrawRequested != 0;
        set => isRedrawRequested = (byte) (value ? 1 : 0);
    }
    
    public bool IsPrefixTitle => ((Flags >> (8 * 3)) & 0xFF) == 1;
}