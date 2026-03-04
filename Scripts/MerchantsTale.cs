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
    }
    
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

        // 4784 东西安全
        // 4775 南北安全

    }

    [ScriptMethod(name: "剑术大师-P3-四方凶兆2-灵击波记录1", eventType: EventTypeEnum.Tether, eventCondition: ["Id:regex:^(0173)$"], userControl: false)]
    public void 剑术大师_P3_四方凶兆2_灵击波记录1(Event evt, ScriptAccessory sa)
    {
        if (_phase != 3) return;

        // 这里记录灵击波的SourceId和TargetId, SourcePosition and TargetPosition
        // 这里灵击波的target可能会是另一个灵击波或者是小队里的人 比如可能会是以下这样
        // [4000FAD6|灵击波] 0173 [4000FAD4|灵击波]
        // [4000FADA|灵击波] 0173 [4000FAD8|灵击波]
        // [4000FACD|灵击波] 0173 [4000FACB|灵击波]
        // [4000FAD1|灵击波] 0173 [4000FACF|灵击波]
        // [4000FACB|灵击波] 0173 [10754C9E|骑士]
        // [4000FACF|灵击波] 0173 [1073616B|占星术士]

    }

    [ScriptMethod(name: "剑术大师-P3-四方凶兆2-灵击波记录2", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(48653)$"])]
    public void 剑术大师_P3_四方凶兆2_灵击波记录2(Event evt, ScriptAccessory sa)
    {
        if (_phase != 3) return;
        // 只执行一次
        // 记录小队里所有人的position
        // 检查灵击波记录1里的list，如果target是小队里的人，看是哪个灵击波连的他，然后看这个灵击波又是属于哪个灵击波的，每个连接小队成员的灵击波一定属于另一个灵击波底下。记下最底的灵击波的Sourceposition以及小队成员的对应关系
        // 如果有些小队成员并不在上面的list中，检查剩余的对应灵击波，看看targetPosition和小队里的position哪个离得近，比如说5m之内的最近的。比如上面的list 绘灵法师不在list中，但是绘灵法师的pos是{162.97, -16.00, -810.53}，// [4000FAD6|灵击波] 0173 [4000FAD4|灵击波]
        // [4000FADA|灵击波] 0173 [4000FAD8|灵击波]这两个灵击波连上的灵击波没有连上队员。那么比较他们的position，最终找到一个合适的然后记录下来。
    }

    [ScriptMethod(name: "剑术大师-P3-四方凶兆2", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46725)$"])]
    public void 剑术大师_P3_四方凶兆2(Event evt, ScriptAccessory sa)
    {
        // 等待1000ms
        // 在灵击波记录2中得到了所有底部灵击波的sourcespotion以及对应的小队队员。
        // 底部的灵击波一共只会出现在四个点 右：{"X":190.00,"Y":-16.00,"Z":-810.00} 左：{"X":150.00,"Y":-16.00,"Z":-820.00} 下：{"X":165.00,"Y":-16.00,"Z":-795.00}上：{"X":175.00,"Y":-16.00,"Z":-835.00}
        // 如果你是东西安全，检查另一个和你一样是东西安全的人，你们两个谁连接了左的灵击波，连接了左的去{188.24, -16.00, -819.50}，另一个去{151.26, -16.00, -810.56}。如果两个人都没有连接左，查看谁连接了右，连接了右的去{151.26, -16.00, -810.56}，另一个去{188.24, -16.00, -819.50}
        // 如果你是南北安全，检查另一个和你一样是南北安全的人，你们两个谁连接了上的灵击波，连接了上的去{174.39, -16.00, -796.65}，另一个去{165.24, -16.00, -833.78}。如果两个人都没有连接左，查看谁连接了右，连接了右的去{165.24, -16.00, -833.78}，另一个去{174.39, -16.00, -796.65}
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