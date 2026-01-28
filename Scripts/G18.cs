using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Newtonsoft.Json;
using KodakkuAssist.Script;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.Draw.Manager;
using KodakkuAssist.Data;
using KodakkuAssist.Extensions;

namespace Codaaaaaa.G18;

[ScriptType(
    guid: "8f2a1d6d-4d3b-4c61-8b9a-6a4b8d0c8b2a",
    name: "g18神秘剧透小脚本",
    territorys: [1279],
    version: "0.0.0.1",
    author: "Codaaaaaa",
    note: "剧透")]
public class G18
{
    #region User Settings
    [UserSetting("Debug")]
    public bool ChatOutput { get; set; } = false;
    #endregion

    // History of resolved results (what we will "report")
    private readonly List<string> _history = new();
    private readonly Queue<string> _slotBaseLastTwo = new();
    private int? _pendingThird = null;

    // Third reel mapping (27-31)
    private static readonly Dictionary<int, string> SlotThirdMap = new()
    {
        { 27, "心碎" },
        { 28, "终钻" },
        { 29, "三钻" },
        { 30, "终钻" },
        { 31, "宝箱" },
    };

    public void Init(ScriptAccessory sa)
    {
        // ResetAll();
        sa.Method.RemoveDraw(".*");
    }

    // private void ResetAll()
    // {
    //     _history.Clear();
    //     _slotFirstTwo.Clear();
    // }

    /// <summary>
    /// Record EnvControl indices when Flag is 1 or 2.
    /// 0-20: direct mapping by pattern (mod 7)
    /// 21-26: slot machine reel 1 & 2 (store indices only)
    /// 27-31: slot machine reel 3 (resolve to "变动" if first two equal, else map)
    /// </summary>
    [ScriptMethod(name: "记录", eventType: EventTypeEnum.EnvControl, eventCondition: ["Flag:regex:^(1|2)$"], userControl: false)]
    public void 记录(Event evt, ScriptAccessory sa)
    {
        if (!TryGetInt(evt, "Index", out var index)) return;

        // 0-20：正常结果 + 更新老虎机前两格候选
        if (index >= 0 && index <= 20)
        {
            var txt = MapNormalIndex(index);
            _history.Add(txt);

            // 维护最近两次正常结果（滑动窗口）
            if (_slotBaseLastTwo.Count == 2) _slotBaseLastTwo.Dequeue();
            _slotBaseLastTwo.Enqueue(txt);

            DebugPrint(sa, $"[G18] Record index={index} => {txt} (baseBuf={string.Join(",", _slotBaseLastTwo)})");

            // 如果第三格已经先到，尝试结算
            TryResolveSlotRound(sa);
            return;
        }

        // 27-31：第三格（可能先到）
        if (index >= 27 && index <= 31)
        {
            _pendingThird = index;
            DebugPrint(sa, $"[G18] Slot got third index={index} => {(SlotThirdMap.TryGetValue(index, out var v) ? v : "?")} (baseBuf={string.Join(",", _slotBaseLastTwo)})");
            TryResolveSlotRound(sa);
            return;
        }

        // 其他 index 不管
        // if (index >= 21 && index <= 26)
        //     {
        //         var txt = $"未知index:{index}";
        //         _history.Add(txt);
        //         if (_slotBaseLastTwo.Count == 2) _slotBaseLastTwo.Dequeue();
        //         _slotBaseLastTwo.Enqueue(txt);

        //         DebugPrint(sa, $"[G18] Record index={index} => {txt} (baseBuf={string.Join(",", _slotBaseLastTwo)})");

        //         // 如果第三格已经先到，尝试结算
        //         TryResolveSlotRound(sa);
        //         return;
        //     }
    }

    [ScriptMethod(name: "变动播报", eventType: EventTypeEnum.EnvControl, eventCondition: ["Flag:regex:^(64|128|256|512|1024|2048|4096|8192|16384)$"])]
    public void 变动播报(Event evt, ScriptAccessory sa)
    {
        if (!TryGetInt(evt, "Index", out var index)) return;
        if (index >= 21 && index <= 26) return;
        
        sa.Method.TextInfo("变动", 5500, true);
    }

    [ScriptMethod(name: "剧透报", eventType: EventTypeEnum.ObjectEffect, eventCondition: ["SourceName:潜网巡梦"])]
    public void 剧透报(Event evt, ScriptAccessory sa)
    {
        if (TryGetInt(evt, "Id1", out var id1))
        {
            if (id1 != 1) return;
        }
        
        if (_history.Count == 0)
        {
            DebugPrint(sa, "[G18] No records yet.");
            return;
        }

        var last = _history.Skip(Math.Max(0, _history.Count - 3)).ToList();
        var msg = $"[G18]：{string.Join(" / ", last)}";

        Output(sa, msg);
    }

    // ---------------- Core Mapping ----------------

    /// <summary>
    /// 0-20 pattern (repeat every 7):
    /// 0走 1小 2大 3终 4宝 5宝/大 6小
    /// </summary>
    private static string MapNormalIndex(int index)
    {
        var m = index % 7;
        return m switch
        {
            0 => "心碎",
            1 => "小钻",
            2 => "三钻",
            3 => "终钻",
            4 => "宝箱",
            5 => "彩",
            6 => "小钻",
            _ => "?"
        };
    }

    // ---------------- Output Helpers ----------------

    private void Output(ScriptAccessory sa, string msg)
    {
        if (ChatOutput)
        {
            // If you prefer not to chat, you can replace this with other UI methods.
            // Example: sa.Method.SendNotification(msg);
            DebugPrint(sa, msg);
        }

        // Most KodakkuAssist versions have SendChat; if yours doesn't, tell me what methods you have in sa.Method.
        sa.Method.TextInfo($"{msg}", 5500, false);
    }

    private void DebugPrint(ScriptAccessory sa, string msg)
    {
        // Keep it safe: debug to chat only when ChatOutput is enabled.
        if (ChatOutput)
            sa.Method.SendChat($"/e {msg}");
    }
    private static bool TryGetInt(Event evt, string key, out int value)
    {
        value = 0;
        try
        {
            var s = evt[key];
            if (string.IsNullOrEmpty(s)) return false;

            // 兼容 "0x.." 或纯数字
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return int.TryParse(s.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out value);

            return int.TryParse(s, out value);
        }
        catch
        {
            return false;
        }
    }
    private void TryResolveSlotRound(ScriptAccessory sa)
    {
        if (_pendingThird == null) return;
        if (_slotBaseLastTwo.Count < 2) return;

        var arr = _slotBaseLastTwo.ToArray(); // [0]=更早, [1]=更近
        var same = arr[0] == arr[1];

        // 感觉有问题先改回来
        var result = same
            ? arr[2]
            : (SlotThirdMap.TryGetValue(_pendingThird.Value, out var v) ? v : $"未知({_pendingThird.Value})");

        _history.Add(result);
        DebugPrint(sa, $"[G18] Slot resolved: base={arr[0]},{arr[1]} third={_pendingThird} => {result}");

        // 一轮结束：清空，防串轮
        _slotBaseLastTwo.Clear();
        _pendingThird = null;
    }

}

#region Helpers

public static class EventExtensions
{
    private static bool ParseHexId(string? idStr, out uint id)
    {
        id = 0;
        if (string.IsNullOrEmpty(idStr)) return false;
        try
        {
            var idStr2 = idStr.Replace("0x", "");
            id = uint.Parse(idStr2, System.Globalization.NumberStyles.HexNumber);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static uint ActionId(this Event evt) => JsonConvert.DeserializeObject<uint>(evt["ActionId"]);
    public static uint SourceId(this Event evt) => ParseHexId(evt["SourceId"], out var id) ? id : 0;
    public static uint TargetId(this Event evt) => ParseHexId(evt["TargetId"], out var id) ? id : 0;
    public static Vector3 SourcePosition(this Event evt) => JsonConvert.DeserializeObject<Vector3>(evt["SourcePosition"]);
    public static Vector3 EffectPosition(this Event evt) => JsonConvert.DeserializeObject<Vector3>(evt["EffectPosition"]);
    public static uint DirectorId(this Event evt) => ParseHexId(evt["DirectorId"], out var id) ? id : 0;
}

public static class ScriptAccessoryExtensions
{
    public static int MyIndex(this ScriptAccessory sa) => sa.Data.PartyList.IndexOf(sa.Data.Me);

    public static DrawPropertiesEdit FastDp(this ScriptAccessory sa, string name, Vector3 pos, uint duration, Vector2 scale, bool safe = false)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = safe ? sa.Data.DefaultSafeColor : sa.Data.DefaultDangerColor;
        dp.Position = pos;
        dp.DestoryAt = duration;
        dp.Scale = scale;
        return dp;
    }

    public static DrawPropertiesEdit WaypointDp(this ScriptAccessory sa, Vector3 pos, uint duration, uint delay = 0, string name = "Waypoint")
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = sa.Data.DefaultSafeColor;
        dp.Owner = sa.Data.Me;
        dp.TargetPosition = pos;
        dp.DestoryAt = duration;
        dp.Delay = delay;
        dp.Scale = new Vector2(2);
        dp.ScaleMode = ScaleMode.YByDistance;
        return dp;
    }
}

#endregion
