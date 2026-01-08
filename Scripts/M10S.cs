using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using KodakkuAssist.Script;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.Draw.Manager;
using KodakkuAssist.Data;
using KodakkuAssist.Extensions;

namespace Codaaaaaa.M9S;

[ScriptType(
    guid: "b7d2d7d7-5e8c-4f53-8a0e-7e7f0a7a1c21",
    name: "阿卡狄亚零式登天斗技场M10S",
    territorys: [1323],
    version: "0.0.0.1",
    author: "Codaaaaaa",
    note: "Template：画图+指路+TTS。请按实际机制替换 ActionId/DataId/TargetIcon。")]
public class M10S
{
    #region 用户设置
    // [UserSetting("是否开启TTS")] public static bool TTSOpen { get; set; } = true;
    #endregion

    private static readonly Vector3 Center = new(100, 0, 100);

    private uint _phase = 1;
    private int 吸血层数 = 0;

    public void Init(ScriptAccessory sa)
    {
        ResetAll();
        sa.Method.RemoveDraw(".*");
    }

    private void ResetAll()
    {
        _phase = 1;
        吸血层数 = 0;
    }

    // Debug
    [ScriptMethod(name: "clear draw", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASCLEAR"], userControl: false)]
    public void ClearDraw(Event evt, ScriptAccessory sa) => sa.Method.RemoveDraw(".*");

    [ScriptMethod(name: "Set Phase 1", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASP1"], userControl: false)]
    public void SetP1(Event evt, ScriptAccessory sa) => _phase = 1;

    [ScriptMethod(name: "Set Phase 2", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASP2"], userControl: false)]
    public void SetP2(Event evt, ScriptAccessory sa) => _phase = 2;

    [ScriptMethod(name: "Set Phase 3", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASP3"], userControl: false)]
    public void SetP3(Event evt, ScriptAccessory sa) => _phase = 3;

    [ScriptMethod(name: "Set Phase 4", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASP4"], userControl: false)]
    public void SetP4(Event evt, ScriptAccessory sa) => _phase = 4;

    [ScriptMethod(name: "Set Phase 5", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASP5"], userControl: false)]
    public void SetP5(Event evt, ScriptAccessory sa) => _phase = 5;

    [ScriptMethod(name: "Show Phase", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:phase"], userControl: false)]
    public void ShowPhase(Event evt, ScriptAccessory sa) => sa.Method.SendChat($"/e Current Phase: {_phase}");

    // =========================
    // 示例：记录分摊/分散（把 ActionId 换成 M9S 对应技能）
    // =========================
    [ScriptMethod(
    name: "分摊死刑",
    eventType: EventTypeEnum.StartCasting,
    eventCondition: ["ActionId:46518"])]
    public void 分摊死刑(Event evt, ScriptAccessory sa)
    {
        var myIdx = sa.MyIndex();
        var color = myIdx switch
        {
            0 => sa.Data.DefaultSafeColor,
            1 => sa.Data.DefaultSafeColor,
            _ => sa.Data.DefaultDangerColor
        };
        var targetId = evt.TargetId();

        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = $"死刑圈-{targetId:X}";
        dp.Owner = targetId;
        dp.Color = color;
        dp.DestoryAt = 5500;
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Scale = new Vector2(6);
        
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    // 开场火冲
    [ScriptMethod(
        name: "开场火冲麻将",
        eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:regex:^(3004|3005|3006|3451)$"]
    )]
    public void 开场火冲麻将(Event evt, ScriptAccessory sa)
    {
        var targetId = evt.TargetId();
        var myId = sa.Data.Me;

        // 只画自己身上的
        if (targetId == 0 || myId == 0 || targetId != myId)
            return;

        // sa.Method.SendChat($"/e 开场火冲麻将状态触发，TargetId={targetId:X}, StatusID={evt["StatusID"]}");
        var wpos = evt["StatusID"] switch
        {
            "3004" => new Vector3(118.5f, 0f, 118.5f),
            "3005" => new Vector3(98.8f, 0f, 118.5f),
            "3006" => new Vector3(118.5f, 0f, 113f),
            "3451" => new Vector3(98.8f, 0f, 113f),
            _ => new Vector3(100f, 0, 100f)
        };
        var dp = sa.WaypointDp(wpos, 20000, 0, "开场火冲麻将指路");
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    [ScriptMethod(
        name: "开场火冲麻将删除画图",
        eventType: EventTypeEnum.StatusRemove,
        eventCondition: ["StatusID:regex:^(3004|3005|3006|3451)$"],
        userControl: false
    )]
    public void 开场火冲麻将删除画图(Event evt, ScriptAccessory sa)
    {
        var targetId = evt.TargetId();
        var myId = sa.Data.Me;

        // 只删自己身上的
        if (targetId == 0 || myId == 0 || targetId != myId)
            return;

        sa.Method.RemoveDraw("开场火冲麻将指路");
    }

    // 空中旋火
    [ScriptMethod(
        name: "空中旋火指路P1",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:46529"]
    )]
    public void 空中旋火指路P1(Event evt, ScriptAccessory sa)
    {
        var myIdx = sa.MyIndex();
        var targetId = evt.TargetId();
        var myId = sa.Data.Me;

        // 只画自己身上的
        if (targetId == 0 || myId == 0 || targetId != myId)
            return;

        var wpos = myIdx switch
        {
            4 => new Vector3(118.01f, 0f, 103.57f),
            5 => new Vector3(111.8f, 0f, 103.57f),
            1 => new Vector3(105.59f, 0f, 103.57f),
            3 => new Vector3(99.38f, 0f, 103.57f),
            7 => new Vector3(93.17f, 0f, 103.57f),

            0 => new Vector3(118.01f, 0f, 94.2f),
            2 => new Vector3(118.01f, 0f, 88f),
            6 => new Vector3(118.01f, 0f, 81.78f),
            
            _ => new Vector3(100f, 0, 100f)
        };

        var dp = sa.WaypointDp(wpos, 15000, 0, "空中旋火指路P1");
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
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

    public static DrawPropertiesEdit FastDpWithPosition(this ScriptAccessory sa, string name, Vector3 pos, uint duration, Vector2 scale, bool safe = false)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = safe ? sa.Data.DefaultSafeColor : sa.Data.DefaultDangerColor;
        dp.Position = pos;
        dp.DestoryAt = duration;
        dp.Scale = scale;
        return dp;
    }

    public static DrawPropertiesEdit FastDpWithTarget(this ScriptAccessory sa, string name, uint targetId, uint duration, Vector2 scale, bool safe = false)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = safe ? sa.Data.DefaultSafeColor : sa.Data.DefaultDangerColor;
        dp.Owner = targetId;
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
