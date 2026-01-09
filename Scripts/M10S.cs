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

using System.Diagnostics;

namespace Codaaaaaa.M9S;

[ScriptType(
    guid: "b7d2d7d7-5e8c-4f53-8a0e-7e7f0a7a1c21",
    name: "阿卡狄亚零式登天斗技场M10S",
    territorys: [1323],
    version: "0.0.0.1",
    author: "Codaaaaaa",
    note: "Template：画图+指路+TTS。请按实际机制替换 ActionId/DataId/TargetIcon。")]
public class M10S
{
    #region 用户设置
    // [UserSetting("是否开启TTS")] public static bool TTSOpen { get; set; } = true;

    // 水波或者镜像水波
    [ UserSetting("P2第一轮打法")] public P2第一轮打法 p2第一轮打法 { get; set; } = P2第一轮打法.水波;
    // 近战优化或者美式野
    [ UserSetting("P2第二三轮打法")] public P2第二三轮打法 p2第二三轮打法 { get; set; } = P2第二三轮打法.近战优化;
    #endregion
    public enum P2第一轮打法
    {
        水波,
        镜像水波
    }

    public enum P2第二三轮打法
    {
        近战优化,
        美式野
    }


    private static readonly Vector3 Center = new(100f, 0f, 100f);

    private uint _phase = 1;
    private uint 炽红 = 19287u;

    // =========================
    // 空中旋火：射线记录 & 点位动态下移
    // =========================
    private bool _airSpinRayActive = false;
    private Vector3 _airSpinRayBossPos;
    private Vector3 _airSpinRayTargetPos;
    private Vector2 _airSpinRayDirXZ; // (dx, dz)
    private readonly Dictionary<int, Vector3> _airSpinP1Override = new();
    private bool _bossBasisReady = false;
    private Vector3 _bossBasisPos;     // 火冲开始时boss位置
    private Vector2 _bossFwdXZ;        // 火冲开始时boss forward(朝场内)
    private Vector2 _bossRightXZ;      // 火冲开始时boss right
    
    private static readonly Dictionary<int, Vector3> AirSpinRowZ103_57 = new()
    {
        { 4, new Vector3(118.01f, 0f, 103.57f) },
        { 5, new Vector3(111.8f,  0f, 103.57f) },
        { 1, new Vector3(105.59f, 0f, 103.57f) },
        { 3, new Vector3(99.38f,  0f, 103.57f) },
        { 7, new Vector3(93.17f,  0f, 103.57f) },
    };

    // =========================
    // 分摊分散
    private int _分摊分散 = 0;
    private Vector3 _浪花位置 = Vector3.Zero;
    public void Init(ScriptAccessory sa)
    {
        ResetAll();
        sa.Method.RemoveDraw(".*");
    }

    private void ResetAll()
    {
        _phase = 1;
        _airSpinRayActive = false;
        _airSpinP1Override.Clear();
        _分摊分散 = 0;
        _P2左右 = 0;
        _P2火圈次数 = 0;
        _浪花位置 = Vector3.Zero;
    }

    // P2左右
    private int _P2左右 = 0; // 1=左 2=右
    private int _P2火圈次数 = 0;

    // Debug
    [ScriptMethod(name: "clear draw", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASCLEAR"], userControl: false)]
    public void ClearDraw(Event evt, ScriptAccessory sa) => sa.Method.RemoveDraw(".*");

    [ScriptMethod(name: "Set Phase 1", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASP1"], userControl: false)]
    public void SetP1(Event evt, ScriptAccessory sa) => _phase = 1;

    [ScriptMethod(name: "Set Phase 2", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASP2"], userControl: false)]
    public void SetP2(Event evt, ScriptAccessory sa) => _phase = 2;

    [ScriptMethod(name: "Set Phase 3", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASP3"], userControl: false)]
    public void SetP3(Event evt, ScriptAccessory sa) => _phase = 3;

    [ScriptMethod(name: "Set Phase 4", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASP4"], userControl: false)]
    public void SetP4(Event evt, ScriptAccessory sa) => _phase = 4;

    [ScriptMethod(name: "Set Phase 5", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASP5"], userControl: false)]
    public void SetP5(Event evt, ScriptAccessory sa) => _phase = 5;

    [ScriptMethod(name: "Show Phase", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:phase"], userControl: false)]
    public void ShowPhase(Event evt, ScriptAccessory sa) => sa.Method.SendChat($"/e Current Phase: {_phase}");

    [ScriptMethod(name: "Show Debug", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:debug"], userControl: false)]
    public void ShowDebug(Event evt, ScriptAccessory sa) => sa.Method.SendChat($"/e Current Param: {_P2火圈次数}, 左右: {_P2左右}");

    // =========================
    // 示例：记录分摊/分散（把 ActionId 换成 M9S 对应技能）
    // =========================
    [ScriptMethod(
    name: "分摊死刑",
    eventType: EventTypeEnum.StartCasting,
    eventCondition: ["ActionId:46518"])]
    public void 分摊死刑(Event evt, ScriptAccessory sa)
    {
        var myIdx = sa.MyIndex();
        var color = myIdx switch
        {
            0 => sa.Data.DefaultSafeColor,
            1 => sa.Data.DefaultSafeColor,
            _ => sa.Data.DefaultDangerColor
        };
        var targetId = evt.TargetId();

        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = $"死刑圈-{targetId:X}";
        dp.Owner = targetId;
        dp.Color = color;
        dp.DestoryAt = 5500;
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Scale = new Vector2(6);
        
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    // 开场火冲
    [ScriptMethod(
        name: "开场火冲麻将",
        eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:regex:^(3004|3005|3006|3451)$"]
    )]
    public void 开场火冲麻将(Event evt, ScriptAccessory sa)
    {
        var targetId = evt.TargetId();
        var myId = sa.Data.Me;

        if (!RayHelpers.TryGetBossBasis(sa, 炽红, Center, out var bossPos, out var fwd, out var right))
        {
            sa.Method.SendChat("/e 找不到Boss，无法缓存火冲基底");
            return;
        }

        _bossBasisReady = true;
        _bossBasisPos = new Vector3(bossPos.X, 0f, bossPos.Z);
        _bossFwdXZ = fwd;
        _bossRightXZ = right;

        sa.Method.SendChat($"/e 缓存Boss基底 pos=({_bossBasisPos.X:F1},{_bossBasisPos.Z:F1}) fwd=({_bossFwdXZ.X:F2},{_bossFwdXZ.Y:F2}) right=({_bossRightXZ.X:F2},{_bossRightXZ.Y:F2})");
        // 只画自己身上的
        if (targetId == 0 || myId == 0 || targetId != myId)
            return;

        // 取 boss 作为锚点 + 计算朝向
        // 后面全部用缓存
        var bossPos0 = _bossBasisPos;
        var fwd0 = _bossFwdXZ;
        var right0 = _bossRightXZ;

        var wpos = evt["StatusID"] switch
        {
            "3004" => RayHelpers.BossRelativePoint(bossPos0, fwd0, right0,  19f, 1f),
            "3005" => RayHelpers.BossRelativePoint(bossPos0, fwd0, right0,  -4f, 1f),
            "3006" => RayHelpers.BossRelativePoint(bossPos0, fwd0, right0,  19f, 6f),
            "3451" => RayHelpers.BossRelativePoint(bossPos0, fwd0, right0,  -4f, 6f),
            _ => Center
        };


        sa.Method.SendChat($"/e 动态麻将点 sid={evt["StatusID"]} boss=({bossPos0.X:F1},{bossPos0.Z:F1}) => wpos=({wpos.X:F1},{wpos.Z:F1})");

        var dp = sa.WaypointDp(wpos, 20000, 0, "开场火冲麻将指路");
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }


    [ScriptMethod(
        name: "开场火冲麻将删除画图空中旋火射线记录",
        eventType: EventTypeEnum.StatusRemove,
        eventCondition: ["StatusID:regex:^(3004|3005|3006|3451)$"],
        userControl: false
    )]
    public void 开场火冲麻将删除画图空中旋火射线记录(Event evt, ScriptAccessory sa)
    {
        var targetId = evt.TargetId();
        var myId = sa.Data.Me;
        var statusId = evt["StatusID"];

        // 只删自己身上的
        if (statusId != "3451")
        {
            if (targetId == 0 || myId == 0 || targetId != myId)
                return;
        }

        sa.Method.RemoveDraw("开场火冲麻将指路");

        // 记录
        if (statusId != "3451")
            return;
        sa.Method.SendChat("/e 记录");
        if (targetId == 0) return;

        var targetObj = sa.Data.Objects.FirstOrDefault(o => o.GameObjectId == targetId);
        if (targetObj == null) {
            // sa.Method.SendChat($"/e 目标对象未找到，TargetId={targetId:X}");
            return;
        }

        var bossObj = sa.Data.Objects.FirstOrDefault(o => o.DataId == 炽红);
        if (bossObj == null) {
            // sa.Method.SendChat($"/e Boss对象未找到，DataId={炽红:X}");
            return;
        }

        var bossPos = bossObj.Position;
        var targetPos = targetObj.Position;

        // 方向（XZ）
        var d = new Vector2(targetPos.X - bossPos.X, targetPos.Z - bossPos.Z);
        // sa.Method.SendChat($"/e 方向d=({d.X},{d.Y})");
        if (d.LengthSquared() < 0.0001f) return;

        _airSpinRayActive = true;
        _airSpinRayBossPos = bossPos;
        _airSpinRayTargetPos = targetPos;
        _airSpinRayDirXZ = d;

        if (!_bossBasisReady)
        {
            // 说明火冲没触发过或者缓存丢了，没法按你的逻辑做
            return;
        }
        var bpos = _bossBasisPos;
        var fwd  = _bossFwdXZ;
        var right= _bossRightXZ;

        var baseMap = new Dictionary<int, Vector3>
        {
            // Row points you described
            { 4, RayHelpers.BossRelativePoint(bpos, fwd, right,  19f, 8f) },
            { 5, RayHelpers.BossRelativePoint(bpos, fwd, right,   9.5f, 8f) },
            { 1, RayHelpers.BossRelativePoint(bpos, fwd, right,   0f,  8f) },
            { 3, RayHelpers.BossRelativePoint(bpos, fwd, right,  -9.5f, 8f) },
            { 7, RayHelpers.BossRelativePoint(bpos, fwd, right, -16f,  8f) },
        };

        _airSpinP1Override.Clear();

        // Move along boss forward until not covered
        foreach (var kv in baseMap)
        {
            var idx = kv.Key;
            var p0 = kv.Value;

            var pNew = RayHelpers.AdjustPointAlongDirUntilNotCoveredXZ(
                p0,
                _airSpinRayBossPos,
                _airSpinRayDirXZ,
                moveDirXZ: fwd,      // IMPORTANT: move along boss forward towards center
                halfWidth: 5f,
                epsilon: 0.05f,
                maxMove: 40f
            );

            pNew = RayHelpers.ClampXZ(pNew, 80f, 120f);
            _airSpinP1Override[idx] = pNew;
        }

        var baseRight19 = RayHelpers.BossRelativePoint(bpos, fwd, right, 19f, 8f);
        // 先拿 right=19, forward=0 的最近不覆盖点当锚点
        var anchor = RayHelpers.AdjustPointAlongDirUntilNotCoveredXZ(
            baseRight19, _airSpinRayBossPos, _airSpinRayDirXZ,
            moveDirXZ: fwd,
            halfWidth: 5f, epsilon: 0.05f, maxMove: 40f
        );

        static Vector3 AddForward(Vector3 p, Vector2 fwd, float dist)
            => new Vector3(p.X + fwd.X * dist, 0f, p.Z + fwd.Y * dist);

        // 再从 anchor 加 9/18/27
        (int idx, float dist)[] col = { (0, 9f), (2, 18f), (6, 27f) };

        foreach (var (idx, dist) in col)
        {
            var p = AddForward(anchor, fwd, dist);

            var pNew = RayHelpers.AdjustPointAlongDirUntilNotCoveredXZ(
                p, _airSpinRayBossPos, _airSpinRayDirXZ,
                moveDirXZ: fwd,
                halfWidth: 5f, epsilon: 0.05f, maxMove: 40f
            );


            pNew = RayHelpers.ClampXZ(pNew, 80f, 120f);
            _airSpinP1Override[idx] = pNew;
        }

        sa.Method.SendChat($"/e Ray boss={bossObj.GameObjectId:X} target={targetObj.GameObjectId:X} dir=({_airSpinRayDirXZ.X:F2},{_airSpinRayDirXZ.Y:F2}), adjusted {_airSpinP1Override[7]}.");
    }
    // 转场换P
    [ScriptMethod(
        name: "转场换P2",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:46553"],
        userControl: false
    )]
    public void 转场换P2(Event evt, ScriptAccessory sa)
    {
        _phase = 2;
        sa.Method.SendChat("/e 转场换P，当前阶段 P2");
    }

    // 空中旋火
    [ScriptMethod(
        name: "空中旋火指路P1",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:46529"]
    )]
    public void 空中旋火指路P1(Event evt, ScriptAccessory sa)
    {
        var myIdx = sa.MyIndex();
        var targetId = evt.TargetId();
        var myId = sa.Data.Me;
        if (_phase == 1)
        {
            // P1
            // 只画自己身上的
            if (targetId == 0 || myId == 0 || targetId != myId)
                return;

            Vector3 wpos;
            if (_airSpinRayActive && _airSpinP1Override.TryGetValue(myIdx, out var ov))
            {
                wpos = ov;
            }
            else
            {
                wpos = myIdx switch
                {
                    _ => new Vector3(0f, 0f, 0f)
                };
            }

            var dp = sa.WaypointDp(wpos, 6000, 0, "空中旋火指路P1");
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }
        
        if (_phase == 2 && _P2火圈次数 <= 3)
        {   
            _P2火圈次数++;
            sa.Method.SendChat($"/e P2火圈次数 {_P2火圈次数}");

            const uint BuffId = 4974;
            var spots = new Vector3[4];
            if (p2第二三轮打法 == P2第二三轮打法.近战优化)
            {
                // 四个落点（y 固定 0）
                // 如果P2左右==1 左边点，否则右边点
                if (_P2左右 == 1)
                {
                    spots = new[]
                    {
                        new Vector3(118.91f, 0.00f,  87.64f),
                        new Vector3(119.09f, 0.00f,  95.83f),
                        new Vector3(118.96f, 0.00f, 103.66f),
                        new Vector3(119.39f, 0.00f, 110.78f),
                    };
                }
                else
                {
                    spots = new[]
                    {
                        new Vector3(118.91f, 0.00f,  87.64f),
                        new Vector3(119.09f, 0.00f,  95.83f),
                        new Vector3(118.96f, 0.00f, 103.66f),
                        new Vector3(119.39f, 0.00f, 110.78f),
                    };
                }
            }
            else
            {
                // 美
                if (_P2左右 == 1)
                {
                    spots = new[]
                    {
                        new Vector3(118.28f, 0.00f,  93.43f), // 6 7
                        new Vector3(112.2f, 0.00f,  93.43f), // 4 5
                        new Vector3(112.2f, 0.00f,  103.74f), // 0 1
                        new Vector3(118.28f, 0.00f,  103.74f), // 2 3
                    };
                }
                else
                {
                    spots = new[]
                    {
                        new Vector3(81.72f, 0.00f,  93.43f), // 6 7
                        new Vector3(87.8f, 0.00f,  93.43f), // 4 5
                        new Vector3(87.8f, 0.00f,  103.74f), // 0 1
                        new Vector3(81.72f, 0.00f,  103.74f), // 2 3
                    };
                }
            }
            // 优先级：6 7 4 5 0 1 2 3
            var prio = new[] { 6, 7, 4, 5, 0, 1, 2, 3 };

            // 1) 找到所有带 4974 的人（用 GetParty + HasStatus）
            var buffed = sa.GetParty()
                .Where(p => p != null && p.EntityId != 0 && p.HasStatus(BuffId))
                .ToList();

            if (buffed.Count == 0) return;

            // 2) 按 prio 排序（按 PartyList 的 index 排）
            var ordered = buffed
                .Select(p => new { Player = p, Idx = sa.Data.PartyList.IndexOf(p.EntityId) })
                .Where(x => x.Idx >= 0) // 找不到就丢掉
                .OrderBy(x => Array.IndexOf(prio, x.Idx)) // prio 里越靠前越优先
                .ToList();

            // prio 里没有的（Array.IndexOf 返回 -1）会排到最前面，所以要过滤掉
            ordered = ordered.Where(x => Array.IndexOf(prio, x.Idx) >= 0).ToList();

            if (ordered.Count == 0) return;

            // 3) 只给自己画：我必须在前4个里
            var mySlot = -1;
            for (int i = 0; i < Math.Min(4, ordered.Count); i++)
            {
                if (ordered[i].Player.EntityId == sa.Data.Me)
                {
                    mySlot = i;
                    break;
                }
            }
            if (mySlot < 0) return;

            var wpos = spots[mySlot];
            var dp = sa.WaypointDp(wpos, 6000, 0, "空中旋火指路P2");
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            
        }
        else if (_phase == 2 && _P2火圈次数 <= 7)
        {
            _P2火圈次数++;
            sa.Method.SendChat($"/e P2火圈次数 {_P2火圈次数}");
            
        }


        
    }

    // 破势乘浪
    [ScriptMethod(
        name: "破势乘浪",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:46542"]
    )]
    public async void 破势乘浪(Event evt, ScriptAccessory sa)
    {
        // await Task.Delay(3000);
        // 获得危险区位置
        var effectPos = evt.EffectPosition();
        var sourceRotation = evt.SourceRotation;
        // 画危险区
        var dpDanger = sa.Data.GetDefaultDrawProperties();
        dpDanger.Name = "破势乘浪-危险区";
        dpDanger.Position = effectPos;
        dpDanger.Color = sa.Data.DefaultDangerColor;
        dpDanger.DestoryAt = 8000;
        dpDanger.Scale = new Vector2(15f, 50f);
        dpDanger.Rotation = sourceRotation;
        dpDanger.FixRotation = true;
        dpDanger.ScaleMode = ScaleMode.None;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dpDanger);

        // 获得 DataId 19290的对象的位置和朝向
        var obj = sa.Data.Objects.FirstOrDefault(o => o.DataId == 19290);
        if (obj == null) return;

        var myIdx = sa.MyIndex();

        // 以 obj 朝向构建局部坐标（y 固定 0）
        var objPos = new Vector3(obj.Position.X, 0f, obj.Position.Z);
        var rot = obj.Rotation;

        // forward: 朝向；left/right: 垂直于朝向
        var forward = new Vector3((float)Math.Sin(rot), 0f, (float)Math.Cos(rot));
        if (forward.LengthSquared() < 1e-6f) forward = new Vector3(0f, 0f, 1f);
        forward = Vector3.Normalize(forward);

        var right = new Vector3(-forward.Z, 0f, forward.X);
        right = Vector3.Normalize(right);
        var left = -right;
        var back = -forward;

        Vector3 wpos;

        if (_分摊分散 == 1)
        {
            sa.Method.EdgeTTS("分摊");
            // 双奶画分摊圈
            if (_phase == 1)
            {
                for (int i = 2; i < 4; i++)
                {
                    var oid = sa.Data.PartyList[i];
                    if (oid == 0) continue;

                    var dpCircle = sa.Data.GetDefaultDrawProperties();
                    dpCircle.Name = $"分摊圈_{oid:X}";
                    dpCircle.Owner = oid;
                    dpCircle.Color = sa.Data.DefaultSafeColor;
                    dpCircle.DestoryAt = 10000;
                    dpCircle.Scale = new Vector2(5f);
                    dpCircle.ScaleMode = ScaleMode.ByTime;

                    sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dpCircle);
                }
            }
            else if (_phase == 2)
            {
                // P2 分摊只画有BUFF的治疗
                var buffId = 4975u;
                for (int i = 2; i < 4; i++)
                {
                    var oid = sa.Data.PartyList[i];
                    var p = sa.GetParty().FirstOrDefault(x => x != null && x.EntityId == oid);
                    if (p == null || oid == 0 || !p.HasStatus(buffId))
                        continue;

                    var dpCircle = sa.Data.GetDefaultDrawProperties();
                    dpCircle.Name = $"分摊圈_{oid:X}";
                    dpCircle.Owner = oid;
                    dpCircle.Color = sa.Data.DefaultSafeColor;
                    dpCircle.DestoryAt = 10000;
                    dpCircle.Scale = new Vector2(5f);
                    dpCircle.ScaleMode = ScaleMode.ByTime;

                    sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dpCircle);
                }
            }
           
            var dangerCenter = new Vector3(effectPos.X, 0f, effectPos.Z);
            float dangerYaw = sourceRotation;
            float dangerW = 15f;
            float dangerL = 50f;

            // 分摊用的“车道距离”：左(-12) / 中(0) / 右(+12)
            float lane = 15f;

            // 分摊只需要判断“后12”这一排会不会被危险区盖住
            float[] fbSamples = { -15f };  // forward * (-12) 等价于 back * 12

            // 选出给偶数组(0/2/4/6)与奇数组(1/3/5/7)的车道
            RayHelpers.PickSafeLanesByDangerRect(
                objPos,
                right,       // obj 的 right
                forward,     // obj 的 forward
                dangerCenter,
                dangerYaw,
                dangerW,
                dangerL,
                lane,
                fbSamples,
                out var evenLane,
                out var oddLane
            );

            bool isOdd = (myIdx % 2) == 1;
            float laneOffset = isOdd ? oddLane : evenLane;
            if (_phase == 1)
            {
                // 分摊固定：后15
                float fbDist = -15f;

                wpos = objPos + right * laneOffset + forward * fbDist;
                wpos = new Vector3(wpos.X, 0f, wpos.Z);

                var dp = sa.WaypointDp(wpos, 7000, 0, "破势乘浪-分摊指路");
                sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }

            sa.Method.SendChat($"/e 分摊 idx={myIdx} evenLane={evenLane:F2} oddLane={oddLane:F2}");
        }
        else
        {
            sa.Method.EdgeTTS("分散");
            // 所有人画 5m 的危险圈
            if (_phase == 1)
            {
                 foreach (var oid in sa.Data.PartyList) // 这里的 oid 就是 uint
                {
                    if (oid == 0) continue;

                    var dpCircle = sa.Data.GetDefaultDrawProperties();
                    dpCircle.Name = $"分散圈_{oid:X}";
                    dpCircle.Owner = oid;
                    dpCircle.Color = sa.Data.DefaultDangerColor;
                    dpCircle.DestoryAt = 10000;
                    dpCircle.Scale = new Vector2(5f);
                    dpCircle.ScaleMode = ScaleMode.ByTime;

                    sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dpCircle);
                }
            }
            else if (_phase == 2)
            {
                // P2 分散只画有BUFF的人
                var buffId = 4975u;
                foreach (var p in sa.GetParty())
                {
                    if (p == null || p.EntityId == 0 || !p.HasStatus(buffId))
                        continue;

                    var dpCircle = sa.Data.GetDefaultDrawProperties();
                    dpCircle.Name = $"分散圈_{p.EntityId:X}";
                    dpCircle.Owner = p.EntityId;
                    dpCircle.Color = sa.Data.DefaultDangerColor;
                    dpCircle.DestoryAt = 10000;
                    dpCircle.Scale = new Vector2(5f);
                    dpCircle.ScaleMode = ScaleMode.ByTime;

                    sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dpCircle);
                }
            }
           

            // 分散：按 idx 映射
            // 3/5/1/7: 左15，前后：+8 / -2 / -12 / -18
            // 2/4/0/6: 右15，前后：+8 / -2 / -12 / -18
            // 这里把 “前” 记为 +forward，“后” 记为 -forward
            float lane = 15f;
            float[] fbSamples = { +8f, -2f, -12f, -18f };

            var dangerCenter = new Vector3(effectPos.X, 0f, effectPos.Z);
            float dangerYaw = sourceRotation;
            float dangerW = 15f;
            float dangerL = 50f;

            RayHelpers.PickSafeLanesByDangerRect(
                objPos,
                right,        // 注意：这里用 obj 的 right（你上面算出来的 Vector3 right）
                forward,      // 用 obj 的 forward
                dangerCenter,
                dangerYaw,
                dangerW,
                dangerL,
                lane,
                fbSamples,
                out var evenLane,
                out var oddLane
            );
            // ====== 每个人前后位置还是按 idx 映射 ======
            float fbDist;
            if (myIdx == 3) fbDist = +8f;
            else if (myIdx == 5) fbDist = -2f;
            else if (myIdx == 1) fbDist = -12f;
            else if (myIdx == 7) fbDist = -18f;
            else if (myIdx == 2) fbDist = +8f;
            else if (myIdx == 4) fbDist = -2f;
            else if (myIdx == 0) fbDist = -12f;
            else if (myIdx == 6) fbDist = -18f;
            else fbDist = -12f;

            bool isOdd = (myIdx % 2) == 1;
            float laneOffset = isOdd ? oddLane : evenLane;

            if (_phase == 1)
            {
                wpos = objPos + right * laneOffset + forward * fbDist;
                wpos = new Vector3(wpos.X, 0f, wpos.Z);

                var dp = sa.WaypointDp(wpos, 7000, 0, "破势乘浪-分散指路");
                sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }
            
            sa.Method.SendChat($"/e 分散 idx={myIdx} evenLane={evenLane:F2} oddLane={oddLane:F2}");
        }
    }

    // 水基佬分摊分散记录
    [ScriptMethod(
        name: "水基佬分摊分散记录",
        eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:2056"],
        userControl: false
    )
    ]
    public void 水基佬分摊分散记录(Event evt, ScriptAccessory sa)
    {
        if (!int.TryParse(evt["Param"], out var param)) return;
        _分摊分散 = param switch
        {
            1005 => 1, // 分摊
            1006 => 2  // 分散
        };
        sa.Method.EdgeTTS(_分摊分散 == 1 ? "待会儿分摊" : "待会儿分散");
    }

    // 击退
    [ScriptMethod(
        name: "惊涛击退画图",
        eventType: EventTypeEnum.SetObjPos,
        eventCondition: ["SourceDataId:19290"]
    )]
    public async void 惊涛击退(Event evt, ScriptAccessory sa)
    {
        var duration = 19000;
        var rotation = evt.SourceRotation;

        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = "击退方向";
        dp.Owner = sa.Data.Me;
        dp.Color = new Vector4 (1f, 1f, 0, 1f); // yellow
        dp.DestoryAt = duration;
        dp.Scale = new Vector2(1f, 10f);
        dp.Rotation = rotation;
        dp.FixRotation = true;
        dp.ScaleMode = ScaleMode.None;

        if (_phase == 1)
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
        
        if (_phase == 2)
        {
            await Task.Delay(30000);
            dp.DestoryAt = 10000;
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
        } 
        

    }

    // ===== 水波P2指路补完：角色/角度工具 =====
    private enum WaterRole
    {
        Tank,
        Healer,
        Melee,
        Ranged
    }

    private static WaterRole GetRoleByPartyIndex(int idx)
    {
        // 默认按你常用的小队排序：
        // 0/1 = T, 2/3 = H, 4/5 = 近战, 6/7 = 远程
        if (idx == 0 || idx == 1) return WaterRole.Tank;
        if (idx == 2 || idx == 3) return WaterRole.Healer;
        if (idx == 4 || idx == 5) return WaterRole.Melee;
        return WaterRole.Ranged;
    }

    private static Vector3 DirFromYaw(float yaw)
    {
        // 与你脚本其它地方保持一致：forward = (sin(yaw), cos(yaw))
        return new Vector3(MathF.Sin(yaw), 0f, MathF.Cos(yaw));
    }

    private static float YawFromForward(Vector3 fwd)
    {
        // 与你 YawFromDir 同一套：yaw = atan2(x, z)
        var d = new Vector2(fwd.X, fwd.Z);
        if (d.LengthSquared() < 1e-6f) return 0f;
        d = Vector2.Normalize(d);
        return MathF.Atan2(d.X, d.Y);
    }

    private static float DegToRad(float deg) => deg * (MathF.PI / 180f);

    // 约定：0=南，90=东，180=北，270=西
    // 北偏东 = 180 - t；南偏东 = 0 + t
    private static float YawDeg_N_By_E(float t) => 180f - t;
    private static float YawDeg_S_By_E(float t) => 0f + t;

    // 北偏西 = 180 + t；南偏西 = 360 - t
    private static float YawDeg_N_By_W(float t) => 180f + t;
    private static float YawDeg_S_By_W(float t) => 360f - t;

    private static float Normalize180(float deg)
    {
        deg %= 360f;
        if (deg > 180f) deg -= 360f;
        if (deg < -180f) deg += 360f;
        return deg;
    }
    private float GetWaterOffsetRad(P2第一轮打法 strat, bool zHigh, WaterRole role, int 左右)
    {
        float yawDeg = 90f; // 默认给个值
        float baseDeg = (左右 == 1) ? 90f : 270f; // 左=东基准；右=西基准

        if (左右 == 1)
        {
            // ===== 你原来的“左(东基准)”逻辑不动 =====
            if (strat == P2第一轮打法.水波)
            {
                if (zHigh)
                {
                    yawDeg = role switch
                    {
                        WaterRole.Tank   => YawDeg_S_By_E(5f),
                        WaterRole.Melee  => YawDeg_N_By_E(5f),
                        WaterRole.Ranged => YawDeg_S_By_E(45f),
                        WaterRole.Healer => YawDeg_S_By_E(30f),
                        _ => 90f
                    };
                }
                else
                {
                    yawDeg = role switch
                    {
                        WaterRole.Melee  => YawDeg_N_By_E(5f),
                        WaterRole.Tank   => YawDeg_S_By_E(5f),
                        WaterRole.Healer => YawDeg_N_By_E(45f),
                        WaterRole.Ranged => YawDeg_N_By_E(30f),
                        _ => 90f
                    };
                }
            }
            else // 镜像水波
            {
                if (zHigh)
                {
                    yawDeg = role switch
                    {
                        WaterRole.Tank   => YawDeg_N_By_E(5f),
                        WaterRole.Melee  => YawDeg_S_By_E(5f),
                        WaterRole.Ranged => YawDeg_S_By_E(35f),
                        WaterRole.Healer => YawDeg_S_By_E(45f),
                        _ => 90f
                    };
                }
                else
                {
                    yawDeg = role switch
                    {
                        WaterRole.Tank   => YawDeg_S_By_E(5f),
                        WaterRole.Melee  => YawDeg_N_By_E(5f),
                        WaterRole.Ranged => YawDeg_N_By_E(30f),
                        WaterRole.Healer => YawDeg_N_By_E(45f),
                        _ => 90f
                    };
                }
            }
        }
        else if (左右 == 2)
        {
            // ===== 右(西基准)：把 E 换成 W，其它结构保持一致 =====
            if (strat == P2第一轮打法.水波)
            {
                if (zHigh)
                {
                    yawDeg = role switch
                    {
                        WaterRole.Tank   => YawDeg_S_By_W(5f),
                        WaterRole.Melee  => YawDeg_N_By_W(5f),
                        WaterRole.Ranged => YawDeg_S_By_W(45f),
                        WaterRole.Healer => YawDeg_S_By_W(30f),
                        _ => 270f
                    };
                }
                else
                {
                    yawDeg = role switch
                    {
                        WaterRole.Melee  => YawDeg_N_By_W(5f),
                        WaterRole.Tank   => YawDeg_S_By_W(5f),
                        WaterRole.Healer => YawDeg_N_By_W(45f),
                        WaterRole.Ranged => YawDeg_N_By_W(30f),
                        _ => 270f
                    };
                }
            }
            else // 镜像水波
            {
                if (zHigh)
                {
                    yawDeg = role switch
                    {
                        WaterRole.Tank   => YawDeg_N_By_W(5f),
                        WaterRole.Melee  => YawDeg_S_By_W(5f),
                        WaterRole.Ranged => YawDeg_S_By_W(35f),
                        WaterRole.Healer => YawDeg_S_By_W(45f),
                        _ => 270f
                    };
                }
                else
                {
                    yawDeg = role switch
                    {
                        WaterRole.Tank   => YawDeg_S_By_W(5f),
                        WaterRole.Melee  => YawDeg_N_By_W(5f),
                        WaterRole.Ranged => YawDeg_N_By_W(30f),
                        WaterRole.Healer => YawDeg_N_By_W(45f),
                        _ => 270f
                    };
                }
            }
        }

        // 最终偏移角：左用东(90)，右用西(270)
        float offDeg = Normalize180(yawDeg - baseDeg);
        return DegToRad(offDeg);
    }

    // 水基佬水波
    [ScriptMethod(
        name: "水波画图",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(46557|46560)$"]
    )]
    public async void 水波画图(Event evt, ScriptAccessory sa)
    {
        var actionId = evt["ActionId"];

        // 给队里所有人画扇形 from boss to oid
        if (!int.TryParse(evt["DurationMilliseconds"], out var duration)) return;
        if (_phase == 1)
        {
            foreach (var oid in sa.Data.PartyList)
            {
                var sourceId = evt.SourceId();
                if (oid == 0) continue;

                var dp = sa.Data.GetDefaultDrawProperties();
                dp.Name = $"水波扇形_{oid:X}";
                dp.Owner = evt.SourceId();
                dp.TargetObject = oid;
                dp.Color = sa.Data.DefaultDangerColor;
                dp.DestoryAt = (uint)duration;
                dp.Scale = new Vector2(60f, 60f); // radius, length
                dp.ScaleMode = ScaleMode.ByTime;
                dp.Radian = float.Pi / 6f; // 30 degrees

                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
            }
        }
        else if (_phase == 2)
        {
            // 只在身上有statusid 4975的人画扇形
            const uint BuffId = 4975;
            var buffed = sa.GetParty()
                .Where(p => p != null && p.EntityId != 0 && p.HasStatus(BuffId))
                .ToList();

            if (buffed.Count == 0) return;
            foreach (var p in buffed)
            {
                var oid = p.EntityId;

                var dp = sa.Data.GetDefaultDrawProperties();
                dp.Name = $"水波扇形_{oid:X}";
                dp.Owner = evt.SourceId();
                dp.TargetObject = oid;
                dp.Color = sa.Data.DefaultDangerColor;
                dp.DestoryAt = (uint)duration;
                dp.Scale = new Vector2(60f, 60f); // radius, length
                dp.ScaleMode = ScaleMode.ByTime;
                dp.Radian = float.Pi / 6f; // 30 degrees

                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
            }

        }
        
        var myIdx = sa.MyIndex();
        var bossId = evt.SourceId();
        var bossObj = sa.Data.Objects.FirstOrDefault(o => o.GameObjectId == bossId);
        // boss 位置（y 固定 0）
        var bossPos = new Vector3(bossObj.Position.X, 0f, bossObj.Position.Z);

        if (bossObj == null) return;
        // 画图，0去boss正前面，1去boss正后，2去boss左侧，3去boss右侧，4去boss左后，5去boss右后，6去boss左前，7去boss右前。去的位置都是以boss为中心，距离4m处
        if (_phase == 1)
        {
            // boss 朝向 -> forward/right（y 固定 0）
            var rot = bossObj.Rotation;
            var fwd = new Vector3((float)Math.Sin(rot), 0f, (float)Math.Cos(rot));
            if (fwd.LengthSquared() < 1e-6f) fwd = new Vector3(0f, 0f, 1f);
            fwd = Vector3.Normalize(fwd);

            var right = new Vector3(-fwd.Z, 0f, fwd.X);
            right = Vector3.Normalize(right);

            var left = -right;
            var back = -fwd;

            float r = 4f; // 距离 4m

            Vector3 wpos = myIdx switch
            {
                0 => bossPos + fwd * r,                        // 正前
                1 => bossPos + back * r,                       // 正后
                2 => bossPos + left * r,                       // 左
                3 => bossPos + right * r,                      // 右
                4 => bossPos + Vector3.Normalize(left + back) * r,   // 左后
                5 => bossPos + Vector3.Normalize(right + back) * r,  // 右后
                6 => bossPos + Vector3.Normalize(left + fwd) * r,    // 左前
                7 => bossPos + Vector3.Normalize(right + fwd) * r,   // 右前
                _ => bossPos
            };

            wpos = new Vector3(wpos.X, 0f, wpos.Z);

            // 指路（持续时间用同一个 duration）
            var dpWp = sa.WaypointDp(wpos, (uint)duration, 0, "水波站位指路");
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpWp);
        }
        else if (_phase >= 2)
        {
            const uint BuffId = 4975;
            float r = 4f;
            // 判断boss在左半场和右半场：x<100左以东基准，x>100右以西基准
            _P2左右 = bossPos.X < 100f ? 1 : 2;
            sa.Method.SendChat($"/e P2左右");
            sa.Method.SendChat($"/e P2左右={_P2左右}");
            // 只在身上有 4975 的人参与分配
            var buffed = sa.GetParty()
                .Where(p => p != null && p.EntityId != 0 && p.HasStatus(BuffId))
                .ToList();

            if (buffed.Count == 0) return;

            // 只给自己画（你之前其它指路也是这个风格）
            if (!buffed.Any(p => p.EntityId == sa.Data.Me)) return;

            // 判断 boss 在上半场还是下半场：z>100
            bool zHigh = bossPos.Z > 100f;

            var myIdx2 = sa.MyIndex();
            if (myIdx2 < 0) return;

            var role = GetRoleByPartyIndex(myIdx2);
            float baseYaw;
            if (_P2左右 == 1)
            {
                // 左半场，以东为基准
                baseYaw = MathF.PI / 2f;
            }
            else
            {
                // 右半场，以西为基准
                baseYaw = -MathF.PI / 2f;
            }
            // float baseYaw = MathF.PI / 2f;

            float offRad = GetWaterOffsetRad(p2第一轮打法, zHigh, role, _P2左右);
            float yaw = baseYaw + offRad;

            var dir = DirFromYaw(yaw);           // (sin(yaw), 0, cos(yaw))
            var wpos2 = bossPos + dir * r;
            wpos2 = new Vector3(wpos2.X, 0f, wpos2.Z);

            var dpWp2 = sa.WaypointDp(wpos2, (uint)duration, 0, "水波站位指路P2");
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpWp2);
        }

        

        await Task.Delay(duration);
        // 记录每个玩家位置
        // 然后// --- 情况 A: 46558 (保持原方向 往每个玩家位置喷30度) ---  // --- 情况 B: 46560 (左右偏转 24度，各画一个 15度扇形) ---
        var snapPos = new Dictionary<uint, Vector3>();
        foreach (var oid in sa.Data.PartyList)
        {
            if (oid == 0) continue;

            var obj = sa.Data.Objects.FirstOrDefault(o => o.GameObjectId == oid);
            if (obj == null) continue;

            snapPos[oid] = new Vector3(obj.Position.X, 0f, obj.Position.Z);
        }

        // 重新抓一次 boss（防止 cast 期间移动）
        bossObj = sa.Data.Objects.FirstOrDefault(o => o.GameObjectId == bossId);
        if (bossObj == null) return;

        bossPos = new Vector3(bossObj.Position.X, 0f, bossObj.Position.Z);

        // ============= 4) 根据 actionId 画后续喷射 =============
        const uint afterMs = 3500; // 后续扇形显示时间，你想短点/长点都行
        const float radius = 60f;
        const float length = 60f;

        static float YawFromDir(Vector2 d)
        {
            // 与你 sin/cos 的 forward 定义一致：yaw=atan2(x,z)
            if (d.LengthSquared() < 1e-6f) return 0f;
            d = Vector2.Normalize(d);
            return MathF.Atan2(d.X, d.Y);
        }

        void DrawFanFixed(string name, float yaw, float radian)
        {
            var dp = sa.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Position = bossPos;               // 固定从 boss 位置发射
            dp.Rotation = yaw;
            dp.FixRotation = true;
            dp.Color = sa.Data.DefaultDangerColor;
            dp.DestoryAt = afterMs;
            dp.Scale = new Vector2(radius, length);
            dp.ScaleMode = ScaleMode.ByTime;
            dp.Radian = radian;
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }

        if (actionId == "46557")
        {
            // --- 情况 A: 保持原方向，往每个玩家当时位置喷 30度 ---
            if (_phase == 1)
            {
                foreach (var kv in snapPos)
                {
                    var oid = kv.Key;
                    var p = kv.Value;

                    var dir = new Vector2(p.X - bossPos.X, p.Z - bossPos.Z);
                    var yaw = YawFromDir(dir);

                    DrawFanFixed($"水波喷射A_{oid:X}", yaw, float.Pi / 6f); // 30°
                }
            }
            else if (_phase == 2)
            {
                const uint BuffId = 4975;
                var buffed = sa.GetParty()
                    .Where(p => p != null && p.EntityId != 0 && p.HasStatus(BuffId))
                    .ToList();
                if (buffed.Count == 0) return;
                foreach (var p in buffed)
                {
                    var oid = p.EntityId;
                    if (!snapPos.TryGetValue(oid, out var pos)) continue;

                    var dir = new Vector2(pos.X - bossPos.X, pos.Z - bossPos.Z);
                    var yaw = YawFromDir(dir);

                    DrawFanFixed($"水波喷射A_{oid:X}", yaw, float.Pi / 6f); // 30°
                }
            }
        }
        else if (actionId == "46560")
        {
            // --- 情况 B: 左右偏转 24度，各画一个 15度扇形 ---
            float off = 24f * (float)Math.PI / 180f;   // 24°
            float rad = 15f * (float)Math.PI / 180f;   // 15°
            if (_phase == 1)
            {
                foreach (var kv in snapPos)
                {
                    var oid = kv.Key;
                    var p = kv.Value;

                    var dir = new Vector2(p.X - bossPos.X, p.Z - bossPos.Z);
                    var baseYaw = YawFromDir(dir);

                    DrawFanFixed($"水波喷射B_L_{oid:X}", baseYaw - off, rad);
                    DrawFanFixed($"水波喷射B_R_{oid:X}", baseYaw + off, rad);
                }
            }
            else if (_phase == 2)
            {
                const uint BuffId = 4975;
                var buffed = sa.GetParty()
                    .Where(p => p != null && p.EntityId != 0 && p.HasStatus(BuffId))
                    .ToList();
                if (buffed.Count == 0) return;
                foreach (var p in buffed)
                {
                    var oid = p.EntityId;
                    if (!snapPos.TryGetValue(oid, out var pos)) continue;

                    var dir = new Vector2(pos.X - bossPos.X, pos.Z - bossPos.Z);
                    var baseYaw = YawFromDir(dir);

                    DrawFanFixed($"水波喷射B_L_{oid:X}", baseYaw - off, rad);
                    DrawFanFixed($"水波喷射B_R_{oid:X}", baseYaw + off, rad);
                }
            }
        }
    }

    [ScriptMethod(name: "狂浪腾空", eventType: EventTypeEnum.EnvControl, eventCondition: ["Index:regex:^\\d+$"])]
    public void 狂浪腾空(Event evt, ScriptAccessory sa)
    {
        if (!EnvHelpers.TryGetGridIndexFlag(evt, out var index, out var flag)) return;
        if (!EnvHelpers.TryGetGridPos(index, out var gridPos)) return;

        var baseName = $"九宫格_{index}";

        // 仅 flag=4/8 时清除并退出（保持原逻辑）
        if (EnvHelpers.IsClearFlag(flag))
        {
            EnvHelpers.ClearGridDraw(sa, baseName);
            return;
        }

        var drawColor = sa.Data.DefaultDangerColor;

        switch (EnvHelpers.GetMechanic(flag))
        {
            case EnvHelpers.GridMechanic.Tankbuster:
                EnvHelpers.DrawTB(sa, baseName, gridPos, drawColor);
                break;
            case EnvHelpers.GridMechanic.Stack:
                EnvHelpers.DrawStack(sa, baseName, gridPos, sa.Data.DefaultSafeColor);
                break;
            case EnvHelpers.GridMechanic.Spread:
                EnvHelpers.DrawSpread(sa, baseName, gridPos, drawColor);
                break;
            default:
                break;
        }
    }

    [ScriptMethod(
        name: "浪尖转体分摊分散",
        eventType: EventTypeEnum.EnvControl,
        eventCondition: ["Index:regex:^\\d+$"]
    )]
    public async void 浪尖转体分摊分散(Event evt, ScriptAccessory sa)
    {
        if (!int.TryParse(evt["Index"], out var index)) return;
        if (!int.TryParse(evt["Flag"], out var flag)) return;

        if (index != 4)
            return;

        if (flag == 2048)
        {
            sa.Method.EdgeTTS("待会儿分散");
            
            await Task.Delay(5000);
            // 有BUFF的人画圈
            const uint BuffId = 4975;
            var buffed = sa.GetParty()
                .Where(p => p != null && p.EntityId != 0 && p.HasStatus(BuffId))
                .ToList();
            if (buffed.Count == 0) return;
            foreach (var p in buffed)
            {
                var oid = p.EntityId;

                var dpCircle = sa.Data.GetDefaultDrawProperties();
                dpCircle.Name = $"浪尖转体分散圈_{oid:X}";
                dpCircle.Owner = oid;
                dpCircle.Color = sa.Data.DefaultDangerColor;
                dpCircle.DestoryAt = 5000;
                dpCircle.Scale = new Vector2(5f);
                dpCircle.ScaleMode = ScaleMode.ByTime;

                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dpCircle);
            }
        }
        else
        {
            sa.Method.EdgeTTS("待会儿分摊");
        }
    }

    // 浪尖转体
    [ScriptMethod(
        name: "浪尖转体",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(46547|46488)$"]
    )]
    public void 浪尖转体(Event evt, ScriptAccessory sa)
    {
        var sourcePos = evt.SourcePosition();
        var sourceRot = evt.SourceRotation;

        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = "浪尖转体-扇形";
        dp.Position = sourcePos;
        dp.Rotation = sourceRot;
        dp.FixRotation = true;
        dp.Color = sa.Data.DefaultDangerColor;
        dp.DestoryAt = 6500;
        dp.Radian = (float)Math.PI * 2f / 3f; // 120 degrees
        dp.Scale = new Vector2(50f);
        dp.ScaleMode = ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

        _浪花位置 = sourcePos;

        // 指路，如果拥有buff 4975继续，否则不指路
        // 如果是近战优化 ，根据sourcePos和sourceRot
            // 如果_P2左右== 1，并且sourcePos.Z小于100 所有人去{89, 0.00, 81}
            // 如果_P2左右== 1，并且sourcePos.Z大于100 所有人去{89, 0.00, 119}

            // 如果_P2左右== 2，并且sourcePos.Z小于100 所有人去{111, 0.00, 81}
            // 如果_P2左右== 2，并且sourcePos.Z大于100 所有人去{111, 0.00, 119}
        // 水
        const uint BuffId = 4975;
        var hasBuff = sa.GetParty()
            .Any(p => p != null && p.EntityId == sa.Data.Me && p.HasStatus(BuffId));
        if (hasBuff)
        {
            if (p2第二三轮打法 == P2第二三轮打法.近战优化)
            {
                Vector3 wpos;
                if (_phase >= 2)
                {
                    if (_P2左右 == 1)
                    {
                        if (sourcePos.Z < 100f)
                            wpos = new Vector3(89f, 0f, 81f);
                        else
                            wpos = new Vector3(89f, 0f, 119f);
                    }
                    else
                    {
                        if (sourcePos.Z < 100f)
                            wpos = new Vector3(111f, 0f, 81f);
                        else
                            wpos = new Vector3(111f, 0f, 119f);
                    }

                    var dp2 = sa.WaypointDp(wpos, 6000, 0, "浪尖转体-指路P2");
                    sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp2);
                }
                else
                {
                    // 原始逻辑：所有人都去 boss 前方 10m 处
                    var forward = new Vector3(MathF.Sin(sourceRot), 0f, MathF.Cos(sourceRot));
                    forward = Vector3.Normalize(forward);
                    wpos = sourcePos + forward * 10f;
                    wpos = new Vector3(wpos.X, 0f, wpos.Z);

                    var dp3 = sa.WaypointDp(wpos, 6000, 0, "浪尖转体-指路");
                    sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp3);
                }
            }
            else
            {
                // 美野

            }
        }

        // 火
        var myIdx = sa.MyIndex();
        const uint 火BuffId = 4974;
        if (p2第二三轮打法 == P2第二三轮打法.近战优化)
        {
            // 指路，如果拥有buff 4975继续，否则不指路
                // 如果_P2左右== 1，并且 _浪花位置.Z小于100 按照优先级0 1 4 5 2 3 6 7, 分别去{116.83, 0.00, 81.5} {109.62, 0.00, 81.5} {90.4, 0.00, 81.5} {82.50, 0.00, 81.5}
                // 如果_P2左右== 1，并且 _浪花位置.Z大于100 按照优先级0 1 4 5 2 3 6 7, 分别去{116.83, 0.00, 118.5} {109.62, 0.00, 118.5} {90.4, 0.00, 118.5} {82.50, 0.00, 118.5}


                // 如果_P2左右== 2，并且 _浪花位置.Z小于100 按照优先级0 1 4 5 2 3 6 7, 分别去{82.50, 0.00, 81.5} {90.4, 0.00, 81.5} {109.62, 0.00, 81.5} {116.83, 0.00, 81.5}
                // 如果_P2左右== 2，并且 _浪花位置.Z大于100 按照优先级0 1 4 5 2 3 6 7 分别去{82.50, 0.00, 118.5} {90.4, 0.00, 118.5} {109.62, 0.00, 118.5} {116.83, 0.00, 118.5}
            // 指路，如果拥有buff 4975继续，否则不指路
            sa.Method.SendChat($"/e 火buff指路 myIdx={myIdx}");
            var has火Buff = sa.GetParty()
                .Any(p => p != null && p.EntityId == sa.Data.Me && p.HasStatus(火BuffId));
            if (!has火Buff) return;

            // 找到自己在小队里的 index（0~7）
            if (myIdx < 0 || myIdx > 7) return;

            // 优先级顺序：0 1 4 5 2 3 6 7
            // 对应分组： (0,1)->组0, (4,5)->组1, (2,3)->组2, (6,7)->组3
            int group = myIdx switch
            {
                0 or 1 => 0,
                4 or 5 => 1,
                2 or 3 => 2,
                _      => 3, // 6 or 7
            };
            sa.Method.SendChat($"/e 火buff指路组={group}, P2左右={_P2左右}");
            // 根据 _浪花位置.Z 选择上下
            bool zHigh = _浪花位置.Z > 100f;
            float z = zHigh ? 118.5f : 81.5f;

            // 根据 _P2左右 选择左右（x 的排列方向）
            // _P2左右==1: 组0/1/2/3 => 116.83, 109.62, 90.4, 82.50
            // _P2左右==2: 组0/1/2/3 => 82.50, 90.4, 109.62, 116.83
            float[] xs = _P2左右 == 1
                ? new float[] { 82.50f, 90.4f, 109.62f, 116.83f }
                : new float[] { 116.83f, 109.62f, 90.4f, 82.50f };

            var wpos = new Vector3(xs[group], 0f, z);

            var dp2 = sa.WaypointDp(wpos, 6000, 0, "浪尖转体-指路P2(近战优化)");
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp2);
        }
        else
        {
            // 美
        }
        
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

    public static DrawPropertiesEdit FastDpWithPosition(this ScriptAccessory sa, string name, Vector3 pos, uint duration, Vector2 scale, bool safe = false)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = safe ? sa.Data.DefaultSafeColor : sa.Data.DefaultDangerColor;
        dp.Position = pos;
        dp.DestoryAt = duration;
        dp.Scale = scale;
        return dp;
    }

    public static DrawPropertiesEdit FastDpWithTarget(this ScriptAccessory sa, string name, uint targetId, uint duration, Vector2 scale, bool safe = false)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = safe ? sa.Data.DefaultSafeColor : sa.Data.DefaultDangerColor;
        dp.Owner = targetId;
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
    public static IEnumerable<IPlayerCharacter> GetParty(this ScriptAccessory sa)
    {
        foreach (var pid in sa.Data.PartyList)
        {
            var obj = sa.Data.Objects.SearchByEntityId(pid);
            if (obj is IPlayerCharacter character) yield return character;
        }
    }
}

internal static class RayHelpers
{
    private static readonly Vector3 Center = new(100f, 0f, 100f);
    private const uint 炽红 = 12345;
    public static bool IsCoveredByRayXZ(Vector3 point, Vector3 bossPos, Vector2 dirXZ, float halfWidth)
    {
        var d2 = dirXZ.LengthSquared();
        if (d2 < 0.0001f) return false;

        var vx = point.X - bossPos.X;
        var vz = point.Z - bossPos.Z;

        var t = (vx * dirXZ.X + vz * dirXZ.Y) / d2;
        if (t < 0f) return false;

        var cross = dirXZ.X * vz - dirXZ.Y * vx;
        var dist = MathF.Abs(cross) / MathF.Sqrt(d2);

        return dist <= halfWidth;
    }

    public static Vector3 AdjustPointDownUntilNotCoveredXZ(
        Vector3 p0, Vector3 bossPos, Vector2 dirXZ,
        float halfWidth, float epsilon, float maxDown)
    {
        if (!IsCoveredByRayXZ(p0, bossPos, dirXZ, halfWidth))
            return p0;

        var dx = dirXZ.X;
        var dz = dirXZ.Y;
        var dLen = MathF.Sqrt(dx * dx + dz * dz);
        if (dLen < 0.0001f) return p0;

        if (MathF.Abs(dx) < 1e-4f)
        {
            var z = p0.Z;
            var minZ = p0.Z - maxDown;
            while (z > minZ)
            {
                z -= 0.1f;
                var p = new Vector3(p0.X, p0.Y, z);
                if (!IsCoveredByRayXZ(p, bossPos, dirXZ, halfWidth))
                    return p;
            }
            return new Vector3(p0.X, p0.Y, minZ);
        }

        var x = p0.X;
        var bz = bossPos.Z;
        var bx = bossPos.X;

        var C = -dx * bz - dz * (x - bx);
        var K = halfWidth * dLen;

        var z1 = ( K - C) / dx;
        var z2 = (-K - C) / dx;

        var low = MathF.Min(z1, z2);
        var high = MathF.Max(z1, z2);

        var z0 = p0.Z;
        if (z0 >= low && z0 <= high)
        {
            var zNew = low - epsilon;
            var pTry = new Vector3(x, p0.Y, zNew);
            if (!IsCoveredByRayXZ(pTry, bossPos, dirXZ, halfWidth))
                return pTry;

            zNew -= 0.2f;
            return new Vector3(x, p0.Y, zNew);
        }

        var zFallback = z0;
        var minZFb = z0 - maxDown;
        while (zFallback > minZFb)
        {
            zFallback -= 0.1f;
            var p = new Vector3(x, p0.Y, zFallback);
            if (!IsCoveredByRayXZ(p, bossPos, dirXZ, halfWidth))
                return p;
        }
        return new Vector3(x, p0.Y, minZFb);
    }

    public static bool TryGetBossBasis(
        ScriptAccessory sa,
        uint bossDataId,
        Vector3 center,
        out Vector3 bossPos,
        out Vector2 forwardXZ,
        out Vector2 rightXZ)
    {
        bossPos = default;
        forwardXZ = default;
        rightXZ = default;

        var bossObj = sa.Data.Objects.FirstOrDefault(o => o.DataId == bossDataId);
        if (bossObj == null) return false;

        bossPos = bossObj.Position;

        // forward = 指向中心（面向场内）
        var f = new Vector2(center.X - bossPos.X, center.Z - bossPos.Z);
        if (f.LengthSquared() < 0.0001f) return false;
        f = Vector2.Normalize(f);

        // right = forward 的右侧（确保和你给的例子匹配）
        var r = new Vector2(-f.Y, f.X);

        forwardXZ = f;
        rightXZ = r;
        return true;
    }

    public static Vector3 BossRelativePoint(
        Vector3 bossPos,
        Vector2 forwardXZ,
        Vector2 rightXZ,
        float rightDist,
        float upDist)
    {
        var x = bossPos.X + rightXZ.X * rightDist + forwardXZ.X * upDist;
        var z = bossPos.Z + rightXZ.Y * rightDist + forwardXZ.Y * upDist;
        return new Vector3(x, 0f, z);
    }

    public static Vector3 AdjustPointAlongDirUntilNotCoveredXZ(
        Vector3 p0,
        Vector3 rayBossPos,
        Vector2 rayDirXZ,
        Vector2 moveDirXZ,   // IMPORTANT: normalized direction you want to move along
        float halfWidth,
        float epsilon,
        float maxMove)
    {
        if (!IsCoveredByRayXZ(p0, rayBossPos, rayDirXZ, halfWidth))
            return p0;

        var r2 = rayDirXZ.LengthSquared();
        if (r2 < 1e-6f) return p0;

        if (moveDirXZ.LengthSquared() < 1e-6f) return p0;
        var m = Vector2.Normalize(moveDirXZ);

        // v0 = p0 - boss
        var v0 = new Vector2(p0.X - rayBossPos.X, p0.Z - rayBossPos.Z);

        // cross(r, v) = rx*vz - rz*vx
        float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

        var r = rayDirXZ;
        var cross0 = Cross(r, v0);
        var rLen = MathF.Sqrt(r2);
        var K = halfWidth * rLen;

        // If moving direction doesn't change cross, stepping won't escape by distance -> fallback stepping
        var crossRM = Cross(r, m);

        // Also consider escaping by t<0 (behind boss on the ray direction)
        var dot0 = Vector2.Dot(v0, r);
        var dotMR = Vector2.Dot(m, r);

        float bestS = float.PositiveInfinity;

        // Candidate 1: escape by distance boundary |cross| > K
        if (MathF.Abs(crossRM) > 1e-6f)
        {
            // Solve cross(s) = ±K, where cross(s) = cross0 + crossRM * s
            var sA = ( K - cross0) / crossRM;
            var sB = (-K - cross0) / crossRM;

            void TryPick(float s)
            {
                if (s < 0) return;
                var s2 = s + epsilon; // step slightly past boundary
                if (s2 > maxMove) return;

                var px = p0.X + m.X * s2;
                var pz = p0.Z + m.Y * s2;
                var pTry = new Vector3(px, p0.Y, pz);
                if (!IsCoveredByRayXZ(pTry, rayBossPos, rayDirXZ, halfWidth))
                    bestS = MathF.Min(bestS, s2);
            }

            TryPick(sA);
            TryPick(sB);
        }

        // Candidate 2: escape by t<0 (move behind ray origin so t<0 => not covered)
        // t(s) = dot(v0 + m*s, r) / |r|^2
        // Need dot(v0 + m*s, r) < 0 => dot0 + dotMR*s < 0
        if (dot0 >= 0f && dotMR < -1e-6f)
        {
            var sT = (-dot0 / dotMR) + epsilon;
            if (sT >= 0f && sT <= maxMove)
            {
                var px = p0.X + m.X * sT;
                var pz = p0.Z + m.Y * sT;
                var pTry = new Vector3(px, p0.Y, pz);
                if (!IsCoveredByRayXZ(pTry, rayBossPos, rayDirXZ, halfWidth))
                    bestS = MathF.Min(bestS, sT);
            }
        }

        // If analytic fails, do small-step fallback
        if (float.IsInfinity(bestS))
        {
            float step = 0.1f;
            for (float s = step; s <= maxMove; s += step)
            {
                var px = p0.X + m.X * s;
                var pz = p0.Z + m.Y * s;
                var pTry = new Vector3(px, p0.Y, pz);
                if (!IsCoveredByRayXZ(pTry, rayBossPos, rayDirXZ, halfWidth))
                    return pTry;
            }
            // worst-case: clamp to maxMove
            return new Vector3(p0.X + m.X * maxMove, p0.Y, p0.Z + m.Y * maxMove);
        }

        return new Vector3(p0.X + m.X * bestS, p0.Y, p0.Z + m.Y * bestS);
    }

    public static Vector3 ClampXZ(Vector3 p, float min, float max)
    {
        var x = MathF.Max(min, MathF.Min(max, p.X));
        var z = MathF.Max(min, MathF.Min(max, p.Z));
        return new Vector3(x, 0f, z);
    }

    public static bool IsInsideRotRectXZ(Vector3 p, Vector3 rectCenter, float yaw, float width, float length)
    {
        // width/length 这里按“全宽/全长”理解，所以半宽半长要 /2
        float hx = width * 0.5f;
        float hz = length * 0.5f;

        // forward / right in XZ
        var f = new Vector2(MathF.Sin(yaw), MathF.Cos(yaw)); // forward
        var r = new Vector2(-f.Y, f.X);                      // right

        var v = new Vector2(p.X - rectCenter.X, p.Z - rectCenter.Z);
        float localX = v.X * r.X + v.Y * r.Y; // right axis
        float localZ = v.X * f.X + v.Y * f.Y; // forward axis

        return MathF.Abs(localX) <= hx && MathF.Abs(localZ) <= hz;
    }

    /// <summary>
    /// 根据危险区（旋转矩形）判断三条车道：左(-lane), 中(0), 右(+lane) 哪些被覆盖。
    /// 返回给偶数组(0/2/4/6)与奇数组(1/3/5/7)的车道偏移值（沿 objRight 方向）。
    /// 优先级：左 -> 中 -> 右，从没覆盖的里面选两条不同的。
    /// </summary>
    public static void PickSafeLanesByDangerRect(
        Vector3 objPos,
        Vector3 objRight,
        Vector3 objForward,
        Vector3 dangerCenter,
        float dangerYaw,
        float dangerWidth,
        float dangerLength,
        float laneOffset,             // 13.86
        IReadOnlyList<float> fbSamples, // {+8,-2,-12,-22}
        out float evenLane,
        out float oddLane)
    {
        // lane candidates in priority order: left, center, right
        float[] lanes = { +laneOffset, 0f, -laneOffset };

        bool LaneCovered(float lane)
        {
            foreach (var fb in fbSamples)
            {
                var p = objPos + objRight * lane + objForward * fb;
                p = new Vector3(p.X, 0f, p.Z);
                if (IsInsideRotRectXZ(p, dangerCenter, dangerYaw, dangerWidth, dangerLength))
                    return true;
            }
            return false;
        }

        var safe = lanes.Where(l => !LaneCovered(l)).ToList();
        if (safe.Count == 0) safe.AddRange(lanes); // 兜底：全覆盖/算法异常时至少不崩

        evenLane = safe[0];
        oddLane = (safe.Count >= 2) ? safe[1] : safe[0];
    }

}

internal static class EnvHelpers
{
    public enum GridMechanic
    {
        None = 0,
        Tankbuster = 1,
        Stack = 2,
        Spread = 3,
    }

    public static bool TryGetGridIndexFlag(Event evt, out int index, out int flag)
    {
        index = 0;
        flag = 0;

        // 你原来的范围是 14~22（方法名写 15-21 但逻辑是 14-22，保持原样）
        if (!int.TryParse(evt["Index"], out index)) return false;
        if (index < 14 || index > 22) return false;

        if (!int.TryParse(evt["Flag"], out flag)) return false;
        return true;
    }

    public static bool TryGetGridPos(int index, out Vector3 pos)
    {
        // 14~22 = 3x3 grid:
        // 14 15 16  -> z=87,  x=87,100,113
        // 17 18 19  -> z=100, x=87,100,113
        // 20 21 22  -> z=113, x=87,100,113
        pos = Vector3.Zero;
        if (index < 14 || index > 22) return false;

        int i = index - 14;
        int row = i / 3; // 0..2
        int col = i % 3; // 0..2

        float x = 87f + 13f * col;
        float z = 87f + 13f * row;
        pos = new Vector3(x, 0f, z);
        return true;
    }

    public static bool IsClearFlag(int flag) => flag == 4 || flag == 8;

    public static bool IsWater(int flag) => flag == 2 || flag == 32 || flag == 128;

    public static GridMechanic GetMechanic(int flag)
    {
        // 保持原判定集合完全一致
        if (flag == 128 || flag == 8192) return GridMechanic.Tankbuster;
        if (flag == 32 || flag == 2048) return GridMechanic.Stack;
        if (flag == 2 || flag == 512) return GridMechanic.Spread;
        return GridMechanic.None;
    }

    public static void ClearGridDraw(ScriptAccessory sa, string baseName)
    {
        sa.Method.RemoveDraw($"{baseName}_TB");
        sa.Method.RemoveDraw($"{baseName}_Stack");
        for (int i = 1; i <= 4; i++)
            sa.Method.RemoveDraw($"{baseName}_Spread_{i}");
    }

    public static void SetupCommon(DrawPropertiesEdit dp, string name, Vector3 pos, Vector4 color)
    {
        dp.Name = name;
        dp.Position = pos;
        dp.Color = color;
        dp.DestoryAt = 9999999;
        dp.ScaleMode = ScaleMode.None;
    }

    public static void DrawTB(ScriptAccessory sa, string baseName, Vector3 pos, Vector4 color)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        SetupCommon(dp, $"{baseName}_TB", pos, color);

        dp.Scale = new Vector2(6f);
        dp.CentreResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
        dp.CentreOrderIndex = 1;

        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    public static void DrawStack(ScriptAccessory sa, string baseName, Vector3 pos, Vector4 color)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        SetupCommon(dp, $"{baseName}_Stack", pos, color);

        dp.Scale = new Vector2(60f);
        dp.Radian = float.Pi / 4;
        dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
        dp.TargetOrderIndex = 1;

        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    public static void DrawSpread(ScriptAccessory sa, string baseName, Vector3 pos, Vector4 color)
    {
        for (uint i = 1; i <= 4; i++)
        {
            var dp = sa.Data.GetDefaultDrawProperties();
            SetupCommon(dp, $"{baseName}_Spread_{i}", pos, color);

            dp.Scale = new Vector2(60f);
            dp.Radian = float.Pi / 4;
            dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
            dp.TargetOrderIndex = i;

            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }
    }
}


#endregion
