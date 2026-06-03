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

using DalamudIGameObject = Dalamud.Game.ClientState.Objects.Types.IGameObject;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Microsoft.Win32.SafeHandles;
using System.Diagnostics.Tracing;

namespace Codaaaaaa.Kefka;

[ScriptType(
    guid: "cc2c6d88-abe5-40be-89da-5f231b9d21d8",
    name: "绝凯夫卡先行版补丁",
    territorys: [1363],
    version: "0.0.0.2",
    author: "Codaaaaaa",
    note: "自用。请支持K佬&灵视佬")]
public class Kefka
{
    #region 用户设置

    internal static bool DebugEnabled;

    [UserSetting("调试：输出调试日志")]
    public bool DebugLog
    {
        get => DebugEnabled;
        set => DebugEnabled = value;
    }

    public enum BigIceSealGuideMode
    {
        十字放黑泥,
        盗火
    }

    [UserSetting("P1_神像1攻略")]
    public XuanHuHuGuideMode XuanHuHuMode { get; set; } = XuanHuHuGuideMode.盗火烬;

    [UserSetting("扩大大冰封指路方式")]
    public BigIceSealGuideMode BigIceSealMode { get; set; } = BigIceSealGuideMode.十字放黑泥;

    public enum XuanHuHuGuideMode
    {
        固定半场,
        盗火烬,
        
    }

    #endregion

    #region 变量和初始化

    private double _phase = 1;

    private readonly object _iceFireLock = new();

    // true = 真冰，false = 假冰
    private bool? _iceIsReal = null;

    // true = 左上右下，false = 左下右上
    private bool? _iceDiagonalIsLeftUpRightDown = null;

    // true = 实际分摊，false = 实际散开
    private bool? _actualStack = null;

    private readonly HashSet<uint> _iceFireTargets = new();
    private readonly HashSet<uint> _waveCannonTargets = new();

    private readonly Dictionary<uint, Vector3> _action47784Targets = new();
    private bool _action47784GuideSent = false;

    private static readonly Vector3 UpperLeftPoint = new(93.90f, 0.00f, 93.94f);
    private static readonly Vector3 UpperRightPoint = new(106.09f, 0.00f, 93.93f);

    // 上场点沿 Z=100 镜像得到的下场点（固定半场不连线用），数值可按实际场地微调
    private static readonly Vector3 LowerLeftPoint = new(93.90f, 0.00f, 106.06f);
    private static readonly Vector3 LowerRightPoint = new(106.09f, 0.00f, 106.07f);

    private const uint RealIceAction = 47768;
    private const uint StackIcon = 0x0080;
    private const uint SpreadIcon = 0x007F;

    public void Init(ScriptAccessory sa)
    {
        _phase = 1;

        lock (_iceFireLock)
        {
            _iceIsReal = null;
            _iceDiagonalIsLeftUpRightDown = null;
            _actualStack = null;
            _iceFireTargets.Clear();
            _waveCannonTargets.Clear();
            _action47784Targets.Clear();
            _action47784GuideSent = false;
        }

        sa.Method.RemoveDraw(".*");
    }

    #endregion

    #region 阶段控制

    [ScriptMethod(name: "Set Phase 1", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASP1"], userControl: false)]
    public void SetP1(Event evt, ScriptAccessory sa) => _phase = 1;

    [ScriptMethod(name: "Set Phase 2", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASP2"], userControl: false)]
    public void SetP2(Event evt, ScriptAccessory sa) => _phase = 2;

    [ScriptMethod(name: "Set Phase 3", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASP3"], userControl: false)]
    public void SetP3(Event evt, ScriptAccessory sa) => _phase = 3;

    [ScriptMethod(name: "Show Phase", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:phase"], userControl: false)]
    public void ShowPhase(Event evt, ScriptAccessory sa) => sa.Debug(_phase);

    #endregion

    #region P1 冰爆指路

    [ScriptMethod(
        name: "P1_冰爆_记录冰真假与方向",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47768|47771)$"],
        userControl: false)]
    public void P1_冰爆_记录冰真假与方向(Event evt, ScriptAccessory sa)
    {
        if (_phase > 1) return;

        var actionId = evt.ActionId();

        bool iceIsReal = actionId == RealIceAction;
        bool rawDiagonalIsLeftUpRightDown = IsLeftUpRightDownByRotation(evt.SourceRotation());

        // 47768 真冰：实际覆盖方向 = SourceRotation 判断出来的方向
        // 47771 假冰：实际覆盖方向 = 反方向
        bool actualDiagonalIsLeftUpRightDown = iceIsReal
            ? rawDiagonalIsLeftUpRightDown
            : !rawDiagonalIsLeftUpRightDown;

        lock (_iceFireLock)
        {
            _iceIsReal = iceIsReal;
            _iceDiagonalIsLeftUpRightDown = actualDiagonalIsLeftUpRightDown;
        }

        sa.Debug($"""
        Ice cast:
        ActionId={actionId}
        SourceRotation={evt.SourceRotation()}
        IceIsReal={iceIsReal}
        RawDiagonal={(rawDiagonalIsLeftUpRightDown ? "左上右下" : "左下右上")}
        ActualDiagonal={(actualDiagonalIsLeftUpRightDown ? "左上右下" : "左下右上")}
        UpperCoveredPoint={(actualDiagonalIsLeftUpRightDown ? "左上点" : "右上点")}
        """);
    }

    [ScriptMethod(
        name: "P1_冰爆_记录分摊散开",
        eventType: EventTypeEnum.TargetIcon,
        eventCondition: ["Id:regex:^(0080|007F)$"],
        userControl: false)]
    public void P1_冰爆_记录分摊散开(Event evt, ScriptAccessory sa)
    {
        uint iconId = evt.IconId();
        uint targetId = evt.TargetId();

        if (targetId == 0)
            return;

        lock (_iceFireLock)
        {
            _iceFireTargets.Add(targetId);

            // 2 个 0080：认为实际是分摊
            if (_iceFireTargets.Count == 2 && iconId == StackIcon)
                _actualStack = true;

            // 8 个 007F：认为实际是散开
            if (_iceFireTargets.Count == 8 && iconId == SpreadIcon)
                _actualStack = false;
        }

        sa.Debug($"""
        Fire icon:
        IconId=0x{iconId:X4}
        TargetId=0x{targetId:X}
        TargetCount={_iceFireTargets.Count}
        ActualStack={_actualStack}
        """);
    }

    [ScriptMethod(
        name: "P1_冰爆_记录002D点名",
        eventType: EventTypeEnum.Tether,
        eventCondition: ["Id:002D"],
        userControl: false)]
    public void P1_冰爆_记录002D点名(Event evt, ScriptAccessory sa)
    {
        uint targetId = evt.TargetId();

        if (targetId == 0)
            return;

        lock (_iceFireLock)
        {
            _waveCannonTargets.Add(targetId);
        }

        sa.Debug($"002D target: 0x{targetId:X}");
    }

    [ScriptMethod(
        name: "P1_冰爆_玄乎乎魔法指路_盗火烬",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:47764"])]
    public void P1_冰爆_玄乎乎魔法指路_盗火烬(Event evt, ScriptAccessory sa)
    {
        if (_phase != 1)
            return;

        if (XuanHuHuMode != XuanHuHuGuideMode.盗火烬)
            return;

        Task.Run(async () =>
        {
            await Task.Delay(100);

            bool? iceDiagonalIsLeftUpRightDown;
            bool? iceIsReal;
            bool? actualStack;
            HashSet<uint> waveCannonTargetsSnapshot;

            lock (_iceFireLock)
            {
                iceDiagonalIsLeftUpRightDown = _iceDiagonalIsLeftUpRightDown;
                iceIsReal = _iceIsReal;
                actualStack = _actualStack;
                waveCannonTargetsSnapshot = new HashSet<uint>(_waveCannonTargets);
            }

            if (iceDiagonalIsLeftUpRightDown == null)
            {
                sa.Debug("玄乎乎魔法指路失败：没有记录到冰方向。");
                return;
            }

            int myIdx = sa.MyIndex();
            bool meHas002D = waveCannonTargetsSnapshot.Contains(sa.Data.Me);

            Vector3 coveredUpperPoint = iceDiagonalIsLeftUpRightDown.Value
                ? UpperLeftPoint
                : UpperRightPoint;

            Vector3 uncoveredUpperPoint = iceDiagonalIsLeftUpRightDown.Value
                ? UpperRightPoint
                : UpperLeftPoint;

            Vector3 myFirstGuidePos = meHas002D
                ? coveredUpperPoint
                : uncoveredUpperPoint;

            uint firstDuration = 3000;

            var firstGuideDp = sa.WaypointDp(
                myFirstGuidePos,
                firstDuration,
                0,
                $"P1_冰爆_玄乎乎魔法指路_第一段_{myIdx}"
            );

            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, firstGuideDp);

            sa.Debug($"""
            玄乎乎魔法第一段指路:
            IceIsReal={iceIsReal}
            ActualStack={actualStack}
            ActualDiagonal={(iceDiagonalIsLeftUpRightDown.Value ? "左上右下" : "左下右上")}
            MeHas002D={meHas002D}
            TargetPoint={myFirstGuidePos}
            DrawName=P1_冰爆_玄乎乎魔法指路_第一段_{myIdx}
            """);

            await Task.Delay(5000);

            var rightSidePoints = new List<Vector3>
            {
                new(103.00f, 0.00f, 100.00f),
                new(109.00f, 0.00f, 100.00f),
                new(113.00f, 0.00f, 100.00f),
                new(119.00f, 0.00f, 100.00f),
            };

            var leftSidePoints = new List<Vector3>
            {
                new(97.00f, 0.00f, 100.00f),
                new(91.00f, 0.00f, 100.00f),
                new(87.00f, 0.00f, 100.00f),
                new(81.00f, 0.00f, 100.00f),
            };

            var party = sa.Data.PartyList;

            var rightSideMembers = party
                .Where(id =>
                {
                    bool has002D = waveCannonTargetsSnapshot.Contains(id);
                    Vector3 firstPos = has002D ? coveredUpperPoint : uncoveredUpperPoint;
                    return firstPos.X > 100.0f;
                })
                .OrderBy(id => party.IndexOf(id))
                .ToList();

            var leftSideMembers = party
                .Where(id =>
                {
                    bool has002D = waveCannonTargetsSnapshot.Contains(id);
                    Vector3 firstPos = has002D ? coveredUpperPoint : uncoveredUpperPoint;
                    return firstPos.X < 100.0f;
                })
                .OrderBy(id => party.IndexOf(id))
                .ToList();

            Vector3? mySecondGuidePos = null;

            int myRightOrder = rightSideMembers.IndexOf(sa.Data.Me);
            if (myRightOrder >= 0 && myRightOrder < rightSidePoints.Count)
                mySecondGuidePos = rightSidePoints[myRightOrder];

            int myLeftOrder = leftSideMembers.IndexOf(sa.Data.Me);
            if (myLeftOrder >= 0 && myLeftOrder < leftSidePoints.Count)
                mySecondGuidePos = leftSidePoints[myLeftOrder];

            if (mySecondGuidePos == null)
            {
                sa.Debug("玄乎乎魔法第二段指路失败：没有找到自己的左右半场排序。");
                return;
            }

            uint secondDuration = 5000;

            var secondGuideDp = sa.WaypointDp(
                mySecondGuidePos.Value,
                secondDuration,
                0,
                $"P1_冰爆_玄乎乎魔法指路_第二段_{myIdx}"
            );

            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, secondGuideDp);

            string side = myRightOrder >= 0 ? "右半场" : "左半场";
            int order = myRightOrder >= 0 ? myRightOrder : myLeftOrder;

            sa.Debug($"""
            玄乎乎魔法第二段指路:
            Side={side}
            Order={order}
            MyIndex={myIdx}
            TargetPoint={mySecondGuidePos.Value}
            DrawName=P1_冰爆_玄乎乎魔法指路_第二段_{myIdx}
            RightMembers={string.Join(",", rightSideMembers.Select(x => party.IndexOf(x)))}
            LeftMembers={string.Join(",", leftSideMembers.Select(x => party.IndexOf(x)))}
            """);
        });
    }

    [ScriptMethod(
        name: "P1_冰爆_玄乎乎魔法指路_固定半场",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:47764"])]
    public void P1_冰爆_玄乎乎魔法指路_固定半场(Event evt, ScriptAccessory sa)
    {
        if (_phase != 1)
            return;

        if (XuanHuHuMode != XuanHuHuGuideMode.固定半场)
            return;

        Task.Run(async () =>
        {
            await Task.Delay(100);

            bool? iceDiagonalIsLeftUpRightDown;
            HashSet<uint> waveCannonTargetsSnapshot;

            lock (_iceFireLock)
            {
                iceDiagonalIsLeftUpRightDown = _iceDiagonalIsLeftUpRightDown;
                waveCannonTargetsSnapshot = new HashSet<uint>(_waveCannonTargets);
            }

            if (iceDiagonalIsLeftUpRightDown == null)
            {
                sa.Debug("固定半场指路失败：没有记录到冰方向。");
                return;
            }

            int myIdx = sa.MyIndex();
            if (myIdx < 0 || myIdx > 7)
            {
                sa.Debug($"固定半场指路失败：自己的 index 异常。MyIndex={myIdx}");
                return;
            }

            // 0/1/2/3 固定左半场，4/5/6/7 固定右半场
            bool isLeftHalf = myIdx <= 3;
            bool meHas002D = waveCannonTargetsSnapshot.Contains(sa.Data.Me);
            // _iceDiagonalIsLeftUpRightDown == true 表示左上右下覆盖，即左上有冰
            bool upperLeftHasIce = iceDiagonalIsLeftUpRightDown.Value;

            Vector3 myGuidePos = GetFixedHalfPos(isLeftHalf, meHas002D, upperLeftHasIce);

            uint duration = 8000;

            var guideDp = sa.WaypointDp(
                myGuidePos,
                duration,
                0,
                $"P1_冰爆_玄乎乎魔法指路_固定半场_{myIdx}"
            );

            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, guideDp);

            sa.Debug($"""
            固定半场指路:
            MyIndex={myIdx}
            Half={(isLeftHalf ? "左半场" : "右半场")}
            MeHas002D={meHas002D}
            UpperLeftHasIce={upperLeftHasIce}
            TargetPoint={myGuidePos}
            DrawName=P1_冰爆_玄乎乎魔法指路_固定半场_{myIdx}
            """);
        });
    }

    [ScriptMethod(
        name: "P1_冰爆_47784换位指路",
        eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:47784"],
        userControl: false)]
    public void P1_冰爆_47784换位指路(Event evt, ScriptAccessory sa)
    {
        uint targetId = evt.TargetId();
        Vector3 effectPosition = evt.TargetPosition();

        if (targetId == 0)
            return;

        _phase = 1.5;

        Dictionary<uint, Vector3>? snapshot = null;

        lock (_iceFireLock)
        {
            if (_action47784GuideSent)
                return;

            _action47784Targets[targetId] = effectPosition;

            sa.Debug($"""
            47784 ActionEffect:
            TargetId=0x{targetId:X}
            TargetIndex={sa.Data.PartyList.IndexOf(targetId)}
            TargetPosition={effectPosition}
            Count={_action47784Targets.Count}
            """);

            if (_action47784Targets.Count < 4)
                return;

            _action47784GuideSent = true;
            snapshot = new Dictionary<uint, Vector3>(_action47784Targets);
        }

        Task.Run(async () =>
        {
            await Task.Delay(100);

            var party = sa.Data.PartyList;
            int myIdx = sa.MyIndex();

            if (myIdx < 0 || myIdx > 7)
            {
                sa.Debug($"47784换位指路失败：自己的 index 异常。MyIndex={myIdx}");
                return;
            }

            var lowGroup = Enumerable.Range(0, 4)
                .Where(i => i < party.Count)
                .Select(i => party[i])
                .ToList();

            var highGroup = Enumerable.Range(4, 4)
                .Where(i => i < party.Count)
                .Select(i => party[i])
                .ToList();

            Vector3? myGuidePos = Get47784GuidePosForGroup(sa.Data.Me, lowGroup, party, snapshot);
            myGuidePos ??= Get47784GuidePosForGroup(sa.Data.Me, highGroup, party, snapshot);

            if (myGuidePos == null)
            {
                sa.Debug($"""
                47784换位指路：自己不需要指路或没有匹配到。
                MyIndex={myIdx}
                MeWasTargeted={snapshot.ContainsKey(sa.Data.Me)}
                Targets={string.Join(",", snapshot.Keys.Select(x => party.IndexOf(x)).OrderBy(x => x))}
                """);
                return;
            }

            uint duration = 4500;

            var guideDp = sa.WaypointDp(
                myGuidePos.Value,
                duration,
                0,
                $"P1_冰爆_47784换位指路_{myIdx}"
            );

            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, guideDp);

            sa.Debug($"""
            47784换位指路:
            MyIndex={myIdx}
            TargetPoint={myGuidePos.Value}
            DrawName=P1_冰爆_47784换位指路_{myIdx}
            All47784Targets={string.Join(",", snapshot.Keys.Select(x => party.IndexOf(x)).OrderBy(x => x))}
            LowGroupTargets={string.Join(",", snapshot.Keys.Where(x => party.IndexOf(x) >= 0 && party.IndexOf(x) <= 3).Select(x => party.IndexOf(x)).OrderBy(x => x))}
            HighGroupTargets={string.Join(",", snapshot.Keys.Where(x => party.IndexOf(x) >= 4 && party.IndexOf(x) <= 7).Select(x => party.IndexOf(x)).OrderBy(x => x))}
            """);
        });
    }

    #endregion

    #region P1 扩大大冰封

    [ScriptMethod(
        name: "P1_第二次扩大大冰封指路",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:47765"])]
    public void P1_第二次扩大大冰封指路(Event evt, ScriptAccessory sa)
    {
        if (_phase >= 2)
            return;

        int myIdx = sa.MyIndex();

        if (myIdx < 0 || myIdx > 7)
        {
            sa.Debug($"扩大大冰封指路失败：自己的 index 异常。MyIndex={myIdx}");
            return;
        }

        Vector3? myGuidePos = BigIceSealMode switch
        {
            BigIceSealGuideMode.十字放黑泥 => GetBigIceSealCrossBlackMudPos(myIdx),
            BigIceSealGuideMode.盗火 => GetBigIceSealStealFirePos(myIdx),
            _ => null
        };

        if (myGuidePos == null)
        {
            sa.Debug($"扩大大冰封指路失败：没有匹配到点位。Mode={BigIceSealMode}, MyIndex={myIdx}");
            return;
        }

        uint duration = 6000;

        var guideDp = sa.WaypointDp(
            myGuidePos.Value,
            duration,
            0,
            $"P1_第二次扩大大冰封指路_{BigIceSealMode}_{myIdx}"
        );

        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, guideDp);

        sa.Debug($"""
        第二次扩大大冰封指路:
        Phase={_phase}
        Mode={BigIceSealMode}
        MyIndex={myIdx}
        TargetPoint={myGuidePos.Value}
        """);
    }

    [ScriptMethod(
        name: "P1_众神之像1_扩大大冰封_假技能范围",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47768|47774)$"])]
    public void P1_众神之像1_扩大大冰封_假技能范围(Event evt, ScriptAccessory sa)
    {
        var sourceId = evt.SourceId();

        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Scale = new(40);
        dp.Radian = float.Pi / 2;
        dp.Owner = sourceId;
        dp.Color = sa.Data.DefaultDangerColor;
        dp.DestoryAt = 5000;

        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    #endregion

    #region P1 屏蔽特效

    [ScriptMethod(
        name: "P1_屏蔽假雷假冰",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47776|47771)$"])]
    public void P1_屏蔽假雷假冰(Event evt, ScriptAccessory sa)
    {
        var obj = sa.GetById((uint)evt.SourceId);
        if (obj == null) return;

        sa.WriteVisible(obj, false);
    }

    #endregion

    #region 工具方法

    private static Vector3? GetBigIceSealCrossBlackMudPos(int index)
    {
        return index switch
        {
            0 or 6 => new Vector3(100.00f, 0.00f, 80.50f),   // 上
            1 or 5 => new Vector3(100.00f, 0.00f, 119.50f),  // 下
            2 or 4 => new Vector3(80.50f, 0.00f, 100.00f),   // 左
            3 or 7 => new Vector3(119.50f, 0.00f, 100.00f),  // 右
            _ => null
        };
    }

    private static Vector3? GetBigIceSealStealFirePos(int index)
    {
        return index switch
        {
            0 or 6 => new Vector3(100.00f, 0.00f, 80.50f),   // 上
            5 or 3 => new Vector3(100.00f, 0.00f, 119.50f),  // 下
            2 or 4 => new Vector3(80.50f, 0.00f, 100.00f),   // 左
            1 or 7 => new Vector3(119.50f, 0.00f, 100.00f),  // 右
            _ => null
        };
    }

    // 固定半场点位
    // 连线的(002D)：基准点 = 本半场的“之前的点”(左半场 UpperLeftPoint / 右半场 UpperRightPoint)
    //   - 左上有冰 => 基准点
    //   - 左上没冰 => 基准点往上 10 (Z - 10)
    // 不连线的：去本半场没冰的那一格(上场 or 下场)
    //   upperLeftHasIce(左上右下有冰): 左半场没冰在下(SW)，右半场没冰在上(NE)
    //  !upperLeftHasIce(左下右上有冰): 左半场没冰在上(NW)，右半场没冰在下(SE)
    private static Vector3 GetFixedHalfPos(bool isLeftHalf, bool tethered, bool upperLeftHasIce)
    {
        Vector3 basePoint = isLeftHalf ? UpperLeftPoint : UpperRightPoint;

        if (tethered)
        {
            return upperLeftHasIce
                ? basePoint
                : new Vector3(basePoint.X, basePoint.Y, basePoint.Z - 10f);
        }

        if (isLeftHalf)
            return upperLeftHasIce ? LowerLeftPoint : UpperLeftPoint;

        return upperLeftHasIce ? UpperRightPoint : LowerRightPoint;
    }

    private static Vector3? Get47784GuidePosForGroup(
        uint me,
        List<uint> groupMembers,
        IReadOnlyList<uint> party,
        Dictionary<uint, Vector3> targetPositions)
    {
        if (!groupMembers.Contains(me))
            return null;

        if (targetPositions.ContainsKey(me))
            return null;

        var targetedMembers = groupMembers
            .Where(id => targetPositions.ContainsKey(id))
            .OrderBy(id => PartyIndex(party, id))
            .ToList();

        var untargetedMembers = groupMembers
            .Where(id => !targetPositions.ContainsKey(id))
            .OrderBy(id => PartyIndex(party, id))
            .ToList();

        // 正常每组应该是 2 个被点，2 个没被点?
        if (targetedMembers.Count != 2 || untargetedMembers.Count != 2)
            return null;

        int myOrder = untargetedMembers.IndexOf(me);

        if (myOrder < 0 || myOrder >= targetedMembers.Count)
            return null;

        uint targetToGo = targetedMembers[myOrder];

        return targetPositions[targetToGo];
    }

    private static int PartyIndex(IReadOnlyList<uint> party, uint id)
    {
        for (int i = 0; i < party.Count; i++)
        {
            if (party[i] == id)
                return i;
        }

        return 999;
    }

    private static bool IsLeftUpRightDownByRotation(float rotation)
    {
        // SourceRotation 转成 0~360 度
        // 只区分两种对角线：约 45/225 度一组，约 135/315 度一组

        double degree = rotation * 180.0 / Math.PI;
        degree = (degree % 360.0 + 360.0) % 360.0;

        double distanceTo45Or225 = Math.Min(
            AngleDistance(degree, 45.0),
            AngleDistance(degree, 225.0)
        );

        double distanceTo135Or315 = Math.Min(
            AngleDistance(degree, 135.0),
            AngleDistance(degree, 315.0)
        );

        return distanceTo45Or225 <= distanceTo135Or315;
    }

    private static double AngleDistance(double a, double b)
    {
        double diff = Math.Abs(a - b) % 360.0;
        return diff > 180.0 ? 360.0 - diff : diff;
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

        if (string.IsNullOrEmpty(idStr))
            return false;

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

    public static uint ActionId(this Event evt)
        => JsonConvert.DeserializeObject<uint>(evt["ActionId"]);

    public static uint DataId(this Event evt)
        => JsonConvert.DeserializeObject<uint>(evt["DataId"]);

    public static uint SourceId(this Event evt)
        => ParseHexId(evt["SourceId"], out var id) ? id : 0;

    public static uint TargetId(this Event evt)
        => ParseHexId(evt["TargetId"], out var id) ? id : 0;

    public static uint IconId(this Event evt)
    {
        var raw = evt["Id"];

        if (string.IsNullOrWhiteSpace(raw))
            return 0;

        raw = raw.Trim();

        if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            raw = raw[2..];

        return uint.TryParse(
            raw,
            System.Globalization.NumberStyles.HexNumber,
            null,
            out var result
        )
            ? result
            : 0;
    }

    public static Vector3 SourcePosition(this Event evt)
        => ParseVector3(evt["SourcePosition"]);

    public static Vector3 EffectPosition(this Event evt)
        => ParseVector3(evt["EffectPosition"]);

    public static Vector3 TargetPosition(this Event evt)
        => ParseVector3(evt["TargetPosition"]);

    public static uint DirectorId(this Event evt)
        => ParseHexId(evt["DirectorId"], out var id) ? id : 0;

    private static Vector3 ParseVector3(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Vector3.Zero;

        raw = raw.Trim();

        // 1. 先尝试标准 JSON
        try
        {
            return JsonConvert.DeserializeObject<Vector3>(raw);
        }
        catch
        {
        }

        // 2. 再尝试非标准格式：X:100, Y:0, Z:100
        try
        {
            float x = ExtractFloat(raw, "X");
            float y = ExtractFloat(raw, "Y");
            float z = ExtractFloat(raw, "Z");
            return new Vector3(x, y, z);
        }
        catch
        {
            return Vector3.Zero;
        }
    }

    private static float ExtractFloat(string raw, string key)
    {
        var pattern = $@"{key}\s*[:=]\s*(-?\d+(?:[\.,]\d+)?)";
        var match = System.Text.RegularExpressions.Regex.Match(
            raw,
            pattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        if (!match.Success)
            return 0f;

        var value = match.Groups[1].Value.Replace(',', '.');

        return float.Parse(
            value,
            System.Globalization.CultureInfo.InvariantCulture
        );
    }
}

public static class ScriptAccessoryExtensions
{
    public static int MyIndex(this ScriptAccessory sa)
        => sa.Data.PartyList.IndexOf(sa.Data.Me);

    public static void Debug(this ScriptAccessory sa, object? text)
    {
        if (!Kefka.DebugEnabled)
            return;

        sa.Method.SendChat($"/e [Debug]{text?.ToString() ?? "null"}");
    }

    public static DrawPropertiesEdit FastDp(
        this ScriptAccessory sa,
        string name,
        Vector3 pos,
        uint duration,
        Vector2 scale,
        bool safe = false)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = safe ? sa.Data.DefaultSafeColor : sa.Data.DefaultDangerColor;
        dp.Position = pos;
        dp.DestoryAt = duration;
        dp.Scale = scale;
        return dp;
    }

    public static DrawPropertiesEdit WaypointToObjectDp(
        this ScriptAccessory sa,
        uint targetObjectId,
        uint duration,
        uint delay = 0,
        string name = "WaypointToObject",
        Vector4? color = null)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? sa.Data.DefaultSafeColor;
        dp.Owner = sa.Data.Me;
        dp.DestoryAt = duration;
        dp.Delay = delay;
        dp.Scale = new Vector2(2);
        dp.ScaleMode = ScaleMode.YByDistance;

        var prop =
            typeof(DrawPropertiesEdit).GetProperty("TargetObject") ??
            typeof(DrawPropertiesEdit).GetProperty("TargetId") ??
            typeof(DrawPropertiesEdit).GetProperty("TargetID");

        if (prop != null && prop.CanWrite)
        {
            var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            object value = targetObjectId;

            if (targetType != typeof(uint))
            {
                value = Convert.ChangeType(targetObjectId, targetType);
            }

            prop.SetValue(dp, value);
        }

        return dp;
    }

    public static DrawPropertiesEdit WaypointDp(
        this ScriptAccessory sa,
        Vector3 target,
        uint duration,
        uint delay = 0,
        string name = "Waypoint",
        Vector4? color = null)
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

    public static DrawPropertiesEdit WaypointFromToDp(
        this ScriptAccessory sa,
        Vector3 from,
        Vector3 to,
        uint duration,
        uint delay = 0,
        string name = "WaypointFromTo",
        Vector4? color = null)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? sa.Data.DefaultSafeColor;
        dp.Owner = 0;
        dp.Position = from;
        dp.TargetPosition = to;
        dp.DestoryAt = duration;
        dp.Delay = delay;
        dp.Scale = new Vector2(2);
        dp.ScaleMode = ScaleMode.YByDistance;
        return dp;
    }
}

public static class HideVfxHelper
{
    public static IGameObject? GetById(this ScriptAccessory sa, ulong gameObjectId)
    {
        return sa.Data.Objects.SearchById(gameObjectId);
    }

    public static unsafe void WriteVisible(this ScriptAccessory sa, IGameObject? actor, bool visible)
    {
        if (actor == null || !actor.IsValid()) return;

        try
        {
            var gameObject = (GameObject*)actor.Address;

            gameObject->RenderFlags = visible
                ? VisibilityFlags.None
                : VisibilityFlags.Model;
        }
        catch (Exception e)
        {
            sa.Log.Error(e.ToString());
        }
    }
}

#endregion