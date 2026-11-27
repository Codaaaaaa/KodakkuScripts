using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Utility.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using KodakkuAssist.Data;
using KodakkuAssist.Extensions;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.Draw.Manager;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.GameEvent.Struct;
using KodakkuAssist.Module.GameOperate;
using KodakkuAssist.Script;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CicerosKodakkuAssist.FuturesRewrittenUltimate.LPDU
{

    [ScriptType(name: Name, territorys: [1238], guid: "79f985d3-6110-48a3-8caf-86f378f91c82",
        version: Version, author: "Karlin & Cicero & Usami & Tetora", note: Note, updateInfo: UpdateInfo)]

    public class Futures_Rewritten_Ultimate
    {

        #region 元信息

        private const string Name = "光暗未来绝境战 FRU - LPDU";
        private const string Version = "0.0.0.1";

        private const string Note =
            $"""
            {Version}
            整合自三个脚本：
                Karlin's FRU script (灵视改装版) - Karlin & Cicero：全程绘图与指路（脚本主体）
                FRU_Patch 补丁 - Usami：P1 罪壤堕(DB闲固)、P2 光暴放泥、P3 时间压缩 / 启示录、P4 光之锁 / 碎片受击警察等
                FRU_DLC 额外补充 - Tetora：严冬风暴、未来的碎片、死亡轮回连线、各类横幅提醒

            已删除所有给队友打头顶标记的功能；读取他人标记来指路的功能保留。
            小队排序需为 MT ST H1 H2 D1 D2 D3 D4。
            带 * 的方法和设置是非原生功能（自动面向、自动使用技能、改变目标可选中状态），
            统一放在用户设置的「* 特殊功能」分组里开关。
            三个原脚本本来就是叠加使用的，部分横幅提示会重复（横幅只显示最早的一个），可在方法列表里关掉不需要的。
            """;

        private const string UpdateInfo =
            $"""
            {Version}
            初版：整合 Karlin(灵视改装版)、Usami 补丁、Tetora 额外补充三个脚本
            """;

        #endregion

        #region 调试

        // 打开后所有指路按全队 8 人绘制，每人用自己的位次 / buff / 标记 / 仇恨算；横幅、TTS、聊天仍只给自己
        // 必须是 const：userControl: Debugging 是特性参数，只吃编译期常量
        private const bool Debugging = false;

        /// <summary>要画指路的位次：Debug 时全队，否则只有自己（不在小队里则为空）</summary>
        private static List<int> GuidanceIndices(ScriptAccessory sa)
        {
            var me = sa.GetMyIndex();
            return Debugging ? [.. Enumerable.Range(0, sa.Data.PartyList.Count)] : me < 0 ? [] : [me];
        }

        /// <summary>指路箭头起点 Owner：正常取 PartyList[idx]，越界回退自己</summary>
        private static uint GuidanceOwner(ScriptAccessory sa, int idx)
            => idx >= 0 && idx < sa.Data.PartyList.Count ? sa.Data.PartyList[idx] : sa.Data.Me;

        /// <summary>位次 idx 对应的玩家对象，代替指路里的 MyObject</summary>
        private static IGameObject? GuidanceObject(ScriptAccessory sa, int idx)
            => sa.Data.Objects.SearchById(GuidanceOwner(sa, idx));

        /// <summary>该视角是不是自己：文字 / TTS / 聊天只给自己发一次</summary>
        private static bool IsMe(ScriptAccessory sa, int idx) => idx == sa.GetMyIndex();

        #endregion

        #region User_Settings_用户设置

        [UserSetting("-----全局设置----- (No actual meaning for this setting/此设置无实际意义)")]
        public bool _____Global_Settings_____ { get; set; } = true;
        [UserSetting("启用文本提示")]
        public bool Enable_Text_Prompts { get; set; } = true;
        [UserSetting("提示的语言")]
        public Languages_Of_Prompts Language_Of_Prompts { get; set; }

        [UserSetting("-----TTS设置----- (No actual meaning for this setting/此设置无实际意义)")]
        public bool _____TTS_Settings_____ { get; set; } = true;
        [UserSetting("启用原版TTS")]
        public bool Enable_Vanilla_TTS { get; set; } = true;
        [UserSetting("启用Daily Routines TTS (It requires the plugin Daily Routines to be enabled already!/需要已经启用插件Daily Routines!)")]
        public bool Enable_Daily_Routines_TTS { get; set; } = false;

        [UserSetting("-----* 特殊功能(非原生)----- (No actual meaning for this setting/此设置无实际意义)")]
        public bool _____Special_Features_____ { get; set; } = true;
        [UserSetting("*P2.5 暗水晶不可选中")]
        public bool P2暗水晶不可选中 { get; set; } = true;
        [UserSetting("*P3一运 时间压缩眩晕时自动面向")]
        public bool P3眩晕自动面向 { get; set; } = true;
        [UserSetting("*P4二运 神圣之翼(Y字击退)自动防击退")]
        public AntiKnockbackEnum AutoAntiKnockback { get; set; } = AntiKnockbackEnum.None;

        [UserSetting("-----P1设置----- (No actual meaning for this setting/此设置无实际意义)")]
        public bool _____Phase1_Settings_____ { get; set; } = true;
        [UserSetting("P1 燃烧击(火雷直线)特性的颜色")]
        public ScriptColor Phase1_Colour_Of_Burnt_Strike_Characteristics { get; set; } = new() { V4 = new(1f, 1f, 0f, 1f) };

        [UserSetting("-----P2设置----- (No actual meaning for this setting/此设置无实际意义)")]
        public bool _____Phase2_Settings_____ { get; set; } = true;
        [UserSetting("P2镜中奇遇 镜子粗略指路的颜色")]
        public ScriptColor Phase2_Colour_Of_Mirror_Rough_Guidance { get; set; } = new() { V4 = new(1f, 1f, 0f, 1f) };
        [UserSetting("P2镜中奇遇 潜在危险区的颜色")]
        public ScriptColor Phase2_Colour_Of_Potential_Dangerous_Zones { get; set; } = new() { V4 = new(1f, 0f, 0f, 1f) };
        [UserSetting("P2光之失控(光暴) 球体AOE的颜色")]
        public ScriptColor Phase2_Colour_Of_Sphere_AOEs { get; set; } = new() { V4 = new(1f, 0f, 0f, 1f) };

        [UserSetting("-----P3设置----- (No actual meaning for this setting/此设置无实际意义)")]
        public bool _____Phase3_Settings_____ { get; set; } = true;
        [UserSetting("P3二运 粗略指路的颜色")]
        public ScriptColor Phase3_Colour_Of_Rough_Guidance { get; set; } = new() { V4 = new(1f, 1f, 0f, 1f) };
        [UserSetting("P3二运 倒数第二次启示(地火)的颜色")]
        public ScriptColor Phase3_Colour_Of_The_Penultimate_Apocalypse { get; set; } = new() { V4 = new(0, 1f, 1f, 1f) };
        [UserSetting("P3二运 引导暗夜舞蹈(最远死刑)的T")]
        public Tanks Phase3_Tank_Who_Baits_Darkest_Dance { get; set; }
        [UserSetting("P3二运 暗夜舞蹈(最远死刑)的颜色")]
        public ScriptColor Phase3_Colour_Of_Darkest_Dance { get; set; } = new() { V4 = new(1f, 0f, 0f, 1f) };

        [UserSetting("-----P4设置----- (No actual meaning for this setting/此设置无实际意义)")]
        public bool _____Phase4_Settings_____ { get; set; } = true;
        [UserSetting("P4一运 忧郁舞蹈(远近死刑)的颜色")]
        public ScriptColor Phase4_Colour_Of_Somber_Dance { get; set; } = new() { V4 = new(1f, 0f, 0f, 1f) };
        [UserSetting("P4二运 正常灯和延时灯的范围显示时间(second/秒)")]
        public float Phase4_Drawing_Duration_Of_Normal_And_Delayed_Lights { get; set; } = 3f;
        [UserSetting("P4二运 光之潮汐(地火)的颜色")]
        public ScriptColor Phase4_Colour_Of_Tidal_Light { get; set; } = new() { V4 = new(1f, 1f, 0f, 1f) };
        [UserSetting("P4二运 白圈指路的颜色")]
        public ScriptColor Phase4_Colour_Of_Residue_Guidance { get; set; } = new() { V4 = new(1f, 1f, 0f, 1f) };
        [UserSetting("P4二运 圣龙气息(龙头)碰撞箱的长度(meter/米)")]
        public float Phase4_Length_Of_Drachen_Wanderer_Hitboxes { get; set; } = 1.5f;
        [UserSetting("P4二运 圣龙气息(龙头)碰撞箱的颜色")]
        public ScriptColor Phase4_Colour_Of_Drachen_Wanderer_Hitboxes { get; set; } = new() { V4 = new(1f, 1f, 0f, 1f) };

        [UserSetting("-----P5设置----- (No actual meaning for this setting/此设置无实际意义)")]
        public bool _____Phase5_Settings_____ { get; set; } = true;
        [UserSetting("P5璀璨之刃(地火) 颜色")]
        public ScriptColor Phase5_Colour_Of_Fulgent_Blade { get; set; } = new() { V4 = new(0, 1f, 1f, 1f) };
        [UserSetting("P5璀璨之刃(地火) 指路当前步的颜色")]
        public ScriptColor Phase5_Colour_Of_The_Current_Guidance_Step { get; set; } = new() { V4 = new(0f, 1f, 0f, 1f) };
        [UserSetting("P5璀璨之刃(地火) 指路下一步的颜色")]
        public ScriptColor Phase5_Colour_Of_The_Next_Guidance_Step { get; set; } = new() { V4 = new(1f, 1f, 0f, 1f) };
        [UserSetting("P5璀璨之刃(地火) Boss中轴线的颜色")]
        public ScriptColor Phase5_Colour_Of_The_Boss_Central_Axis { get; set; } = new() { V4 = new(1f, 0f, 0f, 1f) };
        [UserSetting("P5璀璨之刃(地火) 后Boss面向人群")]
        public bool Phase5_Boss_Faces_Players_After_Fulgent_Blade { get; set; } = true;
        [UserSetting("P5光与暗之翼(踩塔) 挑衅提醒")]
        public bool Phase5_Reminder_To_Provoke_During_Wings_Dark_And_Light { get; set; } = true;

        [UserSetting("-----开发者设置----- (No actual meaning for this setting/此设置无实际意义)")]
        public bool _____Developer_Settings_____ { get; set; } = true;
        [UserSetting("启用开发者模式")]
        public static bool Enable_Developer_Mode { get; set; } = false;
        
        #endregion

        #region Variables_变量
        
        int? firstTargetIcon = null;
        int parse=-1;
        volatile bool isInPhase5 = false;

        int P1雾龙计数 = 0;
        readonly Object P1雾龙计数读写锁_AsAConstant = new Object();
        List<int> P1雾龙记录 = new List<int>{0, 0, 0, 0};
        bool P1雾龙雷 = false;
        List<int> P1转轮召抓人 = [0, 0, 0, 0, 0, 0, 0, 0];
        volatile int phase1_timesBurnishedGloryWasCast = 0;
        volatile List<int> phase1_tetheredPlayersDuringFallOfFaith = [];
        volatile bool phase1_isInFallOfFaith = false;
        volatile int phase1_semaphoreOfShortPrompts = 0;
        volatile int phase1_semaphoreOfDrawing = 0;
        volatile int phase1_semaphoreOfTheFinalPrompt = 0;
        List<int> P1塔 = [0, 0, 0, 0];

        volatile string phase2_bossId = "";
        bool P2DDDircle = false;
        volatile List<int> Phase2_Positions_Of_Icicle_Impact = [];
        Vector3[] phase2_positionToBeKnockedBack = new Vector3[8];
        System.Threading.AutoResetEvent phase2_semaphoreOfGuidanceBeforeKnockback = new System.Threading.AutoResetEvent(false);
        System.Threading.AutoResetEvent phase2_semaphoreOfGuidanceAfterKnockback = new System.Threading.AutoResetEvent(false);
        volatile int phase2_proteanPositionOfTheColourlessMirror = -1;
        System.Threading.AutoResetEvent phase2_semaphoreTheColourlessMirrorWasConfirmed = new System.Threading.AutoResetEvent(false);
        volatile List<int> phase2_proteanPositionsOfRedMirrors = [];
        System.Threading.AutoResetEvent phase2_semaphoreRedMirrorsWereConfirmed = new System.Threading.AutoResetEvent(false);
        volatile List<int> phase2_playersWithLuminousHammer = [];
        System.Threading.AutoResetEvent phase2_semaphoreLuminousHammerWasConfirmed = new System.Threading.AutoResetEvent(false);
        volatile List<int> phase2_stacksOfLightsteeped = [0, 0, 0, 0, 0, 0, 0, 0];
        volatile bool phase2_writePermissionForLightsteeped = true;
        System.Threading.AutoResetEvent phase2_semaphoreFinalLightsteepedWasConfirmed = new System.Threading.AutoResetEvent(false);

        volatile string phase3_bossId = "";
        List<int> P3FireBuff = [0, 0, 0, 0, 0, 0, 0, 0];
        List<int> P3WaterBuff = [0, 0, 0, 0, 0, 0, 0, 0];
        List<int> P3ReturnBuff = [0, 0, 0, 0, 0, 0, 0, 0];
        List<int> P3Lamp = [0, 0, 0, 0, 0, 0, 0, 0];
        List<int> P3LampWise = [0, 0, 0, 0, 0, 0, 0, 0];
        List<int> P3Stack = [0, 0, 0, 0, 0, 0, 0, 0];
        bool P3FloorFireDone = false;
        int P3FloorFire = 0;
        volatile List<Phase3_Types_Of_Dark_Water_III> phase3_typeOfDarkWaterIii = [
            Phase3_Types_Of_Dark_Water_III.NONE,
            Phase3_Types_Of_Dark_Water_III.NONE,
            Phase3_Types_Of_Dark_Water_III.NONE,
            Phase3_Types_Of_Dark_Water_III.NONE,
            Phase3_Types_Of_Dark_Water_III.NONE,
            Phase3_Types_Of_Dark_Water_III.NONE,
            Phase3_Types_Of_Dark_Water_III.NONE,
            Phase3_Types_Of_Dark_Water_III.NONE
        ];
        volatile int phase3_numberOfDarkWaterIiiHasBeenProcessed = 0;
        volatile int phase3_roundOfDarkWaterIii = 0;
        volatile int phase3_rangeSemaphoreOfDarkWaterIii = 0;
        volatile int phase3_guidanceSemaphoreOfDarkWaterIii = 0;
        // LPDU 启示分组：T奶西北、DPS东南；同组两人拿到同一种水时，MT>OT>H1>H2 / M1>M2>R1>R2 里优先级高的去对面
        // 同种水的两人里，这个列表中靠前的去左组（西北）：H2 H1 OT MT M1 M2 R1 R2
        List<int> phase3_locomotive_priority_asAConstant = [3, 2, 1, 0, 4, 5, 6, 7];
        // 两组分摊点：T奶在西北（1 标点方向）、DPS 在东南（3 标点方向），距场中 7m
        static readonly Vector3 phase3_leftGroupPosition_asAConstant = new(95.05f, 0, 95.05f);
        static readonly Vector3 phase3_rightGroupPosition_asAConstant = new(104.95f, 0, 104.95f);
        volatile bool phase3_hasConfirmedInitialSafePositions = false;
        Vector3 phase3_locomotive_initialSafePositionOfTheLeftGroup = new Vector3(100, 0, 100);
        Vector3 phase3_locomotive_initialSafePositionOfTheRightGroup = new Vector3(100, 0, 100);
        Vector3 phase3_locomotive_leftPositionToStackOfTheSecondRound = new Vector3(100, 0, 100);
        Vector3 phase3_locomotive_rightPositionToStackOfTheSecondRound = new Vector3(100, 0, 100);
        Vector3 phase3_finalPositionOfTheBoss = new Vector3(100, 0, 100);

        ulong P4FragmentId;
        List<int> P4Tether = [-1, -1, -1, -1, -1, -1, -1, -1];
        List<int> P4Stack = [0, 0, 0, 0, 0, 0, 0, 0];
        bool P4TetherDone = false;
        List<int> P4ClawBuff = [0, 0, 0, 0, 0, 0, 0, 0];
        volatile int phase4_numberOfMajorDebuffsHaveBeenCounted = 0;
        readonly Object phase4_readwriteLockOfMajorDebuffCounter_AsAConstant = new Object();
        System.Threading.AutoResetEvent phase4_semaphoreMajorDebuffsWereConfirmed = new System.Threading.AutoResetEvent(false);
        volatile int phase4_numberOfIncidentalDebuffsHaveBeenCounted = 0;
        readonly Object phase4_readwriteLockOfIncidentalDebuffCounter_AsAConstant = new Object();
        System.Threading.AutoResetEvent phase4_semaphoreIncidentalDebuffsWereConfirmed = new System.Threading.AutoResetEvent(false);
        List<int> P4OtherBuff = [0, 0, 0, 0, 0, 0, 0, 0];
        // LPDU 时间结晶同种红 debuff 分东西：按此顺序靠前的去西（与启示录同一套优先级）
        static readonly int[] P4LPDU东西优先级 = [3, 2, 1, 0, 4, 5, 6, 7];
        int P4BlueTether = 0;
        List<Vector3> P4WaterPos = [];
        volatile string phase4_id1OfTheDrachenWanderers = "";
        volatile string phase4_id2OfTheDrachenWanderers = "";
        readonly Object phase4_readwriteLockOfDrachenWandererIds_AsAConstant = new Object();
        volatile int phase4_timesTheWyrmclawDebuffWasRemoved = 0;
        volatile List<ulong> phase4_residueIdsFromEastToWest = [0, 0, 0, 0];
        // The leftmost (0), the about left (1), the about right (2), the rightmost (3) while facing south.
        volatile bool phase4_guidanceOfResiduesHasBeenGenerated = false;
        System.Threading.ManualResetEvent phase4_1_ManualReset = new System.Threading.ManualResetEvent(false);
        int phase4_1_TetherCount = 0;
        private static CrystallizeTime _cry = new();
        private static PriorityDict _pd = new();
        private static List<System.Threading.ManualResetEvent> _events = [.. Enumerable.Range(0, 20).Select(_ => new System.Threading.ManualResetEvent(false))];
        
        volatile string phase5_bossId = "";
        volatile bool phase5_hasAcquiredTheFirstTower = false;
        volatile string phase5_indexOfTheFirstTower = "";
        volatile bool phase5_hasConfirmedTheInitialPosition = false;
        Vector3 phase5_leftSideOfTheSouth_asAConstant = new Vector3(98, 0, 107);
        Vector3 phase5_rightSideOfTheSouth_asAConstant = new Vector3(102, 0, 107);
        Vector3 phase5_leftSideOfTheNortheast_asAConstant = new Vector3(107.06f, 0, 98.23f);
        Vector3 phase5_rightSideOfTheNortheast_asAConstant = new Vector3(105.06f, 0, 94.77f);
        Vector3 phase5_leftSideOfTheNorthwest_asAConstant = new Vector3(94.94f, 0, 94.77f);
        Vector3 phase5_rightSideOfTheNorthwest_asAConstant = new Vector3(92.94f, 0, 98.23f);
        Vector3 phase5_standbyPointBetweenSouthAndNortheast_asAConstant = new Vector3(106.06f, 0, 103.50f);
        Vector3 phase5_standbyPointBetweenSouthAndNorthwest_asAConstant = new Vector3(93.94f, 0, 103.50f);
        Vector3 phase5_standbyPointBetweenNortheastAndNorthwest_asAConstant = new Vector3(100, 0, 93);
        Vector3 phase5_positionToTakeHitsOnTheLeft_asAConstant = new Vector3(95.93f, 0, 104.07f);
        Vector3 phase5_positionToBeCoveredOnTheLeft_asAConstant = new Vector3(93.81f, 0, 106.19f);
        Vector3 phase5_positionToStandbyOnTheLeft_asAConstant = new Vector3(99.24f, 0, 108.72f);
        Vector3 phase5_positionToTakeHitsOnTheRight_asAConstant = new Vector3(104.07f, 0, 104.07f);
        Vector3 phase5_positionToBeCoveredOnTheRight_asAConstant = new Vector3(106.19f, 0, 106.19f);
        Vector3 phase5_positionToStandbyOnTheRight_asAConstant = new Vector3(100.76f, 0, 108.72f);
        // The left and right here refer to the left and right while facing the center of the zone (100,0,100).
        private string Phase = "";
        private Vector2? Point1 = new Vector2(0f, 0f);
        private Vector2? Point2 = new Vector2(0f, 0f);
        private Vector2? Point3 = new Vector2(0f, 0f);
        private Vector2? MiddlePoint = new Vector2(0f, 0f);
        private onPoint? OnPoint = null;
        private int bladeCount = 0;
        //private List<Blade> blades = new List<Blade>();
        private ConcurrentBag<Blade> blades = new ConcurrentBag<Blade>();
        private List<Blade> P1P3Blades = new List<Blade>();
        private List<onPoint> onPoints = new List<onPoint>();
        private List<Vector2?> BladeRoutes;
        private readonly object bladeLock = new object();
        private readonly object drawLock = new object();

        // ---- 补丁部分（Usami / Tetora）----
        private static readonly Vector3 Center = new(100, 0, 100);
        private FruPhase _fruPhase = FruPhase.Init;
        private static PriorityDict _usamiPd = new();
        private static Counter _ct = new();
        private static Apocalypse _apo = new();
        private static List<ManualResetEvent> _usamiEvents = [.. Enumerable.Range(0, 20).Select(_ => new ManualResetEvent(false))];
        private static List<ulong> _fragTargets = [];

        private const int Mt = 0;
        private const int St = 1;
        private const int D1 = 4;
        private const ulong FragmentDataId = 17841;

        private const uint 亲疏自行 = 7548;
        private const uint 沉稳咏唱 = 7559;
        private const uint 亲疏自行Buff = 0x4B9;
        private const uint 沉稳咏唱Buff = 0xA0;
        
        #endregion

        #region Enumeration_And_Classes_枚举和类

        public enum AntiKnockbackEnum
        {
            None = -1,
            亲疏自行 = 0,
            沉稳咏唱 = 1
        }

        // Usami 补丁的阶段机，Tetora 的阶段判断也复用它
        private enum FruPhase
        {
            Init,
            P1A_UtopianSky,             // P1A 乐园绝技
            P1B_FallOfFaith,            // P1B 罪壤堕
            P1C_BurntStrike,            // P1C 燃烧击
            P2A_DiamondDust,            // P2A 钻石星辰
            P2B_Mirror,                 // P2B 镜中奇遇
            P2C_LightRampant,           // P2C 光爆
            P2D_AbsoluteZero,           // P2D 绝对零度
            P3A_UltimateRelativity,     // P3A 时间压缩
            P3B_Apocalypse,             // P3B 地火
            P4A_DarklitDragonsong,      // P4A 光暗龙诗
            P4B_CrystallizeTime,        // P4B 时间结晶
            P5A_FulgentBlade,           // P5A 光尘之剑地火
            P5B_ParadiseRegained,       // P5B 复乐园踩塔
            P5C_PolarizingStrike,       // P5C 星灵之剑挡枪
        }

        public enum EventIdx : int
        {
            ApoGrouping = 0,
            ApoPreciseGrouping = 1,
        }

        public class Counter
        {
            // ReSharper disable once NullableWarningSuppressionIsUsed
            public ScriptAccessory accessory { get; set; } = null!;
            public int Number { get; set; } = 0;
            public bool Enable { get; set; } = true;
            public string Annotation = "";

            public void Init(ScriptAccessory _accessory, string annotation, bool enable = true)
            {
                accessory = _accessory;
                Number = 0;
                Enable = enable;
                Annotation = annotation;
            }

            public string ShowCounter()
            {
                var str = $"{Annotation} 计数器【{(Enable ? "使能" : "不使能")}】：{Number}\n";
                accessory.DebugMsg(str, Enable_Developer_Mode);
                return str;
            }

            public void DisableCounter()
            {
                Enable = false;
                var str = $"禁止 {Annotation} 计数器的数值改变。\n";
                accessory.DebugMsg(str, Enable_Developer_Mode);
            }

            public void EnableCounter()
            {
                Enable = true;
                var str = $"使能 {Annotation} 计数器的数值改变。\n";
                accessory.DebugMsg(str, Enable_Developer_Mode);
            }

            public void AddCounter(int num = 1)
            {
                if (!Enable) return;
                Number += num;
            }

            public void TimesCounter(int num = 1)
            {
                if (!Enable) return;
                Number *= num;
            }
        }

        public enum Languages_Of_Prompts
        {

            Simplified_Chinese_简体中文,
            English_英文

        }


        public enum Tanks
        {

            MT,
            OT_ST

        }

        public enum Phase3_Types_Of_Dark_Water_III
        {

            LONG,
            MEDIUM,
            SHORT,
            NONE

        }
        



        public enum Phase4_Relative_Positions_Of_Residues
        {

            Eastmost_最东侧,
            About_East_次东侧,
            About_West_次西侧,
            Westmost_最西侧,
            Unknown_未知

        }

        public class Blade
        {
            public UInt32 Id { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
            public double Rotation { get; set; }
            public Blade(UInt32 id, double x, double y, double rotation)
            {
                Id = id;
                X = x;
                Y = y;
                Rotation = rotation;
            }
        }

        public class onPoint
        {
            public string Name { get; set; }
            public Vector2 OnCoord { get; set; }//      Point
            public Vector2 Coord1 { get; set; }//         1
            public Vector2 Coord2 { get; set; }//     /       \

            //   4|         |2
            public Vector2 Coord3 { get; set; }//     \       /
            public Vector2 Coord4 { get; set; }//         3

            public onPoint(string name, Vector2 onCoord, Vector2 coord1, Vector2 coord2, Vector2 coord3, Vector2 coord4)
            {
                Name = name;
                this.OnCoord = onCoord;
                this.Coord1 = coord1;
                this.Coord2 = coord2;
                this.Coord3 = coord3;
                this.Coord4 = coord4;
            }
        }
        
        #endregion

        #region Initialization_初始化
        
        private void resetPoints()
        {
            onPoints.Clear();
            onPoints.Add(new onPoint("A", new Vector2(100, 93), new Vector2(100, 91.5f), new Vector2(101.4f, 92.9f), new Vector2(100, 94.3f), new Vector2(98.6f, 92.9f)));
            onPoints.Add(new onPoint("B", new Vector2(107, 100), new Vector2(108.5f, 100), new Vector2(107, 101.4f), new Vector2(105.6f, 100), new Vector2(107, 98.6f)));
            onPoints.Add(new onPoint("C", new Vector2(100, 107), new Vector2(100, 108.5f), new Vector2(98.6f, 107), new Vector2(100, 105.6f), new Vector2(101.4f, 107.1f)));
            onPoints.Add(new onPoint("D", new Vector2(93, 100), new Vector2(91.5f, 100), new Vector2(93, 98.6f), new Vector2(94.4f, 100), new Vector2(93, 101.4f)));
        }

        public void Init(ScriptAccessory accessory)
        {
            accessory.Method.RemoveDraw(".*");
            
            parse=1;
            isInPhase5 = false;

            P1雾龙记录 = new List<int>{0, 0, 0, 0};
            P1雾龙计数 = 0;
            P1转轮召抓人 = [0, 0, 0, 0, 0, 0, 0, 0];
            phase1_timesBurnishedGloryWasCast = 0;
            phase1_tetheredPlayersDuringFallOfFaith = [];
            phase1_isInFallOfFaith = false;
            phase1_semaphoreOfShortPrompts = 0;
            phase1_semaphoreOfDrawing = 0;
            phase1_semaphoreOfTheFinalPrompt = 0;
            P1塔 = [0, 0, 0, 0];

            phase2_bossId = "";
            Phase2_Positions_Of_Icicle_Impact.Clear();
            phase2_positionToBeKnockedBack = new Vector3[8];
            phase2_semaphoreOfGuidanceBeforeKnockback = new System.Threading.AutoResetEvent(false);
            phase2_semaphoreOfGuidanceAfterKnockback = new System.Threading.AutoResetEvent(false);
            phase2_proteanPositionOfTheColourlessMirror = -1;
            phase2_semaphoreTheColourlessMirrorWasConfirmed = new System.Threading.AutoResetEvent(false);
            phase2_proteanPositionsOfRedMirrors.Clear();
            phase2_semaphoreRedMirrorsWereConfirmed = new System.Threading.AutoResetEvent(false);
            phase2_playersWithLuminousHammer.Clear();
            phase2_semaphoreLuminousHammerWasConfirmed = new System.Threading.AutoResetEvent(false);
            phase2_stacksOfLightsteeped = [0, 0, 0, 0, 0, 0, 0, 0];
            phase2_writePermissionForLightsteeped = true;
            phase2_semaphoreFinalLightsteepedWasConfirmed = new System.Threading.AutoResetEvent(false);

            phase3_bossId = "";
            P3FloorFireDone = false;
            P3Stack = [0, 0, 0, 0, 0, 0, 0, 0];
            phase3_typeOfDarkWaterIii = [
                Phase3_Types_Of_Dark_Water_III.NONE,
                Phase3_Types_Of_Dark_Water_III.NONE,
                Phase3_Types_Of_Dark_Water_III.NONE,
                Phase3_Types_Of_Dark_Water_III.NONE,
                Phase3_Types_Of_Dark_Water_III.NONE,
                Phase3_Types_Of_Dark_Water_III.NONE,
                Phase3_Types_Of_Dark_Water_III.NONE,
                Phase3_Types_Of_Dark_Water_III.NONE
            ];
            phase3_numberOfDarkWaterIiiHasBeenProcessed = 0;
            phase3_roundOfDarkWaterIii = 0;
            phase3_rangeSemaphoreOfDarkWaterIii = 0;
            phase3_guidanceSemaphoreOfDarkWaterIii = 0;
            phase3_hasConfirmedInitialSafePositions = false;
            phase3_locomotive_initialSafePositionOfTheLeftGroup = new Vector3(100, 0, 100);
            phase3_locomotive_initialSafePositionOfTheRightGroup = new Vector3(100, 0, 100);
            phase3_locomotive_leftPositionToStackOfTheSecondRound = new Vector3(100, 0, 100);
            phase3_locomotive_rightPositionToStackOfTheSecondRound = new Vector3(100, 0, 100);
            phase3_finalPositionOfTheBoss = new Vector3(100, 0, 100);

            P4FragmentId=0;
            P4Tether=[-1,-1,-1,-1,-1,-1,-1,-1];
            P4Stack=[0,0,0,0,0,0,0,0];
            P4TetherDone=false;
            P4ClawBuff=[0,0,0,0,0,0,0,0];
            phase4_numberOfMajorDebuffsHaveBeenCounted = 0;
            phase4_semaphoreMajorDebuffsWereConfirmed = new System.Threading.AutoResetEvent(false);
            phase4_numberOfIncidentalDebuffsHaveBeenCounted = 0;
            phase4_semaphoreIncidentalDebuffsWereConfirmed = new System.Threading.AutoResetEvent(false);
            P4OtherBuff=[0,0,0,0,0,0,0,0];
            P4BlueTether=0;
            P4WaterPos=[];
            phase4_id1OfTheDrachenWanderers = "";
            phase4_id2OfTheDrachenWanderers = "";
            phase4_timesTheWyrmclawDebuffWasRemoved = 0;
            phase4_residueIdsFromEastToWest = [0, 0, 0, 0];
            phase4_guidanceOfResiduesHasBeenGenerated = false;
            phase4_1_ManualReset=new System.Threading.ManualResetEvent(false);
            phase4_1_TetherCount=0;
            // It's not necessary to initialize the static variables... right?

            phase5_bossId = "";
            phase5_hasAcquiredTheFirstTower = false;
            phase5_indexOfTheFirstTower = "";
            phase5_hasConfirmedTheInitialPosition = false;
            blades.Clear();
            P1P3Blades.Clear();
            BladeRoutes = Enumerable.Repeat<Vector2?>(null, 7).ToList();
            resetPoints();//初始化地火坐标

            _fruPhase = FruPhase.Init;
            _usamiEvents = [.. Enumerable.Range(0, 20).Select(_ => new ManualResetEvent(false))];
        }
        
        #endregion
        
        #region Phase_1

        [ScriptMethod(name: "----- Phase 1 ----- (No actual meaning for this toggle/此开关无实际意义)",
            eventType: EventTypeEnum.NpcYell,
            eventCondition: ["Give me your tired",
                            "给我你们疲倦的人"])]

        public void Phase1_Placeholder(Event @event, ScriptAccessory accessory) { }

        [ScriptMethod(name: "P1_八方雷火_引导扇形", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^((4014[48])|40329|40330)$"])]
        public void P1_八方雷火_引导扇形(Event @event, ScriptAccessory accessory)
        {
            if (parse!=1) return;
            if (!ParseObjectId(@event["SourceId"], out var sid)) return;
            foreach (var pm in accessory.Data.PartyList)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = "P1_八方雷火_引导扇形";
                dp.Scale = new(60);
                dp.Radian = float.Pi / 8;
                dp.Owner = sid;
                dp.TargetObject = pm;
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.DestoryAt = 7000;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
            }

        }
        [ScriptMethod(name: "P1_八方雷火_后续扇形", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(40145)$", "TargetIndex:1"])]
        public void P1_八方雷火_后续扇形(Event @event, ScriptAccessory accessory)
        {
            var dur = 2000;
            if (parse!=1) return;
            if (!ParseObjectId(@event["SourceId"], out var sid)) return;
            if (!float.TryParse(@event["SourceRotation"], out var rot)) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P1_八方雷火_后续扇形1";
            dp.Scale = new(60);
            dp.FixRotation = true;
            dp.Rotation = rot;
            dp.Radian = float.Pi / 8;
            dp.Owner = sid;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = dur;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P1_八方雷火_后续扇形2";
            dp.Scale = new(60);
            dp.Radian = float.Pi / 8;
            dp.FixRotation = true;
            dp.Rotation = rot + float.Pi / -8;
            dp.Owner = sid;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 2000;
            dp.DestoryAt = dur;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P1_八方雷火_后续扇形3";
            dp.Scale = new(60);
            dp.FixRotation = true;
            dp.Rotation = rot + float.Pi / -4;
            dp.Radian = float.Pi / 8;
            dp.Owner = sid;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 4000;
            dp.DestoryAt = dur;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

        }
        [ScriptMethod(name: "P1_八方雷火_分散分摊", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^((4014[48])|40329|40330)$"])]
        public void P1_八方雷火_分散分摊(Event @event, ScriptAccessory accessory)
        {
            if (parse!=1) return;
            if (!ParseObjectId(@event["SourceId"], out var sid)) return;
            string prompt = "";

            if (@event["ActionId"] == "40148" || @event["ActionId"] == "40330")
            {
                foreach (var pm in accessory.Data.PartyList)
                {
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = "P1_八方雷火_分散";
                    dp.Scale = new(6);
                    dp.Owner = pm;
                    dp.Color = accessory.Data.DefaultDangerColor;
                    dp.Delay = 5000;
                    dp.DestoryAt = 4000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                }

                if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                {

                    prompt = "分散";

                }

                if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                {

                    prompt = "Spread";

                }

            }
            else
            {
                int[] group = [6, 7, 4, 5, 2, 3, 0, 1];
                var myindex = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
                for (int i = 4; i <= 7; ++i)
                {
                    // The drawing owners here were adjusted a bit by Cicero.
                    // Here's an interesting fact - the action Sinsmoke (stack) will always target DPS.

                    var ismygroup = myindex == i || group[i] == myindex;

                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = "P1_八方雷火_分摊";
                    dp.Scale = new(6);
                    dp.Owner = accessory.Data.PartyList[i];
                    dp.Color = ismygroup ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
                    dp.Delay = 5000;
                    dp.DestoryAt = 4000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                }

                if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                {

                    prompt = "分摊";

                }

                if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                {

                    prompt = "Stack";

                }

            }

            System.Threading.Thread.Sleep(5000);

            if (!prompt.Equals(""))
            {

                if (Enable_Text_Prompts)
                {

                    accessory.Method.TextInfo(prompt, 1500);

                }

                accessory.TTS($"{prompt}", Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);

            }

        }
        [ScriptMethod(name: "P1_八方雷火_引导位置", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^((4014[48])|40329|40330)$"])]
        public void P1_八方雷火_引导位置(Event @event, ScriptAccessory accessory)
        {
            if (parse!=1) return;
            if (!ParseObjectId(@event["SourceId"], out var sid)) return;
            foreach (var 视角 in GuidanceIndices(accessory)) 指路(视角);

            void 指路(int 视角)
            {
                var myindex = 视角;
                var spread = @event["ActionId"] == "40148" || @event["ActionId"] == "40330";
                var rot8 = myindex switch
                {
                    0 => 0,
                    1 => 2,
                    2 => 6,
                    3 => 4,
                    4 => 5,
                    5 => 3,
                    6 => 7,
                    7 => 1,
                    _ => 0,
                };
                var outPoint = spread && (myindex == 2 || myindex == 3 || myindex == 6 || myindex == 7);
                var mPosEnd = RotatePoint(outPoint ? new(100, 0, 90) : new(100, 0, 95), new(100, 0, 100), float.Pi / 4 * rot8);

                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"P1_八方雷火_引导位置-{视角}";
                dp.Scale = new(2);
                dp.Owner = GuidanceOwner(accessory, 视角);
                dp.TargetPosition = mPosEnd;
                dp.ScaleMode |= ScaleMode.YByDistance;
                dp.Color = accessory.Data.DefaultSafeColor;
                dp.DestoryAt = 7000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

            }
        }
        [ScriptMethod(name: "P1_T死刑Buff爆炸", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:4166"])]
        public void P1_T死刑Buff爆炸(Event @event, ScriptAccessory accessory)
        {
            if (parse!=1) return;
            if (!ParseObjectId(@event["TargetId"], out var tid)) return;
            if (!int.TryParse(@event["DurationMilliseconds"], out var dur)) return;
            string prompt = "";

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P1_T死刑Buff爆炸1";
            dp.Scale = new(10);
            dp.Owner = tid;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = dur - 5000;
            dp.DestoryAt = 5000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P1_T死刑Buff爆炸2";
            dp.Scale = new(10);
            dp.Owner = tid;
            dp.CentreResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
            dp.CentreOrderIndex = 1;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = dur - 5000;
            dp.DestoryAt = 5000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            if (accessory.Data.PartyList.IndexOf(accessory.Data.Me) == 0
               ||
               accessory.Data.PartyList.IndexOf(accessory.Data.Me) == 1)
            {

                if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                {

                    prompt = "稍微靠近另一个T";

                }

                if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                {

                    prompt = "Get slightly closer to another tank";

                }

            }

            if (2 <= accessory.Data.PartyList.IndexOf(accessory.Data.Me)
               &&
               accessory.Data.PartyList.IndexOf(accessory.Data.Me) <= 7)
            {

                if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                {

                    prompt = "远离双T";

                }

                if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                {

                    prompt = "Stay away from both tanks";

                }

            }

            System.Threading.Thread.Sleep(dur - 5000);

            if (!prompt.Equals(""))
            {

                if (Enable_Text_Prompts)
                {

                    accessory.Method.TextInfo(prompt, 1500);

                }

                accessory.TTS($"{prompt}", Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);

            }

        }
        [ScriptMethod(name: "P1_雾龙_位置记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(40158)$"], userControl: false)]
        public void P1_雾龙_位置记录(Event @event, ScriptAccessory accessory)
        {
            if (parse!=1) return;
            if (!ParseObjectId(@event["SourceId"], out var sid)) return;
            
            KodakkuAssist.Data.IGameObject? obj=null;
            do {
                ++sid;
                obj=accessory.Data.Objects.SearchByEntityId((uint)sid);
            } while(obj==null);
            
            var dir8 = PositionTo8Dir(obj.Position, new(100, 0, 100));
            P1雾龙记录[dir8 % 4] = 1;
        }
        [ScriptMethod(name: "P1_雾龙_雷火记录", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(4015[45])$"], userControl: false)]
        public void P1_雾龙_雷火记录(Event @event, ScriptAccessory accessory)
        {
            if (parse!=1) return;
            P1雾龙雷 = (@event["ActionId"] == "40155");
        }
        [ScriptMethod(name: "P1_雾龙_范围", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(40158)$"])]
        public void P1_雾龙_范围(Event @event, ScriptAccessory accessory)
        {
            if (parse!=1) return;
            if (!ParseObjectId(@event["SourceId"], out var sid)) return;
            
            KodakkuAssist.Data.IGameObject? obj=null;
            do {
                ++sid;
                obj=accessory.Data.Objects.SearchByEntityId((uint)sid);
            } while(obj==null);

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P1_雾龙范围";
            dp.Scale = new(16, 50);
            dp.Owner = sid;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 9000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

        }
        [ScriptMethod(name: "P1_雾龙_分散分摊", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(4015[45])$"])]
        public void P1_雾龙_分散分摊(Event @event, ScriptAccessory accessory)
        {
            if (parse!=1) return;
            string prompt = "";

            if (@event["ActionId"] == "40155")
            {
                foreach (var pm in accessory.Data.PartyList)
                {
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = "P1_雾龙_分散";
                    dp.Scale = new(5);
                    dp.Owner = pm;
                    dp.Color = accessory.Data.DefaultDangerColor;
                    dp.Delay = 10000;
                    dp.DestoryAt = 5000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                }

                if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                {

                    prompt = "分散";

                }

                if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                {

                    prompt = "Spread";

                }

            }
            else
            {
                List<int> h1group = [0, 2, 4, 6];
                var myindex = accessory.Data.PartyList.IndexOf(accessory.Data.Me);

                var isH1group = h1group.Contains(myindex);

                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = "P1_雾龙_分摊1";
                dp.Scale = new(6);
                dp.Owner = accessory.Data.PartyList[2];
                dp.Color = isH1group ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
                dp.Delay = 10000;
                dp.DestoryAt = 5000;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

                dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = "P1_雾龙_分摊2";
                dp.Scale = new(6);
                dp.Owner = accessory.Data.PartyList[3];
                dp.Color = !isH1group ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
                dp.Delay = 10000;
                dp.DestoryAt = 5000;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

                if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                {

                    prompt = "分摊";

                }

                if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                {

                    prompt = "Stack";

                }

            }

            System.Threading.Thread.Sleep(10000);

            if (!prompt.Equals(""))
            {

                if (Enable_Text_Prompts)
                {

                    accessory.Method.TextInfo(prompt, 1500);

                }

                accessory.TTS($"{prompt}", Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);

            }

        }

        [ScriptMethod(name: "Phase1 Standby Position Of Utopian Sky 乐园绝技(雾龙)待机位置",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(4015[45])$"])]

        public void Phase1_Standby_Position_Of_Utopian_Sky_乐园绝技待机位置(Event @event, ScriptAccessory accessory)
        {

            if (parse!=1)
            {

                return;

            }

            foreach (var 视角 in GuidanceIndices(accessory)) 指路(视角);

            void 指路(int 视角)
            {
                int myIndex = 视角;

                // LPDU 待机：OT 与 R2 换位，其余按八方站位；T 和远程稍微错开
                int rotationMultiplier = myIndex switch
                {
                    0 => 0,
                    1 => 1,
                    2 => 6,
                    3 => 4,
                    4 => 5,
                    5 => 3,
                    6 => 7,
                    7 => 2
                };

                var myPosition = RotatePoint(new(100, 0, 81), new(100, 0, 100), float.Pi / 4 * rotationMultiplier);

                if (myIndex == 0)
                {

                    myPosition = RotatePoint(myPosition, new(100, 0, 100), float.Pi / 72);

                }

                if (myIndex == 1)
                {

                    myPosition = RotatePoint(myPosition, new(100, 0, 100), -(float.Pi / 72));

                }

                if (myIndex == 6)
                {

                    myPosition = RotatePoint(myPosition, new(100, 0, 100), -(float.Pi / 36));

                }

                if (myIndex == 7)
                {

                    myPosition = RotatePoint(myPosition, new(100, 0, 100), float.Pi / 36);

                }

                var currentProperty = accessory.Data.GetDefaultDrawProperties();

                currentProperty.Name = $"Phase1_Standby_Position_Of_Utopian_Sky_乐园绝技待机位置-{视角}";
                currentProperty.Scale = new(2);
                currentProperty.Owner = GuidanceOwner(accessory, 视角);
                currentProperty.TargetPosition = myPosition;
                currentProperty.ScaleMode |= ScaleMode.YByDistance;
                currentProperty.Color = accessory.Data.DefaultSafeColor;
                currentProperty.DestoryAt = 9000;

                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, currentProperty);


            }
        }

        [ScriptMethod(name: "P1_雾龙_处理位置", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(40158)$"])]
        public void P1_雾龙_处理位置(Event @event, ScriptAccessory accessory)
        {
            if (parse!=1) return;

            lock (P1雾龙计数读写锁_AsAConstant)
            {
                P1雾龙计数++;
                if (P1雾龙计数 != 3) return;
                Task.Delay(334).ContinueWith(t =>
                {
                    foreach (var 视角 in GuidanceIndices(accessory)) 指路(视角);

                    void 指路(int 视角)
                    {
                        if (!P1雾龙雷)
                        {
                            var safeDir = P1雾龙记录.IndexOf(0);
                            List<int> h1group = [0, 2, 4, 6];
                            var myindex = 视角;
                            var isH1group = h1group.Contains(myindex);
                            var rot8 = safeDir switch
                            {
                                0 => isH1group ? 0 : 4,
                                1 => isH1group ? 5 : 1,
                                2 => isH1group ? 6 : 2,
                                3 => isH1group ? 7 : 3,
                                _ => 0
                            };
                            var mPosEnd = RotatePoint(new(100, 0, 84), new(100, 0, 100), float.Pi / 4 * rot8);

                            var dp = accessory.Data.GetDefaultDrawProperties();
                            dp.Name = $"P1_雾龙_分摊处理位置-{视角}";
                            dp.Scale = new(2);
                            dp.Owner = GuidanceOwner(accessory, 视角);
                            dp.TargetPosition = mPosEnd;
                            dp.ScaleMode |= ScaleMode.YByDistance;
                            dp.Color = accessory.Data.DefaultSafeColor;
                            dp.DestoryAt = 9000;
                            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                        }
                        else
                        {
                            var safeDir = P1雾龙记录.IndexOf(0);
                            List<int> h1group = [0, 2, 4, 6];
                            var myindex = 视角;
                            var isH1group = h1group.Contains(myindex);
                            Vector3 p1 = new(100.0f, 0, 88.0f);
                            Vector3 p2 = new(100.0f, 0, 80.5f);
                            Vector3 p3 = new(106.5f, 0, 81.5f);
                            Vector3 p4 = new(093.5f, 0, 81.5f);
                            var rot8 = safeDir switch
                            {
                                0 => isH1group ? 0 : 4,
                                1 => isH1group ? 5 : 1,
                                2 => isH1group ? 6 : 2,
                                3 => isH1group ? 7 : 3,
                                _ => 0
                            };
                            var myPosA = myindex switch
                            {
                                0 => p2,
                                1 => p2,
                                2 => p1,
                                3 => p1,
                                4 => p3,
                                5 => p3,
                                6 => p4,
                                7 => p4,
                                _ => p1,
                            };
                            var mPosEnd = RotatePoint(myPosA, new(100, 0, 100), float.Pi / 4 * rot8);

                            var dp = accessory.Data.GetDefaultDrawProperties();
                            dp.Name = $"P1_雾龙_分散处理位置-{视角}";
                            dp.Scale = new(2);
                            dp.Owner = GuidanceOwner(accessory, 视角);
                            dp.TargetPosition = mPosEnd;
                            dp.ScaleMode |= ScaleMode.YByDistance;
                            dp.Color = accessory.Data.DefaultSafeColor;
                            dp.DestoryAt = 9000;
                            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                        }
                    }
                });

            }

        }

        [ScriptMethod(name: "Phase1 Thunder Burnt Strike 雷燃烧击",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(40164)$"])]

        public void Phase1_Thunder_Burnt_Strike_雷燃烧击(Event @event, ScriptAccessory accessory)
        {

            if (parse!=1)
            {

                return;

            }

            if (!ParseObjectId(@event["SourceId"], out var sourceId))
            {

                return;

            }

            var currentProperty = accessory.Data.GetDefaultDrawProperties();

            currentProperty.Name = "Phase1_Second_Strike_Of_Thunder_Burnt_Strike_雷燃烧击第二击";
            currentProperty.Scale = new(20, 40);
            currentProperty.Owner = sourceId;
            currentProperty.Color = Phase1_Colour_Of_Burnt_Strike_Characteristics.V4.WithW(1f);
            currentProperty.Delay = 4000;
            currentProperty.DestoryAt = 5750;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, currentProperty);

            currentProperty = accessory.Data.GetDefaultDrawProperties();

            currentProperty.Name = "Phase1_First_Strike_Of_Thunder_Burnt_Strike_雷燃烧击第一击";
            currentProperty.Scale = new(10, 40);
            currentProperty.Owner = sourceId;
            currentProperty.Color = accessory.Data.DefaultDangerColor.WithW(3f);
            currentProperty.Delay = 4000;
            currentProperty.DestoryAt = 3750;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, currentProperty);

        }

        [ScriptMethod(name: "Phase1 Fire Burnt Strike 火燃烧击",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(40161)$"])]

        public void Phase1_Fire_Burnt_Strike_火燃烧击(Event @event, ScriptAccessory accessory)
        {

            if (parse!=1)
            {

                return;

            }

            if (!ParseObjectId(@event["SourceId"], out var sourceId))
            {

                return;

            }

            var currentProperty = accessory.Data.GetDefaultDrawProperties();

            currentProperty.Name = "Phase1_First_Strike_Of_Fire_Burnt_Strike_火燃烧击第一击";
            currentProperty.Scale = new(10, 40);
            currentProperty.Owner = sourceId;
            currentProperty.Color = accessory.Data.DefaultDangerColor.WithW(3f);
            currentProperty.Delay = 4000;
            currentProperty.DestoryAt = 3750;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, currentProperty);

            currentProperty = accessory.Data.GetDefaultDrawProperties();

            currentProperty.Name = "Phase1_Central_Axis_Of_Fire_Burnt_Strike_火燃烧击中轴线";
            currentProperty.Scale = new(0.5f, 40f);
            currentProperty.Owner = sourceId;
            currentProperty.Color = Phase1_Colour_Of_Burnt_Strike_Characteristics.V4.WithW(25f);
            currentProperty.Delay = 4000;
            currentProperty.DestoryAt = 5750;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, currentProperty);

            for (int i = 6; i <= 34; i += 7)
            {

                currentProperty = accessory.Data.GetDefaultDrawProperties();

                currentProperty.Name = "Phase1_Knockback_Direction_Of_Fire_Burnt_Strike_火燃烧击击退方向";
                currentProperty.Scale = new(1f, 1.618f);
                currentProperty.Owner = sourceId;
                currentProperty.Color = Phase1_Colour_Of_Burnt_Strike_Characteristics.V4.WithW(1f);
                currentProperty.Offset = new Vector3(-5.382f, 0, -i);
                currentProperty.Rotation = float.Pi / 2;
                currentProperty.Delay = 4000;
                currentProperty.DestoryAt = 5750;

                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Arrow, currentProperty);

                currentProperty = accessory.Data.GetDefaultDrawProperties();

                currentProperty.Name = "Phase1_Knockback_Direction_Of_Fire_Burnt_Strike_火燃烧击击退方向";
                currentProperty.Scale = new(1f, 1.618f);
                currentProperty.Owner = sourceId;
                currentProperty.Color = Phase1_Colour_Of_Burnt_Strike_Characteristics.V4.WithW(1f);
                currentProperty.Offset = new Vector3(5.382f, 0, -i);
                currentProperty.Rotation = -(float.Pi / 2);
                currentProperty.Delay = 4000;
                currentProperty.DestoryAt = 5750;

                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Arrow, currentProperty);

            }

        }
        
        [ScriptMethod(name: "P1-乐园绝技-光轮范围", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4015[23])$"])]
        public void P1_乐园绝技_光轮范围(Event evt, ScriptAccessory sa)
        {
            if (!ParseObjectId(@evt["SourceId"], out var sid)) return;
            //var sid = evt.SourceId();
            var delay = 4000;
            var dp = sa.Data.GetDefaultDrawProperties();
            dp.Name = "P1-乐园绝技-光轮范围";
            dp.Owner = sid;
            dp.Scale = new(evt["ActionId"] == "40152" ? 5: 10);
            dp.Color = sa.Data.DefaultDangerColor;
            dp.Delay = delay;
            dp.DestoryAt = 8000 - delay;
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        [ScriptMethod(name: "P1_转轮召_抓人记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:4165"], userControl: false)]
        public void P1_转轮召_抓人记录(Event @event, ScriptAccessory accessory)
        {
            if (parse!=1) return;
            if (!ParseObjectId(@event["TargetId"], out var tid)) return;
            lock (this)
            {
                P1转轮召抓人[accessory.Data.PartyList.IndexOf(((uint)tid))] = 1;
            }
        }

        [ScriptMethod(name: "Phase1 Stack Range Of Turn Of The Heavens 光轮召唤分摊范围",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(40152)$"])]

        public void Phase1_Stack_Range_Of_Turn_Of_The_Heavens_光轮召唤分摊范围(Event @event, ScriptAccessory accessory)
        {

            if (parse!=1)
            {

                return;

            }

            var currentPosition = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);

            if (MathF.Abs(currentPosition.Z - 100) > 1)
            {

                return;

            }

            // 和自己同组（同北或同南）的 Prey 画安全色，另一个画危险色
            int o1 = P1转轮召抓人.IndexOf(1);
            int o2 = P1转轮召抓人.LastIndexOf(1);
            if (o1 < 0) return;
            var north = P1光轮召唤LPDU北组(o1, o2);
            bool meNorth = north.Contains(accessory.Data.PartyList.IndexOf(accessory.Data.Me));

            foreach (var prey in (int[])[o1, o2])
            {

                var currentProperty = accessory.Data.GetDefaultDrawProperties();

                currentProperty.Name = "Phase1_Stack_Range_Of_Turn_Of_The_Heavens_光轮召唤分摊范围";
                currentProperty.Scale = new(6);
                currentProperty.Owner = accessory.Data.PartyList[prey];
                currentProperty.Color = north.Contains(prey) == meNorth ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
                currentProperty.Delay = 6000;
                currentProperty.DestoryAt = 5000;

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, currentProperty);

            }

        }

        [ScriptMethod(name: "P1_转轮召_击退处理位置", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(40152)$"])]
        public void P1_转轮召_击退处理位置(Event @event, ScriptAccessory accessory)
        {
            //dy 7
            if (parse!=1) return;
            if (!ParseObjectId(@event["SourceId"], out var sid)) return;
            var pos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
            if (MathF.Abs(pos.Z - 100) > 1) return;

            var atEast = pos.X - 100 > 1;
            var o1 = P1转轮召抓人.IndexOf(1);
            var o2 = P1转轮召抓人.LastIndexOf(1);
            var upGroup = P1光轮召唤LPDU北组(o1, o2);

            foreach (var 视角 in GuidanceIndices(accessory)) 指路(视角);

            void 指路(int 视角)
            {
                var myindex = 视角;
                var dealpos1 = new Vector3(atEast ? 105.5f : 94.5f, 0, upGroup.Contains(myindex) ? 93 : 107);
                var dealpos2 = new Vector3(atEast ? 102 : 98, 0, upGroup.Contains(myindex) ? 93 : 107);
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"P1_转轮召_击退处理位置1-{视角}";
                dp.Scale = new(2);
                dp.Owner = GuidanceOwner(accessory, 视角);
                dp.TargetPosition = dealpos1;
                dp.ScaleMode |= ScaleMode.YByDistance;
                dp.Color = accessory.Data.DefaultSafeColor;
                dp.DestoryAt = 4000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"P1_转轮召_击退处理位置2-{视角}";
                dp.Scale = new(2);
                dp.Position = dealpos1;
                dp.TargetPosition = dealpos2;
                dp.ScaleMode |= ScaleMode.YByDistance;
                dp.Color = accessory.Data.DefaultSafeColor;
                dp.DestoryAt = 4000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"P1_转轮召_击退处理位置3-{视角}";
                dp.Scale = new(2);
                dp.Owner = GuidanceOwner(accessory, 视角);
                dp.TargetPosition = dealpos2;
                dp.ScaleMode |= ScaleMode.YByDistance;
                dp.Color = accessory.Data.DefaultSafeColor;
                dp.Delay = 4000;
                dp.DestoryAt = 2000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);


            }
        }

        // LPDU 光轮召唤分组：G1(MT/H1/M1/R1) 北、G2(OT/H2/M2/R2) 南；两个 Prey 同组时，该组按 T>M>R>H 排第一的 Prey 去另一组，另一组的 T 换过来
        private static List<int> P1光轮召唤LPDU北组(int o1, int o2)
        {
            List<int> north = [0, 2, 4, 6];
            if (north.Contains(o1) != north.Contains(o2)) return north;
            static int 优先级(int i) => i switch { 0 or 1 => 0, 4 or 5 => 1, 6 or 7 => 2, _ => 3 };
            var mover = 优先级(o1) <= 优先级(o2) ? o1 : o2;
            if (north.Contains(o1)) { north.Remove(mover); north.Add(1); }
            else { north.Remove(0); north.Add(mover); }
            return north;
        }

        [ScriptMethod(name: "Phase1 Fall Of Faith Control 信仰崩塌(四连抓)控制",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(40170)$"],
            userControl: false)]

        public void Phase1_Fall_Of_Faith_Control_信仰崩塌控制(Event @event, ScriptAccessory accessory)
        {

            if (parse!=1)
            {

                return;

            }

            System.Threading.Thread.MemoryBarrier();

            ++phase1_timesBurnishedGloryWasCast;

            System.Threading.Thread.MemoryBarrier();

            if (Enable_Developer_Mode)
            {

                accessory.Method.SendChat($"""
                                           /e 
                                           phase1_timesBurnishedGloryWasCast={phase1_timesBurnishedGloryWasCast}
                                           
                                           """);

            }

            switch (phase1_timesBurnishedGloryWasCast)
            {

                case 1:
                    {

                        phase1_tetheredPlayersDuringFallOfFaith.Clear();

                        phase1_semaphoreOfShortPrompts = 0;
                        phase1_semaphoreOfDrawing = 0;
                        phase1_semaphoreOfTheFinalPrompt = 0;

                        System.Threading.Thread.MemoryBarrier();

                        phase1_isInFallOfFaith = true;

                        break;

                    }

                case 2:
                    {

                        phase1_isInFallOfFaith = false;

                        System.Threading.Thread.MemoryBarrier();

                        phase1_tetheredPlayersDuringFallOfFaith.Clear();

                        phase1_semaphoreOfShortPrompts = 0;
                        phase1_semaphoreOfDrawing = 0;
                        phase1_semaphoreOfTheFinalPrompt = 0;

                        break;

                    }

                default:
                    {

                        phase1_tetheredPlayersDuringFallOfFaith.Clear();

                        phase1_semaphoreOfShortPrompts = 0;
                        phase1_semaphoreOfDrawing = 0;
                        phase1_semaphoreOfTheFinalPrompt = 0;

                        break;
                        // Just a placeholder and should never be reached.

                    }

            }

        }

        [ScriptMethod(name: "Phase1 Record Tethered Players 记录被连线的玩家",
            eventType: EventTypeEnum.Tether,
            eventCondition: ["Id:regex:^(00F9|011F)$"],
            userControl: false)]

        public void Phase1_Record_Tethered_Players_记录被连线的玩家(Event @event, ScriptAccessory accessory)
        {

            if (parse!=1)
            {

                return;

            }

            if (!phase1_isInFallOfFaith)
            {

                return;

            }

            if (!ParseObjectId(@event["TargetId"], out var targetId))
            {

                return;

            }

            int targetIndex = accessory.Data.PartyList.IndexOf(((uint)targetId));
            var tetherType = (@event["Id"].Equals("00F9")) ? (10) : (20);
            // 10 stands for a fire tether.

            System.Threading.Thread.MemoryBarrier();

            phase1_tetheredPlayersDuringFallOfFaith.Add(tetherType + targetIndex);

            System.Threading.Thread.MemoryBarrier();

            phase1_semaphoreOfShortPrompts = 1;
            phase1_semaphoreOfDrawing = 1;
            phase1_semaphoreOfTheFinalPrompt = 1;

        }

        [ScriptMethod(name: "Phase1 Prompt The Type Of The Current Tether 提示当前连线的类型",
            eventType: EventTypeEnum.Tether,
            eventCondition: ["Id:regex:^(00F9|011F)$"])]

        public void Phase1_Prompt_The_Type_Of_The_Current_Tether_提示当前连线的类型(Event @event, ScriptAccessory accessory)
        {

            if (parse!=1)
            {

                return;

            }

            if (!phase1_isInFallOfFaith)
            {

                return;

            }

            while (System.Threading.Interlocked.CompareExchange(ref phase1_semaphoreOfShortPrompts, 0, 1) == 0)
            {

                System.Threading.Thread.Sleep(1);

            }

            System.Threading.Thread.MemoryBarrier();

            if (1 <= phase1_tetheredPlayersDuringFallOfFaith.Count && phase1_tetheredPlayersDuringFallOfFaith.Count <= 3)
            {

                bool isFireTether = (phase1_tetheredPlayersDuringFallOfFaith.Last() < 20);
                string prompt = "";

                if (isFireTether)
                {

                    if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                    {

                        prompt = "火";

                    }

                    if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                    {

                        prompt = "Fire";

                    }

                }

                else
                {

                    if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                    {

                        prompt = "雷";

                    }

                    if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                    {

                        prompt = "Thunder";

                    }

                }

                if (!prompt.Equals(""))
                {

                    if (Enable_Text_Prompts)
                    {

                        accessory.Method.TextInfo(prompt, 1000);

                    }

                    if (Enable_Vanilla_TTS || Enable_Daily_Routines_TTS)
                    {

                        accessory.TTS(prompt, Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);

                    }

                }

            }

        }

        [ScriptMethod(name: "Phase1 Range Of The Current Tether 当前连线的范围",
            eventType: EventTypeEnum.Tether,
            eventCondition: ["Id:regex:^(00F9|011F)$"])]

        public void Phase1_Range_Of_The_Current_Tether_当前连线的范围(Event @event, ScriptAccessory accessory)
        {

            if (parse!=1)
            {

                return;

            }

            if (!phase1_isInFallOfFaith)
            {

                return;

            }

            while (System.Threading.Interlocked.CompareExchange(ref phase1_semaphoreOfDrawing, 0, 1) == 0)
            {

                System.Threading.Thread.Sleep(1);

            }

            System.Threading.Thread.MemoryBarrier();

            bool isFireTether = (phase1_tetheredPlayersDuringFallOfFaith.Last() < 20);
            var currentProperty = accessory.Data.GetDefaultDrawProperties();

            if (isFireTether)
            {

                currentProperty = accessory.Data.GetDefaultDrawProperties();

                currentProperty.Name = "Phase1_Range_Of_The_Fire_Tether_火连线的范围";
                currentProperty.Scale = new(60);
                currentProperty.Radian = float.Pi / 2;
                currentProperty.Owner = accessory.Data.PartyList[(phase1_tetheredPlayersDuringFallOfFaith.Last() % 10)];
                currentProperty.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
                currentProperty.TargetOrderIndex = 1;
                currentProperty.Color = accessory.Data.DefaultDangerColor;
                currentProperty.Delay = 9500;
                currentProperty.DestoryAt = 3800;

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, currentProperty);

            }

            else
            {

                for (uint i = 1; i <= 3; ++i)
                {

                    currentProperty = accessory.Data.GetDefaultDrawProperties();

                    currentProperty.Name = "Phase1_Range_Of_The_Thunder_Tether_雷连线的范围";
                    currentProperty.Scale = new(60);
                    currentProperty.Radian = float.Pi / 3 * 2;
                    currentProperty.Owner = accessory.Data.PartyList[(phase1_tetheredPlayersDuringFallOfFaith.Last() % 10)];
                    currentProperty.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
                    currentProperty.TargetOrderIndex = i;
                    currentProperty.Color = accessory.Data.DefaultDangerColor;
                    currentProperty.Delay = 9500;
                    currentProperty.DestoryAt = 3800;

                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, currentProperty);

                }

            }

        }

        [ScriptMethod(name: "Phase1 Prompt All The Types Of Tethers 提示所有连线的类型",
            eventType: EventTypeEnum.Tether,
            eventCondition: ["Id:regex:^(00F9|011F)$"])]

        public void Phase1_Prompt_All_The_Types_Of_Tethers_提示所有连线的类型(Event @event, ScriptAccessory accessory)
        {

            if (parse!=1)
            {

                return;

            }

            if (!phase1_isInFallOfFaith)
            {

                return;

            }

            while (System.Threading.Interlocked.CompareExchange(ref phase1_semaphoreOfTheFinalPrompt, 0, 1) == 0)
            {

                System.Threading.Thread.Sleep(1);

            }

            System.Threading.Thread.MemoryBarrier();

            if (phase1_tetheredPlayersDuringFallOfFaith.Count != 4)
            {

                return;

            }

            var isFireTether = phase1_tetheredPlayersDuringFallOfFaith.Select(o => o < 20).ToList();

            if (isFireTether.Count != 4)
            {

                return;

            }

            string prompt = "";

            if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
            {

                prompt += (isFireTether[0]) ? "火" : "雷";

            }

            if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
            {

                prompt += (isFireTether[0]) ? "Fire" : "Thunder";

            }

            for (int i = 1; i < isFireTether.Count; ++i)
            {

                if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                {

                    prompt += (isFireTether[i]) ? ",火" : ",雷";

                }

                if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                {

                    prompt += (isFireTether[i]) ? ", Fire" : ", Thunder";

                }

            }

            if (!prompt.Equals(""))
            {

                if (Enable_Text_Prompts)
                {

                    accessory.Method.TextInfo(prompt, 13300);

                }

                if (Enable_Vanilla_TTS || Enable_Daily_Routines_TTS)
                {

                    accessory.TTS(prompt, Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);

                }

            }

        }

        [ScriptMethod(name: "P1_四连线_处理位置", eventType: EventTypeEnum.Tether, eventCondition: ["Id:regex:^(00F9|011F)$"])]
        public void P1_四连线_处理位置(Event @event, ScriptAccessory accessory)
        {
            if (!phase1_isInFallOfFaith) return;
            if (!ParseObjectId(@event["TargetId"], out var tid)) return;
            var dis = 2.5f;//距离点名人
            var far = 5.25f;//距离boss
            Task.Delay(334).ContinueWith(t =>
            {
                foreach (var 视角 in GuidanceIndices(accessory)) 指路(视角);

                void 指路(int 视角)
                {
                    var myindex = 视角;
                    Vector3 t1p1 = new(100, 0, 100 - far);
                    Vector3 t1p2 = new(100, 0, 100 - far - dis);
                    Vector3 t2p1 = new(100, 0, 100 + far);
                    Vector3 t2p2 = new(100, 0, 100 + far + dis);
                    Vector3 t3p1 = new(100, 0, 100 - far - dis);
                    Vector3 t3p2 = new(100, 0, 100 - far);
                    Vector3 t4p1 = new(100, 0, 100 + far + dis);
                    Vector3 t4p2 = new(100, 0, 100 + far);

                    if (phase1_tetheredPlayersDuringFallOfFaith.Count == 1 && tid == GuidanceOwner(accessory, 视角))
                    {
                        var dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Name = $"P1_四连线_线1处理位置1-{视角}";
                        dp.Scale = new(2);
                        dp.Owner = GuidanceOwner(accessory, 视角);
                        dp.TargetPosition = t1p1;
                        dp.ScaleMode |= ScaleMode.YByDistance;
                        dp.Color = accessory.Data.DefaultSafeColor;
                        dp.DestoryAt = 13000;
                        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                        dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Name = $"P1_四连线_线1处理位置2-{视角}";
                        dp.Scale = new(2);
                        dp.Owner = GuidanceOwner(accessory, 视角);
                        dp.TargetPosition = t1p2;
                        dp.ScaleMode |= ScaleMode.YByDistance;
                        dp.Color = accessory.Data.DefaultSafeColor;
                        dp.Delay = 13000;
                        dp.DestoryAt = 6000;
                        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                        if (t1p1 != t1p2)
                        {
                            dp = accessory.Data.GetDefaultDrawProperties();
                            dp.Name = $"P1_四连线_线1处理位置2预指-{视角}";
                            dp.Scale = new(2);
                            dp.Position = t1p1;
                            dp.TargetPosition = t1p2;
                            dp.ScaleMode |= ScaleMode.YByDistance;
                            dp.Color = accessory.Data.DefaultDangerColor;
                            dp.DestoryAt = 13000;
                            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                        }
                    }
                    if (phase1_tetheredPlayersDuringFallOfFaith.Count == 2 && tid == GuidanceOwner(accessory, 视角))
                    {
                        var dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Name = $"P1_四连线_线2处理位置1-{视角}";
                        dp.Scale = new(2);
                        dp.Owner = GuidanceOwner(accessory, 视角);
                        dp.TargetPosition = t2p1;
                        dp.ScaleMode |= ScaleMode.YByDistance;
                        dp.Color = accessory.Data.DefaultSafeColor;
                        dp.DestoryAt = 13500;
                        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                        dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Name = $"P1_四连线_线2处理位置2-{视角}";
                        dp.Scale = new(2);
                        dp.Owner = GuidanceOwner(accessory, 视角);
                        dp.TargetPosition = t2p2;
                        dp.ScaleMode |= ScaleMode.YByDistance;
                        dp.Color = accessory.Data.DefaultSafeColor;
                        dp.Delay = 13500;
                        dp.DestoryAt = 5000;
                        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                        if (t2p1 != t2p2)
                        {
                            dp = accessory.Data.GetDefaultDrawProperties();
                            dp.Name = $"P1_四连线_线2处理位置2预指-{视角}";
                            dp.Scale = new(2);
                            dp.Position = t2p1;
                            dp.TargetPosition = t2p2;
                            dp.ScaleMode |= ScaleMode.YByDistance;
                            dp.Color = accessory.Data.DefaultDangerColor;
                            dp.DestoryAt = 13500;
                            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                        }
                    }
                    if (phase1_tetheredPlayersDuringFallOfFaith.Count == 3 && tid == GuidanceOwner(accessory, 视角))
                    {
                        var dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Name = $"P1_四连线_线3处理位置1-{视角}";
                        dp.Scale = new(2);
                        dp.Owner = GuidanceOwner(accessory, 视角);
                        dp.TargetPosition = t3p1;
                        dp.ScaleMode |= ScaleMode.YByDistance;
                        dp.Color = accessory.Data.DefaultSafeColor;
                        dp.DestoryAt = 7500;
                        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                        dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Name = $"P1_四连线_线3处理位置2-{视角}";
                        dp.Scale = new(3);
                        dp.Owner = GuidanceOwner(accessory, 视角);
                        dp.TargetPosition = t3p2;
                        dp.ScaleMode |= ScaleMode.YByDistance;
                        dp.Color = accessory.Data.DefaultSafeColor;
                        dp.Delay = 7500;
                        dp.DestoryAt = 6000;
                        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                        if (t3p1 != t3p2)
                        {
                            dp = accessory.Data.GetDefaultDrawProperties();
                            dp.Name = $"P1_四连线_线3处理位置2预指-{视角}";
                            dp.Scale = new(2);
                            dp.Position = t3p1;
                            dp.TargetPosition = t3p2;
                            dp.ScaleMode |= ScaleMode.YByDistance;
                            dp.Color = accessory.Data.DefaultDangerColor;
                            dp.DestoryAt = 7500;
                            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                        }
                    }
                    if (phase1_tetheredPlayersDuringFallOfFaith.Count == 4 && tid == GuidanceOwner(accessory, 视角))
                    {
                        var dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Name = $"P1_四连线_线4处理位置1-{视角}";
                        dp.Scale = new(2);
                        dp.Owner = GuidanceOwner(accessory, 视角);
                        dp.TargetPosition = t4p1;
                        dp.ScaleMode |= ScaleMode.YByDistance;
                        dp.Color = accessory.Data.DefaultSafeColor;
                        dp.DestoryAt = 8500;
                        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                        dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Name = $"P1_四连线_线4处理位置2-{视角}";
                        dp.Scale = new(3);
                        dp.Owner = GuidanceOwner(accessory, 视角);
                        dp.TargetPosition = t4p2;
                        dp.ScaleMode |= ScaleMode.YByDistance;
                        dp.Color = accessory.Data.DefaultSafeColor;
                        dp.Delay = 8500;
                        dp.DestoryAt = 5000;
                        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                        if (t4p1 != t4p2)
                        {
                            dp = accessory.Data.GetDefaultDrawProperties();
                            dp.Name = $"P1_四连线_线4处理位置2预指-{视角}";
                            dp.Scale = new(2);
                            dp.Position = t4p1;
                            dp.TargetPosition = t4p2;
                            dp.ScaleMode |= ScaleMode.YByDistance;
                            dp.Color = accessory.Data.DefaultDangerColor;
                            dp.DestoryAt = 8500;
                            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                        }
                    }
                    if (phase1_tetheredPlayersDuringFallOfFaith.Count == 4)
                    {
                        var tehterObjIndex = phase1_tetheredPlayersDuringFallOfFaith.Select(o => o % 10).ToList();
                        var tehterIsFire = phase1_tetheredPlayersDuringFallOfFaith.Select(o => o < 20).ToList();
                        // 闲人按 H1 H2 MT OT M1 M2 R1 R2 排队：1、2 去北，3、4 去南
                        List<int> idleObjIndex = ((List<int>)[2, 3, 0, 1, 4, 5, 6, 7]).Where(i => !tehterObjIndex.Contains(i)).ToList();

                        if (!idleObjIndex.Contains(myindex)) return;

                        // 面向 Boss 左侧高优先级：北侧雷线时优先级高的在东，南侧在西；火线闲人站线后
                        Vector3 i1p1 = tehterIsFire[0] ? new(100, 0, 100 - far - dis) : new(100 + dis, 0, 100 - far);
                        Vector3 i1p2 = tehterIsFire[2] ? new(100, 0, 100 - far - dis) : new(100 + dis, 0, 100 - far);
                        Vector3 i2p1 = tehterIsFire[0] ? new(100, 0, 100 - far - dis) : new(100 - dis, 0, 100 - far);
                        Vector3 i2p2 = tehterIsFire[2] ? new(100, 0, 100 - far - dis) : new(100 - dis, 0, 100 - far);
                        Vector3 i3p1 = tehterIsFire[1] ? new(100, 0, 100 + far + dis) : new(100 - dis, 0, 100 + far);
                        Vector3 i3p2 = tehterIsFire[3] ? new(100, 0, 100 + far + dis) : new(100 - dis, 0, 100 + far);
                        Vector3 i4p1 = tehterIsFire[1] ? new(100, 0, 100 + far + dis) : new(100 + dis, 0, 100 + far);
                        Vector3 i4p2 = tehterIsFire[3] ? new(100, 0, 100 + far + dis) : new(100 + dis, 0, 100 + far);
                        Vector3 dealpos1 = default;
                        Vector3 dealpos2 = default;

                        dealpos1 = idleObjIndex.IndexOf(myindex) switch
                        {
                            0 => i1p1,
                            1 => i2p1,
                            2 => i3p1,
                            3 => i4p1,
                        };
                        dealpos2 = idleObjIndex.IndexOf(myindex) switch
                        {
                            0 => i1p2,
                            1 => i2p2,
                            2 => i3p2,
                            3 => i4p2,
                        };
                        var upgroup = (idleObjIndex.IndexOf(myindex) == 0 || idleObjIndex.IndexOf(myindex) == 1);

                        var dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Name = $"P1_四连线_处理位置1-{视角}";
                        dp.Scale = new(2);
                        dp.Owner = GuidanceOwner(accessory, 视角);
                        dp.TargetPosition = dealpos1;
                        dp.ScaleMode |= ScaleMode.YByDistance;
                        dp.Color = accessory.Data.DefaultSafeColor;
                        dp.DestoryAt = upgroup ? 5000 : 8500;
                        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                        dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Name = $"P1_四连线_处理位置2-{视角}";
                        dp.Scale = new(2);
                        dp.Owner = GuidanceOwner(accessory, 视角);
                        dp.TargetPosition = dealpos2;
                        dp.ScaleMode |= ScaleMode.YByDistance;
                        dp.Color = accessory.Data.DefaultSafeColor;
                        dp.Delay = upgroup ? 5000 : 8500;
                        dp.DestoryAt = upgroup ? 6000 : 5000;
                        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                        if (dealpos1 != dealpos2)
                        {
                            dp = accessory.Data.GetDefaultDrawProperties();
                            dp.Name = $"P1_四连线_处理位置2预指-{视角}";
                            dp.Scale = new(2);
                            dp.Position = dealpos1;
                            dp.TargetPosition = dealpos2;
                            dp.ScaleMode |= ScaleMode.YByDistance;
                            dp.Color = accessory.Data.DefaultDangerColor;
                            dp.DestoryAt = upgroup ? 5000 : 8500;
                            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                        }
                    }
                }
            });
        }

        [ScriptMethod(name: "P1_塔_塔记录器", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4012[234567]|4013[15])$"], userControl: false)]
        public void P1_塔_塔记录器(Event @event, ScriptAccessory accessory)
        {
            if (parse!=1) return;
            lock (this)
            {
                var pos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
                var count = @event["ActionId"] switch
                {
                    "40135" => 1,
                    "40131" => 1,
                    "40122" => 2,
                    "40123" => 3,
                    "40124" => 4,
                    "40125" => 2,
                    "40126" => 3,
                    "40127" => 4,
                };
                if (MathF.Abs(pos.Z - 100) < 1)
                {
                    P1塔[1] = count;
                }
                else
                {
                    if (pos.Z - 100 > 1) P1塔[2] = count;
                    else P1塔[0] = count;
                }
                if (pos.X - 100 > 1)
                {
                    P1塔[3] = 1;
                }
            }
        }

        [ScriptMethod(name: "Phase1 Burnt Strike With Towers And Tank Busters 带有塔和死刑的火燃烧击(最后踩塔)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(40134|40129)$"])]

        public void Phase1_Burnt_Strike_With_Towers_And_Tank_Busters_带有塔和死刑的燃烧击(Event @event, ScriptAccessory accessory)
        {

            if (parse!=1)
            {

                return;

            }

            if (!ParseObjectId(@event["SourceId"], out var sourceId))
            {

                return;

            }

            if (@event["ActionId"].Equals("40134"))
            {
                // Thunder Burnt Strike.

                var currentProperty = accessory.Data.GetDefaultDrawProperties();

                currentProperty.Name = "Phase1_Second_Strike_Of_Thunder_Burnt_Strike_雷燃烧击第二击";
                currentProperty.Scale = new(20, 40);
                currentProperty.Owner = sourceId;
                currentProperty.Color = Phase1_Colour_Of_Burnt_Strike_Characteristics.V4.WithW(1f);
                currentProperty.DestoryAt = 8200;

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, currentProperty);

                currentProperty = accessory.Data.GetDefaultDrawProperties();

                currentProperty.Name = "Phase1_First_Strike_Of_Thunder_Burnt_Strike_雷燃烧击第一击";
                currentProperty.Scale = new(10, 40);
                currentProperty.Owner = sourceId;
                currentProperty.Color = accessory.Data.DefaultDangerColor.WithW(3f);
                currentProperty.DestoryAt = 6500;

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, currentProperty);

            }

            if (@event["ActionId"].Equals("40129"))
            {
                // Fire Burnt Strike.

                var currentProperty = accessory.Data.GetDefaultDrawProperties();

                currentProperty.Name = "Phase1_First_Strike_Of_Fire_Burnt_Strike_火燃烧击第一击";
                currentProperty.Scale = new(10, 40);
                currentProperty.Owner = sourceId;
                currentProperty.Color = accessory.Data.DefaultDangerColor.WithW(3f);
                currentProperty.DestoryAt = 6500;

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, currentProperty);

                currentProperty = accessory.Data.GetDefaultDrawProperties();

                currentProperty.Name = "Phase1_Central_Axis_Of_Fire_Burnt_Strike_火燃烧击中轴线";
                currentProperty.Scale = new(0.5f, 40f);
                currentProperty.Owner = sourceId;
                currentProperty.Color = Phase1_Colour_Of_Burnt_Strike_Characteristics.V4.WithW(25f);
                currentProperty.DestoryAt = 8200;

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, currentProperty);

                for (int i = -4; i <= 4; ++i)
                {

                    currentProperty = accessory.Data.GetDefaultDrawProperties();

                    currentProperty.Name = "Phase1_Knockback_Direction_Of_Fire_Burnt_Strike_火燃烧击击退方向";
                    currentProperty.Scale = new(1f, 1.618f);
                    currentProperty.Owner = sourceId;
                    currentProperty.Color = Phase1_Colour_Of_Burnt_Strike_Characteristics.V4.WithW(1f);
                    currentProperty.Offset = new Vector3(-5.382f, 0, (float)(-(i * 4.595d)));
                    currentProperty.Rotation = float.Pi / 2;
                    currentProperty.DestoryAt = 8200;

                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Arrow, currentProperty);

                    currentProperty = accessory.Data.GetDefaultDrawProperties();

                    currentProperty.Name = "Phase1_Knockback_Direction_Of_Fire_Burnt_Strike_火燃烧击击退方向";
                    currentProperty.Scale = new(1f, 1.618f);
                    currentProperty.Owner = sourceId;
                    currentProperty.Color = Phase1_Colour_Of_Burnt_Strike_Characteristics.V4.WithW(1f);
                    currentProperty.Offset = new Vector3(5.382f, 0, (float)(-(i * 4.595d)));
                    currentProperty.Rotation = -(float.Pi / 2);
                    currentProperty.DestoryAt = 8200;

                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Arrow, currentProperty);

                }

            }

        }

        [ScriptMethod(name: "P1_塔_塔处理位置", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(40134|40129)$"])]
        public void P1_塔_塔处理位置(Event @event, ScriptAccessory accessory)
        {
            if (parse!=1) return;
            Task.Delay(334).ContinueWith(t =>
            {
                foreach (var 视角 in GuidanceIndices(accessory)) 指路(视角);

                void 指路(int 视角)
                {
                    var myIndex = 视角;
                    if (@event["ActionId"] == "40134")
                    {
                        var eastTower = P1塔[3] == 1;
                        //雷
                        if (myIndex == 0 || myIndex == 1)
                        {
                            var dx = eastTower ? -10.5f : 10.5f;
                            var dy = myIndex == 0 ? -5.5f : 5.5f;
                            // The expression was myindex == 1 ? -5.5f : 5.5f before. Obviously it reverses the situation of MT and OT.
                            // The bug fix here credits to @alexandria_prime. Appreciate!
                            var dp = accessory.Data.GetDefaultDrawProperties();
                            dp.Name = $"P1_雷塔_塔处理位置_T-{视角}";
                            dp.Scale = new(2);
                            dp.Owner = GuidanceOwner(accessory, 视角);
                            dp.TargetPosition = new(100 + dx, 0, 100 + dy);
                            dp.ScaleMode |= ScaleMode.YByDistance;
                            dp.Color = accessory.Data.DefaultSafeColor;
                            dp.DestoryAt = 10500;
                            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                        }
                        else
                        {
                            // LPDU：雷塔直接进塔
                            var lpduTower = P1塔LPDU塔序号(myIndex);
                            if (lpduTower < 0) return;
                            Vector3 towerPosition = P1塔位置(lpduTower, eastTower);
                            Vector3 standbyPosition = towerPosition;

                            var dp = accessory.Data.GetDefaultDrawProperties();
                            dp.Name = $"P1_雷塔_塔处理位置_ND-{视角}";
                            dp.Scale = new(2);
                            dp.Owner = GuidanceOwner(accessory, 视角);
                            dp.TargetPosition = standbyPosition;
                            dp.ScaleMode |= ScaleMode.YByDistance;
                            dp.Color = accessory.Data.DefaultSafeColor;
                            dp.DestoryAt = 10500;
                            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                            dp = accessory.Data.GetDefaultDrawProperties();
                            dp.Name = $"P1_雷塔_塔_ND-{视角}";
                            dp.Scale = new(4);
                            dp.Position = towerPosition;
                            dp.Color = accessory.Data.DefaultSafeColor;
                            dp.DestoryAt = 10500;
                            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);

                        }
                    }
                    else
                    {
                        var eastTower = P1塔[3] == 1;
                        //火
                        if (myIndex == 0 || myIndex == 1)
                        {
                            var dx2 = eastTower ? -2f : 2f;
                            var dx1 = eastTower ? -5.5f : 5.5f;
                            var dy = myIndex == 0 ? -5.5f : 5.5f;
                            // The expression was myindex == 1 ? -5.5f : 5.5f before. Same background as the previous one.
                            // The bug fix here credits to @alexandria_prime. Appreciate!

                            var dp = accessory.Data.GetDefaultDrawProperties();
                            dp.Name = $"P1_火塔_塔处理位置_T1-{视角}";
                            // The name of the drawing here was once incorrectly labeled as towers with thunder Burnt Strike.
                            // Same situation for the other four names below.
                            // The corrections credit to @alexandria_prime. Appreciate!
                            dp.Scale = new(2);
                            dp.Owner = GuidanceOwner(accessory, 视角);
                            dp.TargetPosition = new(100 + dx1, 0, 100 + dy);
                            dp.ScaleMode |= ScaleMode.YByDistance;
                            dp.Color = accessory.Data.DefaultSafeColor;
                            dp.DestoryAt = 6500;
                            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                            dp = accessory.Data.GetDefaultDrawProperties();
                            dp.Name = $"P1_火塔_塔处理位置_T2-{视角}";
                            dp.Scale = new(2);
                            dp.Position = new(100 + dx1, 0, 100 + dy);
                            dp.TargetPosition = new(100 + dx2, 0, 100 + dy);
                            dp.ScaleMode |= ScaleMode.YByDistance;
                            dp.Color = accessory.Data.DefaultSafeColor;
                            dp.DestoryAt = 6500;
                            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                            dp = accessory.Data.GetDefaultDrawProperties();
                            dp.Name = $"P1_火塔_塔处理位置_T3-{视角}";
                            dp.Scale = new(2);
                            dp.Owner = GuidanceOwner(accessory, 视角);
                            dp.TargetPosition = new(100 + dx2, 0, 100 + dy);
                            dp.ScaleMode |= ScaleMode.YByDistance;
                            dp.Color = accessory.Data.DefaultSafeColor;
                            dp.Delay = 6500;
                            dp.DestoryAt = 1700;
                            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                        }
                        else
                        {
                            // LPDU：火塔先在场中同高度待机，击退后进塔
                            var lpduTower = P1塔LPDU塔序号(myIndex);
                            if (lpduTower < 0) return;
                            Vector3 towerPosition = P1塔位置(lpduTower, eastTower);
                            Vector3 standbyPosition = new(eastTower ? 102f : 98f, 0, towerPosition.Z);

                            var dp = accessory.Data.GetDefaultDrawProperties();
                            dp.Name = $"P1_火塔_塔处理位置_ND-{视角}";
                            dp.Scale = new(2);
                            dp.Owner = GuidanceOwner(accessory, 视角);
                            dp.TargetPosition = standbyPosition;
                            dp.ScaleMode |= ScaleMode.YByDistance;
                            dp.Color = accessory.Data.DefaultSafeColor;
                            dp.DestoryAt = 9000;
                            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                            dp = accessory.Data.GetDefaultDrawProperties();
                            dp.Name = $"P1_火塔_塔_ND-{视角}";
                            dp.Scale = new(4);
                            dp.Position = towerPosition;
                            dp.Color = accessory.Data.DefaultSafeColor;
                            dp.DestoryAt = 10500;
                            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);

                        }
                    }
                }
            });

        }

        // LPDU 踩塔：H1 北、R2 中、H2 南固定；M1 北、R1 中、M2 南补位，自己的塔只要 1 人时去要 3 人以上的那座（共 6 人，此时只有一座 ≥3）
        private int P1塔LPDU塔序号(int idx)
        {
            int[] 固定 = [2, 7, 3], 补位 = [4, 6, 5];
            var t = Array.IndexOf(固定, idx);
            if (t >= 0) return t;
            t = Array.IndexOf(补位, idx);
            if (t < 0) return -1;
            return P1塔[t] >= 2 ? t : P1塔.FindIndex(0, 3, n => n >= 3);
        }

        // P1 塔坐标：0 北、1 中、2 南；east 为塔在东侧
        private static Vector3 P1塔位置(int t, bool east)
            => new(t == 1 ? (east ? 115.98f : 84.02f) : (east ? 113.08f : 86.92f), 0, t switch { 0 => 90.81f, 1 => 100f, _ => 109.18f });

        #region P1 补丁 (Usami / Tetora)

        #region P1.1 乐园绝技

        [ScriptMethod(name: "乐园绝技阶段转换", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4015[45])$"],
            userControl: Debugging)]
        public void UtopianSkyPhaseChange(Event @event, ScriptAccessory accessory)
        {
            _fruPhase = FruPhase.P1A_UtopianSky;
            accessory.DebugMsg($"当前阶段为：{_fruPhase}", Enable_Developer_Mode);
        }

        #endregion P1.1 乐园绝技

        #region P1.2 罪壤堕

        [ScriptMethod(name: "光焰圆光阶段转换（DEBUG ONLY）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(40170)$"],
            userControl: Debugging)]
        public void BurnishedGloryPhaseChange(Event @event, ScriptAccessory accessory)
        {
            _fruPhase = _fruPhase switch
            {
                FruPhase.P1B_FallOfFaith => FruPhase.P1C_BurntStrike,
                _ => FruPhase.P1B_FallOfFaith
            };
            accessory.DebugMsg($"当前阶段为：{_fruPhase}", Enable_Developer_Mode);
        }

        #endregion 罪壤堕

        // 40167 罪炎：乐园绝技收尾，雷火线（罪壤堕）即将开始
        [ScriptMethod(name: "P1 雷火线排队提示", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:40167"], suppress: 1000)]
        public void P1雷火线排队提示(Event @event, ScriptAccessory accessory)
        {
            if (Enable_Text_Prompts) accessory.Method.TextInfo("雷火线排队", duration: 6000, true);
            accessory.TTS("雷火线排队", Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);
        }

        #endregion

        #endregion

        #region Phase_2

        [ScriptMethod(name: "----- Phase 2 ----- (No actual meaning for this toggle/此开关无实际意义)",
            eventType: EventTypeEnum.NpcYell,
            eventCondition: ["Your poor",
                            "给我你们贫穷的人"])]

        public void Phase2_Placeholder(Event @event, ScriptAccessory accessory) { }

        [ScriptMethod(name: "P2_换P", eventType: EventTypeEnum.Director, eventCondition: ["Instance:800375BF", "Command:8000001E"], userControl: false)]
        public void P2_换P(Event @event, ScriptAccessory accessory)
        {
            parse=2;
        }

        [ScriptMethod(name: "Phase2 Diamond Dust Initialization 钻石星尘初始化",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:40180"],
            userControl: false)]

        public void Phase2_Diamond_Dust_Initialization_钻石星尘初始化(Event @event, ScriptAccessory accessory)
        {

            parse=21;

            phase2_bossId = @event["SourceId"];
            Phase2_Positions_Of_Icicle_Impact.Clear();
            phase2_positionToBeKnockedBack = new Vector3[8];
            phase2_semaphoreOfGuidanceBeforeKnockback = new System.Threading.AutoResetEvent(false);
            phase2_semaphoreOfGuidanceAfterKnockback = new System.Threading.AutoResetEvent(false);

        }

        [ScriptMethod(name: "P2_钻石星尘_钢铁月环记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^((4020[23]))$"], userControl: false)]
        public void P2_钻石星尘_钢铁月环记录(Event @event, ScriptAccessory accessory)
        {
            P2DDDircle = (@event["ActionId"] == "40202");//钢铁
        }
        [ScriptMethod(name: "P2_钻石星尘_钢铁月环", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^((4020[23]))$"])]
        public void P2_钻石星尘_钢铁月环(Event @event, ScriptAccessory accessory)
        {
            if (!ParseObjectId(@event["SourceId"], out var sid)) return;
            if (@event["ActionId"] == "40202")//钢铁
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = "P2_钻石星尘_钢铁";
                dp.Scale = new(16);
                dp.Owner = sid;
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.DestoryAt = 6000;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }
            else
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = "P2_钻石星尘_月环";
                dp.Scale = new(20);
                dp.InnerScale = new(4);
                dp.Radian = float.Pi * 2;
                dp.Owner = sid;
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.DestoryAt = 6000;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
            }
        }
        [ScriptMethod(name: "P2_钻石星尘_扇形引导", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^((4020[23]))$"])]
        public void P2_钻石星尘_扇形引导(Event @event, ScriptAccessory accessory)
        {
            if (!ParseObjectId(@event["SourceId"], out var sid)) return;
            var dur = 3000;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P2_钻石星尘_扇形引导1";
            dp.Scale = new(60);
            dp.Radian = float.Pi / 6;
            dp.Owner = sid;
            dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
            dp.TargetOrderIndex = 1;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 7000 - dur;
            dp.DestoryAt = dur;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P2_钻石星尘_扇形引导2";
            dp.Scale = new(60);
            dp.Radian = float.Pi / 6;
            dp.Owner = sid;
            dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
            dp.TargetOrderIndex = 2;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 7000 - dur;
            dp.DestoryAt = dur;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P2_钻石星尘_扇形引导3";
            dp.Scale = new(60);
            dp.Radian = float.Pi / 6;
            dp.Owner = sid;
            dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
            dp.TargetOrderIndex = 3;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 7000 - dur;
            dp.DestoryAt = dur;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P2_钻石星尘_扇形引导4";
            dp.Scale = new(60);
            dp.Radian = float.Pi / 6;
            dp.Owner = sid;
            dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
            dp.TargetOrderIndex = 4;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 7000 - dur;
            dp.DestoryAt = dur;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);


        }
        [ScriptMethod(name: "P2_钻石星尘_冰花放置位置", eventType: EventTypeEnum.TargetIcon)]
        public void P2_钻石星尘_冰花放置位置(Event @event, ScriptAccessory accessory)
        {
            //accessory.Log.Debug($"{ParsTargetIcon(@event["Id"])}");
            if (ParsTargetIcon(@event["Id"]) != 127) return;
            if (parse!=21) return;
            if (!ParseObjectId(@event["TargetId"], out var tid)) return;
            if (!Debugging && tid != accessory.Data.Me) return;
            var myIndex = accessory.Data.PartyList.IndexOf((uint)tid);
            var rot = myIndex switch
            {
                0 => 6,
                1 => 0,
                2 => 4,
                3 => 2,
                4 => 4,
                5 => 2,
                6 => 6,
                7 => 0,
                _ => 0,
            };
            Vector3 epos1 = P2DDDircle ? new(119.5f, 0, 100.0f) : new(103.5f, 0, 100.0f);
            Vector3 epos2 = P2DDDircle ? new(119.5f, 0, 100.0f) : new(108.0f, 0, 100.0f);
            var dir8 = Phase2_Positions_Of_Icicle_Impact.FirstOrDefault() % 4;
            var dr = dir8 == 0 || dir8 == 2 ? -1 : 0;
            var dealpos1 = RotatePoint(epos1, new(100, 0, 100), float.Pi / 4 * (rot + dr));
            var dealpos2 = RotatePoint(epos2, new(100, 0, 100), float.Pi / 4 * (rot + dr));
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"P2_钻石星尘_冰花放置位置1-{myIndex}";
            dp.Scale = new(2);
            dp.ScaleMode |= ScaleMode.YByDistance;
            dp.Owner = GuidanceOwner(accessory, myIndex);
            dp.TargetPosition = dealpos1;
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.DestoryAt = 5500;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"P2_钻石星尘_冰花放置位置2-{myIndex}";
            dp.Scale = new(2);
            dp.ScaleMode |= ScaleMode.YByDistance;
            dp.Position = dealpos1;
            dp.TargetPosition = dealpos2;
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.DestoryAt = 5500;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"P2_钻石星尘_冰花放置位置3-{myIndex}";
            dp.Scale = new(2);
            dp.ScaleMode |= ScaleMode.YByDistance;
            dp.Owner = GuidanceOwner(accessory, myIndex);
            dp.TargetPosition = dealpos2;
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.Delay = 5500;
            dp.DestoryAt = 2500;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }

        [ScriptMethod(name: "Phase2 Frigid Needle 冰针(冰花)",
            eventType: EventTypeEnum.ActionEffect,
            eventCondition: ["ActionId:40199"])]

        public void Phase2_Frigid_Needle_冰针(Event @event, ScriptAccessory accessory)
        {

            if (parse!=21)
            {

                return;

            }

            Vector3 center = JsonConvert.DeserializeObject<Vector3>(@event["EffectPosition"]);
            var currentProperty = accessory.Data.GetDefaultDrawProperties();

            for (int i = 0; i <= 7; ++i)
            {

                currentProperty = accessory.Data.GetDefaultDrawProperties();

                currentProperty.Name = "Phase2_Frigid_Needle_冰针";
                currentProperty.Scale = new(5, 40);
                currentProperty.Position = center;
                currentProperty.Color = accessory.Data.DefaultDangerColor;
                currentProperty.Rotation = (float.Pi / 4) * i;
                currentProperty.Delay = 3250;
                currentProperty.DestoryAt = 4000;

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, currentProperty);

            }

        }

        [ScriptMethod(name: "P2_钻石星尘_扇形引导位置", eventType: EventTypeEnum.TargetIcon)]
        public void P2_钻石星尘_扇形引导位置(Event @event, ScriptAccessory accessory)
        {
            //accessory.Log.Debug($"{ParsTargetIcon(@event["Id"])}");
            if (ParsTargetIcon(@event["Id"]) != 127) return;
            if (parse!=21) return;
            if (!ParseObjectId(@event["TargetId"], out var tid)) return;
            foreach (var 视角 in GuidanceIndices(accessory)) 指路(视角);

            void 指路(int 视角)
            {
                var myIndex = 视角;
                int[] group = [6, 7, 4, 5, 2, 3, 0, 1];
                if (accessory.Data.PartyList.IndexOf(((uint)tid)) != group[myIndex]) return;
                var rot = myIndex switch
                {
                    0 => 6,
                    1 => 0,
                    2 => 4,
                    3 => 2,
                    4 => 4,
                    5 => 2,
                    6 => 6,
                    7 => 0,
                    _ => 0,
                };
                var dir8 = Phase2_Positions_Of_Icicle_Impact.FirstOrDefault() % 4;
                var dr = dir8 == 0 || dir8 == 2 ? 0 : -1;
                Vector3 epos = P2DDDircle ? new(116.5f, 0, 100f) : new(101f, 0, 100f);
                var dealpos = RotatePoint(epos, new(100, 0, 100), float.Pi / 4 * (rot + dr));
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"P2_钻石星尘_扇形引导位置-{视角}";
                dp.Scale = new(2);
                dp.ScaleMode |= ScaleMode.YByDistance;
                dp.Owner = GuidanceOwner(accessory, 视角);
                dp.TargetPosition = dealpos;
                dp.Color = accessory.Data.DefaultSafeColor;
                dp.DestoryAt = 6500;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }
        }

        [ScriptMethod(name: "Phase2 Record Positions Of Icicle Impact 记录冰柱冲击(冰圈)的位置",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:40198"],
            userControl: false)]

        public void Phase2_Record_Positions_Of_Icicle_Impact_记录冰柱冲击的位置(Event @event, ScriptAccessory accessory)
        {

            if (parse!=21)
            {

                return;

            }

            Vector3 currentPositions = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
            int proteanPosition = PositionTo8Dir(currentPositions, new(100, 0, 100));

            lock (Phase2_Positions_Of_Icicle_Impact)
            {

                Phase2_Positions_Of_Icicle_Impact.Add(proteanPosition);

            }

            if (Enable_Developer_Mode)
            {

                accessory.Method.SendChat($"""
                                           /e 
                                           currentPositions={currentPositions}
                                           proteanPosition={proteanPosition}
                                           
                                           """);

            }

        }

        [ScriptMethod(name: "Phase2 Determine The Position To Be Knocked Back 确定击退位置",
            eventType: EventTypeEnum.ActionEffect,
            eventCondition: ["ActionId:40199"],
            userControl: false,
            suppress: 2000)]

        public void Phase2_Determine_The_Position_To_Be_Knocked_Back_确定击退位置(Event @event, ScriptAccessory accessory)
        {

            if (parse!=21)
            {

                return;

            }

            if (Phase2_Positions_Of_Icicle_Impact.Count == 0)
            {

                return;

            }

            int firstIcicleImpact = Phase2_Positions_Of_Icicle_Impact.First() % 4;
            int rotation = firstIcicleImpact switch
            {
                0 => 2,
                1 => -1,
                2 => 0,
                3 => 1,
            };

            // 按位次各算一份：MT 组和 ST 组击退位置相对
            for (int i = 0; i < phase2_positionToBeKnockedBack.Length; ++i)
            {
                bool inStGroup = ((int[])[1, 3, 5, 7]).Contains(i);
                phase2_positionToBeKnockedBack[i] = RotatePoint(new Vector3(95, 0, 100), new(100, 0, 100), float.Pi / 4 * (rotation + (inStGroup ? 4 : 0)));
            }

            System.Threading.Thread.MemoryBarrier();

            phase2_semaphoreOfGuidanceBeforeKnockback.Set();
            phase2_semaphoreOfGuidanceAfterKnockback.Set();

        }

        [ScriptMethod(name: "Phase2 Guidance Of The Position To Be Knocked Back 击退位置指路",
            eventType: EventTypeEnum.ActionEffect,
            eventCondition: ["ActionId:40199"],
            suppress: 2000)]

        public void Phase2_Guidance_Of_The_Position_To_Be_Knocked_Back_击退位置指路(Event @event, ScriptAccessory accessory)
        {

            if (parse!=21)
            {

                return;

            }

            if (Phase2_Positions_Of_Icicle_Impact.Count == 0)
            {

                return;

            }

            System.Threading.Thread.MemoryBarrier();

            phase2_semaphoreOfGuidanceBeforeKnockback.WaitOne();

            System.Threading.Thread.MemoryBarrier();

            foreach (var 视角 in GuidanceIndices(accessory)) 指路(视角);

            void 指路(int 视角)
            {
                var currentProperty = accessory.Data.GetDefaultDrawProperties();

                currentProperty.Name = $"Phase2_Guidance_Of_The_Position_To_Be_Knocked_Back_击退位置指路-{视角}";
                currentProperty.Scale = new(2);
                currentProperty.ScaleMode |= ScaleMode.YByDistance;
                currentProperty.Owner = GuidanceOwner(accessory, 视角);
                currentProperty.TargetPosition = phase2_positionToBeKnockedBack[视角];
                currentProperty.Color = accessory.Data.DefaultSafeColor;
                currentProperty.DestoryAt = 4500;

                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, currentProperty);

            }
        }

        [ScriptMethod(name: "Phase2 Guidance After Knockback 击退后指路",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:40208"])]

        public void Phase2_Guidance_After_Knockback_击退后指路(Event @event, ScriptAccessory accessory)
        {

            if (parse!=21)
            {

                return;

            }

            if (Phase2_Positions_Of_Icicle_Impact.Count == 0)
            {

                return;

            }

            System.Threading.Thread.MemoryBarrier();

            phase2_semaphoreOfGuidanceAfterKnockback.WaitOne();

            System.Threading.Thread.MemoryBarrier();

            Vector3 positionOfTheReflection = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
            int proteanPositionOfTheReflection = PositionTo8Dir(positionOfTheReflection, new(100, 0, 100));
            foreach (var 视角 in GuidanceIndices(accessory)) 指路(视角);

            void 指路(int 视角)
            {
                int proteanPositionOfTheCurrentGroup = PositionTo8Dir(phase2_positionToBeKnockedBack[视角], new(100, 0, 100));
                int proteanPositionOfTheOppositeGroup = phase2_getOppositeProteanPosition(proteanPositionOfTheCurrentGroup);
                var currentProperty = accessory.Data.GetDefaultDrawProperties();
                string prompt = "";

                if (Enable_Developer_Mode)
                {

                    if (IsMe(accessory, 视角)) accessory.Method.SendChat($"""
                                               /e 
                                               positionOfTheReflection={positionOfTheReflection}
                                               proteanPositionOfTheReflection={proteanPositionOfTheReflection}
                                               proteanPositionOfTheCurrentGroup={proteanPositionOfTheCurrentGroup}
                                               proteanPositionOfTheOppositeGroup={proteanPositionOfTheOppositeGroup}

                                               """);

                }

                currentProperty.Name = $"Phase2_Guidance_After_Knockback_击退后指路-{视角}";
                currentProperty.Scale = new(20);
                currentProperty.InnerScale = new(19);
                currentProperty.Position = new Vector3(100, 0, 100);
                currentProperty.Rotation = float.Pi - (float.Pi / 4 * proteanPositionOfTheCurrentGroup);
                currentProperty.Color = accessory.Data.DefaultSafeColor.WithW(25f);
                currentProperty.DestoryAt = 14250;

                // LPDU：一律顺时针；神使的倒影在本组顺时针 45° 时只有本组改逆时针。两组都走到倒影的前后轴线为止
                int step = ((proteanPositionOfTheCurrentGroup + 1) % 8) == proteanPositionOfTheReflection ? -1 : 1;
                int rotationOfThePath = 1;

                while (((proteanPositionOfTheCurrentGroup + step * rotationOfThePath + 8) % 8) is var p
                      && p != proteanPositionOfTheReflection
                      && p != phase2_getOppositeProteanPosition(proteanPositionOfTheReflection))
                {

                    ++rotationOfThePath;

                }

                currentProperty.Radian = float.Pi / 4 * rotationOfThePath;
                currentProperty.Rotation += -step * (float.Pi / 4 * rotationOfThePath) / 2;

                rotationOfThePath *= 45;

                if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                {

                    prompt = $"{(step == 1 ? "顺时针" : "逆时针")}{rotationOfThePath}度";

                }

                if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                {

                    prompt = $"{(step == 1 ? "Clockwise" : "Counterclockwise")} {rotationOfThePath} degrees";

                }

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, currentProperty);

                if (!prompt.Equals(""))
                {

                    if (Enable_Text_Prompts)
                    {

                        if (IsMe(accessory, 视角)) accessory.Method.TextInfo(prompt, 9000);

                    }

                    if (Enable_Vanilla_TTS || Enable_Daily_Routines_TTS)
                    {

                        if (IsMe(accessory, 视角)) accessory.TTS(prompt, Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);

                    }

                }

            }
            if (!ParseObjectId(@event["SourceId"], out var sourceId))
            {

                return;

            }

            var currentProperty = accessory.Data.GetDefaultDrawProperties();

            currentProperty.Name = "Phase2_Front_Central_Axis_Of_Oracles_Reflection_神使的倒影前中轴线";
            currentProperty.Scale = new(0.5f, 50f);
            currentProperty.Owner = sourceId;
            currentProperty.Color = accessory.Data.DefaultDangerColor.WithW(25f);
            currentProperty.DestoryAt = 14250;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, currentProperty);

            currentProperty = accessory.Data.GetDefaultDrawProperties();

            currentProperty.Name = "Phase2_Rear_Separator_Of_Oracles_Reflection_神使的倒影背分割线";
            currentProperty.Scale = new(0.3f, 10f);
            currentProperty.Owner = sourceId;
            currentProperty.Rotation = float.Pi / 4 * 3;
            currentProperty.Color = accessory.Data.DefaultDangerColor.WithW(25f);
            currentProperty.DestoryAt = 14250;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, currentProperty);

            currentProperty = accessory.Data.GetDefaultDrawProperties();

            currentProperty.Name = "Phase2_Rear_Separator_Of_Oracles_Reflection_神使的倒影背分割线";
            currentProperty.Scale = new(0.3f, 10f);
            currentProperty.Owner = sourceId;
            currentProperty.Rotation = -(float.Pi / 4 * 3);
            currentProperty.Color = accessory.Data.DefaultDangerColor.WithW(25f);
            currentProperty.DestoryAt = 14250;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, currentProperty);

        }

        private int phase2_getOppositeProteanPosition(int currentProteanPosition)
        {

            return currentProteanPosition switch
            {
                0 => 4,
                1 => 5,
                2 => 6,
                3 => 7,
                4 => 0,
                5 => 1,
                6 => 2,
                7 => 3,
                _ => currentProteanPosition
            };

        }

        [ScriptMethod(name: "Phase2 Prediction Of Skating 滑冰预测",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:40208"])]

        public void Phase2_Prediction_Of_Skating_滑冰预测(Event @event, ScriptAccessory accessory)
        {

            if (parse!=21)
            {

                return;

            }

            foreach (var 视角 in GuidanceIndices(accessory)) 指路(视角);

            void 指路(int 视角)
            {
                var currentProperty = accessory.Data.GetDefaultDrawProperties();

                currentProperty.Name = $"Phase2_Prediction_Of_Skating_滑冰预测-{视角}";
                currentProperty.Scale = new(2f, 32f);
                currentProperty.Owner = GuidanceOwner(accessory, 视角);
                currentProperty.Color = accessory.Data.DefaultDangerColor.WithW(3f);
                currentProperty.Delay = 14250;
                currentProperty.DestoryAt = 9000;

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, currentProperty);

            }
        }

        [ScriptMethod(name: "P2_钻石星尘_连续剑范围", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^4019[34]$"])]
        public void P2_钻石星尘_连续剑范围(Event @event, ScriptAccessory accessory)
        {
            if (!ParseObjectId(@event["SourceId"], out var sid)) return;
            var time = 300;
            //93 先正面
            if (@event["ActionId"] == "40193")
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = "P2_钻石星尘_连续剑范围正1";
                dp.Scale = new(30);
                dp.Radian = float.Pi / 2 * 3;
                dp.Owner = sid;
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.DestoryAt = 3500 - time;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

                dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = "P2_钻石星尘_连续剑范围反2";
                dp.Scale = new(30);
                dp.Radian = float.Pi / 2;
                dp.Rotation = float.Pi;
                dp.Owner = sid;
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.Delay = 3500 - time;
                dp.DestoryAt = 2000;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
            }
            else
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = "P2_钻石星尘_连续剑范围反1";
                dp.Scale = new(30);
                dp.Radian = float.Pi / 2;
                dp.Rotation = float.Pi;
                dp.Owner = sid;
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.DestoryAt = 3500 - time;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

                dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = "P2_钻石星尘_连续剑范围正2";
                dp.Scale = new(30);
                dp.Radian = float.Pi / 2 * 3;
                dp.Owner = sid;
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.Delay = 3500 - time;
                dp.DestoryAt = 2000;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
            }
        }
        [ScriptMethod(name: "P2_钻石星尘_Boss背对", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^40208$", "TargetIndex:1"])]
        public void P2_钻石星尘_Boss背对(Event @event, ScriptAccessory accessory)
        {
            if (parse!=21) return;
            if (!ParseObjectId(phase2_bossId, out var sid)) return;

            foreach (var 视角 in GuidanceIndices(accessory)) 指路(视角);

            void 指路(int 视角)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"P2_钻石星尘_Boss背对-{视角}";
                dp.Scale = new(5);
                dp.Owner = GuidanceOwner(accessory, 视角);
                dp.TargetObject = sid;
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.Delay = 3000;
                dp.DestoryAt = 6000;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.SightAvoid, dp);


            }
        }

        [ScriptMethod(name: "Phase2 Reset Semaphores After Diamond Dust 钻石星尘后重置信号灯",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:40210"],
            userControl: false)]

        public void Phase2_Reset_Semaphores_After_Diamond_Dust_钻石星尘后重置信号灯(Event @event, ScriptAccessory accessory)
        {

            if (parse!=21
               &&
               parse!=22)
            {

                return;

            }

            phase2_semaphoreOfGuidanceBeforeKnockback = new System.Threading.AutoResetEvent(false);
            phase2_semaphoreOfGuidanceAfterKnockback = new System.Threading.AutoResetEvent(false);

        }

        [ScriptMethod(name: "Phase2 Mirror Mirror Initialization 镜中奇遇初始化",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:40179"],
            userControl: false)]

        public void Phase2_Mirror_Mirror_Initialization_镜中奇遇初始化(Event @event, ScriptAccessory accessory)
        {

            parse=22;

            phase2_proteanPositionOfTheColourlessMirror = -1;
            phase2_semaphoreTheColourlessMirrorWasConfirmed = new System.Threading.AutoResetEvent(false);
            phase2_proteanPositionsOfRedMirrors.Clear();
            phase2_semaphoreRedMirrorsWereConfirmed = new System.Threading.AutoResetEvent(false);

        }

        [ScriptMethod(name: "P2_双镜_分散分摊", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4022[01])$"])]
        public void P2_双镜_分散分摊(Event @event, ScriptAccessory accessory)
        {
            if (parse!=22) return;
            string prompt = "";
            if (@event["ActionId"] == "40221")
            {
                foreach (var pm in accessory.Data.PartyList)
                {
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = "P2_双镜_分散";
                    dp.Scale = new(5);
                    dp.Owner = pm;
                    dp.Color = accessory.Data.DefaultDangerColor;
                    dp.DestoryAt = 5000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                }

                if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                {

                    prompt = "分散";

                }

                if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                {

                    prompt = "Spread";

                }

            }
            else
            {
                // LPDU 双镜分摊搭档：MT+M2、OT+M1、H1+R1、H2+R2
                int[] group = [5, 4, 6, 7, 1, 0, 2, 3];
                var myindex = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
                for (int i = 0; i < 4; i++)
                {
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = "P2_双镜_分摊";
                    dp.Scale = new(5);
                    dp.Owner = accessory.Data.PartyList[i];
                    dp.Color = group[myindex] == i || i == myindex ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
                    dp.DestoryAt = 5000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                }

                if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                {

                    prompt = "分摊";

                }

                if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                {

                    prompt = "Stack";

                }

            }

            if (!prompt.Equals(""))
            {

                if (Enable_Text_Prompts)
                {

                    accessory.Method.TextInfo(prompt, 1500);

                }

                if (Enable_Vanilla_TTS || Enable_Daily_Routines_TTS)
                {

                    accessory.TTS(prompt, Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);

                }

            }

        }
        [ScriptMethod(name: "P2_双镜_蓝镜月环加引导", eventType: EventTypeEnum.EnvControl, eventCondition: ["DirectorId:800375BF", "State:00020001"])]
        public void P2_双镜_蓝镜月环加引导(Event @event, ScriptAccessory accessory)
        {
            if (parse!=22) return;
            if (!int.TryParse(@event["Index"], out var dir8)) return;
            Vector3 npos = new(100, 0, 80);
            dir8--;
            Vector3 dealpos = RotatePoint(npos, new(100, 0, 100), float.Pi / 4 * dir8);
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P2_双镜_蓝镜月环";
            dp.Scale = new(20);
            dp.InnerScale = new(4);
            dp.Radian = float.Pi * 2;
            dp.Position = dealpos;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 6000;
            dp.DestoryAt = 7000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P2_双镜_蓝镜扇形引导1";
            dp.Scale = new(60);
            dp.Radian = float.Pi / 6;
            dp.Position = dealpos;
            dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
            dp.TargetOrderIndex = 1;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 6000;
            dp.DestoryAt = 7000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P2_双镜_蓝镜扇形引导2";
            dp.Scale = new(60);
            dp.Radian = float.Pi / 6;
            dp.Position = dealpos;
            dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
            dp.TargetOrderIndex = 2;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 6000;
            dp.DestoryAt = 7000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P2_双镜_蓝镜扇形引导3";
            dp.Scale = new(60);
            dp.Radian = float.Pi / 6;
            dp.Position = dealpos;
            dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
            dp.TargetOrderIndex = 3;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 6000;
            dp.DestoryAt = 7000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P2_双镜_蓝镜扇形引导4";
            dp.Scale = new(60);
            dp.Radian = float.Pi / 6;
            dp.Position = dealpos;
            dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
            dp.TargetOrderIndex = 4;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 6000;
            dp.DestoryAt = 7000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

        }
        [ScriptMethod(name: "P2_双镜_红镜月环加引导", eventType: EventTypeEnum.EnvControl, eventCondition: ["DirectorId:800375BF", "State:02000100"])]
        public void P2_双镜_红月环加引导(Event @event, ScriptAccessory accessory)
        {
            if (parse!=22) return;
            if (!int.TryParse(@event["Index"], out var dir8)) return;
            Vector3 npos = new(100, 0, 80);
            dir8--;
            Vector3 dealpos = RotatePoint(npos, new(100, 0, 100), float.Pi / 4 * dir8);
            var dur = 4000;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P2_双镜_红镜月环";
            dp.Scale = new(20);
            dp.InnerScale = new(4);
            dp.Radian = float.Pi * 2;
            dp.Position = dealpos;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 17000;
            dp.DestoryAt = 6000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P2_双镜_红镜扇形引导1";
            dp.Scale = new(60);
            dp.Radian = float.Pi / 6;
            dp.Position = dealpos;
            dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
            dp.TargetOrderIndex = 1;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 23000 - dur;
            dp.DestoryAt = dur;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P2_双镜_红镜扇形引导2";
            dp.Scale = new(60);
            dp.Radian = float.Pi / 6;
            dp.Position = dealpos;
            dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
            dp.TargetOrderIndex = 2;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 23000 - dur;
            dp.DestoryAt = dur;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P2_双镜_红镜扇形引导3";
            dp.Scale = new(60);
            dp.Radian = float.Pi / 6;
            dp.Position = dealpos;
            dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
            dp.TargetOrderIndex = 3;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 23000 - dur;
            dp.DestoryAt = dur;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P2_双镜_红镜扇形引导4";
            dp.Scale = new(60);
            dp.Radian = float.Pi / 6;
            dp.Position = dealpos;
            dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
            dp.TargetOrderIndex = 4;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 23000 - dur;
            dp.DestoryAt = dur;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

        }

        [ScriptMethod(name: "Phase2 Determine The Protean Position Of The Colourless Mirror 确定无色镜子(蓝色镜子)八方位置",
            eventType: EventTypeEnum.EnvControl,
            eventCondition: ["DirectorId:800375BF", "State:00020001"],
            userControl: false)]

        public void Phase2_Determine_The_Protean_Position_Of_The_Colourless_Mirror_确定无色镜子八方位置(Event @event, ScriptAccessory accessory)
        {

            if (parse!=22)
            {

                return;

            }

            if (!int.TryParse(@event["Index"], out var proteanPosition))
            {

                return;

            }

            --proteanPosition;
            // The values of Index, which is from 1 to 8, coincidentally correspond to north, northeast, east, ..., northwest.

            phase2_proteanPositionOfTheColourlessMirror = proteanPosition;

            System.Threading.Thread.MemoryBarrier();

            phase2_semaphoreTheColourlessMirrorWasConfirmed.Set();

        }

        [ScriptMethod(name: "Phase2 Rough Guidance Of The Colourless Mirror 无色镜子(蓝色镜子)粗略指路",
            eventType: EventTypeEnum.EnvControl,
            eventCondition: ["DirectorId:800375BF", "State:00020001"])]

        public void Phase2_Rough_Guidance_Of_The_Colourless_Mirror_无色镜子粗略指路(Event @event, ScriptAccessory accessory)
        {

            if (parse!=22)
            {

                return;

            }

            if (!int.TryParse(@event["Index"], out var proteanPosition))
            {

                return;

            }

            --proteanPosition;

            foreach (var 视角 in GuidanceIndices(accessory)) 指路(视角);

            void 指路(int 视角)
            {
                int myIndex = 视角;
                Vector3 rawPosition = new(100, 0, 100);
                bool isMeleeGroup = true;

                if (myIndex == 0
                   ||
                   myIndex == 1
                   ||
                   myIndex == 4
                   ||
                   myIndex == 5)
                {

                    isMeleeGroup = true;
                    rawPosition = new(100, 0, 85);

                }

                if (myIndex == 2
                   ||
                   myIndex == 3
                   ||
                   myIndex == 6
                   ||
                   myIndex == 7)
                {

                    isMeleeGroup = false;
                    rawPosition = new(100, 0, 80.5f);

                }

                if (rawPosition.Equals(new Vector3(100, 0, 100)))
                {

                    return;

                }

                Vector3 targetPosition = RotatePoint(rawPosition, new(100, 0, 100), float.Pi / 4 * (proteanPosition + ((isMeleeGroup) ? (4) : (0))));

                var currentProperty = accessory.Data.GetDefaultDrawProperties();

                currentProperty.Name = $"Phase2_Rough_Guidance_Of_The_Colourless_Mirror_无色镜子粗略指路-{视角}";
                currentProperty.Scale = new(2);
                currentProperty.ScaleMode |= ScaleMode.YByDistance;
                currentProperty.Owner = GuidanceOwner(accessory, 视角);
                currentProperty.TargetPosition = targetPosition;
                currentProperty.Color = Phase2_Colour_Of_Mirror_Rough_Guidance.V4.WithW(1f);
                currentProperty.DestoryAt = 13000;

                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, currentProperty);

            }
            if (!ParseObjectId(phase2_bossId, out var bossId))
            {

                return;

            }

            var currentProperty = accessory.Data.GetDefaultDrawProperties();

            currentProperty.Name = "Phase2_Potential_Dangerous_Zone_Of_The_Colourless_Mirror_无色镜子潜在危险区";
            currentProperty.Scale = new(4);
            currentProperty.Radian = float.Pi;
            currentProperty.Owner = bossId;
            currentProperty.TargetPosition = RotatePoint(new Vector3(100,0,80),new Vector3(100,0,100),float.Pi/4*proteanPosition);
            currentProperty.Color = Phase2_Colour_Of_Potential_Dangerous_Zones.V4.WithW(3f);
            currentProperty.Delay = 6000;
            currentProperty.DestoryAt = 7000;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, currentProperty);

            currentProperty = accessory.Data.GetDefaultDrawProperties();

            currentProperty.Name = "Phase2_Potential_Dangerous_Zone_Of_The_Colourless_Mirror_无色镜子潜在危险区";
            currentProperty.Scale = new(4);
            currentProperty.Radian = float.Pi / 3;
            currentProperty.Position = RotatePoint(new Vector3(100,0,80),new Vector3(100,0,100),float.Pi/4*proteanPosition);
            currentProperty.TargetObject = bossId;
            currentProperty.Color = Phase2_Colour_Of_Potential_Dangerous_Zones.V4.WithW(3f);
            currentProperty.Delay = 6000;
            currentProperty.DestoryAt = 7000;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, currentProperty);

        }

        [ScriptMethod(name: "Phase2 Determine Protean Positions Of Red Mirrors 确定红色镜子八方位置",
            eventType: EventTypeEnum.EnvControl,
            eventCondition: ["DirectorId:800375BF", "State:02000100"],
            userControl: false)]

        public void Phase2_Determine_Protean_Positions_Of_Red_Mirrors_确定红色镜子八方位置(Event @event, ScriptAccessory accessory)
        {

            if (parse!=22)
            {

                return;

            }

            if (!int.TryParse(@event["Index"], out var proteanPosition))
            {

                return;

            }

            --proteanPosition;

            lock (phase2_proteanPositionsOfRedMirrors)
            {

                if (phase2_proteanPositionsOfRedMirrors.Count < 2)
                {

                    phase2_proteanPositionsOfRedMirrors.Add(proteanPosition);

                }

                if (phase2_proteanPositionsOfRedMirrors.Count == 2)
                {

                    phase2_semaphoreRedMirrorsWereConfirmed.Set();

                }

            }

        }

        [ScriptMethod(name: "Phase2 Rough Guidance Of Red Mirrors 红色镜子粗略指路",
            eventType: EventTypeEnum.EnvControl,
            eventCondition: ["DirectorId:800375BF", "State:02000100"],
            suppress: 2000)]

        public void Phase2_Rough_Guidance_Of_Red_Mirrors_红色镜子粗略指路(Event @event, ScriptAccessory accessory)
        {

            if (parse!=22)
            {

                return;

            }

            System.Threading.Thread.MemoryBarrier();

            phase2_semaphoreTheColourlessMirrorWasConfirmed.WaitOne();
            phase2_semaphoreRedMirrorsWereConfirmed.WaitOne();

            System.Threading.Thread.MemoryBarrier();

            if (phase2_proteanPositionOfTheColourlessMirror == -1)
            {

                return;

            }

            int colourlessMirror = phase2_proteanPositionOfTheColourlessMirror;

            if (phase2_proteanPositionsOfRedMirrors.Count != 2)
            {

                return;

            }

            int redMirror1 = phase2_proteanPositionsOfRedMirrors[0];
            int redMirror2 = phase2_proteanPositionsOfRedMirrors[1];
            int discreteDistanceToTheNext = 1;
            int leftMirror = -1;
            int rightMirror = -1;

            while (((redMirror1 + discreteDistanceToTheNext) % 8) != redMirror2)
            {

                ++discreteDistanceToTheNext;

            }

            if (discreteDistanceToTheNext != 2 && discreteDistanceToTheNext != 6)
            {

                return;

            }

            if (discreteDistanceToTheNext == 2)
            {

                leftMirror = redMirror1;
                rightMirror = redMirror2;

            }

            if (discreteDistanceToTheNext == 6)
            {

                leftMirror = redMirror2;
                rightMirror = redMirror1;

            }

            if (leftMirror == -1 || rightMirror == -1)
            {

                return;

            }

            if (Enable_Developer_Mode)
            {

                accessory.Method.SendChat($"""
                                           /e 
                                           leftMirror={leftMirror}
                                           rightMirror={rightMirror}
                                           discreteDistanceToTheNext={discreteDistanceToTheNext}
                                           
                                           """);

            }

            // 近战组（蓝镜对面）去最近的红镜，距离相同时去顺时针先碰到的那面；远程组去另一面
            int meleeGroup = phase2_getOppositeProteanPosition(colourlessMirror);
            int Clockwise(int mirror) => (mirror - meleeGroup + 8) % 8;
            int Distance(int mirror) => Math.Min(Clockwise(mirror), 8 - Clockwise(mirror));
            int meleeMirror = (Distance(redMirror1), Clockwise(redMirror1)).CompareTo((Distance(redMirror2), Clockwise(redMirror2))) <= 0 ? redMirror1 : redMirror2;
            int rangedMirror = meleeMirror == redMirror1 ? redMirror2 : redMirror1;

            // LPDU 第二组镜子逐人点位：（相对镜子方向的偏角，顺时针为正，度；离场中的半径，米），取自 raidplan 第 19 步
            // 近战镜：MT 往远程镜一侧单独一个点，M1 正中靠内，M2 在 M1 和 OT 之间，OT 贴墙在远离远程镜一侧
            // 远程镜：面向镜子 G1 在右、G2 在左，DPS 贴墙；靠近战那一侧那组的奶站正中，两个奶都稍微偏离近战
            int towardRanged = (meleeMirror + 2) % 8 == rangedMirror ? 1 : -1;
            bool meleeOnTheLeft = towardRanged == 1;   // 面向远程镜时近战镜在左手（逆时针）一侧
            (float degree, float radius) MirrorSpot(int i) => i switch
            {
                0 => (9 * towardRanged, 19.5f),
                1 => (-7 * towardRanged, 19.5f),
                4 => (0, 17.6f),
                5 => (-4 * towardRanged, 18.2f),
                6 => (11, 19.5f),
                7 => (-11, 19.5f),
                2 => ((meleeOnTheLeft ? 7 : 2) * towardRanged, meleeOnTheLeft ? 18.3f : 17.7f),
                _ => ((meleeOnTheLeft ? 2 : 7) * towardRanged, meleeOnTheLeft ? 17.7f : 18.3f),
            };

            foreach (var 视角 in GuidanceIndices(accessory)) 指路(视角);

            void 指路(int 视角)
            {
                bool isMeleeGroup = 视角 is 0 or 1 or 4 or 5;
                var (degree, radius) = MirrorSpot(视角);
                var currentProperty = accessory.Data.GetDefaultDrawProperties();

                currentProperty.Name = $"Phase2_Rough_Guidance_Of_Red_Mirrors_红色镜子粗略指路-{视角}";
                currentProperty.Scale = new(2);
                currentProperty.ScaleMode |= ScaleMode.YByDistance;
                currentProperty.Owner = GuidanceOwner(accessory, 视角);
                currentProperty.TargetPosition = RotatePoint(new Vector3(100, 0, 100 - radius), new(100, 0, 100),
                    float.Pi / 4 * (isMeleeGroup ? meleeMirror : rangedMirror) + float.DegreesToRadians(degree));
                currentProperty.Color = Phase2_Colour_Of_Mirror_Rough_Guidance.V4.WithW(1f);
                currentProperty.Delay = 13500;
                currentProperty.DestoryAt = 9500;

                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, currentProperty);

            }
            var currentProperty = accessory.Data.GetDefaultDrawProperties();

            currentProperty.Name = "Phase2_Potential_Dangerous_Zone_Of_Red_Mirrors_红色镜子潜在危险区";
            currentProperty.Scale = new(4);
            currentProperty.Radian = float.Pi / 3;
            currentProperty.Position = RotatePoint(new(100, 0, 80), new(100, 0, 100), float.Pi / 4 * leftMirror);
            currentProperty.Rotation = float.Pi / 6;
            currentProperty.TargetPosition = new Vector3(100, 0, 100);
            currentProperty.Color = Phase2_Colour_Of_Potential_Dangerous_Zones.V4.WithW(3f);
            currentProperty.Delay = 13500;
            currentProperty.DestoryAt = 10000;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, currentProperty);

            currentProperty = accessory.Data.GetDefaultDrawProperties();

            currentProperty.Name = "Phase2_Potential_Dangerous_Zone_Of_Red_Mirrors_红色镜子潜在危险区";
            currentProperty.Scale = new(4);
            currentProperty.Radian = float.Pi / 3;
            currentProperty.Position = RotatePoint(new(100, 0, 80), new(100, 0, 100), float.Pi / 4 * rightMirror);
            currentProperty.Rotation = -(float.Pi / 6);
            currentProperty.TargetPosition = new Vector3(100, 0, 100);
            currentProperty.Color = Phase2_Colour_Of_Potential_Dangerous_Zones.V4.WithW(3f);
            currentProperty.Delay = 13500;
            currentProperty.DestoryAt = 10000;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, currentProperty);

        }

        [ScriptMethod(name: "Phase2 Reset Semaphores After Mirror Mirror 镜中奇遇后重置信号灯",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:40212"],
            userControl: false)]

        public void Phase2_Reset_Semaphores_After_Mirror_Mirror_镜中奇遇后重置信号灯(Event @event, ScriptAccessory accessory)
        {

            if (parse!=22
               &&
               parse!=23)
            {

                return;

            }

            phase2_semaphoreTheColourlessMirrorWasConfirmed = new System.Threading.AutoResetEvent(false);
            phase2_semaphoreRedMirrorsWereConfirmed = new System.Threading.AutoResetEvent(false);

        }

        [ScriptMethod(name: "Phase2 Light Rampant Initialization 光之失控(光暴)初始化",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:40212"],
            userControl: false)]

        public void Phase2_Light_Rampant_Initialization_光之失控初始化(Event @event, ScriptAccessory accessory)
        {

            parse=23;

            phase2_playersWithLuminousHammer.Clear();
            phase2_semaphoreLuminousHammerWasConfirmed = new System.Threading.AutoResetEvent(false);
            phase2_stacksOfLightsteeped = [0, 0, 0, 0, 0, 0, 0, 0];
            phase2_writePermissionForLightsteeped = true;
            phase2_semaphoreFinalLightsteepedWasConfirmed = new System.Threading.AutoResetEvent(false);

        }

        [ScriptMethod(name: "Phase2 Initial Positions Before Light Rampant 光之失控(光暴)前初始站位",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:40212"])]

        public void Phase2_Initial_Positions_Before_Light_Rampant_光之失控前初始站位(Event @event, ScriptAccessory accessory)
        {

            if (parse!=22
               &&
               parse!=23)
            {

                return;

            }

            foreach (var 视角 in GuidanceIndices(accessory)) 指路(视角);

            void 指路(int 视角)
            {
                int myIndex = 视角;
                // LPDU 双排：北排从西到东 H1 H2 MT OT，南排从西到东 R1 R2 M1 M2（半径 5，相邻 45°）
                double rotation = -0.5d + myIndex switch
                {
                    2 => -1d,
                    3 => 0d,
                    0 => 1d,
                    1 => 2d,
                    5 => 3d,
                    4 => 4d,
                    7 => 5d,
                    _ => 6d
                };

                var currentproperty = accessory.Data.GetDefaultDrawProperties();

                currentproperty.Name = $"Phase2_Initial_Positions_Before_Light_Rampant_光之失控前初始站位-{视角}";
                currentproperty.Scale = new(2);
                currentproperty.Owner = GuidanceOwner(accessory, 视角);
                currentproperty.TargetPosition = RotatePoint(new Vector3(100, 0, 95), new Vector3(100, 0, 100), (float)(float.Pi / 4 * rotation)); ;
                currentproperty.ScaleMode |= ScaleMode.YByDistance;
                currentproperty.Color = accessory.Data.DefaultSafeColor;
                currentproperty.DestoryAt = 5000;

                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, currentproperty);

            }
        }

        [ScriptMethod(name: "Phase2 Determine Luminous Hammer During Light Rampant 光之失控(光暴)确定光流侵蚀(放泥)",
            eventType: EventTypeEnum.TargetIcon,
            userControl: false)]

        public void Phase2_Determine_Luminous_Hammer_During_Light_Rampant_光之失控确定光流侵蚀(Event @event, ScriptAccessory accessory)
        {
            if (ParsTargetIcon(@event["Id"]) != 157)
            {

                return;

            }

            if (parse!=23)
            {

                return;

            }

            if (!ParseObjectId(@event["TargetId"], out var targetId))
            {

                return;

            }

            int currentIndex = accessory.Data.PartyList.IndexOf(((uint)targetId));

            lock (phase2_playersWithLuminousHammer)
            {

                if (phase2_playersWithLuminousHammer.Count < 2)
                {

                    phase2_playersWithLuminousHammer.Add(currentIndex);

                }

                if (phase2_playersWithLuminousHammer.Count == 2)
                {

                    phase2_semaphoreLuminousHammerWasConfirmed.Set();

                }

            }

        }

        [ScriptMethod(name: "Phase2 Determine Stacks Of Lightsteeped During Light Rampant 光之失控(光暴)确定过量光层数",
            eventType: EventTypeEnum.StatusAdd,
            eventCondition: ["StatusID:2257"],
            userControl: false)]

        public void Phase2_Determine_Stacks_Of_Lightsteeped_During_Light_Rampant_光之失控确定过量光层数(Event @event, ScriptAccessory accessory)
        {

            if (parse!=23)
            {

                return;

            }

            if (!phase2_writePermissionForLightsteeped)
            {

                return;

            }

            if (!ParseObjectId(@event["TargetId"], out var targetId))
            {

                return;

            }

            if (!int.TryParse(@event["StackCount"], out var stacks))
            {

                return;

            }

            int currentIndex = accessory.Data.PartyList.IndexOf(((uint)targetId));

            lock (phase2_stacksOfLightsteeped)
            {

                phase2_stacksOfLightsteeped[currentIndex] = stacks;

            }

            if (Enable_Developer_Mode)
            {

                accessory.Method.SendChat($"""
                                           /e 
                                           currentIndex={currentIndex}
                                           stacks={stacks}

                                           """);

            }

        }

        [ScriptMethod(name: "Phase2 Disable The Write Permission For Lightsteeped 禁止写入过量光",
            eventType: EventTypeEnum.ActionEffect,
            eventCondition: ["ActionId:40218"],
            userControl: false)]

        public void Phase2_Disable_The_Write_Permission_For_Lightsteeped_禁止写入过量光(Event @event, ScriptAccessory accessory)
        {

            phase2_writePermissionForLightsteeped = false;

        }

        [ScriptMethod(name: "P2_光之暴走_分散分摊", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4022[01])$"])]
        public void P2_光之暴走_分散分摊(Event @event, ScriptAccessory accessory)
        {
            if (parse!=23) return;
            string prompt = "";
            if (@event["ActionId"] == "40221")
            {
                foreach (var pm in accessory.Data.PartyList)
                {
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = "P2_光之暴走_分散";
                    dp.Scale = new(5);
                    dp.Owner = pm;
                    dp.Color = accessory.Data.DefaultDangerColor;
                    dp.DestoryAt = 5000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                }

                if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                {

                    prompt = "分散";

                }

                if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                {

                    prompt = "Spread";

                }

            }
            else
            {
                int[] group = [6, 7, 4, 5, 2, 3, 0, 1];
                var myindex = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
                for (int i = 0; i < 4; i++)
                {
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = "P2_光之暴走_分摊";
                    dp.Scale = new(5);
                    dp.Owner = accessory.Data.PartyList[i];
                    dp.Color = group[myindex] == i || i == myindex ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
                    dp.DestoryAt = 5000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                }

                if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                {

                    prompt = "分摊";

                }

                if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                {

                    prompt = "Stack";

                }

            }

            if (!prompt.Equals(""))
            {

                if (Enable_Text_Prompts)
                {

                    accessory.Method.TextInfo(prompt, 1500);

                }

                if (Enable_Vanilla_TTS || Enable_Daily_Routines_TTS)
                {

                    accessory.TTS(prompt, Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);

                }

            }

        }
        [ScriptMethod(name: "P2_光之暴走_分摊buff", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:4159"])]
        public void P2_光之暴走_分摊buff(Event @event, ScriptAccessory accessory)
        {
            if (parse!=23) return;
            if (!ParseObjectId(@event["TargetId"], out var tid)) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P2_光之暴走_分摊buff";
            dp.Scale = new(5);
            dp.Owner = tid;
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.Delay = 12000;
            dp.DestoryAt = 5000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        [ScriptMethod(name: "Phase2 Guidance Of Towers During Light Rampant 光之失控(光暴)踩塔指路",
            eventType: EventTypeEnum.TargetIcon,
            suppress: 2000)]

        public void Phase2_Guidance_Of_Towers_During_Light_Rampant_光之失控踩塔指路(Event @event, ScriptAccessory accessory)
        {

            if (ParsTargetIcon(@event["Id"]) != 157)
            {

                return;

            }

            if (parse!=23)
            {

                return;

            }

            System.Threading.Thread.MemoryBarrier();

            phase2_semaphoreLuminousHammerWasConfirmed.WaitOne();

            System.Threading.Thread.MemoryBarrier();

            foreach (var 视角 in GuidanceIndices(accessory)) 指路(视角);

            void 指路(int 视角)
            {
                int myIndex = 视角;

                if (phase2_playersWithLuminousHammer.Contains(myIndex))
                {

                    return;

                }

                // 双排顺时针绕一圈：北排西→东 H1 H2 MT OT，再南排东→西 M2 M1 R2 R1，去掉两个放泥的人
                List<int> playersWithTethers = ((List<int>)[2, 3, 0, 1, 5, 4, 7, 6]).Where(i => !phase2_playersWithLuminousHammer.Contains(i)).ToList();
                int myTetherIndex = playersWithTethers.IndexOf(myIndex);

                Vector3 tower1 = new Vector3(100.00f, 0, 084.00f);
                // North.
                Vector3 tower2 = new Vector3(113.85f, 0, 092.00f);
                // Northeast.
                Vector3 tower3 = new Vector3(113.85f, 0, 108.00f);
                // Southeast.
                Vector3 tower4 = new Vector3(100.00f, 0, 116.00f);
                // South.
                Vector3 tower5 = new Vector3(086.14f, 0, 108.00f);
                // Southwest.
                Vector3 tower6 = new Vector3(086.14f, 0, 092.00f);
                // Northwest.

                // 两个放泥都在北排（蓝绿）时全员顺时针挪一位，南排最西的人补到西北；否则 3-3 不动。
                // 站位槽 西北/北/东北/东南/南/西南 → 塔：北↔南、西北↔东北对换，东南、西南不动
                int shift = phase2_playersWithLuminousHammer.All(i => i <= 3) ? 1 : 0;

                Vector3 myTower = ((myTetherIndex + shift) % 6) switch
                {
                    0 => tower2,
                    1 => tower4,
                    2 => tower6,
                    3 => tower3,
                    4 => tower1,
                    _ => tower5
                };

                // 北侧三塔去北偏东（NNE）集合分摊，南侧三塔去南偏西（SSW），之后顺时针转进安全区
                bool northSide = myTower == tower1 || myTower == tower2 || myTower == tower6;
                Vector3 myMeetingPoint = RotatePoint(new Vector3(100, 0, 82), new Vector3(100, 0, 100), float.Pi / 8 * (northSide ? 1 : 9));

                var currentProperty = accessory.Data.GetDefaultDrawProperties();

                currentProperty.Name = $"Phase2_Guidance_1_Of_Towers_During_Light_Rampant_光之失控踩塔指路1-{视角}";
                currentProperty.Scale = new(2);
                currentProperty.ScaleMode |= ScaleMode.YByDistance;
                currentProperty.Owner = GuidanceOwner(accessory, 视角);
                currentProperty.TargetPosition = myTower;
                currentProperty.Color = accessory.Data.DefaultSafeColor;
                currentProperty.DestoryAt = 10000;

                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, currentProperty);

                currentProperty = accessory.Data.GetDefaultDrawProperties();

                currentProperty.Name = $"Phase2_Highlight_Of_The_Tower_During_Light_Rampant_光之失控塔高亮-{视角}";
                currentProperty.Scale = new(4);
                currentProperty.Position = myTower;
                currentProperty.Color = accessory.Data.DefaultSafeColor;
                currentProperty.DestoryAt = 10000;

                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, currentProperty);

                currentProperty = accessory.Data.GetDefaultDrawProperties();

                currentProperty.Name = $"Phase2_Guidance_2_Preview_Of_Towers_During_Light_Rampant_光之失控踩塔指路2预览-{视角}";
                currentProperty.Scale = new(2);
                currentProperty.ScaleMode |= ScaleMode.YByDistance;
                currentProperty.Position = myTower;
                currentProperty.TargetPosition = myMeetingPoint;
                currentProperty.Color = accessory.Data.DefaultSafeColor;
                currentProperty.DestoryAt = 10000;

                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, currentProperty);

                currentProperty = accessory.Data.GetDefaultDrawProperties();

                currentProperty.Name = $"Phase2_Guidance_2_Of_Towers_During_Light_Rampant_光之失控踩塔指路2-{视角}";
                currentProperty.Scale = new(2);
                currentProperty.ScaleMode |= ScaleMode.YByDistance;
                currentProperty.Owner = GuidanceOwner(accessory, 视角);
                currentProperty.TargetPosition = myMeetingPoint;
                currentProperty.Color = accessory.Data.DefaultSafeColor;
                currentProperty.Delay = 10000;
                currentProperty.DestoryAt = 4000;

                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, currentProperty);

            }
        }

        [ScriptMethod(name: "Phase2 Determine Final Lightsteeped 确定最后的过量光",
            eventType: EventTypeEnum.EnvControl,
            eventCondition: ["DirectorId:800375BF", "State:00020001", "Index:00000015"],
            userControl: false,
            suppress: 2000)]

        public void Phase2_Determine_Final_Lightsteeped_确定最后的过量光(Event @event, ScriptAccessory accessory)
        {

            if (parse!=23)
            {

                return;

            }

            lock (phase2_playersWithLuminousHammer)
            {

                for (int i = 0; i < 8; ++i)
                {

                    lock (phase2_stacksOfLightsteeped)
                    {

                        ++phase2_stacksOfLightsteeped[i];

                    }

                }

            }

            lock (phase2_playersWithLuminousHammer)
            {

                for (int i = 0; i < 8; ++i)
                {

                    if (!phase2_playersWithLuminousHammer.Contains(i))
                    {

                        lock (phase2_stacksOfLightsteeped)
                        {

                            ++phase2_stacksOfLightsteeped[i];

                        }

                    }

                }

            }

            System.Threading.Thread.MemoryBarrier();

            phase2_semaphoreFinalLightsteepedWasConfirmed.Set();

            System.Threading.Thread.MemoryBarrier();

            if (Enable_Developer_Mode)
            {

                accessory.Method.SendChat($"""
                                           /e 
                                           phase2_stacksOfLightsteeped[]={phase2_stacksOfLightsteeped[0]},{phase2_stacksOfLightsteeped[1]},{phase2_stacksOfLightsteeped[2]},{phase2_stacksOfLightsteeped[3]},{phase2_stacksOfLightsteeped[4]},{phase2_stacksOfLightsteeped[5]},{phase2_stacksOfLightsteeped[6]},{phase2_stacksOfLightsteeped[7]}

                                           """);

            }

        }

        [ScriptMethod(name: "Phase2 Guidance Of The Last Tower During Light Rampant 光之失控(光暴)踩最后塔指路",
            eventType: EventTypeEnum.EnvControl,
            eventCondition: ["DirectorId:800375BF", "State:00020001", "Index:00000015"])]

        public void Phase2_Guidance_Of_The_Last_Tower_During_Light_Rampant_光之失控踩最后塔指路(Event @event, ScriptAccessory accessory)
        {

            if (parse!=23)
            {

                return;

            }

            System.Threading.Thread.MemoryBarrier();

            phase2_semaphoreFinalLightsteepedWasConfirmed.WaitOne();

            System.Threading.Thread.MemoryBarrier();

            foreach (var 视角 in GuidanceIndices(accessory)) 指路(视角);

            void 指路(int 视角)
            {
                int myIndex = 视角;

                if (phase2_stacksOfLightsteeped[myIndex] < 3)
                {

                    var currentProperty = accessory.Data.GetDefaultDrawProperties();

                    currentProperty.Name = $"Phase2_Guidance_Of_The_Last_Tower_During_Light_Rampant_光之失控踩最后塔指路-{视角}";
                    currentProperty.Scale = new(2);
                    currentProperty.ScaleMode |= ScaleMode.YByDistance;
                    currentProperty.Owner = GuidanceOwner(accessory, 视角);
                    currentProperty.TargetPosition = new Vector3(100, 0, 100);
                    currentProperty.Color = accessory.Data.DefaultSafeColor;
                    currentProperty.Delay = 2500;
                    currentProperty.DestoryAt = 5500;

                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, currentProperty);

                    currentProperty = accessory.Data.GetDefaultDrawProperties();

                    currentProperty.Name = $"Phase2_Highlight_Of_The_Last_Tower_During_Light_Rampant_光之失控踩最后塔高亮-{视角}";
                    currentProperty.Scale = new(4);
                    currentProperty.Position = new Vector3(100, 0, 100);
                    currentProperty.Color = accessory.Data.DefaultSafeColor;
                    currentProperty.Delay = 2500;
                    currentProperty.DestoryAt = 5500;

                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, currentProperty);

                }

                if (Enable_Developer_Mode)
                {

                    if (IsMe(accessory, 视角)) accessory.Method.SendChat($"""
                                               /e 
                                               phase2_stacksOfLightsteeped[myIndex]={phase2_stacksOfLightsteeped[myIndex]}

                                               """);

                }

            }
        }

        [ScriptMethod(name: "P2_光之暴走_八方分散位置", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(4022[01])$"])]
        public void P2_光之暴走_八方分散位置(Event @event, ScriptAccessory accessory)
        {
            if (parse!=23) return;
            foreach (var 视角 in GuidanceIndices(accessory)) 指路(视角);

            void 指路(int 视角)
            {
                var myindex = 视角;
                var rot8 = myindex switch
                {
                    0 => 0,
                    1 => 2,
                    2 => 6,
                    3 => 4,
                    4 => 5,
                    5 => 3,
                    6 => 7,
                    7 => 1,
                    _ => 0,
                };
                var mPosEnd = RotatePoint(new(100, 0, 95), new(100, 0, 100), float.Pi / 4 * rot8);

                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"P2_光之暴走_八方分散位置-{视角}";
                dp.Scale = new(2);
                dp.Owner = GuidanceOwner(accessory, 视角);
                dp.TargetPosition = mPosEnd;
                dp.ScaleMode |= ScaleMode.YByDistance;
                dp.Color = accessory.Data.DefaultSafeColor;
                dp.DestoryAt = 9000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

            }
        }

        [ScriptMethod(name: "Phase2 Reset Semaphores After Light Rampant 光之失控(光暴)后重置信号灯",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:40224"],
            userControl: false)]

        public void Phase2_Reset_Semaphores_After_Light_Rampant_光之失控后重置信号灯(Event @event, ScriptAccessory accessory)
        {

            if (parse!=23)
            {

                return;

            }

            phase2_semaphoreLuminousHammerWasConfirmed = new System.Threading.AutoResetEvent(false);
            phase2_semaphoreFinalLightsteepedWasConfirmed = new System.Threading.AutoResetEvent(false);

        }

        [ScriptMethod(name: "P2_光之暴走_光球爆炸时间绘制", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(40219)$"],
            userControl: true)]
        public void P2_光之暴走_光球爆炸时间绘制(Event @ev, ScriptAccessory sa)
        {
            // 
            if (!ParseObjectId(@ev["SourceId"], out var sid)) return;
            ScriptColor ColorRed = new ScriptColor { V4 = new Vector4(1.0f, 0f, 0f, 1.0f) };
            var dp = sa.Data.GetDefaultDrawProperties();
            dp.Name = $"光球{sid}";
            dp.Scale = new(11f);
            dp.Owner = sid;
            dp.Color = Phase2_Colour_Of_Sphere_AOEs.V4.WithW(3f);
            dp.Delay = 2500;
            dp.DestoryAt = 2500;
            dp.ScaleMode |= ScaleMode.ByTime;
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        
        #region P2 补丁 (Usami)

        #region P2.1 钻石星辰

        [ScriptMethod(name: "钻石星辰阶段转换（DEBUG ONLY）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(40197)$"],
            userControl: Debugging)]
        public void DiamondDustPhaseChange(Event @event, ScriptAccessory accessory)
        {
            _fruPhase = FruPhase.P2A_DiamondDust;
            accessory.DebugMsg($"当前阶段为：{_fruPhase}", Enable_Developer_Mode);
        }

        #endregion P2.1 钻石星辰

        #region P2.2 镜子

        [ScriptMethod(name: "镜中奇遇阶段转换（DEBUG ONLY）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(40179)$"],
            userControl: Debugging)]
        public void MirrorPhaseChange(Event @event, ScriptAccessory accessory)
        {
            _fruPhase = FruPhase.P2B_Mirror;
            accessory.DebugMsg($"当前阶段为：{_fruPhase}", Enable_Developer_Mode);
        }

        #endregion P2.2 镜子

        #region P2.3 光爆

        [ScriptMethod(name: "光之失控阶段转换（DEBUG ONLY）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(40212)$"],
            userControl: Debugging)]
        public void LightRampartPhaseChange(Event @event, ScriptAccessory accessory)
        {
            _fruPhase = FruPhase.P2C_LightRampant;
            _usamiPd.Init(accessory, "光爆");

            // LPDU 双排放泥：北排西→东 H1 H2 MT OT = 0..3，南排西→东 R1 R2 M1 M2 = 10..13
            // 数小的走北路线：一南一北时北排的人走北，同侧时靠西的人走北
            _usamiPd.AddPriorities([2, 3, 0, 1, 12, 13, 10, 11]);
            accessory.DebugMsg($"当前阶段为：{_fruPhase}", Enable_Developer_Mode);
        }

        [ScriptMethod(name: "光之失控放泥", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(0177)$"],
            userControl: true)]
        public void LuminousHammerGuidance(Event @event, ScriptAccessory accessory)
        {
            if (_fruPhase != FruPhase.P2C_LightRampant) return;
            lock (_usamiPd)
            {
                var tid = @event.TargetId;
                var tidx = accessory.GetPlayerIdIndex(tid);
                _usamiPd.AddActionCount();
                _usamiPd.AddPriority(tidx, 100);
            }

            if (_usamiPd.ActionCount != 2) return;
            _usamiPd.ShowPriorities();

            // +100将优先级值放到最后两位，小的左(下)，大的右(上)
            var tLeft = _usamiPd.SelectSpecificPriorityIndex(6).Key;
            var tRight = _usamiPd.SelectSpecificPriorityIndex(7).Key;
            foreach (var 视角 in GuidanceIndices(accessory)) 指路(视角);

            void 指路(int 视角)
            {
                var myIndex = 视角;
                if ((myIndex != tLeft) && (myIndex != tRight)) return;

                // leftRoute 是北路线：从正北 10m 起顺时针放 5 个泥，约 32° 一个，最后停在东南；南路线为其中心对称
                List<Vector3> leftRoute =
                [
                    new Vector3(100, 0, 90),
                    new Vector3(100, 0, 90).RotatePoint(Center, 32f.DegToRad()),
                    new Vector3(100, 0, 90).RotatePoint(Center, 64f.DegToRad()),
                    new Vector3(100, 0, 90).RotatePoint(Center, 96f.DegToRad()),
                    new Vector3(100, 0, 90).RotatePoint(Center, 128f.DegToRad()),
                ];

                List<Vector3> rightRoute =
                [
                    leftRoute[0].PointCenterSymmetry(Center),
                    leftRoute[1].PointCenterSymmetry(Center),
                    leftRoute[2].PointCenterSymmetry(Center),
                    leftRoute[3].PointCenterSymmetry(Center),
                    leftRoute[4].PointCenterSymmetry(Center),
                ];

                for (var i = 0; i < 4; i++)
                {
                    var dp1 = accessory.DrawGuidance(leftRoute[i], leftRoute[i + 1], 0, 14000, $"Usami-光爆左{i}{i + 1}-{视角}", isSafe: myIndex == tLeft);
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Arrow, dp1);
                    var dp2 = accessory.DrawGuidance(rightRoute[i], rightRoute[i + 1], 0, 14000, $"Usami-光爆右{i}{i + 1}-{视角}", isSafe: myIndex == tRight);
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Arrow, dp2);
                }

                var dp = accessory.DrawGuidance(GuidanceOwner(accessory, 视角), myIndex == tLeft ? leftRoute[0] : rightRoute[0], 0, 8000, $"Usami-光爆初始-{视角}");
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

            }
        }

        [ScriptMethod(name: "光爆球爆炸时间", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(40219)$"],
            userControl: true)]
        public void LightBalloonExplode(Event ev, ScriptAccessory sa)
        {
            if (_fruPhase != FruPhase.P2C_LightRampant) return;
            var sid = ev.SourceId;
            var dp = sa.DrawCircle(sid, 11, 2500, 2500, $"光球{sid}", true);
            dp.Color = ColorHelper.ColorRed.V4.WithW(4f);
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        #endregion P2.3 光爆

        #endregion

        #endregion
        
        #region Phase_2_Intermission

        [ScriptMethod(name: "----- Phase 2.5 ----- (No actual meaning for this toggle/此开关无实际意义)",
            eventType: EventTypeEnum.NpcYell,
            eventCondition: ["Your huddled masses yearning to breathe free",
                            "蜷缩着祈盼自由呼吸的人"])]

        public void Phase2point5_Placeholder(Event @event, ScriptAccessory accessory) { }

        [ScriptMethod(name: "P2.5_暗水晶AOE", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40262"])]
        public void P2_暗水晶AOE(Event @event, ScriptAccessory accessory)
        {
            if (!ParseObjectId(@event["SourceId"], out var sid)) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P2.5_暗水晶AOE";
            dp.Scale = new(50);
            dp.Radian = float.Pi / 9;
            dp.Owner = sid;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 3000;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Fan, dp);
        }
        
        #region P2.5 补丁 (Usami / Tetora)

        #region P2.4 绝对零度

        [ScriptMethod(name: "绝对零度阶段转换（DEBUG ONLY）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(40224)$"],
            userControl: Debugging)]
        public void AbsoluteZeroPhaseChange(Event @event, ScriptAccessory accessory)
        {
            _fruPhase = FruPhase.P2D_AbsoluteZero;
            accessory.DebugMsg($"当前阶段为：{_fruPhase}", Enable_Developer_Mode);
        }

        [ScriptMethod(name: "*P2.5 暗水晶不可选中", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:17828"],
            userControl: true)]
        public async void DarkCrystalUntargetable(Event ev, ScriptAccessory sa)
        {
            if (!P2暗水晶不可选中) return;
            if (_fruPhase != FruPhase.P2D_AbsoluteZero) return;
            var sid = ev.SourceId;
            await Task.Delay(500);
            sa.SetTargetable(sa.GetById(sid), false);
        }

        #endregion P2.4 绝对零度

        // 40256 严冬风暴：光水晶 3s 读条，EffectPosition 7m 圆
        [ScriptMethod(name: "P2.5 严冬风暴(大圈)判定时间绘制", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40256"])]
        public void P25严冬风暴(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"严冬风暴{@event.SourceId}";
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Position = @event.EffectPosition();
            dp.Scale = new Vector2(7f);
            dp.DestoryAt = 3000;
            dp.ScaleMode = ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        [ScriptMethod(name: "P2.5 严冬风暴销毁", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:40256"], userControl: false)]
        public void P25严冬风暴销毁(Event @event, ScriptAccessory accessory)
            => accessory.Method.RemoveDraw($"严冬风暴{@event.SourceId}");

        #endregion

        #endregion

        #region Phase_3

        [ScriptMethod(name: "----- Phase 3 ----- (No actual meaning for this toggle/此开关无实际意义)",
            eventType: EventTypeEnum.NpcYell,
            eventCondition: ["The wretched refuse of your teeming shore",
                            "被你们的繁荣拒之门外受苦的人"])]

        public void Phase3_Placeholder(Event @event, ScriptAccessory accessory) { }

        [ScriptMethod(name: "P3_时间压缩_分P", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(40266)$"], userControl: false)]
        public void P3_时间压缩_分P(Event @event, ScriptAccessory accessory)
        {
            parse=31;
            phase3_bossId = @event["SourceId"];
            P3FireBuff = [0, 0, 0, 0, 0, 0, 0, 0];
            P3WaterBuff = [0, 0, 0, 0, 0, 0, 0, 0];
            P3ReturnBuff = [0, 0, 0, 0, 0, 0, 0, 0];
            P3Lamp = [0, 0, 0, 0, 0, 0, 0, 0];
            P3LampWise = [0, 0, 0, 0, 0, 0, 0, 0];
        }
        [ScriptMethod(name: "P3_时间压缩_Buff记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(2455|2456|2464|2462|2461|2460)$"], userControl: false)]
        public void P3_时间压缩_Buff记录(Event @event, ScriptAccessory accessory)
        {
            if (parse!=31) return;
            if (!ParseObjectId(@event["TargetId"], out var tid)) return;
            if (!float.TryParse(@event["Duration"], out var dur)) return;
            var index = accessory.Data.PartyList.IndexOf(((uint)tid));
            if (index == -1) return;
            //冰
            if (@event["StatusID"] == "2462")
            {
                lock (P3FireBuff)
                {
                    P3FireBuff[index] = 4;
                }
            }
            //火
            if (@event["StatusID"] == "2455")
            {

                var count = 1;
                if (dur > 20) count = 2;
                if (dur > 30) count = 3;
                lock (P3FireBuff)
                {
                    P3FireBuff[index] = count;
                }
            }
            //回返
            if (@event["StatusID"] == "2464")
            {
                var count = 1;
                if (dur > 20) count = 3;
                lock (P3ReturnBuff)
                {
                    P3ReturnBuff[index] = count;
                }
            }
            //水
            if (@event["StatusID"] == "2461")
            {
                lock (P3WaterBuff)
                {
                    P3WaterBuff[index] = 1;
                }
            }
            //圈
            if (@event["StatusID"] == "2460")
            {
                lock (P3WaterBuff)
                {
                    P3WaterBuff[index] = 2;
                }
            }
            //背对
            if (@event["StatusID"] == "2456")
            {
                lock (P3WaterBuff)
                {
                    P3WaterBuff[index] = 3;
                }
            }



        }
        [ScriptMethod(name: "P3_时间压缩_灯记录", eventType: EventTypeEnum.Tether, eventCondition: ["Id:regex:^(0085|0086)$"], userControl: false)]
        public void P3_时间压缩_灯记录(Event @event, ScriptAccessory accessory)
        {
            //0085紫
            //0086黄
            if (parse!=31) return;
            var pos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
            var dir8 = PositionTo8Dir(pos, new(100, 0, 100));
            lock (P3Lamp)
            {
                P3Lamp[dir8] = @event["Id"] == "0086" ? 1 : 2;
            }
        }
        [ScriptMethod(name: "P3_时间压缩_灯顺逆记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2970"], userControl: false)]
        public void P3_时间压缩_灯顺逆记录(Event @event, ScriptAccessory accessory)
        {
            //buff2970, 13 269顺时针 92 348逆时针
            if (parse!=31) return;
            var pos = JsonConvert.DeserializeObject<Vector3>(@event["TargetPosition"]);
            Vector3 centre = new(100, 0, 100);
            var dir8 = PositionTo8Dir(pos, centre);
            P3LampWise[dir8] = @event["StackCount"] == "92" ? 1 : 0;
        }
        [ScriptMethod(name: "P3_时间压缩_灯AOE", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:40235", "TargetIndex:1"])]
        public void P3_时间压缩_灯AOE(Event @event, ScriptAccessory accessory)
        {
            if (parse!=31) return;
            var pos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
            var rot = JsonConvert.DeserializeObject<float>(@event["SourceRotation"]);
            Vector3 centre = new(100, 0, 100);
            var dir8 = PositionTo8Dir(pos, centre);
            var isWise = P3LampWise[dir8] == 1;
            for (int i = 0; i < 9; i++)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = "P3_时间压缩_灯AOE";
                dp.Scale = new(5, 50);
                dp.Position = pos;
                dp.Rotation = rot + (i + 1) * float.Pi / 12 * (isWise ? -1 : 1);
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.DestoryAt = 2000 + (i * 1000);
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            }
        }
        [ScriptMethod(name: "P3_时间压缩_Buff处理位置", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40293"])]
        public void P3_时间压缩_Buff处理位置(Event @event, ScriptAccessory accessory)
        {
            if (parse!=31) return;
            foreach (var 视角 in GuidanceIndices(accessory)) 指路(视角);

            void 指路(int 视角)
            {
                var myIndex = 视角;
                if (myIndex == -1) return;
                var myDir8 = MyLampIndex(myIndex);
                //accessory.Log.Debug($"myDir8 {myDir8}");
                if (myDir8 == -1) return;
                var myRot = myDir8 * float.Pi / 4;

                Vector3 centre = new(100, 0, 100);
                Vector3 fireN = new(100, 0, 84.5f);
                Vector3 returnPosN = P3WaterBuff[myIndex] == 2 ? new(100, 0, 91.5f) : new(100, 0, 98);
                Vector3 stopPos = new(100, 0, 101);
                //火
                var myFire = P3FireBuff[myIndex];
                //短火
                if (myFire == 1)
                {
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"P3_时间压缩_短火_放火-{视角}";
                    dp.Scale = new(2);
                    dp.ScaleMode |= ScaleMode.YByDistance;
                    dp.Owner = GuidanceOwner(accessory, 视角);
                    dp.TargetPosition = RotatePoint(fireN, centre, myRot);
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.DestoryAt = 7500;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                    dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"P3_时间压缩_短火_放回溯-{视角}";
                    dp.Scale = new(2);
                    dp.ScaleMode |= ScaleMode.YByDistance;
                    dp.Owner = GuidanceOwner(accessory, 视角);
                    dp.TargetPosition = RotatePoint(returnPosN, centre, myRot);
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.Delay = 7500;
                    dp.DestoryAt = 5000;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                    dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"P3_时间压缩_短火_场中分摊-{视角}";
                    dp.Scale = new(2);
                    dp.ScaleMode |= ScaleMode.YByDistance;
                    dp.Owner = GuidanceOwner(accessory, 视角);
                    dp.TargetPosition = centre;
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.Delay = 12500;
                    dp.DestoryAt = 5000;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);


                    dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"P3_时间压缩_短火_输出位置-{视角}";
                    dp.Scale = new(2);
                    dp.ScaleMode |= ScaleMode.YByDistance;
                    dp.Owner = GuidanceOwner(accessory, 视角);
                    dp.TargetPosition = RotatePoint(stopPos, centre, myRot);
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.Delay = 22500;
                    dp.DestoryAt = 15000;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                }

                //中火
                if (myFire == 2)
                {
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"P3_时间压缩_中火_中场分摊-{视角}";
                    dp.Scale = new(2);
                    dp.ScaleMode |= ScaleMode.YByDistance;
                    dp.Owner = GuidanceOwner(accessory, 视角);
                    dp.TargetPosition = centre;
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.DestoryAt = 7500;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                    dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"P3_时间压缩_中火_放回溯-{视角}";
                    dp.Scale = new(2);
                    dp.ScaleMode |= ScaleMode.YByDistance;
                    dp.Owner = GuidanceOwner(accessory, 视角);
                    dp.TargetPosition = RotatePoint(returnPosN, centre, myRot);
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.Delay = 7500;
                    dp.DestoryAt = 5000;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                    dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"P3_时间压缩_中火_放火-{视角}";
                    dp.Scale = new(2);
                    dp.ScaleMode |= ScaleMode.YByDistance;
                    dp.Owner = GuidanceOwner(accessory, 视角);
                    dp.TargetPosition = RotatePoint(fireN, centre, myRot);
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.Delay = 12500;
                    dp.DestoryAt = 5000;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                    dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"P3_时间压缩_中火_中场-{视角}";
                    dp.Scale = new(2);
                    dp.ScaleMode |= ScaleMode.YByDistance;
                    dp.Owner = GuidanceOwner(accessory, 视角);
                    dp.TargetPosition = centre;
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.Delay = 17500;
                    dp.DestoryAt = 10000;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                    dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"P3_时间压缩_中火_输出位置-{视角}";
                    dp.Scale = new(2);
                    dp.ScaleMode |= ScaleMode.YByDistance;
                    dp.Owner = GuidanceOwner(accessory, 视角);
                    dp.TargetPosition = RotatePoint(stopPos, centre, myRot);
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.Delay = 32500;
                    dp.DestoryAt = 5000;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                }

                //长火
                if (myFire == 3)
                {
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"P3_时间压缩_长火_中场分摊-{视角}";
                    dp.Scale = new(2);
                    dp.ScaleMode |= ScaleMode.YByDistance;
                    dp.Owner = GuidanceOwner(accessory, 视角);
                    dp.TargetPosition = centre;
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.DestoryAt = 7500;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                    dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"P3_时间压缩_长火_中场分摊-{视角}";
                    dp.Scale = new(2);
                    dp.ScaleMode |= ScaleMode.YByDistance;
                    dp.Owner = GuidanceOwner(accessory, 视角);
                    dp.TargetPosition = centre;
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.Delay = 12500;
                    dp.DestoryAt = 5000;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                    dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"P3_时间压缩_长火_回溯-{视角}";
                    dp.Scale = new(2);
                    dp.ScaleMode |= ScaleMode.YByDistance;
                    dp.Owner = GuidanceOwner(accessory, 视角);
                    dp.TargetPosition = RotatePoint(returnPosN, centre, myRot);
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.Delay = 17500;
                    dp.DestoryAt = 5000;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                    dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"P3_时间压缩_长火_放火-{视角}";
                    dp.Scale = new(2);
                    dp.ScaleMode |= ScaleMode.YByDistance;
                    dp.Owner = GuidanceOwner(accessory, 视角);
                    dp.TargetPosition = RotatePoint(fireN, centre, myRot);
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.Delay = 22500;
                    dp.DestoryAt = 5000;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                    dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"P3_时间压缩_长火_输出-{视角}";
                    dp.Scale = new(2);
                    dp.ScaleMode |= ScaleMode.YByDistance;
                    dp.Owner = GuidanceOwner(accessory, 视角);
                    dp.TargetPosition = RotatePoint(stopPos, centre, myRot);
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.Delay = 27500;
                    dp.DestoryAt = 10000;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                }

                if (myFire == 4)
                {
                    if (myIndex < 4)
                    {
                        var dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Name = $"P3_时间压缩_冰TH_放冰-{视角}";
                        dp.Scale = new(2);
                        dp.ScaleMode |= ScaleMode.YByDistance;
                        dp.Owner = GuidanceOwner(accessory, 视角);
                        dp.TargetPosition = centre;
                        dp.Color = accessory.Data.DefaultSafeColor;
                        dp.DestoryAt = 7500;
                        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                        dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Name = $"P3_时间压缩_冰TH_放回溯-{视角}";
                        dp.Scale = new(2);
                        dp.ScaleMode |= ScaleMode.YByDistance;
                        dp.Owner = GuidanceOwner(accessory, 视角);
                        dp.TargetPosition = RotatePoint(returnPosN, centre, myRot);
                        dp.Color = accessory.Data.DefaultSafeColor;
                        dp.Delay = 7500;
                        dp.DestoryAt = 5000;
                        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                        dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Name = $"P3_时间压缩_冰TH_场中分摊-{视角}";
                        dp.Scale = new(2);
                        dp.ScaleMode |= ScaleMode.YByDistance;
                        dp.Owner = GuidanceOwner(accessory, 视角);
                        dp.TargetPosition = centre;
                        dp.Color = accessory.Data.DefaultSafeColor;
                        dp.Delay = 12500;
                        dp.DestoryAt = 5000;
                        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);


                        dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Name = $"P3_时间压缩_冰TH_输出位置-{视角}";
                        dp.Scale = new(2);
                        dp.ScaleMode |= ScaleMode.YByDistance;
                        dp.Owner = GuidanceOwner(accessory, 视角);
                        dp.TargetPosition = RotatePoint(stopPos, centre, myRot);
                        dp.Color = accessory.Data.DefaultSafeColor;
                        dp.Delay = 22500;
                        dp.DestoryAt = 15000;
                        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                    }
                    else
                    {
                        var dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Name = $"P3_时间压缩_冰D_中场分摊-{视角}";
                        dp.Scale = new(2);
                        dp.ScaleMode |= ScaleMode.YByDistance;
                        dp.Owner = GuidanceOwner(accessory, 视角);
                        dp.TargetPosition = centre;
                        dp.Color = accessory.Data.DefaultSafeColor;
                        dp.DestoryAt = 7500;
                        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                        dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Name = $"P3_时间压缩_冰D_中场分摊-{视角}";
                        dp.Scale = new(2);
                        dp.ScaleMode |= ScaleMode.YByDistance;
                        dp.Owner = GuidanceOwner(accessory, 视角);
                        dp.TargetPosition = centre;
                        dp.Color = accessory.Data.DefaultSafeColor;
                        dp.Delay = 12500;
                        dp.DestoryAt = 5000;
                        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                        dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Name = $"P3_时间压缩_冰D_回溯-{视角}";
                        dp.Scale = new(2);
                        dp.ScaleMode |= ScaleMode.YByDistance;
                        dp.Owner = GuidanceOwner(accessory, 视角);
                        dp.TargetPosition = RotatePoint(returnPosN, centre, myRot);
                        dp.Color = accessory.Data.DefaultSafeColor;
                        dp.Delay = 17500;
                        dp.DestoryAt = 5000;
                        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                        dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Name = $"P3_时间压缩_冰D_放冰-{视角}";
                        dp.Scale = new(2);
                        dp.ScaleMode |= ScaleMode.YByDistance;
                        dp.Owner = GuidanceOwner(accessory, 视角);
                        dp.TargetPosition = centre;
                        dp.Color = accessory.Data.DefaultSafeColor;
                        dp.Delay = 22500;
                        dp.DestoryAt = 5000;
                        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                        dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Name = $"P3_时间压缩_长火_输出-{视角}";
                        dp.Scale = new(2);
                        dp.ScaleMode |= ScaleMode.YByDistance;
                        dp.Owner = GuidanceOwner(accessory, 视角);
                        dp.TargetPosition = RotatePoint(stopPos, centre, myRot);
                        dp.Color = accessory.Data.DefaultSafeColor;
                        dp.Delay = 27500;
                        dp.DestoryAt = 10000;
                        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                    }
                }
            }
        }
        [ScriptMethod(name: "P3_时间压缩_灯处理位置", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2970"])]
        public void P3_时间压缩_灯处理位置(Event @event, ScriptAccessory accessory)
        {
            if (parse!=31) return;
            //buff2970, 13 269顺时针 92 348逆时针
            var pos = JsonConvert.DeserializeObject<Vector3>(@event["TargetPosition"]);
            Vector3 centre = new(100, 0, 100);
            foreach (var 视角 in GuidanceIndices(accessory)) 指路(视角);

            void 指路(int 视角)
            {
                var myIndex = 视角;
                var dir8 = PositionTo8Dir(pos, centre);
                Vector3 nPos = @event["StackCount"] == "92" ? new(98, 0, 90) : new(102, 0, 90);
                if (dir8 == MyLampIndex(myIndex))
                {
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"P3_时间压缩_灯处理位置-{视角}";
                    dp.Scale = new(2);
                    dp.ScaleMode |= ScaleMode.YByDistance;
                    dp.Owner = GuidanceOwner(accessory, 视角);
                    dp.TargetPosition = RotatePoint(nPos, centre, dir8 * float.Pi / 4);
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.DestoryAt = 4000;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                }
            }
        }

        [ScriptMethod(name: "Phase3 Prompt Before Shell Crusher 破盾一击前提示",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:40286"])]

        public void Phase3_Prompt_Before_Shell_Crusher_破盾一击前提示(Event @event, ScriptAccessory accessory)
        {

            if (parse!=31)
            {

                return;

            }

            if (Enable_Text_Prompts)
            {

                if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                {

                    accessory.Method.TextInfo("场中集合分摊", 3000);

                }

                if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                {

                    accessory.Method.TextInfo("Stack in the center", 3000);

                }

            }

            if (Enable_Vanilla_TTS || Enable_Daily_Routines_TTS)
            {

                if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                {

                    accessory.TTS("场中集合分摊", Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);

                }

                if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                {

                    accessory.TTS("Stack in the center", Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);

                }

            }

        }

        [ScriptMethod(name: "P3_时间压缩_黑暗光环", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40290"])]
        public void P3_时间压缩_黑暗光环(Event @event, ScriptAccessory accessory)
        {
            if (parse!=31) return;
            if (!ParseObjectId(@event["SourceId"], out var sid)) return;
            if (!ParseObjectId(@event["TargetId"], out var tid)) return;
            var myindex = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P3_时间压缩_黑暗光环";
            dp.Scale = new(20);
            dp.Owner = sid;
            dp.TargetObject = tid;
            dp.Color = myindex == 0 || myindex == 1 ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 5000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }

        [ScriptMethod(name: "Phase3 Initial Orientation Before The Second Half 二运前的初始面向",
            eventType: EventTypeEnum.ActionEffect,
            eventCondition: ["ActionId:40290"])]

        public void Phase3_Initial_Orientation_Before_The_Second_Half_二运前的初始面向(Event @event, ScriptAccessory accessory)
        {

            if (parse!=31)
            {

                return;

            }

            if (!ParseObjectId(@event["SourceId"], out var sourceId))
            {

                return;

            }

            if (!accessory.Data.EnmityList.TryGetValue(sourceId, out var enmityListOfBoss))
            {

                return;

            }

            if (Enable_Developer_Mode)
            {

                accessory.Method.SendChat($"""
                                           /e 
                                           accessory.Data.Me={accessory.Data.Me}
                                           enmityListOfTheBoss[0]={enmityListOfBoss[0]}

                                           """);

            }

            foreach (var 视角 in GuidanceIndices(accessory)) 指路(视角);

            void 指路(int 视角)
            {
                if (GuidanceOwner(accessory, 视角) != enmityListOfBoss[0])
                {

                    return;

                }

                var currentProperty = accessory.Data.GetDefaultDrawProperties();

                currentProperty.Name = $"Phase3_Initial_Orientation_Before_The_Second_Half_二运前的初始面向-{视角}";
                currentProperty.Scale = new(2);
                currentProperty.ScaleMode |= ScaleMode.YByDistance;
                currentProperty.Owner = GuidanceOwner(accessory, 视角);
                currentProperty.TargetPosition = new Vector3(100, 0, 94);
                currentProperty.Color = accessory.Data.DefaultSafeColor;
                currentProperty.DestoryAt = 12500;

                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, currentProperty);

                if (Enable_Text_Prompts)
                {

                    if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                    {

                        if (IsMe(accessory, 视角)) accessory.Method.TextInfo("让Boss面向正北", 12500);

                    }

                    if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                    {

                        if (IsMe(accessory, 视角)) accessory.Method.TextInfo("Make the Boss orient to the north", 12500);

                    }

                }

                if (Enable_Vanilla_TTS || Enable_Daily_Routines_TTS)
                {

                    if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                    {

                        if (IsMe(accessory, 视角)) accessory.TTS("让Boss面向正北", Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);

                    }

                    if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                    {

                        if (IsMe(accessory, 视角)) accessory.TTS("Make the Boss orient to the north", Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);

                    }

                }

            }
        }

        [ScriptMethod(name: "P3_延迟咏唱回响_分P", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(40269)$"], userControl: false)]
        public void P3_延迟咏唱回响_分P(Event @event, ScriptAccessory accessory)
        {
            parse=32;
            P3FloorFire = -1;
            phase3_typeOfDarkWaterIii = [
                Phase3_Types_Of_Dark_Water_III.NONE,
                Phase3_Types_Of_Dark_Water_III.NONE,
                Phase3_Types_Of_Dark_Water_III.NONE,
                Phase3_Types_Of_Dark_Water_III.NONE,
                Phase3_Types_Of_Dark_Water_III.NONE,
                Phase3_Types_Of_Dark_Water_III.NONE,
                Phase3_Types_Of_Dark_Water_III.NONE,
                Phase3_Types_Of_Dark_Water_III.NONE
            ];
            phase3_numberOfDarkWaterIiiHasBeenProcessed = 0;
            phase3_roundOfDarkWaterIii = 0;
            phase3_rangeSemaphoreOfDarkWaterIii = 0;
            phase3_guidanceSemaphoreOfDarkWaterIii = 0;
            phase3_hasConfirmedInitialSafePositions = false;
            phase3_locomotive_initialSafePositionOfTheLeftGroup = new Vector3(100, 0, 100);
            phase3_locomotive_initialSafePositionOfTheRightGroup = new Vector3(100, 0, 100);
            phase3_locomotive_leftPositionToStackOfTheSecondRound = new Vector3(100, 0, 100);
            phase3_locomotive_rightPositionToStackOfTheSecondRound = new Vector3(100, 0, 100);
            phase3_finalPositionOfTheBoss = new Vector3(100, 0, 100);
        }

        [ScriptMethod(name: "Phase3 Determine Types Of Dark Water III 确定黑暗狂水(分摊)类型",
            eventType: EventTypeEnum.StatusAdd,
            eventCondition: ["StatusID:2461"],
            userControl: false)]

        public void Phase3_Determine_Types_Of_Dark_Water_III_确定黑暗狂水类型(Event @event, ScriptAccessory accessory)
        {

            if (parse!=32)
            {

                return;

            }

            if (!ParseObjectId(@event["TargetId"], out var targetId))
            {

                return;

            }

            int currentIndex = accessory.Data.PartyList.IndexOf(((uint)targetId));
            int duration = Convert.ToInt32(@event["DurationMilliseconds"], 10);

            if (currentIndex < 0 || currentIndex > 7)
            {

                return;

            }

            if (duration > 36000)
            {
                // Actually it's 38000ms (38s), but just in case.

                lock (phase3_typeOfDarkWaterIii)
                {

                    phase3_typeOfDarkWaterIii[currentIndex] = Phase3_Types_Of_Dark_Water_III.LONG;

                }

            }

            else
            {

                if (duration > 27000)
                {
                    // Actually it's 29000ms (29s), but just in case.

                    lock (phase3_typeOfDarkWaterIii)
                    {

                        phase3_typeOfDarkWaterIii[currentIndex] = Phase3_Types_Of_Dark_Water_III.MEDIUM;

                    }

                }

                else
                {

                    if (duration > 8000)
                    {
                        // Actually it's 10000ms (10s), but just in case.

                        lock (phase3_typeOfDarkWaterIii)
                        {

                            phase3_typeOfDarkWaterIii[currentIndex] = Phase3_Types_Of_Dark_Water_III.SHORT;

                        }

                    }

                }

            }

            System.Threading.Thread.MemoryBarrier();

            ++phase3_numberOfDarkWaterIiiHasBeenProcessed;

            System.Threading.Thread.MemoryBarrier();

            if (Enable_Developer_Mode)
            {

                accessory.Method.SendChat($"""
                                           /e 
                                           currentIndex={currentIndex}
                                           duration={duration}
                                           phase3_typeOfDarkWaterIii={phase3_typeOfDarkWaterIii[currentIndex]}
                                           
                                           """);

            }

        }

        [ScriptMethod(name: "Phase3 Prompt Before Dark Water III 暗黑狂水(分摊)前提示",
            eventType: EventTypeEnum.StatusAdd,
            eventCondition: ["StatusID:2461"],
            suppress: 2000)]

        public void Phase3_Prompt_Before_Dark_Water_III_暗黑狂水前提示(Event @event, ScriptAccessory accessory)
        {

            if (parse!=32)
            {

                return;

            }

            while (phase3_numberOfDarkWaterIiiHasBeenProcessed < 6)
            {

                System.Threading.Thread.Sleep(1);

            }

            System.Threading.Thread.MemoryBarrier();

            foreach (var 视角 in GuidanceIndices(accessory)) 指路(视角);

            void 指路(int 视角)
            {
                int myIndex = 视角;
                bool goLeft = phase3_locomotive_shouldGoLeft(myIndex);
                string prompt = "";
                var currentProperty = accessory.Data.GetDefaultDrawProperties();

                currentProperty.Name = $"Phase3_Prompt_Before_Dark_Water_III_暗黑狂水前提示-{视角}";
                currentProperty.Scale = new(2);
                currentProperty.ScaleMode |= ScaleMode.YByDistance;
                currentProperty.Owner = GuidanceOwner(accessory, 视角);
                currentProperty.TargetPosition = (goLeft) ? (phase3_leftGroupPosition_asAConstant) : (phase3_rightGroupPosition_asAConstant);
                currentProperty.Color = accessory.Data.DefaultSafeColor;
                currentProperty.DestoryAt = 5000;

                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, currentProperty);

                // 换组 = 最终分组和职能组（T奶西北、DPS东南）不同
                bool swap = goLeft != (myIndex < 4);

                if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                {

                    prompt = $"{(goLeft ? "西北组" : "东南组")}分摊{(swap ? "（换组）" : "")}";

                }

                if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                {

                    prompt = $"Stack {(goLeft ? "NW" : "SE")}{(swap ? " (swap)" : "")}";

                }

                if (Enable_Text_Prompts)
                {

                    if (IsMe(accessory, 视角)) accessory.Method.TextInfo(prompt, 3500);

                }

                if (IsMe(accessory, 视角)) accessory.TTS($"{prompt}", Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);

            }

        }

        [ScriptMethod(name: "Phase3 Release The Semaphore Of Dark Water III 释放黑暗狂水(分摊)的信号灯",
            eventType: EventTypeEnum.StatusRemove,
            eventCondition: ["StatusID:2458"],
            suppress: 2000,
            userControl: false)]

        public void Phase3_Release_The_Semaphore_Of_Dark_Water_III_释放黑暗狂水的信号灯(Event @event, ScriptAccessory accessory)
        {

            if (parse!=32)
            {

                return;

            }

            if(@event["SourceId"].Equals("00000000")) {
                // A rare local exception. Please refer to the report on Discord for details.
                // In short, there's a very rare chance that a 2458 status from the entity 00000000 will be applied without valid duration.
                // Therefore, that weird 2458 status will be removed immediately, and the removal will cause incorrect guidance.

                return;

            }

            System.Threading.Thread.MemoryBarrier();

            ++phase3_roundOfDarkWaterIii;

            System.Threading.Thread.MemoryBarrier();

            phase3_rangeSemaphoreOfDarkWaterIii = 1;
            phase3_guidanceSemaphoreOfDarkWaterIii = 1;

        }

        [ScriptMethod(name: "Phase3 Range Of Dark Water III 黑暗狂水(分摊)范围",
            eventType: EventTypeEnum.StatusRemove,
            eventCondition: ["StatusID:2458"],
            suppress: 2000)]

        public void Phase3_Range_Of_Dark_Water_III_黑暗狂水范围(Event @event, ScriptAccessory accessory)
        {
            
            if(@event["SourceId"].Equals("00000000")) {

                return;

            }
            
            System.Threading.Thread.MemoryBarrier();

            while (System.Threading.Interlocked.CompareExchange(ref phase3_rangeSemaphoreOfDarkWaterIii, 0, 1) == 0)
            {

                System.Threading.Thread.Sleep(1);

            }

            System.Threading.Thread.MemoryBarrier();

            Phase3_Types_Of_Dark_Water_III currentType = Phase3_Types_Of_Dark_Water_III.NONE;

            switch (phase3_roundOfDarkWaterIii)
            {

                case 1:
                    {

                        currentType = Phase3_Types_Of_Dark_Water_III.SHORT;

                        break;

                    }

                case 2:
                    {

                        currentType = Phase3_Types_Of_Dark_Water_III.MEDIUM;

                        break;

                    }

                case 3:
                    {

                        currentType = Phase3_Types_Of_Dark_Water_III.LONG;

                        break;

                    }

                default:
                    {
                        // Just a placeholder and should never be reached.

                        return;

                    }

            }

            var currentProperty = accessory.Data.GetDefaultDrawProperties();

            if (phase3_numberOfDarkWaterIiiHasBeenProcessed == 6)
            {

                bool goLeft = phase3_locomotive_shouldGoLeft(accessory.Data.PartyList.IndexOf(accessory.Data.Me));

                for (int i = 0; i < 8; ++i)
                {

                    if (phase3_typeOfDarkWaterIii[i] == currentType)
                    {

                        currentProperty = accessory.Data.GetDefaultDrawProperties();

                        currentProperty.Name = "Phase3_Range_Of_Dark_Water_III_黑暗狂水范围";
                        currentProperty.Scale = new(6);
                        currentProperty.Owner = accessory.Data.PartyList[i];
                        currentProperty.DestoryAt = 5000;

                        if (phase3_locomotive_shouldGoLeft(i) == goLeft)
                        {

                            currentProperty.Color = accessory.Data.DefaultSafeColor;

                        }

                        else
                        {

                            currentProperty.Color = accessory.Data.DefaultDangerColor;

                        }

                        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, currentProperty);

                    }

                }

                return;

            }

            for (int i = 0; i < 8; ++i)
            {

                if (phase3_typeOfDarkWaterIii[i] == currentType)
                {

                    currentProperty = accessory.Data.GetDefaultDrawProperties();

                    currentProperty.Name = "Phase3_Range_Of_Dark_Water_III_黑暗狂水范围";
                    currentProperty.Scale = new(6);
                    currentProperty.Owner = accessory.Data.PartyList[i];
                    currentProperty.Color = accessory.Data.DefaultDangerColor;
                    currentProperty.DestoryAt = 5000;

                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, currentProperty);

                }

            }

            if (Enable_Text_Prompts)
            {

                if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                {

                    accessory.Method.TextInfo("分摊", 2000);

                }

                if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                {

                    accessory.Method.TextInfo("Stack", 2000);

                }

            }

            if (Enable_Vanilla_TTS || Enable_Daily_Routines_TTS)
            {

                if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                {

                    accessory.TTS("分摊", Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);

                }

                if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                {

                    accessory.TTS("Stack", Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);

                }

            }

        }

        [ScriptMethod(name: "Phase3 Guidance Of Dark Water III 黑暗狂水(分摊)指路",
            eventType: EventTypeEnum.StatusRemove,
            eventCondition: ["StatusID:2458"],
            suppress: 2000)]

        public void Phase3_Guidance_Of_Dark_Water_III_黑暗狂水指路(Event @event, ScriptAccessory accessory)
        {
            
            if(@event["SourceId"].Equals("00000000")) {

                return;

            }
            
            System.Threading.Thread.MemoryBarrier();

            while (System.Threading.Interlocked.CompareExchange(ref phase3_guidanceSemaphoreOfDarkWaterIii, 0, 1) == 0)
            {

                System.Threading.Thread.Sleep(1);

            }

            System.Threading.Thread.MemoryBarrier();

            if (phase3_numberOfDarkWaterIiiHasBeenProcessed != 6)
            {

                return;

            }

            foreach (var 视角 in GuidanceIndices(accessory)) 指路(视角);

            void 指路(int 视角)
            {
                bool targetPositionConfirmed = false;
                string prompt = "";
                var currentProperty = accessory.Data.GetDefaultDrawProperties();

                currentProperty.Name = $"Phase3_Guidance_Of_Dark_Water_III_黑暗狂水指路-{视角}";
                currentProperty.Scale = new(2);
                currentProperty.ScaleMode |= ScaleMode.YByDistance;
                currentProperty.Owner = GuidanceOwner(accessory, 视角);
                currentProperty.Color = accessory.Data.DefaultSafeColor;
                currentProperty.DestoryAt = 5000;

                bool goLeft = phase3_locomotive_shouldGoLeft(视角);

                if (Enable_Developer_Mode)
                {

                    if (IsMe(accessory, 视角)) accessory.Method.SendChat($"""
                                               /e 
                                               goLeft={goLeft}
                                               phase3_roundOfDarkWaterIii={phase3_roundOfDarkWaterIii}

                                               """);

                }

                switch (phase3_roundOfDarkWaterIii)
                {

                    case 1:
                        {

                            currentProperty.TargetPosition = (goLeft) ? (phase3_leftGroupPosition_asAConstant) : (phase3_rightGroupPosition_asAConstant);

                            targetPositionConfirmed = true;

                            if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                            {

                                prompt = (goLeft) ? ("西北组分摊") : ("东南组分摊");

                            }

                            if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                            {

                                prompt = (goLeft) ? ("Stack NW") : ("Stack SE");

                            }

                            break;

                        }

                    case 2:
                        {

                            currentProperty.TargetPosition = (goLeft) ?
                                (phase3_locomotive_leftPositionToStackOfTheSecondRound) :
                                (phase3_locomotive_rightPositionToStackOfTheSecondRound);

                            targetPositionConfirmed = true;

                            if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                            {

                                prompt = "场中当前侧分摊";

                            }

                            if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                            {

                                prompt = "Stack on this side of the center";

                            }

                            break;

                        }

                    case 3:
                        {

                            if (ParseObjectId(phase3_bossId, out var bossId))
                            {

                                var bossObject = accessory.Data.Objects.SearchById(bossId);

                                if (bossObject != null)
                                {

                                    float currentRotation = bossObject.Rotation;
                                    currentRotation = -(currentRotation - float.Pi);

                                    Vector3 groupPosition = new Vector3(100, 0, 100);

                                    if (Enable_Developer_Mode)
                                    {

                                        if (IsMe(accessory, 视角)) accessory.Method.SendChat($"""
                                                               /e 
                                                               currentRotation={currentRotation}

                                                               """);

                                    }

                                    if (goLeft)
                                    {

                                        groupPosition = new Vector3(bossObject.Position.X - 6.89f,
                                                                  bossObject.Position.Y,
                                                                  bossObject.Position.Z + 6.89f);

                                    }

                                    else
                                    {

                                        groupPosition = new Vector3(bossObject.Position.X + 6.89f,
                                                                  bossObject.Position.Y,
                                                                  bossObject.Position.Z + 6.89f);

                                    }

                                    groupPosition = RotatePoint(groupPosition, bossObject.Position, currentRotation);

                                    currentProperty.TargetPosition = groupPosition;

                                    targetPositionConfirmed = true;

                                }

                            }

                            if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                            {

                                prompt = (goLeft) ? ("左侧分摊") : ("右侧分摊");

                            }

                            if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                            {

                                prompt = (goLeft) ? ("Stack on the left") : ("Stack on the right");

                            }

                            break;

                        }

                    default:
                        {
                            // Just a placeholder and should never be reached.

                            break;

                        }

                }

                if (targetPositionConfirmed)
                {

                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, currentProperty);

                }

                if (!prompt.Equals(""))
                {

                    if (Enable_Text_Prompts)
                    {

                        if (IsMe(accessory, 视角)) accessory.Method.TextInfo(prompt, 2500);

                    }

                    if (IsMe(accessory, 视角)) accessory.TTS($"{prompt}", Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);

                }

            }
        }

        private bool phase3_locomotive_shouldGoLeft(int currentIndex)
        {

            if (currentIndex < 0 || currentIndex > 7)
            {

                return true;

            }

            int locomotiveIndex = phase3_locomotive_getLocomotiveIndex(currentIndex);
            Phase3_Types_Of_Dark_Water_III currentType = phase3_typeOfDarkWaterIii[currentIndex];
            bool goLeft = true;

            for (int i = 0; i < 8; ++i)
            {

                if (phase3_typeOfDarkWaterIii[phase3_locomotive_priority_asAConstant[i]] == currentType && i != locomotiveIndex)
                {

                    if (i > locomotiveIndex)
                    {

                        goLeft = true;
                        // Should go left.

                        break;

                    }

                    if (i < locomotiveIndex)
                    {

                        goLeft = false;
                        // Should go right.

                        break;

                    }

                }

            }

            return goLeft;

        }

        private int phase3_locomotive_getLocomotiveIndex(int currentIndex)
        {

            for (int i = 0; i < 8; ++i)
            {

                if (currentIndex == phase3_locomotive_priority_asAConstant[i])
                {

                    return i;

                }

            }

            return currentIndex;
            // Just a placeholder and should never be reached.

        }

        [ScriptMethod(name: "Phase3 Range Of Spirit Taker 碎灵一击(分散)范围",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:40288"])]

        public void Phase3_Range_Of_Spirit_Taker_碎灵一击范围(Event @event, ScriptAccessory accessory)
        {

            if (parse!=32)
            {

                return;

            }

            for (int i = 0; i < 8; ++i)
            {

                var currentProperty = accessory.Data.GetDefaultDrawProperties();

                currentProperty.Name = "Phase3_Range_Of_Spirit_Taker_碎灵一击范围";
                currentProperty.Scale = new(5);
                currentProperty.Owner = accessory.Data.PartyList[i];
                currentProperty.Color = accessory.Data.DefaultDangerColor;
                currentProperty.Delay = 1250;
                currentProperty.DestoryAt = 2500;

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, currentProperty);

            }

            System.Threading.Thread.Sleep(1000);

            if (Enable_Text_Prompts)
            {

                if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                {

                    accessory.Method.TextInfo("分散", 2000);

                }

                if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                {

                    accessory.Method.TextInfo("Spread", 2000);

                }

            }

            if (Enable_Vanilla_TTS || Enable_Daily_Routines_TTS)
            {

                if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                {

                    accessory.TTS("分散", Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);

                }

                if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                {

                    accessory.TTS("Spread", Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);

                }

            }

        }

        [ScriptMethod(name: "Phase3 Guidance Of Spirit Taker 碎灵一击(分散)指路",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:40288"])]

        public void Phase3_Guidance_Of_Spirit_Taker_碎灵一击指路(Event @event, ScriptAccessory accessory)
        {

            if (parse!=32)
            {

                return;

            }

            foreach (var 视角 in GuidanceIndices(accessory)) 指路(视角);

            void 指路(int 视角)
            {
                bool targetPositionConfirmed = false;
                var currentProperty = accessory.Data.GetDefaultDrawProperties();

                currentProperty.Name = $"Phase3_Guidance_Of_Spirit_Taker_碎灵一击指路-{视角}";
                currentProperty.Scale = new(2);
                currentProperty.ScaleMode |= ScaleMode.YByDistance;
                currentProperty.Owner = GuidanceOwner(accessory, 视角);
                currentProperty.Color = accessory.Data.DefaultSafeColor;
                currentProperty.Delay = 1250;
                currentProperty.DestoryAt = 2500;

                var temporaryProperty = accessory.Data.GetDefaultDrawProperties();

                // LPDU 两组在西北、东南（正西、正东顺时针转 45°），碎灵一击按方框预分散：前排（T/近战）往两侧，后排（奶/远程）往斜后
                Vector3 point1 = RotatePoint(new Vector3(93f, 0f, 101f), new Vector3(100, 0, 100), float.Pi / 4);
                Vector3 point1Extension = RotatePoint(new Vector3(93f, 0f, 109f), new Vector3(100, 0, 100), float.Pi / 4);
                Vector3 point2 = RotatePoint(new Vector3(93f, 0f, 99f), new Vector3(100, 0, 100), float.Pi / 4);
                Vector3 point2Extension = RotatePoint(new Vector3(93f, 0f, 91f), new Vector3(100, 0, 100), float.Pi / 4);
                Vector3 point3 = RotatePoint(new Vector3(92f, 0f, 101f), new Vector3(100, 0, 100), float.Pi / 4);
                Vector3 point3Extension = RotatePoint(new Vector3(85.072f, 0f, 105f), new Vector3(100, 0, 100), float.Pi / 4);
                Vector3 point4 = RotatePoint(new Vector3(92f, 0f, 99f), new Vector3(100, 0, 100), float.Pi / 4);
                Vector3 point4Extension = RotatePoint(new Vector3(85.072f, 0f, 95f), new Vector3(100, 0, 100), float.Pi / 4);
                Vector3 point5 = RotatePoint(new Vector3(107f, 0f, 101f), new Vector3(100, 0, 100), float.Pi / 4);
                Vector3 point5Extension = RotatePoint(new Vector3(107f, 0f, 109f), new Vector3(100, 0, 100), float.Pi / 4);
                Vector3 point6 = RotatePoint(new Vector3(107f, 0f, 99f), new Vector3(100, 0, 100), float.Pi / 4);
                Vector3 point6Extension = RotatePoint(new Vector3(107f, 0f, 91f), new Vector3(100, 0, 100), float.Pi / 4);
                Vector3 point7 = RotatePoint(new Vector3(108f, 0f, 101f), new Vector3(100, 0, 100), float.Pi / 4);
                Vector3 point7Extension = RotatePoint(new Vector3(114.928f, 0f, 105f), new Vector3(100, 0, 100), float.Pi / 4);
                Vector3 point8 = RotatePoint(new Vector3(108f, 0f, 99f), new Vector3(100, 0, 100), float.Pi / 4);
                Vector3 point8Extension = RotatePoint(new Vector3(114.928f, 0f, 95f), new Vector3(100, 0, 100), float.Pi / 4);

                temporaryProperty = accessory.Data.GetDefaultDrawProperties();

                temporaryProperty.Name = $"Phase3_Rough_Guidance_Of_Spirit_Taker_碎灵一击粗略指路-{视角}";
                temporaryProperty.Scale = new(2);
                temporaryProperty.Position = point1;
                temporaryProperty.TargetPosition = point1Extension;
                temporaryProperty.ScaleMode |= ScaleMode.YByDistance;
                temporaryProperty.Color = Phase3_Colour_Of_Rough_Guidance.V4.WithW(1f);
                temporaryProperty.Delay = 1250;
                temporaryProperty.DestoryAt = 2500;

                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, temporaryProperty);

                temporaryProperty = accessory.Data.GetDefaultDrawProperties();

                temporaryProperty.Name = $"Phase3_Rough_Guidance_Of_Spirit_Taker_碎灵一击粗略指路-{视角}";
                temporaryProperty.Scale = new(2);
                temporaryProperty.Position = point2;
                temporaryProperty.TargetPosition = point2Extension;
                temporaryProperty.ScaleMode |= ScaleMode.YByDistance;
                temporaryProperty.Color = Phase3_Colour_Of_Rough_Guidance.V4.WithW(1f);
                temporaryProperty.Delay = 1250;
                temporaryProperty.DestoryAt = 2500;

                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, temporaryProperty);

                temporaryProperty = accessory.Data.GetDefaultDrawProperties();

                temporaryProperty.Name = $"Phase3_Rough_Guidance_Of_Spirit_Taker_碎灵一击粗略指路-{视角}";
                temporaryProperty.Scale = new(2);
                temporaryProperty.Position = point3;
                temporaryProperty.TargetPosition = point3Extension;
                temporaryProperty.ScaleMode |= ScaleMode.YByDistance;
                temporaryProperty.Color = Phase3_Colour_Of_Rough_Guidance.V4.WithW(1f);
                temporaryProperty.Delay = 1250;
                temporaryProperty.DestoryAt = 2500;

                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, temporaryProperty);

                temporaryProperty = accessory.Data.GetDefaultDrawProperties();

                temporaryProperty.Name = $"Phase3_Rough_Guidance_Of_Spirit_Taker_碎灵一击粗略指路-{视角}";
                temporaryProperty.Scale = new(2);
                temporaryProperty.Position = point4;
                temporaryProperty.TargetPosition = point4Extension;
                temporaryProperty.ScaleMode |= ScaleMode.YByDistance;
                temporaryProperty.Color = Phase3_Colour_Of_Rough_Guidance.V4.WithW(1f);
                temporaryProperty.Delay = 1250;
                temporaryProperty.DestoryAt = 2500;

                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, temporaryProperty);

                temporaryProperty = accessory.Data.GetDefaultDrawProperties();

                temporaryProperty.Name = $"Phase3_Rough_Guidance_Of_Spirit_Taker_碎灵一击粗略指路-{视角}";
                temporaryProperty.Scale = new(2);
                temporaryProperty.Position = point5;
                temporaryProperty.TargetPosition = point5Extension;
                temporaryProperty.ScaleMode |= ScaleMode.YByDistance;
                temporaryProperty.Color = Phase3_Colour_Of_Rough_Guidance.V4.WithW(1f);
                temporaryProperty.Delay = 1250;
                temporaryProperty.DestoryAt = 2500;

                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, temporaryProperty);

                temporaryProperty = accessory.Data.GetDefaultDrawProperties();

                temporaryProperty.Name = $"Phase3_Rough_Guidance_Of_Spirit_Taker_碎灵一击粗略指路-{视角}";
                temporaryProperty.Scale = new(2);
                temporaryProperty.Position = point6;
                temporaryProperty.TargetPosition = point6Extension;
                temporaryProperty.ScaleMode |= ScaleMode.YByDistance;
                temporaryProperty.Color = Phase3_Colour_Of_Rough_Guidance.V4.WithW(1f);
                temporaryProperty.Delay = 1250;
                temporaryProperty.DestoryAt = 2500;

                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, temporaryProperty);

                temporaryProperty = accessory.Data.GetDefaultDrawProperties();

                temporaryProperty.Name = $"Phase3_Rough_Guidance_Of_Spirit_Taker_碎灵一击粗略指路-{视角}";
                temporaryProperty.Scale = new(2);
                temporaryProperty.Position = point7;
                temporaryProperty.TargetPosition = point7Extension;
                temporaryProperty.ScaleMode |= ScaleMode.YByDistance;
                temporaryProperty.Color = Phase3_Colour_Of_Rough_Guidance.V4.WithW(1f);
                temporaryProperty.Delay = 1250;
                temporaryProperty.DestoryAt = 2500;

                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, temporaryProperty);

                temporaryProperty = accessory.Data.GetDefaultDrawProperties();

                temporaryProperty.Name = $"Phase3_Rough_Guidance_Of_Spirit_Taker_碎灵一击粗略指路-{视角}";
                temporaryProperty.Scale = new(2);
                temporaryProperty.Position = point8;
                temporaryProperty.TargetPosition = point8Extension;
                temporaryProperty.ScaleMode |= ScaleMode.YByDistance;
                temporaryProperty.Color = Phase3_Colour_Of_Rough_Guidance.V4.WithW(1f);
                temporaryProperty.Delay = 1250;
                temporaryProperty.DestoryAt = 2500;

                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, temporaryProperty);

                // LPDU 逐人定点：MT 往 A、OT 往 D、H1 H2 斜后；M1 往 C、M2 往 B、R1 R2 斜后
                // 换过组的人站和自己交换的那个人的点；多次交换时两边换出去的人按 MT>OT>H1>H2、M1>M2>R1>R2 依次配对
                if (phase3_numberOfDarkWaterIiiHasBeenProcessed == 6)
                {
                    Vector3[] 定点 = [point2Extension, point1Extension, point4Extension, point3Extension,
                                      point5Extension, point6Extension, point7Extension, point8Extension];
                    var 换出T奶 = Enumerable.Range(0, 4).Where(i => !phase3_locomotive_shouldGoLeft(i)).ToList();
                    var 换出DPS = Enumerable.Range(4, 4).Where(i => phase3_locomotive_shouldGoLeft(i)).ToList();
                    int 点位 = 视角;
                    int a = 换出T奶.IndexOf(视角), b = 换出DPS.IndexOf(视角);
                    if (a >= 0 && a < 换出DPS.Count) 点位 = 换出DPS[a];
                    else if (b >= 0 && b < 换出T奶.Count) 点位 = 换出T奶[b];
                    currentProperty.TargetPosition = 定点[点位];
                    targetPositionConfirmed = true;
                }

                if (targetPositionConfirmed)
                {

                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, currentProperty);

                }

            }
        }

        [ScriptMethod(name: "Phase3 Determine Initial Safe Positions Of Apocalypse 确定启示(地火)初始安全位置",
            eventType: EventTypeEnum.ObjectEffect,
            eventCondition: ["Id1:4", "Id2:regex:^(16|64)$"],
            userControl: false,
            suppress: 2000)]

        public void Phase3_Determine_Initial_Safe_Positions_Of_Apocalypse_确定启示初始安全位置(Event @event, ScriptAccessory accessory)
        {

            if (parse!=32)
            {

                return;

            }

            if (phase3_hasConfirmedInitialSafePositions)
            {

                return;

            }

            Vector3 position1OfTheSecond = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
            Vector3 position2OfTheSecond = RotatePoint(position1OfTheSecond, new Vector3(100, 0, 100), float.Pi);
            int clockwise = (@event["Id2"].Equals("64")) ? (-1) : (1);
            Vector3 position1OfTheLast = RotatePoint(position1OfTheSecond, new Vector3(100, 0, 100), float.Pi / 4 * 3 * clockwise);
            Vector3 position2OfTheLast = RotatePoint(position1OfTheSecond, new Vector3(100, 0, 100), float.Pi / 4 * 3 * clockwise + float.Pi);
            Vector3 position1OfThePenultimate = RotatePoint(position1OfTheSecond, new Vector3(100, 0, 100), float.Pi / 2 * clockwise);
            Vector3 position2OfThePenultimate = RotatePoint(position1OfTheSecond, new Vector3(100, 0, 100), float.Pi / 2 * clockwise + float.Pi);
            int direction1OfTheLast = PositionTo8Dir(position1OfTheLast, new Vector3(100, 0, 100));
            int direction1OfThePenultimate = PositionTo8Dir(position1OfThePenultimate, new Vector3(100, 0, 100));
            int direction1OfTheSecond = PositionTo8Dir(position1OfTheSecond, new Vector3(100, 0, 100));

            if (Enable_Developer_Mode)
            {

                accessory.Method.SendChat($"""
                                           /e 
                                           position1OfTheLast={position1OfTheLast}
                                           position2OfTheLast={position2OfTheLast}
                                           clockwise={clockwise}
                                           position1OfThePenultimate={position1OfThePenultimate}
                                           position2OfThePenultimate={position2OfThePenultimate}
                                           position1OfTheSecond={position1OfTheSecond}
                                           position2OfTheSecond={position2OfTheSecond}
                                           direction1OfTheLast={direction1OfTheLast}
                                           direction1OfThePenultimate={direction1OfThePenultimate}
                                           direction1OfTheSecond={direction1OfTheSecond}
                                           
                                           """);

            }

            // LPDU：两个安全点一定一个落在 T奶颜色的标点（A 1 D 4，正北到西南），一个落在 DPS 颜色（2 B 3 C）
            if (direction1OfTheLast == 0
               ||
               direction1OfTheLast == 7
               ||
               direction1OfTheLast == 6
               ||
               direction1OfTheLast == 5)
            {

                phase3_locomotive_initialSafePositionOfTheLeftGroup = position1OfTheLast;
                phase3_locomotive_leftPositionToStackOfTheSecondRound = new Vector3((position1OfTheLast.X - 100) / 3 + 100,
                                                                                  position1OfTheLast.Y,
                                                                                  (position1OfTheLast.Z - 100) / 3 + 100);
                phase3_locomotive_initialSafePositionOfTheRightGroup = position2OfTheLast;
                phase3_locomotive_rightPositionToStackOfTheSecondRound = new Vector3((position2OfTheLast.X - 100) / 3 + 100,
                                                                                   position2OfTheLast.Y,
                                                                                   (position2OfTheLast.Z - 100) / 3 + 100);

                phase3_hasConfirmedInitialSafePositions = true;

            }

            if (direction1OfTheLast == 1
               ||
               direction1OfTheLast == 2
               ||
               direction1OfTheLast == 3
               ||
               direction1OfTheLast == 4)
            {

                phase3_locomotive_initialSafePositionOfTheLeftGroup = position2OfTheLast;
                phase3_locomotive_leftPositionToStackOfTheSecondRound = new Vector3((position2OfTheLast.X - 100) / 3 + 100,
                                                                                  position2OfTheLast.Y,
                                                                                  (position2OfTheLast.Z - 100) / 3 + 100);
                phase3_locomotive_initialSafePositionOfTheRightGroup = position1OfTheLast;
                phase3_locomotive_rightPositionToStackOfTheSecondRound = new Vector3((position1OfTheLast.X - 100) / 3 + 100,
                                                                                   position1OfTheLast.Y,
                                                                                   (position1OfTheLast.Z - 100) / 3 + 100);

                phase3_hasConfirmedInitialSafePositions = true;

            }

        }

        [ScriptMethod(name: "P3_延迟咏唱回响_地火", eventType: EventTypeEnum.ObjectEffect, eventCondition: ["Id1:4", "Id2:regex:^(16|64)$"], suppress: 2000)]
        public void P3_延迟咏唱回响_地火(Event @event, ScriptAccessory accessory)
        {
            if (parse!=32) return;
            if (P3FloorFireDone) return;
            P3FloorFireDone = true;
            Vector3 centre = new(100, 0, 100);
            var pos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
            var clockwise = @event["Id2"] == "64" ? -1 : 1;
            var preTime = 100;
            //间隔11 2 2 2 2 2

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P3_二运_地火_中心";
            dp.Scale = new(9);
            dp.Position = centre;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 3000;
            dp.DestoryAt = 9700;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P3_二运_地火_起始点_11";
            dp.Scale = new(9);
            dp.Position = pos;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 3000;
            dp.DestoryAt = 12000 - preTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P3_二运_地火_起始点_12";
            dp.Scale = new(9);
            dp.Position = pos;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 17000 - preTime;
            dp.DestoryAt = 4000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P3_二运_地火_起始点_21";
            dp.Scale = new(9);
            dp.Position = RotatePoint(pos, centre, float.Pi);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 3000;
            dp.DestoryAt = 12000 - preTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P3_二运_地火_起始点_22";
            dp.Scale = new(9);
            dp.Position = RotatePoint(pos, centre, float.Pi);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 17000 - preTime;
            dp.DestoryAt = 4000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P3_二运_地火_第二点_11";
            dp.Scale = new(9);
            dp.Position = RotatePoint(pos, centre, float.Pi / 4 * clockwise);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 3000;
            dp.DestoryAt = 14000 - preTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P3_二运_地火_第二点_12";
            dp.Scale = new(9);
            dp.Position = RotatePoint(pos, centre, float.Pi / 4 * clockwise);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 19000 - preTime;
            dp.DestoryAt = 2000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P3_二运_地火_第二点_21";
            dp.Scale = new(9);
            dp.Position = RotatePoint(pos, centre, float.Pi / 4 * clockwise + float.Pi);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 3000;
            dp.DestoryAt = 14000 - preTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P3_二运_地火_第二点_22";
            dp.Scale = new(9);
            dp.Position = RotatePoint(pos, centre, float.Pi / 4 * clockwise + float.Pi);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 19000 - preTime;
            dp.DestoryAt = 2000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P3_二运_地火_第三点_11";
            dp.Scale = new(9);
            dp.Position = RotatePoint(pos, centre, float.Pi / 2 * clockwise);
            dp.Color = Phase3_Colour_Of_The_Penultimate_Apocalypse.V4.WithW(1f);
            dp.Delay = 3000;
            dp.DestoryAt = 8000 - preTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P3_二运_地火_第三点_12";
            dp.Scale = new(9);
            dp.Position = RotatePoint(pos, centre, float.Pi / 2 * clockwise);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 11000 - preTime;
            dp.DestoryAt = 8000 - preTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P3_二运_地火_第三点_21";
            dp.Scale = new(9);
            dp.Position = RotatePoint(pos, centre, float.Pi / 2 * clockwise + float.Pi);
            dp.Color = Phase3_Colour_Of_The_Penultimate_Apocalypse.V4.WithW(1f);
            dp.Delay = 3000;
            dp.DestoryAt = 8000 - preTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P3_二运_地火_第三点_22";
            dp.Scale = new(9);
            dp.Position = RotatePoint(pos, centre, float.Pi / 2 * clockwise + float.Pi);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 11000 - preTime;
            dp.DestoryAt = 8000 - preTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P3_二运_地火_第四点_11";
            dp.Scale = new(9);
            dp.Position = RotatePoint(pos, centre, float.Pi / 4 * 3 * clockwise);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 15000 - preTime;
            dp.DestoryAt = 6000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P3_二运_地火_第四点_21";
            dp.Scale = new(9);
            dp.Position = RotatePoint(pos, centre, float.Pi / 4 * 3 * clockwise + float.Pi);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 15000 - preTime;
            dp.DestoryAt = 6000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        }

        [ScriptMethod(name: "Phase3 Rough Guidance Of Initial Safe Positions 初始安全位置粗略指路",
            eventType: EventTypeEnum.ActionEffect,
            eventCondition: ["ActionId:40289"])]

        public void Phase3_Rough_Guidance_Of_Initial_Safe_Positions_初始安全位置粗略指路(Event @event, ScriptAccessory accessory)
        {

            if (parse!=32)
            {

                return;

            }

            foreach (var 视角 in GuidanceIndices(accessory)) 指路(视角);

            void 指路(int 视角)
            {
                bool targetPositionConfirmed = false;
                var currentProperty = accessory.Data.GetDefaultDrawProperties();

                currentProperty.Name = $"Phase3_Rough_Guidance_Of_Initial_Safe_Positions_初始安全位置粗略指路-{视角}";
                currentProperty.Scale = new(2);
                currentProperty.ScaleMode |= ScaleMode.YByDistance;
                currentProperty.Owner = GuidanceOwner(accessory, 视角);
                currentProperty.Color = Phase3_Colour_Of_Rough_Guidance.V4.WithW(1f);
                currentProperty.Delay = 500;
                currentProperty.DestoryAt = 6500;

                bool goLeft = phase3_locomotive_shouldGoLeft(视角);

                if (goLeft)
                {

                    currentProperty.TargetPosition = phase3_locomotive_initialSafePositionOfTheLeftGroup;

                    targetPositionConfirmed = true;

                }

                else
                {

                    currentProperty.TargetPosition = phase3_locomotive_initialSafePositionOfTheRightGroup;

                    targetPositionConfirmed = true;

                }

                if (targetPositionConfirmed)
                {

                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, currentProperty);

                }

            }
        }

        [ScriptMethod(name: "Phase3 Range Of Darkest Dance 暗夜舞蹈(最远死刑)范围",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:40181"])]

        public void Phase3_Range_Of_Darkest_Dance_暗夜舞蹈范围(Event @event, ScriptAccessory accessory)
        {

            if (parse!=32)
            {

                return;

            }

            if (!ParseObjectId(@event["SourceId"], out var sourceId))
            {

                return;

            }

            bool goBait = false;

            if (Phase3_Tank_Who_Baits_Darkest_Dance == Tanks.MT
               &&
               accessory.Data.PartyList.IndexOf(accessory.Data.Me) == 0)
            {

                goBait = true;

            }

            if (Phase3_Tank_Who_Baits_Darkest_Dance == Tanks.OT_ST
               &&
               accessory.Data.PartyList.IndexOf(accessory.Data.Me) == 1)
            {

                goBait = true;

            }

            var currentProperty = accessory.Data.GetDefaultDrawProperties();

            currentProperty.Name = "Phase3_Range_Of_Darkest_Dance_暗夜舞蹈范围";
            currentProperty.Scale = new(8);
            currentProperty.Owner = sourceId;
            currentProperty.CentreResolvePattern = PositionResolvePatternEnum.PlayerFarestOrder;
            currentProperty.Color = Phase3_Colour_Of_Darkest_Dance.V4.WithW(3f);
            currentProperty.Delay = 2200;
            currentProperty.DestoryAt = 4000;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, currentProperty);

            System.Threading.Thread.Sleep(2200);

            if (goBait)
            {

                if (Enable_Text_Prompts)
                {

                    if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                    {

                        accessory.Method.TextInfo("最远死刑", 1500);

                    }

                    if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                    {

                        accessory.Method.TextInfo("Stay away and bait", 1500);

                    }

                }

                if (Enable_Vanilla_TTS || Enable_Daily_Routines_TTS)
                {

                    if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                    {

                        accessory.TTS("最远死刑", Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);

                    }

                    if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                    {

                        accessory.TTS("Stay away and bait", Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);

                    }

                }

            }

            else
            {

                if (Phase3_Tank_Who_Baits_Darkest_Dance == Tanks.MT)
                {

                    if (Enable_Text_Prompts)
                    {

                        if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                        {

                            accessory.Method.TextInfo("远离MT", 1500);

                        }

                        if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                        {

                            accessory.Method.TextInfo("Stay away from MT", 1500);

                        }

                    }

                    if (Enable_Vanilla_TTS || Enable_Daily_Routines_TTS)
                    {

                        if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                        {

                            accessory.TTS("远离MT", Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);

                        }

                        if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                        {

                            accessory.TTS("Stay away from MT", Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);

                        }

                    }

                }

                if (Phase3_Tank_Who_Baits_Darkest_Dance == Tanks.OT_ST)
                {

                    if (Enable_Text_Prompts)
                    {

                        if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                        {

                            accessory.Method.TextInfo("远离ST", 1500);

                        }

                        if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                        {

                            accessory.Method.TextInfo("Stay away from OT", 1500);

                        }

                    }

                    if (Enable_Vanilla_TTS || Enable_Daily_Routines_TTS)
                    {

                        if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                        {

                            accessory.TTS("远离ST", Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);

                        }

                        if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                        {

                            accessory.TTS("Stay away from OT", Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);

                        }

                    }

                }

            }

        }

        [ScriptMethod(name: "Phase3 Guidance Of Darkest Dance 暗夜舞蹈(最远死刑)指路",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:40181"])]

        public void Phase3_Guidance_Of_Darkest_Dance_暗夜舞蹈指路(Event @event, ScriptAccessory accessory)
        {

            if (parse!=32)
            {

                return;

            }

            var tankWhoBaitsDarkestDance = accessory.Data.Objects.SearchById(accessory.Data.PartyList[1]);
            bool goBait = false;

            if (Phase3_Tank_Who_Baits_Darkest_Dance == Tanks.MT)
            {

                tankWhoBaitsDarkestDance = accessory.Data.Objects.SearchById(accessory.Data.PartyList[0]);

                if (accessory.Data.PartyList.IndexOf(accessory.Data.Me) == 0)
                {

                    goBait = true;

                }

            }

            if (Phase3_Tank_Who_Baits_Darkest_Dance == Tanks.OT_ST)
            {

                tankWhoBaitsDarkestDance = accessory.Data.Objects.SearchById(accessory.Data.PartyList[1]);

                if (accessory.Data.PartyList.IndexOf(accessory.Data.Me) == 1)
                {

                    goBait = true;

                }

            }

            if (tankWhoBaitsDarkestDance == null)
            {

                return;

            }

            // ----- Calculations of the position where the tank should bait -----
            // This part was directly inherited from Karlin's script.
            // The algorithm seems to be too mysterious to me, and it definitely works nice.
            // So as a result, except the position was tuned a bit towards the edge, I just keep the part as is.

            var dir8 = P3FloorFire % 10 % 4;
            Vector3 posN = new(100, 0, 86);
            var rot = dir8 switch
            {
                0 => 6,
                1 => 7,
                2 => 0,
                3 => 5
            };
            var pos1 = RotatePoint(posN, new(100, 0, 100), float.Pi / 4 * rot);
            var pos2 = RotatePoint(posN, new(100, 0, 100), float.Pi / 4 * rot + float.Pi);
            var dealpos = ((pos1 - tankWhoBaitsDarkestDance.Position).Length() < (pos2 - tankWhoBaitsDarkestDance.Position).Length()) ? (pos1) : (pos2);

            Vector3 positionToBait = new Vector3((dealpos.X - 100) / 3 * 4 + 100,
                                               dealpos.Y,
                                               (dealpos.Z - 100) / 3 * 4 + 100);

            // ----- -----

            var currentProperty = accessory.Data.GetDefaultDrawProperties();

            if (goBait)
            {

                currentProperty.Owner = accessory.Data.Me;
                currentProperty.Color = accessory.Data.DefaultSafeColor;

            }

            else
            {

                if (Phase3_Tank_Who_Baits_Darkest_Dance == Tanks.MT)
                {

                    currentProperty.Owner = accessory.Data.PartyList[0];
                    currentProperty.Color = Phase3_Colour_Of_Darkest_Dance.V4.WithW(1f);

                }

                else
                {

                    currentProperty.Owner = accessory.Data.PartyList[1];
                    currentProperty.Color = Phase3_Colour_Of_Darkest_Dance.V4.WithW(1f);

                }

            }

            currentProperty.Name = "Phase3_Guidance_Of_Darkest_Dance_暗夜舞蹈指路";
            currentProperty.Scale = new(2);
            currentProperty.ScaleMode |= ScaleMode.YByDistance;
            currentProperty.TargetPosition = positionToBait;
            currentProperty.Delay = 2200;
            currentProperty.DestoryAt = 4000;

            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, currentProperty);

        }

        [ScriptMethod(name: "P3_延迟咏唱回响_击退提示", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:40182", "TargetIndex:1"])]
        public void P3_延迟咏唱回响_击退提示(Event @event, ScriptAccessory accessory)
        {
            if (parse!=32) return;
            if (!ParseObjectId(@event["SourceId"], out var sid)) return;

            foreach (var 视角 in GuidanceIndices(accessory)) 指路(视角);

            void 指路(int 视角)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"P3_延迟咏唱回响_击退提示1-{视角}";
                dp.Scale = new(2, 21);
                dp.Owner = GuidanceOwner(accessory, 视角);
                dp.TargetObject = sid;
                dp.Rotation = float.Pi;
                dp.Color = accessory.Data.DefaultDangerColor.WithW(3);
                dp.DestoryAt = 3000;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);

                dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"P3_延迟咏唱回响_击退提示2-{视角}";
                dp.Scale = new(2);
                dp.Owner = sid;
                dp.TargetObject = GuidanceOwner(accessory, 视角);
                dp.ScaleMode |= ScaleMode.YByDistance;
                dp.Color = accessory.Data.DefaultDangerColor.WithW(3);
                dp.DestoryAt = 3000;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
            }
        }
        [ScriptMethod(name: "P3_延迟咏唱回响_地火记录", eventType: EventTypeEnum.ObjectEffect, eventCondition: ["Id1:4", "Id2:regex:^(16|64)$"], userControl: false)]
        public void P3_延迟咏唱回响_地火记录(Event @event, ScriptAccessory accessory)
        {
            if (parse!=32) return;
            lock (this)
            {
                if (P3FloorFire != -1) return;
                Vector3 centre = new(100, 0, 100);
                var pos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
                P3FloorFire = PositionTo8Dir(pos, new(100, 0, 100));
                P3FloorFire += @event["Id2"] == "64" ? 10 : 20;
            }

        }

        [ScriptMethod(name: "Phase3 Determine The Final Position Of The Boss 确定Boss的最终位置",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:40300"],
            userControl: false)]

        public void Phase3_Determine_The_Final_Position_Of_The_Boss_确定Boss的最终位置(Event @event, ScriptAccessory accessory)
        {

            if (parse!=32)
            {

                return;

            }

            phase3_finalPositionOfTheBoss = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);

        }

        [ScriptMethod(name: "Phase3 Initial Position Of The Boss In Phase4 P4时Boss的初始位置",
            eventType: EventTypeEnum.ActionEffect,
            eventCondition: ["ActionId:40300"])]

        public void Phase3_Initial_Position_Of_The_Boss_In_Phase4_P4时Boss的初始位置(Event @event, ScriptAccessory accessory)
        {

            if (parse!=32)
            {

                return;

            }

            if (phase3_finalPositionOfTheBoss.Equals(new Vector3(100, 0, 100)))
            {

                return;

            }

            bool inTheNorth = true;

            if (phase3_finalPositionOfTheBoss.Z <= 100)
            {

                inTheNorth = false;

            }

            if (phase3_finalPositionOfTheBoss.Z >= 100)
            {

                inTheNorth = true;

            }

            var currentProperty = accessory.Data.GetDefaultDrawProperties();

            currentProperty.Name = "Phase3_Initial_Position_Of_The_Boss_In_Phase4_P4时Boss的初始位置";
            currentProperty.Scale = new(7);
            currentProperty.Position = (inTheNorth) ? (new Vector3(100, 0, 90)) : (new Vector3(100, 0, 110));
            currentProperty.Color = accessory.Data.DefaultSafeColor;
            currentProperty.DestoryAt = 9250;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, currentProperty);

            System.Threading.Thread.Sleep(2000);

            if (Enable_Text_Prompts)
            {

                if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                {

                    accessory.Method.TextInfo(((inTheNorth) ? ("Boss即将出现在正北") : ("Boss即将出现在正南")), 7250);

                }

                if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                {

                    accessory.Method.TextInfo(((inTheNorth) ? ("The Boss will appear in the north") : ("The Boss will appear in the south")), 7250);

                }

            }

            if (Enable_Vanilla_TTS || Enable_Daily_Routines_TTS)
            {

                if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                {

                    accessory.TTS($"{((inTheNorth) ? ("Boss即将出现在正北") : ("Boss即将出现在正南"))}",
                        Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);

                }

                if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                {

                    accessory.TTS(
                        $"{((inTheNorth) ? ("The Boss will appear in the north") : ("The Boss will appear in the south"))}",
                        Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);

                }

            }

        }

        private int MyLampIndex(int myPartyIndex)
        {
            var nLampIndex = 0;
            for (int i = 0; i < 8; i++)
            {
                if (P3Lamp[i] == 1 && P3Lamp[(i + 3) % 8] == 1 && P3Lamp[(i + 5) % 8] == 1)
                {
                    nLampIndex = i;
                    break;
                }
            }
            // LPDU Y 字时间压缩：黄灯连成 Y，nLampIndex 是 Y 的竖（相对南），两臂中间是相对北
            // 相对方位 = nLampIndex + 偏移：北 4、东北 5、东 6、东南 7、南 0、西南 1、西 2、西北 3
            // T奶：冰/短火 北，中火 西，长火 西北/东北；DPS：短火 西南/东南，中火 东，冰/长火 南
            // 同 debuff 的两个人按排队顺序分左右（T奶 H1 MT OT H2，DPS R1 M1 M2 R2），排在前面的去左边
            var buff = P3FireBuff[myPartyIndex];
            if (buff == 0) return -1;
            var 排队 = myPartyIndex < 4 ? new[] { 2, 0, 1, 3 } : new[] { 6, 4, 5, 7 };
            var 搭档 = 排队.FirstOrDefault(i => i != myPartyIndex && P3FireBuff[i] == buff, -1);
            bool 去左边 = 搭档 < 0 || Array.IndexOf(排队, myPartyIndex) < Array.IndexOf(排队, 搭档);
            int 偏移 = (myPartyIndex < 4, buff) switch
            {
                (true, 1 or 4) => 4,
                (true, 2) => 2,
                (true, 3) => 去左边 ? 3 : 5,
                (false, 1) => 去左边 ? 1 : 7,
                (false, 2) => 6,
                _ => 0,
            };
            return (nLampIndex + 偏移) % 8;
        }
        #region P3 补丁 (Usami)

        #region P3.1 时间压缩

        [ScriptMethod(name: "时间压缩阶段转换（DEBUG ONLY）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(40266)$"],
            userControl: Debugging)]
        public void UlrPhaseChange(Event ev, ScriptAccessory sa)
        {
            _fruPhase = FruPhase.P3A_UltimateRelativity;
            sa.DebugMsg($"当前阶段为：{_fruPhase}", Enable_Developer_Mode);
            _ct.Init(sa, "时间压缩");
            _usamiPd.Init(sa, "时间压缩");
            _usamiEvents = Enumerable
                .Range(0, 20)
                .Select(_ => new ManualResetEvent(false))
                .ToList();
        }

        [ScriptMethod(name: "*P3 时间压缩 眩晕时自动面向", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:4163"],
            userControl: true, suppress: 10000)]
        public void UlrAutoFace(Event ev, ScriptAccessory sa)
        {
            if (!P3眩晕自动面向) return;
            if (_fruPhase != FruPhase.P3A_UltimateRelativity) return;
            // 面朝自己的 LPDU 站位方向（背对场中）
            var myDir = MyLampIndex(sa.GetMyIndex());
            if (myDir < 0) return;
            sa.Log.Debug($"眩晕，触发自动面向。");
            sa.SetRotation(sa.Data.MyObject, (myDir * 45f).DegToRad().Game2Logic());
        }

        #endregion P3.1 时间压缩

        #region P3.2 地火

        [ScriptMethod(name: "启示录阶段转换（DEBUG ONLY）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(40269)$"],
            userControl: Debugging)]
        public void ApoPhaseChange(Event @ev, ScriptAccessory sa)
        {
            _fruPhase = FruPhase.P3B_Apocalypse;
            _usamiPd.Init(sa, "启示录");
            _apo.Init(sa, _usamiPd);
            // LPDU：同种水的两人里值小的去左组（西北），即 H2 H1 OT MT M1 M2 R1 R2，与 phase3_locomotive_priority_asAConstant 一致
            _usamiPd.AddPriorities([3, 2, 1, 0, 4, 5, 6, 7]);
            _usamiEvents = Enumerable
                .Range(0, 20)
                .Select(_ => new ManualResetEvent(false))
                .ToList();
            sa.DebugMsg($"当前阶段为：{_fruPhase}", Enable_Developer_Mode);
        }

        [ScriptMethod(name: "水分摊类型记录（DEBUG ONLY）", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2461"], userControl: Debugging)]
        public void DarkWaterTypeRecord(Event ev, ScriptAccessory sa)
        {
            if (_fruPhase != FruPhase.P3B_Apocalypse) return;
            var dur = ev.DurationMilliseconds();
            var tidx = sa.GetPlayerIdIndex(ev.TargetId);

            // 10s, 29s, 38s
            const uint waterShort = 10000 - 2000;
            const uint waterMid = 29000 - 2000;
            const uint waterLong = 38000 - 2000;

            lock (_usamiPd)
            {
                if (dur > waterShort)
                    _usamiPd.AddPriority(tidx, 10);
                if (dur > waterMid)
                    _usamiPd.AddPriority(tidx, 10);
                if (dur > waterLong)
                    _usamiPd.AddPriority(tidx, 10);
                _usamiPd.AddActionCount();

                if (_usamiPd.ActionCount == 6)
                {
                    _apo.Grouping();
                }
            }
        }

        [ScriptMethod(name: "水分摊范围提示", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2461"], userControl: true)]
        public void DarkWaterRange(Event ev, ScriptAccessory sa)
        {
            if (_fruPhase != FruPhase.P3B_Apocalypse) return;
            _usamiEvents[(int)EventIdx.ApoGrouping].WaitOne();

            var tid = ev.TargetId;
            var tidx = sa.GetPlayerIdIndex(tid);
            var dur = ev.DurationMilliseconds();

            var isSameGroup = _apo.GetMyGroup() == _apo.GetPlayerGroup(tidx);

            var dp = sa.DrawCircle(tid, 6, (int)dur - 3000, 3000, $"Usami-狂水{tidx}", true);
            dp.Color = isSameGroup ? sa.Data.DefaultSafeColor : sa.Data.DefaultDangerColor;
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        [ScriptMethod(name: "分组交换提示", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:40270"],
         userControl: true)]
        public void DarkWaterSwapHint(Event @ev, ScriptAccessory sa)
        {
            if (_fruPhase != FruPhase.P3B_Apocalypse) return;
            _usamiEvents[(int)EventIdx.ApoGrouping].WaitOne();

            foreach (var 视角 in GuidanceIndices(sa)) 指路(视角);

            void 指路(int 视角)
            {
                var myIndex = 视角;
                var myPreviousGroup = myIndex < 4 ? 0 : 1;     // 0 左（T奶西北） 1 右（DPS东南）
                var myCurrentGroup = _apo.GetPlayerGroup(myIndex);

                var dp = sa.DrawGuidance(GuidanceOwner(sa, 视角), _apo.IsInLeftGroup(myIndex) ? phase3_leftGroupPosition_asAConstant : phase3_rightGroupPosition_asAConstant, 0, 3000, $"Usami-狂水位置-{视角}");
                sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                var str = myPreviousGroup == myCurrentGroup ? "不换" : "交换";
                if (IsMe(sa, 视角)) if (Enable_Text_Prompts) sa.Method.TextInfo($"{str}（同组同水时优先级高的换）", 3000, true);
                sa.DebugMsg(str, Enable_Developer_Mode);
            }
        }

        [ScriptMethod(name: "碎灵一击范围与分散提示", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40288"], userControl: true)]
        public void SpiritTakerHint(Event @ev, ScriptAccessory sa)
        {
            if (_fruPhase != FruPhase.P3B_Apocalypse) return;
            var myIndex = sa.GetMyIndex();
            for (int i = 0; i < 8; i++)
            {
                var dp = sa.DrawCircle(sa.Data.PartyList[i], 5, 1000, 2000, $"Usami-碎灵一击范围");
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }

            // 左组（T奶）集合点：LPDU 两组在西北、东南，整套分散点从正西、正东顺时针转 45°
            Vector3 leftCenter = new Vector3(92, 0, 100).RotatePoint(Center, 45f.DegToRad());
            Vector3 rightCenter = leftCenter.PointCenterSymmetry(Center);
            List<Vector3> spreadTargetPos = Enumerable.Repeat(new Vector3(0, 0, 0), 20).ToList();

            List<float> rot = [45f, 45f, 225f, 225f, -15f, -15f, -75f, -75f];

            for (int i = 0; i < 8; i += 2)
            {
                spreadTargetPos[i] = leftCenter.ExtendPoint(rot[i].DegToRad(), 8f);
                spreadTargetPos[i + 1] = spreadTargetPos[i].PointCenterSymmetry(Center);
            }

            sa.DebugMsg($"我在左 {_apo.IsInLeftGroup(myIndex)}，我在右{_apo.IsInRightGroup(myIndex)}", Enable_Developer_Mode);

            for (int i = 0; i < 8; i++)
            {
                // 考虑到可能得预站位问题，废弃掉特定路线分散。
                var isSafe = (_apo.IsInLeftGroup(myIndex) && i % 2 == 0) || (_apo.IsInRightGroup(myIndex) && i % 2 == 1);
                var startPos = spreadTargetPos[i].PointInOutside(i % 2 == 0 ? leftCenter : rightCenter, 6f);
                var dp = sa.DrawGuidance(startPos, spreadTargetPos[i], 0, 3000, $"碎灵一击分散", isSafe: isSafe);
                sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Arrow, dp);
            }
        }

        [ScriptMethod(name: "地火分散位置指路", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:40288"], userControl: true, suppress: 10000)]
        public void ApoSpreadGuidance(Event @ev, ScriptAccessory sa)
        {
            if (_fruPhase != FruPhase.P3B_Apocalypse) return;

            foreach (var 视角 in GuidanceIndices(sa)) 指路(视角);

            void 指路(int 视角)
            {
                // LPDU：本组在自己颜色、紧挨地火起点（旋转反方向一侧）的标点分散
                // 远程（奶、远程DPS）站标点外侧左右两个点；近战（T、近战DPS）站内圈：标点方向和再往旋转反方向一格的方向
                // SafePoints[0/2] 是两个标点方向（[0]+3)%8<4 的那个在正北到西南，归左组），[1/3] 是各自往旋转反方向再偏一格
                var myIndex = 视角;
                int safeDir = (_apo.SafePoints[0] + 3) % 8;
                int myMarkIdx = _apo.IsInLeftGroup(myIndex) ? (safeDir < 4 ? 0 : 2) : (safeDir >= 4 ? 0 : 2);
                bool isMelee = myIndex is 0 or 1 or 4 or 5;

                sa.DebugMsg($"玩家的安全标点方位为{_apo.SafePoints[myMarkIdx]}，{(isMelee ? "近战站内圈" : "远程站外圈")}。", Enable_Developer_Mode);

                for (int i = 0; i < 4; i++)
                {
                    List<Vector3> safePos = _apo.GetSafePos(_apo.SafePoints[i]);
                    for (int j = 0; j < (i % 2 == 0 ? 3 : 1); j++)
                    {
                        // GetSafePos：[0] 内圈，[1][2] 外圈
                        bool isSafe = (i == myMarkIdx || i == myMarkIdx + 1) && (j == 0) == isMelee;

                        if (IsMe(sa, 视角)) sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, sa.DrawStaticCircle(safePos[j], isSafe ? sa.Data.DefaultSafeColor.WithW(3f) : sa.Data.DefaultDangerColor.WithW(3f), 0, 10000, $"Usami-分散位dir{i}-idx{j}", 0.5f));
                    }
                }
            }
        }

        [ScriptMethod(name: "地火类型记录（DEBUG ONLY）", eventType: EventTypeEnum.ObjectEffect, eventCondition: ["Id1:4", "Id2:regex:^(16|64)$"], userControl: Debugging)]
        public void ExaflareTypeRecord(Event ev, ScriptAccessory sa)
        {
            if (_fruPhase != FruPhase.P3B_Apocalypse) return;

            lock (_apo)
            {
                if (_apo.ActionCount >= 2) return;
                var northApoDir = ev.SourcePosition.Position2Dirs(Center, 8);
                var rot = ev.Id2();
                _apo.AddActionCount();

                // 记录北方开始点与旋转方向
                if ((northApoDir + 3) % 8 < 4)
                {
                    _apo.NorthStartPoint = northApoDir;
                    _apo.RotationDir = rot == 0x16 ? 1 : -1;
                }

                if (_apo.ActionCount != 2) return;
                _apo.GetSafeDirs();
            }
        }

        [ScriptMethod(name: "暗夜舞蹈引导指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40181"], userControl: true)]
        public void ApoDarkDanceGuidance(Event ev, ScriptAccessory sa)
        {
            if (_fruPhase != FruPhase.P3B_Apocalypse) return;
            // LPDU：坦克去离第一次地火起点 90° 的最终安全点引导，即 SafePoints[1]/[3] 方向

            // 两个点都画，箭头指出去即可
            var tpos1 = new Vector3(100, 0, 80).RotatePoint(Center, (_apo.SafePoints[1] * 45f).DegToRad());
            var tpos2 = new Vector3(100, 0, 80).RotatePoint(Center, (_apo.SafePoints[3] * 45f).DegToRad());

            var isTank = sa.Data.MyObject is { } o && o.IsTank();

            if (Enable_Text_Prompts) sa.Method.TextInfo(isTank ? $"准备引导" : $"避开引导", 3000, true);

            var dp1 = sa.DrawGuidance(tpos1.PointInOutside(Center, 13f), tpos1, 0, 3000, $"Usami-暗夜舞蹈引导位1", scale: 3f, isSafe: false);
            var dp2 = sa.DrawGuidance(tpos2.PointInOutside(Center, 13f), tpos2, 0, 3000, $"Usami-暗夜舞蹈引导位2", scale: 3f, isSafe: false);
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp1);
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp2);
            var dp11 = sa.DrawGuidance(tpos1.PointInOutside(Center, 13f), tpos1, 3000, 2000, $"Usami-暗夜舞蹈引导位1", scale: 3f, isSafe: isTank);
            var dp22 = sa.DrawGuidance(tpos2.PointInOutside(Center, 13f), tpos2, 3000, 2000, $"Usami-暗夜舞蹈引导位2", scale: 3f, isSafe: isTank);
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp11);
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp22);

            var dp = sa.DrawTargetNearFarOrder(ev.SourceId, 1, false, 8, 8, 3000, 2000, $"Usami-暗夜舞蹈目标");
            dp.Color = isTank ? sa.Data.DefaultSafeColor : sa.Data.DefaultDangerColor;
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        [ScriptMethod(name: "暗夜舞蹈击退方向指引", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:40181", "TargetIndex:1"], userControl: true)]
        public void ApoDarkDanceKnockBackGuidance(Event ev, ScriptAccessory sa)
        {
            if (_fruPhase != FruPhase.P3B_Apocalypse) return;
            var sid = ev.SourceId;
            // 将盖娅分成左右
            var isLeft = _apo.IsInLeftGroup(sa.GetMyIndex());
            var dp0 = sa.DrawGuidance(sid, 0, 1500, 3500, $"Usami-击退方向左", -155f.DegToRad().Ccw2Cw(), 3f, isSafe: isLeft);
            var dp1 = sa.DrawGuidance(sid, 0, 1500, 3500, $"Usami-击退方向右", 155f.DegToRad().Ccw2Cw(), 3f, isSafe: !isLeft);
            dp0.Scale = new Vector2(3f, 14f);
            dp1.Scale = new Vector2(3f, 14f);
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp0);
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp1);
        }

        public class Apocalypse
        {
            // ReSharper disable once NullableWarningSuppressionIsUsed
            public ScriptAccessory accessory { get; set; } = null!;
            public int ActionCount { get; set; } = 0;
            public int NorthStartPoint { get; set; } = -1;
            public List<int> SafePoints { get; set; } = [-1, -1, -1, -1];
            public int RotationDir { get; set; } = 0; // 1顺，-1逆
            // ReSharper disable once NullableWarningSuppressionIsUsed
            public List<KeyValuePair<int, int>> Group { get; set; } = null!;
            // ReSharper disable once NullableWarningSuppressionIsUsed
            public List<KeyValuePair<int, int>> TempGroup { get; set; } = null!;
            // ReSharper disable once NullableWarningSuppressionIsUsed
            public PriorityDict Priorities { get; set; } = null!;
            public void Init(ScriptAccessory _accessory, PriorityDict _priorities)
            {
                accessory = _accessory;
                Priorities = _priorities;
                ActionCount = 0;
                NorthStartPoint = -1;
                SafePoints = [-1, -1, -1, -1];
                RotationDir = 0;
            }

            public void AddActionCount()
            {
                ActionCount++;
            }

            public void Grouping()
            {
                TempGroup = Priorities.SelectSmallPriorityIndices(8);

                var str = "";
                str += $"Group初始左：{TempGroup[0].Key}, {TempGroup[2].Key}, {TempGroup[4].Key}, {TempGroup[6].Key}\n";
                str += $"Group初始右：{TempGroup[1].Key}, {TempGroup[3].Key}, {TempGroup[5].Key}, {TempGroup[7].Key}\n";
                accessory.DebugMsg(str, Enable_Developer_Mode);

                // tempGroup 的排序方式为：左无，右无，左短水，右短水，左中水，右中水，左长水，右长水。
                Group = [.. TempGroup];

                var strLeft = $"{accessory.GetPlayerJobByIndex(Group[0].Key)},{accessory.GetPlayerJobByIndex(Group[2].Key)},{accessory.GetPlayerJobByIndex(Group[4].Key)},{accessory.GetPlayerJobByIndex(Group[6].Key)}";
                var strRight = $"{accessory.GetPlayerJobByIndex(Group[1].Key)},{accessory.GetPlayerJobByIndex(Group[3].Key)},{accessory.GetPlayerJobByIndex(Group[5].Key)},{accessory.GetPlayerJobByIndex(Group[7].Key)}";
                accessory.DebugMsg($"\n启示录分摊分组：\n左组：{strLeft}\n右组：{strRight}", Enable_Developer_Mode);

                _usamiEvents[(int)EventIdx.ApoGrouping].Set();
            }

            public void GetSafeDirs()
            {
                /*      
                * 将计算出的安全区 (dir+3)%8，0123为北侧，4567为南侧。
                *           3
                *       2       4
                *   1               5
                *       0       6
                *           7
                */

                // 北地火旋转方向相反一格
                var dir = (NorthStartPoint + 8 - RotationDir) % 8;
                bool isNorthSafePoint = (dir + 3) % 8 < 4;

                // 两组分散用的安全标点
                SafePoints[isNorthSafePoint ? 0 : 2] = dir;
                SafePoints[isNorthSafePoint ? 2 : 0] = GetSymmetricPoint(dir);

                // 再往旋转反方向偏一格：近战内圈的第二个点，也是最后坦克引导最远死刑的方向
                SafePoints[1] = (SafePoints[0] + 8 - RotationDir) % 8;
                SafePoints[3] = (SafePoints[2] + 8 - RotationDir) % 8;

                accessory.DebugMsg($"\n安全标点：左组{SafePoints[0]}，右组{SafePoints[2]}。\n内圈第二点：左组{SafePoints[1]}，右组{SafePoints[3]}。", Enable_Developer_Mode);
            }

            public int GetSymmetricPoint(int dir)
            {
                // 地火顺时针旋转，安全区在逆时针一格。
                return (dir + 8 + 4) % 8;
            }

            public int GetPlayerGroupIdx(int idx)
            {
                // 在Fixed情况下，该函数返回左前、右前、左后、右后的分组。
                return Group.FindIndex(i => i.Key == idx);
            }
            public int GetMyGroupIdx() => GetPlayerGroupIdx(accessory.GetMyIndex());
            public int GetPlayerGroup(int idx) => GetPlayerGroupIdx(idx) % 2;
            public int GetMyGroup() => GetPlayerGroup(accessory.GetMyIndex());
            public bool IsInLeftGroup(int idx) => GetPlayerGroupIdx(idx) % 2 == 0;
            public bool IsInRightGroup(int idx) => GetPlayerGroupIdx(idx) % 2 == 1;

            public List<Vector3> GetSafePos(int dir)
            {
                List<Vector3> safePosList = [new(100, 0, 90.2f), new(104.5f, 0, 80.84f), new(95.5f, 0, 80.84f)];
                for (int i = 0; i < 3; i++)
                    safePosList[i] = safePosList[i].RotatePoint(Center, 45f.DegToRad() * dir);
                return safePosList;
            }

            public void ShowMessage()
            {
                var str = "\n ---- [启示录] ----\n";
                str += $"\n安全标点：左组{SafePoints[0]}，右组{SafePoints[2]}。\n内圈第二点：左组{SafePoints[1]}，右组{SafePoints[3]}。";

                var strLeft = $"{accessory.GetPlayerJobByIndex(Group[0].Key)},{accessory.GetPlayerJobByIndex(Group[2].Key)},{accessory.GetPlayerJobByIndex(Group[4].Key)},{accessory.GetPlayerJobByIndex(Group[6].Key)}";
                var strRight = $"{accessory.GetPlayerJobByIndex(Group[1].Key)},{accessory.GetPlayerJobByIndex(Group[3].Key)},{accessory.GetPlayerJobByIndex(Group[5].Key)},{accessory.GetPlayerJobByIndex(Group[7].Key)}";

                str += $"\n启示录分摊分组：\n左组：{strLeft}\n右组：{strRight}";
                accessory.DebugMsg(str, Enable_Developer_Mode);
            }
        }

        #endregion P3.2 地火

        #endregion

        #endregion

        #region Phase_4

        [ScriptMethod(name: "----- Phase 4 ----- (No actual meaning for this toggle/此开关无实际意义)",
            eventType: EventTypeEnum.NpcYell,
            eventCondition: ["Send these, the homeless, tempest-tost to me",
                            "送来那些无家可归，被风吹雨淋的人"])]

        public void Phase4_Placeholder(Event @event, ScriptAccessory accessory) { }

        [ScriptMethod(name: "P4_具象化_分P", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40246"], userControl: false)]
        public void P4_具象化_分P(Event @event, ScriptAccessory accessory)
        {
            parse=41;
        }
        [ScriptMethod(name: "P4_时间结晶_记忆水晶收集", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40174"], userControl: false)]
        public void P4_时间结晶_记忆水晶收集(Event @event, ScriptAccessory accessory)
        {
            if (!ParseObjectId(@event["SourceId"], out var sid)) return;
            P4FragmentId = sid;
        }
        [ScriptMethod(name: "P4_具象化_天光轮回", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40237"])]
        public void P4_具象化_天光轮回(Event @event, ScriptAccessory accessory)
        {
            if (!ParseObjectId(@event["SourceId"], out var sid)) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P4_具象化_天光轮回";
            dp.Scale = new(4);
            dp.Owner = sid;
            dp.TargetObject = sid;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 8000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        [ScriptMethod(name: "Phase4 Prompt Before Akh Rhai 天光轮回前提示",
            eventType: EventTypeEnum.ActionEffect,
            eventCondition: ["ActionId:40246"])]

        public void Phase4_Prompt_Before_Akh_Rhai_天光轮回前提示(Event @event, ScriptAccessory accessory)
        {

            if (Enable_Text_Prompts)
            {

                if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                {

                    accessory.Method.TextInfo("集合并远离未来的碎片", 9500);

                }

                if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                {

                    accessory.Method.TextInfo("Get together and stay away from Fragment of Fate", 9500);

                }

            }

            if (Enable_Vanilla_TTS || Enable_Daily_Routines_TTS)
            {

                if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                {

                    accessory.TTS("集合并远离未来的碎片", Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);

                }

                if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                {

                    accessory.TTS("Get together and stay away from Fragment of Fate", Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);

                }

            }

        }

        [ScriptMethod(name: "Phase4 Prompt To Dodge Akh Rhai 天光轮回躲避提示",
            eventType: EventTypeEnum.ActionEffect,
            eventCondition: ["ActionId:40186"])]

        public void Phase4_Prompt_To_Dodge_Akh_Rhai_天光轮回躲避提示(Event @event, ScriptAccessory accessory)
        {

            if (Enable_Text_Prompts)
            {

                if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                {

                    accessory.Method.TextInfo("跑！", 3000);

                }

                if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                {

                    accessory.Method.TextInfo("Run!", 3000);

                }

            }

            if (Enable_Vanilla_TTS || Enable_Daily_Routines_TTS)
            {

                if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                {

                    accessory.TTS("跑！", Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);

                }

                if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                {

                    accessory.TTS("Run!", Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);

                }

            }

        }

        [ScriptMethod(name: "P4_暗光龙诗_分P", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40239"], userControl: false)]
        public void P4_暗光龙诗_分P(Event @event, ScriptAccessory accessory)
        {
            parse=42;
            P4Tether = [-1, -1, -1, -1, -1, -1, -1, -1];
            P4Stack = [0, 0, 0, 0, 0, 0, 0, 0];
            P4TetherDone = false;
            phase4_1_ManualReset.Reset();
            phase4_1_TetherCount = 0;
        }

        [ScriptMethod(name: "Phase4 Initial Position Before Darklit Dragonsong 暗光龙诗前预站位",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:40239"])]

        public void Phase4_Initial_Position_Before_Darklit_Dragonsong_暗光龙诗前预站位(Event @event, ScriptAccessory accessory)
        {

            if (parse!=41
               &&
               parse!=42)
            {

                return;

            }

            // LPDU 预站位（真北）：奶在北、T在西、DPS 在南/东（DPS 只为看线方便，可以随便站）
            List<Vector3> initialPosition = [
                new Vector3(93f,0f,102f),       // MT
                new Vector3(93f,0f,104f),       // OT
                new Vector3(98.7f,0f,97.8f),    // H1
                new Vector3(101.3f,0f,97.8f),   // H2
                new Vector3(98f,0f,111.8f),     // M1
                new Vector3(103.7f,0f,110.9f),  // M2
                new Vector3(108.1f,0f,103.3f),  // R1
                new Vector3(106.9f,0f,107.6f),  // R2
            ];

            int myIndex = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            var currentProperty = accessory.Data.GetDefaultDrawProperties();

            for (int i = 0; i < initialPosition.Count; ++i)
            {

                currentProperty = accessory.Data.GetDefaultDrawProperties();

                currentProperty.Name = "Phase4_Initial_Position_Before_Darklit_Dragonsong_暗光龙诗前预站位";
                currentProperty.Scale = new(0.5f);
                currentProperty.Position = initialPosition[i];
                currentProperty.Color = (i == myIndex) ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
                currentProperty.DestoryAt = 5500;

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, currentProperty);

            }

            foreach (var 视角 in GuidanceIndices(accessory)) 指路(视角);

            void 指路(int 视角)
            {
                currentProperty = accessory.Data.GetDefaultDrawProperties();

                currentProperty.Name = $"Phase4_Initial_Position_Before_Darklit_Dragonsong_暗光龙诗前预站位-{视角}";
                currentProperty.Scale = new(2);
                currentProperty.ScaleMode |= ScaleMode.YByDistance;
                currentProperty.Owner = GuidanceOwner(accessory, 视角);
                currentProperty.TargetPosition = initialPosition[视角];
                currentProperty.Color = accessory.Data.DefaultSafeColor;
                currentProperty.DestoryAt = 5500;

                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, currentProperty);

            }
        }

        [ScriptMethod(name: "P4_暗光龙诗_Buff记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2461"], userControl: false)]
        public void P4_暗光龙诗_Buff记录(Event @event, ScriptAccessory accessory)
        {
            if (parse!=42) return;
            if (!ParseObjectId(@event["TargetId"], out var tid)) return;
            var tIndex = accessory.Data.PartyList.IndexOf(((uint)tid));
            P4Stack[tIndex] = 1;
        }
        [ScriptMethod(name: "P4_暗光龙诗_连线收集", eventType: EventTypeEnum.Tether, eventCondition: ["Id:006E"], userControl: false)]
        public void P4_暗光龙诗_连线收集(Event @event, ScriptAccessory accessory)
        {
            if (parse!=42) return;
            if (!ParseObjectId(@event["SourceId"], out var sid)) return;
            if (!ParseObjectId(@event["TargetId"], out var tid)) return;
            var sIndex = accessory.Data.PartyList.IndexOf(((uint)sid));
            var tIndex = accessory.Data.PartyList.IndexOf(((uint)tid));
            lock (this)
            {
                P4Tether[sIndex] = tIndex;
                phase4_1_TetherCount++;
                if (phase4_1_TetherCount == 4)
                {
                    phase4_1_ManualReset.Set();
                }
            }


        }
        [ScriptMethod(name: "P4_暗光龙诗_引导扇形", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40187"])]
        public void P4_暗光龙诗_引导扇形(Event @event, ScriptAccessory accessory)
        {
            if (parse!=42) return;
            if (!ParseObjectId(@event["SourceId"], out var sid)) return;
            for (uint i = 1; i < 5; i++)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = "P4_暗光龙诗_引导扇形";
                dp.Scale = new(20);
                dp.Radian = float.Pi / 3;
                dp.Owner = sid;
                dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
                dp.TargetOrderIndex = i;
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.Delay = 4000;
                dp.DestoryAt = 5000;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
            }
        }
        [ScriptMethod(name: "P4_暗光龙诗_碎灵一击", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:40187"])]
        public void P4_暗光龙诗_碎灵一击(Event @event, ScriptAccessory accessory)
        {
            if (parse!=42) return;

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P4_暗光龙诗_碎灵一击_水晶";
            dp.Scale = new(8.5f);
            dp.Owner = P4FragmentId;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 3500;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            for (int i = 0; i < 8; i++)
            {
                dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = "P4_暗光龙诗_碎灵一击";
                dp.Scale = new(5);
                dp.Owner = accessory.Data.PartyList[i];
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.DestoryAt = 3500;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }
        }
        [ScriptMethod(name: "P4_暗光龙诗_神圣之翼", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4022[78])$"])]
        public void P4_暗光龙诗_神圣之翼(Event @event, ScriptAccessory accessory)
        {
            if (parse!=42) return;
            if (!ParseObjectId(@event["SourceId"], out var sid)) return;

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P4_暗光龙诗_神圣之翼";
            dp.Scale = new(40, 20);
            dp.Owner = sid;
            dp.Rotation = @event["ActionId"] == "40227" ? float.Pi / 2 : float.Pi / -2;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 5000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

        }
        // LPDU 光暗龙诗分组：连线 4 人固定 1T 1奶 2D，首尾相连成一圈
        // 塔：奶去北塔，圈里和奶不相连的那个人也去北塔；和奶相连的两人去南塔
        // 闲人：T奶在西（奶北、T南），DPS 在东（M1>M2>R1>R2 优先级高的在南）
        // 两个水一个在连线、一个在闲人：闲人的水和塔里的水同在北或同在南时，闲人水所在那一列南北互换
        // 最后北塔两人 + 西北 + 东北 在北分摊，其余在南
        // 闲人站位：0 西北、1 西南、2 东北、3 东南
        private (List<int> 北组, List<int> 南组, int[] 闲人站位) P4龙诗LPDU分组()
        {
            var 连线 = Enumerable.Range(0, 8).Where(i => P4Tether[i] != -1).ToList();
            var 闲人 = Enumerable.Range(0, 8).Where(i => P4Tether[i] == -1).ToList();
            if (连线.Count != 4 || 闲人.Count != 4) return ([], [], [-1, -1, -1, -1]);
            int 奶 = 连线.FirstOrDefault(i => i is 2 or 3, 连线[0]);
            var 南组 = 连线.Where(i => P4Tether[i] == 奶 || P4Tether[奶] == i).ToList();
            var 北组 = 连线.Except(南组).ToList();
            var 闲D = 闲人.Where(i => i >= 4).ToList();
            int[] 站位 = [闲人.FirstOrDefault(i => i is 2 or 3, -1), 闲人.FirstOrDefault(i => i is 0 or 1, -1), 闲D.ElementAtOrDefault(1), 闲D.ElementAtOrDefault(0)];
            int 线水 = 连线.FirstOrDefault(i => P4Stack[i] == 1, -1);
            int 闲水位 = Array.FindIndex(站位, i => i >= 0 && P4Stack[i] == 1);
            if (线水 >= 0 && 闲水位 >= 0 && 北组.Contains(线水) == (闲水位 % 2 == 0))
            {
                int 列 = 闲水位 / 2 * 2;
                (站位[列], 站位[列 + 1]) = (站位[列 + 1], 站位[列]);
            }
            北组.AddRange([站位[0], 站位[2]]);
            南组.AddRange([站位[1], 站位[3]]);
            return (北组, 南组, 站位);
        }

        [ScriptMethod(name: "P4_暗光龙诗_水分摊", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4022[78])$"])]
        public void P4_暗光龙诗_水分摊(Event @event, ScriptAccessory accessory)
        {
            if (parse!=42) return;
            var (upGroup, _, _) = P4龙诗LPDU分组();
            var myindex = accessory.Data.PartyList.IndexOf(accessory.Data.Me);

            for (int i = 0; i < 8; i++)
            {
                if (P4Stack[i] != 1) continue;
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = "P4_暗光龙诗_分摊";
                dp.Scale = new(6);
                dp.Owner = accessory.Data.PartyList[i];
                dp.Color = upGroup.Contains(i) == upGroup.Contains(myindex) ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
                dp.DestoryAt = 5000;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }

            var dpFragment = accessory.Data.GetDefaultDrawProperties();
            dpFragment.Name = "P4_暗光龙诗_分摊_水晶";
            dpFragment.Scale = new(9.5f);
            dpFragment.Owner = P4FragmentId;
            dpFragment.Color = accessory.Data.DefaultDangerColor;
            dpFragment.DestoryAt = 5000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dpFragment);
        }
        [ScriptMethod(name: "P4_暗光龙诗_无尽顿悟", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40249"])]
        public void P4_暗光龙诗_无尽顿悟(Event @event, ScriptAccessory accessory)
        {
            if (!ParseObjectId(@event["SourceId"], out var sid)) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P4_暗光龙诗_无尽顿悟";
            dp.Scale = new(4);
            dp.Owner = sid;
            dp.CentreResolvePattern = PositionResolvePatternEnum.OwnerTarget;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 6000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        #region 远近跳
        [ScriptMethod(name: "P4_暗光龙诗_远跳", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40283"])]
        public void P4_暗光龙诗_远跳(Event @event, ScriptAccessory accessory)
        {
            if (parse!=42) return;
            if (!ParseObjectId(@event["SourceId"], out var sid)) return;

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P4_暗光龙诗_远跳";
            dp.Scale = new(8);
            dp.Owner = sid;
            dp.CentreResolvePattern = PositionResolvePatternEnum.PlayerFarestOrder;
            dp.Color = Phase4_Colour_Of_Somber_Dance.V4.WithW(3f);
            dp.Delay = 2000;
            dp.DestoryAt = 3000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);



        }
        [ScriptMethod(name: "P4_暗光龙诗_近跳", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:40284"])]
        public void P4_暗光龙诗_近跳(Event @event, ScriptAccessory accessory)
        {
            if (parse!=42) return;
            var pos = JsonConvert.DeserializeObject<Vector3>(@event["TargetPosition"]);

            var dp2 = accessory.Data.GetDefaultDrawProperties();
            dp2.Name = "P4_暗光龙诗_近跳";
            dp2.Scale = new(8);
            dp2.Position = pos;
            dp2.CentreResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
            dp2.Color = Phase4_Colour_Of_Somber_Dance.V4.WithW(3f);
            dp2.DestoryAt = 3500;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp2);

        }
        #endregion
        [ScriptMethod(name: "P4_暗光龙诗_光之束缚_保持距离提示", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:40271"], suppress: 2000)]
        public void P4_暗光龙诗_光之束缚_保持距离提示(Event @event, ScriptAccessory accessory)
        {
            if (parse!=42) return;
            if (P4Tether[accessory.Data.PartyList.IndexOf(accessory.Data.Me)] == -1) return;
            if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
            {
                if (Enable_Text_Prompts) accessory.Method.TextInfo("线未消失,保持距离", 1500);
                accessory.TTS($"线未消失,保持距离",
                    Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);
            }

            if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
            {
                if (Enable_Text_Prompts) accessory.Method.TextInfo("The tether is still, keep your distance", 1500);
                accessory.TTS($"The tether is still, keep your distance",
                    Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);
            }
        }
        [ScriptMethod(name: "P4_暗光龙诗_塔处理位置", eventType: EventTypeEnum.Tether, eventCondition: ["Id:006E"])]
        public void P4_暗光龙诗_塔处理位置(Event @event, ScriptAccessory accessory)
        {
            if (parse!=42) return;

            if (!ParseObjectId(@event["SourceId"], out var sid)) return;
            if (!Debugging && sid != accessory.Data.Me) return;

            System.Threading.Thread.MemoryBarrier();

            phase4_1_ManualReset.WaitOne();

            System.Threading.Thread.MemoryBarrier();

            var (upGroup, _, _) = P4龙诗LPDU分组();
            var myIndex = accessory.Data.PartyList.IndexOf((uint)sid);
            Vector3 dealpos = upGroup.Contains(myIndex) ? new(100, 0, 92) : new(100, 0, 108);

            var dur = 10000;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"P4_暗光龙诗_塔处理位置-{myIndex}";
            dp.Scale = new(2);
            dp.ScaleMode |= ScaleMode.YByDistance;
            dp.Owner = GuidanceOwner(accessory, myIndex);
            dp.TargetPosition = dealpos;
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.DestoryAt = dur;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"P4_暗光龙诗_塔处理位置-{myIndex}";
            dp.Scale = new(4);
            dp.ScaleMode |= ScaleMode.YByDistance;
            dp.Position = dealpos;
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.DestoryAt = dur;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
        }

        [ScriptMethod(name: "P4_暗光龙诗_引导处理位置", eventType: EventTypeEnum.Tether, eventCondition: ["Id:006E"], suppress: 2000)]
        public void P4_暗光龙诗_引导处理位置(Event @event, ScriptAccessory accessory)
        {
            if (parse!=42) return;

            System.Threading.Thread.MemoryBarrier();

            phase4_1_ManualReset.WaitOne();

            System.Threading.Thread.MemoryBarrier();

            // 闲人站位（西北、西南、东北、东南），引导最近的扇形
            var (_, _, 站位) = P4龙诗LPDU分组();
            Vector3[] 位置 = [new(095.8f, 0, 098.0f), new(095.8f, 0, 102.0f), new(104.2f, 0, 098.0f), new(104.2f, 0, 102.0f)];

            foreach (var 视角 in GuidanceIndices(accessory)) 指路(视角);

            void 指路(int 视角)
            {
                var slot = Array.IndexOf(站位, 视角);
                if (slot < 0) return;

                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"P4_暗光龙诗_引导处理位置-{视角}";
                dp.Scale = new(2);
                dp.ScaleMode |= ScaleMode.YByDistance;
                dp.Owner = GuidanceOwner(accessory, 视角);
                dp.TargetPosition = 位置[slot];
                dp.Color = accessory.Data.DefaultSafeColor;
                dp.DestoryAt = 10000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }
        }

        [ScriptMethod(name: "P4_暗光龙诗_分摊处理位置", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4022[78])$"])]
        public void P4_暗光龙诗_分摊处理位置(Event @event, ScriptAccessory accessory)
        {
            if (parse!=42) return;
            var (upGroup, _, _) = P4龙诗LPDU分组();

            foreach (var 视角 in GuidanceIndices(accessory)) 指路(视角);

            void 指路(int 视角)
            {
                Vector3 dealpos = new(@event["ActionId"] == "40227" ? 105 : 95, 0, upGroup.Contains(视角) ? 92.5f : 107.5f);

                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"P4_暗光龙诗_分摊处理位置-{视角}";
                dp.Scale = new(2);
                dp.ScaleMode |= ScaleMode.YByDistance;
                dp.Owner = GuidanceOwner(accessory, 视角);
                dp.TargetPosition = dealpos;
                dp.Color = accessory.Data.DefaultSafeColor;
                dp.DestoryAt = 5000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }
        }

        public class CrystallizeTime
        {
            public ScriptAccessory Sa { get; set; } = null!;
            public PriorityDict Pr { get; set; } = null!;
            public ulong LeftWyrmSid { get; set; } = 0;
            public ulong RightWyrmSid { get; set; } = 0;
            public int LeftIcePlayerIdx { get; set; } = -1;
            public int RightIcePlayerIdx { get; set; } = -1;
            public int LeftWindPlayerIdx { get; set; } = -1;
            public int RightWindPlayerIdx { get; set; } = -1;

            public void Init(ScriptAccessory accessory, PriorityDict priorityDict)
            {
                Sa = accessory;
                Pr = priorityDict;
                LeftWyrmSid = 0;
                RightWyrmSid = 0;
                LeftIcePlayerIdx = -1;
                RightIcePlayerIdx = -1;
                LeftWindPlayerIdx = -1;
                RightWindPlayerIdx = -1;
            }
        }

        public class PriorityDict
        {
            // ReSharper disable once NullableWarningSuppressionIsUsed
            public ScriptAccessory sa { get; set; } = null!;
            // ReSharper disable once NullableWarningSuppressionIsUsed
            public Dictionary<int, int> Priorities { get; set; } = null!;
            public string Annotation { get; set; } = "";
            public int ActionCount { get; set; } = 0;

            public void Init(ScriptAccessory accessory, string annotation, int partyNum = 8)
            {
                sa = accessory;
                Priorities = new Dictionary<int, int>();
                for (var i = 0; i < partyNum; i++)
                {
                    Priorities.Add(i, 0);
                }
                Annotation = annotation;
                ActionCount = 0;
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
                    sa.Log.Error("输入的列表与内部设置长度不同");

                for (var i = 0; i < Priorities.Count; i++)
                    AddPriority(i, priorities[i]);
            }

            /// <summary>
            /// 输出优先级字典的Key与优先级
            /// </summary>
            /// <returns></returns>
            public string ShowPriorities()
            {
                var str = $"{Annotation} 优先级字典：\n";
                foreach (var pair in Priorities)
                {
                    str += $"Key {pair.Key} ({sa.GetPlayerJobByIndex(pair.Key)}), Value {pair.Value}\n";
                }
                sa.Log.Debug(str);
                return str;
            }

            public string PrintAnnotation()
            {
                sa.Log.Debug(Annotation);
                return Annotation;
            }

            public PriorityDict DeepCopy()
            {
                return JsonConvert.DeserializeObject<PriorityDict>(JsonConvert.SerializeObject(this)) ?? new PriorityDict();
            }

            public void AddActionCount(int count = 1)
            {
                ActionCount += count;
            }

            public bool IsActionCountEqualTo(int times)
            {
                return ActionCount == times;
            }
        }

        [ScriptMethod(name: "P4_时间结晶_分P", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40240"], userControl: false)]
        public void P4_时间结晶_分P(Event @event, ScriptAccessory accessory)
        {
            parse=43;

            _pd.Init(accessory, "时间结晶");
            _cry.Init(accessory, _pd);
            _events = [.. Enumerable.Range(0, 20).Select(_ => new System.Threading.ManualResetEvent(false))];

            // 数字越小越靠西：LPDU 与启示录同一套 H2 H1 OT MT M1 M2 R1 R2
            _pd.AddPriorities([3, 2, 1, 0, 4, 5, 6, 7]);

            P4ClawBuff = [0, 0, 0, 0, 0, 0, 0, 0];
            phase4_numberOfMajorDebuffsHaveBeenCounted = 0;
            phase4_semaphoreMajorDebuffsWereConfirmed = new System.Threading.AutoResetEvent(false);
            phase4_numberOfIncidentalDebuffsHaveBeenCounted = 0;
            phase4_semaphoreIncidentalDebuffsWereConfirmed = new System.Threading.AutoResetEvent(false);
            P4OtherBuff = [0, 0, 0, 0, 0, 0, 0, 0];
            P4WaterPos = [];
            phase4_id1OfTheDrachenWanderers = "";
            phase4_id2OfTheDrachenWanderers = "";
            phase4_timesTheWyrmclawDebuffWasRemoved = 0;
            phase4_residueIdsFromEastToWest = [0, 0, 0, 0];
            phase4_guidanceOfResiduesHasBeenGenerated = false;
        }
        [ScriptMethod(name: "P4_时间结晶_Buff收集", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(326[34]|2454|246[0123])$"], userControl: false)]
        public void P4_时间结晶_Buff收集(Event @event, ScriptAccessory accessory)
        {
            if (parse!=43) return;
            var id = @event["StatusID"];
            if (!ParseObjectId(@event["TargetId"], out var tid)) return;
            var index = accessory.Data.PartyList.IndexOf(((uint)tid));
            //3623红爪 1短2长
            if (id == "3263")
            {
                if (!float.TryParse(@event["Duration"], out float dur)) return;
                P4ClawBuff[index] = dur > 20 ? 2 : 1;
                _pd.AddPriority(index, 0);      // 红 +0
            }

            if (id == "3264")
            {
                P4ClawBuff[index] = 3;
                _pd.AddPriority(index, 100);    // 蓝 +100
            }
            //暗 4
            if (id == "2460")
            {
                P4OtherBuff[index] = 4;
                _pd.AddPriority(index, 40);     // 暗 +40
            }
            //水 3
            if (id == "2461")
            {
                P4OtherBuff[index] = 3;
                _pd.AddPriority(index, 20);     // 水 +20
            }
            //冰 1
            if (id == "2462")
            {
                P4OtherBuff[index] = 1;
                _pd.AddPriority(index, 0);      // 冰 +0
            }
            //风 2
            if (id == "2463")
            {
                P4OtherBuff[index] = 2;
                _pd.AddPriority(index, 10);     // 风 +10
            }
            //土 5
            if (id == "2454")
            {
                P4OtherBuff[index] = 5;
                _pd.AddPriority(index, 30);     // 土 +30
            }

            System.Threading.Thread.MemoryBarrier();

            if (id.Equals("3263")||id.Equals("3264"))
            {

                lock (phase4_readwriteLockOfMajorDebuffCounter_AsAConstant)
                {

                    ++phase4_numberOfMajorDebuffsHaveBeenCounted;

                    System.Threading.Thread.MemoryBarrier();

                    if (phase4_numberOfMajorDebuffsHaveBeenCounted == 8)
                    {

                        phase4_semaphoreMajorDebuffsWereConfirmed.Set();
                        _events[0].Set();   // 蓝红记录完毕
                    }

                }

            }

            if (id.Equals("2460")
               ||
               id.Equals("2461")
               ||
               id.Equals("2462")
               ||
               id.Equals("2463")
               ||
               id.Equals("2454"))
            {

                lock (phase4_readwriteLockOfIncidentalDebuffCounter_AsAConstant)
                {

                    ++phase4_numberOfIncidentalDebuffsHaveBeenCounted;

                    System.Threading.Thread.MemoryBarrier();

                    if (phase4_numberOfIncidentalDebuffsHaveBeenCounted == 8)
                    {

                        phase4_semaphoreIncidentalDebuffsWereConfirmed.Set();
                        _events[1].Set();   // 属性记录完毕
                    }

                }

            }

        }

        [ScriptMethod(name: "P4_时间结晶_计算分组归属",
            eventType: EventTypeEnum.ActionEffect,
            eventCondition: ["ActionId:40298"],
            userControl: false,
            suppress: 10000)]

        public void P4_时间结晶_计算分组归属(Event @event, ScriptAccessory accessory)
        {
            if (parse!=43) return;

            _events[0].WaitOne();
            _events[1].WaitOne();
            /*
            *   优先级值中，个位数对应职业优先级，十位数对应属性Buff，百位数对应红蓝Buff
            *   个位数：依TDH, HTD, HTDH的设置而不同
            *   十位数：冰+0，风+10，水+20，土+30，暗+40
            *   百位数：红+0，蓝+100
            *   升序排列后，可得到：[左红冰，右红冰，左红风，右红风，蓝冰，蓝水，蓝土，蓝暗]
            */
            _cry.LeftIcePlayerIdx = _pd.SelectSpecificPriorityIndex(0).Key;
            _cry.RightIcePlayerIdx = _pd.SelectSpecificPriorityIndex(1).Key;
            _cry.LeftWindPlayerIdx = _pd.SelectSpecificPriorityIndex(2).Key;
            _cry.RightWindPlayerIdx = _pd.SelectSpecificPriorityIndex(3).Key;
            accessory.Log.Debug($"记录下左红冰{_cry.LeftIcePlayerIdx}，右红冰{_cry.RightIcePlayerIdx}，左红风{_cry.LeftWindPlayerIdx}，右红风{_cry.RightWindPlayerIdx}");

            _events[2].Set();   // P4二运优先级记录完毕
        }

        [ScriptMethod(name: "P4_时间结晶_蓝线收集", eventType: EventTypeEnum.Tether, eventCondition: ["Id:0085"], userControl: false)]
        public void P4_时间结晶_蓝线收集(Event @event, ScriptAccessory accessory)
        {
            if (parse!=43) return;
            var pos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
            P4BlueTether = PositionTo6Dir(pos, new(100, 0, 100)) % 3;
        }
        [ScriptMethod(name: "P4_时间结晶_灯AOE", eventType: EventTypeEnum.Tether, eventCondition: ["Id:0085"])]
        public void P4_时间结晶_灯AOE(Event @event, ScriptAccessory accessory)
        {
            if (parse!=43) return;
            var pos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
            Vector3 normalPos = new(pos.X, 0, 200 - pos.Z);
            Vector3 fastPos = new(100, 0, pos.Z > 100 ? 111 : 89);
            uint actualDuration = (0 <= Phase4_Drawing_Duration_Of_Normal_And_Delayed_Lights && Phase4_Drawing_Duration_Of_Normal_And_Delayed_Lights <= 13) ?
                                (uint)(1000 * Phase4_Drawing_Duration_Of_Normal_And_Delayed_Lights) :
                                3000;

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P4_时间结晶_灯AOE_快";
            dp.Scale = new(12);
            dp.Position = fastPos;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 7500;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P4_时间结晶_灯AOE_中";
            dp.Scale = new(12);
            dp.Position = normalPos;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 13000 - actualDuration;
            dp.DestoryAt = actualDuration;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P4_时间结晶_灯AOE_慢";
            dp.Scale = new(12);
            dp.Position = pos;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 18000 - actualDuration;
            dp.DestoryAt = actualDuration;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        [ScriptMethod(name: "P4_时间结晶_土分摊范围", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2454"])]
        public void P4_时间结晶_土分摊范围(Event @event, ScriptAccessory accessory)
        {
            if (parse!=43) return;
            if (!ParseObjectId(@event["TargetId"], out var tid)) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P4_时间结晶_土分摊范围";
            dp.Scale = new(6);
            dp.Owner = tid;
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.Delay = 14000;
            dp.DestoryAt = 3000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P4_时间结晶_土分摊范围_水晶";
            dp.Scale = new(9.5f);
            dp.Owner = P4FragmentId;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 14000;
            dp.DestoryAt = 3000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        [ScriptMethod(name: "P4_时间结晶_碎灵一击", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2452"])]
        public void P4_时间结晶_碎灵一击(Event @event, ScriptAccessory accessory)
        {
            if (parse!=43) return;
            if (!ParseObjectId(@event["TargetId"], out var tid)) return;
            if (tid != accessory.Data.Me) return;

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P4_时间结晶_碎灵一击_水晶";
            dp.Scale = new(8.5f);
            dp.Owner = P4FragmentId;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 3500;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            for (int i = 0; i < 8; i++)
            {
                dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = "P4_时间结晶_碎灵一击";
                dp.Scale = new(5);
                dp.Owner = accessory.Data.PartyList[i];
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.DestoryAt = 3500;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }


        }
        [ScriptMethod(name: "P4_时间结晶_Buff处理位置", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40293"])]
        public void P4_时间结晶_Buff处理位置(Event @event, ScriptAccessory accessory)
        {

            //buff后3.5s
            if (parse!=43) return;
            foreach (var 视角 in GuidanceIndices(accessory)) 指路(视角);

            void 指路(int 视角)
            {
                var myIndex = 视角;
                //短红
                if (P4ClawBuff[myIndex] == 1)
                {
                    // LPDU：同种红 debuff 两人按 H2 H1 OT MT M1 M2 R1 R2 排，靠前的去西（isHigh）
                    bool isHigh = P4LPDU东西优先级.First(i => P4ClawBuff[i] == P4ClawBuff[myIndex]) == myIndex;

                    Vector3 dealpos = isHigh ? new(87, 0, 100) : new(113, 0, 100);

                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"P4_时间结晶_Buff处理位置_撞龙头-{视角}";
                    dp.Scale = new(2);
                    dp.ScaleMode |= ScaleMode.YByDistance;
                    dp.Owner = GuidanceOwner(accessory, 视角);
                    dp.TargetPosition = dealpos;
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.DestoryAt = 10500;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                    //高分摊 088 085 -> 093 082
                    //高闲   081 103 -> 081 097
                    //低分摊 112 085 -> 107 082
                    //低闲   119 103 -> 119 97
                    Vector3 dealpos2 = isHigh ? (P4BlueTether == 1 ? new(081, 0, 103) : new(088, 0, 085)) : (P4BlueTether == 1 ? new(112, 0, 085) : new(119, 0, 103));
                    Vector3 dealpos3 = isHigh ? (P4BlueTether == 1 ? new(081, 0, 097) : new(093, 0, 082)) : (P4BlueTether == 1 ? new(107, 0, 082) : new(119, 0, 097));

                    dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"P4_时间结晶_Buff处理位置_pos2预连线-{视角}";
                    dp.Scale = new(2);
                    dp.ScaleMode |= ScaleMode.YByDistance;
                    dp.Position = dealpos;
                    dp.TargetPosition = dealpos2;
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.DestoryAt = 10500;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                    dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"P4_时间结晶_Buff处理位置_pos2位置-{视角}";
                    dp.Scale = new(2);
                    dp.ScaleMode |= ScaleMode.YByDistance;
                    dp.Owner = GuidanceOwner(accessory, 视角);
                    dp.TargetPosition = dealpos2;
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.Delay = 10500;
                    dp.DestoryAt = 3000;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                    dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"P4_时间结晶_Buff处理位置_pos3预连线-{视角}";
                    dp.Scale = new(2);
                    dp.ScaleMode |= ScaleMode.YByDistance;
                    dp.Position = dealpos2;
                    dp.TargetPosition = dealpos3;
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.DestoryAt = 13500;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                    dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"P4_时间结晶_Buff处理位置_pos3位置-{视角}";
                    dp.Scale = new(2);
                    dp.ScaleMode |= ScaleMode.YByDistance;
                    dp.Owner = GuidanceOwner(accessory, 视角);
                    dp.TargetPosition = dealpos3;
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.Delay = 13500;
                    dp.DestoryAt = 3000;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                }
                //长红
                if (P4ClawBuff[myIndex] == 2)
                {
                    // LPDU：同种红 debuff 两人按 H2 H1 OT MT M1 M2 R1 R2 排，靠前的去西（isHigh）
                    bool isHigh = P4LPDU东西优先级.First(i => P4ClawBuff[i] == P4ClawBuff[myIndex]) == myIndex;

                    Vector3 dealpos1 = isHigh ? new(088.5f, 0, 115.5f) : new(111.5f, 0, 115.5f);
                    Vector3 dealpos2 = isHigh ? new(090.2f, 0, 117.0f) : new(109.8f, 0, 117.0f);
                    Vector3 dealpos3 = isHigh ? new(092.5f, 0, 118.0f) : new(107.5f, 0, 118.0f);
                    Vector3 dealpos4 = isHigh ? new(092.53f, 0, 110.40f) : new(107.47f, 0, 110.40f);
                    // The previous coordinates were: isHigh ? new(092.0f, 0, 110.0f) : new(108.0f, 0, 110.0f);

                    // ----- 0s -> 7.5s -----

                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"P4_时间结晶_Buff处理位置_躲ac-{视角}";
                    dp.Scale = new(2);
                    dp.ScaleMode |= ScaleMode.YByDistance;
                    dp.Owner = GuidanceOwner(accessory, 视角);
                    dp.TargetPosition = dealpos1;
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.DestoryAt = 7500;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                    dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"P4_时间结晶_Buff处理位置_躲ac->击退-{视角}";
                    dp.Scale = new(2);
                    dp.ScaleMode |= ScaleMode.YByDistance;
                    dp.Position = dealpos1;
                    dp.TargetPosition = dealpos2;
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.DestoryAt = 7500;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                    // ----- -----

                    // ----- 7.5s -> 10.5s -----

                    dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"P4_时间结晶_Buff处理位置_击退-{视角}";
                    dp.Scale = new(2);
                    dp.ScaleMode |= ScaleMode.YByDistance;
                    dp.Owner = GuidanceOwner(accessory, 视角);
                    dp.TargetPosition = dealpos2;
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.Delay = 7500;
                    dp.DestoryAt = 3000;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                    dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"P4_时间结晶_Buff处理位置_击退->躲斜点-{视角}";
                    dp.Scale = new(2);
                    dp.ScaleMode |= ScaleMode.YByDistance;
                    dp.Position = dealpos2;
                    dp.TargetPosition = dealpos3;
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.DestoryAt = 10500;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                    // ----- -----

                    // ----- 10.5s -> 13s -----

                    dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"P4_时间结晶_Buff处理位置_躲斜点-{视角}";
                    dp.Scale = new(2);
                    dp.ScaleMode |= ScaleMode.YByDistance;
                    dp.Owner = GuidanceOwner(accessory, 视角);
                    dp.TargetPosition = dealpos3;
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.Delay = 10500;
                    dp.DestoryAt = 2500;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                    dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"P4_时间结晶_Buff处理位置_躲斜点->撞头-{视角}";
                    dp.Scale = new(2);
                    dp.ScaleMode |= ScaleMode.YByDistance;
                    dp.Position = dealpos3;
                    dp.TargetPosition = dealpos4;
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.DestoryAt = 13000;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                    // ----- -----

                    // ----- 13s -> 16s -----

                    dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"P4_时间结晶_Buff处理位置_撞头-{视角}";
                    dp.Scale = new(2);
                    dp.ScaleMode |= ScaleMode.YByDistance;
                    dp.Owner = GuidanceOwner(accessory, 视角);
                    dp.TargetPosition = dealpos4;
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.Delay = 13000;
                    dp.DestoryAt = 3000;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                    // ----- -----

                    // There were some issues in the guidance here which is for the players with long Wyrmclaw debuff.
                    // Cicero has adjusted the process a little bit, and the issues has been fixed now.

                }
                //蓝
                if (P4ClawBuff[myIndex] == 3)
                {
                    if (P4OtherBuff[myIndex] == 4)
                    {
                        Vector3 dealpos1 = P4BlueTether == 1 ? new(112, 0, 85) : new(88, 0, 85);
                        var dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Name = $"P4_时间结晶_Buff处理位置_躲灯1-{视角}";
                        dp.Scale = new(2);
                        dp.ScaleMode |= ScaleMode.YByDistance;
                        dp.Owner = GuidanceOwner(accessory, 视角);
                        dp.TargetPosition = dealpos1;
                        dp.Color = accessory.Data.DefaultSafeColor;
                        dp.DestoryAt = 14500;
                        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                    }
                    else
                    {
                        Vector3 dealpos1 = P4BlueTether == 1 ? new(88, 0, 115) : new(112, 0, 115);
                        Vector3 dealpos2 = P4BlueTether == 1 ? new(090.8f, 0, 116.0f) : new(109.2f, 0, 116.0f);
                        var dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Name = $"P4_时间结晶_Buff处理位置_躲灯ac-{视角}";
                        dp.Scale = new(2);
                        dp.ScaleMode |= ScaleMode.YByDistance;
                        dp.Owner = GuidanceOwner(accessory, 视角);
                        dp.TargetPosition = dealpos1;
                        dp.Color = accessory.Data.DefaultSafeColor;
                        dp.DestoryAt = 7500;
                        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                        dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Name = $"P4_时间结晶_Buff处理位置_躲ac->击退-{视角}";
                        dp.Scale = new(2);
                        dp.ScaleMode |= ScaleMode.YByDistance;
                        dp.Position = dealpos1;
                        dp.TargetPosition = dealpos2;
                        dp.Color = accessory.Data.DefaultSafeColor;
                        dp.DestoryAt = 7500;
                        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                        dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Name = $"P4_时间结晶_Buff处理位置_击退-{视角}";
                        dp.Scale = new(2);
                        dp.ScaleMode |= ScaleMode.YByDistance;
                        dp.Owner = GuidanceOwner(accessory, 视角);
                        dp.TargetPosition = dealpos2;
                        dp.Color = accessory.Data.DefaultSafeColor;
                        dp.Delay = 7500;
                        dp.DestoryAt = 3000;
                        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                    }
                }
            }
        }
        [ScriptMethod(name: "P4_时间结晶_放回返位置", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40251"])]
        public void P4_时间结晶_放回返位置(Event @event, ScriptAccessory accessory)
        {
            if (parse!=43) return;
            var pos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
            P4WaterPos.Add(pos);
            if (P4WaterPos.Count == 1) return;
            foreach (var 视角 in GuidanceIndices(accessory)) 指路(视角);

            void 指路(int 视角)
            {
                var myindex = 视角;
                Vector3 centre = new(100, 0, 100);
                // LPDU：在回返所在角落背对场中，G1（MT H1 M1 R1）在左、G2（OT H2 M2 R2）在右
                // T 站外圈地面圆圈中心（16.5m，偏 12°），队友站 T 斜后方的圆圈外环（13.8m，偏 13°）；下面是回返在东北时的坐标
                var dir8 = PositionTo8Dir((P4WaterPos[0] + P4WaterPos[1]) / 2, centre) - 1;
                Vector3 mtPos = new(108.99f, 0, 86.16f);
                Vector3 stPos = new(113.84f, 0, 91.01f);
                Vector3 mtgPos = new(107.31f, 0, 88.30f);
                Vector3 stgPos = new(111.70f, 0, 92.69f);
                if (myindex == 0)
                {
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"P4_时间结晶_放回返位置_MT-{视角}";
                    dp.Scale = new(2);
                    dp.ScaleMode |= ScaleMode.YByDistance;
                    dp.Owner = GuidanceOwner(accessory, 视角);
                    dp.TargetPosition = RotatePoint(mtPos, centre, float.Pi / 4 * dir8);
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.DestoryAt = 9000;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                }
                if (myindex == 1)
                {
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"P4_时间结晶_放回返位置_ST-{视角}";
                    dp.Scale = new(2);
                    dp.ScaleMode |= ScaleMode.YByDistance;
                    dp.Owner = GuidanceOwner(accessory, 视角);
                    dp.TargetPosition = RotatePoint(stPos, centre, float.Pi / 4 * dir8);
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.DestoryAt = 9000;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                }
                if (myindex == 2 || myindex == 4 || myindex == 6)
                {
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"P4_时间结晶_放回返位置_MTG-{视角}";
                    dp.Scale = new(2);
                    dp.ScaleMode |= ScaleMode.YByDistance;
                    dp.Owner = GuidanceOwner(accessory, 视角);
                    dp.TargetPosition = RotatePoint(mtgPos, centre, float.Pi / 4 * dir8);
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.DestoryAt = 9000;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                }
                if (myindex == 3 || myindex == 5 || myindex == 7)
                {
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"P4_时间结晶_放回返位置_STG-{视角}";
                    dp.Scale = new(2);
                    dp.ScaleMode |= ScaleMode.YByDistance;
                    dp.Owner = GuidanceOwner(accessory, 视角);
                    dp.TargetPosition = RotatePoint(stgPos, centre, float.Pi / 4 * dir8);
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.DestoryAt = 9000;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                }
            }
        }

        [ScriptMethod(name: "Phase4 Acquire IDs Of Drachen Wanderers 获取圣龙气息(龙头)ID",
            eventType: EventTypeEnum.AddCombatant,
            eventCondition: ["DataId:17836"],
            userControl: false)]

        public void Phase4_Acquire_IDs_Of_Drachen_Wanderers_获取圣龙气息ID(Event @event, ScriptAccessory accessory)
        {

            if (parse!=43)
            {

                return;

            }

            lock (phase4_readwriteLockOfDrachenWandererIds_AsAConstant)
            {

                if (!ParseObjectId(@event["SourceId"], out var sourceId)) return;
                var spos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
                if (spos.X < 100)
                {
                    _cry.LeftWyrmSid = sourceId;
                    accessory.Log.Debug($"时间结晶：记录下左侧龙头{spos}的ID{sourceId}");
                }
                else
                {
                    _cry.RightWyrmSid = sourceId;
                    accessory.Log.Debug($"时间结晶：记录下右侧龙头{spos}的ID{sourceId}");
                }


                if ((_cry.LeftWyrmSid != 0) && (_cry.RightWyrmSid != 0))
                {
                    _events[3].Set();
                    accessory.Log.Debug($"时间结晶：左侧右侧龙头记录完毕。");
                }

                if (phase4_id1OfTheDrachenWanderers.Equals(""))
                {

                    phase4_id1OfTheDrachenWanderers = @event["SourceId"];

                }

                else
                {

                    if (phase4_id2OfTheDrachenWanderers.Equals(""))
                    {

                        phase4_id2OfTheDrachenWanderers = @event["SourceId"];

                    }

                }

            }

        }

        [ScriptMethod(name: "Phase4 Hitbox Of Drachen Wanderers 圣龙气息(龙头)碰撞箱",
            eventType: EventTypeEnum.AddCombatant,
            eventCondition: ["DataId:17836"])]

        public void Phase4_Hitbox_Of_Drachen_Wanderers_圣龙气息碰撞箱(Event @event, ScriptAccessory accessory)
        {

            if (parse!=43)
            {

                return;

            }

            if (!ParseObjectId(@event["SourceId"], out var sourceId))
            {

                return;

            }

            var currentProperty = accessory.Data.GetDefaultDrawProperties();

            currentProperty.Name = $"Phase4_Hitbox_Of_Drachen_Wanderers_圣龙气息碰撞箱_{sourceId}";
            currentProperty.Scale = new(2f, Phase4_Length_Of_Drachen_Wanderer_Hitboxes >= 0 ?
                                                Phase4_Length_Of_Drachen_Wanderer_Hitboxes :
                                                1.5f);
            currentProperty.Color = Phase4_Colour_Of_Drachen_Wanderer_Hitboxes.V4.WithW(25f);
            currentProperty.Offset = new(0f, 0f, -1f);
            currentProperty.Owner = sourceId;
            currentProperty.DestoryAt = 34000;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, currentProperty);

        }

        [ScriptMethod(name: "Phase4 Explosion Range Of Drachen Wanderers 圣龙气息(龙头)爆炸范围",
            eventType: EventTypeEnum.AddCombatant,
            eventCondition: ["DataId:17836"])]

        public void Phase4_Explosion_Range_Of_Drachen_Wanderers_圣龙气息爆炸范围(Event @event, ScriptAccessory accessory)
        {

            if (parse!=43)
            {

                return;

            }

            if (!ParseObjectId(@event["SourceId"], out var sourceId))
            {

                return;

            }

            _events[3].WaitOne();

            // Usami:
            // 爆炸范围的常驻显示会引发撞龙头玩家对灯钢铁的误判
            // 想法如下：
            // 玩家为红冰或红风时，不显示自己一侧龙头的爆炸范围，转而使用一个很小的绿色圈表征龙头位置。因为自己一侧的龙头爆炸会影响灯的判断。
            // 一侧红冰撞完就与人群集合，另一侧红冰需观察自己一侧灯，爆炸后速穿，与龙头无关。
            // 红风需观察灯的爆炸然后速穿撞龙头，两个相似的绘图会影响判断。
            // 玩家为蓝Buff，不作修改。蓝暗需要躲避龙头爆炸范围，剩余三人全程不涉及龙头路径，留着也没事。

            var myIndex = accessory.GetMyIndex();
            bool isSameSideWyrm = false;
            if (sourceId == _cry.LeftWyrmSid)
                isSameSideWyrm = (myIndex == _cry.LeftIcePlayerIdx) || (myIndex == _cry.LeftWindPlayerIdx);
            else if (sourceId == _cry.RightWyrmSid)
                isSameSideWyrm = (myIndex == _cry.RightIcePlayerIdx) || (myIndex == _cry.RightWindPlayerIdx);

            var currentProperty = accessory.Data.GetDefaultDrawProperties();
            currentProperty.Name = $"Phase4_Explosion_Range_Of_Drachen_Wanderers_圣龙气息爆炸范围_{sourceId}";
            currentProperty.Scale = isSameSideWyrm ? new(1.5f) : new(12);
            currentProperty.Owner = sourceId;
            currentProperty.Color = isSameSideWyrm ? accessory.Data.DefaultSafeColor.WithW(3f) : accessory.Data.DefaultDangerColor;
            currentProperty.DestoryAt = 34000;
            accessory.Method.SendDraw(isSameSideWyrm ? DrawModeEnum.Imgui : DrawModeEnum.Default, DrawTypeEnum.Circle, currentProperty);


        }

        [ScriptMethod(name: "Phase4 Remove Hitboxes And Explosion Ranges Of Drachen Wanderers 移除圣龙气息(龙头)碰撞箱与爆炸范围",
            eventType: EventTypeEnum.RemoveCombatant,
            eventCondition: ["DataId:17836"],
            userControl: false)]

        public void Phase4_Remove_Hitboxes_And_Explosion_Ranges_Of_Drachen_Wanderers_移除圣龙气息碰撞箱与爆炸范围(Event @event, ScriptAccessory accessory)
        {

            if (parse!=43)
            {

                return;

            }

            if (!ParseObjectId(@event["SourceId"], out var sourceId))
            {

                return;

            }

            accessory.Method.RemoveDraw($"Phase4_Hitbox_Of_Drachen_Wanderers_圣龙气息碰撞箱_{sourceId}");
            accessory.Method.RemoveDraw($"Phase4_Explosion_Range_Of_Drachen_Wanderers_圣龙气息爆炸范围_{sourceId}");

        }

        [ScriptMethod(name: "Phase4 Remove Hitboxes And Explosion Ranges Of Drachen Wanderers In Advance 提前移除圣龙气息(龙头)碰撞箱与爆炸范围",
            eventType: EventTypeEnum.StatusRemove,
            eventCondition: ["StatusID:3263"],
            userControl: false)]

        // The ObjectChanged event with the field "Operate" as "Remove" would be triggered almost three seconds after the Drachen Wanderer is gone.
        // If the drawing removal relies on the event, it would be too late and may cause confusion.
        // Here is an optimized method for players with the Wyrmclaw debuff (the red debuff), which is to monitor the StatusRemove events of the Wyrmclaw debuff and acquire the closest Drachen Wanderer.
        // Obviously, the method would not help if a player with the Wyrmfang debuff (the blue debuff) hits a Drachen Wanderer. However, that's already a wipe, so whatever.
        // Thanks to Cyf5119 for providing a Dalamud way to detect if the player is dead, so that the method would skip the StatusRemove events caused by death.

        public void Phase4_Remove_Hitboxes_And_Explosion_Ranges_Of_Drachen_Wanderers_In_Advance_提前移除圣龙气息碰撞箱与爆炸范围(Event @event, ScriptAccessory accessory)
        {

            if (parse!=43)
            {

                return;

            }

            if (!ParseObjectId(@event["TargetId"], out var targetId))
            {

                return;

            }

            var targetObject = accessory.Data.Objects.SearchById(targetId);

            if (targetObject == null)
            {

                return;

            }

            if(targetObject.IsDead) {
                // Ignore the situations that the debuff was removed due to a death.

                return;

            }

            System.Threading.Thread.MemoryBarrier();

            ++phase4_timesTheWyrmclawDebuffWasRemoved;

            System.Threading.Thread.MemoryBarrier();

            if (phase4_timesTheWyrmclawDebuffWasRemoved < 3 || phase4_timesTheWyrmclawDebuffWasRemoved > 4)
            {

                return;

            }

            if (!ParseObjectId(phase4_id1OfTheDrachenWanderers, out var drachenWandererId1))
            {

                return;

            }

            if (!ParseObjectId(phase4_id2OfTheDrachenWanderers, out var drachenWandererId2))
            {

                return;

            }

            var drachenWandererObject1 = accessory.Data.Objects.SearchById(drachenWandererId1);

            if (drachenWandererObject1 == null)
            {

                return;

            }

            var drachenWandererObject2 = accessory.Data.Objects.SearchById(drachenWandererId2);

            if (drachenWandererObject2 == null)
            {

                return;

            }

            if (Vector3.Distance(targetObject.Position, drachenWandererObject1.Position)
               <=
               Vector3.Distance(targetObject.Position, drachenWandererObject2.Position))
            {

                accessory.Method.RemoveDraw($"Phase4_Hitbox_Of_Drachen_Wanderers_圣龙气息碰撞箱_{drachenWandererId1}");
                accessory.Method.RemoveDraw($"Phase4_Explosion_Range_Of_Drachen_Wanderers_圣龙气息爆炸范围_{drachenWandererId1}");

            }

            else
            {

                accessory.Method.RemoveDraw($"Phase4_Hitbox_Of_Drachen_Wanderers_圣龙气息碰撞箱_{drachenWandererId2}");
                accessory.Method.RemoveDraw($"Phase4_Explosion_Range_Of_Drachen_Wanderers_圣龙气息爆炸范围_{drachenWandererId2}");

            }

        }

        [ScriptMethod(name: "Phase4 Tidal Light 光之潮汐(地火)",
            eventType: EventTypeEnum.ActionEffect,
            eventCondition: ["ActionId:regex:^(40252|40253)$"])]

        public void Phase4_Tidal_Light_光之潮汐(Event @event, ScriptAccessory accessory)
        {

            if (parse!=43)
            {

                return;

            }

            if (!ParseObjectId(@event["SourceId"], out var sourceId))
            {

                return;

            }

            var currentProperty = accessory.Data.GetDefaultDrawProperties();

            currentProperty.Owner = sourceId;
            currentProperty.Offset = new Vector3(0, 0, -10);
            currentProperty.Scale = new(40, 10);
            currentProperty.DestoryAt = 2100;
            currentProperty.Color = Phase4_Colour_Of_Tidal_Light.V4.WithW(3f);

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, currentProperty);

        }

        [ScriptMethod(name: "Phase4 Determine Relative Positions Of Residues 确定白圈相对位置",
            eventType: EventTypeEnum.ObjectChanged,
            eventCondition: ["DataId:2014529"],
            userControl: false)]

        public void Phase4_Determine_Relative_Positions_Of_Residues_确定白圈相对位置(Event @event, ScriptAccessory accessory)
        {

            if (parse!=43)
            {

                return;

            }

            if (!@event["Operate"].Equals("Add"))
            {

                return;

            }

            if (!ParseObjectId(@event["SourceId"], out var sourceId))
            {

                return;

            }

            var sourcePositionInJson = JObject.Parse(@event["SourcePosition"]);
            float currentX = sourcePositionInJson["X"]?.Value<float>() ?? 0;

            if (currentX < 100)
            {

                if (phase4_residueIdsFromEastToWest[3] != 0)
                {

                    lock (phase4_residueIdsFromEastToWest)
                    {

                        phase4_residueIdsFromEastToWest[2] = sourceId;
                        // The about right one while facing south.

                    }

                }

                else
                {

                    lock (phase4_residueIdsFromEastToWest)
                    {

                        phase4_residueIdsFromEastToWest[3] = sourceId;
                        // The rightmost one while facing south.

                    }

                }

            }

            if (currentX > 100)
            {

                if (phase4_residueIdsFromEastToWest[0] != 0)
                {

                    lock (phase4_residueIdsFromEastToWest)
                    {

                        phase4_residueIdsFromEastToWest[1] = sourceId;
                        // The about left one while facing south.

                    }

                }

                else
                {

                    lock (phase4_residueIdsFromEastToWest)
                    {

                        phase4_residueIdsFromEastToWest[0] = sourceId;
                        // The leftmost one while facing south.

                    }

                }

            }

            if (Enable_Developer_Mode)
            {

                accessory.Method.SendChat($"""
                                           /e 
                                           @event["SourceId"]={@event["SourceId"]}
                                           sourceId={sourceId}
                                           @event["SourcePosition"]={@event["SourcePosition"]}
                                           currentX={currentX}
                                           
                                           """);

            }

        }

        [ScriptMethod(name: "Phase4 Guidance Of Residues 白圈指路",
            eventType: EventTypeEnum.ActionEffect,
            eventCondition: ["ActionId:regex:^(40252|40253)$"])]

        public void Phase4_Guidance_Of_Residues_白圈指路(Event @event, ScriptAccessory accessory)
        {

            if (parse!=43)
            {

                return;

            }

            if (phase4_guidanceOfResiduesHasBeenGenerated)
            {

                return;

            }

            foreach (var 视角 in GuidanceIndices(accessory)) 指路(视角);

            void 指路(int 视角)
            {
                int myIndex = 视角;
                Phase4_Relative_Positions_Of_Residues relativePositionOfMyResidue = phase4_getRelativePosition(myIndex);
                ulong idOfMyResidue = phase4_getResidueId(relativePositionOfMyResidue);

                if (Enable_Developer_Mode)
                {

                    if (IsMe(accessory, 视角)) accessory.Method.SendChat($"""
                                               /e 
                                               phase4_residueIdsFromEastToWest[]={phase4_residueIdsFromEastToWest[0]},{phase4_residueIdsFromEastToWest[1]},{phase4_residueIdsFromEastToWest[2]},{phase4_residueIdsFromEastToWest[3]}
                                               P4ClawBuff={P4ClawBuff[myIndex]}
                                               P4OtherBuff={P4OtherBuff[myIndex]}
                                               relativePositionOfMyResidue={relativePositionOfMyResidue}
                                               idOfMyResidue={idOfMyResidue}

                                               """);

                }

                if (relativePositionOfMyResidue != Phase4_Relative_Positions_Of_Residues.Unknown_未知
                   &&
                   idOfMyResidue != 0)
                {

                    var currentProperty = accessory.Data.GetDefaultDrawProperties();

                    currentProperty.Name = $"Phase4_Guidance_Of_Residues_白圈指路-{视角}";
                    currentProperty.Scale = new(2);
                    currentProperty.ScaleMode |= ScaleMode.YByDistance;
                    currentProperty.Owner = GuidanceOwner(accessory, 视角);
                    currentProperty.Color = Phase4_Colour_Of_Residue_Guidance.V4.WithW(1f);
                    currentProperty.DestoryAt = 23000;

                    var residueObject = accessory.Data.Objects.SearchById(idOfMyResidue);

                    if (residueObject != null)
                    {

                        phase4_guidanceOfResiduesHasBeenGenerated = true;

                        currentProperty.TargetPosition = residueObject.Position;

                        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, currentProperty);

                        if (Enable_Text_Prompts)
                        {

                            if (IsMe(accessory, 视角)) accessory.Method.TextInfo(phase4_getResidueDescription(relativePositionOfMyResidue), 2500);

                        }

                        if (IsMe(accessory, 视角)) accessory.TTS($"{phase4_getResidueDescription(relativePositionOfMyResidue)}",
                                        Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);

                        if (Enable_Developer_Mode)
                        {

                            if (IsMe(accessory, 视角)) accessory.Method.SendChat($"""
                                                       /e 
                                                       residueObject.Position={residueObject.Position}

                                                       """);

                        }

                    }

                }

            }
        }

        [ScriptMethod(name: "Phase4 Remove Guidance Of Residues 移除白圈指路",
            eventType: EventTypeEnum.StatusRemove,
            eventCondition: ["StatusID:3264"],
            userControl: false)]

        public void Phase4_Remove_Guidance_Of_Residues_移除白圈指路(Event @event, ScriptAccessory accessory)
        {

            if (parse!=43)
            {

                return;

            }

            if (!ParseObjectId(@event["TargetId"], out var targetId))
            {

                return;

            }

            if (!Debugging && targetId != accessory.Data.Me)
            {

                return;

            }

            accessory.Method.RemoveDraw($"Phase4_Guidance_Of_Residues_白圈指路-{accessory.Data.PartyList.IndexOf((uint)targetId)}");

        }

        [ScriptMethod(name: "Phase4 Highlight Of Residues 白圈高亮",
            eventType: EventTypeEnum.ObjectChanged,
            eventCondition: ["DataId:2014529"])]

        public void Phase4_Highlight_Of_Residues_白圈高亮(Event @event, ScriptAccessory accessory)
        {

            if (parse!=43)
            {

                return;

            }

            if (!@event["Operate"].Equals("Add"))
            {

                return;

            }

            if (!ParseObjectId(@event["SourceId"], out var sourceId))
            {

                return;

            }

            var currentProperty = accessory.Data.GetDefaultDrawProperties();

            currentProperty.Name = $"Phase4_Highlight_Of_Residues_白圈高亮_{sourceId}";
            currentProperty.Scale = new(1f);
            currentProperty.InnerScale = new(0.8f);
            currentProperty.Color = accessory.Data.DefaultDangerColor.WithW(25f);
            currentProperty.Radian = float.Pi * 2;
            currentProperty.Owner = sourceId;
            currentProperty.DestoryAt = 17000;

            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Donut, currentProperty);

        }

        [ScriptMethod(name: "Phase4 Remove Highlights Of Residues 移除白圈高亮",
            eventType: EventTypeEnum.ObjectChanged,
            eventCondition: ["DataId:2014529"],
            userControl: false)]

        public void Phase4_Remove_Highlights_Of_Residues_移除白圈高亮(Event @event, ScriptAccessory accessory)
        {

            if (parse!=43)
            {

                return;

            }

            if (!@event["Operate"].Equals("Remove"))
            {

                return;

            }

            if (!ParseObjectId(@event["SourceId"], out var sourceId))
            {

                return;

            }

            accessory.Method.RemoveDraw($"Phase4_Highlight_Of_Residues_白圈高亮_{sourceId}");

            foreach (var 视角 in GuidanceIndices(accessory)) 指路(视角);

            void 指路(int 视角)
            {
                ulong idOfMyResidue = phase4_getResidueId(phase4_getRelativePosition(视角));

                if (idOfMyResidue != 0
                   &&
                   idOfMyResidue == sourceId)
                {

                    accessory.Method.RemoveDraw($"Phase4_Guidance_Of_Residues_白圈指路-{视角}");

                }

            }
        }

        [ScriptMethod(name: "Phase4 Remove Highlights Of Residues In Advance 提前移除白圈高亮",
            eventType: EventTypeEnum.StatusRemove,
            eventCondition: ["StatusID:3264"],
            userControl: false)]

        // The background and implementation are almost the same as the removal of Hitboxes and Explosion Ranges before.
        // Please refer to the comments following that method for details.

        public void Phase4_Remove_Highlights_Of_Residues_In_Advance_提前移除白圈高亮(Event @event, ScriptAccessory accessory)
        {

            if (parse!=43)
            {

                return;

            }

            if (!ParseObjectId(@event["TargetId"], out var targetId))
            {

                return;

            }

            var targetObject = accessory.Data.Objects.SearchById(targetId);

            if (targetObject == null)
            {

                return;

            }

            Vector3 targetPosition = targetObject.Position;

            if(targetObject.IsDead) {

                return;

            }

            int closestResidue = -1;
            float distanceToTheClosestResidue = float.PositiveInfinity;

            for (int i = 0; i < 4; ++i)
            {

                var residueObject = accessory.Data.Objects.SearchById(phase4_residueIdsFromEastToWest[i]);

                if (residueObject != null)
                {

                    if (Vector3.Distance(targetPosition, residueObject.Position) < distanceToTheClosestResidue)
                    {

                        closestResidue = i;
                        distanceToTheClosestResidue = Vector3.Distance(targetPosition, residueObject.Position);

                    }

                }

            }

            if (0 <= closestResidue && closestResidue <= 3)
            {

                accessory.Method.RemoveDraw($"Phase4_Highlight_Of_Residues_白圈高亮_{phase4_residueIdsFromEastToWest[closestResidue]}");

                foreach (var 视角 in GuidanceIndices(accessory)) 指路(视角);

                void 指路(int 视角)
                {
                    if (targetId != GuidanceOwner(accessory, 视角))
                    {

                        ulong idOfMyResidue = phase4_getResidueId(phase4_getRelativePosition(视角));

                        if (idOfMyResidue != 0
                           &&
                           idOfMyResidue == phase4_residueIdsFromEastToWest[closestResidue])
                        {

                            accessory.Method.RemoveDraw($"Phase4_Guidance_Of_Residues_白圈指路-{视角}");

                        }

                    }

                }
            }

        }

        // LPDU 白圈：暗炎喷发去 D（西侧先出现的那个），黑暗神圣(分摊)去 B（东侧先出现的），黑暗狂水去 3（东侧后出现的），黑暗冰封去 4（西侧后出现的）
        private Phase4_Relative_Positions_Of_Residues phase4_getRelativePosition(int currentIndex)
        {
            if (currentIndex < 0 || currentIndex > 7 || P4ClawBuff[currentIndex] != 3) return Phase4_Relative_Positions_Of_Residues.Unknown_未知;
            // P4OtherBuff：4 暗炎喷发、5 黑暗神圣、3 黑暗狂水、1 黑暗冰封
            return P4OtherBuff[currentIndex] switch
            {
                4 => Phase4_Relative_Positions_Of_Residues.Westmost_最西侧,
                5 => Phase4_Relative_Positions_Of_Residues.Eastmost_最东侧,
                3 => Phase4_Relative_Positions_Of_Residues.About_East_次东侧,
                1 => Phase4_Relative_Positions_Of_Residues.About_West_次西侧,
                _ => Phase4_Relative_Positions_Of_Residues.Unknown_未知,
            };
        }

        private ulong phase4_getResidueId(Phase4_Relative_Positions_Of_Residues relativePosition)
        {

            switch (relativePosition)
            {

                case (Phase4_Relative_Positions_Of_Residues.Eastmost_最东侧):
                    {

                        return phase4_residueIdsFromEastToWest[0];

                    }

                case (Phase4_Relative_Positions_Of_Residues.About_East_次东侧):
                    {

                        return phase4_residueIdsFromEastToWest[1];

                    }

                case (Phase4_Relative_Positions_Of_Residues.About_West_次西侧):
                    {

                        return phase4_residueIdsFromEastToWest[2];

                    }

                case (Phase4_Relative_Positions_Of_Residues.Westmost_最西侧):
                    {

                        return phase4_residueIdsFromEastToWest[3];

                    }

                case (Phase4_Relative_Positions_Of_Residues.Unknown_未知):
                    {

                        return 0;

                    }

                default:
                    {

                        return 0;
                        // Just a placeholder and should never be reached.

                    }

            }

        }

        private String phase4_getResidueDescription(Phase4_Relative_Positions_Of_Residues relativePosition)
        {

            switch (relativePosition)
            {

                case (Phase4_Relative_Positions_Of_Residues.Eastmost_最东侧):
                    {

                        if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                        {

                            return "B点白圈（最东）";

                        }

                        if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                        {

                            return "Puddle at B (eastmost)";

                        }

                        return "";
                        // Just a placeholder and should never be reached.

                    }

                case (Phase4_Relative_Positions_Of_Residues.About_East_次东侧):
                    {

                        if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                        {

                            return "3点白圈（次东）";

                        }

                        if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                        {

                            return "Puddle at 3 (about east)";

                        }

                        return "";
                        // Just a placeholder and should never be reached.

                    }

                case (Phase4_Relative_Positions_Of_Residues.About_West_次西侧):
                    {

                        if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                        {

                            return "4点白圈（次西）";

                        }

                        if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                        {

                            return "Puddle at 4 (about west)";

                        }

                        return "";
                        // Just a placeholder and should never be reached.

                    }

                case (Phase4_Relative_Positions_Of_Residues.Westmost_最西侧):
                    {

                        if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                        {

                            return "D点白圈（最西）";

                        }

                        if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                        {

                            return "Puddle at D (westmost)";

                        }

                        return "";
                        // Just a placeholder and should never be reached.

                    }

                case (Phase4_Relative_Positions_Of_Residues.Unknown_未知):
                    {

                        return "";

                    }

                default:
                    {

                        return "";
                        // Just a placeholder and should never be reached.

                    }

            }

        }

        [ScriptMethod(name: "Phase2 Reset Semaphores After Crystallize Time 时间结晶后重置信号灯",
            eventType: EventTypeEnum.ActionEffect,
            eventCondition: ["ActionId:40332"],
            userControl: false,
            suppress: 10000)]

        public void Phase2_Reset_Semaphores_After_Crystallize_Time_时间结晶后重置信号灯(Event @event, ScriptAccessory accessory)
        {

            if (parse!=43)
            {

                return;

            }

            phase4_semaphoreMajorDebuffsWereConfirmed = new System.Threading.AutoResetEvent(false);
            phase4_semaphoreIncidentalDebuffsWereConfirmed = new System.Threading.AutoResetEvent(false);

        }

        #region P4 补丁 (Usami / Tetora)

        [ScriptMethod(name: "未来的碎片受击警察", eventType: EventTypeEnum.ActionEffect,
            eventCondition: ["ActionId:regex:^(40(190|271|274|241|279|280|277|284|285|289|248|303|250))$"],
            userControl: true)]
        public void FragmentMonitor(Event ev, ScriptAccessory sa)
        {
            if (_fruPhase is not FruPhase.P4A_DarklitDragonsong and not FruPhase.P4B_CrystallizeTime) return;

            IGameObject? tObj = sa.GetById(ev.TargetId);
            if (tObj == null) return;

            lock (_fragTargets)
            {
                // 初始化受击列表，并加入
                var tidx = ev.TargetIndex();
                if (tidx == 1 && _fragTargets.Count != 0)
                    _fragTargets.Clear();
                _fragTargets.Add(ev.TargetId);

                // 受击单位为未来的碎片
                var tDataId = tObj.DataId;
                if (tDataId != FragmentDataId) return;

                // 获得导致碎片受击的技能
                var aid = ev.ActionId;
                // 技能列表
                var skillParam = new Dictionary<uint, string>
                {
                    { 40190, "光之波动引导" },
                    { 40271, "水分摊" },
                    { 40274, "暗分散" },
                    { 40241, "撞龙头" },
                    { 40279, "冰月环" },
                    { 40280, "风击退" },
                    { 40277, "暗分摊" },
                    { 40284, "真夜舞蹈（远）" },
                    { 40285, "真夜舞蹈（近）" },
                    { 40289, "碎灵一击" },
                    { 40248, "死亡轮回（希瓦）" },
                    { 40303, "死亡轮回（盖娅）" },
                    { 40250, "无尽顿悟" },
                };

                var str = "";
                if (_fragTargets.Count != 1)
                {
                    foreach (var target in _fragTargets)
                    {
                        IGameObject? obj = sa.GetById(target);
                        if (obj == null) continue;
                        str += obj.Name + " ";
                    }
                }

                if (skillParam.TryGetValue(aid, out var skillName))
                {
                    sa.Method.SendChat($"/e 未来的碎片 似乎因【{str}】被【{skillName}】打中了！！！<se.11>");
                }
            }
        }

        #region P4.1 光暗龙诗

        [ScriptMethod(name: "光暗龙诗阶段转换（DEBUG ONLY）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(40239)$"],
            userControl: Debugging)]
        public void DarklitDragonsongPhaseChange(Event ev, ScriptAccessory sa)
        {
            _fruPhase = FruPhase.P4A_DarklitDragonsong;
            _usamiEvents = Enumerable
                .Range(0, 20)
                .Select(_ => new ManualResetEvent(false))
                .ToList();
            sa.DebugMsg($"当前阶段为：{_fruPhase}", Enable_Developer_Mode);
        }

        [ScriptMethod(name: "光之锁突出显示", eventType: EventTypeEnum.Tether, eventCondition: ["Id:006E"],
            userControl: true)]
        public void DldLightChainVisual(Event ev, ScriptAccessory sa)
        {
            if (_fruPhase != FruPhase.P4A_DarklitDragonsong) return;
            var dp1 = sa.DrawGuidance(ev.SourceId, ev.TargetId, 0, 9000, $"Usami-光锁外");
            dp1.Color = ColorHelper.ColorDark.V4.WithW(3f);
            var dp2 = sa.DrawGuidance(ev.SourceId, ev.TargetId, 0, 9000, $"Usami-光锁内");
            dp2.Color = ColorHelper.ColorYellow.V4;
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, dp1);
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, dp2);
        }

        #endregion P4.1 光暗龙诗

        #region P4.2 时间结晶

        [ScriptMethod(name: "时间结晶阶段转换（DEBUG ONLY）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(40240)$"],
            userControl: Debugging)]
        public void CrystallizeTimePhaseChange(Event @event, ScriptAccessory accessory)
        {
            _fruPhase = FruPhase.P4B_CrystallizeTime;
            accessory.DebugMsg($"当前阶段为：{_fruPhase}", Enable_Developer_Mode);
        }

        #endregion P4.2 时间结晶

        // 40186 圣龙护甲：P4 开场，盖娅随后出现
        [ScriptMethod(name: "P4 盖娅出现横幅倒计时", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:40186"])]
        public async void P4盖娅出现倒计时(Event @event, ScriptAccessory accessory)
        {
            if (Enable_Text_Prompts) accessory.Method.TextInfo("盖娅即将出现，注意团辅时机", duration: 5300, true);
            await Task.Delay(3800);
            accessory.TTS("盖娅即将出现", Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);
        }

        // 未来的碎片(17841)：3.5m 目标圈，存活期间常驻
        [ScriptMethod(name: "P4 未来的碎片 目标圈绘制", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:17841"])]
        public void P4未来的碎片目标圈(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"未来的碎片{@event.SourceId}";
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(3.5f);
            dp.DestoryAt = 180000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        [ScriptMethod(name: "P4 未来的碎片 绘制销毁", eventType: EventTypeEnum.RemoveCombatant, eventCondition: ["DataId:17841"], userControl: false)]
        public void P4未来的碎片绘制销毁(Event @event, ScriptAccessory accessory)
            => accessory.Method.RemoveDraw($"未来的碎片{@event.SourceId}");

        // 40174 忘却的此岸：碎片全屏 AOE
        [ScriptMethod(name: "P4 忘却的此岸 AOE提示", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40174"])]
        public void P4忘却的此岸提示(Event @event, ScriptAccessory accessory)
        {
            if (Enable_Text_Prompts) accessory.Method.TextInfo("水晶AOE", duration: 4300, false);
            accessory.TTS("水晶AOE", Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);
        }

        // LPDU 死亡轮回 4-4：G1（MT H1 M1 R1）在琳脚下分摊，G2（OT H2 M2 R2）在盖娅脚下
        // 40302 死亡轮回（盖娅）：G2 连线盖娅
        [ScriptMethod(name: "P4 死亡轮回 盖娅连线", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40302"])]
        public void P4死亡轮回盖娅连线(Event @event, ScriptAccessory accessory)
            => 死亡轮回连线(accessory, @event.SourceId, "连线盖娅", g1: false);

        // 40247 死亡轮回（琳）：G1 连线琳
        [ScriptMethod(name: "P4 死亡轮回 琳连线", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40247"])]
        public void P4死亡轮回琳连线(Event @event, ScriptAccessory accessory)
            => 死亡轮回连线(accessory, @event.SourceId, "连线琳", g1: true);

        private static void 死亡轮回连线(ScriptAccessory accessory, ulong bossId, string name, bool g1)
        {
            foreach (var 视角 in GuidanceIndices(accessory)) 指路(视角);

            void 指路(int 视角)
            {
                if (视角 % 2 == 0 != g1) return;
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"{name}-{视角}";
                dp.Owner = GuidanceOwner(accessory, 视角);
                dp.Color = accessory.Data.DefaultSafeColor;
                dp.ScaleMode |= ScaleMode.YByDistance;
                dp.TargetObject = bossId;
                dp.Scale = new(1);
                dp.DestoryAt = 4000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }
        }

        // 40229 神圣之翼：二运 Y 字击退，读条 2.5s 后提示防击退
        [ScriptMethod(name: "P4 二运Y字击退 防击退提醒", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40229"])]
        public async void P4Y字击退提醒(Event @event, ScriptAccessory accessory)
        {
            await Task.Delay(2500);
            if (Enable_Text_Prompts) accessory.Method.TextInfo("防击退", duration: 1500, true);
            accessory.TTS("防击退", Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);
        }

        // 同上时机，按设置自动亲疏 / 沉稳（技能就绪且身上没有对应 buff 才放）
        [ScriptMethod(name: "*P4 二运Y字击退 自动防击退", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40229"])]
        public async void P4Y字击退自动防击退(Event @event, ScriptAccessory accessory)
        {
            if (AutoAntiKnockback == AntiKnockbackEnum.None) return;
            await Task.Delay(2500);
            var (action, buff) = AutoAntiKnockback == AntiKnockbackEnum.亲疏自行 ? (亲疏自行, 亲疏自行Buff) : (沉稳咏唱, 沉稳咏唱Buff);
            if (!action.IsSpellReady() || accessory.HasStatus(accessory.Data.MyObject, buff)) return;
            accessory.Method.UseAction(accessory.Data.Me, action);
            if (Enable_Developer_Mode) accessory.Method.SendChat($"/e [Debug] 已尝试自动使用{AutoAntiKnockback}");
        }

        // 40247 死亡轮回：只取二运结束那次（时间结晶之后）
        [ScriptMethod(name: "P4 最后延后120提示", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40247"])]
        public void P4延后120提示(Event @event, ScriptAccessory accessory)
        {
            if (_fruPhase != FruPhase.P4B_CrystallizeTime) return;
            if (Enable_Text_Prompts) accessory.Method.TextInfo("延后120爆发至P5", duration: 5000, true);
            accessory.TTS("延后爆发", Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);
        }

        #endregion

        #endregion

        #region Phase_5

        [ScriptMethod(name: "----- Phase 5 ----- (No actual meaning for this toggle/此开关无实际意义)",
            eventType: EventTypeEnum.NpcYell,
            eventCondition: ["I lift my lamp beside the golden door!",
                            "我在金门旁为他们将灯举起!"])]

        public void Phase5_Placeholder(Event @event, ScriptAccessory accessory) { }

        [ScriptMethod(name: "Phase5 Initialization 初始化",
            eventType: EventTypeEnum.AddCombatant,
            eventCondition: ["DataId:17839"],
            userControl: false)]

        public void Phase5_Initialization_初始化(Event @event, ScriptAccessory accessory)
        {

            phase5_bossId = @event["SourceId"];
            phase5_hasAcquiredTheFirstTower = false;
            phase5_indexOfTheFirstTower = "";
            phase5_hasConfirmedTheInitialPosition = false;

            System.Threading.Thread.MemoryBarrier();

            isInPhase5 = true;

        }

        [ScriptMethod(name: "Phase5 Destruction 析构",
            eventType: EventTypeEnum.RemoveCombatant,
            eventCondition: ["DataId:17839"],
            userControl: false)]

        public void Phase5_Destruction_析构(Event @event, ScriptAccessory accessory)
        {

            isInPhase5 = false;

            System.Threading.Thread.MemoryBarrier();

            phase5_bossId = "";
            phase5_hasAcquiredTheFirstTower = false;
            phase5_indexOfTheFirstTower = "";
            phase5_hasConfirmedTheInitialPosition = false;

        }

        [ScriptMethod(name: "P5_地火", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(40118|40307)$"])]
        public void P5_地火(Event @event, ScriptAccessory accessory)
        {
            if (!isInPhase5)
            {

                return;

            }

            if (!ParseObjectId(@event["SourceId"], out var sid)) return;

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P5_地火";
            dp.Scale = new(80, 5);
            dp.Owner = sid;
            dp.Color = Phase5_Colour_Of_Fulgent_Blade.V4.WithW(1f);
            dp.DestoryAt = 7000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"P5_地火_前进_{@event["SourceId"]}";
            dp.Scale = new(80, 5);
            dp.Offset = new(0, 0, -5);
            dp.Owner = sid;
            dp.Color = Phase5_Colour_Of_Fulgent_Blade.V4.WithW(1f);
            dp.Delay = 7000;
            dp.DestoryAt = 20000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

        }
        [ScriptMethod(name: "P5_地火消除", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(40118|4030[789])$"], userControl: false)]
        public void P5_地火消除(Event @event, ScriptAccessory accessory)
        {
            if (!isInPhase5)
            {

                return;

            }

            if (!float.TryParse(@event["SourceRotation"], out var rot)) return;
            var pos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
            Vector3 centre = new(100, 0, 100);
            Vector3 posNext = new(pos.X + 5 * MathF.Sin(rot), 0, pos.Z + 5 * MathF.Cos(rot));
            if ((posNext - centre).Length() > 20)
            {
                accessory.Method.RemoveDraw($"P5_地火_前进_{@event["SourceId"]}");
            }
        }

        //1火开始发光特效
        [ScriptMethod(name: "Phase5 Guidance Of Fulgent Blade 璀璨之刃(地火)指路", eventType: EventTypeEnum.ObjectEffect, eventCondition: ["Id2:16"])]
        public void Phase5_Guidance_Of_Fulgent_Blade_璀璨之刃指路(Event @event, ScriptAccessory accessory)
        {
            if (Phase == "P5地火计算完成")//限制分组
             {
                 lock (drawLock)
                 {
                     Phase = "P5运算结束";
                     //accessory.Method.SendChat($"/e P5运算结束");
                     var id = Convert.ToUInt32(@event["SourceId"], 16);
                     Vector2 FarthestPoint = new Vector2();
                     Vector2 ClosestPoint = new Vector2();
                     if (id == P1P3Blades[0].Id || id == P1P3Blades[1].Id) //P1起火
                     {
                         FarthestPoint = FindFarthestPoint(OnPoint, Point1);
                         ClosestPoint = FindClosestPoint(OnPoint, Point1);
                     }
                     else if (id == P1P3Blades[2].Id || id == P1P3Blades[3].Id) //P3起火
                     {
                         FarthestPoint = FindFarthestPoint(OnPoint, Point3);
                         ClosestPoint = FindClosestPoint(OnPoint, Point3);
                     }

                     //远 近 近 远
                     BladeRoutes.Insert(0, FarthestPoint); //第1跑起点 与起火点相对最远
                     BladeRoutes.Insert(1, ClosestPoint); //第2跑起点 与起火点相对最近
                     BladeRoutes.Insert(2, FindFarthestPoint(OnPoint, Point2)); //第3跑路径是上还是下 相对P2最远
                     BladeRoutes.Insert(3, FindClosestPoint(OnPoint, Point2)); //第4跑路径是上还是下 相对P2最近
                     BladeRoutes.Insert(4, ClosestPoint); //第5跑起点 与起火点相对最近
                     BladeRoutes.Insert(5, FarthestPoint); //第5跑起点 与起火点相对最远

                     //指路初期想法： 0绿1红 1绿出2红 2绿出3红
                     //每次2000毫秒？
                     int BladeTimes = 2000;
                     //0绿1红
                     foreach (var 视角 in GuidanceIndices(accessory)) 指路(视角);

                     void 指路(int 视角)
                     {
                         var Goline0 = accessory.Data.GetDefaultDrawProperties();
                         Goline0.Owner = GuidanceOwner(accessory, 视角);
                         Goline0.DestoryAt = 9000;
                         Goline0.Color = Phase5_Colour_Of_The_Current_Guidance_Step.V4;
                         Goline0.Scale = new(2);
                         Goline0.ScaleMode |= ScaleMode.YByDistance;
                         Goline0.TargetPosition = Vector3Fucker(BladeRoutes[0]);
                         accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, Goline0);

                         var line1 = accessory.Data.GetDefaultDrawProperties();
                         line1.Position = Vector3Fucker(BladeRoutes[0]);
                         line1.DestoryAt = 9000;
                         line1.Color = Phase5_Colour_Of_The_Next_Guidance_Step.V4;
                         line1.Scale = new(2);
                         line1.ScaleMode |= ScaleMode.YByDistance;
                         line1.TargetPosition = Vector3Fucker(BladeRoutes[1]);
                         accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, line1);
                         /////////////////////////////////////1绿放2 基础延迟9000
                         var Goline1 = accessory.Data.GetDefaultDrawProperties();
                         Goline1.Owner = GuidanceOwner(accessory, 视角);
                         Goline1.Delay = 9000;
                         Goline1.DestoryAt = BladeTimes;
                         Goline1.Color = Phase5_Colour_Of_The_Current_Guidance_Step.V4;
                         Goline1.Scale = new(2);
                         Goline1.ScaleMode |= ScaleMode.YByDistance;
                         Goline1.TargetPosition = Vector3Fucker(BladeRoutes[1]);
                         accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, Goline1);

                         var line2 = accessory.Data.GetDefaultDrawProperties();
                         line2.Position = Vector3Fucker(BladeRoutes[1]);
                         line2.Delay = 9000;
                         line2.DestoryAt = BladeTimes;
                         line2.Color = Phase5_Colour_Of_The_Next_Guidance_Step.V4;
                         line2.Scale = new(2);
                         line2.ScaleMode |= ScaleMode.YByDistance;
                         line2.TargetPosition = Vector3Fucker(BladeRoutes[2]);
                         accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, line2);

                         /////////////////////////////////////2绿放3 基础延迟9000+bladetime
                         var Goline2 = accessory.Data.GetDefaultDrawProperties();
                         Goline2.Owner = GuidanceOwner(accessory, 视角);
                         Goline2.Delay = 9000 + BladeTimes;
                         Goline2.DestoryAt = BladeTimes;
                         Goline2.Color = Phase5_Colour_Of_The_Current_Guidance_Step.V4;
                         Goline2.Scale = new(2);
                         Goline2.ScaleMode |= ScaleMode.YByDistance;
                         Goline2.TargetPosition = Vector3Fucker(BladeRoutes[2]);
                         accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, Goline2);

                         var line3 = accessory.Data.GetDefaultDrawProperties();
                         line3.Position = Vector3Fucker(BladeRoutes[2]);
                         line3.Delay = 9000 + BladeTimes;
                         line3.DestoryAt = BladeTimes;
                         line3.Color = Phase5_Colour_Of_The_Next_Guidance_Step.V4;
                         line3.Scale = new(2);
                         line3.ScaleMode |= ScaleMode.YByDistance;
                         line3.TargetPosition = Vector3Fucker(BladeRoutes[3]);
                         accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, line3);

                         /////////////////////////////////////3绿放4 基础延迟9000+bladetime*2
                         var Goline3 = accessory.Data.GetDefaultDrawProperties();
                         Goline3.Owner = GuidanceOwner(accessory, 视角);
                         Goline3.Delay = 9000 + BladeTimes * 2;
                         Goline3.DestoryAt = BladeTimes;
                         Goline3.Color = Phase5_Colour_Of_The_Current_Guidance_Step.V4;
                         Goline3.Scale = new(2);
                         Goline3.ScaleMode |= ScaleMode.YByDistance;
                         Goline3.TargetPosition = Vector3Fucker(BladeRoutes[3]);
                         accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, Goline3);

                         var line4 = accessory.Data.GetDefaultDrawProperties();
                         line4.Position = Vector3Fucker(BladeRoutes[3]);
                         line4.Delay = 9000 + BladeTimes * 2;
                         line4.DestoryAt = BladeTimes;
                         line4.Color = Phase5_Colour_Of_The_Next_Guidance_Step.V4;
                         line4.Scale = new(2);
                         line4.ScaleMode |= ScaleMode.YByDistance;
                         line4.TargetPosition = Vector3Fucker(BladeRoutes[4]);
                         accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, line4);

                         /////////////////////////////////////4绿放5 基础延迟9000+bladetime*3
                         var Goline4 = accessory.Data.GetDefaultDrawProperties();
                         Goline4.Owner = GuidanceOwner(accessory, 视角);
                         Goline4.Delay = 9000 + BladeTimes * 3;
                         Goline4.DestoryAt = BladeTimes;
                         Goline4.Color = Phase5_Colour_Of_The_Current_Guidance_Step.V4;
                         Goline4.Scale = new(2);
                         Goline4.ScaleMode |= ScaleMode.YByDistance;
                         Goline4.TargetPosition = Vector3Fucker(BladeRoutes[4]);
                         accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, Goline4);

                         var line5 = accessory.Data.GetDefaultDrawProperties();
                         line5.Position = Vector3Fucker(BladeRoutes[4]);
                         line5.Delay = 9000 + BladeTimes * 3;
                         line5.DestoryAt = BladeTimes;
                         line5.Color = Phase5_Colour_Of_The_Next_Guidance_Step.V4;
                         line5.Scale = new(2);
                         line5.ScaleMode |= ScaleMode.YByDistance;
                         line5.TargetPosition = Vector3Fucker(BladeRoutes[5]);
                         accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, line5);

                         /////////////////////////////////////5绿放6 基础延迟9000+bladetime*4
                         var Goline5 = accessory.Data.GetDefaultDrawProperties();
                         Goline5.Owner = GuidanceOwner(accessory, 视角);
                         Goline5.Delay = 9000 + BladeTimes * 4;
                         Goline5.DestoryAt = BladeTimes;
                         Goline5.Color = Phase5_Colour_Of_The_Current_Guidance_Step.V4;
                         Goline5.Scale = new(2);
                         Goline5.ScaleMode |= ScaleMode.YByDistance;
                         Goline5.TargetPosition = Vector3Fucker(BladeRoutes[5]);
                         accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, Goline5);
                     }
                 }
             }
        }

        [ScriptMethod(name: "Phase5 Boss Central Axis After Fulgent Blade 璀璨之刃(地火)后Boss中轴线",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:40310"])]

        public void Phase5_Boss_Central_Axis_After_Fulgent_Blade_璀璨之刃后Boss中轴线(Event @event, ScriptAccessory accessory)
        {

            if (!isInPhase5)
            {

                return;

            }

            if (!ParseObjectId(@event["SourceId"], out var sourceId))
            {

                return;

            }

            var currentProperty = accessory.Data.GetDefaultDrawProperties();

            currentProperty.Name = "Phase5_Boss_Front_Axis_After_Fulgent_Blade_璀璨之刃后Boss前轴线";
            currentProperty.Owner = sourceId;
            currentProperty.Scale = new(0.5f, 10);
            currentProperty.Color = Phase5_Colour_Of_The_Boss_Central_Axis.V4.WithW(25f);
            currentProperty.DestoryAt = 9000;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, currentProperty);

            currentProperty = accessory.Data.GetDefaultDrawProperties();

            currentProperty.Name = "Phase5_Boss_Rear_Axis_After_Fulgent_Blade_璀璨之刃后Boss后轴线";
            currentProperty.Owner = sourceId;
            currentProperty.Scale = new(0.5f, 10);
            currentProperty.Rotation = float.Pi;
            currentProperty.Color = Phase5_Colour_Of_The_Boss_Central_Axis.V4.WithW(25f);
            currentProperty.DestoryAt = 9000;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, currentProperty);

        }

        [ScriptMethod(name: "Phase5 Side To Stack After Fulgent Blade 璀璨之刃(地火)后的分摊侧",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:40310"])]

        public void Phase5_Side_To_Stack_After_Fulgent_Blade_璀璨之刃后的分摊侧(Event @event, ScriptAccessory accessory)
        {

            if (!isInPhase5)
            {

                return;

            }

            if (!ParseObjectId(@event["SourceId"], out var sourceId))
            {

                return;

            }

            int myIndex = accessory.Data.PartyList.IndexOf(accessory.Data.Me);

            if (myIndex < 0 || myIndex > 7)
            {

                return;

            }

            bool goLeft = false;

            if (myIndex == 0
               ||
               myIndex == 2
               ||
               myIndex == 4
               ||
               myIndex == 6)
            {

                goLeft = true;

            }

            if (myIndex == 1
               ||
               myIndex == 3
               ||
               myIndex == 5
               ||
               myIndex == 7)
            {

                goLeft = false;

            }

            var currentProperty = accessory.Data.GetDefaultDrawProperties();

            currentProperty.Name = "Phase5_Left_Side_After_Fulgent_Blade_璀璨之刃后的左侧";
            currentProperty.Owner = sourceId;
            currentProperty.Scale = new(4);
            currentProperty.Radian = float.Pi;
            currentProperty.Rotation = float.Pi / 2;
            currentProperty.Offset = new Vector3(-0.25f, 0, 0);
            currentProperty.DestoryAt = 9000;

            if (Phase5_Boss_Faces_Players_After_Fulgent_Blade)
            {

                currentProperty.Color = goLeft ?
                    accessory.Data.DefaultDangerColor.WithW(25f) :
                    accessory.Data.DefaultSafeColor.WithW(25f);

            }

            else
            {

                currentProperty.Color = goLeft ?
                    accessory.Data.DefaultSafeColor.WithW(25f) :
                    accessory.Data.DefaultDangerColor.WithW(25f);

            }

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, currentProperty);

            currentProperty = accessory.Data.GetDefaultDrawProperties();

            currentProperty.Name = "Phase5_Right_Side_After_Fulgent_Blade_璀璨之刃后的右侧";
            currentProperty.Owner = sourceId;
            currentProperty.Scale = new(4);
            currentProperty.Radian = float.Pi;
            currentProperty.Rotation = -(float.Pi / 2);
            currentProperty.Offset = new Vector3(0.25f, 0, 0);
            currentProperty.DestoryAt = 9000;

            if (Phase5_Boss_Faces_Players_After_Fulgent_Blade)
            {

                currentProperty.Color = goLeft ?
                    accessory.Data.DefaultSafeColor.WithW(25f) :
                    accessory.Data.DefaultDangerColor.WithW(25f);

            }

            else
            {

                currentProperty.Color = goLeft ?
                    accessory.Data.DefaultDangerColor.WithW(25f) :
                    accessory.Data.DefaultSafeColor.WithW(25f);

            }

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, currentProperty);

            if (Enable_Text_Prompts)
            {

                if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                {

                    accessory.Method.TextInfo(((goLeft) ? ("左侧分摊") : ("右侧分摊")), 9000);

                }

                if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                {

                    accessory.Method.TextInfo(((goLeft) ? ("Stack on the left") : ("Stack on the right")), 9000);

                }

            }

            if (Enable_Vanilla_TTS || Enable_Daily_Routines_TTS)
            {

                if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                {

                    accessory.TTS($"{((goLeft) ? ("左侧分摊") : ("右侧分摊"))}",
                        Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);

                }

                if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                {

                    accessory.TTS($"{((goLeft) ? ("Stack on the left") : ("Stack on the right"))}",
                        Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);

                }

            }

        }

        [ScriptMethod(name: "Phase5 Initialization Of Wings Dark And Light 光与暗之翼(踩塔)初始化",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:40319"],
            userControl: false)]

        public void Phase5_Initialization_Of_Wings_Dark_And_Light_光与暗之翼初始化(Event @event, ScriptAccessory accessory)
        {

            if (!isInPhase5)
            {

                return;

            }

            phase5_hasAcquiredTheFirstTower = false;
            phase5_indexOfTheFirstTower = "";
            phase5_hasConfirmedTheInitialPosition = false;

        }

        [ScriptMethod(name: "P5_光与暗之翼", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(40313|40233)$"])]
        public void P5_光与暗之翼(Event @event, ScriptAccessory accessory)
        {
            if (!isInPhase5)
            {

                return;

            }

            if (!ParseObjectId(@event["SourceId"], out var sid)) return;

            var r = 225f;
            var rot = (180 - r / 2) / 180f * float.Pi;

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P5_光与暗之翼";
            dp.Scale = new(20);
            dp.Owner = sid;
            dp.Radian = r / 180 * float.Pi;
            dp.TargetObject = accessory.Data.EnmityList[sid][0];
            dp.Rotation = @event["ActionId"] == "40313" ? rot : -rot;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 7300;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P5_光与暗之翼_远离靠近";
            dp.Scale = new(4);
            dp.Owner = sid;
            dp.CentreResolvePattern = @event["ActionId"] == "40313" ? PositionResolvePatternEnum.PlayerFarestOrder : PositionResolvePatternEnum.PlayerNearestOrder;
            dp.Rotation = @event["ActionId"] == "40313" ? rot : -rot;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 7300;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P5_光与暗之翼";
            dp.Scale = new(20);
            dp.Owner = sid;
            dp.Radian = r / 180 * float.Pi;
            dp.TargetResolvePattern = PositionResolvePatternEnum.OwnerTarget;
            dp.Rotation = @event["ActionId"] == "40313" ? -rot : rot;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 7300;
            dp.DestoryAt = 4000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P5_光与暗之翼_远离靠近";
            dp.Scale = new(4);
            dp.Owner = sid;
            dp.CentreResolvePattern = @event["ActionId"] == "40313" ? PositionResolvePatternEnum.PlayerNearestOrder : PositionResolvePatternEnum.PlayerFarestOrder;
            dp.Rotation = @event["ActionId"] == "40313" ? rot : -rot;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 7300;
            dp.DestoryAt = 4000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        [ScriptMethod(name: "Phase5 Acquire The First Tower Of Wings Dark And Light 获取光与暗之翼(踩塔)一塔",
            eventType: EventTypeEnum.EnvControl,
            eventCondition: ["DirectorId:800375BF", "State:00010004", "Index:regex:^(0000003[012])"],
            userControl: false)]

        public void Phase5_Acquire_The_First_Tower_Of_Wings_Dark_And_Light_获取光与暗之翼一塔(Event @event, ScriptAccessory accessory)
        {

            if (!isInPhase5)
            {

                return;

            }

            if (!phase5_hasAcquiredTheFirstTower)
            {

                phase5_indexOfTheFirstTower = @event["Index"];

                phase5_hasAcquiredTheFirstTower = true;

            }

        }

        [ScriptMethod(name:"Phase5 Initial Position Of The Current MT Before Towers 踩塔前当前MT的起始位置",
            eventType:EventTypeEnum.EnvControl,
            eventCondition:["DirectorId:800375BF","State:00010004","Index:regex:^(0000003[012])"])]

        public void Phase5_Initial_Position_Of_The_Current_MT_Before_Towers_踩塔前当前MT的起始位置(Event @event, ScriptAccessory accessory) {

            if(!isInPhase5) {

                return;

            }

            if(phase5_hasConfirmedTheInitialPosition) {

                return;

            }

            else {

                phase5_hasConfirmedTheInitialPosition=true;

            }

            if(!ParseObjectId(phase5_bossId, out var bossId)) {

                return;

            }

            if(!accessory.Data.EnmityList.TryGetValue(bossId, out var enmityListOfBoss)) {

                return;

            }

            if(Enable_Developer_Mode) {

                accessory.Method.SendChat($"""
                                           /e 
                                           accessory.Data.Me={accessory.Data.Me}
                                           enmityListOfTheBoss[0]={enmityListOfBoss[0]}

                                           """);

            }

            Parallel.ForEach(GuidanceIndices(accessory), 指路);

            void 指路(int 视角)
            {
                if(GuidanceOwner(accessory, 视角)!=enmityListOfBoss[0]) {

                    return;

                }

                while(!phase5_hasAcquiredTheFirstTower) {

                    System.Threading.Thread.Sleep(1);

                }

                System.Threading.Thread.MemoryBarrier();

                Vector3 positionOfTheFirstTower=new Vector3(100,0,100);

                if(phase5_indexOfTheFirstTower.Equals("00000030")) {

                    positionOfTheFirstTower=new Vector3(93.94f,0,96.50f);

                }

                if(phase5_indexOfTheFirstTower.Equals("00000031")) {

                    positionOfTheFirstTower=new Vector3(106.06f,0,96.50f);

                }

                if(phase5_indexOfTheFirstTower.Equals("00000032")) {

                    positionOfTheFirstTower=new Vector3(100f,0,107f);

                }

                if(positionOfTheFirstTower.Equals(new Vector3(100,0,100))) {

                    return;

                }

                var currentProperty=accessory.Data.GetDefaultDrawProperties();

                currentProperty.Name=$"Phase5_Initial_Position_Of_The_Current_MT_Before_Towers_踩塔前当前MT的起始位置-{视角}";
                currentProperty.Scale=new(2);
                currentProperty.Owner=GuidanceOwner(accessory, 视角);
                currentProperty.TargetPosition=RotatePoint(positionOfTheFirstTower,new Vector3(100,0,100),float.Pi);
                currentProperty.ScaleMode|=ScaleMode.YByDistance;
                currentProperty.Color=accessory.Data.DefaultSafeColor;
                currentProperty.DestoryAt=2300;

                accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperty);

            }
        }

        [ScriptMethod(name:"Phase5 Guidance For Tanks During Towers 坦克踩塔指路",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:regex:^(40313|40233)$"])]

        public void Phase5_Guidance_For_Tanks_During_Towers_坦克踩塔指路(Event @event, ScriptAccessory accessory) {

            if(!isInPhase5) {

                return;

            }

            if(!phase5_hasAcquiredTheFirstTower) {

                return;

            }

            Parallel.ForEach(GuidanceIndices(accessory), 指路);

            void 指路(int 视角)
            {
                if(视角!=0
                   &&
                   视角!=1) {

                    return;

                }

                bool isCurrentMt=true;

                if(!ParseObjectId(phase5_bossId, out var bossId)) {

                    return;

                }

                if(!accessory.Data.EnmityList.TryGetValue(bossId, out var enmityListOfBoss)) {

                    return;

                }

                if(Enable_Developer_Mode) {

                    if (IsMe(accessory, 视角)) accessory.Method.SendChat($"""
                                               /e 
                                               GuidanceOwner(accessory, 视角)={GuidanceOwner(accessory, 视角)}
                                               enmityListOfTheBoss[0]={enmityListOfBoss[0]}

                                               """);

                }

                if(GuidanceOwner(accessory, 视角)==enmityListOfBoss[0]) {

                    isCurrentMt=true;

                }

                else {

                    isCurrentMt=false;

                }

                bool isLeftFirstAndFarFirst=true;

                if(@event["ActionId"].Equals("40313")) {
                    // 40313 stands for left first then right, far first then close.

                    isLeftFirstAndFarFirst=true;

                }

                if(@event["ActionId"].Equals("40233")) {
                    // 40233 stands for right first then left, close first then far.

                    isLeftFirstAndFarFirst=false;

                }

                Vector3 positionOfTheFirstTower=new Vector3(100,0,100);

                if(phase5_indexOfTheFirstTower.Equals("00000030")) {

                    positionOfTheFirstTower=new Vector3(93.94f,0,96.50f);

                }

                if(phase5_indexOfTheFirstTower.Equals("00000031")) {

                    positionOfTheFirstTower=new Vector3(106.06f,0,96.50f);

                }

                if(phase5_indexOfTheFirstTower.Equals("00000032")) {

                    positionOfTheFirstTower=new Vector3(100f,0,107f);

                }

                if(positionOfTheFirstTower.Equals(new Vector3(100,0,100))) {

                    return;

                }

                Vector3 position1OfCurrentMt=RotatePoint(positionOfTheFirstTower,new Vector3(100,0,100),float.Pi);
                // Just opposite the first tower.
                Vector3 position2OfCurrentMt=(isLeftFirstAndFarFirst)?
                    (new((position1OfCurrentMt.X-100)/7+100,0,(position1OfCurrentMt.Z-100)/7+100)):
                    (new((position1OfCurrentMt.X-100)/7*18+100,0,(position1OfCurrentMt.Z-100)/7*18+100));
                // The calculations of Position 2 were directly inherited from Karlin's script.
                // I don't know the mathematical ideas behind the algorithm, but it works and it definitely works great.
                // So as a result, except the multiplier was adjusted from 15 to 18, I just keep the part as is.

                Vector3 position2OfCurrentOt=RotatePoint(position1OfCurrentMt,new(100,0,100),(isLeftFirstAndFarFirst)?
                                                                                                          (convertDegree(120f)):
                                                                                                          (convertDegree(-120f)));
                Vector3 position1OfCurrentOt=(isLeftFirstAndFarFirst)?
                    (new((position2OfCurrentOt.X-100)/7*18+100,0,(position2OfCurrentOt.Z-100)/7*18+100)):
                    (new((position2OfCurrentOt.X-100)/7+100,0,(position2OfCurrentOt.Z-100)/7+100));

                if(isCurrentMt) {

                    var currentProperty=accessory.Data.GetDefaultDrawProperties();

                    currentProperty.Name=$"Phase5_Guidance_1_For_The_Current_MT_During_Towers_当前MT踩塔指路1-{视角}";
                    currentProperty.Scale=new(2);
                    currentProperty.Owner=GuidanceOwner(accessory, 视角);
                    currentProperty.TargetPosition=position1OfCurrentMt;
                    currentProperty.ScaleMode|=ScaleMode.YByDistance;
                    currentProperty.Color=accessory.Data.DefaultSafeColor;
                    currentProperty.DestoryAt=7150;

                    accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperty);

                    currentProperty=accessory.Data.GetDefaultDrawProperties();

                    currentProperty.Name=$"Phase5_Guidance_2_Preview_For_The_Current_MT_During_Towers_当前MT踩塔指路2预览-{视角}";
                    currentProperty.Scale=new(2);
                    currentProperty.Position=position1OfCurrentMt;
                    currentProperty.TargetPosition=position2OfCurrentMt;
                    currentProperty.ScaleMode|=ScaleMode.YByDistance;
                    currentProperty.Color=accessory.Data.DefaultSafeColor;
                    currentProperty.DestoryAt=7150;

                    accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperty);

                    currentProperty=accessory.Data.GetDefaultDrawProperties();

                    currentProperty.Name=$"Phase5_Guidance_2_For_The_Current_MT_During_Towers_当前MT踩塔指路2-{视角}";
                    currentProperty.Scale=new(2);
                    currentProperty.Owner=GuidanceOwner(accessory, 视角);
                    currentProperty.TargetPosition=position2OfCurrentMt;
                    currentProperty.ScaleMode|=ScaleMode.YByDistance;
                    currentProperty.Color=accessory.Data.DefaultSafeColor;
                    currentProperty.Delay=7150;
                    currentProperty.DestoryAt=4250;

                    accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperty);

                    if(Phase5_Reminder_To_Provoke_During_Wings_Dark_And_Light) {

                        System.Threading.Thread.Sleep(1500);

                        if(Enable_Text_Prompts) {

                            if(Language_Of_Prompts==Languages_Of_Prompts.Simplified_Chinese_简体中文) {

                                if (IsMe(accessory, 视角)) accessory.Method.TextInfo("等待挑衅后退避",2500);

                            }

                            if(Language_Of_Prompts==Languages_Of_Prompts.English_英文) {

                                if (IsMe(accessory, 视角)) accessory.Method.TextInfo("Wait for provocation then shirk",2500);

                            }

                        }

                        if(Enable_Vanilla_TTS||Enable_Daily_Routines_TTS) {

                            if(Language_Of_Prompts==Languages_Of_Prompts.Simplified_Chinese_简体中文) {

                                if (IsMe(accessory, 视角)) accessory.TTS("等待挑衅后退避",Enable_Vanilla_TTS,Enable_Daily_Routines_TTS);

                            }

                            if(Language_Of_Prompts==Languages_Of_Prompts.English_英文) {

                                if (IsMe(accessory, 视角)) accessory.TTS("Wait for provocation then shirk",Enable_Vanilla_TTS,Enable_Daily_Routines_TTS);

                            }

                        }

                    }

                }

                else {

                    var currentProperty=accessory.Data.GetDefaultDrawProperties();

                    currentProperty.Name=$"Phase5_Guidance_1_For_The_Current_OT_During_Towers_当前ST踩塔指路1-{视角}";
                    currentProperty.Scale=new(2);
                    currentProperty.Owner=GuidanceOwner(accessory, 视角);
                    currentProperty.TargetPosition=position1OfCurrentOt;
                    currentProperty.ScaleMode|=ScaleMode.YByDistance;
                    currentProperty.Color=accessory.Data.DefaultSafeColor;
                    currentProperty.DestoryAt=7650;

                    accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperty);

                    currentProperty=accessory.Data.GetDefaultDrawProperties();

                    currentProperty.Name=$"Phase5_Guidance_2_Preview_For_The_Current_OT_During_Towers_当前ST踩塔指路2预览-{视角}";
                    currentProperty.Scale=new(2);
                    currentProperty.Position=position1OfCurrentOt;
                    currentProperty.TargetPosition=position2OfCurrentOt;
                    currentProperty.ScaleMode|=ScaleMode.YByDistance;
                    currentProperty.Color=accessory.Data.DefaultSafeColor;
                    currentProperty.DestoryAt=7650;

                    accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperty);

                    currentProperty=accessory.Data.GetDefaultDrawProperties();

                    currentProperty.Name=$"Phase5_Guidance_2_For_The_Current_OT_During_Towers_当前ST踩塔指路2-{视角}";
                    currentProperty.Scale=new(2);
                    currentProperty.Owner=GuidanceOwner(accessory, 视角);
                    currentProperty.TargetPosition=position2OfCurrentOt;
                    currentProperty.ScaleMode|=ScaleMode.YByDistance;
                    currentProperty.Color=accessory.Data.DefaultSafeColor;
                    currentProperty.Delay=7650;
                    currentProperty.DestoryAt=3750;

                    accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperty);

                    if(Phase5_Reminder_To_Provoke_During_Wings_Dark_And_Light) {

                        System.Threading.Thread.Sleep(1000);

                        if(Enable_Text_Prompts) {

                            if(Language_Of_Prompts==Languages_Of_Prompts.Simplified_Chinese_简体中文) {

                                if (IsMe(accessory, 视角)) accessory.Method.TextInfo("立即挑衅！",2500);

                            }

                            if(Language_Of_Prompts==Languages_Of_Prompts.English_英文) {

                                if (IsMe(accessory, 视角)) accessory.Method.TextInfo("Now provoke!",2500);

                            }

                        }

                        if(Enable_Vanilla_TTS||Enable_Daily_Routines_TTS) {

                            if(Language_Of_Prompts==Languages_Of_Prompts.Simplified_Chinese_简体中文) {

                                if (IsMe(accessory, 视角)) accessory.TTS("立即挑衅！",Enable_Vanilla_TTS,Enable_Daily_Routines_TTS);

                            }

                            if(Language_Of_Prompts==Languages_Of_Prompts.English_英文) {

                                if (IsMe(accessory, 视角)) accessory.TTS("Now provoke!",Enable_Vanilla_TTS,Enable_Daily_Routines_TTS);

                            }

                        }

                    }

                }

            }
        }

        [ScriptMethod(name:"Phase5 Guidance For Others During Towers 人群踩塔指路",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:regex:^(40313|40233)$"])]

        public void Phase5_Guidance_For_Others_During_Towers_人群踩塔指路(Event @event, ScriptAccessory accessory) {

            if(!isInPhase5) {

                return;

            }

            if(!phase5_hasAcquiredTheFirstTower) {

                return;

            }

            foreach (var 视角 in GuidanceIndices(accessory)) 指路(视角);

            void 指路(int 视角)
            {
                if(视角==0
                   ||
                   视角==1) {

                    return;

                }

                bool isLeftFirstAndFarFirst=true;

                if(@event["ActionId"].Equals("40313")) {

                    isLeftFirstAndFarFirst=true;

                }

                if(@event["ActionId"].Equals("40233")) {

                    isLeftFirstAndFarFirst=false;

                }

                // The previous spaghetti code is commented out. The entire part was reworked.

                float rotation=0;

                if(phase5_indexOfTheFirstTower.Equals("00000030")) {

                    rotation=float.Pi/3*2;

                }

                if(phase5_indexOfTheFirstTower.Equals("00000031")) {

                    rotation=-(float.Pi/3*2);

                }

                if(phase5_indexOfTheFirstTower.Equals("00000032")) {

                    rotation=0;

                }

                Vector3 positionOfTheFirstTower=RotatePoint(new Vector3(100,0,107),new Vector3(100,0,100),rotation);
                Vector3 positionOfTheLeftTower=RotatePoint(positionOfTheFirstTower,new Vector3(100,0,100),float.Pi/3*2);
                Vector3 positionOfTheRightTower=RotatePoint(positionOfTheFirstTower,new Vector3(100,0,100),-(float.Pi/3*2));
                Vector3 leftOfTheFirstTower=RotatePoint(phase5_leftSideOfTheSouth_asAConstant,new Vector3(100,0,100),rotation);
                Vector3 rightOfTheFirstTower=RotatePoint(phase5_rightSideOfTheSouth_asAConstant,new Vector3(100,0,100),rotation);
                Vector3 leftOfTheLeftTower=RotatePoint(phase5_leftSideOfTheNorthwest_asAConstant,new Vector3(100,0,100),rotation);
                Vector3 rightOfTheRightTower=RotatePoint(phase5_rightSideOfTheNortheast_asAConstant,new Vector3(100,0,100),rotation);
                Vector3 oppositeStandbyPosition=RotatePoint(positionOfTheFirstTower,new Vector3(100,0,100),float.Pi);
                Vector3 leftStandbyPosition=RotatePoint(phase5_standbyPointBetweenSouthAndNorthwest_asAConstant,new Vector3(100,0,100),rotation);
                Vector3 rightStandbyPosition=RotatePoint(phase5_standbyPointBetweenSouthAndNortheast_asAConstant,new Vector3(100,0,100),rotation);
                var currentProperty=accessory.Data.GetDefaultDrawProperties();

                // LPDU：奶妈踩一塔；G1 DPS（M1 R1）去左塔（相对西北），G2 DPS（M2 R2）去右塔（相对东北），都站塔靠相对北的边
                bool isG1=false;
                bool isG2=false;
                bool isHealer=false;

                if(视角==2
                   ||
                   视角==3) {

                    isHealer=true;

                    currentProperty = accessory.Data.GetDefaultDrawProperties();

                    currentProperty.Name=$"Phase5_Guidance_1_For_Healers_During_Towers_奶妈踩塔指路1-{视角}";
                    currentProperty.Scale=new(2);
                    currentProperty.Owner=GuidanceOwner(accessory, 视角);
                    currentProperty.TargetPosition=(isLeftFirstAndFarFirst)?(rightOfTheFirstTower):(leftOfTheFirstTower);
                    currentProperty.ScaleMode|=ScaleMode.YByDistance;
                    currentProperty.Color=accessory.Data.DefaultSafeColor;
                    currentProperty.DestoryAt=7300;

                    accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperty);

                    currentProperty=accessory.Data.GetDefaultDrawProperties();

                    currentProperty.Name=$"Phase5_Guidance_2_Preview_For_Healers_During_Towers_奶妈踩塔指路2预览-{视角}";
                    currentProperty.Scale=new(2);
                    currentProperty.Position=(isLeftFirstAndFarFirst)?(rightOfTheFirstTower):(leftOfTheFirstTower);
                    currentProperty.TargetPosition=oppositeStandbyPosition;
                    currentProperty.ScaleMode|=ScaleMode.YByDistance;
                    currentProperty.Color=accessory.Data.DefaultSafeColor;
                    currentProperty.DestoryAt=7300;

                    accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperty);

                    currentProperty=accessory.Data.GetDefaultDrawProperties();

                    currentProperty.Name=$"Phase5_Guidance_2_For_Healers_During_Towers_奶妈踩塔指路2-{视角}";
                    currentProperty.Scale=new(2);
                    currentProperty.Owner=GuidanceOwner(accessory, 视角);
                    currentProperty.TargetPosition=oppositeStandbyPosition;
                    currentProperty.ScaleMode|=ScaleMode.YByDistance;
                    currentProperty.Color=accessory.Data.DefaultSafeColor;
                    currentProperty.Delay=7300;
                    currentProperty.DestoryAt=7100;

                    accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperty);

                }

                if(视角==5
                   ||
                   视角==7) {

                    isG2=true;

                    currentProperty=accessory.Data.GetDefaultDrawProperties();

                    currentProperty.Name=$"Phase5_Guidance_1_For_Ranges_During_Towers_远程踩塔指路1-{视角}";
                    currentProperty.Scale=new(2);
                    currentProperty.Owner=GuidanceOwner(accessory, 视角);
                    currentProperty.TargetPosition=(isLeftFirstAndFarFirst)?(rightStandbyPosition):(leftStandbyPosition);
                    currentProperty.ScaleMode|=ScaleMode.YByDistance;
                    currentProperty.Color=accessory.Data.DefaultSafeColor;
                    currentProperty.DestoryAt=6900;

                    accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperty);

                    currentProperty=accessory.Data.GetDefaultDrawProperties();

                    currentProperty.Name=$"Phase5_Guidance_2_Preview_For_Ranges_During_Towers_远程踩塔指路2预览-{视角}";
                    currentProperty.Scale=new(2);
                    currentProperty.Position=(isLeftFirstAndFarFirst)?(rightStandbyPosition):(leftStandbyPosition);
                    currentProperty.TargetPosition=rightOfTheRightTower;
                    currentProperty.ScaleMode|=ScaleMode.YByDistance;
                    currentProperty.Color=accessory.Data.DefaultSafeColor;
                    currentProperty.DestoryAt=6900;

                    accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperty);

                    currentProperty=accessory.Data.GetDefaultDrawProperties();

                    currentProperty.Name=$"Phase5_Guidance_2_For_Ranges_During_Towers_远程踩塔指路2-{视角}";
                    currentProperty.Scale=new(2);
                    currentProperty.Owner=GuidanceOwner(accessory, 视角);
                    currentProperty.TargetPosition=rightOfTheRightTower;
                    currentProperty.ScaleMode|=ScaleMode.YByDistance;
                    currentProperty.Color=accessory.Data.DefaultSafeColor;
                    currentProperty.Delay=6900;
                    currentProperty.DestoryAt=7500;

                    accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperty);

                }

                if(视角==4
                   ||
                   视角==6) {

                    isG1=true;

                    currentProperty=accessory.Data.GetDefaultDrawProperties();

                    currentProperty.Name=$"Phase5_Guidance_1_For_Melees_During_Towers_近战踩塔指路1-{视角}";
                    currentProperty.Scale=new(2);
                    currentProperty.Owner=GuidanceOwner(accessory, 视角);
                    currentProperty.TargetPosition=(isLeftFirstAndFarFirst)?(rightStandbyPosition):(leftStandbyPosition);
                    currentProperty.ScaleMode|=ScaleMode.YByDistance;
                    currentProperty.Color=accessory.Data.DefaultSafeColor; 
                    currentProperty.DestoryAt=6900;

                    accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperty);

                    currentProperty=accessory.Data.GetDefaultDrawProperties();

                    currentProperty.Name=$"Phase5_Guidance_2_Preview_For_Melees_During_Towers_近战踩塔指路2预览-{视角}";
                    currentProperty.Scale=new(2);
                    currentProperty.Position=(isLeftFirstAndFarFirst)?(rightStandbyPosition):(leftStandbyPosition);
                    currentProperty.TargetPosition=leftOfTheLeftTower;
                    currentProperty.ScaleMode|=ScaleMode.YByDistance;
                    currentProperty.Color=accessory.Data.DefaultSafeColor;
                    currentProperty.DestoryAt=6900;

                    accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperty);

                    currentProperty=accessory.Data.GetDefaultDrawProperties();

                    currentProperty.Name=$"Phase5_Guidance_2_For_Melees_During_Towers_近战踩塔指路2-{视角}";
                    currentProperty.Scale=new(2);
                    currentProperty.Owner=GuidanceOwner(accessory, 视角);
                    currentProperty.TargetPosition=leftOfTheLeftTower;
                    currentProperty.ScaleMode|=ScaleMode.YByDistance;
                    currentProperty.Color=accessory.Data.DefaultSafeColor;
                    currentProperty.Delay=6900;
                    currentProperty.DestoryAt=7500;

                    accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperty);

                }

                currentProperty=accessory.Data.GetDefaultDrawProperties();

                currentProperty.Name=$"Phase5_Range_Of_Towers_塔的范围-{视角}";
                currentProperty.Scale=new(3);
                currentProperty.Position=positionOfTheFirstTower;
                currentProperty.Color=(isHealer)?(accessory.Data.DefaultSafeColor):(accessory.Data.DefaultDangerColor);
                currentProperty.DestoryAt=7300;

                accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Circle,currentProperty);

                currentProperty=accessory.Data.GetDefaultDrawProperties();

                currentProperty.Name=$"Phase5_Range_Of_Towers_塔的范围-{视角}";
                currentProperty.Scale=new(3);
                currentProperty.Position=positionOfTheLeftTower;
                currentProperty.Color=(isG1)?(accessory.Data.DefaultSafeColor):(accessory.Data.DefaultDangerColor);
                currentProperty.DestoryAt=14400;

                accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Circle,currentProperty);

                currentProperty=accessory.Data.GetDefaultDrawProperties();

                currentProperty.Name=$"Phase5_Range_Of_Towers_塔的范围-{视角}";
                currentProperty.Scale=new(3);
                currentProperty.Position=positionOfTheRightTower;
                currentProperty.Color=(isG2)?(accessory.Data.DefaultSafeColor):(accessory.Data.DefaultDangerColor);
                currentProperty.DestoryAt=14400;

                accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Circle,currentProperty);

            }
        }

        [ScriptMethod(name: "Phase5 Boss Central Axis During Polarizing Strikes 极化打击(挡枪)期间Boss中轴线",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:40316"])]

        public void Phase5_Boss_Central_Axis_During_Polarizing_Strikes_极化打击期间Boss中轴线(Event @event, ScriptAccessory accessory)
        {

            if (!isInPhase5)
            {

                return;

            }

            if (!ParseObjectId(@event["SourceId"], out var sourceId))
            {

                return;

            }

            var currentProperty = accessory.Data.GetDefaultDrawProperties();

            currentProperty.Name = "Phase5_Boss_Front_Axis_During_Polarizing_Strikes_极化打击期间Boss前轴线";
            currentProperty.Owner = sourceId;
            currentProperty.Scale = new(0.5f, 10);
            currentProperty.Color = Phase5_Colour_Of_The_Boss_Central_Axis.V4.WithW(25f);
            currentProperty.DestoryAt = 24000;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, currentProperty);

            currentProperty = accessory.Data.GetDefaultDrawProperties();

            currentProperty.Name = "Phase5_Boss_Rear_Axis_During_Polarizing_Strikes_极化打击期间Boss后轴线";
            currentProperty.Owner = sourceId;
            currentProperty.Scale = new(0.5f, 10);
            currentProperty.Rotation = float.Pi;
            currentProperty.Color = Phase5_Colour_Of_The_Boss_Central_Axis.V4.WithW(25f);
            currentProperty.DestoryAt = 24000;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, currentProperty);

        }

        [ScriptMethod(name: "Phase5 Guidance Of Polarizing Strikes 极化打击(挡枪)指路",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:40316"])]

        public void Phase5_Guidance_Of_Polarizing_Strikes_极化打击指路(Event @event, ScriptAccessory accessory)
        {

            if (!isInPhase5)
            {

                return;

            }

            if (!float.TryParse(@event["SourceRotation"], out float currentRotation))
            {

                return;

            }

            currentRotation = -(currentRotation - float.Pi);

            if (Enable_Developer_Mode)
            {

                accessory.Method.SendChat($"""
                                           /e 
                                           currentRotation={currentRotation}

                                           """);

            }

            Parallel.ForEach(GuidanceIndices(accessory), 指路);

            void 指路(int 视角)
            {
                int myIndex = 视角;
                int myRoundToTakeHits = phase5_getRoundToTakeHits(myIndex);
                bool inTheLeftGroup = true;
                int timelineControl = 0;
                int timeToTakeHits = 0;
                var currentProperty = accessory.Data.GetDefaultDrawProperties();

                if (myRoundToTakeHits < 1 || myRoundToTakeHits > 4)
                {

                    return;

                }

                if (myIndex == 0
                   ||
                   myIndex == 2
                   ||
                   myIndex == 4
                   ||
                   myIndex == 6)
                {

                    inTheLeftGroup = true;

                }

                if (myIndex == 1
                   ||
                   myIndex == 3
                   ||
                   myIndex == 5
                   ||
                   myIndex == 7)
                {

                    inTheLeftGroup = false;

                }

                // ----- Initial guidance -----

                if (myRoundToTakeHits == 1)
                {

                    currentProperty = accessory.Data.GetDefaultDrawProperties();

                    currentProperty.Name = $"Phase5_Initial_Guidance_Of_Polarizing_Strikes_极化打击初始指路-{视角}";
                    currentProperty.Scale = new(2);
                    currentProperty.Owner = GuidanceOwner(accessory, 视角);
                    currentProperty.TargetPosition = inTheLeftGroup ?
                        RotatePoint(phase5_positionToTakeHitsOnTheLeft_asAConstant, new Vector3(100, 0, 100), currentRotation) :
                        RotatePoint(phase5_positionToTakeHitsOnTheRight_asAConstant, new Vector3(100, 0, 100), currentRotation);
                    currentProperty.ScaleMode |= ScaleMode.YByDistance;
                    currentProperty.Color = accessory.Data.DefaultSafeColor;
                    currentProperty.DestoryAt = 4550;
                    timelineControl += 4550;

                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, currentProperty);

                }

                else
                {

                    currentProperty = accessory.Data.GetDefaultDrawProperties();

                    currentProperty.Name = $"Phase5_Initial_Guidance_Of_Polarizing_Strikes_极化打击初始指路-{视角}";
                    currentProperty.Scale = new(2);
                    currentProperty.Owner = GuidanceOwner(accessory, 视角);
                    currentProperty.TargetPosition = inTheLeftGroup ?
                        RotatePoint(phase5_positionToBeCoveredOnTheLeft_asAConstant, new Vector3(100, 0, 100), currentRotation) :
                        RotatePoint(phase5_positionToBeCoveredOnTheRight_asAConstant, new Vector3(100, 0, 100), currentRotation);
                    currentProperty.ScaleMode |= ScaleMode.YByDistance;
                    currentProperty.Color = accessory.Data.DefaultSafeColor;
                    currentProperty.DestoryAt = 4550;
                    timelineControl += 4550;

                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, currentProperty);

                }

                // ----- Be covered in the current group -----

                for (int i = 1; i < myRoundToTakeHits; ++i)
                {

                    currentProperty = accessory.Data.GetDefaultDrawProperties();

                    currentProperty.Name = $"Phase5_Inward_Guidance_Of_Polarizing_Strikes_In_The_Current_Group_极化打击当前组进指路-{视角}";
                    currentProperty.Scale = new(2);
                    currentProperty.Owner = GuidanceOwner(accessory, 视角);
                    currentProperty.TargetPosition = inTheLeftGroup ?
                        RotatePoint(phase5_positionToBeCoveredOnTheLeft_asAConstant, new Vector3(100, 0, 100), currentRotation) :
                        RotatePoint(phase5_positionToBeCoveredOnTheRight_asAConstant, new Vector3(100, 0, 100), currentRotation);
                    currentProperty.ScaleMode |= ScaleMode.YByDistance;
                    currentProperty.Color = accessory.Data.DefaultSafeColor;
                    currentProperty.Delay = timelineControl;
                    currentProperty.DestoryAt = 2450;
                    timelineControl += 2450;

                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, currentProperty);

                    currentProperty = accessory.Data.GetDefaultDrawProperties();

                    currentProperty.Name = $"Phase5_Outward_Guidance_Of_Polarizing_Strikes_In_The_Current_Group_极化打击当前组出指路-{视角}";
                    currentProperty.Scale = new(2);
                    currentProperty.Owner = GuidanceOwner(accessory, 视角);
                    currentProperty.TargetPosition = inTheLeftGroup ?
                        RotatePoint(phase5_positionToStandbyOnTheLeft_asAConstant, new Vector3(100, 0, 100), currentRotation) :
                        RotatePoint(phase5_positionToStandbyOnTheRight_asAConstant, new Vector3(100, 0, 100), currentRotation);
                    currentProperty.ScaleMode |= ScaleMode.YByDistance;
                    currentProperty.Color = accessory.Data.DefaultSafeColor;
                    currentProperty.Delay = timelineControl;
                    currentProperty.DestoryAt = 2250;
                    timelineControl += 2250;

                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, currentProperty);

                }

                // ----- -----

                // ----- Take hits and swap the group -----

                timeToTakeHits = timelineControl - 250;

                currentProperty = accessory.Data.GetDefaultDrawProperties();

                currentProperty.Name = $"Phase5_Inward_Guidance_Of_Polarizing_Strikes_While_Taking_Hits_极化打击挡枪进指路-{视角}";
                currentProperty.Scale = new(2);
                currentProperty.Owner = GuidanceOwner(accessory, 视角);
                currentProperty.TargetPosition = inTheLeftGroup ?
                    RotatePoint(phase5_positionToTakeHitsOnTheLeft_asAConstant, new Vector3(100, 0, 100), currentRotation) :
                    RotatePoint(phase5_positionToTakeHitsOnTheRight_asAConstant, new Vector3(100, 0, 100), currentRotation);
                currentProperty.ScaleMode |= ScaleMode.YByDistance;
                currentProperty.Color = accessory.Data.DefaultSafeColor;
                currentProperty.Delay = timelineControl;
                currentProperty.DestoryAt = 2450;
                timelineControl += 2450;

                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, currentProperty);

                currentProperty = accessory.Data.GetDefaultDrawProperties();

                currentProperty.Name = $"Phase5_Outward_Guidance_Of_Polarizing_Strikes_While_Taking_Hits_极化打击挡枪出指路-{视角}";
                currentProperty.Scale = new(2);
                currentProperty.Owner = GuidanceOwner(accessory, 视角);
                currentProperty.TargetPosition = inTheLeftGroup ?
                    RotatePoint(phase5_positionToStandbyOnTheRight_asAConstant, new Vector3(100, 0, 100), currentRotation) :
                    RotatePoint(phase5_positionToStandbyOnTheLeft_asAConstant, new Vector3(100, 0, 100), currentRotation);
                currentProperty.ScaleMode |= ScaleMode.YByDistance;
                currentProperty.Color = accessory.Data.DefaultSafeColor;
                currentProperty.Delay = timelineControl;
                currentProperty.DestoryAt = 2250;
                timelineControl += 2250;

                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, currentProperty);

                // ----- -----

                // ----- Be covered in the opposite group -----

                for (int i = myRoundToTakeHits + 1; i <= 4; ++i)
                {

                    currentProperty = accessory.Data.GetDefaultDrawProperties();

                    currentProperty.Name = $"Phase5_Inward_Guidance_Of_Polarizing_Strikes_In_The_Opposite_Group_极化打击对组进指路-{视角}";
                    currentProperty.Scale = new(2);
                    currentProperty.Owner = GuidanceOwner(accessory, 视角);
                    currentProperty.TargetPosition = inTheLeftGroup ?
                        RotatePoint(phase5_positionToBeCoveredOnTheRight_asAConstant, new Vector3(100, 0, 100), currentRotation) :
                        RotatePoint(phase5_positionToBeCoveredOnTheLeft_asAConstant, new Vector3(100, 0, 100), currentRotation);
                    currentProperty.ScaleMode |= ScaleMode.YByDistance;
                    currentProperty.Color = accessory.Data.DefaultSafeColor;
                    currentProperty.Delay = timelineControl;
                    currentProperty.DestoryAt = 2450;
                    timelineControl += 2450;

                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, currentProperty);

                    currentProperty = accessory.Data.GetDefaultDrawProperties();

                    currentProperty.Name = $"Phase5_Outward_Guidance_Of_Polarizing_Strikes_In_The_Opposite_Group_极化打击对组出指路-{视角}";
                    currentProperty.Scale = new(2);
                    currentProperty.Owner = GuidanceOwner(accessory, 视角);
                    currentProperty.TargetPosition = inTheLeftGroup ?
                        RotatePoint(phase5_positionToStandbyOnTheRight_asAConstant, new Vector3(100, 0, 100), currentRotation) :
                        RotatePoint(phase5_positionToStandbyOnTheLeft_asAConstant, new Vector3(100, 0, 100), currentRotation);
                    currentProperty.ScaleMode |= ScaleMode.YByDistance;
                    currentProperty.Color = accessory.Data.DefaultSafeColor;
                    currentProperty.Delay = timelineControl;
                    currentProperty.DestoryAt = 2250;
                    timelineControl += 2250;

                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, currentProperty);

                }

                System.Threading.Thread.Sleep(timeToTakeHits);

                if (Enable_Text_Prompts)
                {

                    if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                    {

                        if (IsMe(accessory, 视角)) accessory.Method.TextInfo("挡枪然后换组", 1500);

                    }

                    if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                    {

                        if (IsMe(accessory, 视角)) accessory.Method.TextInfo("Take hits and swap the group", 1500);

                    }

                }

                if (Enable_Vanilla_TTS || Enable_Daily_Routines_TTS)
                {

                    if (Language_Of_Prompts == Languages_Of_Prompts.Simplified_Chinese_简体中文)
                    {

                        if (IsMe(accessory, 视角)) accessory.TTS("挡枪然后换组", Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);

                    }

                    if (Language_Of_Prompts == Languages_Of_Prompts.English_英文)
                    {

                        if (IsMe(accessory, 视角)) accessory.TTS("Take hits and swap the group", Enable_Vanilla_TTS, Enable_Daily_Routines_TTS);

                    }

                }

            }
        }

        private int phase5_getRoundToTakeHits(int currentIndex)
        {

            if (currentIndex == 0 || currentIndex == 1)
            {
                // Tanks.

                return 1;

            }

            if (currentIndex == 4 || currentIndex == 5)
            {
                // Melees.

                return 2;

            }

            if (currentIndex == 6 || currentIndex == 7)
            {
                // Ranges.

                return 3;

            }

            if (currentIndex == 2 || currentIndex == 3)
            {
                // Healers.

                return 4;

            }

            return -1;
            // Just a placeholder and should never be reached.

        }

        public static Vector2? mathPoint(Blade b1, Blade b2)
        {
            //计算方向的正弦和余弦
            float s1 = (float)Math.Sin(b1.Rotation);
            float c1 = (float)Math.Cos(b1.Rotation);
            float s2 = (float)Math.Sin(b2.Rotation);
            float c2 = (float)Math.Cos(b2.Rotation);

            //起点
            float x1 = (float)b1.X;
            float y1 = (float)b1.Y;
            float x2 = (float)b2.X;
            float y2 = (float)b2.Y;

            //计算分母
            float d = s1 * c2 - s2 * c1;

            //检查合法
            if (Math.Abs(d) < 1e-10)
            {
                return null; // 平行
            }

            //计算交点 感恩阿洛
            float X = (x1 * s1 * c2 - x2 * s2 * c1 - (y2 - y1) * c1 * c2) / d;
            float Y = (y2 * c2 * s1 - y1 * c1 * s2 + (x2 - x1) * s1 * s2) / d;

            return new Vector2(X, Y);
        }

        public static Vector2? middlePoint(Vector2? P1, Vector2? P2)
        {
            if (P1.HasValue && P2.HasValue)
            {
                float midX = (P1.Value.X + P2.Value.X) / 2;
                float midY = (P1.Value.Y + P2.Value.Y) / 2;
                return new Vector2(midX, midY);
            }
            return null; //如果任意一个点为 null，返回 null
        }

        //获取与中点最近的点
        public static onPoint FindClosestOnPoint(List<onPoint> points, Vector2? target)
        {
            onPoint closestPoint = null;
            float closestDistance = float.MaxValue;
            foreach (var point in points)
            {
                // 计算距离
                float distance = Vector2.Distance(point.OnCoord, target.Value);
                //如果当前距离小于已知最小距离，则更新
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestPoint = point;
                }
            }
            return closestPoint;//返回最接近的点
        }

        //正点子点最远
        public static Vector2 FindFarthestPoint(onPoint point, Vector2? referencePoint)
        {
            //存储所有坐标
            Vector2[] coords = { point.Coord1, point.Coord2, point.Coord3, point.Coord4 };
            float maxDistance = float.MinValue;//初始化最大距离
            Vector2 farthestCoord = Vector2.Zero;//初始化最远坐标
                                                 //遍历所有坐标找最远
            foreach (var coord in coords)
            {
                float distance = Vector2.Distance(coord, referencePoint.Value);//计算距离
                if (distance > maxDistance)//如果当前距离大于已知最大距离
                {
                    maxDistance = distance;//更新最大距离
                    farthestCoord = coord;//更新最远坐标
                }
            }
            return farthestCoord;//返回最远的坐标
        }

        //正点子点最近
        public static Vector2 FindClosestPoint(onPoint point, Vector2? referencePoint)
        {
            //存储所有坐标
            Vector2[] coords = { point.Coord1, point.Coord2, point.Coord3, point.Coord4 };

            float minDistance = float.MaxValue;//初始化最小距离
            Vector2 closestCoord = Vector2.Zero;//初始化最近坐标

            // 遍历所有坐标，找到最近的
            foreach (var coord in coords)
            {
                float distance = Vector2.Distance(coord, referencePoint.Value);//计算距离
                if (distance < minDistance)//如果当前距离小于已知最小距离
                {
                    minDistance = distance;//更新最小距离
                    closestCoord = coord;//更新最近坐标
                }
            }
            return closestCoord;//返回最近的坐标
        }

        public static Vector3 Vector3Fucker(Vector2? V)
        {
            Vector3 result = new Vector3();
            if (V.HasValue)
            {
                result.X = V.Value.X;
                result.Y = 0;
                result.Z = V.Value.Y;
            }
            return result;
        }

        [ScriptMethod(name: "P5地火", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40306"], userControl: false)]
        public void 阶段记录_P5地火(Event @event, ScriptAccessory accessory)
        {
            Phase = "P5地火";
            blades.Clear();
            P1P3Blades.Clear();
            BladeRoutes.Clear();
            bladeCount = 0;
            BladeRoutes = Enumerable.Repeat<Vector2?>(null, 7).ToList();
            resetPoints();//初始化地火坐标
        }

        [ScriptMethod(name: "调试开关", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40306"], userControl: false)]
        public void 阶段记录_P5调试(Event @event, ScriptAccessory accessory)
        {
            if(Enable_Developer_Mode)accessory.Method.SendChat($"/e KnightRider祝地火順利~");
        }

        //捕获组
        [ScriptMethod(name: "地火数据捕获", eventType: EventTypeEnum.ObjectEffect, eventCondition: ["Id1:1"], userControl: false)]
        public void 地火数据捕获(Event @event, ScriptAccessory accessory)
        {
            if (Phase == "P5地火")//捕获限定区域
            {
                lock (bladeLock)
                {
                    if (bladeCount < 7) //如果地火<7就继续捕获
                    {
                        var pos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
                        //存入数据
                        blades.Add(new Blade(
                            id: Convert.ToUInt32(@event["SourceId"], 16),
                            x: Convert.ToDouble(pos.X),
                            y: Convert.ToDouble(pos.Z),
                            rotation: Convert.ToDouble(@event["SourceRotation"])
                        ));
                        bladeCount++;
                        //accessory.Method.SendChat($"/e {bladeCount}");
                    }
                    if (blades.Count == 6) //如果收集到了6个地火数据
                    {
                        ProcessBlades();//处理三个交点+更改阶段=>P5地火计算完成
                        //accessory.Method.SendChat($"/e 去{OnPoint.Name}点起跑");
                    }
                }
            }
        }
        
        private void ProcessBlades()
        {
            //accessory.Method.SendChat($"/e 收集完成");
            var sortedBlades = blades.OrderBy(b => b.Id).ToList();//按OID排序List
            //accessory.Method.SendChat($"/e 排序完成");
            if (sortedBlades != null)
            {
                //accessory.Method.SendChat($"/e 排序完成");
                //存入13点
                P1P3Blades.Add(sortedBlades[0]);
                P1P3Blades.Add(sortedBlades[1]);
                P1P3Blades.Add(sortedBlades[4]);
                P1P3Blades.Add(sortedBlades[5]);
                //计算三个交点
                Point1 = mathPoint(sortedBlades[0], sortedBlades[1]);//计算第1交点
                Point2 = mathPoint(sortedBlades[2], sortedBlades[3]);//计算第2交点
                Point3 = mathPoint(sortedBlades[4], sortedBlades[5]);//计算第3交点
                MiddlePoint = middlePoint(Point1, Point3);//计算第13中点
                OnPoint = FindClosestOnPoint(onPoints,MiddlePoint);//计算从哪个正点开始起跑
                //accessory.Method.SendChat($"/e 去{OnPoint.Name}点起跑");
                Phase = "P5地火计算完成";
            }
        }

        #region P5 补丁 (Usami)

        #region P5.1 光尘之剑

        [ScriptMethod(name: "光尘之剑阶段转换（DEBUG ONLY）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(40306)$"],
            userControl: Debugging)]
        public void FulgentBladePhaseChange(Event @event, ScriptAccessory accessory)
        {
            _fruPhase = FruPhase.P5A_FulgentBlade;
            accessory.DebugMsg($"当前阶段为：{_fruPhase}", Enable_Developer_Mode);
        }

        #endregion P5.1 光尘之剑

        #region P5.2 复乐园

        [ScriptMethod(name: "复乐园阶段转换（DEBUG ONLY）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(40319)$"],
            userControl: Debugging)]
        public void ParadiseRegainedPhaseChange(Event @event, ScriptAccessory accessory)
        {
            _fruPhase = FruPhase.P5B_ParadiseRegained;
            accessory.DebugMsg($"当前阶段为：{_fruPhase}", Enable_Developer_Mode);
        }

        #endregion P5.2 复乐园

        #region P5.3 星灵之剑

        [ScriptMethod(name: "星灵之剑阶段转换（DEBUG ONLY）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(40316)$"],
            userControl: Debugging)]
        public void PolStrikePhaseChange(Event @event, ScriptAccessory accessory)
        {
            _fruPhase = FruPhase.P5C_PolarizingStrike;
            accessory.DebugMsg($"当前阶段为：{_fruPhase}", Enable_Developer_Mode);
        }

        #endregion P5.3 星灵之剑

        #endregion

        #endregion
        
        #region Common_Mathematical_Wheels_常用数学轮子

        public static float convertDegree(float degree) {
            
            return degree*float.Pi/180f;
            
        }
        
        private int ParsTargetIcon(string id)
        {
            firstTargetIcon ??= int.Parse(id, System.Globalization.NumberStyles.HexNumber);
            return int.Parse(id, System.Globalization.NumberStyles.HexNumber) - (int)firstTargetIcon;
        }
        private static bool ParseObjectId(string? idStr, out ulong id)
        {
            id = 0;
            if (string.IsNullOrEmpty(idStr)) return false;
            try
            {
                var idStr2 = idStr.Replace("0x", "");
                id = ulong.Parse(idStr2, System.Globalization.NumberStyles.HexNumber);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
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
        private int PositionTo6Dir(Vector3 point, Vector3 centre)
        {
            var r = Math.Round(3 - 3 * Math.Atan2(point.X - centre.X, point.Z - centre.Z) / Math.PI) % 6;
            return (int)r;

        }
        private Vector3 RotatePoint(Vector3 point, Vector3 centre, float radian)
        {

            Vector2 v2 = new(point.X - centre.X, point.Z - centre.Z);

            var rot = (MathF.PI - MathF.Atan2(v2.X, v2.Y) + radian);
            var lenth = v2.Length();
            return new(centre.X + MathF.Sin(rot) * lenth, centre.Y, centre.Z - MathF.Cos(rot) * lenth);
        }
        
        #endregion
        
    }

    #region Other_Common_Wheels_其他常用轮子

    public static class Extensions
    {
        public static void TTS(this ScriptAccessory accessory, string text, bool isTTS, bool isDRTTS)
        {
            if (isTTS && isDRTTS)
            {
                accessory.Method.TTS(text);
            }
            else
            {
                if (isDRTTS)
                {
                    accessory.Method.SendChat($"/pdr tts {text}");
                }
                else if (isTTS)
                {
                    accessory.Method.TTS(text);
                }
            }
        }
    }

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
        public static uint DurationMilliseconds(this Event @event)
        {
            return JsonConvert.DeserializeObject<uint>(@event["DurationMilliseconds"]);
        }

        public static uint Id2(this Event @event)
        {
            return ParseHexId(@event["Id2"], out var id) ? id : 0;
        }

        public static uint Id0(this Event @event)
        {
            return ParseHexId(@event["Id"], out var id) ? id : 0;
        }

        public static uint TargetIndex(this Event @event)
        {
            return JsonConvert.DeserializeObject<uint>(@event["TargetIndex"]);
        }

        public static string Message(this Event ev)
        {
            return ev["Message"];
        }

        public static Vector3 EffectPosition(this Event @event)
        {
            return JsonConvert.DeserializeObject<Vector3>(@event["EffectPosition"]);
        }
    }

    public static class IbcHelper
    {
        public static IGameObject? GetById(this ScriptAccessory sa, ulong id)
        {
            return sa.Data.Objects.SearchById(id);
        }

        public static IGameObject? GetMe(this ScriptAccessory sa)
        {
            return sa.Data.Objects.LocalPlayer;
        }

        public static IEnumerable<IGameObject?> GetByDataId(this ScriptAccessory sa, uint dataId)
        {
            return sa.Data.Objects.Where(x => x.DataId == dataId);
        }

        public static string GetCharJob(this ScriptAccessory sa, uint id, bool fullName = false)
        {
            IPlayerCharacter? chara = (IPlayerCharacter?)sa.GetById(id);
            if (chara == null) return "None";
            return fullName ? chara.ClassJob.Value.Name.ToString() : chara.ClassJob.Value.Abbreviation.ToString();
        }

        public static bool AtNorth(this ScriptAccessory sa, uint id, float centerZ)
        {
            var chara = sa.GetById(id);
            if (chara == null) return false;
            return chara.Position.Z <= centerZ;
        }
        public static bool AtSouth(this ScriptAccessory sa, uint id, float centerZ)
        {
            var chara = sa.GetById(id);
            if (chara == null) return false;
            return chara.Position.Z > centerZ;
        }
        public static bool AtWest(this ScriptAccessory sa, uint id, float centerX)
        {
            var chara = sa.GetById(id);
            if (chara == null) return false;
            return chara.Position.X <= centerX;
        }
        public static bool AtEast(this ScriptAccessory sa, uint id, float centerX)
        {
            var chara = sa.GetById(id);
            if (chara == null) return false;
            return chara.Position.X > centerX;
        }

        public static bool HasStatus(this ScriptAccessory sa, uint id, uint statusId)
        {
            IGameObject? chara = sa.GetById(id);
            if (chara == null || !chara.IsValid()) return false;
            unsafe
            {
                BattleChara* charaStruct = (BattleChara*)chara.Address;
                return charaStruct->GetStatusManager()->HasStatus(statusId, id);
            }
        }

        public static bool HasStatus(this ScriptAccessory sa, IBattleChara? battleChara, uint statusId)
        {
            if (battleChara == null || !battleChara.IsValid()) return false;
            unsafe
            {
                BattleChara* charaStruct = (BattleChara*)battleChara.Address;
                return charaStruct->GetStatusManager()->GetStatusIndex(statusId) != -1;
            }
        }
    }

    public static class ActionExt
    {
        // 技能就绪且能对当前目标释放（Tetora）
        public static unsafe bool IsReadyWithCanCast(uint actionId, FFXIVClientStructs.FFXIV.Client.Game.ActionType actionType)
        {
            var am = FFXIVClientStructs.FFXIV.Client.Game.ActionManager.Instance();
            if (am == null) return false;
            var adjustedId = am->GetAdjustedActionId(actionId);
            if (am->GetActionStatus(actionType, adjustedId) != 0) return false;
            ulong targetId = 0;
            var ts = FFXIVClientStructs.FFXIV.Client.Game.Control.TargetSystem.Instance();
            if (ts != null && ts->GetTargetObject() != null)
                targetId = ts->GetTargetObject()->GetGameObjectId();
            return am->GetActionStatus(actionType, adjustedId, targetId) == 0;
        }

        public static bool IsSpellReady(this uint spellId) => IsReadyWithCanCast(spellId, FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action);
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
        /// 适用于旋转，FF14游戏基顺时针旋转为负。
        /// </summary>
        /// <param name="radian"></param>
        /// <returns></returns>
        public static float Cw2Ccw(this float radian)
        {
            return -radian;
        }

        /// <summary>
        /// 适用于旋转，FF14游戏基顺时针旋转为负。
        /// 与Cw2CCw完全相同，为了代码可读性便于区分。
        /// </summary>
        /// <param name="radian"></param>
        /// <returns></returns>
        public static float Ccw2Cw(this float radian)
        {
            return -radian;
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
            var radian = MathF.PI - MathF.Atan2(newPoint.X - center.X, newPoint.Z - center.Z);
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
            return point with { Z = 2 * centerZ - point.Z };
        }

        /// <summary>
        /// 将输入点中心对称
        /// </summary>
        /// <param name="point">输入点</param>
        /// <param name="center">中心点</param>
        /// <returns></returns>
        public static Vector3 PointCenterSymmetry(this Vector3 point, Vector3 center)
        {
            return point.RotatePoint(center, float.Pi);
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
        /// 获得两点之间距离
        /// </summary>
        /// <param name="point"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public static float DistanceTo(this Vector3 point, Vector3 target)
        {
            Vector2 v2 = new(point.X - target.X, point.Z - target.Z);
            return v2.Length();
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
        /// 从第三人称视角出发观察某目标是否在另一目标的右侧。
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

        /// <summary>
        /// 获取给定数的指定位数
        /// </summary>
        /// <param name="val">给定数值</param>
        /// <param name="x">对应位数，个位为1</param>
        /// <returns></returns>
        public static int GetDecimalDigit(this int val, int x)
        {
            string valStr = val.ToString();
            int length = valStr.Length;

            if (x < 1 || x > length)
            {
                return -1;
            }

            char digitChar = valStr[length - x]; // 从右往左取第x位
            return int.Parse(digitChar.ToString());
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
        /// <param name="fourPeople">是否为四人迷宫</param>
        /// <param name="accessory"></param>
        /// <returns></returns>
        public static string GetPlayerJobByIndex(this ScriptAccessory accessory, int idx, bool fourPeople = false)
        {
            var str = idx switch
            {
                0 => "MT",
                1 => fourPeople ? "H1" : "ST",
                2 => fourPeople ? "D1" : "H1",
                3 => fourPeople ? "D2" : "H2",
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

    }

    #region 绘图函数
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
                case ulong sid:
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
                case ulong tid:
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
        => accessory.DrawGuidance(accessory.Data.Me, targetObj, delay, destroy, name, rotation, scale, isSafe);

        // {
        //     return targetObj switch
        //     {
        //         uint uintTarget => accessory.DrawGuidance(accessory.Data.Me, uintTarget, delay, destroy, name, rotation, scale),
        //         Vector3 vectorTarget => accessory.DrawGuidance(accessory.Data.Me, vectorTarget, delay, destroy, name, rotation, scale),
        //         _ => throw new ArgumentException("targetObj 的类型必须是 uint 或 Vector3")
        //     };
        // }

        /// <summary>
        /// 返回扇形左右刀
        /// </summary>
        /// <param name="accessory"></param>
        /// <param name="ownerId">起始目标id，通常为boss</param>
        /// <param name="delay">延时delay ms出现</param>
        /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
        /// <param name="name">绘图名称</param>
        /// <param name="isLeftCleave">是左刀</param>
        /// <param name="radian">扇形角度</param>
        /// <param name="scale">扇形尺寸</param>
        /// <param name="byTime">动画效果随时间填充</param>
        /// <returns></returns>
        public static DrawPropertiesEdit DrawLeftRightCleave(this ScriptAccessory accessory, uint ownerId, bool isLeftCleave, int delay, int destroy, string name, float radian = float.Pi, float scale = 60f, bool byTime = false)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Scale = new Vector2(scale);
            dp.Radian = radian;
            dp.Rotation = isLeftCleave ? float.Pi / 2 : -float.Pi / 2;
            dp.Owner = ownerId;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = delay;
            dp.DestoryAt = destroy;
            dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
            return dp;
        }

        /// <summary>
        /// 返回扇形前后刀
        /// </summary>
        /// <param name="accessory"></param>
        /// <param name="ownerId">起始目标id，通常为boss</param>
        /// <param name="delay">延时delay ms出现</param>
        /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
        /// <param name="name">绘图名称</param>
        /// <param name="isFrontCleave">是前刀</param>
        /// <param name="radian">扇形角度</param>
        /// <param name="scale">扇形尺寸</param>
        /// <param name="byTime">动画效果随时间填充</param>
        /// <returns></returns>
        public static DrawPropertiesEdit DrawFrontBackCleave(this ScriptAccessory accessory, uint ownerId, bool isFrontCleave, int delay, int destroy, string name, float radian = float.Pi, float scale = 60f, bool byTime = false)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Scale = new Vector2(scale);
            dp.Radian = radian;
            dp.Rotation = isFrontCleave ? 0 : -float.Pi;
            dp.Owner = ownerId;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = delay;
            dp.DestoryAt = destroy;
            dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
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
        public static DrawPropertiesEdit DrawTargetNearFarOrder(this ScriptAccessory accessory, ulong ownerId, uint orderIdx,
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
        /// 返回距离某坐标位置最近/最远的dp
        /// </summary>
        /// <param name="accessory"></param>
        /// <param name="position">特定坐标点</param>
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
        public static DrawPropertiesEdit DrawPositionNearFarOrder(this ScriptAccessory accessory, Vector3 position, uint orderIdx,
            bool isNear, float width, float length, int delay, int destroy, string name, bool lengthByDistance = false, bool byTime = false)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Scale = new Vector2(width, length);
            dp.Position = position;
            dp.Delay = delay;
            dp.DestoryAt = destroy;
            dp.TargetResolvePattern =
                isNear ? PositionResolvePatternEnum.PlayerNearestOrder : PositionResolvePatternEnum.PlayerFarestOrder;
            dp.TargetOrderIndex = orderIdx;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.ScaleMode |= lengthByDistance ? ScaleMode.YByDistance : ScaleMode.None;
            dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
            return dp;
        }

        /// <summary>
        /// 返回ownerId施法目标的dp
        /// </summary>
        /// <param name="accessory"></param>
        /// <param name="ownerId">起始目标id，通常为boss</param>
        /// <param name="width">绘图宽度</param>
        /// <param name="length">绘图长度</param>
        /// <param name="delay">延时delay ms出现</param>
        /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
        /// <param name="byTime">动画效果随时间填充</param>
        /// <param name="lengthByDistance">长度是否随距离改变</param>
        /// <param name="name">绘图名称</param>
        /// <returns></returns>
        public static DrawPropertiesEdit DrawOwnersTarget(this ScriptAccessory accessory, uint ownerId, float width, float length, int delay,
            int destroy, string name, bool lengthByDistance = false, bool byTime = false)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Scale = new Vector2(width, length);
            dp.Owner = ownerId;
            dp.Delay = delay;
            dp.DestoryAt = destroy;
            dp.TargetResolvePattern = PositionResolvePatternEnum.OwnerTarget;
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
        /// 返回owner与target的dp，可修改 dp.Owner, dp.TargetObject, dp.Scale
        /// </summary>
        /// <param name="ownerId">起始目标id，通常为自己</param>
        /// <param name="targetId">目标单位id</param>
        /// <param name="rotation">绘图旋转角度</param>
        /// <param name="width">绘图宽度</param>
        /// <param name="length">绘图长度</param>
        /// <param name="delay">延时delay ms出现</param>
        /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
        /// <param name="name">绘图名称</param>
        /// <param name="lengthByDistance">长度是否随距离改变</param>
        /// <param name="byTime">动画效果随时间填充</param>
        /// <param name="accessory"></param>
        /// <returns></returns>
        public static DrawPropertiesEdit DrawTarget2Target(this ScriptAccessory accessory, uint ownerId, uint targetId, float width, float length, int delay, int destroy, string name, float rotation = 0, bool lengthByDistance = false, bool byTime = false)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Scale = new Vector2(width, length);
            dp.Rotation = rotation;
            dp.Owner = ownerId;
            dp.TargetObject = targetId;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = delay;
            dp.DestoryAt = destroy;
            dp.ScaleMode |= lengthByDistance ? ScaleMode.YByDistance : ScaleMode.None;
            dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
            return dp;
        }

        /// <summary>
        /// 返回画向某目标的扇形绘图
        /// </summary>
        /// <param name="sourceId">起始目标id，通常为自己</param>
        /// <param name="targetId">目标单位id</param>
        /// <param name="radian">扇形角度</param>
        /// <param name="scale">扇形尺寸</param>
        /// <param name="delay">延时delay ms出现</param>
        /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
        /// <param name="name">绘图名称</param>
        /// <param name="color">绘图颜色</param>
        /// <param name="rotation">旋转角度</param>
        /// <param name="lengthByDistance">长度是否随距离改变</param>
        /// <param name="byTime">动画效果随时间填充</param>
        /// <param name="accessory"></param>
        /// <returns></returns>
        public static DrawPropertiesEdit DrawFanToTarget(this ScriptAccessory accessory, uint sourceId, uint targetId, float radian, float scale, int delay, int destroy, string name, Vector4 color, float rotation = 0, bool lengthByDistance = false, bool byTime = false)
        {
            var dp = accessory.DrawTarget2Target(sourceId, targetId, scale, scale, delay, destroy, name, rotation, lengthByDistance, byTime);
            dp.Radian = radian;
            dp.Color = color;
            return dp;
        }

        /// <summary>
        /// 返回owner与target之间的连线dp，使用Line绘制
        /// </summary>
        /// <param name="accessory"></param>
        /// <param name="ownerId">起始目标id，通常为自己</param>
        /// <param name="targetId">目标单位id</param>
        /// <param name="scale">线条宽度</param>
        /// <param name="delay">延时delay ms出现</param>
        /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
        /// <param name="name">绘图名称</param>
        /// <returns></returns>
        public static DrawPropertiesEdit DrawConnectionBetweenTargets(this ScriptAccessory accessory, uint ownerId,
            uint targetId, int delay, int destroy, string name, float scale = 1f)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Scale = new Vector2(scale);
            dp.Owner = ownerId;
            dp.TargetObject = targetId;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = delay;
            dp.DestoryAt = destroy;
            dp.ScaleMode |= ScaleMode.YByDistance;
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
        public static DrawPropertiesEdit DrawCircle(this ScriptAccessory accessory, ulong ownerId, float scale, int delay, int destroy, string name, bool byTime = false)
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
        /// 返回环形dp，跟随owner，可修改 dp.Owner, dp.Scale
        /// </summary>
        /// <param name="ownerId">起始目标id，通常为自己或Boss</param>
        /// <param name="delay">延时delay ms出现</param>
        /// <param name="scale">外环实心尺寸</param>
        /// <param name="innerScale">内环空心尺寸</param>
        /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
        /// <param name="name">绘图名称</param>
        /// <param name="byTime">动画效果随时间填充</param>
        /// <param name="accessory"></param>
        /// <returns></returns>
        public static DrawPropertiesEdit DrawDonut(this ScriptAccessory accessory, uint ownerId, float scale, float innerScale, int delay, int destroy, string name, bool byTime = false)
        {
            var dp = accessory.DrawFan(ownerId, float.Pi * 2, 0, scale, innerScale, delay, destroy, name, byTime);
            return dp;
        }

        /// <summary>
        /// 返回静态dp，通常用于指引固定位置。可修改 dp.Position, dp.Rotation, dp.Scale
        /// </summary>
        /// <param name="ownerObj">绘图起始，可输入uint或Vector3</param>
        /// <param name="targetObj">绘图目标，可输入uint或Vector3，为0则无目标</param>
        /// <param name="radian">图形角度</param>
        /// <param name="rotation">旋转角度，以北为0度顺时针</param>
        /// <param name="width">绘图宽度</param>
        /// <param name="length">绘图长度</param>
        /// <param name="color">是Vector4则选用该颜色</param>
        /// <param name="delay">延时delay ms出现</param>
        /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
        /// <param name="name">绘图名称</param>
        /// <param name="accessory"></param>
        /// <returns></returns>
        public static DrawPropertiesEdit DrawStatic(this ScriptAccessory accessory, object ownerObj, object targetObj,
            float radian, float rotation, float width, float length, object color, int delay, int destroy, string name)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Scale = new Vector2(width, length);
            switch (ownerObj)
            {
                case uint sid:
                    dp.Owner = sid;
                    break;
                case ulong sid:
                    dp.Owner = sid;
                    break;
                case Vector3 spos:
                    dp.Position = spos;
                    break;
            }
            switch (targetObj)
            {
                case uint tid:
                    if (tid != 0) dp.TargetObject = tid;
                    break;
                case ulong tid:
                    if (tid != 0) dp.TargetObject = tid;
                    break;
                case Vector3 tpos:
                    dp.TargetPosition = tpos;
                    break;
            }
            dp.Radian = radian;
            dp.Rotation = rotation.Logic2Game();
            switch (color)
            {
                case Vector4 clr:
                    dp.Color = clr;
                    break;
                default:
                    dp.Color = accessory.Data.DefaultDangerColor;
                    break;
            }
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
        public static DrawPropertiesEdit DrawStaticCircle(this ScriptAccessory accessory, Vector3 center, Vector4 color,
            int delay, int destroy, string name, float scale = 1.5f)
            => accessory.DrawStatic(center, (uint)0, 0, 0, scale, scale, color, delay, destroy, name);
        // {
        //     var dp = accessory.DrawStatic(center, (uint)0, 0, 0, scale, scale, color, delay, destroy, name);
        //     return dp;
        // }

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
        public static DrawPropertiesEdit DrawStaticDonut(this ScriptAccessory accessory, Vector3 center, Vector4 color,
            int delay, int destroy, string name, float scale, float innerscale = 0)
            => accessory.DrawStatic(center, (uint)0,
            float.Pi * 2, 0, scale, scale, color, delay, destroy, name);

        // {
        //     var dp = accessory.DrawStatic(center, (uint)0, float.Pi * 2, 0, scale, scale, color, delay, destroy, name);
        //     dp.InnerScale = innerscale != 0f ? new Vector2(innerscale) : new Vector2(scale - 0.05f);
        //     return dp;
        // }

        /// <summary>
        /// 返回矩形
        /// </summary>
        /// <param name="ownerId">起始目标id，通常为自己或Boss</param>
        /// <param name="width">矩形宽度</param>
        /// <param name="length">矩形长度</param>
        /// <param name="delay">延时delay ms出现</param>
        /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
        /// <param name="name">绘图名称</param>
        /// <param name="byTime">动画效果随时间填充</param>
        /// <param name="accessory"></param>
        /// <returns></returns>
        public static DrawPropertiesEdit DrawRect(this ScriptAccessory accessory, uint ownerId, float width, float length, int delay, int destroy, string name, bool byTime = false)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Scale = new Vector2(width, length);
            dp.Owner = ownerId;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = delay;
            dp.DestoryAt = destroy;
            dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
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

        /// <summary>
        /// 返回击退
        /// </summary>
        /// <param name="accessory"></param>
        /// <param name="target">击退源，可输入uint或Vector3</param>
        /// <param name="width">击退绘图宽度</param>
        /// <param name="length">击退绘图长度/距离</param>
        /// <param name="delay">延时delay ms出现</param>
        /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
        /// <param name="name">绘图名称</param>
        /// <param name="ownerId">起始目标ID，通常为自己或其他玩家</param>
        /// <param name="byTime">动画效果随时间填充</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static DrawPropertiesEdit DrawKnockBack(this ScriptAccessory accessory, uint ownerId, object target, float length, int delay, int destroy, string name, float width = 1.5f, bool byTime = false)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Scale = new Vector2(width, length);
            dp.Owner = ownerId;
            switch (target)
            {
                // 根据传入的 tid 类型来决定是使用 TargetObject 还是 TargetPosition
                case uint tid:
                    dp.TargetObject = tid; // 如果 tid 是 uint 类型
                    break;
                case ulong tid:
                    dp.TargetObject = tid; // 如果 tid 是 ulong 类型
                    break;
                case Vector3 tpos:
                    dp.TargetPosition = tpos; // 如果 tid 是 Vector3 类型
                    break;
                default:
                    throw new ArgumentException("DrawKnockBack的目标类型输入错误");
            }
            dp.Rotation = float.Pi;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = delay;
            dp.DestoryAt = destroy;
            dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
            return dp;
        }

        public static DrawPropertiesEdit DrawKnockBack(this ScriptAccessory accessory, object target, float length,
            int delay, int destroy, string name, float width = 1.5f, bool byTime = false)
            => accessory.DrawKnockBack(accessory.Data.Me, target, length, delay, destroy, name, width, byTime);
        // {
        //     return target switch
        //     {
        //         uint uintTarget => accessory.DrawKnockBack(accessory.Data.Me, uintTarget, length, delay, destroy, name, width, byTime),
        //         Vector3 vectorTarget => accessory.DrawKnockBack(accessory.Data.Me, vectorTarget, length, delay, destroy, name, width, byTime),
        //         _ => throw new ArgumentException("target 的类型必须是 uint 或 Vector3")
        //     };
        // }

        /// <summary>
        /// 返回背对
        /// </summary>
        /// <param name="accessory"></param>
        /// <param name="target">背对源，可输入uint或Vector3</param>
        /// <param name="delay">延时delay ms出现</param>
        /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
        /// <param name="name">绘图名称</param>
        /// <param name="ownerId">起始目标ID，通常为自己或其他玩家</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static DrawPropertiesEdit DrawSightAvoid(this ScriptAccessory accessory, uint ownerId, object target, int delay, int destroy, string name)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Owner = ownerId;
            switch (target)
            {
                // 根据传入的 tid 类型来决定是使用 TargetObject 还是 TargetPosition
                case uint tid:
                    dp.TargetObject = tid; // 如果 tid 是 uint 类型
                    break;
                case ulong tid:
                    dp.TargetObject = tid; // 如果 tid 是 ulong 类型
                    break;
                case Vector3 tpos:
                    dp.TargetPosition = tpos; // 如果 tid 是 Vector3 类型
                    break;
                default:
                    throw new ArgumentException("DrawSightAvoid的目标类型输入错误");
            }
            dp.Delay = delay;
            dp.DestoryAt = destroy;
            return dp;
        }

        public static DrawPropertiesEdit DrawSightAvoid(this ScriptAccessory accessory, object target, int delay,
            int destroy, string name)
            => accessory.DrawSightAvoid(accessory.Data.Me, target, delay, destroy, name);
        // {
        //     return target switch
        //     {
        //         uint uintTarget => accessory.DrawSightAvoid(accessory.Data.Me, uintTarget, delay, destroy, name),
        //         Vector3 vectorTarget => accessory.DrawSightAvoid(accessory.Data.Me, vectorTarget, delay, destroy, name),
        //         _ => throw new ArgumentException("target 的类型必须是 uint 或 Vector3")
        //     };
        // }

        /// <summary>
        /// 返回多方向延伸指引
        /// </summary>
        /// <param name="accessory"></param>
        /// <param name="owner">分散源</param>
        /// <param name="extendDirs">分散角度</param>
        /// <param name="myDirIdx">玩家对应角度idx</param>
        /// <param name="width">指引箭头宽度</param>
        /// <param name="length">指引箭头长度</param>
        /// <param name="delay">绘图出现延时</param>
        /// <param name="destroy">绘图消失时间</param>
        /// <param name="name">绘图名称</param>
        /// <param name="colorPlayer">玩家对应箭头指引颜色</param>
        /// <param name="colorNormal">其他玩家对应箭头指引颜色</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static List<DrawPropertiesEdit> DrawExtendDirection(this ScriptAccessory accessory, object owner,
            List<float> extendDirs, int myDirIdx, float width, float length, int delay, int destroy, string name,
            Vector4 colorPlayer, Vector4 colorNormal)
        {
            List<DrawPropertiesEdit> dpList = [];
            switch (owner)
            {
                case uint sid:
                    for (var i = 0; i < extendDirs.Count; i++)
                    {
                        var dp = accessory.DrawGuidance(owner, sid, delay, destroy, $"{name}{i}", extendDirs[i], width);
                        dp.Color = i == myDirIdx ? colorPlayer : colorNormal;
                        dpList.Add(dp);
                    }
                    break;
                case Vector3 spos:
                    for (var i = 0; i < extendDirs.Count; i++)
                    {
                        var dp = accessory.DrawGuidance(spos, spos.ExtendPoint(extendDirs[i], length), delay, destroy,
                            $"{name}{i}", 0, width);
                        dp.Color = i == myDirIdx ? colorPlayer : colorNormal;
                        dpList.Add(dp);
                    }
                    break;
                default:
                    throw new ArgumentException("DrawExtendDirection的目标类型输入错误");
            }

            return dpList;
        }

        /// <summary>
        /// 返回多地点指路指引列表
        /// </summary>
        /// <param name="accessory"></param>
        /// <param name="positions">地点位置</param>
        /// <param name="delay">绘图出现延时</param>
        /// <param name="destroy">绘图消失时间</param>
        /// <param name="name">绘图名称</param>
        /// <param name="colorPosPlayer">对应位置标记行动颜色</param>
        /// <param name="colorPosNormal">对应位置标记准备颜色</param>
        /// <param name="colorGo">指路出发箭头颜色</param>
        /// <param name="colorPrepare">指路准备箭头颜色</param>
        /// <returns>dpList中的三个List：位置标记，玩家指路箭头，地点至下个地点的指路箭头</returns>
        public static List<List<DrawPropertiesEdit>> DrawMultiGuidance(this ScriptAccessory accessory,
            List<Vector3> positions, List<int> delay, List<int> destroy, string name,
            Vector4 colorGo, Vector4 colorPrepare, Vector4 colorPosNormal, Vector4 colorPosPlayer)
        {
            List<List<DrawPropertiesEdit>> dpList = [[], [], []];
            for (var i = 0; i < positions.Count; i++)
            {
                var dpPos = accessory.DrawStaticCircle(positions[i], colorPosPlayer, delay[i], destroy[i], $"{name}pos{i}");
                dpList[0].Add(dpPos);
                var dpGuide = accessory.DrawGuidance(positions[i], colorGo, delay[i], destroy[i], $"{name}guide{i}");
                dpList[1].Add(dpGuide);
                if (i == positions.Count - 1) break;
                var dpPrep = accessory.DrawGuidance(positions[i], positions[i + 1], delay[i], destroy[i], $"{name}prep{i}");
                dpList[2].Add(dpPrep);
            }
            return dpList;
        }

        public static void DebugMsg(this ScriptAccessory accessory, string str, bool debugMode = false, bool debugChat = false)
        {
            if (!debugMode)
                return;
            accessory.Log.Debug($"/e [DEBUG] {str}");

            if (!debugChat)
                return;
            accessory.Method.SendChat($"/e [DEBUG] {str}");
        }

        /// <summary>
        /// 将List内信息转换为字符串。
        /// </summary>
        /// <param name="accessory"></param>
        /// <param name="myList"></param>
        /// <param name="isJob">是职业，在转为字符串前调用转职业函数</param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static string BuildListStr<T>(this ScriptAccessory accessory, List<T> myList, bool isJob = false)
        {
            return string.Join(", ", myList.Select(item =>
            {
                if (isJob && item != null && item is int i)
                    return accessory.GetPlayerJobByIndex(i);
                return item?.ToString() ?? "";
            }));
        }
    }

    #endregion 绘图函数

    #region 特殊函数
    public static class SpecialFunction
    {
        public static void SetTargetable(this ScriptAccessory sa, IGameObject? obj, bool targetable)
        {
            if (obj == null || !obj.IsValid())
            {
                sa.Log.Error($"传入的IGameObject不合法。");
                return;
            }
            unsafe
            {
                GameObject* charaStruct = (GameObject*)obj.Address;
                if (targetable)
                {
                    if (obj.IsDead || obj.IsTargetable) return;
                    charaStruct->TargetableStatus |= ObjectTargetableFlags.IsTargetable;
                }
                else
                {
                    if (!obj.IsTargetable) return;
                    charaStruct->TargetableStatus &= ~ObjectTargetableFlags.IsTargetable;
                }
            }
            sa.Log.Debug($"SetTargetable {targetable} => {obj.Name} {obj}");
        }

        public static void SetRotation(this ScriptAccessory sa, IGameObject? obj, float rotation)
        {
            if (obj == null || !obj.IsValid())
            {
                sa.Log.Error($"传入的IGameObject不合法。");
                return;
            }
            unsafe
            {
                GameObject* charaStruct = (GameObject*)obj.Address;
                charaStruct->SetRotation(rotation);
            }
            sa.Log.Debug($"SetRotation => {obj.Name.TextValue} | {obj} => {rotation}");
        }
    }

    #endregion 特殊函数
    
    #endregion
    
}


