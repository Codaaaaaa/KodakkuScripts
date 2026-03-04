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

namespace Codaaaaaa.MerchantsTale;

[ScriptType(
    guid: "a2c7f9d1-4b6e-4f3a-9c18-7d2e6a5b8c41",
    name: "多变迷宫 异闻商客奇谭 指路+画图",
    territorys: [1317],
    version: "0.0.0.1",
    author: "Codaaaaaa",
    note: "攻略使用的是mmw: https://mmw-ffxiv.feishu.cn/wiki/KvdJwQqfziIab3kPBAAcbCYvn5r")]
public class MerchantsTale
{
    #region 用户设置
    [UserSetting("是否开启TTS")] public static bool TTSOpen { get; set; } = true;
    [UserSetting("TTS播报方式")] public static TTS播报方式 TTSMode { get; set; } = TTS播报方式.原生TTS;
    #endregion

    #region 设置enum
    public enum TTS播报方式
    {
        原生TTS,
        EdgeTTS,
        DrTTS
    }
    #endregion

    // 换P
    private int _phase = 1;

    #region 剑术大师param
    // ====== 天界交叉斩：点名记录 ======
    private readonly object _tjLock = new();
    private uint _tjArrowTarget = 0; // 028C
    private uint _tjMj1Target = 0;   // 014C
    private uint _tjMj2Target = 0;   // 014D

    // ====== 天界交叉斩：坐标配置（按你注释里的点） ======
    private static readonly Vector3 TJ_ArrowPos = new(170.04f, -16.00f, -822.50f);

    // 单：没点名
    private static readonly Vector3 TJ_NoBuff_Single = new(162.39f, -16.00f, -814.90f);
    // 双：没点名
    private static readonly Vector3 TJ_NoBuff_Double = new(170.04f, -16.00f, -822.50f);

    // 圆/环：麻将站位
    private static readonly Vector3 TJ_Pos_A = new(170.02f, -16.00f, -807.20f);
    private static readonly Vector3 TJ_Pos_B = new(170.00f, -16.00f, -814.48f);

    private readonly object _xzLock = new();

    // 4785 / 4786 -> 同buff的人（一般4人/组，但我们只需要找“同组另一个人”）
    private readonly Dictionary<uint, List<uint>> _xzStatusGroups = new()
    {
        [4785u] = new List<uint>(),
        [4786u] = new List<uint>(),
    };

    // 灵击波点名的两个人（满足过滤条件后才记录）
    private readonly List<uint> _xzLingjiTargets = new();

    // 画图名，用于 remove
    private const string XZCircleNamePrefix = "XZ_LinkCircle_";

    // 2m 绿色圈
    private static readonly Vector4 XZGreen = new(0f, 1f, 0f, 0.55f);

    #endregion

    // ----------------------------
    // Phase helper
    // ----------------------------
    [ScriptMethod(name: "Set Phase 1", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASP1"], userControl: false)]
    public void SetP1(Event evt, ScriptAccessory sa) => _phase = 1;

    [ScriptMethod(name: "Set Phase 2", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASP2"], userControl: false)]
    public void SetP2(Event evt, ScriptAccessory sa) => _phase = 2;

    [ScriptMethod(name: "Show Phase", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:phase"], userControl: false)]
    public void ShowPhase(Event evt, ScriptAccessory sa) => sa.Method.SendChat($"/e Current Phase: {_phase}");

    public void Init(ScriptAccessory sa)
    {
        ResetAll();
        sa.Method.RemoveDraw(".*");
    }

    private void ResetAll()
    {
        _phase = 1;

        lock (_tjLock)
        {
            _tjArrowTarget = 0;
            _tjMj1Target = 0;
            _tjMj2Target = 0;
        }
        lock (_xzLock)
        {
            _xzStatusGroups[4785u].Clear();
            _xzStatusGroups[4786u].Clear();
            _xzLingjiTargets.Clear();
        }
        ResetP3();
    }
    private readonly object _p3Lock = new();

    // 记录凶兆分组
    private readonly List<uint> _p3SafeEW = new(); // 4784 东西安全
    private readonly List<uint> _p3SafeNS = new(); // 4775 南北安全

    // 记录灵击波 tether 边（source -> target）
    private readonly List<P3TetherEdge> _p3Tethers = new();

    private bool _p3Resolved = false;           // 48653 只跑一次
    private bool _p3GuideIssued = false;        // 46725 只跑一次
    private int _p3Seq = 0;                     // 防串轮
    private long _p3LastMs = 0;

    private readonly Dictionary<uint, P3MemberLink> _p3MemberToBottom = new(); // memberId -> bottom info

    // 固定四点（你给的）
    private static readonly Vector3 P3_Right = new(190.00f, -16.00f, -810.00f);
    private static readonly Vector3 P3_Left  = new(150.00f, -16.00f, -820.00f);
    private static readonly Vector3 P3_Down  = new(165.00f, -16.00f, -795.00f);
    private static readonly Vector3 P3_Up    = new(175.00f, -16.00f, -835.00f);

    private const float P3PointEps = 6.0f;   // 四点匹配容差（按迷宫那种点位，给大一点更稳）
    private const float P3AssignEps = 5.0f;  // “找不到直接连到队员”的兜底：按 5m 内最近

    private enum P3BottomKey { Unknown, Right, Left, Down, Up }

    private readonly record struct P3TetherEdge(
        uint SourceId,
        uint TargetId,
        Vector3 SourcePos,
        Vector3 TargetPos,
        long TimeMs
    );

    private readonly record struct P3MemberLink(
        uint MemberId,
        uint BottomOrbId,
        Vector3 BottomPos,
        P3BottomKey Key
    );

    private void ResetP3()
    {
        lock (_p3Lock)
        {
            _p3Seq++;
            _p3LastMs = 0;

            _p3SafeEW.Clear();
            _p3SafeNS.Clear();

            _p3Tethers.Clear();
            _p3MemberToBottom.Clear();

            _p3Resolved = false;
            _p3GuideIssued = false;
        }
    }

    private static float DistXZ2(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X;
        float dz = a.Z - b.Z;
        return dx * dx + dz * dz;
    }

    private static P3BottomKey MatchBottomKey(Vector3 p)
    {
        // 只看 XZ
        float eps2 = P3PointEps * P3PointEps;
        if (DistXZ2(p, P3_Right) <= eps2) return P3BottomKey.Right;
        if (DistXZ2(p, P3_Left)  <= eps2) return P3BottomKey.Left;
        if (DistXZ2(p, P3_Down)  <= eps2) return P3BottomKey.Down;
        if (DistXZ2(p, P3_Up)    <= eps2) return P3BottomKey.Up;
        return P3BottomKey.Unknown;
    }

    private static bool IsPartyMember(ScriptAccessory sa, uint id)
        => id != 0 && sa.Data.PartyList.Contains(id);

    private static Vector3 GetObjPos(ScriptAccessory sa, uint id)
        => sa.Data.Objects.FirstOrDefault(o => o.GameObjectId == id)?.Position ?? Vector3.Zero;

    private static uint FindAnother(List<uint> list, uint me)
        => list.FirstOrDefault(x => x != 0 && x != me);

    #region 通用
    [ScriptMethod(name: "aoe播报", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(45870|46686)$"])]
    public void BossAoe(Event evt, ScriptAccessory sa)
    {
        sa.tts("超大AOE", TTSMode, TTSOpen);
    }
    private uint GetDataIdByObjectId(ScriptAccessory sa, uint objectId)
    {
        if (objectId == 0) return 0;
        var obj = sa.Data.Objects.FirstOrDefault(o => o.GameObjectId == objectId);
        if (obj == null) return 0;

        // 如果你这边不是 DataId 字段，把这里改成对应字段
        return obj.DataId;
    }
    private void DrawWaypointToMe(ScriptAccessory sa, Vector3 wpos, int durMs, string name)
    {
        var dpWp = sa.WaypointDp(wpos, (uint)durMs, 0, name);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpWp);
    }
    #endregion

    #region 剑术大师

    // 47566: 圆 单 猜的
    // 47567: 环 单
    // 47568: 圆 双
    // 47569: 环 双 猜的
    [ScriptMethod(name: "剑术大师-P1-天界交叉斩", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(47566|47567|47568|47569)$"])]
    public void 剑术大师_P1_天界交叉斩(Event evt, ScriptAccessory sa)
    {
        uint actionId = evt.ActionId();

        bool isCircle = actionId is 47566u or 47568u;
        bool isDouble = actionId is 47568u or 47569u;

        int durMs = 0;
        _ = int.TryParse(evt["DurationMilliseconds"], out durMs);
        if (durMs <= 0) durMs = 6000;

        _ = Task.Run(async () =>
        {
            await Task.Delay(600);

            uint myId = sa.Data.Me;
            // sa.Method.SendChat($"/e Myid: {myId}");

            uint arrow, mj1, mj2;
            lock (_tjLock)
            {
                arrow = _tjArrowTarget;
                mj1 = _tjMj1Target;
                mj2 = _tjMj2Target;
            }
            uint arrowIdLocal = arrow;
            uint mj1IdLocal = mj1;
            uint mj2IdLocal = mj2;

            Vector3 mj1Target = isCircle ? TJ_Pos_A : TJ_Pos_B;
            Vector3 mj2Target = isCircle ? TJ_Pos_B : TJ_Pos_A;

            bool IsCirclePos(Vector3 p) => p == TJ_Pos_A;
            DrawTypeEnum TypeForMj(Vector3 mjTarget) => IsCirclePos(mjTarget) ? DrawTypeEnum.Circle : DrawTypeEnum.Donut;


            // 画图
            void DrawOnTarget(uint targetId, string namePrefix, DrawTypeEnum type, float radius, Vector4 color, int dur)
            {
                if (targetId == 0) return;
                var dp = sa.Data.GetDefaultDrawProperties();
                dp.Name = $"{namePrefix}_{targetId:X}_{Environment.TickCount64}";
                dp.Owner = targetId;
                dp.DestoryAt = (uint)Math.Max(500, dur);
                dp.Color = color;
                dp.ScaleMode = ScaleMode.None;
                if (type == DrawTypeEnum.Circle)
                {
                    dp.Scale = new Vector2(radius);
                }
                else
                {
                    dp.Scale = new Vector2(60f);
                    dp.InnerScale = new Vector2(radius);
                    dp.Radian = float.Pi * 2;
                }
                sa.Method.SendDraw(DrawModeEnum.Default, type, dp);
            }
            // 1) 箭头：circle
            if (arrowIdLocal != 0)
            {
                DrawOnTarget(arrowIdLocal, "TJ_Arrow", DrawTypeEnum.Circle, 5f, sa.Data.DefaultSafeColor, durMs+2000);
            }
            // 2) 麻将1
            if (mj1IdLocal != 0)
            {
                var t = TypeForMj(mj1Target);
                DrawOnTarget(mj1IdLocal, "TJ_MJ1", t, 8f, sa.Data.DefaultDangerColor, durMs+2000);
            }

            // 3) 麻将2
            if (mj2IdLocal != 0)
            {
                var t = TypeForMj(mj2Target);
                DrawOnTarget(mj2IdLocal, "TJ_MJ2", t, 8f, sa.Data.DefaultDangerColor, durMs+2000);
            }

            // 指路
            Vector3 target = Vector3.Zero;
            string label = "";

            if (myId == arrow && arrow != 0)
            {
                target = TJ_ArrowPos;
                label = "天界交叉斩_箭头";
                sa.tts("箭头位 去A", TTSMode, TTSOpen);
            }
            else if (myId == mj1 && mj1 != 0)
            {
                // 圆：014C->A, 014D->B
                // 环：014C->B, 014D->A
                target = isCircle ? TJ_Pos_A : TJ_Pos_B;
                label = "天界交叉斩_麻将1";
                sa.tts(isCircle ? "麻将一 去C" : "麻将一 去中间", TTSMode, TTSOpen);
            }
            else if (myId == mj2 && mj2 != 0)
            {
                target = isCircle ? TJ_Pos_B : TJ_Pos_A;
                label = "天界交叉斩_麻将2";
                sa.tts(isCircle ? "麻将二 去C" : "麻将二 去中间", TTSMode, TTSOpen);
            }
            else
            {
                // 没点名的人
                target = isDouble ? TJ_NoBuff_Double : TJ_NoBuff_Single;
                label = isDouble ? "天界交叉斩_无点名_双" : "天界交叉斩_无点名_单";
                sa.tts(isDouble ? "无点名 去A分摊" : "无点名 靠边", TTSMode, TTSOpen);
            }

            if (target == Vector3.Zero) return;

            var dp = sa.WaypointDp(
                target: target,
                duration: (uint)Math.Max(1000, durMs),
                delay: 0,
                name: $"{label}_{Environment.TickCount64}",
                color: sa.Data.DefaultSafeColor
            );
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        });
    }

    [ScriptMethod(name: "剑术大师-P1-天界交叉斩-麻将记录", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(028C|014C|014D)$"], userControl: false)]
    public void 剑术大师_P1_天界交叉斩_麻将记录(Event evt, ScriptAccessory sa)
    {
        uint tid = evt.TargetId();
        if (tid == 0) return;

        string iconId = evt["Id"] ?? "";

        lock (_tjLock)
        {
            if (iconId.Equals("028C", StringComparison.OrdinalIgnoreCase))
            {
                _tjArrowTarget = tid;
            }
            else if (iconId.Equals("014C", StringComparison.OrdinalIgnoreCase))
            {
                _tjMj1Target = tid;
            }
            else if (iconId.Equals("014D", StringComparison.OrdinalIgnoreCase))
            {
                _tjMj2Target = tid;
            }
        }
    }

    [ScriptMethod(name: "剑术大师-P1-四方凶兆1-凶兆记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(4785|4786)$"], userControl: false)]
    public void 剑术大师_P1_四方凶兆1_凶兆记录(Event evt, ScriptAccessory sa)
    {
        uint tid = evt.TargetId();
        if (tid == 0) return;

        var sid = evt.StatusId;
        sa.Method.SendChat($"/e StatusId{sid}");

        lock (_xzLock)
        {
            if (!_xzStatusGroups.TryGetValue(sid, out var list)) return;
            if (!list.Contains(tid))
                list.Add(tid);
        }
    }

    [ScriptMethod(name: "剑术大师-P1-四方凶兆1-灵击波记录", eventType: EventTypeEnum.Tether, eventCondition: ["Id:regex:^(0173)$"], userControl: false)]
    public void 剑术大师_P1_四方凶兆1_灵击波记录(Event evt, ScriptAccessory sa)
    {

        uint tid = evt.TargetId();
        if (tid == 0) return;
        // sa.Method.SendChat($"/e tid{tid}");
        uint dataId = GetDataIdByObjectId(sa, tid);
        // sa.Method.SendChat($"/e dataId{dataId}");

        // 过滤：DataId 是 19230 或 19228 的不记录
        if (dataId == 19230u || dataId == 19228u) return;

        lock (_xzLock)
        {
            if (_xzLingjiTargets.Count >= 2) return;
            if (_xzLingjiTargets.Contains(tid)) return;

            _xzLingjiTargets.Add(tid);
            sa.Method.SendChat($"/e [四方凶兆1] 灵击波点名 {_xzLingjiTargets.Count}/2 => tid=0x{tid:X} dataId={dataId}");
        }
    }

    [ScriptMethod(name: "剑术大师-P1-四方凶兆1-接线", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46698)$"])]
    public async void 剑术大师_P1_四方凶兆1_接线(Event evt, ScriptAccessory sa)
    {
        await Task.Delay(1000);
        uint myId = sa.Data.Me;
        sa.Method.SendChat($"/e myId{myId}");
        sa.Method.SendChat($"/e _xzLingjiTargets{_xzLingjiTargets}");

        uint partnerId = 0;
        bool iAmLingji = false;

        lock (_xzLock)
        {
            iAmLingji = _xzLingjiTargets.Contains(myId);
            sa.Method.SendChat($"/e iAmLingji{iAmLingji}");
            if (iAmLingji) return; // 自己被灵击波点了，不接线，不画

            // 找自己属于哪个凶兆组，然后找同组另一个人
            foreach (var kv in _xzStatusGroups)
            {
                var list = kv.Value;
                if (!list.Contains(myId)) continue;

                partnerId = list.FirstOrDefault(x => x != myId);
                break;
            }
        }

        if (partnerId == 0)
        {
            sa.tts("等待同组接线", TTSMode, TTSOpen);
            return;
        }
        

        // 画 2m 绿圈（绑定对方脚下）
        sa.tts("接同组的线", TTSMode, TTSOpen);

        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = $"{XZCircleNamePrefix}{partnerId:X}_{Environment.TickCount64}";
        dp.Owner = partnerId;
        dp.DestoryAt = 8000; // 给个够用时长，后面 ActionEffect 再 remove
        dp.Scale = new Vector2(2f);
        dp.ScaleMode = ScaleMode.None;
        dp.Color = XZGreen;

        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "剑术大师-P1-天界交叉斩-清除绘图", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(46693)$"], userControl:false)]
    public void 剑术大师_P1_天界交叉斩_清除绘图(Event evt, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw(".*");
    }

    [ScriptMethod(name: "剑术大师-P1-四方凶兆1-接线清除绘图", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(46698)$"], userControl:false)]
    public void 剑术大师_P1_四方凶兆1_接线清除绘图(Event evt, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw($"^{XZCircleNamePrefix}.*$");

        lock (_xzLock)
        {
            _xzStatusGroups[4785u].Clear();
            _xzStatusGroups[4786u].Clear();
            _xzLingjiTargets.Clear();
        }
    }
    
    [ScriptMethod(name: "剑术大师-P2-八叶转轮残响回响组合技", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46707|46704)$"])]
    public void 剑术大师_P2_八叶转轮残响回响组合技(Event evt, ScriptAccessory sa)
    {
        uint actionId = evt.ActionId();

        int durMs = 0;
        _ = int.TryParse(evt["DurationMilliseconds"], out durMs);
        if (durMs <= 0) durMs = 6000;

        sa.tts("场中集合", TTSMode, TTSOpen);
        var dpWp = sa.WaypointDp(
            target: new Vector3(170f, -16f, -815f),
            duration: (uint)Math.Max(1000, durMs),
            delay: 0,
            name: $"P2-八叶转轮-场中集合_{actionId}",
            color: sa.Data.DefaultSafeColor
        );
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpWp);

        int delayMs = Math.Max(0, durMs);

        // 46707 用 8向 rect
        float[] rots =
        {
            MathF.PI,              // N
            3f * MathF.PI / 4f,    // NE
            MathF.PI / 2f,         // E
            MathF.PI / 4f,         // SE
            0f,                    // S
            -MathF.PI / 4f,        // SW
            -MathF.PI / 2f,        // W
            -3f * MathF.PI / 4f,   // NW
        };
        string[] dirNames = { "N","NE","E","SE","S","SW","W","NW" };

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delayMs);

                var partyIds = sa.Data.PartyList.ToList();
                if (partyIds.Count == 0) return;

                var posById = new List<(uint id, Vector3 pos)>(partyIds.Count);
                foreach (var pid in partyIds)
                {
                    if (pid == 0) continue;
                    var obj = sa.Data.Objects.FirstOrDefault(o => o.GameObjectId == pid);
                    if (obj == null) continue;
                    posById.Add((pid, obj.Position));
                }
                if (posById.Count == 0) return;

                if (actionId == 46707u)
                {
                    // 冰花
                    foreach (var (pid, basePos) in posById)
                    {
                        for (int i = 0; i < 8; i++)
                        {
                            var dp = sa.Data.GetDefaultDrawProperties();
                            dp.Name = $"P2-八叶转轮_Rect_{pid:X}_{dirNames[i]}_{Environment.TickCount64}";
                            dp.Owner = 0;
                            dp.Position = basePos;
                            dp.Rotation = rots[i];
                            dp.Color = sa.Data.DefaultDangerColor;
                            dp.DestoryAt = 7000;

                            dp.Scale = new Vector2(8f, 28.29f);
                            dp.ScaleMode = ScaleMode.ByTime;

                            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
                        }
                    }
                }
                else
                {
                    // 月环
                    foreach (var (pid, basePos) in posById)
                    {
                        var dp = sa.Data.GetDefaultDrawProperties();
                        dp.Name = $"P2-八叶转轮_Donut_{pid:X}_{Environment.TickCount64}";
                        dp.Owner = 0;
                        dp.Position = basePos;
                        dp.Rotation = 0f;
                        dp.Color = sa.Data.DefaultDangerColor;
                        dp.DestoryAt = 7000;

                        dp.Scale = new Vector2(60f);
                        dp.InnerScale = new Vector2(8f);
                        dp.Radian = float.Pi * 2f;
                        dp.ScaleMode = ScaleMode.None;

                        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
                    }
                }
            }
            catch
            {
            }
        });
    }
    
    private readonly object _eightLock = new();
    private int _eightSeq = 0;
    private long _eightLastMs = 0;

    private readonly List<int> _eightR1 = new(3);
    private readonly List<int> _eightR2 = new(3);
    private readonly List<int> _eightR3 = new(3);

    private bool _eightR2TaskScheduled = false;
    private bool _eightR3GuideIssued = false;

    private static readonly Vector3[] EightCenters = new[]
    {
        new Vector3(157.00f, -16.01f, -828.00f),
        new Vector3(169.97f, -16.01f, -828.00f),
        new Vector3(182.97f, -16.01f, -828.00f),

        new Vector3(157.00f, -16.01f, -815.03f),
        new Vector3(169.97f, -16.01f, -815.03f),
        new Vector3(182.97f, -16.01f, -815.03f),

        new Vector3(157.00f, -16.01f, -802.03f),
        new Vector3(169.97f, -16.01f, -802.03f),
        new Vector3(182.97f, -16.01f, -802.03f),
    };

    private const float EightMatchEps = 0.6f;

    private void ResetEight()
    {
        lock (_eightLock)
        {
            _eightSeq++;
            _eightLastMs = 0;

            _eightR1.Clear();
            _eightR2.Clear();
            _eightR3.Clear();

            _eightR2TaskScheduled = false;
            _eightR3GuideIssued = false;
        }
    }

    private static int MatchEightIndex(Vector3 p)
    {
        int best = -1;
        float bestD2 = float.MaxValue;

        for (int i = 0; i < EightCenters.Length; i++)
        {
            var d = p - EightCenters[i];
            float d2 = d.X * d.X + d.Z * d.Z;
            if (d2 < bestD2) { bestD2 = d2; best = i; }
        }
        return bestD2 <= EightMatchEps * EightMatchEps ? best : -1;
    }

    private static Vector3 GetMePos(ScriptAccessory sa)
    {
        var meId = sa.Data.Me;
        return sa.Data.Objects.FirstOrDefault(o => o.GameObjectId == meId)?.Position ?? Vector3.Zero;
    }

    private static Vector3 PickNearest(Vector3 me, IEnumerable<int> indices)
    {
        float bestD2 = float.MaxValue;
        Vector3 best = Vector3.Zero;

        foreach (var idx in indices)
        {
            var c = EightCenters[idx];
            float d2 = (me.X - c.X) * (me.X - c.X) + (me.Z - c.Z) * (me.Z - c.Z);
            if (d2 < bestD2) { bestD2 = d2; best = c; }
        }
        return best;
    }

    private static void BuildR3FromR1R2(List<int> r1, List<int> r2, List<int> r3)
    {
        r3.Clear();
        var used = new HashSet<int>(r1);
        foreach (var x in r2) used.Add(x);

        for (int i = 0; i < 9; i++)
            if (!used.Contains(i))
                r3.Add(i);
    }

    private void AddIdxToRounds_NoAdvance(int idx)
    {
        if (idx < 0) return;

        if (_eightR1.Count < 3)
        {
            if (!_eightR1.Contains(idx)) _eightR1.Add(idx);
            return;
        }
        if (_eightR2.Count < 3)
        {
            if (!_eightR2.Contains(idx)) _eightR2.Add(idx);
            return;
        }

        if (_eightR3.Count < 3 && !_eightR3.Contains(idx))
            _eightR3.Add(idx);
    }

    [ScriptMethod(
        name: "剑术大师-P2-时差剑波",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:46714"],
        userControl: false)]
    public void 剑术大师_P2_时差剑波(Event evt, ScriptAccessory sa)
    {
        var pos = evt.EffectPosition;
        int idx = MatchEightIndex(pos);
        if (idx < 0) return;

        long now = Environment.TickCount64;
        int seqLocal;

        bool r2Start = false; // 第二轮开始：R2 第一个圈
        bool r3Start = false; // 第三轮开始：R3 第一个圈

        lock (_eightLock)
        {
            // 超时重置
            if (_eightLastMs != 0 && now - _eightLastMs > 5000)
                ResetEight();

            _eightLastMs = now;

            int r2Before = _eightR2.Count;
            int r3Before = _eightR3.Count;

            AddIdxToRounds_NoAdvance(idx);

            if (r2Before == 0 && _eightR2.Count == 1) r2Start = true;
            if (r3Before == 0 && _eightR3.Count == 1) r3Start = true;

            seqLocal = _eightSeq;
        }

        if (r2Start)
        {
            bool canSchedule = false;
            lock (_eightLock)
            {
                if (!_eightR2TaskScheduled)
                {
                    _eightR2TaskScheduled = true;
                    canSchedule = true;
                }
            }

            if (canSchedule)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(250);

                    List<int> r1, r2, r3;
                    int seqNow;

                    lock (_eightLock)
                    {
                        seqNow = _eightSeq;
                        r1 = _eightR1.ToList();
                        r2 = _eightR2.ToList();
                        r3 = _eightR3.ToList();
                    }

                    if (seqNow != seqLocal) return;
                    if (r1.Count < 3 || r2.Count < 3) return;

                    // 推导第三轮
                    var r3Calc = new List<int>(3);
                    BuildR3FromR1R2(r1, r2, r3Calc);

                    var me = GetMePos(sa);
                    if (me == Vector3.Zero) return;

                    var target = PickNearest(me, r3Calc);

                    var dpWp = sa.WaypointDp(target, (uint)3700, 0, "八叶R2->去R3最近圈");
                    sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpWp);
                });
            }
        }

        if (r3Start)
        {
            bool need;
            lock (_eightLock)
            {
                need = !_eightR3GuideIssued && _eightR1.Count == 3;
                if (need) _eightR3GuideIssued = true;
            }

            if (need)
            {
                var me = GetMePos(sa);
                if (me == Vector3.Zero) return;

                List<int> r1Copy;
                lock (_eightLock) r1Copy = _eightR1.ToList();

                var target = PickNearest(me, r1Copy);

                var dpWp = sa.WaypointDp(target, (uint)2000, 3000, "八叶R3->去R1最近圈");
                sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpWp);
            }
        }
    }

    [ScriptMethod(name: "剑术大师-P2-八叶回响-分散圈", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46711)$"])]
    public void 剑术大师_P2_八叶回响_分散圈(Event evt, ScriptAccessory sa)
    {
        var myIdx = sa.MyIndex();
        sa.tts("去标点放圈", TTSMode, TTSOpen);

        var wPos = myIdx switch
        {
            0 => new Vector3(182.97f, -16.00f, -828.07f),
            1 => new Vector3(157.00f, -16.00f, -828.12f),
            2 => new Vector3(156.99f, -16.00f, -801.99f),
            3 => new Vector3(182.97f, -16.00f, -801.92f),
            _ => default
        };
        
        var dpWp = sa.WaypointDp(wPos, (uint)5000, 4000, "剑术大师-P2-八叶回响-分散冰花指路");
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpWp);
    }

    [ScriptMethod(name: "剑术大师-P2-八叶回响-分散圈-第二段指路", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(46712)$"])]
    public void 剑术大师_P2_八叶回响_分散圈_第二段指路(Event evt, ScriptAccessory sa)
    {
        if(_phase != 1) return;

        var myId = sa.Data.Me;
        var targetId = evt.TargetId();
        sa.Method.SendChat($"/e {myId} {targetId}");
        if (targetId == 0 || targetId != myId) return;

        var myIdx = sa.MyIndex();
        sa.tts("快跑", TTSMode, TTSOpen);

        var wPos = myIdx switch
        {
            0 => new Vector3(170.00f, -16.00f, -822.58f),
            1 => new Vector3(170.00f, -16.00f, -822.58f),
            2 => new Vector3(162.40f, -16.00f, -815.06f),
            3 => new Vector3(169.87f, -16.00f, -807.40f),
            _ => default
        };

        var dpWp = sa.WaypointDp(wPos, (uint)3000, 0, "剑术大师-P2-八叶回响-分散冰花指路");
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpWp);
    }

    [ScriptMethod(name: "剑术大师-P2-半场刀", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46716)$"])]
    public void 剑术大师_P2_半场刀(Event evt, ScriptAccessory sa)
    {
        float rot;
        try
        {
            rot = evt.SourceRotation;
        }
        catch
        {
            return;
        }

        var pos = evt.SourcePosition();
        if (pos == Vector3.Zero) return;

        int durMs = 0;
        _ = int.TryParse(evt["DurationMilliseconds"], out durMs);
        if (durMs <= 0) durMs = 6000;

        const float outer = 60f;

        sa.tts("半场刀", TTSMode, TTSOpen);
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = $"P2_半场刀_{Environment.TickCount64}";
        dp.Position = pos;
        dp.Rotation = rot;
        dp.Color = sa.Data.DefaultDangerColor;
        dp.DestoryAt = (uint)(durMs);
        dp.ScaleMode = ScaleMode.ByTime;

        dp.Scale = new Vector2(outer);
        dp.Radian = MathF.PI;         // 180°

        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    [ScriptMethod(name: "剑术大师-P2-冥界重光波-三连分摊TTS", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46717)$"])]
    public void 剑术大师_P2_冥界重光波_三连分摊TTS(Event evt, ScriptAccessory sa)
    {
        sa.tts("三连分摊", TTSMode, TTSOpen);
        // 换p进入p3
        _phase = 3;
    }

    [ScriptMethod(name: "剑术大师-P3-四方凶兆2-凶兆记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(4784|4775)$"], userControl: false)]
    public void 剑术大师_P3_四方凶兆2_凶兆记录(Event evt, ScriptAccessory sa)
    {
        if (_phase != 3) return;

        uint tid = evt.TargetId();
        if (tid == 0) return;

        long now = Environment.TickCount64;

        lock (_p3Lock)
        {
            if (_p3LastMs != 0 && now - _p3LastMs > 12000)
                ResetP3();

            _p3LastMs = now;

            uint sid = evt.StatusId;
            if (sid == 4784u)
            {
                if (!_p3SafeEW.Contains(tid)) _p3SafeEW.Add(tid);
            }
            else if (sid == 4775u)
            {
                if (!_p3SafeNS.Contains(tid)) _p3SafeNS.Add(tid);
            }
        }
    }
    
    [ScriptMethod(name: "剑术大师-P3-四方凶兆2-灵击波记录1", eventType: EventTypeEnum.Tether, eventCondition: ["Id:regex:^(0173)$"], userControl: false)]
    public void 剑术大师_P3_四方凶兆2_灵击波记录1(Event evt, ScriptAccessory sa)
    {
        if (_phase != 3) return;

        uint sid = evt.SourceId();
        uint tid = evt.TargetId();
        if (sid == 0 || tid == 0) return;

        long now = Environment.TickCount64;

        Vector3 sPos = evt.SourcePosition();
        Vector3 tPos = evt.TargetPosition();

        lock (_p3Lock)
        {
            // 超时保护
            if (_p3LastMs != 0 && now - _p3LastMs > 12000)
                ResetP3();
            _p3LastMs = now;

            // 去重
            const int windowMs = 300;
            for (int i = _p3Tethers.Count - 1; i >= 0; i--)
            {
                var e = _p3Tethers[i];
                if (now - e.TimeMs > windowMs) break;
                if (e.SourceId == sid && e.TargetId == tid) return;
            }

            _p3Tethers.Add(new P3TetherEdge(sid, tid, sPos, tPos, now));
        }
    }

    [ScriptMethod(name: "剑术大师-P3-四方凶兆2-灵击波记录2", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(48653)$"], userControl: false)]
    public void 剑术大师_P3_四方凶兆2_灵击波记录2(Event evt, ScriptAccessory sa)
    {
        if (_phase != 3) return;

        int seqLocal;
        lock (_p3Lock)
        {
            if (_p3Resolved) return;
            _p3Resolved = true;
            seqLocal = _p3Seq;
        }

        _ = Task.Run(async () =>
        {
            await Task.Delay(150);

            List<P3TetherEdge> edges;
            List<uint> party;
            Dictionary<uint, Vector3> partyPos = new();

            lock (_p3Lock)
            {
                if (seqLocal != _p3Seq) return;
                edges = _p3Tethers.ToList();
            }

            party = sa.Data.PartyList.Where(x => x != 0).ToList();
            foreach (var pid in party)
                partyPos[pid] = GetObjPos(sa, pid);

            if (edges.Count == 0 || party.Count == 0) return;

            var outMap = new Dictionary<uint, uint>();
            var posOf = new Dictionary<uint, Vector3>();
            foreach (var e in edges)
            {
                outMap[e.SourceId] = e.TargetId;

                if (e.SourcePos != Vector3.Zero) posOf[e.SourceId] = e.SourcePos;
                if (e.TargetPos != Vector3.Zero && !posOf.ContainsKey(e.TargetId)) posOf[e.TargetId] = e.TargetPos;
            }

            // 反向索引
            var inMap = new Dictionary<uint, List<uint>>();
            foreach (var e in edges)
            {
                if (!inMap.TryGetValue(e.TargetId, out var list))
                    inMap[e.TargetId] = list = new List<uint>();
                if (!list.Contains(e.SourceId)) list.Add(e.SourceId);
            }

            var memberToBottom = new Dictionary<uint, P3MemberLink>();

            foreach (var m in party)
            {
                // 找是否有边 (orb -> m)
                var incoming = edges.Where(x => x.TargetId == m).Select(x => x.SourceId).Distinct().ToList();
                if (incoming.Count == 0) continue;

                // 可能会有多条（异常/重复），选一个：优先有位置的
                uint orb = incoming.FirstOrDefault(id => posOf.ContainsKey(id));
                if (orb == 0) orb = incoming[0];

                // 追溯到最底部 orb：一直往上找 parent -> current（且 parent 不是队员）
                uint cur = orb;
                while (true)
                {
                    if (!inMap.TryGetValue(cur, out var parents) || parents.Count == 0) break;

                    uint p = parents.FirstOrDefault(x => !partyPos.ContainsKey(x));
                    if (p == 0) break;

                    cur = p;
                }

                Vector3 bpos = posOf.TryGetValue(cur, out var bp) ? bp : (posOf.TryGetValue(orb, out var op) ? op : Vector3.Zero);
                var key = MatchBottomKey(bpos);

                memberToBottom[m] = new P3MemberLink(m, cur, bpos, key);
            }

            // 2) 兜底：有些队员不在“orb->member”的 target 里
            //    就拿“剩余的底部 orb”，按 5m 内最近分配
            //    先收集所有“看起来像底部”的 orb：它作为 source 出现过，且它的 target 不是队员（或者它没有 parent）
            var candidateBottoms = new HashSet<uint>();

            foreach (var e in edges)
            {
                // source 不是队员 && sourcePos 有意义
                if (partyPos.ContainsKey(e.SourceId)) continue;
                candidateBottoms.Add(e.SourceId);
            }

            // 进一步把 candidate 追溯到真正底部
            uint GetBottom(uint start)
            {
                uint cur = start;
                while (true)
                {
                    if (!inMap.TryGetValue(cur, out var parents) || parents.Count == 0) break;
                    uint p = parents.FirstOrDefault(x => !partyPos.ContainsKey(x));
                    if (p == 0) break;
                    cur = p;
                }
                return cur;
            }

            var bottomToPos = new Dictionary<uint, Vector3>();
            foreach (var c in candidateBottoms)
            {
                uint b = GetBottom(c);
                if (!bottomToPos.ContainsKey(b))
                {
                    Vector3 bp = posOf.TryGetValue(b, out var v) ? v : Vector3.Zero;
                    bottomToPos[b] = bp;
                }
            }

            // 对没分到的队员按最近 bottom 分配（5m 以内）
            float eps2 = P3AssignEps * P3AssignEps;

            foreach (var m in party)
            {
                if (memberToBottom.ContainsKey(m)) continue;

                if (!partyPos.TryGetValue(m, out var mp) || mp == Vector3.Zero) continue;

                uint bestB = 0;
                float bestD2 = float.MaxValue;

                foreach (var kv in bottomToPos)
                {
                    var bp = kv.Value;
                    if (bp == Vector3.Zero) continue;

                    float d2 = DistXZ2(mp, bp);
                    if (d2 < bestD2)
                    {
                        bestD2 = d2;
                        bestB = kv.Key;
                    }
                }

                if (bestB != 0 && bestD2 <= eps2)
                {
                    Vector3 bp = bottomToPos[bestB];
                    var key = MatchBottomKey(bp);
                    memberToBottom[m] = new P3MemberLink(m, bestB, bp, key);
                }
            }

            // 写回共享
            lock (_p3Lock)
            {
                if (seqLocal != _p3Seq) return;

                _p3MemberToBottom.Clear();
                foreach (var kv in memberToBottom)
                    _p3MemberToBottom[kv.Key] = kv.Value;
            }
        });
    }

    [ScriptMethod(name: "剑术大师-P3-四方凶兆2", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46725)$"])]
    public void 剑术大师_P3_四方凶兆2(Event evt, ScriptAccessory sa)
    {
        if (_phase != 3) return;

        int seqLocal;
        lock (_p3Lock)
        {
            if (_p3GuideIssued) return;
            _p3GuideIssued = true;
            seqLocal = _p3Seq;
        }

        _ = Task.Run(async () =>
        {
            await Task.Delay(1000);

            uint me = sa.Data.Me;
            if (me == 0) return;

            List<uint> ew, ns;
            Dictionary<uint, P3MemberLink> map;

            lock (_p3Lock)
            {
                if (seqLocal != _p3Seq) return;
                ew = _p3SafeEW.ToList();
                ns = _p3SafeNS.ToList();
                map = _p3MemberToBottom.ToDictionary(k => k.Key, v => v.Value);
            }

            bool iAmEW = ew.Contains(me);
            bool iAmNS = ns.Contains(me);

            if (!iAmEW && !iAmNS)
            {
                sa.tts("未记录凶兆分组", TTSMode, TTSOpen);
                return;
            }

            uint partner = iAmEW ? FindAnother(ew, me) : FindAnother(ns, me);
            if (partner == 0)
            {
                sa.tts("未找到同组队友", TTSMode, TTSOpen);
                return;
            }

            map.TryGetValue(me, out var myLink);
            map.TryGetValue(partner, out var ptLink);

            Vector3 EW_A = new(188.24f, -16.00f, -819.50f);
            Vector3 EW_B = new(151.26f, -16.00f, -810.56f);

            Vector3 NS_A = new(174.39f, -16.00f, -796.65f);
            Vector3 NS_B = new(165.24f, -16.00f, -833.78f);

            Vector3 go = Vector3.Zero;

            if (iAmEW)
            {
                // - 优先看谁连 LEFT：连 LEFT 的去 EW_A，另一个去 EW_B
                // - 如果两个人都不连 LEFT，则看谁连 RIGHT：连 RIGHT 的去 EW_B，另一个去 EW_A
                bool meLeft = myLink.Key == P3BottomKey.Left;
                bool ptLeft = ptLink.Key == P3BottomKey.Left;

                if (meLeft || ptLeft)
                {
                    go = meLeft ? EW_A : EW_B;
                }
                else
                {
                    bool meRight = myLink.Key == P3BottomKey.Right;
                    bool ptRight = ptLink.Key == P3BottomKey.Right;

                    if (meRight || ptRight)
                        go = meRight ? EW_B : EW_A;
                    else
                    {
                        // 兜底：按站位近的分配
                        sa.Method.SendChat("/e 四方凶兆2发生错误");
                        var mePos = GetObjPos(sa, me);
                        if (mePos != Vector3.Zero)
                        {
                            float d2a = DistXZ2(mePos, EW_A);
                            float d2b = DistXZ2(mePos, EW_B);
                            go = d2a <= d2b ? EW_A : EW_B;
                        }
                    }
                }

                sa.tts("东西安全 去点位", TTSMode, TTSOpen);
            }
            else
            {
                // - 优先看谁连 UP：连 UP 的去 NS_A，另一个去 NS_B
                // - 如果两个人都不连 UP，则看谁连 DOWN：连 DOWN 的去 NS_B，另一个去 NS_A
                bool meUp = myLink.Key == P3BottomKey.Up;
                bool ptUp = ptLink.Key == P3BottomKey.Up;

                if (meUp || ptUp)
                {
                    go = meUp ? NS_A : NS_B;
                }
                else
                {
                    bool meDown = myLink.Key == P3BottomKey.Down;
                    bool ptDown = ptLink.Key == P3BottomKey.Down;

                    if (meDown || ptDown)
                        go = meDown ? NS_B : NS_A;
                    else
                    {
                        sa.Method.SendChat("/e 四方凶兆2发生错误");
                        var mePos = GetObjPos(sa, me);
                        if (mePos != Vector3.Zero)
                        {
                            float d2a = DistXZ2(mePos, NS_A);
                            float d2b = DistXZ2(mePos, NS_B);
                            go = d2a <= d2b ? NS_A : NS_B;
                        }
                    }
                }

                sa.tts("南北安全 去点位", TTSMode, TTSOpen);
            }

            if (go == Vector3.Zero) return;

            DrawWaypointToMe(sa, go, 6500, "P3_四方凶兆2_指路");

            await Task.Delay(5000);
            _phase = 4;
            sa.tts("天界交叉斩准备", TTSMode, TTSOpen);
        });
    }

    [ScriptMethod(name: "剑术大师-P3-四方凶兆3-凶兆记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(4777|4778|4781)$"], userControl: false)]
    public void 剑术大师_P3_四方凶兆3_凶兆记录(Event evt, ScriptAccessory sa)
    {
        if (_phase != 4) return;
        // 只记录自己的
        // 4777 东南危险
        // 4778 西南危险
        // 4781 东北危险
        // xxxx 西北危险
        // 记录StatusID以及点的是小队里的谁
    }
    [ScriptMethod(name: "剑术大师-P3-四方凶兆3-灵击波记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["StatusID:regex:^(46749)$"], userControl: false)]
    public void 剑术大师_P3_四方凶兆3_灵击波记录(Event evt, ScriptAccessory sa)
    {
        if (_phase != 4) return;
        // 记录 SourcePosition
        // X 150左右的是西 190左右的是东
        // Y -795左右的是南 -835左右的是北
        // 差距在0.6之内，可以用之前的那个param

        // 然后接下来的3秒内只能执行一次（因为可能会触发四次，但我想要记录四个SourcePosition但是接下来的画图逻辑只执行一次）
        // 等待200ms等所有SourcePosition都被记录下来了
        // 查看自己的凶兆记录
        // 如果是东南危险，去找西北的灵击波。然后以不是边界的两个灵击波的坐标合一起为中心，画一个Rect，持续三秒.(比如西(X:150, Y:-16, Z: -810)和北(X:165, Y:-16, Z:-835),那么中心就是(165, -16, -810))

        // 结束完清空list，3秒后马上又会来一波新的
    }
    #endregion
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
            var s = idStr.Replace("0x", "");
            id = uint.Parse(s, System.Globalization.NumberStyles.HexNumber);
            return true;
        }
        catch { return false; }
    }

    public static uint ActionId(this Event evt) => JsonConvert.DeserializeObject<uint>(evt["ActionId"]);
    public static uint SourceId(this Event evt) => ParseHexId(evt["SourceId"], out var id) ? id : 0;
    public static uint TargetId(this Event evt) => ParseHexId(evt["TargetId"], out var id) ? id : 0;
    public static Vector3 SourcePosition(this Event evt) => JsonConvert.DeserializeObject<Vector3>(evt["SourcePosition"]);
    public static Vector3 TargetPosition(this Event evt) => JsonConvert.DeserializeObject<Vector3>(evt["TargetPosition"]);
}

public static class ScriptAccessoryExtensions
{
    public static int MyIndex(this ScriptAccessory sa) => sa.Data.PartyList.IndexOf(sa.Data.Me);

    public static void tts(this ScriptAccessory sa, string text, MerchantsTale.TTS播报方式 mode, bool open)
    {
        if (!open) return;
        switch (mode)
        {
            case MerchantsTale.TTS播报方式.原生TTS:
                sa.Method.TTS(text);
                break;
            case MerchantsTale.TTS播报方式.EdgeTTS:
                sa.Method.EdgeTTS(text);
                break;
            case MerchantsTale.TTS播报方式.DrTTS:
                sa.Method.SendChat($"/pdr tts {text}");
                break;
        }
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

    public static DrawPropertiesEdit WaypointDp(this ScriptAccessory sa, Vector3 target, uint duration, uint delay = 0, string name = "Waypoint", Vector4? color = null)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? sa.Data.DefaultSafeColor;
        dp.Owner = sa.Data.Me;            // 绑定自己
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