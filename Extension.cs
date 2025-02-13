﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.System.String;
using Lumina.Excel.Sheets;

namespace Honorific; 

public static unsafe class Extension {
    public static SeString Cleanup(this SeString str) {
        var payloads = new List<Payload>();
        foreach (var payload in str.Payloads) {
            if (payloads.Count > 0 && payloads[^1] is TextPayload lastTextPayload && payload is TextPayload textPayload) {
                lastTextPayload.Text += textPayload.Text;
            } else {
                payloads.Add(payload);
            }
        }
        
        return new SeString(payloads);
    }

    public static bool IsSameAs(this SeString a, SeString b, out byte[]? encoded) {
        encoded = a.EncodeNullTerminated();
        var encodeB = b.EncodeNullTerminated();
        if (encoded.Length != encodeB.Length) return false;
        for (var i = 0; i < encoded.Length; i++) {
            if (encoded[i] != encodeB[i]) return false;
        }
        return true;
    }
    
    public static byte[] EncodeNullTerminated(this SeString str)
    {
        List<byte> byteList = new List<byte>();
        foreach (Payload payload in str.Payloads)
            byteList.AddRange((IEnumerable<byte>) payload.Encode());
        byteList.Add(0);
        return byteList.ToArray();
    }
    
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool TryGetFirst<T>(this IEnumerable<T> values, Func<T, bool> predicate, out T result) where T : struct
    {
        foreach(var val in values)
            if (predicate(val)) {
                result = val;
                return true;
            }
        result = default;
        return false;
    }

    
    public static bool IsPlayerWorld(this World world) {
        if (world.Name.Data.IsEmpty) return false;
        if (world.DataCenter.RowId == 0) return false;
        if (world.IsPublic) return true;
        return char.IsUpper((char)world.Name.Data.Span[0]);
    }
}
