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
    name: "恩欧画图",
    territorys: [1362],
    version: "0.0.0.1",
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
    private readonly List<Vector3> 混沌激流最初两个黑球位置 = new();

    public void Init(ScriptAccessory sa)
    {
        _phase = 1;
        lock (_commonMechanicLock)
        {
            _blackBallTethers.Clear();
        }
        sa.Method.RemoveDraw(".*");
    }

    #endregion

    #region 阶段
    private double _phase = 1;

    [ScriptMethod(name: "Set Phase 1", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASP1"], userControl: false)]
    public void SetP1(Event evt, ScriptAccessory sa) => _phase = 1;

    [ScriptMethod(name: "Set Phase 2", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASP2"], userControl: false)]
    public void SetP2(Event evt, ScriptAccessory sa) => _phase = 2;
    #endregion

    #region 通用机制

    [ScriptMethod(name: "通用机制-无之膨胀", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(49977|49978)$"])]
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
            width: 8f,
            length: 15f,
            duration: 7700,
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

        const uint duration = 6500;
        const float range = 60f;
        const float radian = MathF.PI / 4f; // 45°

        var safeFanIndex = myIdx % 4; // 0/4 -> 0, 1/5 -> 1, 2/6 -> 2, 3/7 -> 3
        for (var targetIdx = 0; targetIdx < 4; targetIdx++)
        {
            DrawFanFromOwnerToPartyIndex(sa,
                name: $"通用机制-扩散波动-index{targetIdx}",
                ownerId: sourceId,
                targetPartyIndex: targetIdx,
                range: range,
                radian: radian,
                duration: duration,
                color: targetIdx == safeFanIndex ? sa.Data.DefaultSafeColor : sa.Data.DefaultDangerColor);
        }
    }

    [ScriptMethod(name: "通用机制-混沌激流(黑球转转乐)", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(19909|19910)$"])]
    public async void 通用机制_混沌激流(Event evt, ScriptAccessory sa)
    {
        var 黑球位置 = evt.SourcePosition();
        var 场地中心 = new Vector3(100f, 0f, 100f);

        // 只记录最先出现的两个黑球位置
        if (混沌激流最初两个黑球位置.Count < 2)
        {
            混沌激流最初两个黑球位置.Add(黑球位置);

            // 第一个黑球出现时，安排一段时间后清空，避免影响下一轮
            if (混沌激流最初两个黑球位置.Count == 1)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(20000);
                    混沌激流最初两个黑球位置.Clear();
                });
            }
        }

        // 画扇形：从场地中心指向当前黑球位置
        DrawFanFromCenterToPosition(
            sa,
            name: $"通用机制-混沌激流-黑球扇形",
            center: 场地中心,
            targetPos: 黑球位置,
            degree: 45f,
            radius: 30f,
            duration: 7000,
            color: sa.Data.DefaultDangerColor
        );

        // 这里不追踪最初两个黑球位置，而是按照时间顺序记录所有黑球出现的位置 index前的先出现。只记录19909的
    }

    [ScriptMethod(name: "通用机制-混沌激流-撞球", eventType: EventTypeEnum.Tether, eventCondition: ["Id:regex:^(0196|0197)$"])]
    public async void 通用机制_混沌激流_撞球(Event evt, ScriptAccessory sa)
    {
        // 按照时间顺序来排， index 0 1去撞  时间顺序出现的黑球的位置中第一个出现的0196和0197,然后0196延迟4000ms，0197延迟2000ms
        // 按照时间顺序来排， index 2 3去撞  时间顺序出现的黑球的位置中第二个出现的0196和0197,然后0196延迟4000ms，0197延迟2000ms
        // 按照时间顺序来排， index 4 5去撞  时间顺序出现的黑球的位置中第三个出现的0196和0197,然后0196延迟4000ms，0197延迟2000ms
        // 按照时间顺序来排， index 6 7去撞  时间顺序出现的黑球的位置中第四个出现的0196和0197,然后0196延迟4000ms，0197延迟2000ms

    }
    
    [ScriptMethod(name: "通用机制-零次元(多段分摊)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(|)$"])]
    public async void 通用机制_零次元(Event evt, ScriptAccessory sa)
    {
        // 被点的人画一个safecolor的矩形
    }

    [ScriptMethod(name: "通用机制-奔流", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(49995|49996|49997)$"])]
    public async void 通用机制_奔流(Event evt, ScriptAccessory sa)
    {
        // 记录EffectPosition. 画一个3200ms的5m的圈
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
        dp.ScaleMode = ScaleMode.ByTime;
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
        // 不同 KodakkuAssist 版本里目标字段名可能不完全一致；用反射避免因为字段名差异直接编译失败。
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
    #endregion

    #endregion

    #region P1
    [ScriptMethod(name: "P1-核心熔毁集合分散", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(50040)$"])]
    public async void P1_核心熔毁(Event evt, ScriptAccessory sa)
    {
        if (_phase != 1) return;
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
        
        // 先指路boss脚底
        var wpos = new Vector3(100f, 0f, 100f);
        sa.WaypointDp(wpos, durationMilliseconds, 0, "核心熔毁集合");
        sa.Method.SendChat("/e Test");

        // 把热病提示移到这里
        await Task.Delay(5000);
        P1_热病buff提醒(evt, sa);

        
        // 八方分散指路+画图+时间

        await Task.Delay(2500);
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

    // [ScriptMethod(name: "P1-热病buff提醒", eventType: EventTypeEnum.StatusAdd, eventCondition:["StatusID:4562"])]
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

    [ScriptMethod(name: "P1-深度冻结(双T核爆)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(|)$"])]
    public async void P1_深度冻结(Event evt, ScriptAccessory sa)
    {
        // index 0指路左上
        // index 1指路右上

    }

    [ScriptMethod(name: "P1-冷却buff提醒", eventType: EventTypeEnum.StatusAdd, eventCondition:["StatusID:4563"])]
    public async void P1_冷却buff提醒(Event evt, ScriptAccessory sa)
    {
        int durationMilliseconds = 0;
        var targetId = evt.TargetId();

        if (targetId!=sa.Data.Me)
        {
            return;
        }
        
        try
        {
            durationMilliseconds=JsonConvert.DeserializeObject<int>(evt["DurationMilliseconds"]);
        }
        catch(Exception e)
        {
            sa.Log.Error("DurationMilliseconds deserialization failed.");
            return;
        }

        if (durationMilliseconds<=0||durationMilliseconds>=7200000)
        {
            return;
        }

        if (热病冷却提示 == 热病提示enum.横幅)
        {
            sa.Method.TextInfo("持续移动,直到这行提示消失",durationMilliseconds,true);
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
    [ScriptMethod(name: "P2转阶段", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(|)$"], userControl: false)]
    public async void P2转阶段(Event evt, ScriptAccessory sa)
    {
        // 无之领域
        _phase = 2;
    }

    [ScriptMethod(name: "P2-虚无大冲击自动防击退", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(|)$"])]
    public async void 虚无大冲击自动防击退(Event evt, ScriptAccessory sa)
    {
        await Task.Delay(1000);

        sa.Method.SendChat("/ac 沉稳咏唱");
        sa.Method.SendChat("/ac 亲疏自行");
    }

    [ScriptMethod(name: "P2-无之涡流(扇形踩塔)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(|)$"])]
    public async void P2_无之涡流(Event evt, ScriptAccessory sa)
    {
        // 这里才initialize list，因为这个机制会重复多遍
        await Task.Delay(200);
        // 场上一共八个固定地方会出现塔，是刚好以场地中心为圆心的一圈的八个点。
        // 以上偏右为第一个塔，依次右偏上，右偏下，下偏右....一直到上偏左记录index 0-7。有塔的为1，没有的是0
        // 会刷新两波每次只会出现四个塔

        // 看谁被点名了，只可能是0 1 2 3被点名或者4 5 6 7被点名
        // 首先是mmw
            // 看塔出现的位置，按照index顺序绘图指路被点名的index最小的去塔index最小的塔，index第二小的去第二index塔，index第三小的去第三index塔，index第四小的去第四index塔。
            // 看塔没有出现的位置，按照index顺序绘图指路没有被点名的index最小的去index最小的空地，index第二小的去第二index空地，index第三小的去第三index空地，index第四小的去第四indexv。

        // 其次是优化
            // 0 2 4 6去左半场
            // 1 3 5 7去右半场
            // 看塔出现的位置，按照index顺序绘图指路
                // 如果是0 2 4 6，被点名的index最小的去塔index(4-7)最大的塔，index第二小的去第二大的index塔(4-7)
                // 如果是1 3 5 7，被点名的index最小的去塔index(0-3)最小的塔，index第二小的去第二小的index塔(0-3)
            // 看塔没有出现的位置，按照index顺序绘图指路
                // 如果是0 2 4 6，被点名的index最小的去塔index(4-7)最大的空地，index第二小的去第二大的index空地(4-7)
                // 如果是1 3 5 7，被点名的index最小的去塔index(0-3)最小的空地，index第二小的去第二小的index空地(0-3)
    }

    #endregion

    #region P3
    [ScriptMethod(name: "P3转场", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(|)$"], userControl: false)]
    public async void P3转场(Event evt, ScriptAccessory sa)
    {
        // 无光的世界
        _phase = 3;
    }

    [ScriptMethod(name: "P3-辣翅", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(|)$"], userControl: false)]
    public async void P3_辣翅(Event evt, ScriptAccessory sa)
    {
        // 这里活性之后的技能，可能不是活性。应该是聚能波动 但要注意限制，发动了好多次
        if (_phase != 3) return;

        // 两组辣翅 得进去看 只用画图
    }

    [ScriptMethod(name: "P3-辣尾", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(|)$"], userControl: false)]
    public async void P3_辣尾(Event evt, ScriptAccessory sa)
    {
        if (_phase != 3) return;

        // 得进去看 只用画图
    }

    [ScriptMethod(name: "P3-暗影神圣(双奶分摊)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(|)$"], userControl: false)]
    public async void P3_暗影神圣(Event evt, ScriptAccessory sa)
    {
        if (_phase != 3) return;

        // 两个index 2 3画range为5的圈
        // 其他人
            // 如果是0 2 4 6，index 2的圈用safeColor，index 1的圈用dangerColor
            // 如果是1 3 5 7，index 1的圈用safeColor，index 2的圈用dangerColor
    }

    [ScriptMethod(name: "P3-核心熔毁集合分散", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(|)$"])]
    public async void P3_核心熔毁(Event evt, ScriptAccessory sa)
    {
        if (_phase != 3) return;
        // 无之漩涡看最短的线的object在哪。
        // 先指路去以object到中心为圆心为面向的前面(10,5)的距离
        
        // 八方分散指路+画图+时间

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