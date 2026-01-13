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
using System.Runtime.CompilerServices;

namespace Codaaaaaa.M9S;

[ScriptType(
    guid: "6f3d1b82-9d44-4c5a-8d77-3a8f5c0f2b1e",
    name: "M11S补充画图",
    territorys: [1325],
    version: "0.0.0.1",
    author: "Codaaaaaa",
    note: "亲友自用，近固只有第一次铸兵之令统治指路，因为只开荒到这里。慎用")]
public class M11S
{
    #region 用户设置
    // [UserSetting("是否开启TTS")] public static bool TTSOpen { get; set; } = true;
    #endregion

    private static readonly Vector3[] DomPoints = new[]
    {
        new Vector3(85f,  0f,  100f),   // 点0
        new Vector3(115f, 0f,  100f),   // 点1
        new Vector3(100f,  0f,  115f),  // 点2
        new Vector3(100f, 0f,  85f),  // 点3
    };

    private static readonly Vector3[][] SafeByMissing = new[]
    {
        // missing = 0 (DomPoints[0] 没出现)
        new[]
        {
            new Vector3(100.14f, 0f, 91.23f), // group0: myIdx 0/4
            new Vector3(99.89f, 0f, 108.96f), // group1: myIdx 1/5
            new Vector3(85.21f, 0f, 94.08f), // group2: myIdx 2/6
            new Vector3(85.49f, 0f, 106.01f), // group3: myIdx 3/7
        },

        // missing = 1 (DomPoints[1] 没出现)
        new[]
        {
            new Vector3(100.14f, 0f, 91.23f), // group0: myIdx 0/4
            new Vector3(99.89f, 0f, 108.96f), // group1: myIdx 1/5
            new Vector3(114.51f, 0f, 106.01f), // group2: myIdx 2/6
            new Vector3(114.51f, 0f, 94.08f), // group3: myIdx 3/7
        },

        // missing = 2 (DomPoints[2] 没出现)
        new[]
        {
            new Vector3(91.25f, 0f, 100.08f),
            new Vector3(108.63f, 0f, 99.68f),
            new Vector3(94.15f, 0f, 114.20f),
            new Vector3(105.70f, 0f, 114.08f),
        },

        // missing = 3 (DomPoints[3] 没出现)
        new[]
        {
            new Vector3(91.25f, 0f, 100.08f),
            new Vector3(108.63f, 0f, 99.68f),
            new Vector3(105.70f, 0f, 85.92f), 
            new Vector3(94.15f, 0f, 85.92f),
        },
    };

    // 匹配固定点的容差（只看 XZ）
    private const float DomPointEps = 1.2f;

    // 收集机制触发的状态（线程安全）
    private readonly object _domLock = new();
    private int _domMask = 0;     // 4bit：出现过哪些点
    private int _domCount = 0;    // 已收到几次（目标 6 次）
    private int _domSeq = 0;      // 每轮机制序号（防止上一轮 Task 影响下一轮）
    private long _domLastTick = 0;

    public void Init(ScriptAccessory sa)
    {
        ResetAll();
        sa.Method.RemoveDraw(".*");
    }

    private void ResetAll()
    {
        lock (_domLock)
        {
            _domMask = 0;
            _domCount = 0;
            _domSeq++;
            _domLastTick = DateTime.UtcNow.Ticks;
        }
    }

    // =========================
    // 统治：6次点名 -> 4点缺1点 -> 指路安全区
    // =========================
    [ScriptMethod(name: "铸兵之令统治指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:46112"])]
    public void 铸兵之令统治指路(Event evt, ScriptAccessory sa)
    {
        if (!int.TryParse(evt["DurationMilliseconds"], out var dur)) return;

        var targetPos = evt.TargetPosition;

        int seqLocal;
        bool startJudgeTask = false;

        lock (_domLock)
        {
            // 超时保护：如果距离上一次 > 4秒，认为新一轮开始
            var nowTicks = DateTime.UtcNow.Ticks;
            var dtMs = (nowTicks - _domLastTick) / TimeSpan.TicksPerMillisecond;
            if (dtMs > 4000)
            {
                _domMask = 0;
                _domCount = 0;
                _domSeq++;
            }
            _domLastTick = nowTicks;

            // 第一发进来就开一个延迟判定任务
            if (_domCount == 0)
                startJudgeTask = true;

            var idx = MatchDomPointIndex(targetPos);
            if (idx >= 0)
                _domMask |= (1 << idx);

            _domCount++;
            seqLocal = _domSeq;
        }

        if (startJudgeTask)
        {
            _ = Task.Run(async () =>
            {
                // 等一小会，给 6 次事件进来
                await Task.Delay(250);

                int mask, cnt, seqNow;
                lock (_domLock)
                {
                    mask = _domMask;
                    cnt = _domCount;
                    seqNow = _domSeq;
                }

                // 如果期间 reset/新一轮了，直接丢弃
                if (seqNow != seqLocal) return;

                if (cnt < 6) return;

                var missing = FindMissingIndexFromMask(mask);
                if (missing < 0 || missing > 3) return;

                var myIdx = sa.MyIndex();
                if (myIdx < 0 || myIdx > 7) return;

                var group = myIdx % 4;
                var safe = SafeByMissing[missing][group];

                DrawWaypointToMe(sa, safe, dur, "Dom_Waypoint");

                // 本轮收尾：清空（也可以不清，让超时逻辑去清）
                lock (_domLock)
                {
                    _domMask = 0;
                    _domCount = 0;
                    _domSeq++;
                }
            });
        }
    }

    private static int MatchDomPointIndex(Vector3 p)
    {
        int best = -1;
        float bestD2 = float.MaxValue;

        for (int i = 0; i < DomPoints.Length; i++)
        {
            var d = p - DomPoints[i];
            var d2 = d.X * d.X + d.Z * d.Z;
            if (d2 < bestD2)
            {
                bestD2 = d2;
                best = i;
            }
        }

        return bestD2 <= DomPointEps * DomPointEps ? best : -1;
    }

    private static int FindMissingIndexFromMask(int mask)
    {
        for (int i = 0; i < 4; i++)
        {
            if ((mask & (1 << i)) == 0) return i;
        }
        return -1;
    }

    private void DrawWaypointToMe(ScriptAccessory sa, Vector3 wpos, int durMs, string name)
    {
        var dpWp = sa.WaypointDp(wpos, (uint)durMs, 0, name);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpWp);
    }

    private void DrawSafeCircle(ScriptAccessory sa, Vector3 pos, int durMs, string name)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Position = pos;
        dp.DestoryAt = durMs;
        dp.Color = sa.Data.DefaultSafeColor;
        dp.Scale = new Vector2(6f);
        dp.ScaleMode = ScaleMode.None;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
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
