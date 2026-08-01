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
    version: "0.0.0.3",
    author: "Codaaaaaa",
    note: "0.0.0.3\n老二有点问题之后修")]
public class TheForkedTowerMagic
{
    #region 用户设置
    [UserSetting("双头决战：只能选中自己Buff对应的头")] public static bool DualHeadTargetLock { get; set; } = false;
    [UserSetting("测试")] public static bool Debug输出 { get; set; } = false;
    // [UserSetting("是否开启TTS")] public static bool TTSOpen { get; set; } = true;
    #endregion

    private void Dbg(ScriptAccessory sa, string msg)
    {
        if (!Debug输出) return;
        sa.Method.SendChat($"/e [魔之塔] {msg}");
    }

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

    #region BOSS2 剑舞者
    private static readonly Vector3 Boss2场中 = new(600f, -674f, 704f);
    private readonly object _leapLock = new();
    private readonly List<Vector3> _leapPositions = [];   // 49594 跃进步法落点，按触发顺序

    // 49585 半圆：SourcePosition为圆心，面对SourceRotation
    [ScriptMethod(name: "BOSS2 - 秘法剑", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:49585"])]
    public void 半圆斩(Event evt, ScriptAccessory sa)
    {
        Dbg(sa, $"半圆斩(49585)：src {evt.SourceId():X8} pos {evt.SourcePosition():F1} rot {evt.SourceRotation():F2}");
        var dp = sa.FastDp($"半圆斩-{evt.SourceId()}", evt.SourcePosition(), 5500, new Vector2(96f));
        dp.Rotation = evt.SourceRotation();
        dp.Radian = MathF.PI;
        dp.ScaleMode = ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    // 49594 跃进步法：TargetDataId不是19830时，按触发顺序记录EffectPosition，排除(0,0,0)
    [ScriptMethod(name: "BOSS2 - 跃进步法记录", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(49596|49597)$"], userControl: false)]
    public void 跃进步法记录(Event evt, ScriptAccessory sa)
    {
        var pos = evt.EffectPosition();
        if (pos.Length() < 0.01f) return;   // 排除(0,0,0)

        lock (_leapLock)
        {
            if (_leapPositions.All(p => DistXZ(p, pos) > 1f))
            {
                _leapPositions.Add(pos);
                Dbg(sa, $"跃进步法记录 #{_leapPositions.Count}：{pos:F1}");
            }
        }
    }

    // 49685 剑技爆发：按顺序指路各落点
    [ScriptMethod(name: "BOSS2 - 剑技爆发指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:49685"])]
    public void 剑技爆发指路(Event evt, ScriptAccessory sa)
    {
        List<Vector3> points;
        lock (_leapLock)
        {
            points = _leapPositions.Select(p => 朝场中偏移(p, 9f)).ToList();
            _leapPositions.Clear();   // 用完清空
        }
        if (points.Count == 0)
        {
            Dbg(sa, $"剑技爆发(49685)：落点list为空，跳过指路");
            return;
        }
        Dbg(sa, $"剑技爆发(49685)：共 {points.Count} 个落点，偏移后 [{string.Join(" | ", points.Select(p => p.ToString("F1")))}]");

        var green = new Vector4(0f, 1f, 0f, 1f);
        var white = new Vector4(1f, 1f, 1f, 1f);

        for (var k = 0; k < points.Count; k++)
        {
            // 第一段显示5s，之后每段3s
            var delay = (uint)(k == 0 ? 0 : 5000 + (k - 1) * 2500);
            var duration = (uint)(k == 0 ? 5000 : 2500);
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement,
                sa.WaypointDp(points[k], duration, delay, $"剑技爆发-me到{k + 1}", green));
            for (var j = k; j < points.Count - 1; j++)
                sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement,
                    sa.WaypointFromToDp(points[j], points[j + 1], duration, delay, $"剑技爆发-{j + 1}到{j + 2}-阶段{k + 1}", white));
        }
    }

    // 将pos沿水平方向朝场中移动dist米
    private static Vector3 朝场中偏移(Vector3 pos, float dist)
    {
        var dir = new Vector3(Boss2场中.X - pos.X, 0, Boss2场中.Z - pos.Z);
        return dir.Length() < 0.01f ? pos : pos + Vector3.Normalize(dir) * dist;
    }

    // 49616 突进：30m长6m宽矩形，4s
    [ScriptMethod(name: "BOSS2 - 突进", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:49616"])]
    public void 突进(Event evt, ScriptAccessory sa)
    {
        Dbg(sa, $"突进(49616)：pos {evt.SourcePosition():F1} rot {evt.SourceRotation():F2}");
        var dp = sa.FastDp("突进", evt.SourcePosition(), 4000, new Vector2(6f, 30f));
        dp.Rotation = evt.SourceRotation();
        dp.ScaleMode = ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    #endregion

    #region BOSS3 惧死者
    // 47465 魔具联动-爆炎 / 47468 古代爆炎：5.5s，施法者上18m危险圈
    [ScriptMethod(name: "BOSS3 - 爆炎", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(47465|47468)$"])]
    public void 爆炎(Event evt, ScriptAccessory sa)
    {
        Dbg(sa, $"爆炎({evt.ActionId()})：src {evt.SourceId():X8}");
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = $"爆炎-{evt.SourceId()}";
        dp.Color = sa.Data.DefaultDangerColor;
        dp.Owner = evt.SourceId();
        dp.DestoryAt = 5500;
        dp.Scale = new Vector2(18f);
        dp.ScaleMode = ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    // 47466 魔具联动-冰封 / 47469 古代冰封：5.5s，以施法者rotation为正面，前后左右各45长15宽的rect十字
    [ScriptMethod(name: "BOSS3 - 冰封十字", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(47466|47469)$"])]
    public void 冰封十字(Event evt, ScriptAccessory sa)
    {
        Dbg(sa, $"冰封十字({evt.ActionId()})：src {evt.SourceId():X8}");
        List<(float Rot, string Tag)> dirs = [(0f, "前"), (MathF.PI / 2, "左"), (MathF.PI, "后"), (-MathF.PI / 2, "右")];
        foreach (var (rot, tag) in dirs)
        {
            var dp = sa.Data.GetDefaultDrawProperties();
            dp.Name = $"冰封十字-{evt.SourceId()}-{tag}";
            dp.Color = sa.Data.DefaultDangerColor;
            dp.Owner = evt.SourceId();
            dp.Rotation = rot;
            dp.DestoryAt = 5500;
            dp.Scale = new Vector2(15f, 45f);
            dp.ScaleMode = ScaleMode.ByTime;
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
    }

    // 47471 古代暴雷：5.5s，SourcePosition上以SourceRotation为正面的45度60m扇形
    [ScriptMethod(name: "BOSS3 - 古代暴雷", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47471"])]
    public void 古代暴雷(Event evt, ScriptAccessory sa)
    {
        Dbg(sa, $"古代暴雷(47479)：pos {evt.SourcePosition():F1} rot {evt.SourceRotation():F2}");
        var dp = sa.FastDp($"古代暴雷-{evt.SourceId()}", evt.SourcePosition(), 5500, new Vector2(60f));
        dp.Rotation = evt.SourceRotation();
        dp.Radian = 45f * MathF.PI / 180f;
        dp.ScaleMode = ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    [ScriptMethod(name: "BOSS3 - 灭亡射线", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47475"])]
    public async void 骷髅头47475(Event evt, ScriptAccessory sa)
    {
        var srcId = evt.SourceId();
        var pos = evt.SourcePosition();

        await Task.Delay(1000);

        // 0.5s后取施法者当前实时朝向，取不到则回退用事件快照
        var srcObj = sa.Data.Objects.SearchById(srcId);
        var rot = srcObj?.Rotation ?? evt.SourceRotation();
        Dbg(sa, $"灭亡射线(47475)：pos {pos:F1} rot {rot:F2}（实时:{srcObj != null}）");

        var dp = sa.FastDp($"灭亡射线-{srcId}", pos, 4000, new Vector2(6f, 30f));
        dp.Rotation = rot;
        dp.ScaleMode = ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    #endregion

    #region BOSS4 目录

    // 48385 封印武器-竖琴：7s，15m危险圈；48387 封印武器-弓：7s，11m危险圈
    [ScriptMethod(name: "BOSS4 - 封印武器", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(48385|48387)$"])]
    public void 封印武器(Event evt, ScriptAccessory sa)
    {
        var isHarp = evt.ActionId() == 48385;
        var radius = isHarp ? 15f : 11f;
        Dbg(sa, $"封印武器-{(isHarp ? "竖琴" : "弓")}({evt.ActionId()})：pos {evt.EffectPosition():F1} 半径 {radius}");

        var dp = sa.FastDp($"封印武器-{evt.SourceId()}", evt.EffectPosition(), 7000, new Vector2(radius));
        dp.ScaleMode = ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    private static readonly Vector3 Boss4中心 = new(0f, -684f, -628f);

    private readonly object _zoneLock = new();
    private float? _fireZoneRot;      // 2015240 火区朝向（上下对称）
    private float? _waterZoneRot;     // 2015241 水区朝向（上下对称）
    private float? _thunderZoneRot;   // 2015242 雷区朝向（上下对称）
    private readonly List<Vector3> _fireballPositions = [];      // 19301 火球，每波两个
    private readonly List<Vector3> _iceballPositions = [];       // 19300 冰球，每波两个
    private readonly List<Vector3> _thunderballPositions = [];   // 19302 雷球，每波两个

    // 2015240 火区 / 2015241 水区 / 2015242 雷区：记录SourceRotation，rotation及其对面就是对应元素区
    [ScriptMethod(name: "BOSS4 - 火区记录", eventType: EventTypeEnum.ObjectChanged, eventCondition: ["Operate:Add", "DataId:2015240"], userControl: false)]
    public void 火区记录(Event evt, ScriptAccessory sa)
    {
        lock (_zoneLock) _fireZoneRot = evt.SourceRotation();
        Dbg(sa, $"火区记录：rot {evt.SourceRotation():F2}");
    }

    [ScriptMethod(name: "BOSS4 - 水区记录", eventType: EventTypeEnum.ObjectChanged, eventCondition: ["Operate:Add", "DataId:2015241"], userControl: false)]
    public void 水区记录(Event evt, ScriptAccessory sa)
    {
        lock (_zoneLock) _waterZoneRot = evt.SourceRotation();
        Dbg(sa, $"水区记录：rot {evt.SourceRotation():F2}");
    }

    [ScriptMethod(name: "BOSS4 - 雷区记录", eventType: EventTypeEnum.ObjectChanged, eventCondition: ["Operate:Add", "DataId:2015242"], userControl: false)]
    public void 雷区记录(Event evt, ScriptAccessory sa)
    {
        lock (_zoneLock) _thunderZoneRot = evt.SourceRotation();
        Dbg(sa, $"雷区记录：rot {evt.SourceRotation():F2}");
    }

    // 元素球每波两个：收齐后按中心→球朝向顺时针转到对应区的角度算duration，
    // 在上下对应区各画一个60度30m扇形；duration多于3s时只在最后3s显示
    [ScriptMethod(name: "BOSS4 - 火球扇形", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:19301"], userControl: false)]
    public void 火球扇形(Event evt, ScriptAccessory sa)
        => 元素球扇形(sa, _fireballPositions, () => _fireZoneRot, evt.SourcePosition(), "火");

    [ScriptMethod(name: "BOSS4 - 冰球扇形", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:19300"], userControl: false)]
    public void 冰球扇形(Event evt, ScriptAccessory sa)
        => 元素球扇形(sa, _iceballPositions, () => _waterZoneRot, evt.SourcePosition(), "冰");

    [ScriptMethod(name: "BOSS4 - 雷球扇形", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:19302"], userControl: false)]
    public void 雷球扇形(Event evt, ScriptAccessory sa)
        => 元素球扇形(sa, _thunderballPositions, () => _thunderZoneRot, evt.SourcePosition(), "雷");

    private void 元素球扇形(ScriptAccessory sa, List<Vector3> balls, Func<float?> getZoneRot, Vector3 pos, string tag)
    {
        float zoneRot;
        lock (_zoneLock)
        {
            if (balls.Any(p => DistXZ(p, pos) <= 1f)) return;
            balls.Add(pos);
            Dbg(sa, $"{tag}球记录 #{balls.Count}：{pos:F1}");
            if (balls.Count < 2) return;

            pos = balls[0];
            balls.Clear();   // 收齐一波即清空，等下一波

            var rot = getZoneRot();
            if (rot is null)
            {
                Dbg(sa, $"{tag}球已收齐但{tag}区未记录，跳过");
                return;
            }
            zoneRot = rot.Value;
        }

        // 中心→球的朝向；该角度约定逆时针增加，顺时针转动即角度减小；区域上下对称，对180度取模
        var ballRad = MathF.Atan2(pos.X - Boss4中心.X, pos.Z - Boss4中心.Z);
        var deltaRad = ((ballRad - zoneRot) % MathF.PI + MathF.PI) % MathF.PI;
        var deltaDeg = deltaRad * 180f / MathF.PI;
        var duration = (uint)(7000 + deltaDeg / 30f * 1000f);
        // duration多于3s时加延迟，只在最后3s显示扇形
        var delay = duration > 3000 ? duration - 3000 : 0;
        var show = duration - delay;
        Dbg(sa, $"{tag}球→{tag}区顺时针 {deltaDeg:F1}度，duration {duration}ms（延迟{delay}ms后显示{show}ms）");

        foreach (var (rot, dirTag) in new[] { (zoneRot, "上"), (zoneRot + MathF.PI, "下") })
        {
            var dp = sa.FastDp($"{tag}区扇形-{dirTag}", Boss4中心, show, new Vector2(30f));
            dp.Delay = delay;
            dp.Rotation = rot;
            dp.Radian = 60f * MathF.PI / 180f;
            dp.ScaleMode = ScaleMode.ByTime;
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Fan, dp);
        }
    }

    private bool _inIceZone = true;   // true=自己在冰区，false=在火区（冰区取水区2015241的朝向）

    // 48399：指路冰火交界（偏冰区10度、离中心9m）3s，并初始化自己在冰区
    [ScriptMethod(name: "BOSS4 - 冰火交界初始指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:48399"])]
    public void 冰火交界初始指路(Event evt, ScriptAccessory sa)
    {
        var guidePos = 冰火交界指路点(sa, 靠近冰: true);
        if (guidePos is null)
        {
            Dbg(sa, $"48399：火区/水区未记录，无法指路");
            return;
        }
        lock (_zoneLock) _inIceZone = true;   // 初始化：自己在冰区
        Dbg(sa, $"48399 初始指路：{guidePos.Value:F1}，标记自己在冰区");
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement,
            sa.WaypointDp(guidePos.Value, 3000, 0, "冰火交界初始指路"));
    }

    // 圆环 2015243火 / 2015244水(视为冰属性) / 2015245雷：出现7s后AOE。
    [ScriptMethod(name: "BOSS4 - 元素圆环换区", eventType: EventTypeEnum.ObjectChanged, eventCondition: ["Operate:Add", "DataId:regex:^(201524[345])$"], suppress: 1000)]
    public async void 元素圆环换区(Event evt, ScriptAccessory sa)
    {
        if (!uint.TryParse(evt["DataId"], out var dataId)) return;
        var ringElement = dataId switch
        {
            2015243u => "火",
            2015244u => "冰",
            _ => "雷"
        };
        Dbg(sa, $"圆环出现：{ringElement}({dataId})，7s后AOE");

        await Task.Delay(5000);   // 还剩2s时判断

        bool goIce;
        lock (_zoneLock)
        {
            var myElement = _inIceZone ? "冰" : "火";
            if (ringElement != myElement)
            {
                Dbg(sa, $"圆环{ringElement}与自身{myElement}不同属性，忽略");
                return;
            }
            goIce = !_inIceZone;      // 自己是冰去火，是火去冰
            _inIceZone = goIce;       // 判定时刻立即更新计划区域，防止后续圆环读到旧值
        }

        var guidePos = 冰火交界指路点(sa, goIce);
        if (guidePos is null)
        {
            Dbg(sa, $"圆环换区：火区/水区未记录，无法指路");
            return;
        }
        Dbg(sa, $"圆环{ringElement}命中自身区域，指路换区到{(goIce ? "冰" : "火")}区：{guidePos.Value:F1}");
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement,
            sa.WaypointDp(guidePos.Value, 2000, 0, "冰火换区指路"));

        sa.Method.TTS("穿", 3);
        sa.Method.TextInfo("穿", 2000, false);
        Dbg(sa, $"已换区：现在在{(goIce ? "冰" : "火")}区");
    }

    // 求冰火两区交界方向：取上下4种组合中夹角最小的相邻交界、离我最近的一处，向目标区偏10度，中心往外9m
    private Vector3? 冰火交界指路点(ScriptAccessory sa, bool 靠近冰)
    {
        float fireRot, iceRot;
        lock (_zoneLock)
        {
            if (_fireZoneRot is null || _waterZoneRot is null) return null;
            fireRot = _fireZoneRot.Value;
            iceRot = _waterZoneRot.Value;
        }

        var mePos = sa.Data.MyObject?.Position ?? Boss4中心;
        var meRad = MathF.Atan2(mePos.X - Boss4中心.X, mePos.Z - Boss4中心.Z);

        List<(float Guide, float AbsDiff)> candidates = [];
        foreach (var i in new[] { iceRot, iceRot + MathF.PI })
        {
            foreach (var f in new[] { fireRot, fireRot + MathF.PI })
            {
                var diff = WrapPi(f - i);                 // 冰→火最短转角
                var boundary = i + diff / 2f;
                var bias = 10f * MathF.PI / 180f * MathF.Sign(diff);
                var guide = 靠近冰 ? boundary - bias : boundary + bias;
                candidates.Add((guide, MathF.Abs(diff)));
            }
        }

        var minDiff = candidates.Min(c => c.AbsDiff);
        var best = candidates
            .Where(c => c.AbsDiff <= minDiff + 0.01f)
            .OrderBy(c => MathF.Abs(WrapPi(c.Guide - meRad)))
            .First();

        return Boss4中心 + new Vector3(MathF.Sin(best.Guide), 0, MathF.Cos(best.Guide)) * 9f;
    }

    private static float WrapPi(float rad)
    {
        rad %= 2f * MathF.PI;
        if (rad > MathF.PI) rad -= 2f * MathF.PI;
        if (rad < -MathF.PI) rad += 2f * MathF.PI;
        return rad;
    }

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
