using System;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using KodakkuAssist.Script;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.Draw.Manager;
using KodakkuAssist.Data;
using KodakkuAssist.Extensions;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Reflection;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace Codaaaaaa.TheForkedTowerMagic;

[ScriptType(
    guid: "45819e25-cb2d-4d84-a508-f110dc6a381a",
    name: "魔之塔画图",
    territorys: [1346],
    version: "0.0.0.1",
    author: "Codaaaaaa",
    note: "0.0.0.1\n目前只完成老一")]
public class TheForkedTowerMagic
{
    #region 用户设置
    [UserSetting("双头决战：只能选中自己Buff对应的头")] public static bool DualHeadTargetLock { get; set; } = false;
    // [UserSetting("是否开启TTS")] public static bool TTSOpen { get; set; } = true;
    #endregion

    // Map: 1136 魔之塔下层 Boss1
    // Map: 1178 魔之塔下层 小怪+Boss2
    // 换P
    private double _phase = 1.0;

    [ScriptMethod(name: "Set Phase 1", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASP1"], userControl: false)]
    public void SetP1(Event evt, ScriptAccessory sa) => _phase = 1;

    [ScriptMethod(name: "Set Phase 2", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASP2"], userControl: false)]
    public void SetP2(Event evt, ScriptAccessory sa) => _phase = 2;
    
    [ScriptMethod(name: "Show Phase", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASP2"], userControl: false)]
    public void ShowPhase(Event evt, ScriptAccessory sa) => sa.Method.SendChat($"/e Phase: {_phase}");

    #region Boss 1 双头

    private const uint 绿头Buff = 4192;      // 只能打 19474 绿头
    private const uint 蓝头Buff = 4194;      // 只能打 19475 蓝头
    private const uint 绿头DataId = 19474;
    private const uint 蓝头DataId = 19475;

    private bool _dualHeadLockOn = false;
    private int _dualHeadLockState = 0;

    private readonly object _ballLock = new();
    private readonly List<Vector3> _thunderBalls = [];  // 19478 雷球
    private readonly List<Vector3> _iceBalls = [];      // 19479 冰球

    // 特殊功能：决战只能选择对应buff的怪物。我抄我抄
    [ScriptMethod(name: "BOSS1 - 决战Buff目标限制", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(419[24])$"], userControl: true)]
    public void 双头决战Buff目标限制(Event evt, ScriptAccessory sa)
    {
        if (!DualHeadTargetLock) return;
        if (_dualHeadLockOn) return;
        _dualHeadLockOn = true;
        _dualHeadLockState = 0;
        sa.Method.RegistFrameworkUpdateAction(Action);

        void Action()
        {
            const int 打绿头 = 1;
            const int 打蓝头 = 2;
            const int 无限制 = 3;

            var myObject = sa.Data.MyObject;
            if (myObject is null) return;

            int currentState = (myObject.HasStatus(绿头Buff), myObject.HasStatus(蓝头Buff)) switch
            {
                (true, false) => 打绿头,
                (false, true) => 打蓝头,
                _ => 无限制
            };
            if (currentState == _dualHeadLockState) return;

            var green = sa.FindByDataId(绿头DataId);
            var blue = sa.FindByDataId(蓝头DataId);
            if (green is null || blue is null) return;   // 找不到目标则下帧重试
            _dualHeadLockState = currentState;

            switch (currentState)
            {
                case 打绿头:
                    sa.SetTargetable(green, true);
                    sa.SetTargetable(blue, false);
                    break;
                case 打蓝头:
                    sa.SetTargetable(green, false);
                    sa.SetTargetable(blue, true);
                    break;
                default:
                    sa.SetTargetable(green, true);
                    sa.SetTargetable(blue, true);
                    break;
            }
        }
    }

    // 47617 蓝头剧毒吐息：8s，EffectPosition 18m危险圈
    [ScriptMethod(name: "BOSS1 - 蓝头剧毒吐息", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47617"])]
    public void 蓝头剧毒吐息(Event evt, ScriptAccessory sa)
    {
        var dp = sa.FastDp("蓝头剧毒吐息", evt.EffectPosition(), 8000, new Vector2(18f));
        dp.ScaleMode = ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    // 47616 绿头风暴吐息：8s，EffectPosition 上 CreateOmen 386
    [ScriptMethod(name: "BOSS1 - 绿头风暴吐息", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47616"])]
    public void 绿头风暴吐息(Event evt, ScriptAccessory sa)
    {
        sa.Method.VfxMethod.CreateOmen(386, new Vector3(30f),
            evt.EffectPosition(), evt.SourceRotation(), sa.Data.DefaultDangerColor, 8000);
    }

    // 50658 双头恐惧：TargetPosition为起点，按SourceRotation画6s矩形，长40宽10
    [ScriptMethod(name: "BOSS1 - 双头恐惧", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:50658"])]
    public void 双头恐惧(Event evt, ScriptAccessory sa)
    {
        var dp = sa.FastDp("双头恐惧", evt.TargetPosition, 6000, new Vector2(10f, 40f));
        dp.Rotation = evt.SourceRotation();
        dp.ScaleMode = ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    // 5403 定时诅咒-东风：击退朝向(-1,0,0)，13s；5404 定时诅咒-西风：击退朝向(1,0,0)，13s
    [ScriptMethod(name: "BOSS1 - 定时诅咒击退", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(540[34])$"])]
    public void 定时诅咒击退(Event evt, ScriptAccessory sa)
    {
        if (evt.TargetId() != sa.Data.Me) return;
        var dir = evt.StatusId == 5403 ? new Vector3(-1, 0, 0) : new Vector3(1, 0, 0);

        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = $"定时诅咒击退-{evt.StatusId}";
        dp.Owner = sa.Data.Me;
        dp.FixRotation = true;
        dp.Rotation = MathF.Atan2(dir.X, dir.Z);
        dp.Color = new Vector4(1f, 1f, 0f, 1f);
        dp.DestoryAt = 13000;
        dp.Scale = new Vector2(0.7f, 15f);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
    }

    // 19478 雷球出现，记录位置
    [ScriptMethod(name: "BOSS1 - 雷球位置记录", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:19478"], userControl: false)]
    public void 雷球位置记录(Event evt, ScriptAccessory sa)
    {
        var pos = evt.SourcePosition();
        lock (_ballLock)
        {
            if (_thunderBalls.All(p => DistXZ(p, pos) > 1f))
                _thunderBalls.Add(pos);
        }
    }

    // 19479 冰球出现，记录位置
    [ScriptMethod(name: "BOSS1 - 冰球位置记录", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:19479"], userControl: false)]
    public void 冰球位置记录(Event evt, ScriptAccessory sa)
    {
        var pos = evt.SourcePosition();
        lock (_ballLock)
        {
            if (_iceBalls.All(p => DistXZ(p, pos) > 1f))
                _iceBalls.Add(pos);
        }
    }

    // 50698 冰簇：EffectPosition 15m内的冰球位置画15m危险圈8s，并从list删除
    [ScriptMethod(name: "BOSS1 - 冰簇", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:50698"])]
    public void 冰簇(Event evt, ScriptAccessory sa)
        => TriggerBallExplosion(sa, _iceBalls, evt.EffectPosition(), "冰簇爆炸");

    // 50697 雷簇：EffectPosition 15m内的雷球位置画15m危险圈8s，并从list删除
    [ScriptMethod(name: "BOSS1 - 雷簇", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:50697"])]
    public void 雷簇(Event evt, ScriptAccessory sa)
        => TriggerBallExplosion(sa, _thunderBalls, evt.EffectPosition(), "雷簇爆炸");

    private void TriggerBallExplosion(ScriptAccessory sa, List<Vector3> balls, Vector3 effectPos, string name)
    {
        List<Vector3> triggered = [];
        lock (_ballLock)
        {
            triggered.AddRange(balls.Where(p => DistXZ(p, effectPos) <= 15f));
            balls.RemoveAll(p => DistXZ(p, effectPos) <= 15f);
        }
        for (var i = 0; i < triggered.Count; i++)
        {
            var dp = sa.FastDp($"{name}-{i}", triggered[i], 10000, new Vector2(15f));
            dp.ScaleMode = ScaleMode.ByTime;
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    // 47735 雷霜暴风雨(绿头19474)：所有剩余雷球/冰球位置画15m危险圈5s，然后清空两个list
    [ScriptMethod(name: "BOSS1 - 雷霜暴风雨", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47735", "SourceDataId:19474"])]
    public void 雷霜暴风雨(Event evt, ScriptAccessory sa)
    {
        List<Vector3> remains = [];
        lock (_ballLock)
        {
            remains.AddRange(_thunderBalls);
            remains.AddRange(_iceBalls);
            _thunderBalls.Clear();
            _iceBalls.Clear();
        }
        for (var i = 0; i < remains.Count; i++)
        {
            var dp = sa.FastDp($"雷霜暴风雨-{i}", remains[i], 7000, new Vector2(15f));
            dp.ScaleMode = ScaleMode.ByTime;
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    // 50703 冰焰凝环：先去EffectPosition朝me方向6m外(6s)，6s后tts"穿"，指路4m内(2s)
    [ScriptMethod(name: "BOSS1 - 冰焰凝环", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(50703|50704)$"])]
    public async void 冰焰凝环(Event evt, ScriptAccessory sa)
    {
        var center = evt.EffectPosition();

        var outPos = center + DirToMe(sa, center) * 6f;
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement,
            sa.WaypointDp(outPos, 6000, 0, "冰焰凝环-外"));

        await Task.Delay(6000);

        sa.Method.TTS("穿", 3);
        var inPos = center + DirToMe(sa, center) * 4f;
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement,
            sa.WaypointDp(inPos, 2000, 0, "冰焰凝环-内"));
    }
    // 50705 冰焰凝环：先去EffectPosition朝me方向6m外(6s)，6s后tts"穿"，指路4m内(2s)
    [ScriptMethod(name: "BOSS1 - 连续冰焰凝环", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(50705)$"])]
    public async void 冰焰凝环2(Event evt, ScriptAccessory sa)
    {
        var center = evt.EffectPosition();

        var outPos = center + DirToMe(sa, center) * 6f;
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement,
            sa.WaypointDp(outPos, 2000, 4000, "冰焰凝环-外"));

        await Task.Delay(6000);

        sa.Method.TTS("穿", 3);
        var inPos = center + DirToMe(sa, center) * 4f;
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement,
            sa.WaypointDp(inPos, 2000, 0, "冰焰凝环-内"));
    }

    // 从center指向me的水平单位向量
    private static Vector3 DirToMe(ScriptAccessory sa, Vector3 center)
    {
        var mePos = sa.Data.MyObject?.Position ?? center;
        var dir = new Vector3(mePos.X - center.X, 0, mePos.Z - center.Z);
        return dir.Length() < 0.01f ? new Vector3(0, 0, 1) : Vector3.Normalize(dir);
    }

    private static float DistXZ(Vector3 a, Vector3 b)
        => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    #endregion
}

#region Helpers

public static class EventExtensions
{
    public static float SourceRotation(this Event evt)
        => JsonConvert.DeserializeObject<float>(evt["SourceRotation"]);
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

    public static IGameObject? FindByDataId(this ScriptAccessory sa, uint dataId)
        => sa.Data.Objects.FirstOrDefault(x => x != null && x.DataId == dataId);

    // 修改物体可选中状态
    public static void SetTargetable(this ScriptAccessory sa, IGameObject? obj, bool targetable)
    {
        if (obj == null || !obj.IsValid())
        {
            sa.Log.Error($"传入的IGameObject不合法。");
            return;
        }
        unsafe
        {
            GameObject* charaStruct = (GameObject*)obj.Address;
            if (targetable)
            {
                if (obj.IsDead || obj.IsTargetable) return;
                charaStruct->TargetableStatus |= ObjectTargetableFlags.IsTargetable;
            }
            else
            {
                if (!obj.IsTargetable) return;
                charaStruct->TargetableStatus &= ~ObjectTargetableFlags.IsTargetable;
            }
        }
        sa.Log.Debug($"SetTargetable {targetable} => {obj.Name} {obj}");
    }

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

    // public static DrawPropertiesEdit WaypointDp(this ScriptAccessory sa, Vector3 pos, uint duration, uint delay = 0, string name = "Waypoint")
    // {
    //     var dp = sa.Data.GetDefaultDrawProperties();
    //     dp.Name = name;
    //     dp.Color = sa.Data.DefaultSafeColor;
    //     dp.Owner = sa.Data.Me;
    //     dp.TargetPosition = pos;
    //     dp.DestoryAt = duration;
    //     dp.Delay = delay;
    //     dp.Scale = new Vector2(2);
    //     dp.ScaleMode = ScaleMode.YByDistance;
    //     return dp;
    // }
    public static DrawPropertiesEdit WaypointDp(this ScriptAccessory sa, Vector3 target, uint duration, uint delay = 0, string name = "Waypoint", Vector4? color = null)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? sa.Data.DefaultSafeColor;
        dp.Owner = sa.Data.Me;            // 仍然绑定自己
        dp.TargetPosition = target;
        dp.DestoryAt = duration;
        dp.Delay = delay;
        dp.Scale = new Vector2(2);
        dp.ScaleMode = ScaleMode.YByDistance;
        return dp;
    }

    public static DrawPropertiesEdit WaypointFromToDp(this ScriptAccessory sa, Vector3 from, Vector3 to, uint duration, uint delay = 0, string name = "WaypointFromTo", Vector4? color = null)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? sa.Data.DefaultSafeColor;
        dp.Owner = 0;                    // 不绑定任何物体，防止跟着人跑
        dp.Position = from;              // 起点
        dp.TargetPosition = to;          // 终点
        dp.DestoryAt = duration;
        dp.Delay = delay;
        dp.Scale = new Vector2(2);
        dp.ScaleMode = ScaleMode.YByDistance;
        return dp;
    }
}

#endregion
