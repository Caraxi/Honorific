using System;
using System.Collections.Generic;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.System.String;

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
    
}
