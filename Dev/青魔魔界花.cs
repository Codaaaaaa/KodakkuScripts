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
using FFXIVClientStructs.FFXIV.Client.Game;
using System.Threading.Tasks;

namespace Codaaaaaa.BlueMage;

[ScriptType(
    guid: "76fb14c3-1185-4580-b020-1f9a25e6f978",
    name: "青魔魔界花整合",
    territorys: [245, 358, 196, 452, 532, 587],
    version: "0.0.0.1",
    author: "Codaaaaaa",
    note: "攻略参考二二二二乱 A12S为拉一起复仇\n\n副本说明:\nT5:1T1N6D注意T青需要在MT位，其他随意，但每个人的kdy排序需相同\nT9:同上\nT13:同上\nA4S:1T1N6D，按照kdy排序1T青2N青345为拉小怪D青678为打腿组D青\nA8S:1T2N5D，按照kdy排序1T2N3盾N456D一组月78D二组月\nA12S:1T1N6D\n\nT青笔记：\nT5：-2s开怪\nT9: -2s预读小侦测开场，即刻白风稳仇+醒梦\nT13: 拉南 -2s预读小侦测开怪，即刻白风稳仇+醒梦\nA4S: 龙之力开场，MT全程远离人群\nA8S: 随意\nA12S: -5s龙之力 -2s魔法锤")]
public class BlueMage
{
    #region 用户设置
    [UserSetting("通用")] public static bool 超硬化提示横幅 { get; set; } = true;
    [UserSetting("通用")] public static bool 自动使用超硬化 { get; set; } = true;
    [UserSetting("通用")] public static bool 启用横幅 { get; set; } = true;
    [UserSetting("通用")] public static bool 启用TTS { get; set; } = true;
    [UserSetting("通用")] public static bool 指挥模式 { get; set; } = false;
    [UserSetting("T13")] public static bool 奶自动防御指示MT { get; set; } = true;

     [UserSetting("测试")] public static bool Debug输出 { get; set; } = false;
    [UserSetting("测试")] public static bool 所有职能都会尝试放超硬化 { get; set; } = false;
    // 开启后 A8S 所有按 index 分工的指路全部画出并按 index 上色（0蓝，1/2绿，3-7红），方便单人测试
    [UserSetting("测试")] public static bool A8S单人测试指路 { get; set; } = false;
    // 304 喷火
    #endregion

    #region 常量
    // 地图：T5 = 245，T9 = 358, T13 = 196, A4S = 452, A8S = 532, A12S = 587
    private const uint T5Territory = 245;
    private const uint T9Territory = 358;
    private const uint T13Territory = 196;
    private const uint A4STerritory = 452;
    private const uint A8STerritory = 532;
    private const uint A12STerritory = 587;

    private const uint TankStatus = 2124;
    private const uint DpsStatus = 2125;
    private const uint HealerStatus = 2126;
    private const uint HardenActionId = 11424;
    private const uint HardenActionType = 1;

    // T13 读条：百万核爆 2991 / 十亿核爆 3001 / 百万核爆冲 3008；奶对 MT 的防御指示技能 18306（GCD）
    private const uint HealerDefenseActionId = 18306;
    private const uint HealerDefenseActionType = 1;
    #endregion

    #region 状态
    private double _phase = 1.0;
    private uint _roleStatus = 0;

    private readonly List<uint> _meteorMarked = new();   // 本轮陨石被点名的人（entityId）
    private bool _meteorScheduled;                        // 本轮是否已排程处理，避免同一轮重复
    private DateTime _meteorLastMark = DateTime.MinValue; // 上次收到点名的时刻，用于区分“新一轮”

    private string? _autoCastLoopGuid;
    private uint _autoCastActionId;
    private uint _autoCastActionType;
    private uint _autoCastTargetId;
    private string _autoCastLabel = "";
    private DateTime _autoCastStart;
    private DateTime _autoCastLastPress;
    private double _autoCastBannerAt;
    private double _autoCastPressAt;
    private double _autoCastTimeout;
    private bool _autoCastAnnounced;
    private bool _autoCastCasting;
    private bool _autoCastAnnounceEnabled = true;
    private uint _autoCastLastBlockStatus;   // 上次因不可用被拦下时的 status 码，用于去重 debug 输出
    private Func<bool> _autoCastGate = () => true;

    // T13 Boss 血量播报
    private string? _t13HpLoopGuid;
    private uint _t13BossId;
    private bool _t13Hp75Done;
    private bool _t13Hp51Done;

    // T13 撞球绿圈
    private readonly List<uint> _t13BallCircles = new();  // 已画圈的球 SourceId
    private DateTime _t13BallLastTts = DateTime.MinValue;  // 上次撞球 TTS 时刻，用于同轮去重
    private bool _t13BallStopDrawing;                      // 596×2 后不再画新圈

    // T13 大地摇动点名
    private readonly List<uint> _quakeMarked = new();     // 本轮 0028 被点名的人
    private bool _quakeScheduled;                          // 本轮是否已排点
    private DateTime _quakeLastMark = DateTime.MinValue;   // 上次点名时刻，用于区分新一轮

    // A4S
    private uint _a4sDiscMarked;                              // 001F 圆盘点名
    private readonly List<uint> _a4sExtinctionMarks = new();  // Tether 0011 灭绝点名（同一人会被点五次）
    private int _a4sExtinctionDrawSeq;                        // 灭绝危险圈序号，保证 Name 唯一
    private string? _a4sAddHpGuid;                            // 3899 小怪血量检测
    private uint _a4sAddWatchId;                              // 被监控的小怪 SourceId
    private string? _a4sSewHpGuid;                            // 3892 扎针血量监控

    private static readonly Vector4 A4SYellow = new(1f, 0.85f, 0f, 1f);
    private static readonly Vector4 A4SBlue = new(0.2f, 0.4f, 1f, 1f);

    // A8S
    private int _a8s5424Count;                                // 本轮 5424 出现次数（第1个归 index3，第2个归 index4）
    private DateTime _a8s5424Last = DateTime.MinValue;        // 上次 5424 出现时刻，用于区分新一轮
    private readonly List<uint> _a8sThunderMarks = new();     // 雷属性压缩(1024)被点名者
    private bool _a8sThunderScheduled;                        // 本轮 10s 传雷检查是否已排程
    private DateTime _a8sThunderLast = DateTime.MinValue;     // 上次 1024 点名时刻，用于区分新一轮
    private bool _a8sP4BeamHardened;                          // P4 5678 只在第一次触发超硬化

    private static readonly Vector4 A8SYellow = new(1f, 0.85f, 0f, 1f);

    // 单人测试模式指路配色：index0 蓝，index1/2 绿，index3-7 红
    private static readonly Vector4 A8STestBlue = new(0.2f, 0.4f, 1f, 1f);
    private static readonly Vector4 A8STestGreen = new(0.2f, 0.85f, 0.3f, 1f);
    private static readonly Vector4 A8STestRed = new(1f, 0.25f, 0.2f, 1f);

    // P1 四个格子小怪出生点 → 各自负责的队列 index
    private static readonly (Vector3 Pos, int Index)[] A8SCellSpawns =
    [
        (new Vector3(-12f, 10.5f, -12f), 3),
        (new Vector3(-12f, 10.5f, 12f), 4),
        (new Vector3(12f, 10.5f, -12f), 5),
        (new Vector3(12f, 10.5f, 12f), 6),
    ];

    // 3899 小怪三个出生点 → 各自负责的队列 index
    private static readonly (Vector3 Pos, int Index)[] A4SAddSpawns =
    [
        (new Vector3(15.53f, 10.59f, -8.97f), 3),
        (new Vector3(0.00f, 10.75f, 18.00f), 4),
        (new Vector3(-15.53f, 10.59f, -8.97f), 5),
    ];

    // A12S
    private bool _a12s6647Done;                                  // 6647 龟壳指引只播报一次
    private string? _a12sRevengeHpGuid;                          // 复仇血量监控
    private string? _a12sSewHpGuid;                              // 时空门后扎针血量监控
    private int _a12sCrystalCount;                               // 6660 审判结晶触发次数
    private readonly List<uint> _a12sPurple = new();             // 1120 紫圈点名
    private readonly List<uint> _a12sStack = new();              // 1122 分摊点名
    private readonly List<uint> _a12sNear = new();               // 1123 近线点名
    private readonly List<uint> _a12sFar = new();                // 1124 远线点名
    private readonly List<(uint A, uint B)> _a12sNearPairs = new();  // Tether 001C 近线配对
    private readonly List<(uint A, uint B)> _a12sFarPairs = new();   // Tether 001D 远线配对

    private static readonly Vector4 A12SGreen = new(0.2f, 0.85f, 0.3f, 1f);
    private static readonly Vector4 A12SWhite = new(1f, 1f, 1f, 1f);

    // 6660 审判结晶四次落点：A→D→C→B 各点顺时针隔一个黄点的第二个黄点（半径 25.3，从北顺时针 22.5° 起，每次 +90°）
    private static readonly Vector3[] A12SCrystalSpots =
    [
        new(9.68f, 400f, -23.37f),
        new(23.37f, 400f, 9.68f),
        new(-9.68f, 400f, 23.37f),
        new(-23.37f, 400f, -9.68f),
    ];
    #endregion

    #region 换p
    [ScriptMethod(name: "Set Phase", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:regex:^setphase"], userControl: false)]
    public void SetPhase(Event evt, ScriptAccessory sa)
    {
        var parts = (evt["Message"] ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !double.TryParse(parts[1], out var p))
        {
            sa.Method.SendChat("/e 用法：/e setphase 3.1");
            return;
        }
        _phase = p;
        sa.Method.SendChat($"/e Phase: {_phase}");
    }

    [ScriptMethod(name: "Show Phase", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:phase"], userControl: false)]
    public void ShowPhase(Event evt, ScriptAccessory sa) => sa.Method.SendChat($"/e Phase: {_phase}");
    #endregion

    public void Init(ScriptAccessory sa)
    {
        _phase = 1;
        StopAutoCast(sa);
        StopBossHpWatch(sa);
        StopT13HpWatch(sa);
        sa.Method.RemoveDraw(".*");
        _rockPositions.Clear();
        _eatStoneCount.Clear();
        _t13BossId = 0;
        _t13BallCircles.Clear();
        _t13BallStopDrawing = false;
        _a4sDiscMarked = 0;
        _a4sExtinctionMarks.Clear();
        StopA4SAddHpWatch(sa);
        StopA4SSewHpWatch(sa);
        _a8s5424Count = 0;
        _a8sThunderMarks.Clear();
        _a8sThunderScheduled = false;
        _a8sP4BeamHardened = false;
        _a12s6647Done = false;
        _a12sCrystalCount = 0;
        A12SClearMechLists();
        StopA12SRevengeHpWatch(sa);
        StopA12SSewHpWatch(sa);
        RefreshRole(sa);
    }

    // 当前是否在指定地图；直接读客户端当前地图，方法开头 if (!InMap(T5Territory)) return; 即可限制
    private static unsafe bool InMap(uint territory) => GameMain.Instance()->CurrentTerritoryTypeId == territory;

    #region 通用方法
    private void RefreshRole(ScriptAccessory sa)
    {
        _roleStatus = 0;
        if (sa.Data.Objects.SearchByEntityId(sa.Data.Me) is IBattleChara me)
        {
            if (me.HasStatus(TankStatus)) _roleStatus = TankStatus;
            else if (me.HasStatus(HealerStatus)) _roleStatus = HealerStatus;
            else if (me.HasStatus(DpsStatus)) _roleStatus = DpsStatus;
        }

        if (_roleStatus == 0)
        {
            sa.Method.TextInfo("[青魔]未检测到职能buff", 5000, true);
            Dbg(sa, "职能检测：无 buff");
        }
        else
        {
            Dbg(sa, $"职能检测：{RoleName(_roleStatus)} ({_roleStatus})");
        }
    }

    [ScriptMethod(name: "职能buff刷新", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(2124|2125|2126)$"], userControl: false)]
    public void 职能buff刷新(Event evt, ScriptAccessory sa)
    {
        if (evt.TargetId() != sa.Data.Me) return;
        if (!uint.TryParse(evt["StatusID"], out var sid)) return;
        if (sid != TankStatus && sid != DpsStatus && sid != HealerStatus) return;

        _roleStatus = sid;
        Dbg(sa, $"StatusAdd 刷新职能：{RoleName(sid)} ({sid})");
    }

    private static string RoleName(uint s) => s switch
    {
        TankStatus => "坦克",
        DpsStatus => "DPS",
        HealerStatus => "治疗",
        _ => "无"
    };

    private void Dbg(ScriptAccessory sa, string msg)
    {
        if (!Debug输出) return;
        sa.Method.SendChat($"/e [青魔T5] {msg}");
    }

    // 统一播报：横幅与 TTS 各由独立设置控制
    private void Announce(ScriptAccessory sa, string text, int durationMs)
    {
        if (启用TTS) sa.Method.TTS(text);
        if (启用横幅) sa.Method.TextInfo(text, durationMs, true);
    }

    // 绕中心(0,0,0)在 XZ 平面顺时针（俯视，+X东 +Z南）旋转 deg 度
    private static Vector3 RotateCW(Vector3 p, float deg)
    {
        float r = deg * MathF.PI / 180f;
        float c = MathF.Cos(r), s = MathF.Sin(r);
        return new Vector3(p.X * c - p.Z * s, p.Y, p.X * s + p.Z * c);
    }

    // ms 毫秒后执行一次 action（用完自动注销）
    private void DelayAction(ScriptAccessory sa, int ms, Action action)
    {
        var start = DateTime.Now;
        string? guid = null;
        guid = sa.Method.RegistFrameworkUpdateAction(() =>
        {
            if ((DateTime.Now - start).TotalMilliseconds < ms) return;
            if (guid != null) sa.Method.UnregistFrameworkUpdateAction(guid);
            action();
        }, true, false);
    }
    #endregion

    #region 通用
    [ScriptMethod(name: "自动施放-确认停止", eventType: EventTypeEnum.ActionEffect, userControl: false)]
    public void 自动施放确认(Event evt, ScriptAccessory sa)
    {
        if (_autoCastLoopGuid == null || !_autoCastCasting) return;
        if (evt.SourceId() != sa.Data.Me) return;
        if (evt.ActionId() != _autoCastActionId) return;

        Dbg(sa, $"{_autoCastLabel}：已确认施放成功，停止重试");
        StopAutoCast(sa);
    }

    // StartCasting 11420 且 SourceId 是自己：以自己为中心画 2s 月环（外 20 内 15）
    [ScriptMethod(
        name: "通用 - 显示雷电咆哮范围",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:11420"])]
    public void 通用11420月环(Event evt, ScriptAccessory sa)
    {
        if (evt.SourceId() != sa.Data.Me) return;

        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = "通用-11420月环";
        dp.Color = new Vector4(0.7f, 0.3f, 1f, 1f);   // 紫色
        dp.Owner = sa.Data.Me;              // 跟随自己
        dp.Scale = new Vector2(20f);        // 外半径 20
        dp.InnerScale = new Vector2(8f);   // 内半径 5
        dp.Radian = 2f * MathF.PI;          // 完整一圈 360°
        dp.ScaleMode = ScaleMode.None;
        dp.DestoryAt = 2000;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
        Dbg(sa, "11420：自身月环 外20 内15，持续2s");
    }

    // pressAtMs：进战后开始按的时刻(ms)，纯粹的开按时间，不做 GCD 补偿。
    // 到点每 250ms 按一次，直到 ActionEffect 确认或超时。横幅只是提示，不阻塞开按（pressAt 早于横幅时也照按）。
    // enableGate：按下时机再次校验是否允许施放；默认沿用「自动使用超硬化」开关
    // announce=false：调用方已自行播报，抑制本次自动施放的横幅/TTS，只按技能
    private void ScheduleAutoCast(ScriptAccessory sa, uint actionId, uint actionType, uint targetId, string label, double pressAtMs, Func<bool>? enableGate = null, bool announce = true)
    {
        StopAutoCast(sa);

        double bannerDur = Math.Min(3000-500,pressAtMs-500);

        _autoCastAnnounceEnabled = announce;
        _autoCastGate = enableGate ?? (() => 自动使用超硬化);
        _autoCastActionId = actionId;
        _autoCastActionType = actionType;
        _autoCastTargetId = targetId;
        _autoCastLabel = label;
        _autoCastPressAt = Math.Max(0, pressAtMs);
        _autoCastBannerAt = Math.Max(0, _autoCastPressAt - bannerDur - 700);
        _autoCastTimeout = _autoCastPressAt + 4000;
        _autoCastStart = DateTime.Now;
        _autoCastLastPress = DateTime.MinValue;
        _autoCastAnnounced = false;
        _autoCastCasting = false;
        _autoCastLastBlockStatus = 0;

        _autoCastLoopGuid = sa.Method.RegistFrameworkUpdateAction(() =>
        {
            try
            {
                // Unregist 不杀本帧已排队的回调，兜底防止停止后又跑一次
                if (_autoCastLoopGuid == null) return;

                double elapsed = (DateTime.Now - _autoCastStart).TotalMilliseconds;

                if (!_autoCastAnnounced && elapsed >= _autoCastBannerAt)
                {
                    _autoCastAnnounced = true;
                    if (超硬化提示横幅 && _autoCastAnnounceEnabled)
                        Announce(sa, _autoCastLabel, (int)bannerDur);
                    Dbg(sa, $"{_autoCastLabel}：提示已发送");
                }

                if (elapsed < _autoCastPressAt) return;

                if (!_autoCastGate())
                {
                    Dbg(sa, $"{_autoCastLabel}：未满足施放条件，停止");
                    StopAutoCast(sa);
                    return;
                }

                if (elapsed > _autoCastTimeout)
                {
                    Dbg(sa, $"{_autoCastLabel}：监控超时，停止");
                    StopAutoCast(sa);
                    return;
                }

                // 玩家死亡时不按
                if (sa.Data.Objects.SearchByEntityId(sa.Data.Me) is IBattleChara me &&
                    (me.IsDead || me.CurrentHp == 0))
                {
                    Dbg(sa, $"{_autoCastLabel}：玩家已死亡，暂不施放");
                    return;
                }

                if ((DateTime.Now - _autoCastLastPress).TotalMilliseconds < 250) return;
                _autoCastLastPress = DateTime.Now;

                // 可用性检查（含冷却）：不可用则本次不按，等下次重试；status 变化时才输出 debug 避免刷屏
                uint status = GetActionStatus(_autoCastActionType, _autoCastActionId);
                if (status != 0)
                {
                    if (status != _autoCastLastBlockStatus)
                    {
                        _autoCastLastBlockStatus = status;
                        Dbg(sa, $"{_autoCastLabel}：技能 {_autoCastActionId} 暂不可用(status={status}，冷却剩余 {GetActionRecastRemain(_autoCastActionType, _autoCastActionId):F1}s)，等待重试");
                    }
                    return;
                }
                _autoCastLastBlockStatus = 0;

                _autoCastCasting = true;
                sa.Method.UseAction(_autoCastTargetId, _autoCastActionId, _autoCastActionType);
                Dbg(sa, $"{_autoCastLabel}：尝试施放 {_autoCastActionId}（等待确认）");
            }
            catch (Exception ex)
            {
                Dbg(sa, $"{_autoCastLabel} 监控异常：{ex.Message}");
                StopAutoCast(sa);
            }
        }, true, false);
    }

    // 技能可用状态：0 = 可用；非 0 为游戏内的不可用原因码（冷却中、被沉默、距离等）
    private static unsafe uint GetActionStatus(uint actionType, uint actionId)
        => ActionManager.Instance()->GetActionStatus((ActionType)actionType, actionId);

    // 冷却剩余秒数（未在冷却时为 0）
    private static unsafe float GetActionRecastRemain(uint actionType, uint actionId)
    {
        var am = ActionManager.Instance();
        return Math.Max(0f,
            am->GetRecastTime((ActionType)actionType, actionId) -
            am->GetRecastTimeElapsed((ActionType)actionType, actionId));
    }

    private void StopAutoCast(ScriptAccessory sa)
    {
        _autoCastCasting = false;
        _autoCastAnnounced = false;
        if (_autoCastLoopGuid == null) return;
        sa.Method.UnregistFrameworkUpdateAction(_autoCastLoopGuid);
        _autoCastLoopGuid = null;
    }
    #endregion
    #region T5
    [ScriptMethod(
        name: "-------T5-------",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(xx|xx)$"])]
    public void t5_start(Event evt, ScriptAccessory sa)
    {
    }

    [ScriptMethod(
        name: "T5 - 开头超硬化",
        eventType: EventTypeEnum.CombatChanged,
        eventCondition: ["InCombat:True"])]
    public void T5进入战斗(Event evt, ScriptAccessory sa)
    {
        if (!InMap(T5Territory)) return;
        if (!所有职能都会尝试放超硬化 && _roleStatus != TankStatus)
        {
            Dbg(sa, $"进入战斗：当前职能 {RoleName(_roleStatus)} 非坦克，跳过超硬化");
            return;
        }
        Dbg(sa, "进入战斗：预约超硬化，进战 3.5s 开按");
        ScheduleAutoCast(sa, HardenActionId, HardenActionType, sa.Data.Me, "横幅结束后开启超硬化", 3500);
    }

    [ScriptMethod(
        name: "T5 - 俯冲指路",
        eventType: EventTypeEnum.PlayActionTimeline,
        eventCondition: ["Id:141"])]
    public void 俯冲指路(Event evt, ScriptAccessory sa)
    {
        if (!InMap(T5Territory)) return;
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement,
            sa.WaypointDp(new Vector3(-24.13f, 50.23f, -16.11f), 3000, 0, "俯冲指路-终", sa.Data.DefaultSafeColor));
        Dbg(sa, "收到 141");
    }

    // 1247：矩形直线 AOE，宽 11 × 长(EffectRange) 30，从施法者位置沿其朝向
    [ScriptMethod(
        name: "T5 - 俯冲绘图",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:1247"])]
    public void 直线AOE1247(Event evt, ScriptAccessory sa)
    {
        if (!InMap(T5Territory)) return;
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = "1247直线AOE";
        dp.Color = sa.Data.DefaultDangerColor;
        dp.Position = evt.SourcePosition();
        dp.Scale = new Vector2(11f, 30f);
        dp.Rotation = evt.SourceRotation();
        dp.FixRotation = true;
        dp.ScaleMode = ScaleMode.None;
        dp.DestoryAt = 900;
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(
        name: "T5 - 手动重置",
        eventType: EventTypeEnum.CombatChanged,
        eventCondition: ["InCombat:False"],
        userControl: false)]
    public void 手动重置(Event evt, ScriptAccessory sa)
    {
        if (!InMap(T5Territory)) return;
        _phase = 1;
        StopAutoCast(sa);
        sa.Method.RemoveDraw(".*");
        RefreshRole(sa);
    }
    #endregion

    #region T9
    [ScriptMethod(
        name: "-------T9-------",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(xx|xx)$"])]
    public void t9_start(Event evt, ScriptAccessory sa){}

    [ScriptMethod(
        name: "T9 - 开头超硬化",
        eventType: EventTypeEnum.CombatChanged,
        eventCondition: ["InCombat:True"])]
    public void T9进入战斗(Event evt, ScriptAccessory sa)
    {
        if (!InMap(T9Territory)) return;
        if (!所有职能都会尝试放超硬化 && _roleStatus != TankStatus)
        {
            Dbg(sa, $"进入战斗：当前职能 {RoleName(_roleStatus)} 非坦克，跳过超硬化");
            return;
        }
        Dbg(sa, "进入战斗：预约超硬化，进战 2.35s 开按");
        ScheduleAutoCast(sa, HardenActionId, HardenActionType, sa.Data.Me, "横幅结束后开启超硬化", 2350);
    }

    // PlayActionTimeline Id:140（_phase<2，首次上天）：指路去{0,0,20} 3s，_phase=2.1，开启新一轮陨石记录。场中心为 0,0,0
    [ScriptMethod(
        name: "T9 - 第一次上天指路",
        eventType: EventTypeEnum.PlayActionTimeline,
        eventCondition: ["Id:140"])]
    public void T9_140首次上天(Event evt, ScriptAccessory sa)
    {
        if (!InMap(T9Territory)) return;
        if (_phase >= 2) return;

        _phase = 2.1;
        _meteorMarked.Clear();
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement,
            sa.WaypointDp(new Vector3(0, 0, 20), 3000, 0, "T9-140指路", sa.Data.DefaultSafeColor));
        Dbg(sa, "T9 140：指路{0,0,20} 3s，_phase=2.1");
    }

    // 陨石指路：TargetIcon Id:0007 每轮点 3 人。记录被点的人；若点到自己，等 100ms 让三人都记录完再算点位。
    [ScriptMethod(
        name: "T9 - 三连陨石指路",
        eventType: EventTypeEnum.TargetIcon,
        eventCondition: ["Id:0007"])]
    public void T9陨石指路(Event evt, ScriptAccessory sa)
    {
        if (!InMap(T9Territory)) return;
        if (_phase < 2) return;

        // 点名头标同帧下发；距上次点名超过 10s 即视为新一轮：清空上一轮记录、允许重新排点
        var now = DateTime.Now;
        if ((now - _meteorLastMark).TotalMilliseconds > 10000)
        {
            _meteorMarked.Clear();
            _meteorScheduled = false;
        }
        _meteorLastMark = now;

        var marked = evt.TargetId();
        if (!_meteorMarked.Contains(marked)) _meteorMarked.Add(marked);

        // 同一轮只排点一次（本方法一轮触发三次，最多一次是自己）
        if (marked == sa.Data.Me && !_meteorScheduled)
        {
            _meteorScheduled = true;
            DelayAction(sa, 100, () => 陨石排点(sa));
        }
    }

    // 按 index 排序(0最小)：最小→120°、中间→240°、最大→0°。一段指到内圈(5.8) 7.5s，随后二段指到外圈(20) 5s。
    private void 陨石排点(ScriptAccessory sa)
    {
        _meteorMarked.Sort((a, b) => sa.Data.PartyList.IndexOf(a).CompareTo(sa.Data.PartyList.IndexOf(b)));
        int rank = _meteorMarked.IndexOf(sa.Data.Me);
        if (rank < 0) return;

        float angle = rank switch { 0 => 120f, 1 => 240f, _ => 0f };
        var inner = RotateCW(new Vector3(0, 0, 5.8f), angle);   // 一段落点
        var outer = RotateCW(new Vector3(0, 0, 20f), angle);    // 二段：同方向往外 20

        // 一段：立即显示 7.5s
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement,
            sa.WaypointDp(inner, 8000, 0, "陨石指路-1", sa.Data.DefaultSafeColor));
        // 二段：7.5s 后显示，持续 5s（DestoryAt = 起显 + 持续）
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement,
            sa.WaypointDp(outer, 3000, 8000, "陨石指路-2", sa.Data.DefaultSafeColor));

        
        DelayAction(sa, 7000, () =>
            sa.Method.VfxMethod.CreateOmen(152, new Vector3(80f, 1f, 80f),
                new Vector3(0, 0, 0), 0f, sa.Data.DefaultSafeColor, 4000));

        Dbg(sa, $"陨石指路：rank{rank} 内{inner:F1} 外{outer:F1}");
    }

    // ActionEffect ActionId:2027 且 TargetId 是自己：删除陨石指路，显示落点指路 4s（T→中心{0,0,0}，非T→外{0,0,20}），_phase=2.2
    [ScriptMethod(
        name: "T9 - 三连陨石指路后半",
        eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:2027"])]
    public void T9陨石落地(Event evt, ScriptAccessory sa)
    {
        if (!InMap(T9Territory)) return;
        if (evt.TargetId() != sa.Data.Me) return;
        if (_phase != 2.1) return;

        sa.Method.RemoveDraw("陨石指路-.*");
        var target = _roleStatus == TankStatus ? new Vector3(0, 0, 0) : new Vector3(0, 0, 20);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement,
            sa.WaypointDp(target, 4000, 0, "陨石落地指路", sa.Data.DefaultSafeColor));
        _phase = 2.2;
        Dbg(sa, $"陨石落地(2027)：{(_roleStatus == TankStatus ? "T→中心" : "非T→外20")}，_phase=2.2");
    }

    // TargetIcon Id:0009
    [ScriptMethod(
        name: "T9 - 马拉松自动开疾跑",
        eventType: EventTypeEnum.TargetIcon,
        eventCondition: ["Id:0009"])]
    public void T9开疾跑(Event evt, ScriptAccessory sa)
    {
        if (!InMap(T9Territory)) return;
        if (_phase != 2.2) return;

        sa.Method.TextInfo("开启疾跑", 1000, false);
        sa.Method.SendChat("/ac 冲刺");
    }

    // TargetIcon Id:000A 且 _phase=2.2：_phase=2.3，画从{0,0,20}(南)顺时针 300° 的圆环弧带（跑动路径）
    [ScriptMethod(
        name: "T9 - 马拉松指路",
        eventType: EventTypeEnum.TargetIcon,
        eventCondition: ["Id:000A"])]
    public void T9弧线指路(Event evt, ScriptAccessory sa)
    {
        if (!InMap(T9Territory)) return;
        if (_phase != 2.2) return;
        _phase = 2.3;

        画顺时针弧(sa, 0f, 300f, 20f, 21.5f, 16000, "T9-顺时针弧");
        Dbg(sa, "T9 000A：从南顺时针300°弧，_phase=2.3");
    }

    // 以中心(0,0,0)画一段圆环弧带。startDeg：起始方向(游戏朝向角，0=南，逆时针为正)；
    // sweepDeg：从起始方向顺时针扫过的角度；半径带 innerR~outerR，持续 durationMs。
    private static void 画顺时针弧(ScriptAccessory sa, float startDeg, float sweepDeg, float innerR, float outerR, int durationMs, string name)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = sa.Data.DefaultSafeColor;
        dp.Position = new Vector3(0, 0, 0);
        // Rotation = 弧带中心朝向；顺时针 = 游戏朝向角递减，弧对称于中心 → 中心 = 起始 - sweep/2
        dp.Rotation = (startDeg - sweepDeg / 2f) * MathF.PI / 180f;
        dp.Radian = sweepDeg * MathF.PI / 180f;
        dp.Scale = new Vector2(outerR, outerR);
        dp.InnerScale = new Vector2(innerR, innerR);
        dp.ScaleMode = ScaleMode.None;
        dp.DestoryAt = durationMs;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
    }
    
    // 石头(2751/2752)按到达顺序记录的落点；每满 6 个视为新一批，自动清空
    private readonly List<Vector3> _rockPositions = new();

    // 石头吃 2788 的次数(按 SourceId 分)；某个石头吃满 3 次即触发一次处理
    private readonly Dictionary<ulong, int> _eatStoneCount = new();

    // 每次收到 AddCombatant DataId 2751/2752：按先后顺序记录 SourcePosition
    [ScriptMethod(
        name: "T9 - 记录石头落点",
        eventType: EventTypeEnum.AddCombatant,
        eventCondition: ["DataId:regex:^(2751|2752)$"],
        userControl: false)]
    public void T9记录石头落点(Event evt, ScriptAccessory sa)
    {
        if (_phase <= 2.2) return;
        if (!InMap(T9Territory)) return;
        if (_rockPositions.Count >= 6) _rockPositions.Clear();
        var pos = evt.SourcePosition();
        _rockPositions.Add(pos);
        Dbg(sa, $"记录石头落点 #{_rockPositions.Count}：{pos:F1}");
    }

    // AddCombatant DataId 2748(石头)：状态可能晚于 AddCombatant 下发，轮询检测 459/460/461
    [ScriptMethod(
        name: "T9 - 小怪指引",
        eventType: EventTypeEnum.AddCombatant,
        eventCondition: ["DataId:2748"])]
    public async void T9石头点名(Event evt, ScriptAccessory sa)
    {
        if (_phase < 2.3) return;
        if (!InMap(T9Territory)) return;
        await Task.Delay(500);
        检测石头状态(sa, evt.SourceId(), 0);
    }

    // 轮询 2748 身上的状态(每 100ms，最多 ~1.5s)，命中后按职责处理
    private void 检测石头状态(ScriptAccessory sa, ulong objId, int attempt)
    {
        if (sa.Data.Objects.SearchById(objId) is not IBattleChara obj)
        {
            if (attempt < 15) DelayAction(sa, 100, () => 检测石头状态(sa, objId, attempt + 1));
            return;
        }

        if (obj.HasStatus(459))
        {
            // 459：DPS 用导弹技能吃这块石头 → 播报 + 连线(channeling 4，绿)链接自己到石头，直到它死亡
            if (_roleStatus == DpsStatus)
            {
                sa.Method.TTS("导弹绿色");
                连线8秒(sa, 4, objId, "石头459");
                Dbg(sa, "石头 459：DPS，连线绿(channeling 4)");
            }
        }
        else if (obj.HasStatus(460))
        {
            // 460：治疗(N)吃陨石 → 播报 + 连线(channeling 5，红) + 后三个石头落点画 3m 绿圈
            if (_roleStatus == HealerStatus)
            {
                sa.Method.TTS("拉红色吃陨石");
                连线8秒(sa, 5, objId, "石头460");
                画石头绿圈(sa, firstThree: false);
                Dbg(sa, "石头 460：治疗，连线红(channeling 5)+后三石头绿圈");
            }
        }
        else if (obj.HasStatus(461))
        {
            // 461：坦克(T)吃陨石 → 播报 + 连线(channeling 3，蓝) + 前三个石头落点画 3m 绿圈
            if (_roleStatus == TankStatus)
            {
                sa.Method.TTS("拉蓝色吃陨石");
                连线8秒(sa, 3, objId, "石头461");
                画石头绿圈(sa, firstThree: true);
                Dbg(sa, $"石头 461：坦克，连线蓝(channeling 3)+前三石头绿圈 {objId}");
            }
        }
        else
        {
            // 三个状态都还没上，继续等
            if (attempt < 15) DelayAction(sa, 100, () => 检测石头状态(sa, objId, attempt + 1));
        }
    }

    // 从自己到 targetId 画一根 channeling 连线，持续 8 秒后自动消失
    private void 连线8秒(ScriptAccessory sa, uint channelingIndex, ulong targetId, string tag)
        => 连线(sa, channelingIndex, targetId, 8000, tag);

    // 从自己到 targetId 画一根 channeling 连线，durationMs 毫秒后自动消失
    private void 连线(ScriptAccessory sa, uint channelingIndex, ulong targetId, int durationMs, string tag)
    {
        if (sa.Data.Objects.SearchByEntityId(sa.Data.Me) is not IBattleChara me) return;

        nint handle = sa.Method.VfxMethod.CreateChanneling(channelingIndex, me.GameObjectId, targetId, null, durationMs);
        if (handle == 0) Dbg(sa, $"{tag}：连线创建失败");
    }

    // 在石头落点列表的前三个 / 后三个坐标上用 imgui 各画一个半径 3m 的绿圈
    private void 画石头绿圈(ScriptAccessory sa, bool firstThree)
    {
        int n = _rockPositions.Count;
        if (n == 0) return;

        int start = firstThree ? 0 : Math.Max(0, n - 3);
        int end = firstThree ? Math.Min(3, n) : n;
        for (int i = start; i < end; i++)
        {
            var dp = sa.Data.GetDefaultDrawProperties();
            dp.Name = $"石头绿圈-{(firstThree ? "前" : "后")}-{i}";
            dp.Color = sa.Data.DefaultSafeColor;
            dp.Position = _rockPositions[i];
            dp.Scale = new Vector2(3f);   // 半径 3m
            dp.ScaleMode = ScaleMode.None;
            dp.DestoryAt = 20000;
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
        }
    }

    // PlayActionTimeline Id:2788(吃石头)：按 SourceId 累计次数，同一块石头吃满 3 次时，
    // 若自己是 DPS → 连线到该石头 + TTS(快导弹)
    [ScriptMethod(
        name: "T9 - 小怪导弹提醒",
        eventType: EventTypeEnum.PlayActionTimeline,
        eventCondition: ["Id:2788"])]
    public void T9石头吃三次(Event evt, ScriptAccessory sa)
    {
        if (!InMap(T9Territory)) return;

        ulong objId = evt.SourceId();
        if (objId == 0) return;

        int count = _eatStoneCount.TryGetValue(objId, out var c) ? c + 1 : 1;
        _eatStoneCount[objId] = count;
        Dbg(sa, $"石头 {objId:X} 吃石头 x{count}");

        if (count != 3) return;   // 只在第 3 次触发一次

        if (_roleStatus == DpsStatus || 所有职能都会尝试放超硬化)
        {
            连线8秒(sa, 4, objId, "吃三次石头");
            sa.Method.TTS("快导弹");
            Dbg(sa, $"石头 {objId:X} 吃满 3 次：DPS 连线绿+播报");
        }
    }

    // ActionEffect ActionId:2023：指路去{0,0,20}，_phase=3
    [ScriptMethod(
        name: "T9 - 百万核爆后指路集合",
        eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:2023"])]
    public void T9_2023指路(Event evt, ScriptAccessory sa)
    {
        if (!InMap(T9Territory)) return;
        if (_phase >= 3) return;
        if (!所有职能都会尝试放超硬化 && _roleStatus == TankStatus) return;

        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement,
            sa.WaypointDp(new Vector3(0, 0, 20), 4000, 0, "T9-2023指路", sa.Data.DefaultSafeColor));
        _phase = 3;
        Dbg(sa, "T9 2023：指路{0,0,20}，_phase=3");
    }

    // PlayActionTimeline Id:140（_phase==3）：非T → 远离T；T → 远离人群并 3s 后开启超硬化
    [ScriptMethod(
        name: "T9 - 百万核爆后T自动超硬化",
        eventType: EventTypeEnum.PlayActionTimeline,
        eventCondition: ["Id:140"])]
    public void T9_140远离(Event evt, ScriptAccessory sa)
    {
        if (!InMap(T9Territory)) return;
        if (_phase != 3) return;

        if (所有职能都会尝试放超硬化 || _roleStatus == TankStatus)
        {
            // 是T：远离人群，随后超硬化
            sa.Method.TTS("远离人群");
            ScheduleAutoCast(sa, HardenActionId, HardenActionType, sa.Data.Me, "远离人群后超硬化", 3000);
        }
        else
        {
            // 不是T：远离T
            sa.Method.TTS("远离T");
            sa.Method.TextInfo("远离T", 4000, true);
        }
    }

    // StartCasting ActionId 2107：若自己职能不是 T，自动使用技能 7559(沉稳)，兜底 2.5s
    [ScriptMethod(
        name: "T9 - 百万核爆后非T自动沉稳",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:2107"])]
    public void T9_2107沉稳(Event evt, ScriptAccessory sa)
    {
        if (!InMap(T9Territory)) return;
        if (_roleStatus == TankStatus)
        {
            Dbg(sa, "2107：当前职能为坦克，跳过沉稳");
            return;
        }
        Dbg(sa, "2107：预约沉稳(7559)，兜底 2.5s");
        ScheduleAutoCast(sa, 7559, HardenActionType, sa.Data.Me, "使用沉稳", 2500);
    }

    // StartCasting 2107 后开启 Boss 血量监控：Boss 血量 <15% 时 TTS+TextInfo 提示"扎针"（只报一次，随 Init 重置）
    [ScriptMethod(
        name: "T9 - Boss残血扎针提醒",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:2107"])]
    public void T9_2107血量监控(Event evt, ScriptAccessory sa)
    {
        if (!InMap(T9Territory)) return;
        StartBossHpWatch(sa, evt.SourceId());
        Dbg(sa, $"2107：开启 Boss 血量监控 {evt.SourceId():X}");
    }

    private string? _bossHpLoopGuid;
    private bool _bossHpAnnounced;

    // 每帧轮询 bossId 血量，跌破 15% 时提示一次"扎针"
    private void StartBossHpWatch(ScriptAccessory sa, ulong bossId)
    {
        StopBossHpWatch(sa);
        if (bossId == 0) return;

        _bossHpLoopGuid = sa.Method.RegistFrameworkUpdateAction(() =>
        {
            if (_bossHpLoopGuid == null || _bossHpAnnounced) return;
            if (sa.Data.Objects.SearchById(bossId) is not IBattleChara boss || boss.MaxHp == 0) return;

            double ratio = boss.CurrentHp / (double)boss.MaxHp;
            if (ratio > 0.15) return;

            _bossHpAnnounced = true;
            Announce(sa, "扎针", 5000);
            Dbg(sa, $"Boss 血量 {ratio:P1} <15%，播报扎针");
        }, true, false);
    }

    private void StopBossHpWatch(ScriptAccessory sa)
    {
        _bossHpAnnounced = false;
        if (_bossHpLoopGuid == null) return;
        sa.Method.UnregistFrameworkUpdateAction(_bossHpLoopGuid);
        _bossHpLoopGuid = null;
    }
    #endregion

    #region T13
    [ScriptMethod(
        name: "-------T13-------",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(xx|xx)$"])]
    public void t13_start(Event evt, ScriptAccessory sa){}

    // 开场 2.35s 后超硬化：仅 T 职，且需开启「自动使用超硬化」
    [ScriptMethod(
        name: "T13 - 开头超硬化",
        eventType: EventTypeEnum.CombatChanged,
        eventCondition: ["InCombat:True"])]
    public void T13进入战斗(Event evt, ScriptAccessory sa)
    {
        if (!InMap(T13Territory)) return;
        _phase = 1;
        _t13BossId = 0;
        _t13BallCircles.Clear();
        _t13BallStopDrawing = false;
        _quakeMarked.Clear();
        _quakeScheduled = false;
        StartT13HpWatch(sa);   // 全职责：监控 Boss 血量转场播报
        if (!所有职能都会尝试放超硬化 && _roleStatus != TankStatus)
        {
            Dbg(sa, $"进入战斗：当前职能 {RoleName(_roleStatus)} 非坦克，跳过超硬化");
            return;
        }
        Dbg(sa, "进入战斗：预约超硬化，进战 2.35s 开按");
        ScheduleAutoCast(sa, HardenActionId, HardenActionType, sa.Data.Me, "横幅结束后开启超硬化", 2350);
    }

    // 百万核爆自动超硬化：读条 2991 → 1.8s 后开超硬化。不检查职责；仅在未开启「奶自动防御指示MT」时生效
    [ScriptMethod(
        name: "T13 - 百万核爆自动超硬化",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:2991"])]
    public void T13百万核爆超硬化(Event evt, ScriptAccessory sa)
    {
        if (!InMap(T13Territory)) return;
        if (_phase > 1) return;
        _t13BossId = evt.SourceId();
        if (奶自动防御指示MT)
        {
            Dbg(sa, "百万核爆：已开启奶自动防御指示MT，跳过自动超硬化");
            return;
        }
        Dbg(sa, "百万核爆：预约超硬化，1.8s 开按");
        ScheduleAutoCast(sa, HardenActionId, HardenActionType, sa.Data.Me, "百万核爆超硬化", 1800);
    }

    // 百万核爆奶自动防御指示：读条 2991 → 1.8s 后对 index 0(MT) 使用 18306(GCD)。仅在开启「奶自动防御指示MT」时生效
    [ScriptMethod(
        name: "T13 - 百万核爆奶自动防御指示",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:2991"])]
    public void T13百万核爆防御指示(Event evt, ScriptAccessory sa)
    {
        if (!InMap(T13Territory)) return;
        if (!奶自动防御指示MT) return;
        if (_phase > 1) return;

        if (sa.Data.PartyList.Count == 0)
        {
            Dbg(sa, "百万核爆防御指示：队列为空，无法取 index 0");
            return;
        }
        var mt = sa.Data.PartyList[0];
        Dbg(sa, $"百万核爆防御指示：预约对 MT {mt:X} 使用 {HealerDefenseActionId}，1.8s 开按");
        ScheduleAutoCast(sa, HealerDefenseActionId, HealerDefenseActionType, mt, "百万核爆给MT防御", 1800, () => 奶自动防御指示MT);
    }

    // 十亿核爆(3001)：全体大 AOE，提示减伤
    [ScriptMethod(
        name: "T13 - 十亿核爆减伤提示",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:3001"])]
    public void T13十亿核爆(Event evt, ScriptAccessory sa)
    {
        if (!InMap(T13Territory)) return;
        _t13BossId = evt.SourceId();
        Announce(sa, "大aoe注意减伤", 4000);
        Dbg(sa, "十亿核爆：提示减伤");
    }

    // Boss 血量监控：跌破 75% / 51% 各播报一次转场提示（全职责）。BossId 由核爆读条捕获
    private void StartT13HpWatch(ScriptAccessory sa)
    {
        StopT13HpWatch(sa);
        _t13Hp75Done = false;
        _t13Hp51Done = false;

        _t13HpLoopGuid = sa.Method.RegistFrameworkUpdateAction(() =>
        {
            if (_t13HpLoopGuid == null) return;
            if (_t13BossId == 0) return;
            if (sa.Data.Objects.SearchByEntityId(_t13BossId) is not IBattleChara boss || boss.MaxHp == 0) return;

            double ratio = boss.CurrentHp / (double)boss.MaxHp;

            if (!_t13Hp75Done && ratio <= 0.75)
            {
                _t13Hp75Done = true;
                if (启用TTS) sa.Method.TTS("即将大aoe转场，注意减伤");
                Dbg(sa, $"Boss 血量 {ratio:P1} ≤75%，播报转场");
            }
            if (!_t13Hp51Done && ratio <= 0.51)
            {
                _t13Hp51Done = true;
                if (启用TTS) sa.Method.TTS("即将大aoe转场，注意减伤");
                Dbg(sa, $"Boss 血量 {ratio:P1} ≤51%，播报转场");
            }
        }, true, false);
    }

    private void StopT13HpWatch(ScriptAccessory sa)
    {
        _t13Hp75Done = false;
        _t13Hp51Done = false;
        if (_t13HpLoopGuid == null) return;
        sa.Method.UnregistFrameworkUpdateAction(_t13HpLoopGuid);
        _t13HpLoopGuid = null;
    }

    // 导弹小怪(3306) 变为可选中：DPS 连线该小怪 10s，横幅提示"导弹小怪" 3s
    [ScriptMethod(
        name: "T13 - 导弹小怪连线",
        eventType: EventTypeEnum.Targetable,
        eventCondition: ["Targetable:True", "DataId:3306"])]
    public void T13导弹小怪(Event evt, ScriptAccessory sa)
    {
        if (!InMap(T13Territory)) return;
        if (_roleStatus != DpsStatus)
        {
            Dbg(sa, $"导弹小怪：当前职能 {RoleName(_roleStatus)} 非 DPS，跳过连线");
            return;
        }
        uint mob = evt.SourceId();
        连线(sa, 3, mob, 10000, "导弹小怪");
        if (启用横幅) sa.Method.TextInfo("导弹小怪", 3000, true);
        Dbg(sa, $"导弹小怪：DPS 连线 {mob:X} 10s");
    }

    // 撞球(3305) 生成：给球本体画 3m 安全色圈 20s；同一轮成批生成，撞球 TTS 只播一次
    [ScriptMethod(
        name: "T13 - 撞球绿圈",
        eventType: EventTypeEnum.AddCombatant,
        eventCondition: ["DataId:3305"])]
    public void T13撞球生成(Event evt, ScriptAccessory sa)
    {
        if (!InMap(T13Territory)) return;
        if (_t13BallStopDrawing)
        {
            Dbg(sa, "撞球：已进入避球阶段，不再画圈");
            return;
        }

        uint ballId = evt.SourceId();
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = $"T13撞球圈-{ballId:X}";
        dp.Color = sa.Data.DefaultSafeColor;
        dp.Owner = ballId;              // 跟随球本体
        dp.Scale = new Vector2(3f);     // 半径 3m
        dp.ScaleMode = ScaleMode.None;
        dp.DestoryAt = 20000;
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
        if (!_t13BallCircles.Contains(ballId)) _t13BallCircles.Add(ballId);

        // 同一轮的球在 1s 内成批生成：距上次 TTS >1.5s 才视为新一轮，只播一次
        var now = DateTime.Now;
        if ((now - _t13BallLastTts).TotalMilliseconds > 1500)
        {
            if (启用TTS) sa.Method.TTS("撞球");
            Dbg(sa, "撞球：TTS 撞球（新一轮）");
        }
        _t13BallLastTts = now;
        Dbg(sa, $"撞球：画绿圈 {ballId:X}");
    }

    // 撞球被撞(3004)：SourceId 即撞掉的球本体，清除其对应的绿圈
    [ScriptMethod(
        name: "T13 - 撞球消除绿圈",
        eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:3004"],
        userControl: false)]
    public void T13撞球消除(Event evt, ScriptAccessory sa)
    {
        if (!InMap(T13Territory)) return;
        uint ballId = evt.SourceId();
        if (!_t13BallCircles.Remove(ballId)) return;   // 不是我们画过的球
        sa.Method.RemoveDraw($"T13撞球圈-{ballId:X}");
        Dbg(sa, $"撞球：3004 清除绿圈 {ballId:X}");
    }

    // 避球易伤(596) 叠到 2 层：提示避开球，清掉所有绿圈，且后续不再画新圈
    [ScriptMethod(
        name: "T13 - 2层后避开球提示",
        eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:596", "StackCount:2"])]
    public void T13避开球(Event evt, ScriptAccessory sa)
    {
        if (!InMap(T13Territory)) return;
        _t13BallStopDrawing = true;
        foreach (var ballId in _t13BallCircles)
            sa.Method.RemoveDraw($"T13撞球圈-{ballId:X}");
        _t13BallCircles.Clear();
        if (启用横幅) sa.Method.TextInfo("避开球", 5000, true);
        Dbg(sa, "596×2：避开球，清除全部绿圈并停止画圈");
    }

    // 百万核爆冲(3008)：读条后 2.5s 自动超硬化。全职责，仅需开启「自动使用超硬化」
    [ScriptMethod(
        name: "T13 - 百万核爆冲自动超硬化",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:3008"])]
    public void T13百万核爆冲(Event evt, ScriptAccessory sa)
    {
        if (!InMap(T13Territory)) return;
        _t13BossId = evt.SourceId();
        Dbg(sa, "百万核爆冲：预约超硬化，2.5s 开按");
        ScheduleAutoCast(sa, HardenActionId, HardenActionType, sa.Data.Me, "百万核爆冲超硬化", 2500);
    }

    // 万亿核爆预告（系统消息"距万亿核爆咏唱完毕还有 10 秒！"）：进入 P4，5s 后全员自动超硬化
    [ScriptMethod(
        name: "T13 - 万亿核爆超硬化",
        eventType: EventTypeEnum.Chat,
        eventCondition: ["Type:SystemMessage", "Message:regex:距万亿核爆咏唱完毕还有 10 秒"])]
    public void T13万亿核爆预告(Event evt, ScriptAccessory sa)
    {
        if (!InMap(T13Territory)) return;
        _phase = 4;
        Dbg(sa, "万亿核爆预告：_phase=4，预约 5s 后全员超硬化");
        ScheduleAutoCast(sa, HardenActionId, HardenActionType, sa.Data.Me, "万亿核爆超硬化", 5000);
    }

    // 死亡轮回(3010)：范围死刑，全员提示 T 远离人群；T 额外 3s 后自动超硬化
    [ScriptMethod(
        name: "T13 - 死亡轮回T超硬化",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:3010"])]
    public void T13死亡轮回(Event evt, ScriptAccessory sa)
    {
        if (!InMap(T13Territory)) return;
        _t13BossId = evt.SourceId();
        Announce(sa, "范围死刑，T远离人群", 2500);
        if (_roleStatus == TankStatus || 所有职能都会尝试放超硬化)
        {
            Dbg(sa, "死亡轮回：T 预约超硬化，1s 开按");
            ScheduleAutoCast(sa, HardenActionId, HardenActionType, sa.Data.Me, "死亡轮回超硬化", 1000, announce: false);
        }
    }

    // P4 百万核爆(2991)：全员同样 1.8s 后自动超硬化，不看防御指示
    [ScriptMethod(
        name: "T13 - P4百万核爆全员超硬化",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:2991"])]
    public void T13_P4百万核爆(Event evt, ScriptAccessory sa)
    {
        if (!InMap(T13Territory)) return;
        if (_phase != 4) return;
        _t13BossId = evt.SourceId();
        Dbg(sa, "P4百万核爆：全员预约超硬化，1.8s 开按");
        ScheduleAutoCast(sa, HardenActionId, HardenActionType, sa.Data.Me, "百万核爆超硬化", 1800);
    }

    // 大地摇动 TargetIcon 0028：两人分摊。按队列 index 排序，小 index 去 boss 面向左 5m、大 index 去右 5m
    [ScriptMethod(
        name: "T13 - 大地摇动指路",
        eventType: EventTypeEnum.TargetIcon,
        eventCondition: ["Id:0028"])]
    public void T13大地摇动(Event evt, ScriptAccessory sa)
    {
        if (!InMap(T13Territory)) return;

        // 点名头标同帧下发；距上次点名超过 10s 即视为新一轮
        var now = DateTime.Now;
        if ((now - _quakeLastMark).TotalMilliseconds > 10000)
        {
            _quakeMarked.Clear();
            _quakeScheduled = false;
        }
        _quakeLastMark = now;

        var marked = evt.TargetId();
        if (!_quakeMarked.Contains(marked)) _quakeMarked.Add(marked);

        // 点到自己后等 100ms 让两人都记录完再算左右
        if (marked == sa.Data.Me && !_quakeScheduled)
        {
            _quakeScheduled = true;
            DelayAction(sa, 100, () => 大地摇动排点(sa));
        }
    }

    private void 大地摇动排点(ScriptAccessory sa)
    {
        if (sa.Data.Objects.FirstOrDefault(o => o.DataId == 3304) is not IBattleChara boss)
        {
            Dbg(sa, "大地摇动：未找到 boss(3304)");
            return;
        }

        _quakeMarked.Sort((a, b) => sa.Data.PartyList.IndexOf(a).CompareTo(sa.Data.PartyList.IndexOf(b)));
        int rank = _quakeMarked.IndexOf(sa.Data.Me);
        if (rank < 0) return;

        // FFXIV：Rotation=0 面向南(+Z)，前向量 (sin, 0, cos)；左=前向逆时针 90°，右=顺时针 90°
        var forward = new Vector3(MathF.Sin(boss.Rotation), 0, MathF.Cos(boss.Rotation));
        var dir = rank == 0 ? RotateCW(forward, -90f) : RotateCW(forward, 90f);
        var dest = boss.Position + dir * 5f;

        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement,
            sa.WaypointDp(dest, 6000, 0, "大地摇动指路", sa.Data.DefaultSafeColor));
        Announce(sa, "就位后使用超硬化", 5000);
        Dbg(sa, $"大地摇动：rank{rank} → boss面向{(rank == 0 ? "左" : "右")} {dest:F1}");
    }

    // 万亿核爆(ActionEffect 3009)：开启 Boss 血量监控，<15% 时 TTS+横幅提示"扎针"（只报一次，随 Init 重置）
    [ScriptMethod(
        name: "T13 - Boss残血扎针提醒",
        eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:3009"])]
    public void T13万亿核爆(Event evt, ScriptAccessory sa)
    {
        if (!InMap(T13Territory)) return;
        StartBossHpWatch(sa, evt.SourceId());
        Dbg(sa, $"3009 万亿核爆：开启 Boss 血量监控 {evt.SourceId():X}");
    }
    #endregion
    
    #region A4S
    [ScriptMethod(
        name: "-------A4S-------",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(xx|xx)$"])]
    public void a4s_start(Event evt, ScriptAccessory sa){}

    // TargetIcon Id:001F 记录圆盘点名 (TargetId)
    [ScriptMethod(
        name: "A4S - 记录圆盘点名",
        eventType: EventTypeEnum.TargetIcon,
        eventCondition: ["Id:001F"],
        userControl: false)]
    public void A4S圆盘点名(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A4STerritory)) return;
        _a4sDiscMarked = evt.TargetId();
        Dbg(sa, $"圆盘点名：{_a4sDiscMarked:X}");
    }

    // AddCombatant 3895：非圆盘点名者，100ms 后在球本体(SourceId)上画 3m 安全色圆 30s + TTS 撞球
    [ScriptMethod(
        name: "A4S - 撞球绿圈",
        eventType: EventTypeEnum.AddCombatant,
        eventCondition: ["DataId:3895"])]
    public void A4S撞球生成(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A4STerritory)) return;
        if (_a4sDiscMarked == sa.Data.Me)
        {
            Dbg(sa, "撞球3895：自己是圆盘点名，跳过");
            return;
        }

        uint ballId = evt.SourceId();
        DelayAction(sa, 100, () =>
        {
            var dp = sa.Data.GetDefaultDrawProperties();
            dp.Name = $"A4S撞球圈-3895-{ballId:X}";
            dp.Color = sa.Data.DefaultSafeColor;
            dp.Owner = ballId;              // 跟随球本体
            dp.Scale = new Vector2(3f);     // 半径 3m
            dp.ScaleMode = ScaleMode.None;
            dp.DestoryAt = 30000;
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
            if (启用TTS) sa.Method.TTS("撞球");
            Dbg(sa, $"撞球3895：画绿圈 {ballId:X}");
        });
    }

    // AddCombatant 3896：非圆盘点名者，100ms 后在球本体(SourceId)上画 4m 黄色圆 30s + textinfo 分摊撞球
    [ScriptMethod(
        name: "A4S - 分摊撞球黄圈",
        eventType: EventTypeEnum.AddCombatant,
        eventCondition: ["DataId:3896"])]
    public void A4S分摊撞球生成(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A4STerritory)) return;
        if (_a4sDiscMarked == sa.Data.Me)
        {
            Dbg(sa, "撞球3896：自己是圆盘点名，跳过");
            return;
        }

        uint ballId = evt.SourceId();
        DelayAction(sa, 100, () =>
        {
            var dp = sa.Data.GetDefaultDrawProperties();
            dp.Name = $"A4S撞球圈-3896-{ballId:X}";
            dp.Color = A4SYellow;
            dp.Owner = ballId;              // 跟随球本体
            dp.Scale = new Vector2(4f);     // 半径 4m
            dp.ScaleMode = ScaleMode.None;
            dp.DestoryAt = 30000;
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
            if (启用横幅) sa.Method.TextInfo("分摊撞球", 3000, true);
            Dbg(sa, $"撞球3896：画黄圈 {ballId:X}");
        });
    }

    // ActionEffect 3944 爆炸：按 SourceId 清除对应球的圈
    [ScriptMethod(
        name: "A4S - 撞球爆炸消圈",
        eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:3944"],
        userControl: false)]
    public void A4S撞球爆炸(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A4STerritory)) return;
        uint ballId = evt.SourceId();
        sa.Method.RemoveDraw($"A4S撞球圈-.*-{ballId:X}");
        Dbg(sa, $"撞球3944：清除圈 {ballId:X}");
    }

    // StartCasting 3932：大 AOE 提示 3s
    [ScriptMethod(
        name: "A4S - 大AOE提示",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:3932"])]
    public void A4S大AOE(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A4STerritory)) return;
        Announce(sa, "大aoe", 3000);
    }

    // StatusRemove 39：提示打断读条 3s，并从自己到 TargetId 连线(channeling 5) 3s
    [ScriptMethod(
        name: "A4S - 打断读条提示",
        eventType: EventTypeEnum.StatusRemove,
        eventCondition: ["StatusID:39"])]
    public void A4S打断读条(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A4STerritory)) return;
        uint tid = evt.TargetId();
        Announce(sa, "打断读条", 3000);
        连线(sa, 5, tid, 3000, "打断读条");
        Dbg(sa, $"StatusRemove 39：打断读条，连线 {tid:X}");
    }

    // Tether 0011：记录灭绝点名（会点五次，同一人），每次在被点名者身上画 4m 危险圈跟人 10s
    [ScriptMethod(
        name: "A4S - 灭绝点名危险圈",
        eventType: EventTypeEnum.Tether,
        eventCondition: ["Id:0011"])]
    public void A4S灭绝点名(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A4STerritory)) return;
        uint marked = evt.TargetId();
        if (!_a4sExtinctionMarks.Contains(marked)) _a4sExtinctionMarks.Add(marked);

        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = $"A4S灭绝圈-{++_a4sExtinctionDrawSeq}";
        dp.Color = sa.Data.DefaultDangerColor;
        dp.Owner = marked;              // 跟随被点名者
        dp.Scale = new Vector2(4f);     // 半径 4m
        dp.ScaleMode = ScaleMode.None;
        dp.DestoryAt = 10000;
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
        Dbg(sa, $"灭绝点名 Tether 0011：{marked:X}");
    }

    // StartCasting 3939：100ms 后若自己被灭绝点名，预约 3s 后超硬化
    [ScriptMethod(
        name: "A4S - 灭绝超硬化",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:3939"])]
    public void A4S灭绝超硬化(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A4STerritory)) return;
        DelayAction(sa, 100, () =>
        {
            if (!_a4sExtinctionMarks.Contains(sa.Data.Me) && !所有职能都会尝试放超硬化)
            {
                Dbg(sa, "灭绝3939：自己未被点名，跳过超硬化");
                return;
            }
            Dbg(sa, "灭绝3939：预约超硬化，3s 开按");
            ScheduleAutoCast(sa, HardenActionId, HardenActionType, sa.Data.Me, "灭绝超硬化", 3000);
        });
    }

    // ActionEffect 3939：清空灭绝点名 list，清除所有 0011 危险圈
    [ScriptMethod(
        name: "A4S - 灭绝结算清理",
        eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:3939"],
        userControl: false)]
    public void A4S灭绝结算(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A4STerritory)) return;
        _a4sExtinctionMarks.Clear();
        sa.Method.RemoveDraw("A4S灭绝圈-.*");
        Dbg(sa, "灭绝3939结算：清空点名与危险圈");
    }

    // AddCombatant 3899：按出生点匹配负责的队列 index；轮到自己则指路+播报，并开启该小怪血量检测(<25% 喂进大怪)
    [ScriptMethod(
        name: "A4S - 拉小怪",
        eventType: EventTypeEnum.AddCombatant,
        eventCondition: ["DataId:3899"])]
    public void A4S小怪生成(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A4STerritory)) return;
        var pos = evt.SourcePosition();
        var spawn = A4SAddSpawns.FirstOrDefault(s =>
            new Vector2(s.Pos.X - pos.X, s.Pos.Z - pos.Z).Length() < 3f);
        if (spawn.Index == 0)
        {
            Dbg(sa, $"小怪3899：出生点 {pos:F1} 未匹配任何预设点");
            return;
        }

        int myIdx = sa.Data.PartyList.IndexOf(sa.Data.Me);
        if (myIdx != spawn.Index)
        {
            Dbg(sa, $"小怪3899：出生点归 index{spawn.Index}，自己是 index{myIdx}，跳过");
            return;
        }

        uint addId = evt.SourceId();
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement,
            sa.WaypointDp(spawn.Pos, 8000, 0, "A4S拉小怪指路", sa.Data.DefaultSafeColor));
        Announce(sa, "拉小怪，修血25以下", 4000);
        StartA4SAddHpWatch(sa, addId);
        Dbg(sa, $"小怪3899：index{spawn.Index} 指路 {spawn.Pos:F1}，监控 {addId:X}");
    }

    // 小怪血量检测：<25% 时播报喂进大怪 + 指路(自己→DataId 3898 的大怪，双端跟踪)，然后关闭检测
    private void StartA4SAddHpWatch(ScriptAccessory sa, uint addId)
    {
        StopA4SAddHpWatch(sa);
        if (addId == 0) return;
        _a4sAddWatchId = addId;

        _a4sAddHpGuid = sa.Method.RegistFrameworkUpdateAction(() =>
        {
            if (_a4sAddHpGuid == null) return;
            if (sa.Data.Objects.SearchById(addId) is not IBattleChara add || add.MaxHp == 0) return;
            if (add.CurrentHp / (double)add.MaxHp >= 0.25) return;

            Announce(sa, "喂进大怪", 4000);
            if (sa.Data.Objects.FirstOrDefault(o => o.DataId == 3898) is IBattleChara big)
            {
                var dp = sa.Data.GetDefaultDrawProperties();
                dp.Name = "A4S喂怪指路";
                dp.Color = sa.Data.DefaultSafeColor;
                dp.Owner = sa.Data.Me;
                dp.TargetObject = big.EntityId;   // 终点跟随大怪
                dp.Scale = new Vector2(2);
                dp.ScaleMode = ScaleMode.YByDistance;
                dp.DestoryAt = 6000;
                sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }
            else
            {
                Dbg(sa, "小怪血量检测：未找到大怪(3898)，只播报不指路");
            }
            Dbg(sa, $"小怪 {addId:X} 血量 <25%：喂进大怪，关闭检测");
            StopA4SAddHpWatch(sa);
        }, true, false);
    }

    private void StopA4SAddHpWatch(ScriptAccessory sa)
    {
        _a4sAddWatchId = 0;
        if (_a4sAddHpGuid == null) return;
        sa.Method.UnregistFrameworkUpdateAction(_a4sAddHpGuid);
        _a4sAddHpGuid = null;
    }

    // Tether 0029：若血量检测中且 SourceId 是被监控的小怪，关闭检测
    [ScriptMethod(
        name: "A4S - 小怪连线停止检测",
        eventType: EventTypeEnum.Tether,
        eventCondition: ["Id:0029"],
        userControl: false)]
    public void A4S小怪连线(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A4STerritory)) return;
        if (_a4sAddHpGuid == null) return;
        if (evt.SourceId() != _a4sAddWatchId) return;
        Dbg(sa, $"Tether 0029：小怪 {_a4sAddWatchId:X} 已连线，关闭血量检测");
        StopA4SAddHpWatch(sa);
    }

    // TargetIcon 0025：TargetId 上画 2m 蓝圈 30s
    [ScriptMethod(
        name: "A4S - 蓝圈点名",
        eventType: EventTypeEnum.TargetIcon,
        eventCondition: ["Id:0025"])]
    public void A4S蓝圈(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A4STerritory)) return;
        A4S画头标圈(sa, evt.TargetId(), A4SBlue, "蓝");
    }

    // TargetIcon 0024：TargetId 上画 2m 红圈 30s
    [ScriptMethod(
        name: "A4S - 红圈点名",
        eventType: EventTypeEnum.TargetIcon,
        eventCondition: ["Id:0024"])]
    public void A4S红圈(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A4STerritory)) return;
        A4S画头标圈(sa, evt.TargetId(), sa.Data.DefaultDangerColor, "红");
    }

    private void A4S画头标圈(ScriptAccessory sa, uint targetId, Vector4 color, string tag)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = $"A4S头标{tag}圈-{targetId:X}";
        dp.Color = color;
        dp.Owner = targetId;            // 跟随被点名者
        dp.Scale = new Vector2(2f);     // 半径 2m
        dp.ScaleMode = ScaleMode.None;
        dp.DestoryAt = 30000;
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
        Dbg(sa, $"头标{tag}圈：{targetId:X}");
    }

    // StartCasting 3934：每个队员脚下画 4m 危险圈，5.5s 消除
    [ScriptMethod(
        name: "A4S - 全员脚下圈",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:3934"])]
    public void A4S全员脚下圈(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A4STerritory)) return;
        foreach (var member in sa.Data.PartyList)
        {
            var dp = sa.Data.GetDefaultDrawProperties();
            dp.Name = $"A4S脚下圈-{member:X}";
            dp.Color = sa.Data.DefaultDangerColor;
            dp.Owner = member;              // 跟随队员
            dp.Scale = new Vector2(4f);     // 半径 4m
            dp.ScaleMode = ScaleMode.None;
            dp.DestoryAt = 5500;
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
        }
        Dbg(sa, "3934：全员脚下 4m 危险圈 5.5s");
    }

    // RemoveCombatant 4104：开启 3892 扎针血量监控（与拉小怪检测相互独立），已开启则不重复；<18% 播报扎针一次后关闭
    [ScriptMethod(
        name: "A4S - 扎针血量监控",
        eventType: EventTypeEnum.RemoveCombatant,
        eventCondition: ["DataId:4104"])]
    public void A4S扎针监控(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A4STerritory)) return;
        if (_a4sSewHpGuid != null)
        {
            Dbg(sa, "扎针监控已开启，跳过");
            return;
        }

        Dbg(sa, "RemoveCombatant 4104：开启 3892 扎针血量监控");
        _a4sSewHpGuid = sa.Method.RegistFrameworkUpdateAction(() =>
        {
            if (_a4sSewHpGuid == null) return;
            if (sa.Data.Objects.FirstOrDefault(o => o.DataId == 3892) is not IBattleChara boss || boss.MaxHp == 0) return;

            double ratio = boss.CurrentHp / (double)boss.MaxHp;
            if (ratio > 0.18) return;

            Announce(sa, "扎针", 5000);
            Dbg(sa, $"3892 血量 {ratio:P1} <18%：播报扎针，关闭监控");
            StopA4SSewHpWatch(sa);
        }, true, false);
    }

    private void StopA4SSewHpWatch(ScriptAccessory sa)
    {
        if (_a4sSewHpGuid == null) return;
        sa.Method.UnregistFrameworkUpdateAction(_a4sSewHpGuid);
        _a4sSewHpGuid = null;
    }
    #endregion

    #region A8S
    [ScriptMethod(
        name: "-------A8S-------",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(xx|xx)$"])]
    public void a8s_start(Event evt, ScriptAccessory sa){}

    // ---------------- 指路辅助（含单人测试模式） ----------------

    private static Vector4 A8SIndexColor(int idx) => idx switch
    {
        0 => A8STestBlue,
        1 or 2 => A8STestGreen,
        _ => A8STestRed,
    };

    // 画 assignedIdx 那份指路：正常模式只有自己是该 index 才画；
    // 单人测试模式无视自己 index，按 index 配色画出
    private void A8SWaypoint(ScriptAccessory sa, int assignedIdx, Vector3 dest, uint duration, string name)
    {
        if (A8S单人测试指路)
        {
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement,
                sa.WaypointDp(dest, duration, 0, $"{name}-测试i{assignedIdx}", A8SIndexColor(assignedIdx)));
            return;
        }
        if (sa.Data.PartyList.IndexOf(sa.Data.Me) != assignedIdx) return;
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement,
            sa.WaypointDp(dest, duration, 0, name, sa.Data.DefaultSafeColor));
    }

    // 按 index 逐一指路：destForIndex 返回 null 表示该 index 无指路；
    // 正常模式只画自己那份，单人测试模式画出所有 index（目的地与颜色都相同的只画一次）
    private void A8SWaypointAll(ScriptAccessory sa, string name, uint duration, Func<int, Vector3?> destForIndex)
    {
        if (A8S单人测试指路)
        {
            var drawn = new List<(Vector3 Pos, Vector4 Color)>();
            for (int i = 0; i < 8; i++)
            {
                if (destForIndex(i) is not { } d) continue;
                var color = A8SIndexColor(i);
                if (drawn.Any(t => t.Color == color && (t.Pos - d).Length() < 0.1f)) continue;
                drawn.Add((d, color));
                sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement,
                    sa.WaypointDp(d, duration, 0, $"{name}-测试i{i}", color));
            }
            return;
        }
        int myIdx = sa.Data.PartyList.IndexOf(sa.Data.Me);
        if (myIdx < 0 || destForIndex(myIdx) is not { } dest) return;
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement,
            sa.WaypointDp(dest, duration, 0, name, sa.Data.DefaultSafeColor));
    }

    // ---------------- P1 ----------------

    // NPC台词"再次启动战斗系统……"：按队列 index 提示爆发交付时机
    [ScriptMethod(
        name: "A8S - 开场爆发提示",
        eventType: EventTypeEnum.Chat,
        eventCondition: ["Type:NPCDialogueAnnouncements", "Message:regex:再次启动战斗系统……"])]
    public void A8S开场爆发提示(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A8STerritory)) return;
        int myIdx = sa.Data.PartyList.IndexOf(sa.Data.Me);
        if (myIdx is 3 or 4 or 5) Announce(sa, "交60秒及以下爆发", 2000);
        else if (myIdx is 6 or 7) Announce(sa, "交90秒及以下爆发", 2000);
        Dbg(sa, $"再次启动战斗系统：index{myIdx} 爆发提示");
    }

    // PlayActionTimeline 3204：集合引导
    [ScriptMethod(
        name: "A8S - 集合引导",
        eventType: EventTypeEnum.PlayActionTimeline,
        eventCondition: ["Id:3204"])]
    public void A8S集合引导(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A8STerritory)) return;
        Announce(sa, "集合引导", 2000);
    }

    // StartCasting 5678(P1/P4)/5732(P3) 巨型光束炮：以 boss 位置沿其朝向画矩形，宽6长70，持续读条时长(约2700ms)
    [ScriptMethod(
        name: "A8S - 巨型光束炮绘图",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(5678|5732)$"])]
    public void A8S巨型光束炮(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A8STerritory)) return;
        if (!int.TryParse(evt["DurationMilliseconds"], out var dur)) dur = 2700;
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = "A8S巨型光束炮";
        dp.Color = sa.Data.DefaultDangerColor;
        dp.Position = evt.SourcePosition();
        dp.Rotation = evt.SourceRotation();
        dp.Scale = new Vector2(6f, 70f);
        dp.FixRotation = true;
        dp.ScaleMode = ScaleMode.None;
        dp.DestoryAt = dur;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        Dbg(sa, $"巨型光束炮({evt.ActionId()})：矩形 6x70 持续 {dur}ms");
    }

    // StartCasting 5682 执行准备预告：仅 TTS
    [ScriptMethod(
        name: "A8S - 执行准备预告",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:5682"])]
    public void A8S执行准备预告(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A8STerritory)) return;
        if (启用TTS) sa.Method.TTS("准备进对应的格子");
    }

    // ObjectChanged Add：按出生点匹配格子归属 index（±12 四角），轮到自己则指路进格子
    [ScriptMethod(
        name: "A8S - 进格子打小怪",
        eventType: EventTypeEnum.ObjectChanged,
        eventCondition: ["Operate:Add"])]
    public void A8S进格子(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A8STerritory)) return;
        var pos = evt.SourcePosition();
        var spawn = A8SCellSpawns.FirstOrDefault(s =>
            new Vector2(s.Pos.X - pos.X, s.Pos.Z - pos.Z).Length() < 1f);
        if (spawn.Index == 0) return;

        int myIdx = sa.Data.PartyList.IndexOf(sa.Data.Me);
        if (myIdx != spawn.Index && !A8S单人测试指路)
        {
            Dbg(sa, $"格子：{pos:F1} 归 index{spawn.Index}，自己是 index{myIdx}，跳过");
            return;
        }

        A8SWaypoint(sa, spawn.Index, spawn.Pos, 5000, "A8S进格子指路");
        if (myIdx == spawn.Index) Announce(sa, "进格子打小怪", 4000);
        Dbg(sa, $"格子：index{spawn.Index} 指路 {spawn.Pos:F1}");
    }

    // StartCasting 5675 永恒射线：T 3s 后自动超硬化
    [ScriptMethod(
        name: "A8S - 永恒射线T超硬化",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:5675"])]
    public void A8S永恒射线(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A8STerritory)) return;
        if (!所有职能都会尝试放超硬化 && _roleStatus != TankStatus) return;
        Dbg(sa, "永恒射线5675：T 预约超硬化，3s 开按");
        ScheduleAutoCast(sa, HardenActionId, HardenActionType, sa.Data.Me, "永恒射线超硬化", 3000);
    }

    // PlayActionTimeline 7737：进入 P2。index2 去场中开盾姿，其他人去北边，指路 3s
    [ScriptMethod(
        name: "A8S - 转P2指路",
        eventType: EventTypeEnum.PlayActionTimeline,
        eventCondition: ["Id:7737"])]
    public void A8S转P2(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A8STerritory)) return;
        if (_phase != 1) return;
        _phase = 2;
        int myIdx = sa.Data.PartyList.IndexOf(sa.Data.Me);
        A8SWaypointAll(sa, "A8S转P2指路", 3000, i =>
            i == 2 ? new Vector3(-2.86f, 10.5f, 0.26f) : new Vector3(0.18f, 10.5f, -21.70f));
        if (myIdx == 2) Announce(sa, "开盾姿拉住中间小怪仇恨", 3000);
        Dbg(sa, $"7737：_phase=2，index{myIdx} 指路");
    }

    // ---------------- P2 ----------------

    // ActionEffect 5692：提示看场中 boss 手
    [ScriptMethod(
        name: "A8S - 看boss手提示",
        eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:5692"])]
    public void A8S看boss手(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A8STerritory)) return;
        if (_phase != 2) return;
        Announce(sa, "注意看场中boss手", 3000);
    }

    // PlayActionTimeline 3208：按 index 指路 5s；index2 不动；index1/5/6/7 另外 20s 后超硬化
    [ScriptMethod(
        name: "A8S - P2就位指路",
        eventType: EventTypeEnum.PlayActionTimeline,
        eventCondition: ["Id:3208"])]
    public void A8S_P2就位(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A8STerritory)) return;
        int myIdx = sa.Data.PartyList.IndexOf(sa.Data.Me);
        A8SWaypointAll(sa, "A8S-P2就位指路", 5000, i =>
            i == 2 ? null : i == 0 ? new Vector3(-0.13f, 10.5f, 20.55f) : new Vector3(11.94f, 10.5f, -0.46f));

        Dbg(sa, $"3208：index{myIdx} 预约超硬化，20s 开按");
        ScheduleAutoCast(sa, HardenActionId, HardenActionType, sa.Data.Me, "就位后超硬化", 20000);
        Dbg(sa, $"3208：index{myIdx} 指路");
    }

    // AddCombatant 5424：同一时间出现两只，第1只归 index3、第2只归 index4；
    // 指路到该怪 XZ 符号对应的 ±10 位置 5s，对应 index 10s 后超硬化
    [ScriptMethod(
        name: "A8S - 5424小怪指路",
        eventType: EventTypeEnum.AddCombatant,
        eventCondition: ["DataId:5424"])]
    public void A8S小怪5424(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A8STerritory)) return;

        // 距上次出现超过 10s 即视为新一轮，从 index3 重新分配
        var now = DateTime.Now;
        if ((now - _a8s5424Last).TotalMilliseconds > 10000) _a8s5424Count = 0;
        _a8s5424Last = now;
        int assigned = _a8s5424Count == 0 ? 3 : 4;
        _a8s5424Count++;

        var pos = evt.SourcePosition();
        int myIdx = sa.Data.PartyList.IndexOf(sa.Data.Me);
        var dest = new Vector3(pos.X >= 0 ? 10f : -10f, 10.5f, pos.Z >= 0 ? 10f : -10f);
        A8SWaypoint(sa, assigned, dest, 5000, "A8S-5424指路");
        if (myIdx != assigned)
        {
            Dbg(sa, $"5424：{pos:F1} 归 index{assigned}，自己是 index{myIdx}，跳过");
            return;
        }

        // Dbg(sa, $"5424：index{assigned} 指路 {dest:F1}，预约超硬化 10s 开按");
        // ScheduleAutoCast(sa, HardenActionId, HardenActionType, sa.Data.Me, "就位后超硬化", 10000);
    }

    // StartCasting 5719 超级气旋：提示击退
    [ScriptMethod(
        name: "A8S - 超级气旋击退提示",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:5719"])]
    public void A8S超级气旋(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A8STerritory)) return;
        Announce(sa, "注意击退", 3000);
    }

    // StatusAdd 1024 雷属性压缩：被点者画黄色 8m 圈（延迟 10s 出现，持续 10s）；
    // 10s 后检查是否仍有人带 1024：有 → 提示传雷；无 → 清除黄圈
    [ScriptMethod(
        name: "A8S - 雷属性压缩",
        eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:1024"])]
    public void A8S雷属性压缩(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A8STerritory)) return;

        // 距上次点名超过 10s 即视为新一轮
        var now = DateTime.Now;
        if ((now - _a8sThunderLast).TotalMilliseconds > 10000)
        {
            _a8sThunderMarks.Clear();
            _a8sThunderScheduled = false;
        }
        _a8sThunderLast = now;

        uint marked = evt.TargetId();
        if (!_a8sThunderMarks.Contains(marked)) _a8sThunderMarks.Add(marked);

        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = $"A8S雷压缩圈-{marked:X}";
        dp.Color = A8SYellow;
        dp.Owner = marked;              // 跟随被点名者
        dp.Scale = new Vector2(8f);     // 半径 8m
        dp.ScaleMode = ScaleMode.None;
        dp.Delay = 10000;               // 10s 后才显示
        dp.DestoryAt = 11000;           // 显示后持续 10s
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
        Dbg(sa, $"雷属性压缩：{marked:X} 画黄圈（延迟10s，持续10s）");

        if (_a8sThunderScheduled) return;
        _a8sThunderScheduled = true;
        DelayAction(sa, 10000, () =>
        {
            bool anyone = sa.Data.PartyList.Any(id =>
                sa.Data.Objects.SearchByEntityId(id) is IBattleChara c && c.HasStatus(1024));
            if (anyone)
            {
                Announce(sa, "准备传雷", 4000);
                Dbg(sa, "雷属性压缩 10s 检查：仍有人带雷，提示传雷");
            }
            else
            {
                sa.Method.RemoveDraw("A8S雷压缩圈-.*");
                Dbg(sa, "雷属性压缩 10s 检查：无人带雷，清除黄圈");
            }
        });
    }

    // StartCasting 5714/5715 雾散爆发：进入 P3
    [ScriptMethod(
        name: "A8S - 雾散爆发转P3",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(5714|5715)$"],
        userControl: false)]
    public void A8S雾散爆发(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A8STerritory)) return;
        _phase = 3;
        Dbg(sa, "雾散爆发：_phase=3");
    }

    // ---------------- P3 ----------------

    // PlayActionTimeline 143 + SourceDataId 5417：index0 与其余分开指路 5s，9s 后全员使用 7559(沉稳)
    [ScriptMethod(
        name: "A8S - P3就位与沉稳",
        eventType: EventTypeEnum.PlayActionTimeline,
        eventCondition: ["Id:143", "SourceDataId:5417"])]
    public void A8S_P3就位(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A8STerritory)) return;
        if (_phase != 3) return;
        int myIdx = sa.Data.PartyList.IndexOf(sa.Data.Me);
        A8SWaypointAll(sa, "A8S-P3就位指路", 5000, i =>
            i == 0 ? new Vector3(0.08f, 10.5f, -3.36f) : new Vector3(7.25f, 10.5f, 0.06f));
        Dbg(sa, $"143/5417：index{myIdx} 指路，预约沉稳 9s 开按");
        ScheduleAutoCast(sa, 7559, HardenActionType, sa.Data.Me, "使用沉稳", 9000);
    }

    // Targetable 5425：T 5s 后超硬化
    [ScriptMethod(
        name: "A8S - 5425现身T超硬化",
        eventType: EventTypeEnum.Targetable,
        eventCondition: ["DataId:5425", "Targetable:True"])]
    public void A8S_5425现身(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A8STerritory)) return;
        if (_phase != 3) return;
        if (!所有职能都会尝试放超硬化 && _roleStatus != TankStatus) return;
        Dbg(sa, "5425 现身：T 预约超硬化，5s 开按");
        ScheduleAutoCast(sa, HardenActionId, HardenActionType, sa.Data.Me, "超硬化", 5000);
    }

    // StartCasting 5731：T 1s 后超硬化
    [ScriptMethod(
        name: "A8S - 分摊死刑T超硬化",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:5731"])]
    public void A8S_5731(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A8STerritory)) return;
        if (!所有职能都会尝试放超硬化 && _roleStatus != TankStatus) return;
        Dbg(sa, "5731：T 预约超硬化，1s 开按");
        ScheduleAutoCast(sa, HardenActionId, HardenActionType, sa.Data.Me, "超硬化", 1000);
    }

    // VfxEvent Id 30：点到自己 → 提示远离人群别去分摊
    [ScriptMethod(
        name: "A8S - 远离分摊提示",
        eventType: EventTypeEnum.VfxEvent,
        eventCondition: ["Id:30"])]
    public void A8S远离分摊(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A8STerritory)) return;
        if (evt.TargetId() != sa.Data.Me) return;
        Announce(sa, "远离人群别去分摊", 3000);
        Dbg(sa, "VfxEvent 30：自己被点，提示远离人群");
    }

    // StartCasting 5733 超级跳：实时最远玩家身上画 5.4m 危险圈（持续读条时长）；T 提示远离引导
    [ScriptMethod(
        name: "A8S - 超级跳",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:5733"])]
    public void A8S超级跳(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A8STerritory)) return;
        if (!int.TryParse(evt["DurationMilliseconds"], out var dur)) dur = 3000;

        // PlayerFarestOrder：圈心实时解析为离 boss 最远的玩家
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = "A8S超级跳";
        dp.Color = sa.Data.DefaultDangerColor;
        dp.Position = new Vector3(0);
        dp.CentreResolvePattern = PositionResolvePatternEnum.PlayerFarestOrder;
        dp.CentreOrderIndex = 1;
        dp.Scale = new Vector2(5.4f);   // 半径 5.4m
        dp.ScaleMode = ScaleMode.None;
        dp.DestoryAt = dur;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        Dbg(sa, $"超级跳5733：最远玩家 5.4m 危险圈 {dur}ms");

        if (所有职能都会尝试放超硬化 || _roleStatus == TankStatus)
            Announce(sa, "远离引导超级跳", 3000);
    }

    // StartCasting 5734 末世宣言：跟随 boss 的 90° 扇形危险区，半径 25，持续 5s
    [ScriptMethod(
        name: "A8S - 末世宣言",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:5734"])]
    public void A8S末世宣言(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A8STerritory)) return;
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = "A8S末世宣言";
        dp.Color = sa.Data.DefaultDangerColor;
        dp.Owner = evt.SourceId();      // 跟随施法者（含朝向）
        dp.Scale = new Vector2(25f);    // 半径 25
        dp.Radian = MathF.PI / 2f;      // 90°
        dp.ScaleMode = ScaleMode.None;
        dp.DestoryAt = 8000;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        Dbg(sa, $"末世宣言5734：{evt.SourceId():X} 扇形 90°x25 5s");
    }

    // PlayActionTimeline 4574：进入 P4，所有人指路 5s 去场东
    [ScriptMethod(
        name: "A8S - 转P4指路",
        eventType: EventTypeEnum.PlayActionTimeline,
        eventCondition: ["Id:4574"])]
    public void A8S转P4(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A8STerritory)) return;
        _phase = 4;
        _a8sP4BeamHardened = false;
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement,
            sa.WaypointDp(new Vector3(23.10f, 10.5f, 0.07f), 3000, 0, "A8S转P4指路", sa.Data.DefaultSafeColor));
        Dbg(sa, "4574：_phase=4，指路 {23.10, 10.50, 0.07}");
    }

    // ---------------- P4 ----------------

    // ActionEffect 5741 且目标是自己：index7 去场边引导远钻，index0 去场东，指路 5s
    [ScriptMethod(
        name: "A8S - 远钻指路",
        eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:5741"])]
    public void A8S远钻(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A8STerritory)) return;
        if (evt.TargetId() != sa.Data.Me) return;

        int myIdx = sa.Data.PartyList.IndexOf(sa.Data.Me);
        A8SWaypointAll(sa, "A8S远钻指路", 5000, i =>
            i == 7 ? (Vector3?)new Vector3(21.95f, 10.5f, 21.95f) : i == 0 ? new Vector3(0f, 10.5f, -12f) : null);
        if (myIdx == 7) Announce(sa, "去场边引导远钻", 2000);
        if (myIdx == 0) Announce(sa, "去A引导近钻", 2000);
        Dbg(sa, $"5741：index{myIdx} 远钻指路");
    }

    // P4 的 StartCasting 5678：所有人 3s 后超硬化（画图由「巨型光束炮绘图」统一处理）
    [ScriptMethod(
        name: "A8S - P4光束炮全员超硬化",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:5678"])]
    public void A8S_P4光束炮超硬化(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A8STerritory)) return;
        if (_phase != 4) return;
        if (_a8sP4BeamHardened) return;
        _a8sP4BeamHardened = true;
        Dbg(sa, "P4 5678：全员预约超硬化，3s 开按");
        ScheduleAutoCast(sa, HardenActionId, HardenActionType, sa.Data.Me, "光束炮超硬化", 3000);
    }

    // StartCasting 5718 究极闪光：从施法者朝场中 {0,10.5,0} 方向前进 17m 处指路 5s
    [ScriptMethod(
        name: "A8S - 究极闪光指路",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:5718"])]
    public void A8S究极闪光(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A8STerritory)) return;
        var pos = evt.SourcePosition();
        var dir = new Vector3(-pos.X, 0, -pos.Z);   // 指向场中 {0,*,0}
        if (dir.Length() < 0.5f)
        {
            Dbg(sa, "究极闪光5718：施法者几乎在场中，无法确定方向");
            return;
        }
        var dest = pos + Vector3.Normalize(dir) * 17f;
        dest.Y = 10.5f;
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement,
            sa.WaypointDp(dest, 5000, 0, "A8S究极闪光指路", sa.Data.DefaultSafeColor));
        Dbg(sa, $"究极闪光5718：{pos:F1} → {dest:F1}");
    }

    // TargetIcon 0042：点到自己 → 2s 后超硬化
    [ScriptMethod(
        name: "A8S - 生命计数法超硬化",
        eventType: EventTypeEnum.TargetIcon,
        eventCondition: ["Id:0042|0040"])]
    public void A8S_0042点名(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A8STerritory)) return;
        if (evt.TargetId() != sa.Data.Me) return;
        Dbg(sa, "0042 0040点名：预约超硬化，2s 开按");
        ScheduleAutoCast(sa, HardenActionId, HardenActionType, sa.Data.Me, "生命计数法超硬化", 2000);
    }

    // StartCasting 5742 正义合神：进入 P5，提示满血后爆发
    [ScriptMethod(
        name: "A8S - 正义合神转P5",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:5742"])]
    public void A8S正义合神(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A8STerritory)) return;
        _phase = 5;
        Announce(sa, "满血后爆发", 4000);
        Dbg(sa, "正义合神5742：_phase=5");
    }

    // StartCasting 5743 终审开庭：index1 去西、index2 去东，指路 5s
    [ScriptMethod(
        name: "A8S - 终审开庭指路",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:5743"])]
    public void A8S终审开庭(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A8STerritory)) return;
        int myIdx = sa.Data.PartyList.IndexOf(sa.Data.Me);
        A8SWaypointAll(sa, "A8S终审开庭指路", 5000, i =>
            i == 1 ? (Vector3?)new Vector3(-18.08f, 10.5f, -0.24f) : i == 2 ? new Vector3(18.28f, 10.5f, 0.13f) : null);
        Dbg(sa, $"终审开庭5743：index{myIdx}");
    }

    // StartCasting 5744 终审闭庭：横幅提示按终极针，随后 TTS 倒数 3、2、1、扎扎扎
    [ScriptMethod(
        name: "A8S - 终审闭庭倒数",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:5744"])]
    public void A8S终审闭庭(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A8STerritory)) return;
        if (启用横幅) sa.Method.TextInfo("听到扎时按下终极针", 5000, true);
        if (启用TTS)
        {
            DelayAction(sa, 1000, () => sa.Method.TTS("3"));
            DelayAction(sa, 2000, () => sa.Method.TTS("2"));
            DelayAction(sa, 3000, () => sa.Method.TTS("1"));
            DelayAction(sa, 4000, () => sa.Method.TTS("扎扎扎"));
        }
        Dbg(sa, "终审闭庭5744：倒数提示");
    }
    #endregion

    #region A12S
    [ScriptMethod(
        name: "-------A12S------",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(xx|xx)$"])]
    public void a12s_start(Event evt, ScriptAccessory sa){}

    // ---------------- 通用（全程生效） ----------------

    // StartCasting 6633 惩罚射线：被点名者身上画 5m 危险圈 4s；自己是 T 且被点名 → 1s 后超硬化
    [ScriptMethod(
        name: "A12S - 惩罚射线",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:6633"])]
    public void A12S惩罚射线(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A12STerritory)) return;
        uint target = evt.TargetId();

        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = $"A12S惩罚射线-{target:X}";
        dp.Color = sa.Data.DefaultDangerColor;
        dp.Owner = target;              // 跟随被点名者
        dp.Scale = new Vector2(5f);     // 半径 5m
        dp.ScaleMode = ScaleMode.None;
        dp.DestoryAt = 4000;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        if (target == sa.Data.Me && (_roleStatus == TankStatus || 所有职能都会尝试放超硬化))
        {
            Dbg(sa, "惩罚射线6633：T 被点名，预约超硬化 1s 开按");
            ScheduleAutoCast(sa, HardenActionId, HardenActionType, sa.Data.Me, "惩罚射线超硬化", 1000);
        }
    }

    // StartCasting 6642 黑圈：EffectPosition 上画 8m 危险圈 5s
    [ScriptMethod(
        name: "A12S - 黑圈",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:6642"])]
    public void A12S黑圈(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A12STerritory)) return;
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = $"A12S黑圈-{evt.SourceId():X}";
        dp.Color = sa.Data.DefaultDangerColor;
        dp.Position = evt.EffectPosition;
        dp.Scale = new Vector2(8f);     // 半径 8m
        dp.ScaleMode = ScaleMode.None;
        dp.DestoryAt = 5000;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    // StartCasting 6637 拜火圣礼：以 boss 为中心 360° 月环，外 60 内 8，持续 6s
    [ScriptMethod(
        name: "A12S - 拜火圣礼",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:6637"])]
    public void A12S拜火圣礼(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A12STerritory)) return;
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = "A12S拜火圣礼";
        dp.Color = sa.Data.DefaultDangerColor;
        dp.Position = evt.SourcePosition();
        dp.Scale = new Vector2(60f);        // 外半径 60
        dp.InnerScale = new Vector2(8f);    // 内半径 8
        dp.Radian = 2f * MathF.PI;
        dp.ScaleMode = ScaleMode.None;
        dp.DestoryAt = 6000;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
    }

    // StartCasting 6635 十字圣礼：以 boss 面向的前后左右四条 16 宽 × 60 长的矩形，持续 6s
    [ScriptMethod(
        name: "A12S - 十字圣礼",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:6635"])]
    public void A12S十字圣礼(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A12STerritory)) return;
        for (int k = 0; k < 4; k++)
        {
            var dp = sa.Data.GetDefaultDrawProperties();
            dp.Name = $"A12S十字圣礼-{k}";
            dp.Color = sa.Data.DefaultDangerColor;
            dp.Position = evt.SourcePosition();
            dp.Rotation = evt.SourceRotation() + k * MathF.PI / 2f;
            dp.FixRotation = true;
            dp.Scale = new Vector2(16f, 60f);   // 宽 16 × 长 60
            dp.ScaleMode = ScaleMode.None;
            dp.DestoryAt = 6000;
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
    }

    // ---------------- P1 ----------------

    // PlayActionTimeline 4584：_phase=2；T 提示白风稳仇后超硬化（仅播报），非 T 提示雷电咆哮小怪
    [ScriptMethod(
        name: "A12S - 转P2提示",
        eventType: EventTypeEnum.PlayActionTimeline,
        eventCondition: ["Id:4584"])]
    public void A12S转P2(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A12STerritory)) return;
        if (_phase != 1) return;
        _phase = 2;
        Announce(sa, _roleStatus == TankStatus ? "白风稳仇后超硬化" : "雷电咆哮小怪", 3000);
        Dbg(sa, "4584：_phase=2");
    }

    // ---------------- P2 ----------------

    // AddCombatant 6080：提示裸吃第一轮小神圣后秒杀小怪
    [ScriptMethod(
        name: "A12S - 6080小怪提示",
        eventType: EventTypeEnum.AddCombatant,
        eventCondition: ["DataId:6080"])]
    public void A12S小怪6080(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A12STerritory)) return;
        if (_phase != 2) return;
        Announce(sa, "裸吃第一轮小神圣后秒杀小怪", 4000);
    }

    // ActionEffect 6647（P2 出现多次，只处理第一次打到自己）：T→龟壳指引+指路+箭头；N→奶T提示
    [ScriptMethod(
        name: "A12S - 龟壳指引",
        eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:6647"])]
    public void A12S龟壳指引(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A12STerritory)) return;
        if (_phase != 2) return;
        if (evt.TargetId() != sa.Data.Me) return;
        if (_a12s6647Done) return;
        _a12s6647Done = true;

        if (_roleStatus == TankStatus)
        {
            Announce(sa, "准备诡异视线+能力技后龟壳", 5000);
            var spot = new Vector3(0.01f, 400f, 4.30f);
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement,
                sa.WaypointDp(spot, 5000, 0, "A12S龟壳指路", sa.Data.DefaultSafeColor));

            // 场上固定箭头：龟壳位朝北 3m 绿色
            var dp = sa.Data.GetDefaultDrawProperties();
            dp.Name = "A12S龟壳箭头-场地";
            dp.Color = A12SGreen;
            dp.Position = spot;
            dp.TargetPosition = spot + new Vector3(0f, 0f, -3f);   // 北 = -Z
            dp.Scale = new Vector2(2f);
            dp.ScaleMode = ScaleMode.YByDistance;
            dp.DestoryAt = 8000;
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Arrow, dp);

            // 自身箭头：跟随自己、始终指向面前，3m 白色
            var dpSelf = sa.Data.GetDefaultDrawProperties();
            dpSelf.Name = "A12S龟壳箭头-自身";
            dpSelf.Color = A12SWhite;
            dpSelf.Owner = sa.Data.Me;
            dpSelf.Rotation = 0f;                  // 不 FixRotation：随人物朝向
            dpSelf.Scale = new Vector2(2f, 3f);    // 宽 2 × 长 3
            dpSelf.ScaleMode = ScaleMode.None;
            dpSelf.DestoryAt = 8000;
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Arrow, dpSelf);
            Dbg(sa, "6647：T 龟壳指引");
        }
        else if (_roleStatus == HealerStatus)
        {
            Announce(sa, "抬满T血，做盾，不要群奶", 5000);
            Dbg(sa, "6647：N 奶T提示");
        }
    }

    // AddCombatant 6079：T 提示诡异视线；非 T 开月笛复仇连线小怪(channeling 5 红 8s)+复仇血量监控
    [ScriptMethod(
        name: "A12S - 6079复仇小怪",
        eventType: EventTypeEnum.AddCombatant,
        eventCondition: ["DataId:6079"])]
    public void A12S小怪6079(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A12STerritory)) return;
        if (_phase != 2) return;
        uint mob = evt.SourceId();
        DelayAction(sa, 500, () =>
        {
            if (_roleStatus == TankStatus)
            {
                Announce(sa, "诡异视线", 2000);
                return;
            }
            Announce(sa, "下一波小怪开月笛，复仇连线小怪", 3000);
            连线(sa, 5, mob, 8000, "复仇连线");
            StartA12SRevengeHpWatch(sa, "停止复仇，继续音爆");
            Dbg(sa, $"6079：非T 连线 {mob:X}，开启复仇血量监控");
        });
    }

    // ActionEffect 6644：T 立即超硬化
    [ScriptMethod(
        name: "A12S - 6644T超硬化",
        eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:6644"])]
    public void A12S_6644超硬化(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A12STerritory)) return;
        if (_phase != 2) return;
        if (!所有职能都会尝试放超硬化 && _roleStatus != TankStatus) return;
        Dbg(sa, "6644：T 立即超硬化");
        ScheduleAutoCast(sa, HardenActionId, HardenActionType, sa.Data.Me, "超硬化", 0, announce: false);
    }

    // NPC播报"距神圣审判还有 10 秒"：_phase=3，全员 5s 后自动超硬化
    [ScriptMethod(
        name: "A12S - 神圣审判转场",
        eventType: EventTypeEnum.Chat,
        eventCondition: ["Type:NPCDialogueAnnouncements", "Message:regex:距神圣审判还有 10 秒"])]
    public void A12S神圣审判转场(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A12STerritory)) return;
        if (_phase != 2) return;
        _phase = 3;
        Dbg(sa, "神圣审判转场：_phase=3，预约 5s 全员超硬化");
        ScheduleAutoCast(sa, HardenActionId, HardenActionType, sa.Data.Me, "神圣审判超硬化", 5000);
    }

    // ---------------- P3 / P3.1 ----------------

    // Targetable 6076：D 提示月笛一套留飞踢
    [ScriptMethod(
        name: "A12S - P3开头月笛提醒留飞踢",
        eventType: EventTypeEnum.Targetable,
        eventCondition: ["Targetable:True", "DataId:6076"])]
    public void A12S_6076现身(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A12STerritory)) return;
        if (_phase != 3) return;
        if (_roleStatus == DpsStatus) Announce(sa, "月笛一套留飞踢", 3000);
    }

    // StatusAdd 1120紫圈/1122分摊/1123近线/1124远线：记录点名（P3、P3.1 共用，进 3.1 时清空）
    [ScriptMethod(
        name: "A12S - 点名记录",
        eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:regex:^(1120|1122|1123|1124)$"],
        userControl: false)]
    public void A12S点名记录(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A12STerritory)) return;
        if (_phase != 3 && _phase != 3.1) return;
        if (!uint.TryParse(evt["StatusID"], out var sid)) return;
        var list = sid switch { 1120 => _a12sPurple, 1122 => _a12sStack, 1123 => _a12sNear, _ => _a12sFar };
        uint target = evt.TargetId();
        if (!list.Contains(target)) list.Add(target);
        Dbg(sa, $"点名 {sid}：{target:X}");
    }

    // Tether 001C近线/001D远线：记录两人配对
    [ScriptMethod(
        name: "A12S - 连线配对",
        eventType: EventTypeEnum.Tether,
        eventCondition: ["Id:regex:^(001C|001D)$"],
        userControl: false)]
    public void A12S连线配对(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A12STerritory)) return;
        if (_phase != 3 && _phase != 3.1) return;
        bool near = evt["Id"] == "001C";
        A12SAddPair(near ? _a12sNearPairs : _a12sFarPairs, evt.SourceId(), evt.TargetId());
        Dbg(sa, $"{(near ? "近线" : "远线")}配对：{evt.SourceId():X} - {evt.TargetId():X}");
    }

    // StartCasting 6651 时间停止：500ms 后按点名分配 LockOn 标记与站位（P3/P3.1 布局不同）
    [ScriptMethod(
        name: "A12S - 时间停止指路",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:6651"])]
    public void A12S时间停止(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A12STerritory)) return;
        if (_phase != 3 && _phase != 3.1) return;
        bool second = _phase == 3.1;
        DelayAction(sa, 500, () => A12S时间停止排点(sa, second));
    }

    private void A12S时间停止排点(ScriptAccessory sa, bool second)
    {
        int Cmp(uint a, uint b) => sa.Data.PartyList.IndexOf(a).CompareTo(sa.Data.PartyList.IndexOf(b));
        _a12sPurple.Sort(Cmp);
        _a12sStack.Sort(Cmp);
        _a12sNear.Sort(Cmp);
        _a12sFar.Sort(Cmp);

        // 点名标记全员可见：紫圈 30m LockOn45，分摊 4m LockOn62
        foreach (var id in _a12sPurple)
        {
            nint h = sa.Method.VfxMethod.CreateLockOn(45, id, null, 10000);
            if (h != 0) sa.Method.VfxMethod.SetVfxScale(h, new Vector3(30f));
        }
        foreach (var id in _a12sStack)
        {
            nint h = sa.Method.VfxMethod.CreateLockOn(62, id, null, 10000);
            if (h != 0) sa.Method.VfxMethod.SetVfxScale(h, new Vector3(4f));
        }

        uint me = sa.Data.Me;
        Vector3? dest = null;
        string tag = "无点名";

        if (_a12sPurple.Contains(me))
        {
            int r = _a12sPurple.IndexOf(me);
            dest = second
                ? (r == 0 ? new Vector3(26f, 400f, 0f) : new Vector3(-26f, 400f, 0f))
                : r switch { 0 => new Vector3(0f, 400f, -26f), 1 => new Vector3(26f, 400f, 0f), _ => new Vector3(-26f, 400f, 0f) };
            tag = $"紫圈{r}";
        }
        else if (_a12sStack.Contains(me))
        {
            int r = _a12sStack.IndexOf(me);
            dest = r == 0 ? new Vector3(-2.80f, 400f, 25.68f) : new Vector3(2.84f, 400f, 25.58f);
            tag = $"分摊{r}";
        }
        else if (_a12sNear.Contains(me))
        {
            if (_a12sNear.Count >= 4)
            {
                var (group, _) = A12SPairInfo(sa, _a12sNearPairs, _a12sNear, me);
                dest = group == 0 ? new Vector3(-2.80f, 400f, 25.68f) : new Vector3(2.84f, 400f, 25.58f);
                tag = $"近线组{group}";
            }
            else
            {
                dest = new Vector3(-2.80f, 400f, 25.68f);
                tag = "近线";
            }
        }
        else if (_a12sFar.Contains(me))
        {
            if (_a12sFar.Count >= 4)
            {
                // 组内 index 小者去场边，大者去北；一组去西南点、二组去东南点
                var (group, rank) = A12SPairInfo(sa, _a12sFarPairs, _a12sFar, me);
                dest = rank == 1 ? new Vector3(0f, 400f, -26f)
                    : group == 0 ? new Vector3(-2.80f, 400f, 25.68f) : new Vector3(2.80f, 400f, 25.68f);
                tag = $"远线组{group}位{rank}";
            }
            else
            {
                int r = _a12sFar.IndexOf(me);
                dest = r == 0
                    ? (second ? new Vector3(2.80f, 400f, 25.68f) : new Vector3(-2.80f, 400f, 25.68f))
                    : new Vector3(0f, 400f, -26f);
                tag = $"远线{r}";
            }
        }
        else if (!second)
        {
            dest = new Vector3(2.80f, 400f, 25.68f);   // P3 唯一无点名者
        }

        if (dest is { } d)
        {
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement,
                sa.WaypointDp(d, 10000, 0, "A12S时间停止指路", sa.Data.DefaultSafeColor));
        }
        Dbg(sa, $"时间停止({(second ? "P3.1" : "P3")})：{tag} → {(dest.HasValue ? dest.Value.ToString("F1") : "无指路")}");
    }

    private static void A12SAddPair(List<(uint A, uint B)> pairs, uint a, uint b)
    {
        if (a == 0 || b == 0 || a == b) return;
        if (pairs.Any(p => (p.A == a && p.B == b) || (p.A == b && p.B == a))) return;
        pairs.Add((a, b));
    }

    // 我在连线配对里的组号与组内位次：组0=含（点名排序后）首位者的那一对；组内按全队 index 小者为 0。
    // 未记录到自己的连线时按排序相邻(0&1、2&3)兜底
    private (int Group, int Rank) A12SPairInfo(ScriptAccessory sa, List<(uint A, uint B)> pairs, List<uint> sorted, uint me)
    {
        uint partner = 0;
        foreach (var (a, b) in pairs)
        {
            if (a == me) { partner = b; break; }
            if (b == me) { partner = a; break; }
        }
        if (partner == 0)
        {
            int idx = sorted.IndexOf(me);
            int pIdx = idx % 2 == 0 ? idx + 1 : idx - 1;
            if (pIdx < 0 || pIdx >= sorted.Count) return (0, 0);
            partner = sorted[pIdx];
            Dbg(sa, "配对：未记录到自己的连线，按排序相邻兜底");
        }
        int group = me == sorted[0] || partner == sorted[0] ? 0 : 1;
        int rank = sa.Data.PartyList.IndexOf(me) <= sa.Data.PartyList.IndexOf(partner) ? 0 : 1;
        return (group, rank);
    }

    // StartCasting 6638 百万神圣：_phase=3.1 并清空点名记录；非 T 提示裸吃打复仇+复仇血量监控
    [ScriptMethod(
        name: "A12S - 百万神圣控血提醒",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:6638"])]
    public void A12S百万神圣(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A12STerritory)) return;
        if (_phase != 3) return;
        _phase = 3.1;
        A12SClearMechLists();
        if (_roleStatus != TankStatus)
        {
            Announce(sa, "裸吃 打复仇", 3000);
            StartA12SRevengeHpWatch(sa, "停止复仇");
        }
        Dbg(sa, "百万神圣6638：_phase=3.1，清空点名记录");
    }

    // ActionEffect 6638 且是自己：T 提示玄之力吃顺劈
    [ScriptMethod(
        name: "A12S - 玄之力提示",
        eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:6638"])]
    public void A12S玄之力(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A12STerritory)) return;
        if (_phase != 3.1) return;
        if (evt.TargetId() != sa.Data.Me) return;
        if (_roleStatus != TankStatus) return;
        Announce(sa, "玄之力吃顺劈", 3000);
    }

    // StartCasting 6659 时空门：_phase=4，开启 boss 10% 扎针监控；index3-6 各自指路进门并提示秒小怪
    [ScriptMethod(
        name: "A12S - 时空门",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:6659"])]
    public void A12S时空门(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A12STerritory)) return;
        if (_phase != 3.1) return;
        _phase = 4;
        _a12sCrystalCount = 0;
        StartA12SSewHpWatch(sa, evt.SourceId());

        int myIdx = sa.Data.PartyList.IndexOf(sa.Data.Me);
        Vector3? dest = myIdx switch
        {
            3 => (Vector3?)new Vector3(-14f, 400f, 14f),   // 左下门
            4 => new Vector3(14f, 400f, 14f),              // 右下门
            5 => new Vector3(-14f, 400f, -14f),            // 左上门
            6 => new Vector3(14f, 400f, -14f),             // 右上门
            _ => null,
        };
        if (dest is { } d)
        {
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement,
                sa.WaypointDp(d, 5000, 0, "A12S时空门指路", sa.Data.DefaultSafeColor));
            Announce(sa, "进门后 月笛一套秒掉", 3000);
        }
        Dbg(sa, $"时空门6659：_phase=4，index{myIdx}");
    }

    // ---------------- P4 ----------------

    // StartCasting 6660 审判结晶（4.5s，多次触发）：按触发次数指路 A→D→C→B 各点旁隔一个黄点的第二个黄点
    [ScriptMethod(
        name: "A12S - 审判结晶指路",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:6660"])]
    public void A12S审判结晶(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A12STerritory)) return;
        if (_phase != 4) return;
        if (_a12sCrystalCount >= A12SCrystalSpots.Length)
        {
            Dbg(sa, "审判结晶6660：已超过 4 次，忽略");
            return;
        }
        var dest = A12SCrystalSpots[_a12sCrystalCount++];
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement,
            sa.WaypointDp(dest, 4500, 0, "A12S审判结晶指路", sa.Data.DefaultSafeColor));
        Dbg(sa, $"审判结晶6660：第{_a12sCrystalCount}次 → {dest:F1}");
    }

    // StartCasting 6634 净化射线：被点名的是自己 → 2s 后超硬化
    [ScriptMethod(
        name: "A12S - 净化射线超硬化",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:6634"])]
    public void A12S净化射线(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A12STerritory)) return;
        if (_phase != 4) return;
        if (evt.TargetId() != sa.Data.Me) return;
        Dbg(sa, "净化射线6634：预约超硬化 2s 开按");
        ScheduleAutoCast(sa, HardenActionId, HardenActionType, sa.Data.Me, "净化射线超硬化", 2000);
    }

    // ---------------- A12S 辅助 ----------------

    // 复仇血量监控：自身血量（不含盾）高于 20% 时播报 stopText 并停止
    private void StartA12SRevengeHpWatch(ScriptAccessory sa, string stopText)
    {
        StopA12SRevengeHpWatch(sa);
        _a12sRevengeHpGuid = sa.Method.RegistFrameworkUpdateAction(() =>
        {
            if (_a12sRevengeHpGuid == null) return;
            if (sa.Data.Objects.SearchByEntityId(sa.Data.Me) is not IBattleChara me || me.MaxHp == 0) return;
            if (me.CurrentHp / (double)me.MaxHp <= 0.20) return;
            Announce(sa, stopText, 2000);
            Dbg(sa, $"复仇监控：血量回到 20% 以上，{stopText}");
            StopA12SRevengeHpWatch(sa);
        }, true, false);
    }

    private void StopA12SRevengeHpWatch(ScriptAccessory sa)
    {
        if (_a12sRevengeHpGuid == null) return;
        sa.Method.UnregistFrameworkUpdateAction(_a12sRevengeHpGuid);
        _a12sRevengeHpGuid = null;
    }

    // 扎针血量监控：boss 血量 <10% 播报一次
    private void StartA12SSewHpWatch(ScriptAccessory sa, uint bossId)
    {
        StopA12SSewHpWatch(sa);
        if (bossId == 0) return;
        _a12sSewHpGuid = sa.Method.RegistFrameworkUpdateAction(() =>
        {
            if (_a12sSewHpGuid == null) return;
            if (sa.Data.Objects.SearchByEntityId(bossId) is not IBattleChara boss || boss.MaxHp == 0) return;
            if (boss.CurrentHp / (double)boss.MaxHp > 0.10) return;
            Announce(sa, "扎针", 5000);
            Dbg(sa, "扎针监控：boss 血量 <10%");
            StopA12SSewHpWatch(sa);
        }, true, false);
    }

    private void StopA12SSewHpWatch(ScriptAccessory sa)
    {
        if (_a12sSewHpGuid == null) return;
        sa.Method.UnregistFrameworkUpdateAction(_a12sSewHpGuid);
        _a12sSewHpGuid = null;
    }

    private void A12SClearMechLists()
    {
        _a12sPurple.Clear();
        _a12sStack.Clear();
        _a12sNear.Clear();
        _a12sFar.Clear();
        _a12sNearPairs.Clear();
        _a12sFarPairs.Clear();
    }

    [ScriptMethod(
        name: "A12S - 手动重置",
        eventType: EventTypeEnum.CombatChanged,
        eventCondition: ["InCombat:False"],
        userControl: false)]
    public void A12S手动重置(Event evt, ScriptAccessory sa)
    {
        if (!InMap(A12STerritory)) return;
        _phase = 1;
        _a12s6647Done = false;
        _a12sCrystalCount = 0;
        A12SClearMechLists();
        StopAutoCast(sa);
        StopA12SRevengeHpWatch(sa);
        StopA12SSewHpWatch(sa);
        sa.Method.RemoveDraw(".*");
        RefreshRole(sa);
    }
    #endregion
}

#region Helpers

public static class EventExtensions
{
    private static bool ParseHexId(string? idStr, out uint id)
        => uint.TryParse(idStr?.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out id);


    public static float SourceRotation(this Event evt) => JsonConvert.DeserializeObject<float>(evt["SourceRotation"]);
    public static uint ActionId(this Event evt) => JsonConvert.DeserializeObject<uint>(evt["ActionId"]);
    public static uint SourceId(this Event evt) => ParseHexId(evt["SourceId"], out var id) ? id : 0;
    public static uint TargetId(this Event evt) => ParseHexId(evt["TargetId"], out var id) ? id : 0;
    public static Vector3 SourcePosition(this Event evt) => JsonConvert.DeserializeObject<Vector3>(evt["SourcePosition"]);
}

public static class ScriptAccessoryExtensions
{
    public static DrawPropertiesEdit WaypointDp(this ScriptAccessory sa, Vector3 target, uint duration, uint delay = 0, string name = "Waypoint", Vector4? color = null)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? sa.Data.DefaultSafeColor;
        dp.Owner = sa.Data.Me;
        dp.TargetPosition = target;
        dp.DestoryAt = duration;
        dp.Delay = delay;
        dp.Scale = new Vector2(2);
        dp.ScaleMode = ScaleMode.YByDistance;
        return dp;
    }
}

#endregion
