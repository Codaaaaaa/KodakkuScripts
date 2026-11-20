using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.STD.Helper;
using KodakkuAssist.Data;
using KodakkuAssist.Extensions;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.Draw.Manager;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.GameEvent.Struct;
using KodakkuAssist.Module.GameEvent.Types;
using KodakkuAssist.Module.GameOperate;
using KodakkuAssist.Script;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CicerosKodakkuAssist.DancingMadUltimate.LPDU;

[ScriptType(name: Name, territorys: [1363], guid: "52a1cc3b-57fb-4893-9608-c0cbaaf210bd",
    version: Version, author: "Cicero 灵视 & Coda & Usami & RyougiMio & Errer", note: Note, updateInfo: UpdateInfo)]
public class Dancing_Mad_Ultimate
{
    #region 元信息

    private const string Name = "Dancing Mad (Ultimate) DMU UMAD - LPDU";
    private const string Version = "0.0.0.1";

    private const string Note =
        $"""
        {Version}
        由五个脚本整合而成：
            Cicero 灵视 - P1~P5 的绘制，P2 遗弃末世全程与 P3 深层痛楚(一运)究极冲击波的指路。
            Coda - P1 众神之像 1~3 的指路，P1~P3 指路的 LPDU 攻略适配。
            Usami - P3 的绘制与指路。
            RyougiMio - P4 的指路。
            Errer - P5 的绘制与指路。

        小队顺序须为 MT ST H1 H2 D1(近战) D2(近战) D3(远程) D4(远程)。
        小队排序测试：聊天框输入 /e kdmutest。

        P1~P3 的指路按 LPDU 攻略：
            P1 支援西 / DPS 东、箭头顺时针大方框、神像3 全固定点。
            P2 遗弃末世 Buddies（AAABBBBA）。
            P3 二运黑洞按地震 debuff 分配接线，不需要标点；冰封四角与踩塔 T+近战在左、H+远程在右（以凯夫卡为北）。
        P2、P3 可在用户设置里改成 PF 打法。

        带 * 的方法与设置项是非原生功能（屏蔽技能特效、Boss 透明化、屏蔽无效目标、移除暴雷连线），
        默认开启，不想要的在用户设置「* 特殊功能」分组里逐项关掉。
        """;

    private const string UpdateInfo =
        $"""
        {Version}
        1. 整合 Cicero / Coda / Usami / RyougiMio / Errer 五个脚本。
        2. P3 与 Usami 重复的范围绘制只保留 Usami 的。
        """;

    #endregion

    #region 用户设置

    // 分隔项：没有实际作用，只把灰色说明当分组标题。插件没有"只显示文字"的设置类型，
    // bool 一定会画勾选框，勾选框旁边显示属性名，所以属性名用不同长度的纯下划线，几乎看不见
    // （至少两个下划线：单个 _ 会盖掉类里所有 out _ 弃元）
    [UserSetting("—————————— 通用 ——————————")]
    public bool __ { get; set; } = true;
    [UserSetting("通用 启用文字提示")]
    public bool enablePrompts { get; set; } = true;
    [UserSetting("通用 启用原生TTS")]
    public bool enableVanillaTts { get; set; } = true;
    [UserSetting("通用 启用Daily Routines TTS\n(需要安装并启用Daily Routines插件!)")]
    public bool enableDailyRoutinesTts { get; set; } = false;
    [UserSetting("通用 机制方向的颜色")]
    public ScriptColor colourOfDirectionIndicators { get; set; } = new() { V4 = new Vector4(1,1,0, 1) }; // Yellow by default.
    [UserSetting("通用 高度危险攻击的颜色")]
    public ScriptColor colourOfExtremelyDangerousAttacks { get; set; } = new() { V4 = new Vector4(1,0,0,1) }; // Red by default.
    [UserSetting("通用 小队排序测试文本发送到的频道")]
    public PartyTestChannels partyTestChannel { get; set; } = PartyTestChannels.默语频道_仅自己可见;

    [UserSetting("\n\n\n\n\n\n—————————— * 特殊功能（非原生） ——————————")]
    public bool ___ { get; set; } = true;
    [UserSetting("*通用 屏蔽呼啦啦爆炎、扩大大冰封和劈啪啪暴雷的技能特效\n（开启后这三种范围改用原生绘制）")]
    public bool blockFireIceThunderVfx { get; set; } = true;
    [UserSetting("*通用 呼啦啦爆炎、扩大大冰封和劈啪啪暴雷\n技能特效屏蔽的方法")]
    public AnimationBlockadeModes animationBlockadeMode { get; set; } = AnimationBlockadeModes.传统方法_改变透明施法者的可见性;
    [UserSetting("*P3 防火墙 屏蔽无效目标")]
    public bool p3FirewallBlockInvalidTargets { get; set; } = true;
    [UserSetting("*P3A 移除暴雷连线")]
    public bool p3aRemoveThunderTether { get; set; } = true;
    [UserSetting("*P3A 真空波读条期间卡奥斯透明")]
    public bool p3aChaosTransparentDuringVacuumWave { get; set; } = true;
    [UserSetting("*P3B 黑洞、冰封期间双Boss透明")]
    public bool p3bBossesTransparentDuringBlackHole { get; set; } = true;
    [UserSetting("*P3B 凯夫卡上桌透明化")]
    public bool p3bKefkaTransparent { get; set; } = true;
    [UserSetting("*P3B 卡奥斯非技能期间透明化")]
    public bool p3bChaosTransparent { get; set; } = true;
    [UserSetting("*P3B 艾克斯迪司非技能期间透明化")]
    public bool p3bExdeathTransparent { get; set; } = true;

    [UserSetting("\n\n\n\n\n\n—————————— 调试 ——————————")]
    public bool ____ { get; set; } = true;
    internal static bool DebugEnabled;
    [UserSetting("调试 输出调试信息\n（Dalamud日志，部分会发到默语频道）")]
    public bool enableDebugLogging { get => DebugEnabled; set => DebugEnabled = value; }

    [UserSetting("\n\n\n\n\n\n—————————— P1 ——————————")]
    public bool _____ { get; set; } = true;
    [UserSetting("P1 连环环陷阱 状态消失前的绘制持续时间(秒)")]
    public double doubleTroubleTrapDuration { get; set; } = 5;

    [UserSetting("\n\n\n\n\n\n—————————— P2 ——————————")]
    public bool ______ { get; set; } = true;
    [UserSetting("P2 按 PF 打法：遗弃末世第 1 轮分摊跟搭档进塔\n（搭档扇形进扇形塔，搭档圆圈进圆圈塔）")]
    public bool followPfStrats { get; set; } = false;
    [UserSetting("P2 遗弃末世 指路\n在偶数轮为咏唱危机·波动(扇形)的位置绘制辅助线")]
    public bool phase2sub2_spellwaveAuxiliaryLines { get; set; } = true;
    [UserSetting("P2 遗弃末世 绘制的视觉类型")]
    public Phase2Sub2_DrawingTypes phase2sub2_drawingType { get; set; } = Phase2Sub2_DrawingTypes.默认;
    [UserSetting("P2 遗弃末世 咏唱危机\n塔判定前的绘制持续时间(秒,最多7)")]
    public double phase2sub2_drawingDuration { get; set; } = 5;
    [UserSetting("P2 遗弃末世 咏唱危机 仅绘制咏唱危机·波动(扇形)")]
    public bool phase2sub2_spellwaveOnly { get; set; } = false;
    [UserSetting("P2 遗弃末世 为塔的位置绘制辅助线")]
    public bool phase2sub2_towerAuxiliaryLines { get; set; } = false;
    [UserSetting("P2 遗弃末世 辅助线的颜色")]
    public ScriptColor phase2sub2_colourOfAuxiliaryLines { get; set; } = new() { V4 = new Vector4(0,1,1, 1) }; // Blue by default.

    [UserSetting("\n\n\n\n\n\n—————————— P3 ——————————")]
    public bool _______ { get; set; } = true;
    [UserSetting("P3 按 PF 打法：冰封四角与踩塔按 G1/G2 分左右（以凯夫卡为北）\n" +
        "勾选 = PF：\n" +
        "  G1（MT H1 D1 D3）在左：MT+H1 西北、D1+D3 西南，踩左塔\n" +
        "  G2（ST H2 D2 D4）在右：ST+H2 东北、D2+D4 东南，踩右塔\n" +
        "不勾 = LPDU：\n" +
        "  T+近战在左：MT+ST 西北、D1+D2 西南，踩左塔\n" +
        "  H+远程在右：H1+H2 东北、D3+D4 东南，踩右塔")]
    public bool p3FollowPfStrats { get; set; } = false;
    [UserSetting("P3 卡奥斯诅咒敕令绘图颜色")]
    public ScriptColor CursedEdictColor { get; set; } = new ScriptColor { V4 = new Vector4(1.0f, 0.643f, 0f, 1.0f) };
    [UserSetting("P3 卡奥斯诅咒敕令绘图深度（Imgui请调2以内）")]
    public static float CursedEdictWhiteVal { get; set; } = 4;
    [UserSetting("P3B1 二运黑洞 指示线颜色")]
    public ScriptColor BhConnLineColor { get; set; } = new ScriptColor { V4 = new Vector4(1.0f, 1.0f, 0f, 1.0f) };
    [UserSetting("P3B2 冰封 以凯夫卡所在方位为北")]
    public static bool BlizKefkaIsNorth { get; set; } = true;

    [UserSetting("\n\n\n\n\n\n—————————— P4 ——————————")]
    public bool ________ { get; set; } = true;
    [UserSetting("P4 诅咒之嚎 类型指示 不绘制自身的诅咒之嚎")]
    public bool phase4_disableCursedShriekOnMe { get; set; } = false;
    [UserSetting("P4 混沌之炎与混沌之水 仅绘制自身的引导范围")]
    public bool phase4_disableStrayBaitOnOthers { get; set; } = true;
    [UserSetting("P4 真假预测播报\n(Off关闭 Echo默语 Party小队频道)")]
    public P4PlanMessageMode PlanMessageMode { get; set; } = P4PlanMessageMode.Off;

    [UserSetting("\n\n\n\n\n\n—————————— P5 ——————————")]
    public bool _________ { get; set; } = true;
    [UserSetting("P5 地火 按 LPDU 打法\n" +
        "勾选 = LPDU：全员一起走中间，最后再分散\n" +
        "不勾 = 按职能分组：MT ST D1 D2 走中间，H1 左、H2 右、D3 上、D4 下")]
    public bool p5FireFollowLpdu { get; set; } = false;
    [UserSetting("P5 地火 步进圈不透明度（%，0~100）")]
    public int p5FireOpacity { get; set; } = 30;

    #endregion

    #region 调试

    // 打开后所有指路按全队 8 人绘制（每人按自己的 buff / 点名 / 职能单独解算），
    // 并显示 userControl: Debugging 的内部方法、输出 Usami 部分的调试信息
    // 必须是 const：userControl: Debugging 是特性参数，只吃编译期常量
    public const bool Debugging = true;

    /// <summary>指路箭头起点 Owner：正常取 PartyList[idx]，越界回退自己</summary>
    private static uint GuidanceOwner(ScriptAccessory sa, int idx)
        => idx >= 0 && idx < sa.Data.PartyList.Count ? sa.Data.PartyList[idx] : sa.Data.Me;

    #endregion

    #region 测试项

    [ScriptMethod(name: "———————— 《测试项》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void 测试项分割线(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "测试项：打印当前阶段", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void 打印当前阶段(Event ev, ScriptAccessory sa)
    {
        sa.DebugMsg($"{_udmP3Param.当前阶段}");
    }

    [ScriptMethod(name: "测试项：展示优先级表格", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void 展示优先级表格(Event ev, ScriptAccessory sa)
    {

        var str = $"优先级字典：\n";
        foreach (var pair in _pd.Priorities)
        {
            str += $"Key {pair.Key} Value {pair.Value}\n";
        }
        sa.DebugMsg($"\n{str}");
        sa.DebugMsg($"\n{_pd.ShowPriorities()}");
    }

    [ScriptMethod(name: "测试项：双Boss赋值", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void 双Boss赋值(Event ev, ScriptAccessory sa)
    {
        _udmP3Param.ObjectId_卡奥斯 = sa.GetByDataId(19508u).First().GameObjectId;
        _udmP3Param.ObjectId_艾克斯迪司 = sa.GetByDataId(19509u).First().GameObjectId;
    }

    #endregion 测试项

    #region 初始化

    public void Init(ScriptAccessory sa)
    {
        Init_Cicero(sa);
        Init_Coda(sa);
        Init_Usami(sa);
        Init_RyougiMio(sa);
        Init_Errer(sa);
    }

    #endregion

    #region 通用（Cicero 灵视）

    #region Variables_And_Semaphores

    private volatile int majorPhase=1;
    private volatile int phase=1;

    /*

    Major Phase 1:

        Phases are separated by Graven Image.
        阶段由众神之像分隔。

    Major Phase 2:

        Phase 1 - (~,Forsaken 遗弃末世)
        Phase 2 - [Forsaken 遗弃末世,Light of Judgment 制裁之光)
        Phase 3 - [Light of Judgment 制裁之光,~)

    Major Phase 3:

        Phase 1 - (~,Bowels of Agony 深层痛楚)
        Phase 2 - [Bowels of Agony 深层痛楚,Earthquake 地震)
        Phase 3 - [Earthquake 地震,~)

    Major Phase 4:

        No phase separation.
        无阶段分隔。

    Major Phase 5:

        I'm retired! For now :D
        我退休了!

    */

    private volatile bool fakeFlagrantFire=false;
    private System.Threading.AutoResetEvent flagrantFireObfuscationSemaphore=new System.Threading.AutoResetEvent(false);
    private HashSet<ulong> partyMembersWithFlagrantFire=new HashSet<ulong>();
    private volatile bool stackFlagrantFireIcon=false;
    private System.Threading.AutoResetEvent flagrantFireIconSemaphore=new System.Threading.AutoResetEvent(false);

    // ----- Major Phase 1 -----

    private volatile int phase1sub2_mysteryMagicCounter=0;
    private System.Threading.AutoResetEvent phase1sub2_waveCannonSemaphore=new System.Threading.AutoResetEvent(false);

    private volatile bool phase1sub3_isFirstHalf=true;

    // ----- End Of Major Phase 1 -----

    // ----- Major Phase 2 -----

    private Phase2Sub2_IconTypes[] phase2sub2_iconType=Enumerable.Range(0,8).Select(i=>Phase2Sub2_IconTypes.UNKNOWN).ToArray();
    private volatile int phase2sub2_iconUpdateCounter=0;
    private System.Threading.AutoResetEvent[] phase2sub2_roundSemaphore=Enumerable.Range(0,7).Select(i=>new System.Threading.AutoResetEvent(false)).ToArray();
    private volatile int phase2sub2_pathOfLightCounter=0; // Its read-write lock is PHASE2_SUB2_PATH_OF_LIGHT_COUNTER_LOCK.
    private List<int> phase2sub2_groupA=new List<int>();
    private List<int> phase2sub2_groupB=new List<int>();
    private int[] phase2sub2_discretizedTowerGap=Enumerable.Range(0,9).Select(i=>-1).ToArray();
    private volatile int phase2sub2_towerCounter=0;
    private volatile int phase2sub2_discretizedPositionOfLastTower=-1;
    private System.Threading.AutoResetEvent phase2sub2_discretizedTowerGap1Semaphore=new System.Threading.AutoResetEvent(false);
    private System.Threading.AutoResetEvent phase2sub2_discretizedTowerGap2Semaphore=new System.Threading.AutoResetEvent(false);
    private volatile bool isFuturesEnd=false;
    private volatile int phase2sub2_allThingsEndingCounter=0; // Its read-write lock is PHASE2_SUB2_ALL_THINGS_ENDING_COUNTER_LOCK.
    private System.Threading.AutoResetEvent phase2sub2_allThingsEndingSemaphore=new System.Threading.AutoResetEvent(false);

    private volatile int phase2sub3_trineCounter=0; // Its read-write lock is PHASE2_SUB3_TRINE_COUNTER_LOCK.

    // ----- End Of Major Phase 2 -----

    // ----- Major Phase 3 -----

    private Vector3 phase3sub2_fireCrystalPosition=ARENA_CENTER;
    private System.Threading.AutoResetEvent phase3sub2_fireCrystalSemaphore=new System.Threading.AutoResetEvent(false);
    private volatile int phase3sub2_infernoCounter=2;
    private Vector3 phase3sub2_waterCrystalPosition=ARENA_CENTER;
    private System.Threading.AutoResetEvent phase3sub2_waterCrystalSemaphore=new System.Threading.AutoResetEvent(false);
    private volatile int phase3sub2_tsunamiCounter=2;
    private volatile int phase3sub2_ultimaBlasterCounter=0; // Its read-write lock is PHASE3_SUB2_ULTIMA_BLASTER_COUNTER_LOCK.
    private volatile int phase3sub2_firstUltimaBlasterPosition=-1;
    private volatile bool phase3sub2_baitClockwise=false;

    private ConcurrentDictionary<ulong,int> phase3sub3_blackHoleDrawingCounter=new ConcurrentDictionary<ulong,int>();
    private volatile int phase3sub3_nothingnessCounter=0;

    // ----- End Of Major Phase 3 -----

    // ----- Major Phase 4 -----

    private volatile bool phase4_fakeNeoExdeathAction=false;
    private volatile bool phase4_fakeChaosAction=false;
    private bool[] phase4_isFakeStrayFlames=Enumerable.Range(0,8).Select(i=>false).ToArray();
    private bool[] phase4_isFakeStraySpray=Enumerable.Range(0,8).Select(i=>false).ToArray();
    private volatile int phase4_statusWithDurationCounter=0;
    private bool[] phase4_isBeyondDeath=Enumerable.Range(0,8).Select(i=>false).ToArray();
    private volatile int phase4_statusWithoutDurationCounter=0;
    private bool[] phase4_isWhiteWound=Enumerable.Range(0,8).Select(i=>false).ToArray();

    // ----- End Of Major Phase 4 -----

    #endregion

    #region Constants_And_Locks

    private const int COMMON_INTERVAL=2500;
    private const int MAXIMUM_DURATION=7200000;
    private const int VISIBILITY_RECOVERY_DELAY=125;
    private const double COMMON_DEVIATION=1;

    private const int PARTY_TEST_DURATION=20000;

    private static readonly Vector3 ARENA_CENTER=new Vector3(100,0,100);
    private const int ARENA_RADIUS=20;

    private static readonly Vector3 PHASE2_SUB2_RAW_TOWER_POSITION=new Vector3(100,0,92);
    private const int PHASE2_SUB2_TOWER_RADIUS=4;
    private readonly object PHASE2_SUB2_PATH_OF_LIGHT_COUNTER_LOCK=new object();
    private readonly object PHASE2_SUB2_ALL_THINGS_ENDING_COUNTER_LOCK=new object();
    private static ImmutableList<bool> phase2sub2_isGroupARound=[false,
                                                        true,
                                                        true,
                                                        true,
                                                        false,
                                                        false,
                                                        false,
                                                        false,
                                                        true];

    private readonly object PHASE2_SUB3_TRINE_COUNTER_LOCK=new object();

    private readonly object PHASE3_SUB2_ULTIMA_BLASTER_COUNTER_LOCK=new object();

    #endregion

    #region Enumerations_And_Classes

    public enum PartyTestChannels {

        不发送到任何频道,
        默语频道_仅自己可见,
        小队频道_所有队员可见

    }

    public enum AnimationBlockadeModes {

        传统方法_改变透明施法者的可见性,
        引擎方法_根据handle移除指定特效

    }

    public enum Phase2Sub2_IconTypes {

        FAN,
        SPREAD,
        STACK,
        UNKNOWN

    }

    public enum Phase2Sub2_DrawingTypes {

        默认,
        全VFX,
        作者推荐_圆形ImGui_其余VFX,
        全ImGui

    }

    #endregion

    private void Init_Cicero(ScriptAccessory accessory) {

        accessory.Method.RemoveDraw(".*");

        VariableAndSemaphoreInitialization();

    }

    private void VariableAndSemaphoreInitialization() {

        majorPhase=1;
        phase=1;

        fakeFlagrantFire=false;
        flagrantFireObfuscationSemaphore.Reset();
        partyMembersWithFlagrantFire.Clear();
        stackFlagrantFireIcon=false;
        flagrantFireIconSemaphore.Reset();

        // ----- Major Phase 1 -----

        phase1sub2_mysteryMagicCounter=0;
        phase1sub2_waveCannonSemaphore.Reset();

        phase1sub3_isFirstHalf=true;

        // ----- End Of Major Phase 1 -----

        // ----- Major Phase 2 -----

        for(int i=0;i<phase2sub2_iconType.Length;++i)phase2sub2_iconType[i]=Phase2Sub2_IconTypes.UNKNOWN;
        phase2sub2_iconUpdateCounter=0;
        for(int i=0;i<phase2sub2_roundSemaphore.Length;++i)phase2sub2_roundSemaphore[i].Reset();
        phase2sub2_pathOfLightCounter=0;
        phase2sub2_groupA.Clear();
        phase2sub2_groupB.Clear();
        for(int i=0;i<phase2sub2_discretizedTowerGap.Length;++i)phase2sub2_discretizedTowerGap[i]=-1;
        phase2sub2_towerCounter=0;
        phase2sub2_discretizedPositionOfLastTower=-1;
        phase2sub2_discretizedTowerGap1Semaphore.Reset();
        phase2sub2_discretizedTowerGap2Semaphore.Reset();
        isFuturesEnd=false;
        phase2sub2_allThingsEndingCounter=0;
        phase2sub2_allThingsEndingSemaphore.Reset();

        phase2sub3_trineCounter=0;

        lock(_trine锁) {

            _trines.Clear();
            _trine落地=0;
            _trine安全半场=null;

        }

        // ----- End Of Major Phase 2 -----

        // ----- Major Phase 3 -----

        phase3sub2_fireCrystalPosition=ARENA_CENTER;
        phase3sub2_fireCrystalSemaphore.Reset();
        phase3sub2_infernoCounter=2;
        phase3sub2_waterCrystalPosition=ARENA_CENTER;
        phase3sub2_waterCrystalSemaphore.Reset();
        phase3sub2_tsunamiCounter=2;
        phase3sub2_ultimaBlasterCounter=0;
        phase3sub2_firstUltimaBlasterPosition=-1;
        phase3sub2_baitClockwise=false;

        phase3sub3_blackHoleDrawingCounter.Clear();
        phase3sub3_nothingnessCounter=0;

        // ----- End Of Major Phase 3 -----

        // ----- Major Phase 4 -----

        phase4_fakeNeoExdeathAction=false;
        phase4_fakeChaosAction=false;
        for(int i=0;i<phase4_isFakeStrayFlames.Length;++i)phase4_isFakeStrayFlames[i]=false;
        for(int i=0;i<phase4_isFakeStraySpray.Length;++i)phase4_isFakeStraySpray[i]=false;
        phase4_statusWithDurationCounter=0;
        for(int i=0;i<phase4_isBeyondDeath.Length;++i)phase4_isBeyondDeath[i]=false;
        phase4_statusWithoutDurationCounter=0;
        for(int i=0;i<phase4_isWhiteWound.Length;++i)phase4_isWhiteWound[i]=false;

        // ----- End Of Major Phase 4 -----

    }

    #region Global

    [ScriptMethod(name: "———————— 《通用》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"])]
    public void 通用_分割线(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name:"通用 小队排序测试",
        eventType:EventTypeEnum.Chat,
        eventCondition:["Type:Echo"])]

    public void 通用_小队排序测试(Event @event,ScriptAccessory accessory) {

        string processedText=(@event["Message"]).Trim().ToLower();

        if(!string.Equals(processedText,"kdmutest")) {

            return;

        }

        string text="请确认如下小队排序是否正确:\n";
        string log=string.Empty;
        KodakkuAssist.Data.IGameObject? sourceObject=null;
        string[] roles=["MT",
                        "ST",
                        "H1",
                        "H2",
                        "D1",
                        "D2",
                        "D3",
                        "D4"];

        for(int i=0;i<roles.Length;++i) {

            sourceObject=accessory.Data.Objects.SearchById(accessory.Data.PartyList[i]);

            if(sourceObject==null||!sourceObject.IsValid()) {

                continue;

            }

            else {

                if(sourceObject is not ICharacter sourceICharacter) {

                    continue;

                }

                else {

                    text+=$"{roles[i]}:{sourceICharacter.Name}。";

                    if(i<roles.Length-1) {

                        text+="\n";

                    }

                    log+=$"{roles[i]}: {accessory.Data.PartyList[i]}\n";

                }

            }

        }

        switch(partyTestChannel) {

            case PartyTestChannels.不发送到任何频道: {

                break;

            }

            case PartyTestChannels.默语频道_仅自己可见: {

                accessory.Method.SendChat($"/e \n{text}");

                break;

            }

            case PartyTestChannels.小队频道_所有队员可见: {

                accessory.Method.SendChat($"/p \n{text}");

                break;

            }

            default: {

                break;

            }

        }

        if(enablePrompts) {

            accessory.Method.TextInfo(text,PARTY_TEST_DURATION);

        }

        accessory.tts(text,enableVanillaTts,enableDailyRoutinesTts);

        if(enableDebugLogging) {

            accessory.Log.Debug($"\n----- Party Test Text -----\n{text}\n\n----- Party Test Log -----\n{log}");

        }

    }

    [ScriptMethod(name:"通用 呼啦啦爆炎 (数据收集1)",
        eventType:EventTypeEnum.TargetIcon,
        eventCondition:["Id:regex:^(02A1|02A2|02A3|02A4|02A5|02A6)$"],
        userControl:false)]

    public void 通用_呼啦啦爆炎_数据收集1(Event @event,ScriptAccessory accessory) {

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        var targetObject=accessory.Data.Objects.SearchById(targetId);

        if(targetObject==null||!targetObject.IsValid()) {

            return;

        }

        else {

            if(targetObject.DataId!=19504) {

                return;

            }

        }

        string log=$"@event[\"Id\"]={@event["Id"]}, ";

        if(string.Equals(@event["Id"],"02A1")) {

            fakeFlagrantFire=true;
            flagrantFireObfuscationSemaphore.Set();

            log+="fake Flagrant Fire";

        }

        if(string.Equals(@event["Id"],"02A2")) {

            fakeFlagrantFire=false;
            flagrantFireObfuscationSemaphore.Set();

            log+="real Flagrant Fire";

        }

        if(string.Equals(@event["Id"],"02A3")) {

            log+="fake Blizzard Blowout";

        }

        if(string.Equals(@event["Id"],"02A4")) {

            log+="real Blizzard Blowout";

        }

        if(string.Equals(@event["Id"],"02A5")) {

            log+="fake Thrumming Thunder";

        }

        if(string.Equals(@event["Id"],"02A6")) {

            log+="real Thrumming Thunder";

        }

        if(enableDebugLogging) {

            accessory.Log.Debug(log);

        }

    }

    [ScriptMethod(name:"通用 呼啦啦爆炎 (数据收集2)",
        eventType:EventTypeEnum.TargetIcon,
        eventCondition:["Id:regex:^(0080|007F)$"],
        userControl:false)]

    public void 通用_呼啦啦爆炎_数据收集2(Event @event,ScriptAccessory accessory) {

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        lock(partyMembersWithFlagrantFire) {

            partyMembersWithFlagrantFire.Add(targetId);

            if(partyMembersWithFlagrantFire.Count==2) {

                if(string.Equals(@event["Id"],"0080")) {

                    stackFlagrantFireIcon=true;
                    flagrantFireIconSemaphore.Set();

                    if(enableDebugLogging) {

                        accessory.Log.Debug($"""
                                             stackFlagrantFireIcon={stackFlagrantFireIcon}
                                             partyMembersWithFlagrantFire:{string.Join(",",partyMembersWithFlagrantFire)}
                                             """);

                    }

                }

            }

            if(partyMembersWithFlagrantFire.Count==1) {

                if(string.Equals(@event["Id"],"007F")) {

                    stackFlagrantFireIcon=false;
                    flagrantFireIconSemaphore.Set();

                    if(enableDebugLogging) {

                        accessory.Log.Debug($"""
                                             stackFlagrantFireIcon={stackFlagrantFireIcon}
                                             partyMembersWithFlagrantFire:{string.Join(",",partyMembersWithFlagrantFire)}
                                             """);

                    }

                }

            }

        }

    }

    [ScriptMethod(name:"通用 呼啦啦爆炎 (范围)",
        eventType:EventTypeEnum.TargetIcon,
        eventCondition:["Id:regex:^(0080|007F)$"],
        suppress:COMMON_INTERVAL)]

    public void 通用_呼啦啦爆炎_范围(Event @event,ScriptAccessory accessory) {

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        bool signalled=flagrantFireObfuscationSemaphore.WaitOne(COMMON_INTERVAL);

        if(!signalled) {

            return;

        }

        signalled=flagrantFireIconSemaphore.WaitOne(COMMON_INTERVAL);

        if(!signalled) {

            return;

        }

        if(stackFlagrantFireIcon) {

            if(!fakeFlagrantFire) {

                foreach(ulong i in partyMembersWithFlagrantFire) {

                    currentProperties=accessory.Data.GetDefaultDrawProperties();

                    currentProperties.Scale=new(6);
                    currentProperties.Owner=i;
                    currentProperties.Color=colourOfDirectionIndicators.V4.WithW(1);
                    currentProperties.DestoryAt=5875;

                    accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Circle,currentProperties);

                }

            }

            else {

                for(int i=0;i<8;++i) {

                    currentProperties=accessory.Data.GetDefaultDrawProperties();

                    currentProperties.Scale=new(5);
                    currentProperties.Owner=accessory.Data.PartyList[i];
                    currentProperties.Color=accessory.Data.DefaultDangerColor;
                    currentProperties.DestoryAt=5875;

                    accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Circle,currentProperties);

                }

            }

        }

        else {

            if(!fakeFlagrantFire) {

                for(int i=0;i<8;++i) {

                    currentProperties=accessory.Data.GetDefaultDrawProperties();

                    currentProperties.Scale=new(5);
                    currentProperties.Owner=accessory.Data.PartyList[i];
                    currentProperties.Color=accessory.Data.DefaultDangerColor;
                    currentProperties.DestoryAt=5875;

                    accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Circle,currentProperties);

                }

            }

            else {

                currentProperties=accessory.Data.GetDefaultDrawProperties();

                currentProperties.Scale=new(6);
                currentProperties.Owner=accessory.Data.Me;
                currentProperties.Color=colourOfDirectionIndicators.V4.WithW(1);
                currentProperties.DestoryAt=5875;

                accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Circle,currentProperties);

            }

        }

    }

    [ScriptMethod(name:"*通用 呼啦啦爆炎 (技能特效屏蔽,引擎方法)",
        eventType:EventTypeEnum.VfxEvent,
        eventCondition:["Id:regex:^(128|127)$"])]

    public void 通用_呼啦啦爆炎_技能特效屏蔽_引擎方法(Event @event,ScriptAccessory accessory) {

        if(!blockFireIceThunderVfx) {

            return;

        }

        if(animationBlockadeMode!=AnimationBlockadeModes.引擎方法_根据handle移除指定特效) {

            return;

        }

        if(!string.Equals(@event["Type"],"LockOn")) {

            return;

        }

        if(!convertHandleIdToDecimal(@event["Handle"],out var handleId)) {

            return;

        }

        accessory.Method.VfxMethod.RemoveVfx(handleId,VfxType.LockOn);

    }

    [ScriptMethod(name:"通用 呼啦啦爆炎 (数据清除)",
        eventType:EventTypeEnum.ActionEffect,
        eventCondition:["ActionId:regex:^(47778|47779)$"],
        suppress:COMMON_INTERVAL,
        userControl:false)]

    public void 通用_呼啦啦爆炎_数据清除(Event @event,ScriptAccessory accessory) {

        fakeFlagrantFire=false;
        flagrantFireObfuscationSemaphore.Reset();
        partyMembersWithFlagrantFire.Clear();
        stackFlagrantFireIcon=false;
        flagrantFireIconSemaphore.Reset();

    }

    [ScriptMethod(name:"通用 扩大大冰封 (范围)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:regex:^(47768|47774)$"])]

    public void 通用_扩大大冰封_范围(Event @event,ScriptAccessory accessory) {

        if(!convertObjectIdToDecimal(@event["SourceId"],out var sourceId)) {

            return;

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        currentProperties.Scale=new(40);
        currentProperties.Radian=float.Pi/2;
        currentProperties.Owner=sourceId;
        currentProperties.DestoryAt=5000;
        currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(1);

        if(!blockFireIceThunderVfx) {

            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Fan,currentProperties);

        }

        else {

            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Fan,currentProperties);

        }

    }

    [ScriptMethod(name:"*通用 扩大大冰封 (技能特效屏蔽,引擎方法)",
        eventType:EventTypeEnum.VfxEvent,
        eventCondition:["Id:737"])]

    public void 通用_扩大大冰封_技能特效屏蔽_引擎方法(Event @event,ScriptAccessory accessory) {

        if(!blockFireIceThunderVfx) {

            return;

        }

        if(animationBlockadeMode!=AnimationBlockadeModes.引擎方法_根据handle移除指定特效) {

            return;

        }

        if(!string.Equals(@event["Type"],"Omen")) {

            return;

        }

        if(!convertHandleIdToDecimal(@event["Handle"],out var handleId)) {

            return;

        }

        accessory.Method.VfxMethod.RemoveVfx(handleId,VfxType.Omen);

    }

    [ScriptMethod(name:"*通用 扩大大冰封 (技能特效屏蔽,传统方法)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:regex:^(47768|47771)$"])]

    public void 通用_扩大大冰封_技能特效屏蔽_传统方法(Event @event,ScriptAccessory accessory) {

        if(!blockFireIceThunderVfx) {

            return;

        }

        if(animationBlockadeMode!=AnimationBlockadeModes.传统方法_改变透明施法者的可见性) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["SourceId"],out var sourceId)) {

            return;

        }

        var sourceObject=accessory.Data.Objects.SearchById(sourceId);

        if(sourceObject==null||!sourceObject.IsValid()) {

            return;

        }

        else {

            adjustVisibility(accessory,sourceObject,false,VISIBILITY_RECOVERY_DELAY+5000);

        }

    }

    [ScriptMethod(name:"通用 劈啪啪暴雷 (范围)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:regex:^(47775|47777)$"])]

    public void 通用_劈啪啪暴雷_范围(Event @event,ScriptAccessory accessory) {

        if(!convertObjectIdToDecimal(@event["SourceId"],out var sourceId)) {

            return;

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        currentProperties.Scale=new(10,40);
        currentProperties.Owner=sourceId;
        currentProperties.DestoryAt=5000;
        currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(1);

        if(!blockFireIceThunderVfx) {

            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Rect,currentProperties);

        }

        else {

            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Rect,currentProperties);

        }

    }

    [ScriptMethod(name:"*通用 劈啪啪暴雷 (技能特效屏蔽,引擎方法)",
        eventType:EventTypeEnum.VfxEvent,
        eventCondition:["Id:171"])]

    public void 通用_劈啪啪暴雷_技能特效屏蔽_引擎方法(Event @event,ScriptAccessory accessory) {

        if(!blockFireIceThunderVfx) {

            return;

        }

        if(animationBlockadeMode!=AnimationBlockadeModes.引擎方法_根据handle移除指定特效) {

            return;

        }

        if(!string.Equals(@event["Type"],"Omen")) {

            return;

        }

        if(!convertHandleIdToDecimal(@event["Handle"],out var handleId)) {

            return;

        }

        accessory.Method.VfxMethod.RemoveVfx(handleId,VfxType.Omen);

    }

    [ScriptMethod(name:"*通用 劈啪啪暴雷 (技能特效屏蔽,传统方法)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:regex:^(47775|47776)$"])]

    public void 通用_劈啪啪暴雷_技能特效屏蔽_传统方法(Event @event,ScriptAccessory accessory) {

        if(!blockFireIceThunderVfx) {

            return;

        }

        if(animationBlockadeMode!=AnimationBlockadeModes.传统方法_改变透明施法者的可见性) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["SourceId"],out var sourceId)) {

            return;

        }

        var sourceObject=accessory.Data.Objects.SearchById(sourceId);

        if(sourceObject==null||!sourceObject.IsValid()) {

            return;

        }

        else {

            adjustVisibility(accessory,sourceObject,false,VISIBILITY_RECOVERY_DELAY+5000);

        }

    }

    #endregion

    #endregion

    #region P1 绘制（Cicero 灵视）

    #region Major_Phase_1

    [ScriptMethod(name: "———————— 《P1》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"])]
    public void P1_分割线(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name:"P1 恶狠狠毁荡 (范围)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:50179"])]

    public void P1_恶狠狠毁荡_范围(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=1) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["SourceId"],out var sourceId)) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        currentProperties.Scale=new(100);
        currentProperties.Radian=float.Pi/3*2;
        currentProperties.Owner=sourceId;
        currentProperties.TargetObject=targetId;
        currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(1);
        currentProperties.DestoryAt=5000;

        accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Fan,currentProperties);

        currentProperties=accessory.Data.GetDefaultDrawProperties();

        currentProperties.Scale=new(100);
        currentProperties.Radian=float.Pi/3*2;
        currentProperties.Owner=sourceId;
        currentProperties.TargetResolvePattern=PositionResolvePatternEnum.OwnerEnmityOrder;
        currentProperties.TargetOrderIndex=2;
        currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(1);
        currentProperties.Delay=5000;
        currentProperties.DestoryAt=3375;

        accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Fan,currentProperties);

    }

    [ScriptMethod(name:"P1 超驱动 (范围)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:50722"])]

    public void P1_超驱动_范围(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=1) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["SourceId"],out var sourceId)) {

            return;

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        currentProperties.Scale=new(5);
        currentProperties.Owner=sourceId;
        currentProperties.CentreResolvePattern=PositionResolvePatternEnum.OwnerEnmityOrder;
        currentProperties.CentreOrderIndex=1;
        currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(1);
        currentProperties.Delay=5000;
        currentProperties.DestoryAt=7375;

        accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,currentProperties);

    }

    [ScriptMethod(name:"P1 连环环陷阱 (范围)",
        eventType:EventTypeEnum.StatusAdd,
        eventCondition:["StatusID:5078"])]

    public void P1_连环环陷阱_范围(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=1) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        int durationMilliseconds=0;

        try {

            durationMilliseconds=JsonConvert.DeserializeObject<int>(@event["DurationMilliseconds"]);

        } catch(Exception e) {

            accessory.Log.Error("DurationMilliseconds deserialization failed.");

            return;

        }

        if(durationMilliseconds<=0||durationMilliseconds>MAXIMUM_DURATION) {

            return;

        }

        int standardDuration=((int)(doubleTroubleTrapDuration*1000));
        int delay=0;
        int duration=0;

        if(durationMilliseconds<=standardDuration) {

            delay=0;
            duration=durationMilliseconds;

        }

        else {

            delay=durationMilliseconds-standardDuration;
            duration=standardDuration;

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        currentProperties.Name=$"P1_连环环陷阱_范围_{targetId}";
        currentProperties.Scale=new(6);
        currentProperties.Owner=targetId;
        currentProperties.Color=accessory.Data.DefaultDangerColor;
        currentProperties.Delay=delay;
        currentProperties.DestoryAt=duration;

        accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Circle,currentProperties);

    }

    [ScriptMethod(name:"P1 连环环陷阱 (击退指示)",
        eventType:EventTypeEnum.StatusAdd,
        eventCondition:["StatusID:5078"])]

    public void P1_连环环陷阱_击退指示(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=1) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        if(!Debugging&&targetId==accessory.Data.Me) {
            return;
        }
        int durationMilliseconds=0;
        try {
            durationMilliseconds=JsonConvert.DeserializeObject<int>(@event["DurationMilliseconds"]);
        } catch(Exception e) {
            accessory.Log.Error("DurationMilliseconds deserialization failed.");
            return;
        }
        if(durationMilliseconds<=0||durationMilliseconds>MAXIMUM_DURATION) {
            return;
        }
        int standardDuration=((int)(doubleTroubleTrapDuration*1000));
        int delay=0;
        int duration=0;

        if(durationMilliseconds<=standardDuration) {

            delay=0;
            duration=durationMilliseconds;

        }

        else {

            delay=durationMilliseconds-standardDuration;
            duration=standardDuration;

        }

        int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
        for(int partyIndex=0;partyIndex<accessory.Data.PartyList.Count;++partyIndex) {
            if(!Debugging&&partyIndex!=myIndex) {
                continue;
            }
            if(accessory.Data.PartyList[partyIndex]==targetId) {
                continue;
            }
            var currentProperties=accessory.Data.GetDefaultDrawProperties();
            currentProperties.Name=$"P1_连环环陷阱_击退指示_{targetId}_{partyIndex}";
            currentProperties.Scale=new(2,14);
            currentProperties.Owner=GuidanceOwner(accessory,partyIndex);
            currentProperties.TargetObject=targetId;
            currentProperties.Rotation=float.Pi;
            currentProperties.Color=colourOfDirectionIndicators.V4.WithW(1);
            currentProperties.FadeDistance=6;
            currentProperties.FadeCentreObject=targetId;
            currentProperties.Delay=delay;
            currentProperties.DestoryAt=duration;
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Arrow,currentProperties);
        }
    }

    [ScriptMethod(name:"P1 连环环陷阱 (范围与击退指示清除)",
        eventType:EventTypeEnum.StatusRemove,
        eventCondition:["StatusID:5078"],
        userControl:false)]

    public void P1_连环环陷阱_范围与击退指示清除(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=1) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        accessory.Method.RemoveDraw($"P1_连环环陷阱_范围_{targetId}");
        accessory.Method.RemoveDraw($"P1_连环环陷阱_击退指示_{targetId}(_\\d)?");

    }

    [ScriptMethod(name:"P1 众神之像 (阶段控制)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:48370"],
        userControl:false)]

    public void P1_众神之像_阶段控制(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=1) {

            return;

        }

        Interlocked.Increment(ref phase);

        if(enableDebugLogging) {

            accessory.Log.Debug($"majorPhase={majorPhase}\nphase={phase}");

        }

    }

    [ScriptMethod(name:"P1 众神之像1 波动弹 (击退指示)",
        eventType:EventTypeEnum.Tether,
        eventCondition:["Id:002D"])]

    public void P1_众神之像1_波动弹_击退指示(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=1) {

            return;

        }

        if(phase!=2) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        if(!Debugging&&targetId!=accessory.Data.Me) {
            return;
        }
        var currentProperties=accessory.Data.GetDefaultDrawProperties();
        currentProperties.Name=$"P1_众神之像1_波动弹_击退指示_{targetId}";
        currentProperties.Scale=new(2,13);
        currentProperties.Owner=targetId;
        currentProperties.FixRotation=true;
        currentProperties.Rotation=0;
        currentProperties.Color=colourOfDirectionIndicators.V4.WithW(1);
        currentProperties.DestoryAt=5125;

        accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Arrow,currentProperties);

    }

    [ScriptMethod(name:"P1 众神之像1 玄乎乎魔法 (数据收集)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:47764"],
        userControl:false)]

    public void P1_众神之像1_玄乎乎魔法_数据收集(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=1) {

            return;

        }

        if(phase!=2) {

            return;

        }

        Interlocked.Increment(ref phase1sub2_mysteryMagicCounter);

        if(phase1sub2_mysteryMagicCounter==1) {

            phase1sub2_waveCannonSemaphore.Set();

        }

    }

    [ScriptMethod(name:"P1 众神之像1 波动炮 (范围)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:47764"])]

    public void P1_众神之像1_波动炮_范围(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=1) {

            return;

        }

        if(phase!=2) {

            return;

        }

        bool signalled=phase1sub2_waveCannonSemaphore.WaitOne(COMMON_INTERVAL);

        if(!signalled) {

            return;

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        for(int i=0;i<8;++i) {

            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(6,100);
            currentProperties.Position=new Vector3(100,0,65);
            currentProperties.TargetObject=accessory.Data.PartyList[i];
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.Delay=5625;
            currentProperties.DestoryAt=4375;

            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Rect,currentProperties);

        }

    }

    [ScriptMethod(name:"P1 众神之像1 爆炸 (精确范围)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:47786"])]

    public void P1_众神之像1_爆炸_精确范围(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=1) {

            return;

        }

        if(phase!=2) {

            return;

        }

        Vector3 effectPosition=ARENA_CENTER;

        try {

            effectPosition=JsonConvert.DeserializeObject<Vector3>(@event["EffectPosition"]);

        } catch(Exception e) {

            accessory.Log.Error("EffectPosition deserialization failed.");

            return;

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        currentProperties.Scale=new(4);
        currentProperties.Position=effectPosition;
        currentProperties.Color=colourOfDirectionIndicators.V4.WithW(1);
        currentProperties.DestoryAt=3000;

        accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Circle,currentProperties);

    }

    [ScriptMethod(name:"P1 众神之像2 重力弹 (范围)",
        eventType:EventTypeEnum.Tether,
        eventCondition:["Id:002D"])]

    public void P1_众神之像2_重力弹_范围(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=1) {

            return;

        }

        if(phase!=3) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        Vector3 sourcePosition=ARENA_CENTER;

        try {

            sourcePosition=JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);

        } catch(Exception e) {

            accessory.Log.Error("SourcePosition deserialization failed.");

            return;

        }

        if(Vector3.Distance(sourcePosition,new Vector3(102.5f,22.5f,27))>COMMON_DEVIATION) {

            return;

        }

        int delay=-1;
        int duration=-1;

        if(phase1sub3_isFirstHalf) {

            delay=0;
            duration=6500;

        }

        else {

            delay=3875;
            duration=4625;

        }

        if(delay<0||duration<0) {

            return;

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        currentProperties.Scale=new(5);
        currentProperties.Owner=targetId;
        currentProperties.Color=colourOfDirectionIndicators.V4.WithW(1);
        currentProperties.Delay=delay;
        currentProperties.DestoryAt=duration;

        accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Circle,currentProperties);

    }

    [ScriptMethod(name:"P1 众神之像2 岩石弹 (范围)",
        eventType:EventTypeEnum.Tether,
        eventCondition:["Id:002D"])]

    public void P1_众神之像2_岩石弹_范围(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=1) {

            return;

        }

        if(phase!=3) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        Vector3 sourcePosition=ARENA_CENTER;

        try {

            sourcePosition=JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);

        } catch(Exception e) {

            accessory.Log.Error("SourcePosition deserialization failed.");

            return;

        }

        if(Vector3.Distance(sourcePosition,new Vector3(126,7,41.5f))>COMMON_DEVIATION) {

            return;

        }

        int delay=-1;

        if(phase1sub3_isFirstHalf) {

            delay=6500;

        }

        else {

            delay=8500;

        }

        if(delay<0) {

            return;

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        currentProperties.Scale=new(5);
        currentProperties.Owner=targetId;
        currentProperties.Color=accessory.Data.DefaultDangerColor;
        currentProperties.Delay=delay;
        currentProperties.DestoryAt=4000;

        accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Circle,currentProperties);

    }

    [ScriptMethod(name:"P1 众神之像2 恶狠狠毁荡 (阶段控制)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:50179"],
        userControl:false)]

    public void P1_众神之像2_恶狠狠毁荡_阶段控制(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=1) {

            return;

        }

        if(phase!=3) {

            return;

        }

        phase1sub3_isFirstHalf=false;

    }

    [ScriptMethod(name:"P1 众神之像2 重力波与扑杀的神气 (范围)",
        eventType:EventTypeEnum.ObjectEffect,
        eventCondition:["Id1:64"])]

    public void P1_众神之像2_重力波与扑杀的神气_范围(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=1) {

            return;

        }

        if(phase!=3) {

            return;

        }

        if(!string.Equals(@event["Id2"],"128")) {

            return;

        }

        Vector3 sourcePosition=ARENA_CENTER;

        try {

            sourcePosition=JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);

        } catch(Exception e) {

            accessory.Log.Error("SourcePosition deserialization failed.");

            return;

        }

        Vector3 orientation=ARENA_CENTER;

        if(Vector3.Distance(sourcePosition,new Vector3(92,15,27))<COMMON_DEVIATION) {

            orientation=new Vector3(ARENA_CENTER.X-10,ARENA_CENTER.Y,ARENA_CENTER.Z);

        }

        if(Vector3.Distance(sourcePosition,new Vector3(116,6.5f,43))<COMMON_DEVIATION) {

            orientation=new Vector3(ARENA_CENTER.X+10,ARENA_CENTER.Y,ARENA_CENTER.Z);

        }

        if(Vector3.Distance(orientation,ARENA_CENTER)<COMMON_DEVIATION) {

            return;

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        currentProperties.Scale=new(100);
        currentProperties.Radian=float.Pi;
        currentProperties.Position=ARENA_CENTER;
        currentProperties.TargetPosition=orientation;
        currentProperties.Color=accessory.Data.DefaultDangerColor;
        currentProperties.DestoryAt=5000;

        accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Fan,currentProperties);

    }

    [ScriptMethod(name:"P1 众神之像3 睡魔的神气 (范围)",
        eventType:EventTypeEnum.Tether,
        eventCondition:["Id:002D"])]

    public void P1_众神之像3_睡魔的神气_范围(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=1) {

            return;

        }

        if(phase!=4) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        Vector3 sourcePosition=ARENA_CENTER;

        try {

            sourcePosition=JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);

        } catch(Exception e) {

            accessory.Log.Error("SourcePosition deserialization failed.");

            return;

        }

        if(Vector3.Distance(sourcePosition,new Vector3(107,8.5f,43))<COMMON_DEVIATION) {

            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(5);
            currentProperties.Owner=targetId;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.Delay=3000;
            currentProperties.DestoryAt=6000;

            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Circle,currentProperties);

        }

    }

    [ScriptMethod(name:"P1 众神之像3 懒惰的神气与圣母颂 (面向指示)",
        eventType:EventTypeEnum.ObjectEffect,
        eventCondition:["Id1:64"])]

    public void P1_众神之像3_懒惰的神气与圣母颂_面向指示(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=1) {

            return;

        }

        if(phase!=4) {

            return;

        }

        if(!string.Equals(@event["Id2"],"128")) {

            return;

        }

        Vector3 sourcePosition=ARENA_CENTER;

        try {

            sourcePosition=JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);

        } catch(Exception e) {

            accessory.Log.Error("SourcePosition deserialization failed.");

            return;

        }

        float rotation=-(float.Pi*2);

        if(Vector3.Distance(sourcePosition,new Vector3(105.25f,13.5f,34))<COMMON_DEVIATION) {

            rotation=float.Pi*2;

        }

        if(Vector3.Distance(sourcePosition,new Vector3(95,12.5f,25))<COMMON_DEVIATION) {

            rotation=float.Pi;

        }

        if(rotation<0) {

            return;

        }

        int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
        for(int partyIndex=0;partyIndex<accessory.Data.PartyList.Count;++partyIndex) {
            if(!Debugging&&partyIndex!=myIndex) {
                continue;
            }
            var currentProperties=accessory.Data.GetDefaultDrawProperties();
            currentProperties.Name=$"P1_众神之像3_懒惰的神气与圣母颂_面向指示_{partyIndex}";
            currentProperties.Scale=new(1,3);
            currentProperties.Owner=GuidanceOwner(accessory,partyIndex);
            currentProperties.FixRotation=true;
            currentProperties.Rotation=rotation;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=9875;
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Arrow,currentProperties);
        }
    }

    #endregion

    #endregion

    #region P1 指路（Coda，LPDU 攻略）

    // LPDU P1：https://raidplan.io/plan/_5FyPYATLhNYkq8S ，传送箭头：https://raidplan.io/plan/saC2CjTaXmSApm6y
    // 小队顺序 MT ST H1 H2 D1(M1) D2(M2) D3(R1) D4(R2)；场地标点 A/B/C/D、1/2/3/4 都在离场中 12m

    #region 全局变量与初始化

    // P1 子阶段：1.0 开场，1.1 神像1，1.2 神像2（从神像1 的波动炮开始算），1.3 神像3，2.0 P2
    private double _phase = 1.0;
    private int _p1StatueCount = 0;

    private static readonly Vector3 ArenaCenter = new(100.00f, 0.00f, 100.00f);

    private void Init_Coda(ScriptAccessory sa)
    {
        _phase = 1.0;
        _p1StatueCount = 0;

        lock (_iceFireLock)
        {
            _iceDiagonalIsLeftUpRightDown = null;
            _waveCannonTargets.Clear();
            _action47784Targets.Clear();
            _action47784GuideSent = false;
        }

        lock (_p1危险区)
            _p1危险区.Clear();

        lock (_g2锁)
        {
            _g2轮次 = 0;
            _g2上次连线 = 0;
            Array.Clear(_g2重力弹);
            _g2安全左上右下 = null;
            _g2半场刀安全侧 = 0;
            _g2第一轮已指路 = false;
        }

        ResetStatue3ArrowState();

        sa.Method.RemoveDraw(".*");
    }

    #endregion

    #region 阶段控制

    [ScriptMethod(name: "Set Phase 1", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASP1"], userControl: false)]
    public void SetP1(Event evt, ScriptAccessory sa)
    {
        _phase = 1.1;
        _p1StatueCount = 1;
        ResetStatue3ArrowState();
    }

    [ScriptMethod(name: "Set Phase 2", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASP2"], userControl: false)]
    public void SetP2(Event evt, ScriptAccessory sa) => _phase = 2.0;

    [ScriptMethod(name: "Set Phase 3", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASP3"], userControl: false)]
    public void SetP3(Event evt, ScriptAccessory sa) => _phase = 3.0;

    [ScriptMethod(name: "Show Phase", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:phase"], userControl: false)]
    public void ShowPhase(Event evt, ScriptAccessory sa) => sa.Debug($"当前 Phase={_phase}, P1StatueCount={_p1StatueCount}");

    [ScriptMethod(
        name: "P1_众神之像_指路阶段控制",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:48370"],
        userControl: false)]
    public void P1_众神之像_指路阶段控制(Event evt, ScriptAccessory sa)
    {
        if (_phase >= 2.0)
            return;

        _p1StatueCount++;

        _phase = _p1StatueCount switch
        {
            1 => 1.1,
            2 => 1.2,
            3 => 1.3,
            _ => _phase,
        };

        if (IsPhase(_phase, 1.3))
            ResetStatue3ArrowState();

        sa.Debug($"P1众神之像阶段控制：Count={_p1StatueCount}, Phase={_phase}");
    }

    [ScriptMethod(
        name: "P1结束_切换到P2",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:47804"],
        userControl: false)]
    public void P1结束_切换到P2(Event evt, ScriptAccessory sa)
    {
        if (!IsPhase(_phase, 1.3))
            return;

        _phase = 2.0;

        sa.Debug($"P1结束检测到 47804，切换到 P2。Phase={_phase}");
    }

    #endregion

    #region P1 冰雷危险区（算最近安全点）

    // 真冰 47768/47774：40m 90° 扇形（两个分身各朝一个对角）；真雷 47775/47777：从施法者沿面向 40×10 直线
    // 和 Cicero 部分画危险区用的是同一组 ID；记到读条结束（4.7s）后自动失效
    private readonly record struct P1危险区(bool 是扇形, Vector2 原点, float 朝向, long 结束);

    private readonly List<P1危险区> _p1危险区 = new();

    [ScriptMethod(
        name: "P1_记录冰雷危险区",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47768|47774|47775|47777)$"],
        userControl: false)]
    public void P1_记录冰雷危险区(Event evt, ScriptAccessory sa)
    {
        if (_phase >= 2.0)
            return;

        var src = evt.SourcePosition();
        var zone = new P1危险区(evt.ActionId() is 47768 or 47774, new Vector2(src.X, src.Z), evt.SourceRotation(), Environment.TickCount64 + 5500);

        lock (_p1危险区)
        {
            _p1危险区.RemoveAll(z => z.结束 < Environment.TickCount64);
            _p1危险区.Add(zone);
        }
    }

    private static bool P1在危险区(P1危险区 z, Vector2 p, float margin)
    {
        var d = p - z.原点;
        var dir = new Vector2(MathF.Sin(z.朝向), MathF.Cos(z.朝向));

        if (z.是扇形)
        {
            float len = d.Length();
            if (len < margin)
                return true;
            if (len > 40 + margin)
                return false;

            float angle = MathF.Acos(Math.Clamp(Vector2.Dot(d, dir) / len, -1f, 1f));
            return angle < MathF.PI / 4 + MathF.Asin(Math.Min(1f, margin / len));
        }

        float along = Vector2.Dot(d, dir);
        float side = MathF.Abs(d.X * dir.Y - d.Y * dir.X);
        return along > -margin && along < 40 + margin && side < 5 + margin;
    }

    /// <summary>离 from 最近、场内 18m 以内、不在任何冰扇形/真雷直线里（留 1.5m）的点；没有危险区就原地</summary>
    private Vector3 P1最近安全点(Vector3 from)
    {
        List<P1危险区> zones;
        lock (_p1危险区)
            zones = _p1危险区.Where(z => z.结束 >= Environment.TickCount64).ToList();

        var f = new Vector2(from.X, from.Z);
        Vector2? best = null;
        float bestDistance = float.MaxValue;

        for (float x = -18; x <= 18; x += 0.5f)
        {
            for (float z = -18; z <= 18; z += 0.5f)
            {
                if (x * x + z * z > 18 * 18)
                    continue;

                var p = new Vector2(100 + x, 100 + z);
                if (zones.Any(zone => P1在危险区(zone, p, 1.5f)))
                    continue;

                float distance = Vector2.Distance(p, f);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = p;
                }
            }
        }

        return best is { } b ? new Vector3(b.X, 0, b.Y) : from;
    }

    #endregion

    #region P1 神像1（火冰 + 波动炮）

    private readonly object _iceFireLock = new();

    // 实际危险对角：true = 左上右下（47768 真冰按面向，47771 假冰取反）
    private bool? _iceDiagonalIsLeftUpRightDown = null;

    // 神像1 被 002D 连线的一组：波动弹 47785 往南击退约 13m
    private readonly HashSet<uint> _waveCannonTargets = new();

    private readonly Dictionary<uint, Vector3> _action47784Targets = new();
    private bool _action47784GuideSent = false;

    private const uint RealIceAction = 47768;

    // 波动炮排队：支援在西、DPS 在东，从场中往外 MT ST H1 H2 / M1 M2 R1 R2
    private static readonly float[] 波动炮排队距离 = [2.25f, 6.81f, 11.45f, 16.25f];

    [ScriptMethod(
        name: "P1_冰爆_记录冰真假与方向",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47768|47771)$"],
        userControl: false)]
    public void P1_冰爆_记录冰真假与方向(Event evt, ScriptAccessory sa)
    {
        if (!IsPhase(_phase, 1.1))
            return;

        bool iceIsReal = evt.ActionId() == RealIceAction;
        bool rawDiagonalIsLeftUpRightDown = IsLeftUpRightDownByRotation(evt.SourceRotation());

        lock (_iceFireLock)
            _iceDiagonalIsLeftUpRightDown = iceIsReal ? rawDiagonalIsLeftUpRightDown : !rawDiagonalIsLeftUpRightDown;
    }

    [ScriptMethod(
        name: "P1_记录002D连线",
        eventType: EventTypeEnum.Tether,
        eventCondition: ["Id:002D"],
        userControl: false)]
    public void P1_记录002D连线(Event evt, ScriptAccessory sa)
    {
        // 连线方向不固定：在小队里的那一端是玩家，另一端是神像
        bool sourceIsPlayer = sa.Data.PartyList.Contains(evt.SourceId());
        uint playerId = sourceIsPlayer ? evt.SourceId() : evt.TargetId();
        Vector3 statuePos = sourceIsPlayer ? evt.TargetPosition() : evt.SourcePosition();

        if (playerId == 0)
            return;

        if (IsPhase(_phase, 1.1))
        {
            lock (_iceFireLock)
                _waveCannonTargets.Add(playerId);
            return;
        }

        if (IsPhase(_phase, 1.2))
            P1_神像2_记录连线(sa, playerId, statuePos);
    }

    // 47764 玄乎乎魔法（神像1 第一次）：4.7s 读条；+2.6s 波动弹击退，+5.7s 呼啦啦爆炎，+10s 波动炮
    // 火：0080 分摊 / 007F 分散，凯夫卡头上 02A1 = 假火要反着做（读 Cicero 部分记下的 stackFlagrantFireIcon / fakeFlagrantFire）
    // 支援西、DPS 东，各去本半场的安全象限；被连线的人先站目标点正北 13m 等击退，落地再走到目标点
    [ScriptMethod(
        name: "P1_神像1_火冰指路",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:47764"])]
    public void P1_神像1_火冰指路(Event evt, ScriptAccessory sa)
    {
        if (!IsPhase(_phase, 1.1))
            return;

        Task.Run(async () =>
        {
            await Task.Delay(100);

            bool? dangerLeftUpRightDown;
            HashSet<uint> knocked;

            lock (_iceFireLock)
            {
                dangerLeftUpRightDown = _iceDiagonalIsLeftUpRightDown;
                knocked = new HashSet<uint>(_waveCannonTargets);
            }

            if (dangerLeftUpRightDown == null)
            {
                sa.Debug("神像1 火冰指路失败：没有记录到冰方向。");
                return;
            }

            bool 分摊 = stackFlagrantFireIcon != fakeFlagrantFire;
            var party = sa.Data.PartyList;
            int myIdx = sa.MyIndex();

            for (int i = 0; i < party.Count && i < 8; i++)
            {
                if (!Debugging && i != myIdx)
                    continue;

                var final = P1神像1站位(i, dangerLeftUpRightDown.Value, 分摊);
                uint finalDelay = 0;

                if (knocked.Contains(party[i]))
                {
                    var preDp = sa.WaypointDp(P1神像1击退前站位(final), 2600, 0, $"P1_神像1_击退前_{i}");
                    preDp.Owner = GuidanceOwner(sa, i);
                    sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, preDp);
                    finalDelay = 2600;
                }

                var finalDp = sa.WaypointDp(final, 5700 - finalDelay, finalDelay, $"P1_神像1_火冰_{i}");
                finalDp.Owner = GuidanceOwner(sa, i);
                sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, finalDp);

                var congaPos = new Vector3(100 + (i < 4 ? -1 : 1) * 波动炮排队距离[i % 4], 0, 100);
                var congaDp = sa.WaypointDp(congaPos, 4200, 5800, $"P1_神像1_波动炮排队_{i}");
                congaDp.Owner = GuidanceOwner(sa, i);
                sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, congaDp);
            }

            sa.Debug($"神像1 火冰：危险对角={(dangerLeftUpRightDown.Value ? "左上右下" : "左下右上")}, 分摊={分摊}, 被连线={string.Join(",", knocked.Select(id => PartyIndex(party, id)))}");
        });
    }

    /// <summary>神像1 火冰站位：分摊 = 半场四人同点；分散 = Boss 南北侧 / Boss 东西侧 / 安全区中间 / 墙边（MT ST H1 H2，DPS 同序）</summary>
    private static Vector3 P1神像1站位(int idx, bool dangerLeftUpRightDown, bool 分摊)
    {
        float sx = idx < 4 ? -1 : 1;
        float sz = dangerLeftUpRightDown ? -sx : sx;

        var (x, z) = 分摊
            ? (7f, 7f)
            : (idx % 4) switch
            {
                0 => (1.5f, 7f),
                1 => (7f, 1.5f),
                2 => (9.5f, 9.5f),
                _ => (18.5f, 1.5f),
            };

        return new Vector3(100 + sx * x, 0, 100 + sz * z);
    }

    /// <summary>被往南击退 13m 的人：先站目标点正北 13m，超出场内 17m 就往场中收</summary>
    private static Vector3 P1神像1击退前站位(Vector3 final)
    {
        var p = new Vector2(final.X - 100, final.Z - 100 - 13);
        if (p.Length() > 17)
            p *= 17 / p.Length();
        return new Vector3(100 + p.X, 0, 100 + p.Y);
    }

    // 47784 波动炮：每组被点名的两人原地留塔，没被点的两人按「从场中往外」的顺序去接本组两座塔
    [ScriptMethod(
        name: "P1_冰爆_47784换位指路",
        eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:47784"],
        userControl: false)]
    public void P1_冰爆_47784换位指路(Event evt, ScriptAccessory sa)
    {
        if (!IsPhase(_phase, 1.1))
            return;

        uint targetId = evt.TargetId();
        Vector3 effectPosition = evt.TargetPosition();

        if (targetId == 0)
            return;

        Dictionary<uint, Vector3> snapshot;

        lock (_iceFireLock)
        {
            if (_action47784GuideSent)
                return;

            _action47784Targets[targetId] = effectPosition;

            if (_action47784Targets.Count < 4)
                return;

            _phase = 1.2;
            _action47784GuideSent = true;
            snapshot = new Dictionary<uint, Vector3>(_action47784Targets);
        }

        Task.Run(async () =>
        {
            await Task.Delay(100);

            var party = sa.Data.PartyList;
            int myIdx = sa.MyIndex();
            var lowGroup = party.Take(4).ToList();
            var highGroup = party.Skip(4).Take(4).ToList();

            for (int i = 0; i < party.Count && i < 8; i++)
            {
                if (!Debugging && i != myIdx)
                    continue;

                var pos = Get47784GuidePosForGroup(party[i], lowGroup, party, snapshot)
                          ?? Get47784GuidePosForGroup(party[i], highGroup, party, snapshot);
                if (pos == null)
                    continue;

                var dp = sa.WaypointDp(pos.Value, 4500, 0, $"P1_冰爆_47784换位指路_{i}");
                dp.Owner = GuidanceOwner(sa, i);
                sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }

            sa.Debug($"47784换位指路：被点名={string.Join(",", snapshot.Keys.Select(x => PartyIndex(party, x)).OrderBy(x => x))}");
        });
    }

    #endregion

    #region P1 神像2（重力弹 / 岩石弹 + 吃水洼）

    // 两轮各 8 根 002D：中间神像 (102.5,22.5,27) 的是重力弹 47788 = 分摊，留 5m 水洼；侧边神像 (126,7,41.5) 的是岩石弹 47792 = 陨石
    // 同一轮里一个职能全是分摊、另一个全是陨石
    // 第 1 轮：连线 → +1.4s 扩大大冰封 47765（对角冰）→ +6.5s 重力弹 → +10.5s 岩石弹；全队站 A 点（北）标点边缘的安全对角那侧
    // 第 2 轮：半场刀预兆 → 连线 → +3.9s 扑杀的神气 47794（半场刀）→ +8.5s 重力弹 → +12.5s 岩石弹；全队站 C 点（南）标点边缘的半场刀安全侧
    // 重力弹结算后：陨石职能分散（G1 = MT H1 M1 R1 面向 Boss 时在左，G2 = ST H2 M2 R2 在右；T/近战最大近战，H/远程外侧），分摊职能去 Boss 脚下
    private readonly object _g2锁 = new();
    private int _g2轮次 = 0;
    private long _g2上次连线 = 0;
    private readonly bool?[] _g2重力弹 = new bool?[8];
    private bool? _g2安全左上右下 = null;
    private int _g2半场刀安全侧 = 0; // +1 东安全，-1 西安全，0 未知
    private bool _g2第一轮已指路 = false;

    private static readonly Vector3 G2重力弹神像 = new(102.5f, 22.5f, 27f);
    private static readonly Vector3 G2岩石弹神像 = new(126f, 7f, 41.5f);

    private void P1_神像2_记录连线(ScriptAccessory sa, uint playerId, Vector3 statuePos)
    {
        int idx = PartyIndex(sa.Data.PartyList, playerId);
        if (idx is < 0 or > 7)
            return;

        long now = Environment.TickCount64;
        bool newRound;
        int round;

        lock (_g2锁)
        {
            newRound = now - _g2上次连线 > 3000;
            if (newRound)
            {
                _g2轮次++;
                Array.Clear(_g2重力弹);
            }

            _g2上次连线 = now;
            round = _g2轮次;
            _g2重力弹[idx] = Vector3.Distance(statuePos, G2重力弹神像) < 1 ? true
                : Vector3.Distance(statuePos, G2岩石弹神像) < 1 ? false
                : null;
        }

        // 第 2 轮没有冰读条可挂，连线到齐后直接指路
        if (newRound && round == 2)
        {
            Task.Run(async () =>
            {
                await Task.Delay(150);

                int side;
                lock (_g2锁)
                    side = _g2半场刀安全侧;

                P1_神像2_分摊与陨石(sa, 2, new Vector3(100 + side * 1.5f, 0, 112), 8500, 4000);
            });
        }
    }

    [ScriptMethod(
        name: "P1_神像2_记录安全对角",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47774|47768)$"],
        userControl: false)]
    public void P1_神像2_记录安全对角(Event evt, ScriptAccessory sa)
    {
        if (!IsPhase(_phase, 1.2))
            return;

        lock (_g2锁)
            _g2安全左上右下 = !IsLeftUpRightDownByRotation(evt.SourceRotation());
    }

    // 半场刀预兆：ObjectEffect 64/128，大球在 (92,15,27) = 西半场挨打，(116,6.5,43) = 东半场挨打（同 Cicero 部分的画图）
    [ScriptMethod(
        name: "P1_神像2_记录半场刀",
        eventType: EventTypeEnum.ObjectEffect,
        eventCondition: ["Id1:64"],
        userControl: false)]
    public void P1_神像2_记录半场刀(Event evt, ScriptAccessory sa)
    {
        if (!IsPhase(_phase, 1.2) || !string.Equals(evt["Id2"], "128"))
            return;

        var src = evt.SourcePosition();
        int side = Vector3.Distance(src, new Vector3(92, 15, 27)) < 1 ? 1
            : Vector3.Distance(src, new Vector3(116, 6.5f, 43)) < 1 ? -1
            : 0;

        if (side != 0)
        {
            lock (_g2锁)
                _g2半场刀安全侧 = side;
        }
    }


    // 47765 扩大大冰封（第 1 轮）：4.7s 读条，+5.1s 重力弹，+9.1s 岩石弹
    [ScriptMethod(
        name: "P1_神像2指路",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:47765"])]
    public void P1_神像2指路(Event evt, ScriptAccessory sa)
    {
        if (!IsPhase(_phase, 1.2))
            return;

        lock (_g2锁)
        {
            if (_g2第一轮已指路)
                return;
            _g2第一轮已指路 = true;
        }

        Task.Run(async () =>
        {
            await Task.Delay(150);

            bool? safeLeftUpRightDown;
            lock (_g2锁)
                safeLeftUpRightDown = _g2安全左上右下;

            // 北边标点：安全对角是左上右下就站标点西侧，否则东侧
            float x = safeLeftUpRightDown switch { true => -1.2f, false => 1.2f, null => 0 };
            P1_神像2_分摊与陨石(sa, 1, new Vector3(100 + x, 0, 88), 5100, 4000);
        });
    }

    private void P1_神像2_分摊与陨石(ScriptAccessory sa, int round, Vector3 stackPos, uint stackMs, uint meteorMs)
    {
        bool? supportStack;

        lock (_g2锁)
        {
            int supports = Enumerable.Range(0, 4).Count(i => _g2重力弹[i] == true);
            int dps = Enumerable.Range(4, 4).Count(i => _g2重力弹[i] == true);
            supportStack = supports == dps ? null : supports > dps;
        }

        int myIdx = sa.MyIndex();

        for (int i = 0; i < sa.Data.PartyList.Count && i < 8; i++)
        {
            if (!Debugging && i != myIdx)
                continue;

            var stackDp = sa.WaypointDp(stackPos, stackMs, 0, $"P1_神像2_第{round}轮分摊_{i}");
            stackDp.Owner = GuidanceOwner(sa, i);
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, stackDp);

            if (supportStack == null)
                continue;

            bool meteor = (i < 4) != supportStack.Value;
            var nextDp = sa.WaypointDp(meteor ? P1神像2陨石站位(i, round) : ArenaCenter, meteorMs, stackMs, $"P1_神像2_第{round}轮散开_{i}");
            nextDp.Owner = GuidanceOwner(sa, i);
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, nextDp);
        }

        sa.Debug($"神像2 第{round}轮：分摊点={stackPos}, 分摊职能={(supportStack == null ? "未知" : supportStack.Value ? "支援" : "DPS")}");
    }

    /// <summary>陨石分散：G1 面向 Boss 在左（第 1 轮从北看 = 东，第 2 轮从南看 = 西），T/近战最大近战 7m，H/远程外侧偏向水洼那侧</summary>
    private static Vector3 P1神像2陨石站位(int idx, int round)
    {
        float g1 = idx % 2 == 0 ? 1 : -1;
        float side = round == 1 ? g1 : -g1;
        float zs = round == 1 ? -1 : 1;

        return isMelee(idx)
            ? new Vector3(100 + side * 7, 0, 100)
            : new Vector3(100 + side * 12.5f, 0, 100 + zs * 9);
    }

    #endregion

    #region P1 神像3（箭头 + 固定点 + 最后火雷）

    private readonly object _statue3ArrowLock = new();

    // 每人身上各有两层箭头 buff，按玩家分开记（Debug 时全队都要解算）
    private sealed class Statue3ArrowRecord
    {
        public int Count;
        public int? SmallDir4;
        public int? LargeDir4;
        public bool? SmallIsShort;
        public bool GuideSent;
    }

    private readonly Dictionary<uint, Statue3ArrowRecord> _statue3Arrows = new();
    private readonly HashSet<uint> _statue3TetherGuided = new();

    private const uint Statue3SmallArrowMinStatus = 0x130C;
    private const uint Statue3SmallArrowMaxStatus = 0x130F;

    // 唰啦啦传送箭头：4876~4879（同向两层都是这组）+ 5079~5082（异向的「拐出去」那个），7s / 10s 两层
    [ScriptMethod(
        name: "P1_神像3_箭头指路",
        eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:regex:^(4876|4877|4878|4879|5079|5080|5081|5082|130C|130D|130E|130F|13D7|13D8|13D9|13DA)$"])]
    public void P1_神像3_箭头指路(Event evt, ScriptAccessory sa)
    {
        if (_phase >= 2.0)
            return;

        // 箭头 buff 是神像3 专属；48370 阶段控制漏了就用它兜底切到 1.3
        if (!IsPhase(_phase, 1.3))
        {
            _phase = 1.3;
            _p1StatueCount = Math.Max(_p1StatueCount, 3);
        }

        uint targetId = evt.TargetId();
        if (targetId == 0)
            targetId = sa.Data.Me;
        if (!Debugging && targetId != sa.Data.Me)
            return;

        int targetIdx = PartyIndex(sa.Data.PartyList, targetId);
        if (targetIdx < 0)
            return;

        uint statusId = evt.StatusId();
        int? dir4 = GetStatue3ArrowDir4(statusId);
        if (dir4 == null)
            return;

        double duration = evt.Duration();
        if (evt.SourceId() == 0 || evt.SourcePosition() == Vector3.Zero || duration > 100)
            return;

        bool isSmallId = statusId >= Statue3SmallArrowMinStatus && statusId <= Statue3SmallArrowMaxStatus;
        bool sameBuff;
        bool smallIsShort;
        int smallDir4;
        int largeDir4;

        lock (_statue3ArrowLock)
        {
            if (!_statue3Arrows.TryGetValue(targetId, out var rec))
                _statue3Arrows[targetId] = rec = new Statue3ArrowRecord();

            if (rec.GuideSent)
                return;

            rec.Count++;

            if (isSmallId)
            {
                rec.SmallDir4 = dir4.Value;
                rec.SmallIsShort = duration < 8.0;
            }
            else
            {
                rec.LargeDir4 = dir4.Value;
            }

            if (rec.Count < 2)
                return;

            sameBuff = !rec.SmallDir4.HasValue || !rec.LargeDir4.HasValue;
            smallIsShort = rec.SmallIsShort ?? false;
            smallDir4 = rec.SmallDir4 ?? 0;
            largeDir4 = rec.LargeDir4 ?? 0;
            rec.GuideSent = true;
        }

        var plan = GetStatue3ArrowPlan(sameBuff, smallIsShort, smallDir4, largeDir4);
        Vector3 firstPos = RotateStatue3RelativePoint(plan.Dir4, plan.FirstRelative);
        Vector3 secondPos = RotateStatue3RelativePoint(plan.Dir4, plan.SecondRelative);

        var firstDp = sa.WaypointDp(firstPos, 7500, 0, $"P1_神像3_箭头指路_第一段_{targetIdx}", new Vector4(0, 1, 0, 1));
        firstDp.Owner = GuidanceOwner(sa, targetIdx);

        var secondDp = sa.WaypointDp(secondPos, 3000, 7500, $"P1_神像3_箭头指路_第二段_{targetIdx}", new Vector4(0, 1, 0, 1));
        secondDp.Owner = GuidanceOwner(sa, targetIdx);

        var previewDp = sa.WaypointFromToDp(firstPos, secondPos, 7500, 0, $"P1_神像3_箭头指路_第二段预览_{targetIdx}", new Vector4(1, 1, 1, 1));

        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, firstDp);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, secondDp);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, previewDp);

        sa.Debug($"神像3箭头：Index={targetIdx}, {plan.Callout}, First={firstPos}, Second={secondPos}");
    }

    // 神像3 连线（睡眠/魅惑都不管）全固定：北 MT 内/R1 外，西 ST 内/R2 外，东 M2 内/H2 外，南 M1 内/H1 外
    // 连线后 3.3s 第三次陷阱击退，+9s 睡魔/圣母的神气结算；击退落地后再去固定点
    [ScriptMethod(
        name: "P1_神像3_固定点指路",
        eventType: EventTypeEnum.Tether,
        eventCondition: ["Id:002D"])]
    public void P1_神像3_固定点指路(Event evt, ScriptAccessory sa)
    {
        if (!IsPhase(_phase, 1.3))
            return;

        uint playerId = sa.Data.PartyList.Contains(evt.SourceId()) ? evt.SourceId() : evt.TargetId();
        if (!Debugging && playerId != sa.Data.Me)
            return;

        int idx = PartyIndex(sa.Data.PartyList, playerId);
        if (idx is < 0 or > 7)
            return;

        lock (_statue3ArrowLock)
        {
            if (!_statue3TetherGuided.Add(playerId))
                return;
        }

        var dp = sa.WaypointDp(P1神像3固定点(idx), 5000, 5000, $"P1_神像3_固定点_{idx}");
        dp.Owner = GuidanceOwner(sa, idx);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    /// <summary>内 = Boss 最大近战 7.5m，外 = 箭头和墙之间 17m</summary>
    private static Vector3 P1神像3固定点(int idx) => idx switch
    {
        0 => new Vector3(100, 0, 92.5f),
        1 => new Vector3(92.5f, 0, 100),
        2 => new Vector3(100, 0, 117),
        3 => new Vector3(117, 0, 100),
        4 => new Vector3(100, 0, 107.5f),
        5 => new Vector3(107.5f, 0, 100),
        6 => new Vector3(100, 0, 83),
        _ => new Vector3(83, 0, 100),
    };

    // 神像3 后的 47764 玄乎乎魔法：火（可能假）+ 真假雷直线 + 视线，+5.8s 一起结算
    // 分摊：DPS 北、支援南，各自找离 Boss 6m 处最近的不在真雷线上的点；分散：原地，被真雷线罩住才挪到最近的安全点
    // [ScriptMethod(
    //     name: "P1_神像3_最后火雷指路",
    //     eventType: EventTypeEnum.StartCasting,
    //     eventCondition: ["ActionId:47764"])]
    // public void P1_神像3_最后火雷指路(Event evt, ScriptAccessory sa)
    // {
    //     if (!IsPhase(_phase, 1.3))
    //         return;

    //     Task.Run(async () =>
    //     {
    //         // 真雷 47775/47777 和 47764 同一帧读条，稍等它们进危险区列表
    //         await Task.Delay(300);

    //         bool 分摊 = stackFlagrantFireIcon != fakeFlagrantFire;
    //         var party = sa.Data.PartyList;
    //         int myIdx = sa.MyIndex();

    //         for (int i = 0; i < party.Count && i < 8; i++)
    //         {
    //             if (!Debugging && i != myIdx)
    //                 continue;

    //             Vector3? from = 分摊
    //                 ? new Vector3(100, 0, i < 4 ? 106 : 94)
    //                 : sa.Data.Objects.SearchById(party[i])?.Position;
    //             if (from == null)
    //                 continue;

    //             var dp = sa.WaypointDp(P1最近安全点(from.Value), 5300, 0, $"P1_神像3_最后火雷_{i}");
    //             dp.Owner = GuidanceOwner(sa, i);
    //             sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    //         }

    //         sa.Debug($"神像3 最后火雷：分摊={分摊}");
    //     });
    // }

    private void ResetStatue3ArrowState()
    {
        lock (_statue3ArrowLock)
        {
            _statue3Arrows.Clear();
            _statue3TetherGuided.Clear();
        }
    }

    #endregion

    #region 工具方法

    private readonly record struct Statue3ArrowPlan(int Dir4, Vector2 FirstRelative, Vector2 SecondRelative, string Callout);

    // 箭头朝向：0 北 1 西 2 南 3 东（两组 ID 同图标同朝向）
    private static int? GetStatue3ArrowDir4(uint statusId)
    {
        return statusId switch
        {
            0x130C or 0x13D7 => 0,
            0x130D or 0x13D8 => 2,
            0x130E or 0x13D9 => 3,
            0x130F or 0x13DA => 1,
            _ => null,
        };
    }

    // LPDU 顺时针方框（方框边长 24m，正点和角上都有标点）：
    // 两个同向箭头：先放所在边的正点，再往逆时针挪一格（6m）
    // 两个不同向：放「正点顺时针一格 + 角」，拐出去的那个（5079~5082 组）放角上，先到期的先放
    private static Statue3ArrowPlan GetStatue3ArrowPlan(bool sameBuff, bool smallIsShort, int smallDir4, int largeDir4)
    {
        if (sameBuff)
        {
            int side = (Math.Max(smallDir4, largeDir4) + 1) % 4;
            return new Statue3ArrowPlan(side, new Vector2(0, -12), new Vector2(-6, -12), $"{CardinalDirText(side)}正点放两次");
        }

        int dir4 = (largeDir4 + 2) % 4;
        return new Statue3ArrowPlan(
            dir4,
            smallIsShort ? new Vector2(6, -12) : new Vector2(12, -12),
            smallIsShort ? new Vector2(12, -12) : new Vector2(6, -12),
            $"{DiagonalClockwiseDirText(dir4)}，{(smallIsShort ? "先边后角" : "先角后边")}");
    }

    private static Vector3 RotateStatue3RelativePoint(int dir4, Vector2 relative)
    {
        float x = relative.X;
        float z = relative.Y;

        (float rotatedX, float rotatedZ) = ((dir4 % 4 + 4) % 4) switch
        {
            0 => (x, z),
            1 => (z, -x),
            2 => (-x, -z),
            3 => (-z, x),
            _ => (x, z),
        };

        return new Vector3(100.00f + rotatedX, 0.00f, 100.00f + rotatedZ);
    }

    private static string CardinalDirText(int dir4)
    {
        return ((dir4 % 4 + 4) % 4) switch
        {
            0 => "上北",
            1 => "左西",
            2 => "下南",
            3 => "右东",
            _ => "未知",
        };
    }

    private static string DiagonalClockwiseDirText(int dir4)
    {
        return ((dir4 % 4 + 4) % 4) switch
        {
            0 => "右上",
            1 => "左上",
            2 => "左下",
            3 => "右下",
            _ => "未知",
        };
    }

    private static Vector3? Get47784GuidePosForGroup(
        uint me,
        List<uint> groupMembers,
        IReadOnlyList<uint> party,
        Dictionary<uint, Vector3> targetPositions)
    {
        if (!groupMembers.Contains(me) || targetPositions.ContainsKey(me))
            return null;

        var targetedMembers = groupMembers
            .Where(id => targetPositions.ContainsKey(id))
            .OrderBy(id => PartyIndex(party, id))
            .ToList();

        var untargetedMembers = groupMembers
            .Where(id => !targetPositions.ContainsKey(id))
            .OrderBy(id => PartyIndex(party, id))
            .ToList();

        if (targetedMembers.Count != 2 || untargetedMembers.Count != 2)
            return null;

        int myOrder = untargetedMembers.IndexOf(me);

        return myOrder is >= 0 and < 2 ? targetPositions[targetedMembers[myOrder]] : null;
    }

    private static int PartyIndex(IReadOnlyList<uint> party, uint playerId)
    {
        for (int i = 0; i < party.Count; i++)
        {
            if (party[i] == playerId)
                return i;
        }

        return -1;
    }

    private static bool IsPhase(double current, double target)
    {
        return Math.Abs(current - target) < 0.0001;
    }

    private static bool IsLeftUpRightDownByRotation(float rotation)
    {
        double degree = rotation * 180.0 / Math.PI;
        degree = (degree % 360.0 + 360.0) % 360.0;

        double distanceTo45Or225 = Math.Min(AngleDistance(degree, 45.0), AngleDistance(degree, 225.0));
        double distanceTo135Or315 = Math.Min(AngleDistance(degree, 135.0), AngleDistance(degree, 315.0));

        return distanceTo45Or225 <= distanceTo135Or315;
    }

    private static double AngleDistance(double a, double b)
    {
        double diff = Math.Abs(a - b) % 360.0;
        return diff > 180.0 ? 360.0 - diff : diff;
    }

    #endregion

    #endregion

    #region P2 绘制与指路（Cicero 灵视）

    #region Major_Phase_2

    [ScriptMethod(name: "———————— 《P2》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"])]
    public void P2_分割线(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name:"P2 (阶段控制)",
        eventType:EventTypeEnum.PlayActionTimeline,
        eventCondition:["Id:4565"],
        userControl:false)]

    public void P2_阶段控制(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=1) {

            return;

        }

        if(!string.Equals(@event["SourceDataId"],"19506")) {

            return;

        }

        accessory.Method.RemoveDraw(".*");

        majorPhase=2;
        phase=1;

        if(enableDebugLogging) {

            accessory.Log.Debug($"majorPhase={majorPhase}\nphase={phase}");

        }

    }

    [ScriptMethod(name:"P2 终末双腕 (范围)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:49740"])]

    public void P2_终末双腕_范围(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=2) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        currentProperties.Scale=new(5);
        currentProperties.Owner=targetId;
        currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(1);
        currentProperties.DestoryAt=5000;

        accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,currentProperties);

    }

    [ScriptMethod(name:"P2 遗弃末世 (阶段控制)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:47804"],
        userControl:false)]

    public void P2_遗弃末世_阶段控制(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=2) {

            return;

        }

        if(phase!=1) {

            return;

        }

        phase=2;

        if(enableDebugLogging) {

            accessory.Log.Debug($"majorPhase={majorPhase}\nphase={phase}");

        }

    }

    // 固定搭档 MT+H1 / ST+H2 / M1+R1 / M2+R2：有分摊的两对是 A 组（第 1/2/3/8 轮进塔），另外两对是 B 组（第 4~7 轮）
    private bool groupPartyMembersAtBeginning(ScriptAccessory accessory) {

        int[][] group=[[0,2],
                       [1,3],
                       [4,6],
                       [5,7]];

        foreach(var pair in group) {

            bool hasStack=pair.Any(i => phase2sub2_iconType[i]==Phase2Sub2_IconTypes.STACK);
            (hasStack?phase2sub2_groupA:phase2sub2_groupB).AddRange(pair);

        }

        if(enableDebugLogging) {

            accessory.Log.Debug($"""
                                 phase2sub2_groupA:{string.Join(",",phase2sub2_groupA)}
                                 phase2sub2_groupB:{string.Join(",",phase2sub2_groupB)}
                                 """);

        }

        return phase2sub2_groupA.Count==4&&phase2sub2_groupB.Count==4;

    }

    [ScriptMethod(name:"P2 遗弃末世 咏唱危机 (数据收集)",
        eventType:EventTypeEnum.TargetIcon,
        eventCondition:["Id:regex:^(02CD|02CC|02CB)$"],
        userControl:false)]

    public void P2_遗弃末世_咏唱危机_数据收集(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=2) {

            return;

        }

        if(phase!=2) {

            return;

        }

        if(phase2sub2_iconUpdateCounter>=8+4*6) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        int targetIndex=accessory.Data.PartyList.IndexOf(((uint)targetId));

        if(!isLegalPartyIndex(targetIndex)) {

            return;

        }

        lock(phase2sub2_iconType) {

            bool validUpdate=false;

            if(string.Equals(@event["Id"],"02CD")) {

                phase2sub2_iconType[targetIndex]=Phase2Sub2_IconTypes.FAN;

                validUpdate=true;

            }

            if(string.Equals(@event["Id"],"02CC")) {

                phase2sub2_iconType[targetIndex]=Phase2Sub2_IconTypes.SPREAD;

                validUpdate=true;

            }

            if(string.Equals(@event["Id"],"02CB")) {

                phase2sub2_iconType[targetIndex]=Phase2Sub2_IconTypes.STACK;

                validUpdate=true;

            }

            if(validUpdate) {

                Interlocked.Increment(ref phase2sub2_iconUpdateCounter);

                if(phase2sub2_iconUpdateCounter>=8) {

                    if(phase2sub2_iconUpdateCounter==8) {

                        if(!groupPartyMembersAtBeginning(accessory)) {

                            return;

                        }

                        phase2sub2_roundSemaphore[0].Set();

                        if(enableDebugLogging) {

                            accessory.Log.Debug($"""
                                                 phase2sub2_iconType:{string.Join(",",phase2sub2_iconType)}
                                                 The initialization (Round 0) semaphore has been signalled.
                                                 """);

                        }

                    }

                    else {

                        int updateCountAfterInitialization=phase2sub2_iconUpdateCounter-8;

                        if(updateCountAfterInitialization%4==0) {

                            int lastRound=updateCountAfterInitialization/4;

                            phase2sub2_roundSemaphore[lastRound].Set();

                            if(enableDebugLogging) {

                                accessory.Log.Debug($"""
                                                    updateCountAfterInitialization={updateCountAfterInitialization}
                                                    currentRound={lastRound}
                                                    Round {lastRound} semaphore has been signalled.
                                                    """);

                            }

                        }

                    }

                }

            }

        }

    }

    private int getDiscretizedTowerGap(int discretizedTower1,int discretizedTower2) {

        int result=-1;

        if(discretizedTower1<0||discretizedTower1>7) {

            return result;

        }

        if(discretizedTower2<0||discretizedTower2>7) {

            return result;

        }

        if(Math.Abs(discretizedTower1-discretizedTower2)==2) {

            result=((discretizedTower1+discretizedTower2)/2+16)%8;

        }

        if(Math.Abs(discretizedTower1-discretizedTower2)==6) {

            result=((discretizedTower1+discretizedTower2+8)/2+16)%8;

        }

        return result;

    }

    // 塔出现：EnvControl Index 1~8，Flag 是 1 或 2（同一轮两座塔也可能不同，第 1 轮常是两个 1），只认 2 会漏塔导致整轮不指路
    [ScriptMethod(name:"P2 遗弃末世 塔 (数据收集)",
        eventType:EventTypeEnum.EnvControl,
        eventCondition:["Flag:regex:^(1|2)$"],
        userControl:false)]

    public void P2_遗弃末世_塔_数据收集(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=2) {

            return;

        }

        if(phase!=2) {

            return;

        }

        if(phase2sub2_towerCounter>=16) {

            return;

        }

        if(!convertStringToSignedInteger(@event["Index"], out var index)) {

            return;

        }

        if(index<1||index>8) {

            return;

        }

        lock(phase2sub2_discretizedTowerGap) {

            Interlocked.Increment(ref phase2sub2_towerCounter);

            if(phase2sub2_towerCounter%2==1) {

                phase2sub2_discretizedPositionOfLastTower=(index-1+8)%8;

            }

            if(phase2sub2_towerCounter%2==0) {

                int discretizedPositionOfCurrentTower=(index-1+8)%8;
                int currentRound=phase2sub2_towerCounter/2;

                phase2sub2_discretizedTowerGap[currentRound]=getDiscretizedTowerGap(phase2sub2_discretizedPositionOfLastTower,discretizedPositionOfCurrentTower);

                if(phase2sub2_discretizedTowerGap[currentRound]==-1) {

                    return;

                }

                if(currentRound==1) {

                    phase2sub2_discretizedTowerGap1Semaphore.Set();

                    if(enableDebugLogging) {

                        accessory.Log.Debug($"""
                                             phase2sub2_towerCounter={phase2sub2_towerCounter}
                                             currentRound={currentRound}
                                             phase2sub2_discretizedPositionOfLastTower={phase2sub2_discretizedPositionOfLastTower}
                                             discretizedPositionOfCurrentTower={discretizedPositionOfCurrentTower}
                                             phase2sub2_discretizedTowerGap[{currentRound}]={phase2sub2_discretizedTowerGap[currentRound]}
                                             The gap 1 semaphore has been signalled.
                                             """);

                    }

                }

                if(currentRound==2) {

                    int trend=phase2sub2_discretizedTowerGap[2]-phase2sub2_discretizedTowerGap[1];

                    if(trend==-7) {

                        trend=1;

                    }

                    if(trend==7) {

                        trend=-1;

                    }

                    for(int i=3;i<phase2sub2_discretizedTowerGap.Length;++i) {

                        phase2sub2_discretizedTowerGap[i]=(phase2sub2_discretizedTowerGap[i-1]+trend+16)%8;

                    }

                    phase2sub2_discretizedTowerGap2Semaphore.Set();

                    if(enableDebugLogging) {

                        accessory.Log.Debug($"""
                                             phase2sub2_towerCounter={phase2sub2_towerCounter}
                                             currentRound={currentRound}
                                             phase2sub2_discretizedPositionOfLastTower={phase2sub2_discretizedPositionOfLastTower}
                                             discretizedPositionOfCurrentTower={discretizedPositionOfCurrentTower}
                                             trend={trend}
                                             phase2sub2_discretizedTowerGap:{string.Join(",",phase2sub2_discretizedTowerGap)}
                                             The gap 2 semaphore has been signalled.
                                             """);

                    }

                }

            }

        }

    }

    [ScriptMethod(name:"P2 遗弃末世 (指路,第1轮)",
        eventType:EventTypeEnum.ActionEffect,
        eventCondition:["ActionId:47804"],
        suppress:COMMON_INTERVAL)]

    public void P2_遗弃末世_指路_第1轮(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=2) {

            return;

        }

        if(phase!=2) {

            return;

        }

        int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);

        if(!isLegalPartyIndex(myIndex)) {

            return;

        }

        bool signalled=phase2sub2_roundSemaphore[0].WaitOne(1500+COMMON_INTERVAL);

        if(!signalled) {

            return;

        }

        signalled=phase2sub2_discretizedTowerGap1Semaphore.WaitOne(1500+COMMON_INTERVAL);
        if(!signalled) {
            return;
        }
        P2_遗弃末世_指路(1,accessory);

    }

    [ScriptMethod(name:"P2 遗弃末世 (指路,第2轮到第8轮)",
        eventType:EventTypeEnum.ActionEffect,
        eventCondition:["ActionId:47806"])]

    public void P2_遗弃末世_指路_第2轮到第8轮(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=2) {

            return;

        }

        if(phase!=2) {

            return;

        }

        if(!string.Equals(@event["TargetIndex"],"1")) {

            return;

        }

        if(phase2sub2_pathOfLightCounter>=2*8) {

            return;

        }

        bool lastRoundEnded=false;
        int lastRound=0;

        lock(PHASE2_SUB2_PATH_OF_LIGHT_COUNTER_LOCK) {

            Interlocked.Increment(ref phase2sub2_pathOfLightCounter);

            if(phase2sub2_pathOfLightCounter%2==0) {

                lastRoundEnded=true;
                lastRound=phase2sub2_pathOfLightCounter/2;

                if(enableDebugLogging) {

                    accessory.Log.Debug($"""
                                         phase2sub2_pathOfLightCounter={phase2sub2_pathOfLightCounter}
                                         lastRoundEnded={lastRoundEnded}
                                         lastRound={lastRound}
                                         """);

                }

            }

        }

        if(lastRoundEnded
           &&
           (1<=lastRound&&lastRound<=8)) {

            if(1<=lastRound&&lastRound<=6) {

                bool signalled=phase2sub2_roundSemaphore[lastRound].WaitOne(625+COMMON_INTERVAL);

                if(!signalled) {

                    return;

                }

                if(lastRound==1) {

                    signalled=phase2sub2_discretizedTowerGap2Semaphore.WaitOne(COMMON_INTERVAL);

                    if(!signalled) {

                        return;

                    }

                }

            }

            phase2sub2_allThingsEndingSemaphore.Reset();

            accessory.Method.RemoveDraw($"P2_遗弃末世_消灭之脚指路1_第{lastRound}轮(_\\d)?");
            accessory.Method.RemoveDraw($"P2_遗弃末世_消灭之脚指路2_第{lastRound}轮");
            accessory.Method.RemoveDraw($"P2_遗弃末世_消灭之脚指路3_第{lastRound}轮(_\\d)?");
            accessory.Method.RemoveDraw($"P2_遗弃末世_指路辅助线1_第{lastRound}轮");
            accessory.Method.RemoveDraw($"P2_遗弃末世_指路辅助线2_第{lastRound}轮");
            accessory.Method.RemoveDraw($"P2_遗弃末世_指路_第{lastRound}轮(_\\d)?");

            if(lastRound==8) {

                P2_遗弃末世_指路_末尾(accessory);

                return;

            }

            P2_遗弃末世_指路(lastRound+1,accessory);

        }

    }

    private void P2_遗弃末世_指路_末尾(ScriptAccessory accessory) {
        int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
        var currentProperties=accessory.Data.GetDefaultDrawProperties();
        for(int partyIndex=0;partyIndex<8;++partyIndex) {
            if(!Debugging&&partyIndex!=myIndex) {
                continue;
            }
            currentProperties=accessory.Data.GetDefaultDrawProperties();
            currentProperties.Name=$"P2_遗弃末世_消灭之脚指路1_末尾_{partyIndex}";
            currentProperties.Scale=new(2);
            currentProperties.Owner=GuidanceOwner(accessory,partyIndex);
            currentProperties.TargetPosition=PHASE2_SUB2_RAW_TOWER_POSITION;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=MAXIMUM_DURATION;
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
        }
        currentProperties=accessory.Data.GetDefaultDrawProperties();

        currentProperties.Name="P2_遗弃末世_消灭之脚指路2_末尾";
        currentProperties.Scale=new(20);
        currentProperties.Radian=((float)(convertDegreesToRadians(5)));
        currentProperties.Position=ARENA_CENTER;
        currentProperties.TargetPosition=PHASE2_SUB2_RAW_TOWER_POSITION;
        currentProperties.Color=accessory.Data.DefaultSafeColor;
        currentProperties.DestoryAt=MAXIMUM_DURATION;

        accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Fan,currentProperties);

        phase2sub2_allThingsEndingSemaphore.WaitOne(6375+COMMON_INTERVAL);

        accessory.Method.RemoveDraw("P2_遗弃末世_消灭之脚指路1_末尾(_\\d)?");
        accessory.Method.RemoveDraw("P2_遗弃末世_消灭之脚指路2_末尾");
        for(int partyIndex=0;partyIndex<8;++partyIndex) {
            if(!Debugging&&partyIndex!=myIndex) {
                continue;
            }
            currentProperties=accessory.Data.GetDefaultDrawProperties();
            currentProperties.Name=$"P2_遗弃末世_消灭之脚指路3_末尾_{partyIndex}";
            currentProperties.Scale=new(2);
            currentProperties.Owner=GuidanceOwner(accessory,partyIndex);
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=5000;
            if(isFuturesEnd) {
                currentProperties.TargetPosition=rotatePosition(PHASE2_SUB2_RAW_TOWER_POSITION,ARENA_CENTER,Math.PI);
            }
            else {
                currentProperties.TargetPosition=PHASE2_SUB2_RAW_TOWER_POSITION;
            }
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
        }
    }

    // ----- LPDU Buddies：https://raidplan.io/plan/142oXOZpPc_jh3dd ，精确站位：https://raidplan.io/plan/A0xlCay835pm5qxs -----
    // 搭档 MT+H1 / ST+H2 / M1+R1 / M2+R2，有分摊的两对是 A 组，按 AAABBBBA 进塔；远程（H/R）站位固定，T/近战看搭档调整
    // 站位都写在「本轮两座塔在南」的局部系里（x 东为正、z 南为正，相对场中）：面向 Boss 左塔（西南）= 扇形塔、右塔（东南）= 圆圈塔
    private static readonly Vector2 P2左塔=new(-5.657f,5.657f);
    private const float P2默认判定圈=6;

    private void P2_遗弃末世_指路(int currentRound,ScriptAccessory accessory) {

        if(currentRound<1||currentRound>8) {

            return;

        }

        int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);

        if(!isLegalPartyIndex(myIndex)) {

            return;

        }

        float hitbox=P2Boss判定圈(accessory);

        // 每人按自己的分组、头顶图标、职能解算；Debug 时画全队
        var positions=new Dictionary<int,Vector3>();
        for(int partyIndex=0;partyIndex<8;++partyIndex) {
            if(!Debugging&&partyIndex!=myIndex) {
                continue;
            }
            var position=P2_遗弃末世_位置(currentRound,partyIndex,hitbox,accessory);
            if(position!=null) {
                positions[partyIndex]=position.Value;
            }
        }
        if(positions.Count==0) {
            return;
        }
        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        // 第 3/5/7 轮先引上一轮的过去/未来终结（第 1 轮没有）
        if(currentRound%2==1&&currentRound>1) {

            Vector3 baitPosition=PHASE2_SUB2_RAW_TOWER_POSITION;

            if(isFuturesEnd) {

                baitPosition=rotatePosition(PHASE2_SUB2_RAW_TOWER_POSITION,ARENA_CENTER,Math.PI);

            }

            baitPosition=rotatePosition(baitPosition,ARENA_CENTER,Math.PI/4*phase2sub2_discretizedTowerGap[currentRound]);

            foreach(var (partyIndex,myPosition) in positions) {
                currentProperties=accessory.Data.GetDefaultDrawProperties();
                currentProperties.Name=$"P2_遗弃末世_消灭之脚指路1_第{currentRound}轮_{partyIndex}";
                currentProperties.Scale=new(2);
                currentProperties.Owner=GuidanceOwner(accessory,partyIndex);
                currentProperties.TargetPosition=baitPosition;
                currentProperties.ScaleMode|=ScaleMode.YByDistance;
                currentProperties.Color=accessory.Data.DefaultSafeColor;
                currentProperties.DestoryAt=MAXIMUM_DURATION;
                accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
            }
            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Name=$"P2_遗弃末世_消灭之脚指路2_第{currentRound}轮";
            currentProperties.Scale=new(20);
            currentProperties.Radian=((float)(convertDegreesToRadians(5)));
            currentProperties.Position=ARENA_CENTER;
            currentProperties.TargetPosition=baitPosition;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=MAXIMUM_DURATION;

            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Fan,currentProperties);

            foreach(var (partyIndex,myPosition) in positions) {
                currentProperties=accessory.Data.GetDefaultDrawProperties();
                currentProperties.Name=$"P2_遗弃末世_消灭之脚指路3_第{currentRound}轮_{partyIndex}";
                currentProperties.Scale=new(2);
                currentProperties.Position=baitPosition;
                currentProperties.TargetPosition=myPosition;
                currentProperties.ScaleMode|=ScaleMode.YByDistance;
                currentProperties.Color=accessory.Data.DefaultDangerColor;
                currentProperties.DestoryAt=MAXIMUM_DURATION;
                accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
            }
            phase2sub2_allThingsEndingSemaphore.WaitOne(6375+COMMON_INTERVAL);
            accessory.Method.RemoveDraw($"P2_遗弃末世_消灭之脚指路1_第{currentRound}轮(_\\d)?");
            accessory.Method.RemoveDraw($"P2_遗弃末世_消灭之脚指路2_第{currentRound}轮");
            accessory.Method.RemoveDraw($"P2_遗弃末世_消灭之脚指路3_第{currentRound}轮(_\\d)?");
        }

        if(currentRound%2==0&&phase2sub2_spellwaveAuxiliaryLines) {

            Vector2 cone=P2偶数轮左塔().cone;
            Vector3[] fanPositions=[P2局部转世界(cone,currentRound),P2局部转世界(new Vector2(-cone.X,cone.Y),currentRound)];

            for(int i=0;i<2;++i) {

                currentProperties=accessory.Data.GetDefaultDrawProperties();

                currentProperties.Name=$"P2_遗弃末世_指路辅助线{i+1}_第{currentRound}轮";
                currentProperties.Scale=new(0.5f);
                currentProperties.Position=fanPositions[i];
                currentProperties.Color=phase2sub2_colourOfAuxiliaryLines.V4.WithW(1);
                currentProperties.DestoryAt=MAXIMUM_DURATION;

                accessory.Method.SendDraw(getDrawModeEnum(DrawTypeEnum.Circle),DrawTypeEnum.Circle,currentProperties);

            }

        }

        foreach(var (partyIndex,myPosition) in positions) {
            currentProperties=accessory.Data.GetDefaultDrawProperties();
            currentProperties.Name=$"P2_遗弃末世_指路_第{currentRound}轮_{partyIndex}";
            currentProperties.Scale=new(2);
            currentProperties.Owner=GuidanceOwner(accessory,partyIndex);
            currentProperties.TargetPosition=myPosition;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=currentRound==1?10750:MAXIMUM_DURATION;
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
        }
    }

    private Vector3? P2_遗弃末世_位置(int currentRound,int myIndex,float hitbox,ScriptAccessory accessory) {

        bool towerIsGroupA=phase2sub2_isGroupARound[currentRound];
        List<int> towerGroup=towerIsGroupA?phase2sub2_groupA:phase2sub2_groupB;
        List<int> helperGroup=towerIsGroupA?phase2sub2_groupB:phase2sub2_groupA;
        bool inTower=towerGroup.Contains(myIndex);

        if(!inTower&&!helperGroup.Contains(myIndex)) {

            return null;

        }

        Vector2? relative=(currentRound%2==1)
            ?P2奇数轮站位(myIndex,inTower,currentRound==1)
            :P2偶数轮站位(myIndex,inTower,towerGroup,hitbox);

        if(enableDebugLogging) {

            accessory.Log.Debug($"""
                                 currentRound={currentRound}
                                 myIndex={myIndex}, inTower={inTower}
                                 phase2sub2_iconType[{myIndex}]={phase2sub2_iconType[myIndex]}
                                 relative={relative}, hitbox={hitbox}
                                 """);

        }

        return relative==null?null:P2局部转世界(relative.Value,currentRound);

    }

    // 局部系是塔在南，原来的离散方位以塔在北为 0，所以多转半圈
    private Vector3 P2局部转世界(Vector2 relative,int currentRound) {

        return rotatePosition(new Vector3(100+relative.X,0,100+relative.Y),ARENA_CENTER,Math.PI/4*phase2sub2_discretizedTowerGap[currentRound]+Math.PI);

    }

    // 奇数轮（2 分摊 1 扇形 1 圆圈，分摊 5m）：
    // 扇形塔 = 支援的分摊（塔内靠 Boss）+ 扇形（塔最南略偏右）；圆圈塔 = DPS 的分摊（塔左侧最大近战）+ 圆圈（塔最东）
    // 搭档两人都是分摊：远程留本职能那边，T/近战换到另一边
    // PF 打法只改第 1 轮：分摊跟搭档进塔（搭档扇形 → 扇形塔，搭档圆圈 → 圆圈塔），之后各轮同 LPDU
    // 闲人固定：T 在 Boss 判定圈内、扇形塔前（吃支援分摊），H 在扇形人正南引扇形；DPS 两人在两塔中间靠圆圈塔吃 DPS 分摊
    private Vector2? P2奇数轮站位(int idx,bool inTower,bool firstRound) {

        if(!inTower) {

            if(isTank(idx)) return new Vector2(-2.8f,2.8f);
            if(isHealer(idx)) return new Vector2(-5.0f,11.9f);
            if(isMeleeDps(idx)) return new Vector2(1.2f,6.4f);
            return new Vector2(1.6f,7.6f);

        }

        switch(phase2sub2_iconType[idx]) {

            case Phase2Sub2_IconTypes.FAN:
                return new Vector2(-5.0f,9.2f);

            case Phase2Sub2_IconTypes.SPREAD:
                return new Vector2(9.06f,5.66f);

            case Phase2Sub2_IconTypes.STACK: {

                // idx^2 = 固定搭档（0↔2、1↔3、4↔6、5↔7）
                var buddy=phase2sub2_iconType[idx^2];
                bool coneTower=isSupporter(idx);

                if(buddy==Phase2Sub2_IconTypes.STACK&&isMelee(idx)) {

                    coneTower=!coneTower;

                }

                else if(followPfStrats&&firstRound&&buddy is Phase2Sub2_IconTypes.FAN or Phase2Sub2_IconTypes.SPREAD) {

                    coneTower=buddy==Phase2Sub2_IconTypes.FAN;

                }

                return coneTower?new Vector2(-4.81f,4.81f):new Vector2(3.46f,6.26f);

            }

            default:
                return null;

        }

    }

    // 偶数轮（2 扇形 2 圆圈）：每座塔前扇形后圆圈
    // H+近战进左塔、远程+T 进右塔，近战和 H 同图标时近战与 T 对调
    // 闲人固定：T 西北 / 近战东北站 Boss 外判定圈引分身（四个分身点名最近的 4 人 = 两个扇形 + 这两人），
    // H / 远程站扇形人外侧引扇形，扇形往东西两边打出去
    private Vector2? P2偶数轮站位(int idx,bool inTower,List<int> towerGroup,float hitbox) {

        var (cone,circle,baiter)=P2偶数轮左塔();

        if(!inTower) {

            if(isTank(idx)) return new Vector2(-0.7071f,-0.7071f)*(hitbox+0.5f);
            if(isMeleeDps(idx)) return new Vector2(0.7071f,-0.7071f)*(hitbox+0.5f);
            if(isHealer(idx)) return baiter;
            return new Vector2(-baiter.X,baiter.Y);

        }

        int healer=towerGroup.FirstOrDefault(isHealer,-1);
        int melee=towerGroup.FirstOrDefault(isMeleeDps,-1);
        bool swap=healer>=0&&melee>=0&&phase2sub2_iconType[healer]==phase2sub2_iconType[melee];
        bool left=isHealer(idx)||(isMeleeDps(idx)&&!swap)||(isTank(idx)&&swap);

        Vector2? spot=phase2sub2_iconType[idx] switch {
            Phase2Sub2_IconTypes.FAN => cone,
            Phase2Sub2_IconTypes.SPREAD => circle,
            _ => null
        };

        if(spot==null) {

            return null;

        }

        return left?spot:new Vector2(-spot.Value.X,spot.Value.Y);

    }

    /// <summary>
    /// 偶数轮左塔（塔心距场中 8m、半径 4m，往里收 0.4m）：以两塔中间面向 Boss 为 12 点，
    /// 扇形点在塔的 1 点钟（往场内收），圆圈点过塔心在对面 7 点钟（往外）；右塔由调用方镜像成 11 点 / 5 点。
    /// 引扇形点 = 扇形点正西 5.5m（咏唱危机·散碎 47809 圆圈 5m + 0.5m 容错），扇形往西打出去
    /// </summary>
    private static (Vector2 cone,Vector2 circle,Vector2 baiter) P2偶数轮左塔() {

        const float towerRadius=3.6f,clock=MathF.PI/6,baitDistance=5+0.5f;

        var offset=new Vector2(MathF.Sin(clock),-MathF.Cos(clock))*towerRadius;
        var cone=P2左塔+offset;

        return (cone,P2左塔-offset,cone-new Vector2(baitDistance,0));

    }

    // P2 凯夫卡（19506）判定圈半径；读不到或不在合理范围就按 6m
    private static float P2Boss判定圈(ScriptAccessory accessory) {

        float radius=accessory.GetByDataId(19506u).FirstOrDefault()?.HitboxRadius??0;
        return radius is > 4.5f and < 11f?radius:P2默认判定圈;

    }

    [ScriptMethod(name:"P2 遗弃末世 消灭之脚 数据收集",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:regex:^(47836|47837)$"],
        userControl:false)]

    public void P2_遗弃末世_消灭之脚_数据收集(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=2) {

            return;

        }

        if(phase!=2) {

            return;

        }

        lock(PHASE2_SUB2_ALL_THINGS_ENDING_COUNTER_LOCK) {

            Interlocked.Increment(ref phase2sub2_allThingsEndingCounter);

            if(phase2sub2_allThingsEndingCounter%4==0) {

                phase2sub2_allThingsEndingSemaphore.Set();

                if(enableDebugLogging) {

                    accessory.Log.Debug($"""
                                         phase2sub2_allThingsEndingCounter={phase2sub2_allThingsEndingCounter}
                                         All Things Ending semaphore has been signalled.
                                         """);

                }

            }

        }

    }

    private DrawModeEnum getDrawModeEnum(DrawTypeEnum currentType) {

        switch(phase2sub2_drawingType) {

            case Phase2Sub2_DrawingTypes.默认: {

                return DrawModeEnum.Default;

            }

            case Phase2Sub2_DrawingTypes.全VFX: {

                return DrawModeEnum.Vfx;

            }

            case Phase2Sub2_DrawingTypes.作者推荐_圆形ImGui_其余VFX: {

                if(currentType==DrawTypeEnum.Circle) {

                    return DrawModeEnum.Imgui;

                }

                else {

                    return DrawModeEnum.Vfx;

                }

            }

            case Phase2Sub2_DrawingTypes.全ImGui: {

                return DrawModeEnum.Imgui;

            }

            default: {

                return DrawModeEnum.Default;

            }

        }

    }

    [ScriptMethod(name:"P2 遗弃末世 咏唱危机 (范围)",
        eventType:EventTypeEnum.EnvControl,
        eventCondition:["Flag:8"])]

    public void P2_遗弃末世_咏唱危机_范围(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=2) {

            return;

        }

        if(phase!=2) {

            return;

        }

        if(!convertStringToSignedInteger(@event["Index"], out var index)) {

            return;

        }

        if(index<1||index>8) {

            return;

        }

        int drawingDuration=((int)(phase2sub2_drawingDuration*1000));

        if(drawingDuration<=0) {

            return;

        }

        int delay=0;
        int duration=0;

        if(drawingDuration>7000) {

            delay=125;
            duration=7000;

        }

        else {

            delay=7125-drawingDuration;
            duration=drawingDuration;

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        for(int i=0;i<8;++i) {

            if(phase2sub2_iconType[i]==Phase2Sub2_IconTypes.UNKNOWN) {

                continue;

            }

            switch(phase2sub2_iconType[i]) {

                case Phase2Sub2_IconTypes.FAN: {

                    currentProperties=accessory.Data.GetDefaultDrawProperties();

                    currentProperties.Scale=new(40);
                    currentProperties.Radian=float.Pi/2;
                    currentProperties.Owner=accessory.Data.PartyList[i];
                    currentProperties.TargetResolvePattern=PositionResolvePatternEnum.PlayerNearestOrder;
                    currentProperties.TargetOrderIndex=1;
                    currentProperties.FadeCentrePosition=rotatePosition(PHASE2_SUB2_RAW_TOWER_POSITION,ARENA_CENTER,Math.PI/4*(index-1));
                    currentProperties.FadeDistance=PHASE2_SUB2_TOWER_RADIUS;
                    currentProperties.FadeMode=FadeMode.OmenCentre;
                    currentProperties.Color=accessory.Data.DefaultDangerColor;
                    currentProperties.Delay=delay;
                    currentProperties.DestoryAt=duration;

                    accessory.Method.SendDraw(getDrawModeEnum(DrawTypeEnum.Fan),DrawTypeEnum.Fan,currentProperties);

                    break;

                }

                case Phase2Sub2_IconTypes.SPREAD: {

                    if(phase2sub2_spellwaveOnly) {

                        break;

                    }

                    currentProperties=accessory.Data.GetDefaultDrawProperties();

                    currentProperties.Scale=new(5);
                    currentProperties.Owner=accessory.Data.PartyList[i];
                    currentProperties.FadeCentrePosition=rotatePosition(PHASE2_SUB2_RAW_TOWER_POSITION,ARENA_CENTER,Math.PI/4*(index-1));
                    currentProperties.FadeDistance=PHASE2_SUB2_TOWER_RADIUS;
                    currentProperties.FadeMode=FadeMode.OmenCentre;
                    currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(1);
                    currentProperties.Delay=delay;
                    currentProperties.DestoryAt=duration;

                    accessory.Method.SendDraw(getDrawModeEnum(DrawTypeEnum.Circle),DrawTypeEnum.Circle,currentProperties);

                    break;

                }

                case Phase2Sub2_IconTypes.STACK: {

                    if(phase2sub2_spellwaveOnly) {

                        break;

                    }

                    currentProperties=accessory.Data.GetDefaultDrawProperties();

                    currentProperties.Scale=new(5);
                    currentProperties.Owner=accessory.Data.PartyList[i];
                    currentProperties.FadeCentrePosition=rotatePosition(PHASE2_SUB2_RAW_TOWER_POSITION,ARENA_CENTER,Math.PI/4*(index-1));
                    currentProperties.FadeDistance=PHASE2_SUB2_TOWER_RADIUS;
                    currentProperties.FadeMode=FadeMode.OmenCentre;
                    currentProperties.Color=colourOfDirectionIndicators.V4.WithW(1);
                    currentProperties.Delay=delay;
                    currentProperties.DestoryAt=duration;

                    accessory.Method.SendDraw(getDrawModeEnum(DrawTypeEnum.Circle),DrawTypeEnum.Circle,currentProperties);

                    break;

                }

                case Phase2Sub2_IconTypes.UNKNOWN: {

                    break;

                }

                default: {

                    break;

                }

            }

        }

    }

    [ScriptMethod(name:"P2 遗弃末世 (塔辅助线)",
        eventType:EventTypeEnum.EnvControl,
        eventCondition:["Flag:regex:^(1|2)$"])]

    public void P2_遗弃末世_塔辅助线(Event @event,ScriptAccessory accessory) {

        if(!phase2sub2_towerAuxiliaryLines) {

            return;

        }

        if(majorPhase!=2) {

            return;

        }

        if(phase!=2) {

            return;

        }

        if(!convertStringToSignedInteger(@event["Index"], out var index)) {

            return;

        }

        if(index<1||index>8) {

            return;

        }

        Vector3 towerCenter=PHASE2_SUB2_RAW_TOWER_POSITION;
        Vector3 relativeNorth=new Vector3(PHASE2_SUB2_RAW_TOWER_POSITION.X,PHASE2_SUB2_RAW_TOWER_POSITION.Y,PHASE2_SUB2_RAW_TOWER_POSITION.Z+1);
        Vector3 relativeWest=new Vector3(PHASE2_SUB2_RAW_TOWER_POSITION.X+1,PHASE2_SUB2_RAW_TOWER_POSITION.Y,PHASE2_SUB2_RAW_TOWER_POSITION.Z);

        towerCenter=rotatePosition(towerCenter,ARENA_CENTER,Math.PI/4*(index-1));
        relativeNorth=rotatePosition(relativeNorth,ARENA_CENTER,Math.PI/4*(index-1));
        relativeWest=rotatePosition(relativeWest,ARENA_CENTER,Math.PI/4*(index-1));

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        currentProperties.Scale=new(0.125f,8);
        currentProperties.Position=towerCenter;
        currentProperties.TargetPosition=relativeNorth;
        currentProperties.Color=phase2sub2_colourOfAuxiliaryLines.V4.WithW(1);
        currentProperties.DestoryAt=10000;

        accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Straight,currentProperties);

        currentProperties=accessory.Data.GetDefaultDrawProperties();

        currentProperties.Scale=new(0.125f,8);
        currentProperties.Position=towerCenter;
        currentProperties.TargetPosition=relativeWest;
        currentProperties.Color=phase2sub2_colourOfAuxiliaryLines.V4.WithW(1);
        currentProperties.DestoryAt=10000;

        accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Straight,currentProperties);

    }

    [ScriptMethod(name:"P2 遗弃末世 过去终结与未来终结 (引导范围)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:regex:^(47826|47827)$"])]

    public void P2_遗弃末世_过去终结与未来终结_引导范围(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=2) {

            return;

        }

        if(phase!=2) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["SourceId"],out var sourceId)) {

            return;

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        for(uint i=1;i<=4;++i) {

            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(5);
            currentProperties.Owner=sourceId;
            currentProperties.CentreResolvePattern=PositionResolvePatternEnum.PlayerNearestOrder;
            currentProperties.CentreOrderIndex=i;
            currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(1);
            currentProperties.DestoryAt=6750;

            accessory.Method.SendDraw(getDrawModeEnum(DrawTypeEnum.Circle),DrawTypeEnum.Circle,currentProperties);

        }

    }

    [ScriptMethod(name:"P2 遗弃末世 过去终结与未来终结 (数据收集)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:regex:^(47826|47827)$"],
        userControl:false)]

    public void P2_遗弃末世_过去终结与未来终结_数据收集(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=2) {

            return;

        }

        if(phase!=2) {

            return;

        }

        if(string.Equals(@event["ActionId"],"47826")) {

            isFuturesEnd=true;

        }

        if(string.Equals(@event["ActionId"],"47827")) {

            isFuturesEnd=false;

        }

        if(enableDebugLogging) {

            accessory.Log.Debug($"""
                                 isFuturesEnd={isFuturesEnd}
                                 """);

        }

    }

    [ScriptMethod(name:"P2 遗弃末世 消灭之脚 (引导范围)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:regex:^(47826|47827)$"])]

    public void P2_遗弃末世_消灭之脚_引导范围(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=2) {

            return;

        }

        if(phase!=2) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["SourceId"],out var sourceId)) {

            return;

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        currentProperties.Scale=new(100);
        currentProperties.Radian=float.Pi;
        currentProperties.Owner=sourceId;
        currentProperties.TargetObject=accessory.Data.Me;
        currentProperties.Color=accessory.Data.DefaultDangerColor;
        currentProperties.Delay=6750;
        currentProperties.DestoryAt=6500;

        if(string.Equals(@event["ActionId"],"47826")) {

            currentProperties.Rotation=0;

        }

        if(string.Equals(@event["ActionId"],"47827")) {

            currentProperties.Rotation=float.Pi;

        }

        accessory.Method.SendDraw(getDrawModeEnum(DrawTypeEnum.Fan),DrawTypeEnum.Fan,currentProperties);

    }

    [ScriptMethod(name:"P2 遗弃末世 消灭之脚 (范围)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:regex:^(47836|47837)$"])]

    public void P2_遗弃末世_消灭之脚_范围(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=2) {

            return;

        }

        if(phase!=2) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["SourceId"],out var sourceId)) {

            return;

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        currentProperties.Scale=new(100);
        currentProperties.Radian=float.Pi;
        currentProperties.Owner=sourceId;
        currentProperties.Color=accessory.Data.DefaultDangerColor;
        currentProperties.DestoryAt=5000;

        if(string.Equals(@event["ActionId"],"47836")) {

            currentProperties.Rotation=0;

        }

        if(string.Equals(@event["ActionId"],"47837")) {

            currentProperties.Rotation=float.Pi;

        }

        accessory.Method.SendDraw(getDrawModeEnum(DrawTypeEnum.Fan),DrawTypeEnum.Fan,currentProperties);

    }

    [ScriptMethod(name:"P2 遗弃末世后 制裁之光 (阶段控制)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:47805"],
        userControl:false)]

    public void P2_遗弃末世后_制裁之光_阶段控制(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=2) {

            return;

        }

        if(phase!=2) {

            return;

        }

        phase=3;

        if(enableDebugLogging) {

            accessory.Log.Debug($"majorPhase={majorPhase}\nphase={phase}");

        }

    }

    [ScriptMethod(name:"P2 遗弃末世后 破坏之翼 (单翼范围)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:regex:^(47821|47822)$"])]

    public void P2_遗弃末世后_破坏之翼_单翼范围(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=2) {

            return;

        }

        if(phase!=3) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["SourceId"],out var sourceId)) {

            return;

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        currentProperties.Scale=new(40,80);
        currentProperties.Owner=sourceId;
        currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(1);
        currentProperties.DestoryAt=4000;

        if(string.Equals(@event["ActionId"],"47821")) {

            currentProperties.Rotation=float.Pi/2;

        }

        if(string.Equals(@event["ActionId"],"47822")) {

            currentProperties.Rotation=-float.Pi/2;

        }

        accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Rect,currentProperties);

    }

    [ScriptMethod(name:"P2 遗弃末世后 破坏之翼 (双翼范围)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:50311"])]

    public void P2_遗弃末世后_破坏之翼_双翼范围(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=2) {

            return;

        }

        if(phase!=3) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["SourceId"],out var sourceId)) {

            return;

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        currentProperties.Scale=new(7);
        currentProperties.Owner=sourceId;
        currentProperties.CentreResolvePattern=PositionResolvePatternEnum.PlayerNearestOrder;
        currentProperties.CentreOrderIndex=1;
        currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(1);
        currentProperties.DestoryAt=4000;

        accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Circle,currentProperties);

        currentProperties=accessory.Data.GetDefaultDrawProperties();

        currentProperties.Scale=new(7);
        currentProperties.Owner=sourceId;
        currentProperties.CentreResolvePattern=PositionResolvePatternEnum.PlayerFarestOrder;
        currentProperties.CentreOrderIndex=1;
        currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(1);
        currentProperties.DestoryAt=4000;

        accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Circle,currentProperties);

    }

    [ScriptMethod(name:"P2 遗弃末世后 异三角 (数据收集与范围)",
        eventType:EventTypeEnum.ObjectChanged,
        eventCondition:["DataId:regex:^(2015154|2015155)$"])]

    public void P2_遗弃末世后_异三角_数据收集与范围(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=2) {

            return;

        }

        if(phase!=3) {

            return;

        }

        if(!string.Equals(@event["Operate"],"Add")) {

            return;

        }

        if(phase2sub3_trineCounter>=7) {

            return;

        }

        Vector3 sourcePosition=ARENA_CENTER;

        try {

            sourcePosition=JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);

        } catch(Exception e) {

            accessory.Log.Error("SourcePosition deserialization failed.");

            return;

        }

        int delay=-1;
        int duration1=-1;
        int duration2=-1;

        lock(PHASE2_SUB3_TRINE_COUNTER_LOCK) {

            Interlocked.Increment(ref phase2sub3_trineCounter);

            if(phase2sub3_trineCounter<=3) {

                delay=0;
                duration1=9875;
                duration2=2000;

            }

            else {

                delay=7875;
                duration1=2000;
                duration2=2000;

            }

        }

        Vector3 position=sourcePosition;

        if(string.Equals(@event["DataId"],"2015154")) {

            position=new Vector3(sourcePosition.X-3,sourcePosition.Y,sourcePosition.Z+5);

        }

        if(string.Equals(@event["DataId"],"2015155")) {

            position=new Vector3(sourcePosition.X+3,sourcePosition.Y,sourcePosition.Z+5);

        }

        if(string.Equals(@event["DataId"],"2015154")
           &&
           Vector3.Distance(sourcePosition,new Vector3(88.45f,0,90))<COMMON_DEVIATION) {

            position=new Vector3(sourcePosition.X+3,sourcePosition.Y,sourcePosition.Z+5); // To be changed in the future.

        }

        if(Vector3.Distance(position,sourcePosition)<COMMON_DEVIATION) {

            return;

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        for(int i=0;i<3;++i) {

            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(6);
            currentProperties.Position=rotatePosition(position,sourcePosition,Math.PI/3*2*i);
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.Delay=delay;
            currentProperties.DestoryAt=duration1;

            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Circle,currentProperties);

            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(6);
            currentProperties.Position=rotatePosition(position,sourcePosition,Math.PI/3*2*i);
            currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(1);
            currentProperties.Delay=delay+duration1;
            currentProperties.DestoryAt=duration2;

            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Circle,currentProperties);

        }

    }

    // ----- LPDU 异三角：https://raidplan.io/plan/apkh6ytq72w8pt3v（Party N CW, Tanks NW CCW）-----
    // 7 个三角按 3/1/3 三组落地，各组落地约 11.8s 后在三个尖角爆 6m 圆（47840）；场中那个总在第 3 组
    // 外圈 6 个位置固定：第 k 个（从北顺时针 0~5）质心在方位 60k+10.9°、距场中 15.28m，尖角在 (60k°,10m)、(60k°,20m)、(60k+30°,17.32m)
    //   西北那个外观朝向和爆炸对不上，所以外圈按位置算，不看 DataId；场中那个看 DataId：2015154 尖朝东，2015155 尖朝西
    // 全员先在场中，破坏之翼半场刀（47821/47822）时站安全半场；第 1 组爆炸后：
    //   人群进「从北开始顺时针」第一个第 1 组三角，坦克进「从西北开始逆时针」第一个第 1 组三角
    //   ST 远：坦克三角的场边；MT 近：场中三角的缺口（两个尖角之间、距场中 7.5m），
    //   选两侧外圈三角都不在第 3 组、离坦克三角最近的缺口，旁边有第 2 组三角就先在坦克三角内侧等它爆完
    // 远近死刑 47823（7m）在第 3 组爆炸时点名最近/最远
    private readonly object _trine锁=new();
    private readonly List<(Vector3 pos,uint dataId)> _trines=new();
    private long _trine落地=0;
    private float? _trine安全半场=null; // 半场刀安全侧的游戏朝向（弧度）

    // 相对第 1 组落地（日志实测）
    private const int TRINE_第1组爆炸=11785,TRINE_第2组爆炸=13835,TRINE_死刑=16370;

    private static Vector3 Trine点(float bearingDegrees,float radius) {

        float rad=bearingDegrees*MathF.PI/180;
        return new Vector3(100+radius*MathF.Sin(rad),0,100-radius*MathF.Cos(rad));

    }

    // 47821 打 Boss 面向 +90°、47822 打 -90°（同上面单翼范围的画法），安全侧取反
    [ScriptMethod(name:"P2 异三角 破坏之翼 (数据收集)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:regex:^(47821|47822)$"],
        userControl:false)]

    public void P2_异三角_破坏之翼_数据收集(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=2||phase!=3) {

            return;

        }

        lock(_trine锁) {

            _trine安全半场=@event.SourceRotation()+(@event.ActionId()==47821?-1:1)*MathF.PI/2;

        }

    }

    [ScriptMethod(name:"P2 异三角 (指路)",
        eventType:EventTypeEnum.ObjectChanged,
        eventCondition:["DataId:regex:^(2015154|2015155)$","Operate:Add"])]

    public void P2_异三角_指路(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=2||phase!=3) {

            return;

        }

        List<(Vector3 pos,uint dataId)> trines;
        long landed;
        float? safeSide;

        lock(_trine锁) {

            if(_trines.Count>=7) {

                return;

            }

            if(_trines.Count==0) {

                _trine落地=Environment.TickCount64;

            }

            _trines.Add((@event.SourcePosition(),@event.DataId()));

            if(_trines.Count<7) {

                return;

            }

            trines=[.._trines];
            landed=_trine落地;
            safeSide=_trine安全半场;

        }

        int ringOf(Vector3 p) {

            float bearing=MathF.Atan2(p.X-100,100-p.Z)*180/MathF.PI;
            return ((int)MathF.Round((bearing-10.9f)/60)%6+6)%6;

        }

        bool isCenter(Vector3 p)=>Vector3.Distance(p,ARENA_CENTER)<3;

        var set1=trines.Take(3).Where(t=>!isCenter(t.pos)).Select(t=>ringOf(t.pos)).ToList();
        var set2=trines.Skip(3).Take(1).Where(t=>!isCenter(t.pos)).Select(t=>ringOf(t.pos)).ToList();
        var set3=trines.Skip(4).Where(t=>!isCenter(t.pos)).Select(t=>ringOf(t.pos)).ToList();
        var center=trines.FirstOrDefault(t=>isCenter(t.pos));

        if(set1.Count!=3||center.dataId==0) {

            accessory.Log.Debug($"P2 异三角：布局不认识，不指路。{string.Join(" ",trines)}");
            return;

        }

        int partyRing=set1.Min();
        int tankRing=set1.Max();
        float tankBearing=60*tankRing+10.9f;

        // 场中三角的缺口在尖角对面：尖朝东（2015154）尖角 90/210/330°，缺口 270/30/150°
        int[] gaps=center.dataId==2015154?[270,30,150]:[90,210,330];
        int[] flanks(int gap)=>[(gap-30)/60%6,(gap+30)/60%6];
        int gap=gaps.Where(g=>!flanks(g).Any(set3.Contains))
            .OrderBy(g=>MathF.Abs(((g-tankBearing)%360+540)%360-180))
            .First();
        bool nearWaits=set2.Any(flanks(gap).Contains);

        Vector3 mid=safeSide is { } a?new Vector3(100+3*MathF.Sin(a),0,100+3*MathF.Cos(a)):ARENA_CENTER;
        Vector3 partySpot=Trine点(60*partyRing+10.9f,15.28f);
        Vector3 farSpot=Trine点(tankBearing,19);
        Vector3 nearWait=Trine点(tankBearing,11);
        Vector3 nearSpot=Trine点(gap,7.5f);

        long now=Environment.TickCount64;
        uint until(int ms)=>(uint)Math.Max(0,landed+ms-now);
        uint boom1=until(TRINE_第1组爆炸),boom2=until(TRINE_第2组爆炸),end=until(TRINE_死刑)+300;

        accessory.Log.Debug($"P2 异三角：第1组={string.Join(",",set1)} 第2组={string.Join(",",set2)} 第3组={string.Join(",",set3)}+场中{center.dataId}，人群={partyRing} 坦克={tankRing} 近死刑缺口={gap}°{(nearWaits?"（先等第2组）":"")}");

        int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);

        for(int i=0;i<accessory.Data.PartyList.Count&&i<8;++i) {

            if(!Debugging&&i!=myIndex) {

                continue;

            }

            Vector3 target=i switch { 0 => nearWaits?nearWait:nearSpot, 1 => farSpot, _ => partySpot };

            var dp=accessory.WaypointDp(mid,boom1,0,$"P2_异三角_场中_{i}");
            dp.Owner=GuidanceOwner(accessory,i);
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,dp);

            dp=accessory.WaypointFromToDp(mid,target,boom1,0,$"P2_异三角_预览_{i}",new Vector4(1,1,1,1));
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,dp);

            uint toTarget=i==0&&nearWaits?boom2:end;
            dp=accessory.WaypointDp(target,toTarget-boom1,boom1,$"P2_异三角_三角_{i}");
            dp.Owner=GuidanceOwner(accessory,i);
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,dp);

            if(i==0&&nearWaits) {

                dp=accessory.WaypointDp(nearSpot,end-boom2,boom2,$"P2_异三角_缺口_{i}");
                dp.Owner=GuidanceOwner(accessory,i);
                accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,dp);

            }

        }

    }

    #endregion

    #endregion

    #region P3 绘制（Cicero 灵视）

    #region Major_Phase_3

    [ScriptMethod(name:"P3 (阶段控制)",
        eventType:EventTypeEnum.PlayActionTimeline,
        eventCondition:["Id:3218"],
        userControl:false)]

    public void P3_阶段控制(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=2) {

            return;

        }

        if(!string.Equals(@event["SourceDataId"],"19504")) {

            return;

        }

        accessory.Method.RemoveDraw(".*");

        majorPhase=3;
        phase=1;

        if(enableDebugLogging) {

            accessory.Log.Debug($"majorPhase={majorPhase}\nphase={phase}");

        }

    }

    #endregion

    #endregion

    #region P3 绘制与指路（Usami）

    private const string DebugVersion = "a";
    private int _runId = 0;
    private static readonly List<string> Role = ["MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4"];
    private static readonly Vector3 Center = new Vector3(100, 0, 100);
    private static readonly Vector3 CrystalBasePos = new Vector3(109.9f, 0, 109.9f);
    private static readonly Vector3 BlackHoleBasePos = new Vector3(100f, 0, 83f);
    private UDMP3Params _udmP3Param = new UDMP3Params();
    private PriorityDict _pd = new PriorityDict();

    // LPDU 一运站位距离（以风水晶为北）：风/火/水水晶外侧要躲开场中艾克斯迪司 14.8m 的暴雷钢铁
    private const float 一运外圈距离 = 15.5f;
    private const float 一运远程距离 = 16f;

    // LPDU 二运接线站位：卡奥斯（全程场中）最大近战、黑洞方向顺时针转，把线拉成 U 形；最后一根线站场边让激光朝外
    private const float 接线站位半径 = 6f;
    private const float 接线站位顺时针角度 = 65f;
    private const float 最后一根线站位半径 = 17f;
    private const float 最后一根线顺时针角度 = 45f;

    private void Init_Usami(ScriptAccessory sa)
    {
        _runId++;
        sa.DebugMsg($"脚本 {Name} v{Version}{DebugVersion} 完成初始化，_runId {_runId}");

        _pd.Init(sa, "初始化");

        _udmP3Param.Reset(sa);
        sa.Method.RemoveDraw(".*");
        sa.Method.ClearFrameworkUpdateAction(this);
    }

    /// <summary>需要指路的人：Debug 时全队，否则只有自己</summary>
    private static IEnumerable<int> 指路对象(ScriptAccessory sa)
    {
        var myIndex = sa.GetMyIndex();
        for (var i = 0; i < Math.Min(8, sa.Data.PartyList.Count); i++)
            if (Debugging || i == myIndex)
                yield return i;
    }

    /// <summary>全队指路时只有自己那份播报</summary>
    private static bool 是自己(ScriptAccessory sa, int idx) => idx == sa.GetMyIndex();

    [ScriptMethod(name: "———————— 《P3》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"])]
    public void P3_分割线(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "P3_分P_重构", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47842"],
        userControl: Debugging)]
    public void P3_分P_重构(Event ev, ScriptAccessory sa)
    {
        _udmP3Param.当前阶段 = 3000;
        sa.DebugMsg($"当前阶段为：P3 重构 {_udmP3Param.当前阶段}");
    }

    [ScriptMethod(name: "*P3B_重构时恢复凯夫卡透明度", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47842)$"], userControl: Debugging)]
    public void P3B_重构时恢复凯夫卡透明度(Event ev, ScriptAccessory sa)
    {
        var obj = sa.GetById(ev.SourceId);
        sa.AlphaModify(obj, 1f, currentAlpha => currentAlpha <= 0.8f);
    }

    [ScriptMethod(name: "P3_获得艾克斯迪司ObjId", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:49891"],
        userControl: Debugging)]
    public void P3_获得艾克斯迪司ObjId(Event ev, ScriptAccessory sa)
    {
        _udmP3Param.ObjectId_艾克斯迪司 = ev.SourceId;
        sa.DebugMsg($"{_udmP3Param.ObjectId_艾克斯迪司:X8}");

        // 顺便恢复透明度
        var obj = sa.GetById(ev.SourceId);
        sa.AlphaModify(obj, 1f, currentAlpha => currentAlpha <= 0.8f);
    }

    [ScriptMethod(name: "P3_获得卡奥斯ObjId", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:49890"],
        userControl: Debugging)]
    public void P3_获得卡奥斯ObjId(Event ev, ScriptAccessory sa)
    {
        _udmP3Param.ObjectId_卡奥斯 = ev.SourceId;
        sa.DebugMsg($"{_udmP3Param.ObjectId_卡奥斯:X8}");

        // 顺便恢复透明度
        var obj = sa.GetById(ev.SourceId);
        sa.AlphaModify(obj, 1f, currentAlpha => currentAlpha <= 0.8f);
    }

    [ScriptMethod(name: "*P3_防火墙_屏蔽无效目标", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:49890"],
        userControl: true)]
    public void P3_防火墙_屏蔽无效目标(Event ev, ScriptAccessory sa)
    {
        if (!p3FirewallBlockInvalidTargets) return;
        if (_udmP3Param.防火墙已开启) return;

        _udmP3Param.防火墙已开启 = true;
        _udmP3Param.判断防火墙目标Framework = sa.Method.RegistFrameworkUpdateAction(Action);
        const uint chaos_buff = 4192;
        const uint exdeath_buff = 4194;

        const int 应战卡奥斯 = 1;
        const int 应战艾克斯迪司 = 2;
        const int 应战双人 = 3;
        const int 没有应战 = 4;

        void Action()
        {
            var myObject = sa.Data.MyObject;
            if (myObject is null) return;

            int currentState = (myObject.HasStatus(chaos_buff), myObject.HasStatus(exdeath_buff)) switch
            {
                (true, false) => 应战卡奥斯,
                (false, true) => 应战艾克斯迪司,
                (false, false) => 没有应战,
                _ => 应战双人
            };

            if (sa.GetMyIndex() <= 1)
            {
                // 坦克职业不参与
                currentState = 应战双人;
            }

            if (currentState == _udmP3Param.上一次防火墙状态 && _udmP3Param.上一次防火墙状态 != 0) return;
            _udmP3Param.上一次防火墙状态 = currentState;

            switch (currentState)
            {
                case 应战卡奥斯:
                    sa.SetTargetable(sa.GetById(_udmP3Param.ObjectId_卡奥斯), true);
                    sa.SetTargetable(sa.GetById(_udmP3Param.ObjectId_艾克斯迪司), false);
                    break;
                case 应战艾克斯迪司:
                    sa.SetTargetable(sa.GetById(_udmP3Param.ObjectId_卡奥斯), false);
                    sa.SetTargetable(sa.GetById(_udmP3Param.ObjectId_艾克斯迪司), true);
                    break;
                default:
                    sa.SetTargetable(sa.GetById(_udmP3Param.ObjectId_卡奥斯), true);
                    sa.SetTargetable(sa.GetById(_udmP3Param.ObjectId_艾克斯迪司), true);
                    break;
            };
        }
    }

    [ScriptMethod(name: "P3_狂暴前解除防火墙", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:47878", "TargetIndex:1"], userControl: Debugging)]
    public void P3_狂暴前解除防火墙(Event ev, ScriptAccessory sa)
    {
        sa.SetTargetable(sa.GetById(_udmP3Param.ObjectId_卡奥斯), true);
        sa.SetTargetable(sa.GetById(_udmP3Param.ObjectId_艾克斯迪司), true);
        _udmP3Param.防火墙已开启 = false;
    }

    [ScriptMethod(name: "P3_根据Buff判断MTST", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(419[24])$"],
        userControl: Debugging)]
    public void P3_根据Buff判断MTST(Event ev, ScriptAccessory sa)
    {
        const uint chaos_buff = 4192;
        const uint exdeath_buff = 4194;

        var idx = sa.GetPlayerIdIndex((uint)ev.TargetId);
        if (idx is < 0 or >= 2) return;

        var statusId = ev.StatusId;

        _udmP3Param.拉艾克斯迪司的是MT = idx switch
        {
            0 => statusId == exdeath_buff,
            1 => statusId == chaos_buff
        };
        sa.DebugMsg(
            $"{sa.GetPlayerJobByIndex(idx)} 获得 {statusId}，拉艾克斯迪司的是 {(_udmP3Param.拉艾克斯迪司的是MT ? "MT" : "ST")}");
    }

    #region P3A 深层痛楚 通用部分

    [ScriptMethod(name: "=============《P3A 深层痛楚》=============", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P3A_分割线(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "P3A_一运分P_深层痛楚", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47858"],
        userControl: Debugging)]
    public void P3A_一运分P_深层痛楚(Event ev, ScriptAccessory sa)
    {
        _pd.Init(sa, "P3一运");
        _pd.AddPriorities([1, 2, 3, 4, 5, 6, 7, 8]);
        _udmP3Param.当前阶段 = 3100;
        sa.DebugMsg($"当前阶段为：P3 一运 深层痛楚 {_udmP3Param.当前阶段}");
    }

    [ScriptMethod(name: "P3A_状态添加记录", eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:regex:^(160[0123])$", "SourceId:E0000000"],
        userControl: Debugging)]
    public void P3A_状态添加(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.当前阶段 != 3100) return;
        const uint 混沌之炎 = 1600;
        const uint 混沌之水 = 1601;
        const uint 混沌之风 = 1602;
        const uint 混沌之逆风 = 1603;

        lock (_pd)
        {
            var statusId = ev.StatusId;
            var playerIdx = sa.GetPlayerIdIndex((uint)ev.TargetId);
            var priVal = ev.StatusId switch
            {
                混沌之炎 => 100,
                混沌之水 => 200,
                混沌之风 => 10,
                混沌之逆风 => 20,
                _ => 0
            };

            _pd.AddPriority(playerIdx, priVal);
            _udmP3Param.一运状态记录次数++;
            sa.DebugMsg($"记录到状态 {statusId} / {priVal} 于角色 {Role[playerIdx]}");

            if (_udmP3Param.一运状态记录完毕())
                _udmP3Param.一运状态记录.Set();

            if (statusId != 混沌之炎) return;
            var statusTime = ev.DurationMilliseconds();
            _udmP3Param.是长火 = statusTime > 30000;
        }
    }

    [ScriptMethod(name: "P3A_水晶生成记录", eventType: EventTypeEnum.ObjectChanged,
        eventCondition: ["Operate:Add", "DataId:regex:^(201529[012])$"],
        userControl: Debugging)]
    public void P3A_水晶生成记录(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.当前阶段 != 3100) return;
        const uint 火水晶 = 2015290;
        const uint 水水晶 = 2015291;
        const uint 风水晶 = 2015292;

        var region = ev.SourcePosition.GetRadian(Center).RadianToRegion(4);
        var dataId = ev.DataId();

        switch (dataId)
        {
            case 火水晶:
                _udmP3Param.火水晶方位 = region;
                break;
            case 水水晶:
                _udmP3Param.水水晶方位 = region;
                break;
            case 风水晶:
                _udmP3Param.风水晶方位 = region;
                break;
        }
        sa.DebugMsg($"记录到水晶 {dataId} 于方位 {region}");

        if (!_udmP3Param.水晶状态记录完毕()) return;
        _udmP3Param.无水晶方位 = 6 - _udmP3Param.火水晶方位 - _udmP3Param.水水晶方位 - _udmP3Param.风水晶方位;
        _udmP3Param.一运水晶记录.Set();
    }

    [ScriptMethod(name: "P3A_暴雷死刑范围", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:47881"], userControl: true)]
    public void P3A_暴雷死刑范围(Event ev, ScriptAccessory sa)
    {
        var dp = sa.DrawCircle(ev.SourceId, 0, 8000, $"靠近死刑", 5f, sa.Data.DefaultDangerColor.WithW(2f), draw: false);
        dp.SetOwnersDistanceOrder(true, 1);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "P3A_暴雷死刑目标连线", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:47881"], userControl: true)]
    public void P3A_暴雷死刑目标连线(Event ev, ScriptAccessory sa)
    {
        var color = new Vector4(0.1f, 1f, 1f, 1f);
        var dp = sa.DrawLine(ev.SourceId, 0, 0, 8000, $"艾克斯迪司与死刑目标连线", 0, 1, 40, color, byY: true, draw: false);
        dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
        dp.TargetOrderIndex = 1;
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, dp);
    }

    // 47881 暴雷死刑：最近目标两段。谁吃、要不要无敌全吃按队伍减伤表，所以只提示不指路
    [ScriptMethod(name: "P3A_暴雷死刑通用提示", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:47881"], userControl: true)]
    public void P3A_暴雷死刑通用提示(Event ev, ScriptAccessory sa)
    {
        sa.Method.TextInfo($"艾克斯迪司 靠近死刑", 3000);
        sa.Method.TTS($"艾克斯迪司 靠近死刑", 3);
    }

    [ScriptMethod(name: "P3A_经纬聚爆", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47869|47870)$"], userControl: true)]
    public void P3A_经纬聚爆第一段(Event ev, ScriptAccessory sa)
    {
        const uint 纬度聚爆 = 47870;    // 先打左右
        const uint 经度聚爆 = 47869;    // 先打上下
        _udmP3Param.绘制经纬聚爆范围 = true;
        _udmP3Param.经纬聚爆第二段绘制完毕 = false;
        _udmP3Param.经纬聚爆先打左右 = ev.ActionId == 纬度聚爆;
        _udmP3Param.ObjectId_卡奥斯 = ev.SourceId;

        // 第一轮
        sa.DrawFan(ev.SourceId, 0, 10000, $"第一轮前左", 90f.DegToRad(), (_udmP3Param.经纬聚爆先打左右 ? 90f : 0f).DegToRad(), 60, 0, sa.Data.DefaultDangerColor.WithW(2f));
        sa.DrawFan(ev.SourceId, 0, 10000, $"第一轮后右", 90f.DegToRad(), (_udmP3Param.经纬聚爆先打左右 ? -90f : 180f).DegToRad(), 60, 0, sa.Data.DefaultDangerColor.WithW(2f));
    }

    [ScriptMethod(name: "P3A_经纬聚爆第二段与删除绘图", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:regex:^(47871)$"], suppress: 500, userControl: true)]
    public void P3A_经纬聚爆第二段与删除绘图(Event ev, ScriptAccessory sa)
    {
        if (!_udmP3Param.绘制经纬聚爆范围) return;

        if (_udmP3Param.经纬聚爆第二段绘制完毕)
            sa.Method.RemoveDraw($"第二轮[前左|后右]");
        else
        {
            sa.Method.RemoveDraw($"第一轮[前左|后右]");
            _udmP3Param.经纬聚爆第二段绘制完毕 = true;
            sa.DrawFan(_udmP3Param.ObjectId_卡奥斯, 0, 10000, $"第二轮前左", 90f.DegToRad(), (!_udmP3Param.经纬聚爆先打左右 ? 90f : 0f).DegToRad(), 60, 0, sa.Data.DefaultDangerColor.WithW(2f));
            sa.DrawFan(_udmP3Param.ObjectId_卡奥斯, 0, 10000, $"第二轮后右", 90f.DegToRad(), (!_udmP3Param.经纬聚爆先打左右 ? -90f : 180f).DegToRad(), 60, 0, sa.Data.DefaultDangerColor.WithW(2f));
        }
    }

    #region P3A 深层痛楚 范围与指路（Cicero 灵视）

    [ScriptMethod(name:"P3 深层痛楚 (阶段控制)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:47858"],
        userControl:false)]

    public void P3_深层痛楚_阶段控制(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=3) {

            return;

        }

        if(phase!=1) {

            return;

        }

        phase=2;

        if(enableDebugLogging) {

            accessory.Log.Debug($"majorPhase={majorPhase}\nphase={phase}");

        }

    }

    [ScriptMethod(name:"P3 深层痛楚 水晶 (数据收集)",
        eventType:EventTypeEnum.ObjectChanged,
        eventCondition:["DataId:regex:^(2015290|2015291|2015292)$"],
        userControl:false)]

    public void P3_深层痛楚_水晶_数据收集(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=3) {

            return;

        }

        if(phase!=2) {

            return;

        }

        if(!string.Equals(@event["Operate"],"Add")) {

            return;

        }

        Vector3 sourcePosition=ARENA_CENTER;

        try {

            sourcePosition=JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);

        } catch(Exception e) {

            accessory.Log.Error("SourcePosition deserialization failed.");

            return;

        }

        if(string.Equals(@event["DataId"],"2015290")) {

            if(Vector3.Distance(phase3sub2_fireCrystalPosition,ARENA_CENTER)>COMMON_DEVIATION) {

                return;

            }

            phase3sub2_fireCrystalPosition=sourcePosition;
            phase3sub2_fireCrystalSemaphore.Set();

            if(enableDebugLogging) {

                accessory.Log.Debug($"phase3sub2_fireCrystalPosition={phase3sub2_fireCrystalPosition}");

            }

        }

        if(string.Equals(@event["DataId"],"2015291")) {

            if(Vector3.Distance(phase3sub2_waterCrystalPosition,ARENA_CENTER)>COMMON_DEVIATION) {

                return;

            }

            phase3sub2_waterCrystalPosition=sourcePosition;
            phase3sub2_waterCrystalSemaphore.Set();

            if(enableDebugLogging) {

                accessory.Log.Debug($"phase3sub2_waterCrystalPosition={phase3sub2_waterCrystalPosition}");

            }

        }

    }

    [ScriptMethod(name:"P3 深层痛楚 混沌之炎 (范围)",
        eventType:EventTypeEnum.StatusAdd,
        eventCondition:["StatusID:1600"])]

    public void P3_深层痛楚_混沌之炎_范围(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=3) {

            return;

        }

        if(phase!=2) {

            return;

        }

        if(string.Equals(@event["SourceId"],"00000000")) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        int durationMilliseconds=0;

        try {

            durationMilliseconds=JsonConvert.DeserializeObject<int>(@event["DurationMilliseconds"]);

        } catch(Exception e) {

            accessory.Log.Error("DurationMilliseconds deserialization failed.");

            return;

        }

        if(durationMilliseconds<=0||durationMilliseconds>MAXIMUM_DURATION) {

            return;

        }

        int standardDuration=0;

        if(durationMilliseconds<=32500) {

            standardDuration=6750;

        }

        else {

            standardDuration=4000;

        }

        int delay=0;
        int duration=0;

        if(durationMilliseconds<=standardDuration) {

            delay=0;
            duration=durationMilliseconds;

        }

        else {

            delay=durationMilliseconds-standardDuration;
            duration=standardDuration;

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        currentProperties.Name=$"P3_深层痛楚_混沌之炎_范围_{targetId}";
        currentProperties.Scale=new(5);
        currentProperties.Owner=targetId;
        currentProperties.Color=accessory.Data.DefaultDangerColor;
        currentProperties.Delay=delay;
        currentProperties.DestoryAt=duration;

        accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Circle,currentProperties);

    }

    [ScriptMethod(name:"P3 深层痛楚 烈焰 (范围)",
        eventType:EventTypeEnum.StatusAdd,
        eventCondition:["StatusID:1600","SourceId:E0000000"],
        suppress:COMMON_INTERVAL)]

    public void P3_深层痛楚_烈焰_范围(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=3) {

            return;

        }

        if(phase!=2) {

            return;

        }

        int durationMilliseconds=0;

        try {

            durationMilliseconds=JsonConvert.DeserializeObject<int>(@event["DurationMilliseconds"]);

        } catch(Exception e) {

            accessory.Log.Error("DurationMilliseconds deserialization failed.");

            return;

        }

        if(durationMilliseconds<=0||durationMilliseconds>MAXIMUM_DURATION) {

            return;

        }

        int standardDuration=0;

        if(durationMilliseconds<=32500) {

            standardDuration=6750;

        }

        else {

            standardDuration=4000;

        }

        int delay=0;
        int duration=0;

        if(durationMilliseconds<=standardDuration) {

            delay=0;
            duration=durationMilliseconds;

        }

        else {

            delay=durationMilliseconds-standardDuration;
            duration=standardDuration;

        }

        bool signalled=phase3sub2_fireCrystalSemaphore.WaitOne(COMMON_INTERVAL);

        if(!signalled) {

            return;

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        currentProperties.Name=$"P3_深层痛楚_烈焰_范围_2";
        currentProperties.Scale=new(10);
        currentProperties.InnerScale=new(5);
        currentProperties.Radian=float.Pi*2;
        currentProperties.Position=phase3sub2_fireCrystalPosition;
        currentProperties.CentreResolvePattern=PositionResolvePatternEnum.PlayerNearestOrder;
        currentProperties.CentreOrderIndex=2;
        currentProperties.Color=accessory.Data.DefaultDangerColor;
        currentProperties.Delay=delay;
        currentProperties.DestoryAt=duration+2875;

        accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Donut,currentProperties);

        currentProperties=accessory.Data.GetDefaultDrawProperties();

        currentProperties.Name=$"P3_深层痛楚_烈焰_范围_1";
        currentProperties.Scale=new(10);
        currentProperties.InnerScale=new(5);
        currentProperties.Radian=float.Pi*2;
        currentProperties.Position=phase3sub2_fireCrystalPosition;
        currentProperties.CentreResolvePattern=PositionResolvePatternEnum.PlayerNearestOrder;
        currentProperties.CentreOrderIndex=1;
        currentProperties.Color=accessory.Data.DefaultDangerColor;
        currentProperties.Delay=delay;
        currentProperties.DestoryAt=duration+2875;

        accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Donut,currentProperties);

    }

    [ScriptMethod(name:"P3 深层痛楚 混沌之炎与烈焰 (范围清除)",
        eventType:EventTypeEnum.StatusRemove,
        eventCondition:["StatusID:1600"],
        userControl:false)]

    public void P3_深层痛楚_混沌之炎与烈焰_范围清除(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=3) {

            return;

        }

        if(string.Equals(@event["SourceId"],"00000000")) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        accessory.Method.RemoveDraw($"P3_深层痛楚_混沌之炎_范围_{targetId}");

        lock(phase3sub2_fireCrystalSemaphore) {

            if(phase3sub2_infernoCounter>=1) {

                accessory.Method.RemoveDraw($"P3_深层痛楚_烈焰_范围_{phase3sub2_infernoCounter}");

                --phase3sub2_infernoCounter;

            }

        }

    }

    [ScriptMethod(name:"P3 深层痛楚 混沌之水 (范围)",
        eventType:EventTypeEnum.StatusAdd,
        eventCondition:["StatusID:1601"])]

    public void P3_深层痛楚_混沌之水_范围(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=3) {

            return;

        }

        if(phase!=2) {

            return;

        }

        if(string.Equals(@event["SourceId"],"00000000")) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        int durationMilliseconds=0;

        try {

            durationMilliseconds=JsonConvert.DeserializeObject<int>(@event["DurationMilliseconds"]);

        } catch(Exception e) {

            accessory.Log.Error("DurationMilliseconds deserialization failed.");

            return;

        }

        if(durationMilliseconds<=0||durationMilliseconds>MAXIMUM_DURATION) {

            return;

        }

        int standardDuration=0;

        if(durationMilliseconds<=32500) {

            standardDuration=6750;

        }

        else {

            standardDuration=4000;

        }

        int delay=0;
        int duration=0;

        if(durationMilliseconds<=standardDuration) {

            delay=0;
            duration=durationMilliseconds;

        }

        else {

            delay=durationMilliseconds-standardDuration;
            duration=standardDuration;

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        currentProperties.Name=$"P3_深层痛楚_混沌之水_范围_{targetId}";
        currentProperties.Scale=new(10);
        currentProperties.InnerScale=new(5);
        currentProperties.Radian=float.Pi*2;
        currentProperties.Owner=targetId;
        currentProperties.Color=accessory.Data.DefaultDangerColor;
        currentProperties.Delay=delay;
        currentProperties.DestoryAt=duration;

        accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Donut,currentProperties);

    }

    [ScriptMethod(name:"P3 深层痛楚 海啸 (范围)",
        eventType:EventTypeEnum.StatusAdd,
        eventCondition:["StatusID:1601","SourceId:E0000000"],
        suppress:COMMON_INTERVAL)]

    public void P3_深层痛楚_海啸_范围(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=3) {

            return;

        }

        if(phase!=2) {

            return;

        }

        int durationMilliseconds=0;

        try {

            durationMilliseconds=JsonConvert.DeserializeObject<int>(@event["DurationMilliseconds"]);

        } catch(Exception e) {

            accessory.Log.Error("DurationMilliseconds deserialization failed.");

            return;

        }

        if(durationMilliseconds<=0||durationMilliseconds>MAXIMUM_DURATION) {

            return;

        }

        int standardDuration=0;

        if(durationMilliseconds<=32500) {

            standardDuration=6750;

        }

        else {

            standardDuration=4000;

        }

        int delay=0;
        int duration=0;

        if(durationMilliseconds<=standardDuration) {

            delay=0;
            duration=durationMilliseconds;

        }

        else {

            delay=durationMilliseconds-standardDuration;
            duration=standardDuration;

        }

        bool signalled=phase3sub2_waterCrystalSemaphore.WaitOne(COMMON_INTERVAL);

        if(!signalled) {

            return;

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        currentProperties.Name=$"P3_深层痛楚_海啸_范围_2";
        currentProperties.Scale=new(5);
        currentProperties.Position=phase3sub2_waterCrystalPosition;
        currentProperties.CentreResolvePattern=PositionResolvePatternEnum.PlayerNearestOrder;
        currentProperties.CentreOrderIndex=2;
        currentProperties.Color=accessory.Data.DefaultDangerColor;
        currentProperties.Delay=delay;
        currentProperties.DestoryAt=duration+2875;

        accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Circle,currentProperties);

        currentProperties=accessory.Data.GetDefaultDrawProperties();

        currentProperties.Name=$"P3_深层痛楚_海啸_范围_1";
        currentProperties.Scale=new(5);
        currentProperties.Position=phase3sub2_waterCrystalPosition;
        currentProperties.CentreResolvePattern=PositionResolvePatternEnum.PlayerNearestOrder;
        currentProperties.CentreOrderIndex=1;
        currentProperties.Color=accessory.Data.DefaultDangerColor;
        currentProperties.Delay=delay;
        currentProperties.DestoryAt=duration+2875;

        accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Circle,currentProperties);

    }

    [ScriptMethod(name:"P3 深层痛楚 混沌之水与海啸 (范围清除)",
        eventType:EventTypeEnum.StatusRemove,
        eventCondition:["StatusID:1601"],
        userControl:false)]

    public void P3_深层痛楚_混沌之水与海啸_范围清除(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=3) {

            return;

        }

        if(string.Equals(@event["SourceId"],"00000000")) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        accessory.Method.RemoveDraw($"P3_深层痛楚_混沌之水_范围_{targetId}");

        lock(phase3sub2_waterCrystalSemaphore) {

            if(phase3sub2_tsunamiCounter>=1) {

                accessory.Method.RemoveDraw($"P3_深层痛楚_海啸_范围_{phase3sub2_tsunamiCounter}");

                --phase3sub2_tsunamiCounter;

            }

        }

    }

    [ScriptMethod(name:"P3 深层痛楚 本影爆碎 (引导指示)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:regex:^(47870|47869)$"])]

    public void P3_深层痛楚_本影爆碎_引导指示(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=3) {

            return;

        }

        if(phase!=2) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["SourceId"],out var sourceId)) {

            return;

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        currentProperties.Name="P3_深层痛楚_本影爆碎_引导指示1";
        currentProperties.Scale=new(2);
        currentProperties.Owner=sourceId;
        currentProperties.TargetResolvePattern=PositionResolvePatternEnum.PlayerFarestOrder;
        currentProperties.TargetOrderIndex=1;
        currentProperties.ScaleMode|=ScaleMode.YByDistance;
        currentProperties.Color=colourOfDirectionIndicators.V4.WithW(1);
        currentProperties.Delay=9875;
        currentProperties.DestoryAt=MAXIMUM_DURATION;

        accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Arrow,currentProperties);

        currentProperties=accessory.Data.GetDefaultDrawProperties();

        currentProperties.Name="P3_深层痛楚_本影爆碎_引导指示2";
        currentProperties.Scale=new(20);
        currentProperties.Owner=sourceId;
        currentProperties.CentreResolvePattern=PositionResolvePatternEnum.PlayerFarestOrder;
        currentProperties.CentreOrderIndex=1;
        currentProperties.Color=colourOfDirectionIndicators.V4.WithW(1);
        currentProperties.Delay=9875;
        currentProperties.DestoryAt=MAXIMUM_DURATION;

        accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,currentProperties);

    }

    [ScriptMethod(name:"P3 深层痛楚 本影爆碎 (引导指示清除)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:47872"],
        userControl:false)]

    public void P3_深层痛楚_本影爆碎_引导指示清除(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=3) {

            return;

        }

        accessory.Method.RemoveDraw("P3_深层痛楚_本影爆碎_引导指示1");
        accessory.Method.RemoveDraw("P3_深层痛楚_本影爆碎_引导指示2");

    }

    [ScriptMethod(name:"P3 深层痛楚 本影爆碎 (落点指示)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:47872"])]

    public void P3_深层痛楚_本影爆碎_落点指示(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=3) {

            return;

        }

        if(phase!=2) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["SourceId"],out var sourceId)) {

            return;

        }

        Vector3 effectPosition=ARENA_CENTER;

        try {

            effectPosition=JsonConvert.DeserializeObject<Vector3>(@event["EffectPosition"]);

        } catch(Exception e) {

            accessory.Log.Error("EffectPosition deserialization failed.");

            return;

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        currentProperties.Scale=new(2);
        currentProperties.Owner=sourceId;
        currentProperties.TargetPosition=effectPosition;
        currentProperties.ScaleMode|=ScaleMode.YByDistance;
        currentProperties.Color=accessory.Data.DefaultDangerColor;
        currentProperties.DestoryAt=5000;

        accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Arrow,currentProperties);

        currentProperties=accessory.Data.GetDefaultDrawProperties();

        currentProperties.Scale=new(20);
        currentProperties.Position=effectPosition;
        currentProperties.Color=accessory.Data.DefaultDangerColor;
        currentProperties.DestoryAt=5000;

        accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,currentProperties);

    }

    [ScriptMethod(name:"P3 深层痛楚 究极冲击波 (数据收集)",
        eventType:EventTypeEnum.ActionEffect,
        eventCondition:["ActionId:47843"])]

    public void P3_深层痛楚_究极冲击波_数据收集(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=3) {

            return;

        }

        if(phase!=2) {

            return;

        }

        if(!string.Equals(@event["TargetIndex"],"1")) {

            return;

        }

        if(phase3sub2_ultimaBlasterCounter>=8) {

            return;

        }

        Vector3 sourcePosition=ARENA_CENTER;

        try {

            sourcePosition=JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);

        } catch(Exception e) {

            accessory.Log.Error("SourcePosition deserialization failed.");

            return;

        }

        int discretizedPosition=discretizePosition(sourcePosition,ARENA_CENTER,8);

        lock(PHASE3_SUB2_ULTIMA_BLASTER_COUNTER_LOCK) {

            Interlocked.Increment(ref phase3sub2_ultimaBlasterCounter);

            if(phase3sub2_ultimaBlasterCounter==1) {

                phase3sub2_firstUltimaBlasterPosition=discretizedPosition;

            }

            if(phase3sub2_ultimaBlasterCounter==2) {

                if((discretizedPosition-1+8)%8==phase3sub2_firstUltimaBlasterPosition) {

                    phase3sub2_baitClockwise=false;

                }

                if((discretizedPosition+1+8)%8==phase3sub2_firstUltimaBlasterPosition) {

                    phase3sub2_baitClockwise=true;

                }

                if(enableDebugLogging) {

                    accessory.Log.Debug($"phase3sub2_firstUltimaBlasterPosition={phase3sub2_firstUltimaBlasterPosition}\nphase3sub2_baitClockwise={phase3sub2_baitClockwise}");

                }

            }

        }

    }

    private int getTargetIconNumber(string id) {

        if(string.Equals(id,"0150")) {

            return 1;

        }

        if(string.Equals(id,"0151")) {

            return 2;

        }

        if(string.Equals(id,"0152")) {

            return 3;

        }

        if(string.Equals(id,"0153")) {

            return 4;

        }

        if(string.Equals(id,"01B5")) {

            return 5;

        }

        if(string.Equals(id,"01B6")) {

            return 6;

        }

        if(string.Equals(id,"01B7")) {

            return 7;

        }

        if(string.Equals(id,"01B8")) {

            return 8;

        }

        return -1;

    }

    [ScriptMethod(name:"P3 深层痛楚 究极冲击波 (范围)",
        eventType:EventTypeEnum.TargetIcon,
        eventCondition:["Id:regex:^(0150|0151|0152|0153|01B5|01B6|01B7|01B8)$"])]

    public void P3_深层痛楚_究极冲击波_范围(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=3) {

            return;

        }

        if(phase!=2) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        int targetIconNumber=getTargetIconNumber(@event["Id"]);

        if(!isLegalPartyIndex(targetIconNumber-1)) {

            return;

        }

        int discretizedPosition=-1;

        if(phase3sub2_baitClockwise) {

            discretizedPosition=(phase3sub2_firstUltimaBlasterPosition+(targetIconNumber-1)+16)%8;

        }

        else {

            discretizedPosition=(phase3sub2_firstUltimaBlasterPosition-(targetIconNumber-1)+16)%8;

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        currentProperties.Scale=new(6,100);
        currentProperties.Position=rotatePosition(new Vector3(100,0,80),ARENA_CENTER,Math.PI/4*discretizedPosition);
        currentProperties.TargetObject=targetId;
        currentProperties.Color=accessory.Data.DefaultDangerColor;
        currentProperties.Delay=1125;
        currentProperties.DestoryAt=11000+250*(targetIconNumber-1);

        accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Rect,currentProperties);

    }

    [ScriptMethod(name:"P3 深层痛楚 究极冲击波 (指路)",
        eventType:EventTypeEnum.TargetIcon,
        eventCondition:["Id:regex:^(0150|0151|0152|0153|01B5|01B6|01B7|01B8)$"])]

    public void P3_深层痛楚_究极冲击波_指路(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=3) {

            return;

        }

        if(phase!=2) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        if(!Debugging&&targetId!=accessory.Data.Me) {
            return;
        }
        int targetIconNumber=getTargetIconNumber(@event["Id"]);

        if(!isLegalPartyIndex(targetIconNumber-1)) {

            return;

        }

        int discretizedPosition=-1;

        if(phase3sub2_baitClockwise) {

            discretizedPosition=(phase3sub2_firstUltimaBlasterPosition+(targetIconNumber-1)+16)%8;

        }

        else {

            discretizedPosition=(phase3sub2_firstUltimaBlasterPosition-(targetIconNumber-1)+16)%8;

        }

        discretizedPosition=(discretizedPosition+4+16)%8;

        Vector3 myPosition=rotatePosition(new Vector3(100,0,81),ARENA_CENTER,Math.PI/4*discretizedPosition);

        // LPDU：与预兆（冲锋）顺逆相反，1 号在冲锋起点对面、往玩家前进方向偏半格
        if(phase3sub2_baitClockwise) {

            myPosition=rotatePosition(myPosition,ARENA_CENTER,Math.PI/8);

        }

        else {

            myPosition=rotatePosition(myPosition,ARENA_CENTER,-(Math.PI/8));

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();
        currentProperties.Name=$"P3_深层痛楚_究极冲击波_指路_{targetId}";
        currentProperties.Scale=new(2);
        currentProperties.Owner=targetId;
        currentProperties.TargetPosition=myPosition;
        currentProperties.ScaleMode|=ScaleMode.YByDistance;
        currentProperties.Color=accessory.Data.DefaultSafeColor;
        currentProperties.Delay=1125;
        currentProperties.DestoryAt=11000+250*(targetIconNumber-1);

        accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);

    }

    #endregion

    #endregion P3A 深层痛楚 通用部分

    #region P3A1 一水火 3100

    [ScriptMethod(name: "———————— 《P3A1 一水火》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P3A1_一水火分割线(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "P3A_暴雷钢铁", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:47890"], userControl: true)]
    public void P3A_暴雷钢铁(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.当前阶段 != 3100) return;
        _udmP3Param.ObjectId_艾克斯迪司 = ev.SourceId;
        sa.DrawCircle(ev.SourceId, 0, 7000, $"P3A_暴雷钢铁 暴雷钢铁", 14.8f, sa.Data.DefaultDangerColor.WithW(2f), byTime: true);
    }

    [ScriptMethod(name: "*P3A_移除暴雷连线", eventType: EventTypeEnum.VfxEvent,
        eventCondition: ["Type:Channeling", "Id:64"], userControl: true)]
    public async void P3A_移除暴雷连线(Event ev, ScriptAccessory sa)
    {
        if (!p3aRemoveThunderTether) return;
        var runId = _runId;
        var obj = sa.GetById(ev.TargetId);
        sa.WriteVisible(obj, false);
        await Task.Delay(7000);
        if (runId != _runId) return;
        sa.WriteVisible(obj, true);
    }

    [ScriptMethod(name: "P3A_深层痛楚判定", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:47858"], suppress: 10000,
        userControl: Debugging)]
    public void P3A_深层痛楚判定(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.当前阶段 != 3100) return;
        if (!_udmP3Param.一运状态记录.WaitOne(2000)) return;
        sa.DebugMsg($"一运状态记录完毕");
        if (!_udmP3Param.一运水晶记录.WaitOne(2000)) return;
        sa.DebugMsg($"一运水晶记录完毕");

        _udmP3Param.一运状态记录.Reset();
        _udmP3Param.一运水晶记录.Reset();

        _udmP3Param.成员分组(sa, _pd);
        sa.DebugMsg($"成员分组完毕");

        _udmP3Param.一水火准备.Set();
    }

    [ScriptMethod(name: "P3A_第一轮水火范围", eventType: EventTypeEnum.ActionEffect,
    eventCondition: ["ActionId:47858"], suppress: 10000,
    userControl: true)]
    public async void P3A_第一轮水火范围(Event ev, ScriptAccessory sa)
    {
        var runId = _runId;

        if (_udmP3Param.当前阶段 != 3100) return;
        await Task.Delay(8000);
        if (runId != _runId) return;
        if (_udmP3Param.当前阶段 != 3100) return;

        DrawWaterFireRange(sa, _udmP3Param.一水火指路与绘图时间);
    }

    [ScriptMethod(name: "P3A_一水火指路", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:47858"], suppress: 10000,
        userControl: true)]
    public async void P3A_一水火指路(Event ev, ScriptAccessory sa)
    {
        var runId = _runId;
        if (_udmP3Param.当前阶段 != 3100) return;
        if (!_udmP3Param.一水火准备.WaitOne(2000)) return;

        sa.DebugMsg($"开始");
        await Task.Delay(8000);
        if (runId != _runId) return;

        foreach (var idx in 指路对象(sa))
            执行水火指路逻辑(sa, idx, _udmP3Param.一水火指路与绘图时间);
        _udmP3Param.一水火准备.Reset();
    }

    [ScriptMethod(name: "P3A_第二轮水火范围", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47869|47870)$"], suppress: 10000, userControl: true)]
    public void P3A_第二轮水火范围(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.当前阶段 != 3102 && _udmP3Param.当前阶段 != 3110) return;
        DrawWaterFireRange(sa, _udmP3Param.二水火指路与绘图时间);
    }

    private void DrawWaterFireRange(ScriptAccessory sa, int destroyTime)
    {
        // 通用绘图范围
        if (_udmP3Param.当前轮为火())
        {
            DrawGroupCircle(sa, _udmP3Param.火组, "DrawWaterFireRange 火组", sa.Data.DefaultDangerColor, 5, destroyTime);
            DrawNearDonut(sa, sa.Data.DefaultDangerColor, destroyTime + 3000);
        }
        else
        {
            DrawGroupDonut(sa, _udmP3Param.水组, "DrawWaterFireRange 水组", sa.Data.DefaultDangerColor, destroyTime);
            DrawNearCircle(sa, sa.Data.DefaultDangerColor, destroyTime + 3000);
        }
    }

    /// <summary>以某水晶方向为基准：离场中 dist 米，再绕场中逆时针转 deg 度（负数为顺时针）</summary>
    private Vector3 水晶方向点(int crystalRegion, float dist, float deg = 0)
    {
        var crystal = _udmP3Param.基于水晶旋转(CrystalBasePos, crystalRegion);
        return crystal.RotateAndExtend(Center, deg.DegToRad(), dist - crystal.GetLength(Center));
    }

    private void 位置指路(ScriptAccessory sa, int idx, Vector3 pos, string tip, int destroyTime)
    {
        sa.DrawGuidance(GuidanceOwner(sa, idx), pos, 0, destroyTime, $"P3 位置指路 {idx}", sa.Data.DefaultSafeColor);
        if (!是自己(sa, idx)) return;
        sa.Method.TextInfo(tip, 3000);
        sa.Method.TTS(tip, 3);
    }

    // LPDU 水火站位（以风水晶为北，西 = 逆时针侧）：远程在火水晶外，治疗在水水晶，T 与近战在风水晶外；
    // 火的那一轮带火的近战往西偏 20°、带火的 T 往东偏 25°，其余叠在风水晶外
    private void 执行水火指路逻辑(ScriptAccessory sa, int idx, int destroyTime)
    {
        var p = _udmP3Param;
        var 有火 = p.当前轮为火() && p.查询组序(p.火组, idx) >= 0;
        var (pos, tip) = idx switch
        {
            6 or 7 => (水晶方向点(p.火水晶方位, 一运远程距离), "火水晶外"),
            2 or 3 => 治疗水火站位(idx),
            4 or 5 when 有火 => (水晶方向点(p.风水晶方位, 一运外圈距离, 20f), "风水晶外 西侧放火"),
            _ when 有火 => (水晶方向点(p.风水晶方位, 一运外圈距离, -25f), "风水晶外 东侧放火"),
            _ => (水晶方向点(p.风水晶方位, 一运外圈距离), "风水晶外 集合"),
        };
        sa.DrawGuidance(GuidanceOwner(sa, idx), pos, 0, destroyTime, $"P3A 水火指路 {idx}", sa.Data.DefaultSafeColor);
        if (!是自己(sa, idx)) return;
        sa.Method.TextInfo($"前往 {tip}", 6000);
        sa.Method.TTS(tip, 3);
    }

    // 第一轮场中有暴雷钢铁，两奶在水水晶外沿场边分开（H2 靠风水晶侧、H1 靠空角侧）；
    // 第二轮沿半径分开（H1 在水水晶、H2 在场中偏水水晶 4m）
    private (Vector3, string) 治疗水火站位(int idx)
    {
        var p = _udmP3Param;
        if (p.当前阶段 != 3100)
            return idx == 2
                ? (水晶方向点(p.水水晶方位, 14f), "水水晶")
                : (水晶方向点(p.水水晶方位, 4f), "场中偏水水晶");
        var 风在逆时针侧 = p.风水晶方位 == (p.水水晶方位 + 1) % 4;
        var deg = (idx == 3 ? 25f : -20f) * (风在逆时针侧 ? 1 : -1);
        return (水晶方向点(p.水水晶方位, 一运远程距离, deg), idx == 3 ? "水水晶外 靠风水晶侧" : "水水晶外 靠空角侧");
    }
    void DrawFacingArrow(ScriptAccessory sa, int idx, int dir, bool isSupport, string name, int destroyTime)
    {
        var dp = sa.DrawLine(GuidanceOwner(sa, idx), 0, 0, destroyTime, $"{name} {idx}",
            isSupport ? (45f + dir * 90f).DegToRad() : 0, 1f, 4.5f,
            isSupport ? sa.Data.DefaultSafeColor : sa.Data.DefaultDangerColor, draw: false);
        dp.FixRotation = isSupport;
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Arrow, dp);
    }

    private void DrawGroupCircle(
        ScriptAccessory sa, List<KeyValuePair<int, int>> group, string name, Vector4 color, float scale, int destroyTime)
    {
        for (var i = 0; i < group.Count; i++)
            sa.DrawCircle(sa.Data.PartyList[group[i].Key], 0, destroyTime, $"{name}钢铁{i + 1}", scale, color);
    }

    private void DrawGroupDonut(
        ScriptAccessory sa, List<KeyValuePair<int, int>> group, string name, Vector4 color, int destroyTime)
    {
        for (var i = 0; i < group.Count; i++)
            sa.DrawDonut(sa.Data.PartyList[group[i].Key], 0, destroyTime, $"{name}月环{i + 1}", 10, 5, color);
    }

    private void DrawNearCircle(ScriptAccessory sa, Vector4 color, int destroyTime)
    {
        var crystalPos = new Vector3(109.9f, 0, 109.9f);
        crystalPos = _udmP3Param.基于水晶旋转(crystalPos, _udmP3Param.水水晶方位);

        for (int i = 0; i < 2; i++)
        {
            var dp = sa.DrawCircle(crystalPos, 0, destroyTime, $"DrawNearCircle 水水晶钢铁{i + 1}", 5f, color, draw: false);
            dp.SetOwnersDistanceOrder(true, (uint)(i + 1));
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    private void DrawNearDonut(ScriptAccessory sa, Vector4 color, int destroyTime)
    {
        var crystalPos = new Vector3(109.9f, 0, 109.9f);
        crystalPos = _udmP3Param.基于水晶旋转(crystalPos, _udmP3Param.火水晶方位);

        for (int i = 0; i < 2; i++)
        {
            var dp = sa.DrawDonut(crystalPos, 0, destroyTime, $"DrawNearCircle 火水晶月环{i + 1}", 10f, 5f, color, draw: false);
            dp.SetOwnersDistanceOrder(true, (uint)(i + 1));
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
        }
    }

    #endregion P3A1 一水火 3100

    #region P3A1 暴雷死刑 3101 3102

    [ScriptMethod(name: "P3A_暴雷死刑阶段推进", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:47881"], userControl: Debugging)]
    public void P3A_暴雷死刑阶段推进(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.当前阶段 != 3100) return;
        _udmP3Param.当前阶段 = 3101;
        sa.DebugMsg($"暴雷死刑，一水火结束");
    }

    [ScriptMethod(name: "P3A_暴雷死刑后阶段推进", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:47884"], suppress: 10000, userControl: Debugging)]
    public void P3A_暴雷死刑后阶段推进(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.当前阶段 != 3101) return;
        _udmP3Param.当前阶段 = 3102;
    }

    #endregion P3A1 暴雷死刑 3101 3102

    #region P3A1 二水火 3110

    [ScriptMethod(name: "———————— 《P3A1 二水火》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P3A1_二水火分割线(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "P3A_二水火阶段推进", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47869|47870)$"], suppress: 10000, userControl: Debugging)]
    public void P3A_二水火阶段推进(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.当前阶段 != 3102) return;
        _udmP3Param.当前阶段 = 3110;
        sa.DebugMsg($"二水火开始，转阶段 {_udmP3Param.当前阶段}");
    }

    [ScriptMethod(name: "P3A_二水火指路", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47869|47870)$"], suppress: 10000, userControl: true)]
    public void P3A_二水火指路(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.当前阶段 != 3102 && _udmP3Param.当前阶段 != 3110) return;
        sa.DebugMsg($"开始");
        foreach (var idx in 指路对象(sa))
            执行水火指路逻辑(sa, idx, _udmP3Param.二水火指路与绘图时间);
    }

    [ScriptMethod(name: "P3A_二水火结束阶段推进", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:regex:^(4786[01])$"], suppress: 10000, userControl: Debugging)]
    public void P3A_二水火结束阶段推进(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.当前阶段 != 3110) return;
        _udmP3Param.当前阶段 = 3111;
        sa.DebugMsg($"二水火结束，转阶段 {_udmP3Param.当前阶段}");
    }

    [ScriptMethod(name: "P3A_二水火结束指路", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:regex:^(4786[01])$"], suppress: 10000, userControl: true)]
    public void P3A_二水火结束(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.当前阶段 != 3110 && _udmP3Param.当前阶段 != 3111) return;

        /*
         * 混沌之水月环 混沌之水 47862
         * 混沌之水引发的钢铁 海啸 47861
         * 混沌之火钢铁 混沌之炎 47859
         * 混沌之火引发的月环 烈焰 47860
         * 混沌之风引发的分摊 龙卷风 47864
         */

        // LPDU：二水火结束后 D3 去空角场边引超级跳，其余非 T 去风水晶集合
        sa.DebugMsg($"二水火结束，当前阶段 {_udmP3Param.当前阶段}");
        var windPos = 水晶方向点(_udmP3Param.风水晶方位, 14f);
        foreach (var idx in 指路对象(sa))
        {
            if (idx == 6)
                位置指路(sa, idx, 水晶方向点(_udmP3Param.无水晶方位, 一运外圈距离), "去空角场边 引导超级跳", 6500);
            else if (idx >= 2)
                位置指路(sa, idx, windPos, "风水晶集合", 6500);
        }
    }

    #endregion P3A1 二水火 3110

    #region P3A1 真空波 3111

    [ScriptMethod(name: "———————— 《P3A1 真空波》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P3A1_真空波分割线(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "P3A_真空波", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47891)$"], userControl: true)]
    public void P3A_真空波(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.当前阶段 != 3111) return;
        sa.DebugMsg($"开始");

        foreach (var idx in 指路对象(sa))
            执行真空波指路逻辑(sa, idx);
    }

    [ScriptMethod(name: "*P3A_真空波读条期间卡奥斯透明", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47891)$"], userControl: true)]
    public async void P3A_真空波读条期间卡奥斯透明(Event ev, ScriptAccessory sa)
    {
        if (!p3aChaosTransparentDuringVacuumWave) return;

        if (_udmP3Param.当前阶段 != 3111) return;
        sa.DebugMsg($"执行");

        var runId = _runId;
        var obj = sa.GetById(_udmP3Param.ObjectId_卡奥斯);
        sa.AlphaModify(obj, 0.3f);
        await Task.Delay(8000);
        if (runId != _runId) return;
        sa.AlphaModify(obj, 1f);
    }

    [ScriptMethod(name: "P3A_真空波指引线", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47891)$"], userControl: true)]
    public void P3A_真空波指引线(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.当前阶段 != 3111) return;
        DrawStaticGuideLineVw(sa, _udmP3Param.真空波指路时间);
    }

    [ScriptMethod(name: "P3A_真空波后分摊", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:regex:^(47891)$"], suppress: 10000, userControl: true)]
    public void P3A_真空波后分摊(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.当前阶段 != 3111) return;
        sa.DebugMsg($"击退后分摊");

        // LPDU 同职能两两分摊：MT+ST、H1+H2、D1+D2、D3+D4
        foreach (var idx in 指路对象(sa))
            sa.DrawCircle(sa.Data.PartyList[idx ^ 1], 0, 4000, $"真空波分摊 {idx}", 6, sa.Data.DefaultSafeColor);
    }

    // LPDU 真空波：同职能两两站一条线，一前一后。角度从艾克斯迪司看向场中算，正数 = 东（逆时针）
    // T 西 20°、近战东 20°、治疗西 50°、远程东 45°；(角度, 离艾克斯迪司距离)
    private static readonly (float deg, float dist)[] 真空波站位 =
        [(-20f, 4f), (-20f, 6f), (-50f, 6.5f), (-50f, 8f), (20f, 4f), (20f, 5.5f), (45f, 7.5f), (45f, 6f)];

    private void 执行真空波指路逻辑(ScriptAccessory sa, int myIndex)
    {
        var (deg, dist) = 真空波站位[myIndex];
        Vector3 bossPos = sa.GetById(_udmP3Param.ObjectId_艾克斯迪司).Position;
        var guidePos = bossPos.RotateAndExtend(Center, 0, -dist).RotateAndExtend(bossPos, deg.DegToRad());
        sa.DrawGuidance(GuidanceOwner(sa, myIndex), guidePos, 0, _udmP3Param.真空波指路时间, $"执行真空波指路逻辑 指路 {myIndex}", sa.Data.DefaultSafeColor);

        // 面向指示：混沌之风背对、混沌之逆风面向
        var needBack = _udmP3Param.需要背对(_pd.Priorities[myIndex]);
        var needBackStr = needBack ? "背对" : "面向";
        DrawFacingArrow(sa, myIndex, 0, false, $"执行真空波指路逻辑 面向辅助自身", _udmP3Param.真空波指路时间);

        var dp = sa.DrawLine(GuidanceOwner(sa, myIndex), _udmP3Param.ObjectId_艾克斯迪司, 0, _udmP3Param.真空波指路时间, $"执行真空波指路逻辑 面向辅助正确面向 {myIndex}",
            needBack ? 180f.DegToRad() : 0, 1f, 4.5f, sa.Data.DefaultSafeColor, draw: false);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Arrow, dp);

        if (!是自己(sa, myIndex)) return;
        sa.Method.TextInfo($"目标圈内 {needBackStr} 艾克斯迪司 击退", 4000);
        sa.Method.TTS($"{needBackStr}Boss击退", 3);
    }

    private void DrawStaticGuideLineVw(ScriptAccessory sa, int destroyTime)
    {
        var myIndex = sa.GetMyIndex();
        Vector3 bossPos = sa.GetById(_udmP3Param.ObjectId_艾克斯迪司).Position;
        var baseRadian = 180f.DegToRad() + bossPos.GetRadian(Center);

        // 四条线依次是 T、治疗、近战、远程（与 真空波站位 的角度一致）
        List<float> biasDeg = [-20, -50, 20, 45];
        var myLine = myIndex switch { 0 or 1 => 0, 2 or 3 => 1, 4 or 5 => 2, _ => 3 };
        for (int i = 0; i < 4; i++)
        {
            var dp = sa.DrawLine(bossPos, 0, 0, destroyTime, $"DrawStaticGuideLineVw 指引线{i}",
                baseRadian + biasDeg[i].DegToRad(), 20f, 50f, sa.Data.DefaultSafeColor, draw: false);
            dp.Color = i == myLine ? sa.Data.DefaultSafeColor : sa.Data.DefaultDangerColor;
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Line, dp);
        }
    }

    #endregion P3A1 真空波 3111

    #region P3B 地震 通用部分

    [ScriptMethod(name: "=============《P3B 地震》=============", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P3B_分割线(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "P3B_二运分P_地震", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:50545"],
        userControl: Debugging)]
    public void P3A_二运分P_地震(Event ev, ScriptAccessory sa)
    {
        _pd.Init(sa, "P3二运");
        _pd.AddPriorities([1, 2, 3, 4, 5, 6, 7, 8]);
        _udmP3Param.当前阶段 = 3200;

        sa.DebugMsg($"当前阶段为：P3 二运 地震 {_udmP3Param.当前阶段}");
    }

    [ScriptMethod(name: "*P3B_黑洞、冰封期间双Boss透明", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47867)$"], userControl: true)]
    public void P3B_黑洞期间双Boss透明(Event ev, ScriptAccessory sa)
    {
        if (!p3bBossesTransparentDuringBlackHole) return;

        const uint 黑洞 = 47867;
        var obj1 = sa.GetById(_udmP3Param.ObjectId_卡奥斯);
        var obj2 = sa.GetById(_udmP3Param.ObjectId_艾克斯迪司);
        sa.AlphaModify(obj1, 0.3f);
        sa.AlphaModify(obj2, ev.ActionId == 黑洞 ? 0.75f : 0.3f);
    }

    [ScriptMethod(name: "*P3B_凯夫卡上桌透明化", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(4784[67]|4785[235])$"], userControl: true)]
    public async void P3B_凯夫卡上桌透明化(Event ev, ScriptAccessory sa)
    {
        if (!p3bKefkaTransparent) return;
        var runId = _runId;
        var obj = sa.GetById(ev.SourceId);
        sa.AlphaModify(obj, 0.3f);
        await Task.Delay(10000);
        if (runId != _runId) return;
        sa.AlphaModify(obj, 1f);
    }

    [ScriptMethod(name: "*P3B_卡奥斯非技能期间透明化", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47867|47869|47870)$"], userControl: true)]
    public async void P3B_卡奥斯非技能期间透明化(Event ev, ScriptAccessory sa)
    {
        if (!p3bChaosTransparent) return;
        if (_udmP3Param.当前阶段 != 3200) return;

        const uint 诅咒敕令 = 47867;
        var awaitTime = ev.ActionId == 诅咒敕令 ? 5000 : 7500;

        var runId = _runId;
        var obj = sa.GetById(_udmP3Param.ObjectId_卡奥斯);
        sa.AlphaModify(obj, 1f, currentAlpha => currentAlpha <= 0.8f);
        await Task.Delay(awaitTime);
        if (runId != _runId) return;
        sa.AlphaModify(obj, 0.5f, currentAlpha => currentAlpha >= 0.8f);
    }

    [ScriptMethod(name: "*P3B_艾克斯迪司非技能期间透明化", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47881)$"], userControl: true)]
    public async void P3B_艾克斯迪司非技能期间透明化(Event ev, ScriptAccessory sa)
    {
        if (!p3bExdeathTransparent) return;
        if (_udmP3Param.当前阶段 != 3200) return;

        var runId = _runId;
        var obj = sa.GetById(_udmP3Param.ObjectId_艾克斯迪司);
        sa.AlphaModify(obj, 1f, currentAlpha => currentAlpha <= 0.8f);
        await Task.Delay(5000);
        if (runId != _runId) return;
        sa.AlphaModify(obj, 0.75f, currentAlpha => currentAlpha >= 0.8f);
    }

    [ScriptMethod(name: "P3B_响亮亮耳光范围与指路", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(4784[67])$"], userControl: true)]
    public void P3B_响亮亮耳光范围与指路(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.当前阶段 != 3200) return;

        const uint 右手接分摊 = 47846;
        const uint 左手接职能 = 47847;
        var aid = ev.ActionId;
        var actionStr = aid switch
        {
            右手接分摊 => "右手接分摊",
            左手接职能 => "左手接职能",
        };

        var isStack = aid == 右手接分摊;

        _udmP3Param.凯夫卡方位 = _udmP3Param.巨大凯夫卡方位获取(sa, true);
        var (attackRegion, safeRegion) = _udmP3Param.巨大凯夫卡半场刀方向(sa, aid == 右手接分摊);

        // 画半场刀与钢铁范围
        sa.DrawCircle(Center, 0, 8000, $"[P3B_响亮亮耳光] 场中钢铁", 6f, sa.Data.DefaultDangerColor.WithW(2f));
        var biasCenter = (Center - new Vector3(0, 0, 3.8f)).RotateAndExtend(Center, -45f.DegToRad() * safeRegion);
        sa.DrawRect(biasCenter, Center, 0, 7000, $"[P3B_响亮亮耳光] 半场刀", 0, 60f, 60f, sa.Data.DefaultDangerColor.WithW(2f));

        // 画分摊或职能线
        List<float> biasDeg = [0, -45f, 45f]; // H T D
        var lineCount = isStack ? 1 : 3;
        static int RoleOf(int idx) => idx switch
        {
            0 or 1 => 1,
            2 or 3 => 0,
            _ => 2
        };

        for (int i = 0; i < lineCount; i++)
        {
            var guidePos = biasCenter.RotateAndExtend(Center, biasDeg[i].DegToRad(), 3.5f);
            var dp = sa.DrawLine(Center, guidePos, 0, 8000, $"响亮亮耳光 指引线{i}",
                0, 20f, 50f, sa.Data.DefaultSafeColor, draw: false);
            dp.Color = i switch
            {
                0 => new Vector4(0.1f, 1f, 0.1f, 1),    // Healer
                1 => new Vector4(0.1f, 0.1f, 1f, 1),    // Tank
                2 => new Vector4(1f, 0.1f, 0.1f, 1),    // DPS
            };
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Line, dp);

            foreach (var idx in 指路对象(sa))
                if (RoleOf(idx) == i || isStack)
                    sa.DrawGuidance(GuidanceOwner(sa, idx), guidePos, 0, 8000, $"响亮亮耳光 指路 {idx}", sa.Data.DefaultSafeColor);
        }

        sa.DebugMsg($"{actionStr}");
        sa.Method.TextInfo($"响亮亮耳光 {actionStr}", 3000);
        sa.Method.TTS($"{actionStr}", 3);
    }

    [ScriptMethod(name: "P3B_诅咒敕令半场刀", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47873)$"], userControl: true)]
    public void P3B_诅咒敕令半场刀(Event ev, ScriptAccessory sa)
    {
        sa.DrawRect(ev.SourceId, 800, 4200, $"诅咒敕令半场刀", 0, 80, 30, CursedEdictColor.V4.WithW(CursedEdictWhiteVal), byTime: true);
    }

    [ScriptMethod(name: "P3B_本色出演的我辣尾", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47854)$"], userControl: true)]
    public void P3B_本色出演的我辣尾(Event ev, ScriptAccessory sa)
    {
        var dp = sa.DrawRect(ev.SourceId, 0, 5000, $"本色出演的我辣尾", 0, 16, 100, sa.Data.DefaultDangerColor.WithW(2f),
            draw: false);
        dp.Offset = new Vector3(0, 0, 40);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "P3B_白洞抬血提示", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(48486)$"], userControl: true)]
    public void P3B_白洞抬血提示(Event ev, ScriptAccessory sa)
    {
        if (sa.GetMyIndex() is not 2 and not 3) return;
        sa.Method.TextInfo($"抬满全队！", 3000, true);
        sa.Method.TTS($"抬满全队", 3);
    }

    [ScriptMethod(name: "P3B_追踪巨大凯夫卡", eventType: EventTypeEnum.PlayActionTimeline,
        eventCondition: ["Id:7748", "SourceDataId:19504"], userControl: true)]
    public void P3B_追踪巨大凯夫卡(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.当前阶段 != 3200) return;
        var region = _udmP3Param.巨大凯夫卡方位获取(sa, false);
        var kefkaPos = new Vector3(100, 0, 120);
        kefkaPos = kefkaPos.RotateAndExtend(Center, region * 45f.DegToRad());
        var color = new Vector4(0.1f, 1f, 1f, 1f);
        sa.DrawConnection(sa.Data.Me, kefkaPos, 0, 3000, $"跟踪巨大凯夫卡", color);
        sa.Method.TextInfo($"凯夫卡重新出现了！", 1000);
    }

    #endregion P3B 地震 通用部分

    #region P3B1 黑洞 3200

    [ScriptMethod(name: "———————— 《P3B1 黑洞》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P3B1_分割线(Event ev, ScriptAccessory sa)
    {
    }

    // 地震 debuff 决定接线身份（LPDU 不标点）：3004/3005/3006 第一/二/三目标，1604 混沌之泥土
    // 每组 DPS 接凯夫卡顺时针第 1 个黑洞、支援第 2 个、带泥的人第 3 个：
    // 110/120/130 = 第一目标 DPS/支援/带泥，210/220/230 = 第二目标同理，310/320 = 第三目标 DPS/支援
    [ScriptMethod(name: "P3B1_按debuff记录接线身份", eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:regex:^(300[456]|1604)$"], userControl: Debugging)]
    public void P3B1_按debuff记录接线身份(Event ev, ScriptAccessory sa)
    {
        const uint 混沌之泥土 = 1604;
        const uint 第一目标 = 3004;
        if (_udmP3Param.当前阶段 != 3200) return;
        lock (_pd)
        {
            if (_udmP3Param.黑洞已出现) return;

            var tidx = sa.GetPlayerIdIndex((uint)ev.TargetId);
            if (tidx is < 0 or >= 8) return;

            if (ev.StatusId == 混沌之泥土)
                _udmP3Param.有泥[tidx] = true;
            else
                _udmP3Param.接线顺位[tidx] = (int)(ev.StatusId - 第一目标) + 1;

            var line = _udmP3Param.接线顺位[tidx];
            if (line == 0) return;
            var slot = _udmP3Param.有泥[tidx] ? 30 : tidx <= 3 ? 20 : 10;
            _pd.Priorities[tidx] = (tidx + 1) + line * 100 + slot;
            sa.DebugMsg($"{sa.GetPlayerJobByIndex(tidx)} 第{line}目标{(_udmP3Param.有泥[tidx] ? " 带泥" : "")} == {_pd.Priorities[tidx]}");
        }
    }

    [ScriptMethod(name: "P3B1_黑洞轮数增加", eventType: EventTypeEnum.AddCombatant,
        eventCondition: ["DataId:19512"], userControl: Debugging, suppress: 5000)]
    public void P3B1_黑洞轮数增加(Event ev, ScriptAccessory sa)
    {
        if (!_udmP3Param.黑洞已出现)
        {
            _udmP3Param.黑洞已出现 = true;
            sa.DebugMsg($"接线身份记录完毕");
            sa.DebugMsg($"{_pd.ShowPriorities()}");
        }
        _udmP3Param.黑洞字典.Clear();
        _udmP3Param.黑洞轮数++;
        sa.DebugMsg($"当前阶段 {_udmP3Param.当前阶段}，清除黑洞字典 {_udmP3Param.黑洞字典.Count}");
        _udmP3Param.黑洞轮数增加完毕.Set();
    }

    [ScriptMethod(name: "P3B1_凯夫卡方位获取", eventType: EventTypeEnum.AddCombatant,
        eventCondition: ["DataId:19512"], userControl: Debugging, suppress: 5000)]
    public void P3B1_凯夫卡方位获取(Event ev, ScriptAccessory sa)
    {
        _udmP3Param.凯夫卡方位 = _udmP3Param.巨大凯夫卡方位获取(sa, true);
        _udmP3Param.凯夫卡方位获取完毕.Set();
    }

    [ScriptMethod(name: "P3B1_外黑洞获取", eventType: EventTypeEnum.AddCombatant,
        eventCondition: ["DataId:19512"], userControl: Debugging)]
    public void P3B1_外黑洞获取(Event ev, ScriptAccessory sa)
    {
        if (!_udmP3Param.黑洞轮数增加完毕.WaitOne(2000)) return;
        if (!_udmP3Param.凯夫卡方位获取完毕.WaitOne(2000)) return;

        lock (_pd)
        {
            var sid = ev.SourceId;
            var obj = sa.GetById(sid);
            if (obj == null) return;
            var pos = obj.Position;

            // 黑洞距离场中 17
            if (Vector3.Distance(pos, Center) < 16.5f) return;

            // A顺计数
            var region = pos.GetRadian(Center).RadianToRegion(8, 4, isDiagDiv: true, isCw: true) % 8;
            var relativeRegion = (region + 8 - _udmP3Param.凯夫卡方位) % 8;

            _udmP3Param.黑洞字典.TryAdd(sid,
                (round: _udmP3Param.黑洞轮数, region: region, relativeRegion: relativeRegion, hasTether: false));
            var count = _udmP3Param.黑洞字典.Count;
            sa.DebugMsg($"获取到方位 {region} 相对方位 {relativeRegion} 的黑洞 #{count}");

            if (_udmP3Param.黑洞字典.Count < 3) return;

            // 判断是否有线
            uint tetherId = 0x54;
            foreach (var 黑洞 in _udmP3Param.黑洞字典.ToList())
            {
                var bc = sa.GetById(黑洞.Key);
                if (bc == null) continue;
                var valueTuple = _udmP3Param.黑洞字典[黑洞.Key];
                var tetherCount = sa.GetTetherSource((IBattleChara)bc, tetherId).Count;
                valueTuple.hasTether = tetherCount > 0;
                _udmP3Param.黑洞字典[黑洞.Key] = valueTuple;
                sa.DebugMsg(
                    $"方位 {黑洞.Value.region} 相对方位 {黑洞.Value.relativeRegion} 的黑洞 0x{bc.GameObjectId:X8} {(tetherCount > 0 ? "有" : "没")}线");
            }

            排序黑洞字典();
            _udmP3Param.外黑洞获取完毕.Set();
            _udmP3Param.黑洞轮数增加完毕.Reset();
            _udmP3Param.凯夫卡方位获取完毕.Reset();
        }
    }

    [ScriptMethod(name: "P3B1_接线任务准备_每轮初始", eventType: EventTypeEnum.AddCombatant,
        eventCondition: ["DataId:19512"], userControl: Debugging, suppress: 5000)]
    public void P3B1_接线任务准备_每轮初始(Event ev, ScriptAccessory sa)
    {
        if (!_udmP3Param.外黑洞获取完毕.WaitOne(2000)) return;
        lock (_udmP3Param)
            _udmP3Param.黑洞接线任务准备.Reset();
        清理接线Framework(sa);
        _udmP3Param.玩家正在接线 = false;

        // 第一轮黑洞起始，第一目标 DPS
        // 第二轮黑洞起始，第一目标 DPS / 支援 / 带泥
        // 第三轮黑洞起始，第二目标 DPS / 支援 / 带泥
        // 第四轮黑洞起始，第三目标 DPS / 支援

        var currentTasks = 获取本轮黑洞接线任务(sa, _udmP3Param.黑洞轮数, _udmP3Param.黑洞喷射轮数);
        lock (_udmP3Param)
        {
            _udmP3Param.当前黑洞接线任务 = currentTasks;
            _udmP3Param.黑洞接线任务版本++;
            _udmP3Param.外黑洞获取完毕.Reset();
            _udmP3Param.黑洞接线任务准备.Set();
        }
    }

    [ScriptMethod(name: "P3B1_黑洞接线范围绘图_每轮初始", eventType: EventTypeEnum.AddCombatant,
        eventCondition: ["DataId:19512"], userControl: true, suppress: 5000)]
    public void P3B1_黑洞接线范围绘图_每轮初始(Event ev, ScriptAccessory sa)
    {
        if (!等待黑洞接线任务(sa, ref _udmP3Param.黑洞接线范围绘图已消费版本,
                "P3B1_黑洞接线范围绘图_每轮初始", out var tasks)) return;
        foreach (var task in tasks)
            绘制黑洞接线范围(sa, task);
    }

    [ScriptMethod(name: "P3B1_黑洞接线TTS_每轮初始", eventType: EventTypeEnum.AddCombatant,
        eventCondition: ["DataId:19512"], userControl: true, suppress: 5000)]
    public void P3B1_黑洞接线TTS_每轮初始(Event ev, ScriptAccessory sa)
    {
        if (!等待黑洞接线任务(sa, ref _udmP3Param.黑洞接线TTS已消费版本,
                "P3B1_黑洞接线TTS_每轮初始", out var tasks)) return;
        bool ttsHinted = false;
        foreach (var task in tasks)
            ttsHinted |= 播报黑洞接线TTS(sa, task.playerIdx, ttsHinted);
    }

    [ScriptMethod(name: "P3B1_黑洞接线指路_每轮初始", eventType: EventTypeEnum.AddCombatant,
        eventCondition: ["DataId:19512"], userControl: true, suppress: 5000)]
    public void P3B1_黑洞接线指路_每轮初始(Event ev, ScriptAccessory sa)
    {
        if (!等待黑洞接线任务(sa, ref _udmP3Param.黑洞接线指路已消费版本,
                "P3B1_黑洞接线指路_每轮初始", out var tasks)) return;
        if (_udmP3Param.黑洞喷射轮数 >= 10) return;
        foreach (var task in tasks)
            挂载玩家向黑洞方位接线指路(sa, task);
        执行接线Framework(sa, tasks);
    }

    [ScriptMethod(name: "P3B1_接线任务准备_中途", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:47868", "TargetIndex:1"], userControl: Debugging, suppress: 2500)]
    public void P3B1_接线任务准备_中途(Event ev, ScriptAccessory sa)
    {
        lock (_udmP3Param)
            _udmP3Param.黑洞接线任务准备.Reset();
        // 删掉上一轮的连线
        清理接线Framework(sa);

        sa.Method.RemoveDraw($"绘制黑洞接线 R{_udmP3Param.黑洞轮数} T{_udmP3Param.黑洞喷射轮数}.*");
        _udmP3Param.黑洞喷射轮数++;
        if (_udmP3Param.黑洞轮数 is 1 or 4)
        {
            翻转黑洞有线状态();
            排序黑洞字典();
        }

        var currentTasks = 获取本轮黑洞接线任务(sa, _udmP3Param.黑洞轮数, _udmP3Param.黑洞喷射轮数);
        lock (_udmP3Param)
        {
            _udmP3Param.当前黑洞接线任务 = currentTasks;
            _udmP3Param.黑洞接线任务版本++;
            _udmP3Param.黑洞接线任务准备.Set();
        }
    }

    [ScriptMethod(name: "P3B1_黑洞接线范围绘图_中途", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:47868", "TargetIndex:1"], userControl: true, suppress: 2500)]
    public void P3B1_黑洞接线范围绘图_中途(Event ev, ScriptAccessory sa)
    {
        if (!等待黑洞接线任务(sa, ref _udmP3Param.黑洞接线范围绘图已消费版本,
                "P3B1_黑洞接线范围绘图_中途", out var tasks)) return;
        foreach (var task in tasks)
            绘制黑洞接线范围(sa, task);
    }

    [ScriptMethod(name: "P3B1_黑洞接线TTS_中途", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:47868", "TargetIndex:1"], userControl: true, suppress: 2500)]
    public void P3B1_黑洞接线TTS_中途(Event ev, ScriptAccessory sa)
    {
        if (!等待黑洞接线任务(sa, ref _udmP3Param.黑洞接线TTS已消费版本,
                "P3B1_黑洞接线TTS_中途", out var tasks)) return;
        bool ttsHinted = false;
        foreach (var task in tasks)
            ttsHinted |= 播报黑洞接线TTS(sa, task.playerIdx, ttsHinted);

        if (ttsHinted || !_udmP3Param.玩家正在接线) return;
        _udmP3Param.玩家正在接线 = false;
        sa.Method.TextInfo($"回到人群", 2000);
        sa.Method.TTS($"回到人群", 3);
    }

    [ScriptMethod(name: "P3B1_黑洞接线指路_中途", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:47868", "TargetIndex:1"], userControl: true, suppress: 2500)]
    public void P3B1_黑洞接线指路_中途(Event ev, ScriptAccessory sa)
    {
        if (!等待黑洞接线任务(sa, ref _udmP3Param.黑洞接线指路已消费版本,
                "P3B1_黑洞接线指路_中途", out var tasks)) return;
        if (_udmP3Param.黑洞喷射轮数 >= 10) return;
        foreach (var task in tasks)
            挂载玩家向黑洞方位接线指路(sa, task);
        执行接线Framework(sa, tasks);
    }

    private bool 播报黑洞接线TTS(ScriptAccessory sa, int playerIdx, bool ttsHinted)
    {
        var myIndex = sa.GetMyIndex();
        if (playerIdx == myIndex)
        {
            if (ttsHinted) return true;
            sa.Method.TextInfo($"去接线", 2000);
            sa.Method.TTS($"去接线", 3);
            _udmP3Param.玩家正在接线 = true;
            return true;
        }
        return false;
    }

    private bool 等待黑洞接线任务(ScriptAccessory sa, ref int consumedVersion, string caller,
        out List<(int playerIdx, List<int> realRegions, List<ulong> blackHoles, string name)> tasks)
    {
        tasks = [];
        for (var i = 0; i < 40; i++)
        {
            if (!_udmP3Param.黑洞接线任务准备.WaitOne(50)) continue;
            lock (_udmP3Param)
            {
                if (_udmP3Param.黑洞接线任务版本 <= consumedVersion)
                {
                    _udmP3Param.黑洞接线任务准备.Reset();
                    continue;
                }
                consumedVersion = _udmP3Param.黑洞接线任务版本;
                tasks = _udmP3Param.当前黑洞接线任务.ToList();
                return true;
            }
        }
        sa.DebugMsg($"等待黑洞接线任务超时");
        return false;
    }

    private void 翻转黑洞有线状态()
    {
        foreach (var 黑洞 in _udmP3Param.黑洞字典.ToList())
        {
            var valueTuple = 黑洞.Value;
            valueTuple.hasTether = !valueTuple.hasTether;
            _udmP3Param.黑洞字典[黑洞.Key] = valueTuple;
        }
    }

    private void 排序黑洞字典()
    {
        _udmP3Param.黑洞字典 = _udmP3Param.黑洞字典
            .OrderByDescending(kvp => kvp.Value.hasTether)
            .ThenBy(kvp => kvp.Value.relativeRegion)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    private List<int> 筛选本轮接线玩家(ScriptAccessory sa, int bhRound, int bhEffectRound)
    {
        // 第 i 个接线身份接本轮排序后的第 i 个黑洞（凯夫卡顺时针）
        List<int> markFeatureList = (bhRound, bhEffectRound) switch
        {
            (1, 0) => [110],
            (1, 1) => [110, 120],
            (2, 2) => [110, 120, 130],
            (2, 3) => [210, 120, 130],
            (2, 4) => [210, 220, 130],
            (3, 5) => [210, 220, 230],
            (3, 6) => [310, 220, 230],
            (3, 7) => [310, 320, 230],
            (4, 8) => [310, 320],
            (4, 9) => [320],
            _ => []
        };

        var isKnownNoop = (bhRound, bhEffectRound) is (1, 2) or (2, 5) or (3, 8) or (4, 10);
        if (markFeatureList.Count == 0 && !isKnownNoop)
        {
            sa.DebugMsg($"输入值错误 ({bhRound} {bhEffectRound})");
        }
        return markFeatureList;
    }

    private KeyValuePair<int, int> 按接线身份找玩家(ScriptAccessory sa, int markFeature)
    {
        var matchedPlayers = _pd.Priorities
            .Where(kvp => kvp.Value / 10 * 10 == markFeature)
            .OrderBy(kvp => kvp.Value)
            .ToList();

        if (matchedPlayers.Count == 0)
            return new KeyValuePair<int, int>(-1, 0);

        var myIndex = sa.GetMyIndex();
        var myIndexInMatchedPlayersList = matchedPlayers.FindIndex(kvp => kvp.Key == myIndex);

        return myIndexInMatchedPlayersList >= 0 ? matchedPlayers[myIndexInMatchedPlayersList] : matchedPlayers[0];
    }

    private List<(int playerIdx, List<int> realRegions, List<ulong> blackHoles, string name)> 获取本轮黑洞接线任务(ScriptAccessory sa, int bhRound, int bhEffectRound)
    {
        var markFeatureList = 筛选本轮接线玩家(sa, bhRound, bhEffectRound);
        var tasks = new List<(int playerIdx, List<int> realRegions, List<ulong> blackHoles, string name)>();

        for (int i = 0; i < markFeatureList.Count; i++)
        {
            var markFeature = markFeatureList[i];
            // 黑洞字典已在生成时排好序
            var bh = _udmP3Param.黑洞字典.ElementAt(i);
            var blackHoles = new List<ulong> { bh.Key };
            var realRegions = new List<int> { bh.Value.region };

            var player = 按接线身份找玩家(sa, markFeature);
            if (player.Key < 0)
            {
                sa.DebugMsg($"未找到接线身份 {markFeature} 对应玩家");
                continue;
            }

            var regionStr = string.Join(",", realRegions);
            var taskName = $"R{bhRound} T{bhEffectRound} {player.Key} {regionStr}";
            tasks.Add((player.Key, realRegions, blackHoles, taskName));
            sa.DebugMsg(
                $"{taskName} | 玩家 {sa.GetPlayerJobByIndex(player.Key)}({player.Key}) | 特征 {markFeature} | 方位 {regionStr} | 黑洞 {string.Join(",", blackHoles.Select(x => x.ToString("X8")))}");
        }

        sa.DebugMsg($"R{bhRound} T{bhEffectRound} 共 {tasks.Count} 个任务");
        return tasks;
    }

    private bool 黑洞接线任务属于自己(ScriptAccessory sa,
        (int playerIdx, List<int> realRegions, List<ulong> blackHoles, string name) task)
    {
        return Debugging || sa.GetMyIndex() == task.playerIdx;
    }

    private void 绘制黑洞接线范围(ScriptAccessory sa,
        (int playerIdx, List<int> realRegions, List<ulong> blackHoles, string name) task)
    {
        if (!黑洞接线任务属于自己(sa, task)) return;
        var color = BhConnLineColor.V4;

        for (int i = 0; i < task.blackHoles.Count; i++)
        {
            var dp = sa.DrawLine(task.blackHoles[i], 0, 0, 7000, $"绘制黑洞接线 {task.name} L{task.realRegions[i]}",
                0, 1, 40, color, byY: true, draw: false);
            dp.TargetResolvePattern = PositionResolvePatternEnum.TetherTarget;
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, dp);

            var dp2 = sa.DrawRect(task.blackHoles[i], 0, 0, 7000, $"绘制黑洞接线 {task.name} R{task.realRegions[i]}",
                0, 6, 40, sa.Data.DefaultDangerColor, draw: false);
            dp2.TargetResolvePattern = PositionResolvePatternEnum.TetherTarget;
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp2);
        }

        sa.DebugMsg($"{sa.GetPlayerJobByIndex(task.playerIdx)} 接真实方位 {string.Join(",", task.realRegions)} 的黑洞");
    }

    private void 挂载玩家向黑洞方位接线指路(ScriptAccessory sa,
        (int playerIdx, List<int> realRegions, List<ulong> blackHoles, string name) task)
    {
        if (!黑洞接线任务属于自己(sa, task)) return;
        var 最后一根 = _udmP3Param.黑洞轮数 == 4 && _udmP3Param.黑洞喷射轮数 == 9;
        var guidePos = 计算接线站位(task.realRegions[0], 最后一根);

        挂载接线Framework(sa, guidePos, task);

        sa.DebugMsg($"{sa.GetPlayerJobByIndex(task.playerIdx)} 接真实方位 {string.Join(",", task.realRegions)} 的黑洞");
    }

    private void 挂载接线Framework(ScriptAccessory sa, Vector3 guidePos,
        (int playerIdx, List<int> realRegions, List<ulong> blackHoles, string name) task)
    {
        // task
        // -- playerIdx：需接线的玩家
        // -- realRegions：需接线黑洞所在方位
        // -- blackHoles：需接线黑洞 ObjId
        // -- name：任务名称 $"R{bhRound} T{bhEffectRound} {player.Key} {regionStr}
        // ---- bhRound：黑洞轮数
        // ---- bhEffectRound：黑洞喷射轮数
        // ---- player.Key：同 playerIdx
        // ---- regionStr：需接线黑洞所在方位的集合，即 realRegions 的 Join

        // 绘图指路条件：task.blackHoles 中的黑洞产生的线都被接到
        // 每有一根线，需一个 fwElement 进行控制
        // fwElement
        // -- Name：Framework 的 Name，计划用 {player.Key} {blackHoles[i]}
        // -- PlayerIndex：需接线 Player 的 Index
        // -- BlackHoleId：需接线的黑洞源
        // -- PlayerObjId：需接线 Player 的 ObjectId
        // -- FwHandle：用于挂载 sa.Method.RegistFrameworkUpdateAction(Action)
        // -- Tethered：若成功接上线则为 true
        // -- GuidePos：接到线/接全线后的指路位置

        // List 中一个 Framework 去控制每个黑洞
        // 外部一个 Framework 检查 List 中的元素是否都是 tethered，然后画指路线

        for (int i = 0; i < task.blackHoles.Count; i++)
        {
            ulong playerObjId = sa.Data.PartyList[task.playerIdx];
            bool tethered = false;

            var fwElement = new 接线Framework元素
            {
                Name = $"{task.playerIdx} {task.blackHoles[i]}",
                TaskName = task.name,
                PlayerIndex = task.playerIdx,
                BlackHoleId = task.blackHoles[i],
                PlayerObjId = playerObjId,
                Tethered = tethered,
                FwHandle = "",
                GuidePos = guidePos
            };

            _udmP3Param.接线FrameworkList.Add(fwElement);
            sa.DebugMsg(
                $"{fwElement.TaskName} | {sa.GetPlayerJobByIndex(fwElement.PlayerIndex)}({fwElement.PlayerIndex}) 玩家 {fwElement.PlayerObjId:X8} | 黑洞 {fwElement.BlackHoleId:X8} | 真实方位 {task.realRegions[i]} | 元素 {fwElement.Name}");
        }
    }

    private void 执行接线Framework(ScriptAccessory sa,
        List<(int playerIdx, List<int> realRegions, List<ulong> blackHoles, string name)> tasks)
    {
        // 筛选出本轮相关的线元素
        List<接线Framework元素> 当前接线任务Fws = _udmP3Param.接线FrameworkList.ToList();
        if (当前接线任务Fws.Count == 0) return;
        sa.DebugMsg(
            $"本轮任务 {tasks.Count} 个，接线元素 {当前接线任务Fws.Count} 个：{string.Join(" | ", tasks.Select(task => $"{task.name}:{task.blackHoles.Count}"))}");

        Dictionary<string, bool> 去站位指路绘制状态 = tasks.ToDictionary(task => task.name, _ => false);
        Dictionary<string, bool> 上一次任务接线完成状态 = tasks.ToDictionary(task => task.name, _ => false);
        string 去站位指路绘图名(string taskName) => $"绘制黑洞接线 {taskName} G去站位";

        _udmP3Param.当前接线任务Framework = sa.Method.RegistFrameworkUpdateAction(Action);
        void Action()
        {
            foreach (var fw in 当前接线任务Fws)
            {
                var bh = sa.GetById(fw.BlackHoleId);
                if (bh == null)
                {
                    if (fw.上一次黑洞存在状态)
                    {
                        sa.DebugMsg($"{fw.TaskName} | {fw.Name} 黑洞 {fw.BlackHoleId:X8} 当前取不到");
                        fw.上一次黑洞存在状态 = false;
                    }
                    continue;
                }
                if (!fw.上一次黑洞存在状态)
                {
                    sa.DebugMsg($"{fw.TaskName} | {fw.Name} 黑洞 {fw.BlackHoleId:X8} 已重新取到");
                    fw.上一次黑洞存在状态 = true;
                }
                var 当前接线目标 = sa.GetTetherSource((IBattleChara)bh, 0x54);
                var 当前接线目标列表 = string.Join(",", 当前接线目标.Select(x => x.ToString("X8")));
                if (当前接线目标列表 != fw.上一次接线目标列表)
                {
                    var 当前接线目标Job列表 = string.Join(",", 当前接线目标.Select(x => sa.GetPlayerJobById((uint)x)));
                    sa.DebugMsg(
                        $"{fw.TaskName} | {fw.Name} 线目标变化：{(当前接线目标列表 == "" ? "无" : 当前接线目标列表)} | {当前接线目标Job列表}");
                    fw.上一次接线目标列表 = 当前接线目标列表;
                }
                var 刚才已接到线 = fw.Tethered;
                var 当前已接到线 = 当前接线目标.Contains(fw.PlayerObjId);
                fw.Tethered = 当前已接到线;

                if (当前已接到线)
                {
                    // 如果接到线了，就不需要画去接这根线的指路，清空上一次去接线的相关绘图。
                    if (!fw.去接线指路已绘制 && 刚才已接到线) continue;
                    if (fw.上一次去接线指路绘图名 != "")
                        sa.Method.RemoveDraw(fw.上一次去接线指路绘图名);
                    sa.DebugMsg($"{fw.TaskName} | {fw.Name} 接到线了，目标 {fw.PlayerObjId:X8}/{sa.GetPlayerJobByIndex(fw.PlayerIndex)}");
                    fw.去接线指路已绘制 = false;
                    fw.上一次接线目标 = 0;
                    fw.上一次去接线指路绘图名 = "";
                    continue;
                }

                if (当前接线目标.Count != 1)
                {
                    // 如果这根线没有处在“明确连到一个玩家”的状态（没有玩家给你连了），则清空“去接线”与“去站位”绘图
                    if (去站位指路绘制状态.GetValueOrDefault(fw.TaskName))
                    {
                        sa.Method.RemoveDraw(去站位指路绘图名(fw.TaskName));
                        去站位指路绘制状态[fw.TaskName] = false;
                        sa.DebugMsg($"{fw.TaskName} | {fw.Name} 线目标数量 {当前接线目标.Count}，移除去站位指路");
                    }
                    if (!fw.去接线指路已绘制) continue;
                    if (fw.上一次去接线指路绘图名 != "")
                        sa.Method.RemoveDraw(fw.上一次去接线指路绘图名);
                    sa.DebugMsg($"{fw.TaskName} | {fw.Name} 线目标数量 {当前接线目标.Count}，移除去接线指路");
                    fw.去接线指路已绘制 = false;
                    fw.上一次接线目标 = 0;
                    fw.上一次去接线指路绘图名 = "";
                    continue;
                }

                // 如果这根线连着错误的玩家
                var 当前接线目标ObjId = 当前接线目标[0];
                if (fw.去接线指路已绘制 && 当前接线目标ObjId == fw.上一次接线目标) continue;
                if (去站位指路绘制状态.GetValueOrDefault(fw.TaskName))
                {
                    sa.Method.RemoveDraw(去站位指路绘图名(fw.TaskName));
                    去站位指路绘制状态[fw.TaskName] = false;
                    sa.DebugMsg($"{fw.TaskName} | {fw.Name} 线被错误目标 {当前接线目标ObjId:X8}/{sa.GetPlayerJobById((uint)当前接线目标ObjId)} 接到，移除去站位指路");
                }
                if (fw.上一次去接线指路绘图名 != "")
                    sa.Method.RemoveDraw(fw.上一次去接线指路绘图名);

                // 顺利来到这里：当前没接到线，黑洞明确有线连着玩家，这根线连着错误的玩家
                var 当前去接线指路绘图名 = $"{fw.去接线指路绘图名前缀} {当前接线目标ObjId:X8}";
                sa.DebugMsg(
                    $"{fw.TaskName} | {fw.Name} 快去接线：{sa.GetPlayerJobByIndex(fw.PlayerIndex)}({fw.PlayerObjId:X8}) -> {sa.GetPlayerJobById((uint)当前接线目标ObjId)}({当前接线目标ObjId:X8})");
                sa.DrawGuidance(fw.PlayerObjId, 当前接线目标ObjId, 0, 7000, 当前去接线指路绘图名, sa.Data.DefaultSafeColor);
                fw.去接线指路已绘制 = true;
                fw.上一次接线目标 = 当前接线目标ObjId;
                fw.上一次去接线指路绘图名 = 当前去接线指路绘图名;
            }

            foreach (var taskFws in 当前接线任务Fws.GroupBy(fw => fw.TaskName))
            {
                // 使用 groupby 将所有线元素进行分组，若一人接两根，会有 R1 T1 5 0,2 这种同名 TaskName 的情况
                var taskName = taskFws.Key;
                var allTethered = taskFws.All(fw => fw.Tethered);
                if (allTethered != 上一次任务接线完成状态.GetValueOrDefault(taskName))
                {
                    sa.DebugMsg(
                        $"{taskName} 接线完成状态变化：{allTethered} | {string.Join(",", taskFws.Select(fw => $"{fw.Name}={fw.Tethered}"))}");
                    上一次任务接线完成状态[taskName] = allTethered;
                }
                if (!allTethered)
                {
                    if (!去站位指路绘制状态.GetValueOrDefault(taskName)) continue;
                    sa.Method.RemoveDraw(去站位指路绘图名(taskName));
                    去站位指路绘制状态[taskName] = false;
                    sa.DebugMsg($"{taskName} 未全接到，移除去站位指路");
                    continue;
                }

                if (去站位指路绘制状态.GetValueOrDefault(taskName)) continue;
                foreach (var fw in taskFws)
                    sa.Method.RemoveDraw($"绘制黑洞接线 {taskName} G去接线 {fw.Name}.*");

                var guideFw = taskFws.First();
                sa.DebugMsg($"去站位吧 {taskName} | 玩家 {sa.GetPlayerJobByIndex(guideFw.PlayerIndex)}({guideFw.PlayerObjId:X8}) | GuidePos {guideFw.GuidePos}");
                // $"绘制黑洞接线 {taskName} G去站位";
                sa.DrawGuidance(guideFw.PlayerObjId, guideFw.GuidePos, 0, 7000, 去站位指路绘图名(taskName), sa.Data.DefaultSafeColor);
                去站位指路绘制状态[taskName] = true;
            }
        }
    }

    private void 清理接线Framework(ScriptAccessory sa)
    {
        foreach (var fw in _udmP3Param.接线FrameworkList)
        {
            if (fw.FwHandle != "")
                sa.Method.UnregistFrameworkUpdateAction(fw.FwHandle);
        }
        _udmP3Param.接线FrameworkList.Clear();

        if (_udmP3Param.当前接线任务Framework != "")
            sa.Method.UnregistFrameworkUpdateAction(_udmP3Param.当前接线任务Framework);
        _udmP3Param.当前接线任务Framework = "";
    }

    /// <summary>接线后的站位：从黑洞方向顺时针转，站在卡奥斯（场中）最大近战；最后一根线站场边让激光朝外</summary>
    private Vector3 计算接线站位(int realRegion, bool 最后一根)
    {
        var bhpos = BlackHoleBasePos.RotateAndExtend(Center, realRegion * -45f.DegToRad());
        var (deg, r) = 最后一根 ? (最后一根线顺时针角度, 最后一根线站位半径) : (接线站位顺时针角度, 接线站位半径);
        return bhpos.RotateAndExtend(Center, -deg.DegToRad(), r - bhpos.GetLength(Center));
    }

    #region P3B1 黑洞 范围（Cicero 灵视）

    [ScriptMethod(name:"P3 地震 (阶段控制)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:50546"],
        userControl:false)]

    public void P3_地震_阶段控制(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=3) {

            return;

        }

        if(phase!=2) {

            return;

        }

        phase=3;

        if(enableDebugLogging) {

            accessory.Log.Debug($"majorPhase={majorPhase}\nphase={phase}");

        }

    }

    [ScriptMethod(name:"P3 地震 黑洞 (碰撞箱范围)",
        eventType:EventTypeEnum.AddCombatant,
        eventCondition:["DataId:19512"])]

    public void P3_地震_黑洞_碰撞箱范围(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=3) {

            return;

        }

        if(phase!=3) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["SourceId"],out var sourceId)) {

            return;

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        currentProperties.Name=$"P3_地震_黑洞_碰撞箱范围_{sourceId}";
        currentProperties.Scale=new(2);
        currentProperties.Owner=sourceId;
        currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(1);
        currentProperties.DestoryAt=MAXIMUM_DURATION;

        accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Circle,currentProperties);

    }

    [ScriptMethod(name:"P3 地震 黑洞 (碰撞箱范围清除)",
        eventType:EventTypeEnum.RemoveCombatant,
        eventCondition:["DataId:19512"],
        userControl:false)]

    public void P3_地震_黑洞_碰撞箱范围清除(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=3) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["SourceId"],out var sourceId)) {

            return;

        }

        accessory.Method.RemoveDraw($"P3_地震_黑洞_碰撞箱范围_{sourceId}");

    }

    [ScriptMethod(name:"P3 地震 黑洞 (连线范围)",
        eventType:EventTypeEnum.VfxEvent,
        eventCondition:["Id:84"])]

    public void P3_地震_黑洞_连线范围(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=3) {

            return;

        }

        if(phase!=3) {

            return;

        }

        if(!string.Equals(@event["Type"],"Channeling")) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"], out var targetId)) {

            return;

        }

        lock(phase3sub3_blackHoleDrawingCounter) {

            int lastDrawing=phase3sub3_blackHoleDrawingCounter.GetOrAdd(sourceId,0);

            accessory.Method.RemoveDraw($"P3_地震_黑洞_连线范围_{sourceId}_{lastDrawing}");

            Interlocked.Increment(ref lastDrawing);
            phase3sub3_blackHoleDrawingCounter[sourceId]=lastDrawing;

            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Name=$"P3_地震_黑洞_连线范围_{sourceId}_{lastDrawing}";
            currentProperties.Scale=new(6,125);
            currentProperties.Owner=sourceId;
            currentProperties.TargetObject=targetId;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.DestoryAt=MAXIMUM_DURATION;

            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Rect,currentProperties);

        }

    }

    [ScriptMethod(name:"P3 地震 黑洞 (连线范围清除1)",
        eventType:EventTypeEnum.ActionEffect,
        eventCondition:["ActionId:47868"],
        userControl:false)]

    public void P3_地震_黑洞_连线范围清除1(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=3) {

            return;

        }

        if(!string.Equals(@event["TargetIndex"],"1")) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["SourceId"],out var sourceId)) {

            return;

        }

        lock(phase3sub3_blackHoleDrawingCounter) {

            Interlocked.Increment(ref phase3sub3_nothingnessCounter);

            if(!(new List<int>{4,5,6,7,8,9,13,14,15,16,17,18}.Contains(phase3sub3_nothingnessCounter))) {

                accessory.Method.RemoveDraw(@$"^P3_地震_黑洞_连线范围_{sourceId}_.*$");

                phase3sub3_blackHoleDrawingCounter.TryRemove(sourceId,out _);

            }

        }

    }

    [ScriptMethod(name:"P3 地震 黑洞 (连线范围清除2)",
        eventType:EventTypeEnum.RemoveCombatant,
        eventCondition:["DataId:19512"],
        userControl:false)]

    public void P3_地震_黑洞_连线范围清除2(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=3) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["SourceId"],out var sourceId)) {

            return;

        }

        lock(phase3sub3_blackHoleDrawingCounter) {

            accessory.Method.RemoveDraw(@$"^P3_地震_黑洞_连线范围_{sourceId}_.*$");

            phase3sub3_blackHoleDrawingCounter.TryRemove(sourceId,out _);

        }

    }

    #endregion

    #endregion P3B1 黑洞 3200

    #region P3B2 冰封 3210

    [ScriptMethod(name: "———————— 《P3B2 冰封》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P3B2_分割线(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "P3B2_冰封_转阶段", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:47887"], userControl: Debugging)]
    public void P3B2_冰封_转阶段(Event ev, ScriptAccessory sa)
    {
        _udmP3Param.当前阶段 = 3210;
        sa.DebugMsg($"当前阶段为：P3B2 二运 冰封 {_udmP3Param.当前阶段}");
        _udmP3Param.凯夫卡方位 = _udmP3Param.巨大凯夫卡方位获取(sa, false);
        _udmP3Param.冰封北方位 = BlizKefkaIsNorth ? _udmP3Param.凯夫卡方位 : (_udmP3Param.凯夫卡方位 + 4) % 8;
        _udmP3Param.西塔方位 = (_udmP3Param.冰封北方位 + 2) % 8;
        _udmP3Param.东塔方位 = (_udmP3Param.冰封北方位 - 2 + 8) % 8;
        _udmP3Param.凯夫卡方位获取完毕.Set();
        sa.DebugMsg($"冰封北方位 继承自 凯夫卡方位 {_udmP3Param.凯夫卡方位} C逆");
    }

    [ScriptMethod(name: "P3B2_冰封_引导路径", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:47887"], userControl: true)]
    public void P3B2_冰封_引导路径一(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.冰封北方位 == -1)
        {
            if (!_udmP3Param.凯夫卡方位获取完毕.WaitOne(2000)) return;
            _udmP3Param.凯夫卡方位获取完毕.Reset();
        }

        if (_udmP3Param.当前阶段 != 3210) return;
        _udmP3Param.绘制冰封引导路径 = true;

        foreach (var myIndex in 指路对象(sa))
        {
            // LPDU 第一次引导：支援在北（凯夫卡侧）、DPS 在南
            var initPos = myIndex <= 3 ? new Vector3(100, 0, 110f) : new Vector3(100, 0, 90f);
            initPos = _udmP3Param.基于凯夫卡旋转(initPos, _udmP3Param.冰封北方位);

            var targetPos = 冰封第二枚黄圈位置(myIndex);
            if (是自己(sa, myIndex))
                _udmP3Param.第二枚黄圈引导位置 = targetPos;

            var owner = GuidanceOwner(sa, myIndex);
            sa.DrawGuidance(owner, initPos, 0, 3000, $"冰封路径一 黄圈 1 {myIndex}", sa.Data.DefaultSafeColor);
            sa.DrawGuidance(initPos, targetPos, 0, 3000, $"冰封路径一 黄圈 1->2 {myIndex}", sa.Data.DefaultDangerColor);
            sa.DrawGuidance(owner, targetPos, 3000, 3000, $"冰封路径一 黄圈 2 {myIndex}", sa.Data.DefaultSafeColor);
        }
    }

    /// <summary>
    /// 冰封分左右（以凯夫卡为北，西 = 左）。四角和踩塔同侧：支援在北侧两角、DPS 在南侧两角。
    /// LPDU：T+近战在西（MT+ST 西北、D1+D2 西南），H+远程在东（H1+H2 东北、D3+D4 东南）。
    /// PF：G1（MT H1 D1 D3）在西（MT+H1 西北、D1+D3 西南），G2（ST H2 D2 D4）在东（ST+H2 东北、D2+D4 东南）。
    /// </summary>
    private bool 冰封在西侧(int myIndex)
        => p3FollowPfStrats ? myIndex is 0 or 2 or 4 or 6 : myIndex is 0 or 1 or 4 or 5;

    /// <summary>冰封第二枚黄圈的引导位置：支援去北侧斜角、DPS 去南侧斜角，左右按 冰封在西侧</summary>
    private Vector3 冰封第二枚黄圈位置(int myIndex)
    {
        // 基准点在凯夫卡方向，逆时针为正：西北 45°、东北 -45°、西南 135°、东南 -135°
        var rotDeg = (myIndex <= 3 ? 45f : 135f) * (冰封在西侧(myIndex) ? 1 : -1);
        var targetPos = new Vector3(100, 0, 114.14f).RotateAndExtend(Center, rotDeg.DegToRad());
        return _udmP3Param.基于凯夫卡旋转(targetPos, _udmP3Param.冰封北方位);
    }

    [ScriptMethod(name: "P3B2_冰封_引导路径二", eventType: EventTypeEnum.TargetIcon,
        eventCondition: ["Id:00A1"], userControl: Debugging)]
    public void P3B2_冰封_引导路径二(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.当前阶段 != 3210) return;
        if (!_udmP3Param.绘制冰封引导路径) return;
        _udmP3Param.是第一次分摊 = !_udmP3Param.是第一次分摊;

        var targetIdx = sa.GetPlayerIdIndex((uint)ev.TargetId);
        _udmP3Param.分摊点TN = targetIdx <= 3;
        foreach (var myIndex in 指路对象(sa))
        {
            if (_udmP3Param.需要踩塔(myIndex))
                绘制冰封踩塔路径(sa, myIndex);
            else
                绘制冰封分摊路径(sa, myIndex);
        }
    }

    private int 获得踩塔方位(int myIndex)
        => 冰封在西侧(myIndex) ? _udmP3Param.西塔方位 : _udmP3Param.东塔方位;

    private void 绘制冰封踩塔路径(ScriptAccessory sa, int idx)
    {
        Vector3 stackPos = Center;
        var owner = GuidanceOwner(sa, idx);
        var towerRegion = 获得踩塔方位(idx);
        Vector3 towerPos = new Vector3(100, 0, 110).RotateAndExtend(Center, towerRegion * 45f.DegToRad());

        // 凯夫卡一定先跺右脚出西塔，如果是踩塔，东塔玩家需要延迟指路，西塔玩家直接去
        bool towerDelay = towerRegion == (BlizKefkaIsNorth ? _udmP3Param.东塔方位 : _udmP3Param.西塔方位);

        if (_udmP3Param.是第一次分摊)
        {
            sa.DrawGuidance(冰封第二枚黄圈位置(idx), towerPos, 0, 3000, $"冰封路径二 黄圈 2->塔 {idx}", sa.Data.DefaultDangerColor);
            sa.DrawCircle(towerPos, 3000, towerDelay ? 3500 : 2500, $"冰封路径二 塔范围 {towerRegion}", 5f, sa.Data.DefaultSafeColor);
            sa.DrawGuidance(owner, towerPos, 3000, towerDelay ? 3500 : 2500, $"冰封路径二 黄圈 塔 {idx}", sa.Data.DefaultSafeColor);
            sa.DrawGuidance(towerPos, stackPos, 3000, towerDelay ? 3500 : 2500, $"冰封路径二 踩塔->分摊 {idx}", sa.Data.DefaultDangerColor);
        }
        else
        {
            sa.DrawCircle(towerPos, towerDelay ? 1000 : 0, towerDelay ? 3000 : 4000,
                $"冰封路径二 塔范围 {towerRegion}", 6f, sa.Data.DefaultSafeColor);
            sa.DrawGuidance(owner, towerPos, towerDelay ? 1000 : 0, towerDelay ? 3000 : 4000,
                $"冰封路径二 塔 第二轮 {idx}", sa.Data.DefaultSafeColor);
        }

    }

    private void 绘制冰封分摊路径(ScriptAccessory sa, int idx)
    {
        Vector3 stackPos = Center;
        var owner = GuidanceOwner(sa, idx);
        var towerRegion = 获得踩塔方位(idx);
        Vector3 towerPos = new Vector3(100, 0, 110).RotateAndExtend(Center, towerRegion * 45f.DegToRad());
        bool towerDelay = towerRegion == (BlizKefkaIsNorth ? _udmP3Param.东塔方位 : _udmP3Param.西塔方位);

        if (_udmP3Param.是第一次分摊)
        {
            sa.DrawGuidance(冰封第二枚黄圈位置(idx), stackPos, 0, 3000, $"冰封路径二 黄圈 2->分摊 {idx}", sa.Data.DefaultDangerColor);
            sa.DrawGuidance(owner, stackPos, 3000, towerDelay ? 3500 : 2500, $"冰封路径二 黄圈 分摊 {idx}", sa.Data.DefaultSafeColor);
            sa.DrawGuidance(stackPos, towerPos, 3000, towerDelay ? 3500 : 2500, $"冰封路径二 分摊->踩塔 {idx}", sa.Data.DefaultDangerColor);
        }
        else
        {
            sa.DrawGuidance(owner, stackPos, towerDelay ? 1000 : 0, towerDelay ? 3000 : 4000, $"冰封路径二 分摊 第二轮 {idx}", sa.Data.DefaultSafeColor);
        }
    }

    [ScriptMethod(name: "P3B2_轰击/顶起", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:47875", "TargetIndex:1"], userControl: true)]
    public void P3B2_轰击旋风(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.当前阶段 != 3210) return;
        if (Vector3.Distance(_udmP3Param.第一次轰击位置, ev.TargetPosition) > 80)
            _udmP3Param.第一次轰击位置 = ev.TargetPosition;
        else
        {
            sa.DrawCircle(ev.TargetPosition, 0, 15000, $"轰击旋风1", 6, sa.Data.DefaultDangerColor.WithW(2f));
            sa.DrawCircle(_udmP3Param.第一次轰击位置, 0, 15000, $"轰击旋风2", 6, sa.Data.DefaultDangerColor.WithW(2f));

            // 顺便恢复透明度
            var obj1 = sa.GetById(_udmP3Param.ObjectId_卡奥斯);
            sa.AlphaModify(obj1, 1f, currentAlpha => currentAlpha <= 0.8f);
            var obj2 = sa.GetById(_udmP3Param.ObjectId_艾克斯迪司);
            sa.AlphaModify(obj2, 1f, currentAlpha => currentAlpha <= 0.8f);
        }

    }

    [ScriptMethod(name: "P3B2_顶起删除轰击旋风", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:47878", "TargetIndex:1"], userControl: Debugging)]
    public void P3B2_顶起删除轰击旋风(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw($"轰击旋风.*");
        _udmP3Param.当前阶段 = 3300;
    }

    [ScriptMethod(name: "P3B2_冰封移动提示", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:47889"], userControl: true)]
    public async void P3B2_冰封移动提示(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.当前阶段 != 3210) return;

        var runId = _runId;
        await Task.Delay(1000);
        if (runId != _runId) return;
        if (_udmP3Param.当前阶段 != 3210) return;

        sa.Method.TextInfo($"远离场中 动动动", 3000);
        sa.Method.TTS($"远离场中 动动动", 3);
    }

    #region P3B2 轰击 范围（Cicero 灵视）

    [ScriptMethod(name:"P3 地震 轰击 (范围)",
        eventType:EventTypeEnum.TargetIcon,
        eventCondition:["Id:00A1"])]

    public void P3_地震_轰击_范围(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=3) {

            return;

        }

        if(phase!=3) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        if(targetId==accessory.Data.Me) {

            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(6);
            currentProperties.Owner=targetId;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=5125;

            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,currentProperties);

        }

        else {

            int targetIndex=accessory.Data.PartyList.IndexOf(((uint)targetId));

            if(!isLegalPartyIndex(targetIndex)) {

                return;

            }

            int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);

            if(!isLegalPartyIndex(myIndex)) {

                return;

            }

            if(isSupporter(targetIndex)==isSupporter(myIndex)
               &&
               isDps(targetIndex)==isDps(myIndex)) {

                currentProperties=accessory.Data.GetDefaultDrawProperties();

                currentProperties.Scale=new(6);
                currentProperties.Owner=targetId;
                currentProperties.Color=accessory.Data.DefaultSafeColor;
                currentProperties.DestoryAt=5125;

                accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,currentProperties);

                /*

                currentProperties=accessory.Data.GetDefaultDrawProperties();

                currentProperties.Scale=new(2);
                currentProperties.Owner=accessory.Data.Me;
                currentProperties.TargetObject=targetId;
                currentProperties.ScaleMode|=ScaleMode.YByDistance;
                currentProperties.Color=accessory.Data.DefaultSafeColor;
                currentProperties.DestoryAt=5125;

                accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);

                // The guidance part has been removed to prevent confusion.

                */

            }

            else {

                currentProperties=accessory.Data.GetDefaultDrawProperties();

                currentProperties.Scale=new(6);
                currentProperties.Owner=targetId;
                currentProperties.Color=accessory.Data.DefaultDangerColor;
                currentProperties.DestoryAt=5125;

                accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,currentProperties);

            }

        }

    }

    #endregion

    #endregion P3B2 冰封 3210

    #endregion

    #region P4 绘制（Cicero 灵视）

    #region Major_Phase_4

    [ScriptMethod(name: "———————— 《P4》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"])]
    public void P4_分割线(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name:"P4 (阶段控制)",
        eventType:EventTypeEnum.PlayActionTimeline,
        eventCondition:["Id:7747"],
        userControl:false)]

    public void P4_阶段控制(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=3) {

            return;

        }

        if(!string.Equals(@event["SourceDataId"],"18475")) {

            return;

        }

        accessory.Method.RemoveDraw(".*");

        majorPhase=4;
        phase=1;

        if(enableDebugLogging) {

            accessory.Log.Debug($"majorPhase={majorPhase}\nphase={phase}");

        }

    }

    [ScriptMethod(name:"P4 新生艾克斯迪司 (数据收集)",
        eventType:EventTypeEnum.StatusAdd,
        eventCondition:["StatusID:2056"],
        userControl:false)]

    public void P4_新生艾克斯迪司_数据收集(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=4) {

            return;

        }

        if(string.Equals(@event["SourceId"],"00000000")) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        var targetObject=accessory.Data.Objects.SearchById(targetId);

        if(targetObject==null||!targetObject.IsValid()) {

            return;

        }

        else {

            if(targetObject.DataId!=19510) {

                return;

            }

        }

        if(string.Equals(@event["Param"],"1122")) {

            phase4_fakeNeoExdeathAction=false;

        }

        if(string.Equals(@event["Param"],"1121")) {

            phase4_fakeNeoExdeathAction=true;

        }

    }

    [ScriptMethod(name:"P4 卡奥斯 (数据收集)",
        eventType:EventTypeEnum.StatusAdd,
        eventCondition:["StatusID:2056"],
        userControl:false)]

    public void P4_卡奥斯_数据收集(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=4) {

            return;

        }

        if(string.Equals(@event["SourceId"],"00000000")) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        var targetObject=accessory.Data.Objects.SearchById(targetId);

        if(targetObject==null||!targetObject.IsValid()) {

            return;

        }

        else {

            if(targetObject.DataId!=19507) {

                return;

            }

        }

        if(string.Equals(@event["Param"],"1120")) {

            phase4_fakeChaosAction=false;

        }

        if(string.Equals(@event["Param"],"1119")) {

            phase4_fakeChaosAction=true;

        }

    }

    [ScriptMethod(name:"P4 诅咒之嚎 (类型指示)",
        eventType:EventTypeEnum.StatusAdd,
        eventCondition:["StatusID:5543"])]

    public void P4_诅咒之嚎_类型指示(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=4) {

            return;

        }

        if(string.Equals(@event["SourceId"],"00000000")) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        if(targetId==accessory.Data.Me) {

            if(phase4_disableCursedShriekOnMe) {

                return;

            }

        }

        int durationMilliseconds=0;

        try {

            durationMilliseconds=JsonConvert.DeserializeObject<int>(@event["DurationMilliseconds"]);

        } catch(Exception e) {

            accessory.Log.Error("DurationMilliseconds deserialization failed.");

            return;

        }

        if(durationMilliseconds<=0||durationMilliseconds>MAXIMUM_DURATION) {

            return;

        }

        int standardDuration=8000;
        int delay=0;
        int duration=0;

        if(durationMilliseconds<=standardDuration) {

            delay=0;
            duration=durationMilliseconds;

        }

        else {

            delay=durationMilliseconds-standardDuration;
            duration=standardDuration;

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        if(!phase4_fakeNeoExdeathAction) {

            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Name=$"P4_诅咒之嚎_类型指示1_{targetId}";
            currentProperties.Scale=new(0.125f,6);
            currentProperties.Owner=targetId;
            currentProperties.FixRotation=true;
            currentProperties.Rotation=float.Pi/4;
            currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(4);
            currentProperties.Delay=delay;
            currentProperties.DestoryAt=duration;

            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Straight,currentProperties);

            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Name=$"P4_诅咒之嚎_类型指示2_{targetId}";
            currentProperties.Scale=new(0.125f,6);
            currentProperties.Owner=targetId;
            currentProperties.FixRotation=true;
            currentProperties.Rotation=-(float.Pi/4);
            currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(4);
            currentProperties.Delay=delay;
            currentProperties.DestoryAt=duration;

            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Straight,currentProperties);

        }

        else {

            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Name=$"P4_诅咒之嚎_类型指示1_{targetId}";
            currentProperties.Scale=new(0.125f,2);
            currentProperties.Owner=targetId;
            currentProperties.FixRotation=true;
            currentProperties.Rotation=-(float.Pi/4*3);
            currentProperties.Color=accessory.Data.DefaultSafeColor.WithW(4);
            currentProperties.Delay=delay;
            currentProperties.DestoryAt=duration;

            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Rect,currentProperties);

            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Name=$"P4_诅咒之嚎_类型指示2_{targetId}";
            currentProperties.Scale=new(0.125f,4);
            currentProperties.Owner=targetId;
            currentProperties.FixRotation=true;
            currentProperties.Rotation=float.Pi/4*3;
            currentProperties.Color=accessory.Data.DefaultSafeColor.WithW(4);
            currentProperties.Delay=delay;
            currentProperties.DestoryAt=duration;

            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Rect,currentProperties);

        }

    }

    [ScriptMethod(name:"P4 诅咒之嚎 (面向指示)",
        eventType:EventTypeEnum.StatusAdd,
        eventCondition:["StatusID:5543"])]

    public void P4_诅咒之嚎_面向指示(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=4) {

            return;

        }

        if(string.Equals(@event["SourceId"],"00000000")) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        if(!Debugging&&targetId==accessory.Data.Me) {
            return;
        }
        int durationMilliseconds=0;
        try {
            durationMilliseconds=JsonConvert.DeserializeObject<int>(@event["DurationMilliseconds"]);
        } catch(Exception e) {
            accessory.Log.Error("DurationMilliseconds deserialization failed.");
            return;
        }
        if(durationMilliseconds<=0||durationMilliseconds>MAXIMUM_DURATION) {
            return;
        }
        int standardDuration=8000;
        int delay=0;
        int duration=0;

        if(durationMilliseconds<=standardDuration) {

            delay=0;
            duration=durationMilliseconds;

        }

        else {

            delay=durationMilliseconds-standardDuration;
            duration=standardDuration;

        }

        int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
        for(int partyIndex=0;partyIndex<accessory.Data.PartyList.Count;++partyIndex) {
            if(!Debugging&&partyIndex!=myIndex) {
                continue;
            }
            if(accessory.Data.PartyList[partyIndex]==targetId) {
                continue;
            }
            var currentProperties=accessory.Data.GetDefaultDrawProperties();
            currentProperties.Name=$"P4_诅咒之嚎_面向指示_{targetId}_{partyIndex}";
            currentProperties.Scale=new(1,3);
            currentProperties.Owner=GuidanceOwner(accessory,partyIndex);
            currentProperties.TargetObject=targetId;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.Delay=delay;
            currentProperties.DestoryAt=duration;
            if(!phase4_fakeNeoExdeathAction) {
                currentProperties.Rotation=float.Pi;
            }
            else {
                currentProperties.Rotation=0;
            }
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Arrow,currentProperties);
        }
    }

    [ScriptMethod(name:"P4 诅咒之嚎 (指示清除)",
        eventType:EventTypeEnum.StatusRemove,
        eventCondition:["StatusID:5543"],
        userControl:false)]

    public void P4_诅咒之嚎_指示清除(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=4) {

            return;

        }

        if(string.Equals(@event["SourceId"],"00000000")) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        accessory.Method.RemoveDraw($"P4_诅咒之嚎_类型指示1_{targetId}");
        accessory.Method.RemoveDraw($"P4_诅咒之嚎_类型指示2_{targetId}");
        accessory.Method.RemoveDraw($"P4_诅咒之嚎_面向指示_{targetId}(_\\d)?");

    }

    [ScriptMethod(name:"P4 叉形闪电 (范围)",
        eventType:EventTypeEnum.StatusAdd,
        eventCondition:["StatusID:5544"])]

    public void P4_叉形闪电_范围(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=4) {

            return;

        }

        if(string.Equals(@event["SourceId"],"00000000")) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        int durationMilliseconds=0;

        try {

            durationMilliseconds=JsonConvert.DeserializeObject<int>(@event["DurationMilliseconds"]);

        } catch(Exception e) {

            accessory.Log.Error("DurationMilliseconds deserialization failed.");

            return;

        }

        if(durationMilliseconds<=0||durationMilliseconds>MAXIMUM_DURATION) {

            return;

        }

        int standardDuration=4250;
        int delay=0;
        int duration=0;

        if(durationMilliseconds<=standardDuration) {

            delay=0;
            duration=durationMilliseconds;

        }

        else {

            delay=durationMilliseconds-standardDuration;
            duration=standardDuration;

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        currentProperties.Name=$"P4_叉形闪电_范围_{targetId}";
        currentProperties.Scale=new(8);
        currentProperties.Owner=targetId;
        currentProperties.Delay=delay;
        currentProperties.DestoryAt=duration;

        if(!phase4_fakeNeoExdeathAction) {

            currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(1);

        }

        else {

            currentProperties.Color=colourOfDirectionIndicators.V4.WithW(1);

        }

        accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Circle,currentProperties);

    }

    [ScriptMethod(name:"P4 叉形闪电 (范围清除)",
        eventType:EventTypeEnum.StatusRemove,
        eventCondition:["StatusID:5544"],
        userControl:false)]

    public void P4_叉形闪电_范围清除(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=4) {

            return;

        }

        if(string.Equals(@event["SourceId"],"00000000")) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        accessory.Method.RemoveDraw($"P4_叉形闪电_范围_{targetId}");

    }

    [ScriptMethod(name:"P4 水属性压缩 (范围)",
        eventType:EventTypeEnum.StatusAdd,
        eventCondition:["StatusID:5545"])]

    public void P4_水属性压缩_范围(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=4) {

            return;

        }

        if(string.Equals(@event["SourceId"],"00000000")) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        int durationMilliseconds=0;

        try {

            durationMilliseconds=JsonConvert.DeserializeObject<int>(@event["DurationMilliseconds"]);

        } catch(Exception e) {

            accessory.Log.Error("DurationMilliseconds deserialization failed.");

            return;

        }

        if(durationMilliseconds<=0||durationMilliseconds>MAXIMUM_DURATION) {

            return;

        }

        int standardDuration=4250;
        int delay=0;
        int duration=0;

        if(durationMilliseconds<=standardDuration) {

            delay=0;
            duration=durationMilliseconds;

        }

        else {

            delay=durationMilliseconds-standardDuration;
            duration=standardDuration;

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        currentProperties.Name=$"P4_水属性压缩_范围_{targetId}";
        currentProperties.Scale=new(8);
        currentProperties.Owner=targetId;
        currentProperties.Delay=delay;
        currentProperties.DestoryAt=duration;

        if(!phase4_fakeNeoExdeathAction) {

            currentProperties.Color=colourOfDirectionIndicators.V4.WithW(1);

        }

        else {

            currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(1);

        }

        accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Circle,currentProperties);

    }

    [ScriptMethod(name:"P4 水属性压缩 (范围清除)",
        eventType:EventTypeEnum.StatusRemove,
        eventCondition:["StatusID:5545"],
        userControl:false)]

    public void P4_水属性压缩_范围清除(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=4) {

            return;

        }

        if(string.Equals(@event["SourceId"],"00000000")) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        accessory.Method.RemoveDraw($"P4_水属性压缩_范围_{targetId}");

    }

    [ScriptMethod(name:"P4 混沌之炎 (引导范围)",
        eventType:EventTypeEnum.StatusAdd,
        eventCondition:["StatusID:5547"])]

    public void P4_混沌之炎_引导范围(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=4) {

            return;

        }

        if(string.Equals(@event["SourceId"],"00000000")) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        if(phase4_disableStrayBaitOnOthers) {

            if(targetId!=accessory.Data.Me) {

                return;

            }

        }

        int durationMilliseconds=0;

        try {

            durationMilliseconds=JsonConvert.DeserializeObject<int>(@event["DurationMilliseconds"]);

        } catch(Exception e) {

            accessory.Log.Error("DurationMilliseconds deserialization failed.");

            return;

        }

        if(durationMilliseconds<=0||durationMilliseconds>MAXIMUM_DURATION) {

            return;

        }

        int standardDuration=5500;
        int delay=0;
        int duration=0;

        if(durationMilliseconds<=standardDuration) {

            delay=0;
            duration=durationMilliseconds;

        }

        else {

            delay=durationMilliseconds-standardDuration;
            duration=standardDuration;

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        if(!phase4_fakeChaosAction) {

            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Name=$"P4_混沌之炎_引导范围_{targetId}";
            currentProperties.Scale=new(6);
            currentProperties.Owner=targetId;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.Delay=delay;
            currentProperties.DestoryAt=duration;

            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,currentProperties);

        }

        else {

            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Name=$"P4_混沌之炎_引导范围_{targetId}";
            currentProperties.Scale=new(40);
            currentProperties.InnerScale=new(6);
            currentProperties.Radian=float.Pi*2;
            currentProperties.Owner=targetId;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.Delay=delay;
            currentProperties.DestoryAt=duration;

            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Donut,currentProperties);

        }

    }

    [ScriptMethod(name:"P4 混沌之炎 (引导范围清除)",
        eventType:EventTypeEnum.StatusRemove,
        eventCondition:["StatusID:5547"],
        userControl:false)]

    public void P4_混沌之炎_引导范围清除(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=4) {

            return;

        }

        if(string.Equals(@event["SourceId"],"00000000")) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        accessory.Method.RemoveDraw($"P4_混沌之炎_引导范围_{targetId}");

    }

    [ScriptMethod(name:"P4 混沌之水 (引导范围)",
        eventType:EventTypeEnum.StatusAdd,
        eventCondition:["StatusID:5548"])]

    public void P4_混沌之水_引导范围(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=4) {

            return;

        }

        if(string.Equals(@event["SourceId"],"00000000")) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        if(phase4_disableStrayBaitOnOthers) {

            if(targetId!=accessory.Data.Me) {

                return;

            }

        }

        int durationMilliseconds=0;

        try {

            durationMilliseconds=JsonConvert.DeserializeObject<int>(@event["DurationMilliseconds"]);

        } catch(Exception e) {

            accessory.Log.Error("DurationMilliseconds deserialization failed.");

            return;

        }

        if(durationMilliseconds<=0||durationMilliseconds>MAXIMUM_DURATION) {

            return;

        }

        int standardDuration=5500;
        int delay=0;
        int duration=0;

        if(durationMilliseconds<=standardDuration) {

            delay=0;
            duration=durationMilliseconds;

        }

        else {

            delay=durationMilliseconds-standardDuration;
            duration=standardDuration;

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        if(!phase4_fakeChaosAction) {

            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Name=$"P4_混沌之水_引导范围_{targetId}";
            currentProperties.Scale=new(40);
            currentProperties.InnerScale=new(6);
            currentProperties.Radian=float.Pi*2;
            currentProperties.Owner=targetId;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.Delay=delay;
            currentProperties.DestoryAt=duration;

            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Donut,currentProperties);

        }

        else {

            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Name=$"P4_混沌之水_引导范围_{targetId}";
            currentProperties.Scale=new(6);
            currentProperties.Owner=targetId;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.Delay=delay;
            currentProperties.DestoryAt=duration;

            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,currentProperties);

        }

    }

    [ScriptMethod(name:"P4 混沌之水 (引导范围清除)",
        eventType:EventTypeEnum.StatusRemove,
        eventCondition:["StatusID:5548"],
        userControl:false)]

    public void P4_混沌之水_引导范围清除(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=4) {

            return;

        }

        if(string.Equals(@event["SourceId"],"00000000")) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        accessory.Method.RemoveDraw($"P4_混沌之水_引导范围_{targetId}");

    }

    [ScriptMethod(name:"P4 混沌之炎 (范围)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:regex:^(47906|47907)$"])]

    public void P4_混沌之炎_范围(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=4) {

            return;

        }

        Vector3 effectPosition=ARENA_CENTER;

        try {

            effectPosition=JsonConvert.DeserializeObject<Vector3>(@event["EffectPosition"]);

        } catch(Exception e) {

            accessory.Log.Error("EffectPosition deserialization failed.");

            return;

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        if(string.Equals(@event["ActionId"],"47906")) {

            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(6);
            currentProperties.Position=effectPosition;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.DestoryAt=5000;

            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,currentProperties);

        }

        if(string.Equals(@event["ActionId"],"47907")) {

            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(40);
            currentProperties.InnerScale=new(6);
            currentProperties.Radian=float.Pi*2;
            currentProperties.Position=effectPosition;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.DestoryAt=5000;

            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Donut,currentProperties);

        }

    }

    [ScriptMethod(name:"P4 混沌之水 (范围)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:regex:^(47908|47909)$"])]

    public void P4_混沌之水_范围(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=4) {

            return;

        }

        Vector3 effectPosition=ARENA_CENTER;

        try {

            effectPosition=JsonConvert.DeserializeObject<Vector3>(@event["EffectPosition"]);

        } catch(Exception e) {

            accessory.Log.Error("EffectPosition deserialization failed.");

            return;

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        if(string.Equals(@event["ActionId"],"47908")) {

            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(40);
            currentProperties.InnerScale=new(6);
            currentProperties.Radian=float.Pi*2;
            currentProperties.Position=effectPosition;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.DestoryAt=5000;

            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Donut,currentProperties);

        }

        if(string.Equals(@event["ActionId"],"47909")) {

            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(6);
            currentProperties.Position=effectPosition;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.DestoryAt=5000;

            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,currentProperties);

        }

    }

    [ScriptMethod(name:"P4 超越死亡与亚拉戈领域 (数据收集)",
        eventType:EventTypeEnum.StatusAdd,
        eventCondition:["StatusID:regex:^(1382|454|5464)$"],
        userControl:false)]

    public void P4_超越死亡与亚拉戈领域_数据收集(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=4) {

            return;

        }

        if(string.Equals(@event["SourceId"],"00000000")) {

            return;

        }

        if(phase4_statusWithDurationCounter>=8) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        int targetIndex=accessory.Data.PartyList.IndexOf(((uint)targetId));

        if(!isLegalPartyIndex(targetIndex)) {

            return;

        }

        lock(phase4_isBeyondDeath) {

            if(string.Equals(@event["StatusID"],"1382")) {

                phase4_isBeyondDeath[targetIndex]=true;

            }

            if(string.Equals(@event["StatusID"],"5464")) { // Square Enix sucks.

                phase4_isBeyondDeath[targetIndex]=true;

            }

            if(string.Equals(@event["StatusID"],"454")) {

                phase4_isBeyondDeath[targetIndex]=false;

            }

            Interlocked.Increment(ref phase4_statusWithDurationCounter);

            if(phase4_statusWithDurationCounter==8) {

                if(enableDebugLogging) {

                    accessory.Log.Debug($"""
                                         phase4_isBeyondDeath:{string.Join(",",phase4_isBeyondDeath)}
                                         """);

                }

            }

        }

    }

    [ScriptMethod(name:"P4 生者之伤与死者之伤 (数据收集)",
        eventType:EventTypeEnum.StatusAdd,
        eventCondition:["StatusID:regex:^(5541|5542|4887|4888)$"],
        userControl:false)]

    public void P4_生者之伤与死者之伤_数据收集(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=4) {

            return;

        }

        if(string.Equals(@event["SourceId"],"00000000")) {

            return;

        }

        if(phase4_statusWithoutDurationCounter>=8) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        int targetIndex=accessory.Data.PartyList.IndexOf(((uint)targetId));

        if(!isLegalPartyIndex(targetIndex)) {

            return;

        }

        lock(phase4_isWhiteWound) {

            if(string.Equals(@event["StatusID"],"5541")) {

                phase4_isWhiteWound[targetIndex]=true;

            }

            if(string.Equals(@event["StatusID"],"4887")) { // Square Enix sucks again.

                phase4_isWhiteWound[targetIndex]=true;

            }

            if(string.Equals(@event["StatusID"],"5542")) {

                phase4_isWhiteWound[targetIndex]=false;

            }

            if(string.Equals(@event["StatusID"],"4888")) { // Square Enix sucks again.

                phase4_isWhiteWound[targetIndex]=false;

            }

            Interlocked.Increment(ref phase4_statusWithoutDurationCounter);

            if(phase4_statusWithoutDurationCounter==8) {

                if(enableDebugLogging) {

                    accessory.Log.Debug($"""
                                         phase4_isWhiteWound:{string.Join(",",phase4_isWhiteWound)}
                                         """);

                }

            }

        }

    }

    [ScriptMethod(name:"P4 生死之境 (范围)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:50070"])]

    public void P4_生死之境_范围(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=4) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["SourceId"],out var sourceId)) {

            return;

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        currentProperties.Scale=new(2,48);
        currentProperties.Owner=sourceId;
        currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(2);
        currentProperties.DestoryAt=5500;

        accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Rect,currentProperties);

    }

    [ScriptMethod(name:"P4 生者黑暗光与死者黑暗光 (安全区范围)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:regex:^(50068|50069)$"])]

    public void P4_生者黑暗光与死者黑暗光_安全区范围(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=4) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["SourceId"],out var sourceId)) {

            return;

        }

        int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);

        if(!isLegalPartyIndex(myIndex)) {

            return;

        }

        if(phase4_isBeyondDeath[myIndex]) {

            if(phase4_isWhiteWound[myIndex]) {

                if(string.Equals(@event["ActionId"],"50069")) {

                    return;

                }

            }

            else {

                if(string.Equals(@event["ActionId"],"50068")) {

                    return;

                }

            }

        }

        else {

            if(phase4_isWhiteWound[myIndex]) {

                if(string.Equals(@event["ActionId"],"50068")) {

                    return;

                }

            }

            else {

                if(string.Equals(@event["ActionId"],"50069")) {

                    return;

                }

            }

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        currentProperties.Scale=new(21,47);
        currentProperties.Owner=sourceId;
        currentProperties.Color=accessory.Data.DefaultSafeColor;
        currentProperties.DestoryAt=5500;

        accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Rect,currentProperties);

    }

    #endregion

    #endregion

    #region P4 指路（RyougiMio）

    #region State

    private enum Phase
    {
        Init,
        P4,
        Done,
    }

    public enum P4PlanMessageMode
    {
        Off,
        Echo,
        Party,
    }

    private enum P4HalfSide
    {
        Unknown,
        Left,
        Right,
    }

    private enum P4HalfColor
    {
        Unknown,
        Blue,
        Purple,
    }

    private enum P4ChainStep
    {
        None,
        Move,
        Petrify,
        Element,
    }

    private enum P4ClockDirection
    {
        Unknown,
        Twelve,
        Three,
        Six,
        Nine,
    }

    private enum P4ElementCall
    {
        Unknown,
        FireSteel,
        WaterMoon,
    }

    private enum P4BuffGroupKind
    {
        Unknown,
        A,
        B,
        C,
        D,
        E,
        F,
    }

    private enum P4DangerShape
    {
        Rect,
        Fan,
    }

    private enum P4BuffCategory
    {
        Mixed,
        ThunderShare,
        Petrify,
        Element,
    }

    private sealed class P4BuffRecord
    {
        public int PartyIndex;
        public uint StatusId;
        public int State;
        public int DurationMs;
        public long ExpiresAt;
        public int SnapshotRemainingMs;
    }

    private sealed class P4BuffGroup
    {
        public P4BuffGroupKind Kind;
        public P4BuffCategory Category;
        public int DurationMs;
        public int SnapshotRemainingMs;
        public long ExpiresAt;
        public readonly List<P4BuffRecord> Records = new List<P4BuffRecord>();
    }

    private sealed class P4DangerRecord
    {
        public uint ActionId;
        public P4DangerShape Shape;
        public Vector3 Position;
        public float Rotation;
        public long CapturedAt;
    }

    private const uint InvalidObjectId = 0xE0000000;
    private const uint P4StartActionId = 49884;
    private const uint P4PurpleSourceActionId = 50068;
    private const uint P4ResolveActionId = 50069;
    private const uint P4ChainStartActionEffectId = 50070;
    private const uint P4TruthStatusId = 2056;
    private const uint P4XDataId = 19510;
    private const uint P4CDataId = 19507;
    private const int P4StatusWindowMs = 11000;
    private const int P4GuideDurationMs = 6000;
    private const int P4ChainGuideDurationMs = 8000;
    private const int P4ChainDelayMs = 500;
    private const int P4StateUnknown = 0;
    private const int P4StateTrue = 1;
    private const int P4StateFalse = 2;
    private const float P4GuideOffset = 4.0f;
    private const float P4ChainGuideOffset = 8.0f;
    private const int P4BuffGroupToleranceMs = 1200;
    private const int P4MaxScheduledGroupCount = 6;
    private const float P4RectDangerWidth = 10.0f;
    private const float P4RectDangerLength = 40.0f;
    private const float P4FanDangerRadius = 40.0f;
    private const float P4FanDangerRadian = MathF.PI / 2.0f;
    private const int P4DFanLookbackMs = 10000;
    private const string P4DrawPrefix = "KDYD_P4";

    private static readonly Vector3 P4ArenaCenter = new Vector3(100.0f, 0.0f, 100.0f);
    private static readonly Vector3 DefaultNorth = new Vector3(0.0f, 0.0f, -1.0f);
    private static readonly string[] PartyPriorityLabels = new[] { "MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4" };
    private static readonly uint[] P4TrackedStatusIds = new uint[] { 5541, 5542, 5543, 5544, 5545, 5546, 5547, 5548, 454, 1382 };
    private static readonly float[] P4CardinalClocks = new[] { 0.0f, 3.0f, 6.0f, 9.0f };
    private static readonly float[] P4FinalHalfClocks = new[] { 11.5f, 12.5f, 2.5f, 3.5f, 5.5f, 6.5f, 8.5f, 9.5f };

    private ScriptAccessory _acc;
    private Phase _p4Phase = Phase.Init;
    private int _generation;
    private readonly object _p4Lock = new object();
    private readonly int[,] _p4StatusStateByPartyAndStatus = new int[8, 10];
    private readonly long[,] _p4StatusExpiresAtByPartyAndStatus = new long[8, 10];
    private readonly P4HalfSide[] _p4ResolvedSideByPartyIndex = new P4HalfSide[8];
    private readonly P4HalfColor[] _p4ResolvedColorByPartyIndex = new P4HalfColor[8];
    private readonly List<P4BuffRecord> _p4BuffRecords = new List<P4BuffRecord>();
    private readonly List<P4BuffGroup> _p4BuffGroups = new List<P4BuffGroup>();
    private readonly List<P4DangerRecord> _p4DangerRecords = new List<P4DangerRecord>();
    private P4ChainStep _p4ChainStep = P4ChainStep.None;
    private int _p4ChainRound;
    private int _p4ChainStepGeneration;
    private bool _p4BuffGroupsScheduled;
    private bool _p4BuffGroupsScheduleQueued;
    private bool _p4FGroupDangerDrawn;
    private long _p4FGroupStartedAt;
    private P4ElementCall _p4CElementCall = P4ElementCall.Unknown;
    private P4ElementCall _p4FElementCall = P4ElementCall.Unknown;
    private int _p4XParam;
    private long _p4XExpiresAt;
    private long _p4XUpdatedAt;
    private int _p4CParam;
    private long _p4CExpiresAt;
    private long _p4CUpdatedAt;
    private int _p4XEventCount;
    private int _p4GroupSnapshotParam;
    private long _p4GroupSnapshotAt;
    private int _p4FourthXParam;
    private long _p4FourthSnapshotAt;
    private Vector3 _p4FourthTargetPosition;
    private Vector3 _p4FourthNewTwelveDirection;
    private bool _p4FourthDirectionReady;
    private Vector3 _p4HotpotBlueSourcePosition;
    private Vector3 _p4HotpotPurpleSourcePosition;
    private bool _p4HotpotBlueSourceReady;
    private bool _p4HotpotPurpleSourceReady;
    private bool _p4HotpotResolved;

    #endregion

    #region Initialization

    private void Init_RyougiMio(ScriptAccessory accessory)
    {
        _acc = accessory;
        _p4Phase = Phase.Init;
        _generation++;
        ResetP4State();

        accessory.Method.RemoveDraw(".*");
    }

    private void ResetMechanic(ScriptAccessory accessory, bool removeDraw = true)
    {
        _generation++;
        ResetP4State();

        if (removeDraw)
            accessory.Method.RemoveDraw($"{P4DrawPrefix}_.*");
    }

    private void SetPhase(ScriptAccessory accessory, Phase phase)
    {
        if (_p4Phase == phase) return;

        _p4Phase = phase;
        ResetMechanic(accessory);
    }

    private void ResetP4State()
    {

        lock (_p4Lock)
        {
            Array.Clear(_p4StatusStateByPartyAndStatus, 0, _p4StatusStateByPartyAndStatus.Length);
            Array.Clear(_p4StatusExpiresAtByPartyAndStatus, 0, _p4StatusExpiresAtByPartyAndStatus.Length);
            Array.Clear(_p4ResolvedSideByPartyIndex, 0, _p4ResolvedSideByPartyIndex.Length);
            Array.Clear(_p4ResolvedColorByPartyIndex, 0, _p4ResolvedColorByPartyIndex.Length);
            _p4BuffRecords.Clear();
            _p4BuffGroups.Clear();
            _p4DangerRecords.Clear();

            _p4ChainStep = P4ChainStep.None;
            _p4ChainRound = 0;
            _p4ChainStepGeneration++;
            _p4BuffGroupsScheduled = false;
            _p4BuffGroupsScheduleQueued = false;
            _p4FGroupDangerDrawn = false;
            _p4FGroupStartedAt = 0;
            _p4CElementCall = P4ElementCall.Unknown;
            _p4FElementCall = P4ElementCall.Unknown;
            _p4XParam = 0;
            _p4XExpiresAt = 0;
            _p4XUpdatedAt = 0;
            _p4CParam = 0;
            _p4CExpiresAt = 0;
            _p4CUpdatedAt = 0;
            _p4XEventCount = 0;
            _p4GroupSnapshotParam = 0;
            _p4GroupSnapshotAt = 0;
            _p4FourthXParam = 0;
            _p4FourthSnapshotAt = 0;
            _p4FourthTargetPosition = default;
            _p4FourthNewTwelveDirection = DefaultNorth;
            _p4FourthDirectionReady = false;
            _p4HotpotBlueSourcePosition = default;
            _p4HotpotPurpleSourcePosition = default;
            _p4HotpotBlueSourceReady = false;
            _p4HotpotPurpleSourceReady = false;
            _p4HotpotResolved = false;
        }
    }

    #endregion

    #region Helpers

    private void QTTS(string text, int rate = 0)
    {
        if (!enableVanillaTts || _acc == null) return;
        _acc.Method.TTS(text, rate);
    }

    private void QText(string text, int duration, bool isWarning = false)
    {
        if (!enablePrompts || _acc == null) return;
        _acc.Method.TextInfo(text, duration, isWarning);
    }

    private void Alert(string text, int duration = 3000, bool isWarning = true)
    {
        QTTS(text);
        QText(text, duration, isWarning);
    }

    private void DebugEcho(ScriptAccessory accessory, string message)
    {
        if (!enableDebugLogging) return;
        accessory.Log.Debug($"[KDYD_P4] {message}");
        accessory.Method.SendChat($"/e [KDYD_P4] {message}");
    }

    private static long NowMs()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    private static int Duration(Event @event, int fallback = 5000)
    {
        return int.TryParse(@event["DurationMilliseconds"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var duration)
            && duration > 0
            ? duration
            : fallback;
    }

    private static bool TryGetDurationMs(Event @event, out int durationMs)
    {
        durationMs = 0;

        var raw = @event["DurationMilliseconds"];
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var duration))
            return false;

        if (duration <= 0.0 || duration > int.MaxValue)
            return false;

        durationMs = (int)Math.Round(duration);
        return durationMs > 0;
    }

    private static bool TryParseObjectId(string text, out uint id)
    {
        id = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        text = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text.Substring(2) : text;
        return uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out id);
    }

    private static bool TryParseUInt(string text, out uint value)
    {
        value = 0;
        return !string.IsNullOrWhiteSpace(text)
            && uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryGetActionId(Event @event, out uint actionId)
    {
        return TryParseUInt(@event["ActionId"], out actionId);
    }

    private static bool TryGetStatusId(Event @event, out uint statusId)
    {
        var raw = @event["StatusID"];
        if (string.IsNullOrWhiteSpace(raw))
            raw = @event["StatusId"];

        return TryParseUInt(raw, out statusId);
    }

    private static bool TryGetSourceId(Event @event, out uint sourceId)
    {
        var raw = @event["SourceId"];
        if (string.IsNullOrWhiteSpace(raw))
            raw = @event["SourceID"];

        if (!TryParseObjectId(raw, out sourceId))
            sourceId = (uint)@event.SourceId;

        return sourceId != 0 && sourceId != InvalidObjectId;
    }

    private static bool TryGetTargetId(Event @event, out uint targetId)
    {
        var raw = @event["TargetId"];
        if (string.IsNullOrWhiteSpace(raw))
            raw = @event["TargetID"];

        if (!TryParseObjectId(raw, out targetId))
            targetId = (uint)@event.TargetId;

        return targetId != 0 && targetId != InvalidObjectId;
    }

    private static bool TryGetTargetDataId(Event @event, ScriptAccessory accessory, out uint dataId)
    {
        dataId = 0;

        var raw = @event["TargetDataId"];
        if (string.IsNullOrWhiteSpace(raw))
            raw = @event["TargetDataID"];
        if (string.IsNullOrWhiteSpace(raw))
            raw = @event["DataId"];
        if (string.IsNullOrWhiteSpace(raw))
            raw = @event["DataID"];

        if (TryParseUInt(raw, out dataId) && dataId != 0)
            return true;

        if (!TryGetTargetId(@event, out var targetId))
            return false;

        var obj = accessory.Data.Objects.SearchById(targetId);
        if (obj == null) return false;

        dataId = obj.DataId;
        return dataId != 0;
    }

    private static Vector3 ResolveEventTargetPosition(Event @event, ScriptAccessory accessory)
    {
        var position = @event.TargetPosition;
        if (IsLikelyArenaPosition(position))
            return position;

        if (TryGetTargetId(@event, out var targetId))
        {
            var obj = accessory.Data.Objects.SearchById(targetId);
            if (obj != null)
                return obj.Position;
        }

        return position;
    }

    private static uint GetMyId(ScriptAccessory accessory)
    {
        var myObject = accessory.Data.MyObject;
        if (myObject != null)
        {
            var myObjectId = (uint)myObject.EntityId;
            if (myObjectId != 0 && myObjectId != InvalidObjectId)
                return myObjectId;
        }

        return accessory.Data.Me;
    }

    private static int GetPlayerIndex(ScriptAccessory accessory, uint objectId)
    {
        for (var i = 0; i < accessory.Data.PartyList.Count; i++)
        {
            if (accessory.Data.PartyList[i] == objectId)
                return i;
        }

        return -1;
    }

    private static int GetMyIndex(ScriptAccessory accessory)
    {
        return GetPlayerIndex(accessory, GetMyId(accessory));
    }

    private static string FormatObjectId(uint objectId)
    {
        return objectId == 0 ? "-" : objectId.ToString("X8", CultureInfo.InvariantCulture);
    }

    private static string FormatPosition(Vector3 position)
    {
        return $"({position.X:F2},{position.Y:F2},{position.Z:F2})";
    }

    private static string PartyPriorityLabel(int partyIndex)
    {
        if (partyIndex < 0)
            return "-";

        return partyIndex < PartyPriorityLabels.Length
            ? PartyPriorityLabels[partyIndex]
            : $"P{partyIndex + 1}";
    }

    private static bool IsLikelyArenaPosition(Vector3 position)
    {
        return position.X > 50.0f && position.X < 150.0f
            && position.Z > 50.0f && position.Z < 150.0f;
    }

    private static bool TryNormalizeFromCenter(Vector3 position, out Vector3 direction)
    {
        var delta = new Vector3(position.X - P4ArenaCenter.X, 0.0f, position.Z - P4ArenaCenter.Z);
        var length = MathF.Sqrt(delta.X * delta.X + delta.Z * delta.Z);
        if (length < 0.001f)
        {
            direction = DefaultNorth;
            return false;
        }

        direction = new Vector3(delta.X / length, 0.0f, delta.Z / length);
        return true;
    }

    private static Vector3 RightVectorFromNewTwelve(Vector3 newTwelveDirection)
    {
        return new Vector3(-newTwelveDirection.Z, 0.0f, newTwelveDirection.X);
    }

    private static P4HalfSide SideOfPosition(Vector3 position, Vector3 rightVector)
    {
        var delta = new Vector3(position.X - P4ArenaCenter.X, 0.0f, position.Z - P4ArenaCenter.Z);
        var dot = delta.X * rightVector.X + delta.Z * rightVector.Z;
        return dot >= 0.0f ? P4HalfSide.Right : P4HalfSide.Left;
    }

    private static P4HalfSide OppositeSide(P4HalfSide side)
    {
        if (side == P4HalfSide.Left) return P4HalfSide.Right;
        if (side == P4HalfSide.Right) return P4HalfSide.Left;
        return P4HalfSide.Unknown;
    }

    private static P4HalfColor OppositeColor(P4HalfColor color)
    {
        if (color == P4HalfColor.Blue) return P4HalfColor.Purple;
        if (color == P4HalfColor.Purple) return P4HalfColor.Blue;
        return P4HalfColor.Unknown;
    }

    private static bool TryGetP4TrackedStatusIndex(uint statusId, out int index)
    {
        if (statusId >= 5541 && statusId <= 5548)
        {
            index = (int)(statusId - 5541);
            return true;
        }

        if (statusId == 454)
        {
            index = 8;
            return true;
        }

        if (statusId == 1382)
        {
            index = 9;
            return true;
        }

        index = -1;
        return false;
    }

    private static bool TryGetP4StateValueFromParam(int param, out int value)
    {
        if (param == 1122 || param == 1120)
        {
            value = P4StateTrue;
            return true;
        }

        if (param == 1121 || param == 1119)
        {
            value = P4StateFalse;
            return true;
        }

        value = P4StateUnknown;
        return false;
    }

    #region P4 Hotpot Helpers

    private static bool TryGetP4DirectTrueStatus(uint statusId, out uint trueStatusId)
    {
        switch (statusId)
        {
            case 4887:
                trueStatusId = 5542;
                return true;
            case 4888:
                trueStatusId = 5541;
                return true;
            case 5464:
                trueStatusId = 454;
                return true;
            default:
                trueStatusId = 0;
                return false;
        }
    }

    private bool TryGetActiveP4StateValueLocked(long now, out int value)
    {
        return TryGetActiveP4StateValueLocked(now, out value, out _, out _, out _);
    }

    private bool TryGetActiveP4StateValueLocked(long now, out int value, out string windowName, out int param, out int remainingMs)
    {
        value = P4StateUnknown;
        windowName = "-";
        param = 0;
        remainingMs = 0;

        var hasX = _p4XParam != 0 && now <= _p4XExpiresAt;
        var hasC = _p4CParam != 0 && now <= _p4CExpiresAt;
        if (!hasX && !hasC)
            return false;

        if (hasX && (!hasC || _p4XUpdatedAt >= _p4CUpdatedAt))
        {
            windowName = "X";
            param = _p4XParam;
            remainingMs = RemainingMs(_p4XExpiresAt, now);
        }
        else
        {
            windowName = "C";
            param = _p4CParam;
            remainingMs = RemainingMs(_p4CExpiresAt, now);
        }

        return TryGetP4StateValueFromParam(param, out value);
    }

    private static Vector3 ExtendGame(Vector3 position, float gameRotation, float length)
    {
        return new Vector3(
            position.X + MathF.Sin(gameRotation) * length,
            position.Y,
            position.Z + MathF.Cos(gameRotation) * length);
    }

    private static Vector3 ExtendDir(Vector3 position, float directionRadians, float length)
    {
        return new Vector3(
            position.X + MathF.Sin(directionRadians) * length,
            position.Y,
            position.Z - MathF.Cos(directionRadians) * length);
    }

    private static float Norm2Pi(float radians)
    {
        var value = radians % (2.0f * MathF.PI);
        return value < 0 ? value + 2.0f * MathF.PI : value;
    }

    private static float GameToDir(float gameRotation)
    {
        return Norm2Pi(MathF.PI - gameRotation);
    }

    private static float DirToGame(float directionRadians)
    {
        return MathF.PI - directionRadians;
    }

    private static int Dir8(Vector3 position, Vector3 center)
    {
        var direction = Math.Round(4.0 - 4.0 * Math.Atan2(position.X - center.X, position.Z - center.Z) / Math.PI) % 8.0;
        return direction < 0 ? (int)direction + 8 : (int)direction;
    }

    private static bool TryResolveExclusiveP4Truth(int stateA, uint statusA, int stateB, uint statusB, out uint trueStatus)
    {
        if (stateA == P4StateTrue)
        {
            trueStatus = statusA;
            return true;
        }

        if (stateB == P4StateTrue)
        {
            trueStatus = statusB;
            return true;
        }

        if (stateA == P4StateFalse)
        {
            trueStatus = statusB;
            return true;
        }

        if (stateB == P4StateFalse)
        {
            trueStatus = statusA;
            return true;
        }

        trueStatus = 0;
        return false;
    }

    private static bool TryResolveP4PlayerColor(int[,] states, int partyIndex, out P4HalfColor color, out uint trueShapeStatus, out uint trueColorStatus)
    {
        color = P4HalfColor.Unknown;
        trueShapeStatus = 0;
        trueColorStatus = 0;

        if (partyIndex < 0 || partyIndex >= 8)
            return false;

        if (!TryResolveExclusiveP4Truth(states[partyIndex, 0], 5541, states[partyIndex, 1], 5542, out trueShapeStatus))
            return false;

        if (!TryResolveExclusiveP4Truth(states[partyIndex, 8], 454, states[partyIndex, 9], 1382, out trueColorStatus))
            return false;

        if ((trueShapeStatus == 5541 && trueColorStatus == 454)
            || (trueShapeStatus == 5542 && trueColorStatus == 1382))
        {
            color = P4HalfColor.Blue;
            return true;
        }

        if ((trueShapeStatus == 5541 && trueColorStatus == 1382)
            || (trueShapeStatus == 5542 && trueColorStatus == 454))
        {
            color = P4HalfColor.Purple;
            return true;
        }

        return false;
    }

    private static P4HalfSide SideForColor(P4HalfColor color, P4HalfColor leftColor, P4HalfColor rightColor)
    {
        if (color == leftColor) return P4HalfSide.Left;
        if (color == rightColor) return P4HalfSide.Right;
        return P4HalfSide.Unknown;
    }

    #endregion

    private static string FormatP4Side(P4HalfSide side)
    {
        switch (side)
        {
            case P4HalfSide.Left: return "L";
            case P4HalfSide.Right: return "R";
            default: return "-";
        }
    }

    private static string FormatP4Color(P4HalfColor color)
    {
        switch (color)
        {
            case P4HalfColor.Blue: return "蓝";
            case P4HalfColor.Purple: return "紫";
            default: return "-";
        }
    }

    private static string FormatP4StatusStates(int[,] states, int partyIndex)
    {
        return string.Join(" ", P4TrackedStatusIds.Select((statusId, statusIndex) => $"{statusId}={states[partyIndex, statusIndex]}"));
    }

    private static int RemainingMs(long expiresAt, long now)
    {
        if (expiresAt <= now)
            return 0;

        var remaining = expiresAt - now;
        return remaining > int.MaxValue ? int.MaxValue : (int)remaining;
    }

    private static string FormatRemainingMs(int remainingMs)
    {
        return remainingMs > 0
            ? $"{remainingMs / 1000.0:F1}s"
            : "-";
    }

    private static string FormatP4StatusStatesWithRemaining(int[,] states, long[,] expiresAt, int partyIndex, long now)
    {
        return string.Join(
            " ",
            P4TrackedStatusIds.Select((statusId, statusIndex) =>
                $"{statusId}={states[partyIndex, statusIndex]}({FormatRemainingMs(RemainingMs(expiresAt[partyIndex, statusIndex], now))})"));
    }

    private static bool IsP4ChainDebugStatus(uint statusId)
    {
        return statusId >= 5543 && statusId <= 5548;
    }

    private static bool IsP4LaterXStatus(uint statusId)
    {
        return statusId == 5543 || statusId == 5544 || statusId == 5545 || statusId == 5546;
    }

    private static bool IsP4LaterCStatus(uint statusId)
    {
        return statusId == 5547 || statusId == 5548;
    }

    private static bool TryGetP4BuffCategory(uint statusId, out P4BuffCategory category)
    {
        switch (statusId)
        {
            case 5544:
            case 5545:
            case 5546:
                category = P4BuffCategory.ThunderShare;
                return true;
            case 5543:
                category = P4BuffCategory.Petrify;
                return true;
            case 5547:
            case 5548:
                category = P4BuffCategory.Element;
                return true;
            default:
                category = default;
                return false;
        }
    }

    private static string P4BuffName(uint statusId)
    {
        switch (statusId)
        {
            case 5547: return "Fire";
            case 5548: return "Water";
            case 5545: return "WaterShare";
            case 5544: return "Thunder";
            case 5543: return "Petrify";
            case 5546: return "Accel";
            default: return statusId.ToString(CultureInfo.InvariantCulture);
        }
    }

    private static bool TryResolveP4LaterStateFromWindow(uint statusId, int xParam, long xExpiresAt, int cParam, long cExpiresAt, long now, out int state, out string windowName, out int usedParam, out int remainingMs)
    {
        state = P4StateUnknown;
        windowName = "-";
        usedParam = 0;
        remainingMs = 0;

        if (IsP4LaterXStatus(statusId))
        {
            if (xParam == 0 || now > xExpiresAt)
                return false;

            windowName = "X";
            usedParam = xParam;
            remainingMs = RemainingMs(xExpiresAt, now);
            return TryGetP4StateValueFromParam(xParam, out state);
        }

        if (IsP4LaterCStatus(statusId))
        {
            if (cParam == 0 || now > cExpiresAt)
                return false;

            windowName = "C";
            usedParam = cParam;
            remainingMs = RemainingMs(cExpiresAt, now);
            return TryGetP4StateValueFromParam(cParam, out state);
        }

        return false;
    }

    private static string FormatP4StatusStateWithRemaining(int[,] states, long[,] expiresAt, int partyIndex, uint statusId, long now)
    {
        if (partyIndex < 0
            || partyIndex >= 8
            || !TryGetP4TrackedStatusIndex(statusId, out var statusIndex))
            return $"{statusId}=0(-)";

        return $"{statusId}={states[partyIndex, statusIndex]}({FormatRemainingMs(RemainingMs(expiresAt[partyIndex, statusIndex], now))})";
    }

    private static string FormatP4StatusesWithRemaining(int[,] states, long[,] expiresAt, int partyIndex, long now, params uint[] statusIds)
    {
        return string.Join(" ", statusIds.Select(statusId => FormatP4StatusStateWithRemaining(states, expiresAt, partyIndex, statusId, now)));
    }

    private static string FormatP4WindowDebug(string name, int param, int remainingMs, bool active)
    {
        if (param == 0)
            return $"{name}-";

        return $"{name}{param}({(active ? FormatRemainingMs(remainingMs) : "-")})";
    }

    private static string FormatP4UsedWindowDebug(string name, int param, int remainingMs)
    {
        if (string.IsNullOrWhiteSpace(name) || name == "-")
            return "-";

        if (name == "direct")
            return "direct";

        return $"{name}{param}({FormatRemainingMs(remainingMs)})";
    }

    private static bool IsTnPartyIndex(int partyIndex)
    {
        return partyIndex >= 0 && partyIndex <= 3;
    }

    private static bool IsDpsPartyIndex(int partyIndex)
    {
        return partyIndex >= 4 && partyIndex <= 7;
    }

    private static bool TryGetP4ActiveSoonState(
        int[,] states,
        long[,] expiresAt,
        int partyIndex,
        uint statusId,
        long now,
        out int state,
        out int remainingMs)
    {
        state = P4StateUnknown;
        remainingMs = 0;

        if (partyIndex < 0 || partyIndex >= 8)
            return false;

        if (!TryGetP4TrackedStatusIndex(statusId, out var statusIndex))
            return false;

        state = states[partyIndex, statusIndex];
        remainingMs = RemainingMs(expiresAt[partyIndex, statusIndex], now);
        return state != P4StateUnknown && remainingMs > 0 && remainingMs < 10000;
    }

    private static bool HasP4StatusStateSoon(
        int[,] states,
        long[,] expiresAt,
        int partyIndex,
        uint statusId,
        int expectedState,
        long now,
        out int remainingMs)
    {
        return TryGetP4ActiveSoonState(states, expiresAt, partyIndex, statusId, now, out var state, out remainingMs)
            && state == expectedState;
    }

    private static void NormalizeP4ExclusivePairByTruth(int[,] states, long[,] expiresAt, int partyIndex, int statusIndexA, int statusIndexB, long now)
    {
        if (partyIndex < 0 || partyIndex >= 8)
            return;

        var stateA = RemainingMs(expiresAt[partyIndex, statusIndexA], now) > 0
            ? states[partyIndex, statusIndexA]
            : P4StateUnknown;
        var stateB = RemainingMs(expiresAt[partyIndex, statusIndexB], now) > 0
            ? states[partyIndex, statusIndexB]
            : P4StateUnknown;

        if (stateA == P4StateTrue || stateB == P4StateTrue)
            return;

        if (stateA == P4StateFalse)
        {
            var oldExpiresAt = expiresAt[partyIndex, statusIndexA];
            states[partyIndex, statusIndexA] = P4StateUnknown;
            expiresAt[partyIndex, statusIndexA] = 0;
            states[partyIndex, statusIndexB] = P4StateTrue;
            if (expiresAt[partyIndex, statusIndexB] <= 0)
                expiresAt[partyIndex, statusIndexB] = oldExpiresAt;
            return;
        }

        if (stateB == P4StateFalse)
        {
            var oldExpiresAt = expiresAt[partyIndex, statusIndexB];
            states[partyIndex, statusIndexB] = P4StateUnknown;
            expiresAt[partyIndex, statusIndexB] = 0;
            states[partyIndex, statusIndexA] = P4StateTrue;
            if (expiresAt[partyIndex, statusIndexA] <= 0)
                expiresAt[partyIndex, statusIndexA] = oldExpiresAt;
        }
    }

    private static P4ClockDirection ResolveP4MoveClock(int[,] states, long[,] expiresAt, int partyIndex, long now)
        => RoleDefaultClock(HasP4StatusStateSoon(states, expiresAt, partyIndex, 5544, P4StateTrue, now, out _), partyIndex);

    private static Vector3 P4ClockTarget(Vector3 newTwelveDirection, P4ClockDirection clock)
    {
        var rightVector = RightVectorFromNewTwelve(newTwelveDirection);
        switch (clock)
        {
            case P4ClockDirection.Twelve:
                return P4ArenaCenter + newTwelveDirection * P4ChainGuideOffset;
            case P4ClockDirection.Three:
                return P4ArenaCenter + rightVector * P4ChainGuideOffset;
            case P4ClockDirection.Six:
                return P4ArenaCenter - newTwelveDirection * P4ChainGuideOffset;
            case P4ClockDirection.Nine:
                return P4ArenaCenter - rightVector * P4ChainGuideOffset;
            default:
                return P4ArenaCenter;
        }
    }

    private static string FormatP4Clock(P4ClockDirection clock)
    {
        switch (clock)
        {
            case P4ClockDirection.Twelve: return "12";
            case P4ClockDirection.Three: return "3";
            case P4ClockDirection.Six: return "6";
            case P4ClockDirection.Nine: return "9";
            default: return "-";
        }
    }

    private static string FormatP4BuffGroupKind(P4BuffGroupKind kind)
    {
        switch (kind)
        {
            case P4BuffGroupKind.A: return "A";
            case P4BuffGroupKind.B: return "B";
            case P4BuffGroupKind.C: return "C";
            case P4BuffGroupKind.D: return "D";
            case P4BuffGroupKind.E: return "E";
            case P4BuffGroupKind.F: return "F";
            default: return "-";
        }
    }

    private static float ClockToRadians(float clock)
    {
        return clock / 6.0f * MathF.PI;
    }

    private static Vector3 P4ClockPoint(Vector3 newTwelveDirection, float clock, float distance)
    {
        var rightVector = RightVectorFromNewTwelve(newTwelveDirection);
        var radians = ClockToRadians(clock);
        var direction = newTwelveDirection * MathF.Cos(radians) + rightVector * MathF.Sin(radians);
        return P4ArenaCenter + direction * distance;
    }

    // LPDU：分摊支援北 / DPS 南，分散支援西 / DPS 东（RaidPlan guufe9q559evt7pj Set #1、#4）
    private static P4ClockDirection RoleDefaultClock(bool isThunder, int partyIndex)
    {
        if (IsTnPartyIndex(partyIndex))
            return isThunder ? P4ClockDirection.Nine : P4ClockDirection.Twelve;

        if (IsDpsPartyIndex(partyIndex))
            return isThunder ? P4ClockDirection.Three : P4ClockDirection.Six;

        return P4ClockDirection.Unknown;
    }

    private static float ClockDirectionToClock(P4ClockDirection clock)
    {
        switch (clock)
        {
            case P4ClockDirection.Twelve: return 0.0f;
            case P4ClockDirection.Three: return 3.0f;
            case P4ClockDirection.Six: return 6.0f;
            case P4ClockDirection.Nine: return 9.0f;
            default: return 0.0f;
        }
    }

    private static string P4MoveCallText(int state)
    {
        if (state == P4StateTrue) return "停停停";
        if (state == P4StateFalse) return "动动动";
        return null;
    }

    private static string P4PetrifyCallText(int state, bool isSelf)
    {
        if (state == P4StateTrue) return isSelf ? "你是背对石化" : "背对石化";
        if (state == P4StateFalse) return isSelf ? "你是正对石化" : "正对石化";
        return null;
    }

    private static bool TryResolveP4ElementCall(
        int[,] states,
        long[,] expiresAt,
        long now,
        out P4ElementCall call,
        out int partyIndex,
        out uint statusId,
        out int state,
        out int remainingMs)
    {
        call = P4ElementCall.Unknown;
        partyIndex = -1;
        statusId = 0;
        state = P4StateUnknown;
        remainingMs = 0;

        for (var i = 0; i < 8; i++)
        {
            if (TryGetP4ActiveSoonState(states, expiresAt, i, 5547, now, out state, out remainingMs))
            {
                call = state == P4StateTrue ? P4ElementCall.FireSteel : P4ElementCall.WaterMoon;
                partyIndex = i;
                statusId = 5547;
                return call != P4ElementCall.Unknown;
            }

            if (TryGetP4ActiveSoonState(states, expiresAt, i, 5548, now, out state, out remainingMs))
            {
                call = state == P4StateFalse ? P4ElementCall.FireSteel : P4ElementCall.WaterMoon;
                partyIndex = i;
                statusId = 5548;
                return call != P4ElementCall.Unknown;
            }
        }

        return false;
    }

    private static string P4ElementCallText(P4ElementCall call)
    {
        switch (call)
        {
            case P4ElementCall.FireSteel: return "火钢铁 中间集合稍后散开";
            case P4ElementCall.WaterMoon: return "水月环 中间集合稍后不动";
            default: return null;
        }
    }

    private static string FormatP4ElementCall(P4ElementCall call)
    {
        switch (call)
        {
            case P4ElementCall.FireSteel: return "火钢铁";
            case P4ElementCall.WaterMoon: return "水月环";
            default: return "-";
        }
    }

    private bool TryStartP4Chain(out int round, out int stepGeneration)
    {
        lock (_p4Lock)
        {
            if (_p4ChainStep != P4ChainStep.None || _p4ChainRound > 0)
            {
                round = _p4ChainRound;
                stepGeneration = _p4ChainStepGeneration;
                return false;
            }

            _p4ChainRound = 1;
            _p4ChainStep = P4ChainStep.Move;
            _p4ChainStepGeneration++;
            round = _p4ChainRound;
            stepGeneration = _p4ChainStepGeneration;
            return true;
        }
    }

    private bool TryAdvanceP4ChainStep(P4ChainStep requiredStep, P4ChainStep nextStep, out int round, out int stepGeneration)
    {
        lock (_p4Lock)
        {
            if (_p4ChainRound <= 0 || _p4ChainStep != requiredStep)
            {
                round = _p4ChainRound;
                stepGeneration = _p4ChainStepGeneration;
                return false;
            }

            _p4ChainStep = nextStep;
            _p4ChainStepGeneration++;
            round = _p4ChainRound;
            stepGeneration = _p4ChainStepGeneration;
            return true;
        }
    }

    private bool TryAdvanceP4ChainRound(out int round, out int stepGeneration)
    {
        lock (_p4Lock)
        {
            if (_p4ChainRound <= 0 || _p4ChainStep != P4ChainStep.Element)
            {
                round = _p4ChainRound;
                stepGeneration = _p4ChainStepGeneration;
                return false;
            }

            _p4ChainRound++;
            _p4ChainStep = P4ChainStep.Move;
            _p4ChainStepGeneration++;
            round = _p4ChainRound;
            stepGeneration = _p4ChainStepGeneration;
            return true;
        }
    }

    private void ScheduleP4DelayedStep(
        ScriptAccessory accessory,
        int generation,
        int stepGeneration,
        int round,
        P4ChainStep step,
        int delayMs,
        Action action)
    {
        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(Math.Max(0, delayMs));

                if (generation != _generation || _p4Phase != Phase.P4)
                    return;

                lock (_p4Lock)
                {
                    if (_p4ChainStepGeneration != stepGeneration
                        || _p4ChainRound != round
                        || _p4ChainStep != step)
                        return;
                }

                action();
            }
            catch (Exception ex)
            {
                DebugEcho(accessory, $"P4链式阶段{step}延迟执行失败：{ex.Message}");
            }
        });
    }

    private void ScheduleP4SelfTtsAtRemaining3(ScriptAccessory accessory, int generation, int round, int partyIndex, int expectedState, int remainingMs, string text)
    {
        if (string.IsNullOrWhiteSpace(text) || remainingMs <= 0)
            return;

        var delayMs = Math.Max(0, remainingMs - 3000);
        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delayMs);

                if (generation != _generation || _p4Phase != Phase.P4)
                    return;

                lock (_p4Lock)
                {
                    if (_p4ChainRound != round || _p4ChainStep == P4ChainStep.None)
                        return;

                    if (partyIndex < 0
                        || partyIndex >= 8
                        || !TryGetP4TrackedStatusIndex(5546, out var statusIndex)
                        || _p4StatusStateByPartyAndStatus[partyIndex, statusIndex] != expectedState)
                        return;

                    var currentRemaining = RemainingMs(_p4StatusExpiresAtByPartyAndStatus[partyIndex, statusIndex], NowMs());
                    if (currentRemaining <= 0 || currentRemaining > 4500)
                        return;
                }

                QTTS(text);
            }
            catch (Exception ex)
            {
                DebugEcho(accessory, $"P4 5546 TTS延迟执行失败：{ex.Message}");
            }
        });
    }

    private void SendP4PartyCallouts(ScriptAccessory accessory, IReadOnlyList<string> messages, int generation, int round)
    {
        if (PlanMessageMode != P4PlanMessageMode.Party || messages == null || messages.Count == 0)
            return;

        Task.Run(async () =>
        {
            try
            {
                for (var i = 0; i < messages.Count; i++)
                {
                    if (generation != _generation || _p4Phase != Phase.P4)
                        return;

                    lock (_p4Lock)
                    {
                        if (_p4ChainRound != round || _p4ChainStep == P4ChainStep.None)
                            return;
                    }

                    accessory.Method.SendChat($"/p {messages[i]}");
                    if (i + 1 < messages.Count)
                        await Task.Delay(150);
                }
            }
            catch (Exception ex)
            {
                DebugEcho(accessory, $"P4小队频道提示发送失败：{ex.Message}");
            }
        });
    }

    private void ScheduleP4LaterGroupsIfReady(ScriptAccessory accessory)
    {
        var generation = _generation;

        lock (_p4Lock)
        {
            if (_p4BuffGroupsScheduled || _p4BuffGroupsScheduleQueued)
                return;

            if (_p4GroupSnapshotAt <= 0 || (_p4GroupSnapshotParam != 1121 && _p4GroupSnapshotParam != 1122))
                return;

            if (BuildP4BuffGroupsLocked().Count < P4MaxScheduledGroupCount)
                return;

            _p4BuffGroupsScheduleQueued = true;
        }

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(500);
                FinalizeAndScheduleP4LaterGroups(accessory, generation);
            }
            catch (Exception ex)
            {
                DebugEcho(accessory, $"P4GROUP queue fail {ex.Message}");
            }
        });
    }

    private void FinalizeAndScheduleP4LaterGroups(ScriptAccessory accessory, int generation)
    {
        var groups = new List<P4BuffGroup>();
        lock (_p4Lock)
        {
            if (generation != _generation || _p4Phase != Phase.P4)
                return;

            if (_p4BuffGroupsScheduled)
                return;

            groups = BuildP4BuffGroupsLocked();
            if (groups.Count < P4MaxScheduledGroupCount)
            {
                _p4BuffGroupsScheduleQueued = false;
                return;
            }

            _p4BuffGroups.Clear();
            _p4BuffGroups.AddRange(groups);
            _p4BuffGroupsScheduled = true;
            _p4BuffGroupsScheduleQueued = false;
        }

        DebugEcho(accessory, $"P4GROUP ready {string.Join(" ", groups.Select(FormatP4GroupReadyDebug))}");
        SendP4GroupPlanSummaries(accessory, groups, generation);
        foreach (var group in groups)
            ScheduleP4BuffGroup(accessory, generation, group);
    }

    private List<P4BuffGroup> BuildP4BuffGroupsLocked()
    {
        if (_p4GroupSnapshotAt <= 0)
            return new List<P4BuffGroup>();

        var snapshotAt = _p4GroupSnapshotAt;
        var sorted = _p4BuffRecords
            .Where(r => r.ExpiresAt > snapshotAt && TryGetP4BuffCategory(r.StatusId, out _))
            .Select(r => new
            {
                Record = r,
                SnapshotRemainingMs = (int)Math.Max(1L, r.ExpiresAt - snapshotAt),
            })
            .OrderBy(x => x.SnapshotRemainingMs)
            .ThenBy(x => x.Record.StatusId)
            .ThenBy(x => x.Record.PartyIndex)
            .ToList();

        var groups = new List<P4BuffGroup>();
        foreach (var item in sorted)
        {
            var record = item.Record;
            record.SnapshotRemainingMs = item.SnapshotRemainingMs;

            var group = groups.FirstOrDefault(g =>
                Math.Abs(g.SnapshotRemainingMs - item.SnapshotRemainingMs) <= P4BuffGroupToleranceMs);
            if (group == null)
            {
                group = new P4BuffGroup
                {
                    Category = TryGetP4BuffCategory(record.StatusId, out var category) ? category : P4BuffCategory.Mixed,
                    DurationMs = record.DurationMs,
                    SnapshotRemainingMs = item.SnapshotRemainingMs,
                    ExpiresAt = snapshotAt + item.SnapshotRemainingMs,
                };
                groups.Add(group);
            }

            group.Records.Add(record);
            group.DurationMs = (int)Math.Round(group.Records.Average(r => r.DurationMs));
            group.SnapshotRemainingMs = (int)Math.Round(group.Records.Average(r => r.SnapshotRemainingMs));
            group.ExpiresAt = snapshotAt + group.SnapshotRemainingMs;
            group.Category = ResolveP4BuffGroupCategory(group.Records);
        }

        groups = groups
            .OrderBy(g => g.SnapshotRemainingMs)
            .Take(P4MaxScheduledGroupCount)
            .ToList();
        for (var i = 0; i < groups.Count; i++)
            groups[i].Kind = (P4BuffGroupKind)(i + 1);

        return groups;
    }

    private static P4BuffCategory ResolveP4BuffGroupCategory(IReadOnlyList<P4BuffRecord> records)
    {
        var categories = records
            .Select(r => TryGetP4BuffCategory(r.StatusId, out var category) ? category : P4BuffCategory.Mixed)
            .Distinct()
            .ToList();

        return categories.Count == 1 ? categories[0] : P4BuffCategory.Mixed;
    }

    private static string FormatP4GroupReadyDebug(P4BuffGroup group)
    {
        return $"{FormatP4BuffGroupKind(group.Kind)}:{group.Category}:{group.Records.Count} snap={FormatRemainingMs(group.SnapshotRemainingMs)} now={FormatRemainingMs(RemainingMs(group.ExpiresAt, NowMs()))} ids={FormatP4GroupStatusIds(group)} dur={FormatP4GroupDurations(group)}";
    }

    private static string FormatP4GroupStatusIds(P4BuffGroup group)
    {
        return string.Join(
            "/",
            group.Records
                .Select(r => r.StatusId)
                .Distinct()
                .OrderBy(id => id)
                .Select(P4BuffName));
    }

    private static string FormatP4GroupDurations(P4BuffGroup group)
    {
        var durations = group.Records
            .Select(r => r.DurationMs)
            .Where(ms => ms > 0)
            .Distinct()
            .OrderBy(ms => ms)
            .Select(FormatRemainingMs)
            .ToList();

        return durations.Count > 0 ? string.Join("/", durations) : "-";
    }

    private void ScheduleP4BuffGroup(ScriptAccessory accessory, int generation, P4BuffGroup group)
    {
        var triggerRemainingMs = P4GroupTriggerRemainingMs(group.Kind);
        if (triggerRemainingMs <= 0)
            return;

        var delayMs = (int)Math.Max(0L, group.ExpiresAt - NowMs() - triggerRemainingMs);
        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delayMs);
                if (generation != _generation || _p4Phase != Phase.P4)
                    return;

                ExecuteP4BuffGroup(accessory, generation, group.Kind);
            }
            catch (Exception ex)
            {
                DebugEcho(accessory, $"P4GROUP {FormatP4BuffGroupKind(group.Kind)} delayed fail {ex.Message}");
            }
        });
    }

    private static int P4GroupTriggerRemainingMs(P4BuffGroupKind kind)
    {
        switch (kind)
        {
            case P4BuffGroupKind.A: return 6500;
            case P4BuffGroupKind.B: return 4900;
            case P4BuffGroupKind.C: return 6000;
            case P4BuffGroupKind.D: return 8500;
            case P4BuffGroupKind.E: return 5500;
            case P4BuffGroupKind.F: return 4900;
            default: return 0;
        }
    }

    private void ExecuteP4BuffGroup(ScriptAccessory accessory, int generation, P4BuffGroupKind kind)
    {
        P4BuffGroup group = null;
        Vector3 newTwelveDirection;
        lock (_p4Lock)
        {
            group = _p4BuffGroups.FirstOrDefault(g => g.Kind == kind);
            newTwelveDirection = DefaultNorth;
            if (kind == P4BuffGroupKind.F)
                _p4FGroupStartedAt = NowMs();
        }

        if (group == null)
        {
            DebugEcho(accessory, $"P4GROUP {FormatP4BuffGroupKind(kind)} missing");
            return;
        }

        DebugEcho(accessory, $"P4GROUP {FormatP4BuffGroupKind(kind)} fire records={FormatP4GroupRecords(group)}");

        switch (kind)
        {
            case P4BuffGroupKind.A:
                ExecuteP4ThunderShareGroup(accessory, generation, group, newTwelveDirection, 8.0f, 4000, "P4_A", false, 0);
                break;
            case P4BuffGroupKind.B:
                ExecuteP4PetrifyGroup(accessory, group, 4000, "P4_B");
                break;
            case P4BuffGroupKind.C:
                ExecuteP4ElementGroup(accessory, group, newTwelveDirection, P4BuffGroupKind.C, 5500);
                break;
            case P4BuffGroupKind.D:
                ExecuteP4GroupD(accessory, generation, group, newTwelveDirection);
                break;
            case P4BuffGroupKind.E:
                ExecuteP4PetrifyGroup(accessory, group, 5000, "P4_E");
                break;
            case P4BuffGroupKind.F:
                ExecuteP4ElementGroup(accessory, group, newTwelveDirection, P4BuffGroupKind.F, 4500);
                TryDrawP4FinalSafe(accessory, generation);
                break;
        }
    }

    private static string FormatP4GroupRecords(P4BuffGroup group)
    {
        return string.Join(
            " ",
            group.Records
                .OrderBy(r => r.PartyIndex)
                .ThenBy(r => r.StatusId)
                .Select(r => $"{PartyPriorityLabel(r.PartyIndex)}:{P4BuffName(r.StatusId)}={r.State}/{FormatRemainingMs(RemainingMs(r.ExpiresAt, NowMs()))}"));
    }

    private bool TryGetP4ThunderState(IReadOnlyList<P4BuffRecord> records, int partyIndex, out bool isThunder)
    {
        isThunder = false;

        var thunder = records.FirstOrDefault(r => r.PartyIndex == partyIndex && r.StatusId == 5544);
        if (thunder != null)
        {
            if (thunder.State == P4StateTrue)
            {
                isThunder = true;
                return true;
            }

            if (thunder.State == P4StateFalse)
            {
                isThunder = false;
                return true;
            }
        }

        var waterShare = records.FirstOrDefault(r => r.PartyIndex == partyIndex && r.StatusId == 5545);
        if (waterShare != null)
        {
            if (waterShare.State == P4StateTrue)
            {
                isThunder = false;
                return true;
            }

            if (waterShare.State == P4StateFalse)
            {
                isThunder = true;
                return true;
            }
        }

        return false;
    }

    private bool TryGetP4AccelCall(IReadOnlyList<P4BuffRecord> records, int partyIndex, out string call)
    {
        call = null;
        var accel = records.FirstOrDefault(r => r.PartyIndex == partyIndex && r.StatusId == 5546);
        if (accel == null)
            return false;

        call = P4MoveCallText(accel.State);
        return !string.IsNullOrWhiteSpace(call);
    }

    private void SendP4GroupPlanSummaries(ScriptAccessory accessory, IReadOnlyList<P4BuffGroup> groups, int generation)
    {
        if (groups == null || PlanMessageMode == P4PlanMessageMode.Off)
            return;

        var messages = new List<string>();
        AddP4RoundPlanSummary(
            accessory,
            messages,
            "\u7b2c\u4e00\u8f6e",
            groups.FirstOrDefault(g => g.Kind == P4BuffGroupKind.A),
            groups.FirstOrDefault(g => g.Kind == P4BuffGroupKind.B),
            groups.FirstOrDefault(g => g.Kind == P4BuffGroupKind.C));
        messages.Add("---------------");
        AddP4RoundPlanSummary(
            accessory,
            messages,
            "\u7b2c\u4e8c\u8f6e",
            groups.FirstOrDefault(g => g.Kind == P4BuffGroupKind.D),
            groups.FirstOrDefault(g => g.Kind == P4BuffGroupKind.E),
            groups.FirstOrDefault(g => g.Kind == P4BuffGroupKind.F));
        SendP4PlanMessages(accessory, messages, generation);
    }

    private void AddP4RoundPlanSummary(
        ScriptAccessory accessory,
        List<string> messages,
        string roundLabel,
        P4BuffGroup thunderGroup,
        P4BuffGroup petrifyGroup,
        P4BuffGroup elementGroup)
    {
        if (messages == null)
            return;

        AddP4ThunderSharePlanSummary(accessory, messages, thunderGroup, roundLabel);
        AddP4PetrifyPlanSummary(accessory, messages, petrifyGroup, roundLabel);
        AddP4ElementPlanSummary(messages, elementGroup, roundLabel);
    }

    private void AddP4ThunderSharePlanSummary(ScriptAccessory accessory, List<string> messages, P4BuffGroup group, string roundLabel)
    {
        if (messages == null || group == null)
            return;

        var accelParts = new List<string>();
        var accelStop = new List<string>();
        var accelMove = new List<string>();
        var thunder = new List<string>();
        var records = group.Records;

        for (var i = 0; i < 8; i++)
        {
            var accel = records.FirstOrDefault(r => r.PartyIndex == i && r.StatusId == 5546);
            if (accel != null)
            {
                if (accel.State == P4StateTrue)
                    accelStop.Add(PartyMemberJobName(accessory, i));
                else if (accel.State == P4StateFalse)
                    accelMove.Add(PartyMemberJobName(accessory, i));
            }

            if (TryGetP4ThunderState(records, i, out var isThunder) && isThunder)
                thunder.Add(PartyMemberJobName(accessory, i));
        }

        if (accelMove.Count > 0)
            accelParts.Add($"{string.Join("\uff0c", accelMove)} \u52a8\u52a8\u52a8");
        if (accelStop.Count > 0)
            accelParts.Add($"{string.Join("\uff0c", accelStop)} \u505c\u505c\u505c");

        if (accelParts.Count > 0)
            messages.Add($"{roundLabel}\uff1a{string.Join("    ", accelParts)}");
        if (thunder.Count > 0)
            messages.Add($"{roundLabel}\uff1a{string.Join(" ", thunder)} \u96f7\u5206\u6563");
    }

    private void AddP4PetrifyPlanSummary(ScriptAccessory accessory, List<string> messages, P4BuffGroup group, string roundLabel)
    {
        if (messages == null || group == null)
            return;

        var faceOut = new List<string>();
        var faceEachOther = new List<string>();
        foreach (var record in group.Records.Where(r => r.StatusId == 5543).OrderBy(r => r.PartyIndex))
        {
            if (record.State == P4StateTrue)
                faceOut.Add(PartyMemberJobName(accessory, record.PartyIndex));
            else if (record.State == P4StateFalse)
                faceEachOther.Add(PartyMemberJobName(accessory, record.PartyIndex));
        }

        if (faceOut.Count > 0)
            messages.Add($"{roundLabel}\uff1a{string.Join(" ", faceOut)} \u80cc\u5bf9");
        if (faceEachOther.Count > 0)
            messages.Add($"{roundLabel}\uff1a{string.Join(" ", faceEachOther)} \u6b63\u5bf9");
    }

    private void AddP4ElementPlanSummary(List<string> messages, P4BuffGroup group, string roundLabel)
    {
        if (messages == null || group == null)
            return;

        var call = ResolveP4ElementCallFromRecords(group.Records);
        if (call == P4ElementCall.FireSteel)
            messages.Add($"{roundLabel}\uff1a\u5168\u4f53\u94a2\u94c1");
        else if (call == P4ElementCall.WaterMoon)
            messages.Add($"{roundLabel}\uff1a\u5168\u4f53\u6708\u73af");
    }

    private void SendP4PlanMessages(ScriptAccessory accessory, IReadOnlyList<string> messages, int generation)
    {
        if (messages == null || messages.Count == 0)
            return;

        switch (PlanMessageMode)
        {
            case P4PlanMessageMode.Party:
                SendP4PartyMessages(accessory, messages, generation);
                break;
            case P4PlanMessageMode.Echo:
                SendP4EchoMessages(accessory, messages, generation);
                break;
        }
    }

    private void SendP4PartyMessages(ScriptAccessory accessory, IReadOnlyList<string> messages, int generation)
    {
        if (messages == null || messages.Count == 0)
            return;

        Task.Run(async () =>
        {
            for (var i = 0; i < messages.Count; i++)
            {
                if (generation != _generation || _p4Phase != Phase.P4)
                    return;

                accessory.Method.SendChat($"/p {messages[i]}");
                if (i + 1 < messages.Count)
                    await Task.Delay(150);
            }
        });
    }

    private void SendP4EchoMessages(ScriptAccessory accessory, IReadOnlyList<string> messages, int generation)
    {
        if (messages == null || messages.Count == 0)
            return;

        Task.Run(async () =>
        {
            for (var i = 0; i < messages.Count; i++)
            {
                if (generation != _generation || _p4Phase != Phase.P4)
                    return;

                accessory.Method.SendChat($"/e {messages[i]}");
                if (i + 1 < messages.Count)
                    await Task.Delay(150);
            }
        });
    }

    private void ExecuteP4ThunderShareGroup(
        ScriptAccessory accessory,
        int generation,
        P4BuffGroup group,
        Vector3 newTwelveDirection,
        float distance,
        int duration,
        string drawName,
        bool sendAccelParty,
        int delay)
    {
        var records = group.Records;
        var targets = new Vector3[8];
        var partyMessages = new List<string>();

        for (var i = 0; i < 8; i++)
        {
            var hasThunderState = TryGetP4ThunderState(records, i, out var isThunder);
            if (isThunder)
            {
                partyMessages.Add($"{PartyPriorityLabel(i)}{PartyMemberJobName(accessory, i)}\u96f7\u5206\u6563");
            }

            var clock = RoleDefaultClock(isThunder, i);
            targets[i] = P4ClockPoint(newTwelveDirection, ClockDirectionToClock(clock), distance);

            if (TryGetP4AccelCall(records, i, out var accelCall))
                partyMessages.Add($"{PartyPriorityLabel(i)}{PartyMemberJobName(accessory, i)}{accelCall}");

            DebugEcho(accessory, $"P4{FormatP4BuffGroupKind(group.Kind)} MOVE {PartyPriorityLabel(i)} thunder={(hasThunderState ? isThunder.ToString() : "-")} accel={(TryGetP4AccelCall(records, i, out var c) ? c : "-")} clock={FormatP4Clock(clock)} target={FormatPosition(targets[i])}");
        }

        if (sendAccelParty)
            SendP4PartyCalloutsSimple(accessory, partyMessages, generation);

        var myIndex = GetMyIndex(accessory);
        if (myIndex >= 0 && myIndex < 8)
        {
            var hasThunderState = TryGetP4ThunderState(records, myIndex, out var isThunder);
            var parts = new List<string> { isThunder ? "钢铁" : "分摊" };
            if (TryGetP4AccelCall(records, myIndex, out var accelCall))
                parts.Add(accelCall);
            Alert(string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p))));
        }

        for (var i = 0; i < 8; i++)
        {
            if (!Debugging && i != myIndex)
                continue;
            var hasThunderState = TryGetP4ThunderState(records, i, out _);
            if (hasThunderState || IsTnPartyIndex(i) || IsDpsPartyIndex(i))
                DrawP4SelfGuide(accessory, $"{P4DrawPrefix}_{drawName}_Guide", targets[i], duration, delay, i);
        }
    }

    private void SendP4PartyCalloutsSimple(ScriptAccessory accessory, IReadOnlyList<string> messages, int generation)
    {
        if (PlanMessageMode != P4PlanMessageMode.Party || messages == null || messages.Count == 0)
            return;

        Task.Run(async () =>
        {
            for (var i = 0; i < messages.Count; i++)
            {
                if (generation != _generation || _p4Phase != Phase.P4)
                    return;

                accessory.Method.SendChat($"/p {messages[i]}");
                if (i + 1 < messages.Count)
                    await Task.Delay(150);
            }
        });
    }

    /// <summary>partyIndex 为空时画给自己；图名带下标，Debug 全队时不互相覆盖</summary>
    private void DrawP4SelfGuide(ScriptAccessory accessory, string guideName, Vector3 target, int duration, int delay = 0, int partyIndex = -1)
    {
        if (partyIndex < 0)
            partyIndex = GetMyIndex(accessory);
        DrawGuide(accessory, $"{guideName}_{partyIndex}", target, duration, delay, partyIndex: partyIndex);
    }

    // 5543 诅咒之嚎（石化）LPDU：只指两个持有者去 Boss 脚下（场中），闲人不管。RaidPlan guufe9q559evt7pj Set #2 / #5
    private void ExecuteP4PetrifyGroup(ScriptAccessory accessory, P4BuffGroup group, int duration, string drawName)
    {
        var petrifies = group.Records.Where(r => r.StatusId == 5543).ToList();
        var holders = petrifies.Select(r => r.PartyIndex).ToHashSet();
        var state = petrifies.FirstOrDefault()?.State ?? P4StateUnknown;

        var myIndex = GetMyIndex(accessory);
        if (holders.Contains(myIndex))
            Alert("石化去场中");

        foreach (var i in holders)
        {
            if (!Debugging && i != myIndex)
                continue;
            DrawP4SelfGuide(accessory, $"{P4DrawPrefix}_{drawName}_Guide", P4ArenaCenter, duration, 0, i);
        }

        DebugEcho(accessory, $"P4{FormatP4BuffGroupKind(group.Kind)} PETRI state={state} holders={string.Join(",", holders.Select(PartyPriorityLabel))}");
    }

    private void ExecuteP4ElementGroup(ScriptAccessory accessory, P4BuffGroup group, Vector3 newTwelveDirection, P4BuffGroupKind kind, int duration)
    {
        var call = ResolveP4ElementCallFromRecords(group.Records);
        if (kind == P4BuffGroupKind.C)
            _p4CElementCall = call;
        if (kind == P4BuffGroupKind.F)
            _p4FElementCall = call;

        var text = call == P4ElementCall.FireSteel ? "场中集合钢铁" : call == P4ElementCall.WaterMoon ? "场中集合月环" : null;
        if (!string.IsNullOrWhiteSpace(text))
            QTTS(text);

        var myIndex = GetMyIndex(accessory);
        for (var i = 0; i < 8; i++)
        {
            if (!Debugging && i != myIndex)
                continue;
            DrawP4SelfGuide(accessory, $"{P4DrawPrefix}_P4_{FormatP4BuffGroupKind(kind)}_Center_Guide", P4ArenaCenter, duration, 0, i);
        }
        DebugEcho(accessory, $"P4{FormatP4BuffGroupKind(kind)} ELEM result={FormatP4ElementCall(call)} records={FormatP4GroupRecords(group)}");
    }

    private P4ElementCall ResolveP4ElementCallFromRecords(IReadOnlyList<P4BuffRecord> records)
    {
        var record = records
            .Where(r => r.StatusId == 5547 || r.StatusId == 5548)
            .OrderBy(r => r.PartyIndex)
            .ThenBy(r => r.StatusId)
            .FirstOrDefault();
        if (record == null)
            return P4ElementCall.Unknown;

        if (record.StatusId == 5547)
            return record.State == P4StateTrue ? P4ElementCall.FireSteel : P4ElementCall.WaterMoon;

        return record.State == P4StateFalse ? P4ElementCall.FireSteel : P4ElementCall.WaterMoon;
    }

    private void ExecuteP4GroupD(ScriptAccessory accessory, int generation, P4BuffGroup group, Vector3 newTwelveDirection)
    {
        var startedAt = NowMs();
        var firstDistance = _p4CElementCall == P4ElementCall.WaterMoon ? 3.0f : 8.0f;
        ExecuteP4ThunderShareGroup(accessory, generation, group, newTwelveDirection, firstDistance, 5000, "P4_D_First", false, 0);

        Task.Run(async () =>
        {
            await Task.Delay(5000);

            var deadline = NowMs() + 1500;
            while (NowMs() < deadline)
            {
                if (generation != _generation || _p4Phase != Phase.P4)
                    return;

                if (CountP4Dangers(P4DangerShape.Fan, startedAt) >= 2)
                    break;

                await Task.Delay(100);
            }

            if (generation != _generation || _p4Phase != Phase.P4)
                return;

            DrawP4GroupDSafe(accessory, group, newTwelveDirection, startedAt);
        });
    }

    private void DrawP4GroupDSafe(ScriptAccessory accessory, P4BuffGroup group, Vector3 newTwelveDirection, long minCapturedAt)
    {
        var safeByRoleClock = FindSafeHalfClockByQuadrant(newTwelveDirection, minCapturedAt);
        var myIndex = GetMyIndex(accessory);
        if (myIndex < 0 || myIndex >= 8)
            return;

        for (var i = 0; i < 8; i++)
        {
            if (!Debugging && i != myIndex)
                continue;
            TryGetP4ThunderState(group.Records, i, out var isThunder);
            var baseClock = ClockDirectionToClock(RoleDefaultClock(isThunder, i));
            var targetClock = safeByRoleClock.ContainsKey(baseClock) ? safeByRoleClock[baseClock] : baseClock;
            var target = P4ClockPoint(newTwelveDirection, targetClock, 8.0f);
            DrawP4SelfGuide(accessory, $"{P4DrawPrefix}_P4_D_Safe_Guide", target, 3000, 0, i);
            DebugEcho(accessory, $"P4D SAFE {PartyPriorityLabel(i)} base={baseClock:F1} safe={targetClock:F1} target={FormatPosition(target)}");
        }
    }

    private int CountP4Dangers(P4DangerShape shape, long minCapturedAt)
    {
        lock (_p4Lock)
        {
            return _p4DangerRecords.Count(r => r.Shape == shape && r.CapturedAt >= minCapturedAt);
        }
    }

    private List<P4DangerRecord> GetP4RecentDangers(P4DangerShape shape, int count, int lookbackMs, long minCapturedAt = 0)
    {
        var now = NowMs();
        lock (_p4Lock)
        {
            return _p4DangerRecords
                .Where(r => r.Shape == shape
                    && r.CapturedAt >= minCapturedAt
                    && now - r.CapturedAt <= lookbackMs)
                .OrderByDescending(r => r.CapturedAt)
                .Take(count)
                .OrderBy(r => r.CapturedAt)
                .ToList();
        }
    }

    private Dictionary<float, float> FindSafeHalfClockByQuadrant(Vector3 newTwelveDirection, long minCapturedAt)
    {
        var dangers = GetP4RecentDangers(P4DangerShape.Fan, 2, P4DFanLookbackMs, minCapturedAt);
        var result = new Dictionary<float, float>();
        var candidates = new Dictionary<float, float[]>
        {
            { 0.0f, new[] { 11.5f, 12.5f } },
            { 3.0f, new[] { 2.5f, 3.5f } },
            { 6.0f, new[] { 5.5f, 6.5f } },
            { 9.0f, new[] { 8.5f, 9.5f } },
        };

        foreach (var pair in candidates)
        {
            var safeClock = pair.Value.FirstOrDefault(clock =>
                IsPointSafeForDangers(P4ClockPoint(newTwelveDirection, clock, 8.0f), dangers));
            result[pair.Key] = safeClock == 0.0f && pair.Key != 0.0f ? pair.Key : safeClock;
        }

        return result;
    }

    private bool TryDrawP4FinalSafe(ScriptAccessory accessory, int generation)
    {
        long startedAt;
        Vector3 newTwelveDirection;
        P4ElementCall call;
        List<P4DangerRecord> dangers;

        lock (_p4Lock)
        {
            if (_p4FGroupDangerDrawn || _p4FGroupStartedAt <= 0)
                return false;

            startedAt = _p4FGroupStartedAt;
            newTwelveDirection = DefaultNorth;
            call = _p4FElementCall;
            var rects = _p4DangerRecords
                .Where(r => r.Shape == P4DangerShape.Rect && r.CapturedAt >= startedAt)
                .OrderBy(r => r.CapturedAt)
                .Take(2)
                .ToList();
            var fans = _p4DangerRecords
                .Where(r => r.Shape == P4DangerShape.Fan && r.CapturedAt >= startedAt)
                .OrderBy(r => r.CapturedAt)
                .Take(2)
                .ToList();

            if (rects.Count < 2 || fans.Count < 2)
                return false;

            dangers = rects.Concat(fans).ToList();
        }

        // LPDU 最终：全队集合在同一个安全点（RaidPlan guufe9q559evt7pj Set #6.2）。月环贴近场中，钢铁站出 Boss 判定圈
        var distance = call == P4ElementCall.WaterMoon ? 3.0f : 8.0f;
        var safeClocks = P4FinalHalfClocks
            .Where(clock => IsPointSafeForDangers(P4ClockPoint(newTwelveDirection, clock, distance), dangers))
            .ToList();

        if (safeClocks.Count == 0)
        {
            DebugEcho(accessory, $"P4F FINAL no-safe dist={distance:F1} dangers={FormatP4Dangers(dangers)}");
            return false;
        }

        lock (_p4Lock)
        {
            if (_p4FGroupDangerDrawn || generation != _generation || _p4Phase != Phase.P4)
                return false;

            _p4FGroupDangerDrawn = true;
        }

        var myIndex = GetMyIndex(accessory);
        var target = P4ClockPoint(newTwelveDirection, safeClocks[0], distance);
        for (var i = 0; i < 8; i++)
        {
            if (!Debugging && i != myIndex)
                continue;
            DrawP4SelfGuide(accessory, $"{P4DrawPrefix}_P4_F_Final_Guide", target, 5000, 0, i);
        }
        DebugEcho(accessory, $"P4F FINAL clock={safeClocks[0]:F1} dist={distance:F1} target={FormatPosition(target)} call={FormatP4ElementCall(call)} dangers={FormatP4Dangers(dangers)}");

        return true;
    }

    private static bool IsPointSafeForDangers(Vector3 point, IReadOnlyList<P4DangerRecord> dangers)
    {
        return dangers == null || dangers.All(danger => !IsPointInP4Danger(point, danger));
    }

    private static bool IsPointInP4Danger(Vector3 point, P4DangerRecord danger)
    {
        return danger.Shape == P4DangerShape.Rect
            ? IsPointInP4RectDanger(point, danger)
            : IsPointInP4FanDanger(point, danger);
    }

    private static bool IsPointInP4RectDanger(Vector3 point, P4DangerRecord danger)
    {
        var forward = new Vector3(MathF.Sin(danger.Rotation), 0.0f, MathF.Cos(danger.Rotation));
        var right = new Vector3(forward.Z, 0.0f, -forward.X);
        var delta = new Vector3(point.X - danger.Position.X, 0.0f, point.Z - danger.Position.Z);
        var along = delta.X * forward.X + delta.Z * forward.Z;
        var side = delta.X * right.X + delta.Z * right.Z;

        return along >= -0.25f
            && along <= P4RectDangerLength + 0.25f
            && MathF.Abs(side) <= P4RectDangerWidth / 2.0f + 0.25f;
    }

    private static bool IsPointInP4FanDanger(Vector3 point, P4DangerRecord danger)
    {
        var delta = new Vector3(point.X - danger.Position.X, 0.0f, point.Z - danger.Position.Z);
        var distance = MathF.Sqrt(delta.X * delta.X + delta.Z * delta.Z);
        if (distance > P4FanDangerRadius + 0.25f)
            return false;

        if (distance < 0.001f)
            return true;

        var forward = new Vector3(MathF.Sin(danger.Rotation), 0.0f, MathF.Cos(danger.Rotation));
        var dot = (delta.X * forward.X + delta.Z * forward.Z) / distance;
        var limit = MathF.Cos(P4FanDangerRadian / 2.0f + 0.02f);
        return dot >= limit;
    }

    private static string FormatP4Dangers(IReadOnlyList<P4DangerRecord> dangers)
    {
        if (dangers == null || dangers.Count == 0)
            return "-";

        return string.Join(
            ",",
            dangers.Select(d => $"{d.ActionId}/{d.Shape}@{FormatPosition(d.Position)}/{d.Rotation:F2}"));
    }

    private static bool TryConvertToUInt(object value, out uint result)
    {
        result = 0;
        if (value == null)
            return false;

        switch (value)
        {
            case byte v:
                result = v;
                return true;
            case ushort v:
                result = v;
                return true;
            case uint v:
                result = v;
                return true;
            case int v when v >= 0:
                result = (uint)v;
                return true;
            case long v when v >= 0 && v <= uint.MaxValue:
                result = (uint)v;
                return true;
            case string s:
                return TryParseUInt(s, out result);
        }

        try
        {
            if (value is IConvertible convertible)
            {
                result = convertible.ToUInt32(CultureInfo.InvariantCulture);
                return true;
            }
        }
        catch
        {
            result = 0;
        }

        return false;
    }

    private static object ReadPropertyValue(object value, string propertyName)
    {
        if (value == null)
            return null;

        var property = value.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        return property?.GetValue(value);
    }

    private static bool TryReadUIntProperty(object value, string propertyName, out uint result)
    {
        result = 0;
        var propertyValue = ReadPropertyValue(value, propertyName);
        return TryConvertToUInt(propertyValue, out result);
    }

    private static bool TryExtractClassJobRowId(object value, out uint rowId)
    {
        rowId = 0;
        if (value == null)
            return false;

        if (TryConvertToUInt(value, out rowId))
            return true;

        if (TryReadUIntProperty(value, "RowId", out rowId)
            || TryReadUIntProperty(value, "Id", out rowId)
            || TryReadUIntProperty(value, "ID", out rowId))
            return true;

        var nestedValue = ReadPropertyValue(value, "Value");
        return nestedValue != null
            && !ReferenceEquals(nestedValue, value)
            && TryExtractClassJobRowId(nestedValue, out rowId);
    }

    private static bool TryGetPartyMemberClassJobRowId(ScriptAccessory accessory, int partyIndex, out uint rowId)
    {
        rowId = 0;
        if (partyIndex < 0 || partyIndex >= accessory.Data.PartyList.Count)
            return false;

        var obj = accessory.Data.Objects.SearchById(accessory.Data.PartyList[partyIndex]);
        if (obj == null)
            return false;

        if (TryReadUIntProperty(obj, "ClassJobId", out rowId)
            || TryReadUIntProperty(obj, "ClassJobID", out rowId))
            return true;

        return TryExtractClassJobRowId(ReadPropertyValue(obj, "ClassJob"), out rowId);
    }

    private static string PartyMemberJobName(ScriptAccessory accessory, int partyIndex)
    {
        return TryGetPartyMemberClassJobRowId(accessory, partyIndex, out var rowId)
            ? JobShortName(rowId)
            : "未知职业";
    }

    private static string JobShortName(uint classJobRowId)
    {
        switch (classJobRowId)
        {
            case 1: return "剑术";
            case 2: return "格斗";
            case 3: return "斧术";
            case 4: return "枪术";
            case 5: return "弓术";
            case 6: return "幻术";
            case 7: return "咒术";
            case 19: return "骑士";
            case 20: return "武僧";
            case 21: return "战士";
            case 22: return "龙骑";
            case 23: return "诗人";
            case 24: return "白魔";
            case 25: return "黑魔";
            case 26: return "秘术";
            case 27: return "召唤";
            case 28: return "学者";
            case 29: return "双剑";
            case 30: return "忍者";
            case 31: return "机工";
            case 32: return "暗骑";
            case 33: return "占星";
            case 34: return "武士";
            case 35: return "赤魔";
            case 36: return "青魔";
            case 37: return "绝枪";
            case 38: return "舞者";
            case 39: return "钐镰";
            case 40: return "贤者";
            case 41: return "蝰蛇";
            case 42: return "绘灵";
            default: return "未知";
        }
    }

    private static string JobChineseName(uint classJobRowId)
    {
        switch (classJobRowId)
        {
            case 1: return "剑术师";
            case 2: return "格斗家";
            case 3: return "斧术师";
            case 4: return "枪术师";
            case 5: return "弓箭手";
            case 6: return "幻术师";
            case 7: return "咒术师";
            case 19: return "骑士";
            case 20: return "武僧";
            case 21: return "战士";
            case 22: return "龙骑士";
            case 23: return "吟游诗人";
            case 24: return "白魔法师";
            case 25: return "黑魔法师";
            case 26: return "秘术师";
            case 27: return "召唤师";
            case 28: return "学者";
            case 29: return "双剑师";
            case 30: return "忍者";
            case 31: return "机工士";
            case 32: return "暗黑骑士";
            case 33: return "占星术士";
            case 34: return "武士";
            case 35: return "赤魔法师";
            case 36: return "青魔法师";
            case 37: return "绝枪战士";
            case 38: return "舞者";
            case 39: return "钐镰客";
            case 40: return "贤者";
            case 41: return "蝰蛇剑士";
            case 42: return "绘灵法师";
            default: return "未知职业";
        }
    }

    private void DebugP4Resolve(
        ScriptAccessory accessory,
        int[,] states,
        IReadOnlyList<P4HalfSide> sides,
        IReadOnlyList<P4HalfColor> colors,
        int fourthParam,
        P4HalfSide sourceSide,
        P4HalfColor leftColor,
        P4HalfColor rightColor,
        Vector3 newTwelveDirection,
        Vector3 l1,
        Vector3 r1)
    {
        if (!enableDebugLogging) return;

        DebugEcho(accessory, $"P4一运结算 fourthParam={fourthParam} sourceSide={FormatP4Side(sourceSide)} left={FormatP4Color(leftColor)} right={FormatP4Color(rightColor)} new12={FormatPosition(newTwelveDirection)} L1={FormatPosition(l1)} R1={FormatPosition(r1)}");

        for (var i = 0; i < 8; i++)
        {
            var objectId = i < accessory.Data.PartyList.Count ? accessory.Data.PartyList[i] : 0;
            DebugEcho(
                accessory,
                $"{PartyPriorityLabel(i)} {FormatObjectId(objectId)} => color={FormatP4Color(colors[i])} side={FormatP4Side(sides[i])}");
        }
    }

    private void ExecuteP4ChainMove(ScriptAccessory accessory, int generation, int round)
    {
        var states = new int[8, 10];
        var expiresAt = new long[8, 10];
        Vector3 newTwelveDirection;
        bool directionReady;
        var now = NowMs();

        lock (_p4Lock)
        {
            for (var i = 0; i < 8; i++)
                NormalizeP4ExclusivePairByTruth(_p4StatusStateByPartyAndStatus, _p4StatusExpiresAtByPartyAndStatus, i, 3, 4, now);

            Array.Copy(_p4StatusStateByPartyAndStatus, states, _p4StatusStateByPartyAndStatus.Length);
            Array.Copy(_p4StatusExpiresAtByPartyAndStatus, expiresAt, _p4StatusExpiresAtByPartyAndStatus.Length);
            newTwelveDirection = _p4FourthDirectionReady ? _p4FourthNewTwelveDirection : DefaultNorth;
            directionReady = _p4FourthDirectionReady;
        }

        var clocks = new P4ClockDirection[8];
        var targets = new Vector3[8];
        var moveCalls = new string[8];
        var moveCallStates = new int[8];
        var moveCallRemaining = new int[8];
        var partyMessages = new List<string>();

        for (var i = 0; i < 8; i++)
        {
            clocks[i] = ResolveP4MoveClock(states, expiresAt, i, now);
            targets[i] = P4ClockTarget(newTwelveDirection, clocks[i]);

            if (TryGetP4ActiveSoonState(states, expiresAt, i, 5546, now, out var state5546, out var remaining5546))
            {
                var callText = P4MoveCallText(state5546);
                if (!string.IsNullOrWhiteSpace(callText))
                {
                    moveCalls[i] = callText;
                    moveCallStates[i] = state5546;
                    moveCallRemaining[i] = remaining5546;

                    if (i < accessory.Data.PartyList.Count)
                        partyMessages.Add($"{PartyMemberJobName(accessory, i)} {PartyPriorityLabel(i)} {callText}");
                }
            }
        }

        var myIndex = GetMyIndex(accessory);
        if (Debugging)
        {
            for (var i = 0; i < 8; i++)
            {
                if (i == myIndex || clocks[i] == P4ClockDirection.Unknown)
                    continue;
                DrawGuide(accessory, $"{P4DrawPrefix}_P4_Chain_Move_Guide_{i}", targets[i], P4ChainGuideDurationMs, partyIndex: i);
            }
        }

        if (myIndex >= 0 && myIndex < 8 && clocks[myIndex] != P4ClockDirection.Unknown)
        {
            var target = targets[myIndex];
            DrawGuide(accessory, $"{P4DrawPrefix}_P4_Chain_Move_Guide_{myIndex}", target, P4ChainGuideDurationMs);
            DebugEcho(accessory, $"P4循环移动指路已画：round={round} my={PartyPriorityLabel(myIndex)} clock={FormatP4Clock(clocks[myIndex])} target={FormatPosition(target)} duration={P4ChainGuideDurationMs}ms");

            if (!string.IsNullOrWhiteSpace(moveCalls[myIndex]))
                ScheduleP4SelfTtsAtRemaining3(accessory, generation, round, myIndex, moveCallStates[myIndex], moveCallRemaining[myIndex], moveCalls[myIndex]);
        }
        else
        {
            DebugEcho(accessory, $"P4循环移动指路未画：round={round} myIndex={myIndex}");
        }

        SendP4PartyCallouts(accessory, partyMessages, generation, round);
        DebugP4ChainMove(accessory, round, states, expiresAt, clocks, targets, moveCalls, moveCallRemaining, newTwelveDirection, directionReady, now);
    }

    private void ExecuteP4Petrify(ScriptAccessory accessory, int round)
    {
        var states = new int[8, 10];
        var expiresAt = new long[8, 10];
        var now = NowMs();

        lock (_p4Lock)
        {
            Array.Copy(_p4StatusStateByPartyAndStatus, states, _p4StatusStateByPartyAndStatus.Length);
            Array.Copy(_p4StatusExpiresAtByPartyAndStatus, expiresAt, _p4StatusExpiresAtByPartyAndStatus.Length);
        }

        var myIndex = GetMyIndex(accessory);
        string alertText = null;
        var alertPartyIndex = -1;
        var alertState = P4StateUnknown;
        var alertRemaining = 0;

        if (TryGetP4ActiveSoonState(states, expiresAt, myIndex, 5543, now, out var myState5543, out var myRemaining5543))
        {
            alertText = P4PetrifyCallText(myState5543, true);
            alertPartyIndex = myIndex;
            alertState = myState5543;
            alertRemaining = myRemaining5543;
        }
        else
        {
            for (var i = 0; i < 8; i++)
            {
                if (!TryGetP4ActiveSoonState(states, expiresAt, i, 5543, now, out var state5543, out var remaining5543))
                    continue;

                alertText = P4PetrifyCallText(state5543, false);
                alertPartyIndex = i;
                alertState = state5543;
                alertRemaining = remaining5543;
                break;
            }
        }

        if (!string.IsNullOrWhiteSpace(alertText))
            Alert(alertText);

        DebugP4Petrify(accessory, round, states, expiresAt, alertPartyIndex, alertState, alertRemaining, alertText, now);
    }

    private void ExecuteP4Element(ScriptAccessory accessory, int round)
    {
        var states = new int[8, 10];
        var expiresAt = new long[8, 10];
        var now = NowMs();

        lock (_p4Lock)
        {
            Array.Copy(_p4StatusStateByPartyAndStatus, states, _p4StatusStateByPartyAndStatus.Length);
            Array.Copy(_p4StatusExpiresAtByPartyAndStatus, expiresAt, _p4StatusExpiresAtByPartyAndStatus.Length);
        }

        TryResolveP4ElementCall(states, expiresAt, now, out var call, out var partyIndex, out var statusId, out var state, out var remainingMs);
        var text = P4ElementCallText(call);
        if (!string.IsNullOrWhiteSpace(text))
            QTTS(text);

        DebugP4Element(accessory, round, states, expiresAt, call, partyIndex, statusId, state, remainingMs, now);
    }

    private void DebugP4ChainMove(
        ScriptAccessory accessory,
        int round,
        int[,] states,
        long[,] expiresAt,
        IReadOnlyList<P4ClockDirection> clocks,
        IReadOnlyList<Vector3> targets,
        IReadOnlyList<string> moveCalls,
        IReadOnlyList<int> moveCallRemaining,
        Vector3 newTwelveDirection,
        bool directionReady,
        long now)
    {
        if (!enableDebugLogging) return;

        DebugEcho(accessory, $"P4MOVE r={round} new12={FormatPosition(newTwelveDirection)} ready={directionReady}");
        for (var i = 0; i < 8; i++)
        {
            var objectId = i < accessory.Data.PartyList.Count ? accessory.Data.PartyList[i] : 0;
            var buffs = FormatP4StatusesWithRemaining(states, expiresAt, i, now, 5544, 5545, 5546);
            DebugEcho(
                accessory,
                $"{PartyPriorityLabel(i)} {FormatObjectId(objectId)} {buffs} => clock={FormatP4Clock(clocks[i])} target={FormatPosition(targets[i])} call={moveCalls[i] ?? "-"}({FormatRemainingMs(moveCallRemaining[i])})");
        }
    }
    private void DebugP4Petrify(
        ScriptAccessory accessory,
        int round,
        int[,] states,
        long[,] expiresAt,
        int alertPartyIndex,
        int alertState,
        int alertRemaining,
        string alertText,
        long now)
    {
        if (!enableDebugLogging) return;

        DebugEcho(accessory, $"P4PETRI r={round} alert={PartyPriorityLabel(alertPartyIndex)} state={alertState}({FormatRemainingMs(alertRemaining)}) text={alertText ?? "-"}");
        for (var i = 0; i < 8; i++)
        {
            TryGetP4ActiveSoonState(states, expiresAt, i, 5543, now, out var state5543, out var remaining5543);
            var call = P4PetrifyCallText(state5543, false) ?? "-";
            var objectId = i < accessory.Data.PartyList.Count ? accessory.Data.PartyList[i] : 0;
            DebugEcho(
                accessory,
                $"{PartyPriorityLabel(i)} {FormatObjectId(objectId)} {FormatP4StatusStateWithRemaining(states, expiresAt, i, 5543, now)} => active={state5543}({FormatRemainingMs(remaining5543)}) call={call}");
        }
    }
    private void DebugP4Element(
        ScriptAccessory accessory,
        int round,
        int[,] states,
        long[,] expiresAt,
        P4ElementCall call,
        int partyIndex,
        uint statusId,
        int state,
        int remainingMs,
        long now)
    {
        if (!enableDebugLogging) return;

        DebugEcho(accessory, $"P4ELEM r={round} result={FormatP4ElementCall(call)} src={PartyPriorityLabel(partyIndex)} id={statusId} state={state}({FormatRemainingMs(remainingMs)})");
        for (var i = 0; i < 8; i++)
        {
            TryGetP4ActiveSoonState(states, expiresAt, i, 5547, now, out var active5547, out var activeRemaining5547);
            TryGetP4ActiveSoonState(states, expiresAt, i, 5548, now, out var active5548, out var activeRemaining5548);
            var objectId = i < accessory.Data.PartyList.Count ? accessory.Data.PartyList[i] : 0;
            DebugEcho(
                accessory,
                $"{PartyPriorityLabel(i)} {FormatObjectId(objectId)} {FormatP4StatusesWithRemaining(states, expiresAt, i, now, 5547, 5548)} => active5547={active5547}({FormatRemainingMs(activeRemaining5547)}) active5548={active5548}({FormatRemainingMs(activeRemaining5548)})");
        }
    }
    #endregion

    #region Draw Helpers

    private void DrawCircle(ScriptAccessory accessory, string name, Vector3 position, float radius, int duration, int delay = 0, Vector4? color = null, ScaleMode scaleMode = ScaleMode.ByTime)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Position = position;
        dp.Scale = new Vector2(radius);
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = duration;
        dp.ScaleMode = scaleMode;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    private void DrawCircleOnOwner(ScriptAccessory accessory, string name, uint ownerId, float radius, int duration, int delay = 0, Vector4? color = null, ScaleMode scaleMode = ScaleMode.ByTime)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Owner = ownerId;
        dp.Scale = new Vector2(radius);
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = duration;
        dp.ScaleMode = scaleMode;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    private void DrawRect(Event @event, ScriptAccessory accessory, string name, float width, float length, int duration, int delay = 0, Vector4? color = null, ScaleMode scaleMode = ScaleMode.YByTime)
    {
        DrawRect(accessory, name, @event.SourcePosition, @event.SourceRotation, width, length, duration, delay, color, scaleMode);
    }

    private void DrawRect(ScriptAccessory accessory, string name, Vector3 position, float rotation, float width, float length, int duration, int delay = 0, Vector4? color = null, ScaleMode scaleMode = ScaleMode.YByTime)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Position = position;
        dp.Rotation = rotation;
        dp.Scale = new Vector2(width, length);
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = duration;
        dp.ScaleMode = scaleMode;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    private void DrawFan(Event @event, ScriptAccessory accessory, string name, float radius, float radian, int duration, int delay = 0, Vector4? color = null, ScaleMode scaleMode = ScaleMode.ByTime)
    {
        DrawFan(accessory, name, @event.SourcePosition, @event.SourceRotation, radius, radian, duration, delay, color, scaleMode);
    }

    private void DrawFan(ScriptAccessory accessory, string name, Vector3 position, float rotation, float radius, float radian, int duration, int delay = 0, Vector4? color = null, ScaleMode scaleMode = ScaleMode.ByTime)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Position = position;
        dp.Rotation = rotation;
        dp.Scale = new Vector2(radius);
        dp.Radian = radian;
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = duration;
        dp.ScaleMode = scaleMode;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    private void DrawLineToTarget(ScriptAccessory accessory, string name, uint sourceId, uint targetId, float width, int duration, int delay = 0, Vector4? color = null)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Owner = sourceId;
        dp.TargetObject = targetId;
        dp.Scale = new Vector2(width, 1.0f);
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = duration;
        dp.ScaleMode = ScaleMode.YByDistance;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    // 指路风格跟 P1~P3 一致：绿色箭头（WaypointDp，默认安全色）
    private void DrawGuide(ScriptAccessory accessory, string name, Vector3 targetPosition, int duration, int delay = 0, int partyIndex = -1)
    {
        var dp = accessory.WaypointDp(targetPosition, (uint)duration, (uint)Math.Max(0, delay), name);
        dp.Owner = partyIndex < 0 ? GetMyId(accessory) : GuidanceOwner(accessory, partyIndex);
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    #endregion

    #region Mechanisms

    [ScriptMethod(name: "P4开始", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:49884"], userControl: false)]
    public void P4_Start(Event @event, ScriptAccessory accessory)
    {
        _acc = accessory;
        _p4Phase = Phase.P4;
        ResetMechanic(accessory);
    }

    [ScriptMethod(name: "P4 2056真伪窗口", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2056"], userControl: false)]
    public void P4_StatusTruthWindow(Event @event, ScriptAccessory accessory)
    {
        _acc = accessory;
        if (_p4Phase != Phase.P4) return;

        if (!int.TryParse(@event["Param"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var param))
            return;

        if (!TryGetTargetDataId(@event, accessory, out var targetDataId))
            return;

        var now = NowMs();

        if (targetDataId == P4XDataId && (param == 1121 || param == 1122))
        {
            var targetPosition = ResolveEventTargetPosition(@event, accessory);
            var newTwelveDirection = DefaultNorth;
            var directionReady = IsLikelyArenaPosition(targetPosition)
                && TryNormalizeFromCenter(targetPosition, out newTwelveDirection);

            lock (_p4Lock)
            {
                _p4XParam = param;
                _p4XUpdatedAt = now;
                _p4XExpiresAt = now + P4StatusWindowMs;
                _p4XEventCount++;

                if (_p4XEventCount == 3)
                {
                    _p4GroupSnapshotParam = param;
                    _p4GroupSnapshotAt = now;
                }

                if (_p4XEventCount == 4)
                {
                    _p4FourthXParam = param;
                    _p4FourthSnapshotAt = now;
                    _p4FourthTargetPosition = targetPosition;
                    _p4FourthNewTwelveDirection = directionReady ? newTwelveDirection : DefaultNorth;
                    _p4FourthDirectionReady = directionReady;
                }
            }
        }
        else if (targetDataId == P4CDataId && (param == 1119 || param == 1120))
        {
            lock (_p4Lock)
            {
                _p4CParam = param;
                _p4CUpdatedAt = now;
                _p4CExpiresAt = now + P4StatusWindowMs;
            }
        }

        ScheduleP4LaterGroupsIfReady(accessory);
    }

    [ScriptMethod(name: "P4 状态变量赋值", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(554[1-8]|454|1382|4887|4888|5464)$"], userControl: false)]
    public void P4_TrackedStatusAdd(Event @event, ScriptAccessory accessory)
    {
        _acc = accessory;
        if (_p4Phase != Phase.P4) return;

        if (!TryGetStatusId(@event, out var rawStatusId))
            return;

        var directTrue = TryGetP4DirectTrueStatus(rawStatusId, out var directTrueStatusId);
        var statusId = directTrue ? directTrueStatusId : rawStatusId;

        if (!TryGetP4TrackedStatusIndex(statusId, out var statusIndex))
            return;

        if (!TryGetTargetId(@event, out var targetId))
            return;

        var partyIndex = GetPlayerIndex(accessory, targetId);
        if (partyIndex < 0 || partyIndex >= 8)
            return;

        var now = NowMs();
        var hasDuration = TryGetDurationMs(@event, out var durationMs);
        var assigned = false;
        var assignedState = P4StateUnknown;
        var xParam = 0;
        var cParam = 0;
        var xExpiresAt = 0L;
        var cExpiresAt = 0L;
        var xRemainingMs = 0;
        var cRemainingMs = 0;
        var xActive = false;
        var cActive = false;
        var usedWindow = "-";
        var usedParam = 0;
        var usedWindowRemainingMs = 0;
        var storedState = P4StateUnknown;
        var storedRemainingMs = 0;
        var chainRound = 0;
        var chainStep = P4ChainStep.None;

        lock (_p4Lock)
        {
            if (hasDuration)
                _p4StatusExpiresAtByPartyAndStatus[partyIndex, statusIndex] = now + durationMs;

            if (directTrue)
            {
                _p4StatusStateByPartyAndStatus[partyIndex, statusIndex] = P4StateTrue;
                assigned = true;
                assignedState = P4StateTrue;
                usedWindow = "direct";
            }
            else if (IsP4ChainDebugStatus(statusId)
                && TryResolveP4LaterStateFromWindow(statusId, _p4XParam, _p4XExpiresAt, _p4CParam, _p4CExpiresAt, now, out var laterStateValue, out usedWindow, out usedParam, out usedWindowRemainingMs))
            {
                _p4StatusStateByPartyAndStatus[partyIndex, statusIndex] = laterStateValue;
                assigned = true;
                assignedState = laterStateValue;

                if (hasDuration)
                {
                    _p4BuffRecords.Add(new P4BuffRecord
                    {
                        PartyIndex = partyIndex,
                        StatusId = statusId,
                        State = laterStateValue,
                        DurationMs = durationMs,
                        ExpiresAt = now + durationMs,
                    });
                }
            }
            else if (!IsP4ChainDebugStatus(statusId)
                && TryGetActiveP4StateValueLocked(now, out var stateValue, out usedWindow, out usedParam, out usedWindowRemainingMs))
            {
                _p4StatusStateByPartyAndStatus[partyIndex, statusIndex] = stateValue;
                assigned = true;
                assignedState = stateValue;
            }

            xParam = _p4XParam;
            cParam = _p4CParam;
            xExpiresAt = _p4XExpiresAt;
            cExpiresAt = _p4CExpiresAt;
            xRemainingMs = RemainingMs(_p4XExpiresAt, now);
            cRemainingMs = RemainingMs(_p4CExpiresAt, now);
            xActive = _p4XParam != 0 && xRemainingMs > 0;
            cActive = _p4CParam != 0 && cRemainingMs > 0;
            storedState = _p4StatusStateByPartyAndStatus[partyIndex, statusIndex];
            storedRemainingMs = RemainingMs(_p4StatusExpiresAtByPartyAndStatus[partyIndex, statusIndex], now);
            chainRound = _p4ChainRound;
            chainStep = _p4ChainStep;
        }

        if (IsP4ChainDebugStatus(statusId))
        {
            DebugEcho(
                accessory,
                $"P4BUFF+ r={chainRound}/{chainStep} {PartyPriorityLabel(partyIndex)} {P4BuffName(statusId)} id={statusId} set={(assigned ? assignedState.ToString(CultureInfo.InvariantCulture) : "-")} now={storedState}({FormatRemainingMs(storedRemainingMs)}) dur={(hasDuration ? FormatRemainingMs(durationMs) : "-")} use={FormatP4UsedWindowDebug(usedWindow, usedParam, usedWindowRemainingMs)} {FormatP4WindowDebug("X", xParam, xRemainingMs, xActive)} {FormatP4WindowDebug("C", cParam, cRemainingMs, cActive)}");
            ScheduleP4LaterGroupsIfReady(accessory);
        }
    }

    [ScriptMethod(name: "P4 50070后续循环开始", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:50070"], userControl: false)]
    public void P4_ChainStartActionEffect(Event @event, ScriptAccessory accessory)
    {
        _acc = accessory;
        if (_p4Phase != Phase.P4) return;

        accessory.Method.RemoveDraw($"{P4DrawPrefix}_P4_First_.*");
        DebugEcho(accessory, "P4 later timeline start by 50070");
        ScheduleP4LaterGroupsIfReady(accessory);
    }

    [ScriptMethod(name: "P4 后续状态移除推进", eventType: EventTypeEnum.StatusRemove, eventCondition: ["StatusID:regex:^554[3-8]$"], userControl: false)]
    public void P4_TrackedStatusRemove(Event @event, ScriptAccessory accessory)
    {
        _acc = accessory;
        if (_p4Phase != Phase.P4) return;

        if (!TryGetStatusId(@event, out var statusId))
            return;

        if (!TryGetP4TrackedStatusIndex(statusId, out var statusIndex))
            return;

        if (!TryGetTargetId(@event, out var targetId))
            return;

        var partyIndex = GetPlayerIndex(accessory, targetId);
        if (partyIndex < 0 || partyIndex >= 8)
            return;
    }

    [ScriptMethod(name: "P4 后续危险范围捕获", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(47777|47775|47768|47774)$"], userControl: false)]
    public void P4_LaterDangerStartCasting(Event @event, ScriptAccessory accessory)
    {
        _acc = accessory;
        if (_p4Phase != Phase.P4) return;

        if (!TryGetActionId(@event, out var actionId))
            return;

        P4DangerRecord record;
        if (actionId == 47777 || actionId == 47775)
        {
            record = new P4DangerRecord
            {
                ActionId = actionId,
                Shape = P4DangerShape.Rect,
                Position = @event.SourcePosition,
                Rotation = @event.SourceRotation,
                CapturedAt = NowMs(),
            };
        }
        else
        {
            record = new P4DangerRecord
            {
                ActionId = actionId,
                Shape = P4DangerShape.Fan,
                Position = P4ArenaCenter,
                Rotation = @event.SourceRotation,
                CapturedAt = NowMs(),
            };
        }

        var shouldTryFinal = false;
        lock (_p4Lock)
        {
            _p4DangerRecords.Add(record);
            shouldTryFinal = _p4FGroupStartedAt > 0 && !_p4FGroupDangerDrawn;
        }

        DebugEcho(accessory, $"P4DANGER+ id={actionId} shape={record.Shape} pos={FormatPosition(record.Position)} rot={record.Rotation:F3}");

        if (shouldTryFinal)
            TryDrawP4FinalSafe(accessory, _generation);
    }

    [ScriptMethod(name: "P4 一运半场指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(50068|50069)$"], userControl: false)]
    public void P4_FirstMechanicGuide(Event @event, ScriptAccessory accessory)
    {
        _acc = accessory;
        if (_p4Phase != Phase.P4) return;

        if (!TryGetActionId(@event, out var actionId))
            return;

        var states = new int[8, 10];
        int fourthParam;
        int fourthCount;
        long fourthSnapshotAt;
        Vector3 fourthTargetPosition;
        Vector3 blueSourcePosition;
        Vector3 purpleSourcePosition;
        Vector3 newTwelveDirection;
        Vector3 midpoint;

        lock (_p4Lock)
        {
            if (_p4HotpotResolved)
                return;

            if (actionId == P4ResolveActionId)
            {
                _p4HotpotBlueSourcePosition = @event.SourcePosition;
                _p4HotpotBlueSourceReady = true;
            }
            else if (actionId == P4PurpleSourceActionId)
            {
                _p4HotpotPurpleSourcePosition = @event.SourcePosition;
                _p4HotpotPurpleSourceReady = true;
            }
            else
            {
                return;
            }

            fourthParam = _p4FourthXParam;
            fourthCount = _p4XEventCount;
            fourthSnapshotAt = _p4FourthSnapshotAt;
            fourthTargetPosition = _p4FourthTargetPosition;
            blueSourcePosition = _p4HotpotBlueSourcePosition;
            purpleSourcePosition = _p4HotpotPurpleSourcePosition;

            if (!_p4HotpotBlueSourceReady || !_p4HotpotPurpleSourceReady)
            {
                DebugEcho(
                    accessory,
                    $"P4HOTPOT WAIT action={actionId} blueReady={_p4HotpotBlueSourceReady} purpleReady={_p4HotpotPurpleSourceReady} blue50069={FormatPosition(_p4HotpotBlueSourcePosition)} purple50068={FormatPosition(_p4HotpotPurpleSourcePosition)}");
                return;
            }

            if (fourthParam != 1121 && fourthParam != 1122)
            {
                DebugEcho(accessory, $"P4一运结算跳过：第四次19510/2056未就绪 param={fourthParam} target={FormatPosition(_p4FourthTargetPosition)}");
                return;
            }

            midpoint = new Vector3(
                (blueSourcePosition.X + purpleSourcePosition.X) * 0.5f,
                (blueSourcePosition.Y + purpleSourcePosition.Y) * 0.5f,
                (blueSourcePosition.Z + purpleSourcePosition.Z) * 0.5f);

            if (!TryNormalizeFromCenter(midpoint, out newTwelveDirection))
            {
                DebugEcho(accessory, $"P4一运结算跳过：50068/50069中点无效 blue50069={FormatPosition(blueSourcePosition)} purple50068={FormatPosition(purpleSourcePosition)} mid={FormatPosition(midpoint)}");
                return;
            }

            Array.Copy(_p4StatusStateByPartyAndStatus, states, _p4StatusStateByPartyAndStatus.Length);
            _p4HotpotResolved = true;
        }

        var rightVector = RightVectorFromNewTwelve(newTwelveDirection);
        var l1 = P4ArenaCenter - rightVector * P4GuideOffset;
        var r1 = P4ArenaCenter + rightVector * P4GuideOffset;
        var blueSide = SideOfPosition(blueSourcePosition, rightVector);
        var purpleSide = SideOfPosition(purpleSourcePosition, rightVector);
        var blueDelta = new Vector3(blueSourcePosition.X - P4ArenaCenter.X, 0.0f, blueSourcePosition.Z - P4ArenaCenter.Z);
        var purpleDelta = new Vector3(purpleSourcePosition.X - P4ArenaCenter.X, 0.0f, purpleSourcePosition.Z - P4ArenaCenter.Z);
        var blueDot = blueDelta.X * rightVector.X + blueDelta.Z * rightVector.Z;
        var purpleDot = purpleDelta.X * rightVector.X + purpleDelta.Z * rightVector.Z;
        var leftColor = blueSide == P4HalfSide.Left
            ? P4HalfColor.Blue
            : purpleSide == P4HalfSide.Left
                ? P4HalfColor.Purple
                : P4HalfColor.Unknown;
        var rightColor = blueSide == P4HalfSide.Right
            ? P4HalfColor.Blue
            : purpleSide == P4HalfSide.Right
                ? P4HalfColor.Purple
                : P4HalfColor.Unknown;
        if (leftColor == P4HalfColor.Unknown && rightColor != P4HalfColor.Unknown)
            leftColor = OppositeColor(rightColor);
        if (rightColor == P4HalfColor.Unknown && leftColor != P4HalfColor.Unknown)
            rightColor = OppositeColor(leftColor);
        var fourthAgeMs = fourthSnapshotAt > 0 ? NowMs() - fourthSnapshotAt : -1;

        DebugEcho(
            accessory,
            $"P4HOTPOT DIAG xCount={fourthCount} fourthParam={fourthParam} fourthAge={fourthAgeMs}ms fourthTarget={FormatPosition(fourthTargetPosition)} blue50069={FormatPosition(blueSourcePosition)} purple50068={FormatPosition(purpleSourcePosition)} mid={FormatPosition(midpoint)} new12={FormatPosition(newTwelveDirection)} rightVec={FormatPosition(rightVector)} blueDot={blueDot:F3} purpleDot={purpleDot:F3} blueSide={FormatP4Side(blueSide)} purpleSide={FormatP4Side(purpleSide)} left={FormatP4Color(leftColor)} right={FormatP4Color(rightColor)}");

        var sides = new P4HalfSide[8];
        var colors = new P4HalfColor[8];

        for (var i = 0; i < 8; i++)
        {
            if (!TryResolveP4PlayerColor(states, i, out var color, out _, out _))
            {
                colors[i] = P4HalfColor.Unknown;
                sides[i] = P4HalfSide.Unknown;
                continue;
            }

            colors[i] = color;
            sides[i] = SideForColor(color, leftColor, rightColor);
        }

        lock (_p4Lock)
        {
            Array.Copy(sides, _p4ResolvedSideByPartyIndex, sides.Length);
            Array.Copy(colors, _p4ResolvedColorByPartyIndex, colors.Length);
        }

        var myIndex = GetMyIndex(accessory);
        if (Debugging)
        {
            for (var i = 0; i < sides.Length; i++)
            {
                if (i == myIndex || sides[i] is not (P4HalfSide.Left or P4HalfSide.Right))
                    continue;
                var pos = sides[i] == P4HalfSide.Right ? r1 : l1;
                DrawGuide(accessory, $"{P4DrawPrefix}_P4_First_Guide_{i}", pos, P4GuideDurationMs, partyIndex: i);
            }
        }

        if (myIndex >= 0 && myIndex < sides.Length)
        {
            var mySide = sides[myIndex];
            if (mySide == P4HalfSide.Left || mySide == P4HalfSide.Right)
            {
                var targetPosition = mySide == P4HalfSide.Right ? r1 : l1;
                DrawGuide(accessory, $"{P4DrawPrefix}_P4_First_Guide_{myIndex}", targetPosition, P4GuideDurationMs);
                DebugEcho(accessory, $"P4一运指路已画：my={PartyPriorityLabel(myIndex)} side={FormatP4Side(mySide)} color={FormatP4Color(colors[myIndex])} target={FormatPosition(targetPosition)} duration={P4GuideDurationMs}ms");
            }
            else
            {
                DebugEcho(accessory, $"P4一运指路未画：my={PartyPriorityLabel(myIndex)} side={FormatP4Side(mySide)} color={FormatP4Color(colors[myIndex])}");
            }
        }
        else
        {
            DebugEcho(accessory, $"P4一运指路未画：myIndex={myIndex} partyCount={accessory.Data.PartyList.Count}");
        }

        DebugP4Resolve(accessory, states, sides, colors, fourthParam, blueSide, leftColor, rightColor, newTwelveDirection, l1, r1);
    }

    #endregion

    #endregion

    #region P5 绘制（Cicero 灵视）

    #region Major_Phase_5

    [ScriptMethod(name: "———————— 《P5》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"])]
    public void P5_分割线(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name:"P5 (阶段控制)",
        eventType:EventTypeEnum.Targetable,
        eventCondition:["DataId:19511"],
        userControl:false)]

    public void P5_阶段控制(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=4) {

            return;

        }

        if(!string.Equals(@event["Targetable"],"True")) {

            return;

        }

        accessory.Method.RemoveDraw(".*");

        majorPhase=5;
        phase=1;

        if(enableDebugLogging) {

            accessory.Log.Debug($"majorPhase={majorPhase}\nphase={phase}");

        }

    }

    [ScriptMethod(name:"P5 癫狂交响曲 神圣 (范围)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:47952"])]

    public void P5_癫狂交响曲_神圣_范围(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=5) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["SourceId"],out var sourceId)) {

            return;

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        for(uint i=3;i<=8;++i) {

            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(5);
            currentProperties.Owner=sourceId;
            currentProperties.CentreResolvePattern=PositionResolvePatternEnum.OwnerEnmityOrder;
            currentProperties.CentreOrderIndex=i;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.DestoryAt=5875;

            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,currentProperties);

        }

        for(uint i=1;i<=3;++i) {

            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(5);
            currentProperties.Owner=sourceId;
            currentProperties.CentreResolvePattern=PositionResolvePatternEnum.PlayerNearestOrder;
            currentProperties.CentreOrderIndex=i;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.Delay=5875;
            currentProperties.DestoryAt=3125;

            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,currentProperties);

        }

    }

    [ScriptMethod(name:"P5 癫狂交响曲 核爆与混沌核爆 (范围)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:47952"])]

    public void P5_癫狂交响曲_核爆与混沌核爆_范围(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=5) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["SourceId"],out var sourceId)) {

            return;

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        for(uint i=1;i<=2;++i) {

            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(5);
            currentProperties.Owner=sourceId;
            currentProperties.CentreResolvePattern=PositionResolvePatternEnum.OwnerEnmityOrder;
            currentProperties.CentreOrderIndex=i;
            currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(1);
            currentProperties.DestoryAt=5875;

            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,currentProperties);

        }

        currentProperties=accessory.Data.GetDefaultDrawProperties();

        currentProperties.Scale=new(5);
        currentProperties.Owner=sourceId;
        currentProperties.CentreResolvePattern=PositionResolvePatternEnum.OwnerEnmityOrder;
        currentProperties.CentreOrderIndex=1;
        currentProperties.Delay=5875;
        currentProperties.DestoryAt=3125;

        int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);

        if(!isLegalPartyIndex(myIndex)) {

            currentProperties.Color=accessory.Data.DefaultDangerColor;

        }

        else {

            if(isTank(myIndex)) {

                currentProperties.Color=colourOfDirectionIndicators.V4.WithW(1);

            }

            else {

                currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(1);

            }

        }

        accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,currentProperties);

    }

    [ScriptMethod(name:"P5 癫狂交响曲 顺手加个神圣 (范围)",
        eventType:EventTypeEnum.StatusAdd,
        eventCondition:["StatusID:5351"])]

    public void P5_癫狂交响曲_顺手加个神圣_范围(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=5) {

            return;

        }

        if(string.Equals(@event["SourceId"],"00000000")) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        int durationMilliseconds=0;

        try {

            durationMilliseconds=JsonConvert.DeserializeObject<int>(@event["DurationMilliseconds"]);

        } catch(Exception e) {

            accessory.Log.Error("DurationMilliseconds deserialization failed.");

            return;

        }

        if(durationMilliseconds<=0||durationMilliseconds>MAXIMUM_DURATION) {

            return;

        }

        int standardDuration=3375;
        int delay=0;
        int duration=0;

        if(durationMilliseconds<=standardDuration) {

            delay=0;
            duration=durationMilliseconds;

        }

        else {

            delay=durationMilliseconds-standardDuration;
            duration=standardDuration;

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        currentProperties.Name=$"P5_癫狂交响曲_顺手加个神圣_范围_{targetId}";
        currentProperties.Scale=new(6);
        currentProperties.Owner=targetId;
        currentProperties.Color=colourOfDirectionIndicators.V4.WithW(1);
        currentProperties.Delay=delay;
        currentProperties.DestoryAt=duration;

        accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,currentProperties);

    }

    [ScriptMethod(name:"P5 癫狂交响曲 顺手加个神圣 (范围清除)",
        eventType:EventTypeEnum.StatusRemove,
        eventCondition:["StatusID:5351"],
        userControl:false)]

    public void P5_癫狂交响曲_顺手加个神圣_范围清除(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=5) {

            return;

        }

        if(string.Equals(@event["SourceId"],"00000000")) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        accessory.Method.RemoveDraw($"P5_癫狂交响曲_顺手加个神圣_范围_{targetId}");

    }

    [ScriptMethod(name:"P5 癫狂交响曲 顺手加个核爆 (范围)",
        eventType:EventTypeEnum.StatusAdd,
        eventCondition:["StatusID:5350"])]

    public void P5_癫狂交响曲_顺手加个核爆_范围(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=5) {

            return;

        }

        if(string.Equals(@event["SourceId"],"00000000")) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        int durationMilliseconds=0;

        try {

            durationMilliseconds=JsonConvert.DeserializeObject<int>(@event["DurationMilliseconds"]);

        } catch(Exception e) {

            accessory.Log.Error("DurationMilliseconds deserialization failed.");

            return;

        }

        if(durationMilliseconds<=0||durationMilliseconds>MAXIMUM_DURATION) {

            return;

        }

        int standardDuration=3375;
        int delay=0;
        int duration=0;

        if(durationMilliseconds<=standardDuration) {

            delay=0;
            duration=durationMilliseconds;

        }

        else {

            delay=durationMilliseconds-standardDuration;
            duration=standardDuration;

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        currentProperties.Name=$"P5_癫狂交响曲_顺手加个核爆_范围_{targetId}";
        currentProperties.Scale=new(25);
        currentProperties.Owner=targetId;
        currentProperties.Color=accessory.Data.DefaultDangerColor;
        currentProperties.Delay=delay;
        currentProperties.DestoryAt=duration;

        accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,currentProperties);

    }

    [ScriptMethod(name:"P5 癫狂交响曲 顺手加个核爆 (范围清除)",
        eventType:EventTypeEnum.StatusRemove,
        eventCondition:["StatusID:5350"],
        userControl:false)]

    public void P5_癫狂交响曲_顺手加个核爆_范围清除(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=5) {

            return;

        }

        if(string.Equals(@event["SourceId"],"00000000")) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        accessory.Method.RemoveDraw($"P5_癫狂交响曲_顺手加个核爆_范围_{targetId}");

    }

    #endregion

    #endregion

    #region P5 绘制与指路（Errer）

    public enum FireSafeStrategy { Center, Up, Down, Left, Right }

    #region State

    private const string P5DrawPrefix = "Errer_YW_P5Fire";
    private const string FireSafePrefix = P5DrawPrefix + "_Safe";
    private const string TowerPrefix = "Errer_P5T";
    private static readonly Vector3 P5ArenaCenter = new(100f, 0f, 100f);
    private static readonly Vector3 TowerAnchorA = new(100f, 0f, 85f);

    private const DrawModeEnum DM = DrawModeEnum.Default;
    private const int TowerGuideDurationMs = 6500;
    private const int TowerAssignMaxWaitMs = 2500;
    private const int AdvanceDrawMs = 1500;     // 地火步进圈在读条结束前多久开始画
    private const float FloodMaxDist = 8f;      // 洪水交点离场中超过这个距离视为无效组
    private const float FireCircleRadius = 6f;
    private const float FireStepDistance = 7f;
    private const int FireStepCount = 7;
    private const int FireFirstExplosionDelayMs = 700;
    private const int FireExplosionIntervalMs = 700;
    private const int FireAfterExplosionKeepMs = 0;
    private const int FireHitBaseMs = 4069;
    private const int FireHitIntervalMs = 513;
    private const int FireWaveIntervalMs = 2500;
    private const int FireGuideDurationMs = 18000;
    private const int FireSequenceGapMs = 10000;  // 超过此间隔视为新序列，允许重新初始化
    private const int FireInitialPointDurationMs = 10000;
    private const float FireRouteOffset = 5f;
    private const float FireDiagonalComponentOffset = 2.5f;
    private const float SteelRadius = 10f;
    private const float DonutInnerRadius = 10f;
    private const float DonutOuterRadius = 50f;
    private const int SteelDonutDurationMs = 8000;
    private const int WaterDelayMs = 4000;
    private const int WaterDurationMs = 1800;
    private const float WaterWidth = 10f;
    private const float WaterLength = 60f;
    private const float WaterBackOffset = 30f;
    private const float FloodSafeDistance = 13.5f;
    private const int Flood2DelayMs = 0;
    private const int Flood2DurationMs = 3500;
    private const int Flood1DelayMs = 3500;
    private const int Flood1DurationMs = 3000;
    private const float CastSpreadRadius = 5f;
    private const int TowerNewDebuffMinRemainMs = 16000;
    private const int TowerNewDebuffMaxRemainMs = 20000;
    private const int TowerAssignPollMs = 100;

    private int _fireCount;
    private int _fireDrawGen;   // 全局只增不减，新旧绘图名永不重叠
    private bool _fireGuideInitialized;

    // 安全点方向按职能下标取（FireAutoStrategies，勾了 LPDU 打法则全员 Center），不同人路线不同；
    // 每种方向一条轨道，各自记相位、画路线，指路箭头画给走这条轨道的人
    private sealed class FireGuideTrack
    {
        public FireSafeStrategy Strategy;
        public readonly List<int> PartyIndices = new();
        public int LeftCol;
        public int RightCol;
        public Vector3 SafeCenter = new(100f, 0f, 100f);
        public int Gen;     // 当前这段指路的绘图代号，见 ClearFireTrackDraws
        public bool InitialPointDrawn;
        public bool RouteCompleted;
        public bool FirstHalfDrawn;
        public readonly List<int> Cols = new();
        public readonly List<int> Rels = new();
    }

    private readonly List<FireGuideTrack> _fireTracks = new();
    // 当前正在处理的轨道；只在 _fireLock 内切换
    private FireGuideTrack _curFire = new();
    private int _fireCandidateLeftCol { get => _curFire.LeftCol; set => _curFire.LeftCol = value; }
    private int _fireCandidateRightCol { get => _curFire.RightCol; set => _curFire.RightCol = value; }
    private Vector3 _fireSafeCenter { get => _curFire.SafeCenter; set => _curFire.SafeCenter = value; }
    private bool _fireInitialPointDrawn { get => _curFire.InitialPointDrawn; set => _curFire.InitialPointDrawn = value; }
    private bool _fireRouteCompleted { get => _curFire.RouteCompleted; set => _curFire.RouteCompleted = value; }
    private bool _fireFirstHalfDrawn { get => _curFire.FirstHalfDrawn; set => _curFire.FirstHalfDrawn = value; }
    private List<int> _fireGuideCols => _curFire.Cols;
    private List<int> _fireGuideRels => _curFire.Rels;
    // 框架每方法一线程且无序列化，47931(初始化)与47932(步进)会并发变更火状态，
    // 迟到的 47931 会 Clear 掉已记录的相位导致 n 索引错位、误选路线分支，故加锁串行化。
    private readonly object _fireLock = new object();
    private DateTime _lastFireActivityTime = DateTime.MinValue;

    private enum TowerElement { Fire = 2015294, Ice = 2015295, Thunder = 2015296 }

    private static readonly FireSafeStrategy[] FireAutoStrategies =
    {
        FireSafeStrategy.Center, FireSafeStrategy.Center,
        FireSafeStrategy.Left, FireSafeStrategy.Right,
        FireSafeStrategy.Center, FireSafeStrategy.Center,
        FireSafeStrategy.Up, FireSafeStrategy.Down
    };

    private static readonly string[] TowerLabels = { "MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4" };

    private readonly object _towerLock = new();
    private readonly Dictionary<Vector3, TowerElement> _towerRoundTowers = new();
    private readonly Dictionary<int, (TowerElement Element, DateTime ExpireAt)> _towerDebuffs = new();
    private readonly Dictionary<int, Vector3> _towerRawTargets = new();
    private readonly HashSet<TowerElement> _towerSeenDouble = new();
    private readonly HashSet<int> _towerIdlePlayers = new();
    private bool _towerCooling, _towerIdleLocked;
    private int _towerMechanicSeq;
    private int _towerRound;

    // ── 洪水2穿1：每4个读条为一组，异向配对 ──
    private int _floodCount;
    private int _floodGroupCount;
    private readonly Vector3[] _floodNegPos = new Vector3[2]; private readonly float[] _floodNegRot = new float[2]; private int _negCount;
    private readonly Vector3[] _floodPosPos = new Vector3[2]; private readonly float[] _floodPosRot = new float[2]; private int _posCount;
    private readonly float[] _floodSeqRot = new float[4]; private int _seqCount;
    private Vector3? _savedFloodSafe1;
    private Vector3? _savedFloodMid1;
    // 框架每个事件处理器独立线程(ScriptManager 每方法一线程,无序列化)，配对读条会并发进入 OnWater，
    // 必须加锁串行化洪水状态变更，避免丢失更新(_floodCount/计数器/数组竞态)。
    private readonly object _floodLock = new object();

    #endregion

    #region Init

    private void Init_Errer(ScriptAccessory accessory)
    {
        lock (_fireLock)
        {
            _fireCount = 0;
            _lastFireActivityTime = DateTime.MinValue;
            ClearFireSafeGuideState();
        }
        _floodCount = 0;
        _floodGroupCount = 0;
        _negCount = 0; _posCount = 0; _seqCount = 0;
        _savedFloodSafe1 = null;
        _savedFloodMid1 = null;
        ResetTowerGuideState(true);
        accessory.Method.RemoveDraw($"{P5DrawPrefix}_.*");
        accessory.Method.RemoveDraw($"{TowerPrefix}_.*");
    }

    #endregion

    #region 地火步进圈

    // 47932 地火：每个读条沿面向 7 段步进爆炸，6m 圈、间隔 7m / 700ms
    [ScriptMethod(name: "P5 地火 (步进圈范围)", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:47932"])]
    public void OnFireCast(Event @event, ScriptAccessory accessory)
    {
        var pos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
        var dir = RotationToDirection(float.Parse(@event["SourceRotation"]));
        var castDurationMs = DurationMs(@event, 5000);
        var delayMs = Math.Max(0, castDurationMs - AdvanceDrawMs);
        // 计数只用来给同时读条的多路地火区分 dp.Name
        var round = Interlocked.Increment(ref _fireCount);

        for (var i = 0; i < FireStepCount; i++)
        {
            var explodeAtMs = castDurationMs + FireFirstExplosionDelayMs + FireExplosionIntervalMs * i;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"{P5DrawPrefix}_Fire{round}_Step{i}";
            dp.Position = pos + dir * (FireStepDistance * i);
            dp.Scale = new Vector2(FireCircleRadius);
            dp.Color = accessory.Data.DefaultDangerColor.WithW(Math.Clamp(p5FireOpacity, 0, 100) / 100f);
            dp.Delay = delayMs;
            dp.DestoryAt = Math.Max(1, explodeAtMs - delayMs + FireAfterExplosionKeepMs);
            dp.ScaleMode = ScaleMode.None;
            accessory.Method.SendDraw(DM, DrawTypeEnum.Circle, dp);
        }
    }

    // 47931 地火起手：按安全点方向初始化轨道并画初始点；47932 每路地火：记录相位、推进路线；
    // 47934 咏唱中分散：地火结束，收掉安全点
    [ScriptMethod(name: "P5 地火 (安全点指路)", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47931|47932|47934)$"])]
    public void OnFireSafeGuide(Event @event, ScriptAccessory accessory)
    {
        var actionId = @event.ActionId();
        if (actionId == 47934)
        {
            RemoveFireSafeGuideDraws(accessory);
            return;
        }

        // 火状态(相位列表/初始化标记)在框架多线程下会并发变更，整体串行化。
        lock (_fireLock)
        {
            EnsureFireSequenceInitialized(accessory);
            _lastFireActivityTime = DateTime.Now;
            if (actionId == 47932)
                HandleFireSafeGuideCast(accessory, JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]));
        }
    }

    // 幂等初始化：仅在检测到"新序列"时才重置火状态并重画安全点。
    // 新序列判定：尚未初始化，或距上次火事件已超过 FireSequenceGapMs。
    // 这样迟到的 47931 不会清掉同一序列中 47932 已记录的相位。
    // 调用方必须已持有 _fireLock。
    private void EnsureFireSequenceInitialized(ScriptAccessory accessory)
    {
        var gapMs = (DateTime.Now - _lastFireActivityTime).TotalMilliseconds;
        var isNewSequence = !_fireGuideInitialized || gapMs > FireSequenceGapMs;
        if (!isNewSequence)
            return;

        // 只删上一轮各轨道的代号；整前缀删会把下面马上画的新图一起删掉（见 ClearFireTrackDraws）
        foreach (var old in _fireTracks)
        {
            _curFire = old;
            ClearFireTrackDraws(accessory);
        }
        ClearFireSafeGuideState();
        _fireGuideInitialized = true;

        var myIndex = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
        for (var i = 0; i < Math.Min(8, accessory.Data.PartyList.Count); i++)
        {
            if (!Debugging && i != myIndex)
                continue;
            var st = ResolveFireSafeStrategy(i);
            var track = _fireTracks.FirstOrDefault(t => t.Strategy == st);
            if (track == null)
                _fireTracks.Add(track = new FireGuideTrack { Strategy = st, Gen = ++_fireDrawGen });
            track.PartyIndices.Add(i);
        }

        foreach (var track in _fireTracks)
        {
            _curFire = track;
            InitFireTrack(accessory, track.Strategy);
        }
    }

    private void InitFireTrack(ScriptAccessory accessory, FireSafeStrategy strategy)
    {
        (_fireCandidateLeftCol, _fireCandidateRightCol) = FireCandidateColumns(strategy);
        _fireSafeCenter = FireSafeCenter(_fireCandidateLeftCol, _fireCandidateRightCol);
        DrawFireGuideTo(accessory, "Center", _fireSafeCenter, 0, FireGuideDurationMs);

        if (enableDebugLogging)
            accessory.Method.SendChat($"/e [地火安全] 方向={strategy} L={_fireCandidateLeftCol} R={_fireCandidateRightCol} center=({_fireSafeCenter.X:F1},{_fireSafeCenter.Z:F1})");
    }

    private void ClearFireSafeGuideState()
    {
        _fireGuideInitialized = false;
        _fireTracks.Clear();
        _curFire = new FireGuideTrack();
    }

    private void HandleFireSafeGuideCast(ScriptAccessory accessory, Vector3 pos)
    {
        foreach (var track in _fireTracks)
        {
            _curFire = track;
            HandleFireSafeGuideCastForTrack(accessory, pos);
        }
    }

    private void HandleFireSafeGuideCastForTrack(ScriptAccessory accessory, Vector3 pos)
    {
        var col = FireInitialColumn(pos.X);
        if (col > 3)
            return;

        var isLeft = pos.X < P5ArenaCenter.X;
        var candidateCol = isLeft ? _fireCandidateLeftCol : _fireCandidateRightCol;
        var rel = PositiveMod(col + 1 - candidateCol, 3);
        _fireGuideCols.Add(col);
        _fireGuideRels.Add(rel);
        var n = _fireGuideRels.Count;

        if (enableDebugLogging)
            accessory.Method.SendChat($"/e [地火安全] n={n} side={(isLeft ? "左" : "右")} cols={string.Join(":", _fireGuideCols)} rels={string.Join(":", _fireGuideRels)}");

        if (!_fireInitialPointDrawn && n <= 3 && rel != 0 && _fireGuideRels.Take(n - 1).All(r => r == 0))
        {
            DrawFireInitialPoint(accessory, n, rel);
            _fireInitialPointDrawn = true;
        }

        if (_fireRouteCompleted || n < 2)
            return;

        if (n == 2)
        {
            var rel1 = _fireGuideRels[0];
            var rel2 = _fireGuideRels[1];
            if (rel1 != 0 && rel2 != 0)
                DrawFireFullRouteFrom12(accessory, rel1, rel2);
            else if (rel1 != 0 && rel2 == 0)
                DrawFireFirstHalfRoute(accessory, rel1);
        }
        else if (n == 3)
        {
            var rel1 = _fireGuideRels[0];
            var rel2 = _fireGuideRels[1];
            var rel3 = _fireGuideRels[2];
            if (rel1 == 0 && rel2 != 0 && rel3 != 0)
                DrawFireFullRouteFrom23(accessory, rel2, rel3);
        }
        else if (n == 4)
        {
            var rel1 = _fireGuideRels[0];
            var rel2 = _fireGuideRels[1];
            var rel3 = _fireGuideRels[2];
            var rel4 = _fireGuideRels[3];
            if (rel1 == 0 && rel2 == 0 && rel3 != 0 && rel4 != 0)
                DrawFireFullRouteFrom34(accessory, rel3, rel4);
            else if (_fireFirstHalfDrawn && rel1 != 0 && rel2 == 0 && rel4 != 0)
                DrawFireSecondHalfRoute(accessory, rel1, rel4);
        }
    }

    private void DrawFireFullRouteFrom12(ScriptAccessory accessory, int rel1, int rel2)
    {
        var clockwise = rel1 == rel2;
        var dir4 = FireDir4FromRelPair(rel1, rel2);
        var lateSafeCol = _fireCandidateRightCol + (rel2 == 1 ? 1 : 0);
        var earlyDangerCol = _fireCandidateLeftCol + (rel1 == 1 ? 0 : 1);
        DrawFireFullRoute(accessory, "Full12", dir4, clockwise, FireDelayForColumn(lateSafeCol) - FireWaveIntervalMs, FireDelayForColumn(earlyDangerCol));
    }

    private void DrawFireFullRouteFrom23(ScriptAccessory accessory, int rel2, int rel3)
    {
        var clockwise = rel2 != rel3;
        var dir4 = FireDir4FromRelPair(rel3, rel2);
        var lateSafeCol = _fireCandidateLeftCol + (rel3 == 1 ? 1 : 0);
        var earlyDangerCol = _fireCandidateRightCol + (rel2 == 1 ? 0 : 1);
        DrawFireFullRoute(accessory, "Full23", dir4, clockwise, FireDelayForColumn(lateSafeCol) - FireWaveIntervalMs, FireDelayForColumn(earlyDangerCol));
    }

    private void DrawFireFullRouteFrom34(ScriptAccessory accessory, int rel3, int rel4)
    {
        var clockwise = rel3 == rel4;
        var dir4 = FireDir4FromRelPair(rel3, rel4);
        var lateSafeCol = _fireCandidateRightCol + (rel4 == 1 ? 1 : 0);
        var earlyDangerCol = _fireCandidateLeftCol + (rel3 == 1 ? 0 : 1);
        DrawFireFullRoute(accessory, "Full34", dir4, clockwise, FireDelayForColumn(lateSafeCol) - FireWaveIntervalMs, FireDelayForColumn(earlyDangerCol));
    }

    private void DrawFireFirstHalfRoute(ScriptAccessory accessory, int rel1)
    {
        var dirN4 = rel1 == 1 ? 3 : 1;
        var from = FireDiagonalPoint(dirN4);
        var to = _fireSafeCenter + (_fireSafeCenter - from);
        var delay = Math.Max(0, FireDelayForColumn(_fireCandidateRightCol + 1) - FireWaveIntervalMs);
        var total = Math.Max(FireGuideDurationMs, delay + 5000);

        ClearFireTrackDraws(accessory);
        DrawFireGuideTo(accessory, "FirstHalf_From", from, 0, delay);
        DrawFirePreview(accessory, "FirstHalf_Preview", from, to, 0, delay);
        DrawFireGuideTo(accessory, "FirstHalf_To", to, delay, total - delay);
        _fireFirstHalfDrawn = true;

        if (enableDebugLogging)
            accessory.Method.SendChat($"/e [地火安全] 路线14前半 dirN4={dirN4} delay={delay}ms");
    }

    private void DrawFireSecondHalfRoute(ScriptAccessory accessory, int rel1, int rel4)
    {
        var clockwise = rel1 == rel4;
        var dir4 = FireDir4FromRelPair(rel1, rel4);
        var route = FireFullRouteCardinals(dir4, clockwise);
        var p2 = FireCardinalPoint(route[1]);
        var p3 = FireCardinalPoint(route[2]);
        var delay = Math.Max(0, FireDelayForColumn(_fireCandidateLeftCol + (rel1 == 1 ? 0 : 1)));
        var total = Math.Max(FireGuideDurationMs, delay + 5000);

        // 后半段取代前半段，不然两根绿线同时出现
        ClearFireTrackDraws(accessory);
        DrawFireGuideTo(accessory, "SecondHalf_P2", p2, 0, delay);
        DrawFirePreview(accessory, "SecondHalf_Preview", p2, p3, 0, delay);
        DrawFireGuideTo(accessory, "SecondHalf_P3", p3, delay, total - delay);
        _fireRouteCompleted = true;

        if (enableDebugLogging)
            accessory.Method.SendChat($"/e [地火安全] 路线14后半 dir4={dir4} clockwise={clockwise} delay={delay}ms");
    }

    private void DrawFireInitialPoint(ScriptAccessory accessory, int n, int rel)
    {
        var dirN4 = n == 2 ? (rel == 1 ? 2 : 0) : (rel == 1 ? 3 : 1);
        var pos = FireDiagonalPoint(dirN4);
        ClearFireTrackDraws(accessory);
        DrawFireGuideTo(accessory, "InitialPoint", pos, 0, FireInitialPointDurationMs);

        if (enableDebugLogging)
            accessory.Method.SendChat($"/e [地火安全] 初始斜点 n={n} rel={rel} dirN4={dirN4}");
    }

    private void DrawFireFullRoute(ScriptAccessory accessory, string suffix, int dir4, bool clockwise, int delay1, int delay2)
    {
        ClearFireTrackDraws(accessory);

        delay1 = Math.Max(0, delay1);
        delay2 = Math.Max(delay1 + 1, delay2);
        var total = Math.Max(FireGuideDurationMs, delay2 + 5000);
        var route = FireFullRouteCardinals(dir4, clockwise);
        var p1 = FireCardinalPoint(route[0]);
        var p2 = FireCardinalPoint(route[1]);
        var p3 = FireCardinalPoint(route[2]);

        DrawFireGuideTo(accessory, $"{suffix}_P1", p1, 0, delay1);
        DrawFirePreview(accessory, $"{suffix}_Preview12", p1, p2, 0, delay1);
        DrawFireGuideTo(accessory, $"{suffix}_P2", p2, delay1, delay2 - delay1);
        DrawFirePreview(accessory, $"{suffix}_Preview23", p2, p3, delay1, delay2 - delay1);
        DrawFireGuideTo(accessory, $"{suffix}_P3", p3, delay2, total - delay2);
        _fireRouteCompleted = true;

        if (enableDebugLogging)
            accessory.Method.SendChat($"/e [地火安全] {suffix} dir4={dir4} clockwise={clockwise} delay1={delay1}ms delay2={delay2}ms");
    }

    // 绘图名：{FireSafePrefix}_{方向}_g{代号}_{suffix}[_{下标}]
    private string FireDrawName(string suffix) => $"{FireSafePrefix}_{_curFire.Strategy}_g{_curFire.Gen}_{suffix}";

    private void DrawFireGuideTo(ScriptAccessory accessory, string suffix, Vector3 target, int delayMs, int durationMs)
    {
        foreach (var i in _curFire.PartyIndices)
            P5Guide(accessory, FireDrawName(suffix), i, target, delayMs, durationMs);
    }

    private void DrawFirePreview(ScriptAccessory accessory, string suffix, Vector3 from, Vector3 to, int delayMs, int durationMs)
        => P5Preview(accessory, FireDrawName(suffix), from, to, delayMs, durationMs);

    // 新的一段路线取代当前轨道之前画的指路；别把其他方向的轨道一起删了。
    // RemoveDraw 下一帧才在主线程执行、正则不锚定，而 Imgui 指路 SendDraw 当场入列：先删再画同前缀，
    // 新图会被一起删掉（之前地火整段不指路就是这个）。所以每段换一个新代号，只删旧代号
    private void ClearFireTrackDraws(ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw($"{FireSafePrefix}_{_curFire.Strategy}_g{_curFire.Gen}_");
        _curFire.Gen = ++_fireDrawGen;
    }

    private void RemoveFireSafeGuideDraws(ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw($"{FireSafePrefix}_.*");
    }

    private FireSafeStrategy ResolveFireSafeStrategy(int partyIndex)
    {
        // LPDU 全员同一条轨道，不按职能拆
        if (p5FireFollowLpdu)
            return FireSafeStrategy.Center;

        return partyIndex >= 0 && partyIndex < FireAutoStrategies.Length ? FireAutoStrategies[partyIndex] : FireSafeStrategy.Center;
    }

    private static (int Left, int Right) FireCandidateColumns(FireSafeStrategy strategy)
    {
        return strategy switch
        {
            FireSafeStrategy.Up => (4, 2),
            FireSafeStrategy.Down => (2, 4),
            FireSafeStrategy.Left => (2, 2),
            FireSafeStrategy.Right => (4, 4),
            _ => (3, 3)
        };
    }

    private static Vector3 FireSafeCenter(int leftCol, int rightCol)
    {
        return new Vector3(70f + 5f * leftCol + 5f * rightCol, 0f, 100f - 5f * leftCol + 5f * rightCol);
    }

    private static int FireInitialColumn(float x)
    {
        return PositiveMod((int)MathF.Round((x - 70f) / 5f), 7) + 1;
    }

    private static int FireDelayForColumn(int col)
    {
        return FireHitBaseMs + FireHitIntervalMs * col;
    }

    private static int FireDir4FromRelPair(int firstRel, int secondRel)
    {
        return (firstRel, secondRel) switch
        {
            (1, 1) => 3,
            (1, 2) => 0,
            (2, 1) => 2,
            (2, 2) => 1,
            _ => 0
        };
    }

    private static int[] FireFullRouteCardinals(int dir4, bool clockwise)
    {
        return (dir4, clockwise) switch
        {
            (0, false) => new[] { 0, 1, 2 },
            (0, true) => new[] { 0, 3, 2 },
            (1, false) => new[] { 1, 2, 3 },
            (1, true) => new[] { 1, 0, 3 },
            (2, false) => new[] { 2, 3, 0 },
            (2, true) => new[] { 2, 1, 0 },
            (3, false) => new[] { 3, 0, 1 },
            (3, true) => new[] { 3, 2, 1 },
            _ => new[] { 0, 1, 2 }
        };
    }

    private Vector3 FireCardinalPoint(int cardinal)
    {
        return _fireSafeCenter + FireCardinalOffset(cardinal);
    }

    private static Vector3 FireCardinalOffset(int cardinal)
    {
        return cardinal switch
        {
            0 => new Vector3(0f, 0f, -FireRouteOffset),
            1 => new Vector3(-FireRouteOffset, 0f, 0f),
            2 => new Vector3(0f, 0f, FireRouteOffset),
            3 => new Vector3(FireRouteOffset, 0f, 0f),
            _ => Vector3.Zero
        };
    }

    private Vector3 FireDiagonalPoint(int dirN4)
    {
        var offset = dirN4 switch
        {
            0 => new Vector3(-FireDiagonalComponentOffset, 0f, -FireDiagonalComponentOffset),
            1 => new Vector3(-FireDiagonalComponentOffset, 0f, FireDiagonalComponentOffset),
            2 => new Vector3(FireDiagonalComponentOffset, 0f, FireDiagonalComponentOffset),
            3 => new Vector3(FireDiagonalComponentOffset, 0f, -FireDiagonalComponentOffset),
            _ => Vector3.Zero
        };
        return _fireSafeCenter + offset;
    }

    #endregion

    #region 钢铁/月环（二选一的灾祟）

    // 49742 二选一的灾祟·地震：Boss 脚下 10m 钢铁
    [ScriptMethod(name: "P5 二选一的灾祟 钢铁 (范围)", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:49742"])]
    public void OnSteel(Event @event, ScriptAccessory accessory)
    {
        if (!P5TryParseObjectId(@event["SourceId"], out var sid)) return;

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"{P5DrawPrefix}_Steel";
        dp.Owner = sid;
        dp.Scale = new Vector2(SteelRadius);
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.DestoryAt = SteelDonutDurationMs;
        dp.ScaleMode = ScaleMode.None;
        accessory.Method.SendDraw(DM, DrawTypeEnum.Circle, dp);
    }

    // 49743 二选一的灾祟·龙卷：10m 内安全的 50m 月环
    [ScriptMethod(name: "P5 二选一的灾祟 月环 (范围)", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:49743"])]
    public void OnDonut(Event @event, ScriptAccessory accessory)
    {
        if (!P5TryParseObjectId(@event["SourceId"], out var sid)) return;

        var dpDanger = accessory.Data.GetDefaultDrawProperties();
        dpDanger.Name = $"{P5DrawPrefix}_DonutDanger";
        dpDanger.Owner = sid;
        dpDanger.Scale = new Vector2(DonutOuterRadius);
        dpDanger.InnerScale = new Vector2(DonutInnerRadius);
        dpDanger.Radian = MathF.PI * 2f;
        dpDanger.Color = accessory.Data.DefaultDangerColor;
        dpDanger.DestoryAt = SteelDonutDurationMs;
        dpDanger.ScaleMode = ScaleMode.None;
        accessory.Method.SendDraw(DM, DrawTypeEnum.Donut, dpDanger);
    }

    #endregion

    #region 咏唱中分散圈

    // 47934 咏唱中分散：读条结束后全员 5m 分散
    [ScriptMethod(name: "P5 咏唱中分散圈 (范围)", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:47934"])]
    public void OnCastSpread(Event @event, ScriptAccessory accessory)
    {
        var durationMs = DurationMs(@event, 4700) + 1000;
        for (var i = 0; i < Math.Min(8, accessory.Data.PartyList.Count); i++)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"{P5DrawPrefix}_CastSpread{i}";
            dp.Owner = accessory.Data.PartyList[i];
            dp.Scale = new Vector2(CastSpreadRadius);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = durationMs;
            dp.ScaleMode = ScaleMode.None;
            accessory.Method.SendDraw(DM, DrawTypeEnum.Circle, dp);
        }
    }

    #endregion

    #region 三星踩塔指路

    [ScriptMethod(name: "三星踩塔机制开始", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:47938"], userControl: false)]
    public void OnTowerMechanicStart(Event @event, ScriptAccessory accessory)
    {
        int seq;
        lock (_towerLock)
        {
            ClearTowerGuideState(false);
            _towerCooling = false;
            _towerIdleLocked = false;
            seq = ++_towerMechanicSeq;
            _towerRound = 0;
        }

        accessory.Method.RemoveDraw($"{TowerPrefix}_.*");
        var delay = DurationMs(@event, 4700) + 1000;
        if (enableDebugLogging) accessory.Method.SendChat($"/e [塔] 机制开始，{delay}ms 后锁定闲人");
        _ = Task.Run(async () => { await Task.Delay(delay); LockTowerIdlePlayers(accessory, seq); });
    }

    // 三星踩塔：每轮 4 座火/冰/雷塔（ObjectEffect Id1=16）出齐后，等新易伤上齐再按元素分配并指路
    // 机制开始/易伤记录两个方法只收数据，关掉这个方法就不分配也不画
    [ScriptMethod(name: "P5 三星踩塔 (指路)", eventType: EventTypeEnum.ObjectEffect,
        eventCondition: ["Id1:16"])]
    public void OnTowerObjectEffect(Event @event, ScriptAccessory accessory)
    {
        var pos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
        if (!P5TryParseObjectId(@event["SourceId"], out var sourceId)) return;
        var obj = accessory.Data.Objects.SearchById(sourceId);
        if (obj == null) return;
        var dataId = obj.DataId;
        if (dataId is not (2015294 or 2015295 or 2015296)) return;

        bool shouldAssign;
        int count;
        lock (_towerLock)
        {
            _towerRoundTowers.TryAdd(RoundTowerPos(pos), (TowerElement)dataId);
            count = _towerRoundTowers.Count;
            shouldAssign = count >= 4 && !_towerCooling;
            if (shouldAssign) _towerCooling = true;
        }

        if (enableDebugLogging) accessory.Method.SendChat($"/e [塔] {DateTime.Now:HH:mm:ss.fff} {TowerElementLabel((TowerElement)dataId)} ({pos.X:F1},{pos.Z:F1}) n={count}");
        if (shouldAssign) _ = Task.Run(async () => await WaitAndAssignTowerGuides(accessory));
    }

    [ScriptMethod(name: "三星踩塔易伤", eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:regex:^(2902|2903|2998)$"], userControl: false)]
    public void OnTowerDebuff(Event @event, ScriptAccessory accessory)
    {
        if (!P5TryParseObjectId(@event["TargetId"], out var targetId)) return;

        var partyIndex = accessory.Data.PartyList.IndexOf(targetId);
        if (partyIndex < 0) return;

        var element = int.Parse(@event["StatusID"]) switch
        {
            2902 => TowerElement.Fire,
            2903 => TowerElement.Ice,
            2998 => TowerElement.Thunder,
            _ => TowerElement.Fire
        };
        var expireAt = DateTime.Now.AddMilliseconds(DurationMs(@event, 20000));
        lock (_towerLock) _towerDebuffs[partyIndex] = (element, expireAt);
        if (enableDebugLogging) accessory.Method.SendChat($"/e [易伤] {TowerPlayerLabel(partyIndex)} → {TowerElementLabel(element)} until {expireAt:HH:mm:ss.fff}");
    }

    private async Task WaitAndAssignTowerGuides(ScriptAccessory accessory)
    {
        var waited = 0;
        var pollMs = Math.Max(50, TowerAssignPollMs);
        while (waited < TowerAssignMaxWaitMs)
        {
            if (CountCurrentTowerDebuffs() >= 6) break;
            await Task.Delay(pollMs);
            waited += pollMs;
        }

        if (enableDebugLogging) accessory.Method.SendChat($"/e [塔] 等待新易伤 {waited}ms count={CountCurrentTowerDebuffs()}/6");
        AssignTowerGuides(accessory);
    }

    private void AssignTowerGuides(ScriptAccessory accessory)
    {
        List<(Vector3 Pos, TowerElement Element, float Angle)> towers;
        Dictionary<int, (TowerElement Element, DateTime ExpireAt)> debuffs;
        int round;
        lock (_towerLock)
        {
            towers = _towerRoundTowers.Select(kv => (kv.Key, kv.Value, TowerAngle(kv.Key))).OrderBy(t => t.Item3).ToList();
            _towerRoundTowers.Clear();
            debuffs = _towerDebuffs.ToDictionary(kv => kv.Key, kv => kv.Value);
            round = ++_towerRound;
            _towerRawTargets.Clear();
            _towerCooling = false;
        }

        if (towers.Count != 4)
        {
            if (enableDebugLogging) accessory.Method.SendChat($"/e [塔] 第{round}轮塔数异常 {towers.Count}，跳过");
            return;
        }

        var groups = towers.GroupBy(t => t.Element).ToDictionary(g => g.Key, g => g.ToList());
        if (groups.Count != 3 || groups.Values.Count(g => g.Count == 2) != 1)
        {
            if (enableDebugLogging) accessory.Method.SendChat($"/e [塔] 第{round}轮元素分布异常 {string.Join(" ", groups.Select(g => $"{TowerElementLabel(g.Key)}x{g.Value.Count}"))}");
            return;
        }

        var doubleElement = groups.First(g => g.Value.Count == 2).Key;
        if (!_towerSeenDouble.Add(doubleElement) && enableDebugLogging) accessory.Method.SendChat($"/e [塔] 双塔元素重复：{TowerElementLabel(doubleElement)}");
        if (enableDebugLogging)
        {
            accessory.Method.SendChat($"/e ═══ 第{round}轮 塔{towers.Count} 双{TowerElementLabel(doubleElement)} ═══");
            foreach (var t in towers) accessory.Method.SendChat($"/e   {TowerElementLabel(t.Element)}塔 @({t.Pos.X:F1},{t.Pos.Z:F1}) ang={t.Angle:F2}");
        }

        var used = new HashSet<int>();
        var markIndex = 0;
        void Put(int partyIndex, Vector3 pos, string label)
        {
            if (markIndex >= TowerLabels.Length || partyIndex < 0 || partyIndex >= accessory.Data.PartyList.Count || !used.Add(partyIndex)) return;
            lock (_towerLock)
            {
                _towerRawTargets[partyIndex] = pos;
            }
            DrawTowerGuide(accessory, partyIndex, pos);
            if (enableDebugLogging) accessory.Method.SendChat($"/e   分配 {TowerPlayerLabel(partyIndex)} debuff:{(debuffs.TryGetValue(partyIndex, out var d) ? TowerElementLabel(d.Element) : "无")} → {label}");
            markIndex++;
        }

        var idlePlayers = GetTowerIdlePlayers();
        var now = DateTime.Now;
        var activeDebuffs = debuffs
            .Where(kv => !idlePlayers.Contains(kv.Key))
            .Where(kv =>
            {
                var remainMs = (kv.Value.ExpireAt - now).TotalMilliseconds;
                return remainMs >= TowerNewDebuffMinRemainMs && remainMs <= TowerNewDebuffMaxRemainMs;
            })
            .OrderBy(kv => kv.Key)
            .ToList();
        if (enableDebugLogging) accessory.Method.SendChat($"/e [塔] 第{round}轮使用新易伤 {activeDebuffs.Count}/6 remain={TowerNewDebuffMinRemainMs}-{TowerNewDebuffMaxRemainMs}ms");
        foreach (var kv in activeDebuffs)
        {
            var target = ClockwiseFirstDifferentTower(towers, kv.Value.Element);
            if (target.HasValue) Put(kv.Key, target.Value.Pos, $"顺异:{TowerElementLabel(target.Value.Element)}");
        }

        var idleTarget = CounterClockwiseFromTowerAnchor(towers, doubleElement);
        foreach (var partyIndex in idlePlayers)
        {
            if (markIndex >= TowerLabels.Length) break;
            if (!used.Contains(partyIndex)) Put(partyIndex, idleTarget.Pos, $"逆双:{TowerElementLabel(doubleElement)}");
        }

        if (enableDebugLogging) TowerSummary(accessory, towers);
    }

    private int CountCurrentTowerDebuffs()
    {
        var idlePlayers = GetTowerIdlePlayers();
        var now = DateTime.Now;
        lock (_towerLock)
        {
            return _towerDebuffs
                .Where(kv => !idlePlayers.Contains(kv.Key))
                .Count(kv =>
                {
                    var remainMs = (kv.Value.ExpireAt - now).TotalMilliseconds;
                    return remainMs >= TowerNewDebuffMinRemainMs && remainMs <= TowerNewDebuffMaxRemainMs;
                });
        }
    }

    private void DrawTowerGuide(ScriptAccessory accessory, int partyIndex, Vector3 pos)
    {
        if (!Debugging && partyIndex != accessory.MyIndex()) return;
        P5Guide(accessory, $"{TowerPrefix}_Guide", partyIndex, pos, 0, TowerGuideDurationMs);
    }

    private void LockTowerIdlePlayers(ScriptAccessory accessory, int seq)
    {
        List<int> idle;
        int debuffCount;
        lock (_towerLock)
        {
            if (seq != _towerMechanicSeq || _towerIdleLocked) return;
            var now = DateTime.Now.AddMilliseconds(3000);
            var active = _towerDebuffs.Where(kv => kv.Value.ExpireAt > now).Select(kv => kv.Key).ToHashSet();
            idle = Enumerable.Range(0, Math.Min(8, accessory.Data.PartyList.Count)).Where(i => !active.Contains(i)).OrderBy(i => i).ToList();
            debuffCount = active.Count;
            _towerIdlePlayers.Clear();
            foreach (var i in idle) _towerIdlePlayers.Add(i);
            _towerIdleLocked = true;
        }

        if (enableDebugLogging) accessory.Method.SendChat($"/e [塔] 锁定闲人：{string.Join(" ", idle.Select(TowerPlayerLabel))} debuffs={debuffCount} idle={idle.Count}");
    }

    private List<int> GetTowerIdlePlayers()
    {
        lock (_towerLock)
        {
            return _towerIdlePlayers.OrderBy(i => i).ToList();
        }
    }

    private void ResetTowerGuideState(bool advanceSeq)
    {
        lock (_towerLock)
        {
            ClearTowerGuideState(advanceSeq);
            _towerCooling = false;
            _towerIdleLocked = false;
            _towerRound = 0;
        }
    }

    private void ClearTowerGuideState(bool advanceSeq)
    {
        _towerRoundTowers.Clear();
        _towerDebuffs.Clear();
        _towerRawTargets.Clear();
        _towerSeenDouble.Clear();
        _towerIdlePlayers.Clear();
        if (advanceSeq) _towerMechanicSeq++;
    }

    private void TowerSummary(ScriptAccessory accessory, List<(Vector3 Pos, TowerElement Element, float Angle)> towers)
    {
        Dictionary<int, Vector3> raw;
        lock (_towerLock) raw = new Dictionary<int, Vector3>(_towerRawTargets);
        accessory.Method.SendChat("/e ── 踩塔汇总 ──");
        foreach (var t in towers)
        {
            var who = string.Join(" ", Enumerable.Range(0, 8).Where(i => raw.ContainsKey(i) && RoundTowerPos(raw[i]) == RoundTowerPos(t.Pos)).Select(TowerPlayerLabel));
            accessory.Method.SendChat($"/e   {TowerElementLabel(t.Element)}塔 @({t.Pos.X:F1},{t.Pos.Z:F1}) → {who}");
        }
    }

    private static (Vector3 Pos, TowerElement Element, float Angle)? ClockwiseFirstDifferentTower(List<(Vector3 Pos, TowerElement Element, float Angle)> towers, TowerElement element)
    {
        var anchors = towers.Where(t => t.Element == element).OrderBy(t => t.Angle).ToList();
        if (anchors.Count == 2 && ClockwiseTowerDelta(anchors[1].Angle, anchors[0].Angle) < ClockwiseTowerDelta(anchors[0].Angle, anchors[1].Angle))
            anchors.Reverse();
        foreach (var anchor in anchors)
        {
            var target = towers.Where(t => t.Element != element).OrderBy(t => ClockwiseTowerDelta(anchor.Angle, t.Angle)).FirstOrDefault();
            if (target != default) return target;
        }
        return null;
    }

    private static (Vector3 Pos, TowerElement Element, float Angle) CounterClockwiseFromTowerAnchor(List<(Vector3 Pos, TowerElement Element, float Angle)> towers, TowerElement element)
    {
        var anchorAngle = TowerAngle(TowerAnchorA);
        return towers.Where(t => t.Element == element).OrderBy(t => CounterClockwiseTowerDelta(anchorAngle, t.Angle)).First();
    }

    private static float ClockwiseTowerDelta(float from, float to) => (to - from + MathF.PI * 2f) % (MathF.PI * 2f);

    private static float CounterClockwiseTowerDelta(float from, float to) => (from - to + MathF.PI * 2f) % (MathF.PI * 2f);

    private static Vector3 RoundTowerPos(Vector3 pos) => new(MathF.Round(pos.X, 1), 0f, MathF.Round(pos.Z, 1));

    private static float TowerAngle(Vector3 pos)
    {
        var angle = MathF.Atan2(pos.X - P5ArenaCenter.X, P5ArenaCenter.Z - pos.Z);
        return angle < 0f ? angle + MathF.PI * 2f : angle;
    }

    private static string TowerElementLabel(TowerElement element) => element switch
    {
        TowerElement.Fire => "火",
        TowerElement.Ice => "冰",
        TowerElement.Thunder => "雷",
        _ => "?"
    };

    private static string TowerPlayerLabel(int index) => index >= 0 && index < TowerLabels.Length ? TowerLabels[index] : $"P{index}";

    #endregion

    #region 地水（矩形AOE）

    // 49539 地水：Boss 身后 10×60 矩形（中心 = Boss 位置 - 前方 30m），读条 4s 后判定
    [ScriptMethod(name: "P5 地水 (范围)", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:49539"])]
    public void OnWater(Event @event, ScriptAccessory accessory)
    {
        var pos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
        var rot = float.Parse(@event["SourceRotation"]);
        var dir = new Vector3(MathF.Sin(rot), 0f, MathF.Cos(rot));

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"{P5DrawPrefix}_Water";
        dp.Position = pos - dir * WaterBackOffset;
        dp.Rotation = rot;
        dp.Scale = new Vector2(WaterWidth, WaterLength);
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = WaterDelayMs;
        dp.DestoryAt = WaterDurationMs;
        dp.ScaleMode = ScaleMode.None;
        accessory.Method.SendDraw(DM, DrawTypeEnum.Rect, dp);
    }

    // 洪水2穿1：地水每 4 个读条为一组，取负向×正向两条矩形中线离场中最近的交点作安全点；
    // 第 1 组的点先存着，第 2 组出来后按 2 → 1 连成路线
    [ScriptMethod(name: "P5 洪水 2穿1 (安全点指路)", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:49539"])]
    public void OnFlood(Event @event, ScriptAccessory accessory)
    {
        var pos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
        var rot = float.Parse(@event["SourceRotation"]);

        // 配对读条并发进入，串行化全部洪水状态变更，杜绝丢失更新
        lock (_floodLock)
        {
        _floodCount++;

        if (enableDebugLogging)
            accessory.Method.SendChat($"/e [洪水] #{_floodCount} 读条 pos=({pos.X:F1},{pos.Z:F1}) rot={rot:F3}");

        if (_seqCount >= 4)
            _seqCount = 0;

        _floodSeqRot[_seqCount] = rot;
        _seqCount++;

        if (rot < 0f && _negCount < 2)
        {
            _floodNegPos[_negCount] = pos;
            _floodNegRot[_negCount] = rot;
            _negCount++;
        }
        else if (rot >= 0f && _posCount < 2)
        {
            _floodPosPos[_posCount] = pos;
            _floodPosRot[_posCount] = rot;
            _posCount++;
        }

        if (enableDebugLogging)
            accessory.Method.SendChat($"/e [洪水] 读条#{_floodCount} rot={rot:F3} {(rot < 0f ? "负" : "正")} seq={_seqCount} neg={_negCount} pos={_posCount}");

        if (_negCount >= 1 && _posCount >= 1 && _negCount + _posCount >= 4)
        {
            _floodGroupCount++;

            if (enableDebugLogging)
                accessory.Method.SendChat($"/e [洪水] 触发配对 groupCount={_floodGroupCount} saved1={( _savedFloodSafe1.HasValue ? "有" : "无")}");

            // 遍历所有 neg×pos 组合，取距场中最近的交点
            Vector3? best = null; float bestDist = float.MaxValue;
            int bestNegIndex = -1, bestPosIndex = -1;
            for (int ni = 0; ni < _negCount; ni++)
            for (int pi = 0; pi < _posCount; pi++)
            {
                var dn = new Vector3(MathF.Sin(_floodNegRot[ni]), 0f, MathF.Cos(_floodNegRot[ni]));
                var dpo = new Vector3(MathF.Sin(_floodPosRot[pi]), 0f, MathF.Cos(_floodPosRot[pi]));
                var pt = LineIntersection2D(_floodNegPos[ni], dn, _floodPosPos[pi], dpo);
                if (pt == null) continue;
                var d = Vector3.Distance(pt.Value, P5ArenaCenter);
                if (d < bestDist)
                {
                    best = pt.Value;
                    bestDist = d;
                    bestNegIndex = ni;
                    bestPosIndex = pi;
                }
            }

            if (best == null || bestDist > FloodMaxDist)
            {
                if (enableDebugLogging)
                    accessory.Method.SendChat($"/e [洪水] 组{_floodGroupCount} 交点距场中={bestDist:F1}m 超出阈值{FloodMaxDist:F1}m ✗");
                // 该组交点无效，回滚 groupCount，避免占用有效组编号导致 组1保存/组2绘制 配对错位
                _floodGroupCount--;
                _negCount = 0; _posCount = 0;
                _seqCount = 0;
                return;
            }
            var safePos = best.Value;

            _negCount = 0; _posCount = 0;

            // 向场中偏移4米
            var toCenter = P5ArenaCenter - safePos;
            var toCenterLen = toCenter.Length();
            if (toCenterLen > 0.001f)
                safePos += toCenter / toCenterLen * 4f;

            var midPos = TryBuildFloodMidNode(safePos, bestNegIndex, bestPosIndex, out var node) ? node : (Vector3?)null;

            if (enableDebugLogging)
            {
                accessory.Method.SendChat($"/e [洪水] 组{_floodGroupCount} 交点=({safePos.X:F1},{safePos.Z:F1}) 距场中={bestDist:F1}m ✓");
                if (midPos.HasValue)
                    accessory.Method.SendChat($"/e [洪水] 离散中间节点=({midPos.Value.X:F1},{midPos.Value.Z:F1})");
            }

            if (_floodGroupCount == 1)
            {
                _savedFloodSafe1 = safePos;
                _savedFloodMid1 = midPos;
            }
            else if (_floodGroupCount == 2)
            {
                if (_savedFloodSafe1.HasValue)
                {
                    var routeMid = _savedFloodMid1 ?? BuildFloodRouteMidFallback(safePos, _savedFloodSafe1.Value);
                    DrawFloodRoute(accessory, safePos, routeMid, _savedFloodSafe1.Value);
                }
                else
                {
                    DrawFloodLeg(accessory, "To2", safePos, null, Flood2DelayMs, Flood2DelayMs + Flood2DurationMs);
                }

                _savedFloodSafe1 = null;
                _savedFloodMid1 = null;
            }

            _seqCount = 0;
        }
        } // lock (_floodLock)
    }

    private bool TryBuildFloodMidNode(Vector3 safePos, int bestNegIndex, int bestPosIndex, out Vector3 node)
    {
        node = default;
        if (_seqCount < 1 || bestNegIndex < 0 || bestPosIndex < 0) return false;

        var radius = Vector3.Distance(safePos, P5ArenaCenter);
        if (radius < 0.001f) return false;

        var firstWaveIsNeg = _floodSeqRot[0] < 0f;
        var source = firstWaveIsNeg ? _floodNegPos[bestNegIndex] : _floodPosPos[bestPosIndex];
        var delta = safePos - P5ArenaCenter;

        if (MathF.Abs(delta.X) >= MathF.Abs(delta.Z))
        {
            var north = new Vector3(P5ArenaCenter.X, 0f, P5ArenaCenter.Z - radius);
            var south = new Vector3(P5ArenaCenter.X, 0f, P5ArenaCenter.Z + radius);
            node = Vector3.Distance(source, north) <= Vector3.Distance(source, south) ? north : south;
        }
        else
        {
            var west = new Vector3(P5ArenaCenter.X - radius, 0f, P5ArenaCenter.Z);
            var east = new Vector3(P5ArenaCenter.X + radius, 0f, P5ArenaCenter.Z);
            node = Vector3.Distance(source, west) <= Vector3.Distance(source, east) ? west : east;
        }

        return true;
    }

    private static Vector3 BuildFloodRouteMidFallback(Vector3 from, Vector3 to)
    {
        var fromDelta = from - P5ArenaCenter;
        var toDelta = to - P5ArenaCenter;
        var guideDelta = fromDelta + toDelta;
        if (guideDelta.Length() < 0.001f)
            guideDelta = toDelta.Length() >= fromDelta.Length() ? toDelta : fromDelta;

        var radius = MathF.Max(fromDelta.Length(), toDelta.Length());
        if (radius < 0.001f)
            return P5ArenaCenter;

        if (MathF.Abs(guideDelta.X) >= MathF.Abs(guideDelta.Z))
        {
            var north = new Vector3(P5ArenaCenter.X, 0f, P5ArenaCenter.Z - radius);
            var south = new Vector3(P5ArenaCenter.X, 0f, P5ArenaCenter.Z + radius);
            return Vector3.Distance(from, north) <= Vector3.Distance(from, south) ? north : south;
        }

        var west = new Vector3(P5ArenaCenter.X - radius, 0f, P5ArenaCenter.Z);
        var east = new Vector3(P5ArenaCenter.X + radius, 0f, P5ArenaCenter.Z);
        return Vector3.Distance(from, west) <= Vector3.Distance(from, east) ? west : east;
    }

    // 2 → 中转 → 1：绿线指当前该去的点，白线预览下一段
    private void DrawFloodRoute(ScriptAccessory a, Vector3 safe2, Vector3 mid, Vector3 safe1)
    {
        var firstResolve = Math.Max(Flood2DelayMs, Flood1DelayMs);
        var endAt = Math.Max(Flood2DelayMs + Flood2DurationMs, Flood1DelayMs + Flood1DurationMs);
        var secondResolve = Math.Min(endAt, firstResolve + Math.Max(1, Flood1DurationMs / 3));

        DrawFloodLeg(a, "To2", safe2, mid, Flood2DelayMs, firstResolve);
        DrawFloodLeg(a, "ToMid", mid, safe1, firstResolve, secondResolve);
        DrawFloodLeg(a, "To1", safe1, null, secondResolve, endAt);
    }

    private void DrawFloodLeg(ScriptAccessory a, string suffix, Vector3 target, Vector3? next, int startMs, int endMs)
    {
        var name = $"{P5DrawPrefix}_Flood{suffix}";
        foreach (var i in 指路对象(a))
            P5Guide(a, name, i, target, startMs, endMs - startMs);
        if (next.HasValue)
            P5Preview(a, $"{name}_Preview", target, next.Value, startMs, endMs - startMs);
    }

    #endregion

    #region Helpers

    // 指路风格跟 P1~P3 一致：当前要去的点 = 绿色箭头（WaypointDp，默认安全色），下一段 = 白色预览线
    private static readonly Vector4 P5PreviewColor = new(1, 1, 1, 1);

    private static void P5Guide(ScriptAccessory sa, string name, int partyIndex, Vector3 target, int delayMs, int durationMs)
    {
        if (durationMs <= 0) return;
        var dp = sa.WaypointDp(target, (uint)durationMs, (uint)Math.Max(0, delayMs), $"{name}_{partyIndex}");
        dp.Owner = GuidanceOwner(sa, partyIndex);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    private static void P5Preview(ScriptAccessory sa, string name, Vector3 from, Vector3 to, int delayMs, int durationMs)
    {
        if (durationMs <= 0) return;
        var dp = sa.WaypointFromToDp(from, to, (uint)durationMs, (uint)Math.Max(0, delayMs), name, P5PreviewColor);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    private static Vector3 RotationToDirection(float radians)
    {
        return new Vector3(MathF.Sin(radians), 0f, MathF.Cos(radians));
    }

    private static int PositiveMod(int value, int mod)
    {
        return (value % mod + mod) % mod;
    }

    private static bool P5TryParseObjectId(string str, out uint id)
    {
        id = 0;
        if (string.IsNullOrEmpty(str)) return false;
        str = str.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? str[2..] : str;
        return uint.TryParse(str, System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture, out id);
    }

    private static Vector3? LineIntersection2D(Vector3 p1, Vector3 d1, Vector3 p2, Vector3 d2)
    {
        var crossD = d1.X * d2.Z - d1.Z * d2.X;
        if (MathF.Abs(crossD) < 0.0001f) return null;
        var delta = p2 - p1;
        var t = (delta.X * d2.Z - delta.Z * d2.X) / crossD;
        return new Vector3(p1.X + t * d1.X, 0f, p1.Z + t * d1.Z);
    }

    private static Vector3 ClampToDistance(Vector3 p, Vector3 c, float maxD)
    {
        var d = p - c;
        var len = d.Length();
        if (len < 0.001f || len <= maxD) return p;
        return c + d / len * maxD;
    }

    private Vector3 FloodFallback(Vector3 p1, Vector3 p2)
    {
        var mid = (p1 + p2) * 0.5f;
        var dir = mid - P5ArenaCenter;
        if (dir.Length() < 0.001f) dir = new Vector3(1f, 0f, 0f);
        else dir = Vector3.Normalize(dir);
        return P5ArenaCenter + dir * FloodSafeDistance;
    }

    private static int DurationMs(Event @event, int fallback = 5000)
    {
        return int.TryParse(@event["DurationMilliseconds"], out var d) && d > 0 ? d : fallback;
    }

    #endregion

    #endregion

    #region Helpers（Cicero 灵视）

    #region Commons

    public static bool convertObjectIdToDecimal(string? rawObjectId,out ulong result) {

        result=0;

        if(string.IsNullOrWhiteSpace(rawObjectId)) {

            return false;

        }

        string objectId=rawObjectId.Trim();

        objectId=objectId.StartsWith("0x",StringComparison.OrdinalIgnoreCase)?objectId.Substring(2):objectId;

        return ulong.TryParse(objectId,System.Globalization.NumberStyles.HexNumber,null,out result);

    }

    public static bool convertHandleIdToDecimal(string? rawHandle,out nint result) {

        result=0;

        if(string.IsNullOrWhiteSpace(rawHandle)) {

            return false;

        }

        string handleId=rawHandle.Trim();

        handleId=handleId.StartsWith("0x",StringComparison.OrdinalIgnoreCase)?handleId.Substring(2):handleId;

        return nint.TryParse(handleId,System.Globalization.NumberStyles.HexNumber,null,out result);

    }

    public static bool convertStringToSignedInteger(string? rawString,out int result) {

        result=0;

        if(string.IsNullOrWhiteSpace(rawString)) {

            return false;

        }

        string cleanString=rawString.Trim();

        return int.TryParse(cleanString,System.Globalization.NumberStyles.Integer,null,out result);

    }

    public static int discretizePosition(Vector3 position,Vector3 center,int numberOfDirections,bool diagonalSplit=true) {

        if(diagonalSplit) {

            return (int)(

                (Math.Round(

                    (numberOfDirections/2.0d)-(numberOfDirections/2.0d)*Math.Atan2(position.X-center.X,position.Z-center.Z)/Math.PI

                )%numberOfDirections+numberOfDirections)%numberOfDirections

            );

        }

        else {

            return (int)(

                (Math.Floor(

                    (numberOfDirections/2.0d)-(numberOfDirections/2.0d)*Math.Atan2(position.X-center.X,position.Z-center.Z)/Math.PI

                )%numberOfDirections+numberOfDirections)%numberOfDirections

            );

        }

    }

    public static double getRotation(Vector3 position,Vector3 center) {

        return (position.Equals(center))?
            (0):
            ((Math.PI-Math.Atan2(position.X-center.X,position.Z-center.Z)+2*Math.PI)%(2*Math.PI));

    }

    public static double getRotationDifference(Vector3 position1,Vector3 position2,Vector3 center) {

        double rawDifference=(getRotation(position2,center)-getRotation(position1,center)+2*Math.PI)%(2*Math.PI);

        return (rawDifference<=Math.PI)?(rawDifference):(rawDifference-2*Math.PI);

    }

    public static Vector3 rotatePosition(Vector3 position,Vector3 center,double radian,bool preserveHeight=true) {

        Vector2 positionInVector2=new Vector2(position.X-center.X,position.Z-center.Z);
        double polarAngleAfterRotation=Math.PI-Math.Atan2(positionInVector2.X,positionInVector2.Y)+radian;

        return new Vector3((float)(center.X+Math.Sin(polarAngleAfterRotation)*positionInVector2.Length()),
            ((preserveHeight)?(position.Y):(center.Y)),
            (float)(center.Z-Math.Cos(polarAngleAfterRotation)*positionInVector2.Length()));

    }

    public static double convertPolarToCartesian(double polarRotation) {

        return Math.PI-polarRotation;

    }

    public static double convertDegreesToRadians(double degree) {

        return degree*Math.PI/180.0;

    }

    public static bool isLegalPartyIndex(int partyIndex) {

        return (0<=partyIndex&&partyIndex<=7);

    }

    public static bool isSupporter(int partyIndex) {

        return partyIndex switch {

            0 => true,
            1 => true,
            2 => true,
            3 => true,
            _ => false

        };

    }

    public static bool isDps(int partyIndex) {

        return partyIndex switch {

            4 => true,
            5 => true,
            6 => true,
            7 => true,
            _ => false

        };

    }

    public static bool isMelee(int partyIndex) {

        return partyIndex switch {

            0 => true,
            1 => true,
            4 => true,
            5 => true,
            _ => false

        };

    }

    public static bool isRanged(int partyIndex) {

        return partyIndex switch {

            2 => true,
            3 => true,
            6 => true,
            7 => true,
            _ => false

        };

    }

    public static bool isTank(int partyIndex) {

        return isSupporter(partyIndex)&&isMelee(partyIndex);

    }

    public static bool isHealer(int partyIndex) {

        return isSupporter(partyIndex)&&isRanged(partyIndex);

    }

    public static bool isMeleeDps(int partyIndex) {

        return isDps(partyIndex)&&isMelee(partyIndex);

    }

    public static bool isRangedDps(int partyIndex) {

        return isDps(partyIndex)&&isRanged(partyIndex);

    }

    public static bool isInGroup1(int partyIndex) {

        return partyIndex switch {

            0 => true,
            2 => true,
            4 => true,
            6 => true,
            _ => false

        };

    }

    public static bool isInGroup2(int partyIndex) {

        return partyIndex switch {

            1 => true,
            3 => true,
            5 => true,
            7 => true,
            _ => false

        };

    }

    #endregion

    #region Unsafe_Commons

    public static unsafe void adjustVisibility(ScriptAccessory accessory,IGameObject? targetIGameObject,bool isVisible,int recoveryDelay=-1) {

        if(targetIGameObject==null||!targetIGameObject.IsValid()) {

            return;

        }

        try {

            var targetGameObject=((GameObject*)(targetIGameObject.Address));
            var originalVisibility=targetGameObject->RenderFlags;

            if(isVisible) {

                targetGameObject->RenderFlags=VisibilityFlags.None;

            }

            else {

                targetGameObject->RenderFlags=VisibilityFlags.Model;

            }

            if(recoveryDelay<=0) {

                return;

            }

            System.Threading.Tasks.Task.Delay(recoveryDelay).ContinueWith(_=> {

                if(targetIGameObject==null||!targetIGameObject.IsValid()) {

                    return;

                }

                try {

                    var targetGameObject=((GameObject*)(targetIGameObject.Address));

                    targetGameObject->RenderFlags=originalVisibility;

                } catch(Exception e) {

                    accessory.Log.Error(e.ToString());

                }

            });

        } catch(Exception e) {

            accessory.Log.Error(e.ToString());

        }

    }

    #endregion

    #endregion

}

#region Helpers（Coda）

public static class EventExtensions
{
    public static float SourceRotation(this Event evt)
        => JsonConvert.DeserializeObject<float>(evt["SourceRotation"]);

    private static bool ParseHexId(string? idStr, out uint id)
    {
        id = 0;

        if (string.IsNullOrEmpty(idStr))
            return false;

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

    private static bool TryParseUIntAny(string? raw, out uint id)
    {
        id = 0;

        if (string.IsNullOrWhiteSpace(raw))
            return false;

        raw = raw.Trim();

        if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return uint.TryParse(
                raw[2..],
                System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture,
                out id
            );
        }

        if (raw.Any(c => c is >= 'A' and <= 'F' or >= 'a' and <= 'f'))
        {
            return uint.TryParse(
                raw,
                System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture,
                out id
            );
        }

        return uint.TryParse(
            raw,
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out id
        );
    }

    public static uint ActionId(this Event evt)
        => JsonConvert.DeserializeObject<uint>(evt["ActionId"]);

    public static uint DataId(this Event evt)
        => JsonConvert.DeserializeObject<uint>(evt["DataId"]);

    public static uint StatusId(this Event evt)
    {
        if (TryParseUIntAny(evt["StatusID"], out var id))
            return id;

        if (TryParseUIntAny(evt["StatusId"], out id))
            return id;

        if (TryParseUIntAny(evt["ActionId"], out id))
            return id;

        return 0;
    }

    public static double Duration(this Event evt)
    {
        return double.TryParse(
            evt["Duration"],
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var duration
        )
            ? duration
            : 0.0;
    }

    public static uint SourceId(this Event evt)
        => ParseHexId(evt["SourceId"], out var id) ? id : 0;

    public static uint TargetId(this Event evt)
        => ParseHexId(evt["TargetId"], out var id) ? id : 0;

    public static uint IconId(this Event evt)
    {
        var raw = evt["Id"];

        if (string.IsNullOrWhiteSpace(raw))
            return 0;

        raw = raw.Trim();

        if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            raw = raw[2..];

        return uint.TryParse(
            raw,
            System.Globalization.NumberStyles.HexNumber,
            null,
            out var result
        )
            ? result
            : 0;
    }

    public static Vector3 SourcePosition(this Event evt)
        => ParseVector3(evt["SourcePosition"]);

    public static Vector3 EffectPosition(this Event evt)
        => ParseVector3(evt["EffectPosition"]);

    public static Vector3 TargetPosition(this Event evt)
        => ParseVector3(evt["TargetPosition"]);

    public static uint DirectorId(this Event evt)
        => ParseHexId(evt["DirectorId"], out var id) ? id : 0;

    private static Vector3 ParseVector3(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Vector3.Zero;

        raw = raw.Trim();

        try
        {
            return JsonConvert.DeserializeObject<Vector3>(raw);
        }
        catch
        {
        }

        try
        {
            float x = ExtractFloat(raw, "X");
            float y = ExtractFloat(raw, "Y");
            float z = ExtractFloat(raw, "Z");
            return new Vector3(x, y, z);
        }
        catch
        {
            return Vector3.Zero;
        }
    }

    private static float ExtractFloat(string raw, string key)
    {
        var pattern = $@"{key}\s*[:=]\s*(-?\d+(?:[\.,]\d+)?)";
        var match = System.Text.RegularExpressions.Regex.Match(
            raw,
            pattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        if (!match.Success)
            return 0f;

        var value = match.Groups[1].Value.Replace(',', '.');

        return float.Parse(
            value,
            System.Globalization.CultureInfo.InvariantCulture
        );
    }
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

    public static DrawPropertiesEdit WaypointDp(
        this ScriptAccessory sa,
        Vector3 target,
        uint duration,
        uint delay = 0,
        string name = "Waypoint",
        Vector4? color = null)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? sa.Data.DefaultSafeColor;
        dp.Owner = sa.Data.Me;
        dp.TargetPosition = target;
        dp.DestoryAt = duration;
        dp.Delay = delay;
        dp.Scale = new Vector2(2);
        dp.ScaleMode = ScaleMode.YByDistance;
        return dp;
    }

    public static DrawPropertiesEdit WaypointFromToDp(
        this ScriptAccessory sa,
        Vector3 from,
        Vector3 to,
        uint duration,
        uint delay = 0,
        string name = "WaypointFromTo",
        Vector4? color = null)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? sa.Data.DefaultSafeColor;
        dp.Position = from;
        dp.Owner = 0;
        dp.TargetPosition = to;
        dp.DestoryAt = duration;
        dp.Delay = delay;
        dp.Scale = new Vector2(2);
        dp.ScaleMode = ScaleMode.YByDistance;
        return dp;
    }

    public static void Debug(this ScriptAccessory sa, object message)
    {
        if (!Dancing_Mad_Ultimate.DebugEnabled)
            return;

        sa.Method.SendChat($"/e [Kefka] {message}");
    }
}

#endregion

#region Helpers（Cicero 灵视）

#region Extensions

public static class CiceroScriptAccessoryExtensions
{

    public static void tts(this ScriptAccessory accessory,string text,bool enableVanillaTts,bool enableDailyRoutinesTts) {

        if(enableVanillaTts) {

            accessory.Method.TTS(text);

        }

        else {

            if(enableDailyRoutinesTts) {

                accessory.Method.SendChat($"/pdr tts {text}");

            }

        }

    }

}

#endregion

#endregion

#region Helpers（Usami）

#region 优先级字典
internal class PriorityDict
{
    // ReSharper disable once NullableWarningSuppressionIsUsed
    public Dictionary<int, int> Priorities {get; set;} = null!;
    public string Annotation { get; set; } = "";
    public int ActionCount { get; set; } = 0;

    private static readonly List<string> Role = ["MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4"];

    public void Init(ScriptAccessory sa, string annotation, int partyNum = 8, bool refreshActionCount = true)
    {
        Priorities = new Dictionary<int, int>();
        for (var i = 0; i < partyNum; i++)
        {
            Priorities.Add(i, 0);
        }
        Annotation = annotation;
        if (refreshActionCount)
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
        var sortedPriorities = SelectMiddlePriorityIndices(0, Priorities.Count, descending);
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
        var sortedPriorities = SelectMiddlePriorityIndices(0, Priorities.Count, descending);
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
            str += $"Key {pair.Key} {(showJob ? $"({Role[pair.Key]})" : "")}, Value {pair.Value}\n";
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

    public string ShowGroup(string name, List<KeyValuePair<int, int>> kvpList)
    {
        return $"{name}：{string.Join(" ", kvpList.Select(x => $"({Role[x.Key]}, {x.Value})"))}";
    }
}

#endregion 优先级字典 类

#region 参数容器类
internal class UDMP3Params
{
    public bool Debugging = true;
    public int 当前阶段 = 0;
    public ulong ObjectId_卡奥斯 = 0;
    public ulong ObjectId_艾克斯迪司 = 0;
    public bool 拉艾克斯迪司的是MT = false;
    public string 判断防火墙目标Framework = "";
    public int 上一次防火墙状态 = 0;
    public bool 防火墙已开启 = false;
    public bool 经纬聚爆先打左右 = false;
    public bool 绘制经纬聚爆范围 = false;
    public bool 经纬聚爆第二段绘制完毕 = false;

    // ---- 一运 ----
    public bool 是长火 = false;
    public int 一运状态记录次数 = 0;
    public int 火水晶方位 = -1;
    public int 水水晶方位 = -1;
    public int 风水晶方位 = -1;
    public int 无水晶方位 = -1;

    public List<KeyValuePair<int, int>> 火组 = new();
    public List<KeyValuePair<int, int>> 水组 = new();
    public ManualResetEvent 一运状态记录 = new(false);
    public ManualResetEvent 一运水晶记录 = new(false);
    public ManualResetEvent 一水火准备 = new(false);

    public int 一水火指路与绘图时间 = 10000;
    public int 二水火指路与绘图时间 = 9000;
    public int 真空波指路时间 = 7000;

    // ---- 二运 ----
    public int 黑洞轮数 = 0;
    public int 黑洞喷射轮数 = 0;
    public bool 玩家正在接线 = false;
    public Dictionary<ulong, (int round, int region, int relativeRegion, bool hasTether)> 黑洞字典 = new();
    public ManualResetEvent 外黑洞获取完毕 = new(false);
    public ManualResetEvent 黑洞轮数增加完毕 = new(false);
    public ManualResetEvent 凯夫卡方位获取完毕 = new(false);
    public int 凯夫卡方位 = 0;

    public int 冰封北方位 = -1;
    public int 西塔方位 = -1;
    public int 东塔方位 = -1;
    public bool 分摊点TN = false;
    public bool 绘制冰封引导路径 = false;
    public bool 是第一次分摊 = false;
    public Vector3 第二枚黄圈引导位置 = new Vector3(100, 0, 100);
    public Vector3 第一次轰击位置 = new Vector3(0, 0, 0);
    public bool 黑洞已出现 = false;
    public int[] 接线顺位 = new int[8];   // 第一/二/三目标 = 1/2/3
    public bool[] 有泥 = new bool[8];

    public List<(int playerIdx, List<int> realRegions, List<ulong> blackHoles, string name)> 当前黑洞接线任务 = [];
    public ManualResetEvent 黑洞接线任务准备 = new(false);
    public int 黑洞接线任务版本 = 0;
    public int 黑洞接线范围绘图已消费版本 = 0;
    public int 黑洞接线TTS已消费版本 = 0;
    public int 黑洞接线指路已消费版本 = 0;
    public List<接线Framework元素> 接线FrameworkList = [];
    public string 当前接线任务Framework = "";

    public void Reset(ScriptAccessory sa)
    {
        当前阶段 = 0;

        // 一运
        是长火 = false;
        一运状态记录次数 = 0;
        火水晶方位 = -1;
        水水晶方位 = -1;
        风水晶方位 = -1;
        无水晶方位 = -1;
        拉艾克斯迪司的是MT = false;
        sa.Method.UnregistFrameworkUpdateAction(判断防火墙目标Framework);
        判断防火墙目标Framework = "";
        上一次防火墙状态 = 0;
        防火墙已开启 = false;
        经纬聚爆先打左右 = false;
        绘制经纬聚爆范围 = false;
        经纬聚爆第二段绘制完毕 = false;

        ObjectId_卡奥斯 = 0;
        ObjectId_艾克斯迪司 = 0;

        一运状态记录 = new(false);
        一运水晶记录 = new(false);
        一水火准备 = new(false);

        火组 = new();
        水组 = new();

        一水火指路与绘图时间 = 10000;
        二水火指路与绘图时间 = 9000;
        真空波指路时间 = 7000;

        // 二运
        黑洞轮数 = 0;
        黑洞喷射轮数 = 0;
        玩家正在接线 = false;
        黑洞字典 = new Dictionary<ulong, (int round, int region, int relativeRegion, bool hasTether)>();
        外黑洞获取完毕 = new(false);
        黑洞轮数增加完毕 = new(false);
        凯夫卡方位获取完毕 = new(false);
        凯夫卡方位 = 0;

        冰封北方位 = -1;
        西塔方位 = -1;
        东塔方位 = -1;
        分摊点TN = false;
        绘制冰封引导路径 = false;
        是第一次分摊 = false;
        第二枚黄圈引导位置 = new Vector3(100, 0, 100);
        第一次轰击位置 = new Vector3(0, 0, 0);
        黑洞已出现 = false;
        接线顺位 = new int[8];
        有泥 = new bool[8];

        当前黑洞接线任务 = [];
        黑洞接线任务准备 = new(false);
        黑洞接线任务版本 = 0;
        黑洞接线范围绘图已消费版本 = 0;
        黑洞接线TTS已消费版本 = 0;
        黑洞接线指路已消费版本 = 0;
        foreach (var fw in 接线FrameworkList)
        {
            if (fw.FwHandle != "")
                sa.Method.UnregistFrameworkUpdateAction(fw.FwHandle);
        }
        接线FrameworkList.Clear();
        if (当前接线任务Framework != "")
            sa.Method.UnregistFrameworkUpdateAction(当前接线任务Framework);
        当前接线任务Framework = "";

        sa.DebugMsg($"绝妖星乱舞 P3 参数重置");
    }
}

internal static class P3AExtension
{
    public static bool 一运状态记录完毕(this UDMP3Params prm) =>
        prm.一运状态记录次数 == 12;

    public static bool 水晶状态记录完毕(this UDMP3Params prm) =>
        prm is { 火水晶方位: >= 0, 水水晶方位: >= 0, 风水晶方位: >= 0 };

    public static void 成员分组(this UDMP3Params prm, ScriptAccessory sa, PriorityDict pd)
    {
        prm.火组.Clear();
        prm.水组.Clear();

        // 优先级 0~3 是只有风的人，4~5 火，6~7 水
        prm.火组.Add(pd.SelectSpecificPriorityIndex(4));
        prm.火组.Add(pd.SelectSpecificPriorityIndex(5));
        prm.水组.Add(pd.SelectSpecificPriorityIndex(6));
        prm.水组.Add(pd.SelectSpecificPriorityIndex(7));

        // 再按 KVP.value 的个位数大小进行 sort，小的在前
        prm.火组.Sort((a, b) => a.Value.GetDecimalDigit(0).CompareTo(b.Value.GetDecimalDigit(0)));
        prm.水组.Sort((a, b) => a.Value.GetDecimalDigit(0).CompareTo(b.Value.GetDecimalDigit(0)));
    }

    public static bool 当前轮为火(this UDMP3Params prm)
        // 第一轮 XOR 是长火
        => prm.当前阶段 == 3100 ^ prm.是长火;

    public static bool 需要背对(this UDMP3Params prm, int pdValue)
        // 混沌之风，背对击退距离减半
        => pdValue.GetDecimalDigit(1) == 1;

    public static Vector3 基于水晶旋转(this UDMP3Params prm, Vector3 basePos, int crystalRegion)
    {
        var center = new Vector3(100, 0, 100);
        return basePos.RotateAndExtend(center, (crystalRegion * 90f).DegToRad());
    }

    public static int 查询组序(this UDMP3Params prm, List<KeyValuePair<int, int>> group, int myIndex)
        => group.FindIndex(kvp => kvp.Key == myIndex);
}

internal static class P3BExtension
{

    public static int 巨大凯夫卡方位获取(this UDMP3Params prm, ScriptAccessory sa, bool cwFromA = true)
    {
        var obj = sa.GetByDataId(19504u).FirstOrDefault();
        if (obj == null)
        {
            sa.DebugMsg($"获取失败");
            return 0;
        }

        // ? A顺计数 : C逆计数
        var region = cwFromA
            ? (obj.Rotation.RadianToRegion(8, 4, isDiagDiv: true, isCw: true) + 4) % 8
            : (obj.Rotation.RadianToRegion(8, isDiagDiv: true) + 4) % 8;

        // prm.凯夫卡方位 = region;
        sa.DebugMsg($"凯夫卡方位 {region} {(cwFromA ? "A顺" : "C逆")}");
        return region;
    }

    public static (int attackRegion, int safeRegion) 巨大凯夫卡半场刀方向(this UDMP3Params prm, ScriptAccessory sa, bool isRightHand)
    {
        // A顺，
        var bossRegion = prm.凯夫卡方位;
        var attackRegion = (bossRegion + 8 + (isRightHand ? -2 : 2)) % 8;
        var safeRegion = (bossRegion + 8 + (isRightHand ? 2 : -2)) % 8;
        sa.DebugMsg($"攻击 {attackRegion} 安全 {safeRegion} A顺");
        return (attackRegion, safeRegion);
    }

    public static Vector3 基于凯夫卡旋转(this UDMP3Params prm, Vector3 basePos, int bossRegion)
    {
        var center = new Vector3(100, 0, 100);
        return basePos.RotateAndExtend(center, (bossRegion * 45f).DegToRad());
    }

    public static bool 需要踩塔(this UDMP3Params prm, int myIndex)
    {
        // 分摊TN 我是TN 我分摊 false
        // 分摊TN 我是D  我踩塔 true
        // 分摊D  我是TN 我踩塔 true
        // 分摊D  我是TN 我分摊 false
        return prm.分摊点TN ^ (myIndex <= 3);
    }
}

internal class 接线Framework元素
{
    public string Name { get; init; } = "";
    public string TaskName { get; init; } = "";
    public int PlayerIndex { get; init; }
    public string FwHandle { get; set; } = "";
    public bool Tethered { get; set; } = false;
    public ulong BlackHoleId { get; init; }
    public ulong PlayerObjId { get; init; }
    public Vector3 GuidePos { get; init; }
    public bool 去接线指路已绘制 { get; set; } = false;
    public ulong 上一次接线目标 { get; set; } = 0;
    public string 上一次去接线指路绘图名 { get; set; } = "";
    public string 上一次接线目标列表 { get; set; } = "";
    public bool 上一次黑洞存在状态 { get; set; } = true;
    public string 去接线指路绘图名前缀 => $"绘制黑洞接线 {TaskName} G去接线 {Name}";
}

#endregion 参数容器类

#region 函数集
internal static class UsamiEventExtensions
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

    public static bool TryParseVfxHandle(this Event ev, out nint handleId)
    {
        handleId = 0;
        var rawHandle = ev["Handle"];
        if (string.IsNullOrWhiteSpace(rawHandle))
            return false;

        var handleText = rawHandle.Trim();
        if (handleText.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            handleText = handleText[2..];

        return nint.TryParse(handleText, System.Globalization.NumberStyles.HexNumber, null, out handleId);
    }

    public static uint Id0(this Event ev)
    {
        return ParseHexId(ev["Id"], out var id) ? id : 0;
    }

    public static uint DurationMilliseconds(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["DurationMilliseconds"]);
    }
}

internal static class IbcHelper
{
    public static IGameObject? GetById(this ScriptAccessory sa, ulong gameObjectId)
    {
        return sa.Data.Objects.SearchById(gameObjectId);
    }
    public static IEnumerable<IGameObject?> GetByDataId(this ScriptAccessory sa, uint dataId)
    {
        return sa.Data.Objects.Where(x => x.DataId == dataId);
    }
    public static List<ulong> GetTetherSource(this ScriptAccessory sa, IBattleChara? battleChara, uint tetherId)
    {
        List<ulong> tetherSourceId = [];
        if (battleChara == null || !battleChara.IsValid()) return [];
        unsafe
        {
            BattleChara* chara = (BattleChara*)battleChara.Address;
            var tetherList = chara->Vfx.Tethers;

            foreach (var tether in tetherList)
            {
                if (tether.Id != tetherId) continue;
                tetherSourceId.Add(tether.TargetId.ObjectId);
            }
        }
        return tetherSourceId;
    }
}
#region 计算函数

internal static class MathTools
{
    public static float DegToRad(this float deg) => (deg + 360f) % 360f / 180f * float.Pi;
    public static float RadToDeg(this float rad) => (rad + 2 * float.Pi) % (2 * float.Pi) / float.Pi * 180f;

    /// <summary>
    /// 获得任意点与中心点的弧度值，以(0, 0, 1)方向为0，以(1, 0, 0)方向为pi/2。
    /// 即，逆时针方向增加。
    /// </summary>
    /// <param name="point">任意点</param>
    /// <param name="center">中心点</param>
    /// <returns></returns>
    public static float GetRadian(this Vector3 point, Vector3 center)
        => MathF.Atan2(point.X - center.X, point.Z - center.Z);

    /// <summary>
    /// 获得任意点与中心点的长度。
    /// </summary>
    /// <param name="point">任意点</param>
    /// <param name="center">中心点</param>
    /// <returns></returns>
    public static float GetLength(this Vector3 point, Vector3 center)
        => new Vector2(point.X - center.X, point.Z - center.Z).Length();

    /// <summary>
    /// 将任意点以中心点为圆心，逆时针旋转并延长。
    /// </summary>
    /// <param name="point">任意点</param>
    /// <param name="center">中心点</param>
    /// <param name="radian">旋转弧度</param>
    /// <param name="length">基于该点延伸长度</param>
    /// <returns></returns>
    public static Vector3 RotateAndExtend(this Vector3 point, Vector3 center, float radian, float length = 0)
    {
        var baseRad = point.GetRadian(center);
        var baseLength = point.GetLength(center);
        var rotRad = baseRad + radian;
        return new Vector3(
            center.X + MathF.Sin(rotRad) * (length + baseLength),
            center.Y,
            center.Z + MathF.Cos(rotRad) * (length + baseLength)
        );
    }

    /// <summary>
    /// 获得某角度所在划分区域
    /// </summary>
    /// <param name="radian">输入弧度</param>
    /// <param name="regionNum">区域划分数量</param>
    /// <param name="baseRegionIdx">0度所在区域的初始Idx</param>>
    /// <param name="isDiagDiv">是否为斜分割，默认为false</param>
    /// <param name="isCw">是否顺时针增加，默认为false</param>
    /// <returns></returns>
    public static int RadianToRegion(this float radian, int regionNum, int baseRegionIdx = 0, bool isDiagDiv = false, bool isCw = false)
    {
        var sepRad = float.Pi * 2 / regionNum;
        var inputAngle = radian * (isCw ? -1 : 1) + (isDiagDiv ? sepRad / 2 : 0);
        var rad = (inputAngle + 4 * float.Pi) % (2 * float.Pi);
        return ((int)Math.Floor(rad / sepRad) + baseRegionIdx + regionNum) % regionNum;
    }

    /// <summary>
    /// 将输入点左右折叠
    /// </summary>
    /// <param name="point">待折叠点</param>
    /// <param name="centerX">中心折线坐标点</param>
    /// <returns></returns>
    public static Vector3 FoldPointHorizon(this Vector3 point, float centerX)
        => point with { X = 2 * centerX - point.X };

    /// <summary>
    /// 将输入点上下折叠
    /// </summary>
    /// <param name="point">待折叠点</param>
    /// <param name="centerZ">中心折线坐标点</param>
    /// <returns></returns>
    public static Vector3 FoldPointVertical(this Vector3 point, float centerZ)
        => point with { Z = 2 * centerZ - point.Z };

    /// <summary>
    /// 将输入点中心对称
    /// </summary>
    /// <param name="point">输入点</param>
    /// <param name="center">中心点</param>
    /// <returns></returns>
    public static Vector3 PointCenterSymmetry(this Vector3 point, Vector3 center)
        => point.RotateAndExtend(center, float.Pi, 0);

    /// <summary>
    /// 获取给定整数的指定位数
    /// </summary>
    /// <param name="val">给定数值</param>
    /// <param name="x">对应位数，个位为0</param>
    /// <returns>返回指定位的数字，如果x超出范围返回0</returns>
    public static int GetDecimalDigit(this int val, int x)
        => (int)(Math.Abs(val) / Math.Pow(10, x) % 10);

    /// <summary>
    /// 获取整数的指定二进制位值
    /// </summary>
    /// <param name="val">给定数值</param>
    /// <param name="bitPosition">二进制位位置，从最低位开始，最低位为0</param>
    /// <returns>返回指定位的值：0 或 1</returns>
    public static int GetBinaryBit(this int val, int bitPosition)
        => (val >> bitPosition) & 1;

    /// <summary>
    /// 获得两个弧度（rad到radReference）的差值，逆时针增加大于0
    /// </summary>
    /// <param name="rad">取值角度</param>
    /// <param name="radReference">参考角度</param>
    /// <returns></returns>
    public static float GetDiffRad(this float rad, float radReference)
    {
        var diff = (rad - radReference + 4 * float.Pi) % (2 * float.Pi);
        if (diff > float.Pi) diff -= 2 * float.Pi;
        return diff;
    }
}

#endregion 计算函数

#region 位置序列函数
internal static class IndexHelper
{
    /// <summary>
    /// 输入玩家dataId，获得对应的位置index
    /// </summary>
    /// <param name="pid">玩家SourceId</param>
    /// <param name="sa"></param>
    /// <returns>该玩家对应的位置index</returns>
    public static int GetPlayerIdIndex(this ScriptAccessory sa, uint pid)
    {
        // 获得玩家 IDX
        return sa.Data.PartyList.IndexOf(pid);
    }

    /// <summary>
    /// 获得主视角玩家对应的位置index
    /// </summary>
    /// <param name="sa"></param>
    /// <returns>主视角玩家对应的位置index</returns>
    public static int GetMyIndex(this ScriptAccessory sa)
    {
        return sa.Data.PartyList.IndexOf(sa.Data.Me);
    }

    /// <summary>
    /// 输入玩家dataId，获得对应的位置称呼，输出字符仅作文字输出用
    /// </summary>
    /// <param name="pid">玩家SourceId</param>
    /// <param name="sa"></param>
    /// <returns>该玩家对应的位置称呼</returns>
    public static string GetPlayerJobById(this ScriptAccessory sa, uint pid)
    {
        // 获得玩家职能简称，无用处，仅作DEBUG输出
        var idx = sa.Data.PartyList.IndexOf(pid);
        var str = sa.GetPlayerJobByIndex(idx);
        return str;
    }

    /// <summary>
    /// 输入位置index，获得对应的位置称呼，输出字符仅作文字输出用
    /// </summary>
    /// <param name="idx">位置index</param>
    /// <param name="fourPeople">是否为四人迷宫</param>
    /// <param name="sa"></param>
    /// <returns></returns>
    public static string GetPlayerJobByIndex(this ScriptAccessory sa, int idx, bool fourPeople = false)
    {
        List<string> role8 = ["MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4"];
        List<string> role4 = ["T", "H", "D1", "D2"];
        if (idx < 0 || idx >= 8 || (fourPeople && idx >= 4))
            return "Unknown";
        return fourPeople ? role4[idx] : role8[idx];
    }

}
#endregion 位置序列函数

#region 绘图函数

internal static class DrawTools
{
    /// <summary>
    /// 返回绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">绘图基准，可为UID或位置</param>
    /// <param name="targetObj">绘图指向目标，可为UID或位置</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="radian">绘制图形弧度范围</param>
    /// <param name="rotation">绘制图形旋转弧度，以owner面前为基准，逆时针增加</param>
    /// <param name="width">绘制图形宽度，部分图形可保持与长度一致</param>
    /// <param name="length">绘制图形长度，部分图形可保持与宽度一致</param>
    /// <param name="innerWidth">绘制图形内宽，部分图形可保持与长度一致</param>
    /// <param name="innerLength">绘制图形内长，部分图形可保持与宽度一致</param>
    /// <param name="drawModeEnum">绘图方式</param>
    /// <param name="drawTypeEnum">绘图类型</param>
    /// <param name="color">使用颜色</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <param name="byY">动画效果随距离变更</param>
    /// <param name="draw">是否直接绘图</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawOwnerBase(this ScriptAccessory sa,
        object ownerObj, object targetObj, int delay, int destroy, string name,
        float radian, float rotation, float width, float length, float innerWidth, float innerLength,
        DrawModeEnum drawModeEnum, DrawTypeEnum drawTypeEnum, Vector4 color,
        bool byTime = false, bool byY = false, bool draw = true)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(width, length);
        dp.InnerScale = new Vector2(innerWidth, innerLength);
        dp.Radian = radian;
        dp.Rotation = rotation;
        dp.Color = color;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
        dp.ScaleMode |= byY ? ScaleMode.YByDistance : ScaleMode.None;

        switch (ownerObj)
        {
            case uint u:
                dp.Owner = u;
                break;
            case ulong ul:
                dp.Owner = ul;
                break;
            case Vector3 spos:
                dp.Position = spos;
                break;
            default:
                throw new ArgumentException($"ownerObj {ownerObj} 的目标类型 {ownerObj.GetType()} 输入错误");
        }

        switch (targetObj)
        {
            case 0:
            case 0u:
                break;
            case uint u:
                dp.TargetObject = u;
                break;
            case ulong ul:
                dp.TargetObject = ul;
                break;
            case Vector3 tpos:
                dp.TargetPosition = tpos;
                break;
            default:
                throw new ArgumentException($"targetObj {targetObj} 的目标类型 {targetObj.GetType()} 输入错误");
        }

        if (draw)
            sa.Method.SendDraw(drawModeEnum, drawTypeEnum, dp);
        return dp;
    }

    /// <summary>
    /// 返回指路绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">出发点</param>
    /// <param name="targetObj">结束点</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="rotation">箭头旋转角度</param>
    /// <param name="width">箭头宽度</param>
    /// <param name="color">使用颜色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawGuidance(this ScriptAccessory sa,
        object ownerObj, object targetObj, int delay, int destroy, string name,
        Vector4 color, float rotation = 0, float width = 1f, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, 0, rotation, width,
            width, 0, 0, DrawModeEnum.Imgui, DrawTypeEnum.Displacement, color, false, true, draw);

    public static DrawPropertiesEdit DrawGuidance(this ScriptAccessory sa,
        object targetObj, int delay, int destroy, string name, Vector4 color, float rotation = 0, float width = 1f,
        bool draw = true)
        => sa.DrawGuidance((ulong)sa.Data.Me, targetObj, delay, destroy, name, color, rotation, width, draw);

    /// <summary>
    /// 返回圆形绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">圆心</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="scale">圆形径长</param>
    /// <param name="byTime">是否随时间扩充</param>
    /// <param name="color">使用颜色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawCircle(this ScriptAccessory sa,
        object ownerObj, int delay, int destroy, string name,
        float scale, Vector4 color, bool byTime = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, 0, delay, destroy, name, 2 * float.Pi, 0, scale, scale,
            0, 0, DrawModeEnum.Default, DrawTypeEnum.Circle, color, byTime,false, draw);

    /// <summary>
    /// 返回环形绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">圆心</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="outScale">外径</param>
    /// <param name="innerScale">内径</param>
    /// <param name="byTime">是否随时间扩充</param>
    /// <param name="color">使用颜色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawDonut(this ScriptAccessory sa,
        object ownerObj, int delay, int destroy, string name,
        float outScale, float innerScale, Vector4 color, bool byTime = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, 0, delay, destroy, name, 2 * float.Pi, 0, outScale, outScale, innerScale,
            innerScale, DrawModeEnum.Default, DrawTypeEnum.Donut, color, byTime, false, draw);

    /// <summary>
    /// 返回扇形绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">圆心</param>
    /// <param name="targetObj">目标</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="radian">弧度</param>
    /// <param name="rotation">旋转角度</param>
    /// <param name="outScale">外径</param>
    /// <param name="innerScale">内径</param>
    /// <param name="byTime">是否随时间扩充</param>
    /// <param name="color">使用颜色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawFan(this ScriptAccessory sa,
        object ownerObj, object targetObj, int delay, int destroy, string name, float radian, float rotation,
        float outScale, float innerScale, Vector4 color, bool byTime = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, radian, rotation, outScale, outScale, innerScale,
            innerScale, DrawModeEnum.Default, innerScale == 0 ? DrawTypeEnum.Fan : DrawTypeEnum.Donut, color, byTime, false, draw);

    public static DrawPropertiesEdit DrawFan(this ScriptAccessory sa,
        object ownerObj, int delay, int destroy, string name, float radian, float rotation,
        float outScale, float innerScale, Vector4 color, bool byTime = false, bool draw = true)
        => sa.DrawFan(ownerObj, 0, delay, destroy, name, radian, rotation, outScale, innerScale, color, byTime, draw);

    /// <summary>
    /// 返回矩形绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">矩形起始</param>
    /// <param name="targetObj">目标</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="rotation">旋转角度</param>
    /// <param name="width">矩形宽度</param>
    /// <param name="length">矩形长度</param>
    /// <param name="byTime">是否随时间扩充</param>
    /// <param name="byY">是否随距离扩充</param>
    /// <param name="color">使用颜色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawRect(this ScriptAccessory sa,
        object ownerObj, object targetObj, int delay, int destroy, string name, float rotation,
        float width, float length, Vector4 color, bool byTime = false, bool byY = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, 0, rotation, width, length, 0, 0,
            DrawModeEnum.Default, DrawTypeEnum.Rect, color, byTime, byY, draw);

    public static DrawPropertiesEdit DrawRect(this ScriptAccessory sa,
        object ownerObj, int delay, int destroy, string name, float rotation,
        float width, float length, Vector4 color, bool byTime = false, bool byY = false, bool draw = true)
        => sa.DrawRect(ownerObj, 0, delay, destroy, name, rotation, width, length, color, byTime, byY, draw);

    /// <summary>
    /// 返回线型绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">线条起始</param>
    /// <param name="targetObj">线条目标</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="rotation">旋转角度</param>
    /// <param name="width">线条宽度</param>
    /// <param name="length">线条长度</param>
    /// <param name="byTime">是否随时间扩充</param>
    /// <param name="byY">是否随距离扩充</param>
    /// <param name="color">使用颜色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawLine(this ScriptAccessory sa,
        object ownerObj, object targetObj, int delay, int destroy, string name, float rotation,
        float width, float length, Vector4 color, bool byTime = false, bool byY = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, 1, rotation, width, length, 0, 0,
            DrawModeEnum.Default, DrawTypeEnum.Line, color, byTime, byY, draw);

    /// <summary>
    /// 返回两对象间连线绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">起始源</param>
    /// <param name="targetObj">目标源</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="width">线宽</param>
    /// <param name="color">使用颜色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawConnection(this ScriptAccessory sa, object ownerObj, object targetObj,
        int delay, int destroy, string name, Vector4 color, float width = 1f, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, 0, 0, width, width,
            0, 0, DrawModeEnum.Imgui, DrawTypeEnum.Line, color, false, true, draw);

    /// <summary>
    /// 赋予输入的dp以ownerId为源的远近目标绘图
    /// </summary>
    /// <param name="self"></param>
    /// <param name="isNearOrder">从owner计算，近顺序或远顺序</param>
    /// <param name="orderIdx">从1开始</param>
    /// <returns></returns>
    public static DrawPropertiesEdit SetOwnersDistanceOrder(this DrawPropertiesEdit self, bool isNearOrder,
        uint orderIdx)
    {
        self.CentreResolvePattern = isNearOrder
            ? PositionResolvePatternEnum.PlayerNearestOrder
            : PositionResolvePatternEnum.PlayerFarestOrder;
        self.CentreOrderIndex = orderIdx;
        return self;
    }
}

#endregion 绘图函数

#region 调试函数

internal static class DebugFunction
{
    private static readonly List<string> PrefixWhiteList = [""];
    private static readonly List<string> PrefixBlackList = [""];
    public static void DebugMsg(this ScriptAccessory sa, string msg,
        [CallerMemberName] string prefix = "",
        bool showInChatBox = true, bool enableWhiteList = false, bool enableBlackList = true)
    {
        if (!Dancing_Mad_Ultimate.Debugging) return;

        if (enableWhiteList)
            if (!PrefixWhiteList.Contains(prefix)) return;
        if (enableBlackList)
            if (PrefixBlackList.Contains(prefix)) return;

        sa.Log.Debug($"[{prefix}] {msg}");
        if (!showInChatBox) return;
        sa.Method.SendChat($"/e [{prefix}] {msg}");
    }
}

#endregion 调试函数

#region 特殊函数

internal static class SpecialFunction
{
    public static void SetRotation(this ScriptAccessory sa, IGameObject? obj, float radian, bool show = false)
    {
        if (obj == null || !obj.IsValid())
        {
            sa.Log.Error($"传入的IGameObject不合法。");
            return;
        }
        unsafe
        {
            GameObject* charaStruct = (GameObject*)obj.Address;
            charaStruct->SetRotation(radian);
        }
        // sa.Log.Debug($"改变面向 {obj.Name.TextValue} | {obj.GameObjectId} => {radian.RadToDeg()}");

        if (!show) return;
        var ownerObj = sa.GetById(obj.GameObjectId);
        if (ownerObj == null) return;
        var dp = sa.DrawGuidance(obj.GameObjectId, 0, 0, 2000, $"改变面向 {obj.Name.TextValue}", sa.Data.DefaultSafeColor, radian, draw: false);
        dp.FixRotation = true;
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Arrow, dp);
    }

    public static unsafe bool IsMoving(this ScriptAccessory sa)
    {
        FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentMap* ptr = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentMap.Instance();
        return ptr is not null && ptr->IsPlayerMoving;
    }

    public static unsafe void WriteVisible(this ScriptAccessory sa, IGameObject? actor, bool visible)
    {
        const VisibilityFlags VISIBLE_FLAG = VisibilityFlags.None;
        const VisibilityFlags INVISIBILITY_FLAG = VisibilityFlags.Model;
        try
        {
            var flagsPtr = &((GameObject*)actor?.Address)->RenderFlags;
            *flagsPtr = visible ? VISIBLE_FLAG : INVISIBILITY_FLAG;
        }
        catch (Exception e)
        {
            sa.Log.Error(e.ToString());
            throw;
        }
    }

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

    public static unsafe void AlphaModify(this ScriptAccessory sa, IGameObject? obj, float alpha,
        Func<float, bool>? shouldModify = null)
    {
        alpha = Math.Clamp(alpha, 0f, 1f);
        sa.Method.RunOnMainThreadAsync(Action);
        void Action()
        {
            if (obj == null) return;

            Character* charaStruct = (Character*)obj.Address;
            if (!obj.IsValid() || !charaStruct->IsReadyToDraw())
            {
                sa.Log.Error($"传入的IGameObject不合法。");
                return;
            }

            if (!charaStruct->IsCharacter())
            {
                sa.Log.Error($"传入的IGameObject不是Character，无法修改透明度。");
                return;
            }

            if (shouldModify != null && !shouldModify(charaStruct -> Alpha))
                return;

            charaStruct->Alpha = alpha;
            // charaStruct->DisableDraw();
            // charaStruct->EnableDraw();

            // sa.Log.Debug($"AlphaModify => {obj.Name.TextValue} | {obj} => {alpha}");
        }
    }
}

#endregion 特殊函数

#endregion 函数集

#endregion
