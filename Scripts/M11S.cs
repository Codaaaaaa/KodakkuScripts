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

namespace Codaaaaaa.M11S;

[ScriptType(
    guid: "6f3d1b82-9d44-4c5a-8277-3a8f5c0f2b1e",
    name: "M11S补充画图",
    territorys: [1325],
    version: "0.0.0.6",
    author: "Codaaaaaa",
    note: "设置里面改打法，但目前支持的不是很多有很大概率被电。\n- 目前只有铸兵之令的近固以及王者陨石L改美野的画图\n- 该脚本只对RyougiMio佬的画图更新前做指路补充，需要配合使用。\n- 谢谢灵视佬和7dsa1wd1s佬提供的arr")]
public class M11S
{
    #region 用户设置
    // [UserSetting("是否开启TTS")] public static bool TTSOpen { get; set; } = true;
    [UserSetting("铸兵之令统治打法")] public 铸兵之令统治打法 铸兵之令统治打法选择 { get; set; } = 铸兵之令统治打法.近战固定法;
    [UserSetting("王者陨石L改踩塔打法")] public 王者陨石踩塔打法 王者陨石踩塔打法选择 { get; set; } = 王者陨石踩塔打法.tndd;
    [UserSetting("王者陨石L改踩塔击飞打法")] public 王者陨石击飞打法 王者陨石踩塔击飞打法选择 { get; set; } = 王者陨石击飞打法.同平台;
    // [UserSetting("流星雨打法")] public 流星雨打法 流星雨打法选择 { get; set; } = 流星雨打法.奶远近;
    #endregion

    public enum 流星雨打法
    {
        奶远近
    }
    
    public enum 铸兵之令统治打法
    {
        近战固定法,
    }
    public enum 王者陨石踩塔打法
    {
        近近远远,
        tndd
    }
    public enum 王者陨石击飞打法
    {
        同平台,
        闲人斜飞_未经过充分测试
    }

    private enum Corner
    {
        左上,
        右上,
        左下,
        右下,
        未设定
    }


    private static readonly Vector3[] DomPoints = new[]
    {
        new Vector3(85f,  0f,  100f),   // 点0
        new Vector3(115f, 0f,  100f),   // 点1
        new Vector3(100f,  0f,  115f),  // 点2
        new Vector3(100f, 0f,  85f),  // 点3
    };

    private static readonly Vector3[][] SafeByMissing近固 = new[]
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

    private static readonly IReadOnlyDictionary<Corner, Vector3[]> 王者陨石塔位置
        = new Dictionary<Corner, Vector3[]>
    {
        [Corner.左上] = new[]
        {
            new Vector3(86.10f, 0f, 88.73f), // 拉线踩塔
            new Vector3(83.94f, 0f, 91.43f), // 闲人踩塔同平台
            new Vector3(86.94f, 0f, 90.25f), // 闲人踩塔斜飞
        },
        [Corner.右上] = new[]
        {
            new Vector3(113.05f, 0f, 88.90f),
            new Vector3(116.02f, 0f, 91.73f),
            new Vector3(113.02f, 0f, 90.22f),
        },
        [Corner.左下] = new[]
        {
            new Vector3(86.74f, 0f, 110.78f),
            new Vector3(84.40f, 0f, 108.26f),
            new Vector3(86.94f, 0f, 109.79f),
        },
        [Corner.右下] = new[]
        {
            new Vector3(113.40f, 0f, 111.06f),
            new Vector3(116.06f, 0f, 108.47f),
            new Vector3(113.05f, 0f, 109.78f),
        },
    };
    private static readonly IReadOnlyDictionary<Corner, Vector3[]> 王者陨石火圈引导美
        = new Dictionary<Corner, Vector3[]>
    {
        [Corner.左上] = new[]
        {
            new Vector3(92.86f, 0f, 97.76f),
            new Vector3(92.47f, 0f, 91.70f),
            new Vector3(92.41f, 0f, 85.55f),
        },
        [Corner.右上] = new[]
        {
            new Vector3(107.14f, 0f, 97.76f),
            new Vector3(107.53f, 0f, 91.70f),
            new Vector3(107.59f, 0f, 85.55f),
        },
        [Corner.左下] = new[]
        {
            new Vector3(92.86f, 0f, 102.24f),
            new Vector3(92.47f, 0f, 108.30f),
            new Vector3(92.41f, 0f, 114.45f),
        },
        [Corner.右下] = new[]
        {
            new Vector3(107.14f, 0f, 102.24f),
            new Vector3(107.53f, 0f, 108.30f),
            new Vector3(107.59f, 0f, 114.45f),
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

    // 王者陨石拉线记录
    private bool 王者陨石是否有拉线Buff = false;
    private Vector3 王者陨石陨石Pos = Vector3.Zero;
    private Corner 王者陨石下一次Corner = Corner.未设定;

    private readonly object _meteorLock = new();
    private CancellationTokenSource _meteorCts = new();
    private int _meteorSeq = 0;

    private (int seq, CancellationToken token) GetMeteorToken()
    {
        lock (_meteorLock)
            return (_meteorSeq, _meteorCts.Token);
    }

    private bool IsMeteorSeqValid(int seq)
    {
        lock (_meteorLock)
            return seq == _meteorSeq;
    }

    private void CancelMeteorTasks()
    {
        lock (_meteorLock)
        {
            _meteorSeq++;
            try { _meteorCts.Cancel(); } catch { }
            try { _meteorCts.Dispose(); } catch { }
            _meteorCts = new CancellationTokenSource();
        }
    }
    public void Init(ScriptAccessory sa)
    {
        ResetAll();
        sa.Method.RemoveDraw(".*");
    }

    private void ResetAll()
    {
        CancelMeteorTasks();

        王者陨石是否有拉线Buff = false;
        王者陨石陨石Pos = Vector3.Zero;
        王者陨石下一次Corner = Corner.未设定;

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
                var safe = SafeByMissing近固[missing][group];

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

    [ScriptMethod(name: "王者陨石指路-开场", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47760"])]
    public void 王者陨石指路开场(Event evt, ScriptAccessory sa)
    {
        if (!int.TryParse(evt["DurationMilliseconds"], out var dur)) return;

        var myIdx = sa.MyIndex();
        if (myIdx < 0 || myIdx > 7) return;

        // myIdx 奇数去左边，偶数去右边
        var targetPos = myIdx % 2 == 0 ? new Vector3(98.05f, 0f, 99.29f) : new Vector3(102.47f, 0f, 100.09f);

        DrawWaypointToMe(sa, targetPos, dur, "Meteor_Waypoint");
    }

    [ScriptMethod(name: "王者陨石指路-拉线记录buff", eventType: EventTypeEnum.Tether, eventCondition: ["Id:0039"], userControl: false)]
    public void 王者陨石指路拉线记录buff(Event evt, ScriptAccessory sa)
    {
        var myIdx = sa.MyIndex();
        if (myIdx < 0 || myIdx > 7) return;

        var targetId = evt.TargetId();
        var myId = sa.Data.Me;
        if (targetId != myId) return;

        sa.Method.SendChat("/e 记录王者陨石拉线Buff和位置");
        王者陨石是否有拉线Buff = true;
        王者陨石陨石Pos = evt.SourcePosition();
    }

    [ScriptMethod(name: "王者陨石指路-拉线", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:46144"])]
    public async void 王者陨石指路拉线(Event evt, ScriptAccessory sa)
    {
        var (seq, token) = GetMeteorToken();

         _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(2000, token); // 等buff先触发
                if (!IsMeteorSeqValid(seq)) return;

                var myIdx = sa.MyIndex();
                if (myIdx < 0 || myIdx > 7) return;

                // 先确定第一次踩哪里的塔
                if (王者陨石踩塔打法选择 == 王者陨石踩塔打法.近近远远)
                {
                    王者陨石下一次Corner = myIdx switch
                    {
                        0 or 4 => Corner.左上,
                        1 or 5 => Corner.右上,
                        2 or 6 => Corner.左下,
                        3 or 7 => Corner.右下,
                        _ => Corner.未设定,
                    };
                }
                else if (王者陨石踩塔打法选择 == 王者陨石踩塔打法.tndd)
                {
                    王者陨石下一次Corner = myIdx switch
                    {
                        0 or 2 => Corner.左上,
                        1 or 3 => Corner.右上,
                        4 or 6 => Corner.左下,
                        5 or 7 => Corner.右下,
                        _ => Corner.未设定,
                    };
                }

                // 第一次踩塔循环
                await RunMeteorCycleAsync(sa, seq, token);

                await Task.Delay(6500, token); // 视情况调小/调大
                sa.Method.SendChat("/e 第二次踩塔，出现buff");

                // 你可以按你自己的机制点，在这里等 buff / tetherPos 更新完成
                // （如果你已经在别的事件里更新了 王者陨石是否有拉线Buff / 王者陨石陨石Pos，就只需要等一小会）
                

                // 第二次踩塔循环（此时王者陨石是否有拉线Buff 通常变 true）
                await RunMeteorCycleAsync(sa, seq, token);
            }
            catch (OperationCanceledException)
            {
                // 任务被取消，安全退出
                sa.Method.SendChat("/e 王者陨石指路任务已取消");
            }
            catch (Exception ex)
            {
                sa.Method.SendChat($"/e [M11S] Meteor task crashed: {ex.GetType().Name}: {ex.Message}");
            }
        });
    }

    [ScriptMethod(name: "六连风圈指路", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:19183"])]
    public async void 六连风圈指路(Event evt, ScriptAccessory sa)
    {

        var myIdx = sa.MyIndex();
        if (myIdx < 0 || myIdx > 7) return;

        var sourcePos = evt.SourcePosition();
        if (MathF.Abs(sourcePos.X - 100f) > 0.1f || MathF.Abs(sourcePos.Z - 87.97f) > 0.1f)
            return;

        var wPos = myIdx switch
        {
            0 => new Vector3(103.35f, 0.00f, 83.27f),
            1 => new Vector3(93.43f, 0.00f, 116.62f),
            2 => new Vector3(85.91f, 0.00f, 96.18f),
            3 => new Vector3(115.15f, 0.00f, 103.49f),
            4 => new Vector3(95.72f, 0.00f, 83.80f),
            5 => new Vector3(101.60f, 0.00f, 117.08f),
            6 => new Vector3(86.04f, 0.00f, 102.22f),
            7 => new Vector3(116.16f, 0.00f, 96.73f),
            _ => default
        };

        DrawWaypointToMe(sa, wPos, 6000, "六连风圈指路");
    }




    private static bool IsLeftCorner(Corner c) => c is Corner.左上 or Corner.左下;
    private static bool IsRightCorner(Corner c) => c is Corner.右上 or Corner.右下;

    private async Task RunMeteorCycleAsync(ScriptAccessory sa, int seq, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!IsMeteorSeqValid(seq)) return;

        if (王者陨石下一次Corner == Corner.未设定) return;

        // 1) 先引导去踩塔：idx 逻辑更新
        int idx;
        if (王者陨石是否有拉线Buff)
        {
            // 默认仍然以 0 为主，但按你描述在特定条件下改为 1
            idx = 0;

            var meteorPos = 王者陨石陨石Pos;
            bool hasMeteorPos = meteorPos != Vector3.Zero;

            if (hasMeteorPos)
            {
                bool cornerRight = IsRightCorner(王者陨石下一次Corner);
                bool cornerLeft  = IsLeftCorner(王者陨石下一次Corner);

                // 你给的规则：
                // - 陨石.X < 100：如果 corner 在右边 => idx=1，否则 idx=0
                // - 陨石.X > 100：如果 corner 在左边 => idx=1，否则 idx=0
                if (meteorPos.X < 100f)
                {
                    idx = cornerRight ? 1 : 0;
                }
                else if (meteorPos.X > 100f)
                {
                    idx = cornerLeft ? 1 : 0;
                }
                // X == 100：保持 idx=0
            }
        }
        else
        {
            // 没拉线 buff：保持你原逻辑
            idx = (王者陨石踩塔击飞打法选择 == 王者陨石击飞打法.同平台 ? 1 : 2);
        }

        sa.Method.SendChat($"/e 王者陨石下一次Corner: {王者陨石下一次Corner}，idx: {idx}");
        var wPos = 王者陨石塔位置[王者陨石下一次Corner][idx];
        DrawWaypointToMe(sa, wPos, 11000, "Meteor_Tower_Waypoint");
        await Task.Delay(11000, token);
        if (!IsMeteorSeqValid(seq)) return;

        sa.Method.SendChat("/e 空中飞人+判断下一次踩塔位置");

        // 2) 空中飞人后，计算下一次 Corner
        UpdateCornerAfterKnockback();

        await Task.Delay(2500, token);
        if (!IsMeteorSeqValid(seq)) return;
        sa.Method.SendChat("/e 引导到待定位置");

        // 3) 待定位置（你的原代码不动）
        var wPos2 = 王者陨石下一次Corner switch
        {
            Corner.左上 => 王者陨石是否有拉线Buff ? new Vector3(88.15f, 0f, 100f) : new Vector3(93.08f, 0f, 100.38f),
            Corner.右上 => 王者陨石是否有拉线Buff ? new Vector3(111.85f, 0f, 100f) : new Vector3(106.92f, 0f, 100.38f),
            Corner.左下 => 王者陨石是否有拉线Buff ? new Vector3(88.15f, 0f, 100f) : new Vector3(93.08f, 0f, 100.38f),
            Corner.右下 => 王者陨石是否有拉线Buff ? new Vector3(111.85f, 0f, 100f) : new Vector3(106.92f, 0f, 100.38f),
            _ => Vector3.Zero,
        };
        DrawWaypointToMe(sa, wPos2, 4000, "待定位置_Waypoint");
        await Task.Delay(3500, token);
        if (!IsMeteorSeqValid(seq)) return;
        // TODO: 这里再加一个根据同边同组没有buff的人的位置，修改corner。就近原则。
        await Task.Delay(500, token);
        if (!IsMeteorSeqValid(seq)) return;

        sa.Method.SendChat("/e 引导火圈");

        // 4) 火圈引导（你的原代码不动）
        // foreach (var pos in 王者陨石火圈引导美[王者陨石下一次Corner])
        // {
        //     DrawWaypointToMe(sa, pos, 2000, "火圈_Waypoint");
        //     await Task.Delay(2000);
        //     sa.Method.SendChat("/e 下一个火圈");
        // }
        var fireList = 王者陨石火圈引导美[王者陨石下一次Corner];

        for (int i = 0; i < fireList.Length; i++)
        {
            sa.Method.SendChat($"/e i={i}");
            if (i == 2 && !王者陨石是否有拉线Buff)
            {
                // 在引导第二个火圈时重算一次 Corner
                UpdateCornerByMyPosition(sa);

                // Corner 可能变了，重新取火圈路径
                fireList = 王者陨石火圈引导美[王者陨石下一次Corner];
            }

            var pos = fireList[i];
            if (王者陨石是否有拉线Buff)
                DrawWaypointToMe(sa, pos, 2000, "火圈_Waypoint");
            await Task.Delay(2000, token);
            if (!IsMeteorSeqValid(seq)) return;
            sa.Method.SendChat("/e 下一个火圈");
        }

        // 5) 最终位置（你的原代码不动）
        sa.Method.SendChat("/e 最终位置_Waypoint");
        var wPos3 = 王者陨石下一次Corner switch
        {
            Corner.左上 => 王者陨石是否有拉线Buff ? new Vector3(84.01f, 0f, 80.59f) : new Vector3(83.92f, 0f, 88.78f),
            Corner.右上 => 王者陨石是否有拉线Buff ? new Vector3(116.01f, 0f, 80.91f) : new Vector3(115.98f, 0f, 88.78f),
            Corner.左下 => 王者陨石是否有拉线Buff ? new Vector3(84.01f, 0f, 119.21f) : new Vector3(83.87f, 0f, 111.25f),
            Corner.右下 => 王者陨石是否有拉线Buff ? new Vector3(115.96f, 0f, 119.02f) : new Vector3(116.01f, 0f, 111.13f),
            _ => Vector3.Zero,
        };
        DrawWaypointToMe(sa, wPos3, 2000, "最终位置_Waypoint");
        await Task.Delay(2000, token);
        if (!IsMeteorSeqValid(seq)) return;

        // 初始化
        王者陨石是否有拉线Buff = false;
        王者陨石陨石Pos = Vector3.Zero;
    }

    private void UpdateCornerAfterKnockback()
    {
        if (王者陨石是否有拉线Buff)
            {
                var meteorPos = 王者陨石陨石Pos;

            // 上下：按你之前的 Z 判断（你说“上下按之前的计算”）
            // 注意：你原注释写反了，这里按你原代码：Z>100 -> Upper
            bool isUpper = true;
            bool hasMeteorPos = meteorPos != Vector3.Zero;
            if (hasMeteorPos)
            {
                isUpper = meteorPos.Z > 100f;
            }

            // 左右：按你新规则
            // 陨石.X < 100 => corner 一定是右
            // 陨石.X > 100 => corner 一定是左
            // X==100 或没抓到位置：保留原 corner 的左右
            bool isLeft;
            if (hasMeteorPos && meteorPos.X < 100f)
                isLeft = false; // 右
            else if (hasMeteorPos && meteorPos.X > 100f)
                isLeft = true;  // 左
            else
                isLeft = IsLeftCorner(王者陨石下一次Corner); // fallback

            王者陨石下一次Corner = (isLeft, isUpper) switch
            {
                (true, true) => Corner.左上,
                (true, false) => Corner.左下,
                (false, true) => Corner.右上,
                (false, false) => Corner.右下,
            };

            return;
        }

        // 没有拉线buff：
        // - 同平台：只换上下（左上<->左下, 右上<->右下）
        // - 斜飞：只换左右（左上<->右上, 左下<->右下）
        if (王者陨石踩塔击飞打法选择 == 王者陨石击飞打法.同平台)
        {
            王者陨石下一次Corner = 王者陨石下一次Corner switch
            {
                Corner.左上 => Corner.左下,
                Corner.左下 => Corner.左上,
                Corner.右上 => Corner.右下,
                Corner.右下 => Corner.右上,
                _ => 王者陨石下一次Corner,
            };
        }
        else
        {
            王者陨石下一次Corner = 王者陨石下一次Corner switch
            {
                Corner.左上 => Corner.右上,
                Corner.右上 => Corner.左上,
                Corner.左下 => Corner.右下,
                Corner.右下 => Corner.左下,
                _ => 王者陨石下一次Corner,
            };
        }
    }
    private void UpdateCornerByMyPosition(ScriptAccessory sa)
    {
        // 仅用于“没有拉线 buff”的情况
        if (王者陨石是否有拉线Buff) return;

        // 这里用 sa.Data.MePosition（如果你环境里属性名不同，你改成对应的“自己坐标”即可）
        var meId = sa.Data.Me;
        // 通过id获得自己的坐标
        var p = sa.Data.Objects.FirstOrDefault(o => o.GameObjectId == meId)?.Position ?? Vector3.Zero;
        if (p == Vector3.Zero) return;

        bool isLeft = IsLeftCorner(王者陨石下一次Corner);
        bool isUpper = 王者陨石下一次Corner is Corner.左上 or Corner.右上;

        // X<100 左 / X>100 右；X==100 保持
        if (p.X < 100f) isLeft = true;
        else if (p.X > 100f) isLeft = false;

        // Z<100 上 / Z>100 下；Z==100 保持
        if (p.Z < 100f) isUpper = true;
        else if (p.Z > 100f) isUpper = false;

        王者陨石下一次Corner = (isLeft, isUpper) switch
        {
            (true,  true)  => Corner.左上,
            (true,  false) => Corner.左下,
            (false, true)  => Corner.右上,
            (false, false) => Corner.右下,
        };

        sa.Method.SendChat($"/e [无拉线] 按站位重算Corner => {王者陨石下一次Corner} (X={p.X:0.00}, Z={p.Z:0.00})");
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
