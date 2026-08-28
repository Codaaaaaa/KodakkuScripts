using System;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Script;
using KodakkuAssist.Module.Draw;
using Dalamud.Utility.Numerics;
using System.Numerics;
using System.Runtime.Intrinsics.Arm;
using Dalamud.Memory.Exceptions;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using static Dalamud.Interface.Utility.Raii.ImRaii;
using KodakkuAssist.Module.GameOperate;
using System.Threading;
using KodakkuAssist.Module.Draw.Manager;
using KodakkuAssist.Extensions;
using KodakkuAssist.Module.Script.Type;

namespace KarlinScriptNamespace
{
    // 1112 为忆罪宫，仅用于 "/e =Exaflare" 地火模拟器（补丁部分）
    [ScriptType(name:"Dragonsong's Reprise (Ultimate) DSR - LPDU", territorys: [968, 1112], guid: "baadb811-5bd3-4e61-bb1f-f3eab163c52f", version:"0.0.0.6", author: "Karlin")]
    public class DragongSingDrawLpdu
    {
        
        [UserSetting("P5 一运连线冲锋显示延迟(ms)")]
        public int p5TetherCrashDelay { get; set; } = 3000;

        [UserSetting("P6 分散分摊标记")]
        public bool p6Mark {  get; set; }=false;

        [UserSetting("P7 死亡轮回116分摊")]
        public bool p7_116 { get; set; } = true;

        object lockObj=new object();
        
        bool p1Charge=false;
        bool p3TowerDeal = false;
        bool p5Deal = false;
        
        int? firstTargetIcon = null;
        uint p1GrenoId = 0;
        uint p1AdelId = 0;
        uint p3BossId = 0;
        uint p6FireBallCount;
        uint p6FireBallCount2;
        uint tordanId = 0;
        uint darkDragonId = 0;
        uint whiteDragonId = 0;

        double parse = 0;
        Vector3 p2AdelPos = Vector3.Zero;
        Vector3 p2ZPos = Vector3.Zero;
        Vector3 p5DivePos = Vector3.Zero;
        Vector3 p5GrenoPos = Vector3.Zero;
        Vector3 p5GreekPos = Vector3.Zero;
        Vector3 p6WhitePos = Vector3.Zero;
        Vector3 p7Stone1 = Vector3.Zero;
        Vector3 p7Stone2 = Vector3.Zero;
        Dictionary<string, HashSet<uint>> p3majong=new Dictionary<string, HashSet<uint>>();
        List<uint> p2BlueCircle = [];
        List<int> p1sony = [];
        List<bool> p2SafeDir = [];
        List<bool> p2Stone = [];
        List<bool> p2Tower = [];
        List<bool> p3Boom = [];
        List<int> p3Tower = [];
        List<int> p2StoneTeam = [];
        List<int> p5sony = [];
        List<int> p5sony_sixuan = [];
        List<int> p6tether = [];
        List<int> p6lightDark = [];

        (int, int) p2Jump = (-1,-1);
        (int, int) p2StoneMem = (-1, -1);

        private bool p5DeathMarkDone = false;

        private bool autoTargetHighestHpRunning = false;
        private string? autoTargetHighestHpActionGuid = null;

        // ===== 以下设置与状态来自合并进来的绝龙诗补丁 =====
        [UserSetting("站位提示圈绘图-普通颜色")]
        public static ScriptColor PosColorNormal { get; set; } = new ScriptColor { V4 = new Vector4(1.0f, 1.0f, 1.0f, 1.0f) };
        [UserSetting("站位提示圈绘图-玩家站位颜色")]
        public static ScriptColor PosColorPlayer { get; set; } = new ScriptColor { V4 = new Vector4(0.0f, 1.0f, 1.0f, 1.0f) };

        public enum ExaflareSpecStrategyEnum
        {
            绝不去前方_NeverFront,
            绝不跑无脑火_NeverUniverse,
            绝不多跑_LeastMovement,
            绝对前方_AlwaysFront,
            关闭_PleaseDontDoThat,
        }
        [UserSetting("地火指路特殊策略")]
        public static ExaflareSpecStrategyEnum ExaflareStrategy { get; set; } = ExaflareSpecStrategyEnum.绝不跑无脑火_NeverUniverse;

        [UserSetting("地火（百京核爆）使用程序预设颜色")]
        public static bool ExaflareBuiltInColor { get; set; } = true;
        [UserSetting("地火（百京核爆）爆炸区颜色")]
        public ScriptColor ExaflareColor { get; set; } = new ScriptColor { V4 = new Vector4(1.0f, 1.0f, 0f, 1.0f) };
        [UserSetting("地火（百京核爆）是否绘制下一枚地火预警区")]
        public static bool ExaflareWarnDrawn { get; set; } = true;
        [UserSetting("地火（百京核爆）预警区颜色")]
        public ScriptColor ExaflareWarnColor { get; set; } = new ScriptColor { V4 = new Vector4(0.6f, 0.6f, 1.0f, 1.0f) };

        private enum DsrPhase
        {
            Init,                   // 初始
            Phase2Strength,         // P2 一运
            Phase2Sancity,          // P2 二运
            Phase3Nidhogg,          // P3 大师兄
            Phase4Eyes,             // P4 龙眼
            Phase5HeavensWrath,     // P5 一运
            Phase5HeavensDeath,     // P5 二运
            Phase6IceAndFire1,      // P6 一冰火
            Phase6NearOrFar1,       // P6 一远近
            Phase6Flame,            // P6 十字火
            Phase6NearOrFar2,       // P6 二远近
            Phase6IceAndFire2,      // P6 二冰火
            Phase6Cauterize,        // P6 俯冲
            Phase7Exaflare1,        // P7 一地火
            Phase7Stack1,           // P7 一分摊
            Phase7Nuclear1,         // P7 一核爆
            Phase7Exaflare2,        // P7 二地火
            Phase7Stack2,           // P7 二分摊
            Phase7Nuclear2,         // P7 二核爆
            Phase7Exaflare3,        // P7 三地火
            Phase7Stack3,           // P7 三分摊
            Phase7Enrage,           // P7 狂暴
        }

        private static List<string> _role = ["MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4"];
        private static Vector3 _center = new Vector3(100, 0, 100);
        private DsrPhase _dsrPhase = DsrPhase.Init;
        private List<bool> _drawn = new bool[20].ToList();                  // 绘图记录
        private volatile List<bool> _recorded = new bool[20].ToList();      // 被记录flag
        private int _pureOfHeartBaitCount = 0;                              // P1/P4.5 纯洁心灵引导次数
        private List<bool> _p2SafeDirection = new bool[8].ToList();         // P2 一运冲锋安全位置
        private Vector3 _p2ThordanPos = new Vector3(0, 0, 0);               // P2 一运托尔丹位置
        private List<uint> _p2TetherKnightId = [0, 0];                      // P2 一运接线骑士ID，顺序左、右
        private bool _p3DfgEnable = false;                                  // P3 指路使能
        private static PriorityDict _dfg = new PriorityDict();              // P3 机制记录
        private List<Vector3> _p3TowerAppearPos = [];                       // P3 塔生成位置
        private int _p4MirageDiveNum = 0;                                   // P4 幻象冲次数
        private bool _p4PrepareToCenter = false;                            // P4 幻象冲准备回中
        private List<bool> _p4MirageDiveNumFirstRoundTarget = new bool[8].ToList();         // P4 幻象冲第一轮目标
        private List<int> _p4MirageDivePos = [];                            // P4 幻象冲目标方位，左上为0顺时针增加
        private Vector3 _p5VedrfolnirPos = new Vector3(0, 0, 0);            // P5 白龙位置
        private List<bool> _p6DragonsGlowAction = [false, false];           // P6 双龙吐息记录
        private List<bool> _p6DragonsWingAction = [false, false, false];    // P6 双龙远近记录 [远T/近F，左安全T/右安全F，前安全T/后安全F/内安全T/外安全F]
        private List<bool> _p7FirstEnmityOrder = [false, false];            // P7 平A仇恨记录
        private readonly List<int> _p7TrinityOrderIdx = [4, 5, 6, 7, 2, 3]; // P7 接刀顺序
        private bool _p7TrinityDisordered = false;                          // P7 接刀顺序是否出错
        private bool _p7TrinityTankDisordered = false;                      // P7 坦克接刀仇恨是否出错
        private int _p7TrinityNum = 0;                                      // P7 接刀次数
        private DsrExaflare? _p7Exaflare = null;                            // P7 地火Class
        private uint _p7BossId = 0;                                         // P7 boss Id

        private ManualResetEvent _thrustEvent = new(false);
        private ManualResetEvent _thordanCastAtEdgeEvent = new(false);
        private ManualResetEvent _mirageDiveRound = new(false);
        private ManualResetEvent _p5VedrfolnirPosRecordEvent = new(false);
        private ManualResetEvent _iceAndFireEvent = new(false);
        private ManualResetEvent _nearOrFarWingsEvent = new(false);
        private ManualResetEvent _nearOrFarCauterizeEvent = new(false);
        private ManualResetEvent _nearOrFarInOutEvent = new(false);
        private ManualResetEvent _bladeEvent = new(false);
        private ManualResetEvent _trinityEvent = new(false);

        private const uint ChariotBlade = 298;

        // 补丁内部调试开关：为 true 时，仅供调试的记录类方法也会出现在用户设置面板
        private const bool Debugging = false;



        public void Init(ScriptAccessory accessory)
        {
            parse = 0;

            p6FireBallCount = 0;
            p6FireBallCount2 = 0;

            firstTargetIcon =null;
            p1Charge = false;
            p3TowerDeal = false;
            p5Deal = false;

            p3majong =new Dictionary<string, HashSet<uint>>();
            p5DivePos = Vector3.Zero;
            p5GrenoPos = Vector3.Zero;
            p5GreekPos = Vector3.Zero;
            p5sony = [0, 0, 0, 0, 0, 0, 0, 0];
            p5sony_sixuan = [0, 0, 0, 0, 0, 0, 0, 0];
            p1sony = [0, 0, 0, 0, 0, 0, 0, 0];
            p3Tower = [0,0,0,0];
            p6tether = [0, 0, 0, 0, 0, 0, 0, 0];
            p6lightDark= [0, 0, 0, 0, 0, 0, 0, 0];
            p2BlueCircle = [];
            p2SafeDir = [true, true, true, true, true, true, true, true];
            p2Stone = [false, false, false, false, false, false, false, false];
            p2Tower = [false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false];
            p3Boom = [false,false,false,false];
            p2Jump = (-1, -1);
            
            p5DeathMarkDone = false;

            accessory.Method.MarkClear();

            InitPatch(accessory);
        }

        /// <summary>
        /// 补丁部分（原 Usami DSR_Patch）的状态重置，随 Init 一起调用
        /// </summary>
        private void InitPatch(ScriptAccessory accessory)
        {
            accessory.Method.RemoveDraw(".*");

            _dsrPhase = DsrPhase.Init;
            _drawn = new bool[20].ToList();
            _recorded = new bool[20].ToList();
            _p7BossId = 0;
            _pureOfHeartBaitShown = false;

            _thordanCastAtEdgeEvent = new ManualResetEvent(false);
            _thrustEvent = new ManualResetEvent(false);
            _mirageDiveRound = new ManualResetEvent(false);
            _p5VedrfolnirPosRecordEvent = new ManualResetEvent(false);
            _iceAndFireEvent = new ManualResetEvent(false);
            _nearOrFarWingsEvent = new ManualResetEvent(false);
            _nearOrFarCauterizeEvent = new ManualResetEvent(false);
            _nearOrFarInOutEvent = new ManualResetEvent(false);
            _bladeEvent = new ManualResetEvent(false);
            _trinityEvent = new ManualResetEvent(false);
        }

        #region P1

        [ScriptMethod(name: "---- 《P1 & P4.5 门神》 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld"],
            userControl: true)]
        public void P1_分节线(Event @event, ScriptAccessory accessory)
        {
        }

        [ScriptMethod(name: "P1 BossId", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:28532"],userControl:false)]
        public void P1_BossId(Event @event, ScriptAccessory accessory)
        {
            if (ParseObjectId(@event["SourceId"], out var sid))
            {
                p1GrenoId=sid;
            }
        }
        [ScriptMethod(name: "P1 阶段记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25300"],userControl:false)]
        public void P1_阶段记录(Event @event, ScriptAccessory accessory)
        {
            if(parse==0) { parse = 1; }
            parse = Math.Round(parse + 0.1, 1);
            if (ParseObjectId(@event["SourceId"], out var sid))
            {
                p1AdelId = sid;
            }
            
        }
        [ScriptMethod(name: "P1 钢铁",eventType: EventTypeEnum.StartCasting,eventCondition: ["ActionId:25307"])]
        public void P1_钢铁(Event @event, ScriptAccessory accessory)
        {
            var dp=accessory.Data.GetDefaultDrawProperties();
            dp.Scale = new(6);
            dp.Color = accessory.Data.DefaultDangerColor;
            if (ParseObjectId(@event["SourceId"],out var sid))
            {
                dp.Owner= sid;
            }
            dp.DestoryAt = 5000;
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,dp);
        }
        [ScriptMethod(name: "P1 月环", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25306"])]
        public void P1_月环(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Scale = new(70);
            dp.InnerScale = new(6);
            dp.Radian = float.Pi * 2;
            dp.Color=accessory.Data.DefaultDangerColor;
            if (ParseObjectId(@event["SourceId"], out var sid))
            {
                dp.Owner = sid;
            }
            dp.DestoryAt = 5000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
        }

        [ScriptMethod(name: "P1 苍穹炽焰", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25309"])]
        public void P1_苍穹炽焰(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P1_苍穹炽焰";
            dp.Scale = new(4);
            dp.Color = accessory.Data.DefaultSafeColor;
            if (ParseObjectId(@event["TargetId"], out var tid))
            {
                dp.Owner = tid;
            }
            dp.DestoryAt = 5000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        }

        [ScriptMethod(name: "P1 直线多维空间斩", eventType: EventTypeEnum.TargetIcon)]
        public void P1_直线多维空间斩(Event @event, ScriptAccessory accessory)
        {
            if (parse <1|| parse>=2) return;
            if (ParsTargetIcon(@event["Id"]) != 0) return;

            var dp = accessory.Data.GetDefaultDrawProperties();
           
            dp.Scale = new(8,70);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Owner = p1GrenoId;
            if (ParseObjectId(@event["TargetId"], out var tid))
            {
                dp.TargetObject = tid;
            }
            dp.DestoryAt = 6000;
            dp.Name = $"P1 直线多维空间斩{tid:X}";
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }

        [ScriptMethod(name: "P1 次元裂缝危险区", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:13071"])]
        public void P1_次元裂缝危险区(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Scale = new(9);
            dp.Color = accessory.Data.DefaultDangerColor;
            if (ParseObjectId(@event["SourceId"], out var id))
            {
                dp.Owner = id;
            }
            dp.DestoryAt = 60000;
            dp.Name = $"P1 次元裂缝危险区{id:X}";
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        [ScriptMethod(name: "P1 次元裂缝危险区移除", eventType: EventTypeEnum.RemoveCombatant, eventCondition: ["DataId:13071"],userControl:false)]
        public void P1_次元裂缝危险区移除(Event @event, ScriptAccessory accessory)
        {
            if (ParseObjectId(@event["SourceId"], out var id))
            {
                accessory.Method.RemoveDraw($"P1 次元裂缝危险区{id:X}");
            }
        }

        [ScriptMethod(name: "P1 光芒剑阿代尔斐尔位置(ImGui)", eventType: EventTypeEnum.Targetable, eventCondition: ["Targetable:True"])]
        public void P1_光芒剑阿代尔斐尔位置(Event @event, ScriptAccessory accessory)
        {
            if (parse!=1.1) return;
            if (!ParseObjectId(@event["SourceId"], out var sid)) return;
            if (sid != p1AdelId) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P1_光芒剑阿代尔斐尔位置";
            dp.TargetObject = sid;
            dp.Owner = accessory.Data.Me;
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.ScaleMode |= ScaleMode.YByDistance;
            dp.DestoryAt = 7000;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, dp);


        }
        [ScriptMethod(name: "P1 光芒剑(火神冲)", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:25294"])]
        public void P1_光芒剑(Event @event, ScriptAccessory accessory)
        {
            if (p1Charge) return;
            p1Charge = true;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Scale = new(9);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 7000;

            if (float.TryParse(@event["SourceRotation"],out var r))
            {
                if(MathF.Abs(r+float.Pi/4)<0.1 || MathF.Abs(r - float.Pi *0.75f) < 0.1)
                {
                    dp.Name = "P1_光芒剑(111.00,111.00)";
                    dp.Position = new(111, 0, 111);
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                    dp.Name = "P1_光芒剑(89.00,89.00)";
                    dp.Position = new(89, 0, 89);
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                }
                if (MathF.Abs(r - float.Pi / 4) < 0.1 || MathF.Abs(r + float.Pi * 0.75f) < 0.1)
                {
                    dp.Name = "P1_光芒剑(111.00,89.00)";
                    dp.Position = new(111, 0, 89);
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                    dp.Name = "P1_光芒剑(89.00,111.00)";
                    dp.Position = new(89, 0, 111);
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                }
            }

            dp.Name = "P1_光芒剑(78.00,100.00)";
            dp.Position = new(78, 0, 100);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            dp.Name = "P1_光芒剑(92.52,100.00)";
            dp.Position = new(92.52f, 0, 100);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            dp.Name = "P1_光芒剑(107.48,100.00)";
            dp.Position = new(107.48f, 0, 100);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            dp.Name = "P1_光芒剑(122.00,100.00)";
            dp.Position = new(122, 0, 100);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            dp.Name = "P1_光芒剑(100.00,78.00)";
            dp.Position = new(100, 0, 78);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            dp.Name = "P1_光芒剑(100,92.52.00)";
            dp.Position = new(100, 0, 92.52f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            dp.Name = "P1_光芒剑(100.00,107.48)";
            dp.Position = new(100, 0, 107.48f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            dp.Name = "P1_光芒剑(100.00,122.00)";
            dp.Position = new(100, 0, 122);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        }
        [ScriptMethod(name: "P1 光球爆炸范围移除", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:25295"], userControl: false)]
        public void P1_光球爆炸范围移除(Event @event, ScriptAccessory accessory)
        {
            if (parse > 2) return;
            var pos= JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
            var name = $"P1_光芒剑\\({pos.X:f2},{pos.Z:f2}\\)";
            accessory.Method.RemoveDraw(name);
        }
        [ScriptMethod(name: "P1 击退预测", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25308"])]
        public void P1_击退预测(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Scale = new(1.5f,16);
            dp.Color = accessory.Data.DefaultDangerColor.WithW(2);
            dp.Owner = accessory.Data.Me;
            if (ParseObjectId(@event["SourceId"], out var sid))
            {
                dp.TargetObject = sid;
            }
            dp.Rotation = float.Pi;
            dp.DestoryAt = 5000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
        }
        [ScriptMethod(name: "P1 索尼记录", eventType: EventTypeEnum.TargetIcon, userControl: false)]
        public void P1_索尼记录(Event @event, ScriptAccessory accessory)
        {
            
            if (parse != 1.2) return;
            var sony = ParsTargetIcon(@event["Id"]) - 47;
            if (sony < 0 || sony > 3) return;
            if (ParseObjectId(@event["TargetId"], out var id))
            {
                var index = accessory.Data.PartyList.ToList().IndexOf(id);
                p1sony[index] = sony;
            }
        }
        [ScriptMethod(name: "P1 索尼击退位置(ImGui)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25308"])]
        public void P1_索尼击退位置(Event @event, ScriptAccessory accessory)
        {
            accessory.Log.Debug($"parse{parse}");
            if (parse !=1.2) return;
            
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.Scale = new(1);
            dp.Owner = accessory.Data.Me;
            dp.DestoryAt = 4000;
            dp.ScaleMode |= ScaleMode.YByDistance;

            var index = accessory.Data.PartyList.ToList().IndexOf(accessory.Data.Me);
            
                
            var cpos = new Vector3(100, 0, 100);
            var npos = new Vector3(100, 0, 96);
            
            //○
            if (p1sony[index] == 0)
            {
                var p1= RotatePoint(npos, cpos, float.Pi / 2);
                var p2= RotatePoint(npos, cpos, float.Pi / -2);
                
                dp.Name= "P1索尼○1";
                dp.TargetPosition = p1;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                dp.Name = "P1索尼○2";
                dp.TargetPosition = p2;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }
            //▽
            if (p1sony[index] == 1)
            {
                if(index==2||index==3)
                {
                    var p = RotatePoint(npos, cpos, float.Pi / 4 * 3);
                    dp.Name = "P1索尼▽奶";
                    dp.TargetPosition = p;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                }
                else
                {
                    var p = RotatePoint(npos, cpos, float.Pi / -4);
                    dp.Name = "P1索尼▽D";
                    dp.TargetPosition = p;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                }
            }
            //□
            if (p1sony[index] == 2)
            {
                if (index == 0 || index == 1)
                {
                    var p = RotatePoint(npos, cpos, float.Pi / 4);
                    dp.Name = "P1索尼□T";
                    dp.TargetPosition = p;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                }
                else
                {
                    var p = RotatePoint(npos, cpos, float.Pi / -4 * 3);
                    dp.Name = "P1索尼□D";
                    dp.TargetPosition = p;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                }
            }
            //×
            if (p1sony[index] == 3)
            {
                if (index == 0 || index == 1)
                {
                    var p = npos;
                    dp.Name = "P1索尼×T";
                    dp.TargetPosition = p;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                }
                else
                {
                    var p = RotatePoint(npos, cpos, float.Pi);
                    dp.Name = "P1索尼×D";
                    dp.TargetPosition = p;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                }
            }


        }
        [ScriptMethod(name: "P1 光翼闪", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25316"])]
        public void P1_光翼闪(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Scale = new(18);
            dp.Radian = float.Pi / 6;
            dp.Color = accessory.Data.DefaultDangerColor;
            if (ParseObjectId(@event["SourceId"], out var sid))
            {
                dp.Owner = sid;
            }
            dp.Delay = 10000;
            dp.DestoryAt = 20000;
            dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan , dp);
            dp.TargetOrderIndex = 2;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp); 
        }

        [ScriptMethod(name: "P1 苍穹刻印玩家", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2661"])]
        public void P1_苍穹刻印玩家(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Scale = new(3);
            dp.Color = accessory.Data.DefaultDangerColor;
            if (ParseObjectId(@event["TargetId"], out var tid))
            {
                dp.Owner = tid;
            }
            dp.DestoryAt = 5000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            
        }
        [ScriptMethod(name: "P1 苍穹刻印落地", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25370"])]
        public void P1_苍穹刻印落地(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P1_苍穹刻印落地";
            dp.Scale = new(3);
            dp.Color = accessory.Data.DefaultDangerColor;
            if (ParseObjectId(@event["SourceId"], out var sid))
            {
                dp.Owner = sid;
            }
            dp.DestoryAt = 2500;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        }
        private bool _pureOfHeartBaitShown = false;
        [ScriptMethod(name: "P1 纯洁心灵引导", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25316"], 
            userControl: true)]
        public void P1_纯洁心灵引导(Event @event, ScriptAccessory accessory)
        {
            _pureOfHeartBaitCount = 0;
            _pureOfHeartBaitShown = true;
            // 纯洁心灵引导顺序H1H2, D1D2，D3D4，MTST
            var myIndex = accessory.GetMyIndex();
            // 此处为第一次纯洁心灵，如果非H1H2，不参与
            if (myIndex is not (2 or 3)) return;
            // todo 修改delay与destroy
            P1_绘制纯洁心灵引导(accessory, 0, 15000);
        }

        private void P1_绘制纯洁心灵引导(ScriptAccessory sa, int delay, int destroy)
        {
            var myIndex = sa.GetMyIndex();
            Vector3[] baitPos = [new(87.0f, 0.0f, 108.0f), new(91.0f, 0.0f, 108.0f)];
            var baitPosIdx = 1 - myIndex % 2;   // 偶数索引(MT/H1/D1/D3)在内点，奇数索引(ST/H2/D2/D4)在外点
            for (var posIdx = 0; posIdx < baitPos.Length; posIdx++)
            {
                var color = baitPosIdx == posIdx ? PosColorPlayer.V4 : PosColorNormal.V4;
                var dp = sa.DrawStaticCircle(baitPos[posIdx], color, delay, destroy, $"纯洁心灵", 0.5f);
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                if (baitPosIdx != posIdx) continue;
                var dpGuide = sa.DrawGuidance(baitPos[posIdx], delay, destroy, $"纯洁心灵指路");
                sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpGuide);
            }
        }

        [ScriptMethod(name: "P1 纯洁心灵引导后续", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:25369"], 
            userControl: false)]
        public void P1_纯洁心灵引导后续(Event @event, ScriptAccessory sa)
        {
            if (!_pureOfHeartBaitShown) return;
            if (@event.TargetIndex() != 1) return;
            var myIndex = sa.GetMyIndex();
            lock (this)
            {
                _pureOfHeartBaitCount++;
                sa.Log.Debug($"纯洁心灵引导次数：{_pureOfHeartBaitCount}");
                if (_pureOfHeartBaitCount > 6) return;
                var baitDict = new Dictionary<int, int> { { 1, 4 }, { 2, 5 }, { 3, 6 }, { 4, 7 }, { 5, 0 }, { 6, 1 } };
                if (baitDict[_pureOfHeartBaitCount] != myIndex) return;
                sa.Log.Debug($"开始绘制玩家的纯洁心灵引导");
                P1_绘制纯洁心灵引导(sa, 0, 5000);
            }
        }

        #endregion

        #region P2

        [ScriptMethod(name: "---- 《P2 骑神托尔丹》 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld"],
            userControl: true)]
        public void P2_分节线(Event @event, ScriptAccessory accessory)
        {
        }

        #region 一运
        [ScriptMethod(name: "P2 1运记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25555"],userControl:false)]
        public void P2_1运记录(Event @event, ScriptAccessory accessory)
        {
            parse = 2.1;
            firstTargetIcon = null;
            if (ParseObjectId(@event["SourceId"], out var id))
            {
                tordanId = id;
            }
        }
        [ScriptMethod(name: "P2 1运波勒克兰冲锋", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:3781"])]
        public void P2_1运波勒克兰冲锋(Event @event, ScriptAccessory accessory)
        {
            if (parse != 2.1) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Scale = new(16,52);
            dp.Color = accessory.Data.DefaultDangerColor;
            if (ParseObjectId(@event["SourceId"], out var sid))
            {
                dp.Owner = sid;
            }
            dp.DestoryAt = 7000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
        [ScriptMethod(name: "P2 1运伊尼亚斯冲锋", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:3782"])]
        public void P2_1运伊尼亚斯冲锋(Event @event, ScriptAccessory accessory)
        {
            if (parse != 2.1) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Scale = new(16, 52);
            dp.Color = accessory.Data.DefaultDangerColor;
            if (ParseObjectId(@event["SourceId"], out var sid))
            {
                dp.Owner = sid;
            }
            dp.DestoryAt = 7000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
        [ScriptMethod(name: "P2 1运韦尔吉纳冲锋", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:3783"])]
        public void P2_1运韦尔吉纳冲锋(Event @event, ScriptAccessory accessory)
        {
            if (parse != 2.1) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Scale = new(16, 52);
            dp.Color = accessory.Data.DefaultDangerColor;
            if (ParseObjectId(@event["SourceId"], out var sid))
            {
                dp.Owner = sid;
            }
            dp.DestoryAt = 7000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
        [ScriptMethod(name: "P2 1运冲锋位置记录", eventType: EventTypeEnum.NpcYell,userControl:false)]
        public void P2_1运冲锋位置记录(Event @event, ScriptAccessory accessory)
        {
            if (parse != 2.1) return;
            var str= @event["Id"];
            if (str != "3781" && str != "3782" && str != "3783") return;
            var sourcePos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
            var dir= PositionTo8Dir(sourcePos, new(100, 0, 100));
            if (dir == 0 || dir == 4)
            {
                p2SafeDir[0] = false;
                p2SafeDir[4] = false;
            }
            if (dir == 1 || dir == 5)
            {
                p2SafeDir[1] = false;
                p2SafeDir[5] = false;
            }
            if (dir == 2 || dir == 6)
            {
                p2SafeDir[2] = false;
                p2SafeDir[6] = false;
            }
            if (dir == 3 || dir == 7)
            {
                p2SafeDir[3] = false;
                p2SafeDir[7] = false;
            }
        }
        [ScriptMethod(name: "P2 1运冲锋安全区位置(Imgui)", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:3781"])]
        public void P2_1运冲锋安全区位置(Event @event, ScriptAccessory accessory)
        {
            if (parse != 2.1) return;
            Task.Delay(100).ContinueWith(y =>
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = "P2 1运冲锋安全区位置";
                dp.ScaleMode |= ScaleMode.YByDistance;
                dp.Color = accessory.Data.DefaultSafeColor;
                dp.Owner = accessory.Data.Me;
                dp.DestoryAt = 7000;

                var idIndex = accessory.Data.PartyList.ToList().IndexOf(accessory.Data.Me);
                var cpos = new Vector3(100, 0, 100);
                var npos = new Vector3(100, 0, 82);
                //MT
                if(idIndex==0|| idIndex == 2 || idIndex == 4 || idIndex == 6)
                {
                    dp.TargetPosition = RotatePoint(npos, cpos, p2SafeDir.LastIndexOf(true) * float.Pi / 4);
                }
                else//ST
                {
                    dp.TargetPosition = RotatePoint(npos, cpos, p2SafeDir.IndexOf(true) * float.Pi / 4);
                }

                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, dp);
            });
            
        }
        [ScriptMethod(name: "P2 1运地震", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25558"])]
        public void P2_1运地震(Event @event, ScriptAccessory accessory)
        {
            if (parse != 2.1) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Name = "P2 1运地震";
            if (ParseObjectId(@event["SourceId"], out var sid))
            {
                dp.Owner = sid;
            }

            dp.Scale = new(6);
            dp.DestoryAt = 6000;
            dp.Radian = float.Pi * 2;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            dp.Scale = new(12);
            dp.InnerScale= new(6);
            dp.Delay = 4000;
            dp.DestoryAt = 4000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);

            dp.Scale = new(18);
            dp.InnerScale = new(12);
            dp.Delay = 6000;
            dp.DestoryAt = 4000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);

            dp.Scale = new(24);
            dp.InnerScale = new(18);
            dp.Delay = 8000;
            dp.DestoryAt = 4000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);

            dp.Scale = new(30);
            dp.InnerScale = new(24);
            dp.Delay = 10000;
            dp.DestoryAt = 4000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
        }
        [ScriptMethod(name: "P2 1运穿天记录", eventType: EventTypeEnum.TargetIcon,userControl:false)]
        public void P2_1运穿天记录(Event @event, ScriptAccessory accessory)
        {
            if (parse != 2.1) return;
            if (ParsTargetIcon(@event["Id"]) != 0) return;
            
            if (ParseObjectId(@event["TargetId"], out var id))
            {
                p2BlueCircle.Add(id);
            }
        }
        [ScriptMethod(name: "P2 1运空间破碎", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25564"])]
        public void P2_1运空间破碎(Event @event, ScriptAccessory accessory)
        {
            if (parse != 2.1) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P2 1运空间破碎";
            dp.Color = accessory.Data.DefaultDangerColor;
            if (ParseObjectId(@event["SourceId"], out var sid))
            {
                dp.Owner = sid;
            }

            dp.Scale = new(9);
            dp.Delay = 3000;
            dp.DestoryAt = 9000- dp.Delay;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

           
        }
        [ScriptMethod(name: "P2 1运穿天(大圈)", eventType: EventTypeEnum.TargetIcon)]
        public void P2_1运穿天(Event @event, ScriptAccessory accessory)
        {
            if (parse != 2.1) return;
            if (ParsTargetIcon(@event["Id"]) != 0) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Scale = new(24);
            dp.Color = accessory.Data.DefaultDangerColor.WithW(0.5f);
            if (ParseObjectId(@event["TargetId"], out var id))
            {
                dp.Owner = id;
            }
            dp.Delay = 6000;
            dp.DestoryAt = 3000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            
        }
        [ScriptMethod(name: "P2 一运穿天连线(ImGui)", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:25562"])]
        public void P2_一运穿天连线(Event @event, ScriptAccessory accessory)
        {
            if (parse != 2.1) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P2 一运穿天连线";
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Owner = p2BlueCircle[0];
            dp.ScaleMode |= ScaleMode.YByDistance;
            dp.DestoryAt = 9000;
            for (int i = 1; i < p2BlueCircle.Count; i++)
            {
                dp.TargetObject= p2BlueCircle[i];
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, dp);
            }
        }
        [ScriptMethod(name: "P2 1运让勒努冲锋", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:2551"])]
        public void P2_1运让勒努冲锋(Event @event, ScriptAccessory accessory)
        {
            if (parse != 2.1) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Scale = new(8, 50);
            dp.Color = accessory.Data.DefaultDangerColor;
            if (ParseObjectId(@event["SourceId"], out var sid))
            {
                dp.Owner = sid;
            }
            dp.TargetResolvePattern = PositionResolvePatternEnum.OwnerTarget;
            dp.Delay = 3000;
            dp.DestoryAt = 3000;
            dp.ScaleMode |= ScaleMode.YByDistance;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
        [ScriptMethod(name: "P2 1运阿代尔菲尔冲锋", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:2550"])]
        public void P2_1运阿代尔菲尔冲锋(Event @event, ScriptAccessory accessory)
        {
            if (parse != 2.1) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Scale = new(8, 50);
            dp.Color = accessory.Data.DefaultDangerColor;
            if (ParseObjectId(@event["SourceId"], out var sid))
            {
                dp.Owner = sid;
            }
            dp.TargetResolvePattern = PositionResolvePatternEnum.OwnerTarget;
            dp.Delay = 3000;
            dp.DestoryAt = 3000;
            dp.ScaleMode |= ScaleMode.YByDistance;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
        [ScriptMethod(name: "P2 1运骑神位置(Imgui)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25563"])]
        public void P2_1运骑神位置(Event @event, ScriptAccessory accessory)
        {
            if (parse != 2.1) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P2 1运骑神位置";
            dp.Scale = new(8, 50);
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.TargetObject = tordanId;
            dp.Owner = accessory.Data.Me;
            dp.ScaleMode |= ScaleMode.YByDistance;
            dp.Delay = 500;
            dp.DestoryAt = 7000;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, dp);
        }

        [ScriptMethod(name: "P2 一运不可视刀范围", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25545"])]
        public void P2_一运不可视刀范围(Event @event, ScriptAccessory accessory)
        {
            var sid = @event.SourceId();
            var dp = accessory.DrawFan(sid, float.Pi / 6, 0, 30, 0, 0, 1500, $"不可视刀");
            dp.Color = accessory.Data.DefaultDangerColor.WithW(1.5f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }

        [ScriptMethod(name: "P2 一运阶段记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25555"], userControl: false)]
        public void P2_一运阶段记录(Event @event, ScriptAccessory sa)
        {
            _dsrPhase = DsrPhase.Phase2Strength;
            _p2SafeDirection = new bool[8].ToList();
            _p2ThordanPos = new Vector3(0, 0, 0);
            _p2TetherKnightId = [0, 0];
            sa.Log.Debug($"当前阶段为：{_dsrPhase}");
        }

        [ScriptMethod(name: "P2 一运冲锋方位记录", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:regex:^(378[123])$"], userControl: false)]
        public void P2_一运冲锋方位记录(Event @event, ScriptAccessory sa)
        {
            if (_dsrPhase != DsrPhase.Phase2Strength) return;

            var spos = @event.SourcePosition();
            var dir = spos.Position2Dirs(_center, 8);
            lock (_p2SafeDirection)
            {
                _p2SafeDirection[dir % 4] = true;
                sa.Log.Debug($"List内部true的数量：{_p2SafeDirection.Count(x => x)}");
                if (_p2SafeDirection.Count(x => x) != 3) return;
                _thrustEvent.Set();
            }
        }

        [ScriptMethod(name: "P2 一运分散安全位置指引", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:3781"], userControl: true)]
        public void P2_一运分散安全位置指引(Event @event, ScriptAccessory accessory)
        {
            if (_dsrPhase != DsrPhase.Phase2Strength) return;
            _thrustEvent.WaitOne();

            // 由于在处理_p2SafeDirection时，设置了其方位余4，即一定为0、1、2、3，必能在前4个index中找到唯一的false。
            var safeDir = _p2SafeDirection.IndexOf(false);
            var northPos = new Vector3(100, 0, 80);
            var myIndex = accessory.GetMyIndex();
            var isStGroup = myIndex % 2 == 1;
            // ST组在0、1、2、3
            var tposCenter =
                northPos.RotatePoint(_center, isStGroup ? safeDir * float.Pi / 4 : (safeDir + 4) * float.Pi / 4);
            var tposIn = tposCenter.PointInOutside(_center, 7.5f);
            var tposLeft = tposCenter.RotatePoint(_center, 20f.DegToRad());
            var tposRight = tposCenter.RotatePoint(_center, -20f.DegToRad());
            List<Vector3> tposList = [tposCenter, tposIn, tposLeft, tposRight];

            var dp = accessory.DrawGuidance(tposList[myIndex / 2], 0, 7000, $"P2一运安全区位置{myIndex}");
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

            _thrustEvent.Reset();
        }

        [ScriptMethod(name: "P2 一运分散安全位置指引消失", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:25548"], userControl: false)]
        public void P2_一运分散安全位置指引消失(Event @event, ScriptAccessory accessory)
        {
            if (_dsrPhase != DsrPhase.Phase2Strength) return;
            var myIndex = accessory.GetMyIndex();

            accessory.Method.RemoveDraw($"P2一运安全区位置{myIndex}");
        }

        [ScriptMethod(name: "P2 一运骑神边缘位置记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25550"], userControl: false)]
        public void P2_一运骑神边缘位置记录(Event @event, ScriptAccessory accessory)
        {
            if (_dsrPhase != DsrPhase.Phase2Strength) return;
            var spos = @event.SourcePosition();
            _p2ThordanPos = spos;
            _thordanCastAtEdgeEvent.Set();
        }

        [ScriptMethod(name: "P2 一运坦克接线提示", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:regex:^(255[01])$"], userControl: true)]
        public void P2_一运坦克接线提示(Event @event, ScriptAccessory sa)
        {
            if (_dsrPhase != DsrPhase.Phase2Strength) return;
            var myIndex = sa.GetMyIndex();
            if (myIndex > 1) return;
            _thordanCastAtEdgeEvent.WaitOne();
            lock (_p2TetherKnightId)
            {
                var sid = @event.SourceId();
                var sname = @event.SourceName();
                var spos = @event.SourcePosition();
                // var rad = spos.FindRadian(_p2ThordanPos);

                var atRight = spos.IsAtRight(_p2ThordanPos, _center);
                _p2TetherKnightId[atRight ? 1 : 0] = sid;

                // 此处Id为16进制转10进制表示
                sa.Log.Debug($"记录{sname}（对话{@event.Id()}）在{(atRight ? "右" : "左")}");

                if (_p2TetherKnightId.Contains(0)) return;
                var targetKnightIdx = myIndex == 0 ? 0 : 1;
                var chara = sa.GetById(_p2TetherKnightId[targetKnightIdx]);
                if (chara == null) return;

                var knightPos = chara.Position;
                var tetherEdgePos = _p2ThordanPos.RotatePoint(_center, (myIndex == 0 ? 1 : -1) * 18f.DegToRad());
                tetherEdgePos = tetherEdgePos.PointInOutside(_center, 3f);
                var dp = sa.DrawGuidance(knightPos, tetherEdgePos, 0, 10000, $"接线路径");
                sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }
        }

        [ScriptMethod(name: "P2 一运接线提示删除", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:25550"], userControl: false)]
        public void P2_一运接线提示删除(Event @event, ScriptAccessory sa)
        {
            if (_dsrPhase != DsrPhase.Phase2Strength) return;
            sa.Method.RemoveDraw($"接线路径");
            _thordanCastAtEdgeEvent.Reset();
        }

        #endregion

        #region 二运
        [ScriptMethod(name: "P2 二运记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25569"], userControl: false)]
        public void P2_二运记录(Event @event, ScriptAccessory accessory)
        {
            parse = 2.2;
        }

        [ScriptMethod(name: "P2 二运预站位", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25569"])]
        public void P2_二运预站位(Event @event, ScriptAccessory accessory)
        {
            var index = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P2_二运预站位";
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.Owner = accessory.Data.Me; 
            dp.DestoryAt = 4000;
            dp.Scale = new Vector2(2);
            dp.ScaleMode = ScaleMode.YByDistance;
            if (index == 0) dp.TargetPosition = new(92, 0, 99);
            if (index == 1) dp.TargetPosition = new(109, 0, 101);
            if (index == 2) dp.TargetPosition = new(92, 0, 99);
            if (index == 3) dp.TargetPosition = new(109, 0, 101);
            if (index == 4) dp.TargetPosition = new(92, 0, 99);
            if (index == 5) dp.TargetPosition = new(109, 0, 101);
            if (index == 6) dp.TargetPosition = new(92, 0, 99);
            if (index == 7) dp.TargetPosition = new(109, 0, 101);

            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

        }

        [ScriptMethod(name: "P2 二运龙眼背对", eventType: EventTypeEnum.EnvControl, eventCondition: ["DirectorId:8003759A", "Id:00020001"])]
        public void P2_二运龙眼背对(Event @event, ScriptAccessory accessory)
        {
            if (parse != 2.2) return;
            var index=int.Parse(@event["Index"],System.Globalization.NumberStyles.HexNumber);
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P2_二运龙眼背对";
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Owner = accessory.Data.Me;
            dp.Delay = 4500;
            dp.DestoryAt = 5000;
            if (index == 0) dp.TargetPosition = new(100, 0, 65);
            if (index == 1) dp.TargetPosition = new(124.75f, 0, 75.25f);
            if (index == 2) dp.TargetPosition = new(135, 0, 100);
            if (index == 3) dp.TargetPosition = new(124.75f, 0, 124.75f);
            if (index == 4) dp.TargetPosition = new(100, 0, 135);
            if (index == 5) dp.TargetPosition = new(75.25f, 0, 124.75f);
            if (index == 6) dp.TargetPosition = new(65, 0, 100);
            if (index == 7) dp.TargetPosition = new(75.25f, 0, 75.25f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.SightAvoid, dp);
        }
        [ScriptMethod(name: "P2 二运骑神背对", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25552"])]
        public void P2_二运骑神背对(Event @event, ScriptAccessory accessory)
        {
            if (parse != 2.2) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P2_二运骑神背对";
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Owner = accessory.Data.Me;
            if (ParseObjectId(@event["TargetId"], out var id))
            {
                dp.TargetObject = id;
            }
            dp.DestoryAt = 5000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.SightAvoid, dp);

        }
        [ScriptMethod(name: "P2 二运泽菲兰位置记录", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:2549"], userControl: false)]
        public void P2_二运泽菲兰位置记录(Event @event, ScriptAccessory accessory)
        {
            if (parse != 2.2) return;
            p2ZPos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
            
        }
        [ScriptMethod(name: "P2 二运劈刀记录", eventType: EventTypeEnum.TargetIcon, userControl: false)]
        public void P2_二运劈刀记录(Event @event, ScriptAccessory accessory)
        {
            if (parse != 2.2) return;
            var tid = ParsTargetIcon(@event["Id"]);
            if (tid != -279 && tid != -280) return;
            if (ParseObjectId(@event["TargetId"], out var id))
            {
                var index = accessory.Data.PartyList.ToList().IndexOf(id);
                if (tid == -280) p2Jump.Item1 = index;
                if (tid == -279) p2Jump.Item2 = index;
            }
        }
        [ScriptMethod(name: "P2 二运阿代尔菲尔位置", eventType: EventTypeEnum.SetObjPos, eventCondition: ["SourceDataId:12601"], userControl: false)]
        public void P2_二运阿代尔菲尔位置(Event @event, ScriptAccessory accessory)
        {
            if (parse != 2.2) return;
            p2AdelPos=JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
        }
        [ScriptMethod(name: "P2 二运劈刀起跑位置(Imgui)", eventType: EventTypeEnum.TargetIcon)]
        public void P2_二运劈刀起跑位置(Event @event, ScriptAccessory accessory)
        {
            if (parse != 2.2) return;
            if (ParsTargetIcon(@event["Id"]) != -279) return;
            Task.Delay(100).ContinueWith(t =>
            {
                // 默认分组：MT/H1/D1/D3(偶数index)为g1，ST/H2/D2/D4(奇数index)为g2
                var group = new int[8];
                for (int i = 0; i < 8; i++) group[i] = i % 2 == 0 ? 1 : 2;

                // 劈刀点名强制归组：-280(Item1)进g1，-279(Item2)进g2
                // 点名者若不在目标组，则与自己的对位(MT-ST/H1-H2/D1-D2/D3-D4)互换，保持4/4
                void ForceGroup(int index, int target)
                {
                    if (index < 0 || index > 7) return;
                    if (group[index] == target) return;
                    var partner = index ^ 1;
                    group[partner] = group[index];
                    group[index] = target;
                }

                ForceGroup(p2Jump.Item1, 1);
                ForceGroup(p2Jump.Item2, 2);


                var drot = p2AdelPos.X > 100? float.Pi / 45: float.Pi / -45;
                var meIndex = accessory.Data.PartyList.ToList().IndexOf(accessory.Data.Me);
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Scale = new(1.5f, 20);
                dp.Color = accessory.Data.DefaultSafeColor.WithW(3);
                dp.Owner = accessory.Data.Me;
                dp.DestoryAt = 5000;
                dp.ScaleMode |= ScaleMode.YByDistance;

                var cpos = new Vector3(100, 0, 100);
                var sPos = (p2ZPos - cpos) / 15 * 19.5f + cpos;
                if (meIndex >= 0 && meIndex < 8 && group[meIndex] == 1)
                {
                    dp.TargetPosition = RotatePoint(sPos, cpos, float.Pi + drot * 3);
                }
                else
                {
                    dp.TargetPosition = RotatePoint(sPos, cpos, drot * 3);
                }

                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);



                var dp2 = accessory.Data.GetDefaultDrawProperties();
                dp2.Color = accessory.Data.DefaultSafeColor.WithW(3);
                dp2.Scale = new(1.5f, 20);
                dp2.ScaleMode |= ScaleMode.YByDistance;
                dp2.DestoryAt = 15000;
                dp2.Position=dp.TargetPosition;
                dp2.TargetPosition = RotatePoint(dp2.Position.Value, cpos, drot * 5);
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp2);
            });
        }
        [ScriptMethod(name: "P2 二运光球爆炸范围", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:13070"])]
        public void P2_二运光球爆炸范围(Event @event, ScriptAccessory accessory)
        {
            if (parse != 2.2) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Scale = new(9);
            dp.Color = accessory.Data.DefaultDangerColor;
            var idStr = @event["SourceId"];
            if (ParseObjectId(idStr, out var id))
            {
                dp.Owner = id;
            }
            dp.DestoryAt = 2000;
            dp.Name = $"P2二运光球爆炸范围{idStr}";

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        [ScriptMethod(name: "P2 光球爆炸范围移除", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:25295"],userControl:false)]
        public void P2_光球爆炸范围移除(Event @event, ScriptAccessory accessory)
        {
            if (parse != 2.2) return;
            accessory.Method.RemoveDraw($"P2二运光球爆炸范围{@event["SourceId"]}");
        }
        [ScriptMethod(name: "P2 二运陨石记录", eventType: EventTypeEnum.TargetIcon,userControl:false)]
        public void P2_2运陨石记录(Event @event, ScriptAccessory accessory)
        {
            if (parse != 2.2) return;
            if (ParsTargetIcon(@event["Id"]) != -45) return;
            if (ParseObjectId(@event["TargetId"], out var id))
            {
                p2Stone[accessory.Data.PartyList.ToList().IndexOf(id)] = true;
            }
            var s1 = p2Stone.IndexOf(true);
            var s2 = p2Stone.LastIndexOf(true);
            //记录分组
            // if (s1 != s2)
            // {
            //     p2StoneMem = (s1, s2);
            //     //AB mt h2
            //     if (s1 == 0 && s2 == 3)
            //     {
            //         p2StoneTeam = [0, 6, 5, 1, 3, 7, 6, 2];
            //     }
            //     //AB d14
            //     if (s1 == 4 && s2 == 7)
            //     {
            //         p2StoneTeam = [4, 0, 5, 1, 7, 3, 6, 2];
            //     }
            //     //AC 双t
            //     if (s1 == 0 && s2 == 1)
            //     {
            //         p2StoneTeam = [0, 4, 7, 3, 1, 5, 6, 2];
            //     }
            //     //AC d12
            //     if (s1 == 4 && s2 == 5)
            //     {
            //         p2StoneTeam = [4, 0, 7, 3, 5, 1, 6, 2];
            //     }
            //     //AD mt H1
            //     if (s1 == 0 && s2 == 2)
            //     {
            //         p2StoneTeam = [0, 4, 7, 3, 2, 6, 5, 1];
            //     }
            //     //AD d13
            //     if (s1 == 4 && s2 == 6)
            //     {
            //         p2StoneTeam = [4, 0, 7, 3, 6, 2, 5, 1];
            //     }
            //     //BC h2 st
            //     if (s1 == 1 && s2 == 3)
            //     {
            //         p2StoneTeam = [3, 7, 4, 0, 1, 5, 6, 2];
            //     }
            //     //BC d24
            //     if (s1 == 5 && s2 == 7)
            //     {
            //         p2StoneTeam = [7, 3, 4, 0, 5, 1, 6, 2];
            //     }
            //     //BD h12
            //     if (s1 == 2 && s2 == 3)
            //     {
            //         p2StoneTeam = [4, 0, 3, 7, 5, 1, 2, 6];
            //     }
            //     //BD d34
            //     if (s1 == 6 && s2 == 7)
            //     {
            //         p2StoneTeam = [4, 0, 7, 3, 5, 1, 6, 2];
            //     }
            //     //CD st h1
            //     if (s1 == 1 && s2 == 2)
            //     {
            //         p2StoneTeam = [2, 6, 7, 3, 1, 5, 4, 0];
            //     }
            //     //CD d23
            //     if (s1 == 5 && s2 == 6)
            //     {
            //         p2StoneTeam = [6, 2, 7, 3, 5, 1, 4, 0];
            //     }
            // }
            if (s1 != s2)
            {
                p2StoneMem = (s1, s2);

                // 基础站位：
                // N: MT D3
                // E: H2 D2
                // S: H1 D4
                // W: ST D1
                p2StoneTeam = [0, 6, 3, 5, 2, 7, 1, 4];

                HashSet<int> meteorPlayers = [s1, s2];

                foreach (var meteor in meteorPlayers)
                {
                    var pos = p2StoneTeam.IndexOf(meteor);

                    // 0=N, 1=E, 2=S, 3=W
                    var group = pos / 2;

                    // 0=TN lane, 1=DPS lane
                    var lane = pos % 2;

                    // 已经在 N / S，不需要移动
                    if (group == 0 || group == 2)
                        continue;

                    int clockwiseGroup;
                    int counterClockwiseGroup;

                    if (group == 1)
                    {
                        // E -> 顺时针 S，逆时针 N
                        clockwiseGroup = 2;
                        counterClockwiseGroup = 0;
                    }
                    else
                    {
                        // W -> 顺时针 N，逆时针 S
                        clockwiseGroup = 0;
                        counterClockwiseGroup = 2;
                    }

                    // 只找同 lane 的对位
                    var clockwisePos = clockwiseGroup * 2 + lane;
                    var counterClockwisePos = counterClockwiseGroup * 2 + lane;

                    // 默认顺时针
                    var targetPos = clockwisePos;

                    // 顺时针对位本身也是陨石 -> 改走逆时针
                    if (meteorPlayers.Contains(p2StoneTeam[clockwisePos]))
                    {
                        targetPos = counterClockwisePos;
                    }

                    // 只交换陨石本人和对位，不移动整组
                    (p2StoneTeam[pos], p2StoneTeam[targetPos]) =
                        (p2StoneTeam[targetPos], p2StoneTeam[pos]);
                }
            }
        }
        [ScriptMethod(name: "P2 二运陨石连线(ImGui)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25576"])]
        public void P2_2运陨石连线(Event @event, ScriptAccessory accessory)
        {
            if (parse != 2.2) return;
            Task.Delay(100).ContinueWith(t =>
            {
                
                var s1 = p2Stone.IndexOf(true);
                var s2 = p2Stone.LastIndexOf(true);
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.ScaleMode |= ScaleMode.YByDistance;
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.Owner = accessory.Data.PartyList[s1];
                dp.TargetObject = accessory.Data.PartyList[s2];
                dp.DestoryAt = 12000;
                dp.Name = "P2 2运陨石双人连线(ImGui)";
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, dp);
            });

        }
        [ScriptMethod(name: "P2 二运冰分摊位置(ImGui)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25576"])]
        public void P2_2运冰分摊位置(Event @event, ScriptAccessory accessory)
        {
            if (parse != 2.2) return;
            Task.Delay(100).ContinueWith(t =>
            {
                var idIndex = accessory.Data.PartyList.ToList().IndexOf(accessory.Data.Me);
                var dir4=p2StoneTeam.IndexOf(idIndex)/2;
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = "P2 2运冰分摊位置(ImGui)";
                dp.Scale = new(3f, 10);
                dp.ScaleMode |= ScaleMode.YByDistance;
                dp.Color = accessory.Data.DefaultSafeColor;
                dp.Owner = accessory.Data.Me;
                dp.TargetPosition = RotatePoint(new(100,0,88.5f),new(100,0,100),float.Pi/2*dir4);
                dp.DestoryAt = 7000;

                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            });
            

            
            //accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);


        }
        [ScriptMethod(name: "P2 二运第一轮塔记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:29564"],userControl:false)]
        public void P2_二运第一轮塔记录(Event @event, ScriptAccessory accessory)
        {
            if (parse != 2.2) return;
            var sourcePos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
            var cpos = new Vector3(100, 0, 100);
            if ((sourcePos-cpos).Length() > 7)
            {
                var dir = (PositionTo12Dir(sourcePos, cpos) + 1) % 12;
                p2Tower[dir] = true;
            }
            else
            {
                var dir = PositionTo8Dir(sourcePos, cpos) / 2 + 12;
                p2Tower[dir] = true;
            }
        }
        [ScriptMethod(name: "P2 二运第一轮塔位置(ImGui)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:29563"])]
        public void P2_二运第一轮塔位置(Event @event, ScriptAccessory accessory)
        {
            if (parse != 2.2) return;
            Task.Delay(100).ContinueWith(t =>
            {
                List<int> towerMem = [-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1];
                List<int> alternate = [];
                //高优先级
                for (int i = 0; i < 4; i++)
                {
                    var MemIndex = p2StoneTeam[i * 2];
                    //中
                    if (p2Tower[i * 3 + 1])
                    {
                        towerMem[i * 3 + 1] = MemIndex;
                        continue;
                    }
                    //左
                    if (p2Tower[i * 3])
                    {
                        towerMem[i * 3] = MemIndex;
                        continue;
                    }
                    //右
                    if (p2Tower[i * 3 + 2])
                    {
                        towerMem[i * 3 + 2] = MemIndex;
                        continue;
                    }
                }

                //低优先级
                for (int i = 0; i < 4; i++)
                {
                    var MemIndex = p2StoneTeam[i * 2 + 1];
                    //左
                    if (p2Tower[i * 3] && towerMem[i * 3] == -1)
                    {
                        towerMem[i * 3] = MemIndex;
                        continue;
                    }
                    //右
                    if (p2Tower[i * 3 + 2] && towerMem[i * 3 + 2] == -1)
                    {
                        towerMem[i * 3 + 2] = MemIndex;
                        continue;
                    }
                    //内左
                    if (p2Tower[i + 12] && towerMem[i + 12] == -1)
                    {
                        towerMem[i + 12] = MemIndex;
                        continue;
                    }
                    //补塔
                    alternate.Add(MemIndex);
                }

                //补塔
                foreach (var mem in alternate)
                {
                    for (int i = 12; i < 16; i++)
                    {
                        if (p2Tower[i] && towerMem[i] == -1)
                        {
                            towerMem[i] = mem;
                            break;
                        }
                    }
                }

                var idIndex = accessory.Data.PartyList.ToList().IndexOf(accessory.Data.Me);
                var npos = new Vector3(100, 0, 82);
                var npos2 = new Vector3(100, 0, 94);
                var cpos = new Vector3(100, 0, 100);
                var dp = accessory.Data.GetDefaultDrawProperties();
                var tIndex = towerMem.IndexOf(idIndex);
                if (tIndex >= 0 && tIndex < 12)
                {
                    dp.Position = RotatePoint(npos, cpos, float.Pi / 6 * (tIndex - 1));
                }
                if (tIndex >= 12 && tIndex < 16)
                {
                    dp.Position = RotatePoint(npos2, cpos, float.Pi / 2 * (tIndex - 12) + float.Pi / 4);
                }

                dp.Name = "P2 2运第一轮塔位置(ImGui)";
                dp.DestoryAt = 12000;
                dp.Color = accessory.Data.DefaultSafeColor;
                dp.Scale = new(3);
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);

                var dp2 = accessory.Data.GetDefaultDrawProperties();
                dp2.Name = "P2 2运第一轮塔位置(ImGui)";
                dp2.Color= accessory.Data.DefaultSafeColor;
                dp2.Owner = accessory.Data.Me;
                dp2.TargetPosition = dp.Position;
                dp2.Scale = new(3f, 10);
                dp2.ScaleMode |= ScaleMode.YByDistance;
                dp2.Delay = 7500;
                dp2.DestoryAt = 4500;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp2);

            });
        }
        [ScriptMethod(name: "P2 二运第二轮塔位置(ImGui)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:28650"])]
        public void P2_二运第二轮塔位置(Event @event, ScriptAccessory accessory)
        {
            if (parse != 2.2) return;

            var index = accessory.Data.PartyList.ToList().IndexOf(accessory.Data.Me);
            var posIndex =p2StoneTeam.IndexOf(index);
            if (index == p2StoneMem.Item1) posIndex = p2StoneTeam.IndexOf(p2StoneMem.Item2);
            if (index == p2StoneMem.Item2) posIndex = p2StoneTeam.IndexOf(p2StoneMem.Item1);

            var npos = new Vector3(100, 0, 82);
            var cpos = new Vector3(100, 0, 100);
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.TargetPosition = RotatePoint(npos, cpos, float.Pi / 4 * posIndex);
            

            dp.Name = "P2 2运第一轮塔位置(ImGui)";
            dp.DestoryAt = 11000;
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.Position = dp.TargetPosition;
            dp.Scale = new(3);
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);


        }
        [ScriptMethod(name: "P2 二运阶段记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25569"], userControl: false)]
        public void P2_二运阶段记录(Event @event, ScriptAccessory sa)
        {
            _dsrPhase = DsrPhase.Phase2Sancity;
            sa.Log.Debug($"当前阶段为：{_dsrPhase}");
        }

        #endregion
        [ScriptMethod(name: "P2 二运结束记录", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:25533"],userControl:false)]
        public void P2_二运结束记录(Event @event, ScriptAccessory accessory)
        {
            parse = 2.3;
        }
        [ScriptMethod(name: "P2 骑神奋力一挥（右)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25536"])]
        public void P2_骑神奋力一挥_右(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultDangerColor;
            if (ParseObjectId(@event["SourceId"], out var id))
            {
                dp.Owner = id;
            }

            dp.Scale = new(40);
            dp.Radian = float.Pi / 180 * 130;
            dp.Rotation= float.Pi / 180 * -65;
            dp.DestoryAt = 4000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

            dp.Color = accessory.Data.DefaultSafeColor;
            dp.TargetColor = accessory.Data.DefaultDangerColor;
            dp.Rotation = float.Pi;
            dp.DestoryAt = 6000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

        }
        [ScriptMethod(name: "P2 骑神奋力一挥（左)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25537"])]
        public void P2_骑神奋力一挥_左(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultDangerColor;
            if (ParseObjectId(@event["SourceId"], out var id))
            {
                dp.Owner = id;
            }

            dp.Scale = new(40);
            dp.Radian = float.Pi / 180 * 130;
            dp.Rotation = float.Pi / 180 * 65;
            dp.DestoryAt = 4000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

            dp.Color = accessory.Data.DefaultSafeColor;
            dp.TargetColor = accessory.Data.DefaultDangerColor;
            dp.Rotation = float.Pi;
            dp.DestoryAt = 6000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

        }

        #endregion

        #region P3

        [ScriptMethod(name: "---- 《P3 尼德霍格》 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld"],
            userControl: true)]
        public void P3_分节线(Event @event, ScriptAccessory accessory)
        {
        }

        [ScriptMethod(name: "P3 记录", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:26376"],userControl:false)]
        public void P3_记录(Event @event, ScriptAccessory accessory)
        {
            parse = 3;
            if (ParseObjectId(@event["SourceId"], out var id))
            {
                p3BossId = id;
            }
        }
        [ScriptMethod(name: "P3 牙尾连旋(钢铁月环)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:26386"])]
        public void P3_牙尾连旋(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultDangerColor;
            if (ParseObjectId(@event["SourceId"], out var id))
            {
                dp.Owner = id;
            }

            dp.Scale = new(8);
            dp.Radian = float.Pi *2;
            dp.DestoryAt = 11500;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            dp.Scale = new(40);
            dp.InnerScale = new(8);
            dp.Delay = 11500;
            dp.DestoryAt = 3000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);

        }
        [ScriptMethod(name: "P3 尾牙连旋(月环钢铁)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:26387"])]
        public void P3_尾牙连旋(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultDangerColor;
            if (ParseObjectId(@event["SourceId"], out var id))
            {
                dp.Owner = id;
            }

            dp.Scale = new(40);
            dp.InnerScale= new(8);
            dp.Radian = float.Pi * 2;
            dp.DestoryAt = 11500;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);

            dp.Delay = 11500;
            dp.Scale = new(8);
            dp.DestoryAt = 3000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        }
        [ScriptMethod(name: "P3 原地塔预测", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:26382"])]
        public void P3_原地塔预测(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultSafeColor;
            if (ParseObjectId(@event["SourceId"], out var id))
            {
                dp.Owner = id;
            }
            dp.Scale = new(5);
            dp.DestoryAt = 4000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        [ScriptMethod(name: "P3 上箭头塔预测", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:26383"])]
        public void P3_上箭头塔预测(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultSafeColor;
            if (ParseObjectId(@event["SourceId"], out var id))
            {
                dp.Owner = id;
            }
            dp.Offset = new(0, 0, -14);
            dp.Scale = new(5);
            dp.DestoryAt = 4000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        [ScriptMethod(name: "P3 下箭头塔预测", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:26384"])]
        public void P3_下箭头塔预测(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultSafeColor;
            if (ParseObjectId(@event["SourceId"], out var id))
            {
                dp.Owner = id;
            }
            dp.Offset = new(0, 0, -14);
            dp.Scale = new(5);
            dp.DestoryAt = 4000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        [ScriptMethod(name: "P3 塔位置确定", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:26385"])]
        public void P3_塔位置确定(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultSafeColor;
            if (ParseObjectId(@event["SourceId"], out var id))
            {
                dp.Owner = id;
            }
            dp.Scale = new(5);
            dp.DestoryAt = 2500;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        [ScriptMethod(name: "P3 麻将武神枪引导", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:26385"])]
        public void P3_麻将武神枪引导(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultDangerColor;
            if (ParseObjectId(@event["SourceId"], out var id))
            {
                dp.Owner = id;
            }
            dp.Scale = new(8,62);
            dp.DestoryAt = 2500;
            dp.TargetResolvePattern=PositionResolvePatternEnum.PlayerNearestOrder;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
        [ScriptMethod(name: "P3 四塔武神枪引导", eventType: EventTypeEnum.StartCasting)]
        public void P3_四塔武神枪引导(Event @event, ScriptAccessory accessory)
        {
            if (parse != 3) return;
            var aid = @event["ActionId"];
            if(aid!= "26391" && aid != "26392" && aid != "26393" && aid != "26394") return;
            var str = @event["SourceId"];
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Name = $"P3_四塔武神枪引导{str}";
            if (ParseObjectId(str, out var id))
            {
                dp.Owner = id;
            }
            dp.Scale = new(8, 62);
            dp.Delay = 5000;
            dp.DestoryAt = 2500;
            dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
        [ScriptMethod(name: "P3 四塔武神枪移除", eventType: EventTypeEnum.Tether, eventCondition: ["Id:0054"],userControl:false)]
        public void P3_四塔武神枪移除(Event @event, ScriptAccessory accessory)
        {
            if (parse != 3) return;
            accessory.Method.RemoveDraw($"P3_四塔武神枪引导{@event["SourceId"]}");
        }
        [ScriptMethod(name: "P3 武神枪确定", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:26378"])]
        public void P3_武神枪确定(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultDangerColor;
            if (ParseObjectId(@event["SourceId"], out var id))
            {
                dp.Owner = id;
            }
            dp.Scale = new(8, 62);
            dp.DestoryAt = 4500;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
        [ScriptMethod(name: "P3 同组麻将连线(ImGui)", eventType: EventTypeEnum.StatusAdd)]
        public void P3_同组麻将连线(Event @event, ScriptAccessory accessory)
        {
            if (parse != 3) return;
            var stasusid = @event["StatusID"];
            if (stasusid != "3004" && stasusid != "3005" && stasusid != "3006") return;
            if (ParseObjectId(@event["TargetId"], out var id))
            {
                if (p3majong.ContainsKey(stasusid))
                {
                    p3majong[stasusid].Add(id);
                }
                else
                {
                    p3majong.Add(stasusid, []);
                    p3majong[stasusid].Add(id);
                }
            }


            if (id == accessory.Data.Me)
            {
                Task.Delay(100).ContinueWith((o) =>
                {
                    foreach (var tid in p3majong[stasusid])
                    {
                        var dp=accessory.Data.GetDefaultDrawProperties();
                        dp.Name = "P3 同组麻将连线";
                        dp.Owner = id;
                        dp.TargetObject = tid;
                        dp.Color=accessory.Data.DefaultSafeColor;
                        dp.DestoryAt = 6000;
                        dp.ScaleMode |= ScaleMode.YByDistance;
                        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, dp);
                    }
                });

            }
        }
        [ScriptMethod(name: "P3 腾龙枪", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:26380"])]
        public void P3_腾龙枪(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultDangerColor;
            if (ParseObjectId(@event["SourceId"], out var id))
            {
                dp.Owner = id;
            }
            dp.Scale = new(13);
            dp.Radian = float.Pi / 2;
            dp.DestoryAt = 3500;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }
        [ScriptMethod(name: "P3 四塔记录", eventType: EventTypeEnum.StartCasting,userControl:false)]
        public void P3_四塔记录(Event @event, ScriptAccessory accessory)
        {
            if (parse != 3) return;
            var aid = @event["ActionId"];
            if (aid != "26391" && aid != "26392" && aid != "26393" && aid != "26394") return;
            var num=int.Parse(aid)-26390;
            var sourcePos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
            var dir = PositionTo8Dir(sourcePos, new(100, 0, 100))/2;
            p3Tower[dir] = num;
        }
        [ScriptMethod(name: "P3 四塔站位(ImGui)", eventType: EventTypeEnum.StartCasting)]
        public void P3_四塔站位(Event @event, ScriptAccessory accessory)
        {
            if (parse != 3 || p3TowerDeal) return;
            var aid = @event["ActionId"];
            if (aid != "26391" && aid != "26392" && aid != "26393" && aid != "26394") return;
            p3TowerDeal = true;
            
            Task.Delay(100).ContinueWith(t =>
            {
                var idIndex = accessory.Data.PartyList.ToList().IndexOf(accessory.Data.Me);
                var myTower = -1;
                //D4
                if (idIndex == 7) { myTower = 0; }
                //H2
                if (idIndex == 3) { myTower = 1; }
                //H1
                if (idIndex == 2) { myTower = 2; }
                //D3
                if (idIndex == 6) { myTower = 3; }
                //St
                if (idIndex == 1) 
                {
                    
                    if (p3Tower[0] >= 2) { myTower = 0;}
                    else
                    {
                        if (p3Tower[1] > 2) { myTower = 1; }
                        else if (p3Tower[3] > 2) { myTower = 3; }
                        else if (p3Tower[2] > 2) { myTower = 2; }
                    }
                }
                //D2
                if (idIndex == 5)
                {
                    if (p3Tower[1] >= 2) { myTower = 1; }
                    else
                    {
                        if (p3Tower[2] > 2) { myTower = 2; }
                        else if (p3Tower[0] > 2) { myTower = 0; }
                        else if (p3Tower[3] > 2) { myTower = 3; }
                    }
                }
                //D1
                if (idIndex == 4)
                {
                    if (p3Tower[2] >= 2) { myTower = 2; }
                    else
                    {
                        if (p3Tower[3] > 2) { myTower = 3; }
                        else if (p3Tower[1] > 2) { myTower = 1; }
                        else if (p3Tower[0] > 2) { myTower = 0; }
                    }
                }
                //Mt
                if (idIndex == 0)
                {
                    if (p3Tower[3] >= 2) { myTower = 3; }
                    else
                    {
                        if (p3Tower[0] > 2) { myTower = 0; }
                        else if (p3Tower[2] > 2) { myTower = 2; }
                        else if (p3Tower[1] > 2) { myTower = 1; }
                    }
                }

                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Color = accessory.Data.DefaultSafeColor;
                dp.Position=RotatePoint(new(108,0,92),new(100,0,100),float.Pi/2*myTower);
                dp.Scale = new(5);
                dp.DestoryAt = 5000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);

            });
        }
        [ScriptMethod(name: "P3 追魂炮T辅助(ImGui)", eventType: EventTypeEnum.Tether, eventCondition: ["Id:0054"])]
        public void P3_追魂T炮辅助(Event @event, ScriptAccessory accessory)
        {
            if (parse != 3) return;
            if (!ParseObjectId(@event["SourceId"], out var id)) return;
            var idIndex = accessory.Data.PartyList.ToList().IndexOf(accessory.Data.Me);
            if ((idIndex == 0 && id == p3BossId && !p3Boom[0]) || (idIndex == 1 && id != p3BossId && !p3Boom[1]))
            {
                p3Boom[idIndex] = true;
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"P3 追魂炮{(idIndex==0?"M":"S")}T辅助";
                dp.Color = accessory.Data.DefaultSafeColor;
                dp.Owner = id;
                dp.Scale = new(10);
                dp.TargetResolvePattern = PositionResolvePatternEnum.OwnerTarget;
                dp.ScaleMode |= ScaleMode.YByDistance;
                dp.DestoryAt = 7000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, dp);
            }
        }
        [ScriptMethod(name: "P3 追魂炮范围", eventType: EventTypeEnum.Tether, eventCondition: ["Id:0054"])]
        public void P3_追魂炮范围(Event @event, ScriptAccessory accessory)
        {
            if (parse != 3) return;
            if (!ParseObjectId(@event["SourceId"], out var id)) return;
            
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P3_追魂炮范围";
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Owner = id;
            dp.Scale = new(5);
            dp.CentreResolvePattern = PositionResolvePatternEnum.OwnerTarget;
            dp.DestoryAt = 7000;
            if (id == p3BossId && !p3Boom[2])
            {
                p3Boom[2] = true;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }
            if (id != p3BossId && !p3Boom[3])
            {
                p3Boom[3] = true;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }
        }

        [ScriptMethod(name: "P3 阶段记录", eventType: EventTypeEnum.ActionEffect,
            eventCondition: ["ActionId:26376"], userControl: Debugging)]
        public void P3_阶段记录(Event ev, ScriptAccessory sa)
        {
            _dsrPhase = DsrPhase.Phase3Nidhogg;
            _p3DfgEnable = false;
            // 百位：一麻+0，二麻+100，三麻+100
            // 十位：下箭头+0，中+10，下箭头+20
            // 个位：左中右站位分别+0, +1, +2
            // 如此安排，个位可随时变，十位改变后，个位无力干涉
            _dfg.Init(sa, "堕天龙炎冲");
            _p3TowerAppearPos = [];
            sa.Log.Debug($"当前阶段为：{_dsrPhase}");
        }

        [ScriptMethod(name: "P3 堕天龙炎冲流程指路", eventType: EventTypeEnum.StatusAdd,
            eventCondition:["StatusID:regex:^(300[456])$"], userControl: true)]
        public void P3_堕天龙炎冲流程指路(Event ev, ScriptAccessory sa)
        {
            if (_dsrPhase != DsrPhase.Phase3Nidhogg) return;
            _p3DfgEnable = true;
            var stid = ev.StatusId;
            var tid = ev.TargetId;
            var tidx = sa.GetPlayerIdIndex(tid);

            var lmVal = stid switch
            {
                3004 => 0,      // 一麻
                3005 => 100,    // 二麻
                3006 => 200,    // 三麻
                _ => 0
            };
            lock (_dfg)
            {
                // 前三位一麻，中二位二麻，后三位三麻
                _dfg.AddPriority(tidx, lmVal);
                sa.Log.Debug($"玩家 {sa.GetPlayerJobByIndex(tidx)} 为 {lmVal/100+1} 麻。");
            }
        }

        [ScriptMethod(name: "P3 箭头记录", eventType: EventTypeEnum.StatusAdd,
            eventCondition:["StatusID:regex:^(275[567])$"], userControl: Debugging)]
        public void P3_箭头记录(Event ev, ScriptAccessory sa)
        {
            if (_dsrPhase != DsrPhase.Phase3Nidhogg) return;
            if (!_p3DfgEnable) return;
            lock (_dfg)
            {
                var stid = ev.StatusId;
                var tid = ev.TargetId;
                var tidx = sa.GetPlayerIdIndex(tid);

                var dirVal = stid switch
                {
                    2756 => 20, // 上箭头，上B
                    2757 => 0, // 下箭头，下D
                    2755 => 10, // 原地，中
                    _ => 10
                };

                _dfg.AddPriority(tidx, dirVal);
                _dfg.AddActionCount();
                sa.Log.Debug($"玩家 {sa.GetPlayerJobByIndex(tidx)} 为 {dirVal switch
                {
                    0 => "下箭头",
                    10 => "原地",
                    _ => "上箭头"
                }}。");

                if (_dfg.ActionCount != 8) return;

                // 获得自身数值，并依据方位更新
                var myPriority = _dfg.Priorities[sa.GetMyIndex()];
                P3_刷新同组左右位置(sa, myPriority);
                sa.Log.Debug($"玩家在 {_dfg.Annotation} 机制的数值为：{myPriority}");
            }
        }

        private void P3_刷新同组左右位置(ScriptAccessory sa, int myPriority)
        {
            // 获得同组玩家Id
            var myGroupVal = (myPriority / 100) switch
            {
                // 此处取值含义为
                // 十位：从第几个开始取
                // 个位：取几个玩家
                0 => 3,
                1 => 32,
                2 => 53,
                _ => 0
            };

            if (myGroupVal == 0)
            {
                sa.Log.Error($"GetDfgGroupPlayers 中 myGroupVal == 0");
                return;
            }

            var myGroupDict = _dfg.SelectMiddlePriorityIndices(myGroupVal / 10, myGroupVal % 10);
            List<KeyValuePair<int, ulong>> myGroupPlayerIds = [];
            for (int i = 0; i < myGroupVal % 10; i++)
            {
                var pidx = myGroupDict[i].Key;
                var eid = sa.Data.PartyList[pidx];
                var prior = myGroupDict[i].Value;
                myGroupPlayerIds.Add(new KeyValuePair<int, ulong>(pidx, eid));
                sa.Log.Debug($"与我同组的玩家有{sa.GetPlayerJobByIndex(pidx)}，其优先级数值为{prior}, EntityId为{eid}");
            }

            // 根据同组左右位置排序
            var sortedGroupPlayerIds = myGroupPlayerIds
                .OrderBy(v => sa.GetById(v.Value).Position.X)
                .ToList();

            // 根据排序为优先级字典添加值
            for (int i = 0; i < sortedGroupPlayerIds.Count; i++)
            {
                var pidx = sortedGroupPlayerIds[i].Key;
                // 删除个位
                _dfg.Priorities[pidx] = _dfg.Priorities[pidx] / 10 * 10;
                _dfg.AddPriority(pidx, i);

                sa.Log.Debug($"检测到{sa.GetPlayerJobByIndex(pidx)}在{P3_取麻将方位字符(i, sortedGroupPlayerIds.Count == 2)}，更新其优先级值为{_dfg.Priorities[pidx]}");
            }
        }

        private string P3_取麻将方位字符(int myDfgIdx, bool isSecondRound = false)
        {
            var str = myDfgIdx switch
            {
                0 => "左",
                1 => "中",
                2 => "右",
                3 => "左",
                4 => "右",
                5 => "左",
                6 => "中",
                7 => "右",
                _ => "未知"
            };

            if (isSecondRound && myDfgIdx is 0 or 1)
                str = myDfgIdx == 1 ? "右" : "左";
            return str;
        }

        private Vector3 P3_取麻将塔坐标(int myDfgIdx)
        {
            var towerPos = myDfgIdx switch
            {
                0 => new Vector3(_center.X - 7.5f, 0, _center.Z),
                1 => new Vector3(_center.X, 0, _center.Z + 7.5f),
                2 => new Vector3(_center.X + 7.5f, 0, _center.Z),
                3 => new Vector3(91.75f, 0, 90.8f),
                4 => new Vector3(108.25f, 0, 90.8f),
                5 => new Vector3(_center.X - 7.5f, 0, _center.Z),
                6 => new Vector3(_center.X, 0, _center.Z + 7.5f),
                7 => new Vector3(_center.X + 7.5f, 0, _center.Z),
                _ => new Vector3(0, 0, 0)
            };
            return towerPos;
        }

        [ScriptMethod(name: "P3 麻将放塔与分摊", eventType: EventTypeEnum.StartCasting,
            eventCondition:["ActionId:regex:^(2638[67])$"], userControl: Debugging)]
        public void P3_麻将放塔与分摊(Event @event, ScriptAccessory sa)
        {
            if (_dsrPhase != DsrPhase.Phase3Nidhogg) return;
            if (!_p3DfgEnable) return;
            _dfg.AddActionCount(10);
            // 仅需获得排序，便可知麻将流程
            var myPriority = _dfg.Priorities[sa.GetMyIndex()];
            var myDfgIdx = _dfg.FindPriorityIndexOfKey(sa.GetMyIndex());
            var hasArrow = myPriority / 10 % 10 != 1;
            var posStr = P3_取麻将方位字符(myDfgIdx, myDfgIdx is 3 or 4);
            var towerPos = P3_取麻将塔坐标(myDfgIdx);

            const int lashGnashCastTime = 7600;
            const int inOutCastFirst = 3700;
            const int inOutCastSecond = 3100;
            const int towerExistTime = 6800;

            if (_dfg.ActionCount == 18) // 正常情况下，第一轮钢铁月环读条时，该值为18。期间五次放塔点名，第二轮钢铁月环读条时，该值为33。
            {
                switch (myDfgIdx)
                {
                    case 0:
                    case 1:
                    case 2:
                        sa.Log.Debug($"一麻{posStr} 第一轮，先去{posStr}{towerPos}放塔，再回人群");
                        P3_绘制塔指路(towerPos, 0, lashGnashCastTime, $"放塔1", sa);
                        // 十位数代表箭头，若为1则是原地，无需画面向
                        P3_绘制塔面向(towerPos, 0, lashGnashCastTime, $"放塔1面向", sa, hasArrow);
                        P3_绘制回人群(lashGnashCastTime, towerExistTime, $"人群", sa);
                        break;
                    case 3:
                    case 4:
                        sa.Log.Debug($"二麻{posStr} 第一轮，先回人群，再去{posStr}{towerPos}放塔");
                        P3_绘制回人群(0, lashGnashCastTime, $"人群", sa);
                        const int jump2DelayTime = lashGnashCastTime + inOutCastFirst + inOutCastSecond;
                        const int jump2Destroy = 17700 - jump2DelayTime;  // 17700 从下方时间节点处取
                        P3_绘制塔指路(towerPos, jump2DelayTime, jump2Destroy, $"放塔2", sa);
                        P3_绘制塔面向(towerPos, jump2DelayTime, jump2Destroy, $"放塔2面向", sa, hasArrow);
                        break;
                    case 5:
                    case 6:
                    case 7:
                        sa.Log.Debug($"三麻{posStr} 第一轮，回人群");
                        P3_绘制回人群(0, lashGnashCastTime, $"人群", sa);
                        break;
                }
            }
            else if (_dfg.ActionCount == 33)
            {
                switch (myDfgIdx)
                {
                    case 0:
                    case 2:
                        sa.Log.Debug($"一麻{posStr} 第二轮，引导后回人群");
                        P3_绘制回人群(26900 - 21500, 28900 - 26900, $"分摊", sa);
                        break;
                    case 1:
                        sa.Log.Debug($"一麻{posStr} 第二轮，回人群");
                        P3_绘制回人群(0, lashGnashCastTime, $"分摊", sa);
                        break;
                    case 3:
                    case 4:
                        sa.Log.Debug($"二麻{posStr} 第二轮，回人群");
                        P3_绘制回人群(0, lashGnashCastTime, $"分摊", sa);
                        break;
                    case 5:
                    case 6:
                    case 7:
                        sa.Log.Debug($"三麻{posStr}第二轮，先去{posStr}{towerPos}放塔，再回人群");
                        P3_绘制塔指路(towerPos, 0, lashGnashCastTime, $"放塔", sa);
                        P3_绘制塔面向(towerPos, 0, lashGnashCastTime, $"放塔3面向", sa, hasArrow);
                        P3_绘制回人群(lashGnashCastTime, towerExistTime, $"人群", sa);
                        break;
                }
            }
            else
            {
                sa.Log.Error($"P3_麻将放塔与分摊 出错，_dfg.ActionCount = {_dfg.ActionCount}");
            }
        }

        [ScriptMethod(name: "P3 麻将踩塔指路", eventType: EventTypeEnum.ActionEffect, 
            eventCondition:["ActionId:regex:^(2638[234])$", "TargetIndex:1"], userControl: Debugging)]
        public void P3_麻将踩塔指路(Event ev, ScriptAccessory sa)
        {
            if (_dsrPhase != DsrPhase.Phase3Nidhogg) return;
            // 此举动为放塔，若玩家组不按预站位处理，此时有机会对脚本进行调整
            if (!_p3DfgEnable) return;
            lock (_dfg)
            {
                _dfg.AddActionCount();
                var tid = ev.TargetId;
                var aid = ev.ActionId;
                var sid = ev.SourceId;
                var myDfgIdx = _dfg.FindPriorityIndexOfKey(sa.GetMyIndex());
                // 后面生成塔位置的sid已经不是原来的sid了，需要在这里找到他经偏置后的位置
                var tpos = P3_取塔生成坐标(sa, sid, aid);
                _p3TowerAppearPos.Add(tpos);

                var towerRound = _dfg.ActionCount switch
                {
                    21 => 0,
                    23 => 1,
                    36 => 2,
                    _ => -1
                };
                if (towerRound == -1)
                {
                    sa.Log.Debug($"_dfg.ActionCount == {_dfg.ActionCount}，未到数值，退出");
                    return;
                }

                var myPriority = _dfg.Priorities[sa.GetMyIndex()];
                // 一/二/三麻玩家放完塔，刷新组内成员相对位置，以便更改后续逻辑
                if (towerRound == myPriority / 100)
                    P3_刷新同组左右位置(sa, myPriority);

                // 根据三枚塔坐标左中右排序
                _p3TowerAppearPos.Sort((pos1, pos2) => pos1.X.CompareTo(pos2.X));

                // 输入当前的轮次，以及我的优先级位次，画塔
                P3_绘制塔范围(sa, towerRound, myDfgIdx, myPriority);

                // 清空塔
                _p3TowerAppearPos = [];
            }
        }

        private DrawPropertiesEdit P3_绘制塔指路(Vector3 towerPos, int delay, int destroy, string name, ScriptAccessory accessory, bool draw = true)
        {
            var dp = accessory.DrawDirPos(towerPos, delay, destroy, name);
            if (draw)
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            return dp;
        }
        private DrawPropertiesEdit P3_绘制塔面向(Vector3 towerPos, int delay, int destroy, string name, ScriptAccessory accessory, bool draw = true)
        {
            const int left = 0;
            const int middle = 1;
            const int right = 2;

            var targetPos = towerPos.ExtendPoint(-90f.DegToRad(), 3.1f);
            var dp = accessory.DrawDirPos2Pos(towerPos, targetPos, delay, destroy, name);
            dp.Scale = new Vector2(3f);
            dp.Color = ColorHelper.ColorYellow.V4;
            if (draw)
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Arrow, dp);
            return dp;
        }

        private DrawPropertiesEdit P3_绘制回人群(int delay, int destroy, string name, ScriptAccessory accessory, bool draw = true)
        {
            var stackPos = new Vector3(100, 0, 92);
            var dp = accessory.DrawDirPos(stackPos, delay, destroy, name);
            if (draw)
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            return dp;
        }

        private void P3_绘制塔范围(ScriptAccessory sa, int towerRound, int myDfgIdx, int myPriority)
        {
            // 计算持续时间
            // towerExistTime - towerCastingTime
            //     0, 6800 - 3000  => 3800
            //     6800 - 3000 + 300, 3000     => 3300
            //         => 7100

            const int towerExistTime = 7100;

            var myRound = myDfgIdx switch
            {
                // 玩家需踩第几轮塔
                0 => 1,
                2 => 1,
                1 => 2,
                3 => 2,
                4 => 2,
                5 => 0,
                6 => 0,
                7 => 0,
                _ => -1
            };
            if (myRound == -1)
            {
                sa.Log.Error($"myDfgIdx = {myDfgIdx} 导致 myRound = {myRound}");
                return;
            }
            var isMyRound = myRound == towerRound;
            var myTowerPos = P3_取麻将方位字符(myDfgIdx);

            for (int i = 0; i < _p3TowerAppearPos.Count; i++)
            {
                // 当前是玩家放塔轮次，且该塔为玩家方位
                var thisTowerPos = P3_取麻将方位字符(i, towerRound == 1);
                var isMyTower = isMyRound && (thisTowerPos == myTowerPos);

                var color = isMyTower ? sa.Data.DefaultSafeColor.WithW(1.5f) : sa.Data.DefaultDangerColor;
                var dp1 = sa.DrawStaticCircle(_p3TowerAppearPos[i], color, 0, towerExistTime, $"塔{towerRound}{thisTowerPos}", 5f);
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp1);

                if (!isMyTower) continue;
                sa.Log.Debug($"检测到玩家需踩第 {myRound} 轮的 {myTowerPos} 塔");
                var dp01 = sa.DrawDirPos(_p3TowerAppearPos[i], 0, towerExistTime, $"塔{towerRound}{thisTowerPos}指路");
                sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp01);
            }
        }

        private Vector3 P3_取塔生成坐标(ScriptAccessory sa, ulong sid, uint type)
        {
            // const uint inPlace = 26382;
            // const uint front = 26383;
            // const uint behind = 26384;

            var chara = sa.GetById(sid);
            var srot = chara.Rotation;
            var spos = chara.Position;

            if (type == 26382) return spos;
            var newPos = spos.ExtendPoint(srot.Game2Logic(), 14);
            return newPos;
        }

        // 0        Casting LashGnash           0
        // +7600    Stack #1 + Jump #1          7600
        // +3700    Chariot/Donut #1            11300
        // +3100    Donut/Chariot #1            14400
        // +0       Towers #1                   14400
        // +2500    StartCast Geirskogul #1     16900
        // +800     Jump #2                     17700
        // +3800    Casting LashGnash           21500
        // +2800    Towers #2                   24300
        // +2600    StartCast Geirskogul #2     26900
        // +2200    Stack #2 + Jump #3          28900
        // +3700    Chariot/Donut #2            32600
        // +3100    Donut/Chariot #2            35700
        // +0       Towers #3                   35700
        // +2000    StartCast Geirskogul #3     37700
        // +4500    Geirskogul #3               42200

        // TowerExistTime       6800, 6600, 6800
        // PlaceTowerTimeNode   7600, 17700, 28900

        #endregion

        #region P4

        [ScriptMethod(name: "---- 《P4 龙眼》 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld"],
            userControl: true)]
        public void P4_分节线(Event @event, ScriptAccessory accessory)
        {
        }

        [ScriptMethod(name: "P4 记录", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:29750"], userControl: false)]
        public void P4_记录(Event @event, ScriptAccessory accessory)
        {
            parse = 4;
        }

        [ScriptMethod(name: "P4 阶段记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2748"],
            userControl: false)]
        public void P4_阶段记录(Event @event, ScriptAccessory sa)
        {
            if (_dsrPhase == DsrPhase.Phase4Eyes) return;
            _dsrPhase = DsrPhase.Phase4Eyes;
            _p4MirageDiveNum = 0;
            _p4MirageDiveNumFirstRoundTarget = new bool[8].ToList();
            _p4MirageDivePos = [];
            _p4PrepareToCenter = false;
            sa.Log.Debug($"当前阶段为：{_dsrPhase}");
        }

        [ScriptMethod(name: "P4 开场就位提示", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2748"],
            userControl: true)]
        public void P4_开场就位提示(Event @event, ScriptAccessory accessory)
        {
            var tid = @event.TargetId();
            if (tid != accessory.Data.Me) return;
            var myIndex = accessory.GetMyIndex();
            // MT D1 D2 H1
            var isBlueEye = myIndex is 0 or 2 or 4 or 5;
            var isTank = myIndex is 0 or 1;
            accessory.Method.TextInfo($"{(isTank ? "开启盾姿，" : "")}{(isBlueEye ? "左侧蓝球" : "右侧红球")}就位", 3000, isTank);
        }

        [ScriptMethod(name: "P4 红蓝Buff置换提示", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(277[56])$"],
            userControl: true)]
        public void P4_红蓝Buff置换(Event @event, ScriptAccessory accessory)
        {
            if (_dsrPhase != DsrPhase.Phase4Eyes) return;
            var tid = @event.TargetId();
            if (tid != accessory.Data.Me) return;
            const uint redBuff = 2775;
            const uint blueBuff = 2776;
            var stid = @event.StatusId();
            var myIndex = accessory.GetMyIndex();
            if (_drawn[0]) return;
            _drawn[0] = true;

            var needChange = (myIndex < 4 && stid != blueBuff) || (myIndex >= 4 && stid != redBuff);
            if (!needChange) return;
            var dp = accessory.DrawGuidance(_center, 0, 5000, $"红蓝Buff置换");
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            accessory.Method.TextInfo($"场中换Buff", 3000);
        }

        [ScriptMethod(name: "P4 红蓝Buff置换消除", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(277[56])$"],
            userControl: false)]
        public void P4_红蓝Buff置换消除(Event @event, ScriptAccessory accessory)
        {
            if (_dsrPhase != DsrPhase.Phase4Eyes) return;
            var tid = @event.TargetId();
            if (tid != accessory.Data.Me) return;
            const uint redBuff = 2775;
            const uint blueBuff = 2776;
            var stid = @event.StatusId();
            var myIndex = accessory.GetMyIndex();

            var changeComplete = (myIndex < 4 && stid == blueBuff) || (myIndex >= 4 && stid == redBuff);
            if (!changeComplete) return;
            accessory.Method.RemoveDraw($"红蓝Buff置换");
        }

        [ScriptMethod(name: "P4 DPS撞球提示", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(1260[78])$"],
            userControl: true)]
        public void P4_DPS撞球提示(Event @event, ScriptAccessory accessory)
        {
            if (_dsrPhase != DsrPhase.Phase4Eyes) return;
            if (_drawn[1]) return;
            _drawn[1] = true;
            // 球出现开始计时
            var myIndex = accessory.GetMyIndex();
            if (myIndex < 4) return;

            var orbPos = new Vector3(83, 0, 100);
            if (myIndex is 6 or 7)
                orbPos = orbPos.FoldPointHorizon(_center.X);

            // 要细致的话，需要找到球什么时候变大的时间点
            var dp0 = accessory.DrawGuidance(orbPos, 4000, 2000, $"DPS撞球准备");
            dp0.Color = accessory.Data.DefaultDangerColor;
            var dp1 = accessory.DrawGuidance(orbPos, 6000, 5000, $"DPS撞球");
            dp1.Color = accessory.Data.DefaultSafeColor;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp0);
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp1);
        }

        [ScriptMethod(name: "P4 DPS撞球提示消失", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:26817"],
            userControl: false)]
        public void P4_DPS撞球提示消失(Event @event, ScriptAccessory accessory)
        {
            if (_dsrPhase != DsrPhase.Phase4Eyes) return;
            var tid = @event.TargetId();
            if (tid != accessory.Data.Me) return;
            var myIndex = accessory.GetMyIndex();
            if (myIndex < 4) return;
            accessory.Method.RemoveDraw($"DPS撞球.*");
        }

        [ScriptMethod(name: "P4 TN撞球提示", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(1260[78])$"],
            userControl: true)]
        public void P4_TN撞球提示(Event @event, ScriptAccessory accessory)
        {
            if (_dsrPhase != DsrPhase.Phase4Eyes) return;
            if (_drawn[2]) return;
            _drawn[2] = true;
            // 球出现开始计时
            var myIndex = accessory.GetMyIndex();
            if (myIndex >= 4) return;

            var orbPos = new Vector3(90, 0, 93);
            if (myIndex >= 2)
                orbPos = orbPos.FoldPointVertical(_center.Z);
            if (myIndex % 2 == 1)
                orbPos = orbPos.FoldPointHorizon(_center.X);

            // accessory.Method.TextInfo($"与DPS换Buff", 2500);
            var dp0 = accessory.DrawGuidance(orbPos, 10000, 2000, $"TN撞球准备");
            dp0.Color = accessory.Data.DefaultDangerColor;
            var dp1 = accessory.DrawGuidance(orbPos, 12000, 5000, $"TN撞球");
            dp1.Color = accessory.Data.DefaultSafeColor;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp0);
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp1);
        }

        [ScriptMethod(name: "P4 TN撞球前换Buff提示", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:26817"],
            userControl: true)]
        public void P4_TN撞球前换Buff提示(Event @event, ScriptAccessory accessory)
        {
            if (_dsrPhase != DsrPhase.Phase4Eyes) return;
            if (_drawn[5]) return;
            _drawn[5] = true;
            // 球出现开始计时
            var myIndex = accessory.GetMyIndex();
            if (myIndex >= 4) return;

            accessory.Method.TextInfo($"与DPS换Buff", 2500);
        }

        [ScriptMethod(name: "P4 TN撞球提示消失", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:26815"],
            userControl: false)]
        public void P4_TN撞球提示消失(Event @event, ScriptAccessory accessory)
        {
            if (_dsrPhase != DsrPhase.Phase4Eyes) return;
            var tid = @event.TargetId();
            if (tid != accessory.Data.Me) return;
            var myIndex = accessory.GetMyIndex();
            if (myIndex >= 4) return;
            accessory.Method.RemoveDraw($"TN撞球.*");
        }

        [ScriptMethod(name: "P4 幻象冲初始就位提示", eventType: EventTypeEnum.RemoveCombatant, eventCondition: ["DataId:12607"],
            userControl: true)]
        public void P4_幻象冲初始就位提示(Event @event, ScriptAccessory accessory)
        {
            if (_dsrPhase != DsrPhase.Phase4Eyes) return;
            if (_drawn[3]) return;
            _drawn[3] = true;

            Vector3 targetPos;
            var myIndex = accessory.GetMyIndex();
            if (myIndex >= 4)
                targetPos = new(90, 0, 100);
            else
            {
                targetPos = new(84.5f, 0, 94.5f);
                targetPos = targetPos.RotatePoint(new(90, 0, 100), myIndex * 90f.DegToRad());
            }
            var dp = accessory.DrawGuidance(targetPos, 0, 5000, $"幻象冲就位提示");
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }

        [ScriptMethod(name: "P4 幻象冲次数与目标记录", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:26820", "TargetIndex:1"],
            userControl: false)]
        public void P4_幻象冲次数与目标记录(Event @event, ScriptAccessory accessory)
        {
            if (_dsrPhase != DsrPhase.Phase4Eyes) return;
            var tid = @event.TargetId();
            var tidx = accessory.GetPlayerIdIndex(tid);
            lock (_p4MirageDiveNumFirstRoundTarget)
            {
                _p4MirageDiveNum++;
                if (_p4MirageDiveNum <= 2)
                    _p4MirageDiveNumFirstRoundTarget[tidx] = true;
            }

            lock (_p4MirageDivePos)
            {
                var tpos = @event.TargetPosition();
                var tdir = tpos.Position2Dirs(new Vector3(90, 0, 100), 4, false);
                _p4MirageDivePos.Add((tdir + 1) % 4);
                if (_p4MirageDivePos.Count != 2) return;
                _p4MirageDivePos.Sort();
                _mirageDiveRound.Set();
            }
        }

        [ScriptMethod(name: "P4 幻象冲等待回中提示", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:26820", "TargetIndex:1"],
            userControl: true)]
        public void P4_幻象冲等待回中提示(Event @event, ScriptAccessory sa)
        {
            if (_dsrPhase != DsrPhase.Phase4Eyes) return;
            if (_p4PrepareToCenter) return;
            var tid = @event.TargetId();
            if (tid != sa.Data.Me) return;
            if (_p4MirageDiveNum > 6) return;
            _p4PrepareToCenter = true;

            var dp = sa.DrawGuidance(new Vector3(90, 0, 100), 0, 5000, $"幻象冲等待回中提示");
            dp.Color = sa.Data.DefaultDangerColor;
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            sa.Log.Debug($"玩家受到伤害，准备回中");
        }

        [ScriptMethod(name: "P4 幻象冲回中提示", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2776"],
            userControl: true)]
        public void P4_幻象冲回中提示(Event @event, ScriptAccessory sa)
        {
            if (_dsrPhase != DsrPhase.Phase4Eyes) return;
            if (!_p4PrepareToCenter) return;
            var tid = @event.TargetId();
            if (tid != sa.Data.Me) return;
            if (_p4MirageDiveNum > 6) return;
            _p4PrepareToCenter = false;

            sa.Method.RemoveDraw($"幻象冲等待回中提示");
            var dp = sa.DrawGuidance(new Vector3(90, 0, 100), 0, 2500, $"幻象冲回中提示");
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            sa.Log.Debug($"玩家Buff交换完毕，回中");
        }

        [ScriptMethod(name: "P4 幻象冲交换提示", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:26820", "TargetIndex:1"],
            userControl: true)]
        public void P4_幻象冲交换提示(Event @event, ScriptAccessory sa)
        {
            if (_dsrPhase != DsrPhase.Phase4Eyes) return;
            if (_drawn[4]) return;
            _drawn[4] = true;
            _mirageDiveRound.WaitOne();

            _drawn[4] = false;
            _mirageDiveRound.Reset();

            if (_p4MirageDiveNum > 6) return;
            var highPriorityPlayer = _p4MirageDiveNum switch
            {
                2 => 4,
                4 => 6,
                6 => _p4MirageDiveNumFirstRoundTarget.IndexOf(true),
                _ => 0,
            };
            var lowPriorityPlayer = _p4MirageDiveNum switch
            {
                2 => 5,
                4 => 7,
                6 => _p4MirageDiveNumFirstRoundTarget.LastIndexOf(true),
                _ => 0,
            };

            var basePos = new Vector3(84.5f, 0, 94.5f);
            var highPriorityPos = basePos.RotatePoint(new(90, 0, 100), _p4MirageDivePos[0] * 90f.DegToRad());
            var lowPriorityPos = basePos.RotatePoint(new(90, 0, 100), _p4MirageDivePos[1] * 90f.DegToRad());

            var highPriorityPlayerJob = sa.GetPlayerJobByIndex(highPriorityPlayer);
            var lowPriorityPlayerJob = sa.GetPlayerJobByIndex(lowPriorityPlayer);
            var myIndex = sa.GetMyIndex();

            if (myIndex == highPriorityPlayer)
            {
                var dp = sa.DrawGuidance(highPriorityPos, 0, 5000, $"高优先级就位{highPriorityPlayer}");
                sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }
            if (myIndex == lowPriorityPlayer)
            {
                var dp = sa.DrawGuidance(lowPriorityPos, 0, 5000, $"低优先级就位{lowPriorityPlayer}");
                sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }

            var str = "";
            str += $"第{_p4MirageDiveNum / 2}轮，高优先级{highPriorityPlayerJob}去{_p4MirageDivePos[0]}号位\n";
            str += $"第{_p4MirageDiveNum / 2}轮，低优先级{lowPriorityPlayerJob}去{_p4MirageDivePos[1]}号位";
            sa.Log.Debug(str);
            _p4MirageDivePos.Clear();
        }

        #endregion

        #region P5

        [ScriptMethod(name: "---- 《P5 伪典托尔丹》 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld"],
            userControl: true)]
        public void P5_分节线(Event @event, ScriptAccessory accessory)
        {
        }

        #region 一运
        [ScriptMethod(name: "P5 一运记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27529"], userControl: false)]
        public void P5_一运记录(Event @event, ScriptAccessory accessory)
        {
            parse = 5.1;
        }
        [ScriptMethod(name: "P5 一运旋风冲", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27531"])]
        public void P5_一运旋风冲(Event @event, ScriptAccessory accessory)
        {
            if (parse != 5.1) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultDangerColor;
            if (ParseObjectId(@event["SourceId"], out var id))
            {
                dp.Owner = id;
            }

            dp.Scale = new(10,60);
            dp.DestoryAt = 6000;
            dp.Name = $"P5一运旋风冲";
            
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            
        }
        [ScriptMethod(name: "P5 一运白龙位置连线(ImGui)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27531"])]
        public void P5_一运白龙位置连线(Event @event, ScriptAccessory accessory)
        {
            if (parse != 5.1) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultDangerColor;
            if (ParseObjectId(@event["SourceId"], out var id))
            {
                dp.TargetObject = id;
            }
            dp.Owner = accessory.Data.Me;
            dp.Scale = new(5);
            dp.ScaleMode |= ScaleMode.YByDistance;
            dp.DestoryAt = 6000;
            dp.Name = $"P5一运白龙位置连线";

            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, dp);

        }
        [ScriptMethod(name: "P5 一运双骑士螺旋枪", eventType: EventTypeEnum.Tether, eventCondition: ["Id:0005"])]
        public void P5_一运双骑士连线(Event @event, ScriptAccessory accessory)
        {
            if (parse != 5.1) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultDangerColor;
            if (ParseObjectId(@event["SourceId"], out var id))
            {
                dp.Owner = id;
            }
            if (ParseObjectId(@event["TargetId"], out var tid))
            {
                dp.TargetObject = tid;
            }

            dp.Scale = new(16, 60);
            dp.ScaleMode |= ScaleMode.YByDistance;
            dp.Delay = p5TetherCrashDelay;
            dp.DestoryAt = 6000- p5TetherCrashDelay;
            dp.Name = $"P5一运双骑士连线冲锋";

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

        }
        [ScriptMethod(name: "P5 一运雷翼", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2833"])]
        public void P5_一运雷翼(Event @event, ScriptAccessory accessory)
        {
            if (parse != 5.1) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultDangerColor;
            if (ParseObjectId(@event["TargetId"], out var id))
            {
                dp.Owner = id;
            }
            dp.Scale = new(5);
            dp.DestoryAt = 5000;
            dp.Delay = int.TryParse(@event["DurationMilliseconds"], out var d) ? (d - dp.DestoryAt) :8000;
            dp.Name = $"P5一运雷翼{id:X8}";
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        }
        [ScriptMethod(name: "P5 一运穿天", eventType: EventTypeEnum.TargetIcon)]
        public void P5_一运穿天(Event @event, ScriptAccessory accessory)
        {
            if (parse != 5.1 || ParsTargetIcon(@event["Id"]) != -316) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultDangerColor.WithW(0.5f);
            if (ParseObjectId(@event["TargetId"], out var id))
            {
                dp.Owner = id;
            }
            dp.Scale = new(24);
            dp.Delay = 2000;
            dp.DestoryAt = 4000;
            dp.Name = $"P5一运穿天{id:X8}";

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        }
        [ScriptMethod(name: "P5 阿斯卡隆之仁・揭示", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25546"])]
        public void P5_阿斯卡隆之仁揭示(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultDangerColor;
            if (ParseObjectId(@event["SourceId"], out var id))
            {
                dp.Owner = id;
            }

            dp.Scale = new(50);
            dp.Radian = float.Pi / 180 * 30;
            dp.DestoryAt = 4000;
            foreach (var tid in accessory.Data.PartyList)
            {
                dp.Name = $"P5 阿斯卡隆之仁・揭示 {tid:X8}";
                dp.TargetObject = tid;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
            }




        }
        [ScriptMethod(name: "P5 一运双龙俯冲处理位置记录", eventType: EventTypeEnum.SetObjPos,eventCondition: ["SourceDataId:12603"],userControl:false)]
        public void P5_一运双龙俯冲处理位置记录(Event @event, ScriptAccessory accessory)
        {
            if (parse != 5.1) return;
            var pos= JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
            p5DivePos = new((pos.X - 100) / 9 * 19 + 100, pos.Y, (pos.Z - 100) / 9 * 19 + 100);
        }
        [ScriptMethod(name: "P5 一运双龙俯冲处理位置", eventType: EventTypeEnum.TargetIcon)]
        public void P5_一运双龙俯冲处理位置(Event @event, ScriptAccessory accessory)
        {
            if (parse != 5.1 || ParsTargetIcon(@event["Id"]) != -310) return;
            if (!ParseObjectId(@event["TargetId"], out var id) || id!=accessory.Data.Me) return;

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultSafeColor.WithW(2f);
            dp.Owner = id;
            dp.TargetPosition=p5DivePos;
            dp.Scale = new(1,60);
            dp.DestoryAt = 5000;
            dp.ScaleMode |= ScaleMode.YByDistance;
            dp.Name = $"P5一运双龙俯冲处理位置";

            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

        }
        [ScriptMethod(name: "P5 一运白龙俯冲", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27534"])]
        public void P5_一运白龙俯冲(Event @event, ScriptAccessory accessory)
        {
            if (parse != 5.1) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultDangerColor;
            if (ParseObjectId(@event["SourceId"], out var id))
            {
                dp.Owner = id;
            }
            dp.Scale = new(20, 48);
            dp.DestoryAt = 6000;
            dp.Name = $"P5一运白龙俯冲";

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

        }
        [ScriptMethod(name: "P5 一运黑龙俯冲", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27533"])]
        public void P5_一运黑龙俯冲(Event @event, ScriptAccessory accessory)
        {
            if (parse != 5.1) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultDangerColor;
            if (ParseObjectId(@event["SourceId"], out var id))
            {
                dp.Owner = id;
            }
            dp.Scale = new(20, 48);
            dp.DestoryAt = 6000;
            dp.Name = $"P5一运黑龙俯冲";

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

        }
        [ScriptMethod(name: "P5 一运格里诺位置记录", eventType: EventTypeEnum.SetObjPos, eventCondition: ["SourceDataId:12602"], userControl: false)]
        public void P5_一运格里诺位置记录(Event @event, ScriptAccessory accessory)
        {
            if (parse != 5.1) return;
            p5GrenoPos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
        }
        [ScriptMethod(name: "P5 一运连线格里诺(ImGui)", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:25546"])]
        public void P5_一运连线格里诺(Event @event, ScriptAccessory accessory)
        {
            if (parse != 5.1) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P5 一运连线格里诺";
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.Owner = accessory.Data.Me;
            dp.TargetPosition = p5GrenoPos;
            dp.Scale = new(5);
            dp.DestoryAt = 8000;
            dp.ScaleMode |= ScaleMode.YByDistance;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, dp);

        }
        [ScriptMethod(name: "P5 一运阶段记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27529"], userControl: false)]
        public void P5_一运阶段记录(Event @event, ScriptAccessory sa)
        {
            _dsrPhase = DsrPhase.Phase5HeavensWrath;
            sa.Log.Debug($"当前阶段为：{_dsrPhase}");
        }

        [ScriptMethod(name: "P5 一运旋风预警", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27531"])]
        public async void P5_一运旋风预警(Event @event, ScriptAccessory accessory)
        {
            P5_绘制旋风(3000, 3000, accessory);
            await Task.Delay(3000);
            accessory.Method.TextInfo("旋风", 3000, true);
        }

        [ScriptMethod(name: "P5 一运旋风危险位置", eventType: EventTypeEnum.ObjectChanged, eventCondition: ["DataId:2001168", "Operate:Add"])]
        public void P5_一运旋风危险位置(Event @event, ScriptAccessory accessory)
        {
            var spos = @event.SourcePosition();
            var dp = accessory.DrawStaticCircle(spos, ColorHelper.ColorRed.V4.WithW(3), 0, 4000, $"旋风{spos}");
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        private void P5_绘制旋风(int delay, int destroy, ScriptAccessory accessory)
        {
            for (var i = 0; i < accessory.Data.PartyList.Count; i++)
            {
                var dp = accessory.DrawCircle(accessory.Data.PartyList[i], 1.5f, delay, destroy, $"旋风{i}", true);
                dp.Color = accessory.Data.DefaultDangerColor.WithW(2f);
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }
        }

        [ScriptMethod(name: "P5 一运大圈火预警", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25573"])]
        public void P5_一运大圈火预警(Event @event, ScriptAccessory accessory)
        {
            if (_dsrPhase != DsrPhase.Phase5HeavensWrath) return;
            var spos = @event.SourcePosition();
            var dp = accessory.DrawStaticCircle(spos, ColorHelper.ColorRed.V4.WithW(1.5f), 0, 4000, $"大圈火危险区", 8f);
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        [ScriptMethod(name: "P5 一运白龙位置记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27531"],
            userControl: false)]
        public void P5_一运白龙位置记录(Event @event, ScriptAccessory accessory)
        {
            if (_dsrPhase != DsrPhase.Phase5HeavensWrath) return;
            var spos = @event.SourcePosition();
            _p5VedrfolnirPos = spos;
            _p5VedrfolnirPosRecordEvent.Set();
        }

        [ScriptMethod(name: "P5 一运连线指路", eventType: EventTypeEnum.Tether, eventCondition: ["Id:0005"],
            userControl: true)]
        public void P5_一运连线指路(Event @event, ScriptAccessory accessory)
        {
            if (_dsrPhase != DsrPhase.Phase5HeavensWrath) return;
            _p5VedrfolnirPosRecordEvent.WaitOne();
            var tid = @event.TargetId();
            if (tid != accessory.Data.Me) return;
            var spos = @event.SourcePosition();
            var atRight = spos.IsAtRight(_p5VedrfolnirPos, _center);
            var targetPos = spos.RotatePoint(_center, (atRight ? 1 : -1) * 172.5f.DegToRad());

            targetPos = targetPos.PointInOutside(_center, 2f);
            var dp = accessory.DrawGuidance(targetPos, 0, 8000, $"一运连线指路");
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }

        [ScriptMethod(name: "P5 一运连线指路消失", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:27530"],
            userControl: false)]
        public void P5_一运连线指路消失(Event @event, ScriptAccessory accessory)
        {
            if (_dsrPhase != DsrPhase.Phase5HeavensWrath) return;
            accessory.Method.RemoveDraw($"一运连线指路");
        }

        [ScriptMethod(name: "P5 一运穿天指路", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:000E"],
            userControl: true)]
        public void P5_一运穿天指路(Event @event, ScriptAccessory accessory)
        {
            if (_dsrPhase != DsrPhase.Phase5HeavensWrath) return;
            _p5VedrfolnirPosRecordEvent.WaitOne();
            var tid = @event.TargetId();
            if (tid != accessory.Data.Me) return;

            var targetPos = _p5VedrfolnirPos.RotatePoint(_center, -67.5f.DegToRad());
            targetPos = targetPos.PointInOutside(_center, 2f);
            var dp = accessory.DrawGuidance(targetPos, 0, 8000, $"一运穿天指路");
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }

        [ScriptMethod(name: "P5 一运穿天指路消失", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:29346"],
            userControl: false)]
        public void P5_一运穿天指路消失(Event @event, ScriptAccessory accessory)
        {
            if (_dsrPhase != DsrPhase.Phase5HeavensWrath) return;
            _p5VedrfolnirPosRecordEvent.Reset();
            accessory.Method.RemoveDraw($"一运穿天指路");
        }

        #endregion

        #region 二运
        [ScriptMethod(name: "P5 二运记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27538"], userControl: false)]
        public void P5_二运记录(Event @event, ScriptAccessory accessory)
        {
            parse = 5.2;
            p5sony = [0, 0, 0, 0, 0, 0, 0, 0];
            p5sony_sixuan = [0, 0, 0, 0, 0, 0, 0, 0];
            if (ParseObjectId(@event["SourceId"], out var id))
            {
                tordanId = id;
            }
        }
        [ScriptMethod(name: "P5 二运黑龙俯冲", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27533"])]
        public void P5_二运黑龙俯冲(Event @event, ScriptAccessory accessory)
        {
            if (parse != 5.2) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultDangerColor;
            if (ParseObjectId(@event["SourceId"], out var id))
            {
                dp.Owner = id;
            }
            dp.Scale = new(20, 48);
            dp.DestoryAt = 6000;
            dp.Name = $"P5一运黑龙俯冲";

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

        }
        [ScriptMethod(name: "P5 二运旋风冲", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27531"])]
        public void P5_二运旋风冲(Event @event, ScriptAccessory accessory)
        {
            if (parse != 5.2) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultDangerColor;
            if (ParseObjectId(@event["SourceId"], out var id))
            {
                dp.Owner = id;
            }

            dp.Scale = new(10, 60);
            dp.DestoryAt = 6000;
            dp.Name = $"P5二运旋风冲";

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

        }
        [ScriptMethod(name: "P5 二运战女神之枪", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27539"])]
        public void P5_二运战女神之枪(Event @event, ScriptAccessory accessory)
        {
            if (parse != 5.2) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultDangerColor;
            if (ParseObjectId(@event["SourceId"], out var id))
            {
                dp.Owner = id;
            }
            dp.Scale = new(10, 50);
            dp.DestoryAt = 6000;
            dp.Name = $"P5二运战女神之枪";

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

        }
        [ScriptMethod(name: "P5 二运地震", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25558"])]
        public void P5_二运地震(Event @event, ScriptAccessory accessory)
        {
            if (parse != 5.2) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultDangerColor;
            if (ParseObjectId(@event["SourceId"], out var sid))
            {
                dp.Owner = sid;
            }
            dp.Name = "P5_二运地震";

            dp.Scale = new(6);
            dp.DestoryAt = 6000;
            dp.Radian = float.Pi * 2;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            dp.Scale = new(12);
            dp.InnerScale = new(6);
            dp.Delay = 4000;
            dp.DestoryAt = 4000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);

            dp.Scale = new(18);
            dp.InnerScale = new(12);
            dp.Delay = 6000;
            dp.DestoryAt = 4000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);

            dp.Scale = new(24);
            dp.InnerScale = new(18);
            dp.Delay = 8000;
            dp.DestoryAt = 4000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);

            dp.Scale = new(30);
            dp.InnerScale = new(24);
            dp.Delay = 10000;
            dp.DestoryAt = 4000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
        }
        [ScriptMethod(name: "P5 二运龙眼背对", eventType: EventTypeEnum.EnvControl, eventCondition: ["DirectorId:8003759A", "Id:00020001"])]
        public void P5_二运龙眼背对(Event @event, ScriptAccessory accessory)
        {
            if (parse != 5.2) return;
            var index = int.Parse(@event["Index"], System.Globalization.NumberStyles.HexNumber);
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Owner = accessory.Data.Me;
            dp.Delay = 16000;
            dp.DestoryAt = 7000;
            if (index == 0) dp.TargetPosition = new(100, 0, 65);
            if (index == 1) dp.TargetPosition = new(124.75f, 0, 75.25f);
            if (index == 2) dp.TargetPosition = new(135, 0, 100);
            if (index == 3) dp.TargetPosition = new(124.75f, 0, 124.75f);
            if (index == 4) dp.TargetPosition = new(100, 0, 135);
            if (index == 5) dp.TargetPosition = new(75.25f, 0, 124.75f);
            if (index == 6) dp.TargetPosition = new(65, 0, 100);
            if (index == 7) dp.TargetPosition = new(75.25f, 0, 75.25f);
            dp.Name = "P5 二运龙眼背对";
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.SightAvoid, dp);
        }
        [ScriptMethod(name: "P5 二运骑神背对", eventType: EventTypeEnum.EnvControl, eventCondition: ["DirectorId:8003759A", "Id:00020001"])]
        public void P5_二运骑神背对(Event @event, ScriptAccessory accessory)
        {
            if (parse != 5.2) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Owner = accessory.Data.Me;
            dp.TargetObject = tordanId;
            dp.Delay = 16000;
            dp.DestoryAt = 7000;
            
            dp.Name = "P5 二运骑神背对";
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.SightAvoid, dp);
        }
        [ScriptMethod(name: "P5 二运索尼记录", eventType: EventTypeEnum.TargetIcon,userControl:false)]
        public void P5_二运索尼记录(Event @event, ScriptAccessory accessory)
        {
            if (parse != 5.2) return;
            var sony = ParsTargetIcon(@event["Id"])+49;
            if (sony < 0 || sony > 3) return;
            if(ParseObjectId(@event["TargetId"], out var id))
            {
                var index= accessory.Data.PartyList.ToList().IndexOf(id);
                p5sony[index] += sony;
            }
        }
        [ScriptMethod(name: "P5 二运死宣记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2976"],userControl: false)]
        public void P5_二运死宣记录(Event @event, ScriptAccessory accessory)
        {
            if (parse != 5.2) return;
            
            if (ParseObjectId(@event["TargetId"], out var id))
            {
                var index = accessory.Data.PartyList.ToList().IndexOf(id);
                p5sony[index] +=10;
                p5sony_sixuan[index] = 1;
            }
        }

        [ScriptMethod(name: "P5 二运未点死宣标记", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2976"])]
        public async void P5_二运未点死宣标记(Event @event, ScriptAccessory accessory)
        {
            if (parse != 5.2) return;

            // 2976 会触发很多次，只让这个标记逻辑执行一次
            if (p5DeathMarkDone) return;
            p5DeathMarkDone = true;

            // 等待记录 function 先把 p5sony 更新完
            int waitCount = 0;
            while (p5sony_sixuan.Count(x => x == 1) < 4 && waitCount < 20)
            {
                await Task.Delay(100);
                waitCount++;
            }

            int markIndex = 1;

            // 从低到高，给所有 p5sony 为 0 的人上标记
            for (int i = 0; i < p5sony_sixuan.Count; i++)
            {
                if (p5sony_sixuan[i] == 0)
                {
                    var target = PartyIndexToMarkerTarget(i);

                    if (markIndex <= 3)
                    {
                        accessory.Method.SendChat($"/mk 止步{markIndex} <{target}>");
                        accessory.Method.SendChat($"/e 止步{markIndex} <{target}>");
                    }
                    else if (markIndex == 4)
                    {
                        accessory.Method.SendChat($"/mk 三角 <{target}>");
                        accessory.Method.SendChat($"/e 三角 <{target}>");
                    }

                    markIndex++;
                }
            }
        }
        private int PartyIndexToMarkerTarget(int partyIndex)
        {
            // PartyList[7] -> <1>
            // PartyList[0] -> <2>
            // PartyList[1] -> <3>
            // ...
            // PartyList[6] -> <8>
            return partyIndex == 7 ? 1 : partyIndex + 2;
        }
        [ScriptMethod(name: "P5 二运盖里克位置记录", eventType: EventTypeEnum.SetObjPos, eventCondition: ["SourceDataId:12637"],userControl:false)]
        public void P5_二运盖里克位置记录(Event @event, ScriptAccessory accessory)
        {
            if (parse != 5.2) return;
            p5GreekPos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
        }
        [ScriptMethod(name: "P5 二运死宣六方站位(ImGui)", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2976"])]
        public void P5_二运死宣六方站位(Event @event, ScriptAccessory accessory)
        {
            if (parse != 5.2) return;
            Task.Delay(100).ContinueWith(t =>
            {
                if (p5Deal) return;
                var count = p5sony.Where(s => s > 5).Count();
                if (count != 4) return;
                p5Deal = true;
                var idIndex = accessory.Data.PartyList.ToList().IndexOf(accessory.Data.Me);
                var sony = p5sony[idIndex];
                var posid = sony > 0 ? 4 : 0;
                for (int i = 0; i < idIndex; i++)
                {
                    if(sony== p5sony[i])
                    {
                        posid++;
                    }
                }
                var cpos = new Vector3(100, 0, 100);
                var npos = 19.5f*Vector3.Normalize(new(p5GreekPos.X - 100, p5GreekPos.Y, p5GreekPos.Z - 100)) + cpos;
                if(posid==4||posid==7) { npos = 13 * Vector3.Normalize(new(p5GreekPos.X - 100, p5GreekPos.Y, p5GreekPos.Z - 100)) + cpos; }
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Color = accessory.Data.DefaultSafeColor;
                dp.Owner = accessory.Data.Me;
                dp.Scale = new(1.5f, 60);
                dp.DestoryAt = 7000;
                dp.ScaleMode |= ScaleMode.YByDistance;
                dp.Name = $"P5二运死宣引导站位{sony}";

                var d = float.Pi / 180f;
                if (posid == 0) dp.TargetPosition = RotatePoint(npos, cpos, d * -90);
                if (posid == 1) dp.TargetPosition = RotatePoint(npos, cpos, d * -142.5f);
                if (posid == 2) dp.TargetPosition = RotatePoint(npos, cpos, d * 142.5f);
                if (posid == 3) dp.TargetPosition = RotatePoint(npos, cpos, d * 90);
                if (posid == 4) dp.TargetPosition = RotatePoint(npos, cpos, d * -90);
                if (posid == 5) dp.TargetPosition = RotatePoint(npos, cpos, d * -37.5f);
                if (posid == 6) dp.TargetPosition = RotatePoint(npos, cpos, d * 37.5f);
                if (posid == 7) dp.TargetPosition = RotatePoint(npos, cpos, d * 90);
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

            });
        }
        [ScriptMethod(name: "P5 二运索尼引导站位(ImGui)", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:27533"])]
        public void P5_二运索尼引导站位(Event @event, ScriptAccessory accessory)
        {
            if (parse != 5.2) return;

            var idIndex = accessory.Data.PartyList.ToList().IndexOf(accessory.Data.Me);
            var sony = p5sony[idIndex];
            var posid = sony > 0 ? 4 : 0;
            for (int i = 0; i < idIndex; i++)
            {
                if (sony == p5sony[i])
                {
                    posid++;
                }
            }
            var cpos = new Vector3(100, 0, 100);
            var npos = 10 * Vector3.Normalize(new(p5GreekPos.X - 100, p5GreekPos.Y, p5GreekPos.Z - 100)) + cpos;

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.Owner = accessory.Data.Me;
            dp.Scale = new(1.5f, 60);
            dp.DestoryAt = 8000;
            dp.ScaleMode |= ScaleMode.YByDistance;
            dp.Name = $"P5二运索尼引导站位{sony}";

            var d = float.Pi / 180f;
            dp.TargetPosition = cpos;
            if (posid == 4) dp.TargetPosition = RotatePoint(npos, cpos, d * -90);
            if (posid == 7) dp.TargetPosition = RotatePoint(npos, cpos, d * 90);


            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }
        [ScriptMethod(name: "P5 二运索尼处理位置(横排法)(ImGui)", eventType: EventTypeEnum.TargetIcon)]
        public void P5_二运索尼处理位置_横排法(Event @event, ScriptAccessory accessory)
        {
            if (parse != 5.2) return;
            if (!ParseObjectId(@event["TargetId"], out var id) || id != accessory.Data.Me) return;
            Task.Delay(100).ContinueWith(ca =>
            {
                var index = accessory.Data.PartyList.ToList().IndexOf(id);
                var sony =p5sony[index];
                var priority = p5sony.IndexOf(sony) == index;
                var cpos = new Vector3(100, 0, 100);
                var npos = 4*Vector3.Normalize(new(p5GreekPos.X-100,p5GreekPos.Y,p5GreekPos.Z-100))+ cpos;
                var npos2 = 20f * Vector3.Normalize(new(p5GreekPos.X - 100, p5GreekPos.Y, p5GreekPos.Z - 100)) + cpos;


                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Color = accessory.Data.DefaultSafeColor;
                dp.Owner = id;
                dp.Scale = new(3, 60);
                dp.DestoryAt = 5000;
                dp.ScaleMode |= ScaleMode.YByDistance;
                dp.Name = $"P5二运索尼{sony}处理位置";

                var dp2 = accessory.Data.GetDefaultDrawProperties();
                dp2.Color = accessory.Data.DefaultSafeColor;
                dp2.Scale = new(1f);
                dp2.DestoryAt = 5000;
                dp2.Name = $"P5二运索尼{sony}击退终点";
                //死宣○
                if (sony == 10)
                {
                    if (priority)
                    {
                        dp.TargetPosition = RotatePoint(npos, cpos, float.Pi / -2);
                        dp2.Position = RotatePoint(npos2, cpos, float.Pi / -2);
                    }
                    else 
                    { 
                        dp.TargetPosition = RotatePoint(npos, cpos, float.Pi / 2);
                        dp2.Position = RotatePoint(npos2, cpos, float.Pi / 2);
                    }
                }
                //死宣▽
                if (sony == 11)
                {
                    dp.TargetPosition = RotatePoint(npos, cpos, float.Pi * 0.75f);
                    dp2.Position = RotatePoint(npos2, cpos, float.Pi * 0.75f);
                }
                //死宣□
                if (sony == 12)
                {
                    dp.TargetPosition = RotatePoint(npos, cpos, float.Pi * -0.75f);
                    dp2.Position = RotatePoint(npos2, cpos, float.Pi * -0.75f);
                }
                //▽
                if (sony == 1)
                {
                    dp.TargetPosition = RotatePoint(npos, cpos, float.Pi * -0.25f);
                    dp2.Position = RotatePoint(npos2, cpos, float.Pi * -0.25f);
                }
                //□
                if (sony == 2)
                {
                    dp.TargetPosition = RotatePoint(npos, cpos, float.Pi * 0.25f);
                    dp2.Position = RotatePoint(npos2, cpos, float.Pi * 0.25f);
                }
                //×
                if (sony == 3)
                {
                    if (priority)
                    {
                        dp.TargetPosition = npos;
                        dp2.Position = npos2;
                    }
                    else
                    {
                        dp.TargetPosition = RotatePoint(npos, cpos, float.Pi);
                        dp2.Position = RotatePoint(npos2, cpos, float.Pi);
                    }
                }


                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp2);
            });

        }



        [ScriptMethod(name: "P5 二运阶段记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27538"], userControl: false)]
        public void P5_二运阶段记录(Event @event, ScriptAccessory sa)
        {
            _dsrPhase = DsrPhase.Phase5HeavensDeath;
            sa.Log.Debug($"当前阶段为：{_dsrPhase}");
        }

        [ScriptMethod(name: "P5 二运斧头哥方位指引", eventType: EventTypeEnum.PlayActionTimeline, eventCondition: ["Id:7747", "SourceDataId:12637"])]
        public void P5_二运斧头哥方位指引(Event @event, ScriptAccessory sa)
        {
            if (_dsrPhase != DsrPhase.Phase5HeavensDeath) return;
            var spos = @event.SourcePosition();
            sa.Log.Debug($"找到斧头哥位置{spos}");
            var dp = sa.DrawDirPos2Pos(_center, spos, 0, 4000, $"场中指向斧头哥", 2f);
            dp.Color = ColorHelper.ColorWhite.V4;
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
        }

        #endregion
        #endregion

        #region P6

        [ScriptMethod(name: "---- 《P6 双龙》 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld"],
            userControl: true)]
        public void P6_分节线(Event @event, ScriptAccessory accessory)
        {
        }

        [ScriptMethod(name: "P6 开场记录", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:26215"], userControl: false)]
        public void P6_开场记录(Event @event, ScriptAccessory accessory)
        {
            parse = 6.1;
            p6FireBallCount = 0;
            p6FireBallCount2 = 0;
        }
        [ScriptMethod(name: "P6 阶段累加", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27969"],userControl:false)]
        public void P6_阶段累加(Event @event, ScriptAccessory accessory)
        {
            parse=Math.Round(parse + 0.1, 1);
            if (ParseObjectId(@event["SourceId"], out var id))
            {
                whiteDragonId = id;
            }
        }
        [ScriptMethod(name: "P6 黑龙ID", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27971"], userControl: false)]
        public void P6_黑龙ID(Event @event, ScriptAccessory accessory)
        {
            if (ParseObjectId(@event["SourceId"], out var id))
            {
                darkDragonId = id;
            }
        }
        [ScriptMethod(name: "P6 白龙位置id记录", eventType: EventTypeEnum.SetObjPos, eventCondition: ["SourceDataId:12613"], userControl: false)]
        public void P6_白龙位置id记录(Event @event, ScriptAccessory accessory)
        {
            p6WhitePos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
            if (ParseObjectId(@event["SourceId"], out var sid))
            {
                whiteDragonId = sid;
            }

        }
        [ScriptMethod(name: "P6 开场冰火线收集", eventType: EventTypeEnum.Tether, userControl: false)]
        public void P6_开场冰火线收集(Event @event, ScriptAccessory accessory)
        {
            if (parse != 6.1) return;
            
            if (!ParseObjectId(@event["SourceId"],out var sid)) return;
            if (!ParseObjectId(@event["TargetId"], out var tid)) return;
            p6tether[accessory.Data.PartyList.ToList().IndexOf(sid)] = tid==whiteDragonId ? 2 : 1;

        }
        [ScriptMethod(name: "P6 第一次冰火线站位(Imgui)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27960"])]
        public void P6_第一次冰火线站位(Event @event, ScriptAccessory accessory)
        {
            if (parse != 6.1) return;
            
            List<Vector3> postions = [new(100, 0, 109.33f), new(95.7f, 0, 119), new(104.3f, 0, 119)];
            //45 26 37
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P6 第一次冰火线站位";
            dp.Owner = accessory.Data.Me;
            dp.ScaleMode |= ScaleMode.YByDistance;
            dp.Scale = new(1.5f);
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.DestoryAt = 7000;
            var idIndex = accessory.Data.PartyList.ToList().IndexOf(accessory.Data.Me);
            //D1
            if (idIndex == 4) dp.TargetPosition = postions[0];
            if (idIndex == 2) dp.TargetPosition = postions[1];
            if (idIndex == 3) dp.TargetPosition = postions[2];
            //D2
            if (idIndex == 5)
            {
                if (p6tether[4]!= p6tether[5])
                {
                    dp.TargetPosition = postions[0];
                }
                else
                {
                    if(p6tether[2] == p6tether[6])
                    {
                        dp.TargetPosition = postions[1];
                    }
                    else
                    {
                        dp.TargetPosition = postions[2];
                    }
                }
            }
            //D3
            if (idIndex == 6)
            {
                if (p6tether[2] != p6tether[6])
                {
                    dp.TargetPosition = postions[1];
                }
                else
                {
                    if (p6tether[4] == p6tether[5])
                    {
                        dp.TargetPosition = postions[0];
                    }
                    else
                    {
                        dp.TargetPosition = postions[2];
                    }
                }
            }
            //D4
            if (idIndex == 7)
            {
                if (p6tether[3] != p6tether[7])
                {
                    dp.TargetPosition = postions[2];
                }
                else
                {
                    if (p6tether[4] == p6tether[5])
                    {
                        dp.TargetPosition = postions[0];
                    }
                    else
                    {
                        dp.TargetPosition = postions[1];
                    }
                }
            }
            if (idIndex >1)
            {
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }
            
        }
        [ScriptMethod(name: "P6 第一次冰火线黑龙扇形", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27955"])]
        public void P6_第一次冰火线黑龙扇形(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultDangerColor;
            if (ParseObjectId(@event["SourceId"], out var id))
            {
                dp.Owner = id;
            }
            dp.Scale = new(50);
            dp.Radian = float.Pi / 6f;
            dp.DestoryAt = 7000;
            dp.Name = "P6 第一次冰火线黑龙扇形";
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

        }
        [ScriptMethod(name: "P6 第一次冰火线白龙扇形", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27957"])]
        public void P6_第一次冰火线白龙扇形(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultDangerColor;
            if (ParseObjectId(@event["SourceId"], out var id))
            {
                dp.Owner = id;
            }
            dp.Scale = new(50);
            dp.Radian = float.Pi / 6f;
            dp.DestoryAt = 7000;
            dp.Name = "P6 第一次冰火线白龙扇形";
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

        }

        [ScriptMethod(
            name: "读条27969自动选中附近最高血量敌人",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:27969"]
        )]
        public void AutoTargetHighestHpEnemy(Event @event, ScriptAccessory accessory)
        {
            if (autoTargetHighestHpRunning) return;

            autoTargetHighestHpRunning = true;
            var startTime = DateTime.Now;

            autoTargetHighestHpActionGuid = accessory.Method.RegistFrameworkUpdateAction(() =>
            {
                try
                {
                    if ((DateTime.Now - startTime).TotalMilliseconds >= 9000)
                    {
                        if (autoTargetHighestHpActionGuid != null)
                        {
                            accessory.Method.UnregistFrameworkUpdateAction(autoTargetHighestHpActionGuid);
                            autoTargetHighestHpActionGuid = null;
                        }

                        autoTargetHighestHpRunning = false;
                        return;
                    }

                    var me = accessory.Data.MyObject;
                    if (me == null) return;

                    var mePos = me.Position;

                    var target = accessory.Data.Objects
                        .Where(obj => obj != null)
                        .OfType<IBattleChara>()
                        .Where(obj =>
                            obj.IsTargetable
                            && !obj.IsDead
                            && obj.CurrentHp > 0
                            && obj.ObjectKind == ObjectKind.BattleNpc
                            && IsEnemyByEnmityList(obj, accessory)
                            && Vector3.Distance(obj.Position, mePos) <= 30
                        )
                        .OrderByDescending(obj => obj.CurrentHp)
                        .FirstOrDefault();

                    if (target != null)
                    {
                        accessory.Method.SelectTarget(target.EntityId);
                    }
                }
                catch (Exception ex)
                {
                    accessory.Log.Debug($"AutoTargetHighestHpEnemy error: {ex.Message}");
                }
            });
        }

        private bool IsEnemyByEnmityList(IBattleChara obj, ScriptAccessory accessory)
        {
            var gameObjectId = obj.GameObjectId;

            foreach (var pair in accessory.Data.EnmityList)
            {
                if (pair.Key == gameObjectId) return true;

                if (pair.Value != null && pair.Value.Contains(gameObjectId))
                {
                    return true;
                }
            }

            return false;
        }

        [ScriptMethod(name: "P6 无尽轮回", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27969"], userControl: false)]
        public void P6_无尽轮回(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P6 无尽轮回";
            dp.Scale = new(4);
            dp.DestoryAt = 8300;
            var idIndex = accessory.Data.PartyList.ToList().IndexOf(accessory.Data.Me);
            if(idIndex==0|| idIndex == 2 || idIndex == 4 || idIndex == 6)
            {
                dp.Owner = accessory.Data.PartyList[2];
                dp.Color = accessory.Data.DefaultSafeColor;
                accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,dp);

                dp.Owner = accessory.Data.PartyList[3];
                dp.Color = accessory.Data.DefaultDangerColor;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }
            else
            {
                dp.Owner = accessory.Data.PartyList[3];
                dp.Color = accessory.Data.DefaultSafeColor;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

                dp.Owner = accessory.Data.PartyList[2];
                dp.Color = accessory.Data.DefaultDangerColor;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }

        }
        [ScriptMethod(name: "P6 灭杀誓言分散", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:27960"], userControl: false)]
        public void P6_灭杀誓言分散(Event @event, ScriptAccessory accessory)
        {
            if (parse != 6.1) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.DestoryAt = 7300;
            for (int i = 4; i < accessory.Data.PartyList.Count; i++)
            {
                dp.Name = $"P6 灭杀誓言分散 D{i-3}";
                dp.Owner = accessory.Data.PartyList[i];
                dp.Color = accessory.Data.DefaultDangerColor;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }
        }
        [ScriptMethod(name: "P6 灭杀誓言范围", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2896"])]
        public void P6_灭杀誓言范围(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultDangerColor;
            if (ParseObjectId(@event["TargetId"], out var id))
            {
                dp.Owner = id;
            }
            dp.Scale = new(5);
            dp.DestoryAt = 5000;
            dp.Delay = int.TryParse(@event["DurationMilliseconds"], out var d) ? d - 5000 : 0;
            
            dp.Name = $"P6 灭杀誓言";

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        }
        [ScriptMethod(name: "P6 第一次冰火线安全点(ImGui)", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:26215"])]
        public void P6_第一次冰火线安全点(Event @event, ScriptAccessory accessory)
        {
            if (parse != 6.1) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultSafeColor;
            
            dp.Scale = new(0.2f);
            dp.Delay = 20000;
            dp.DestoryAt = 15000;
            dp.Name = $"P6 第一次冰火线安全点";

            dp.Position = new(95.7f, 0, 119);
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
            dp.Position = new(104.3f, 0, 119);
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
            dp.Position = new(100, 0, 109.33f);
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
        }
        [ScriptMethod(name: "P6 神圣之翼(左近)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27939"])]
        public void P6_神圣之翼左近(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultDangerColor;
            if (ParseObjectId(@event["SourceId"], out var id))
            {
                dp.Owner = id;
            }
            dp.Scale = new(10);
            dp.DestoryAt = 8000;
            dp.CentreResolvePattern=PositionResolvePatternEnum.PlayerNearestOrder;
            dp.CentreOrderIndex = 1;
            dp.Name = "P6_神圣之翼近1";
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            dp.CentreOrderIndex = 2;
            dp.Name = "P6_神圣之翼近2";
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            var dp2 = accessory.Data.GetDefaultDrawProperties();
            dp2.Color = accessory.Data.DefaultDangerColor;
            dp2.Scale = new(22, 50);
            dp2.Owner = id;
            dp2.DestoryAt = 8000;
            dp2.Offset = new(-11, 0, 0);
            dp2.Name = "P6_神圣之翼左";
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp2);

        }
        [ScriptMethod(name: "P6 神圣之翼(左远)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27940"])]
        public void P6_神圣之翼左远(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultDangerColor;
            if (ParseObjectId(@event["SourceId"], out var id))
            {
                dp.Owner = id;
            }
            dp.Scale = new(10);
            dp.DestoryAt = 8000;
            dp.CentreResolvePattern = PositionResolvePatternEnum.PlayerFarestOrder;
            dp.CentreOrderIndex = 1;
            dp.Name = "P6_神圣之翼远1";
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            dp.CentreOrderIndex = 2;
            dp.Name = "P6_神圣之翼远2";
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            var dp2 = accessory.Data.GetDefaultDrawProperties();
            dp2.Color = accessory.Data.DefaultDangerColor;
            dp2.Scale = new(22, 50);
            dp2.Owner = id;
            dp2.DestoryAt = 8000;
            dp2.Offset = new(-11, 0, 0);
            dp2.Name = "P6_神圣之翼左";
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp2);

        }
        [ScriptMethod(name: "P6 神圣之翼(右近)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27942"])]
        public void P6_神圣之翼右近(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultDangerColor;
            if (ParseObjectId(@event["SourceId"], out var id))
            {
                dp.Owner = id;
            }
            dp.Scale = new(10);
            dp.DestoryAt = 8000;
            dp.CentreResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
            dp.CentreOrderIndex = 1;
            dp.Name = "P6_神圣之翼近1";
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            dp.CentreOrderIndex = 2;
            dp.Name = "P6_神圣之翼近2";
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            var dp2 = accessory.Data.GetDefaultDrawProperties();
            dp2.Color = accessory.Data.DefaultDangerColor;
            dp2.Scale = new(22, 50);
            dp2.Owner = id;
            dp2.DestoryAt = 8000;
            dp2.Offset = new(11, 0, 0);
            dp2.Name = "P6_神圣之翼右";
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp2);

        }
        [ScriptMethod(name: "P6 神圣之翼(右远)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27943"])]
        public void P6_神圣之翼右远(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultDangerColor;
            if (ParseObjectId(@event["SourceId"], out var id))
            {
                dp.Owner = id;
            }
            dp.Scale = new(10);
            dp.DestoryAt = 8000;
            dp.CentreResolvePattern = PositionResolvePatternEnum.PlayerFarestOrder;
            dp.CentreOrderIndex = 1;
            dp.Name = "P6_神圣之翼远1";
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            dp.CentreOrderIndex = 2;
            dp.Name = "P6_神圣之翼远2";
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            var dp2 = accessory.Data.GetDefaultDrawProperties();
            dp2.Color = accessory.Data.DefaultDangerColor;
            dp2.Scale = new(22, 50);
            dp2.Owner = id;
            dp2.DestoryAt = 8000;
            dp2.Offset = new(11, 0, 0);
            dp2.Name = "P6_神圣之翼右";
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp2);

        }
        [ScriptMethod(name: "P6 第一次黑龙俯冲", eventType: EventTypeEnum.StartCasting)]
        public void P6_第一次黑龙俯冲(Event @event, ScriptAccessory accessory)
        {
            if (parse != 6.2) return;
            if (!uint.TryParse(@event["ActionId"], out var actionId)) return;
            if (actionId != 27939 && actionId != 27940 && actionId != 27942 && actionId != 27943) return;

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Scale = new(22, 80);
            dp.Owner = darkDragonId;
            dp.DestoryAt = 7500;
            dp.Name = "P6_第一次黑龙俯冲";
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

        }
        [ScriptMethod(name: "P6 燃烧之翼", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27948"])]
        public void P6_燃烧之翼(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultDangerColor;
            if (ParseObjectId(@event["SourceId"], out var id))
            {
                dp.Owner = id;
            }
            dp.Scale = new(21, 50);
            dp.DestoryAt = 6500;
            dp.Name = "P6 燃烧之翼";
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

        }
        [ScriptMethod(name: "P6 燃烧之尾", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27950"])]
        public void P6_燃烧之尾(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultDangerColor;
            if (ParseObjectId(@event["SourceId"], out var id))
            {
                dp.Owner = id;
            }
            dp.Scale = new(18, 50);
            dp.DestoryAt = 6500;
            dp.Name = "P6 燃烧之尾";
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

        }
        [ScriptMethod(name: "P6 火球范围", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:13238"])]
        public void P6_火球范围(Event @event, ScriptAccessory accessory)
        {
            lock (lockObj)
            {
                p6FireBallCount++;
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Color=accessory.Data.DefaultDangerColor;
                //第一轮
                if (p6FireBallCount == 3)
                {
                    dp.Name = "P6 火球范围1";
                    dp.Scale = new(18, 44);
                    dp.Position = new Vector3(100, 0, 100);
                    dp.DestoryAt = 12000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
                    dp.Rotation = float.Pi / 2;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
                }
                //第二轮
                if (p6FireBallCount == 6)
                {
                    dp.Name = "P6 火球范围2";
                    dp.Scale = new(18, 70);
                    var ipos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
                    var pos = new Vector3(100, 0, 100);
                    if (ipos.X < 93.5f) pos.X = 87;
                    if (ipos.X > 106.5f) pos.X = 113;
                    if (ipos.Z < 93.5f) pos.Z = 87;
                    if (ipos.Z > 106.5f) pos.Z = 113;
                    dp.Position = pos;
                    dp.Delay = 6000;
                    dp.DestoryAt = 12000 - dp.Delay;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
                    dp.Rotation = float.Pi / 2;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
                }
                if (p6FireBallCount == 9)
                {
                    dp.Name = "P6 火球范围3";
                    dp.Scale = new(18, 70);
                    var ipos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
                    var pos = new Vector3(100, 0, 100);
                    if (ipos.X < 93.5f) pos.X = 87;
                    if (ipos.X > 106.5f) pos.X = 113;
                    if (ipos.Z < 93.5f) pos.Z = 87;
                    if (ipos.Z > 106.5f) pos.Z = 113;
                    dp.Position = pos;
                    dp.Delay = 8000;
                    dp.DestoryAt = 12000- dp.Delay;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
                    dp.Rotation = float.Pi / 2;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
                }
            }
        }
        [ScriptMethod(name: "P6 十字火白龙俯冲", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:27973"])]
        public void P6_十字火白龙俯冲(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Owner = whiteDragonId;
            dp.Scale = new(22, 80);
            dp.Delay = 1500;
            dp.DestoryAt = 11000- dp.Delay;
            dp.Name = "P6 十字火白龙俯冲";
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

        }
        
        [ScriptMethod(name: "P6 十字火起跑位置(ImGui)", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:13238"])]
        public void P6_十字火起跑位置(Event @event, ScriptAccessory accessory)
        {
            lock (lockObj)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                p6FireBallCount2++;
                if (p6FireBallCount2 == 6)
                {
                    dp.Name = "P6 十字火起跑位置";
                    dp.Scale = new(1.5f);
                    dp.ScaleMode |= ScaleMode.YByDistance;
                    dp.Color=accessory.Data.DefaultSafeColor;
                    dp.Owner = accessory.Data.Me;

                    var ipos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
                    var pos = new Vector3(100, 0, 100);
                    if (ipos.Z < 93.5f) pos.Z = 109.5f;
                    if (ipos.Z > 106.5f) pos.Z = 90.5f;
                    if (p6WhitePos.X < 99) pos.X = 121.5f;
                    else pos.X = 78.5f;

                    dp.TargetPosition = pos;
                    dp.DestoryAt = 6000;

                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                    
                }
                
            }
        }
        [ScriptMethod(name: "P6 第二次冰火线ND站位(ImGui)", eventType: EventTypeEnum.StartCasting)]
        public void P6_第二次冰火线ND站位(Event @event, ScriptAccessory accessory)
        {
            if (parse != 6.3) return;
            var aidStr = @event["ActionId"];
            if (aidStr != "27956" && aidStr != "27957") return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P6 第二次冰火线ND站位";
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.Owner = accessory.Data.Me;
            dp.Scale = new(1.5f);
            dp.ScaleMode |= ScaleMode.YByDistance;
            dp.DestoryAt = 7000;

            var idIndex = accessory.Data.PartyList.ToList().IndexOf(accessory.Data.Me);

            if (idIndex == 2) dp.TargetPosition = new(100, 0, 80.5f);
            if (idIndex == 3) dp.TargetPosition = new(100, 0, 119.7f);
            if (idIndex == 4) dp.TargetPosition = new(103.7f, 0, 89.2f);
            if (idIndex == 5) dp.TargetPosition = new(97, 0, 110.2f);
            if (idIndex == 6) dp.TargetPosition = new(107.2f, 0, 81.7f);
            if (idIndex == 7) dp.TargetPosition = new(92.5f, 0, 118);

            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

        }
        [ScriptMethod(name: "P6 双龙冰火俯冲", eventType: EventTypeEnum.StatusAdd)]
        public void P6_双龙冰火俯冲(Event @event, ScriptAccessory accessory)
        {
            if (parse != 6.3) return;
            var StatusIDStr = @event["StatusID"];
            if (StatusIDStr != "2898" && StatusIDStr != "2899") return;
            if (!ParseObjectId(@event["TargetId"], out var id) || id != accessory.Data.Me) return;

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Scale = new(22, 56);
            dp.Delay= 6500;
            dp.DestoryAt = 12500-dp.Delay;
            if (StatusIDStr == "2898")
            {
                dp.Name = "P6 双龙冰火俯冲 黑龙 火 危险";
                dp.Owner = darkDragonId;
                dp.Color = accessory.Data.DefaultDangerColor;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

                dp.Name = "P6 双龙冰火俯冲 白龙 火 安全";
                dp.Owner = whiteDragonId;
                dp.Color = accessory.Data.DefaultSafeColor;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            }
            else
            {
                dp.Name = "P6 双龙冰火俯冲 黑龙 冰 安全";
                dp.Owner = darkDragonId;
                dp.Color = accessory.Data.DefaultSafeColor;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

                dp.Name = "P6 双龙冰火俯冲 白龙 冰 危险";
                dp.Owner = whiteDragonId;
                dp.Color = accessory.Data.DefaultDangerColor;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            }
            

        }

        [ScriptMethod(name: "P6 双龙冰火俯冲 T黑龙", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27966"])]
        public void P6_双龙冰火俯冲_T黑龙(Event @event, ScriptAccessory accessory)
        {
            if (parse != 6.3) return;
            var index= accessory.Data.PartyList.ToList().IndexOf(accessory.Data.Me);
            if (index == 0)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Scale = new(22, 56);
                dp.DestoryAt = 5000;
                dp.Name = "P6 双龙冰火俯冲 MT黑龙 危险";
                dp.Owner = darkDragonId;
                dp.Color = accessory.Data.DefaultDangerColor;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            }
            if (index == 1)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Scale = new(22, 56);
                dp.DestoryAt = 5000;
                dp.Name = "P6 双龙冰火俯冲 ST黑龙 安全";
                dp.Owner = darkDragonId;
                dp.Color = accessory.Data.DefaultSafeColor;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            }
        }
        [ScriptMethod(name: "P6 双龙冰火俯冲 T白龙", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27966"])]
        public void P6_双龙冰火俯冲_T白龙(Event @event, ScriptAccessory accessory)
        {
            if (parse != 6.3) return;
            var index = accessory.Data.PartyList.ToList().IndexOf(accessory.Data.Me);
            if (index == 0)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Scale = new(22, 56);
                dp.DestoryAt = 5000;
                dp.Name = "P6 双龙冰火俯冲 MT白龙 安全";
                dp.Owner = whiteDragonId;
                dp.Color = accessory.Data.DefaultSafeColor;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            }
            if (index == 1)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Scale = new(22, 56);
                dp.DestoryAt = 5000;
                dp.Name = "P6 双龙冰火俯冲 ST白龙 危险";
                dp.Owner = whiteDragonId;
                dp.Color = accessory.Data.DefaultDangerColor;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            }
        }

        [ScriptMethod(name: "P6 暗buff记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2758"], userControl: false)]
        public void P6_暗buff记录(Event @event, ScriptAccessory accessory)
        {
            if (ParseObjectId(@event["TargetId"], out var id))
            {
                p6lightDark[accessory.Data.PartyList.ToList().IndexOf(id)] = 1;
            }
            
        }
        [ScriptMethod(name: "P6 光buff记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2759"], userControl: false)]
        public void P6_光buff记录(Event @event, ScriptAccessory accessory)
        {
            if (ParseObjectId(@event["TargetId"], out var id))
            {
                p6lightDark[accessory.Data.PartyList.ToList().IndexOf(id)] = 2;
            }

        }
        [ScriptMethod(name: "P6 邪念之炎/同归于尽之炎", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27974"])]
        public void P6_邪念之炎(Event @event, ScriptAccessory accessory)
        {
            Task.Delay(18000).ContinueWith(t =>
            {
                var plist = accessory.Data.PartyList.ToList();
                var idIndex = plist.IndexOf(accessory.Data.Me);
                for (int i = 0; i < p6lightDark.Count; i++)
                {
                    if (p6lightDark[i] == 0) continue;
                    if (p6lightDark[i] == 1)
                    {
                        var dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Owner = plist[i];
                        dp.Color = accessory.Data.DefaultDangerColor;
                        dp.Scale = new(5);
                        dp.DestoryAt = 5000;
                        dp.Name = "P6 邪念之炎";
                        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                    }
                    if(p6lightDark[i] == 2)
                    {
                        var dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Owner = plist[i];
                        dp.Scale = new(4);
                        dp.DestoryAt = 5000;
                        dp.Name = "P6 同归于尽之炎";
                        if (i==idIndex ||(p6lightDark.IndexOf(2)==i && p6lightDark.IndexOf(0)==idIndex)|| (p6lightDark.LastIndexOf(2) == i && p6lightDark.LastIndexOf(0) == idIndex))
                        {
                            dp.Color = accessory.Data.DefaultSafeColor;
                        }
                        else
                        {
                            dp.Color = accessory.Data.DefaultDangerColor;
                        }
                        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                    }
                    
                }
                
            });
            

        }
        [ScriptMethod(name: "P6 邪念之炎/同归于尽之炎标记", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27974"], userControl: false)]
        public void P6_邪念之炎标记(Event @event, ScriptAccessory accessory)
        {
            if (!p6Mark) return;
            accessory.Method.MarkClear();
            Task.Delay(50).ContinueWith(t =>
            {
                var plist = accessory.Data.PartyList.ToList();
                int attack = 0;
                int stop = 8;
                int bind = 5;
                for (int i = 0; i < p6lightDark.Count; i++)
                {
                    if (p6lightDark[i] == 0)
                    {
                        //无
                        stop++;
                        accessory.Method.Mark(plist[i], (MarkType)stop);
                    }
                    if (p6lightDark[i] == 1)
                    {
                        //分散
                        attack++;
                        accessory.Method.Mark(plist[i], (MarkType)attack);
                    }
                    if (p6lightDark[i] == 2)
                    {
                        //分摊
                        bind++;
                        accessory.Method.Mark(plist[i], (MarkType)bind);
                    }

                }
            });
            Task.Delay(23000).ContinueWith(t =>
            {
                accessory.Method.MarkClear();
            });
        }

        #region 冰火

        [ScriptMethod(name: "P6 一冰火阶段记录", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:12613"], userControl: false)]
        public void P6_一冰火阶段记录(Event @event, ScriptAccessory sa)
        {
            // 圣龙出现代表进入一冰火
            if (_dsrPhase != DsrPhase.Phase5HeavensDeath) return;
            _dsrPhase = DsrPhase.Phase6IceAndFire1;
            _p6DragonsGlowAction = [false, false];
            _recorded = new bool[20].ToList();
            sa.Log.Debug($"当前阶段为：{_dsrPhase}");
        }

        [ScriptMethod(name: "P6 二冰火阶段记录", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(2794[79])$"], userControl: false)]
        public void P6_二冰火阶段记录(Event @event, ScriptAccessory sa)
        {
            // 以辣翅辣尾作为二冰火的开始
            if (_dsrPhase != DsrPhase.Phase6NearOrFar2) return;
            _dsrPhase = DsrPhase.Phase6IceAndFire2;
            _p6DragonsGlowAction = [false, false];
            _recorded = new bool[20].ToList();
            sa.Log.Debug($"当前阶段为：{_dsrPhase}");
        }


        [ScriptMethod(name: "P6 冰火吐息记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(2795[4567])$"], userControl: false)]
        public void P6_冰火吐息记录(Event @event, ScriptAccessory accessory)
        {
            const uint blackBuster = 27954;
            const uint whiteBuster = 27956;
            const uint blackGlow = 27955;
            const uint whiteGlow = 27957;

            if (_dsrPhase != DsrPhase.Phase6IceAndFire1 && _dsrPhase != DsrPhase.Phase6IceAndFire2) return;
            var aid = @event.ActionId();
            switch (aid)
            {
                case blackBuster:
                case blackGlow:
                    _p6DragonsGlowAction[0] = aid == blackGlow;
                    break;
                case whiteBuster:
                case whiteGlow:
                    _p6DragonsGlowAction[1] = aid == whiteGlow;
                    break;
            }

            lock (_recorded)
            {
                _recorded[1] = _recorded[0];
                _recorded[0] = true;
                if (_recorded[0] && _recorded[1])
                    _iceAndFireEvent.Set();
            }
        }

        [ScriptMethod(name: "P6 冰火死刑双T处理", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27960"])]
        public void P6_冰火死刑双T处理(Event @event, ScriptAccessory accessory)
        {
            if (_dsrPhase is not (DsrPhase.Phase6IceAndFire1 or DsrPhase.Phase6IceAndFire2))
                return;
            _iceAndFireEvent.WaitOne();
            // await Task.Delay(100);
            var myIndex = accessory.GetMyIndex();
            var tankBusterPosition = new Vector3[4];
            tankBusterPosition[0] = new Vector3(84.5f, 0, 88f);
            tankBusterPosition[1] = tankBusterPosition[0].FoldPointHorizon(_center.X);
            tankBusterPosition[2] = tankBusterPosition[0];
            tankBusterPosition[3] = tankBusterPosition[1].FoldPointVertical(_center.Z);

            if (_p6DragonsGlowAction[0] && _p6DragonsGlowAction[1])
            {
                // 场中分摊死刑，自己不是T不显示指路
                if (myIndex > 1) return;
                // 删除K佬脚本中双T的小啾啾
                accessory.Method.RemoveDraw("P6 第二次冰火线ND站位.*");
                var dp = accessory.DrawDirPos(_center, 0, 6000, $"冰火场中分摊指路");
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }
            else
            {
                // 场边死刑，自己的死刑不显示圈，避免瞎眼
                var busterIdx = _p6DragonsGlowAction.FindIndex(x => x == false);

                var str = "";
                str += $"黑龙喷:{_p6DragonsGlowAction[0]}, 白龙喷:{_p6DragonsGlowAction[1]}\n";
                str += $"是{(busterIdx == 0 ? "黑龙" : "白龙")}的死刑。";
                accessory.Log.Debug($"{str}");

                var isMyBuster = myIndex == busterIdx;
                var dp = accessory.DrawCircle(accessory.Data.PartyList[busterIdx], isMyBuster ? 2f : 15f, 0, 6000, $"冰火死刑");
                dp.Color = isMyBuster ? ColorHelper.ColorRed.V4 : ColorHelper.ColorYellow.V4;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

                // 场边分散，自己不是T不显示指路
                if (myIndex > 1) return;
                // 删除K佬脚本中双T的小啾啾
                accessory.Method.RemoveDraw("P6 第二次冰火线ND站位.*");
                var isIceAndFire2 = _dsrPhase == DsrPhase.Phase6IceAndFire2;

                var dp0 = accessory.DrawDirPos(tankBusterPosition[isIceAndFire2 ? myIndex + 2 : myIndex], 0, 6000,
                    $"冰火死刑位置指路");
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp0);

                var dp1 = accessory.DrawStaticCircle(tankBusterPosition[isIceAndFire2 ? myIndex + 2 : myIndex],
                    PosColorPlayer.V4.WithW(1.5f), 0, 6000, $"冰火死刑点区域", 1f);
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp1);
            }
            _iceAndFireEvent.Reset();
        }

        #endregion 冰火

        #region 远近

        [ScriptMethod(name: "P6 远近阶段记录", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:27970"], userControl: false)]
        public void P6_远近阶段记录(Event @event, ScriptAccessory accessory)
        {
            // 因为黑龙先飞，白龙后读条，所以用无尽轮回的ActionEffect做阶段节点
            if (_dsrPhase is DsrPhase.Phase6NearOrFar1 or DsrPhase.Phase6NearOrFar2)
                return;
            _dsrPhase = _dsrPhase switch
            {
                DsrPhase.Phase6IceAndFire1 => DsrPhase.Phase6NearOrFar1,
                DsrPhase.Phase6Flame => DsrPhase.Phase6NearOrFar2,
                _ => DsrPhase.Phase6NearOrFar1,
            };
            _p6DragonsWingAction = [false, false, false];   // P6 双龙远近记录
            accessory.Log.Debug($"当前阶段为：{_dsrPhase}");
        }

        [ScriptMethod(name: "P6 远近翅膀记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(279(39|4[023]))$"], userControl: false)]
        public void P6_远近翅膀记录(Event @event, ScriptAccessory accessory)
        {
            // LEFT左翼发光，玩家视角左侧安全。
            const uint leftFar = 27940;
            const uint leftNear = 27939;
            const uint rightFar = 27943;
            // const uint rightNear = 27942;

            if (_dsrPhase is not (DsrPhase.Phase6NearOrFar1 or DsrPhase.Phase6NearOrFar2))
                return;

            var aid = @event.ActionId();
            // [远T/近F，左安全T/右安全F，前安全T/后安全F/内安全T/外安全F]
            _p6DragonsWingAction[0] = aid is leftFar or rightFar;
            _p6DragonsWingAction[1] = aid is leftFar or leftNear;
            accessory.Log.Debug($"检测到{(_p6DragonsWingAction[0] ? "T远离" : "T靠近")}, {(_p6DragonsWingAction[1] ? "左" : "右")}安全");
            _nearOrFarWingsEvent.Set();
        }


        [ScriptMethod(name: "P6 远近俯冲记录", eventType: EventTypeEnum.PlayActionTimeline, eventCondition: ["Id:7747", "SourceDataId:12612"], userControl: false)]
        public void P6_远近俯冲记录(Event @event, ScriptAccessory accessory)
        {
            if (_dsrPhase != DsrPhase.Phase6NearOrFar1) return;
            var spos = @event.SourcePosition();
            // [远T/近F，左安全T/右安全F，前安全T/后安全F/内安全T/外安全F]
            _p6DragonsWingAction[2] = spos.X < _center.X;
            accessory.Log.Debug($"检测到{(_p6DragonsWingAction[2] ? "前安全" : "后安全")}");
            _nearOrFarCauterizeEvent.Set();
        }

        [ScriptMethod(name: "P6 远近内外记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(2794[79])$"], userControl: false)]
        public void P6_远近内外记录(Event @event, ScriptAccessory accessory)
        {
            const uint insideSafe = 27947;
            // const uint outsideSafe = 27949;
            if (_dsrPhase != DsrPhase.Phase6NearOrFar2) return;
            var aid = @event.ActionId();
            // [远T/近F，左安全T/右安全F，前安全T/后安全F/内安全T/外安全F]
            _p6DragonsWingAction[2] = aid == insideSafe;
            accessory.Log.Debug($"检测到{(_p6DragonsWingAction[2] ? "内安全" : "外安全")}");
            _nearOrFarInOutEvent.Set();
        }

        [ScriptMethod(name: "P6 一远近指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(279(39|4[023]))$"])]
        public void P6_一远近指路(Event @event, ScriptAccessory accessory)
        {
            if (_dsrPhase != DsrPhase.Phase6NearOrFar1) return;
            _nearOrFarCauterizeEvent.WaitOne();
            _nearOrFarWingsEvent.WaitOne();
            Vector3[] nearOrFarSafePos = P6_取象限安全点(_p6DragonsWingAction);
            var nearOrFarDirPosIdx = P6_取象限安全点序号(_p6DragonsWingAction);
            accessory.Log.Debug($"MT去{nearOrFarDirPosIdx[0]}, ST去{nearOrFarDirPosIdx[1]}, 人群去{nearOrFarDirPosIdx[2]}");

            var myIndex = accessory.GetMyIndex();
            var myPartIdx = myIndex >= 2 ? 2 : myIndex;
            var targetPos = nearOrFarSafePos[nearOrFarDirPosIdx[myPartIdx]];

            for (var i = 0; i < 3; i++)
            {
                var tempPos = nearOrFarSafePos[nearOrFarDirPosIdx[i]];
                var color = i == myPartIdx ? PosColorPlayer.V4.WithW(1.5f) : PosColorNormal.V4;
                var dp0 = accessory.DrawStaticCircle(tempPos, color, 0, 7500, $"一远近位置{i}", 1f);
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp0);
            }

            var dp = accessory.DrawDirPos(targetPos, 0, 7500, $"一远近指路");
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            _nearOrFarCauterizeEvent.Reset();
            _nearOrFarWingsEvent.Reset();
        }

        private Vector3[] P6_取象限安全点(List<bool> wings)
        {
            // 第一象限内的四个端点
            // 象限内四个点Idx顺序为，以第一象限基准（面向白龙左上），从左上开始顺时针
            // 上下平移，左右折叠
            Vector3[] quarterSafePos = new Vector3[4];
            quarterSafePos[0] = new Vector3(120f, 0, 80f);
            quarterSafePos[1] = new Vector3(120f, 0, 98f);
            quarterSafePos[2] = new Vector3(102f, 0, 98f);
            quarterSafePos[3] = new Vector3(102f, 0, 80f);
            for (var i = 0; i < 4; i++)
            {
                // 后安全，向后平移
                if (!wings[2])
                    quarterSafePos[i] -= new Vector3(22f, 0, 0);
                // 右安全，左右折叠
                if (!wings[1])
                    quarterSafePos[i] = quarterSafePos[i].FoldPointVertical(_center.Z);
            }
            return quarterSafePos;
        }

        private static int[] P6_取象限安全点序号(List<bool> wings)
        {
            // return数组，代表MT、ST、人群的安全位置Index

            // 打远，双T远离，人群靠近
            // 打近，双T靠近，人群远离
            return wings[0] ? [2, 3, 1] : [1, 0, 3];
        }

        [ScriptMethod(name: "P6 二远近指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(2794[79])$"])]
        public void P6_二远近指路(Event @event, ScriptAccessory accessory)
        {
            if (_dsrPhase != DsrPhase.Phase6NearOrFar2) return;
            _nearOrFarInOutEvent.WaitOne();
            _nearOrFarWingsEvent.WaitOne();

            Vector3[] nearOrFarSafePos = P6_取直线安全点(_p6DragonsWingAction);
            int[] nearOrFarDirPosIdx = P6_取直线安全点序号(_p6DragonsWingAction);

            var myIndex = accessory.GetMyIndex();
            var myPartIdx = myIndex >= 2 ? 2 : myIndex;
            var targetPos = nearOrFarSafePos[nearOrFarDirPosIdx[myPartIdx]];

            for (var i = 0; i < 3; i++)
            {
                var color = i == myPartIdx ? PosColorPlayer.V4.WithW(1.5f) : PosColorNormal.V4;
                var tempPos = nearOrFarSafePos[nearOrFarDirPosIdx[i]];
                var dp0 = accessory.DrawStaticCircle(tempPos, color, 0, 7500, $"二远近位置{i}", 1f);
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp0);
            }

            var dp = accessory.DrawDirPos(targetPos, 0, 7500, $"二远近指路");
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            _nearOrFarInOutEvent.Reset();
            _nearOrFarWingsEvent.Reset();
        }

        private static Vector3[] P6_取直线安全点(List<bool> wings)
        {
            // 直线近中远三点
            Vector3[] lineSafePos = new Vector3[3];
            lineSafePos[0] = new Vector3(120f, 0, 100f);
            lineSafePos[1] = new Vector3(100f, 0, 100f);
            lineSafePos[2] = new Vector3(80f, 0, 100f);

            Vector3 dv3 = new(0f, 0f, 0f);

            // 左安全减，右安全加
            dv3 += new Vector3(0f, 0f, 2f) * (wings[1] ? -1 : 1);
            // 内安全不动，外安全乘
            dv3 *= wings[2] ? 1 : 5;

            for (var i = 0; i < 3; i++)
                lineSafePos[i] += dv3;

            return lineSafePos;
        }

        private static int[] P6_取直线安全点序号(List<bool> wings)
        {
            // return数组，代表MT、ST、人群的安全位置Index

            // 打远，双T远离，人群靠近
            // 打近，双T靠近，人群远离
            return wings[0] ? [1, 2, 0] : [1, 0, 2];
        }

        #endregion 远近

        #region 十字火

        [ScriptMethod(name: "P6 十字火阶段记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27973"], userControl: false)]
        public void P6_十字火阶段记录(Event @event, ScriptAccessory accessory)
        {
            _dsrPhase = DsrPhase.Phase6Flame;
            accessory.Log.Debug($"当前阶段为：{_dsrPhase}");
        }

        [ScriptMethod(name: "P6 十字火分摊目标", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27974"])]
        public void P6_十字火分摊目标(Event @event, ScriptAccessory accessory)
        {
            if (_dsrPhase != DsrPhase.Phase6Flame) return;
            var tid = @event.TargetId();
            var dp = accessory.DrawCircle(tid, 6, 0, 12500, $"死亡轮回目标");
            dp.Color = accessory.Data.DefaultSafeColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        #endregion 十字火

        #region 俯冲

        [ScriptMethod(name: "P6 俯冲双T指路", eventType: EventTypeEnum.PlayActionTimeline, eventCondition: ["Id:7737", "SourceDataId:12613"])]
        public void P6_俯冲双T指路(Event @event, ScriptAccessory accessory)
        {
            if (_dsrPhase != DsrPhase.Phase6IceAndFire2) return;
            _dsrPhase = DsrPhase.Phase6Cauterize;
            accessory.Log.Debug($"当前阶段为：{_dsrPhase}");

            Vector3[] cauterizePos = new Vector3[2];
            cauterizePos[0] = new Vector3(95f, 0, 79f);
            cauterizePos[1] = new Vector3(105f, 0, 79f);

            var myIndex = accessory.GetMyIndex();
            if (myIndex > 1) return;

            var dp = accessory.DrawDirPos(cauterizePos[myIndex], 0, 5000, $"俯冲T挡枪位置{myIndex}");
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }

        #endregion 俯冲

        #endregion

        #region P7

        [ScriptMethod(name: "---- 《P7 龙威骑神托尔丹》 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld"],
            userControl: true)]
        public void P7_分节线(Event @event, ScriptAccessory accessory)
        {
        }

        [ScriptMethod(name: "P7 开场记录", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:29752"], userControl: false)]
        public void P7_开场记录(Event @event, ScriptAccessory accessory)
        {
            parse = 7.0;
        }
        [ScriptMethod(name: "P7 阶段累加地火", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:28059"], userControl: false)]
        public void P7_阶段累加地火(Event @event, ScriptAccessory accessory)
        {
            parse = Math.Round(parse + 0.1, 1);
        }
        [ScriptMethod(name: "P7 阶段累加死亡轮回", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:28051"], userControl: false)]
        public void P7_阶段累加死亡轮回(Event @event, ScriptAccessory accessory)
        {
            parse = Math.Round(parse + 0.1, 1);
        }
        [ScriptMethod(name: "P7 阶段累加陨石", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:28057"], userControl: false)]
        public void P7_阶段累加陨石(Event @event, ScriptAccessory accessory)
        {
            parse = Math.Round(parse + 0.1, 1);
        }
        [ScriptMethod(name: "P7 钢铁", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2056", "StackCount:42"])]
        public void P7_钢铁(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P7 钢铁";
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Scale = new(8);
            if (ParseObjectId(@event["TargetId"], out var id))
            {
                dp.Owner = id;
            }
            if (parse == 7.3 || parse == 7.6 || parse == 7.9)
            {
                dp.DestoryAt = 8000;
            }
            else
            {
                dp.DestoryAt = 6000;
            }
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        [ScriptMethod(name: "P7 月环", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2056", "StackCount:43"])]
        public void P7_月环(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P7 月环";
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Radian = float.Pi * 2;
            dp.Scale = new(50);
            dp.InnerScale = new(8);
            if (ParseObjectId(@event["TargetId"], out var id))
            {
                dp.Owner = id;
            }
            if (parse == 7.3 || parse == 7.6 || parse == 7.9)
            {
                dp.DestoryAt = 8000;
            }else
            {
                dp.DestoryAt = 6000;
            }
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
        }
        [ScriptMethod(name: "P7 脑死地火点位", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:28059"])]
        public void P7_脑死地火点位(Event @event, ScriptAccessory accessory)
        {
            var cpos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
            var r = float.Parse(@event["SourceRotation"]);

            var pos1 = new Vector3(cpos.X + MathF.Sin(r)*-8, cpos.Y, cpos.Z + MathF.Cos(r)*-8);
            var pos2 = new Vector3(cpos.X + MathF.Sin(r) * -14, cpos.Y, cpos.Z + MathF.Cos(r) * -14);

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P7 脑死地火点位1";
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.Scale = new(1.5f);
            dp.ScaleMode |= ScaleMode.YByDistance;
            dp.Owner = accessory.Data.Me;
            dp.TargetPosition=pos1;
            dp.DestoryAt = 9000;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P7 脑死地火点位2";
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.Scale = new(1.5f);
            dp.ScaleMode |= ScaleMode.YByDistance;
            dp.Owner = accessory.Data.Me;
            dp.TargetPosition = pos2;
            dp.Delay = 9000;
            dp.DestoryAt = 2000;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P7 脑死地火点位3";
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.Scale = new(1.5f);
            dp.ScaleMode |= ScaleMode.YByDistance;
            dp.Position = pos1;
            dp.TargetPosition = pos2;
            dp.DestoryAt = 9000;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);



        }

        [ScriptMethod(name: "P7 死亡轮回剑分摊处(Imgui)", eventType: EventTypeEnum.StartCasting)]
        public void P7_死亡轮回剑分摊处(Event @event, ScriptAccessory accessory)
        {
            Task.Delay(50).ContinueWith(t =>
            {
                var idstr = @event["ActionId"];
                if (idstr != "29452" && idstr != "29453" && idstr != "29454") return;

                var idIndex = accessory.Data.PartyList.ToList().IndexOf(accessory.Data.Me);

                var isme = false;
                accessory.Log.Debug($"parse:{parse}");
                if (parse == 7.2 || !p7_116)
                {
                    if (idstr == "29452" && (idIndex == 3 || idIndex == 5 || idIndex == 7)) isme = true;
                    if (idstr == "29453" && (idIndex == 2 || idIndex == 4 || idIndex == 6)) isme = true;
                    if (idstr == "29454" && (idIndex == 0 || idIndex == 1)) isme = true;
                }
                else
                {
                    if (parse == 7.5)
                    {
                        if (idstr == "29452" && (idIndex == 0)) isme = true;
                        if (idstr == "29453" && (idIndex != 0 && idIndex != 1)) isme = true;
                        if (idstr == "29454" && (idIndex == 1)) isme = true;
                    }
                    if (parse == 7.8)
                    {
                        if (idstr == "29452" && (idIndex == 1)) isme = true;
                        if (idstr == "29453" && (idIndex != 0 && idIndex != 1)) isme = true;
                        if (idstr == "29454" && (idIndex == 0)) isme = true;
                    }
                }

                if (isme)
                {
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = "P7 死亡轮回剑分摊处";
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.Scale = new(1.5f);
                    dp.ScaleMode |= ScaleMode.YByDistance;
                    dp.Owner = accessory.Data.Me;
                    if (ParseObjectId(@event["SourceId"], out var sid))
                    {
                        dp.TargetObject = sid;
                    }
                    dp.DestoryAt = 6700;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                    dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = "P7 死亡轮回剑分摊范围";
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.Scale = new(4);
                    if (ParseObjectId(@event["SourceId"], out var sid2))
                    {
                        dp.Owner = sid2;
                    }
                    dp.DestoryAt = 12000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                }
            });
            
            
        }

        [ScriptMethod(name: "P7 一号核爆", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:28058"])]
        public void P7_一号核爆(Event @event, ScriptAccessory accessory)
        {
            

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P7 一号核爆";
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Scale = new(21f);
            if (ParseObjectId(@event["SourceId"], out var sid))
            {
                dp.Owner = sid;
            }
            dp.DestoryAt = 9000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        [ScriptMethod(name: "P7 二号核爆", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:28114"])]
        public void P7_二号核爆(Event @event, ScriptAccessory accessory)
        {


            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P7 二号核爆";
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Scale = new(21f);
            if (ParseObjectId(@event["SourceId"], out var sid))
            {
                dp.Owner = sid;
            }
            dp.Delay = 9000;
            dp.DestoryAt = 4000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        [ScriptMethod(name: "P7 三号核爆", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:28115"])]
        public void P7_三号核爆(Event @event, ScriptAccessory accessory)
        {


            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P7 二号核爆";
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Scale = new(21f);
            if (ParseObjectId(@event["SourceId"], out var sid))
            {
                dp.Owner = sid;
            }
            dp.Delay = 13000;
            dp.DestoryAt = 4000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        [ScriptMethod(name: "P7 一号核爆位置收集", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:28058"],userControl:false)]
        public void P7_一号核爆位置收集(Event @event, ScriptAccessory accessory)
        {
            p7Stone1 = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
        }
        [ScriptMethod(name: "P7 二号核爆位置收集", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:28114"], userControl: false)]
        public void P7_二号核爆位置收集(Event @event, ScriptAccessory accessory)
        {
            p7Stone2 = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
        }
        [ScriptMethod(name: "P7 核爆1跑2(Imgui)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:28114"])]
        public void P7_核爆1跑2(Event @event, ScriptAccessory accessory)
        {
            Task.Delay(50).ContinueWith(t =>
            {
                var cpos = new Vector3(100, 0, 100);
                var dot1 = Vector3.Normalize(cpos - p7Stone1);
                var pos1 = p7Stone1 + dot1 * 21f;
                var stone2pos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
                var dot2 = Vector3.Normalize(cpos - p7Stone2);
                var pos2 = stone2pos + dot2 * 21f;

                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = "P7 核爆跑1";
                dp.Color = accessory.Data.DefaultSafeColor;
                dp.Owner = accessory.Data.Me;
                dp.TargetPosition = pos1;
                dp.Scale = new(1.5f);
                dp.ScaleMode |= ScaleMode.YByDistance;
                dp.DestoryAt = 9000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                var dp2 = accessory.Data.GetDefaultDrawProperties();
                dp2.Name = "P7 核爆1跑2";
                dp2.Color = accessory.Data.DefaultSafeColor;
                dp2.Position = pos1;
                dp2.TargetPosition = pos2;
                dp2.Scale = new(1.5f);
                dp2.ScaleMode |= ScaleMode.YByDistance;
                dp2.DestoryAt = 9000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp2);

                var dp3 = accessory.Data.GetDefaultDrawProperties();
                dp3.Name = "P7 核爆跑2";
                dp3.Color = accessory.Data.DefaultSafeColor;
                dp3.Owner = accessory.Data.Me;
                dp3.TargetPosition = pos2;
                dp3.Scale = new(1.5f);
                dp3.ScaleMode |= ScaleMode.YByDistance;
                dp3.Delay = 9000;
                dp3.DestoryAt = 4000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp3);
            });
        }
        [ScriptMethod(name: "P7 核爆2跑3(Imgui)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:28115"])]
        public void P7_核爆2跑3(Event @event, ScriptAccessory accessory)
        {
            Task.Delay(50).ContinueWith(t =>
            {
                var cpos = new Vector3(100, 0, 100);
                var dot1 = Vector3.Normalize(cpos - p7Stone2);
                var pos1 = p7Stone2 + dot1 * 21f;
                var stone3pos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
                var dot2 = Vector3.Normalize(cpos - stone3pos);
                var pos2 = stone3pos + dot2 * 21f;

                

                var dp2 = accessory.Data.GetDefaultDrawProperties();
                dp2.Name = "P7 核爆2跑3";
                dp2.Color = accessory.Data.DefaultSafeColor;
                dp2.Position = pos1;
                dp2.TargetPosition = pos2;
                dp2.Scale = new(1.5f);
                dp2.ScaleMode |= ScaleMode.YByDistance;
                dp2.DestoryAt = 13000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp2);

                var dp3 = accessory.Data.GetDefaultDrawProperties();
                dp3.Name = "P7 核爆跑3";
                dp3.Color = accessory.Data.DefaultSafeColor;
                dp3.Owner = accessory.Data.Me;
                dp3.TargetPosition = pos2;
                dp3.Scale = new(1.5f);
                dp3.ScaleMode |= ScaleMode.YByDistance;
                dp3.Delay = 13000;
                dp3.DestoryAt = 4000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp3);
            });
        }

        #region 地火

        [ScriptMethod(name: "P7 BossId记录与地火初始化", eventType: EventTypeEnum.PlayActionTimeline, eventCondition: ["SourceDataId:12616"], userControl: false)]
        public void P7_BossId记录与地火初始化(Event @event, ScriptAccessory accessory)
        {
            var sid = @event.SourceId();
            _p7BossId = sid;
            List<int> scoreList = ExaflareStrategy switch
            {
                // moveStep,isFront,isUniverse
                ExaflareSpecStrategyEnum.绝不去前方_NeverFront => [2, 100, 50],
                ExaflareSpecStrategyEnum.绝不跑无脑火_NeverUniverse => [2, 10, 100],
                ExaflareSpecStrategyEnum.绝不多跑_LeastMovement => [20, 10, 50],
                ExaflareSpecStrategyEnum.绝对前方_AlwaysFront => [2, -10, 50],
                _ => [-10, 100, 0],
            };
            _p7Exaflare = new DsrExaflare(scoreList);
        }


        [ScriptMethod(name: "P7 钢铁月环剑记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2056", "Param:regex:^(29[89])$"], userControl: false)]
        public void P7_钢铁月环剑记录(Event @event, ScriptAccessory accessory)
        {
            var param = @event.Param();
            accessory.Log.Debug($"钢铁月环剑：{param}（298钢铁，299月环）");
            _p7Exaflare?.SetBladeType(param);
            if (!P7_是否地火阶段()) return;
            _bladeEvent.Set();
        }

        [ScriptMethod(name: "P7 地火范围绘制", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:28060"])]
        public void P7_地火范围绘制(Event @event, ScriptAccessory accessory)
        {
            // 面相为前、左、右的扩散
            var spos = @event.SourcePosition();
            var srot = @event.SourceRotation();
            var bossChara = accessory.GetById(_p7BossId);
            var bossRot = bossChara?.Rotation ?? float.Pi;
            var bossPos = bossChara?.Position ?? _center;
            const int intervalTime = 1900;
            const int castTime = 6900;
            const int extendDistance = 7;
            const int dirNum = 3;
            const int extNum = 6;
            const int advWarnNum = 1;   // 预警向外延伸几个
            float[] flareRot = [0, -float.Pi / 2, float.Pi / 2];

            Vector3[,] exaflarePos = P7_构建地火坐标矩阵(spos, dirNum, extNum, srot, flareRot, extendDistance);
            P7_绘制地火场景(exaflarePos, ExaflareWarnDrawn, advWarnNum, castTime, intervalTime, accessory);

            if (_p7Exaflare == null) return;
            lock (_p7Exaflare)
            {
                _p7Exaflare.SetBossPos(bossPos, accessory);
                _p7Exaflare.AddExaflare(spos, bossRot, srot, accessory);
            }
        }

        [ScriptMethod(name: "P7 地火特殊解法指路", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2056", "Param:regex:^(29[89])$"])]
        public void P7_地火特殊解法指路(Event @event, ScriptAccessory accessory)
        {
            // 记录完钢铁月环后可计算
            if (_p7Exaflare == null) return;
            if (!P7_是否地火阶段()) return;
            if (ExaflareStrategy == ExaflareSpecStrategyEnum.关闭_PleaseDontDoThat) return;
            if (!_p7Exaflare.ExaflareRecordComplete()) return;
            _bladeEvent.WaitOne();
            var guidePosList = _p7Exaflare.ExportExaflareSolution(accessory);
            accessory.Log.Debug($"你选择的策略是{ExaflareStrategy}");
            P7_绘制地火指路点(guidePosList, accessory);
            _bladeEvent.Reset();
        }

        private void P7_绘制地火指路点(List<Vector3> guidePosList, ScriptAccessory accessory)
        {
            const int intervalTime = 1900;
            const int castTime = 6900;
            const int baseTime = castTime - 900;    // 900ms为冰火剑附加到托尔丹身上的时间

            for (var i = 0; i < guidePosList.Count; i++)
            {
                var delay = i == 0 ? 0 : baseTime + (i - 1) * intervalTime;
                var destroy = i == 0 ? baseTime : intervalTime;

                var dp01 = accessory.DrawDirPos(guidePosList[i], delay, destroy, $"地火第{i}步-玩家-位置");
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp01);
                if (i >= guidePosList.Count - 1) continue;
                var dp12 = accessory.DrawDirPos2Pos(guidePosList[i], guidePosList[i + 1], delay, destroy, $"地火第{i}步-位置-位置");
                dp12.Color = accessory.Data.DefaultDangerColor;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp12);
            }
        }

        /// <summary>
        /// 画地火场景
        /// </summary>
        /// <param name="exaflarePos">地火矩阵</param>
        /// <param name="warnDrawn">是否画预警地火</param>
        /// <param name="advWarnNum">画多少格预警地火</param>
        /// <param name="castTime">初始地火技能施法时间</param>
        /// <param name="intervalTime">地火间隔时间</param>
        /// <param name="accessory"></param>
        private void P7_绘制地火场景(Vector3[,] exaflarePos, bool warnDrawn, int advWarnNum, int castTime, int intervalTime, ScriptAccessory accessory)
        {
            var dirNum = exaflarePos.GetLength(0);
            var extNum = exaflarePos.GetLength(1);

            for (var ext = 0; ext < extNum; ext++)
            {
                // 计算各位置的出现时间与延时时间。往往第一枚地火需要特殊处理，后续采用同时间隔
                var destroy = ext == 0 ? castTime : intervalTime;
                var delay= ext == 0 ? 0 : castTime + (ext - 1) * intervalTime;

                if (ext == 0)
                {
                    // 本体地火，对原地的地火(ext=0)，只画一个dir=0，不以任何角度向外延伸
                    P7_绘制地火(exaflarePos[0, ext], delay, destroy, accessory);
                    P7_绘制地火边缘(exaflarePos[0, ext], delay, destroy, accessory);
                }
                else
                {
                    // 对后续的地火(ext>0)，以对应角度向外延伸
                    for (var dir = 0; dir < dirNum; dir++)
                    {
                        P7_绘制地火(exaflarePos[dir, ext], delay, destroy, accessory);
                        P7_绘制地火边缘(exaflarePos[dir, ext], delay, destroy, accessory);
                    }
                }

                if (!warnDrawn) continue;
                for (var adv = 1; adv <= advWarnNum; adv++)
                {
                    if (ext >= extNum - adv) continue;
                    for (var dir = 0; dir < dirNum; dir++)
                        P7_绘制地火预警(exaflarePos[dir, ext + adv], adv, delay, destroy, intervalTime, accessory);
                }
            }
        }

        /// <summary>
        /// 构建地火坐标矩阵
        /// </summary>
        /// <param name="sourcePos">地火本体位置</param>
        /// <param name="dirNum">一枚地火涉及几个方向</param>
        /// <param name="extNum">一枚地火延伸几次</param>
        /// <param name="sourceRot">释放地火幻影旋转角度</param>
        /// <param name="flareRot">各方向旋转角度</param>
        /// <param name="extDistance">地火步进延伸距离</param>
        private Vector3[,] P7_构建地火坐标矩阵(Vector3 sourcePos, int dirNum, int extNum, float sourceRot, float[] flareRot, float extDistance)
        {
            Vector3[,] exaflarePos = new Vector3[dirNum, extNum];
            if (flareRot.Length != dirNum) return exaflarePos;
            for (var ext = 0; ext < extNum; ext++)
                for (var dir = 0; dir < dirNum; dir++)
                    exaflarePos[dir, ext] = sourcePos.ExtendPoint(sourceRot.Game2Logic() + flareRot[dir], ext * extDistance);
            return exaflarePos;
        }

        private void P7_绘制地火(Vector3 spos, int delay, int destroy, ScriptAccessory accessory)
        {
            const int scale = 6;
            var color = ExaflareBuiltInColor ? ColorHelper.ColorExaflare.V4 : ExaflareColor.V4.WithW(1f);
            var dp = accessory.DrawStaticCircle(spos, color, delay, destroy, $"地火{spos}", scale);
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        private void P7_绘制地火边缘(Vector3 spos, int delay, int destroy, ScriptAccessory accessory)
        {
            const float scale = 6;
            // const float innerScale = scale - 0.05f;
            var color = ExaflareBuiltInColor ? ColorHelper.ColorExaflare.V4 : ExaflareColor.V4.WithW(1.5f);
            var dp = accessory.DrawStaticDonut(spos, color, delay, destroy, $"地火边缘{spos}", scale);
            // dp.Color = ColorHelper.colorDark.V4;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Donut, dp);
        }

        private void P7_绘制地火预警(Vector3 spos, int adv, int delay, int destroy, int interval, ScriptAccessory accessory)
        {
            const int scale = 6;
            var destroyItv = interval * (adv - 1);
            var color = ExaflareBuiltInColor ? ColorHelper.ColorExaflareWarn.V4.WithW(1f / adv) : ExaflareWarnColor.V4.WithW(1f / adv);
            var dp = accessory.DrawStaticCircle(spos, color, delay, destroy + destroyItv, $"地火预警{spos}", scale);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        private void P7_地火模拟(float[] srot, float bossRotRad, uint bladeType, ScriptAccessory accessory)
        {
            accessory.Log.Debug($"你选择的策略是{ExaflareStrategy}");

            List<int> scoreList = ExaflareStrategy switch
            {
                // moveStep,isFront,isUniverse
                ExaflareSpecStrategyEnum.绝不去前方_NeverFront => [2, 100, 50],
                ExaflareSpecStrategyEnum.绝不跑无脑火_NeverUniverse => [2, 10, 100],
                ExaflareSpecStrategyEnum.绝不多跑_LeastMovement => [20, 10, 50],
                ExaflareSpecStrategyEnum.绝对前方_AlwaysFront => [2, -10, 50],
                _ => [-10, 100, 0],
            };
            _p7Exaflare = new DsrExaflare(scoreList);

            // 面相为前、左、右的扩散
            // var spos = @event.SourcePosition();
            // var srot = @event.SourceRotation();
            Vector3[] spos =
            [
                _center.ExtendPoint(bossRotRad.Game2Logic() - float.Pi, 8),
                _center.ExtendPoint(bossRotRad.Game2Logic() + 60f.DegToRad(), 8),
                _center.ExtendPoint(bossRotRad.Game2Logic() - 60f.DegToRad(), 8)
            ];
            var bossChara = accessory.GetById(_p7BossId);
            var bossRot = bossChara?.Rotation ?? bossRotRad;
            var bossPos = bossChara?.Position ?? _center;
            const int intervalTime = 1900;
            const int castTime = 6900;
            const int extendDistance = 7;
            const int dirNum = 3;
            const int extNum = 6;
            const int advWarnNum = 1;   // 预警向外延伸几个
            float[] flareRot = [0, -float.Pi / 2, float.Pi / 2];

            for (int i = 0; i < 3; i++)
            {
                Vector3[,] exaflarePos = P7_构建地火坐标矩阵(spos[i], dirNum, extNum, srot[i], flareRot, extendDistance);
                // 画地火箭头
                var dp1 = accessory.DrawDirPos2Pos(spos[i], spos[i].ExtendPoint(srot[i].Game2Logic() + flareRot[0], 6), 0, castTime, $"箭头1", 5.9f);
                var dp2 = accessory.DrawDirPos2Pos(spos[i], spos[i].ExtendPoint(srot[i].Game2Logic() + flareRot[1], 6), 0, castTime, $"箭头2", 5.9f);
                var dp3 = accessory.DrawDirPos2Pos(spos[i], spos[i].ExtendPoint(srot[i].Game2Logic() + flareRot[2], 6), 0, castTime, $"箭头3", 5.9f);
                dp1.Color = ColorHelper.ColorRed.V4;
                dp2.Color = ColorHelper.ColorRed.V4;
                dp3.Color = ColorHelper.ColorRed.V4;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp1);
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp2);
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp3);

                P7_绘制地火场景(exaflarePos, ExaflareWarnDrawn, advWarnNum, castTime, intervalTime, accessory);
                if (_p7Exaflare == null) return;
                lock (_p7Exaflare)
                {
                    _p7Exaflare.SetBossPos(bossPos, accessory);
                    _p7Exaflare.AddExaflare(spos[i], bossRot, srot[i], accessory);
                }
            }
            _p7Exaflare.SetBladeType(bladeType);
            switch (bladeType)
            {
                case ChariotBlade:
                    var dp1 = accessory.DrawStaticCircle(_center, accessory.Data.DefaultDangerColor.WithW(2f), 0, castTime, $"钢铁", 8f);
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp1);
                    break;
                case ChariotBlade + 1:
                    var dp2 = accessory.DrawStaticDonut(_center, accessory.Data.DefaultDangerColor.WithW(2f), 0, castTime, $"月环", 50f, 8f);
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp2);
                    break;
            }

            // 记录完钢铁月环后可计算
            if (_p7Exaflare == null) return;
            // if (!P7_是否地火阶段()) return;
            if (ExaflareStrategy == ExaflareSpecStrategyEnum.关闭_PleaseDontDoThat) return;
            if (!_p7Exaflare.ExaflareRecordComplete()) return;
            var guidePosList = _p7Exaflare.ExportExaflareSolution(accessory);
            P7_绘制地火指路点(guidePosList, accessory);
        }

        [ScriptMethod(name: "P7 忆罪宫地火模拟器", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:=Exaflare"], userControl: false)]
        public void P7_忆罪宫地火模拟器(Event @event, ScriptAccessory accessory)
        {
            // ---- DEBUG CODE ----

            _center = new Vector3(400, -54.97f, -400);
            Random random = new Random();
            float bossRotLogicDeg = random.Next(0, 360);
            var bossRotLogicRad = bossRotLogicDeg.DegToRad();
            accessory.Log.Debug($"随机到的Boss面向为{bossRotLogicRad.RadToDeg()}");
            float[] srot =
            [
                (random.Next(0, 8) * float.Pi / 4 + bossRotLogicRad).Logic2Game(),
                (random.Next(0, 8) * float.Pi / 4 + bossRotLogicRad).Logic2Game(),
                (random.Next(0, 8) * float.Pi / 4 + bossRotLogicRad).Logic2Game()
            ];
            Vector3 bossFace = _center.ExtendPoint(bossRotLogicRad, 8f);
            var dp = accessory.DrawDirPos2Pos(_center, bossFace, 0, 7000, $"面相", 7.9f);
            dp.Color = ColorHelper.ColorDark.V4;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            P7_地火模拟(srot, bossRotLogicRad.Logic2Game(), (uint)random.Next(0, 2) + ChariotBlade, accessory);
            // -- DEBUG CODE END --
        }

        #endregion 地火

        #region 接刀

        [ScriptMethod(name: "P7 阶段记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(2805[179]|28206)$"], userControl: false)]
        public void P7_阶段记录(Event @event, ScriptAccessory accessory)
        {
            _dsrPhase = _dsrPhase switch
            {
                DsrPhase.Phase6Cauterize => DsrPhase.Phase7Exaflare1,
                DsrPhase.Phase7Exaflare1 => DsrPhase.Phase7Stack1,
                DsrPhase.Phase7Stack1 => DsrPhase.Phase7Nuclear1,
                DsrPhase.Phase7Nuclear1 => DsrPhase.Phase7Exaflare2,
                DsrPhase.Phase7Exaflare2 => DsrPhase.Phase7Stack2,
                DsrPhase.Phase7Stack2 => DsrPhase.Phase7Nuclear2,
                DsrPhase.Phase7Nuclear2 => DsrPhase.Phase7Exaflare3,
                DsrPhase.Phase7Exaflare3 => DsrPhase.Phase7Stack3,
                DsrPhase.Phase7Stack3 => DsrPhase.Phase7Enrage,
                _ => DsrPhase.Phase7Exaflare1,
            };
            accessory.Log.Debug($"当前阶段为：{_dsrPhase}");

            if (!_p7FirstEnmityOrder.Contains(true))
            {
                // 初始化
                _p7FirstEnmityOrder = [true, false];
                _p7TrinityDisordered = false;
                _p7TrinityTankDisordered = false;
                _p7TrinityNum = 0;
            }
            else
            {
                _p7FirstEnmityOrder[0] = !_p7FirstEnmityOrder[0];
                _p7FirstEnmityOrder[1] = !_p7FirstEnmityOrder[1];
                accessory.Log.Debug($"MT为{(_p7FirstEnmityOrder[0] ? "一仇" : "二仇")}，ST为{(_p7FirstEnmityOrder[1] ? "一仇" : "二仇")}");
            }
            _trinityEvent.Set();

            if (!P7_是否分摊阶段()) return;
            List<int> scoreList = ExaflareStrategy switch
            {
                // moveStep,isFront,isUniverse
                ExaflareSpecStrategyEnum.绝不去前方_NeverFront => [2, 100, 50],
                ExaflareSpecStrategyEnum.绝不跑无脑火_NeverUniverse => [2, 10, 100],
                ExaflareSpecStrategyEnum.绝不多跑_LeastMovement => [20, 10, 50],
                ExaflareSpecStrategyEnum.绝对前方_AlwaysFront => [2, -10, 50],
                _ => [-10, 100, 0],
            };
            _p7Exaflare = new DsrExaflare(scoreList);

        }

        private bool P7_是否地火阶段()
        {
            return _dsrPhase is DsrPhase.Phase7Exaflare1 or DsrPhase.Phase7Exaflare2 or DsrPhase.Phase7Exaflare3;
        }

        private bool P7_是否分摊阶段()
        {
            return _dsrPhase is DsrPhase.Phase7Stack1 or DsrPhase.Phase7Stack2 or DsrPhase.Phase7Stack3;
        }

        [ScriptMethod(name: "P7 三剑一体接刀", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(2805[179])$"])]
        public void P7_三剑一体接刀(Event @event, ScriptAccessory accessory)
        {
            _trinityEvent.WaitOne();
            var aid = @event.ActionId();
            var sid = @event.SourceId();
            const uint exaflare = 28059;
            const uint stack = 28051;
            const uint nuclear = 28057;

            var delay = aid switch
            {
                exaflare => 15200,
                stack => 18500,
                nuclear => 27200,
                _ => 0
            };

            delay = _dsrPhase switch
            {
                DsrPhase.Phase7Stack1 => delay,
                DsrPhase.Phase7Stack2 => delay + 1100,
                DsrPhase.Phase7Stack3 => delay + 2200,
                _ => delay
            };

            P7_绘制三剑一体仇恨(sid, delay - 4000, 4000, 1, accessory);
            P7_绘制三剑一体仇恨(sid, delay - 4000, 4000, 2, accessory);
            P7_绘制三剑一体仇恨(sid, delay, 4000, 1, accessory);
            P7_绘制三剑一体仇恨(sid, delay, 4000, 2, accessory);
            P7_绘制三剑一体近距(sid, delay - 4000, 4000, accessory);
            P7_绘制三剑一体近距(sid, delay, 4000, accessory);
            _trinityEvent.Reset();
        }

        private void P7_绘制三剑一体仇恨(uint sid, int delay, int destroy, uint aggroIdx, ScriptAccessory accessory)
        {
            var myIndex = accessory.GetMyIndex();
            Vector4 color;

            if (myIndex > 1 || _p7TrinityTankDisordered)
                color = accessory.Data.DefaultDangerColor;
            else
            {
                switch (_p7FirstEnmityOrder[myIndex])
                {
                    case true when aggroIdx == 1:
                    case false when aggroIdx == 2:
                        color = accessory.Data.DefaultSafeColor;
                        break;
                    default:
                        color = accessory.Data.DefaultDangerColor;
                        break;
                }
            }

            var dp = accessory.DrawOwnersEnmityOrder(sid, aggroIdx, 3f, 3f, delay, destroy, $"三剑一体仇恨{aggroIdx}", byTime: true);
            dp.Color = color.WithW(2f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        private void P7_绘制三剑一体近距(uint sid, int delay, int destroy, ScriptAccessory accessory)
        {
            var myIndex = accessory.GetMyIndex();

            var dp = accessory.DrawTargetNearFarOrder(sid, 1, true, 3f, 3f, delay, destroy, $"三剑一体近距", byTime: true);
            if (_p7TrinityDisordered)
                dp.Color = accessory.Data.DefaultDangerColor;
            else
                dp.Color = myIndex == _p7TrinityOrderIdx[_p7TrinityNum] ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        [ScriptMethod(name: "P7 三剑一体接刀记录", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:28065"], userControl: false)]
        public void P7_三剑一体接刀记录(Event @event, ScriptAccessory accessory)
        {
            // 主视角为T，忽略脚下接刀
            var myIndex = accessory.GetMyIndex();
            if (myIndex < 2) return;

            var targetIdx = @event.TargetIndex();
            if (targetIdx != 1)
            {
                if (_p7TrinityDisordered) return;
                accessory.Log.Debug($"有人多接了一刀，失效");
                accessory.Method.TextInfo($"有人多接了一刀，不再以安全色提示", 3000, true);
                _p7TrinityDisordered = true;
                return;
            }

            var tid = @event.TargetId();
            var tidx = accessory.GetPlayerIdIndex(tid);
            if (_p7TrinityOrderIdx[_p7TrinityNum] != tidx && !_p7TrinityDisordered)
            {
                accessory.Log.Debug($"接刀人错误，失效");
                accessory.Method.TextInfo($"接刀人错误，不再以安全色提示", 3000, true);
                _p7TrinityDisordered = true;
            }

            _p7TrinityNum++;
            if (_p7TrinityNum >= 6)
                _p7TrinityNum = 0;

            var targetRecent = accessory.GetPlayerJobByIndex(tidx);
            var targetNext = accessory.GetPlayerJobByIndex(_p7TrinityOrderIdx[_p7TrinityNum]);
            accessory.Log.Debug($"刚刚接刀的是{targetRecent}，下一个接刀人为{targetNext}");
        }

        [ScriptMethod(name: "P7 三剑一体T刀记录", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(2806[34])$"], userControl: false)]
        public void P7_三剑一体T刀记录(Event @event, ScriptAccessory accessory)
        {
            var aid = @event.ActionId();
            var tid = @event.TargetId();

            // 非T玩家接到刀
            var tidx = accessory.GetPlayerIdIndex(tid);
            if (tidx > 1) return;

            // 主视角不是T
            var myIndex = accessory.GetMyIndex();
            if (myIndex > 1) return;

            // 已经失效
            if (_p7TrinityTankDisordered) return;

            const uint aggro1 = 28063;
            const uint aggro2 = 28064;

            // 一仇效果，但目标是二仇 || 二仇效果，但目标是一仇
            if ((_p7FirstEnmityOrder[tidx] || aid != aggro1) && (!_p7FirstEnmityOrder[tidx] || aid != aggro2)) return;
            accessory.Log.Debug($"接刀仇恨错误，失效");
            accessory.Method.TextInfo($"接刀仇恨错误，不再以安全色提示", 3000, true);
            _p7TrinityTankDisordered = true;
        }

        #endregion 接刀

        #endregion


        [ScriptMethod(name: "TargetIconPrint", eventType: EventTypeEnum.TargetIcon)]
        public void TestTargetIcon(Event @event, ScriptAccessory accessory)
        {
            accessory.Log.Debug($"TargetIcon: {@event["TargetId"]} {ParsTargetIcon(@event["Id"])}"); 
        }

        private static bool ParseObjectId(string? idStr, out uint id)
        {
            id = 0;
            if (string.IsNullOrEmpty(idStr)) return false;
            try
            {
                var idStr2 = idStr.Replace("0x", "");
                id = uint.Parse(idStr2, System.Globalization.NumberStyles.HexNumber);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        private int ParsTargetIcon(string id)
        {
            firstTargetIcon??= int.Parse(id, System.Globalization.NumberStyles.HexNumber);
            return int.Parse(id, System.Globalization.NumberStyles.HexNumber) - (int)firstTargetIcon;
        }
        private Vector3 RotatePoint(Vector3 point, Vector3 centre, float radian)
        {

            Vector2 v2 = new(point.X - centre.X, point.Z - centre.Z);

            var rot = (MathF.PI - MathF.Atan2(v2.X, v2.Y) + radian);
            var lenth = v2.Length();
            return new(centre.X + MathF.Sin(rot) * lenth, centre.Y, centre.Z - MathF.Cos(rot) * lenth);
        }
        /// <summary>
        /// 向下取
        /// </summary>
        /// <param name="point"></param>
        /// <param name="centre"></param>
        /// <returns></returns>
        private int PositionFloorTo4Dir(Vector3 point, Vector3 centre)
        {
            // Dirs: N = 0, NE = 1, ..., NW = 7
            var r = Math.Floor(2 - 2 * Math.Atan2(point.X - centre.X, point.Z - centre.Z) / Math.PI) % 4;
            return (int)r;

        }

        /// <summary>
        /// 向近的取
        /// </summary>
        /// <param name="point"></param>
        /// <param name="centre"></param>
        /// <returns></returns>
        private int PositionRoundTo4Dir(Vector3 point, Vector3 centre)
        {

            var r = Math.Round(2 - 2 * Math.Atan2(point.X - centre.X, point.Z - centre.Z) / Math.PI) % 4;
            return (int)r;
        }

        /// <summary>
        /// 向近的取
        /// </summary>
        /// <param name="point"></param>
        /// <param name="centre"></param>
        /// <returns></returns>
        private int PositionTo8Dir(Vector3 point, Vector3 centre)
        {
            // Dirs: N = 0, NE = 1, ..., NW = 7
            var r = Math.Round(4 - 4 * Math.Atan2(point.X - centre.X, point.Z - centre.Z) / Math.PI) % 8;
            return (int)r;

        }
        private int PositionTo12Dir(Vector3 point, Vector3 centre)
        {
            // Dirs: N = 0, NE = 1, ..., NW = 7
            var r = Math.Round(6 - 6 * Math.Atan2(point.X - centre.X, point.Z - centre.Z) / Math.PI) % 12;
            return (int)r;

        }

        public class PriorityDict
        {
            // ReSharper disable once NullableWarningSuppressionIsUsed
            public ScriptAccessory sa {get; set;} = null!;
            // ReSharper disable once NullableWarningSuppressionIsUsed
            public Dictionary<int, int> Priorities {get; set;} = null!;
            public string Annotation { get; set; } = "";
            public int ActionCount { get; set; } = 0;

            public void Init(ScriptAccessory accessory, string annotation, int partyNum = 8)
            {
                sa = accessory;
                Priorities = new Dictionary<int, int>();
                ActionCount = 0;
                for (var i = 0; i < partyNum; i++)
                {
                    Priorities.Add(i, 0);
                }
                Annotation = annotation;
            }

            /// <summary>
            /// 为特定Key增加优先级
            /// </summary>
            /// <param name="idx">key</param>
            /// <param name="priority">优先级数值</param>
            public void AddPriority(int idx, int priority)
            {
                Priorities[idx] += priority;
            }

            /// <summary>
            /// 从Priorities中找到前num个数值最小的，得到新的Dict返回
            /// </summary>
            /// <param name="num"></param>
            /// <returns></returns>
            public List<KeyValuePair<int, int>> SelectSmallPriorityIndices(int num)
            {
                return SelectMiddlePriorityIndices(0, num);
            }

            /// <summary>
            /// 从Priorities中找到前num个数值最大的，得到新的Dict返回
            /// </summary>
            /// <param name="num"></param>
            /// <returns></returns>
            public List<KeyValuePair<int, int>> SelectLargePriorityIndices(int num)
            {
                return SelectMiddlePriorityIndices(0, num, true);
            }

            /// <summary>
            /// 从Priorities中找到升序排列中间的数值，得到新的Dict返回
            /// </summary>
            /// <param name="skip">跳过skip个元素。若从第二个开始取，skip=1</param>
            /// <param name="num"></param>
            /// <param name="descending">降序排列，默认为false</param>
            /// <returns></returns>
            public List<KeyValuePair<int, int>> SelectMiddlePriorityIndices(int skip, int num, bool descending = false)
            {
                if (Priorities.Count < skip + num)
                    return new List<KeyValuePair<int, int>>();

                IEnumerable<KeyValuePair<int, int>> sortedPriorities;
                if (descending)
                {
                    // 根据值从大到小降序排序，并取前num个键
                    sortedPriorities = Priorities
                        .OrderByDescending(pair => pair.Value) // 先根据值排列
                        .ThenBy(pair => pair.Key) // 再根据键排列
                        .Skip(skip) // 跳过前skip个元素
                        .Take(num); // 取前num个键值对
                }
                else
                {
                    // 根据值从小到大升序排序，并取前num个键
                    sortedPriorities = Priorities
                        .OrderBy(pair => pair.Value) // 先根据值排列
                        .ThenBy(pair => pair.Key) // 再根据键排列
                        .Skip(skip) // 跳过前skip个元素
                        .Take(num); // 取前num个键值对
                }

                return sortedPriorities.ToList();
            }

            /// <summary>
            /// 从Priorities中找到升序排列第idx位的数据，得到新的Dict返回
            /// </summary>
            /// <param name="idx"></param>
            /// <param name="descending">降序排列，默认为false</param>
            /// <returns></returns>
            public KeyValuePair<int, int> SelectSpecificPriorityIndex(int idx, bool descending = false)
            {
                var sortedPriorities = SelectMiddlePriorityIndices(0, 8, descending);
                return sortedPriorities[idx];
            }

            /// <summary>
            /// 从Priorities中找到对应key的数据，得到其Value排序后位置返回
            /// </summary>
            /// <param name="key"></param>
            /// <param name="descending">降序排列，默认为false</param>
            /// <returns></returns>
            public int FindPriorityIndexOfKey(int key, bool descending = false)
            {
                var sortedPriorities = SelectMiddlePriorityIndices(0, 8, descending);
                var i = 0;
                foreach (var dict in sortedPriorities)
                {
                    if (dict.Key == key) return i;
                    i++;
                }

                return i;
            }

            /// <summary>
            /// 一次性增加优先级数值
            /// 通常适用于特殊优先级（如H-T-D-H）
            /// </summary>
            /// <param name="priorities"></param>
            public void AddPriorities(List<int> priorities)
            {
                if (Priorities.Count != priorities.Count)
                    throw new ArgumentException("输入的列表与内部设置长度不同");

                for (var i = 0; i < Priorities.Count; i++)
                    AddPriority(i, priorities[i]);
            }

            /// <summary>
            /// 输出优先级字典的Key与优先级
            /// </summary>
            /// <returns></returns>
            public string ShowPriorities(bool showJob = true)
            {
                var str = $"{Annotation} ({ActionCount}-th) 优先级字典：\n";
                if (Priorities.Count == 0)
                {
                    str += $"PriorityDict Empty.\n";
                    return str;
                }
                foreach (var pair in Priorities)
                {
                    str += $"Key {pair.Key} {(showJob ? $"({_role[pair.Key]})" : "")}, Value {pair.Value}\n";
                }

                return str;
            }

            public PriorityDict DeepCopy()
            {
                return JsonConvert.DeserializeObject<PriorityDict>(JsonConvert.SerializeObject(this)) ?? new PriorityDict();
            }

            public void AddActionCount(int count = 1)
            {
                ActionCount += count;
            }
        }

    }

    #region Class 地火

    public class DsrExaflare(List<int> scoreList)
    {
        // 右上0，下1，左2
        private List<Vector3> ExaflarePosList { get; set; } = Enumerable.Repeat(new Vector3(0, 0, 0), 3).ToList();
        private Vector3 BossPos { get; set; } = new Vector3(0, 0, 0);
        private List<int> ExaflareDirList { get; set; } = [0, 0, 0];
        private uint BladeType { get; set; } = 0;
        private List<ExaflareSolution> ExaflareSolutionList { get; set; } = [];
        public int RecordedExaflareNum = 0;

        private ExaflareSolution BuildOneStepSolutionNew(ScriptAccessory accessory)
        {
            // 一步火
            const bool isUniverse = false;
            var moveStep = 0;
            Vector3 pos2;
            Vector3 pos3;
            int targetExaflareIdx;
            var debugText = $"[a][一步火]: \n";

            if (!IsFrontPointedByExaflare(0))
                targetExaflareIdx = 0;
            else if (!IsFrontPointedByExaflare(2))
                targetExaflareIdx = 2;
            else
            {
                targetExaflareIdx = 0;
                moveStep++;
            }

            pos2 = ExaflarePosList[targetExaflareIdx];

            if (moveStep == 0)
            {
                debugText += $"[a]检测到{GetExaflareIdxStr(targetExaflareIdx)}地火未被指向，可作为安全点\n";
                pos3 = pos2;
            }
            else
            {
                debugText += $"[a]检测到前方地火均被指向，走前方两步火，随便取左上作安全点\n";
                pos3 = ExaflarePosList[1].PointInOutside(BossPos, 12f);
            }

            // pos1 根据职能定义起跑点
            var myIndex = accessory.GetMyIndex();
            var pos1 = FindFirstSafePosAtFront(targetExaflareIdx, myIndex < 1);
            debugText += $"[a]玩家序号为{myIndex}, 为{(myIndex < 1?"坦克":"人群")}视角，\n倾向于{(myIndex < 1?"前方":"后方")}就位\n";
            moveStep++;

            accessory.Log.Debug(debugText);

            return new ExaflareSolution([pos1, pos2, pos3], moveStep, true, isUniverse, "一步火", scoreList,
                accessory);
        }

        private ExaflareSolution BuildTwoStepSolution(ScriptAccessory accessory)
        {
            // 两步火
            var backExaflarePos = ExaflarePosList[1];
            var isUniverse = false;
            var moveStep = 0;
            // pos1 读条时，找背后地火的钢铁月环安全区
            var pos1 = FindFirstSafePos(1, true);
            moveStep++;
            // pos2 一炸后，找背后地火位置
            var pos2 = backExaflarePos;
            // pos3 二炸后，观察前面两枚
            Vector3 pos3;
            var debugText = $"[b][两步火]: \n";

            // 前方两地火是否指向背后
            var idx0Point = IsBackPointedByExaflare(0);
            var idx2Point = IsBackPointedByExaflare(2);

            if (!idx0Point && !idx2Point)
            {
                // 都未指向背后，原地
                pos3 = backExaflarePos;
                debugText += $"[b]检测到前方地火前方地火都未指向背后，转为背后一步火\n";
            }
            else if (!idx0Point && idx2Point)
            {
                // 右上未指向背后，去左侧
                pos3 = backExaflarePos.RotatePoint(BossPos, 45f.DegToRad());
                moveStep++;
                debugText += $"[b]检测到右上地火未指向背后，去左后\n";
            }
            else if (idx0Point && !idx2Point)
            {
                // 左上未指向背后，去右侧
                pos3 = backExaflarePos.RotatePoint(BossPos, -45f.DegToRad());
                moveStep++;
                debugText += $"[b]检测到左上地火未指向背后，去右侧\n";
            }
            else
            {
                // 全部指向背后，无脑火
                pos3 = FindUniversalSafePos();
                isUniverse = true;
                moveStep++;
                debugText += $"[b]检测到地火全指向背后，转为无脑火\n";
            }
            accessory.Log.Debug(debugText);
            return new ExaflareSolution([pos1, pos2, pos3], moveStep, false, isUniverse, "两步火", scoreList,
                accessory);
        }

        /// <summary>
        /// 以某一枚地火开始，顺时针或逆时针处就位
        /// </summary>
        /// <param name="exaflareIdx">某一枚地火</param>
        /// <param name="isCw">顺时针找</param>
        /// <returns></returns>
        private Vector3 FindFirstSafePos(int exaflareIdx, bool isCw)
        {
            var exaflarePos = ExaflarePosList[exaflareIdx];
            var rad = exaflarePos.FindRadian(BossPos) + (isCw ? 50f.DegToRad() : -50f.DegToRad());
            var firstSafePos = BossPos.ExtendPoint(rad, IsChariot() ? 8.5f : 7.5f);
            return firstSafePos;
        }

        private Vector3 FindFirstSafePosAtFront(int exaflareIdx, bool isTank)
        {
            // var exaflarePos = ExaflarePosList[exaflareIdx];
            if (isTank) // 是坦克，则前方起跑
            {
                if (exaflareIdx == 0)
                    return FindFirstSafePos(exaflareIdx, false);
                if (exaflareIdx == 2)
                    return FindFirstSafePos(exaflareIdx, true);
            }
            else
            {
                if (exaflareIdx == 0)
                    return FindFirstSafePos(exaflareIdx, true);
                if (exaflareIdx == 2)
                    return FindFirstSafePos(exaflareIdx, false);
            }
            return new Vector3(0, 0, 0);
        }

        private Vector3 FindUniversalSafePos()
        {
            return ExaflarePosList[1].PointInOutside(BossPos, 13.2f - 8f, true);
        }

        public void SetBossPos(Vector3 bossPosV3, ScriptAccessory accessory)
        {
            BossPos = bossPosV3;
            // accessory.DebugMsg($"设置Boss位置{BossPos}", debugMode);
        }

        /// <summary>
        /// 增加地火属性
        /// </summary>
        /// <param name="exaflarePosV3">地火位置</param>
        /// <param name="bossRotation">Boss旋转角度</param>
        /// <param name="exaflareRot">地火旋转角度</param>
        /// <param name="accessory"></param>
        public void AddExaflare(Vector3 exaflarePosV3, float bossRotation, float exaflareRot, ScriptAccessory accessory)
        {
            var idx = FindExaflareIdx(exaflarePosV3, bossRotation);
            // 差值无需互转
            var exaflareRelativeDir = exaflareRot.Game2Logic() - bossRotation.Game2Logic();
            var dir = exaflareRelativeDir.Rad2Dirs(8);
            ExaflareDirList[idx] = dir;
            ExaflarePosList[idx] = exaflarePosV3;
            accessory.Log.Debug($"添加{GetExaflareIdxStr(idx)}地火，坐标{exaflarePosV3}，面向{GetDirStr(dir)}");
            RecordedExaflareNum++;
        }

        /// <summary>
        /// 根据地火中心位置找到对应地火本体方位的idx
        /// 因为地火位置会根据Boss面向改变，所以要减去boss旋转的偏置量
        /// </summary>
        /// <param name="exaflarePosV3">地火中心位置</param>
        /// <param name="bossRotation">Boss面向</param>
        /// <returns></returns>
        private int FindExaflareIdx(Vector3 exaflarePosV3, float bossRotation)
        {
            var exaflareBaseDir = exaflarePosV3.FindRadian(BossPos);
            var exaflareRelativeDir = exaflareBaseDir - bossRotation.Game2Logic();
            var idx = exaflareRelativeDir.Rad2Dirs(3, false);
            return idx;
        }

        /// <summary>
        /// 返回该枚地火是否为正角，当八方方位为偶数时是正角
        /// </summary>
        /// <param name="idx">某枚地火</param>
        /// <returns></returns>
        private bool IsExaflareRightDir(int idx)
        {
            return ExaflareDirList[idx] % 2 == 0;
        }

        /// <summary>
        /// 找到背后是否被序号为idx的地火指
        /// </summary>
        /// <param name="idx">地火序号</param>
        /// <returns></returns>
        private bool IsBackPointedByExaflare(int idx)
        {
            // 右上地火指向背后地火的条件：右上地火不是正火且方向不等于1
            // 左上地火指向背后地火的条件：左上地火不是正火且方向不等于7
            var result = idx switch
            {
                0 => !IsExaflareRightDir(idx) && ExaflareDirList[idx] != 1,
                2 => !IsExaflareRightDir(idx) && ExaflareDirList[idx] != 7,
                _ => false
            };
            return result;
        }

        /// <summary>
        /// 找到前方序号为idx的地火是否被指
        /// </summary>
        /// <param name="idx">地火序号</param>
        /// <returns></returns>
        private bool IsFrontPointedByExaflare(int idx)
        {
            // 右上地火被指：左上地火为正火，且方向不为6（朝左） 或 背后地火是斜火，且方向不为5（朝左下）
            // 左上地火被指：右上地火为正火，且方向不为2（朝右） 或 背后地火是斜火，且方向不为3（朝右下）
            var result = idx switch
            {
                0 => (IsExaflareRightDir(2) && ExaflareDirList[2] != 6) ||
                     (!IsExaflareRightDir(1) && ExaflareDirList[1] != 5),
                2 => (IsExaflareRightDir(0) && ExaflareDirList[0] != 2) ||
                     (!IsExaflareRightDir(1) && ExaflareDirList[1] != 3),
                _ => false
            };
            return result;
        }

        public void SetBladeType(uint type)
        {
            BladeType = type;
        }

        private bool IsChariot()
        {
            const uint chariotFireBlade = 298;
            return BladeType == chariotFireBlade;
        }

        private void AddExaflareSolution(ExaflareSolution solution)
        {
            ExaflareSolutionList.Add(solution);
        }

        public List<Vector3> ExportExaflareSolution(ScriptAccessory accessory)
        {
            AddExaflareSolution(BuildOneStepSolutionNew(accessory));
            AddExaflareSolution(BuildTwoStepSolution(accessory));

            ExaflareSolutionList = ExaflareSolutionList.OrderBy(solution => solution.Score).ToList();
            accessory.Log.Debug($"两解法对比，优先级高的是{ExaflareSolutionList[0].Description}，为{ExaflareSolutionList[0].Score}分");
            return ExaflareSolutionList[0].ExaflareSolutionPosList;
        }

        /*
         * 下述为构建地火的方法，今后可以单独做成一个class调用
         */

        // /// <summary>
        // /// 构建地火坐标
        // /// </summary>
        // /// <param name="center">中心</param>
        // /// <param name="rotation">旋转角度</param>
        // /// <param name="extendDistance">延伸距离</param>
        // /// <returns></returns>
        // private Vector3 GetExaflarePos(Vector3 center, float rotation, float extendDistance)
        // {
        //     return center.ExtendPoint(rotation, extendDistance);
        // }

        // private Vector3[] BuildExaflareVector(Vector3 center, float rotation, int extendNum, float extendDistance)
        // {
        //     var exaflarePos = new Vector3[extendNum];
        //     for (var i = 0; i < extendNum; i++)
        //         exaflarePos[i] = GetExaflarePos(center, rotation, (i + 1) * extendDistance);
        //     return exaflarePos;
        // }

        public bool ExaflareRecordComplete()
        {
            return RecordedExaflareNum == 3;
        }

        private string GetExaflareIdxStr(int idx)
        {
            return idx switch
            {
                0 => "右上",
                1 => "背后",
                2 => "左上",
                _ => "未知"
            };
        }

        private string GetDirStr(int idx)
        {
            return idx switch
            {
                0 => "正上",
                1 => "右上",
                2 => "正右",
                3 => "右下",
                4 => "正下",
                5 => "左下",
                6 => "正左",
                7 => "左上",
                _ => "未知"
            };
        }

        public class ExaflareSolution
        {
            /*
             * 地火优选策略
             * 地火共有四种解法选项：
             * 1、绝不去前方 NeverFront
             *      背后两步火>无脑火>>>前方一步火>前方两步火
             * 2、绝不跑无脑火 NeverUniverse
             *      背后两步火>前方一步火>前方两步火>>>无脑火
             * 3、绝不多跑 LeastMovement
             *      前方一步火>背后两步火>前方两步火>>>无脑火
             * 4、绝对前方 AlwaysFront
             *      前方一步火>前方两步火>背后两步火>无脑火
             * 四种解法被求解后，分数低者取胜。
             *
             * 解法情况与影响分值的对应关系
             *                  basic   moveStep    isFront     isUniverse
             * NeverFront        100        2         100           50
             * NeverUniverse     100        2          10          100
             * LeastMovement     100       20          10           50
             * AlwaysFront       100        2         -10           50
             */
            public List<Vector3> ExaflareSolutionPosList { get; set; }
            public int MoveStep { get; set; }
            public bool IsFront { get; set; }
            public bool IsUniverse { get; set; }
            public int Score { get; set; }
            public string Description { get; set; }

            public ExaflareSolution(List<Vector3> exaflareSolutionPosList, int moveStep, bool isFront, bool isUniverse,
                string description, List<int> scoreList, ScriptAccessory accessory)
            {
                ExaflareSolutionPosList = exaflareSolutionPosList;
                MoveStep = moveStep;
                IsFront = isFront;
                IsUniverse = isUniverse;
                Score = CalcScore(scoreList, accessory, description);
                Description = description;
            }
            private int CalcScore(List<int> scoreList, ScriptAccessory accessory, string description)
            {
                const int moveStepIdx = 0;
                const int isFrontIdx = 1;
                const int isUniverseIdx = 2;
                const int baseScore = 100;
                var moveStepScore = scoreList[moveStepIdx] * MoveStep;
                var isFrontScore = IsFront ? scoreList[isFrontIdx] : 0;
                var isUniverseScore = IsUniverse ? scoreList[isUniverseIdx] : 0;
                var totalScore = baseScore + moveStepScore + isFrontScore + isUniverseScore;
                accessory.Log.Debug(
                    $"{description}的得分为：基础{baseScore} + 步数{moveStepScore} + 前方{isFrontScore} + 无脑{isUniverseScore} = {totalScore}");
                return totalScore;
            }
        }
    }

    #endregion


    #region 函数集
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
            catch (Exception)
            {
                return false;
            }
        }

        public static uint ActionId(this Event @event)
        {
            return JsonConvert.DeserializeObject<uint>(@event["ActionId"]);
        }

        public static uint SourceId(this Event @event)
        {
            return ParseHexId(@event["SourceId"], out var id) ? id : 0;
        }

        public static uint TargetId(this Event @event)
        {
            return ParseHexId(@event["TargetId"], out var id) ? id : 0;
        }

        public static uint TargetIndex(this Event @event)
        {
            return JsonConvert.DeserializeObject<uint>(@event["TargetIndex"]);
        }

        public static Vector3 SourcePosition(this Event @event)
        {
            return JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
        }

        public static Vector3 TargetPosition(this Event @event)
        {
            return JsonConvert.DeserializeObject<Vector3>(@event["TargetPosition"]);
        }

        public static float SourceRotation(this Event @event)
        {
            return JsonConvert.DeserializeObject<float>(@event["SourceRotation"]);
        }

        public static string SourceName(this Event @event)
        {
            return @event["SourceName"];
        }

        public static uint Id(this Event @event)
        {
            return ParseHexId(@event["Id"], out var id) ? id : 0;
        }

        public static uint StatusId(this Event @event)
        {
            return JsonConvert.DeserializeObject<uint>(@event["StatusID"]);
        }

        public static uint StackCount(this Event @event)
        {
            return JsonConvert.DeserializeObject<uint>(@event["StackCount"]);
        }

        public static uint Param(this Event @event)
        {
            return JsonConvert.DeserializeObject<uint>(@event["Param"]);
        }
    }

    public static class IbcHelper
    {
        // 本文件同时引入了 Dalamud 的 IGameObject，这里必须写全名以免歧义
        public static KodakkuAssist.Data.IGameObject? GetById(this ScriptAccessory sa, ulong id)
        {
            return sa.Data.Objects.SearchById(id);
        }
    }

    public static class DirectionCalc
    {
        public static float DegToRad(this float deg) => (deg + 360f) % 360f / 180f * float.Pi;
        public static float RadToDeg(this float rad) => (rad + 2 * float.Pi) % (2 * float.Pi) / float.Pi * 180f;

        // 以北为0建立list
        // Game         List    Logic
        // 0            - 4     pi
        // 0.25 pi      - 3     0.75pi
        // 0.5 pi       - 2     0.5pi
        // 0.75 pi      - 1     0.25pi
        // pi           - 0     0
        // 1.25 pi      - 7     1.75pi
        // 1.5 pi       - 6     1.5pi
        // 1.75 pi      - 5     1.25pi
        // Logic = Pi - Game (+ 2pi)

        /// <summary>
        /// 将游戏基角度（以南为0，逆时针增加）转为逻辑基角度（以北为0，顺时针增加）
        /// 算法与Logic2Game完全相同，但为了代码可读性，便于区分。
        /// </summary>
        /// <param name="radian">游戏基角度</param>
        /// <returns>逻辑基角度</returns>
        public static float Game2Logic(this float radian)
        {
            // if (r < 0) r = (float)(r + 2 * Math.PI);
            // if (r > 2 * Math.PI) r = (float)(r - 2 * Math.PI);

            var r = float.Pi - radian;
            r = (r + float.Pi * 2) % (float.Pi * 2);
            return r;
        }

        /// <summary>
        /// 将逻辑基角度（以北为0，顺时针增加）转为游戏基角度（以南为0，逆时针增加）
        /// 算法与Game2Logic完全相同，但为了代码可读性，便于区分。
        /// </summary>
        /// <param name="radian">逻辑基角度</param>
        /// <returns>游戏基角度</returns>
        public static float Logic2Game(this float radian)
        {
            // var r = (float)Math.PI - radian;
            // if (r < Math.PI) r = (float)(r + 2 * Math.PI);
            // if (r > Math.PI) r = (float)(r - 2 * Math.PI);

            return radian.Game2Logic();
        }

        /// <summary>
        /// 输入逻辑基角度，获取逻辑方位（斜分割以正上为0，正分割以右上为0，顺时针增加）
        /// </summary>
        /// <param name="radian">逻辑基角度</param>
        /// <param name="dirs">方位总数</param>
        /// <param name="diagDivision">斜分割，默认true</param>
        /// <returns>逻辑基角度对应的逻辑方位</returns>
        public static int Rad2Dirs(this float radian, int dirs, bool diagDivision = true)
        {
            var r = diagDivision
                ? Math.Round(radian / (2f * float.Pi / dirs))
                : Math.Floor(radian / (2f * float.Pi / dirs));
            r = (r + dirs) % dirs;
            return (int)r;
        }

        /// <summary>
        /// 输入坐标，获取逻辑方位（斜分割以正上为0，正分割以右上为0，顺时针增加）
        /// </summary>
        /// <param name="point">坐标点</param>
        /// <param name="center">中心点</param>
        /// <param name="dirs">方位总数</param>
        /// <param name="diagDivision">斜分割，默认true</param>
        /// <returns>该坐标点对应的逻辑方位</returns>
        public static int Position2Dirs(this Vector3 point, Vector3 center, int dirs, bool diagDivision = true)
        {
            double dirsDouble = dirs;
            var r = diagDivision
                ? Math.Round(dirsDouble / 2 - dirsDouble / 2 * Math.Atan2(point.X - center.X, point.Z - center.Z) / Math.PI) % dirsDouble
                : Math.Floor(dirsDouble / 2 - dirsDouble / 2 * Math.Atan2(point.X - center.X, point.Z - center.Z) / Math.PI) % dirsDouble;
            return (int)r;
        }

        /// <summary>
        /// 以逻辑基弧度旋转某点
        /// </summary>
        /// <param name="point">待旋转点坐标</param>
        /// <param name="center">中心</param>
        /// <param name="radian">旋转弧度</param>
        /// <returns>旋转后坐标点</returns>
        public static Vector3 RotatePoint(this Vector3 point, Vector3 center, float radian)
        {
            // 围绕某点顺时针旋转某弧度
            Vector2 v2 = new(point.X - center.X, point.Z - center.Z);
            var rot = MathF.PI - MathF.Atan2(v2.X, v2.Y) + radian;
            var length = v2.Length();
            return new Vector3(center.X + MathF.Sin(rot) * length, center.Y, center.Z - MathF.Cos(rot) * length);

            // 另一种方案待验证
            // var nextPos = Vector3.Transform((point - center), Matrix4x4.CreateRotationY(radian)) + center;
        }

        /// <summary>
        /// 以逻辑基角度从某中心点向外延伸
        /// </summary>
        /// <param name="center">待延伸中心点</param>
        /// <param name="radian">旋转弧度</param>
        /// <param name="length">延伸长度</param>
        /// <returns>延伸后坐标点</returns>
        public static Vector3 ExtendPoint(this Vector3 center, float radian, float length)
        {
            // 令某点以某弧度延伸一定长度
            return new Vector3(center.X + MathF.Sin(radian) * length, center.Y, center.Z - MathF.Cos(radian) * length);
        }

        /// <summary>
        /// 寻找外侧某点到中心的逻辑基弧度
        /// </summary>
        /// <param name="center">中心</param>
        /// <param name="newPoint">外侧点</param>
        /// <returns>外侧点到中心的逻辑基弧度</returns>
        public static float FindRadian(this Vector3 newPoint, Vector3 center)
        {
            // 找到某点到中心的弧度
            float radian = MathF.PI - MathF.Atan2(newPoint.X - center.X, newPoint.Z - center.Z);
            if (radian < 0)
                radian += 2 * MathF.PI;
            return radian;
        }

        /// <summary>
        /// 将输入点左右折叠
        /// </summary>
        /// <param name="point">待折叠点</param>
        /// <param name="centerX">中心折线坐标点</param>
        /// <returns></returns>
        public static Vector3 FoldPointHorizon(this Vector3 point, float centerX)
        {
            // Vector3 v3 = new(2 * centerX - point.X, point.Y, point.Z);
            // return v3;
            return point with { X = 2 * centerX - point.X };
        }

        /// <summary>
        /// 将输入点上下折叠
        /// </summary>
        /// <param name="point">待折叠点</param>
        /// <param name="centerZ">中心折线坐标点</param>
        /// <returns></returns>
        public static Vector3 FoldPointVertical(this Vector3 point, float centerZ)
        {
            // Vector3 v3 = new(point.X, point.Y, 2 * centerZ - point.Z);
            // return v3;
            return point with { Z = 2 * centerZ - point.Z };
        }

        /// <summary>
        /// 将输入点朝某中心点往内/外同角度延伸，默认向内
        /// </summary>
        /// <param name="point">待延伸点</param>
        /// <param name="center">中心点</param>
        /// <param name="length">延伸长度</param>
        /// <param name="isOutside">是否向外延伸</param>>
        /// <returns></returns>
        public static Vector3 PointInOutside(this Vector3 point, Vector3 center, float length, bool isOutside = false)
        {
            Vector2 v2 = new(point.X - center.X, point.Z - center.Z);
            var targetPos = (point - center) / v2.Length() * length * (isOutside ? 1 : -1) + point;
            return targetPos;
        }

        /// <summary>
        /// 寻找两点之间的角度差，范围0~360deg
        /// </summary>
        /// <param name="basePoint">基准位置</param>
        /// <param name="targetPos">比较目标位置</param>
        /// <param name="center">场地中心</param>
        /// <returns></returns>
        public static float FindRadianDifference(this Vector3 targetPos, Vector3 basePoint, Vector3 center)
        {
            var baseRad = basePoint.FindRadian(center);
            var targetRad = targetPos.FindRadian(center);
            var deltaRad = targetRad - baseRad;
            if (deltaRad < 0)
                deltaRad += float.Pi * 2;
            return deltaRad;
        }

        /// <summary>
        /// 从场中看向场外是否在右侧，多用于场边敌人的分身机制
        /// </summary>
        /// <param name="basePoint">基准位置</param>
        /// <param name="targetPos">比较目标位置</param>
        /// <param name="center">场地中心</param>
        /// <returns></returns>
        public static bool IsAtRight(this Vector3 targetPos, Vector3 basePoint, Vector3 center)
        {
            // 从场中看向场外，在右侧
            return targetPos.FindRadianDifference(basePoint, center) < float.Pi;
        }
    }

    public static class IndexHelper
    {
        /// <summary>
        /// 输入玩家dataId，获得对应的位置index
        /// </summary>
        /// <param name="pid">玩家SourceId</param>
        /// <param name="accessory"></param>
        /// <returns>该玩家对应的位置index</returns>
        public static int GetPlayerIdIndex(this ScriptAccessory accessory, ulong pid)
        {
            // 获得玩家 IDX
            return accessory.Data.PartyList.IndexOf((uint)pid);
        }

        /// <summary>
        /// 获得主视角玩家对应的位置index
        /// </summary>
        /// <param name="accessory"></param>
        /// <returns>主视角玩家对应的位置index</returns>
        public static int GetMyIndex(this ScriptAccessory accessory)
        {
            return accessory.Data.PartyList.IndexOf(accessory.Data.Me);
        }

        /// <summary>
        /// 输入玩家dataId，获得对应的位置称呼，输出字符仅作文字输出用
        /// </summary>
        /// <param name="pid">玩家SourceId</param>
        /// <param name="accessory"></param>
        /// <returns>该玩家对应的位置称呼</returns>
        public static string GetPlayerJobById(this ScriptAccessory accessory, uint pid)
        {
            // 获得玩家职能简称，无用处，仅作DEBUG输出
            var idx = accessory.Data.PartyList.IndexOf(pid);
            var str = accessory.GetPlayerJobByIndex(idx);
            return str;
        }

        /// <summary>
        /// 输入位置index，获得对应的位置称呼，输出字符仅作文字输出用
        /// </summary>
        /// <param name="idx">位置index</param>
        /// <param name="accessory"></param>
        /// <returns></returns>
        public static string GetPlayerJobByIndex(this ScriptAccessory accessory, int idx)
        {
            var str = idx switch
            {
                0 => "MT",
                1 => "ST",
                2 => "H1",
                3 => "H2",
                4 => "D1",
                5 => "D2",
                6 => "D3",
                7 => "D4",
                _ => "unknown"
            };
            return str;
        }
    }

    public static class ColorHelper
    {
        public static ScriptColor ColorRed = new ScriptColor { V4 = new Vector4(1.0f, 0f, 0f, 1.0f) };
        public static ScriptColor ColorPink = new ScriptColor { V4 = new Vector4(1f, 0f, 1f, 1.0f) };
        public static ScriptColor ColorCyan = new ScriptColor { V4 = new Vector4(0f, 1f, 0.8f, 1.0f) };
        public static ScriptColor ColorDark = new ScriptColor { V4 = new Vector4(0f, 0f, 0f, 1.0f) };
        public static ScriptColor ColorLightBlue = new ScriptColor { V4 = new Vector4(0.48f, 0.40f, 0.93f, 1.0f) };
        public static ScriptColor ColorWhite = new ScriptColor { V4 = new Vector4(1f, 1f, 1f, 2f) };
        public static ScriptColor ColorYellow = new ScriptColor { V4 = new Vector4(1.0f, 1.0f, 0f, 1.0f) };
        public static ScriptColor ColorExaflare = new ScriptColor { V4 = new Vector4(1.0f, 1.0f, 0.0f, 1.5f) };
        public static ScriptColor ColorExaflareWarn = new ScriptColor { V4 = new Vector4(0.6f, 0.6f, 1f, 1.0f) };
    }

    public static class ListHelper
    {
        /// <summary>
        /// 将List转为String以输出
        /// </summary>
        /// <param name="list"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static string StringList<T>(this List<T> list)
        {
            return string.Join(", ", list);
        }
    }

    public static class AssignDp
    {
        /// <summary>
        /// 返回箭头指引相关dp
        /// </summary>
        /// <param name="accessory"></param>
        /// <param name="ownerObj">箭头起始，可输入uint或Vector3</param>
        /// <param name="targetObj">箭头指向目标，可输入uint或Vector3，为0则无目标</param>
        /// <param name="delay">绘图出现延时</param>
        /// <param name="destroy">绘图消失时间</param>
        /// <param name="name">绘图名称</param>
        /// <param name="rotation">箭头旋转角度</param>
        /// <param name="scale">箭头宽度</param>
        /// <param name="isSafe">使用安全色</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static DrawPropertiesEdit DrawGuidance(this ScriptAccessory accessory, 
            object ownerObj, object targetObj, int delay, int destroy, string name, float rotation = 0, float scale = 1f, bool isSafe = true)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Scale = new Vector2(scale);
            dp.Rotation = rotation;
            dp.ScaleMode |= ScaleMode.YByDistance;
            dp.Color = isSafe ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
            dp.Delay = delay;
            dp.DestoryAt = destroy;

            switch (ownerObj)
            {
                case uint sid:
                    dp.Owner = sid;
                    break;
                case Vector3 spos:
                    dp.Position = spos;
                    break;
                default:
                    throw new ArgumentException("ownerObj的目标类型输入错误");
            }

            switch (targetObj)
            {
                case uint tid:
                    if (tid != 0) dp.TargetObject = tid;
                    break;
                case Vector3 tpos:
                    dp.TargetPosition = tpos;
                    break;
            }

            return dp;
        }

        public static DrawPropertiesEdit DrawGuidance(this ScriptAccessory accessory, 
            object targetObj, int delay, int destroy, string name, float rotation = 0, float scale = 1f, bool isSafe = true)
        {
            return targetObj switch
            {
                uint uintTarget => accessory.DrawGuidance(accessory.Data.Me, uintTarget, delay, destroy, name, rotation, scale, isSafe),
                Vector3 vectorTarget => accessory.DrawGuidance(accessory.Data.Me, vectorTarget, delay, destroy, name, rotation, scale, isSafe),
                _ => throw new ArgumentException("targetObj 的类型必须是 uint 或 Vector3")
            };
        }


        /// <summary>
        /// 返回自己指向某目标地点的dp，可修改dp.TargetPosition, dp.Scale
        /// </summary>
        /// <param name="targetPos">指向地点</param>
        /// <param name="delay">延时delay ms出现</param>
        /// <param name="scale">指路线条宽度</param>
        /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
        /// <param name="name">绘图名称</param>
        /// <param name="accessory"></param>
        /// <returns></returns>
        public static DrawPropertiesEdit DrawDirPos(this ScriptAccessory accessory, Vector3 targetPos, int delay, int destroy, string name, float scale = 1f)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Scale = new Vector2(scale);
            dp.Owner = accessory.Data.Me;
            dp.TargetPosition = targetPos;
            dp.ScaleMode |= ScaleMode.YByDistance;
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.Delay = delay;
            dp.DestoryAt = destroy;
            return dp;
        }

        /// <summary>
        /// 返回起始地点指向某目标地点的dp，可修改dp.Position, dp.TargetPosition, dp.Scale
        /// </summary>
        /// <param name="startPos">起始地点</param>
        /// <param name="targetPos">指向地点</param>
        /// <param name="delay">延时delay ms出现</param>
        /// <param name="scale">指路线条宽度</param>
        /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
        /// <param name="name">绘图名称</param>
        /// <param name="accessory"></param>
        /// <returns></returns>
        public static DrawPropertiesEdit DrawDirPos2Pos(this ScriptAccessory accessory, Vector3 startPos, Vector3 targetPos, int delay, int destroy, string name, float scale = 1f)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Scale = new Vector2(scale);
            dp.Position = startPos;
            dp.TargetPosition = targetPos;
            dp.ScaleMode |= ScaleMode.YByDistance;
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.Delay = delay;
            dp.DestoryAt = destroy;
            return dp;
        }

        /// <summary>
        /// 返回距离某对象目标最近/最远的dp
        /// </summary>
        /// <param name="accessory"></param>
        /// <param name="ownerId">起始目标id，通常为boss</param>
        /// <param name="orderIdx">顺序，从1开始</param>
        /// <param name="delay">延时delay ms出现</param>
        /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
        /// <param name="name">绘图名称</param>
        /// <param name="width">绘图宽度</param>
        /// <param name="length">绘图长度</param>
        /// <param name="isNear">true为最近，false为最远</param>
        /// <param name="byTime">动画效果随时间填充</param>
        /// <param name="lengthByDistance">长度是否随距离改变</param>
        /// <returns></returns>
        public static DrawPropertiesEdit DrawTargetNearFarOrder(this ScriptAccessory accessory, uint ownerId, uint orderIdx,
            bool isNear, float width, float length, int delay, int destroy, string name, bool lengthByDistance = false, bool byTime = false)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Scale = new Vector2(width, length);
            dp.Owner = ownerId;
            dp.Delay = delay;
            dp.DestoryAt = destroy;
            dp.CentreResolvePattern =
                isNear ? PositionResolvePatternEnum.PlayerNearestOrder : PositionResolvePatternEnum.PlayerFarestOrder;
            dp.CentreOrderIndex = orderIdx;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.ScaleMode |= lengthByDistance ? ScaleMode.YByDistance : ScaleMode.None;
            dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
            return dp;
        }

        /// <summary>
        /// 返回ownerId仇恨相关的dp
        /// </summary>
        /// <param name="accessory"></param>
        /// <param name="ownerId">起始目标id，通常为boss</param>
        /// <param name="orderIdx">仇恨顺序，从1开始</param>
        /// <param name="width">绘图宽度</param>
        /// <param name="length">绘图长度</param>
        /// <param name="delay">延时delay ms出现</param>
        /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
        /// <param name="byTime">动画效果随时间填充</param>
        /// <param name="lengthByDistance">长度是否随距离改变</param>
        /// <param name="name">绘图名称</param>
        /// <returns></returns>
        public static DrawPropertiesEdit DrawOwnersEnmityOrder(this ScriptAccessory accessory, uint ownerId, uint orderIdx, float width, float length, int delay, int destroy, string name, bool lengthByDistance = false, bool byTime = false)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Scale = new Vector2(width, length);
            dp.Owner = ownerId;
            dp.Delay = delay;
            dp.DestoryAt = destroy;
            dp.CentreResolvePattern = PositionResolvePatternEnum.OwnerEnmityOrder;
            dp.CentreOrderIndex = orderIdx;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.ScaleMode |= lengthByDistance ? ScaleMode.YByDistance : ScaleMode.None;
            dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
            return dp;
        }

        /// <summary>
        /// 返回圆形dp，跟随owner，可修改 dp.Owner, dp.Scale
        /// </summary>
        /// <param name="ownerId">起始目标id，通常为自己或Boss</param>
        /// <param name="scale">圆圈尺寸</param>
        /// <param name="delay">延时delay ms出现</param>
        /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
        /// <param name="name">绘图名称</param>
        /// <param name="byTime">动画效果随时间填充</param>
        /// <param name="accessory"></param>
        /// <returns></returns>
        public static DrawPropertiesEdit DrawCircle(this ScriptAccessory accessory, uint ownerId, float scale, int delay, int destroy, string name, bool byTime = false)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Scale = new Vector2(scale);
            dp.Owner = ownerId;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = delay;
            dp.DestoryAt = destroy;
            dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
            return dp;
        }

        /// <summary>
        /// 返回静态dp，通常用于指引固定位置。可修改 dp.Position, dp.Rotation, dp.Scale
        /// </summary>
        /// <param name="center">绘图中心位置</param>
        /// <param name="radian">图形角度</param>
        /// <param name="rotation">旋转角度，以北为0度顺时针</param>
        /// <param name="width">绘图宽度</param>
        /// <param name="length">绘图长度</param>
        /// <param name="delay">延时delay ms出现</param>
        /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
        /// <param name="name">绘图名称</param>
        /// <param name="accessory"></param>
        /// <returns></returns>
        public static DrawPropertiesEdit DrawStatic(this ScriptAccessory accessory, Vector3 center, float radian, float rotation, float width, float length, int delay, int destroy, string name)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Scale = new Vector2(width, length);
            dp.Position = center;
            dp.Radian = radian;
            dp.Rotation = rotation.Logic2Game();
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = delay;
            dp.DestoryAt = destroy;
            return dp;
        }

        /// <summary>
        /// 返回静态圆圈dp，通常用于指引固定位置。
        /// </summary>
        /// <param name="accessory"></param>
        /// <param name="center">圆圈中心位置</param>
        /// <param name="color">圆圈颜色</param>
        /// <param name="scale">圆圈尺寸，默认1.5f</param>
        /// <param name="delay">延时delay ms出现</param>
        /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
        /// <param name="name">绘图名称</param>
        /// <returns></returns>
        public static DrawPropertiesEdit DrawStaticCircle(this ScriptAccessory accessory, Vector3 center, Vector4 color, int delay, int destroy, string name, float scale = 1.5f)
        {
            var dp = accessory.DrawStatic(center, 0, 0, scale, scale, delay, destroy, name);
            dp.Color = color;
            return dp;
        }

        /// <summary>
        /// 返回静态月环dp，通常用于指引固定位置。
        /// </summary>
        /// <param name="accessory"></param>
        /// <param name="center">月环中心位置</param>
        /// <param name="color">月环颜色</param>
        /// <param name="scale">月环外径，默认1.5f</param>
        /// <param name="innerscale">月环内径，默认scale-0.05f</param>
        /// <param name="delay">延时delay ms出现</param>
        /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
        /// <param name="name">绘图名称</param>
        /// <returns></returns>
        public static DrawPropertiesEdit DrawStaticDonut(this ScriptAccessory accessory, Vector3 center, Vector4 color, int delay, int destroy, string name, float scale, float innerscale = 0)
        {
            var dp = accessory.DrawStatic(center, float.Pi * 2, 0, scale, scale, delay, destroy, name);
            dp.Color = color;
            dp.InnerScale = innerscale != 0f ? new Vector2(innerscale) : new Vector2(scale - 0.05f);
            return dp;
        }

        /// <summary>
        /// 返回扇形
        /// </summary>
        /// <param name="ownerId">起始目标id，通常为自己或Boss</param>
        /// <param name="radian">扇形弧度</param>
        /// <param name="rotation">图形旋转角度</param>
        /// <param name="scale">扇形尺寸</param>
        /// <param name="innerScale">扇形内环空心尺寸</param>
        /// <param name="delay">延时delay ms出现</param>
        /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
        /// <param name="name">绘图名称</param>
        /// <param name="byTime">动画效果随时间填充</param>
        /// <param name="accessory"></param>
        /// <returns></returns>
        public static DrawPropertiesEdit DrawFan(this ScriptAccessory accessory, uint ownerId, float radian, float rotation, float scale, float innerScale, int delay, int destroy, string name, bool byTime = false)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Scale = new Vector2(scale);
            dp.InnerScale = new Vector2(innerScale);
            dp.Radian = radian;
            dp.Rotation = rotation;
            dp.Owner = ownerId;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = delay;
            dp.DestoryAt = destroy;
            dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
            return dp;
        }
    }

    #endregion

}

