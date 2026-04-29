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
    public void Init(ScriptAccessory sa)
    {
        _phase = 1;
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
        // 画图钢铁月环 49977是钢铁(40) 49978是月环(60,10)
    }
    [ScriptMethod(name: "通用机制-回归重波动(单奶妈黑球)", eventType: EventTypeEnum.Tether, eventCondition: ["Id:regex:^(01AE)$"])]
    public async void 通用机制_回归重波动(Event evt, ScriptAccessory sa)
    {
        // 被点名的人画矩形 (以场内为中心，从场外连线的黑球到被点名的人)
        // 用safeColor
    }
    [ScriptMethod(name: "通用机制-回归波动(双奶妈黑球)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(|)$"])]
    public async void 通用机制_回归波动(Event evt, ScriptAccessory sa)
    {
        // 被点名的人画矩形 (从场外连线的黑球到被点名的人)
        // 但是要判断index。
            // 如果被点名的index是1,然后你是1 3 5 7，用safeColor。
            // 如果被点名的index是1,然后你是0 2 4 8，用dangerColor。
            // 如果被点名的index是2,然后你是0 2 4 8，用safeColor
            // 如果被点名的index是2,然后你是1 3 5 7，用dangerColor
    }
    [ScriptMethod(name: "通用机制-集束波动(双奶扇形分摊)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(50033)$"])]
    public async void 通用机制_集束波动(Event evt, ScriptAccessory sa)
    {
        // 两个index 2 3画120扇形 (从boss到两个index)
        // index 2指路左边 3指路右边
        // 其他人
            // 如果是0 2 4 6，index 2的扇形用safeColor，index 1的扇形用dangerColor
            // 如果是1 3 5 7，index 1的扇形用safeColor，index 2的扇形用dangerColor
    }
    [ScriptMethod(name: "通用机制-扩散波动(两两扇形分摊)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(|)$"])]
    public async void 通用机制_扩散波动(Event evt, ScriptAccessory sa)
    {
        // 看 有没有写点名，没有的话就画0 1 2 3，45度扇形
        // 其他人
            // 如果是0 4，index 0 4的扇形用safeColor，其他的扇形用dangerColor
            // 如果是1 5，index 1 5的扇形用safeColor，其他的扇形用dangerColor
            // 如果是2 6，index 2 6的扇形用safeColor，其他的扇形用dangerColor
            // 如果是3 7，index 3 7的扇形用safeColor，其他的扇形用dangerColor
    }
    [ScriptMethod(name: "通用机制-混沌激流(黑球转转乐)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(|)$"])]
    public async void 通用机制_混沌激流(Event evt, ScriptAccessory sa)
    {
        // 画扇形 具体怎么没想好

        // 记录被打两次的黑球的位置

        // 分成四个半场。按照先黄后紫的顺序撞
    }
    [ScriptMethod(name: "通用机制-零次元(多段分摊)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(|)$"], userControl: false)]
    public async void 通用机制_零次元(Event evt, ScriptAccessory sa)
    {
        // 被点的人画一个safecolor的矩形
    }
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