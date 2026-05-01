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

namespace Codaaaaaa.Enuo;

[ScriptType(
    guid: "8c4a9f2d-6b31-4e0a-9f27-1d7c5b8a3e46",
    name: "恩欧歼殛战画图",
    territorys: [1362],
    version: "0.0.0.2",
    author: "Codaaaaaa",
    note: "mmw文档+NOCCHH")]
public class Enuo
{
    #region 用户设置
    // [UserSetting("是否开启TTS")] public static bool TTSOpen { get; set; } = true;
    [UserSetting("")] public static 热病提示enum 热病冷却提示 { get; set; } = 热病提示enum.横幅;
    [UserSetting("")] public static P2打法法enum P2打法 { get; set; } = P2打法法enum.NOCCHH;
    #endregion

    #region enum
    public enum 热病提示enum
    {
        横幅,
        默语,
        TTS
    }

    public enum P2打法法enum
    {
        NOCCHH,
        MMW
    }
    #endregion

    #region 变量和初始化
    private readonly object _commonMechanicLock = new();
    private readonly List<(uint OrbId, uint PlayerId, DateTime Time)> _blackBallTethers = new();

    // 混沌激流
    private readonly List<(Vector3 vector3, uint sourceId)> 混沌激流黑球生成位置 = new();
    private readonly List<(int GlobalOrder, Vector3 Pos, uint SourceId)> 混沌激流_0196tethers = new();
    private readonly List<(int GlobalOrder, Vector3 Pos, uint SourceId)> 混沌激流_0197tethers = new();
    private bool 混沌激流_0196已分配 = false;
    private bool 混沌激流_0197已分配 = false;
    private DateTime 撞球易伤上次提示时间 = DateTime.MinValue;

    private readonly object P1深度冻结锁 = new();
    private bool P1深度冻结冷却已提醒 = false;

    // P2 无之涡流
    private readonly object P2无之涡流锁 = new();
    private readonly List<int> P2无之涡流点名玩家 = new();
    private readonly bool[] P2无之涡流塔出现 = new bool[8];
    private readonly Vector3[] P2无之涡流塔位置 = new Vector3[8];
    private int P2无之涡流塔数量 = 0;
    private bool P2无之涡流已分配 = false;
    private static readonly Vector3[] P2_无之涡流固定点位 =
    [
        new Vector3(109.54f, -0.02f, 76.89f),   // 0
        new Vector3(123.09f, -0.02f, 90.41f),   // 1
        new Vector3(123.09f, -0.02f, 109.54f),  // 2 
        new Vector3(109.54f, -0.02f, 123.09f),  // 3 
        new Vector3(90.41f, -0.02f, 123.09f),   // 4 
        new Vector3(76.89f, -0.02f, 109.54f),   // 5 
        new Vector3(76.89f, -0.02f, 90.41f),    // 6 
        new Vector3(90.41f, -0.02f, 76.89f),    // 7 
        
    ];
    private uint P2无之涡流BossId = 0;

    private int P2_获取无之涡流塔Index(Vector3 pos)
    {
        var bestIndex = -1;
        var bestDist = float.MaxValue;

        for (int i = 0; i < P2_无之涡流固定点位.Length; i++)
        {
            var dist = Vector3.Distance(pos, P2_无之涡流固定点位[i]);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestIndex = i;
            }
        }

        return bestDist <= 5f ? bestIndex : -1;
    }

    public void Init(ScriptAccessory sa)
    {
        _phase = 1;
        lock (_commonMechanicLock)
        {
            _blackBallTethers.Clear();
        }

        lock (P1深度冻结锁)
        {
            P1深度冻结冷却已提醒 = false;
        }

        混沌激流黑球生成位置.Clear();
        混沌激流_0196tethers.Clear();
        混沌激流_0197tethers.Clear();
        混沌激流_0196已分配 = false;
        混沌激流_0197已分配 = false;
        撞球易伤上次提示时间 = DateTime.MinValue;

        P2_重置无之涡流();

        sa.Method.RemoveDraw(".*");
    }

    #endregion

    #region 阶段
    private double _phase = 1;

    [ScriptMethod(name: "Set Phase 1", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASP1"], userControl: false)]
    public void SetP1(Event evt, ScriptAccessory sa) => _phase = 1;

    [ScriptMethod(name: "Set Phase 2", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASP2"], userControl: false)]
    public void SetP2(Event evt, ScriptAccessory sa) => _phase = 2;

    [ScriptMethod(name: "Set Phase 3", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASP3"], userControl: false)]
    public void SetP3(Event evt, ScriptAccessory sa) => _phase = 3;
    #endregion

    #region 通用机制

    [ScriptMethod(name: "通用机制-无之膨胀", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(49977|49978|49979|49980)$"])]
    public async void 通用机制_无之膨胀(Event evt, ScriptAccessory sa)
    {
        var sourceId = evt.SourceId();
        if (sourceId == 0) return;

        const uint duration = 7700;
        var actionId = evt.ActionId();

        if (actionId == 49977)
        {
            // 49977 钢铁：半径 40
            DrawCircleOwner(sa, "通用机制-无之膨胀-钢铁", sourceId, 40f, duration, sa.Data.DefaultDangerColor);
        }
        else if (actionId == 49978)
        {
            // 49978 月环：外圈 60，内圈 10
            DrawDonutOwner(sa, "通用机制-无之膨胀-月环", sourceId, 60f, 40f, duration, sa.Data.DefaultDangerColor);
        }
        else if (actionId == 49979)
        {
            // 49979 钢铁：半径 20
            DrawCircleOwner(sa, "通用机制-无之膨胀-本体-钢铁", sourceId, 12f, duration, sa.Data.DefaultDangerColor);
        }
        else if (actionId == 49980)
        {
            // 49978 月环：外圈 60，内圈 10
            DrawDonutOwner(sa, "通用机制-无之膨胀-本体-月环", sourceId, 40f, 6f, duration, sa.Data.DefaultDangerColor);
        }
    }

    [ScriptMethod(name: "通用机制-回归重波动(单奶妈黑球)", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(02BE)$"])]
    public async void 通用机制_回归重波动(Event evt, ScriptAccessory sa)
    {
        if (!TryGetTetherOrbAndPlayer(evt, sa, out var orbId, out var playerId, out _)) return;

        RecordBlackBallTether(orbId, playerId);

        // 单奶黑球：被点名的人连线矩形固定 safeColor。
        DrawRectFromOwnerToTarget(sa,
            name: $"通用机制-回归重波动-{orbId:X}-{playerId:X}",
            ownerId: orbId,
            targetId: playerId,
            width: 6f,
            length: 15f,
            duration: 9500,
            color: sa.Data.DefaultSafeColor);
    }

    [ScriptMethod(name: "通用机制-回归波动(双奶妈黑球)", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(02BD)$"])]
    public async void 通用机制_回归波动(Event evt, ScriptAccessory sa)
    {
        var myIdx = sa.MyIndex();
        var targetId = evt.TargetId();
        var targetIdx = sa.Data.PartyList.IndexOf(targetId);
        var orbId = evt.SourceId();

        if (orbId == 0) return;
        if (!IsValidPartyIndex(targetIdx)) return;

        // 按你的 comment：被点名 index 为奇数时 1/3/5/7 safe；为偶数时 0/2/4/6 safe。
        var isSafeForMe = (myIdx % 2) == (targetIdx % 2);
        DrawRectFromOwnerToTarget(sa,
            name: $"通用机制-回归波动-{targetIdx}",
            ownerId: orbId,
            targetId: targetId,
            width: 6f,
            length: 15f,
            duration: 9500,
            color: isSafeForMe ? sa.Data.DefaultSafeColor : sa.Data.DefaultDangerColor);

    }

    [ScriptMethod(name: "通用机制-集束波动(双奶扇形分摊)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(50033)$"])]
    public async void 通用机制_集束波动(Event evt, ScriptAccessory sa)
    {
        var sourceId = evt.SourceId();
        if (sourceId == 0) return;

        var myIdx = sa.MyIndex();
        if (!IsValidPartyIndex(myIdx)) return;

        const uint duration = 4800;
        const float range = 60f;
        const float radian = MathF.PI * 10f / 18f; // 110°

        // index 2 左边，index 3 右边。偶数组 0/2/4/6 吃 index 2，奇数组 1/3/5/7 吃 index 3。
        DrawFanFromOwnerToPartyIndex(sa, "通用机制-集束波动-index2", sourceId, 2, range, radian, duration,
            IsEvenGroup(myIdx) ? sa.Data.DefaultSafeColor : sa.Data.DefaultDangerColor);
        DrawFanFromOwnerToPartyIndex(sa, "通用机制-集束波动-index3", sourceId, 3, range, radian, duration,
            IsOddGroup(myIdx) ? sa.Data.DefaultSafeColor : sa.Data.DefaultDangerColor);

        if (myIdx == 2)
        {
            var dp = sa.WaypointDp(new Vector3(88f, 0f, 100f), duration, 0, "集束波动-index2-左边");
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }
        else if (myIdx == 3)
        {
            var dp = sa.WaypointDp(new Vector3(112f, 0f, 100f), duration, 0, "集束波动-index3-右边");
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }
    }

    [ScriptMethod(name: "通用机制-扩散波动(两两扇形分摊)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(50032)$"])]
    public async void 通用机制_扩散波动(Event evt, ScriptAccessory sa)
    {
        var sourceId = evt.SourceId();
        if (sourceId == 0) return;

        var myIdx = sa.MyIndex();
        if (!IsValidPartyIndex(myIdx)) return;

        const uint duration = 4800;
        const float range = 60f;
        const float radian = MathF.PI / 3f;

        var safeFanIndex = myIdx switch
        {
            0 or 6 => 0,
            1 or 5 => 1,
            2 or 4 => 2,
            3 or 7 => 3,
            _ => -1
        };

        for (var targetIdx = 0; targetIdx < 4; targetIdx++)
        {
            DrawFanFromOwnerToPartyIndex(sa,
                name: $"通用机制-扩散波动-index{targetIdx}",
                ownerId: sourceId,
                targetPartyIndex: targetIdx,
                range: range,
                radian: radian,
                duration: duration,
                color: targetIdx == safeFanIndex
                    ? sa.Data.DefaultSafeColor
                    : sa.Data.DefaultDangerColor);
        }
    }

    [ScriptMethod(name: "通用机制-混沌激流(黑球转转乐)", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(19909|19910)$"])]
    public async void 通用机制_混沌激流(Event evt, ScriptAccessory sa)
    {
        var pos = evt.SourcePosition();
        var sourceId = evt.SourceId();

        var dataId = evt.DataId();

        bool firstOfRound = false;
        int countAfter;
        lock (_commonMechanicLock)
        {
            if (dataId == 19909)
            {
                if (混沌激流黑球生成位置.Count == 0)
                    {
                        混沌激流_0196tethers.Clear();
                        混沌激流_0197tethers.Clear();
                        混沌激流_0196已分配 = false;
                        混沌激流_0197已分配 = false;
                        撞球易伤上次提示时间 = DateTime.MinValue;
                    }

                混沌激流黑球生成位置.Add((pos, sourceId));
                countAfter = 混沌激流黑球生成位置.Count;
                firstOfRound = countAfter == 1;
            }
            
        }

        if (firstOfRound)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(30000);
                lock (_commonMechanicLock)
                {
                    混沌激流黑球生成位置.Clear();
                    混沌激流_0196tethers.Clear();
                    混沌激流_0197tethers.Clear();
                    混沌激流_0196已分配 = false;
                    混沌激流_0197已分配 = false;
                    撞球易伤上次提示时间 = DateTime.MinValue;
                }
            });
        }

        DrawFanFromCenterToPosition(
            sa,
            name: $"通用机制-混沌激流-黑球扇形-{sourceId:X}",
            center: new Vector3(100f, 0f, 100f),
            targetPos: pos,
            degree: 45f,
            radius: 30f,
            duration: 7000,
            color: sa.Data.DefaultDangerColor);
    }

    [ScriptMethod(name: "通用机制-混沌激流-撞球", eventType: EventTypeEnum.Tether, eventCondition: ["Id:regex:^(0196|0197)$"])]
    public async void 通用机制_混沌激流_撞球(Event evt, ScriptAccessory sa)
    {
        var myIdx = sa.MyIndex();
        var srcId = evt.SourceId();
        var tgtId = evt.TargetId();
        var sourcePos = evt.SourcePosition();
        var rawId = (evt["Id"] ?? string.Empty);

        if (!IsValidPartyIndex(myIdx))
        {
            return;
        }

        bool is0196;
        var cleanId = rawId.Replace("0x", "").Trim();
        if (cleanId.Equals("0196", StringComparison.OrdinalIgnoreCase)) is0196 = true;
        else if (cleanId.Equals("0197", StringComparison.OrdinalIgnoreCase)) is0196 = false;
        else
        {
            return;
        }

        Vector3 myTargetPos = default;
        uint myTargetObjectId = 0;
        bool shouldDraw = false;
        int myTypeOrder = myIdx / 2;
        int globalOrder = -1;
        float minDist = float.MaxValue;
        int listCountAfterAdd = 0;
        bool deduped = false;
        bool alreadyAssigned = false;

        lock (_commonMechanicLock)
        {
            if (混沌激流黑球生成位置.Count == 0)
            {
                return;
            }

            // 打印当前黑球list
            var ballListStr = string.Join(" | ", 混沌激流黑球生成位置.Select((p, i) => $"[{i}]({p.vector3.X:F1},{p.vector3.Z:F1})"));

            for (int i = 0; i < 混沌激流黑球生成位置.Count; i++)
            {
                var dist = Vector3.Distance(sourcePos, 混沌激流黑球生成位置[i].vector3);
                if (dist < minDist)
                {
                    minDist = dist;
                    globalOrder = i;
                }
            }

            if (globalOrder < 0) return;
            var matchedBall = 混沌激流黑球生成位置[globalOrder];
            var matchedPos = matchedBall.vector3;
            var matchedSourceId = matchedBall.sourceId;

            var list = is0196 ? 混沌激流_0196tethers : 混沌激流_0197tethers;

            if (list.Any(t => t.GlobalOrder == globalOrder))
            {
                deduped = true;
                return;
            }

            list.Add((globalOrder, matchedPos, matchedSourceId));
            listCountAfterAdd = list.Count;

            var listStr = string.Join(",", list.Select(t => t.GlobalOrder));

            if (list.Count < 4)
            {
                return;
            }

            alreadyAssigned = is0196 ? 混沌激流_0196已分配 : 混沌激流_0197已分配;
            if (alreadyAssigned)
            {
                return;
            }

            var 标准顺序 = 获取混沌激流标准顺时针顺序(sa);

            var rankMap = 标准顺序
                .Select((globalOrder, rank) => new { globalOrder, rank })
                .ToDictionary(x => x.globalOrder, x => x.rank);

            var sorted = list
                .OrderBy(t => rankMap.TryGetValue(t.GlobalOrder, out var rank) ? rank : 999)
                .ToList();

            var sortedStr = string.Join(",", sorted.Select((t, i) =>
                $"#{i}=global{t.GlobalOrder}({t.Pos.X:F1},{t.Pos.Z:F1})"));

            if (myTypeOrder >= sorted.Count)
            {
                return;
            }
            var myTarget = sorted[myTypeOrder];

            myTargetPos = myTarget.Pos;
            myTargetObjectId = myTarget.SourceId;

            shouldDraw = myTargetObjectId != 0;

            if (is0196) 混沌激流_0196已分配 = true;
            else 混沌激流_0197已分配 = true;
        }

        if (!shouldDraw) return;

        uint delay = is0196 ? 6000u : 2000u;
        uint duration = is0196 ? 6000u : 4000u;

        var dp = sa.WaypointToObjectDp(
            myTargetObjectId,
            duration,
            delay,
            $"混沌激流-撞球-{(is0196 ? "0196" : "0197")}-{myTypeOrder}-{myTargetObjectId:X}"
        );

        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    [ScriptMethod(name: "通用机制-奔流", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(49995|49996|49997)$"])]
    public async void 通用机制_奔流(Event evt, ScriptAccessory sa)
    {
        var pos = evt.EffectPosition();

        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = $"通用机制-奔流-{pos.X:F1}-{pos.Z:F1}-{evt.SourceId():X}";
        dp.Position = pos;
        dp.Scale = new Vector2(7f);
        dp.Color = sa.Data.DefaultDangerColor;
        dp.DestoryAt = 6000;
        dp.ScaleMode = ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "通用机制-撞球易伤提示", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(2941)$"])]
    public async void 通用机制_撞球易伤提示(Event evt, ScriptAccessory sa)
    {
        int durationMilliseconds = 4450;
        var targetId = evt.TargetId();
        if (targetId !=sa.Data.Me) return;

        if ((DateTime.UtcNow - 撞球易伤上次提示时间).TotalSeconds < 30)
        {
            return;
        }

        var sourceId = evt.SourceId();
        if (sourceId == 0) return;

        var sourceObj = sa.Data.Objects.SearchByEntityId(sourceId);
        if (sourceObj == null) return;

        var dataId = sourceObj.DataId;

        // 只处理混沌激流黑球
        if (dataId != 19909 && dataId != 19910)
        {
            return;
        }

        撞球易伤上次提示时间 = DateTime.UtcNow;

        if (热病冷却提示 == 热病提示enum.横幅)
        {
            sa.Method.TextInfo("等这行提示消失再去撞球", durationMilliseconds, true);
        }
        else if (热病冷却提示 == 热病提示enum.默语)
        {
            sa.Method.SendChat("/e 等提示消失再去撞球");
        }
        else if (热病冷却提示 == 热病提示enum.TTS)
        {
            sa.Method.TTS("等提示消失再去撞球");
        }
    }

    #region 通用机制 helpers
    private static bool IsValidPartyIndex(int idx) => idx >= 0 && idx <= 7;
    private static bool IsEvenGroup(int idx) => idx is 0 or 2 or 4 or 6;
    private static bool IsOddGroup(int idx) => idx is 1 or 3 or 5 or 7;

    private void DrawCircleOwner(ScriptAccessory sa, string name, uint ownerId, float radius, uint duration, Vector4 color, uint delay = 0)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Owner = ownerId;
        dp.Scale = new Vector2(radius);
        dp.Color = color;
        dp.Delay = delay;
        dp.DestoryAt = duration;
        dp.ScaleMode = ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    private void DrawDonutOwner(ScriptAccessory sa, string name, uint ownerId, float outerRadius, float innerRadius, uint duration, Vector4 color, uint delay = 0)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Owner = ownerId;
        dp.Scale = new Vector2(outerRadius);
        dp.InnerScale = new Vector2(innerRadius);
        dp.Radian = MathF.PI * 2f;
        dp.Color = color;
        dp.Delay = delay;
        dp.DestoryAt = duration;
        // dp.ScaleMode = ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
    }

    private void DrawRectFromOwnerToTarget(ScriptAccessory sa, string name, uint ownerId, uint targetId, float width, float length, uint duration, Vector4 color, uint delay = 0)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Owner = ownerId;
        dp.Scale = new Vector2(width, length);
        dp.Color = color;
        dp.Delay = delay;
        dp.DestoryAt = duration;
        dp.ScaleMode = ScaleMode.ByTime;
        SetDrawTargetObject(dp, targetId);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    private void DrawFanFromOwnerToPartyIndex(ScriptAccessory sa, string name, uint ownerId, int targetPartyIndex, float range, float radian, uint duration, Vector4 color, uint delay = 0)
    {
        if (!IsValidPartyIndex(targetPartyIndex)) return;
        var targetId = sa.Data.PartyList[targetPartyIndex];
        if (targetId == 0) return;

        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Owner = ownerId;
        dp.Scale = new Vector2(range);
        dp.Radian = radian;
        dp.Color = color;
        dp.Delay = delay;
        dp.DestoryAt = duration;
        dp.ScaleMode = ScaleMode.ByTime;
        SetDrawTargetObject(dp, targetId);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    private void DrawFanFromCenterToPosition(
        ScriptAccessory sa,
        string name,
        Vector3 center,
        Vector3 targetPos,
        float degree,
        float radius,
        uint duration,
        Vector4 color)
    {
        var dp = sa.Data.GetDefaultDrawProperties();

        dp.Name = name;
        dp.Owner = 0;
        dp.Position = center;
        dp.Rotation = GetRadian(center, targetPos);
        dp.Radian = degree * MathF.PI / 180f;
        dp.Scale = new Vector2(radius);
        dp.Color = color;
        dp.DestoryAt = duration;
        dp.ScaleMode = ScaleMode.ByTime;

        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    private void DrawFanFromPositionToTarget(
        ScriptAccessory sa,
        string name,
        Vector3 fromPos,
        uint targetId,
        float range,
        float radian,
        uint duration,
        Vector4 color,
        uint delay = 0)
    {
        if (targetId == 0) return;

        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Owner = 0;
        dp.Position = fromPos;
        dp.Scale = new Vector2(range);
        dp.Radian = radian;
        dp.Color = color;
        dp.Delay = delay;
        dp.DestoryAt = duration;
        dp.ScaleMode = ScaleMode.ByTime;

        SetDrawTargetObject(dp, targetId);

        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    private float GetRadian(Vector3 from, Vector3 to)
    {
        var dx = to.X - from.X;
        var dz = to.Z - from.Z;

        return MathF.Atan2(dx, dz);
    }

    private bool TryGetTetherOrbAndPlayer(Event evt, ScriptAccessory sa, out uint orbId, out uint playerId, out int playerIndex)
    {
        var sourceId = evt.SourceId();
        var targetId = evt.TargetId();
        var sourceIndex = sa.Data.PartyList.IndexOf(sourceId);
        var targetIndex = sa.Data.PartyList.IndexOf(targetId);

        if (IsValidPartyIndex(sourceIndex))
        {
            playerId = sourceId;
            playerIndex = sourceIndex;
            orbId = targetId;
            return orbId != 0 && playerId != 0;
        }

        if (IsValidPartyIndex(targetIndex))
        {
            playerId = targetId;
            playerIndex = targetIndex;
            orbId = sourceId;
            return orbId != 0 && playerId != 0;
        }

        orbId = sourceId;
        playerId = targetId;
        playerIndex = targetIndex;
        return orbId != 0 && playerId != 0;
    }

    private void RecordBlackBallTether(uint orbId, uint playerId)
    {
        lock (_commonMechanicLock)
        {
            var now = DateTime.UtcNow;
            _blackBallTethers.RemoveAll(x => (now - x.Time).TotalMilliseconds > 15000 || x.PlayerId == playerId);
            _blackBallTethers.Add((orbId, playerId, now));
        }
    }

    private static void SetDrawTargetObject(DrawPropertiesEdit dp, uint targetId)
    {
        if (TrySetDrawProperty(dp, "TargetObject", targetId)) return;
        if (TrySetDrawProperty(dp, "TargetId", targetId)) return;
        TrySetDrawProperty(dp, "TargetID", targetId);
    }

    private static bool TrySetDrawProperty<T>(DrawPropertiesEdit dp, string propertyName, T value)
    {
        try
        {
            var prop = typeof(DrawPropertiesEdit).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (prop == null || !prop.CanWrite) return false;

            var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            object converted = value;
            if (targetType != typeof(T))
            {
                converted = Convert.ChangeType(value, targetType);
            }
            prop.SetValue(dp, converted);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private List<int> 获取混沌激流标准顺时针顺序(ScriptAccessory sa)
    {
        // 默认 AddCombatant 顺序
        var normal = Enumerable.Range(0, 混沌激流黑球生成位置.Count).ToList();

        if (混沌激流黑球生成位置.Count < 8)
        {
            return normal;
        }

        var center = new Vector3(100f, 0f, 100f);
        float totalDelta = 0f;

        for (int i = 0; i < 8; i++)
        {
            var p1 = 混沌激流黑球生成位置[i].vector3;
            var p2 = 混沌激流黑球生成位置[(i + 1) % 8].vector3;

            var a1 = MathF.Atan2(p1.Z - center.Z, p1.X - center.X);
            var a2 = MathF.Atan2(p2.Z - center.Z, p2.X - center.X);

            var delta = NormalizeRadian(a2 - a1);
            totalDelta += delta;
        }

        // 这里用 totalDelta < 0 当作 0->1->2->... 是顺时针。
        // 如果你实测发现完全反了，就把这里改成 totalDelta > 0。
        var isClockwise = totalDelta > 0f;

        if (isClockwise)
        {
            // 0 1 2 3 4 5 6 7
            return new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 };
        }

        // 0 1 2 3 4 5 6 7 -> 1 0 7 6 5 4 3 2
        return new List<int> { 1, 0, 7, 6, 5, 4, 3, 2 };
    }

    private static float NormalizeRadian(float rad)
    {
        while (rad > MathF.PI) rad -= MathF.PI * 2f;
        while (rad < -MathF.PI) rad += MathF.PI * 2f;
        return rad;
    }
    private void P2_尝试分配无之涡流(ScriptAccessory sa)
    {
        var myIdx = sa.MyIndex();
        if (!IsValidPartyIndex(myIdx)) return;

        Vector3 targetPos = default;
        bool shouldDraw = false;
        bool isMyMarked = false;

        lock (P2无之涡流锁)
        {
            if (P2无之涡流已分配) return;
            if (P2无之涡流点名玩家.Count < 4) return;
            if (P2无之涡流塔数量 < 4) return;

            var towers = Enumerable.Range(0, 8)
                .Where(i => P2无之涡流塔出现[i])
                .OrderBy(i => i)
                .ToList();

            var empties = Enumerable.Range(0, 8)
                .Where(i => !P2无之涡流塔出现[i])
                .OrderBy(i => i)
                .ToList();

            var marked = P2无之涡流点名玩家
                .Distinct()
                .OrderBy(i => i)
                .ToList();

            var unmarked = Enumerable.Range(0, 8)
                .Where(i => !marked.Contains(i))
                .OrderBy(i => i)
                .ToList();

            isMyMarked = marked.Contains(myIdx);

            int assignedSpot = -1;

            if (P2打法 == P2打法法enum.MMW)
            {
                assignedSpot = P2_无之涡流_MMW分配(myIdx, marked, unmarked, towers, empties);
            }
            else
            {
                assignedSpot = P2_无之涡流_NOCCHH分配(myIdx, marked, unmarked, towers, empties);
            }

            if (assignedSpot < 0 || assignedSpot > 7) return;

            targetPos = P2_无之涡流固定点位[assignedSpot];

            if (P2无之涡流塔出现[assignedSpot])
            {
                targetPos = P2无之涡流塔位置[assignedSpot];
            }

            P2无之涡流已分配 = true;
            shouldDraw = true;
        }

        if (!shouldDraw) return;

        var drawTargetPos = targetPos;

        if (isMyMarked)
        {
            var center = new Vector3(100f, 0f, 100f);
            var dirToCenter = center - targetPos;

            if (dirToCenter.LengthSquared() > 0.001f)
            {
                drawTargetPos = targetPos + Vector3.Normalize(dirToCenter) * 10f;
            }
        }

        var dp = sa.WaypointDp(
            drawTargetPos,
            duration: 7000,
            delay: 0,
            name: $"P2-无之涡流-指路-{myIdx}"
        );
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

        if (!isMyMarked)
        {
            var circle = sa.FastDp(
                name: $"P2-无之涡流-目标点-{myIdx}",
                pos: drawTargetPos,
                duration: 7000,
                scale: new Vector2(6f),
                safe: true
            );
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, circle);
        }
        
        _ = Task.Run(async () =>
        {
            await Task.Delay(10000);
            P2_重置无之涡流();
        });
    }

    private int P2_无之涡流_MMW分配(
        int myIdx,
        List<int> marked,
        List<int> unmarked,
        List<int> towers,
        List<int> empties)
    {
        // 没点名的人去塔
        if (unmarked.Contains(myIdx))
        {
            var rank = unmarked.IndexOf(myIdx);
            if (rank < 0 || rank >= towers.Count) return -1;
            return towers[rank];
        }

        // 被点名的人去空地
        if (marked.Contains(myIdx))
        {
            var rank = marked.IndexOf(myIdx);
            if (rank < 0 || rank >= empties.Count) return -1;
            return empties[rank];
        }

        return -1;
    }

    private int P2_无之涡流_NOCCHH分配(
        int myIdx,
        List<int> marked,
        List<int> unmarked,
        List<int> towers,
        List<int> empties)
    {
        var isMarked = marked.Contains(myIdx);
        var isUnmarked = unmarked.Contains(myIdx);
        if (!isMarked && !isUnmarked) return -1;

        var isEven = IsEvenGroup(myIdx);

        var playerGroup = isMarked
            ? marked.Where(i => IsEvenGroup(i) == isEven).OrderBy(i => i).ToList()
            : unmarked.Where(i => IsEvenGroup(i) == isEven).OrderBy(i => i).ToList();

        var rank = playerGroup.IndexOf(myIdx);
        if (rank < 0) return -1;

        var sourceSpots = isMarked ? empties : towers;

        List<int> targetSpots;

        if (isEven)
        {
            // 0/2/4/6 去左半场：index 4-7，按 index 从大到小
            targetSpots = sourceSpots
                .Where(i => i >= 4 && i <= 7)
                .OrderByDescending(i => i)
                .ToList();
        }
        else
        {
            // 1/3/5/7 去右半场：index 0-3，按 index 从小到大
            targetSpots = sourceSpots
                .Where(i => i >= 0 && i <= 3)
                .OrderBy(i => i)
                .ToList();
        }

        if (rank >= targetSpots.Count)
        {
            return P2_无之涡流_MMW分配(myIdx, marked, unmarked, towers, empties);
        }

        return targetSpots[rank];
    }

    private void P2_重置无之涡流()
    {
        lock (P2无之涡流锁)
        {
            P2无之涡流点名玩家.Clear();

            for (int i = 0; i < 8; i++)
            {
                P2无之涡流塔出现[i] = false;
                P2无之涡流塔位置[i] = default;
            }

            P2无之涡流塔数量 = 0;
            P2无之涡流已分配 = false;
        }

        P2无之涡流BossId = 0;
    }

    #endregion

    #endregion

    #region P1
    [ScriptMethod(name: "P1-核心熔毁集合分散", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(50040)$"])]
    public async void P1_核心熔毁(Event evt, ScriptAccessory sa)
    {
        var targetId = evt.TargetId();
        // if (targetId !=sa.Data.Me) return;
        // sa.Method.SendChat($"/e {targetId}");
        // sa.Method.SendChat($"/e {sa.Data.Me}");

        uint durationMilliseconds = 5200;

        if (durationMilliseconds<=0||durationMilliseconds>=7200000)
        {
            sa.Method.SendChat("/e DurationMilliseconds2");
            return;
        }

        await Task.Delay(5000);
        P1_热病buff提醒(evt, sa);

        
        // 八方分散指路+画图+时间

        await Task.Delay(2500);
        for (int i = 0; i < sa.Data.PartyList.Count; i++)
        {
            var playerId = sa.Data.PartyList[i];

            var dpFoot = sa.Data.GetDefaultDrawProperties();
            dpFoot.Name = $"核心熔毁-脚下黄圈-{i}-{playerId:X}";
            dpFoot.Owner = playerId;
            dpFoot.Color = sa.Data.DefaultDangerColor;
            dpFoot.DestoryAt = 3300;
            dpFoot.ScaleMode = ScaleMode.ByTime;
            dpFoot.Scale = new Vector2(5f);

            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dpFoot);
        }
        for (int i = 0; i < sa.Data.PartyList.Count; i++)
        {
            var playerId = sa.Data.PartyList[i];

            var obj = sa.Data.Objects.SearchById(playerId);
            if (obj == null) continue;

            var pos = obj.Position;

            var dpFoot = sa.Data.GetDefaultDrawProperties();
            dpFoot.Name = $"核心熔毁-脚下定点黄圈-{i}-{playerId:X}";
            dpFoot.Position = pos;                  // 定点位置
            dpFoot.Color = sa.Data.DefaultDangerColor;
            dpFoot.DestoryAt = 2300;
            dpFoot.ScaleMode = ScaleMode.ByTime;
            dpFoot.Scale = new Vector2(5f);

            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dpFoot);
        }
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = $"核心熔毁-八方分散-{targetId:X}";
        dp.Owner = targetId;
        dp.Color = sa.Data.DefaultDangerColor;
        dp.DestoryAt = 3300;
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Scale = new Vector2(5);
        
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        var myIdx = sa.MyIndex();
        if (myIdx < 0 || myIdx > 7) return;

        Vector3 wpos2 = myIdx switch
        {
            0 => new Vector3(100f, 0.00f, 88f),
            1 => new Vector3(100f, 0.00f, 112f),
            2 => new Vector3(88f, 0.00f, 100f),
            3 => new Vector3(112f, 0.00f, 100f),
            4 => new Vector3(91.515f, 0.00f, 108.485f),  // 左下
            5 => new Vector3(108.485f, 0.00f, 108.485f), // 右下
            6 => new Vector3(91.515f, 0.00f, 91.515f),   // 左上
            7 => new Vector3(108.485f, 0.00f, 91.515f),  // 右上
        };

        var dp2 = sa.WaypointDp(wpos2, 3300, 0, $"八方分散指路-{myIdx}");
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp2);


    }

    private async void P1_热病buff提醒(Event evt, ScriptAccessory sa)
    {
        int durationMilliseconds = 1509;

        if (durationMilliseconds<=0||durationMilliseconds>=7200000)
        {
            return;
        }

        if (热病冷却提示 == 热病提示enum.横幅)
        {
            sa.Method.TextInfo("停止移动,直到这行提示消失", durationMilliseconds,true);
        }
        else if (热病冷却提示 == 热病提示enum.默语)
        {
            sa.Method.SendChat("/e 停止移动");
        }
        else if (热病冷却提示 == 热病提示enum.TTS)
        {
            sa.Method.TTS("停止移动");
        }
    }

    [ScriptMethod(name: "P1-深度冻结(双T核爆)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(50044)$"])]
    public async void P1_深度冻结(Event evt, ScriptAccessory sa)
    {
        if (_phase != 1) return;

        var myIdx = sa.MyIndex();
        if (!IsValidPartyIndex(myIdx)) return;

        const uint duration = 6000;

        if (myIdx == 0)
        {
            var dp = sa.WaypointDp(new Vector3(88f, 0f, 88f), duration, 0, "深度冻结-MT-左上");
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }
        else if (myIdx == 1)
        {
            var dp = sa.WaypointDp(new Vector3(112f, 0f, 88f), duration, 0, "深度冻结-ST-右上");
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }

        await Task.Delay(5000);

        if (!P1_尝试设置深度冻结提醒())
            return;

        P1_冷却buff提醒(sa);

        await Task.Delay(10000);
        P1_重置深度冻结提醒();
    }
    private bool P1_尝试设置深度冻结提醒()
    {
        lock (P1深度冻结锁)
        {
            if (P1深度冻结冷却已提醒) return false;

            P1深度冻结冷却已提醒 = true;
            return true;
        }
    }

    private void P1_重置深度冻结提醒()
    {
        lock (P1深度冻结锁)
        {
            P1深度冻结冷却已提醒 = false;
        }
    }
    private void P1_冷却buff提醒(ScriptAccessory sa)
    {
        int durationMilliseconds = 2509;

        if (热病冷却提示 == 热病提示enum.横幅)
        {
            sa.Method.TextInfo("持续移动,直到这行提示消失", durationMilliseconds, true);
        }
        else if (热病冷却提示 == 热病提示enum.默语)
        {
            sa.Method.SendChat("/e 持续移动");
        }
        else if (热病冷却提示 == 热病提示enum.TTS)
        {
            sa.Method.TTS("持续移动");
        }
    }

    #endregion

    #region P2
    [ScriptMethod(name: "P2-无之涡流-Boss记录", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(19911)$"], userControl: false)]
    public void P2_无之涡流_Boss记录(Event evt, ScriptAccessory sa)
    {
        if (_phase != 2) return;

        var sourceId = evt.SourceId();
        if (sourceId == 0) return;

        P2无之涡流BossId = sourceId;
    }
    [ScriptMethod(name: "P2转阶段", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(50010)$"], userControl: false)]
    public async void P2转阶段(Event evt, ScriptAccessory sa)
    {
        // 无之领域
        _phase = 2;
    }

    [ScriptMethod(name: "P2-虚无大冲击自动防击退", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(49369)$"])]
    public async void 虚无大冲击自动防击退(Event evt, ScriptAccessory sa)
    {
        await Task.Delay(1000);

        sa.Method.SendChat("/ac 沉稳咏唱");
        sa.Method.SendChat("/ac 亲疏自行");
    }

    [ScriptMethod(name: "P2-无之涡流-点名记录", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(02D1)$"])]
    public async void P2_无之涡流_点名记录(Event evt, ScriptAccessory sa)
    {
        if (_phase != 2) return;

        var targetId = evt.TargetId();   // 被点名玩家
        var targetIdx = sa.Data.PartyList.IndexOf(targetId);
        if (!IsValidPartyIndex(targetIdx)) return;

        lock (P2无之涡流锁)
        {
            if (!P2无之涡流点名玩家.Contains(targetIdx))
            {
                P2无之涡流点名玩家.Add(targetIdx);
            }
        }

        // 给所有被点名的人画一个 60° 危险扇形（从 boss 到人）
        // 60° = PI / 3
        DrawFanFromPositionToTarget(
            sa,
            name: $"P2-无之涡流-点名扇形-{targetIdx}",
            fromPos: new Vector3(100f, 0f, 100f),
            targetId: targetId,
            range: 60f,
            radian: MathF.PI / 3f,
            duration: 8000,
            color: sa.Data.DefaultDangerColor
        );

        P2_尝试分配无之涡流(sa);
    }

    [ScriptMethod(name: "P2-无之涡流(扇形踩塔)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(50013)$"])]
    public async void P2_无之涡流(Event evt, ScriptAccessory sa)
    {
        if (_phase != 2) return;

        await Task.Delay(200);

        var pos = evt.SourcePosition();
        var towerIndex = P2_获取无之涡流塔Index(pos);
        if (towerIndex < 0) return;

        lock (P2无之涡流锁)
        {
            if (P2无之涡流塔出现[towerIndex])
            {
                return;
            }

            P2无之涡流塔出现[towerIndex] = true;
            P2无之涡流塔位置[towerIndex] = pos;
            P2无之涡流塔数量++;
        }

        P2_尝试分配无之涡流(sa);
    }

    #endregion

    #region P3
    [ScriptMethod(name: "P3转场", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(50029)$"], userControl: false)]
    public async void P3转场(Event evt, ScriptAccessory sa)
    {
        // 无光的世界
        _phase = 3;
    }

    [ScriptMethod(name: "P3-聚能波动", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(49985|49986)$"], userControl: false)]
    public async void P3_聚能波动(Event evt, ScriptAccessory sa)
    {
        if (_phase != 3) return;

        var actionId = evt.ActionId();
        var sourceId = evt.SourceId();
        if (sourceId == 0) return;

        var pos = evt.SourcePosition();
        var rot = evt.SourceRotation();

        if (actionId == 49985)
        {
            // 49985 矩形
            var dp = sa.Data.GetDefaultDrawProperties();
            dp.Name = $"P3-聚能波动-矩形-{sourceId:X}";
            dp.Owner = 0;
            dp.Position = pos;
            dp.Rotation = rot;
            dp.Scale = new Vector2(16f, 80f);
            dp.Color = sa.Data.DefaultDangerColor;
            dp.DestoryAt = 6300;
            dp.ScaleMode = ScaleMode.ByTime;

            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
        else if (actionId == 49986)
        {
            // 49986 辣翅
            var dp = sa.Data.GetDefaultDrawProperties();
            dp.Name = $"P3-聚能波动-辣翅-{sourceId:X}";
            dp.Owner = sourceId;
            dp.Scale = new Vector2(16f, 80f);
            dp.Offset = new Vector3(-16f, 0f, 0f);
            dp.Color = sa.Data.DefaultDangerColor;
            dp.DestoryAt = 5700;
            dp.ScaleMode = ScaleMode.ByTime;

            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

            var dp2 = sa.Data.GetDefaultDrawProperties();
            dp2.Name = $"P3-聚能波动-辣翅-{sourceId:X}";
            dp2.Owner = sourceId;
            dp2.Scale = new Vector2(16f, 800f);
            dp2.Offset = new Vector3(16f, 0f, 0f);
            dp2.Color = sa.Data.DefaultDangerColor;
            dp2.DestoryAt = 5700;
            dp2.ScaleMode = ScaleMode.ByTime;

            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp2);
        }
    }

    [ScriptMethod(name: "P3-暗影神圣(双奶分摊)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(50045)$"], userControl: false)]
    public async void P3_暗影神圣(Event evt, ScriptAccessory sa)
    {
        if (_phase != 3) return;

        var myIdx = sa.MyIndex();
        if (!IsValidPartyIndex(myIdx)) return;

        const uint duration = 6000;
        const float range = 6f;

        var index2Id = sa.Data.PartyList[2];
        var index3Id = sa.Data.PartyList[3];
        if (index2Id == 0 || index3Id == 0) return;

        // 偶数组安全，奇数组危险
        var dp2 = sa.Data.GetDefaultDrawProperties();
        dp2.Name = "P3-暗影神圣-index2";
        dp2.Owner = index2Id;
        dp2.Scale = new Vector2(range);
        dp2.Color = IsEvenGroup(myIdx) ? sa.Data.DefaultSafeColor : sa.Data.DefaultDangerColor;
        dp2.DestoryAt = duration;
        dp2.ScaleMode = ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp2);

        // 奇数组安全，偶数组危险
        var dp3 = sa.Data.GetDefaultDrawProperties();
        dp3.Name = "P3-暗影神圣-index3";
        dp3.Owner = index3Id;
        dp3.Scale = new Vector2(range);
        dp3.Color = IsOddGroup(myIdx) ? sa.Data.DefaultSafeColor : sa.Data.DefaultDangerColor;
        dp3.DestoryAt = duration;
        dp3.ScaleMode = ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp3);
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
    public static uint DataId(this Event evt) => JsonConvert.DeserializeObject<uint>(evt["DataId"]);
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