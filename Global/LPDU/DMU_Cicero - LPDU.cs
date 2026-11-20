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
    version: Version, author: "Cicero & Codaaaaaa & Usami & RyougiMio & Errer", note: Note, updateInfo: UpdateInfo)]
public class Dancing_Mad_Ultimate
{
    #region Metadata

    private const string Name = "Dancing Mad (Ultimate) DMU UMAD - LPDU";
    private const string Version = "0.0.0.1";

    private const string Note =
        $"""
        {Version}
        Merged from five scripts:
            Cicero - P1~P5 draws, full P2 Forsaken guidance, and the P3 Bowels of Agony (P3A) Ultima Blaster guidance.
            Coda - P1 guidance.
            Usami - P3 draws and guidance.
            RyougiMio - P4 guidance.
            Errer - P5 draws and guidance.

        Party order must be MT OT H1 H2 M1 M2 R1 R2 (M = melee, R = ranged).
        Party order test: type /e kdmutest in chat.

        Methods and settings marked with * are non-vanilla features (hiding skill VFX, transparent bosses, blocking invalid targets, removing the Thunder III tether).
        They are on by default; turn off the ones you don't want in the '* Special Features' group of the user settings.
        """;

    private const string UpdateInfo =
        $"""
        {Version}
        Merged the Cicero / Coda / Usami / RyougiMio / Errer scripts.
        """;

    #endregion

    #region User Settings

    // Separator items: no real effect, they just use the grey note as a group title. The plugin has no "text only" setting type,
    // a bool always draws a checkbox with the property name next to it, so the property names are underscores of different lengths, almost invisible
    // (at least two underscores: a single _ would shadow every out _ discard in the class)
    [UserSetting("—————————— General ——————————")]
    public bool __ { get; set; } = true;
    [UserSetting("General - Enable text prompts")]
    public bool enablePrompts { get; set; } = true;
    [UserSetting("General - Enable vanilla TTS")]
    public bool enableVanillaTts { get; set; } = true;
    [UserSetting("General - Enable Daily Routines TTS\n(requires the Daily Routines plugin to be installed and enabled!)")]
    public bool enableDailyRoutinesTts { get; set; } = false;
    [UserSetting("General - Colour of mechanic direction indicators")]
    public ScriptColor colourOfDirectionIndicators { get; set; } = new() { V4 = new Vector4(1,1,0, 1) }; // Yellow by default.
    [UserSetting("General - Colour of extremely dangerous attacks")]
    public ScriptColor colourOfExtremelyDangerousAttacks { get; set; } = new() { V4 = new Vector4(1,0,0,1) }; // Red by default.
    [UserSetting("General - Channel for the party order test text")]
    public PartyTestChannels partyTestChannel { get; set; } = PartyTestChannels.EchoChannel_VisibleOnlyToYou;

    [UserSetting("\n\n\n\n\n\n—————————— * Special Features (non-vanilla) ——————————")]
    public bool ___ { get; set; } = true;
    [UserSetting("*General - Hide the VFX of Flagrant Fire III, Blizzard III Blowout and Thrumming Thunder III\n(when enabled, these three AoEs are drawn by the script instead)")]
    public bool blockFireIceThunderVfx { get; set; } = true;
    [UserSetting("*General - How to hide the VFX of\nFlagrant Fire III, Blizzard III Blowout and Thrumming Thunder III")]
    public AnimationBlockadeModes animationBlockadeMode { get; set; } = AnimationBlockadeModes.LegacyMethod_ToggleVisibilityOfInvisibleCasters;
    [UserSetting("*P3 Firewall - Make the boss you can't damage untargetable")]
    public bool p3FirewallBlockInvalidTargets { get; set; } = true;
    [UserSetting("*P3A Remove the Thunder III tether")]
    public bool p3aRemoveThunderTether { get; set; } = true;
    [UserSetting("*P3A Make Chaos transparent while Vacuum Wave is casting")]
    public bool p3aChaosTransparentDuringVacuumWave { get; set; } = true;
    [UserSetting("*P3B Make both bosses transparent during Black Holes and Blizzard III")]
    public bool p3bBossesTransparentDuringBlackHole { get; set; } = true;
    [UserSetting("*P3B Make Giant Kefka transparent")]
    public bool p3bKefkaTransparent { get; set; } = true;
    [UserSetting("*P3B Make Chaos transparent when not casting")]
    public bool p3bChaosTransparent { get; set; } = true;
    [UserSetting("*P3B Make Exdeath transparent when not casting")]
    public bool p3bExdeathTransparent { get; set; } = true;

    [UserSetting("\n\n\n\n\n\n—————————— Debug ——————————")]
    public bool ____ { get; set; } = true;
        internal static bool DebugEnabled;
        [UserSetting("Debug - Output debug info\n(Dalamud log, some of it also goes to the Echo channel)")]
        public bool enableDebugLogging { get => DebugEnabled; set => DebugEnabled = value; }

    [UserSetting("\n\n\n\n\n\n—————————— P1 ——————————")]
    public bool _____ { get; set; } = true;
    [UserSetting("P1 Double-trouble Trap - Draw duration before the debuff expires (seconds)")]
    public double doubleTroubleTrapDuration { get; set; } = 5;

    [UserSetting("\n\n\n\n\n\n—————————— P2 ——————————")]
    public bool ______ { get; set; } = true;
    [UserSetting("P2 Use PF strat: Forsaken set 1 stacks take the tower with their buddy\n(buddy has a cone -> cone tower, buddy has a circle -> circle tower)")]
    public bool followPfStrats { get; set; } = false;
    [UserSetting("P2 Forsaken guidance\nDraw helper lines to the Spellwave (cone) spots on even sets")]
    public bool phase2sub2_spellwaveAuxiliaryLines { get; set; } = true;
    [UserSetting("P2 Forsaken - Draw style")]
    public Phase2Sub2_DrawingTypes phase2sub2_drawingType { get; set; } = Phase2Sub2_DrawingTypes.Default;
    [UserSetting("P2 Forsaken Spell's Trouble\nDraw duration before the towers resolve (seconds, max 7)")]
    public double phase2sub2_drawingDuration { get; set; } = 5;
    [UserSetting("P2 Forsaken Spell's Trouble - Only draw Spellwave (cones)")]
    public bool phase2sub2_spellwaveOnly { get; set; } = false;
    [UserSetting("P2 Forsaken - Draw helper lines to the tower spots")]
    public bool phase2sub2_towerAuxiliaryLines { get; set; } = false;
    [UserSetting("P2 Forsaken - Colour of helper lines")]
    public ScriptColor phase2sub2_colourOfAuxiliaryLines { get; set; } = new() { V4 = new Vector4(0,1,1, 1) }; // Blue by default.

    [UserSetting("\n\n\n\n\n\n—————————— P3 ——————————")]
    public bool _______ { get; set; } = true;
    [UserSetting("P3 Use PF strat: Blizzard III corners and towers split left/right by G1/G2 (Kefka = north)\n" +
        "Checked = PF:\n" +
        "  G1 (MT H1 M1 R1) left: MT+H1 NW, M1+R1 SW, soak left tower\n" +
        "  G2 (OT H2 M2 R2) right: OT+H2 NE, M2+R2 SE, soak right tower\n" +
        "Unchecked = LPDU:\n" +
        "  T + melee left: MT+OT NW, M1+M2 SW, soak left tower\n" +
        "  H + ranged right: H1+H2 NE, R1+R2 SE, soak right tower")]
    public bool p3FollowPfStrats { get; set; } = false;
    [UserSetting("P3 Chaos Damning Edict - Draw colour")]
    public ScriptColor CursedEdictColor { get; set; } = new ScriptColor { V4 = new Vector4(1.0f, 0.643f, 0f, 1.0f) };
    [UserSetting("P3 Chaos Damning Edict - Draw intensity (keep it at 2 or below for ImGui)")]
    public static float CursedEdictWhiteVal { get; set; } = 4;
    [UserSetting("P3B1 Black Holes - Colour of indicator lines")]
    public ScriptColor BhConnLineColor { get; set; } = new ScriptColor { V4 = new Vector4(1.0f, 1.0f, 0f, 1.0f) };
    [UserSetting("P3B2 Blizzard III - Use Kefka's position as north")]
    public static bool BlizKefkaIsNorth { get; set; } = true;

    [UserSetting("\n\n\n\n\n\n—————————— P4 ——————————")]
    public bool ________ { get; set; } = true;
    [UserSetting("P4 Cursed Shriek type indicator - Don't draw my own Cursed Shriek")]
    public bool phase4_disableCursedShriekOnMe { get; set; } = false;
    [UserSetting("P4 Entropy & Dynamic Fluid - Only draw my own bait AoE")]
    public bool phase4_disableStrayBaitOnOthers { get; set; } = true;
    [UserSetting("P4 Real/fake prediction callout\n(Off = disabled, Echo = Echo channel, Party = party chat)")]
    public P4PlanMessageMode PlanMessageMode { get; set; } = P4PlanMessageMode.Off;

    [UserSetting("\n\n\n\n\n\n—————————— P5 ——————————")]
    public bool _________ { get; set; } = true;
    [UserSetting("P5 Exaflares - Use LPDU strat\n" +
        "Checked = LPDU: everyone walks through the middle together, spread at the end\n" +
        "Unchecked = by role: MT OT M1 M2 walk through the middle, H1 left, H2 right, R1 up, R2 down")]
    public bool p5FireFollowLpdu { get; set; } = false;
    [UserSetting("P5 Exaflares - Opacity of the exaflare circles (%, 0~100)")]
    public int p5FireOpacity { get; set; } = 30;

    #endregion

    #region Debug

    // When on, all guidance is drawn for all 8 players (each solved from their own buff / marker / role),
    // internal methods with userControl: Debugging are shown, and Usami's section prints debug info
    // Must be const: userControl: Debugging is an attribute argument and only takes compile-time constants
    public const bool Debugging = false;

    /// <summary>Owner of the guidance arrow start: normally PartyList[idx], falls back to me if out of range</summary>
    private static uint GuidanceOwner(ScriptAccessory sa, int idx)
        => idx >= 0 && idx < sa.Data.PartyList.Count ? sa.Data.PartyList[idx] : sa.Data.Me;

    #endregion

    #region Tests

    [ScriptMethod(name: "———————— [Tests] ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void Test_Separator(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "Test: print current stage", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void PrintCurrentStage(Event ev, ScriptAccessory sa)
    {
        sa.DebugMsg($"{_udmP3Param.CurrentStage}");
    }

    [ScriptMethod(name: "Test: show priority table", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void ShowPriorityTable(Event ev, ScriptAccessory sa)
    {

        var str = $"Priority dict:\n";
        foreach (var pair in _pd.Priorities)
        {
            str += $"Key {pair.Key} Value {pair.Value}\n";
        }
        sa.DebugMsg($"\n{str}");
        sa.DebugMsg($"\n{_pd.ShowPriorities()}");
    }

    [ScriptMethod(name: "Test: assign both bosses", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void AssignBothBosses(Event ev, ScriptAccessory sa)
    {
        _udmP3Param.ObjectId_Chaos = sa.GetByDataId(19508u).First().GameObjectId;
        _udmP3Param.ObjectId_Exdeath = sa.GetByDataId(19509u).First().GameObjectId;
    }

    #endregion Tests

    #region Init

    public void Init(ScriptAccessory sa)
    {
        Init_Cicero(sa);
        Init_Coda(sa);
        Init_Usami(sa);
        Init_RyougiMio(sa);
        Init_Errer(sa);
    }

    #endregion

    #region General (Cicero)

    #region Variables_And_Semaphores

    private volatile int majorPhase=1;
    private volatile int phase=1;

    /*

    Major Phase 1:

        Phases are separated by Graven Image.

    Major Phase 2:

        Phase 1 - (~,Forsaken)
        Phase 2 - [Forsaken,Light of Judgment)
        Phase 3 - [Light of Judgment,~)

    Major Phase 3:

        Phase 1 - (~,Bowels of Agony)
        Phase 2 - [Bowels of Agony,Earthquake)
        Phase 3 - [Earthquake,~)

    Major Phase 4:

        No phase separation.

    Major Phase 5:

        I'm retired! For now :D

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

        DoNotSend,
        EchoChannel_VisibleOnlyToYou,
        PartyChannel_VisibleToAllMembers

    }

    public enum AnimationBlockadeModes {

        LegacyMethod_ToggleVisibilityOfInvisibleCasters,
        EngineMethod_RemoveSpecificVfxByHandle

    }

    public enum Phase2Sub2_IconTypes {

        FAN,
        SPREAD,
        STACK,
        UNKNOWN

    }

    public enum Phase2Sub2_DrawingTypes {

        Default,
        AllVfx,
        AuthorRecommended_CirclesImGui_RestVfx,
        AllImGui

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

        lock(_trineLock) {

            _trines.Clear();
            _trineLanded=0;
            _trineSafeHalf=null;

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

    [ScriptMethod(name: "———————— [General] ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"])]
    public void General_Separator(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name:"General Party Order Test",
        eventType:EventTypeEnum.Chat,
        eventCondition:["Type:Echo"])]

    public void General_PartyOrderTest(Event @event,ScriptAccessory accessory) {

        string processedText=(@event["Message"]).Trim().ToLower();

        if(!string.Equals(processedText,"kdmutest")) {

            return;

        }

        string text="Please check that the party order below is correct:\n";
        string log=string.Empty;
        KodakkuAssist.Data.IGameObject? sourceObject=null;
        string[] roles=["MT",
                        "OT",
                        "H1",
                        "H2",
                        "M1",
                        "M2",
                        "R1",
                        "R2"];

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

                    text+=$"{roles[i]}:{sourceICharacter.Name}. ";

                    if(i<roles.Length-1) {

                        text+="\n";

                    }

                    log+=$"{roles[i]}: {accessory.Data.PartyList[i]}\n";

                }

            }

        }

        switch(partyTestChannel) {

            case PartyTestChannels.DoNotSend: {

                break;

            }

            case PartyTestChannels.EchoChannel_VisibleOnlyToYou: {

                accessory.Method.SendChat($"/e \n{text}");

                break;

            }

            case PartyTestChannels.PartyChannel_VisibleToAllMembers: {

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

    [ScriptMethod(name:"General Flagrant Fire III (Data Collection 1)",
        eventType:EventTypeEnum.TargetIcon,
        eventCondition:["Id:regex:^(02A1|02A2|02A3|02A4|02A5|02A6)$"],
        userControl:false)]

    public void General_FlagrantFireIII_DataCollection1(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"General Flagrant Fire III (Data Collection 2)",
        eventType:EventTypeEnum.TargetIcon,
        eventCondition:["Id:regex:^(0080|007F)$"],
        userControl:false)]

    public void General_FlagrantFireIII_DataCollection2(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"General Flagrant Fire III (AoE)",
        eventType:EventTypeEnum.TargetIcon,
        eventCondition:["Id:regex:^(0080|007F)$"],
        suppress:COMMON_INTERVAL)]

    public void General_FlagrantFireIII_Aoe(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"*General Flagrant Fire III (Hide VFX, engine method)",
        eventType:EventTypeEnum.VfxEvent,
        eventCondition:["Id:regex:^(128|127)$"])]

    public void General_FlagrantFireIII_VfxBlock_EngineMethod(Event @event,ScriptAccessory accessory) {

        if(!blockFireIceThunderVfx) {

            return;

        }

        if(animationBlockadeMode!=AnimationBlockadeModes.EngineMethod_RemoveSpecificVfxByHandle) {

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

    [ScriptMethod(name:"General Flagrant Fire III (Clear Data)",
        eventType:EventTypeEnum.ActionEffect,
        eventCondition:["ActionId:regex:^(47778|47779)$"],
        suppress:COMMON_INTERVAL,
        userControl:false)]

    public void General_FlagrantFireIII_DataClear(Event @event,ScriptAccessory accessory) {

        fakeFlagrantFire=false;
        flagrantFireObfuscationSemaphore.Reset();
        partyMembersWithFlagrantFire.Clear();
        stackFlagrantFireIcon=false;
        flagrantFireIconSemaphore.Reset();

    }

    [ScriptMethod(name:"General Blizzard III Blowout (AoE)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:regex:^(47768|47774)$"])]

    public void General_BlizzardIIIBlowout_Aoe(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"*General Blizzard III Blowout (Hide VFX, engine method)",
        eventType:EventTypeEnum.VfxEvent,
        eventCondition:["Id:737"])]

    public void General_BlizzardIIIBlowout_VfxBlock_EngineMethod(Event @event,ScriptAccessory accessory) {

        if(!blockFireIceThunderVfx) {

            return;

        }

        if(animationBlockadeMode!=AnimationBlockadeModes.EngineMethod_RemoveSpecificVfxByHandle) {

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

    [ScriptMethod(name:"*General Blizzard III Blowout (Hide VFX, legacy method)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:regex:^(47768|47771)$"])]

    public void General_BlizzardIIIBlowout_VfxBlock_LegacyMethod(Event @event,ScriptAccessory accessory) {

        if(!blockFireIceThunderVfx) {

            return;

        }

        if(animationBlockadeMode!=AnimationBlockadeModes.LegacyMethod_ToggleVisibilityOfInvisibleCasters) {

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

    [ScriptMethod(name:"General Thrumming Thunder III (AoE)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:regex:^(47775|47777)$"])]

    public void General_ThrummingThunderIII_Aoe(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"*General Thrumming Thunder III (Hide VFX, engine method)",
        eventType:EventTypeEnum.VfxEvent,
        eventCondition:["Id:171"])]

    public void General_ThrummingThunderIII_VfxBlock_EngineMethod(Event @event,ScriptAccessory accessory) {

        if(!blockFireIceThunderVfx) {

            return;

        }

        if(animationBlockadeMode!=AnimationBlockadeModes.EngineMethod_RemoveSpecificVfxByHandle) {

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

    [ScriptMethod(name:"*General Thrumming Thunder III (Hide VFX, legacy method)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:regex:^(47775|47776)$"])]

    public void General_ThrummingThunderIII_VfxBlock_LegacyMethod(Event @event,ScriptAccessory accessory) {

        if(!blockFireIceThunderVfx) {

            return;

        }

        if(animationBlockadeMode!=AnimationBlockadeModes.LegacyMethod_ToggleVisibilityOfInvisibleCasters) {

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

    #region P1 Draws (Cicero)

    #region Major_Phase_1

    [ScriptMethod(name: "———————— [P1] ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"])]
    public void P1_Separator(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name:"P1 Revolting Ruin III (AoE)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:50179"])]

    public void P1_RevoltingRuinIII_Aoe(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"P1 Hyperdrive (AoE)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:50722"])]

    public void P1_Hyperdrive_Aoe(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"P1 Double-trouble Trap (AoE)",
        eventType:EventTypeEnum.StatusAdd,
        eventCondition:["StatusID:5078"])]

    public void P1_DoubleTroubleTrap_Aoe(Event @event,ScriptAccessory accessory) {

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

        currentProperties.Name=$"P1_DoubleTroubleTrap_Aoe_{targetId}";
        currentProperties.Scale=new(6);
        currentProperties.Owner=targetId;
        currentProperties.Color=accessory.Data.DefaultDangerColor;
        currentProperties.Delay=delay;
        currentProperties.DestoryAt=duration;

        accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Circle,currentProperties);

    }

    [ScriptMethod(name:"P1 Double-trouble Trap (Knockback Indicator)",
        eventType:EventTypeEnum.StatusAdd,
        eventCondition:["StatusID:5078"])]

    public void P1_DoubleTroubleTrap_KnockbackIndicator(Event @event,ScriptAccessory accessory) {

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
            currentProperties.Name=$"P1_DoubleTroubleTrap_KnockbackIndicator_{targetId}_{partyIndex}";
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

    [ScriptMethod(name:"P1 Double-trouble Trap (Clear AoE & Knockback Indicator)",
        eventType:EventTypeEnum.StatusRemove,
        eventCondition:["StatusID:5078"],
        userControl:false)]

    public void P1_DoubleTroubleTrap_ClearAoeAndKnockbackIndicator(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=1) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        accessory.Method.RemoveDraw($"P1_DoubleTroubleTrap_Aoe_{targetId}");
        accessory.Method.RemoveDraw($"P1_DoubleTroubleTrap_KnockbackIndicator_{targetId}(_\\d)?");

    }

    [ScriptMethod(name:"P1 Graven Image (Phase Control)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:48370"],
        userControl:false)]

    public void P1_GravenImage_PhaseControl(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=1) {

            return;

        }

        Interlocked.Increment(ref phase);

        if(enableDebugLogging) {

            accessory.Log.Debug($"majorPhase={majorPhase}\nphase={phase}");

        }

    }

    [ScriptMethod(name:"P1 Graven Image 1 Pulse Wave (Knockback Indicator)",
        eventType:EventTypeEnum.Tether,
        eventCondition:["Id:002D"])]

    public void P1_GravenImage1_PulseWave_KnockbackIndicator(Event @event,ScriptAccessory accessory) {

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
        currentProperties.Name=$"P1_GravenImage1_PulseWave_KnockbackIndicator_{targetId}";
        currentProperties.Scale=new(2,13);
        currentProperties.Owner=targetId;
        currentProperties.FixRotation=true;
        currentProperties.Rotation=0;
        currentProperties.Color=colourOfDirectionIndicators.V4.WithW(1);
        currentProperties.DestoryAt=5125;

        accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Arrow,currentProperties);

    }

    [ScriptMethod(name:"P1 Graven Image 1 Mystery Magic (Data Collection)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:47764"],
        userControl:false)]

    public void P1_GravenImage1_MysteryMagic_DataCollection(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"P1 Graven Image 1 Wave Cannon (AoE)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:47764"])]

    public void P1_GravenImage1_WaveCannon_Aoe(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"P1 Graven Image 1 Explosion (Precise AoE)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:47786"])]

    public void P1_GravenImage1_Explosion_PreciseAoe(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"P1 Graven Image 2 Gravitas (AoE)",
        eventType:EventTypeEnum.Tether,
        eventCondition:["Id:002D"])]

    public void P1_GravenImage2_Gravitas_Aoe(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"P1 Graven Image 2 Vitrophyre (AoE)",
        eventType:EventTypeEnum.Tether,
        eventCondition:["Id:002D"])]

    public void P1_GravenImage2_Vitrophyre_Aoe(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"P1 Graven Image 2 Revolting Ruin III (Phase Control)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:50179"],
        userControl:false)]

    public void P1_GravenImage2_RevoltingRuinIII_PhaseControl(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=1) {

            return;

        }

        if(phase!=3) {

            return;

        }

        phase1sub3_isFirstHalf=false;

    }

    [ScriptMethod(name:"P1 Graven Image 2 Gravitational Wave & Intemperate Will (AoE)",
        eventType:EventTypeEnum.ObjectEffect,
        eventCondition:["Id1:64"])]

    public void P1_GravenImage2_GravitationalWaveAndIntemperateWill_Aoe(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"P1 Graven Image 3 Idyllic Will (AoE)",
        eventType:EventTypeEnum.Tether,
        eventCondition:["Id:002D"])]

    public void P1_GravenImage3_IdyllicWill_Aoe(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"P1 Graven Image 3 Indolent Will & Ave Maria (Facing Indicator)",
        eventType:EventTypeEnum.ObjectEffect,
        eventCondition:["Id1:64"])]

    public void P1_GravenImage3_IndolentWillAndAveMaria_FacingIndicator(Event @event,ScriptAccessory accessory) {

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
            currentProperties.Name=$"P1_GravenImage3_IndolentWillAndAveMaria_FacingIndicator_{partyIndex}";
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

    #region P1 Guidance (Coda, LPDU strat)

    // LPDU P1: https://raidplan.io/plan/_5FyPYATLhNYkq8S , Tele-trouncing arrows: https://raidplan.io/plan/saC2CjTaXmSApm6y
    // Party order MT OT H1 H2 M1 M2 R1 R2; waymarks A/B/C/D and 1/2/3/4 are all 12m from the middle

    #region Globals & Init

    // P1 sub-phases: 1.0 opener, 1.1 Graven Image 1, 1.2 Graven Image 2 (counted from Graven Image 1's Wave Cannon), 1.3 Graven Image 3, 2.0 P2
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

        lock (_p1DangerZones)
            _p1DangerZones.Clear();

        lock (_g2Lock)
        {
            _g2Round = 0;
            _g2LastTethers = 0;
            Array.Clear(_g2Gravitas);
            _g2SafeIsNwSe = null;
            _g2CleaveSafeSide = 0;
            _g2Round1Guided = false;
        }

        ResetStatue3ArrowState();

        sa.Method.RemoveDraw(".*");
    }

    #endregion

    #region Phase Control

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
    public void ShowPhase(Event evt, ScriptAccessory sa) => sa.Debug($"Current Phase={_phase}, P1StatueCount={_p1StatueCount}");

    [ScriptMethod(
        name: "P1_GravenImage_GuidancePhaseControl",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:48370"],
        userControl: false)]
    public void P1_GravenImage_GuidancePhaseControl(Event evt, ScriptAccessory sa)
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

        sa.Debug($"P1 Graven Image phase control: Count={_p1StatueCount}, Phase={_phase}");
    }

    [ScriptMethod(
        name: "P1End_SwitchToP2",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:47804"],
        userControl: false)]
    public void P1End_SwitchToP2(Event evt, ScriptAccessory sa)
    {
        if (!IsPhase(_phase, 1.3))
            return;

        _phase = 2.0;

        sa.Debug($"P1 end: saw 47804, switching to P2. Phase={_phase}");
    }

    #endregion

    #region P1 Ice/Thunder Danger Zones (nearest safe spot)

    // Real ice 47768/47774: 40m 90° cone (the two clones each face one diagonal); real thunder 47775/47777: 40×10 line from the caster along its facing
    // Same IDs as the danger zones drawn in Cicero's section; entries expire on their own once the cast ends (4.7s)
    private readonly record struct P1DangerZone(bool IsCone, Vector2 Origin, float Facing, long EndTime);

    private readonly List<P1DangerZone> _p1DangerZones = new();

    [ScriptMethod(
        name: "P1_RecordIceThunderDangerZones",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47768|47774|47775|47777)$"],
        userControl: false)]
    public void P1_RecordIceThunderDangerZones(Event evt, ScriptAccessory sa)
    {
        if (_phase >= 2.0)
            return;

        var src = evt.SourcePosition();
        var zone = new P1DangerZone(evt.ActionId() is 47768 or 47774, new Vector2(src.X, src.Z), evt.SourceRotation(), Environment.TickCount64 + 5500);

        lock (_p1DangerZones)
        {
            _p1DangerZones.RemoveAll(z => z.EndTime < Environment.TickCount64);
            _p1DangerZones.Add(zone);
        }
    }

    private static bool P1InDangerZone(P1DangerZone z, Vector2 p, float margin)
    {
        var d = p - z.Origin;
        var dir = new Vector2(MathF.Sin(z.Facing), MathF.Cos(z.Facing));

        if (z.IsCone)
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

    /// <summary>Closest point to from that is within 18m of the middle and outside every ice cone / real thunder line (1.5m margin); stays put if there are no danger zones</summary>
    private Vector3 P1NearestSafeSpot(Vector3 from)
    {
        List<P1DangerZone> zones;
        lock (_p1DangerZones)
            zones = _p1DangerZones.Where(z => z.EndTime >= Environment.TickCount64).ToList();

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
                if (zones.Any(zone => P1InDangerZone(zone, p, 1.5f)))
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

    #region P1 Graven Image 1 (Fire/Ice + Wave Cannon)

    private readonly object _iceFireLock = new();

    // Actual danger diagonal: true = NW/SE (47768 real ice follows its facing, 47771 fake ice is inverted)
    private bool? _iceDiagonalIsLeftUpRightDown = null;

    // The Graven Image 1 group tethered by 002D: Pulse Wave 47785 knocks them about 13m south
    private readonly HashSet<uint> _waveCannonTargets = new();

    private readonly Dictionary<uint, Vector3> _action47784Targets = new();
    private bool _action47784GuideSent = false;

    private const uint RealIceAction = 47768;

    // Wave Cannon conga: supports west, DPS east, from the middle outward MT OT H1 H2 / M1 M2 R1 R2
    private static readonly float[] WaveCannonCongaDistance = [2.25f, 6.81f, 11.45f, 16.25f];

    [ScriptMethod(
        name: "P1_Ice_RecordRealFakeAndDirection",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47768|47771)$"],
        userControl: false)]
    public void P1_Ice_RecordRealFakeAndDirection(Event evt, ScriptAccessory sa)
    {
        if (!IsPhase(_phase, 1.1))
            return;

        bool iceIsReal = evt.ActionId() == RealIceAction;
        bool rawDiagonalIsLeftUpRightDown = IsLeftUpRightDownByRotation(evt.SourceRotation());

        lock (_iceFireLock)
            _iceDiagonalIsLeftUpRightDown = iceIsReal ? rawDiagonalIsLeftUpRightDown : !rawDiagonalIsLeftUpRightDown;
    }

    [ScriptMethod(
        name: "P1_Record002DTethers",
        eventType: EventTypeEnum.Tether,
        eventCondition: ["Id:002D"],
        userControl: false)]
    public void P1_Record002DTethers(Event evt, ScriptAccessory sa)
    {
        // Tether direction isn't fixed: the end that's in the party is the player, the other end is the statue
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
            P1_GravenImage2_RecordTethers(sa, playerId, statuePos);
    }

    // 47764 Mystery Magic (Graven Image 1, first one): 4.7s cast; +2.6s Pulse Wave knockback, +5.7s Flagrant Fire III, +10s Wave Cannon
    // Fire: 0080 stack / 007F spread, 02A1 over Kefka's head = fake fire, do the opposite (reads stackFlagrantFireIcon / fakeFlagrantFire recorded by Cicero's section)
    // Supports west, DPS east, each to the safe quadrant of their half; tethered players first stand 13m north of their spot for the knockback, then walk to the spot after landing
    [ScriptMethod(
        name: "P1_GravenImage1_FireIceGuide",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:47764"])]
    public void P1_GravenImage1_FireIceGuide(Event evt, ScriptAccessory sa)
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
                sa.Debug("Graven Image 1 fire/ice guide failed: no ice direction recorded.");
                return;
            }

            bool isStackCall = stackFlagrantFireIcon != fakeFlagrantFire;
            var party = sa.Data.PartyList;
            int myIdx = sa.MyIndex();

            for (int i = 0; i < party.Count && i < 8; i++)
            {
                if (!Debugging && i != myIdx)
                    continue;

                var final = P1GravenImage1Spot(i, dangerLeftUpRightDown.Value, isStackCall);
                uint finalDelay = 0;

                if (knocked.Contains(party[i]))
                {
                    var preDp = sa.WaypointDp(P1GravenImage1PreKnockbackSpot(final), 2600, 0, $"P1_GravenImage1_PreKnockback_{i}");
                    preDp.Owner = GuidanceOwner(sa, i);
                    sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, preDp);
                    finalDelay = 2600;
                }

                var finalDp = sa.WaypointDp(final, 5700 - finalDelay, finalDelay, $"P1_GravenImage1_FireIce_{i}");
                finalDp.Owner = GuidanceOwner(sa, i);
                sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, finalDp);

                var congaPos = new Vector3(100 + (i < 4 ? -1 : 1) * WaveCannonCongaDistance[i % 4], 0, 100);
                var congaDp = sa.WaypointDp(congaPos, 4200, 5800, $"P1_GravenImage1_WaveCannonConga_{i}");
                congaDp.Owner = GuidanceOwner(sa, i);
                sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, congaDp);
            }

            sa.Debug($"Graven Image 1 fire/ice: danger diagonal={(dangerLeftUpRightDown.Value ? "NW/SE" : "SW/NE")}, stack={isStackCall}, tethered={string.Join(",", knocked.Select(id => PartyIndex(party, id)))}");
        });
    }

    /// <summary>Graven Image 1 fire/ice spots: stack = the four of each half on one spot; spread = N/S of boss / E/W of boss / middle of the safe spot / wall (MT OT H1 H2, DPS in the same order)</summary>
    private static Vector3 P1GravenImage1Spot(int idx, bool dangerLeftUpRightDown, bool isStackCall)
    {
        float sx = idx < 4 ? -1 : 1;
        float sz = dangerLeftUpRightDown ? -sx : sx;

        var (x, z) = isStackCall
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

    /// <summary>Players knocked 13m south: stand 13m north of the spot first, pulled toward the middle if that's more than 17m out</summary>
    private static Vector3 P1GravenImage1PreKnockbackSpot(Vector3 final)
    {
        var p = new Vector2(final.X - 100, final.Z - 100 - 13);
        if (p.Length() > 17)
            p *= 17 / p.Length();
        return new Vector3(100 + p.X, 0, 100 + p.Y);
    }

    // 47784 Wave Cannon: in each group the two targeted players drop their towers where they stand, the other two soak the group's two towers in middle-outward order
    [ScriptMethod(
        name: "P1_WaveCannon_47784SwapGuide",
        eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:47784"],
        userControl: false)]
    public void P1_WaveCannon_47784SwapGuide(Event evt, ScriptAccessory sa)
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

                var dp = sa.WaypointDp(pos.Value, 4500, 0, $"P1_WaveCannon_47784SwapGuide_{i}");
                dp.Owner = GuidanceOwner(sa, i);
                sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }

            sa.Debug($"47784 swap guide: targeted={string.Join(",", snapshot.Keys.Select(x => PartyIndex(party, x)).OrderBy(x => x))}");
        });
    }

    #endregion

    #region P1 Graven Image 2 (Gravitas / Vitrophyre + Puddles)

    // Two rounds of 8 002D tethers each: from the middle statue (102.5,22.5,27) = Gravitas 47788 = stack, leaves a 5m puddle; from the side statue (126,7,41.5) = Vitrophyre 47792 = meteor
    // Within a round one role gets all the stacks, the other all the meteors
    // Round 1: tethers → +1.4s Blizzard III Blowout 47765 (diagonal ice) → +6.5s Gravitas → +10.5s Vitrophyre; whole party at the edge of waymark A (north) on the safe diagonal's side
    // Round 2: cleave tell → tethers → +3.9s Intemperate Will 47794 (half-room cleave) → +8.5s Gravitas → +12.5s Vitrophyre; whole party at the edge of waymark C (south) on the cleave's safe side
    // After Gravitas resolves: the meteor role spreads (G1 = MT H1 M1 R1 on the left facing the boss, G2 = OT H2 M2 R2 on the right; T/melee at max melee, H/ranged further out), the stack role goes under the boss
    private readonly object _g2Lock = new();
    private int _g2Round = 0;
    private long _g2LastTethers = 0;
    private readonly bool?[] _g2Gravitas = new bool?[8];
    private bool? _g2SafeIsNwSe = null;
    private int _g2CleaveSafeSide = 0; // +1 = east safe, -1 = west safe, 0 = unknown
    private bool _g2Round1Guided = false;

    private static readonly Vector3 GI2GravitasStatue = new(102.5f, 22.5f, 27f);
    private static readonly Vector3 GI2VitrophyreStatue = new(126f, 7f, 41.5f);

    private void P1_GravenImage2_RecordTethers(ScriptAccessory sa, uint playerId, Vector3 statuePos)
    {
        int idx = PartyIndex(sa.Data.PartyList, playerId);
        if (idx is < 0 or > 7)
            return;

        long now = Environment.TickCount64;
        bool newRound;
        int round;

        lock (_g2Lock)
        {
            newRound = now - _g2LastTethers > 3000;
            if (newRound)
            {
                _g2Round++;
                Array.Clear(_g2Gravitas);
            }

            _g2LastTethers = now;
            round = _g2Round;
            _g2Gravitas[idx] = Vector3.Distance(statuePos, GI2GravitasStatue) < 1 ? true
                : Vector3.Distance(statuePos, GI2VitrophyreStatue) < 1 ? false
                : null;
        }

        // Round 2 has no ice cast to hook onto, so guide as soon as all tethers are in
        if (newRound && round == 2)
        {
            Task.Run(async () =>
            {
                await Task.Delay(150);

                int side;
                lock (_g2Lock)
                    side = _g2CleaveSafeSide;

                P1_GravenImage2_StacksAndMeteors(sa, 2, new Vector3(100 + side * 1.5f, 0, 112), 8500, 4000);
            });
        }
    }

    [ScriptMethod(
        name: "P1_GravenImage2_RecordSafeDiagonal",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47774|47768)$"],
        userControl: false)]
    public void P1_GravenImage2_RecordSafeDiagonal(Event evt, ScriptAccessory sa)
    {
        if (!IsPhase(_phase, 1.2))
            return;

        lock (_g2Lock)
            _g2SafeIsNwSe = !IsLeftUpRightDownByRotation(evt.SourceRotation());
    }

    // Cleave tell: ObjectEffect 64/128, big orb at (92,15,27) = west half gets hit, (116,6.5,43) = east half gets hit (same as the draw in Cicero's section)
    [ScriptMethod(
        name: "P1_GravenImage2_RecordCleave",
        eventType: EventTypeEnum.ObjectEffect,
        eventCondition: ["Id1:64"],
        userControl: false)]
    public void P1_GravenImage2_RecordCleave(Event evt, ScriptAccessory sa)
    {
        if (!IsPhase(_phase, 1.2) || !string.Equals(evt["Id2"], "128"))
            return;

        var src = evt.SourcePosition();
        int side = Vector3.Distance(src, new Vector3(92, 15, 27)) < 1 ? 1
            : Vector3.Distance(src, new Vector3(116, 6.5f, 43)) < 1 ? -1
            : 0;

        if (side != 0)
        {
            lock (_g2Lock)
                _g2CleaveSafeSide = side;
        }
    }


    // 47765 Blizzard III Blowout (round 1): 4.7s cast, +5.1s Gravitas, +9.1s Vitrophyre
    [ScriptMethod(
        name: "P1_GravenImage2Guide",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:47765"])]
    public void P1_GravenImage2Guide(Event evt, ScriptAccessory sa)
    {
        if (!IsPhase(_phase, 1.2))
            return;

        lock (_g2Lock)
        {
            if (_g2Round1Guided)
                return;
            _g2Round1Guided = true;
        }

        Task.Run(async () =>
        {
            await Task.Delay(150);

            bool? safeLeftUpRightDown;
            lock (_g2Lock)
                safeLeftUpRightDown = _g2SafeIsNwSe;

            // North waymark: stand on its west side if the safe diagonal is NW/SE, otherwise on its east side
            float x = safeLeftUpRightDown switch { true => -1.2f, false => 1.2f, null => 0 };
            P1_GravenImage2_StacksAndMeteors(sa, 1, new Vector3(100 + x, 0, 88), 5100, 4000);
        });
    }

    private void P1_GravenImage2_StacksAndMeteors(ScriptAccessory sa, int round, Vector3 stackPos, uint stackMs, uint meteorMs)
    {
        bool? supportStack;

        lock (_g2Lock)
        {
            int supports = Enumerable.Range(0, 4).Count(i => _g2Gravitas[i] == true);
            int dps = Enumerable.Range(4, 4).Count(i => _g2Gravitas[i] == true);
            supportStack = supports == dps ? null : supports > dps;
        }

        int myIdx = sa.MyIndex();

        for (int i = 0; i < sa.Data.PartyList.Count && i < 8; i++)
        {
            if (!Debugging && i != myIdx)
                continue;

            var stackDp = sa.WaypointDp(stackPos, stackMs, 0, $"P1_GravenImage2_Round{round}Stack_{i}");
            stackDp.Owner = GuidanceOwner(sa, i);
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, stackDp);

            if (supportStack == null)
                continue;

            bool meteor = (i < 4) != supportStack.Value;
            var nextDp = sa.WaypointDp(meteor ? P1GravenImage2MeteorSpot(i, round) : ArenaCenter, meteorMs, stackMs, $"P1_GravenImage2_Round{round}Spread_{i}");
            nextDp.Owner = GuidanceOwner(sa, i);
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, nextDp);
        }

        sa.Debug($"Graven Image 2 round {round}: stack spot={stackPos}, stack role={(supportStack == null ? "Unknown" : supportStack.Value ? "Supports" : "DPS")}");
    }

    /// <summary>Meteor spread: G1 on the left facing the boss (round 1 seen from north = east, round 2 seen from south = west), T/melee at max melee 7m, H/ranged further out toward the puddle side</summary>
    private static Vector3 P1GravenImage2MeteorSpot(int idx, int round)
    {
        float g1 = idx % 2 == 0 ? 1 : -1;
        float side = round == 1 ? g1 : -g1;
        float zs = round == 1 ? -1 : 1;

        return isMelee(idx)
            ? new Vector3(100 + side * 7, 0, 100)
            : new Vector3(100 + side * 12.5f, 0, 100 + zs * 9);
    }

    #endregion

    #region P1 Graven Image 3 (Arrows + Fixed Spots + Final Fire/Thunder)

    private readonly object _statue3ArrowLock = new();

    // Everyone has two arrow buffs; tracked per player (in Debug the whole party is solved)
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

    // Tele-portent arrows: 4876~4879 (both stacks are from this set when they point the same way) + 5079~5082 (the "turn-out" one when they differ), 7s / 10s stacks
    [ScriptMethod(
        name: "P1_GravenImage3_ArrowsGuide",
        eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:regex:^(4876|4877|4878|4879|5079|5080|5081|5082|130C|130D|130E|130F|13D7|13D8|13D9|13DA)$"])]
    public void P1_GravenImage3_ArrowsGuide(Event evt, ScriptAccessory sa)
    {
        if (_phase >= 2.0)
            return;

        // Arrow buffs only exist in Graven Image 3; if the 48370 phase control was missed, fall back to 1.3 on them
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

        var firstDp = sa.WaypointDp(firstPos, 7500, 0, $"P1_GravenImage3_ArrowsGuide_Part1_{targetIdx}", new Vector4(0, 1, 0, 1));
        firstDp.Owner = GuidanceOwner(sa, targetIdx);

        var secondDp = sa.WaypointDp(secondPos, 3000, 7500, $"P1_GravenImage3_ArrowsGuide_Part2_{targetIdx}", new Vector4(0, 1, 0, 1));
        secondDp.Owner = GuidanceOwner(sa, targetIdx);

        var previewDp = sa.WaypointFromToDp(firstPos, secondPos, 7500, 0, $"P1_GravenImage3_ArrowsGuide_Part2Preview_{targetIdx}", new Vector4(1, 1, 1, 1));

        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, firstDp);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, secondDp);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, previewDp);

        sa.Debug($"Graven Image 3 arrows: Index={targetIdx}, {plan.Callout}, First={firstPos}, Second={secondPos}");
    }

    // Graven Image 3 tethers (ignore sleep/confuse) are fully static: N MT in / R1 out, W OT in / R2 out, E M2 in / H2 out, S M1 in / H1 out
    // 3.3s after the tethers comes the third trap knockback, +9s Idyllic Will / Ave Maria resolve; go to the fixed spot after landing from the knockback
    [ScriptMethod(
        name: "P1_GravenImage3_FixedSpotsGuide",
        eventType: EventTypeEnum.Tether,
        eventCondition: ["Id:002D"])]
    public void P1_GravenImage3_FixedSpotsGuide(Event evt, ScriptAccessory sa)
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

        var dp = sa.WaypointDp(P1GravenImage3FixedSpot(idx), 5000, 5000, $"P1_GravenImage3_FixedSpot_{idx}");
        dp.Owner = GuidanceOwner(sa, idx);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    /// <summary>In = boss max melee 7.5m, out = 17m, between the arrow and the wall</summary>
    private static Vector3 P1GravenImage3FixedSpot(int idx) => idx switch
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

    // 47764 Mystery Magic after Graven Image 3: fire (maybe fake) + real/fake thunder lines + gaze, all resolve at +5.8s
    // Stack: DPS north, supports south, each to the nearest point 6m from the boss that isn't on a real thunder line; spread: stay put, only move to the nearest safe spot if a real thunder line covers you
    // [ScriptMethod(
    //     name: "P1_GravenImage3_FinalFireThunderGuide",
    //     eventType: EventTypeEnum.StartCasting,
    //     eventCondition: ["ActionId:47764"])]
    // public void P1_GravenImage3_FinalFireThunderGuide(Event evt, ScriptAccessory sa)
    // {
    //     if (!IsPhase(_phase, 1.3))
    //         return;

    //     Task.Run(async () =>
    //     {
    //         // Real thunder 47775/47777 casts on the same frame as 47764; wait a moment for them to enter the danger zone list
    //         await Task.Delay(300);

    //         bool isStackCall = stackFlagrantFireIcon != fakeFlagrantFire;
    //         var party = sa.Data.PartyList;
    //         int myIdx = sa.MyIndex();

    //         for (int i = 0; i < party.Count && i < 8; i++)
    //         {
    //             if (!Debugging && i != myIdx)
    //                 continue;

    //             Vector3? from = isStackCall
    //                 ? new Vector3(100, 0, i < 4 ? 106 : 94)
    //                 : sa.Data.Objects.SearchById(party[i])?.Position;
    //             if (from == null)
    //                 continue;

    //             var dp = sa.WaypointDp(P1NearestSafeSpot(from.Value), 5300, 0, $"P1_GravenImage3_FinalFireThunder_{i}");
    //             dp.Owner = GuidanceOwner(sa, i);
    //             sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    //         }

    //         sa.Debug($"Graven Image 3 final fire/thunder: stack={isStackCall}");
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

    #region Utilities

    private readonly record struct Statue3ArrowPlan(int Dir4, Vector2 FirstRelative, Vector2 SecondRelative, string Callout);

    // Arrow facing: 0 N 1 W 2 S 3 E (both ID sets share icons and facings)
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

    // LPDU clockwise box (box side 24m, waymarks on the cardinals and the corners):
    // Two arrows the same way: drop on that side's cardinal first, then one step (6m) counterclockwise
    // Two different arrows: drop on "one step clockwise of the cardinal + the corner"; the turn-out one (5079~5082 set) goes on the corner, whichever expires first is dropped first
    private static Statue3ArrowPlan GetStatue3ArrowPlan(bool sameBuff, bool smallIsShort, int smallDir4, int largeDir4)
    {
        if (sameBuff)
        {
            int side = (Math.Max(smallDir4, largeDir4) + 1) % 4;
            return new Statue3ArrowPlan(side, new Vector2(0, -12), new Vector2(-6, -12), $"{CardinalDirText(side)} cardinal, drop both there");
        }

        int dir4 = (largeDir4 + 2) % 4;
        return new Statue3ArrowPlan(
            dir4,
            smallIsShort ? new Vector2(6, -12) : new Vector2(12, -12),
            smallIsShort ? new Vector2(12, -12) : new Vector2(6, -12),
            $"{DiagonalClockwiseDirText(dir4)}, {(smallIsShort ? "side spot first, then corner" : "corner first, then side spot")}");
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
            0 => "North",
            1 => "West",
            2 => "South",
            3 => "East",
            _ => "Unknown",
        };
    }

    private static string DiagonalClockwiseDirText(int dir4)
    {
        return ((dir4 % 4 + 4) % 4) switch
        {
            0 => "NE",
            1 => "NW",
            2 => "SW",
            3 => "SE",
            _ => "Unknown",
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

    #region P2 Draws & Guidance (Cicero)

    #region Major_Phase_2

    [ScriptMethod(name: "———————— [P2] ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"])]
    public void P2_Separator(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name:"P2 (Phase Control)",
        eventType:EventTypeEnum.PlayActionTimeline,
        eventCondition:["Id:4565"],
        userControl:false)]

    public void P2_PhaseControl(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"P2 Ultimate Embrace (AoE)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:49740"])]

    public void P2_UltimateEmbrace_Aoe(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"P2 Forsaken (Phase Control)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:47804"],
        userControl:false)]

    public void P2_Forsaken_PhaseControl(Event @event,ScriptAccessory accessory) {

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

    // Fixed buddies MT+H1 / OT+H2 / M1+R1 / M2+R2: the two pairs with a stack are group A (towers on sets 1/2/3/8), the other two pairs are group B (sets 4~7)
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

    [ScriptMethod(name:"P2 Forsaken Spell's Trouble (Data Collection)",
        eventType:EventTypeEnum.TargetIcon,
        eventCondition:["Id:regex:^(02CD|02CC|02CB)$"],
        userControl:false)]

    public void P2_Forsaken_SpellsTrouble_DataCollection(Event @event,ScriptAccessory accessory) {

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

    // Towers spawning: EnvControl Index 1~8, Flag 1 or 2 (the two towers of one set can differ, set 1 is often both 1); only accepting 2 misses towers and kills the whole set's guidance
    [ScriptMethod(name:"P2 Forsaken Towers (Data Collection)",
        eventType:EventTypeEnum.EnvControl,
        eventCondition:["Flag:regex:^(1|2)$"],
        userControl:false)]

    public void P2_Forsaken_Towers_DataCollection(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"P2 Forsaken (Guidance, Set 1)",
        eventType:EventTypeEnum.ActionEffect,
        eventCondition:["ActionId:47804"],
        suppress:COMMON_INTERVAL)]

    public void P2_Forsaken_Guide_Set1(Event @event,ScriptAccessory accessory) {

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
        P2_Forsaken_Guide(1,accessory);

    }

    [ScriptMethod(name:"P2 Forsaken (Guidance, Sets 2-8)",
        eventType:EventTypeEnum.ActionEffect,
        eventCondition:["ActionId:47806"])]

    public void P2_Forsaken_Guide_Sets2To8(Event @event,ScriptAccessory accessory) {

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

            accessory.Method.RemoveDraw($"P2_Forsaken_AllThingsEndingGuide1_Set{lastRound}(_\\d)?");
            accessory.Method.RemoveDraw($"P2_Forsaken_AllThingsEndingGuide2_Set{lastRound}");
            accessory.Method.RemoveDraw($"P2_Forsaken_AllThingsEndingGuide3_Set{lastRound}(_\\d)?");
            accessory.Method.RemoveDraw($"P2_Forsaken_GuideHelperLine1_Set{lastRound}");
            accessory.Method.RemoveDraw($"P2_Forsaken_GuideHelperLine2_Set{lastRound}");
            accessory.Method.RemoveDraw($"P2_Forsaken_Guide_Set{lastRound}(_\\d)?");

            if(lastRound==8) {

                P2_Forsaken_Guide_Final(accessory);

                return;

            }

            P2_Forsaken_Guide(lastRound+1,accessory);

        }

    }

    private void P2_Forsaken_Guide_Final(ScriptAccessory accessory) {
        int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
        var currentProperties=accessory.Data.GetDefaultDrawProperties();
        for(int partyIndex=0;partyIndex<8;++partyIndex) {
            if(!Debugging&&partyIndex!=myIndex) {
                continue;
            }
            currentProperties=accessory.Data.GetDefaultDrawProperties();
            currentProperties.Name=$"P2_Forsaken_AllThingsEndingGuide1_Final_{partyIndex}";
            currentProperties.Scale=new(2);
            currentProperties.Owner=GuidanceOwner(accessory,partyIndex);
            currentProperties.TargetPosition=PHASE2_SUB2_RAW_TOWER_POSITION;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=MAXIMUM_DURATION;
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
        }
        currentProperties=accessory.Data.GetDefaultDrawProperties();

        currentProperties.Name="P2_Forsaken_AllThingsEndingGuide2_Final";
        currentProperties.Scale=new(20);
        currentProperties.Radian=((float)(convertDegreesToRadians(5)));
        currentProperties.Position=ARENA_CENTER;
        currentProperties.TargetPosition=PHASE2_SUB2_RAW_TOWER_POSITION;
        currentProperties.Color=accessory.Data.DefaultSafeColor;
        currentProperties.DestoryAt=MAXIMUM_DURATION;

        accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Fan,currentProperties);

        phase2sub2_allThingsEndingSemaphore.WaitOne(6375+COMMON_INTERVAL);

        accessory.Method.RemoveDraw("P2_Forsaken_AllThingsEndingGuide1_Final(_\\d)?");
        accessory.Method.RemoveDraw("P2_Forsaken_AllThingsEndingGuide2_Final");
        for(int partyIndex=0;partyIndex<8;++partyIndex) {
            if(!Debugging&&partyIndex!=myIndex) {
                continue;
            }
            currentProperties=accessory.Data.GetDefaultDrawProperties();
            currentProperties.Name=$"P2_Forsaken_AllThingsEndingGuide3_Final_{partyIndex}";
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

    // ----- LPDU Buddies: https://raidplan.io/plan/142oXOZpPc_jh3dd , exact positions: https://raidplan.io/plan/A0xlCay835pm5qxs -----
    // Buddies MT+H1 / OT+H2 / M1+R1 / M2+R2, the two pairs with a stack are group A, towers go AAABBBBA; ranged (H/R) spots are fixed, T/melee adjust around their buddy
    // Spots are written in a local frame with "this set's two towers in the south" (x east positive, z south positive, relative to the middle): facing the boss, left tower (SW) = cone tower, right tower (SE) = circle tower
    private static readonly Vector2 P2LeftTower=new(-5.657f,5.657f);
    private const float P2DefaultHitbox=6;

    private void P2_Forsaken_Guide(int currentRound,ScriptAccessory accessory) {

        if(currentRound<1||currentRound>8) {

            return;

        }

        int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);

        if(!isLegalPartyIndex(myIndex)) {

            return;

        }

        float hitbox=P2BossHitbox(accessory);

        // Each player is solved from their own group, head icon and role; Debug draws the whole party
        var positions=new Dictionary<int,Vector3>();
        for(int partyIndex=0;partyIndex<8;++partyIndex) {
            if(!Debugging&&partyIndex!=myIndex) {
                continue;
            }
            var position=P2_Forsaken_Position(currentRound,partyIndex,hitbox,accessory);
            if(position!=null) {
                positions[partyIndex]=position.Value;
            }
        }
        if(positions.Count==0) {
            return;
        }
        var currentProperties=accessory.Data.GetDefaultDrawProperties();

        // Sets 3/5/7 first bait the previous set's Past's/Future's End (set 1 has none)
        if(currentRound%2==1&&currentRound>1) {

            Vector3 baitPosition=PHASE2_SUB2_RAW_TOWER_POSITION;

            if(isFuturesEnd) {

                baitPosition=rotatePosition(PHASE2_SUB2_RAW_TOWER_POSITION,ARENA_CENTER,Math.PI);

            }

            baitPosition=rotatePosition(baitPosition,ARENA_CENTER,Math.PI/4*phase2sub2_discretizedTowerGap[currentRound]);

            foreach(var (partyIndex,myPosition) in positions) {
                currentProperties=accessory.Data.GetDefaultDrawProperties();
                currentProperties.Name=$"P2_Forsaken_AllThingsEndingGuide1_Set{currentRound}_{partyIndex}";
                currentProperties.Scale=new(2);
                currentProperties.Owner=GuidanceOwner(accessory,partyIndex);
                currentProperties.TargetPosition=baitPosition;
                currentProperties.ScaleMode|=ScaleMode.YByDistance;
                currentProperties.Color=accessory.Data.DefaultSafeColor;
                currentProperties.DestoryAt=MAXIMUM_DURATION;
                accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
            }
            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Name=$"P2_Forsaken_AllThingsEndingGuide2_Set{currentRound}";
            currentProperties.Scale=new(20);
            currentProperties.Radian=((float)(convertDegreesToRadians(5)));
            currentProperties.Position=ARENA_CENTER;
            currentProperties.TargetPosition=baitPosition;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=MAXIMUM_DURATION;

            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Fan,currentProperties);

            foreach(var (partyIndex,myPosition) in positions) {
                currentProperties=accessory.Data.GetDefaultDrawProperties();
                currentProperties.Name=$"P2_Forsaken_AllThingsEndingGuide3_Set{currentRound}_{partyIndex}";
                currentProperties.Scale=new(2);
                currentProperties.Position=baitPosition;
                currentProperties.TargetPosition=myPosition;
                currentProperties.ScaleMode|=ScaleMode.YByDistance;
                currentProperties.Color=accessory.Data.DefaultDangerColor;
                currentProperties.DestoryAt=MAXIMUM_DURATION;
                accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
            }
            phase2sub2_allThingsEndingSemaphore.WaitOne(6375+COMMON_INTERVAL);
            accessory.Method.RemoveDraw($"P2_Forsaken_AllThingsEndingGuide1_Set{currentRound}(_\\d)?");
            accessory.Method.RemoveDraw($"P2_Forsaken_AllThingsEndingGuide2_Set{currentRound}");
            accessory.Method.RemoveDraw($"P2_Forsaken_AllThingsEndingGuide3_Set{currentRound}(_\\d)?");
        }

        if(currentRound%2==0&&phase2sub2_spellwaveAuxiliaryLines) {

            Vector2 cone=P2EvenSetLeftTower().cone;
            Vector3[] fanPositions=[P2LocalToWorld(cone,currentRound),P2LocalToWorld(new Vector2(-cone.X,cone.Y),currentRound)];

            for(int i=0;i<2;++i) {

                currentProperties=accessory.Data.GetDefaultDrawProperties();

                currentProperties.Name=$"P2_Forsaken_GuideHelperLine{i+1}_Set{currentRound}";
                currentProperties.Scale=new(0.5f);
                currentProperties.Position=fanPositions[i];
                currentProperties.Color=phase2sub2_colourOfAuxiliaryLines.V4.WithW(1);
                currentProperties.DestoryAt=MAXIMUM_DURATION;

                accessory.Method.SendDraw(getDrawModeEnum(DrawTypeEnum.Circle),DrawTypeEnum.Circle,currentProperties);

            }

        }

        foreach(var (partyIndex,myPosition) in positions) {
            currentProperties=accessory.Data.GetDefaultDrawProperties();
            currentProperties.Name=$"P2_Forsaken_Guide_Set{currentRound}_{partyIndex}";
            currentProperties.Scale=new(2);
            currentProperties.Owner=GuidanceOwner(accessory,partyIndex);
            currentProperties.TargetPosition=myPosition;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=currentRound==1?10750:MAXIMUM_DURATION;
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
        }
    }

    private Vector3? P2_Forsaken_Position(int currentRound,int myIndex,float hitbox,ScriptAccessory accessory) {

        bool towerIsGroupA=phase2sub2_isGroupARound[currentRound];
        List<int> towerGroup=towerIsGroupA?phase2sub2_groupA:phase2sub2_groupB;
        List<int> helperGroup=towerIsGroupA?phase2sub2_groupB:phase2sub2_groupA;
        bool inTower=towerGroup.Contains(myIndex);

        if(!inTower&&!helperGroup.Contains(myIndex)) {

            return null;

        }

        Vector2? relative=(currentRound%2==1)
            ?P2OddSetSpot(myIndex,inTower,currentRound==1)
            :P2EvenSetSpot(myIndex,inTower,towerGroup,hitbox);

        if(enableDebugLogging) {

            accessory.Log.Debug($"""
                                 currentRound={currentRound}
                                 myIndex={myIndex}, inTower={inTower}
                                 phase2sub2_iconType[{myIndex}]={phase2sub2_iconType[myIndex]}
                                 relative={relative}, hitbox={hitbox}
                                 """);

        }

        return relative==null?null:P2LocalToWorld(relative.Value,currentRound);

    }

    // The local frame has the towers in the south, the old discrete regions had towers-in-the-north as 0, so rotate an extra half turn
    private Vector3 P2LocalToWorld(Vector2 relative,int currentRound) {

        return rotatePosition(new Vector3(100+relative.X,0,100+relative.Y),ARENA_CENTER,Math.PI/4*phase2sub2_discretizedTowerGap[currentRound]+Math.PI);

    }

    // Odd sets (2 stacks 1 cone 1 circle, stack 5m):
    // Cone tower = supports' stack (in the tower, boss side) + cone (southmost point of the tower, slightly right); circle tower = DPS stack (max melee on the tower's left) + circle (eastmost point of the tower)
    // Both buddies have stacks: the ranged stays on their own role's side, T/melee swaps to the other side
    // PF strat only changes set 1: stacks take the tower with their buddy (buddy cone → cone tower, buddy circle → circle tower), later sets same as LPDU
    // Helpers are fixed: T inside the boss hitbox in front of the cone tower (takes the supports' stack), H directly south of the cone player to bait the cone; the two DPS between the towers, toward the circle tower, take the DPS stack
    private Vector2? P2OddSetSpot(int idx,bool inTower,bool firstRound) {

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

                // idx^2 = fixed buddy (0↔2, 1↔3, 4↔6, 5↔7)
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

    // Even sets (2 cones 2 circles): each tower has the cone in front and the circle behind
    // H + melee take the left tower, ranged + T the right tower; if melee and H have the same icon, melee swaps with T
    // Helpers are fixed: T NW / melee NE on the boss's outer hitbox ring to bait the clones (the four clones target the closest 4 = the two cones + these two),
    // H / ranged stand outside the cone players to bait the cones, which fire out east and west
    private Vector2? P2EvenSetSpot(int idx,bool inTower,List<int> towerGroup,float hitbox) {

        var (cone,circle,baiter)=P2EvenSetLeftTower();

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
    /// Even-set left tower (tower center 8m from the middle, radius 4m, pulled in 0.4m): with the midpoint of the two towers facing the boss as 12 o'clock,
    /// the cone spot is at the tower's 1 o'clock (pulled toward the middle), the circle spot is across the tower center at 7 o'clock (outward); the caller mirrors it to 11 / 5 o'clock for the right tower.
    /// Cone bait spot = 5.5m due west of the cone spot (Spellscatter 47809 circle 5m + 0.5m margin), the cone fires out west
    /// </summary>
    private static (Vector2 cone,Vector2 circle,Vector2 baiter) P2EvenSetLeftTower() {

        const float towerRadius=3.6f,clock=MathF.PI/6,baitDistance=5+0.5f;

        var offset=new Vector2(MathF.Sin(clock),-MathF.Cos(clock))*towerRadius;
        var cone=P2LeftTower+offset;

        return (cone,P2LeftTower-offset,cone-new Vector2(baitDistance,0));

    }

    // P2 Kefka (19506) hitbox radius; fall back to 6m if it can't be read or is out of range
    private static float P2BossHitbox(ScriptAccessory accessory) {

        float radius=accessory.GetByDataId(19506u).FirstOrDefault()?.HitboxRadius??0;
        return radius is > 4.5f and < 11f?radius:P2DefaultHitbox;

    }

    [ScriptMethod(name:"P2 Forsaken All Things Ending Data Collection",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:regex:^(47836|47837)$"],
        userControl:false)]

    public void P2_Forsaken_AllThingsEnding_DataCollection(Event @event,ScriptAccessory accessory) {

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

            case Phase2Sub2_DrawingTypes.Default: {

                return DrawModeEnum.Default;

            }

            case Phase2Sub2_DrawingTypes.AllVfx: {

                return DrawModeEnum.Vfx;

            }

            case Phase2Sub2_DrawingTypes.AuthorRecommended_CirclesImGui_RestVfx: {

                if(currentType==DrawTypeEnum.Circle) {

                    return DrawModeEnum.Imgui;

                }

                else {

                    return DrawModeEnum.Vfx;

                }

            }

            case Phase2Sub2_DrawingTypes.AllImGui: {

                return DrawModeEnum.Imgui;

            }

            default: {

                return DrawModeEnum.Default;

            }

        }

    }

    [ScriptMethod(name:"P2 Forsaken Spell's Trouble (AoE)",
        eventType:EventTypeEnum.EnvControl,
        eventCondition:["Flag:8"])]

    public void P2_Forsaken_SpellsTrouble_Aoe(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"P2 Forsaken (Tower Helper Lines)",
        eventType:EventTypeEnum.EnvControl,
        eventCondition:["Flag:regex:^(1|2)$"])]

    public void P2_Forsaken_TowerHelperLines(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"P2 Forsaken Past's End & Future's End (Bait AoE)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:regex:^(47826|47827)$"])]

    public void P2_Forsaken_PastAndFuturesEnd_BaitAoe(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"P2 Forsaken Past's End & Future's End (Data Collection)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:regex:^(47826|47827)$"],
        userControl:false)]

    public void P2_Forsaken_PastAndFuturesEnd_DataCollection(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"P2 Forsaken All Things Ending (Bait AoE)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:regex:^(47826|47827)$"])]

    public void P2_Forsaken_AllThingsEnding_BaitAoe(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"P2 Forsaken All Things Ending (AoE)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:regex:^(47836|47837)$"])]

    public void P2_Forsaken_AllThingsEnding_Aoe(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"P2 Post-Forsaken Light of Judgment (Phase Control)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:47805"],
        userControl:false)]

    public void P2_PostForsaken_LightOfJudgment_PhaseControl(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"P2 Post-Forsaken Wings of Destruction (Single Wing AoE)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:regex:^(47821|47822)$"])]

    public void P2_PostForsaken_WingsOfDestruction_SingleWingAoe(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"P2 Post-Forsaken Wings of Destruction (Double Wing AoE)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:50311"])]

    public void P2_PostForsaken_WingsOfDestruction_DoubleWingAoe(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"P2 Post-Forsaken Trine (Data Collection & AoE)",
        eventType:EventTypeEnum.ObjectChanged,
        eventCondition:["DataId:regex:^(2015154|2015155)$"])]

    public void P2_PostForsaken_Trine_DataCollectionAndAoe(Event @event,ScriptAccessory accessory) {

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

    // ----- LPDU Trines: https://raidplan.io/plan/apkh6ytq72w8pt3v (Party N CW, Tanks NW CCW) -----
    // 7 triangles land in 3 sets of 3/1/3; ~11.8s after each set lands, 6m circles explode on its three points (47840); the middle one is always in set 3
    // The 6 outer positions are fixed: the k-th (0~5 clockwise from north) has its centroid at bearing 60k+10.9°, 15.28m from the middle, points at (60k°,10m), (60k°,20m), (60k+30°,17.32m)
    //   The NW one's look doesn't match its explosion, so the outer ones are computed from position, not DataId; the middle one uses DataId: 2015154 points east, 2015155 points west
    // Everyone starts in the middle and stands on the safe half for the Wings of Destruction cleave (47821/47822); after set 1 explodes:
    //   the party goes into the first set-1 triangle "clockwise from north", the tanks into the first set-1 triangle "counterclockwise from NW"
    //   OT far: the arena edge of the tank triangle; MT near: a gap of the middle triangle (between two points, 7.5m from the middle),
    //   picking the gap whose neighbouring outer triangles aren't in set 3 and that's closest to the tank triangle; if a set-2 triangle is next to it, wait inside the tank triangle until it blows up
    // Near/far tankbuster 47823 (7m) targets the closest/farthest when set 3 explodes
    private readonly object _trineLock=new();
    private readonly List<(Vector3 pos,uint dataId)> _trines=new();
    private long _trineLanded=0;
    private float? _trineSafeHalf=null; // Game rotation (radians) of the cleave's safe side

    // Relative to set 1 landing (measured from logs)
    private const int TRINE_Set1Explosion=11785,TRINE_Set2Explosion=13835,TRINE_Tankbuster=16370;

    private static Vector3 TrinePoint(float bearingDegrees,float radius) {

        float rad=bearingDegrees*MathF.PI/180;
        return new Vector3(100+radius*MathF.Sin(rad),0,100-radius*MathF.Cos(rad));

    }

    // 47821 hits boss facing +90°, 47822 hits -90° (same as the single wing draw above), the safe side is the opposite
    [ScriptMethod(name:"P2 Trine Wings of Destruction (Data Collection)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:regex:^(47821|47822)$"],
        userControl:false)]

    public void P2_Trine_WingsOfDestruction_DataCollection(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=2||phase!=3) {

            return;

        }

        lock(_trineLock) {

            _trineSafeHalf=@event.SourceRotation()+(@event.ActionId()==47821?-1:1)*MathF.PI/2;

        }

    }

    [ScriptMethod(name:"P2 Trine (Guidance)",
        eventType:EventTypeEnum.ObjectChanged,
        eventCondition:["DataId:regex:^(2015154|2015155)$","Operate:Add"])]

    public void P2_Trine_Guide(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=2||phase!=3) {

            return;

        }

        List<(Vector3 pos,uint dataId)> trines;
        long landed;
        float? safeSide;

        lock(_trineLock) {

            if(_trines.Count>=7) {

                return;

            }

            if(_trines.Count==0) {

                _trineLanded=Environment.TickCount64;

            }

            _trines.Add((@event.SourcePosition(),@event.DataId()));

            if(_trines.Count<7) {

                return;

            }

            trines=[.._trines];
            landed=_trineLanded;
            safeSide=_trineSafeHalf;

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

            accessory.Log.Debug($"P2 Trine: unknown layout, no guidance.{string.Join(" ",trines)}");
            return;

        }

        int partyRing=set1.Min();
        int tankRing=set1.Max();
        float tankBearing=60*tankRing+10.9f;

        // The middle triangle's gaps are opposite its points: pointing east (2015154) points at 90/210/330°, gaps at 270/30/150°
        int[] gaps=center.dataId==2015154?[270,30,150]:[90,210,330];
        int[] flanks(int gap)=>[(gap-30)/60%6,(gap+30)/60%6];
        int gap=gaps.Where(g=>!flanks(g).Any(set3.Contains))
            .OrderBy(g=>MathF.Abs(((g-tankBearing)%360+540)%360-180))
            .First();
        bool nearWaits=set2.Any(flanks(gap).Contains);

        Vector3 mid=safeSide is { } a?new Vector3(100+3*MathF.Sin(a),0,100+3*MathF.Cos(a)):ARENA_CENTER;
        Vector3 partySpot=TrinePoint(60*partyRing+10.9f,15.28f);
        Vector3 farSpot=TrinePoint(tankBearing,19);
        Vector3 nearWait=TrinePoint(tankBearing,11);
        Vector3 nearSpot=TrinePoint(gap,7.5f);

        long now=Environment.TickCount64;
        uint until(int ms)=>(uint)Math.Max(0,landed+ms-now);
        uint boom1=until(TRINE_Set1Explosion),boom2=until(TRINE_Set2Explosion),end=until(TRINE_Tankbuster)+300;

        accessory.Log.Debug($"P2 Trine: set 1={string.Join(",",set1)} set 2={string.Join(",",set2)} set 3={string.Join(",",set3)}+middle:{center.dataId}, party={partyRing} tanks={tankRing} near-TB gap={gap}°{(nearWaits?" (wait for set 2 first)":"")}");

        int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);

        for(int i=0;i<accessory.Data.PartyList.Count&&i<8;++i) {

            if(!Debugging&&i!=myIndex) {

                continue;

            }

            Vector3 target=i switch { 0 => nearWaits?nearWait:nearSpot, 1 => farSpot, _ => partySpot };

            var dp=accessory.WaypointDp(mid,boom1,0,$"P2_Trine_Middle_{i}");
            dp.Owner=GuidanceOwner(accessory,i);
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,dp);

            dp=accessory.WaypointFromToDp(mid,target,boom1,0,$"P2_Trine_Preview_{i}",new Vector4(1,1,1,1));
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,dp);

            uint toTarget=i==0&&nearWaits?boom2:end;
            dp=accessory.WaypointDp(target,toTarget-boom1,boom1,$"P2_Trine_Triangle_{i}");
            dp.Owner=GuidanceOwner(accessory,i);
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,dp);

            if(i==0&&nearWaits) {

                dp=accessory.WaypointDp(nearSpot,end-boom2,boom2,$"P2_Trine_Gap_{i}");
                dp.Owner=GuidanceOwner(accessory,i);
                accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,dp);

            }

        }

    }

    #endregion

    #endregion

    #region P3 Draws (Cicero)

    #region Major_Phase_3

    [ScriptMethod(name:"P3 (Phase Control)",
        eventType:EventTypeEnum.PlayActionTimeline,
        eventCondition:["Id:3218"],
        userControl:false)]

    public void P3_PhaseControl(Event @event,ScriptAccessory accessory) {

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

    #region P3 Draws & Guidance (Usami)

    private const string DebugVersion = "a";
    private int _runId = 0;
    private static readonly List<string> Role = ["MT", "OT", "H1", "H2", "M1", "M2", "R1", "R2"];
    private static readonly Vector3 Center = new Vector3(100, 0, 100);
    private static readonly Vector3 CrystalBasePos = new Vector3(109.9f, 0, 109.9f);
    private static readonly Vector3 BlackHoleBasePos = new Vector3(100f, 0, 83f);
    private UDMP3Params _udmP3Param = new UDMP3Params();
    private PriorityDict _pd = new PriorityDict();

    // LPDU P3A spot distances (wind crystal = north): the spots outside the wind/fire/water crystals must dodge middle Exdeath's 14.8m Thunder III chariot
    private const float P3AOuterRingDistance = 15.5f;
    private const float P3ARangedDistance = 16f;

    // LPDU P3B tether spots: at Chaos's (always in the middle) max melee, rotated clockwise from the black hole's direction, pulling the tether into a U; the last tether stands at the edge so the laser points out
    private const float TetherSpotRadius = 6f;
    private const float TetherSpotCwAngle = 65f;
    private const float LastTetherSpotRadius = 17f;
    private const float LastTetherCwAngle = 45f;

    private void Init_Usami(ScriptAccessory sa)
    {
        _runId++;
        sa.DebugMsg($"Script {Name} v{Version}{DebugVersion} initialized, _runId {_runId}");

        _pd.Init(sa, "Init");

        _udmP3Param.Reset(sa);
        sa.Method.RemoveDraw(".*");
        sa.Method.ClearFrameworkUpdateAction(this);
    }

    /// <summary>Players to guide: whole party in Debug, otherwise only me</summary>
    private static IEnumerable<int> GuidedPlayers(ScriptAccessory sa)
    {
        var myIndex = sa.GetMyIndex();
        for (var i = 0; i < Math.Min(8, sa.Data.PartyList.Count); i++)
            if (Debugging || i == myIndex)
                yield return i;
    }

    /// <summary>When guiding the whole party, only my own share gets callouts</summary>
    private static bool IsMe(ScriptAccessory sa, int idx) => idx == sa.GetMyIndex();

    [ScriptMethod(name: "———————— [P3] ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"])]
    public void P3_Separator(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "P3_Subphase_DefinitionOfInsanity", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47842"],
        userControl: Debugging)]
    public void P3_Subphase_DefinitionOfInsanity(Event ev, ScriptAccessory sa)
    {
        _udmP3Param.CurrentStage = 3000;
        sa.DebugMsg($"Current stage: P3 Definition of Insanity {_udmP3Param.CurrentStage}");
    }

    [ScriptMethod(name: "*P3B_RestoreKefkaOpacityOnDefinitionOfInsanity", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47842)$"], userControl: Debugging)]
    public void P3B_RestoreKefkaOpacityOnDefinitionOfInsanity(Event ev, ScriptAccessory sa)
    {
        var obj = sa.GetById(ev.SourceId);
        sa.AlphaModify(obj, 1f, currentAlpha => currentAlpha <= 0.8f);
    }

    [ScriptMethod(name: "P3_GetExdeathObjId", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:49891"],
        userControl: Debugging)]
    public void P3_GetExdeathObjId(Event ev, ScriptAccessory sa)
    {
        _udmP3Param.ObjectId_Exdeath = ev.SourceId;
        sa.DebugMsg($"{_udmP3Param.ObjectId_Exdeath:X8}");

        // Restore opacity while we're at it
        var obj = sa.GetById(ev.SourceId);
        sa.AlphaModify(obj, 1f, currentAlpha => currentAlpha <= 0.8f);
    }

    [ScriptMethod(name: "P3_GetChaosObjId", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:49890"],
        userControl: Debugging)]
    public void P3_GetChaosObjId(Event ev, ScriptAccessory sa)
    {
        _udmP3Param.ObjectId_Chaos = ev.SourceId;
        sa.DebugMsg($"{_udmP3Param.ObjectId_Chaos:X8}");

        // Restore opacity while we're at it
        var obj = sa.GetById(ev.SourceId);
        sa.AlphaModify(obj, 1f, currentAlpha => currentAlpha <= 0.8f);
    }

    [ScriptMethod(name: "*P3_Firewall_BlockInvalidTargets", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:49890"],
        userControl: true)]
    public void P3_Firewall_BlockInvalidTargets(Event ev, ScriptAccessory sa)
    {
        if (!p3FirewallBlockInvalidTargets) return;
        if (_udmP3Param.FirewallEnabled) return;

        _udmP3Param.FirewallEnabled = true;
        _udmP3Param.FirewallTargetFramework = sa.Method.RegistFrameworkUpdateAction(Action);
        const uint chaos_buff = 4192;
        const uint exdeath_buff = 4194;

        const int FightingChaos = 1;
        const int FightingExdeath = 2;
        const int FightingBoth = 3;
        const int FightingNone = 4;

        void Action()
        {
            var myObject = sa.Data.MyObject;
            if (myObject is null) return;

            int currentState = (myObject.HasStatus(chaos_buff), myObject.HasStatus(exdeath_buff)) switch
            {
                (true, false) => FightingChaos,
                (false, true) => FightingExdeath,
                (false, false) => FightingNone,
                _ => FightingBoth
            };

            if (sa.GetMyIndex() <= 1)
            {
                // Tank jobs don't take part
                currentState = FightingBoth;
            }

            if (currentState == _udmP3Param.LastFirewallState && _udmP3Param.LastFirewallState != 0) return;
            _udmP3Param.LastFirewallState = currentState;

            switch (currentState)
            {
                case FightingChaos:
                    sa.SetTargetable(sa.GetById(_udmP3Param.ObjectId_Chaos), true);
                    sa.SetTargetable(sa.GetById(_udmP3Param.ObjectId_Exdeath), false);
                    break;
                case FightingExdeath:
                    sa.SetTargetable(sa.GetById(_udmP3Param.ObjectId_Chaos), false);
                    sa.SetTargetable(sa.GetById(_udmP3Param.ObjectId_Exdeath), true);
                    break;
                default:
                    sa.SetTargetable(sa.GetById(_udmP3Param.ObjectId_Chaos), true);
                    sa.SetTargetable(sa.GetById(_udmP3Param.ObjectId_Exdeath), true);
                    break;
            };
        }
    }

    [ScriptMethod(name: "P3_DisableFirewallBeforeEnrage", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:47878", "TargetIndex:1"], userControl: Debugging)]
    public void P3_DisableFirewallBeforeEnrage(Event ev, ScriptAccessory sa)
    {
        sa.SetTargetable(sa.GetById(_udmP3Param.ObjectId_Chaos), true);
        sa.SetTargetable(sa.GetById(_udmP3Param.ObjectId_Exdeath), true);
        _udmP3Param.FirewallEnabled = false;
    }

    [ScriptMethod(name: "P3_DetermineMtOtByBuff", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(419[24])$"],
        userControl: Debugging)]
    public void P3_DetermineMtOtByBuff(Event ev, ScriptAccessory sa)
    {
        const uint chaos_buff = 4192;
        const uint exdeath_buff = 4194;

        var idx = sa.GetPlayerIdIndex((uint)ev.TargetId);
        if (idx is < 0 or >= 2) return;

        var statusId = ev.StatusId;

        _udmP3Param.MtTanksExdeath = idx switch
        {
            0 => statusId == exdeath_buff,
            1 => statusId == chaos_buff
        };
        sa.DebugMsg(
            $"{sa.GetPlayerJobByIndex(idx)} got {statusId}, Exdeath tank is {(_udmP3Param.MtTanksExdeath ? "MT" : "OT")}");
    }

    #region P3A Bowels of Agony Common

    [ScriptMethod(name: "============= [P3A Bowels of Agony] =============", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P3A_Separator(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "P3A_SubphaseStart_BowelsOfAgony", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47858"],
        userControl: Debugging)]
    public void P3A_SubphaseStart_BowelsOfAgony(Event ev, ScriptAccessory sa)
    {
        _pd.Init(sa, "P3A");
        _pd.AddPriorities([1, 2, 3, 4, 5, 6, 7, 8]);
        _udmP3Param.CurrentStage = 3100;
        sa.DebugMsg($"Current stage: P3A Bowels of Agony {_udmP3Param.CurrentStage}");
    }

    [ScriptMethod(name: "P3A_RecordStatusAdded", eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:regex:^(160[0123])$", "SourceId:E0000000"],
        userControl: Debugging)]
    public void P3A_StatusAdded(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.CurrentStage != 3100) return;
        const uint Entropy = 1600;
        const uint DynamicFluid = 1601;
        const uint Headwind = 1602;
        const uint Tailwind = 1603;

        lock (_pd)
        {
            var statusId = ev.StatusId;
            var playerIdx = sa.GetPlayerIdIndex((uint)ev.TargetId);
            var priVal = ev.StatusId switch
            {
                Entropy => 100,
                DynamicFluid => 200,
                Headwind => 10,
                Tailwind => 20,
                _ => 0
            };

            _pd.AddPriority(playerIdx, priVal);
            _udmP3Param.P3AStatusRecordCount++;
            sa.DebugMsg($"Recorded status {statusId} / {priVal} on {Role[playerIdx]}");

            if (_udmP3Param.P3AStatusRecordDone())
                _udmP3Param.P3AStatusRecord.Set();

            if (statusId != Entropy) return;
            var statusTime = ev.DurationMilliseconds();
            _udmP3Param.IsLongFire = statusTime > 30000;
        }
    }

    [ScriptMethod(name: "P3A_RecordCrystalSpawns", eventType: EventTypeEnum.ObjectChanged,
        eventCondition: ["Operate:Add", "DataId:regex:^(201529[012])$"],
        userControl: Debugging)]
    public void P3A_RecordCrystalSpawns(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.CurrentStage != 3100) return;
        const uint FireCrystal = 2015290;
        const uint WaterCrystal = 2015291;
        const uint WindCrystal = 2015292;

        var region = ev.SourcePosition.GetRadian(Center).RadianToRegion(4);
        var dataId = ev.DataId();

        switch (dataId)
        {
            case FireCrystal:
                _udmP3Param.FireCrystalRegion = region;
                break;
            case WaterCrystal:
                _udmP3Param.WaterCrystalRegion = region;
                break;
            case WindCrystal:
                _udmP3Param.WindCrystalRegion = region;
                break;
        }
        sa.DebugMsg($"Recorded crystal {dataId} at region {region}");

        if (!_udmP3Param.CrystalRecordDone()) return;
        _udmP3Param.EmptyCrystalRegion = 6 - _udmP3Param.FireCrystalRegion - _udmP3Param.WaterCrystalRegion - _udmP3Param.WindCrystalRegion;
        _udmP3Param.P3ACrystalRecord.Set();
    }

    [ScriptMethod(name: "P3A_ThunderTankbuster_Aoe", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:47881"], userControl: true)]
    public void P3A_ThunderTankbuster_Aoe(Event ev, ScriptAccessory sa)
    {
        var dp = sa.DrawCircle(ev.SourceId, 0, 8000, $"NearestTankbuster", 5f, sa.Data.DefaultDangerColor.WithW(2f), draw: false);
        dp.SetOwnersDistanceOrder(true, 1);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "P3A_ThunderTankbuster_TargetTether", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:47881"], userControl: true)]
    public void P3A_ThunderTankbuster_TargetTether(Event ev, ScriptAccessory sa)
    {
        var color = new Vector4(0.1f, 1f, 1f, 1f);
        var dp = sa.DrawLine(ev.SourceId, 0, 0, 8000, $"ExdeathTankbusterTargetLine", 0, 1, 40, color, byY: true, draw: false);
        dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
        dp.TargetOrderIndex = 1;
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, dp);
    }

    // 47881 Thunder III tankbuster: closest target, two hits. Who takes it and whether to invuln both depends on the party's mit plan, so callout only, no guidance
    [ScriptMethod(name: "P3A_ThunderTankbuster_GeneralCallout", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:47881"], userControl: true)]
    public void P3A_ThunderTankbuster_GeneralCallout(Event ev, ScriptAccessory sa)
    {
        sa.Method.TextInfo($"Exdeath tankbuster on closest", 3000);
        sa.Method.TTS($"Exdeath tankbuster on closest", 3);
    }

    [ScriptMethod(name: "P3A_Implosion", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47869|47870)$"], userControl: true)]
    public void P3A_Implosion_Part1(Event ev, ScriptAccessory sa)
    {
        const uint LatitudinalImplosion = 47870;    // Sides first
        const uint LongitudinalImplosion = 47869;    // Front/back first
        _udmP3Param.ImplosionAoeActive = true;
        _udmP3Param.ImplosionPart2Drawn = false;
        _udmP3Param.ImplosionSidesFirst = ev.ActionId == LatitudinalImplosion;
        _udmP3Param.ObjectId_Chaos = ev.SourceId;

        // Round 1
        sa.DrawFan(ev.SourceId, 0, 10000, $"ImplosionRound1_FrontLeft", 90f.DegToRad(), (_udmP3Param.ImplosionSidesFirst ? 90f : 0f).DegToRad(), 60, 0, sa.Data.DefaultDangerColor.WithW(2f));
        sa.DrawFan(ev.SourceId, 0, 10000, $"ImplosionRound1_BackRight", 90f.DegToRad(), (_udmP3Param.ImplosionSidesFirst ? -90f : 180f).DegToRad(), 60, 0, sa.Data.DefaultDangerColor.WithW(2f));
    }

    [ScriptMethod(name: "P3A_Implosion_Part2AndClear", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:regex:^(47871)$"], suppress: 500, userControl: true)]
    public void P3A_Implosion_Part2AndClear(Event ev, ScriptAccessory sa)
    {
        if (!_udmP3Param.ImplosionAoeActive) return;

        if (_udmP3Param.ImplosionPart2Drawn)
            sa.Method.RemoveDraw($"ImplosionRound2_(FrontLeft|BackRight)");
        else
        {
            sa.Method.RemoveDraw($"ImplosionRound1_(FrontLeft|BackRight)");
            _udmP3Param.ImplosionPart2Drawn = true;
            sa.DrawFan(_udmP3Param.ObjectId_Chaos, 0, 10000, $"ImplosionRound2_FrontLeft", 90f.DegToRad(), (!_udmP3Param.ImplosionSidesFirst ? 90f : 0f).DegToRad(), 60, 0, sa.Data.DefaultDangerColor.WithW(2f));
            sa.DrawFan(_udmP3Param.ObjectId_Chaos, 0, 10000, $"ImplosionRound2_BackRight", 90f.DegToRad(), (!_udmP3Param.ImplosionSidesFirst ? -90f : 180f).DegToRad(), 60, 0, sa.Data.DefaultDangerColor.WithW(2f));
        }
    }

    #region P3A Bowels of Agony AoE & Guidance (Cicero)

    [ScriptMethod(name:"P3 Bowels of Agony (Phase Control)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:47858"],
        userControl:false)]

    public void P3_BowelsOfAgony_PhaseControl(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"P3 Bowels of Agony Crystals (Data Collection)",
        eventType:EventTypeEnum.ObjectChanged,
        eventCondition:["DataId:regex:^(2015290|2015291|2015292)$"],
        userControl:false)]

    public void P3_BowelsOfAgony_Crystals_DataCollection(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"P3 Bowels of Agony Entropy (AoE)",
        eventType:EventTypeEnum.StatusAdd,
        eventCondition:["StatusID:1600"])]

    public void P3_BowelsOfAgony_Entropy_Aoe(Event @event,ScriptAccessory accessory) {

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

        currentProperties.Name=$"P3_BowelsOfAgony_Entropy_Aoe_{targetId}";
        currentProperties.Scale=new(5);
        currentProperties.Owner=targetId;
        currentProperties.Color=accessory.Data.DefaultDangerColor;
        currentProperties.Delay=delay;
        currentProperties.DestoryAt=duration;

        accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Circle,currentProperties);

    }

    [ScriptMethod(name:"P3 Bowels of Agony Inferno (AoE)",
        eventType:EventTypeEnum.StatusAdd,
        eventCondition:["StatusID:1600","SourceId:E0000000"],
        suppress:COMMON_INTERVAL)]

    public void P3_BowelsOfAgony_Inferno_Aoe(Event @event,ScriptAccessory accessory) {

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

        currentProperties.Name=$"P3_BowelsOfAgony_Inferno_Aoe_2";
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

        currentProperties.Name=$"P3_BowelsOfAgony_Inferno_Aoe_1";
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

    [ScriptMethod(name:"P3 Bowels of Agony Entropy & Inferno (Clear AoE)",
        eventType:EventTypeEnum.StatusRemove,
        eventCondition:["StatusID:1600"],
        userControl:false)]

    public void P3_BowelsOfAgony_EntropyAndInferno_ClearAoe(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=3) {

            return;

        }

        if(string.Equals(@event["SourceId"],"00000000")) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        accessory.Method.RemoveDraw($"P3_BowelsOfAgony_Entropy_Aoe_{targetId}");

        lock(phase3sub2_fireCrystalSemaphore) {

            if(phase3sub2_infernoCounter>=1) {

                accessory.Method.RemoveDraw($"P3_BowelsOfAgony_Inferno_Aoe_{phase3sub2_infernoCounter}");

                --phase3sub2_infernoCounter;

            }

        }

    }

    [ScriptMethod(name:"P3 Bowels of Agony Dynamic Fluid (AoE)",
        eventType:EventTypeEnum.StatusAdd,
        eventCondition:["StatusID:1601"])]

    public void P3_BowelsOfAgony_DynamicFluid_Aoe(Event @event,ScriptAccessory accessory) {

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

        currentProperties.Name=$"P3_BowelsOfAgony_DynamicFluid_Aoe_{targetId}";
        currentProperties.Scale=new(10);
        currentProperties.InnerScale=new(5);
        currentProperties.Radian=float.Pi*2;
        currentProperties.Owner=targetId;
        currentProperties.Color=accessory.Data.DefaultDangerColor;
        currentProperties.Delay=delay;
        currentProperties.DestoryAt=duration;

        accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Donut,currentProperties);

    }

    [ScriptMethod(name:"P3 Bowels of Agony Tsunami (AoE)",
        eventType:EventTypeEnum.StatusAdd,
        eventCondition:["StatusID:1601","SourceId:E0000000"],
        suppress:COMMON_INTERVAL)]

    public void P3_BowelsOfAgony_Tsunami_Aoe(Event @event,ScriptAccessory accessory) {

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

        currentProperties.Name=$"P3_BowelsOfAgony_Tsunami_Aoe_2";
        currentProperties.Scale=new(5);
        currentProperties.Position=phase3sub2_waterCrystalPosition;
        currentProperties.CentreResolvePattern=PositionResolvePatternEnum.PlayerNearestOrder;
        currentProperties.CentreOrderIndex=2;
        currentProperties.Color=accessory.Data.DefaultDangerColor;
        currentProperties.Delay=delay;
        currentProperties.DestoryAt=duration+2875;

        accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Circle,currentProperties);

        currentProperties=accessory.Data.GetDefaultDrawProperties();

        currentProperties.Name=$"P3_BowelsOfAgony_Tsunami_Aoe_1";
        currentProperties.Scale=new(5);
        currentProperties.Position=phase3sub2_waterCrystalPosition;
        currentProperties.CentreResolvePattern=PositionResolvePatternEnum.PlayerNearestOrder;
        currentProperties.CentreOrderIndex=1;
        currentProperties.Color=accessory.Data.DefaultDangerColor;
        currentProperties.Delay=delay;
        currentProperties.DestoryAt=duration+2875;

        accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Circle,currentProperties);

    }

    [ScriptMethod(name:"P3 Bowels of Agony Dynamic Fluid & Tsunami (Clear AoE)",
        eventType:EventTypeEnum.StatusRemove,
        eventCondition:["StatusID:1601"],
        userControl:false)]

    public void P3_BowelsOfAgony_DynamicFluidAndTsunami_ClearAoe(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=3) {

            return;

        }

        if(string.Equals(@event["SourceId"],"00000000")) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        accessory.Method.RemoveDraw($"P3_BowelsOfAgony_DynamicFluid_Aoe_{targetId}");

        lock(phase3sub2_waterCrystalSemaphore) {

            if(phase3sub2_tsunamiCounter>=1) {

                accessory.Method.RemoveDraw($"P3_BowelsOfAgony_Tsunami_Aoe_{phase3sub2_tsunamiCounter}");

                --phase3sub2_tsunamiCounter;

            }

        }

    }

    [ScriptMethod(name:"P3 Bowels of Agony Umbra Smash (Bait Indicator)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:regex:^(47870|47869)$"])]

    public void P3_BowelsOfAgony_UmbraSmash_BaitIndicator(Event @event,ScriptAccessory accessory) {

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

        currentProperties.Name="P3_BowelsOfAgony_UmbraSmash_BaitIndicator1";
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

        currentProperties.Name="P3_BowelsOfAgony_UmbraSmash_BaitIndicator2";
        currentProperties.Scale=new(20);
        currentProperties.Owner=sourceId;
        currentProperties.CentreResolvePattern=PositionResolvePatternEnum.PlayerFarestOrder;
        currentProperties.CentreOrderIndex=1;
        currentProperties.Color=colourOfDirectionIndicators.V4.WithW(1);
        currentProperties.Delay=9875;
        currentProperties.DestoryAt=MAXIMUM_DURATION;

        accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,currentProperties);

    }

    [ScriptMethod(name:"P3 Bowels of Agony Umbra Smash (Clear Bait Indicator)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:47872"],
        userControl:false)]

    public void P3_BowelsOfAgony_UmbraSmash_ClearBaitIndicator(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=3) {

            return;

        }

        accessory.Method.RemoveDraw("P3_BowelsOfAgony_UmbraSmash_BaitIndicator1");
        accessory.Method.RemoveDraw("P3_BowelsOfAgony_UmbraSmash_BaitIndicator2");

    }

    [ScriptMethod(name:"P3 Bowels of Agony Umbra Smash (Landing Indicator)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:47872"])]

    public void P3_BowelsOfAgony_UmbraSmash_LandingIndicator(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"P3 Bowels of Agony Ultima Blaster (Data Collection)",
        eventType:EventTypeEnum.ActionEffect,
        eventCondition:["ActionId:47843"])]

    public void P3_BowelsOfAgony_UltimaBlaster_DataCollection(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"P3 Bowels of Agony Ultima Blaster (AoE)",
        eventType:EventTypeEnum.TargetIcon,
        eventCondition:["Id:regex:^(0150|0151|0152|0153|01B5|01B6|01B7|01B8)$"])]

    public void P3_BowelsOfAgony_UltimaBlaster_Aoe(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"P3 Bowels of Agony Ultima Blaster (Guidance)",
        eventType:EventTypeEnum.TargetIcon,
        eventCondition:["Id:regex:^(0150|0151|0152|0153|01B5|01B6|01B7|01B8)$"])]

    public void P3_BowelsOfAgony_UltimaBlaster_Guide(Event @event,ScriptAccessory accessory) {

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

        // LPDU: opposite rotation to the tell (charge); #1 is across from the charge start, shifted half a step in the players' direction of travel
        if(phase3sub2_baitClockwise) {

            myPosition=rotatePosition(myPosition,ARENA_CENTER,Math.PI/8);

        }

        else {

            myPosition=rotatePosition(myPosition,ARENA_CENTER,-(Math.PI/8));

        }

        var currentProperties=accessory.Data.GetDefaultDrawProperties();
        currentProperties.Name=$"P3_BowelsOfAgony_UltimaBlaster_Guide_{targetId}";
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

    #endregion P3A Bowels of Agony Common

    #region P3A1 Fire & Water 1 3100

    [ScriptMethod(name: "———————— [P3A1 Fire & Water 1] ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P3A1_FireWater1Separator(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "P3A_ThunderChariot", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:47890"], userControl: true)]
    public void P3A_ThunderChariot(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.CurrentStage != 3100) return;
        _udmP3Param.ObjectId_Exdeath = ev.SourceId;
        sa.DrawCircle(ev.SourceId, 0, 7000, $"P3A_ThunderChariot ThunderChariot", 14.8f, sa.Data.DefaultDangerColor.WithW(2f), byTime: true);
    }

    [ScriptMethod(name: "*P3A_RemoveThunderTether", eventType: EventTypeEnum.VfxEvent,
        eventCondition: ["Type:Channeling", "Id:64"], userControl: true)]
    public async void P3A_RemoveThunderTether(Event ev, ScriptAccessory sa)
    {
        if (!p3aRemoveThunderTether) return;
        var runId = _runId;
        var obj = sa.GetById(ev.TargetId);
        sa.WriteVisible(obj, false);
        await Task.Delay(7000);
        if (runId != _runId) return;
        sa.WriteVisible(obj, true);
    }

    [ScriptMethod(name: "P3A_BowelsOfAgonyResolve", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:47858"], suppress: 10000,
        userControl: Debugging)]
    public void P3A_BowelsOfAgonyResolve(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.CurrentStage != 3100) return;
        if (!_udmP3Param.P3AStatusRecord.WaitOne(2000)) return;
        sa.DebugMsg($"P3A statuses recorded");
        if (!_udmP3Param.P3ACrystalRecord.WaitOne(2000)) return;
        sa.DebugMsg($"P3A crystals recorded");

        _udmP3Param.P3AStatusRecord.Reset();
        _udmP3Param.P3ACrystalRecord.Reset();

        _udmP3Param.GroupMembers(sa, _pd);
        sa.DebugMsg($"Party grouped");

        _udmP3Param.FireWater1Ready.Set();
    }

    [ScriptMethod(name: "P3A_FireWaterRound1Aoe", eventType: EventTypeEnum.ActionEffect,
    eventCondition: ["ActionId:47858"], suppress: 10000,
    userControl: true)]
    public async void P3A_FireWaterRound1Aoe(Event ev, ScriptAccessory sa)
    {
        var runId = _runId;

        if (_udmP3Param.CurrentStage != 3100) return;
        await Task.Delay(8000);
        if (runId != _runId) return;
        if (_udmP3Param.CurrentStage != 3100) return;

        DrawWaterFireRange(sa, _udmP3Param.FireWater1GuideAndDrawTime);
    }

    [ScriptMethod(name: "P3A_FireWater1Guide", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:47858"], suppress: 10000,
        userControl: true)]
    public async void P3A_FireWater1Guide(Event ev, ScriptAccessory sa)
    {
        var runId = _runId;
        if (_udmP3Param.CurrentStage != 3100) return;
        if (!_udmP3Param.FireWater1Ready.WaitOne(2000)) return;

        sa.DebugMsg($"Start");
        await Task.Delay(8000);
        if (runId != _runId) return;

        foreach (var idx in GuidedPlayers(sa))
            RunFireWaterGuideLogic(sa, idx, _udmP3Param.FireWater1GuideAndDrawTime);
        _udmP3Param.FireWater1Ready.Reset();
    }

    [ScriptMethod(name: "P3A_FireWaterRound2Aoe", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47869|47870)$"], suppress: 10000, userControl: true)]
    public void P3A_FireWaterRound2Aoe(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.CurrentStage != 3102 && _udmP3Param.CurrentStage != 3110) return;
        DrawWaterFireRange(sa, _udmP3Param.FireWater2GuideAndDrawTime);
    }

    private void DrawWaterFireRange(ScriptAccessory sa, int destroyTime)
    {
        // Shared AoE draws
        if (_udmP3Param.IsFireRound())
        {
            DrawGroupCircle(sa, _udmP3Param.FireGroup, "DrawWaterFireRange FireGroup", sa.Data.DefaultDangerColor, 5, destroyTime);
            DrawNearDonut(sa, sa.Data.DefaultDangerColor, destroyTime + 3000);
        }
        else
        {
            DrawGroupDonut(sa, _udmP3Param.WaterGroup, "DrawWaterFireRange WaterGroup", sa.Data.DefaultDangerColor, destroyTime);
            DrawNearCircle(sa, sa.Data.DefaultDangerColor, destroyTime + 3000);
        }
    }

    /// <summary>Based on a crystal's direction: dist meters from the middle, then rotated deg degrees counterclockwise around the middle (negative = clockwise)</summary>
    private Vector3 CrystalDirectionPoint(int crystalRegion, float dist, float deg = 0)
    {
        var crystal = _udmP3Param.RotateFromCrystal(CrystalBasePos, crystalRegion);
        return crystal.RotateAndExtend(Center, deg.DegToRad(), dist - crystal.GetLength(Center));
    }

    private void PositionGuide(ScriptAccessory sa, int idx, Vector3 pos, string tip, int destroyTime)
    {
        sa.DrawGuidance(GuidanceOwner(sa, idx), pos, 0, destroyTime, $"P3 PositionGuide {idx}", sa.Data.DefaultSafeColor);
        if (!IsMe(sa, idx)) return;
        sa.Method.TextInfo(tip, 3000);
        sa.Method.TTS(tip, 3);
    }

    // LPDU fire/water spots (wind crystal = north, west = counterclockwise side): ranged outside the fire crystal, healers at the water crystal, T and melee outside the wind crystal;
    // in the fire round, melee with fire shift 20° west and the T with fire 25° east, everyone else stacks outside the wind crystal
    private void RunFireWaterGuideLogic(ScriptAccessory sa, int idx, int destroyTime)
    {
        var p = _udmP3Param;
        var hasFire = p.IsFireRound() && p.QueryGroupOrder(p.FireGroup, idx) >= 0;
        var (pos, tip) = idx switch
        {
            6 or 7 => (CrystalDirectionPoint(p.FireCrystalRegion, P3ARangedDistance), "Outside fire crystal"),
            2 or 3 => HealerFireWaterSpot(idx),
            4 or 5 when hasFire => (CrystalDirectionPoint(p.WindCrystalRegion, P3AOuterRingDistance, 20f), "Outside wind crystal, drop fire west"),
            _ when hasFire => (CrystalDirectionPoint(p.WindCrystalRegion, P3AOuterRingDistance, -25f), "Outside wind crystal, drop fire east"),
            _ => (CrystalDirectionPoint(p.WindCrystalRegion, P3AOuterRingDistance), "Outside wind crystal, stack"),
        };
        sa.DrawGuidance(GuidanceOwner(sa, idx), pos, 0, destroyTime, $"P3A FireWaterGuide {idx}", sa.Data.DefaultSafeColor);
        if (!IsMe(sa, idx)) return;
        sa.Method.TextInfo($"Go: {tip}", 6000);
        sa.Method.TTS(tip, 3);
    }

    // Round 1 has the Thunder III chariot in the middle, so the two healers split along the wall outside the water crystal (H2 on the wind crystal side, H1 on the empty side);
    // round 2 they split along the radius (H1 at the water crystal, H2 4m from the middle toward the water crystal)
    private (Vector3, string) HealerFireWaterSpot(int idx)
    {
        var p = _udmP3Param;
        if (p.CurrentStage != 3100)
            return idx == 2
                ? (CrystalDirectionPoint(p.WaterCrystalRegion, 14f), "Water crystal")
                : (CrystalDirectionPoint(p.WaterCrystalRegion, 4f), "Middle, toward water crystal");
        var windIsCcwSide = p.WindCrystalRegion == (p.WaterCrystalRegion + 1) % 4;
        var deg = (idx == 3 ? 25f : -20f) * (windIsCcwSide ? 1 : -1);
        return (CrystalDirectionPoint(p.WaterCrystalRegion, P3ARangedDistance, deg), idx == 3 ? "Outside water crystal, wind side" : "Outside water crystal, empty side");
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
            sa.DrawCircle(sa.Data.PartyList[group[i].Key], 0, destroyTime, $"{name}Chariot{i + 1}", scale, color);
    }

    private void DrawGroupDonut(
        ScriptAccessory sa, List<KeyValuePair<int, int>> group, string name, Vector4 color, int destroyTime)
    {
        for (var i = 0; i < group.Count; i++)
            sa.DrawDonut(sa.Data.PartyList[group[i].Key], 0, destroyTime, $"{name}Donut{i + 1}", 10, 5, color);
    }

    private void DrawNearCircle(ScriptAccessory sa, Vector4 color, int destroyTime)
    {
        var crystalPos = new Vector3(109.9f, 0, 109.9f);
        crystalPos = _udmP3Param.RotateFromCrystal(crystalPos, _udmP3Param.WaterCrystalRegion);

        for (int i = 0; i < 2; i++)
        {
            var dp = sa.DrawCircle(crystalPos, 0, destroyTime, $"DrawNearCircle WaterCrystalChariot{i + 1}", 5f, color, draw: false);
            dp.SetOwnersDistanceOrder(true, (uint)(i + 1));
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    private void DrawNearDonut(ScriptAccessory sa, Vector4 color, int destroyTime)
    {
        var crystalPos = new Vector3(109.9f, 0, 109.9f);
        crystalPos = _udmP3Param.RotateFromCrystal(crystalPos, _udmP3Param.FireCrystalRegion);

        for (int i = 0; i < 2; i++)
        {
            var dp = sa.DrawDonut(crystalPos, 0, destroyTime, $"DrawNearCircle FireCrystalDonut{i + 1}", 10f, 5f, color, draw: false);
            dp.SetOwnersDistanceOrder(true, (uint)(i + 1));
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
        }
    }

    #endregion P3A1 Fire & Water 1 3100

    #region P3A1 Thunder III Tankbuster 3101 3102

    [ScriptMethod(name: "P3A_ThunderTankbuster_AdvancePhase", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:47881"], userControl: Debugging)]
    public void P3A_ThunderTankbuster_AdvancePhase(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.CurrentStage != 3100) return;
        _udmP3Param.CurrentStage = 3101;
        sa.DebugMsg($"Thunder III tankbuster, Fire & Water 1 done");
    }

    [ScriptMethod(name: "P3A_PostThunderTankbuster_AdvancePhase", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:47884"], suppress: 10000, userControl: Debugging)]
    public void P3A_PostThunderTankbuster_AdvancePhase(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.CurrentStage != 3101) return;
        _udmP3Param.CurrentStage = 3102;
    }

    #endregion P3A1 Thunder III Tankbuster 3101 3102

    #region P3A1 Fire & Water 2 3110

    [ScriptMethod(name: "———————— [P3A1 Fire & Water 2] ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P3A1_FireWater2Separator(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "P3A_FireWater2_AdvancePhase", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47869|47870)$"], suppress: 10000, userControl: Debugging)]
    public void P3A_FireWater2_AdvancePhase(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.CurrentStage != 3102) return;
        _udmP3Param.CurrentStage = 3110;
        sa.DebugMsg($"Fire & Water 2 started, stage -> {_udmP3Param.CurrentStage}");
    }

    [ScriptMethod(name: "P3A_FireWater2Guide", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47869|47870)$"], suppress: 10000, userControl: true)]
    public void P3A_FireWater2Guide(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.CurrentStage != 3102 && _udmP3Param.CurrentStage != 3110) return;
        sa.DebugMsg($"Start");
        foreach (var idx in GuidedPlayers(sa))
            RunFireWaterGuideLogic(sa, idx, _udmP3Param.FireWater2GuideAndDrawTime);
    }

    [ScriptMethod(name: "P3A_FireWater2End_AdvancePhase", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:regex:^(4786[01])$"], suppress: 10000, userControl: Debugging)]
    public void P3A_FireWater2End_AdvancePhase(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.CurrentStage != 3110) return;
        _udmP3Param.CurrentStage = 3111;
        sa.DebugMsg($"Fire & Water 2 done, stage -> {_udmP3Param.CurrentStage}");
    }

    [ScriptMethod(name: "P3A_FireWater2EndGuide", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:regex:^(4786[01])$"], suppress: 10000, userControl: true)]
    public void P3A_FireWater2End(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.CurrentStage != 3110 && _udmP3Param.CurrentStage != 3111) return;

        /*
         * Stray Spray donut: Stray Spray 47862
         * Chariot from Stray Spray: Tsunami 47861
         * Stray Flames chariot: Stray Flames 47859
         * Donut from Stray Flames: Inferno 47860
         * Stack from Stray Gusts: Cyclone 47864
         */

        // LPDU: after Fire & Water 2, R1 goes to the empty side's wall to bait the jump, everyone else except the tanks stacks at the wind crystal
        sa.DebugMsg($"Fire & Water 2 done, current stage {_udmP3Param.CurrentStage}");
        var windPos = CrystalDirectionPoint(_udmP3Param.WindCrystalRegion, 14f);
        foreach (var idx in GuidedPlayers(sa))
        {
            if (idx == 6)
                PositionGuide(sa, idx, CrystalDirectionPoint(_udmP3Param.EmptyCrystalRegion, P3AOuterRingDistance), "Empty side wall, bait the jump", 6500);
            else if (idx >= 2)
                PositionGuide(sa, idx, windPos, "Stack at wind crystal", 6500);
        }
    }

    #endregion P3A1 Fire & Water 2 3110

    #region P3A1 Vacuum Wave 3111

    [ScriptMethod(name: "———————— [P3A1 Vacuum Wave] ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P3A1_VacuumWaveSeparator(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "P3A_VacuumWave", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47891)$"], userControl: true)]
    public void P3A_VacuumWave(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.CurrentStage != 3111) return;
        sa.DebugMsg($"Start");

        foreach (var idx in GuidedPlayers(sa))
            RunVacuumWaveGuideLogic(sa, idx);
    }

    [ScriptMethod(name: "*P3A_ChaosTransparentDuringVacuumWave", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47891)$"], userControl: true)]
    public async void P3A_ChaosTransparentDuringVacuumWave(Event ev, ScriptAccessory sa)
    {
        if (!p3aChaosTransparentDuringVacuumWave) return;

        if (_udmP3Param.CurrentStage != 3111) return;
        sa.DebugMsg($"Run");

        var runId = _runId;
        var obj = sa.GetById(_udmP3Param.ObjectId_Chaos);
        sa.AlphaModify(obj, 0.3f);
        await Task.Delay(8000);
        if (runId != _runId) return;
        sa.AlphaModify(obj, 1f);
    }

    [ScriptMethod(name: "P3A_VacuumWaveGuideLines", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47891)$"], userControl: true)]
    public void P3A_VacuumWaveGuideLines(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.CurrentStage != 3111) return;
        DrawStaticGuideLineVw(sa, _udmP3Param.VacuumWaveGuideTime);
    }

    [ScriptMethod(name: "P3A_PostVacuumWaveStacks", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:regex:^(47891)$"], suppress: 10000, userControl: true)]
    public void P3A_PostVacuumWaveStacks(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.CurrentStage != 3111) return;
        sa.DebugMsg($"Stacks after knockback");

        // LPDU role pair stacks: MT+OT, H1+H2, M1+M2, R1+R2
        foreach (var idx in GuidedPlayers(sa))
            sa.DrawCircle(sa.Data.PartyList[idx ^ 1], 0, 4000, $"VacuumWaveStack {idx}", 6, sa.Data.DefaultSafeColor);
    }

    // LPDU Vacuum Wave: role pairs stand in a line, one in front of the other. Angles measured from Exdeath toward the middle, positive = east (counterclockwise)
    // T 20° west, melee 20° east, healers 50° west, ranged 45° east; (angle, distance from Exdeath)
    private static readonly (float deg, float dist)[] VacuumWaveSpots =
        [(-20f, 4f), (-20f, 6f), (-50f, 6.5f), (-50f, 8f), (20f, 4f), (20f, 5.5f), (45f, 7.5f), (45f, 6f)];

    private void RunVacuumWaveGuideLogic(ScriptAccessory sa, int myIndex)
    {
        var (deg, dist) = VacuumWaveSpots[myIndex];
        Vector3 bossPos = sa.GetById(_udmP3Param.ObjectId_Exdeath).Position;
        var guidePos = bossPos.RotateAndExtend(Center, 0, -dist).RotateAndExtend(bossPos, deg.DegToRad());
        sa.DrawGuidance(GuidanceOwner(sa, myIndex), guidePos, 0, _udmP3Param.VacuumWaveGuideTime, $"VacuumWaveGuide Guide {myIndex}", sa.Data.DefaultSafeColor);

        // Facing indicator: Headwind = back to the boss, Tailwind = face the boss
        var needBack = _udmP3Param.NeedFaceAway(_pd.Priorities[myIndex]);
        var needBackStr = needBack ? "Back to" : "Face";
        DrawFacingArrow(sa, myIndex, 0, false, $"VacuumWaveGuide FacingSelf", _udmP3Param.VacuumWaveGuideTime);

        var dp = sa.DrawLine(GuidanceOwner(sa, myIndex), _udmP3Param.ObjectId_Exdeath, 0, _udmP3Param.VacuumWaveGuideTime, $"VacuumWaveGuide FacingCorrect {myIndex}",
            needBack ? 180f.DegToRad() : 0, 1f, 4.5f, sa.Data.DefaultSafeColor, draw: false);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Arrow, dp);

        if (!IsMe(sa, myIndex)) return;
        sa.Method.TextInfo($"Inside hitbox, {needBackStr} Exdeath for knockback", 4000);
        sa.Method.TTS($"{needBackStr} boss, knockback", 3);
    }

    private void DrawStaticGuideLineVw(ScriptAccessory sa, int destroyTime)
    {
        var myIndex = sa.GetMyIndex();
        Vector3 bossPos = sa.GetById(_udmP3Param.ObjectId_Exdeath).Position;
        var baseRadian = 180f.DegToRad() + bossPos.GetRadian(Center);

        // The four lines are T, healers, melee, ranged in order (same angles as VacuumWaveSpots)
        List<float> biasDeg = [-20, -50, 20, 45];
        var myLine = myIndex switch { 0 or 1 => 0, 2 or 3 => 1, 4 or 5 => 2, _ => 3 };
        for (int i = 0; i < 4; i++)
        {
            var dp = sa.DrawLine(bossPos, 0, 0, destroyTime, $"DrawStaticGuideLineVw Line{i}",
                baseRadian + biasDeg[i].DegToRad(), 20f, 50f, sa.Data.DefaultSafeColor, draw: false);
            dp.Color = i == myLine ? sa.Data.DefaultSafeColor : sa.Data.DefaultDangerColor;
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Line, dp);
        }
    }

    #endregion P3A1 Vacuum Wave 3111

    #region P3B Earthquake Common

    [ScriptMethod(name: "============= [P3B Earthquake] =============", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P3B_Separator(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "P3B_SubphaseStart_Earthquake", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:50545"],
        userControl: Debugging)]
    public void P3B_SubphaseStart_Earthquake(Event ev, ScriptAccessory sa)
    {
        _pd.Init(sa, "P3B");
        _pd.AddPriorities([1, 2, 3, 4, 5, 6, 7, 8]);
        _udmP3Param.CurrentStage = 3200;

        sa.DebugMsg($"Current stage: P3B Earthquake {_udmP3Param.CurrentStage}");
    }

    [ScriptMethod(name: "*P3B_BossesTransparentDuringBlackHolesAndBlizzard", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47867)$"], userControl: true)]
    public void P3B_BossesTransparentDuringBlackHoles(Event ev, ScriptAccessory sa)
    {
        if (!p3bBossesTransparentDuringBlackHole) return;

        const uint bhEntry = 47867;
        var obj1 = sa.GetById(_udmP3Param.ObjectId_Chaos);
        var obj2 = sa.GetById(_udmP3Param.ObjectId_Exdeath);
        sa.AlphaModify(obj1, 0.3f);
        sa.AlphaModify(obj2, ev.ActionId == bhEntry ? 0.75f : 0.3f);
    }

    [ScriptMethod(name: "*P3B_GiantKefkaTransparent", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(4784[67]|4785[235])$"], userControl: true)]
    public async void P3B_GiantKefkaTransparent(Event ev, ScriptAccessory sa)
    {
        if (!p3bKefkaTransparent) return;
        var runId = _runId;
        var obj = sa.GetById(ev.SourceId);
        sa.AlphaModify(obj, 0.3f);
        await Task.Delay(10000);
        if (runId != _runId) return;
        sa.AlphaModify(obj, 1f);
    }

    [ScriptMethod(name: "*P3B_ChaosTransparentWhenIdle", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47867|47869|47870)$"], userControl: true)]
    public async void P3B_ChaosTransparentWhenIdle(Event ev, ScriptAccessory sa)
    {
        if (!p3bChaosTransparent) return;
        if (_udmP3Param.CurrentStage != 3200) return;

        const uint DamningEdict = 47867;
        var awaitTime = ev.ActionId == DamningEdict ? 5000 : 7500;

        var runId = _runId;
        var obj = sa.GetById(_udmP3Param.ObjectId_Chaos);
        sa.AlphaModify(obj, 1f, currentAlpha => currentAlpha <= 0.8f);
        await Task.Delay(awaitTime);
        if (runId != _runId) return;
        sa.AlphaModify(obj, 0.5f, currentAlpha => currentAlpha >= 0.8f);
    }

    [ScriptMethod(name: "*P3B_ExdeathTransparentWhenIdle", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47881)$"], userControl: true)]
    public async void P3B_ExdeathTransparentWhenIdle(Event ev, ScriptAccessory sa)
    {
        if (!p3bExdeathTransparent) return;
        if (_udmP3Param.CurrentStage != 3200) return;

        var runId = _runId;
        var obj = sa.GetById(_udmP3Param.ObjectId_Exdeath);
        sa.AlphaModify(obj, 1f, currentAlpha => currentAlpha <= 0.8f);
        await Task.Delay(5000);
        if (runId != _runId) return;
        sa.AlphaModify(obj, 0.75f, currentAlpha => currentAlpha >= 0.8f);
    }

    [ScriptMethod(name: "P3B_SlapHappy_AoeAndGuide", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(4784[67])$"], userControl: true)]
    public void P3B_SlapHappy_AoeAndGuide(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.CurrentStage != 3200) return;

        const uint RightSlapsThenStack = 47846;
        const uint LeftSlapsThenRoleStacks = 47847;
        var aid = ev.ActionId;
        var actionStr = aid switch
        {
            RightSlapsThenStack => "Right slaps, party stack",
            LeftSlapsThenRoleStacks => "Left slaps, role stacks",
        };

        var isStack = aid == RightSlapsThenStack;

        _udmP3Param.KefkaRegion = _udmP3Param.GetGiantKefkaRegion(sa, true);
        var (attackRegion, safeRegion) = _udmP3Param.GiantKefkaCleaveDirection(sa, aid == RightSlapsThenStack);

        // Draw the cleave and the chariot
        sa.DrawCircle(Center, 0, 8000, $"[P3B_SlapHappy] MiddleChariot", 6f, sa.Data.DefaultDangerColor.WithW(2f));
        var biasCenter = (Center - new Vector3(0, 0, 3.8f)).RotateAndExtend(Center, -45f.DegToRad() * safeRegion);
        sa.DrawRect(biasCenter, Center, 0, 7000, $"[P3B_SlapHappy] Cleave", 0, 60f, 60f, sa.Data.DefaultDangerColor.WithW(2f));

        // Draw the party stack line or the role lines
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
            var dp = sa.DrawLine(Center, guidePos, 0, 8000, $"SlapHappy Line{i}",
                0, 20f, 50f, sa.Data.DefaultSafeColor, draw: false);
            dp.Color = i switch
            {
                0 => new Vector4(0.1f, 1f, 0.1f, 1),    // Healer
                1 => new Vector4(0.1f, 0.1f, 1f, 1),    // Tank
                2 => new Vector4(1f, 0.1f, 0.1f, 1),    // DPS
            };
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Line, dp);

            foreach (var idx in GuidedPlayers(sa))
                if (RoleOf(idx) == i || isStack)
                    sa.DrawGuidance(GuidanceOwner(sa, idx), guidePos, 0, 8000, $"SlapHappy Guide {idx}", sa.Data.DefaultSafeColor);
        }

        sa.DebugMsg($"{actionStr}");
        sa.Method.TextInfo($"Slap Happy: {actionStr}", 3000);
        sa.Method.TTS($"{actionStr}", 3);
    }

    [ScriptMethod(name: "P3B_DamningEdict_Cleave", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47873)$"], userControl: true)]
    public void P3B_DamningEdict_Cleave(Event ev, ScriptAccessory sa)
    {
        sa.DrawRect(ev.SourceId, 800, 4200, $"DamningEdictCleave", 0, 80, 30, CursedEdictColor.V4.WithW(CursedEdictWhiteVal), byTime: true);
    }

    [ScriptMethod(name: "P3B_LookUponMeAndDespair", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47854)$"], userControl: true)]
    public void P3B_LookUponMeAndDespair(Event ev, ScriptAccessory sa)
    {
        var dp = sa.DrawRect(ev.SourceId, 0, 5000, $"LookUponMeAndDespair", 0, 16, 100, sa.Data.DefaultDangerColor.WithW(2f),
            draw: false);
        dp.Offset = new Vector3(0, 0, 40);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "P3B_WhiteHole_HealUpCallout", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(48486)$"], userControl: true)]
    public void P3B_WhiteHole_HealUpCallout(Event ev, ScriptAccessory sa)
    {
        if (sa.GetMyIndex() is not 2 and not 3) return;
        sa.Method.TextInfo($"Heal everyone to full!", 3000, true);
        sa.Method.TTS($"Heal everyone to full", 3);
    }

    [ScriptMethod(name: "P3B_TrackGiantKefka", eventType: EventTypeEnum.PlayActionTimeline,
        eventCondition: ["Id:7748", "SourceDataId:19504"], userControl: true)]
    public void P3B_TrackGiantKefka(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.CurrentStage != 3200) return;
        var region = _udmP3Param.GetGiantKefkaRegion(sa, false);
        var kefkaPos = new Vector3(100, 0, 120);
        kefkaPos = kefkaPos.RotateAndExtend(Center, region * 45f.DegToRad());
        var color = new Vector4(0.1f, 1f, 1f, 1f);
        sa.DrawConnection(sa.Data.Me, kefkaPos, 0, 3000, $"TrackGiantKefka", color);
        sa.Method.TextInfo($"Kefka is back!", 1000);
    }

    #endregion P3B Earthquake Common

    #region P3B1 Black Holes 3200

    [ScriptMethod(name: "———————— [P3B1 Black Holes] ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P3B1_Separator(Event ev, ScriptAccessory sa)
    {
    }

    // The Earthquake debuffs decide the tether roles (LPDU uses no markers): 3004/3005/3006 First/Second/Third in Line, 1604 Accretion
    // In each line group, DPS take the 1st black hole clockwise from Kefka, supports the 2nd, the Accretion player the 3rd:
    // 110/120/130 = First in Line DPS/support/Accretion, 210/220/230 = Second in Line likewise, 310/320 = Third in Line DPS/support
    [ScriptMethod(name: "P3B1_RecordTetherRolesByDebuff", eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:regex:^(300[456]|1604)$"], userControl: Debugging)]
    public void P3B1_RecordTetherRolesByDebuff(Event ev, ScriptAccessory sa)
    {
        const uint Accretion = 1604;
        const uint FirstInLine = 3004;
        if (_udmP3Param.CurrentStage != 3200) return;
        lock (_pd)
        {
            if (_udmP3Param.BlackHoleAppeared) return;

            var tidx = sa.GetPlayerIdIndex((uint)ev.TargetId);
            if (tidx is < 0 or >= 8) return;

            if (ev.StatusId == Accretion)
                _udmP3Param.HasAccretion[tidx] = true;
            else
                _udmP3Param.TetherOrder[tidx] = (int)(ev.StatusId - FirstInLine) + 1;

            var line = _udmP3Param.TetherOrder[tidx];
            if (line == 0) return;
            var slot = _udmP3Param.HasAccretion[tidx] ? 30 : tidx <= 3 ? 20 : 10;
            _pd.Priorities[tidx] = (tidx + 1) + line * 100 + slot;
            sa.DebugMsg($"{sa.GetPlayerJobByIndex(tidx)} #{line} in line{(_udmP3Param.HasAccretion[tidx] ? " +Accretion" : "")} == {_pd.Priorities[tidx]}");
        }
    }

    [ScriptMethod(name: "P3B1_IncrementBlackHoleRound", eventType: EventTypeEnum.AddCombatant,
        eventCondition: ["DataId:19512"], userControl: Debugging, suppress: 5000)]
    public void P3B1_IncrementBlackHoleRound(Event ev, ScriptAccessory sa)
    {
        if (!_udmP3Param.BlackHoleAppeared)
        {
            _udmP3Param.BlackHoleAppeared = true;
            sa.DebugMsg($"Tether roles recorded");
            sa.DebugMsg($"{_pd.ShowPriorities()}");
        }
        _udmP3Param.BlackHoleDict.Clear();
        _udmP3Param.BlackHoleRound++;
        sa.DebugMsg($"Current stage {_udmP3Param.CurrentStage}, cleared black hole dict {_udmP3Param.BlackHoleDict.Count}");
        _udmP3Param.BlackHoleRoundIncremented.Set();
    }

    [ScriptMethod(name: "P3B1_GetKefkaRegion", eventType: EventTypeEnum.AddCombatant,
        eventCondition: ["DataId:19512"], userControl: Debugging, suppress: 5000)]
    public void P3B1_GetKefkaRegion(Event ev, ScriptAccessory sa)
    {
        _udmP3Param.KefkaRegion = _udmP3Param.GetGiantKefkaRegion(sa, true);
        _udmP3Param.KefkaRegionAcquired.Set();
    }

    [ScriptMethod(name: "P3B1_GetOuterBlackHoles", eventType: EventTypeEnum.AddCombatant,
        eventCondition: ["DataId:19512"], userControl: Debugging)]
    public void P3B1_GetOuterBlackHoles(Event ev, ScriptAccessory sa)
    {
        if (!_udmP3Param.BlackHoleRoundIncremented.WaitOne(2000)) return;
        if (!_udmP3Param.KefkaRegionAcquired.WaitOne(2000)) return;

        lock (_pd)
        {
            var sid = ev.SourceId;
            var obj = sa.GetById(sid);
            if (obj == null) return;
            var pos = obj.Position;

            // Black holes are 17 from the middle
            if (Vector3.Distance(pos, Center) < 16.5f) return;

            // count CW from A
            var region = pos.GetRadian(Center).RadianToRegion(8, 4, isDiagDiv: true, isCw: true) % 8;
            var relativeRegion = (region + 8 - _udmP3Param.KefkaRegion) % 8;

            _udmP3Param.BlackHoleDict.TryAdd(sid,
                (round: _udmP3Param.BlackHoleRound, region: region, relativeRegion: relativeRegion, hasTether: false));
            var count = _udmP3Param.BlackHoleDict.Count;
            sa.DebugMsg($"Got region {region} relative region {relativeRegion} black hole #{count}");

            if (_udmP3Param.BlackHoleDict.Count < 3) return;

            // Check whether there's a tether
            uint tetherId = 0x54;
            foreach (var bhEntry in _udmP3Param.BlackHoleDict.ToList())
            {
                var bc = sa.GetById(bhEntry.Key);
                if (bc == null) continue;
                var valueTuple = _udmP3Param.BlackHoleDict[bhEntry.Key];
                var tetherCount = sa.GetTetherSource((IBattleChara)bc, tetherId).Count;
                valueTuple.hasTether = tetherCount > 0;
                _udmP3Param.BlackHoleDict[bhEntry.Key] = valueTuple;
                sa.DebugMsg(
                    $"Region {bhEntry.Value.region} relative region {bhEntry.Value.relativeRegion} black hole 0x{bc.GameObjectId:X8} {(tetherCount > 0 ? "has" : "no")} tether");
            }

            SortBlackHoleDict();
            _udmP3Param.OuterBlackHolesAcquired.Set();
            _udmP3Param.BlackHoleRoundIncremented.Reset();
            _udmP3Param.KefkaRegionAcquired.Reset();
        }
    }

    [ScriptMethod(name: "P3B1_PrepareTetherTasks_RoundStart", eventType: EventTypeEnum.AddCombatant,
        eventCondition: ["DataId:19512"], userControl: Debugging, suppress: 5000)]
    public void P3B1_PrepareTetherTasks_RoundStart(Event ev, ScriptAccessory sa)
    {
        if (!_udmP3Param.OuterBlackHolesAcquired.WaitOne(2000)) return;
        lock (_udmP3Param)
            _udmP3Param.BlackHoleTetherTasksReady.Reset();
        CleanupTetherFramework(sa);
        _udmP3Param.PlayerIsTethering = false;

        // Black hole round 1 start: First in Line DPS
        // Black hole round 2 start: First in Line DPS / support / Accretion
        // Black hole round 3 start: Second in Line DPS / support / Accretion
        // Black hole round 4 start: Third in Line DPS / support

        var currentTasks = GetRoundBlackHoleTetherTasks(sa, _udmP3Param.BlackHoleRound, _udmP3Param.BlackHoleEffectRound);
        lock (_udmP3Param)
        {
            _udmP3Param.CurrentBlackHoleTetherTasks = currentTasks;
            _udmP3Param.BlackHoleTetherTaskVersion++;
            _udmP3Param.OuterBlackHolesAcquired.Reset();
            _udmP3Param.BlackHoleTetherTasksReady.Set();
        }
    }

    [ScriptMethod(name: "P3B1_BlackHoleTetherAoe_RoundStart", eventType: EventTypeEnum.AddCombatant,
        eventCondition: ["DataId:19512"], userControl: true, suppress: 5000)]
    public void P3B1_BlackHoleTetherAoe_RoundStart(Event ev, ScriptAccessory sa)
    {
        if (!WaitForBlackHoleTetherTasks(sa, ref _udmP3Param.BlackHoleTetherAoeConsumedVersion,
                "P3B1_BlackHoleTetherAoe_RoundStart", out var tasks)) return;
        foreach (var task in tasks)
            DrawBlackHoleTetherAoe(sa, task);
    }

    [ScriptMethod(name: "P3B1_BlackHoleTetherTts_RoundStart", eventType: EventTypeEnum.AddCombatant,
        eventCondition: ["DataId:19512"], userControl: true, suppress: 5000)]
    public void P3B1_BlackHoleTetherTts_RoundStart(Event ev, ScriptAccessory sa)
    {
        if (!WaitForBlackHoleTetherTasks(sa, ref _udmP3Param.BlackHoleTetherTtsConsumedVersion,
                "P3B1_BlackHoleTetherTts_RoundStart", out var tasks)) return;
        bool ttsHinted = false;
        foreach (var task in tasks)
            ttsHinted |= SpeakBlackHoleTetherTts(sa, task.playerIdx, ttsHinted);
    }

    [ScriptMethod(name: "P3B1_BlackHoleTetherGuide_RoundStart", eventType: EventTypeEnum.AddCombatant,
        eventCondition: ["DataId:19512"], userControl: true, suppress: 5000)]
    public void P3B1_BlackHoleTetherGuide_RoundStart(Event ev, ScriptAccessory sa)
    {
        if (!WaitForBlackHoleTetherTasks(sa, ref _udmP3Param.BlackHoleTetherGuideConsumedVersion,
                "P3B1_BlackHoleTetherGuide_RoundStart", out var tasks)) return;
        if (_udmP3Param.BlackHoleEffectRound >= 10) return;
        foreach (var task in tasks)
            MountTetherGuideTowardBlackHole(sa, task);
        RunTetherFramework(sa, tasks);
    }

    [ScriptMethod(name: "P3B1_PrepareTetherTasks_MidRound", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:47868", "TargetIndex:1"], userControl: Debugging, suppress: 2500)]
    public void P3B1_PrepareTetherTasks_MidRound(Event ev, ScriptAccessory sa)
    {
        lock (_udmP3Param)
            _udmP3Param.BlackHoleTetherTasksReady.Reset();
        // Remove the previous round's tethers
        CleanupTetherFramework(sa);

        sa.Method.RemoveDraw($"DrawBlackHoleTether R{_udmP3Param.BlackHoleRound} T{_udmP3Param.BlackHoleEffectRound}.*");
        _udmP3Param.BlackHoleEffectRound++;
        if (_udmP3Param.BlackHoleRound is 1 or 4)
        {
            FlipBlackHoleTetherState();
            SortBlackHoleDict();
        }

        var currentTasks = GetRoundBlackHoleTetherTasks(sa, _udmP3Param.BlackHoleRound, _udmP3Param.BlackHoleEffectRound);
        lock (_udmP3Param)
        {
            _udmP3Param.CurrentBlackHoleTetherTasks = currentTasks;
            _udmP3Param.BlackHoleTetherTaskVersion++;
            _udmP3Param.BlackHoleTetherTasksReady.Set();
        }
    }

    [ScriptMethod(name: "P3B1_BlackHoleTetherAoe_MidRound", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:47868", "TargetIndex:1"], userControl: true, suppress: 2500)]
    public void P3B1_BlackHoleTetherAoe_MidRound(Event ev, ScriptAccessory sa)
    {
        if (!WaitForBlackHoleTetherTasks(sa, ref _udmP3Param.BlackHoleTetherAoeConsumedVersion,
                "P3B1_BlackHoleTetherAoe_MidRound", out var tasks)) return;
        foreach (var task in tasks)
            DrawBlackHoleTetherAoe(sa, task);
    }

    [ScriptMethod(name: "P3B1_BlackHoleTetherTts_MidRound", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:47868", "TargetIndex:1"], userControl: true, suppress: 2500)]
    public void P3B1_BlackHoleTetherTts_MidRound(Event ev, ScriptAccessory sa)
    {
        if (!WaitForBlackHoleTetherTasks(sa, ref _udmP3Param.BlackHoleTetherTtsConsumedVersion,
                "P3B1_BlackHoleTetherTts_MidRound", out var tasks)) return;
        bool ttsHinted = false;
        foreach (var task in tasks)
            ttsHinted |= SpeakBlackHoleTetherTts(sa, task.playerIdx, ttsHinted);

        if (ttsHinted || !_udmP3Param.PlayerIsTethering) return;
        _udmP3Param.PlayerIsTethering = false;
        sa.Method.TextInfo($"Back to the group", 2000);
        sa.Method.TTS($"Back to the group", 3);
    }

    [ScriptMethod(name: "P3B1_BlackHoleTetherGuide_MidRound", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:47868", "TargetIndex:1"], userControl: true, suppress: 2500)]
    public void P3B1_BlackHoleTetherGuide_MidRound(Event ev, ScriptAccessory sa)
    {
        if (!WaitForBlackHoleTetherTasks(sa, ref _udmP3Param.BlackHoleTetherGuideConsumedVersion,
                "P3B1_BlackHoleTetherGuide_MidRound", out var tasks)) return;
        if (_udmP3Param.BlackHoleEffectRound >= 10) return;
        foreach (var task in tasks)
            MountTetherGuideTowardBlackHole(sa, task);
        RunTetherFramework(sa, tasks);
    }

    private bool SpeakBlackHoleTetherTts(ScriptAccessory sa, int playerIdx, bool ttsHinted)
    {
        var myIndex = sa.GetMyIndex();
        if (playerIdx == myIndex)
        {
            if (ttsHinted) return true;
            sa.Method.TextInfo($"Take tether", 2000);
            sa.Method.TTS($"Take tether", 3);
            _udmP3Param.PlayerIsTethering = true;
            return true;
        }
        return false;
    }

    private bool WaitForBlackHoleTetherTasks(ScriptAccessory sa, ref int consumedVersion, string caller,
        out List<(int playerIdx, List<int> realRegions, List<ulong> blackHoles, string name)> tasks)
    {
        tasks = [];
        for (var i = 0; i < 40; i++)
        {
            if (!_udmP3Param.BlackHoleTetherTasksReady.WaitOne(50)) continue;
            lock (_udmP3Param)
            {
                if (_udmP3Param.BlackHoleTetherTaskVersion <= consumedVersion)
                {
                    _udmP3Param.BlackHoleTetherTasksReady.Reset();
                    continue;
                }
                consumedVersion = _udmP3Param.BlackHoleTetherTaskVersion;
                tasks = _udmP3Param.CurrentBlackHoleTetherTasks.ToList();
                return true;
            }
        }
        sa.DebugMsg($"Timed out waiting for black hole tether tasks");
        return false;
    }

    private void FlipBlackHoleTetherState()
    {
        foreach (var bhEntry in _udmP3Param.BlackHoleDict.ToList())
        {
            var valueTuple = bhEntry.Value;
            valueTuple.hasTether = !valueTuple.hasTether;
            _udmP3Param.BlackHoleDict[bhEntry.Key] = valueTuple;
        }
    }

    private void SortBlackHoleDict()
    {
        _udmP3Param.BlackHoleDict = _udmP3Param.BlackHoleDict
            .OrderByDescending(kvp => kvp.Value.hasTether)
            .ThenBy(kvp => kvp.Value.relativeRegion)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    private List<int> FilterRoundTetherPlayers(ScriptAccessory sa, int bhRound, int bhEffectRound)
    {
        // The i-th tether role takes the i-th black hole in this round's order (clockwise from Kefka)
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
            sa.DebugMsg($"Bad input ({bhRound} {bhEffectRound})");
        }
        return markFeatureList;
    }

    private KeyValuePair<int, int> FindPlayerByTetherRole(ScriptAccessory sa, int markFeature)
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

    private List<(int playerIdx, List<int> realRegions, List<ulong> blackHoles, string name)> GetRoundBlackHoleTetherTasks(ScriptAccessory sa, int bhRound, int bhEffectRound)
    {
        var markFeatureList = FilterRoundTetherPlayers(sa, bhRound, bhEffectRound);
        var tasks = new List<(int playerIdx, List<int> realRegions, List<ulong> blackHoles, string name)>();

        for (int i = 0; i < markFeatureList.Count; i++)
        {
            var markFeature = markFeatureList[i];
            // The black hole dict is already sorted when it's built
            var bh = _udmP3Param.BlackHoleDict.ElementAt(i);
            var blackHoles = new List<ulong> { bh.Key };
            var realRegions = new List<int> { bh.Value.region };

            var player = FindPlayerByTetherRole(sa, markFeature);
            if (player.Key < 0)
            {
                sa.DebugMsg($"No player found for tether role {markFeature}");
                continue;
            }

            var regionStr = string.Join(",", realRegions);
            var taskName = $"R{bhRound} T{bhEffectRound} {player.Key} {regionStr}";
            tasks.Add((player.Key, realRegions, blackHoles, taskName));
            sa.DebugMsg(
                $"{taskName} | player {sa.GetPlayerJobByIndex(player.Key)}({player.Key}) | key {markFeature} | region {regionStr} | black holes {string.Join(",", blackHoles.Select(x => x.ToString("X8")))}");
        }

        sa.DebugMsg($"R{bhRound} T{bhEffectRound} total {tasks.Count} tasks");
        return tasks;
    }

    private bool BlackHoleTetherTaskIsMine(ScriptAccessory sa,
        (int playerIdx, List<int> realRegions, List<ulong> blackHoles, string name) task)
    {
        return Debugging || sa.GetMyIndex() == task.playerIdx;
    }

    private void DrawBlackHoleTetherAoe(ScriptAccessory sa,
        (int playerIdx, List<int> realRegions, List<ulong> blackHoles, string name) task)
    {
        if (!BlackHoleTetherTaskIsMine(sa, task)) return;
        var color = BhConnLineColor.V4;

        for (int i = 0; i < task.blackHoles.Count; i++)
        {
            var dp = sa.DrawLine(task.blackHoles[i], 0, 0, 7000, $"DrawBlackHoleTether {task.name} L{task.realRegions[i]}",
                0, 1, 40, color, byY: true, draw: false);
            dp.TargetResolvePattern = PositionResolvePatternEnum.TetherTarget;
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, dp);

            var dp2 = sa.DrawRect(task.blackHoles[i], 0, 0, 7000, $"DrawBlackHoleTether {task.name} R{task.realRegions[i]}",
                0, 6, 40, sa.Data.DefaultDangerColor, draw: false);
            dp2.TargetResolvePattern = PositionResolvePatternEnum.TetherTarget;
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp2);
        }

        sa.DebugMsg($"{sa.GetPlayerJobByIndex(task.playerIdx)} takes real region {string.Join(",", task.realRegions)} black hole");
    }

    private void MountTetherGuideTowardBlackHole(ScriptAccessory sa,
        (int playerIdx, List<int> realRegions, List<ulong> blackHoles, string name) task)
    {
        if (!BlackHoleTetherTaskIsMine(sa, task)) return;
        var isLastTether = _udmP3Param.BlackHoleRound == 4 && _udmP3Param.BlackHoleEffectRound == 9;
        var guidePos = CalcTetherSpot(task.realRegions[0], isLastTether);

        MountTetherFramework(sa, guidePos, task);

        sa.DebugMsg($"{sa.GetPlayerJobByIndex(task.playerIdx)} takes real region {string.Join(",", task.realRegions)} black hole");
    }

    private void MountTetherFramework(ScriptAccessory sa, Vector3 guidePos,
        (int playerIdx, List<int> realRegions, List<ulong> blackHoles, string name) task)
    {
        // task
        // -- playerIdx: the player who needs to take the tether
        // -- realRegions: regions of the black holes to tether
        // -- blackHoles: ObjIds of the black holes to tether
        // -- name: task name $"R{bhRound} T{bhEffectRound} {player.Key} {regionStr}
        // ---- bhRound: black hole round
        // ---- bhEffectRound: black hole firing round
        // ---- player.Key: same as playerIdx
        // ---- regionStr: the set of regions of the black holes to tether, i.e. the Join of realRegions

        // Draw the guidance once every tether from the black holes in task.blackHoles has been taken
        // Each tether needs one fwElement to control it
        // fwElement
        // -- Name: the Framework's Name, planned as {player.Key} {blackHoles[i]}
        // -- PlayerIndex: Index of the player who needs to take the tether
        // -- BlackHoleId: the source black hole of the tether
        // -- PlayerObjId: ObjectId of the player who needs to take the tether
        // -- FwHandle: handle from sa.Method.RegistFrameworkUpdateAction(Action)
        // -- Tethered: true once the tether has been taken
        // -- GuidePos: guide position after taking the tether / all tethers

        // One Framework in the List controls each black hole
        // One outer Framework checks whether every element in the List is tethered, then draws the guide line

        for (int i = 0; i < task.blackHoles.Count; i++)
        {
            ulong playerObjId = sa.Data.PartyList[task.playerIdx];
            bool tethered = false;

            var fwElement = new TetherFrameworkElement
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

            _udmP3Param.TetherFrameworkList.Add(fwElement);
            sa.DebugMsg(
                $"{fwElement.TaskName} | {sa.GetPlayerJobByIndex(fwElement.PlayerIndex)}({fwElement.PlayerIndex}) player {fwElement.PlayerObjId:X8} | black holes {fwElement.BlackHoleId:X8} | real region {task.realRegions[i]} | elements {fwElement.Name}");
        }
    }

    private void RunTetherFramework(ScriptAccessory sa,
        List<(int playerIdx, List<int> realRegions, List<ulong> blackHoles, string name)> tasks)
    {
        // Filter out the tether elements for this round
        List<TetherFrameworkElement> curTetherTaskFws = _udmP3Param.TetherFrameworkList.ToList();
        if (curTetherTaskFws.Count == 0) return;
        sa.DebugMsg(
            $"Round tasks {tasks.Count}, tether elements {curTetherTaskFws.Count}: {string.Join(" | ", tasks.Select(task => $"{task.name}:{task.blackHoles.Count}"))}");

        Dictionary<string, bool> goToSpotGuideDrawState = tasks.ToDictionary(task => task.name, _ => false);
        Dictionary<string, bool> lastTaskTetheredState = tasks.ToDictionary(task => task.name, _ => false);
        string goToSpotDrawName(string taskName) => $"DrawBlackHoleTether {taskName} G-GoToSpot";

        _udmP3Param.curTetherTaskFramework = sa.Method.RegistFrameworkUpdateAction(Action);
        void Action()
        {
            foreach (var fw in curTetherTaskFws)
            {
                var bh = sa.GetById(fw.BlackHoleId);
                if (bh == null)
                {
                    if (fw.lastBlackHoleExistState)
                    {
                        sa.DebugMsg($"{fw.TaskName} | {fw.Name} black hole {fw.BlackHoleId:X8} not available right now");
                        fw.lastBlackHoleExistState = false;
                    }
                    continue;
                }
                if (!fw.lastBlackHoleExistState)
                {
                    sa.DebugMsg($"{fw.TaskName} | {fw.Name} black hole {fw.BlackHoleId:X8} available again");
                    fw.lastBlackHoleExistState = true;
                }
                var curTetherTarget = sa.GetTetherSource((IBattleChara)bh, 0x54);
                var curTetherTargetList = string.Join(",", curTetherTarget.Select(x => x.ToString("X8")));
                if (curTetherTargetList != fw.lastTetherTargetList)
                {
                    var curTetherTargetJobList = string.Join(",", curTetherTarget.Select(x => sa.GetPlayerJobById((uint)x)));
                    sa.DebugMsg(
                        $"{fw.TaskName} | {fw.Name} tether target changed: {(curTetherTargetList == "" ? "none" : curTetherTargetList)} | {curTetherTargetJobList}");
                    fw.lastTetherTargetList = curTetherTargetList;
                }
                var justGotTether = fw.Tethered;
                var curHasTether = curTetherTarget.Contains(fw.PlayerObjId);
                fw.Tethered = curHasTether;

                if (curHasTether)
                {
                    // If we got the tether, there's no need to draw the guide to take it; clear the previous go-to-tether draws.
                    if (!fw.goToTetherGuideDrawn && justGotTether) continue;
                    if (fw.lastGoToTetherDrawName != "")
                        sa.Method.RemoveDraw(fw.lastGoToTetherDrawName);
                    sa.DebugMsg($"{fw.TaskName} | {fw.Name} got the tether, target {fw.PlayerObjId:X8}/{sa.GetPlayerJobByIndex(fw.PlayerIndex)}");
                    fw.goToTetherGuideDrawn = false;
                    fw.lastTetherTarget = 0;
                    fw.lastGoToTetherDrawName = "";
                    continue;
                }

                if (curTetherTarget.Count != 1)
                {
                    // If this tether isn't "clearly attached to one player" (nobody has it), clear the "go take tether" and "go to spot" draws
                    if (goToSpotGuideDrawState.GetValueOrDefault(fw.TaskName))
                    {
                        sa.Method.RemoveDraw(goToSpotDrawName(fw.TaskName));
                        goToSpotGuideDrawState[fw.TaskName] = false;
                        sa.DebugMsg($"{fw.TaskName} | {fw.Name} tether target count {curTetherTarget.Count}, removing go-to-spot guide");
                    }
                    if (!fw.goToTetherGuideDrawn) continue;
                    if (fw.lastGoToTetherDrawName != "")
                        sa.Method.RemoveDraw(fw.lastGoToTetherDrawName);
                    sa.DebugMsg($"{fw.TaskName} | {fw.Name} tether target count {curTetherTarget.Count}, removing go-to-tether guide");
                    fw.goToTetherGuideDrawn = false;
                    fw.lastTetherTarget = 0;
                    fw.lastGoToTetherDrawName = "";
                    continue;
                }

                // If this tether is attached to the wrong player
                var curTetherTargetObjId = curTetherTarget[0];
                if (fw.goToTetherGuideDrawn && curTetherTargetObjId == fw.lastTetherTarget) continue;
                if (goToSpotGuideDrawState.GetValueOrDefault(fw.TaskName))
                {
                    sa.Method.RemoveDraw(goToSpotDrawName(fw.TaskName));
                    goToSpotGuideDrawState[fw.TaskName] = false;
                    sa.DebugMsg($"{fw.TaskName} | {fw.Name} tether taken by wrong target {curTetherTargetObjId:X8}/{sa.GetPlayerJobById((uint)curTetherTargetObjId)}, removing go-to-spot guide");
                }
                if (fw.lastGoToTetherDrawName != "")
                    sa.Method.RemoveDraw(fw.lastGoToTetherDrawName);

                // Getting here means: we don't have the tether, the black hole clearly has a tether on a player, and it's the wrong player
                var curGoToTetherDrawName = $"{fw.goToTetherDrawNamePrefix} {curTetherTargetObjId:X8}";
                sa.DebugMsg(
                    $"{fw.TaskName} | {fw.Name} go take the tether: {sa.GetPlayerJobByIndex(fw.PlayerIndex)}({fw.PlayerObjId:X8}) -> {sa.GetPlayerJobById((uint)curTetherTargetObjId)}({curTetherTargetObjId:X8})");
                sa.DrawGuidance(fw.PlayerObjId, curTetherTargetObjId, 0, 7000, curGoToTetherDrawName, sa.Data.DefaultSafeColor);
                fw.goToTetherGuideDrawn = true;
                fw.lastTetherTarget = curTetherTargetObjId;
                fw.lastGoToTetherDrawName = curGoToTetherDrawName;
            }

            foreach (var taskFws in curTetherTaskFws.GroupBy(fw => fw.TaskName))
            {
                // groupby all the tether elements; if one player takes two tethers you get same-named TaskNames like R1 T1 5 0,2
                var taskName = taskFws.Key;
                var allTethered = taskFws.All(fw => fw.Tethered);
                if (allTethered != lastTaskTetheredState.GetValueOrDefault(taskName))
                {
                    sa.DebugMsg(
                        $"{taskName} tether done state changed: {allTethered} | {string.Join(",", taskFws.Select(fw => $"{fw.Name}={fw.Tethered}"))}");
                    lastTaskTetheredState[taskName] = allTethered;
                }
                if (!allTethered)
                {
                    if (!goToSpotGuideDrawState.GetValueOrDefault(taskName)) continue;
                    sa.Method.RemoveDraw(goToSpotDrawName(taskName));
                    goToSpotGuideDrawState[taskName] = false;
                    sa.DebugMsg($"{taskName} not all tethered, removing go-to-spot guide");
                    continue;
                }

                if (goToSpotGuideDrawState.GetValueOrDefault(taskName)) continue;
                foreach (var fw in taskFws)
                    sa.Method.RemoveDraw($"DrawBlackHoleTether {taskName} G-GoToTether {fw.Name}.*");

                var guideFw = taskFws.First();
                sa.DebugMsg($"Go to spot {taskName} | player {sa.GetPlayerJobByIndex(guideFw.PlayerIndex)}({guideFw.PlayerObjId:X8}) | GuidePos {guideFw.GuidePos}");
                // $"DrawBlackHoleTether {taskName} G-GoToSpot";
                sa.DrawGuidance(guideFw.PlayerObjId, guideFw.GuidePos, 0, 7000, goToSpotDrawName(taskName), sa.Data.DefaultSafeColor);
                goToSpotGuideDrawState[taskName] = true;
            }
        }
    }

    private void CleanupTetherFramework(ScriptAccessory sa)
    {
        foreach (var fw in _udmP3Param.TetherFrameworkList)
        {
            if (fw.FwHandle != "")
                sa.Method.UnregistFrameworkUpdateAction(fw.FwHandle);
        }
        _udmP3Param.TetherFrameworkList.Clear();

        if (_udmP3Param.curTetherTaskFramework != "")
            sa.Method.UnregistFrameworkUpdateAction(_udmP3Param.curTetherTaskFramework);
        _udmP3Param.curTetherTaskFramework = "";
    }

    /// <summary>Spot after taking the tether: rotate clockwise from the black hole's direction and stand at Chaos's (middle) max melee; the last tether stands at the edge so the laser points out</summary>
    private Vector3 CalcTetherSpot(int realRegion, bool isLastTether)
    {
        var bhpos = BlackHoleBasePos.RotateAndExtend(Center, realRegion * -45f.DegToRad());
        var (deg, r) = isLastTether ? (LastTetherCwAngle, LastTetherSpotRadius) : (TetherSpotCwAngle, TetherSpotRadius);
        return bhpos.RotateAndExtend(Center, -deg.DegToRad(), r - bhpos.GetLength(Center));
    }

    #region P3B1 Black Holes AoE (Cicero)

    [ScriptMethod(name:"P3 Earthquake (Phase Control)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:50546"],
        userControl:false)]

    public void P3_Earthquake_PhaseControl(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"P3 Earthquake Black Hole (Hitbox AoE)",
        eventType:EventTypeEnum.AddCombatant,
        eventCondition:["DataId:19512"])]

    public void P3_Earthquake_BlackHole_HitboxAoe(Event @event,ScriptAccessory accessory) {

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

        currentProperties.Name=$"P3_Earthquake_BlackHole_HitboxAoe_{sourceId}";
        currentProperties.Scale=new(2);
        currentProperties.Owner=sourceId;
        currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(1);
        currentProperties.DestoryAt=MAXIMUM_DURATION;

        accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Circle,currentProperties);

    }

    [ScriptMethod(name:"P3 Earthquake Black Hole (Clear Hitbox AoE)",
        eventType:EventTypeEnum.RemoveCombatant,
        eventCondition:["DataId:19512"],
        userControl:false)]

    public void P3_Earthquake_BlackHole_ClearHitboxAoe(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=3) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["SourceId"],out var sourceId)) {

            return;

        }

        accessory.Method.RemoveDraw($"P3_Earthquake_BlackHole_HitboxAoe_{sourceId}");

    }

    [ScriptMethod(name:"P3 Earthquake Black Hole (Tether AoE)",
        eventType:EventTypeEnum.VfxEvent,
        eventCondition:["Id:84"])]

    public void P3_Earthquake_BlackHole_TetherAoe(Event @event,ScriptAccessory accessory) {

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

            accessory.Method.RemoveDraw($"P3_Earthquake_BlackHole_TetherAoe_{sourceId}_{lastDrawing}");

            Interlocked.Increment(ref lastDrawing);
            phase3sub3_blackHoleDrawingCounter[sourceId]=lastDrawing;

            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Name=$"P3_Earthquake_BlackHole_TetherAoe_{sourceId}_{lastDrawing}";
            currentProperties.Scale=new(6,125);
            currentProperties.Owner=sourceId;
            currentProperties.TargetObject=targetId;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.DestoryAt=MAXIMUM_DURATION;

            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Rect,currentProperties);

        }

    }

    [ScriptMethod(name:"P3 Earthquake Black Hole (Clear Tether AoE 1)",
        eventType:EventTypeEnum.ActionEffect,
        eventCondition:["ActionId:47868"],
        userControl:false)]

    public void P3_Earthquake_BlackHole_ClearTetherAoe1(Event @event,ScriptAccessory accessory) {

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

                accessory.Method.RemoveDraw(@$"^P3_Earthquake_BlackHole_TetherAoe_{sourceId}_.*$");

                phase3sub3_blackHoleDrawingCounter.TryRemove(sourceId,out _);

            }

        }

    }

    [ScriptMethod(name:"P3 Earthquake Black Hole (Clear Tether AoE 2)",
        eventType:EventTypeEnum.RemoveCombatant,
        eventCondition:["DataId:19512"],
        userControl:false)]

    public void P3_Earthquake_BlackHole_ClearTetherAoe2(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=3) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["SourceId"],out var sourceId)) {

            return;

        }

        lock(phase3sub3_blackHoleDrawingCounter) {

            accessory.Method.RemoveDraw(@$"^P3_Earthquake_BlackHole_TetherAoe_{sourceId}_.*$");

            phase3sub3_blackHoleDrawingCounter.TryRemove(sourceId,out _);

        }

    }

    #endregion

    #endregion P3B1 Black Holes 3200

    #region P3B2 Blizzard III 3210

    [ScriptMethod(name: "———————— [P3B2 Blizzard III] ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P3B2_Separator(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "P3B2_Blizzard_SwitchPhase", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:47887"], userControl: Debugging)]
    public void P3B2_Blizzard_SwitchPhase(Event ev, ScriptAccessory sa)
    {
        _udmP3Param.CurrentStage = 3210;
        sa.DebugMsg($"Current stage: P3B2 Blizzard III {_udmP3Param.CurrentStage}");
        _udmP3Param.KefkaRegion = _udmP3Param.GetGiantKefkaRegion(sa, false);
        _udmP3Param.BlizzardNorthRegion = BlizKefkaIsNorth ? _udmP3Param.KefkaRegion : (_udmP3Param.KefkaRegion + 4) % 8;
        _udmP3Param.WestTowerRegion = (_udmP3Param.BlizzardNorthRegion + 2) % 8;
        _udmP3Param.EastTowerRegion = (_udmP3Param.BlizzardNorthRegion - 2 + 8) % 8;
        _udmP3Param.KefkaRegionAcquired.Set();
        sa.DebugMsg($"Blizzard north region inherited from Kefka region {_udmP3Param.KefkaRegion} CCW from C");
    }

    [ScriptMethod(name: "P3B2_Blizzard_BaitPath", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:47887"], userControl: true)]
    public void P3B2_Blizzard_BaitPath1(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.BlizzardNorthRegion == -1)
        {
            if (!_udmP3Param.KefkaRegionAcquired.WaitOne(2000)) return;
            _udmP3Param.KefkaRegionAcquired.Reset();
        }

        if (_udmP3Param.CurrentStage != 3210) return;
        _udmP3Param.DrawBlizzardBaitPath = true;

        foreach (var myIndex in GuidedPlayers(sa))
        {
            // LPDU first bait: supports north (Kefka side), DPS south
            var initPos = myIndex <= 3 ? new Vector3(100, 0, 110f) : new Vector3(100, 0, 90f);
            initPos = _udmP3Param.RotateFromKefka(initPos, _udmP3Param.BlizzardNorthRegion);

            var targetPos = BlizzardSecondPuddlePos(myIndex);
            if (IsMe(sa, myIndex))
                _udmP3Param.SecondPuddleBaitPos = targetPos;

            var owner = GuidanceOwner(sa, myIndex);
            sa.DrawGuidance(owner, initPos, 0, 3000, $"BlizzardPath1 Puddle 1 {myIndex}", sa.Data.DefaultSafeColor);
            sa.DrawGuidance(initPos, targetPos, 0, 3000, $"BlizzardPath1 Puddle 1->2 {myIndex}", sa.Data.DefaultDangerColor);
            sa.DrawGuidance(owner, targetPos, 3000, 3000, $"BlizzardPath1 Puddle 2 {myIndex}", sa.Data.DefaultSafeColor);
        }
    }

    /// <summary>
    /// Blizzard III left/right split (Kefka = north, west = left). Corners and towers are on the same side: supports in the two north corners, DPS in the two south corners.
    /// LPDU: T + melee west (MT+OT NW, M1+M2 SW), H + ranged east (H1+H2 NE, R1+R2 SE).
    /// PF: G1 (MT H1 M1 R1) west (MT+H1 NW, M1+R1 SW), G2 (OT H2 M2 R2) east (OT+H2 NE, M2+R2 SE).
    /// </summary>
    private bool BlizzardOnWestSide(int myIndex)
        => p3FollowPfStrats ? myIndex is 0 or 2 or 4 or 6 : myIndex is 0 or 1 or 4 or 5;

    /// <summary>Bait spot for Blizzard III's second yellow puddle: supports to the north diagonal, DPS to the south diagonal, left/right by BlizzardOnWestSide</summary>
    private Vector3 BlizzardSecondPuddlePos(int myIndex)
    {
        // Reference point toward Kefka, counterclockwise positive: NW 45°, NE -45°, SW 135°, SE -135°
        var rotDeg = (myIndex <= 3 ? 45f : 135f) * (BlizzardOnWestSide(myIndex) ? 1 : -1);
        var targetPos = new Vector3(100, 0, 114.14f).RotateAndExtend(Center, rotDeg.DegToRad());
        return _udmP3Param.RotateFromKefka(targetPos, _udmP3Param.BlizzardNorthRegion);
    }

    [ScriptMethod(name: "P3B2_Blizzard_BaitPath2", eventType: EventTypeEnum.TargetIcon,
        eventCondition: ["Id:00A1"], userControl: Debugging)]
    public void P3B2_Blizzard_BaitPath2(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.CurrentStage != 3210) return;
        if (!_udmP3Param.DrawBlizzardBaitPath) return;
        _udmP3Param.IsFirstStack = !_udmP3Param.IsFirstStack;

        var targetIdx = sa.GetPlayerIdIndex((uint)ev.TargetId);
        _udmP3Param.StackSpotIsTN = targetIdx <= 3;
        foreach (var myIndex in GuidedPlayers(sa))
        {
            if (_udmP3Param.NeedSoakTower(myIndex))
                DrawBlizzardTowerPath(sa, myIndex);
            else
                DrawBlizzardStackPath(sa, myIndex);
        }
    }

    private int GetTowerRegion(int myIndex)
        => BlizzardOnWestSide(myIndex) ? _udmP3Param.WestTowerRegion : _udmP3Param.EastTowerRegion;

    private void DrawBlizzardTowerPath(ScriptAccessory sa, int idx)
    {
        Vector3 stackPos = Center;
        var owner = GuidanceOwner(sa, idx);
        var towerRegion = GetTowerRegion(idx);
        Vector3 towerPos = new Vector3(100, 0, 110).RotateAndExtend(Center, towerRegion * 45f.DegToRad());

        // Kefka always stomps his right foot first, spawning the west tower; when soaking, the east tower players get delayed guidance, the west tower players go straight away
        bool towerDelay = towerRegion == (BlizKefkaIsNorth ? _udmP3Param.EastTowerRegion : _udmP3Param.WestTowerRegion);

        if (_udmP3Param.IsFirstStack)
        {
            sa.DrawGuidance(BlizzardSecondPuddlePos(idx), towerPos, 0, 3000, $"BlizzardPath2 Puddle 2->Tower {idx}", sa.Data.DefaultDangerColor);
            sa.DrawCircle(towerPos, 3000, towerDelay ? 3500 : 2500, $"BlizzardPath2 TowerAoe {towerRegion}", 5f, sa.Data.DefaultSafeColor);
            sa.DrawGuidance(owner, towerPos, 3000, towerDelay ? 3500 : 2500, $"BlizzardPath2 Puddle Tower {idx}", sa.Data.DefaultSafeColor);
            sa.DrawGuidance(towerPos, stackPos, 3000, towerDelay ? 3500 : 2500, $"BlizzardPath2 Tower->Stack {idx}", sa.Data.DefaultDangerColor);
        }
        else
        {
            sa.DrawCircle(towerPos, towerDelay ? 1000 : 0, towerDelay ? 3000 : 4000,
                $"BlizzardPath2 TowerAoe {towerRegion}", 6f, sa.Data.DefaultSafeColor);
            sa.DrawGuidance(owner, towerPos, towerDelay ? 1000 : 0, towerDelay ? 3000 : 4000,
                $"BlizzardPath2 Tower Round2 {idx}", sa.Data.DefaultSafeColor);
        }

    }

    private void DrawBlizzardStackPath(ScriptAccessory sa, int idx)
    {
        Vector3 stackPos = Center;
        var owner = GuidanceOwner(sa, idx);
        var towerRegion = GetTowerRegion(idx);
        Vector3 towerPos = new Vector3(100, 0, 110).RotateAndExtend(Center, towerRegion * 45f.DegToRad());
        bool towerDelay = towerRegion == (BlizKefkaIsNorth ? _udmP3Param.EastTowerRegion : _udmP3Param.WestTowerRegion);

        if (_udmP3Param.IsFirstStack)
        {
            sa.DrawGuidance(BlizzardSecondPuddlePos(idx), stackPos, 0, 3000, $"BlizzardPath2 Puddle 2->Stack {idx}", sa.Data.DefaultDangerColor);
            sa.DrawGuidance(owner, stackPos, 3000, towerDelay ? 3500 : 2500, $"BlizzardPath2 Puddle Stack {idx}", sa.Data.DefaultSafeColor);
            sa.DrawGuidance(stackPos, towerPos, 3000, towerDelay ? 3500 : 2500, $"BlizzardPath2 Stack->Tower {idx}", sa.Data.DefaultDangerColor);
        }
        else
        {
            sa.DrawGuidance(owner, stackPos, towerDelay ? 1000 : 0, towerDelay ? 3000 : 4000, $"BlizzardPath2 Stack Round2 {idx}", sa.Data.DefaultSafeColor);
        }
    }

    [ScriptMethod(name: "P3B2_KnockDown/BigBang", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:47875", "TargetIndex:1"], userControl: true)]
    public void P3B2_KnockDownSpots(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.CurrentStage != 3210) return;
        if (Vector3.Distance(_udmP3Param.FirstKnockDownPos, ev.TargetPosition) > 80)
            _udmP3Param.FirstKnockDownPos = ev.TargetPosition;
        else
        {
            sa.DrawCircle(ev.TargetPosition, 0, 15000, $"KnockDownSpot1", 6, sa.Data.DefaultDangerColor.WithW(2f));
            sa.DrawCircle(_udmP3Param.FirstKnockDownPos, 0, 15000, $"KnockDownSpot2", 6, sa.Data.DefaultDangerColor.WithW(2f));

            // Restore opacity while we're at it
            var obj1 = sa.GetById(_udmP3Param.ObjectId_Chaos);
            sa.AlphaModify(obj1, 1f, currentAlpha => currentAlpha <= 0.8f);
            var obj2 = sa.GetById(_udmP3Param.ObjectId_Exdeath);
            sa.AlphaModify(obj2, 1f, currentAlpha => currentAlpha <= 0.8f);
        }

    }

    [ScriptMethod(name: "P3B2_BigBang_ClearKnockDownSpots", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:47878", "TargetIndex:1"], userControl: Debugging)]
    public void P3B2_BigBang_ClearKnockDownSpots(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw($"KnockDownSpot.*");
        _udmP3Param.CurrentStage = 3300;
    }

    [ScriptMethod(name: "P3B2_BlizzardMoveCallout", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:47889"], userControl: true)]
    public async void P3B2_BlizzardMoveCallout(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.CurrentStage != 3210) return;

        var runId = _runId;
        await Task.Delay(1000);
        if (runId != _runId) return;
        if (_udmP3Param.CurrentStage != 3210) return;

        sa.Method.TextInfo($"Away from middle, move!", 3000);
        sa.Method.TTS($"Away from middle, move!", 3);
    }

    #region P3B2 Knock Down AoE (Cicero)

    [ScriptMethod(name:"P3 Earthquake Knock Down (AoE)",
        eventType:EventTypeEnum.TargetIcon,
        eventCondition:["Id:00A1"])]

    public void P3_Earthquake_KnockDown_Aoe(Event @event,ScriptAccessory accessory) {

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

    #endregion P3B2 Blizzard III 3210

    #endregion

    #region P4 Draws (Cicero)

    #region Major_Phase_4

    [ScriptMethod(name: "———————— [P4] ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"])]
    public void P4_Separator(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name:"P4 (Phase Control)",
        eventType:EventTypeEnum.PlayActionTimeline,
        eventCondition:["Id:7747"],
        userControl:false)]

    public void P4_PhaseControl(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"P4 Neo Exdeath (Data Collection)",
        eventType:EventTypeEnum.StatusAdd,
        eventCondition:["StatusID:2056"],
        userControl:false)]

    public void P4_NeoExdeath_DataCollection(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"P4 Chaos (Data Collection)",
        eventType:EventTypeEnum.StatusAdd,
        eventCondition:["StatusID:2056"],
        userControl:false)]

    public void P4_Chaos_DataCollection(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"P4 Cursed Shriek (Type Indicator)",
        eventType:EventTypeEnum.StatusAdd,
        eventCondition:["StatusID:5543"])]

    public void P4_CursedShriek_TypeIndicator(Event @event,ScriptAccessory accessory) {

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

            currentProperties.Name=$"P4_CursedShriek_TypeIndicator1_{targetId}";
            currentProperties.Scale=new(0.125f,6);
            currentProperties.Owner=targetId;
            currentProperties.FixRotation=true;
            currentProperties.Rotation=float.Pi/4;
            currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(4);
            currentProperties.Delay=delay;
            currentProperties.DestoryAt=duration;

            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Straight,currentProperties);

            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Name=$"P4_CursedShriek_TypeIndicator2_{targetId}";
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

            currentProperties.Name=$"P4_CursedShriek_TypeIndicator1_{targetId}";
            currentProperties.Scale=new(0.125f,2);
            currentProperties.Owner=targetId;
            currentProperties.FixRotation=true;
            currentProperties.Rotation=-(float.Pi/4*3);
            currentProperties.Color=accessory.Data.DefaultSafeColor.WithW(4);
            currentProperties.Delay=delay;
            currentProperties.DestoryAt=duration;

            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Rect,currentProperties);

            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Name=$"P4_CursedShriek_TypeIndicator2_{targetId}";
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

    [ScriptMethod(name:"P4 Cursed Shriek (Facing Indicator)",
        eventType:EventTypeEnum.StatusAdd,
        eventCondition:["StatusID:5543"])]

    public void P4_CursedShriek_FacingIndicator(Event @event,ScriptAccessory accessory) {

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
            currentProperties.Name=$"P4_CursedShriek_FacingIndicator_{targetId}_{partyIndex}";
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

    [ScriptMethod(name:"P4 Cursed Shriek (Clear Indicators)",
        eventType:EventTypeEnum.StatusRemove,
        eventCondition:["StatusID:5543"],
        userControl:false)]

    public void P4_CursedShriek_ClearIndicators(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=4) {

            return;

        }

        if(string.Equals(@event["SourceId"],"00000000")) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        accessory.Method.RemoveDraw($"P4_CursedShriek_TypeIndicator1_{targetId}");
        accessory.Method.RemoveDraw($"P4_CursedShriek_TypeIndicator2_{targetId}");
        accessory.Method.RemoveDraw($"P4_CursedShriek_FacingIndicator_{targetId}(_\\d)?");

    }

    [ScriptMethod(name:"P4 Forked Lightning (AoE)",
        eventType:EventTypeEnum.StatusAdd,
        eventCondition:["StatusID:5544"])]

    public void P4_ForkedLightning_Aoe(Event @event,ScriptAccessory accessory) {

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

        currentProperties.Name=$"P4_ForkedLightning_Aoe_{targetId}";
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

    [ScriptMethod(name:"P4 Forked Lightning (Clear AoE)",
        eventType:EventTypeEnum.StatusRemove,
        eventCondition:["StatusID:5544"],
        userControl:false)]

    public void P4_ForkedLightning_ClearAoe(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=4) {

            return;

        }

        if(string.Equals(@event["SourceId"],"00000000")) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        accessory.Method.RemoveDraw($"P4_ForkedLightning_Aoe_{targetId}");

    }

    [ScriptMethod(name:"P4 Compressed Water (AoE)",
        eventType:EventTypeEnum.StatusAdd,
        eventCondition:["StatusID:5545"])]

    public void P4_CompressedWater_Aoe(Event @event,ScriptAccessory accessory) {

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

        currentProperties.Name=$"P4_CompressedWater_Aoe_{targetId}";
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

    [ScriptMethod(name:"P4 Compressed Water (Clear AoE)",
        eventType:EventTypeEnum.StatusRemove,
        eventCondition:["StatusID:5545"],
        userControl:false)]

    public void P4_CompressedWater_ClearAoe(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=4) {

            return;

        }

        if(string.Equals(@event["SourceId"],"00000000")) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        accessory.Method.RemoveDraw($"P4_CompressedWater_Aoe_{targetId}");

    }

    [ScriptMethod(name:"P4 Entropy (Bait AoE)",
        eventType:EventTypeEnum.StatusAdd,
        eventCondition:["StatusID:5547"])]

    public void P4_Entropy_BaitAoe(Event @event,ScriptAccessory accessory) {

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

            currentProperties.Name=$"P4_Entropy_BaitAoe_{targetId}";
            currentProperties.Scale=new(6);
            currentProperties.Owner=targetId;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.Delay=delay;
            currentProperties.DestoryAt=duration;

            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,currentProperties);

        }

        else {

            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Name=$"P4_Entropy_BaitAoe_{targetId}";
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

    [ScriptMethod(name:"P4 Entropy (Clear Bait AoE)",
        eventType:EventTypeEnum.StatusRemove,
        eventCondition:["StatusID:5547"],
        userControl:false)]

    public void P4_Entropy_ClearBaitAoe(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=4) {

            return;

        }

        if(string.Equals(@event["SourceId"],"00000000")) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        accessory.Method.RemoveDraw($"P4_Entropy_BaitAoe_{targetId}");

    }

    [ScriptMethod(name:"P4 Dynamic Fluid (Bait AoE)",
        eventType:EventTypeEnum.StatusAdd,
        eventCondition:["StatusID:5548"])]

    public void P4_DynamicFluid_BaitAoe(Event @event,ScriptAccessory accessory) {

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

            currentProperties.Name=$"P4_DynamicFluid_BaitAoe_{targetId}";
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

            currentProperties.Name=$"P4_DynamicFluid_BaitAoe_{targetId}";
            currentProperties.Scale=new(6);
            currentProperties.Owner=targetId;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.Delay=delay;
            currentProperties.DestoryAt=duration;

            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,currentProperties);

        }

    }

    [ScriptMethod(name:"P4 Dynamic Fluid (Clear Bait AoE)",
        eventType:EventTypeEnum.StatusRemove,
        eventCondition:["StatusID:5548"],
        userControl:false)]

    public void P4_DynamicFluid_ClearBaitAoe(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=4) {

            return;

        }

        if(string.Equals(@event["SourceId"],"00000000")) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        accessory.Method.RemoveDraw($"P4_DynamicFluid_BaitAoe_{targetId}");

    }

    [ScriptMethod(name:"P4 Stray Flames (AoE)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:regex:^(47906|47907)$"])]

    public void P4_StrayFlames_Aoe(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"P4 Stray Spray (AoE)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:regex:^(47908|47909)$"])]

    public void P4_StraySpray_Aoe(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"P4 Beyond Death & Allagan Field (Data Collection)",
        eventType:EventTypeEnum.StatusAdd,
        eventCondition:["StatusID:regex:^(1382|454|5464)$"],
        userControl:false)]

    public void P4_BeyondDeathAndAllaganField_DataCollection(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"P4 White Wound & Black Wound (Data Collection)",
        eventType:EventTypeEnum.StatusAdd,
        eventCondition:["StatusID:regex:^(5541|5542|4887|4888)$"],
        userControl:false)]

    public void P4_WhiteAndBlackWound_DataCollection(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"P4 Edge of Death (AoE)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:50070"])]

    public void P4_EdgeOfDeath_Aoe(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"P4 White Antilight & Black Antilight (Safe Zone)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:regex:^(50068|50069)$"])]

    public void P4_WhiteAndBlackAntilight_SafeZone(Event @event,ScriptAccessory accessory) {

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

    #region P4 Guidance (RyougiMio)

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
    private static readonly string[] PartyPriorityLabels = new[] { "MT", "OT", "H1", "H2", "M1", "M2", "R1", "R2" };
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
            case P4HalfColor.Blue: return "Blue";
            case P4HalfColor.Purple: return "Purple";
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

    // LPDU: stacks supports north / DPS south, spreads supports west / DPS east (RaidPlan guufe9q559evt7pj Set #1, #4)
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
        if (state == P4StateTrue) return "Stop!";
        if (state == P4StateFalse) return "Keep moving!";
        return null;
    }

    private static string P4PetrifyCallText(int state, bool isSelf)
    {
        if (state == P4StateTrue) return isSelf ? "Your gaze: look away" : "Look away from gaze";
        if (state == P4StateFalse) return isSelf ? "Your gaze: look at" : "Look at gaze";
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
            case P4ElementCall.FireSteel: return "Fire out: stack middle, then spread";
            case P4ElementCall.WaterMoon: return "Water in: stack middle, then stay";
            default: return null;
        }
    }

    private static string FormatP4ElementCall(P4ElementCall call)
    {
        switch (call)
        {
            case P4ElementCall.FireSteel: return "Fire out";
            case P4ElementCall.WaterMoon: return "Water in";
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
                DebugEcho(accessory, $"P4 chain stage{step}Delayed run failed: {ex.Message}");
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
                DebugEcho(accessory, $"P4 5546 delayed TTS failed: {ex.Message}");
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
                DebugEcho(accessory, $"P4 party chat callout failed: {ex.Message}");
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
            var parts = new List<string> { isThunder ? "Spread" : "Stack" };
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

    /// <summary>Draws for me when partyIndex is null; draw names carry the index so the whole-party Debug draws don't overwrite each other</summary>
    private void DrawP4SelfGuide(ScriptAccessory accessory, string guideName, Vector3 target, int duration, int delay = 0, int partyIndex = -1)
    {
        if (partyIndex < 0)
            partyIndex = GetMyIndex(accessory);
        DrawGuide(accessory, $"{guideName}_{partyIndex}", target, duration, delay, partyIndex: partyIndex);
    }

    // 5543 Cursed Shriek (petrify) LPDU: only guide the two holders under the boss (middle), ignore everyone else. RaidPlan guufe9q559evt7pj Set #2 / #5
    private void ExecuteP4PetrifyGroup(ScriptAccessory accessory, P4BuffGroup group, int duration, string drawName)
    {
        var petrifies = group.Records.Where(r => r.StatusId == 5543).ToList();
        var holders = petrifies.Select(r => r.PartyIndex).ToHashSet();
        var state = petrifies.FirstOrDefault()?.State ?? P4StateUnknown;

        var myIndex = GetMyIndex(accessory);
        if (holders.Contains(myIndex))
            Alert("Shriek: go under boss");

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

        var text = call == P4ElementCall.FireSteel ? "Stack middle, then out" : call == P4ElementCall.WaterMoon ? "Stack middle, then in" : null;
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

        // LPDU final: the whole party stacks on one safe spot (RaidPlan guufe9q559evt7pj Set #6.2). Donut = hug the middle, chariot = stand outside the boss hitbox
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
            : "Unknown job";
    }

    private static string JobShortName(uint classJobRowId)
    {
        switch (classJobRowId)
        {
            case 1: return "GLA";
            case 2: return "PGL";
            case 3: return "MRD";
            case 4: return "LNC";
            case 5: return "ARC";
            case 6: return "CNJ";
            case 7: return "THM";
            case 19: return "PLD";
            case 20: return "MNK";
            case 21: return "WAR";
            case 22: return "DRG";
            case 23: return "BRD";
            case 24: return "WHM";
            case 25: return "BLM";
            case 26: return "ACN";
            case 27: return "SMN";
            case 28: return "SCH";
            case 29: return "ROG";
            case 30: return "NIN";
            case 31: return "MCH";
            case 32: return "DRK";
            case 33: return "AST";
            case 34: return "SAM";
            case 35: return "RDM";
            case 36: return "BLU";
            case 37: return "GNB";
            case 38: return "DNC";
            case 39: return "RPR";
            case 40: return "SGE";
            case 41: return "VPR";
            case 42: return "PCT";
            default: return "Unknown";
        }
    }

    private static string JobChineseName(uint classJobRowId)
    {
        switch (classJobRowId)
        {
            case 1: return "Gladiator";
            case 2: return "Pugilist";
            case 3: return "Marauder";
            case 4: return "Lancer";
            case 5: return "Archer";
            case 6: return "Conjurer";
            case 7: return "Thaumaturge";
            case 19: return "Paladin";
            case 20: return "Monk";
            case 21: return "Warrior";
            case 22: return "Dragoon";
            case 23: return "Bard";
            case 24: return "White Mage";
            case 25: return "Black Mage";
            case 26: return "Arcanist";
            case 27: return "Summoner";
            case 28: return "Scholar";
            case 29: return "Rogue";
            case 30: return "Ninja";
            case 31: return "Machinist";
            case 32: return "Dark Knight";
            case 33: return "Astrologian";
            case 34: return "Samurai";
            case 35: return "Red Mage";
            case 36: return "Blue Mage";
            case 37: return "Gunbreaker";
            case 38: return "Dancer";
            case 39: return "Reaper";
            case 40: return "Sage";
            case 41: return "Viper";
            case 42: return "Pictomancer";
            default: return "Unknown job";
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

        DebugEcho(accessory, $"P4 part-1 resolve fourthParam={fourthParam} sourceSide={FormatP4Side(sourceSide)} left={FormatP4Color(leftColor)} right={FormatP4Color(rightColor)} new12={FormatPosition(newTwelveDirection)} L1={FormatPosition(l1)} R1={FormatPosition(r1)}");

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
            DebugEcho(accessory, $"P4 loop move guide drawn: round={round} my={PartyPriorityLabel(myIndex)} clock={FormatP4Clock(clocks[myIndex])} target={FormatPosition(target)} duration={P4ChainGuideDurationMs}ms");

            if (!string.IsNullOrWhiteSpace(moveCalls[myIndex]))
                ScheduleP4SelfTtsAtRemaining3(accessory, generation, round, myIndex, moveCallStates[myIndex], moveCallRemaining[myIndex], moveCalls[myIndex]);
        }
        else
        {
            DebugEcho(accessory, $"P4 loop move guide not drawn: round={round} myIndex={myIndex}");
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

    // Guidance style matches P1~P3: green arrow (WaypointDp, default safe colour)
    private void DrawGuide(ScriptAccessory accessory, string name, Vector3 targetPosition, int duration, int delay = 0, int partyIndex = -1)
    {
        var dp = accessory.WaypointDp(targetPosition, (uint)duration, (uint)Math.Max(0, delay), name);
        dp.Owner = partyIndex < 0 ? GetMyId(accessory) : GuidanceOwner(accessory, partyIndex);
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    #endregion

    #region Mechanisms

    [ScriptMethod(name: "P4 Start", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:49884"], userControl: false)]
    public void P4_Start(Event @event, ScriptAccessory accessory)
    {
        _acc = accessory;
        _p4Phase = Phase.P4;
        ResetMechanic(accessory);
    }

    [ScriptMethod(name: "P4 2056 Real/Fake Window", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2056"], userControl: false)]
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

    [ScriptMethod(name: "P4 Status Variable Assignment", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(554[1-8]|454|1382|4887|4888|5464)$"], userControl: false)]
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

    [ScriptMethod(name: "P4 50070 Follow-up Loop Start", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:50070"], userControl: false)]
    public void P4_ChainStartActionEffect(Event @event, ScriptAccessory accessory)
    {
        _acc = accessory;
        if (_p4Phase != Phase.P4) return;

        accessory.Method.RemoveDraw($"{P4DrawPrefix}_P4_First_.*");
        DebugEcho(accessory, "P4 later timeline start by 50070");
        ScheduleP4LaterGroupsIfReady(accessory);
    }

    [ScriptMethod(name: "P4 Follow-up Status Removal Advance", eventType: EventTypeEnum.StatusRemove, eventCondition: ["StatusID:regex:^554[3-8]$"], userControl: false)]
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

    [ScriptMethod(name: "P4 Follow-up Danger Zone Capture", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(47777|47775|47768|47774)$"], userControl: false)]
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

    [ScriptMethod(name: "P4 Part 1 Half-Arena Guidance", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(50068|50069)$"], userControl: false)]
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
                DebugEcho(accessory, $"P4 part-1 resolve skipped: 4th 19510/2056 not ready param={fourthParam} target={FormatPosition(_p4FourthTargetPosition)}");
                return;
            }

            midpoint = new Vector3(
                (blueSourcePosition.X + purpleSourcePosition.X) * 0.5f,
                (blueSourcePosition.Y + purpleSourcePosition.Y) * 0.5f,
                (blueSourcePosition.Z + purpleSourcePosition.Z) * 0.5f);

            if (!TryNormalizeFromCenter(midpoint, out newTwelveDirection))
            {
                DebugEcho(accessory, $"P4 part-1 resolve skipped: 50068/50069 midpoint invalid blue50069={FormatPosition(blueSourcePosition)} purple50068={FormatPosition(purpleSourcePosition)} mid={FormatPosition(midpoint)}");
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
                DebugEcho(accessory, $"P4 part-1 guide drawn: my={PartyPriorityLabel(myIndex)} side={FormatP4Side(mySide)} color={FormatP4Color(colors[myIndex])} target={FormatPosition(targetPosition)} duration={P4GuideDurationMs}ms");
            }
            else
            {
                DebugEcho(accessory, $"P4 part-1 guide not drawn: my={PartyPriorityLabel(myIndex)} side={FormatP4Side(mySide)} color={FormatP4Color(colors[myIndex])}");
            }
        }
        else
        {
            DebugEcho(accessory, $"P4 part-1 guide not drawn: myIndex={myIndex} partyCount={accessory.Data.PartyList.Count}");
        }

        DebugP4Resolve(accessory, states, sides, colors, fourthParam, blueSide, leftColor, rightColor, newTwelveDirection, l1, r1);
    }

    #endregion

    #endregion

    #region P5 Draws (Cicero)

    #region Major_Phase_5

    [ScriptMethod(name: "———————— [P5] ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"])]
    public void P5_Separator(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name:"P5 (Phase Control)",
        eventType:EventTypeEnum.Targetable,
        eventCondition:["DataId:19511"],
        userControl:false)]

    public void P5_PhaseControl(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"P5 Maddening Orchestra Holy (AoE)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:47952"])]

    public void P5_MaddeningOrchestra_Holy_Aoe(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"P5 Maddening Orchestra Flare & Chaotic Flare (AoE)",
        eventType:EventTypeEnum.StartCasting,
        eventCondition:["ActionId:47952"])]

    public void P5_MaddeningOrchestra_FlareAndChaoticFlare_Aoe(Event @event,ScriptAccessory accessory) {

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

    [ScriptMethod(name:"P5 Maddening Orchestra Extra Holy (AoE)",
        eventType:EventTypeEnum.StatusAdd,
        eventCondition:["StatusID:5351"])]

    public void P5_MaddeningOrchestra_ExtraHoly_Aoe(Event @event,ScriptAccessory accessory) {

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

        currentProperties.Name=$"P5_MaddeningOrchestra_ExtraHoly_Aoe_{targetId}";
        currentProperties.Scale=new(6);
        currentProperties.Owner=targetId;
        currentProperties.Color=colourOfDirectionIndicators.V4.WithW(1);
        currentProperties.Delay=delay;
        currentProperties.DestoryAt=duration;

        accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,currentProperties);

    }

    [ScriptMethod(name:"P5 Maddening Orchestra Extra Holy (Clear AoE)",
        eventType:EventTypeEnum.StatusRemove,
        eventCondition:["StatusID:5351"],
        userControl:false)]

    public void P5_MaddeningOrchestra_ExtraHoly_ClearAoe(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=5) {

            return;

        }

        if(string.Equals(@event["SourceId"],"00000000")) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        accessory.Method.RemoveDraw($"P5_MaddeningOrchestra_ExtraHoly_Aoe_{targetId}");

    }

    [ScriptMethod(name:"P5 Maddening Orchestra Extra Flare (AoE)",
        eventType:EventTypeEnum.StatusAdd,
        eventCondition:["StatusID:5350"])]

    public void P5_MaddeningOrchestra_ExtraFlare_Aoe(Event @event,ScriptAccessory accessory) {

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

        currentProperties.Name=$"P5_MaddeningOrchestra_ExtraFlare_Aoe_{targetId}";
        currentProperties.Scale=new(25);
        currentProperties.Owner=targetId;
        currentProperties.Color=accessory.Data.DefaultDangerColor;
        currentProperties.Delay=delay;
        currentProperties.DestoryAt=duration;

        accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,currentProperties);

    }

    [ScriptMethod(name:"P5 Maddening Orchestra Extra Flare (Clear AoE)",
        eventType:EventTypeEnum.StatusRemove,
        eventCondition:["StatusID:5350"],
        userControl:false)]

    public void P5_MaddeningOrchestra_ExtraFlare_ClearAoe(Event @event,ScriptAccessory accessory) {

        if(majorPhase!=5) {

            return;

        }

        if(string.Equals(@event["SourceId"],"00000000")) {

            return;

        }

        if(!convertObjectIdToDecimal(@event["TargetId"],out var targetId)) {

            return;

        }

        accessory.Method.RemoveDraw($"P5_MaddeningOrchestra_ExtraFlare_Aoe_{targetId}");

    }

    #endregion

    #endregion

    #region P5 Draws & Guidance (Errer)

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
    private const int AdvanceDrawMs = 1500;     // How long before the cast ends to start drawing the exaflare circles
    private const float FloodMaxDist = 8f;      // A Flood intersection further than this from the middle is treated as an invalid group
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
    private const int FireSequenceGapMs = 10000;  // Beyond this gap it's a new sequence and may be re-initialized
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
    private int _fireDrawGen;   // Only ever increases, so old and new draw names never overlap
    private bool _fireGuideInitialized;

    // The safe spot direction comes from the role index (FireAutoStrategies, everyone Center if the LPDU strat is checked), so routes differ per player;
    // one track per direction, each with its own phases and route, the guide arrow drawn for whoever walks that track
    private sealed class FireGuideTrack
    {
        public FireSafeStrategy Strategy;
        public readonly List<int> PartyIndices = new();
        public int LeftCol;
        public int RightCol;
        public Vector3 SafeCenter = new(100f, 0f, 100f);
        public int Gen;     // Draw code of the current guide segment, see ClearFireTrackDraws
        public bool InitialPointDrawn;
        public bool RouteCompleted;
        public bool FirstHalfDrawn;
        public readonly List<int> Cols = new();
        public readonly List<int> Rels = new();
    }

    private readonly List<FireGuideTrack> _fireTracks = new();
    // Track currently being processed; only switched inside _fireLock
    private FireGuideTrack _curFire = new();
    private int _fireCandidateLeftCol { get => _curFire.LeftCol; set => _curFire.LeftCol = value; }
    private int _fireCandidateRightCol { get => _curFire.RightCol; set => _curFire.RightCol = value; }
    private Vector3 _fireSafeCenter { get => _curFire.SafeCenter; set => _curFire.SafeCenter = value; }
    private bool _fireInitialPointDrawn { get => _curFire.InitialPointDrawn; set => _curFire.InitialPointDrawn = value; }
    private bool _fireRouteCompleted { get => _curFire.RouteCompleted; set => _curFire.RouteCompleted = value; }
    private bool _fireFirstHalfDrawn { get => _curFire.FirstHalfDrawn; set => _curFire.FirstHalfDrawn = value; }
    private List<int> _fireGuideCols => _curFire.Cols;
    private List<int> _fireGuideRels => _curFire.Rels;
    // The framework runs each method on its own thread with no serialization, so 47931 (init) and 47932 (step) mutate the fire state concurrently;
    // a late 47931 would Clear the recorded phases, shifting the n index and picking the wrong route branch, so it's serialized with a lock.
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

    private static readonly string[] TowerLabels = { "MT", "OT", "H1", "H2", "M1", "M2", "R1", "R2" };

    private readonly object _towerLock = new();
    private readonly Dictionary<Vector3, TowerElement> _towerRoundTowers = new();
    private readonly Dictionary<int, (TowerElement Element, DateTime ExpireAt)> _towerDebuffs = new();
    private readonly Dictionary<int, Vector3> _towerRawTargets = new();
    private readonly HashSet<TowerElement> _towerSeenDouble = new();
    private readonly HashSet<int> _towerIdlePlayers = new();
    private bool _towerCooling, _towerIdleLocked;
    private int _towerMechanicSeq;
    private int _towerRound;

    // ── Flood 2-then-1: every 4 casts are one group, paired by opposite direction ──
    private int _floodCount;
    private int _floodGroupCount;
    private readonly Vector3[] _floodNegPos = new Vector3[2]; private readonly float[] _floodNegRot = new float[2]; private int _negCount;
    private readonly Vector3[] _floodPosPos = new Vector3[2]; private readonly float[] _floodPosRot = new float[2]; private int _posCount;
    private readonly float[] _floodSeqRot = new float[4]; private int _seqCount;
    private Vector3? _savedFloodSafe1;
    private Vector3? _savedFloodMid1;
    // The framework runs each event handler on its own thread (ScriptManager: one thread per method, no serialization), so paired casts enter OnWater concurrently;
    // Flood state changes must be serialized with a lock to avoid lost updates (_floodCount / counters / array races).
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

    #region Exaflare Circles

    // 47932 exaflare: each cast explodes 7 times stepping along its facing, 6m circles, 7m / 700ms apart
    [ScriptMethod(name: "P5 Exaflares (Exaflare AoE)", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:47932"])]
    public void OnFireCast(Event @event, ScriptAccessory accessory)
    {
        var pos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
        var dir = RotationToDirection(float.Parse(@event["SourceRotation"]));
        var castDurationMs = DurationMs(@event, 5000);
        var delayMs = Math.Max(0, castDurationMs - AdvanceDrawMs);
        // The counter only gives distinct dp.Names to multiple exaflare lines casting at once
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

    // 47931 exaflare start: init the tracks by safe-spot direction and draw the starting spot; 47932 each exaflare line: record its phase, advance the route;
    // 47934 Stray Entropy spread: exaflares are done, remove the safe spot
    [ScriptMethod(name: "P5 Exaflares (Safe Spot Guidance)", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47931|47932|47934)$"])]
    public void OnFireSafeGuide(Event @event, ScriptAccessory accessory)
    {
        var actionId = @event.ActionId();
        if (actionId == 47934)
        {
            RemoveFireSafeGuideDraws(accessory);
            return;
        }

        // The fire state (phase list / init flag) is mutated concurrently by the framework's threads, so it's serialized as a whole.
        lock (_fireLock)
        {
            EnsureFireSequenceInitialized(accessory);
            _lastFireActivityTime = DateTime.Now;
            if (actionId == 47932)
                HandleFireSafeGuideCast(accessory, JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]));
        }
    }

    // Idempotent init: only reset the fire state and redraw the safe spot when a "new sequence" is detected.
    // New sequence: not initialized yet, or more than FireSequenceGapMs since the last fire event.
    // That way a late 47931 won't wipe the phases 47932 already recorded in the same sequence.
    // The caller must already hold _fireLock.
    private void EnsureFireSequenceInitialized(ScriptAccessory accessory)
    {
        var gapMs = (DateTime.Now - _lastFireActivityTime).TotalMilliseconds;
        var isNewSequence = !_fireGuideInitialized || gapMs > FireSequenceGapMs;
        if (!isNewSequence)
            return;

        // Only remove each track's previous code; removing the whole prefix would also wipe the new draws made right below (see ClearFireTrackDraws)
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
            accessory.Method.SendChat($"/e [ExaSafe] dir={strategy} L={_fireCandidateLeftCol} R={_fireCandidateRightCol} center=({_fireSafeCenter.X:F1},{_fireSafeCenter.Z:F1})");
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
            accessory.Method.SendChat($"/e [ExaSafe] n={n} side={(isLeft ? "L" : "R")} cols={string.Join(":", _fireGuideCols)} rels={string.Join(":", _fireGuideRels)}");

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
            accessory.Method.SendChat($"/e [ExaSafe] route14 first half dirN4={dirN4} delay={delay}ms");
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

        // The second half replaces the first half, otherwise two green lines show at once
        ClearFireTrackDraws(accessory);
        DrawFireGuideTo(accessory, "SecondHalf_P2", p2, 0, delay);
        DrawFirePreview(accessory, "SecondHalf_Preview", p2, p3, 0, delay);
        DrawFireGuideTo(accessory, "SecondHalf_P3", p3, delay, total - delay);
        _fireRouteCompleted = true;

        if (enableDebugLogging)
            accessory.Method.SendChat($"/e [ExaSafe] route14 second half dir4={dir4} clockwise={clockwise} delay={delay}ms");
    }

    private void DrawFireInitialPoint(ScriptAccessory accessory, int n, int rel)
    {
        var dirN4 = n == 2 ? (rel == 1 ? 2 : 0) : (rel == 1 ? 3 : 1);
        var pos = FireDiagonalPoint(dirN4);
        ClearFireTrackDraws(accessory);
        DrawFireGuideTo(accessory, "InitialPoint", pos, 0, FireInitialPointDurationMs);

        if (enableDebugLogging)
            accessory.Method.SendChat($"/e [ExaSafe] initial diagonal n={n} rel={rel} dirN4={dirN4}");
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
            accessory.Method.SendChat($"/e [ExaSafe] {suffix} dir4={dir4} clockwise={clockwise} delay1={delay1}ms delay2={delay2}ms");
    }

    // Draw name: {FireSafePrefix}_{direction}_g{code}_{suffix}[_{index}]
    private string FireDrawName(string suffix) => $"{FireSafePrefix}_{_curFire.Strategy}_g{_curFire.Gen}_{suffix}";

    private void DrawFireGuideTo(ScriptAccessory accessory, string suffix, Vector3 target, int delayMs, int durationMs)
    {
        foreach (var i in _curFire.PartyIndices)
            P5Guide(accessory, FireDrawName(suffix), i, target, delayMs, durationMs);
    }

    private void DrawFirePreview(ScriptAccessory accessory, string suffix, Vector3 from, Vector3 to, int delayMs, int durationMs)
        => P5Preview(accessory, FireDrawName(suffix), from, to, delayMs, durationMs);

    // A new route segment replaces the guidance this track drew before; don't remove other directions' tracks along with it.
    // RemoveDraw runs on the main thread a frame later with an unanchored regex, while an Imgui guide SendDraw is queued immediately: removing then drawing with the same prefix
    // wipes the new draws too (that's why the exaflares used to get no guidance at all). So each segment gets a new code and only the old code is removed
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
        // LPDU: everyone shares one track, not split by role
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

    #region Chariot/Donut (Catastrophic Choice)

    // 49742 Catastrophic Choice (Quake): 10m chariot under the boss
    [ScriptMethod(name: "P5 Catastrophic Choice Chariot (AoE)", eventType: EventTypeEnum.StartCasting,
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

    // 49743 Catastrophic Choice (Tornado): 50m donut, safe within 10m
    [ScriptMethod(name: "P5 Catastrophic Choice Donut (AoE)", eventType: EventTypeEnum.StartCasting,
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

    #region Stray Entropy Spread Circles

    // 47934 Stray Entropy: everyone spreads 5m after the cast ends
    [ScriptMethod(name: "P5 Stray Entropy Spread (AoE)", eventType: EventTypeEnum.StartCasting,
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

    #region Celestriad Tower Guidance

    [ScriptMethod(name: "P5 Celestriad Towers Start", eventType: EventTypeEnum.StartCasting,
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
        if (enableDebugLogging) accessory.Method.SendChat($"/e [Tower] mechanic start, {delay}ms until idle players are locked");
        _ = Task.Run(async () => { await Task.Delay(delay); LockTowerIdlePlayers(accessory, seq); });
    }

    // Celestriad towers: each round, once all 4 fire/ice/thunder towers (ObjectEffect Id1=16) are up, wait for the new vulns to land, then assign by element and guide
    // The mechanic-start / vuln-record methods only collect data; turn this method off and nothing is assigned or drawn
    [ScriptMethod(name: "P5 Celestriad Towers (Guidance)", eventType: EventTypeEnum.ObjectEffect,
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

        if (enableDebugLogging) accessory.Method.SendChat($"/e [Tower] {DateTime.Now:HH:mm:ss.fff} {TowerElementLabel((TowerElement)dataId)} ({pos.X:F1},{pos.Z:F1}) n={count}");
        if (shouldAssign) _ = Task.Run(async () => await WaitAndAssignTowerGuides(accessory));
    }

    [ScriptMethod(name: "P5 Celestriad Tower Vulnerabilities", eventType: EventTypeEnum.StatusAdd,
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
        if (enableDebugLogging) accessory.Method.SendChat($"/e [Vuln] {TowerPlayerLabel(partyIndex)} → {TowerElementLabel(element)} until {expireAt:HH:mm:ss.fff}");
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

        if (enableDebugLogging) accessory.Method.SendChat($"/e [Tower] waiting for new vulns {waited}ms count={CountCurrentTowerDebuffs()}/6");
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
            if (enableDebugLogging) accessory.Method.SendChat($"/e [Tower] round {round} bad tower count {towers.Count}, skipping");
            return;
        }

        var groups = towers.GroupBy(t => t.Element).ToDictionary(g => g.Key, g => g.ToList());
        if (groups.Count != 3 || groups.Values.Count(g => g.Count == 2) != 1)
        {
            if (enableDebugLogging) accessory.Method.SendChat($"/e [Tower] round {round} bad element distribution {string.Join(" ", groups.Select(g => $"{TowerElementLabel(g.Key)}x{g.Value.Count}"))}");
            return;
        }

        var doubleElement = groups.First(g => g.Value.Count == 2).Key;
        if (!_towerSeenDouble.Add(doubleElement) && enableDebugLogging) accessory.Method.SendChat($"/e [Tower] double-tower element repeated: {TowerElementLabel(doubleElement)}");
        if (enableDebugLogging)
        {
            accessory.Method.SendChat($"/e ═══ round {round} towers{towers.Count} double{TowerElementLabel(doubleElement)} ═══");
            foreach (var t in towers) accessory.Method.SendChat($"/e   {TowerElementLabel(t.Element)} tower @({t.Pos.X:F1},{t.Pos.Z:F1}) ang={t.Angle:F2}");
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
            if (enableDebugLogging) accessory.Method.SendChat($"/e   assign {TowerPlayerLabel(partyIndex)} debuff:{(debuffs.TryGetValue(partyIndex, out var d) ? TowerElementLabel(d.Element) : "none")} → {label}");
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
        if (enableDebugLogging) accessory.Method.SendChat($"/e [Tower] round {round} using new vulns {activeDebuffs.Count}/6 remain={TowerNewDebuffMinRemainMs}-{TowerNewDebuffMaxRemainMs}ms");
        foreach (var kv in activeDebuffs)
        {
            var target = ClockwiseFirstDifferentTower(towers, kv.Value.Element);
            if (target.HasValue) Put(kv.Key, target.Value.Pos, $"CW-diff:{TowerElementLabel(target.Value.Element)}");
        }

        var idleTarget = CounterClockwiseFromTowerAnchor(towers, doubleElement);
        foreach (var partyIndex in idlePlayers)
        {
            if (markIndex >= TowerLabels.Length) break;
            if (!used.Contains(partyIndex)) Put(partyIndex, idleTarget.Pos, $"CCW-double:{TowerElementLabel(doubleElement)}");
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

        if (enableDebugLogging) accessory.Method.SendChat($"/e [Tower] locked idle: {string.Join(" ", idle.Select(TowerPlayerLabel))} debuffs={debuffCount} idle={idle.Count}");
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
        accessory.Method.SendChat("/e ── Tower summary ──");
        foreach (var t in towers)
        {
            var who = string.Join(" ", Enumerable.Range(0, 8).Where(i => raw.ContainsKey(i) && RoundTowerPos(raw[i]) == RoundTowerPos(t.Pos)).Select(TowerPlayerLabel));
            accessory.Method.SendChat($"/e   {TowerElementLabel(t.Element)} tower @({t.Pos.X:F1},{t.Pos.Z:F1}) → {who}");
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
        TowerElement.Fire => "Fire",
        TowerElement.Ice => "Ice",
        TowerElement.Thunder => "Thunder",
        _ => "?"
    };

    private static string TowerPlayerLabel(int index) => index >= 0 && index < TowerLabels.Length ? TowerLabels[index] : $"P{index}";

    #endregion

    #region Flood (Rect AoE)

    // 49539 Flood: 10×60 rect behind the boss (center = boss position - 30m forward), resolves 4s after the cast
    [ScriptMethod(name: "P5 Flood (AoE)", eventType: EventTypeEnum.StartCasting,
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

    // Flood 2-then-1: every 4 Flood casts are one group; the safe spot is the intersection of a negative × positive pair of rect center lines closest to the middle;
    // group 1's spot is stored first, and once group 2 appears they're linked into a 2 → 1 route
    [ScriptMethod(name: "P5 Flood 2-then-1 (Safe Spot Guidance)", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:49539"])]
    public void OnFlood(Event @event, ScriptAccessory accessory)
    {
        var pos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
        var rot = float.Parse(@event["SourceRotation"]);

        // Paired casts arrive concurrently; serialize all Flood state changes to rule out lost updates
        lock (_floodLock)
        {
        _floodCount++;

        if (enableDebugLogging)
            accessory.Method.SendChat($"/e [Flood] #{_floodCount} cast pos=({pos.X:F1},{pos.Z:F1}) rot={rot:F3}");

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
            accessory.Method.SendChat($"/e [Flood] cast#{_floodCount} rot={rot:F3} {(rot < 0f ? "neg" : "pos")} seq={_seqCount} neg={_negCount} pos={_posCount}");

        if (_negCount >= 1 && _posCount >= 1 && _negCount + _posCount >= 4)
        {
            _floodGroupCount++;

            if (enableDebugLogging)
                accessory.Method.SendChat($"/e [Flood] pairing triggered groupCount={_floodGroupCount} saved1={( _savedFloodSafe1.HasValue ? "yes" : "no")}");

            // Go through every neg×pos combination and take the intersection closest to the middle
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
                    accessory.Method.SendChat($"/e [Flood] group{_floodGroupCount} intersection dist to middle={bestDist:F1}m over threshold{FloodMaxDist:F1}m ✗");
                // This group's intersection is invalid; roll back groupCount so it doesn't use up a valid group number and misalign the group 1 save / group 2 draw pairing
                _floodGroupCount--;
                _negCount = 0; _posCount = 0;
                _seqCount = 0;
                return;
            }
            var safePos = best.Value;

            _negCount = 0; _posCount = 0;

            // Shift 4m toward the middle
            var toCenter = P5ArenaCenter - safePos;
            var toCenterLen = toCenter.Length();
            if (toCenterLen > 0.001f)
                safePos += toCenter / toCenterLen * 4f;

            var midPos = TryBuildFloodMidNode(safePos, bestNegIndex, bestPosIndex, out var node) ? node : (Vector3?)null;

            if (enableDebugLogging)
            {
                accessory.Method.SendChat($"/e [Flood] group{_floodGroupCount} intersection=({safePos.X:F1},{safePos.Z:F1}) dist to middle={bestDist:F1}m ✓");
                if (midPos.HasValue)
                    accessory.Method.SendChat($"/e [Flood] discrete middle node=({midPos.Value.X:F1},{midPos.Value.Z:F1})");
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

    // 2 → transfer → 1: the green line points at the current spot, the white line previews the next segment
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
        foreach (var i in GuidedPlayers(a))
            P5Guide(a, name, i, target, startMs, endMs - startMs);
        if (next.HasValue)
            P5Preview(a, $"{name}_Preview", target, next.Value, startMs, endMs - startMs);
    }

    #endregion

    #region Helpers

    // Guidance style matches P1~P3: current spot = green arrow (WaypointDp, default safe colour), next segment = white preview line
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

    #region Helpers (Cicero)

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

#region Helpers (Coda)

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

#region Helpers (Cicero)

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

#region Helpers (Usami)

#region Priority Dict
internal class PriorityDict
{
    // ReSharper disable once NullableWarningSuppressionIsUsed
    public Dictionary<int, int> Priorities {get; set;} = null!;
    public string Annotation { get; set; } = "";
    public int ActionCount { get; set; } = 0;

    private static readonly List<string> Role = ["MT", "OT", "H1", "H2", "M1", "M2", "R1", "R2"];

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
    /// Add priority to a specific Key
    /// </summary>
    /// <param name="idx">key</param>
    /// <param name="priority">priority value</param>
    public void AddPriority(int idx, int priority)
    {
        Priorities[idx] += priority;
    }

    /// <summary>
    /// Take the num smallest values from Priorities and return them as a new Dict
    /// </summary>
    /// <param name="num"></param>
    /// <returns></returns>
    public List<KeyValuePair<int, int>> SelectSmallPriorityIndices(int num)
    {
        return SelectMiddlePriorityIndices(0, num);
    }

    /// <summary>
    /// Take the num largest values from Priorities and return them as a new Dict
    /// </summary>
    /// <param name="num"></param>
    /// <returns></returns>
    public List<KeyValuePair<int, int>> SelectLargePriorityIndices(int num)
    {
        return SelectMiddlePriorityIndices(0, num, true);
    }

    /// <summary>
    /// Take the middle values of the ascending order from Priorities and return them as a new Dict
    /// </summary>
    /// <param name="skip">number of elements to skip. To start from the second one, skip=1</param>
    /// <param name="num"></param>
    /// <param name="descending">sort descending, false by default</param>
    /// <returns></returns>
    public List<KeyValuePair<int, int>> SelectMiddlePriorityIndices(int skip, int num, bool descending = false)
    {
        if (Priorities.Count < skip + num)
            return new List<KeyValuePair<int, int>>();

        IEnumerable<KeyValuePair<int, int>> sortedPriorities;
        if (descending)
        {
            // Sort by value descending and take the first num keys
            sortedPriorities = Priorities
                .OrderByDescending(pair => pair.Value) // Sort by value first
                .ThenBy(pair => pair.Key) // then by key
                .Skip(skip) // Skip the first skip elements
                .Take(num); // Take the first num pairs
        }
        else
        {
            // Sort by value ascending and take the first num keys
            sortedPriorities = Priorities
                .OrderBy(pair => pair.Value) // Sort by value first
                .ThenBy(pair => pair.Key) // then by key
                .Skip(skip) // Skip the first skip elements
                .Take(num); // Take the first num pairs
        }

        return sortedPriorities.ToList();
    }

    /// <summary>
    /// Take the idx-th entry of the ascending order from Priorities and return it as a new Dict
    /// </summary>
    /// <param name="idx"></param>
    /// <param name="descending">sort descending, false by default</param>
    /// <returns></returns>
    public KeyValuePair<int, int> SelectSpecificPriorityIndex(int idx, bool descending = false)
    {
        var sortedPriorities = SelectMiddlePriorityIndices(0, Priorities.Count, descending);
        return sortedPriorities[idx];
    }

    /// <summary>
    /// Find the given key in Priorities and return the sorted position of its Value
    /// </summary>
    /// <param name="key"></param>
    /// <param name="descending">sort descending, false by default</param>
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
    /// Add priority values in one go
    /// usually for special priorities (e.g. H-T-D-H)
    /// </summary>
    /// <param name="priorities"></param>
    public void AddPriorities(List<int> priorities)
    {
        if (Priorities.Count != priorities.Count)
            throw new ArgumentException("Input list length differs from the internal setting");

        for (var i = 0; i < Priorities.Count; i++)
            AddPriority(i, priorities[i]);
    }

    /// <summary>
    /// Print the priority dict's Keys and priorities
    /// </summary>
    /// <returns></returns>
    public string ShowPriorities(bool showJob = true)
    {
        var str = $"{Annotation} ({ActionCount}-th) priority dict:\n";
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
        return $"{name}: {string.Join(" ", kvpList.Select(x => $"({Role[x.Key]}, {x.Value})"))}";
    }
}

#endregion Priority Dict Class

#region Param Container Classes
internal class UDMP3Params
{
    public bool Debugging = true;
    public int CurrentStage = 0;
    public ulong ObjectId_Chaos = 0;
    public ulong ObjectId_Exdeath = 0;
    public bool MtTanksExdeath = false;
    public string FirewallTargetFramework = "";
    public int LastFirewallState = 0;
    public bool FirewallEnabled = false;
    public bool ImplosionSidesFirst = false;
    public bool ImplosionAoeActive = false;
    public bool ImplosionPart2Drawn = false;

    // ---- P3A ----
    public bool IsLongFire = false;
    public int P3AStatusRecordCount = 0;
    public int FireCrystalRegion = -1;
    public int WaterCrystalRegion = -1;
    public int WindCrystalRegion = -1;
    public int EmptyCrystalRegion = -1;

    public List<KeyValuePair<int, int>> FireGroup = new();
    public List<KeyValuePair<int, int>> WaterGroup = new();
    public ManualResetEvent P3AStatusRecord = new(false);
    public ManualResetEvent P3ACrystalRecord = new(false);
    public ManualResetEvent FireWater1Ready = new(false);

    public int FireWater1GuideAndDrawTime = 10000;
    public int FireWater2GuideAndDrawTime = 9000;
    public int VacuumWaveGuideTime = 7000;

    // ---- P3B ----
    public int BlackHoleRound = 0;
    public int BlackHoleEffectRound = 0;
    public bool PlayerIsTethering = false;
    public Dictionary<ulong, (int round, int region, int relativeRegion, bool hasTether)> BlackHoleDict = new();
    public ManualResetEvent OuterBlackHolesAcquired = new(false);
    public ManualResetEvent BlackHoleRoundIncremented = new(false);
    public ManualResetEvent KefkaRegionAcquired = new(false);
    public int KefkaRegion = 0;

    public int BlizzardNorthRegion = -1;
    public int WestTowerRegion = -1;
    public int EastTowerRegion = -1;
    public bool StackSpotIsTN = false;
    public bool DrawBlizzardBaitPath = false;
    public bool IsFirstStack = false;
    public Vector3 SecondPuddleBaitPos = new Vector3(100, 0, 100);
    public Vector3 FirstKnockDownPos = new Vector3(0, 0, 0);
    public bool BlackHoleAppeared = false;
    public int[] TetherOrder = new int[8];   // First/Second/Third in Line = 1/2/3
    public bool[] HasAccretion = new bool[8];

    public List<(int playerIdx, List<int> realRegions, List<ulong> blackHoles, string name)> CurrentBlackHoleTetherTasks = [];
    public ManualResetEvent BlackHoleTetherTasksReady = new(false);
    public int BlackHoleTetherTaskVersion = 0;
    public int BlackHoleTetherAoeConsumedVersion = 0;
    public int BlackHoleTetherTtsConsumedVersion = 0;
    public int BlackHoleTetherGuideConsumedVersion = 0;
    public List<TetherFrameworkElement> TetherFrameworkList = [];
    public string curTetherTaskFramework = "";

    public void Reset(ScriptAccessory sa)
    {
        CurrentStage = 0;

        // P3A
        IsLongFire = false;
        P3AStatusRecordCount = 0;
        FireCrystalRegion = -1;
        WaterCrystalRegion = -1;
        WindCrystalRegion = -1;
        EmptyCrystalRegion = -1;
        MtTanksExdeath = false;
        sa.Method.UnregistFrameworkUpdateAction(FirewallTargetFramework);
        FirewallTargetFramework = "";
        LastFirewallState = 0;
        FirewallEnabled = false;
        ImplosionSidesFirst = false;
        ImplosionAoeActive = false;
        ImplosionPart2Drawn = false;

        ObjectId_Chaos = 0;
        ObjectId_Exdeath = 0;

        P3AStatusRecord = new(false);
        P3ACrystalRecord = new(false);
        FireWater1Ready = new(false);

        FireGroup = new();
        WaterGroup = new();

        FireWater1GuideAndDrawTime = 10000;
        FireWater2GuideAndDrawTime = 9000;
        VacuumWaveGuideTime = 7000;

        // P3B
        BlackHoleRound = 0;
        BlackHoleEffectRound = 0;
        PlayerIsTethering = false;
        BlackHoleDict = new Dictionary<ulong, (int round, int region, int relativeRegion, bool hasTether)>();
        OuterBlackHolesAcquired = new(false);
        BlackHoleRoundIncremented = new(false);
        KefkaRegionAcquired = new(false);
        KefkaRegion = 0;

        BlizzardNorthRegion = -1;
        WestTowerRegion = -1;
        EastTowerRegion = -1;
        StackSpotIsTN = false;
        DrawBlizzardBaitPath = false;
        IsFirstStack = false;
        SecondPuddleBaitPos = new Vector3(100, 0, 100);
        FirstKnockDownPos = new Vector3(0, 0, 0);
        BlackHoleAppeared = false;
        TetherOrder = new int[8];
        HasAccretion = new bool[8];

        CurrentBlackHoleTetherTasks = [];
        BlackHoleTetherTasksReady = new(false);
        BlackHoleTetherTaskVersion = 0;
        BlackHoleTetherAoeConsumedVersion = 0;
        BlackHoleTetherTtsConsumedVersion = 0;
        BlackHoleTetherGuideConsumedVersion = 0;
        foreach (var fw in TetherFrameworkList)
        {
            if (fw.FwHandle != "")
                sa.Method.UnregistFrameworkUpdateAction(fw.FwHandle);
        }
        TetherFrameworkList.Clear();
        if (curTetherTaskFramework != "")
            sa.Method.UnregistFrameworkUpdateAction(curTetherTaskFramework);
        curTetherTaskFramework = "";

        sa.DebugMsg($"DMU P3 params reset");
    }
}

internal static class P3AExtension
{
    public static bool P3AStatusRecordDone(this UDMP3Params prm) =>
        prm.P3AStatusRecordCount == 12;

    public static bool CrystalRecordDone(this UDMP3Params prm) =>
        prm is { FireCrystalRegion: >= 0, WaterCrystalRegion: >= 0, WindCrystalRegion: >= 0 };

    public static void GroupMembers(this UDMP3Params prm, ScriptAccessory sa, PriorityDict pd)
    {
        prm.FireGroup.Clear();
        prm.WaterGroup.Clear();

        // Priorities 0~3 are wind-only players, 4~5 fire, 6~7 water
        prm.FireGroup.Add(pd.SelectSpecificPriorityIndex(4));
        prm.FireGroup.Add(pd.SelectSpecificPriorityIndex(5));
        prm.WaterGroup.Add(pd.SelectSpecificPriorityIndex(6));
        prm.WaterGroup.Add(pd.SelectSpecificPriorityIndex(7));

        // then sort by the ones digit of KVP.value, smaller first
        prm.FireGroup.Sort((a, b) => a.Value.GetDecimalDigit(0).CompareTo(b.Value.GetDecimalDigit(0)));
        prm.WaterGroup.Sort((a, b) => a.Value.GetDecimalDigit(0).CompareTo(b.Value.GetDecimalDigit(0)));
    }

    public static bool IsFireRound(this UDMP3Params prm)
        // In round 1 the XOR is the long fire
        => prm.CurrentStage == 3100 ^ prm.IsLongFire;

    public static bool NeedFaceAway(this UDMP3Params prm, int pdValue)
        // Headwind: knockback halved when your back is to it
        => pdValue.GetDecimalDigit(1) == 1;

    public static Vector3 RotateFromCrystal(this UDMP3Params prm, Vector3 basePos, int crystalRegion)
    {
        var center = new Vector3(100, 0, 100);
        return basePos.RotateAndExtend(center, (crystalRegion * 90f).DegToRad());
    }

    public static int QueryGroupOrder(this UDMP3Params prm, List<KeyValuePair<int, int>> group, int myIndex)
        => group.FindIndex(kvp => kvp.Key == myIndex);
}

internal static class P3BExtension
{

    public static int GetGiantKefkaRegion(this UDMP3Params prm, ScriptAccessory sa, bool cwFromA = true)
    {
        var obj = sa.GetByDataId(19504u).FirstOrDefault();
        if (obj == null)
        {
            sa.DebugMsg($"Lookup failed");
            return 0;
        }

        // ? count CW from A : count CCW from C
        var region = cwFromA
            ? (obj.Rotation.RadianToRegion(8, 4, isDiagDiv: true, isCw: true) + 4) % 8
            : (obj.Rotation.RadianToRegion(8, isDiagDiv: true) + 4) % 8;

        // prm.KefkaRegion = region;
        sa.DebugMsg($"Kefka region {region} {(cwFromA ? "CW from A" : "CCW from C")}");
        return region;
    }

    public static (int attackRegion, int safeRegion) GiantKefkaCleaveDirection(this UDMP3Params prm, ScriptAccessory sa, bool isRightHand)
    {
        // CW from A,
        var bossRegion = prm.KefkaRegion;
        var attackRegion = (bossRegion + 8 + (isRightHand ? -2 : 2)) % 8;
        var safeRegion = (bossRegion + 8 + (isRightHand ? 2 : -2)) % 8;
        sa.DebugMsg($"Attack {attackRegion} safe {safeRegion} CW from A");
        return (attackRegion, safeRegion);
    }

    public static Vector3 RotateFromKefka(this UDMP3Params prm, Vector3 basePos, int bossRegion)
    {
        var center = new Vector3(100, 0, 100);
        return basePos.RotateAndExtend(center, (bossRegion * 45f).DegToRad());
    }

    public static bool NeedSoakTower(this UDMP3Params prm, int myIndex)
    {
        // Stack TN, I'm TN, I stack false
        // Stack TN, I'm D, I soak true
        // Stack D, I'm TN, I soak true
        // Stack D, I'm TN, I stack false
        return prm.StackSpotIsTN ^ (myIndex <= 3);
    }
}

internal class TetherFrameworkElement
{
    public string Name { get; init; } = "";
    public string TaskName { get; init; } = "";
    public int PlayerIndex { get; init; }
    public string FwHandle { get; set; } = "";
    public bool Tethered { get; set; } = false;
    public ulong BlackHoleId { get; init; }
    public ulong PlayerObjId { get; init; }
    public Vector3 GuidePos { get; init; }
    public bool goToTetherGuideDrawn { get; set; } = false;
    public ulong lastTetherTarget { get; set; } = 0;
    public string lastGoToTetherDrawName { get; set; } = "";
    public string lastTetherTargetList { get; set; } = "";
    public bool lastBlackHoleExistState { get; set; } = true;
    public string goToTetherDrawNamePrefix => $"DrawBlackHoleTether {TaskName} G-GoToTether {Name}";
}

#endregion Param Container Classes

#region Function Set
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
#region Math Functions

internal static class MathTools
{
    public static float DegToRad(this float deg) => (deg + 360f) % 360f / 180f * float.Pi;
    public static float RadToDeg(this float rad) => (rad + 2 * float.Pi) % (2 * float.Pi) / float.Pi * 180f;

    /// <summary>
    /// Gets the radian of any point relative to the center, with (0, 0, 1) as 0 and (1, 0, 0) as pi/2.
    /// i.e. increasing counterclockwise.
    /// </summary>
    /// <param name="point">any point</param>
    /// <param name="center">center point</param>
    /// <returns></returns>
    public static float GetRadian(this Vector3 point, Vector3 center)
        => MathF.Atan2(point.X - center.X, point.Z - center.Z);

    /// <summary>
    /// Gets the distance between any point and the center.
    /// </summary>
    /// <param name="point">any point</param>
    /// <param name="center">center point</param>
    /// <returns></returns>
    public static float GetLength(this Vector3 point, Vector3 center)
        => new Vector2(point.X - center.X, point.Z - center.Z).Length();

    /// <summary>
    /// Rotates any point counterclockwise around the center and extends it.
    /// </summary>
    /// <param name="point">any point</param>
    /// <param name="center">center point</param>
    /// <param name="radian">rotation radian</param>
    /// <param name="length">extra length from that point</param>
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
    /// Gets the region an angle falls into
    /// </summary>
    /// <param name="radian">input radian</param>
    /// <param name="regionNum">number of regions</param>
    /// <param name="baseRegionIdx">starting Idx of the region at 0°</param>>
    /// <param name="isDiagDiv">whether the split is diagonal, false by default</param>
    /// <param name="isCw">whether it increases clockwise, false by default</param>
    /// <returns></returns>
    public static int RadianToRegion(this float radian, int regionNum, int baseRegionIdx = 0, bool isDiagDiv = false, bool isCw = false)
    {
        var sepRad = float.Pi * 2 / regionNum;
        var inputAngle = radian * (isCw ? -1 : 1) + (isDiagDiv ? sepRad / 2 : 0);
        var rad = (inputAngle + 4 * float.Pi) % (2 * float.Pi);
        return ((int)Math.Floor(rad / sepRad) + baseRegionIdx + regionNum) % regionNum;
    }

    /// <summary>
    /// Mirrors the input point left/right
    /// </summary>
    /// <param name="point">point to mirror</param>
    /// <param name="centerX">coordinate of the center fold line</param>
    /// <returns></returns>
    public static Vector3 FoldPointHorizon(this Vector3 point, float centerX)
        => point with { X = 2 * centerX - point.X };

    /// <summary>
    /// Mirrors the input point up/down
    /// </summary>
    /// <param name="point">point to mirror</param>
    /// <param name="centerZ">coordinate of the center fold line</param>
    /// <returns></returns>
    public static Vector3 FoldPointVertical(this Vector3 point, float centerZ)
        => point with { Z = 2 * centerZ - point.Z };

    /// <summary>
    /// Point-mirrors the input point through the center
    /// </summary>
    /// <param name="point">input point</param>
    /// <param name="center">center point</param>
    /// <returns></returns>
    public static Vector3 PointCenterSymmetry(this Vector3 point, Vector3 center)
        => point.RotateAndExtend(center, float.Pi, 0);

    /// <summary>
    /// Gets the given digit of an integer
    /// </summary>
    /// <param name="val">the integer</param>
    /// <param name="x">digit position, ones = 0</param>
    /// <returns>the digit at that position, 0 if x is out of range</returns>
    public static int GetDecimalDigit(this int val, int x)
        => (int)(Math.Abs(val) / Math.Pow(10, x) % 10);

    /// <summary>
    /// Gets the given binary bit of an integer
    /// </summary>
    /// <param name="val">the integer</param>
    /// <param name="bitPosition">bit position, counted from the lowest bit, lowest = 0</param>
    /// <returns>the bit value: 0 or 1</returns>
    public static int GetBinaryBit(this int val, int bitPosition)
        => (val >> bitPosition) & 1;

    /// <summary>
    /// Gets the difference between two radians (rad to radReference), counterclockwise positive
    /// </summary>
    /// <param name="rad">angle</param>
    /// <param name="radReference">reference angle</param>
    /// <returns></returns>
    public static float GetDiffRad(this float rad, float radReference)
    {
        var diff = (rad - radReference + 4 * float.Pi) % (2 * float.Pi);
        if (diff > float.Pi) diff -= 2 * float.Pi;
        return diff;
    }
}

#endregion Math Functions

#region Position Sequence Functions
internal static class IndexHelper
{
    /// <summary>
    /// Takes a player dataId and returns their position index
    /// </summary>
    /// <param name="pid">player SourceId</param>
    /// <param name="sa"></param>
    /// <returns>the player's position index</returns>
    public static int GetPlayerIdIndex(this ScriptAccessory sa, uint pid)
    {
        // Get the player IDX
        return sa.Data.PartyList.IndexOf(pid);
    }

    /// <summary>
    /// Gets the local player's position index
    /// </summary>
    /// <param name="sa"></param>
    /// <returns>the local player's position index</returns>
    public static int GetMyIndex(this ScriptAccessory sa)
    {
        return sa.Data.PartyList.IndexOf(sa.Data.Me);
    }

    /// <summary>
    /// Takes a player dataId and returns their position name, for text output only
    /// </summary>
    /// <param name="pid">player SourceId</param>
    /// <param name="sa"></param>
    /// <returns>the player's position name</returns>
    public static string GetPlayerJobById(this ScriptAccessory sa, uint pid)
    {
        // Get the player's role short name; not used, DEBUG output only
        var idx = sa.Data.PartyList.IndexOf(pid);
        var str = sa.GetPlayerJobByIndex(idx);
        return str;
    }

    /// <summary>
    /// Takes a position index and returns the position name, for text output only
    /// </summary>
    /// <param name="idx">position index</param>
    /// <param name="fourPeople">whether it's a four-player dungeon</param>
    /// <param name="sa"></param>
    /// <returns></returns>
    public static string GetPlayerJobByIndex(this ScriptAccessory sa, int idx, bool fourPeople = false)
    {
        List<string> role8 = ["MT", "OT", "H1", "H2", "M1", "M2", "R1", "R2"];
        List<string> role4 = ["T", "H", "D1", "D2"];
        if (idx < 0 || idx >= 8 || (fourPeople && idx >= 4))
            return "Unknown";
        return fourPeople ? role4[idx] : role8[idx];
    }

}
#endregion Position Sequence Functions

#region Draw Functions

internal static class DrawTools
{
    /// <summary>
    /// Returns a draw
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">draw anchor, UID or position</param>
    /// <param name="targetObj">draw target, UID or position</param>
    /// <param name="delay">appears after delay ms</param>
    /// <param name="destroy">disappears destroy ms after appearing</param>
    /// <param name="name">draw name</param>
    /// <param name="radian">arc of the shape</param>
    /// <param name="rotation">rotation of the shape relative to the owner's front, counterclockwise positive</param>
    /// <param name="width">shape width, some shapes can match the length</param>
    /// <param name="length">shape length, some shapes can match the width</param>
    /// <param name="innerWidth">shape inner width, some shapes can match the length</param>
    /// <param name="innerLength">shape inner length, some shapes can match the width</param>
    /// <param name="drawModeEnum">draw mode</param>
    /// <param name="drawTypeEnum">draw type</param>
    /// <param name="color">colour</param>
    /// <param name="byTime">fill animation over time</param>
    /// <param name="byY">animation follows distance</param>
    /// <param name="draw">whether to draw immediately</param>
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
                throw new ArgumentException($"ownerObj {ownerObj} target type {ownerObj.GetType()} invalid input");
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
                throw new ArgumentException($"targetObj {targetObj} target type {targetObj.GetType()} invalid input");
        }

        if (draw)
            sa.Method.SendDraw(drawModeEnum, drawTypeEnum, dp);
        return dp;
    }

    /// <summary>
    /// Returns a guidance draw
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">start point</param>
    /// <param name="targetObj">end point</param>
    /// <param name="delay">delay</param>
    /// <param name="destroy">time until it disappears</param>
    /// <param name="name">draw name</param>
    /// <param name="rotation">arrow rotation</param>
    /// <param name="width">arrow width</param>
    /// <param name="color">colour</param>
    /// <param name="draw">whether to draw immediately</param>
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
    /// Returns a circle draw
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">circle center</param>
    /// <param name="delay">delay</param>
    /// <param name="destroy">time until it disappears</param>
    /// <param name="name">draw name</param>
    /// <param name="scale">circle radius</param>
    /// <param name="byTime">whether it fills over time</param>
    /// <param name="color">colour</param>
    /// <param name="draw">whether to draw immediately</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawCircle(this ScriptAccessory sa,
        object ownerObj, int delay, int destroy, string name,
        float scale, Vector4 color, bool byTime = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, 0, delay, destroy, name, 2 * float.Pi, 0, scale, scale,
            0, 0, DrawModeEnum.Default, DrawTypeEnum.Circle, color, byTime,false, draw);

    /// <summary>
    /// Returns a donut draw
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">circle center</param>
    /// <param name="delay">delay</param>
    /// <param name="destroy">time until it disappears</param>
    /// <param name="name">draw name</param>
    /// <param name="outScale">outer radius</param>
    /// <param name="innerScale">inner radius</param>
    /// <param name="byTime">whether it fills over time</param>
    /// <param name="color">colour</param>
    /// <param name="draw">whether to draw immediately</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawDonut(this ScriptAccessory sa,
        object ownerObj, int delay, int destroy, string name,
        float outScale, float innerScale, Vector4 color, bool byTime = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, 0, delay, destroy, name, 2 * float.Pi, 0, outScale, outScale, innerScale,
            innerScale, DrawModeEnum.Default, DrawTypeEnum.Donut, color, byTime, false, draw);

    /// <summary>
    /// Returns a fan draw
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">circle center</param>
    /// <param name="targetObj">target</param>
    /// <param name="delay">delay</param>
    /// <param name="destroy">time until it disappears</param>
    /// <param name="name">draw name</param>
    /// <param name="radian">arc</param>
    /// <param name="rotation">rotation angle</param>
    /// <param name="outScale">outer radius</param>
    /// <param name="innerScale">inner radius</param>
    /// <param name="byTime">whether it fills over time</param>
    /// <param name="color">colour</param>
    /// <param name="draw">whether to draw immediately</param>
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
    /// Returns a rect draw
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">rect start</param>
    /// <param name="targetObj">target</param>
    /// <param name="delay">delay</param>
    /// <param name="destroy">time until it disappears</param>
    /// <param name="name">draw name</param>
    /// <param name="rotation">rotation angle</param>
    /// <param name="width">rect width</param>
    /// <param name="length">rect length</param>
    /// <param name="byTime">whether it fills over time</param>
    /// <param name="byY">whether it extends with distance</param>
    /// <param name="color">colour</param>
    /// <param name="draw">whether to draw immediately</param>
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
    /// Returns a line draw
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">line start</param>
    /// <param name="targetObj">line target</param>
    /// <param name="delay">delay</param>
    /// <param name="destroy">time until it disappears</param>
    /// <param name="name">draw name</param>
    /// <param name="rotation">rotation angle</param>
    /// <param name="width">line width</param>
    /// <param name="length">line length</param>
    /// <param name="byTime">whether it fills over time</param>
    /// <param name="byY">whether it extends with distance</param>
    /// <param name="color">colour</param>
    /// <param name="draw">whether to draw immediately</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawLine(this ScriptAccessory sa,
        object ownerObj, object targetObj, int delay, int destroy, string name, float rotation,
        float width, float length, Vector4 color, bool byTime = false, bool byY = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, 1, rotation, width, length, 0, 0,
            DrawModeEnum.Default, DrawTypeEnum.Line, color, byTime, byY, draw);

    /// <summary>
    /// Returns a connection draw between two objects
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">start source</param>
    /// <param name="targetObj">target source</param>
    /// <param name="delay">delay</param>
    /// <param name="destroy">time until it disappears</param>
    /// <param name="name">draw name</param>
    /// <param name="width">line width</param>
    /// <param name="color">colour</param>
    /// <param name="draw">whether to draw immediately</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawConnection(this ScriptAccessory sa, object ownerObj, object targetObj,
        int delay, int destroy, string name, Vector4 color, float width = 1f, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, 0, 0, width, width,
            0, 0, DrawModeEnum.Imgui, DrawTypeEnum.Line, color, false, true, draw);

    /// <summary>
    /// Gives the input dp a near/far-order target draw sourced from ownerId
    /// </summary>
    /// <param name="self"></param>
    /// <param name="isNearOrder">counted from the owner, near order or far order</param>
    /// <param name="orderIdx">starts at 1</param>
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

#endregion Draw Functions

#region Debug Functions

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

#endregion Debug Functions

#region Special Functions

internal static class SpecialFunction
{
    public static void SetRotation(this ScriptAccessory sa, IGameObject? obj, float radian, bool show = false)
    {
        if (obj == null || !obj.IsValid())
        {
            sa.Log.Error($"The IGameObject passed in is invalid.");
            return;
        }
        unsafe
        {
            GameObject* charaStruct = (GameObject*)obj.Address;
            charaStruct->SetRotation(radian);
        }
        // sa.Log.Debug($"Change facing {obj.Name.TextValue} | {obj.GameObjectId} => {radian.RadToDeg()}");

        if (!show) return;
        var ownerObj = sa.GetById(obj.GameObjectId);
        if (ownerObj == null) return;
        var dp = sa.DrawGuidance(obj.GameObjectId, 0, 0, 2000, $"Change facing {obj.Name.TextValue}", sa.Data.DefaultSafeColor, radian, draw: false);
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
            sa.Log.Error($"The IGameObject passed in is invalid.");
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
                sa.Log.Error($"The IGameObject passed in is invalid.");
                return;
            }

            if (!charaStruct->IsCharacter())
            {
                sa.Log.Error($"The IGameObject passed in is not a Character; can't change its opacity.");
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

#endregion Special Functions

#endregion Function Set

#endregion
