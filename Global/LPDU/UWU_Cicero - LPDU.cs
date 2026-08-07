using System;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Concurrent;
using System.Numerics;
using System.Linq;
using System.Diagnostics;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.GameOperate;
using KodakkuAssist.Script;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Dalamud.Utility.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.STD.Helper;
using KodakkuAssist.Data;
using Lumina.Data.Parsing;

namespace CicerosKodakkuAssist.WeaponsRefrainUltimate.ChinaDataCenter
{

    [ScriptType(name:"The Weapon's Refrain (Ultimate) UWU - LPDU",
        territorys:[777],
        guid:"8a2b7d8a-4eeb-4840-84bb-195fd13645ea",
        version:"0.0.4.4",
        note:scriptNotes,
        author:"Cicero 灵视")]

    public class Weapons_Refrain_Ultimate
    {
        
        public const string scriptNotes=
            """
            0.0.4.4 修改:第二次地狱之火炎指路改为指向本轮南侧边缘;第二次地火喷发D3、D4统一从本轮西北侧沿西弧线回到南侧人群;第二次灼热咆哮点名者改去本轮东侧;后半地裂引导轴线改为朝北;爆击之究极幻想指路职责调整(第一颗炸弹及其引爆增加D1,飞翎雨北侧小点改为MT/ST/D1,第二颗炸弹及其引爆为MT/ST/D1,第三四颗炸弹及其引爆与深红旋风后原ST路线改为仅MT,深红旋风后普通路线适用于除MT与灼热外的队员);乱击之究极幻想调整(MT、ST起始都站西南,六人弧线站位改为H1正北/D3/D4/H2/D2/D1正西,寒风西南扇形MT与ST都显示,地火后箭头不再含MT,寒风后寒风点名/ST/MT从ST位置指向场中);设置项、文字提示与TTS文本翻译为英文;以太波动预站位与击退后路线MT改与ST一致(西南对角线,第二段90,104→90,96)。

            究极神兵绝境战的脚本。
            由于先前的究极神兵绝境战脚本(作者@baelixac)已经停止维护很久了,在最新版本的可达鸭上会出现编译错误,因此我决定从零完全重写这个副本的脚本。
            
            脚本已经完工,后续将仅有修复bug的更新(如有)而不会有大的改动。

            适配的攻略是国服野队一套。
            如果指路不适配你采用的攻略,可以在方法设置中将相关的指路关闭。所有指路方法均标注有"(指路)"后缀。
            
            支持进行小队排序测试,可以在聊天框中输入/e kuwutest来检查小队排序是否正确。
            输入/e kuwuclear清除小队排序测试产生的目标标记。

            如果在使用过程中遇到了异常,请先检查可达鸭本体与脚本是否都更新到了最新版本,小队职能是否已正确设置,异常是否可以稳定复现。
            如果上述三点都没有问题,请带着A Realm Recorded插件的录像文件在可达鸭Discord内联系@_publius_cornelius_scipio_反馈异常。
            
            
            
            授予与接受专断权力的人都是罪人。无论(专断权力)出现在世界上何处,每个人都有义务竭尽全力抵抗它。
            只要能够以合乎理性的方式摆脱它,那么忍受它便是一种犯罪。
            ......
            法律与专断权力永远势不两立。你只要向我指出一位执政者,我便会指出财产权(的存在);你只要向我指出权力,我便会指出保护(的义务)。
            说任何人能够拥有专断权力,在概念上是矛盾,在宗教上是亵渎,在政治上是邪恶。任何官职的授予都已经包含了相应的职责。
            否则,执政者存在的意义又是什么?设想权力可以仅仅为了权力自身而存在,在观念上就是荒谬的。法官受永恒的正义法则指引和约束,而我们所有人也都服从(这些法则)。
            ......
            诸位之中有着基督教的代表,(这一宗教)宣称他们的上帝是爱,他们制度的根本精神就是明爱。这种宗教如此地憎恶压迫,以至于我们所敬拜的上帝以人的形象显现时,祂并未以伟大和威严的形象出现,而是与最卑微的人感同身受,从而确立了一条坚定且支配性的原则:
            人的福祉乃是一切政府的目的,因为那位自然的主宰选择以卑下者的身份显现。正是这些因素影响着他们,激励着他们,并将继续激励他们反对一切压迫——
            因为他们知道,那位在他们之中居首,也在我们所有人之中居首者,无论对于受牧养的羊群,还是对于牧养羊群的人,都曾使自己成为"众人的仆人"。
            ......
            (因此,)我以印度人民的名义弹劾他!他颠覆了他们的法律、权利与自由,摧毁了他们的财产,使他们的国家满目疮痍。
            我以他所践踏的那些永恒的正义法则之名,并依据这些法则所赋予的权威,弹劾他!
            我以人性本身的名义弹劾他!他残酷地凌辱、伤害与压迫(人性),无论男女老幼、阶级地位与境遇。
            
            ——埃德蒙·伯克,于沃伦·黑斯廷斯弹劾案的开庭陈述,1788年2月18日
            """;
        
        #region User_Settings
        
        [UserSetting("General - Enable text prompts")]
        public bool enablePrompts { get; set; } = false;
        [UserSetting("General - Enable vanilla TTS")]
        public bool enableVanillaTts { get; set; } = false;
        [UserSetting("General - Enable Daily Routines TTS (requires the Daily Routines plugin!)")]
        public bool enableDailyRoutinesTts { get; set; } = false;
        [UserSetting("General - Colour of mechanic direction indicators")]
        public ScriptColor colourOfDirectionIndicators { get; set; } = new() { V4 = new Vector4(1,1,0, 1) }; // Yellow by default.
        [UserSetting("General - Colour of extremely dangerous attacks")]
        public ScriptColor colourOfExtremelyDangerousAttacks { get; set; } = new() { V4 = new Vector4(1,0,0,1) }; // Red by default.
        [UserSetting("General - Enable shenanigans")]
        public bool enableShenanigans { get; set; } = false;
        [UserSetting("General - Channel for the party sort test text")]
        public PartyTestChannels partyTestChannel { get; set; } = PartyTestChannels.EchoChannel_VisibleOnlyToYou;
        [UserSetting("General - Do not draw my own Searing Wind")]
        public bool disableSearingWindOnMe { get; set; } = false;
        [UserSetting("Debug - Enable debug logging to the Dalamud log")]
        public bool enableDebugLogging { get; set; } = false;
        [UserSetting("Debug - Skip phase checks in all methods")]
        public bool skipPhaseChecks { get; set; } = false;
        [UserSetting("Debug - Preserve drawings while switching phases")]
        public bool preserveDrawingsWhileSwitchingPhase { get; set; } = false;
        
        // ----- Major Phase 1 -----
        
        [UserSetting("Garuda - Colour of D2's rough guidance")]
        public ScriptColor phase1_colourOfM2ImpreciseGuidance { get; set; } = new() { V4 = new Vector4(0,1,1, 1) }; // Blue by default.
        [UserSetting("Garuda - Colour of the second Mistral Song's rough range")]
        public ScriptColor phase1_colourOfImpreciseRangeOfMistralSong { get; set; } = new() { V4 = new Vector4(0,1,1, 1) }; // Blue by default.
        
        // ----- End Of Major Phase 1 -----
        
        // ----- Major Phase 2 -----
        
        [UserSetting("Ifrit - Infernal Nail: colour of the north indicator")]
        public ScriptColor phase2_colourOfNorthIndicator { get; set; } = new() { V4 = new Vector4(0,1,1, 1) }; // Blue by default.
        [UserSetting("Ifrit - Infernal Nail: mark the kill order (only ONE party member should enable assists like this!)")]
        public bool phase2_enableNailOrderAssistance { get; set; } = false;
        
        // ----- End Of Major Phase 2 -----
        
        // ----- Major Phase 3 -----
        
        [UserSetting("Titan - Granite Gaol: party callouts (only ONE party member should enable assists like this!)")]
        public bool phase3_enableRockThrowAssistance { get; set; } = false;
        
        // ----- End Of Major Phase 3 -----
        
        // ----- Major Phase 4 -----
        
        
        
        // ----- End Of Major Phase 4 -----
        
        // ----- Major Phase 5 -----
        
        
        
        // ----- End Of Major Phase 5 -----

        #endregion
        
        #region Variables_And_Semaphores
        
        private volatile int majorPhase=1;
        private volatile int phase=1;
        
        /*

        Major Phase 1 - Garuda 迦楼罗:

            Phases are separated by Feather Rain.
            阶段由飞翎雨分隔。
        
        Major Phase 2 - Ifrit 伊弗利特:
        
            Phase 1 - (~,First Incinerate 第一次烈焰焚烧)
            Phase 2 - [First Incinerate 第一次烈焰焚烧,Second Hellfire 第二次地狱之火炎)
            Phase 3 - [Second Hellfire 第二次地狱之火炎,Second Crimson Cyclone 第二次深红旋风)
            Phase 4 - [Second Crimson Cyclone 第二次深红旋风,Flaming Crush 烈焰碎击)
            Phase 5 - [Flaming Crush 烈焰碎击,~)
            
        Major Phase 3 - Titan 泰坦:

            Phase 1 - (~,Second Geocrush 第二次大地粉碎)
            Phase 2 - [Second Geocrush 第二次大地粉碎,Second Weight of the Land 第二次大地之重)
            Phase 3 - [Second Weight of the Land 第二次大地之重,Third Rock Buster & Mountain Buster Combo 第三次碎岩山崩连击)
            Phase 4 - [Third Rock Buster & Mountain Buster Combo 第三次碎岩山崩连击,~)
            
        Major Phase 4 - Ascian Lahabrea 无影拉哈布雷亚:

            No phase separation.
            无阶段分隔。
            
        Major Phase 5 - Ultima Weapon 究极神兵:

            Phase 1 - (~,Ultimate Predation 追击之究极幻想)
            Phase 2 - First half of Ultimate Predation 追击之究极幻想前半
            Phase 3 - Second half of Ultimate Predation 追击之究极幻想后半
            Phase 4 - Ultimate Annihilation 爆击之究极幻想
            Phase 5 - Ultimate Suppression 乱击之究极幻想
            Phase 6 - [Aetheric Boom 以太波动,~)

        */
        
        // ----- Major Phase 1 -----

        private volatile int phase1_slipstreamCounter=0;
        private ulong phase1_targetOfMistralSong=0;
        private System.Threading.AutoResetEvent phase1_mistralSongSemaphore=new System.Threading.AutoResetEvent(false);
        private System.Threading.AutoResetEvent phase1_downburstSemaphore=new System.Threading.AutoResetEvent(false);

        private Vector3 phase1_gigastormPosition=ARENA_CENTER;
        private System.Threading.ManualResetEvent phase1_gigastormSemaphore=new System.Threading.ManualResetEvent(false);
        private int[] stackOfThermalLow=Enumerable.Range(0,8).Select(i=>0).ToArray();
        private bool[] phase1_hasEliminatedThermalLow=Enumerable.Range(0,8).Select(i=>false).ToArray();
        
        private bool[] phase1_tankBuster=Enumerable.Range(0,4).Select(i=>false).ToArray();
        
        private ConcurrentDictionary<ulong,int> phase1_mesohighDrawingCounter=new ConcurrentDictionary<ulong,int>();
        private volatile bool garudaHasWoken=false;
        
        // ----- End Of Major Phase 1 -----
        
        // ----- Major Phase 2 -----
        
        private ulong phase2_ifritId=0;
        private bool[] phase2_initialSafeZone=Enumerable.Range(0,4).Select(i=>true).ToArray();
        private System.Threading.AutoResetEvent phase2_firstCrimsonCycloneSemaphore=new System.Threading.AutoResetEvent(false);
        private volatile int phase2_radiantPlumeCounter=0;
        private System.Threading.AutoResetEvent phase2_radiantPlumeSemaphore=new System.Threading.AutoResetEvent(false);
        
        private System.Threading.AutoResetEvent phase2_firstIncinerateSemaphore=new System.Threading.AutoResetEvent(false);
        private bool[] phase2_infernalNailDeployed=Enumerable.Range(0,8).Select(i=>false).ToArray();
        private ulong[] phase2_infernalNailId=Enumerable.Range(0,8).Select(i=>((ulong)0)).ToArray();
        private volatile int phase2_infernalNailCounter=0;
        private int[] phase2_infernalNail=Enumerable.Range(0,4).Select(i=>-1).ToArray();
        private double phase2_temporaryRotation=0;
        private System.Threading.AutoResetEvent phase2_infernalNailSemaphore1=new System.Threading.AutoResetEvent(false);
        private System.Threading.AutoResetEvent phase2_infernalNailSemaphore2=new System.Threading.AutoResetEvent(false);
        private System.Threading.AutoResetEvent phase2_infernalNailSemaphore3=new System.Threading.AutoResetEvent(false);
        private System.Threading.AutoResetEvent phase2_infernalNailSemaphore4=new System.Threading.AutoResetEvent(false);
        private volatile int phase2_infernalFetterDrawingCounter=0;
        private List<int> phase2_detonationOrder=new List<int>();
        private volatile bool ifritHasWoken=false;
        
        private double phase2_temporaryRotation2=0;
        private System.Threading.AutoResetEvent phase2_hellfireSemaphore1=new System.Threading.AutoResetEvent(false);
        private System.Threading.AutoResetEvent phase2_hellfireSemaphore2=new System.Threading.AutoResetEvent(false);
        private System.Threading.AutoResetEvent phase2_hellfireSemaphore3=new System.Threading.AutoResetEvent(false);
        private HashSet<ulong> partyMembersWithSearingWind=new HashSet<ulong>();

        private List<int> phase2_readableDetonationOrder=new List<int>(); 
        private volatile bool phase2_disableCrimsonCycloneGuidance=false;
        private volatile int phase2_discretizedInitialRotation=0;
        private volatile bool phase2_clockwise=false;
        private System.Threading.AutoResetEvent phase2_thirdCrimsonCycloneSemaphore=new System.Threading.AutoResetEvent(false);
        
        // ----- End Of Major Phase 2 -----
        
        // ----- Major Phase 3 -----
        
        private ulong phase3_titanId=0;
        private volatile int phase3_discretizedLandingPosition=0;
        private System.Threading.AutoResetEvent phase3_secondGeocrushSemaphore=new System.Threading.AutoResetEvent(false);

        private volatile int phase3_boulderCounter=0;
        private volatile int phase3_bouldersOnLeft=0,phase3_bouldersOnRight=0;
        private volatile bool phase3_leftSafeZone=false;
        private System.Threading.AutoResetEvent phase3_bombBoulderSemaphore=new System.Threading.AutoResetEvent(false);
        private volatile int phase3_rockThrowCounter=0;
        private bool[] phase3_isRockThrow=Enumerable.Range(0,8).Select(i=>false).ToArray();
        private List<int> phase3_rockThrowOrder=new List<int>();
        private System.Threading.AutoResetEvent phase3_rockThrowSemaphore1=new System.Threading.AutoResetEvent(false);
        private System.Threading.AutoResetEvent phase3_rockThrowSemaphore2=new System.Threading.AutoResetEvent(false);
        private System.Threading.AutoResetEvent phase3_rockThrowSemaphore3=new System.Threading.AutoResetEvent(false);
        private volatile bool titanHasWoken=false;

        private ulong phase3_secondRockThrowTarget=0;
        private volatile int phase3_tumultCounter=0;
        private System.Threading.AutoResetEvent phase3_tumultSemaphore=new System.Threading.AutoResetEvent(false);
        private volatile int phase3_boulderRemovalCounter=0;
        private System.Threading.AutoResetEvent phase3_boulderRemovalSemaphore=new System.Threading.AutoResetEvent(false);
        
        // ----- End Of Major Phase 3 -----
        
        // ----- Major Phase 5 -----
        
        private ulong phase5_ultimaWeaponId=0;
        private System.Threading.AutoResetEvent phase5_firstTankPurgeSemaphore=new System.Threading.AutoResetEvent(false);

        private volatile int phase5sub2_discretizedTitanPosition=-1;
        private System.Threading.AutoResetEvent phase5sub2_titanSemaphore=new System.Threading.AutoResetEvent(false);
        private volatile int phase5sub2_ultimaWeaponAppearanceCounter=0;
        private System.Threading.AutoResetEvent phase5sub2_ultimaWeaponAppearance1Semaphore=new System.Threading.AutoResetEvent(false);
        private System.Threading.AutoResetEvent phase5sub2_ultimaWeaponAppearance2Semaphore=new System.Threading.AutoResetEvent(false);
        
        private System.Threading.AutoResetEvent phase5sub4_ultimateAnnihilationSemaphore=new System.Threading.AutoResetEvent(false);
        private volatile int phase5sub4_discretizedIfritPosition=-1;
        private HashSet<ulong> phase5sub4_existingAetheroplasm=new HashSet<ulong>();
        private System.Threading.AutoResetEvent phase5sub4_aetheroplasmAppearance1Semaphore=new System.Threading.AutoResetEvent(false);
        private System.Threading.AutoResetEvent phase5sub4_aetheroplasmAppearance234Semaphore=new System.Threading.AutoResetEvent(false);
        private System.Threading.AutoResetEvent phase5sub4_aetheroplasmAppearance4Semaphore=new System.Threading.AutoResetEvent(false);
        private volatile int phase5sub4_aetheroplasmDetonationCounter=0;
        private System.Threading.AutoResetEvent phase5sub4_aetheroplasmDetonation1Semaphore=new System.Threading.AutoResetEvent(false);
        private System.Threading.AutoResetEvent phase5sub4_aetheroplasmDetonation2Semaphore=new System.Threading.AutoResetEvent(false);
        private System.Threading.AutoResetEvent phase5sub4_aetheroplasmDetonation3Semaphore=new System.Threading.AutoResetEvent(false);
        private System.Threading.AutoResetEvent phase5sub4_aetheroplasmDetonation4Semaphore=new System.Threading.AutoResetEvent(false);
        private volatile int phase5sub4_featherRainCounter=0;
        private System.Threading.AutoResetEvent phase5sub4_featherRainSemaphore1=new System.Threading.AutoResetEvent(false);
        private System.Threading.AutoResetEvent phase5sub4_featherRainSemaphore2=new System.Threading.AutoResetEvent(false);
        
        private System.Threading.AutoResetEvent phase5sub5_ultimateSuppressionSemaphore=new System.Threading.AutoResetEvent(false);
        private volatile int phase5sub5_eruptionCounter=0;
        private bool[] phase5sub5_isEruption=Enumerable.Range(0,8).Select(i=>false).ToArray();
        private System.Threading.AutoResetEvent phase5sub5_eruptionSemaphore=new System.Threading.AutoResetEvent(false);
        private volatile int phase5sub5_mistralSongCounter=0;
        private bool[] phase5sub5_isMistralSong=Enumerable.Range(0,8).Select(i=>false).ToArray();
        private System.Threading.AutoResetEvent phase5sub5_mistralSongSemaphore=new System.Threading.AutoResetEvent(false);
        private ulong phase5sub5_rockThrowTarget=0;
        
        private System.Threading.AutoResetEvent phase5sub6_ultimaSemaphore=new System.Threading.AutoResetEvent(false);
        private volatile int phase5sub6_ultimaplasmStackCounter=0;
        private HashSet<ulong>[] phase5sub6_ultimaplasm={new HashSet<ulong>(),new HashSet<ulong>(),new HashSet<ulong>(),new HashSet<ulong>()};

        private EnragePhasePatterns enragePhasePattern=EnragePhasePatterns.UNKNOWN;
        private System.Threading.AutoResetEvent phase5sub6_ifritSemaphore=new System.Threading.AutoResetEvent(false);
        private System.Threading.AutoResetEvent phase5sub6_titanSemaphore=new System.Threading.AutoResetEvent(false);
        
        // ----- End Of Major Phase 5 -----
        
        #endregion
        
        #region Constants_And_Locks
        
        private const int MAXIMUM_DURATION=7200000;
        private const int COMMON_INTERVAL=2500;
        
        private static readonly Vector3 ARENA_CENTER=new Vector3(100,0,100);
        // The arena is a circle with a radius of 19.5.
        
        #endregion
        
        #region Enumerations_And_Classes
        
        public enum PartyTestChannels {

            DoNotSend,
            EchoChannel_VisibleOnlyToYou,
            PartyChannel_VisibleToAllMembers

        }
        
        public enum EnragePhasePatterns {
            
            GARUDA_IFRIT_TITAN,
            TITAN_IFRIT_GARUDA,
            IFRIT_GARUDA_TITAN,
            UNKNOWN

        }
        
        #endregion
        
        #region Initialization
        
        public void Init(ScriptAccessory accessory) {
            
            accessory.Method.RemoveDraw(".*");
            
            if(phase2_enableNailOrderAssistance||phase3_enableRockThrowAssistance) {

                accessory.Method.MarkClear();
                
            }
            
            VariableAndSemaphoreInitialization();
            
            if(enableShenanigans) {

                shenaniganSemaphore.Set();

            }

        }

        private void VariableAndSemaphoreInitialization() {

            majorPhase=1;
            phase=1;
            
            // ----- Major Phase 1 -----
            
            phase1_slipstreamCounter=0;
            phase1_targetOfMistralSong=0;
            phase1_mistralSongSemaphore.Reset();
            phase1_downburstSemaphore.Reset();
            
            phase1_gigastormPosition=ARENA_CENTER;
            phase1_gigastormSemaphore.Reset();
            for(int i=0;i<stackOfThermalLow.Length;++i)stackOfThermalLow[i]=0;
            for(int i=0;i<phase1_hasEliminatedThermalLow.Length;++i)phase1_hasEliminatedThermalLow[i]=false;
            
            for(int i=0;i<phase1_tankBuster.Length;++i)phase1_tankBuster[i]=false;
            
            phase1_mesohighDrawingCounter.Clear();
            garudaHasWoken=false;

            // ----- End Of Major Phase 1 -----

            // ----- Major Phase 2 -----

            phase2_ifritId=0;
            for(int i=0;i<phase2_initialSafeZone.Length;++i)phase2_initialSafeZone[i]=true;
            phase2_firstCrimsonCycloneSemaphore.Reset();
            phase2_radiantPlumeCounter=0;
            phase2_radiantPlumeSemaphore.Reset();

            phase2_firstIncinerateSemaphore.Reset();
            for(int i=0;i<phase2_infernalNailDeployed.Length;++i)phase2_infernalNailDeployed[i]=false;
            for(int i=0;i<phase2_infernalNailId.Length;++i)phase2_infernalNailId[i]=((ulong)0);
            phase2_infernalNailCounter=0;
            for(int i=0;i<phase2_infernalNail.Length;++i)phase2_infernalNail[i]=-1;
            phase2_temporaryRotation=0;
            phase2_infernalNailSemaphore1.Reset();
            phase2_infernalNailSemaphore2.Reset();
            phase2_infernalNailSemaphore3.Reset();
            phase2_infernalNailSemaphore4.Reset();
            phase2_infernalFetterDrawingCounter=0;
            ifritHasWoken=false;
            phase2_detonationOrder.Clear();

            phase2_temporaryRotation2=0;
            phase2_hellfireSemaphore1.Reset();
            phase2_hellfireSemaphore2.Reset();
            phase2_hellfireSemaphore3.Reset();
            partyMembersWithSearingWind.Clear();

            phase2_readableDetonationOrder.Clear();
            phase2_disableCrimsonCycloneGuidance=false;
            phase2_discretizedInitialRotation=0;
            phase2_clockwise=false;
            phase2_thirdCrimsonCycloneSemaphore.Reset();

            // ----- End Of Major Phase 2 -----

            // ----- Major Phase 3 -----

            phase3_titanId=0;
            phase3_discretizedLandingPosition=0;
            phase3_secondGeocrushSemaphore.Reset();
            
            phase3_boulderCounter=0;
            phase3_bouldersOnLeft=0;phase3_bouldersOnRight=0;
            phase3_leftSafeZone=false;
            phase3_bombBoulderSemaphore.Reset();
            phase3_rockThrowCounter=0;
            for(int i=0;i<phase3_isRockThrow.Length;++i)phase3_isRockThrow[i]=false;
            phase3_rockThrowOrder.Clear();
            phase3_rockThrowSemaphore1.Reset();
            phase3_rockThrowSemaphore2.Reset();
            phase3_rockThrowSemaphore3.Reset();
            titanHasWoken=false;
            
            phase3_secondRockThrowTarget=0;
            phase3_tumultCounter=0;
            phase3_tumultSemaphore.Reset();
            phase3_boulderRemovalCounter=0;
            phase3_boulderRemovalSemaphore.Reset();

            // ----- End Of Major Phase 3 -----

            // ----- Major Phase 5 -----

            phase5_ultimaWeaponId=0;
            phase5_firstTankPurgeSemaphore.Reset();
            
            phase5sub2_discretizedTitanPosition=-1;
            phase5sub2_titanSemaphore.Reset();
            phase5sub2_ultimaWeaponAppearanceCounter=0;
            phase5sub2_ultimaWeaponAppearance1Semaphore.Reset();
            phase5sub2_ultimaWeaponAppearance2Semaphore.Reset();

            phase5sub4_ultimateAnnihilationSemaphore.Reset();
            phase5sub4_discretizedIfritPosition=-1;
            phase5sub4_existingAetheroplasm.Clear();
            phase5sub4_aetheroplasmAppearance1Semaphore.Reset();
            phase5sub4_aetheroplasmAppearance234Semaphore.Reset();
            phase5sub4_aetheroplasmAppearance4Semaphore.Reset();
            phase5sub4_aetheroplasmDetonationCounter=0;
            phase5sub4_aetheroplasmDetonation1Semaphore.Reset();
            phase5sub4_aetheroplasmDetonation2Semaphore.Reset();
            phase5sub4_aetheroplasmDetonation3Semaphore.Reset();
            phase5sub4_aetheroplasmDetonation4Semaphore.Reset();
            phase5sub4_featherRainCounter=0;
            phase5sub4_featherRainSemaphore1.Reset();
            phase5sub4_featherRainSemaphore2.Reset();

            phase5sub5_ultimateSuppressionSemaphore.Reset();
            phase5sub5_eruptionCounter=0;
            for(int i=0;i<phase5sub5_isEruption.Length;++i)phase5sub5_isEruption[i]=false;
            phase5sub5_eruptionSemaphore.Reset();
            phase5sub5_mistralSongCounter=0;
            for(int i=0;i<phase5sub5_isMistralSong.Length;++i)phase5sub5_isMistralSong[i]=false;
            phase5sub5_mistralSongSemaphore.Reset();
            phase5sub5_rockThrowTarget=0;

            phase5sub6_ultimaSemaphore.Reset();
            phase5sub6_ultimaplasmStackCounter=0;
            for(int i=0;i<phase5sub6_ultimaplasm.Length;++i)phase5sub6_ultimaplasm[i].Clear();

            enragePhasePattern=EnragePhasePatterns.UNKNOWN;
            phase5sub6_ifritSemaphore.Reset();
            phase5sub6_titanSemaphore.Reset();

            // ----- End Of Major Phase 5 -----

        }
        
        #endregion
        
        #region Shenanigans
        
        private System.Threading.AutoResetEvent shenaniganSemaphore=new System.Threading.AutoResetEvent(false);
        private static ImmutableList<string> quotes=[
            "Greet the banks of the Jordan, and the fallen towers of Zion...",
            "Over the burial mounds, the wind howls past.",
            "Slaves are not the bricks that pave your road, nor are they chapters in your history of redemption.",
            "You have made us for yourself, O Lord, and our heart is restless until it rests in you.",
            "No! I am still alive! I shall live forever! There is something within me that can never die!",
            "The living are denied a seat at the table, while the dead lie honoured in their coffins.",
            "All they that take the sword shall perish with the sword.",
            "Injustice anywhere is a threat to justice everywhere.",
            "To my death, I never saw the dawn break over my homeland.",
            "I was born into a kind world and loved it with all my heart. I die in an evil world, and at parting I have nothing to say.",
            "You cannot raise someone on pain, nor fill their belly with fury.",
            "\"We have made it through!\"",
            "The bison were slaughtered, and the villagers feasted on what remained.",
            "Those who draw fire upon themselves soon learn that the burn arrives hand in hand with the warmth.",
            "Built upon shifting sands, the great edifice must fall.",
            "A faithful man shall abound with blessings.",
            "She smiled sorrowfully and vanished into the endless night sky.",
            "The end may justify the means, but something must justify the end.",
            "The faithful fall one after another, and the blight of ignorance spreads far and wide.",
            "They bled their last defending pebbles in the sand.",
            "History is mankind's endeavour to recall its ideals.\n-- Eamon de Valera, 1929",
            "Let us dedicate ourselves to what the Greeks wrote so many years ago: to tame the savageness of man and make gentle the life of this world.\n-- Robert F. Kennedy, 1968",
            "Yesterday is not ours to recover, but tomorrow is ours to win or to lose.\n-- Lyndon B. Johnson, 1964",
            "The end of hope is the beginning of defeat.\n-- Charles de Gaulle, 1945",
            "When I lay down my office and return home, I shall take nothing with me but clean hands.\n-- Antonio de Oliveira Salazar, 1968",
            "When smashing monuments, save the pedestals. They always come in handy.\n-- Stanislaw Jerzy Lec, 1957",
            "Fear not the path of truth for the lack of people walking on it.\n-- Robert F. Kennedy, 1968",
            "The rocket performed perfectly, except for landing on the wrong planet.\n-- Wernher von Braun, after the first V-2 struck London, 1944",
            "Do not pray for easy lives, my friends. Pray to be stronger men.\n-- John F. Kennedy, 1963",
            "The optimist proclaims that we live in the best of all possible worlds; and the pessimist fears this is true.\n-- James Branch Cabell, The Silver Stallion, 1926",
            "One seldom recognizes the devil when he is putting his hand on your shoulder.\n-- Albert Speer, 1972",
            "They don't ask much of you. They only want you to hate the things you love and to love the things you despise.\n-- Boris Pasternak, 1960",
            "Most economic fallacies derive from the tendency to assume that there is a fixed pie, that one party can gain only at the expense of another.\n-- Milton Friedman, 1980",
            "There are three kinds of lies: lies, damned lies, and statistics.\n-- Mark Twain, 1907",
            "Bite me once, shame on the dog; bite me over and over, shame on me for allowing it.\n-- Phyllis Schlafly, 1995",
            "Feng shui - believe in it if you will, but I believe far more that success is the work of man.\n-- Li Ka-shing, 1969",
            "A good reputation for yourself and your business is an asset no balance sheet can show, yet its value is beyond measure.\n-- Li Ka-shing, 1967",
            "Wealth comes and goes, but learning serves you all your life.\n-- Stanley Ho, 1966",
            "People often say, \"We live in a corrupt and dishonest society.\" Yet it is not entirely so; the kind and the good are still the majority.\n-- John Paul I, 1978",
            "Half the confusion in the world comes from not knowing how little we need.\n-- Rear Admiral Richard E. Byrd, in Antarctica, 1935"
        ];

        [ScriptMethod(name:"Shenanigans",
            eventType:EventTypeEnum.AddCombatant,
            eventCondition:["DataId:8722"],
            suppress:14000,
            userControl:false)]

        public void Shenanigans(Event @event,ScriptAccessory accessory) {
            
            if(!enableShenanigans) {

                return;

            }

            bool signalled=shenaniganSemaphore.WaitOne(14000);

            if(!signalled) {

                return;

            }

            System.Threading.Thread.Sleep(4000);
            
            string prompt=quotes[new System.Random().Next(0,quotes.Count)];

            if(!string.IsNullOrWhiteSpace(prompt)) {

                if(enablePrompts) {
                    
                    accessory.Method.TextInfo(prompt,10000);
                    
                }
                    
                accessory.tts(prompt,enableVanillaTts,enableDailyRoutinesTts);
                
            }

        }

        #endregion
        
        #region Global
        
        [ScriptMethod(name:"通用 小队排序测试",
            eventType:EventTypeEnum.Chat,
            eventCondition:["Type:Echo"])]

        public void 通用_小队排序测试(Event @event,ScriptAccessory accessory) {

            string processedText=(@event["Message"]).Trim().ToLower();
            
            if(!string.Equals(processedText,"kuwutest")) {

                return;

            }
            
            string text="Please confirm the following party sort order:\n";
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
            KodakkuAssist.Module.GameOperate.MarkType[] marks=[MarkType.Stop1, // MT
                                                               MarkType.Stop2, // OT (ST)
                                                               MarkType.Bind1, // H1
                                                               MarkType.Bind2, // H2
                                                               MarkType.Attack1, // M1 (D1)
                                                               MarkType.Attack2, // M2 (D2)
                                                               MarkType.Attack3, // R1 (D3)
                                                               MarkType.Attack4]; // R2 (D4)

            for(int i=0;i<marks.Length;++i) {
                
                accessory.Method.Mark(accessory.Data.PartyList[i],marks[i]);
                
                sourceObject=accessory.Data.Objects.SearchById(accessory.Data.PartyList[i]);
                
                if(sourceObject==null) {

                    continue;
                
                }
                
                else {
                
                    if(sourceObject is not ICharacter sourceICharacter) {

                        continue;
                    
                    }

                    else {
                        
                        text+=$"{roles[i]}: {sourceICharacter.Name}, marked as {marks[i].ToString()}.";

                        if(i<marks.Length-1) {

                            text+="\n";

                        }
                        
                        log+=$"Mark {accessory.Data.PartyList[i]} as {marks[i].ToString()}\n";

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

                accessory.Method.TextInfo(text,20000);
                
            }
            
            accessory.tts(text,enableVanillaTts,enableDailyRoutinesTts);

            if(enableDebugLogging) {
                
                accessory.Log.Debug($"\n-----Party Test Text-----\n{text}\n\n-----Party Test Log-----\n{log}");
                
            }

        }
        
        [ScriptMethod(name:"通用 小队排序测试清除",
            eventType:EventTypeEnum.Chat,
            eventCondition:["Type:Echo"],
            userControl:false)]

        public void 通用_小队排序测试清除(Event @event,ScriptAccessory accessory) {

            string processedText=(@event["Message"]).Trim().ToLower();
            
            if(!string.Equals(processedText,"kuwuclear")) {

                return;

            }
            
            accessory.Method.MarkClear();
            
            if(enableDebugLogging) {
                
                accessory.Log.Debug("Now trying to clear party test signs...");
                
            }

        }
        
        [ScriptMethod(name:"通用 飞翎雨 (范围)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11085"])]

        public void 通用_飞翎雨_范围(Event @event,ScriptAccessory accessory) {
            
            Vector3 effectPosition=ARENA_CENTER;

            try {

                effectPosition=JsonConvert.DeserializeObject<Vector3>(@event["EffectPosition"]);

            } catch(Exception e) {
                
                accessory.Log.Error("EffectPosition deserialization failed.");

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(3);
            currentProperties.Position=effectPosition;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.DestoryAt=1000;
            
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,currentProperties);

        }
        
        [ScriptMethod(name:"通用 低气压 (更新)",
            eventType:EventTypeEnum.StatusAdd,
            eventCondition:["StatusID:1525"],
            userControl:false)]

        public void 状态_低气压_更新(Event @event,ScriptAccessory accessory) {

            if(!convertObjectIdToDecimal(@event["TargetId"], out var targetId)) {
                
                return;
                
            }

            int targetIndex=accessory.Data.PartyList.IndexOf(((uint)targetId));
            
            if(!isLegalPartyIndex(targetIndex)) {

                return;

            }
            
            if(!convertStringToSignedInteger(@event["StackCount"], out var stackCount)) {
                
                return;
                
            }

            if(stackCount<0) {

                return;

            }

            int stackBefore=0;

            lock(stackOfThermalLow) {
                
                stackBefore=stackOfThermalLow[targetIndex];
                
                stackOfThermalLow[targetIndex]=stackCount;
                
            }

            if(enableDebugLogging) {
                
                accessory.Log.Debug($"stackOfThermalLow[{targetIndex}]:{stackBefore}->{stackCount}");
                
            }

        }
        
        [ScriptMethod(name:"通用 低气压 (移除)",
            eventType:EventTypeEnum.StatusRemove,
            eventCondition:["StatusID:1525"],
            userControl:false)]

        public void 状态_低气压_移除(Event @event,ScriptAccessory accessory) {

            if(!convertObjectIdToDecimal(@event["TargetId"], out var targetId)) {
                
                return;
                
            }

            int targetIndex=accessory.Data.PartyList.IndexOf(((uint)targetId));
            
            if(!isLegalPartyIndex(targetIndex)) {

                return;

            }

            bool anomalousStackCount=false;
            
            if(!convertStringToSignedInteger(@event["StackCount"], out var stackCount)) {

                anomalousStackCount=true;

            }

            if(stackCount<0) {

                anomalousStackCount=true;

            }

            bool recordMismatch=false;
            int expectedStack=0;

            lock(stackOfThermalLow) {
                
                expectedStack=stackOfThermalLow[targetIndex];
                
                stackOfThermalLow[targetIndex]=0;
                
            }
            
            if(!anomalousStackCount) {

                if(expectedStack!=stackCount) {

                    recordMismatch=true;

                }
                
            }

            else {

                recordMismatch=true;

            }

            if(enableDebugLogging) {

                if(anomalousStackCount) {
                    
                    accessory.Log.Debug($"stackOfThermalLow[{targetIndex}]:?->0\nanomalousStackCount={anomalousStackCount}\nexpectedStack={expectedStack}");
                    
                }

                else {

                    if(recordMismatch) {
                        
                        accessory.Log.Debug($"stackOfThermalLow[{targetIndex}]:{stackCount}->0\nexpectedStack={expectedStack}");
                        
                    }

                    else {
                        
                        accessory.Log.Debug($"stackOfThermalLow[{targetIndex}]:{stackCount}->0");
                        
                    }
                    
                }
                
            }

        }
        
        [ScriptMethod(name:"通用 觉醒 (数据获取)",
            eventType:EventTypeEnum.StatusAdd,
            eventCondition:["StatusID:1529"],
            userControl:false)]
    
        public void 觉醒_数据获取(Event @event,ScriptAccessory accessory) {

            if(majorPhase!=1&&majorPhase!=2&&majorPhase!=3&&!skipPhaseChecks) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["TargetId"], out var targetId)) {
                
                return;
                
            }
            
            var targetObject=accessory.Data.Objects.SearchById(targetId);

            if(targetObject==null) {

                return;

            }

            switch(targetObject.DataId) {

                case 8722: {
                    
                    garudaHasWoken=true;

                    if(enableDebugLogging) {
                        
                        accessory.Log.Debug($"garudaHasWoken={garudaHasWoken}");
                        
                    }

                    break;

                }
                
                case 8730: {
                    
                    ifritHasWoken=true;

                    if(enableDebugLogging) {
                        
                        accessory.Log.Debug($"ifritHasWoken={ifritHasWoken}");
                        
                    }

                    break;

                }
                
                case 8727: {
                    
                    titanHasWoken=true;

                    if(enableDebugLogging) {
                        
                        accessory.Log.Debug($"titanHasWoken={titanHasWoken}");
                        
                    }

                    break;

                }

                default: {

                    break;

                }
                
            }

        }
        
        [ScriptMethod(name:"通用 美翼与妙翅的邪轮旋风 (范围)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11084"])]

        public void 通用_美翼与妙翅的邪轮旋风_范围(Event @event,ScriptAccessory accessory) {
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(8.36f);
            currentProperties.Owner=sourceId;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.DestoryAt=3000;
        
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,currentProperties);

        }
        
        [ScriptMethod(name:"通用 灼热 (范围)",
            eventType:EventTypeEnum.StatusAdd,
            eventCondition:["StatusID:1578"])]

        public void 通用_灼热_范围(Event @event,ScriptAccessory accessory) {
            
            if(!convertObjectIdToDecimal(@event["TargetId"], out var targetId)) {
                
                return;
                
            }
            
            if(disableSearingWindOnMe) {

                if(targetId==accessory.Data.Me) {

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
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Name=$"通用_灼热_范围_{targetId}";
            currentProperties.Scale=new(15);
            currentProperties.Owner=targetId;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.DestoryAt=durationMilliseconds;
            
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,currentProperties);
            
        }
        
        [ScriptMethod(name:"通用 灼热 (数据获取)",
            eventType:EventTypeEnum.StatusAdd,
            eventCondition:["StatusID:1578"],
            userControl:false)]

        public void 通用_灼热_数据获取(Event @event,ScriptAccessory accessory) {
            
            if(!convertObjectIdToDecimal(@event["TargetId"], out var targetId)) {
                
                return;
                
            }

            bool elementDoesntExist=false;
            
            lock(partyMembersWithSearingWind) {

                elementDoesntExist=partyMembersWithSearingWind.Add(targetId);

            }

            if(enableDebugLogging) {
                
                accessory.Log.Debug($"Trying to add {targetId} to partyMembersWithSearingWind...\nelementDoesntExist={elementDoesntExist}");
                
            }
            
        }
        
        [ScriptMethod(name:"通用 灼热 (数据清除与范围清除)",
            eventType:EventTypeEnum.StatusRemove,
            eventCondition:["StatusID:1578"],
            userControl:false)]

        public void 通用_灼热_数据清除与范围清除(Event @event,ScriptAccessory accessory) {
            
            if(!convertObjectIdToDecimal(@event["TargetId"], out var targetId)) {
                
                return;
                
            }

            bool elementFound=false;

            lock(partyMembersWithSearingWind) {

                elementFound=partyMembersWithSearingWind.Remove(targetId);

            }
            
            if(enableDebugLogging) {
                
                accessory.Log.Debug($"Trying to remove {targetId} in partyMembersWithSearingWind...\nelementFound={elementFound}");
                
            }
            
            accessory.Method.RemoveDraw($"通用_灼热_范围_{targetId}");
            
        }
        
        [ScriptMethod(name:"通用 烈焰碎击 (范围)",
            eventType:EventTypeEnum.TargetIcon,
            eventCondition:["Id:0075"])]

        public void 通用_烈焰碎击_范围(Event @event,ScriptAccessory accessory) {
            
            if(!convertObjectIdToDecimal(@event["TargetId"], out var targetId)) {
                
                return;
                
            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(4);
            currentProperties.Owner=targetId;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.DestoryAt=5125;
            
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,currentProperties);

        }
        
        [ScriptMethod(name:"通用 大地之重 (精确范围)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11109"])]

        public void 通用_大地之重_精确范围(Event @event,ScriptAccessory accessory) {
            
            Vector3 effectPosition=ARENA_CENTER;

            try {

                effectPosition=JsonConvert.DeserializeObject<Vector3>(@event["EffectPosition"]);

            } catch(Exception e) {
                
                accessory.Log.Error("EffectPosition deserialization failed.");

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(6);
            currentProperties.Position=effectPosition;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.DestoryAt=3000;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Circle,currentProperties);
            
        }
        
        [ScriptMethod(name:"通用 地裂 (精确范围)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11120"])]

        public void 通用_地裂_精确范围(Event @event,ScriptAccessory accessory) {
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(6,40);
            currentProperties.Owner=sourceId;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.DestoryAt=2250;
        
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Rect,currentProperties);

        }
        
        [ScriptMethod(name:"通用 觉醒后的地裂 (范围)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11298"])]

        public void 通用_觉醒后的地裂_范围(Event @event,ScriptAccessory accessory) {
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(6,40);
            currentProperties.Owner=sourceId;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.DestoryAt=2000;
        
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Rect,currentProperties);

        }
        
        [ScriptMethod(name:"通用 吸附式炸弹 (范围)",
            eventType:EventTypeEnum.StatusAdd,
            eventCondition:["StatusID:1532"])]
    
        public void 通用_吸附式炸弹_范围(Event @event,ScriptAccessory accessory) {
            
            if(!convertObjectIdToDecimal(@event["TargetId"], out var targetId)) {
                
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
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Name=$"通用_吸附式炸弹_范围_{targetId}";
            currentProperties.Scale=new(4);
            currentProperties.Owner=targetId;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.DestoryAt=durationMilliseconds;
            
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,currentProperties);
        
        }
        
        [ScriptMethod(name:"通用 吸附式炸弹 (范围清除)",
            eventType:EventTypeEnum.StatusRemove,
            eventCondition:["StatusID:1532"],
            userControl:false)]

        public void 通用_吸附式炸弹_范围清除(Event @event,ScriptAccessory accessory) {
            
            if(!convertObjectIdToDecimal(@event["TargetId"], out var targetId)) {
                
                return;
                
            }
            
            accessory.Method.RemoveDraw($"通用_吸附式炸弹_范围_{targetId}");
            
        }
        
        [ScriptMethod(name:"通用 究极神兵的地裂 (精确范围)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11135"])]

        public void 通用_究极神兵的地裂_精确范围(Event @event,ScriptAccessory accessory) {
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(6,40);
            currentProperties.Owner=sourceId;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.DestoryAt=2250;
        
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Rect,currentProperties);

        }
        
        #endregion
        
        #region Garuda
        
        [ScriptMethod(name:"迦楼罗 向北拉Boss (指示,仅MT)",
            eventType:EventTypeEnum.AddCombatant,
            eventCondition:["DataId:8722"],
            suppress:COMMON_INTERVAL)]

        public void 迦楼罗_向北拉Boss_指示_仅MT(Event @event,ScriptAccessory accessory) {
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }
            
            System.Threading.Tasks.Task.Delay(4000).ContinueWith(_=> {
                
                if(majorPhase!=1&&!skipPhaseChecks) {

                    return;

                }

                if(phase!=1&&!skipPhaseChecks) {

                    return;

                }
                
                int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            
                if(!isLegalPartyIndex(myIndex)) {

                    return;

                }

                if(myIndex!=0) {

                    return;

                }
            
                var currentProperties=accessory.Data.GetDefaultDrawProperties();

                currentProperties.Name="迦楼罗_向北拉Boss_指示_仅MT";
                currentProperties.Scale=new(2);
                currentProperties.Owner=sourceId;
                currentProperties.TargetPosition=new Vector3(100f,0,84f);
                currentProperties.ScaleMode|=ScaleMode.YByDistance;
                currentProperties.Color=colourOfDirectionIndicators.V4.WithW(1);
                currentProperties.DestoryAt=MAXIMUM_DURATION;
            
                accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
                
            });

        }
        
        [ScriptMethod(name:"迦楼罗 向东拉Boss (指示清除)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11091"],
            userControl:false)]

        public void 迦楼罗_向北拉Boss_指示清除(Event @event,ScriptAccessory accessory) {

            if(majorPhase!=1&&!skipPhaseChecks) {

                return;

            }

            if(phase!=1&&!skipPhaseChecks) {

                return;

            }
            
            accessory.Method.RemoveDraw("迦楼罗_向北拉Boss_指示_仅MT");

        }
        
        [ScriptMethod(name:"迦楼罗 螺旋气流 (范围)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11091"])]

        public void 迦楼罗_螺旋气流_范围(Event @event,ScriptAccessory accessory) {

            if(majorPhase!=1&&!skipPhaseChecks) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(12);
            currentProperties.Owner=sourceId;
            currentProperties.Radian=float.Pi/2;
            currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(1);
            currentProperties.DestoryAt=2500;
        
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Fan,currentProperties);

        }
        
        [ScriptMethod(name:"迦楼罗 螺旋气流 (计数器)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11091"],
            userControl:false)]

        public void 迦楼罗_螺旋气流_计数器(Event @event,ScriptAccessory accessory) {

            if(majorPhase!=1&&!skipPhaseChecks) {

                return;

            }

            Interlocked.Increment(ref phase1_slipstreamCounter);

            if(phase1_slipstreamCounter==2||phase1_slipstreamCounter==3) {

                phase1_downburstSemaphore.Set();

            }
            
            if(enableDebugLogging) {
                
                accessory.Log.Debug($"phase1_slipstreamCounter={phase1_slipstreamCounter}");
                
            }

        }
        
        [ScriptMethod(name:"迦楼罗 第一次寒风之歌 (数据获取)",
            eventType:EventTypeEnum.TargetIcon,
            eventCondition:["Id:0010"],
            userControl:false)]

        public void 迦楼罗_第一次寒风之歌_数据获取(Event @event,ScriptAccessory accessory) {

            if(majorPhase!=1&&!skipPhaseChecks) {

                return;

            }

            if(phase!=1&&!skipPhaseChecks) {

                return;

            }

            if(phase1_targetOfMistralSong!=0) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["TargetId"], out var targetId)) {
                
                return;
                
            }
            
            phase1_targetOfMistralSong=targetId;

            phase1_mistralSongSemaphore.Set();

            if(enableDebugLogging) {
                
                accessory.Log.Debug($"phase1_targetOfMistralSong={phase1_targetOfMistralSong}");
                
            }

        }
        
        [ScriptMethod(name:"迦楼罗 第一次寒风之歌 (范围)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11091"])]

        public void 迦楼罗_第一次寒风之歌_范围(Event @event,ScriptAccessory accessory) {

            if(majorPhase!=1&&!skipPhaseChecks) {

                return;

            }

            if(phase!=1&&!skipPhaseChecks) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }

            bool signalled=phase1_mistralSongSemaphore.WaitOne(COMMON_INTERVAL);
            
            if(!signalled) {

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(3.5f,40);
            currentProperties.Owner=sourceId;
            currentProperties.TargetObject=phase1_targetOfMistralSong;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=5250;
        
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Rect,currentProperties);

        }
        
        [ScriptMethod(name:"迦楼罗 第一次大龙卷风 (范围)",
            eventType:EventTypeEnum.ActionEffect,
            eventCondition:["ActionId:11074"])]

        public void 迦楼罗_第一次大龙卷风_范围(Event @event,ScriptAccessory accessory) {

            if(majorPhase!=1&&!skipPhaseChecks) {

                return;

            }

            if(phase!=1&&!skipPhaseChecks) {

                return;

            }

            if(!string.Equals(@event["TargetIndex"],"1")) {

                return;

            }
            
            Vector3 targetPosition=ARENA_CENTER;

            try {

                targetPosition=JsonConvert.DeserializeObject<Vector3>(@event["TargetPosition"]);

            } catch(Exception e) {
                
                accessory.Log.Error("TargetPosition deserialization failed.");

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(8);
            currentProperties.Position=targetPosition;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.DestoryAt=18375;
            
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,currentProperties);

        }
        
        [ScriptMethod(name:"迦楼罗 拉刺羽 (指示,仅ST)",
            eventType:EventTypeEnum.AddCombatant,
            eventCondition:["DataId:8726"])]

        public void 迦楼罗_拉刺羽_指示_仅ST(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=1&&!skipPhaseChecks) {

                return;

            }

            if(phase!=1&&!skipPhaseChecks) {

                return;

            }
                
            int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            
            if(!isLegalPartyIndex(myIndex)) {

                return;

            }

            if(myIndex!=1) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Name="迦楼罗_拉刺羽_指示_仅ST";
            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetObject=sourceId;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=colourOfDirectionIndicators.V4.WithW(1);
            currentProperties.DestoryAt=MAXIMUM_DURATION;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);

        }
        
        [ScriptMethod(name:"迦楼罗 拉刺羽 (指示清除)",
            eventType:EventTypeEnum.Tether,
            eventCondition:["Id:0011"],
            userControl:false)]

        public void 迦楼罗_拉刺羽_指示清除(Event @event,ScriptAccessory accessory) {

            if(majorPhase!=1&&!skipPhaseChecks) {

                return;

            }

            if(phase!=1&&!skipPhaseChecks) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }
            
            var sourceObject=accessory.Data.Objects.SearchById(sourceId);

            if(sourceObject==null) {

                return;

            }

            if(sourceObject.DataId!=8726) {

                return;

            }
            
            accessory.Method.RemoveDraw("迦楼罗_拉刺羽_指示_仅ST");

        }
        
        [ScriptMethod(name:"迦楼罗 下行突风 (范围)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11091"])]

        public void 迦楼罗_下行突风_范围(Event @event,ScriptAccessory accessory) {

            if(majorPhase!=1&&!skipPhaseChecks) {

                return;

            }
            
            bool signalled=phase1_downburstSemaphore.WaitOne(COMMON_INTERVAL);
            
            if(!signalled) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(12);
            currentProperties.Owner=sourceId;
            currentProperties.Radian=float.Pi/2;
            currentProperties.TargetResolvePattern=PositionResolvePatternEnum.OwnerEnmityOrder;
            currentProperties.TargetOrderIndex=1;
            currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(1);
            currentProperties.Delay=2500;
            currentProperties.DestoryAt=3500;
        
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Fan,currentProperties);

        }
        
        [ScriptMethod(name:"迦楼罗 飞翎雨 (阶段控制)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11085"],
            suppress:COMMON_INTERVAL,
            userControl:false)]

        public void 迦楼罗_飞翎雨_阶段控制(Event @event,ScriptAccessory accessory) {

            if(majorPhase!=1&&!skipPhaseChecks) {

                return;

            }

            Interlocked.Increment(ref phase);

            if(phase==3) {
                
                accessory.Method.RemoveDraw(@"^迦楼罗_低气压_指路_.*$");
                
            }
            
            if(enableDebugLogging) {
                
                accessory.Log.Debug($"majorPhase={majorPhase}\nphase={phase}");
                
            }

        }
        
        [ScriptMethod(name:"迦楼罗 大暴风 (精确范围)",
            eventType:EventTypeEnum.ObjectChanged,
            eventCondition:["DataId:2002792"])]

        public void 迦楼罗_大暴风_精确范围(Event @event,ScriptAccessory accessory) {

            if(majorPhase!=1&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=1&&phase!=2&&!skipPhaseChecks) {

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
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(6);
            currentProperties.Position=sourcePosition;
            currentProperties.Color=colourOfDirectionIndicators.V4.WithW(1);
            currentProperties.DestoryAt=23000;
        
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Circle,currentProperties);

        }
        
        [ScriptMethod(name:"迦楼罗 大暴风 (数据获取)",
            eventType:EventTypeEnum.ObjectChanged,
            eventCondition:["DataId:2002792"],
            userControl:false)]

        public void 迦楼罗_大暴风_数据获取(Event @event,ScriptAccessory accessory) {

            if(majorPhase!=1&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=1&&phase!=2&&!skipPhaseChecks) {

                return;

            }
            
            bool signalled=phase1_gigastormSemaphore.WaitOne(0);

            if(signalled) {

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

            phase1_gigastormPosition=sourcePosition;

            phase1_gigastormSemaphore.Set();

        }
        
        [ScriptMethod(name:"迦楼罗 烈风刃 (指路)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11080"])]

        public void 迦楼罗_烈风刃_指路(Event @event,ScriptAccessory accessory) {

            if(majorPhase!=1&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=2&&!skipPhaseChecks) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["TargetId"], out var targetId)) {
                
                return;
                
            }

            int targetIndex=accessory.Data.PartyList.IndexOf(((uint)targetId));
            
            if(!isLegalPartyIndex(targetIndex)) {

                return;

            }
            
            int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            
            if(!isLegalPartyIndex(myIndex)) {

                return;

            }

            bool mtDodges=false;
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(5);
            currentProperties.Owner=targetId;
            currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(1);
            currentProperties.DestoryAt=2000;

            if(myIndex==0) {

                if(stackOfThermalLow[myIndex]<1) {
                    
                    currentProperties.Color=accessory.Data.DefaultSafeColor;
                    
                }

                else {
                    
                    currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(1);

                    mtDodges=true;

                }
                
            }

            else {
                
                currentProperties.Color=accessory.Data.DefaultSafeColor;
                
            }
        
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,currentProperties);

            if(myIndex!=targetIndex&&!mtDodges) {
                
                currentProperties=accessory.Data.GetDefaultDrawProperties();
            
                currentProperties.Scale=new(2);
                currentProperties.Owner=accessory.Data.Me;
                currentProperties.TargetObject=accessory.Data.PartyList[targetIndex];
                currentProperties.ScaleMode|=ScaleMode.YByDistance;
                currentProperties.Color=accessory.Data.DefaultSafeColor;
                currentProperties.DestoryAt=2000;
            
                accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
                
            }

        }
        
        [ScriptMethod(name:"迦楼罗 低气压 (指路,仅ST)",
            eventType:EventTypeEnum.StatusAdd,
            eventCondition:["StatusID:1525"])]

        public void 迦楼罗_低气压_指路_仅ST(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=1&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=1&&phase!=2&&!skipPhaseChecks) {

                return;

            }
            
            int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            
            if(!isLegalPartyIndex(myIndex)) {

                return;

            }

            if(myIndex!=1) {

                return;

            }

            if(phase1_hasEliminatedThermalLow[myIndex]) {

                return;

            }

            if(!convertObjectIdToDecimal(@event["TargetId"], out var targetId)) {
                
                return;
                
            }

            if(targetId!=accessory.Data.Me) {

                return;

            }
            
            if(!convertStringToSignedInteger(@event["StackCount"], out var stackCount)) {
                
                return;
                
            }

            if(stackCount!=2) {

                return;

            }
            
            bool signalled=phase1_gigastormSemaphore.WaitOne(15250);
            
            if(!signalled) {

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Name="迦楼罗_低气压_指路_仅ST";
            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetPosition=phase1_gigastormPosition;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=MAXIMUM_DURATION;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);

        }
        
        [ScriptMethod(name:"迦楼罗 低气压 (指路清除,仅ST)",
            eventType:EventTypeEnum.StatusRemove,
            eventCondition:["StatusID:1525"],
            userControl:false)]

        public void 迦楼罗_低气压_指路清除_仅ST(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=1&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=1&&phase!=2&&!skipPhaseChecks) {

                return;

            }
            
            if(phase1_hasEliminatedThermalLow[1]) {

                return;

            }

            if(!convertObjectIdToDecimal(@event["TargetId"], out var targetId)) {
                
                return;
                
            }

            int targetIndex=accessory.Data.PartyList.IndexOf(((uint)targetId));
            
            if(!isLegalPartyIndex(targetIndex)) {

                return;

            }

            if(targetIndex!=1) {

                return;

            }
            
            if(!convertStringToSignedInteger(@event["StackCount"], out var stackCount)) {
                
                return;
                
            }

            if(stackCount<2) {

                return;

            }

            phase1_hasEliminatedThermalLow[1]=true;
            
            accessory.Method.RemoveDraw("迦楼罗_低气压_指路_仅ST");

        }
        
        [ScriptMethod(name:"迦楼罗 低气压 (指路清除,仅ST)",
            eventType:EventTypeEnum.ActionEffect,
            eventCondition:["ActionId:11189"],
            userControl:false)]

        public void 迦楼罗_低气压_指路清除2_仅ST(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=1&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=1&&phase!=2&&!skipPhaseChecks) {

                return;

            }
            
            if(!string.Equals(@event["TargetIndex"],"1")) {

                return;

            }

            if(!convertObjectIdToDecimal(@event["TargetId"], out var targetId)) {
                
                return;
                
            }

            int targetIndex=accessory.Data.PartyList.IndexOf(((uint)targetId));
            
            if(!isLegalPartyIndex(targetIndex)) {

                return;

            }

            if(targetIndex!=1) {

                return;

            }
            
            if(phase1_hasEliminatedThermalLow[targetIndex]) {

                return;

            }

            phase1_hasEliminatedThermalLow[1]=true;
            
            accessory.Method.RemoveDraw("迦楼罗_低气压_指路_仅ST");

        }
        
        [ScriptMethod(name:"迦楼罗 低气压 (指路,远程DPS与治疗)",
            eventType:EventTypeEnum.StatusAdd,
            eventCondition:["StatusID:1525"])]

        public void 迦楼罗_低气压_指路_远程DPS与治疗(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=1&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=2&&!skipPhaseChecks) {

                return;

            }
            
            int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            
            if(!isLegalPartyIndex(myIndex)) {

                return;

            }

            if(!isRanged(myIndex)) {

                return;

            }

            if(phase1_hasEliminatedThermalLow[myIndex]) {

                return;

            }

            if(!convertObjectIdToDecimal(@event["TargetId"], out var targetId)) {
                
                return;
                
            }

            if(targetId!=accessory.Data.Me) {

                return;

            }
            
            if(!convertStringToSignedInteger(@event["StackCount"], out var stackCount)) {
                
                return;
                
            }

            if(stackCount!=1) {

                return;

            }
            
            bool signalled=phase1_gigastormSemaphore.WaitOne(6250);
            
            if(!signalled) {

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Name=$"迦楼罗_低气压_指路_远程DPS与治疗_{myIndex}";
            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetPosition=phase1_gigastormPosition;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=MAXIMUM_DURATION;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);

        }
        
        [ScriptMethod(name:"迦楼罗 低气压 (指路清除,远程DPS与治疗)",
            eventType:EventTypeEnum.StatusRemove,
            eventCondition:["StatusID:1525"],
            userControl:false)]

        public void 迦楼罗_低气压_指路清除_远程DPS与治疗(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=1&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=2&&!skipPhaseChecks) {

                return;

            }

            if(!convertObjectIdToDecimal(@event["TargetId"], out var targetId)) {
                
                return;
                
            }

            int targetIndex=accessory.Data.PartyList.IndexOf(((uint)targetId));
            
            if(!isLegalPartyIndex(targetIndex)) {

                return;

            }

            if(!isRanged(targetIndex)) {

                return;

            }
            
            if(phase1_hasEliminatedThermalLow[targetIndex]) {

                return;

            }
            
            if(!convertStringToSignedInteger(@event["StackCount"], out var stackCount)) {
                
                return;
                
            }

            if(stackCount<1) {

                return;

            }

            phase1_hasEliminatedThermalLow[targetIndex]=true;
            
            accessory.Method.RemoveDraw($"迦楼罗_低气压_指路_远程DPS与治疗_{targetIndex}");

        }
        
        [ScriptMethod(name:"迦楼罗 低气压 (指路清除2,远程DPS与治疗)",
            eventType:EventTypeEnum.ActionEffect,
            eventCondition:["ActionId:regex:^(11079|11189)$"],
            userControl:false)]

        public void 迦楼罗_低气压_指路清除2_远程DPS与治疗(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=1&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=2&&!skipPhaseChecks) {

                return;

            }
            
            if(!string.Equals(@event["TargetIndex"],"1")) {

                return;

            }

            if(!convertObjectIdToDecimal(@event["TargetId"], out var targetId)) {
                
                return;
                
            }

            int targetIndex=accessory.Data.PartyList.IndexOf(((uint)targetId));
            
            if(!isLegalPartyIndex(targetIndex)) {

                return;

            }

            if(!isRanged(targetIndex)) {

                return;

            }
            
            if(phase1_hasEliminatedThermalLow[targetIndex]) {

                return;

            }

            phase1_hasEliminatedThermalLow[targetIndex]=true;
            
            accessory.Method.RemoveDraw($"迦楼罗_低气压_指路_远程DPS与治疗_{targetIndex}");

        }
        
        [ScriptMethod(name:"迦楼罗 低气压 (指路,仅D1)",
            eventType:EventTypeEnum.StatusAdd,
            eventCondition:["StatusID:1525"])]

        public void 迦楼罗_低气压_指路_仅D1(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=1&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=2&&!skipPhaseChecks) {

                return;

            }
            
            int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            
            if(!isLegalPartyIndex(myIndex)) {

                return;

            }

            if(myIndex!=4) {

                return;

            }

            if(phase1_hasEliminatedThermalLow[myIndex]) {

                return;

            }

            if(!convertObjectIdToDecimal(@event["TargetId"], out var targetId)) {
                
                return;
                
            }

            if(targetId!=accessory.Data.Me) {

                return;

            }
            
            if(!convertStringToSignedInteger(@event["StackCount"], out var stackCount)) {
                
                return;
                
            }

            if(stackCount!=2) {

                return;

            }
            
            bool signalled=phase1_gigastormSemaphore.WaitOne(6250);
            
            if(!signalled) {

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Name=$"迦楼罗_低气压_指路_近战DPS_{myIndex}";
            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetPosition=phase1_gigastormPosition;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=MAXIMUM_DURATION;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);

        }
        
        [ScriptMethod(name:"迦楼罗 低气压 (粗略指路,仅D2)",
            eventType:EventTypeEnum.StatusRemove,
            eventCondition:["StatusID:1525"])]

        public void 迦楼罗_低气压_粗略指路_仅D2(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=1&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=2&&!skipPhaseChecks) {

                return;

            }
            
            int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            
            if(!isLegalPartyIndex(myIndex)) {

                return;

            }

            if(myIndex!=5) {

                return;

            }

            if(phase1_hasEliminatedThermalLow[myIndex]) {

                return;

            }

            if(!convertObjectIdToDecimal(@event["TargetId"], out var targetId)) {
                
                return;
                
            }

            int targetIndex=accessory.Data.PartyList.IndexOf(((uint)targetId));
            
            if(!isLegalPartyIndex(targetIndex)) {

                return;

            }

            if(targetIndex!=4) {

                return;

            }
            
            if(!convertStringToSignedInteger(@event["StackCount"], out var stackCount)) {
                
                return;
                
            }

            if(stackCount<2) {

                return;

            }
            
            bool signalled=phase1_gigastormSemaphore.WaitOne(6250);
            
            if(!signalled) {

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Name=$"迦楼罗_低气压_指路_近战DPS_{myIndex}";
            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetPosition=phase1_gigastormPosition;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=phase1_colourOfM2ImpreciseGuidance.V4.WithW(1);
            currentProperties.DestoryAt=MAXIMUM_DURATION;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);

        }
        
        [ScriptMethod(name:"迦楼罗 低气压 (指路清除,近战DPS)",
            eventType:EventTypeEnum.StatusRemove,
            eventCondition:["StatusID:1525"],
            userControl:false)]

        public void 迦楼罗_低气压_指路清除_近战DPS(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=1&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=2&&!skipPhaseChecks) {

                return;

            }

            if(!convertObjectIdToDecimal(@event["TargetId"], out var targetId)) {
                
                return;
                
            }

            int targetIndex=accessory.Data.PartyList.IndexOf(((uint)targetId));
            
            if(!isLegalPartyIndex(targetIndex)) {

                return;

            }

            if(!isMeleeDps(targetIndex)) {

                return;

            }
            
            if(phase1_hasEliminatedThermalLow[targetIndex]) {

                return;

            }
            
            if(!convertStringToSignedInteger(@event["StackCount"], out var stackCount)) {
                
                return;
                
            }

            if(stackCount<2) {

                return;

            }

            phase1_hasEliminatedThermalLow[targetIndex]=true;
            
            accessory.Method.RemoveDraw($"迦楼罗_低气压_指路_近战DPS_{targetIndex}");

        }
        
        [ScriptMethod(name:"迦楼罗 低气压 (指路清除2,近战DPS)",
            eventType:EventTypeEnum.ActionEffect,
            eventCondition:["ActionId:11189"],
            userControl:false)]

        public void 迦楼罗_低气压_指路清除2_近战DPS(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=1&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=2&&!skipPhaseChecks) {

                return;

            }
            
            if(!string.Equals(@event["TargetIndex"],"1")) {

                return;

            }

            if(!convertObjectIdToDecimal(@event["TargetId"], out var targetId)) {
                
                return;
                
            }

            int targetIndex=accessory.Data.PartyList.IndexOf(((uint)targetId));
            
            if(!isLegalPartyIndex(targetIndex)) {

                return;

            }

            if(!isMeleeDps(targetIndex)) {

                return;

            }
            
            if(phase1_hasEliminatedThermalLow[targetIndex]) {

                return;

            }

            phase1_hasEliminatedThermalLow[targetIndex]=true;
            
            accessory.Method.RemoveDraw($"迦楼罗_低气压_指路_近战DPS_{targetIndex}");

        }
        
        [ScriptMethod(name:"迦楼罗 向东北拉Boss (指示,仅MT)",
            eventType:EventTypeEnum.ActionEffect,
            eventCondition:["ActionId:11093"],
            suppress:COMMON_INTERVAL)]

        public void 迦楼罗_向东北拉Boss_指示_仅MT(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=1&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=3&&!skipPhaseChecks) {

                return;

            }
            
            int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            
            if(!isLegalPartyIndex(myIndex)) {

                return;

            }

            if(myIndex!=0) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Name="迦楼罗_向东北拉Boss_指示_仅MT";
            currentProperties.Scale=new(2);
            currentProperties.Owner=sourceId;
            currentProperties.TargetPosition=new Vector3(108.132f,0,91.868f);
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=colourOfDirectionIndicators.V4.WithW(1);
            currentProperties.DestoryAt=20500;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);

        }
        
        [ScriptMethod(name:"迦楼罗 第二次寒风之歌 (数据获取)",
            eventType:EventTypeEnum.PlayActionTimeline,
            eventCondition:["SourceDataId:8723"],
            userControl:false)]

        public void 迦楼罗_第二次寒风之歌_数据获取(Event @event,ScriptAccessory accessory) {

            if(majorPhase!=1&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=3&&phase!=4&&!skipPhaseChecks) {

                return;

            }
            
            if(!string.Equals(@event["Id"],"7747")) {

                return;

            }
            
            Vector3 sourcePosition=ARENA_CENTER;

            try {

                sourcePosition=JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);

            } catch(Exception e) {
                
                accessory.Log.Error("SourcePosition deserialization failed.");

                return;

            }

            if(Vector3.Distance(sourcePosition,ARENA_CENTER)<18.5f) {

                return;

            }
            
            int discretizedPosition=discretizePosition(sourcePosition,ARENA_CENTER,4);

            lock(phase1_tankBuster) {

                phase1_tankBuster[discretizedPosition]=true;

            }

            if(enableDebugLogging) {
                
                accessory.Log.Debug($"phase1_tankBuster[{discretizedPosition}]=true");
                
            }

        }
        
        [ScriptMethod(name:"迦楼罗 台风眼 (精确范围)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11090"])]

        public void 迦楼罗_台风眼_精确范围(Event @event,ScriptAccessory accessory) {

            if(majorPhase!=1&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=4&&phase!=5&&!skipPhaseChecks) {

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();
                
            currentProperties.Scale=new(25);
            currentProperties.InnerScale=new(11.5f);
            currentProperties.Radian=float.Pi*2;
            currentProperties.Position=ARENA_CENTER;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.DestoryAt=3000;
        
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Donut,currentProperties);

        }
        
        [ScriptMethod(name:"迦楼罗 第二次寒风之歌 (粗略范围)",
            eventType:EventTypeEnum.PlayActionTimeline,
            eventCondition:["SourceDataId:8723"])]

        public void 迦楼罗_第二次寒风之歌_粗略范围(Event @event,ScriptAccessory accessory) {

            if(majorPhase!=1&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=3&&phase!=4&&!skipPhaseChecks) {

                return;

            }
            
            if(!string.Equals(@event["Id"],"7747")) {

                return;

            }
            
            Vector3 sourcePosition=ARENA_CENTER;

            try {

                sourcePosition=JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);

            } catch(Exception e) {
                
                accessory.Log.Error("SourcePosition deserialization failed.");

                return;

            }

            if(Vector3.Distance(sourcePosition,ARENA_CENTER)<18.5f) {

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(3.5f,40);
            currentProperties.Position=sourcePosition;
            currentProperties.TargetPosition=ARENA_CENTER;
            currentProperties.Color=phase1_colourOfImpreciseRangeOfMistralSong.V4.WithW(1);
            currentProperties.DestoryAt=7125;
        
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Rect,currentProperties);

        }
        
        [ScriptMethod(name:"迦楼罗 第二次寒风之歌 (指路,DPS与治疗)",
            eventType:EventTypeEnum.TargetIcon,
            eventCondition:["Id:0010"],
            suppress:COMMON_INTERVAL)]

        public void 迦楼罗_第二次寒风之歌_指路_DPS与治疗(Event @event,ScriptAccessory accessory) {

            if(majorPhase!=1&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=4&&!skipPhaseChecks) {

                return;

            }
            
            int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            
            if(!isLegalPartyIndex(myIndex)) {

                return;

            }

            if(!isDps(myIndex)&&!isHealer(myIndex)) {

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();
            
            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetPosition=new Vector3(108f, 0, 106f);
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=5125;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);

        }
        
        [ScriptMethod(name:"迦楼罗 第二次寒风之歌 (指路,仅坦克)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11086"])]

        public void 迦楼罗_第二次寒风之歌_指路_坦克(Event @event,ScriptAccessory accessory) {

            if(majorPhase!=1&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=4&&!skipPhaseChecks) {

                return;

            }
            
            int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            
            if(!isLegalPartyIndex(myIndex)) {

                return;

            }

            if(!isTank(myIndex)) {

                return;

            }

            int myDiscretizedPosition=0;
            bool anomalousPosition=false;

            if(myIndex==0) {
                
                myDiscretizedPosition=0;

                while(!phase1_tankBuster[myDiscretizedPosition]) {

                    ++myDiscretizedPosition;

                    if(myDiscretizedPosition>=3) {

                        anomalousPosition=true;

                        break;

                    }

                }
                
            }
            
            if(myIndex==1) {
                
                myDiscretizedPosition=3;
                
                while(!phase1_tankBuster[myDiscretizedPosition]) {

                    --myDiscretizedPosition;

                    if(myDiscretizedPosition<=0) {

                        anomalousPosition=true;

                        break;

                    }

                }
                
            }

            if(enableDebugLogging) {
                
                accessory.Log.Debug($"myIndex={myIndex}\ndiscretizedPosition={myDiscretizedPosition}\nanomalousPosition={anomalousPosition}");
                
            }

            if(anomalousPosition) {

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();
            
            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetPosition=rotatePosition(new Vector3(100,0,89.5f),ARENA_CENTER,Math.PI/2*myDiscretizedPosition);
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=3000;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);

        }
        
        [ScriptMethod(name:"迦楼罗 第二次大龙卷风 (范围)",
            eventType:EventTypeEnum.ActionEffect,
            eventCondition:["ActionId:11083"])]

        public void 迦楼罗_第二次大龙卷风_范围(Event @event,ScriptAccessory accessory) {

            if(majorPhase!=1&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=4&&!skipPhaseChecks) {

                return;

            }

            if(!string.Equals(@event["TargetIndex"],"1")) {

                return;

            }
            
            Vector3 targetPosition=ARENA_CENTER;

            try {

                targetPosition=JsonConvert.DeserializeObject<Vector3>(@event["TargetPosition"]);

            } catch(Exception e) {
                
                accessory.Log.Error("TargetPosition deserialization failed.");

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(8);
            currentProperties.Position=targetPosition;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.DestoryAt=6250;
            
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,currentProperties);

        }
        
        [ScriptMethod(name:"迦楼罗 中高压 (范围)",
            eventType:EventTypeEnum.Tether,
            eventCondition:["Id:0004"])]
    
        public void 迦楼罗_中高压_范围(Event @event,ScriptAccessory accessory) {

            if(majorPhase!=1&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=5&&!skipPhaseChecks) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }
            
            if(!convertObjectIdToDecimal(@event["TargetId"], out var targetId)) {
                
                return;
                
            }

            lock(phase1_mesohighDrawingCounter) {
                
                int lastDrawing=phase1_mesohighDrawingCounter.GetOrAdd(sourceId,0);
            
                accessory.Method.RemoveDraw($"迦楼罗_中高压_范围_{sourceId}_{lastDrawing}");

                ++lastDrawing;
                phase1_mesohighDrawingCounter[sourceId]=lastDrawing;
            
                var currentProperties=accessory.Data.GetDefaultDrawProperties();

                currentProperties.Name=$"迦楼罗_中高压_范围_{sourceId}_{lastDrawing}";
                currentProperties.Scale=new(3);
                currentProperties.Owner=targetId;
                currentProperties.Color=accessory.Data.DefaultDangerColor;
                currentProperties.DestoryAt=MAXIMUM_DURATION;

                accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,currentProperties);
                
            }
        
        }
        
        [ScriptMethod(name:"迦楼罗 中高压 (范围清除)",
            eventType:EventTypeEnum.ActionEffect,
            eventCondition:["ActionId:11081"],
            suppress:COMMON_INTERVAL,
            userControl:false)]
    
        public void 迦楼罗_中高压_范围清除(Event @event,ScriptAccessory accessory) {

            if(majorPhase!=1&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=5&&phase!=6&&!skipPhaseChecks) {

                return;

            }
            
            accessory.Method.RemoveDraw(@"^迦楼罗_中高压_范围_.*$");
            
            phase1_mesohighDrawingCounter.Clear();
        
        }
        
        [ScriptMethod(name:"迦楼罗 中高压 (指路,ST与D3)",
            eventType:EventTypeEnum.Tether,
            eventCondition:["Id:0004"],
            suppress:5000+COMMON_INTERVAL)]
    
        public void 迦楼罗_中高压_指路_ST与D3(Event @event,ScriptAccessory accessory) {

            if(majorPhase!=1&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=5&&!skipPhaseChecks) {

                return;

            }
            
            int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            
            if(!isLegalPartyIndex(myIndex)) {

                return;

            }

            if(myIndex!=1&&myIndex!=7) {

                return;

            }

            Vector3 myPosition=ARENA_CENTER;

            if(myIndex==1) {

                myPosition=new Vector3(107f,0,100);

            }

            if(myIndex==7) {
                
                myPosition=new Vector3(93f,0,100);
                
            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();
            
            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetPosition=myPosition;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=5000;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);

        }
        
        [ScriptMethod(name:"迦楼罗 最后一次邪轮旋风 (范围)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11086"])]

        public void 迦楼罗_最后一次邪轮旋风_范围(Event @event,ScriptAccessory accessory) {

            if(majorPhase!=1&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=6&&!skipPhaseChecks) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            if(garudaHasWoken) {
                
                currentProperties=accessory.Data.GetDefaultDrawProperties();
                
                currentProperties.Scale=new(20);
                currentProperties.InnerScale=new(8.5f);
                currentProperties.Radian=float.Pi*2;
                currentProperties.Owner=sourceId;
                currentProperties.Color=accessory.Data.DefaultDangerColor;
                currentProperties.Delay=3000;
                currentProperties.DestoryAt=2250;
        
                accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Donut,currentProperties);
                
                currentProperties=accessory.Data.GetDefaultDrawProperties();

                currentProperties.Scale=new(12);
                currentProperties.Owner=sourceId;
                currentProperties.TargetResolvePattern=PositionResolvePatternEnum.OwnerEnmityOrder;
                currentProperties.TargetOrderIndex=1;
                currentProperties.Color=accessory.Data.DefaultSafeColor;
                currentProperties.Delay=5250;
                currentProperties.DestoryAt=3500;
        
                accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Fan,currentProperties);
                
            }

            else {
                
                currentProperties=accessory.Data.GetDefaultDrawProperties();

                currentProperties.Scale=new(12);
                currentProperties.Owner=sourceId;
                currentProperties.TargetResolvePattern=PositionResolvePatternEnum.OwnerEnmityOrder;
                currentProperties.TargetOrderIndex=1;
                currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(1);
                currentProperties.Delay=3000;
                currentProperties.DestoryAt=3500;
        
                accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Fan,currentProperties);
                
            }

        }
        
        #endregion
        
        #region Ifrit
        
        [ScriptMethod(name:"伊弗利特 第一次深红旋风 (阶段控制与数据获取)",
            eventType:EventTypeEnum.PlayActionTimeline,
            eventCondition:["SourceDataId:8730"],
            userControl:false)]

        public void 伊弗利特_第一次深红旋风_阶段控制与数据获取(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=1&&!skipPhaseChecks) {

                return;

            }
            
            if(!string.Equals(@event["Id"],"7747")) {

                return;

            }

            majorPhase=2;
            phase=1;

            if(!preserveDrawingsWhileSwitchingPhase) {
                
                accessory.Method.RemoveDraw("^(?!伊弗利特_第一次深红旋风_范围$).*$");
                
            }
            
            if(phase2_enableNailOrderAssistance) {

                accessory.Method.MarkClear();
                
            }

            phase2_firstCrimsonCycloneSemaphore.Set();
            
            Vector3 sourcePosition=ARENA_CENTER;

            try {

                sourcePosition=JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);

            } catch(Exception e) {
                
                accessory.Log.Error("SourcePosition deserialization failed.");

                return;

            }
            
            int discretizedPosition=discretizePosition(sourcePosition,ARENA_CENTER,4);
            
            phase2_initialSafeZone[discretizedPosition]=false;
            phase2_initialSafeZone[(discretizedPosition+2)%4]=false;
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }

            phase2_ifritId=sourceId;

            if(enableDebugLogging) {
                
                accessory.Log.Debug($"majorPhase={majorPhase}\nphase={phase}\nphase2_initialSafeZone[{discretizedPosition}]=false\nphase2_initialSafeZone[{(discretizedPosition+2)%4}]=false\nphase2_ifritId={phase2_ifritId}");
                
            }

        }
        
        [ScriptMethod(name:"伊弗利特 第一次深红旋风 (范围)",
            eventType:EventTypeEnum.PlayActionTimeline,
            eventCondition:["SourceDataId:8730"])]

        public void 伊弗利特_第一次深红旋风_范围(Event @event,ScriptAccessory accessory) {
            
            if(!string.Equals(@event["Id"],"7747")) {

                return;

            }
            
            Vector3 sourcePosition=ARENA_CENTER;

            try {

                sourcePosition=JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);

            } catch(Exception e) {
                
                accessory.Log.Error("SourcePosition deserialization failed.");

                return;

            }
            
            bool signalled=phase2_firstCrimsonCycloneSemaphore.WaitOne(COMMON_INTERVAL);

            if(!signalled) {

                return;

            }
            
            if(majorPhase!=2&&!skipPhaseChecks) {

                return;

            }

            if(phase!=1&&!skipPhaseChecks) {

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Name="伊弗利特_第一次深红旋风_范围";
            currentProperties.Scale=new(18,44);
            currentProperties.Position=sourcePosition;
            currentProperties.TargetPosition=ARENA_CENTER;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.DestoryAt=5125;
        
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Rect,currentProperties);

        }
        
        [ScriptMethod(name:"伊弗利特 光辉炎柱 (数据获取)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11105"],
            userControl:false)]

        public void 伊弗利特_光辉炎柱_数据获取(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=2&&!skipPhaseChecks) {

                return;

            }

            if(phase!=1&&!skipPhaseChecks) {

                return;

            }

            if(phase2_radiantPlumeCounter>=10) {

                return;

            }
            
            Vector3 effectPosition=ARENA_CENTER;

            try {

                effectPosition=JsonConvert.DeserializeObject<Vector3>(@event["EffectPosition"]);

            } catch(Exception e) {
                
                accessory.Log.Error("EffectPosition deserialization failed.");

                return;

            }

            for(int i=0;i<4;++i) {

                if(Vector3.Distance(effectPosition,rotatePosition(new Vector3(100,0,82),ARENA_CENTER,Math.PI/2*i))<0.1) {

                    phase2_initialSafeZone[i]=false;

                    if(enableDebugLogging) {
                        
                        accessory.Log.Debug($"phase2_initialSafeZone[{i}]=false");
                        
                    }

                }
                
            }

            lock(phase2_radiantPlumeSemaphore) {

                Interlocked.Increment(ref phase2_radiantPlumeCounter);

                if(phase2_radiantPlumeCounter==10) {

                    phase2_radiantPlumeSemaphore.Set();

                }

            }
                
        }
        
        [ScriptMethod(name:"伊弗利特 光辉炎柱 (指路)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11105"],
            suppress:COMMON_INTERVAL)]

        public void 伊弗利特_光辉炎柱_指路(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=2&&!skipPhaseChecks) {

                return;

            }

            if(phase!=1&&!skipPhaseChecks) {

                return;

            }
            
            bool signalled=phase2_radiantPlumeSemaphore.WaitOne(COMMON_INTERVAL);

            if(!signalled) {

                return;

            }

            int discretizedSafeZone=Array.IndexOf(phase2_initialSafeZone,true);

            if(enableDebugLogging) {
                
                accessory.Log.Debug($"discretizedSafeZone={discretizedSafeZone}");
                
            }
            
            if(discretizedSafeZone<0||discretizedSafeZone>3) {

                return;

            }

            Vector3 myPosition=rotatePosition(new Vector3(100,0,81.5f),ARENA_CENTER,Math.PI/2*discretizedSafeZone);
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();
            
            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetPosition=myPosition;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=4000;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);

        }
        
        [ScriptMethod(name:"伊弗利特 火神爆裂 (击退指示)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11102"])]

        public void 伊弗利特_火神爆裂_击退指示(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=2&&!skipPhaseChecks) {

                return;

            }

            if(phase!=1&&!skipPhaseChecks) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(2);
            currentProperties.Owner=sourceId;
            currentProperties.TargetObject=accessory.Data.Me;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=colourOfDirectionIndicators.V4.WithW(1);
            currentProperties.Delay=3000;
            currentProperties.DestoryAt=8125;
                
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
                
            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(2,15);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetObject=sourceId;
            currentProperties.Rotation=float.Pi;
            currentProperties.Color=colourOfDirectionIndicators.V4.WithW(1);
            currentProperties.Delay=3000;
            currentProperties.DestoryAt=8125;
                
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);

        }
        
        [ScriptMethod(name:"伊弗利特 火神爆裂 (阶段控制)",
            eventType:EventTypeEnum.ActionEffect,
            eventCondition:["ActionId:11095"],
            suppress:COMMON_INTERVAL,
            userControl:false)]

        public void 伊弗利特_火神爆裂_阶段控制(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=2&&!skipPhaseChecks) {

                return;

            }

            if(phase!=1&&!skipPhaseChecks) {

                return;

            }

            phase=2;

            phase2_firstIncinerateSemaphore.Set();
            
            if(enableDebugLogging) {
                
                accessory.Log.Debug($"majorPhase={majorPhase}\nphase={phase}");
                
            }

        }
        
        [ScriptMethod(name:"伊弗利特 第一次烈焰焚烧 (范围)",
            eventType:EventTypeEnum.ActionEffect,
            eventCondition:["ActionId:11095"],
            suppress:COMMON_INTERVAL)]

        public void 伊弗利特_第一次烈焰焚烧_范围(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=2&&!skipPhaseChecks) {

                return;

            }
            
            bool signalled=phase2_firstIncinerateSemaphore.WaitOne(COMMON_INTERVAL);

            if(!signalled) {

                return;

            }

            if(phase!=2&&!skipPhaseChecks) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(15);
            currentProperties.Radian=float.Pi/3*2;
            currentProperties.Owner=sourceId;
            currentProperties.TargetResolvePattern=PositionResolvePatternEnum.OwnerEnmityOrder;
            currentProperties.TargetOrderIndex=1;
            currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(1);
            currentProperties.DestoryAt=10125;
        
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Fan,currentProperties);

        }
        
        [ScriptMethod(name:"伊弗利特 火狱之楔 (数据获取)",
            eventType:EventTypeEnum.AddCombatant,
            eventCondition:["DataId:8731"],
            userControl:false)]

        public void 伊弗利特_火狱之楔_数据获取(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=2&&!skipPhaseChecks) {

                return;

            }

            if(phase!=2&&!skipPhaseChecks) {

                return;

            }

            if(phase2_infernalNailCounter>=4) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
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

            lock(phase2_infernalNailDeployed) {

                phase2_infernalNailId[discretizedPosition]=sourceId;
                phase2_infernalNailDeployed[discretizedPosition]=true;

                Interlocked.Increment(ref phase2_infernalNailCounter);

                if(phase2_infernalNailCounter==4) {

                    int theFourthNail=0;

                    while(theFourthNail<8) {

                        int theNextNail=(theFourthNail+1)%8;

                        if(phase2_infernalNailDeployed[theFourthNail]&&phase2_infernalNailDeployed[theNextNail]) {

                            break;

                        }

                        else {
                            
                            ++theFourthNail;

                        }

                    }

                    if(theFourthNail>=8) {

                        return;

                    }
                    
                    phase2_infernalNail[3]=theFourthNail;
                    phase2_infernalNail[2]=(theFourthNail+1)%8;
                    phase2_infernalNail[1]=(theFourthNail+6)%8;
                    phase2_infernalNail[0]=(theFourthNail+3)%8;

                    phase2_temporaryRotation=Math.PI/4*(0.5d+theFourthNail);

                    phase2_infernalNailSemaphore1.Set();
                    phase2_infernalNailSemaphore2.Set();
                    phase2_infernalNailSemaphore3.Set();
                    phase2_infernalNailSemaphore4.Set();

                    if(enableDebugLogging) {
                        
                        accessory.Log.Debug($"""
                                             phase2_infernalNailDeployed:{string.Join(",",phase2_infernalNailDeployed)}
                                             phase2_infernalNail:{string.Join(",",phase2_infernalNail)}
                                             phase2_temporaryRotation={phase2_temporaryRotation}
                                             """);
                        
                    }

                }

            }

        }
        
        // [ScriptMethod(name:"伊弗利特 火狱之楔 (目标指示,仅DPS)",
        //     eventType:EventTypeEnum.AddCombatant,
        //     eventCondition:["DataId:8731"],
        //     suppress:COMMON_INTERVAL)]

        // public void 伊弗利特_火狱之楔_目标指示_仅DPS(Event @event,ScriptAccessory accessory) {
            
        //     if(majorPhase!=2&&!skipPhaseChecks) {

        //         return;

        //     }

        //     if(phase!=2&&!skipPhaseChecks) {

        //         return;

        //     }

        //     int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            
        //     if(!isLegalPartyIndex(myIndex)) {

        //         return;

        //     }

        //     if(!isDps(myIndex)) {

        //         return;

        //     }
            
        //     bool signalled=phase2_infernalNailSemaphore1.WaitOne(COMMON_INTERVAL);
            
        //     if(!signalled) {

        //         return;

        //     }

        //     int myNail=myIndex switch {
                
        //         4 => phase2_infernalNail[0],
        //         5 => phase2_infernalNail[1],
        //         6 => phase2_infernalNail[2],
        //         7 => phase2_infernalNail[3],
        //         _ => -1
                
        //     };

        //     if(myNail==-1) {

        //         return;

        //     }

        //     ulong idOfMyNail=phase2_infernalNailId[myNail];
            
        //     var currentProperties=accessory.Data.GetDefaultDrawProperties();

        //     currentProperties.Name=$"伊弗利特_火狱之楔_目标指示_仅DPS_{idOfMyNail}";
        //     currentProperties.Scale=new(0.25f);
        //     currentProperties.Owner=accessory.Data.Me;
        //     currentProperties.TargetObject=idOfMyNail;
        //     currentProperties.ScaleMode|=ScaleMode.YByDistance;
        //     currentProperties.Color=colourOfDirectionIndicators.V4.WithW(1);
        //     currentProperties.DestoryAt=MAXIMUM_DURATION;
            
        //     accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Rect,currentProperties);

        // }
        
        // [ScriptMethod(name:"伊弗利特 火狱之楔 (目标指示清除)",
        //     eventType:EventTypeEnum.ActionEffect,
        //     eventCondition:["ActionId:11096"],
        //     userControl:false)]

        // public void 伊弗利特_火狱之楔_目标指示清除(Event @event,ScriptAccessory accessory) {
            
        //     if(majorPhase!=2&&!skipPhaseChecks) {

        //         return;

        //     }
            
        //     if(!string.Equals(@event["TargetIndex"],"1")) {

        //         return;

        //     }

        //     if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
        //         return;
                
        //     }
            
        //     accessory.Method.RemoveDraw($"伊弗利特_火狱之楔_目标指示_仅DPS_{sourceId}");

        // }
        
        [ScriptMethod(name:"伊弗利特 火狱之楔 (引爆顺序获取)",
            eventType:EventTypeEnum.ActionEffect,
            eventCondition:["ActionId:11096"],
            userControl:false)]

        public void 伊弗利特_火狱之楔_引爆顺序获取(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=2&&!skipPhaseChecks) {

                return;

            }

            if(phase2_detonationOrder.Count>=4) {

                return;

            }
            
            if(!string.Equals(@event["TargetIndex"],"1")) {

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

            lock(phase2_detonationOrder) {
                
                phase2_detonationOrder.Add(discretizedPosition);

                if(phase2_detonationOrder.Count==4) {

                    if(enableDebugLogging) {
                        
                        accessory.Log.Debug($"phase2_detonationOrder:{string.Join(",",phase2_detonationOrder)}");
                        
                    }
                    
                }
                
            }

        }
        
        [ScriptMethod(name:"伊弗利特 第一次地火喷发 (指路,远程DPS与MT)",
            eventType:EventTypeEnum.AddCombatant,
            eventCondition:["DataId:8731"],
            suppress:COMMON_INTERVAL)]

        public void 伊弗利特_第一次地火喷发_指路_远程DPS与MT(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=2&&!skipPhaseChecks) {

                return;

            }

            if(phase!=2&&!skipPhaseChecks) {

                return;

            }

            int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            
            if(!isLegalPartyIndex(myIndex)) {

                return;

            }

            if(!isRangedDps(myIndex)&&myIndex!=0) {

                return;

            }
            
            bool signalled=phase2_infernalNailSemaphore2.WaitOne(COMMON_INTERVAL);
            
            if(!signalled) {

                return;

            }

            Vector3 myPosition=myIndex switch {
                
                0 => new Vector3(100,0,88.5f),
                7 => new Vector3(113.643f,0,108.358f),
                6 => new Vector3(86.357f,0,108.358f),
                _ => ARENA_CENTER
                
            };
            // Geometric Construction:
            // https://www.geogebra.org/calculator/bs4sbfem

            myPosition=rotatePosition(myPosition,ARENA_CENTER,phase2_temporaryRotation);
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetPosition=myPosition;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;

            if(isRangedDps(myIndex)) {
                
                currentProperties.DestoryAt=11250;
                
            }
            
            if(myIndex==0) {
                
                currentProperties.DestoryAt=17250;
                
            }
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);

            if(isRangedDps(myIndex)) {
                
                currentProperties=accessory.Data.GetDefaultDrawProperties();
                
                currentProperties.Scale=new(17.5f);
                currentProperties.InnerScale=new(14.5f);
                currentProperties.Radian=((float)(convertDegreesToRadians(121.492)));
                currentProperties.Position=ARENA_CENTER;
                currentProperties.TargetPosition=myPosition;
                currentProperties.Color=accessory.Data.DefaultSafeColor;
                currentProperties.DestoryAt=17250;

                if(myIndex==7) {

                    currentProperties.Rotation=((float)(convertDegreesToRadians(121.492/2)));

                }
                
                if(myIndex==6) {
                    
                    currentProperties.Rotation=-((float)(convertDegreesToRadians(121.492/2)));
                    
                }
                
                accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Donut,currentProperties);
                
            }

        }
        
        [ScriptMethod(name:"伊弗利特 火狱之楔 (指北针)",
            eventType:EventTypeEnum.AddCombatant,
            eventCondition:["DataId:8731"],
            suppress:COMMON_INTERVAL)]

        public void 伊弗利特_火狱之楔_指北针(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=2&&!skipPhaseChecks) {

                return;

            }

            if(phase!=2&&!skipPhaseChecks) {

                return;

            }
            
            bool signalled=phase2_infernalNailSemaphore3.WaitOne(COMMON_INTERVAL);
            
            if(!signalled) {

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();
            
            currentProperties.Scale=new(2,14);
            currentProperties.Position=rotatePosition(new Vector3(100,0,107),ARENA_CENTER,phase2_temporaryRotation);
            currentProperties.TargetPosition=rotatePosition(new Vector3(100,0,93),ARENA_CENTER,phase2_temporaryRotation);
            currentProperties.Color=phase2_colourOfNorthIndicator.V4.WithW(1);
            currentProperties.DestoryAt=17250;
        
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Arrow,currentProperties);

        }
        
        [ScriptMethod(name:"伊弗利特 火狱之楔 (标记顺序)",
            eventType:EventTypeEnum.AddCombatant,
            eventCondition:["DataId:8731"],
            suppress:COMMON_INTERVAL)]

        public void 伊弗利特_火狱之楔_标记顺序(Event @event,ScriptAccessory accessory) {

            if(!phase2_enableNailOrderAssistance) {

                return;

            }
            
            if(majorPhase!=2&&!skipPhaseChecks) {

                return;

            }

            if(phase!=2&&!skipPhaseChecks) {

                return;

            }
            
            bool signalled=phase2_infernalNailSemaphore4.WaitOne(COMMON_INTERVAL);
            
            if(!signalled) {

                return;

            }
            
            accessory.Method.Mark(((uint)phase2_infernalNailId[phase2_infernalNail[0]]),MarkType.Attack1);
            accessory.Method.Mark(((uint)phase2_infernalNailId[phase2_infernalNail[1]]),MarkType.Attack2);
            accessory.Method.Mark(((uint)phase2_infernalNailId[phase2_infernalNail[2]]),MarkType.Attack3);
            accessory.Method.Mark(((uint)phase2_infernalNailId[phase2_infernalNail[3]]),MarkType.Attack4);

            if(enableDebugLogging) {

                accessory.Log.Debug($"""
                                     Mark {phase2_infernalNailId[phase2_infernalNail[0]]} as {MarkType.Attack1}
                                     Mark {phase2_infernalNailId[phase2_infernalNail[1]]} as {MarkType.Attack2}
                                     Mark {phase2_infernalNailId[phase2_infernalNail[2]]} as {MarkType.Attack3}
                                     Mark {phase2_infernalNailId[phase2_infernalNail[3]]} as {MarkType.Attack4}
                                     """);

            }

        }
        
        [ScriptMethod(name:"伊弗利特 火狱之锁 (连线指示)",
            eventType:EventTypeEnum.Tether,
            eventCondition:["Id:0009"])]

        public void 伊弗利特_火狱之锁_连线指示(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=2&&!skipPhaseChecks) {

                return;

            }

            if(phase!=2&&!skipPhaseChecks) {

                return;

            }
            
            accessory.Method.RemoveDraw($"伊弗利特_火狱之锁_连线指示_{phase2_infernalFetterDrawingCounter}");
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }
            
            if(!convertObjectIdToDecimal(@event["TargetId"], out var targetId)) {
                
                return;
                
            }

            if(accessory.Data.Me!=sourceId&&accessory.Data.Me!=targetId) {

                return;

            }
            
            int sourceIndex=accessory.Data.PartyList.IndexOf(((uint)sourceId));
            
            if(!isLegalPartyIndex(sourceIndex)) {

                return;

            }
            
            int targetIndex=accessory.Data.PartyList.IndexOf(((uint)targetId));
            
            if(!isLegalPartyIndex(targetIndex)) {

                return;
            }

            ulong sourceInDrawing=sourceId,targetInDrawing=targetId;
            bool anomalousTether=true;

            if(isTank(sourceIndex)&&(isDps(targetIndex)||isHealer(targetIndex))) {

                anomalousTether=false;

                sourceInDrawing=sourceId;
                targetInDrawing=targetId;

            }
            
            if(isTank(targetIndex)&&(isDps(sourceIndex)||isHealer(sourceIndex))) {

                anomalousTether=false;

                sourceInDrawing=targetId;
                targetInDrawing=sourceId;

            }

            if(anomalousTether) {

                if(accessory.Data.Me==sourceId) {
                    
                    sourceInDrawing=sourceId;
                    targetInDrawing=targetId;

                }
                
                if(accessory.Data.Me==targetId) {
                    
                    sourceInDrawing=targetId;
                    targetInDrawing=sourceId;

                }
                
            }
            
            Interlocked.Increment(ref phase2_infernalFetterDrawingCounter);
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Name=$"伊弗利特_火狱之锁_连线指示_{phase2_infernalFetterDrawingCounter}";
            currentProperties.Scale=new(2);
            currentProperties.Owner=sourceInDrawing;
            currentProperties.TargetObject=targetInDrawing;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=colourOfDirectionIndicators.V4.WithW(1);
            currentProperties.DestoryAt=21000;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
            
        }
        
        [ScriptMethod(name:"伊弗利特 第一次灼热咆哮 (指路)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11099"])]

        public void 伊弗利特_第一次灼热咆哮_指路(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=2&&!skipPhaseChecks) {

                return;

            }

            if(phase!=2&&!skipPhaseChecks) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["TargetId"], out var targetId)) {
                
                return;
                
            }

            if(accessory.Data.Me!=targetId) {

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();
            
            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetPosition=rotatePosition(new Vector3(100,0,118.5f),ARENA_CENTER,phase2_temporaryRotation);
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=20750;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
            
        }
        
        [ScriptMethod(name:"伊弗利特 地狱之火炎 (阶段控制)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11102"],
            userControl:false)]

        public void 伊弗利特_地狱之火炎_阶段控制(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=2&&!skipPhaseChecks) {

                return;

            }

            if(phase!=2&&!skipPhaseChecks) {

                return;

            }
            
            phase=3;
            
            int temporaryRotation2=-1;

            if(phase2_infernalNail[2]%2==1) {

                temporaryRotation2=phase2_infernalNail[2];

            }
            
            if(phase2_infernalNail[3]%2==1) {

                temporaryRotation2=phase2_infernalNail[3];

            }

            if(temporaryRotation2==-1) {

                return;

            }
            
            phase2_temporaryRotation2=Math.PI/4*temporaryRotation2;

            phase2_hellfireSemaphore1.Set();
            phase2_hellfireSemaphore2.Set();
            phase2_hellfireSemaphore3.Set();
                    
            if(enableDebugLogging) {
                
                accessory.Log.Debug($"majorPhase={majorPhase}\nphase={phase}\nphase2_temporaryRotation2={phase2_temporaryRotation2}");
                
            }
            
        }
        
        [ScriptMethod(name:"伊弗利特 第二次地狱之火炎 (指路,除远程DPS)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11102"])]

        public void 伊弗利特_第二次地狱之火炎_指路(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=2&&!skipPhaseChecks) {

                return;

            }
            
            bool signalled=phase2_hellfireSemaphore1.WaitOne(COMMON_INTERVAL);
            
            if(!signalled) {

                return;

            }

            if(phase!=3&&!skipPhaseChecks) {

                return;

            }
            
            int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            
            if(!isLegalPartyIndex(myIndex)) {

                return;

            }

            if(isRangedDps(myIndex)) {

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            // 指向本轮的南侧边缘(原版为北侧边缘81.5)。
            currentProperties.TargetPosition=rotatePosition(new Vector3(100,0,118.5f),ARENA_CENTER,Math.PI+phase2_temporaryRotation2);
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.DestoryAt=9250;
            
            if(isHealer(myIndex)) {
                    
                currentProperties.Color=colourOfDirectionIndicators.V4.WithW(1);
                    
            }

            else {
                    
                currentProperties.Color=accessory.Data.DefaultSafeColor;
                    
            }
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);

        }
        
        [ScriptMethod(name:"伊弗利特 第二次地狱之火炎 (指北针)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11102"])]

        public void 伊弗利特_第二次地狱之火炎_指北针(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=2&&!skipPhaseChecks) {

                return;

            }
            
            bool signalled=phase2_hellfireSemaphore3.WaitOne(COMMON_INTERVAL);
            
            if(!signalled) {

                return;

            }

            if(phase!=3&&!skipPhaseChecks) {

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Name="伊弗利特_第二次地狱之火炎_指北针";
            currentProperties.Scale=new(2,14);
            currentProperties.Position=rotatePosition(new Vector3(100,0,107),ARENA_CENTER,Math.PI+phase2_temporaryRotation2);
            currentProperties.TargetPosition=rotatePosition(new Vector3(100,0,93),ARENA_CENTER,Math.PI+phase2_temporaryRotation2);
            currentProperties.Color=phase2_colourOfNorthIndicator.V4.WithW(1);
            currentProperties.DestoryAt=MAXIMUM_DURATION;
        
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Arrow,currentProperties);

        }
        
        [ScriptMethod(name:"伊弗利特 第二次灼热咆哮 (指路,除远程DPS)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11099"])]

        public void 伊弗利特_第二次灼热咆哮_指路(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=2&&!skipPhaseChecks) {

                return;

            }

            if(phase!=3&&!skipPhaseChecks) {

                return;

            }
            
            int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            
            if(!isLegalPartyIndex(myIndex)) {

                return;

            }

            if(isRangedDps(myIndex)) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["TargetId"], out var targetId)) {
                
                return;
                
            }

            bool targeted=false;

            if(targetId==accessory.Data.Me) {

                targeted=true;

            }

            Vector3 myPosition=ARENA_CENTER;

            // 人群在南侧边缘:点名者去右(东)侧边缘,未点名留南侧。
            if(targeted) {

                myPosition=rotatePosition(new Vector3(118.5f,0,100),ARENA_CENTER,Math.PI+phase2_temporaryRotation2);

            }

            else {

                myPosition=rotatePosition(new Vector3(100,0,81.5f),ARENA_CENTER,phase2_temporaryRotation2);

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetPosition=myPosition;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=15125;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);

        }
        
        [ScriptMethod(name:"伊弗利特 第二次地火喷发 (指路,仅远程DPS)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11102"])]

        public void 伊弗利特_第二次地火喷发_指路_仅远程DPS(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=2&&!skipPhaseChecks) {

                return;

            }
            
            bool signalled=phase2_hellfireSemaphore2.WaitOne(COMMON_INTERVAL);
            
            if(!signalled) {

                return;

            }

            if(phase!=3&&!skipPhaseChecks) {

                return;

            }

            int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            
            if(!isLegalPartyIndex(myIndex)) {

                return;

            }

            if(!isRangedDps(myIndex)) {

                return;

            }

            // D3、D4统一站本轮西北侧(半径18.5,北偏西54.7°),再沿西侧弧线回到南侧人群。
            // 原版为D3右后(115.099,110.689)/D4左后(84.901,110.689),人群在北。
            Vector3 myPosition=new Vector3(84.901f,0,89.311f);
            // Geometric Construction:
            // https://www.geogebra.org/calculator/bs4sbfem

            myPosition=rotatePosition(myPosition,ARENA_CENTER,Math.PI+phase2_temporaryRotation2);
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetPosition=myPosition;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=14375;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);

            currentProperties=accessory.Data.GetDefaultDrawProperties();
                
            currentProperties.Scale=new(19.5f);
            currentProperties.InnerScale=new(18.5f);
            currentProperties.Radian=((float)(convertDegreesToRadians(125.296)));
            currentProperties.Position=ARENA_CENTER;
            currentProperties.TargetPosition=myPosition;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=24375;

            // 弧带从西北站位经西侧到南侧人群,跨度与原版一致125.296°。
            currentProperties.Rotation=((float)(convertDegreesToRadians(125.296/2)));

            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Donut,currentProperties);

        }
        
        [ScriptMethod(name:"伊弗利特 第二次深红旋风 (范围)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11099"])]

        public void 伊弗利特_第二次深红旋风_范围(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=2&&!skipPhaseChecks) {

                return;

            }

            if(phase!=3&&!skipPhaseChecks) {

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(18,44);
            currentProperties.Position=ARENA_CENTER;
            currentProperties.TargetPosition=new Vector3(ARENA_CENTER.X,ARENA_CENTER.Y,ARENA_CENTER.Z-1);
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.DestoryAt=15125;
        
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Straight,currentProperties);
            
            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(18,44);
            currentProperties.Position=ARENA_CENTER;
            currentProperties.TargetPosition=new Vector3(ARENA_CENTER.X+1,ARENA_CENTER.Y,ARENA_CENTER.Z);
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.DestoryAt=15125;
        
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Straight,currentProperties);

        }
        
        [ScriptMethod(name:"伊弗利特 第二次深红旋风 (阶段控制)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11103"],
            suppress:COMMON_INTERVAL,
            userControl:false)]

        public void 伊弗利特_第二次深红旋风_阶段控制(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=2&&!skipPhaseChecks) {

                return;

            }

            if(phase!=3&&!skipPhaseChecks) {

                return;

            }
            
            phase=4;
                    
            if(enableDebugLogging) {
                
                accessory.Log.Debug($"majorPhase={majorPhase}\nphase={phase}");
                
            }

        }
        
        [ScriptMethod(name:"伊弗利特 第三次灼热咆哮 (指示)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11099"])]

        public void 伊弗利特_第三次灼热咆哮_指示(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=2&&!skipPhaseChecks) {

                return;

            }

            if(phase!=4&&!skipPhaseChecks) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["TargetId"], out var targetId)) {
                
                return;
                
            }

            if(targetId!=accessory.Data.Me) {

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(17);
            currentProperties.Position=rotatePosition(new Vector3(100,0,81.5f),ARENA_CENTER,Math.PI+phase2_temporaryRotation2);
            currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(1);
            currentProperties.DestoryAt=4000;
            
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,currentProperties);

        }
        
        [ScriptMethod(name:"伊弗利特 烈焰碎击 (阶段控制)",
            eventType:EventTypeEnum.ActionEffect,
            eventCondition:["ActionId:11101"],
            suppress:COMMON_INTERVAL,
            userControl:false)]

        public void 伊弗利特_烈焰碎击_阶段控制(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=2&&!skipPhaseChecks) {

                return;

            }

            if(phase!=4&&!skipPhaseChecks) {

                return;

            }

            accessory.Method.RemoveDraw("伊弗利特_第二次地狱之火炎_指北针");
            
            phase=5;
                    
            if(enableDebugLogging) {
                
                accessory.Log.Debug($"majorPhase={majorPhase}\nphase={phase}");
                
            }

        }
        
        [ScriptMethod(name:"伊弗利特 第三次深红旋风 (范围)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11103"])]

        public void 伊弗利特_第三次深红旋风_范围(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=2&&!skipPhaseChecks) {

                return;

            }

            if(phase!=5&&!skipPhaseChecks) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }

            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(18,44);
            currentProperties.Owner=sourceId;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.DestoryAt=3000;
        
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Rect,currentProperties);

            if(sourceId==phase2_ifritId) {

                if(ifritHasWoken) {
                    
                    Vector3 sourcePosition=ARENA_CENTER;

                    try {

                        sourcePosition=JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);

                    } catch(Exception e) {
                
                        accessory.Log.Error("SourcePosition deserialization failed.");

                        return;

                    }
            
                    int discretizedPosition=discretizePosition(sourcePosition,ARENA_CENTER,8);

                    Vector3 targetPosition1=new Vector3(ARENA_CENTER.X,ARENA_CENTER.Y,ARENA_CENTER.Z-1);
                    Vector3 targetPosition2=new Vector3(ARENA_CENTER.X+1,ARENA_CENTER.Y,ARENA_CENTER.Z);

                    if(discretizedPosition%2==0) {

                        targetPosition1=rotatePosition(targetPosition1,ARENA_CENTER,Math.PI/4);
                        targetPosition2=rotatePosition(targetPosition2,ARENA_CENTER,Math.PI/4);

                    }
                    
                    currentProperties=accessory.Data.GetDefaultDrawProperties();

                    currentProperties.Scale=new(10,44);
                    currentProperties.Position=ARENA_CENTER;
                    currentProperties.TargetPosition=targetPosition1;
                    currentProperties.Color=accessory.Data.DefaultDangerColor;
                    currentProperties.Delay=2125;
                    currentProperties.DestoryAt=3000;
        
                    accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Straight,currentProperties);
            
                    currentProperties=accessory.Data.GetDefaultDrawProperties();

                    currentProperties.Scale=new(10,44);
                    currentProperties.Position=ARENA_CENTER;
                    currentProperties.TargetPosition=targetPosition2;
                    currentProperties.Color=accessory.Data.DefaultDangerColor;
                    currentProperties.Delay=2125;
                    currentProperties.DestoryAt=3000;
        
                    accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Straight,currentProperties);
                    
                }
                
            }
            
        }
        
        [ScriptMethod(name:"伊弗利特 第三次深红旋风 (数据计算)",
            eventType:EventTypeEnum.PlayActionTimeline,
            eventCondition:["SourceDataId:8730"],
            userControl:false)]

        public void 伊弗利特_第三次深红旋风_数据计算(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=2&&!skipPhaseChecks) {

                return;

            }

            if(phase!=5&&!skipPhaseChecks) {

                return;

            }
            
            if(!string.Equals(@event["Id"],"7737")) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }

            if(sourceId!=phase2_ifritId) {

                return;

            }
            
            var sourceObject=accessory.Data.Objects.SearchById(sourceId);
            uint currentHp=1408009;

            if(sourceObject==null) {
                
                phase2_disableCrimsonCycloneGuidance=true;

                return;
                
            }

            else {
                
                if(sourceObject is not ICharacter sourceICharacter) {
                    
                    phase2_disableCrimsonCycloneGuidance=true;

                    return;
                    
                }

                else {

                    currentHp=sourceICharacter.CurrentHp;

                    if(currentHp<=1) {
                        
                        phase2_disableCrimsonCycloneGuidance=true;

                        return;

                    }

                }
                
            }
            
            if(phase2_detonationOrder.Count!=4) {
                
                phase2_disableCrimsonCycloneGuidance=true;
                
                phase2_thirdCrimsonCycloneSemaphore.Set();

                return;
                
            }
            
            phase2_readableDetonationOrder.Clear();

            for(int i=0;i<4;++i) {

                phase2_readableDetonationOrder.Add(Array.IndexOf(phase2_infernalNail,phase2_detonationOrder[i])+1);

                if(phase2_readableDetonationOrder.Last()<1||phase2_readableDetonationOrder.Last()>4) {

                    phase2_disableCrimsonCycloneGuidance=true;
                    
                    phase2_thirdCrimsonCycloneSemaphore.Set();

                    return;

                }

            }
            
            if(phase2_readableDetonationOrder.Count!=4) {
                
                phase2_disableCrimsonCycloneGuidance=true;
                
                phase2_thirdCrimsonCycloneSemaphore.Set();

                return;
                
            }

            if(phase2_readableDetonationOrder[0]==1
               &&
               phase2_readableDetonationOrder[1]==2
               &&
               phase2_readableDetonationOrder[2]==3
               &&
               phase2_readableDetonationOrder[3]==4) {
                
                phase2_disableCrimsonCycloneGuidance=false;
                phase2_discretizedInitialRotation=(phase2_infernalNail[0]+1)%8;
                phase2_clockwise=false;
                
            }

            else {
                
                if(phase2_readableDetonationOrder[0]==2
                   &&
                   phase2_readableDetonationOrder[1]==1
                   &&
                   phase2_readableDetonationOrder[2]==3
                   &&
                   phase2_readableDetonationOrder[3]==4) {
                    
                    phase2_disableCrimsonCycloneGuidance=false;
                    phase2_discretizedInitialRotation=(phase2_infernalNail[1]+7)%8;
                    phase2_clockwise=true;
                
                }

                else {
                    
                    phase2_disableCrimsonCycloneGuidance=true;
                    
                }
                
            }

            phase2_thirdCrimsonCycloneSemaphore.Set();

            if(enableDebugLogging) {
                
                accessory.Log.Debug($"""
                                     sourceICharacter.CurrentHp={currentHp}
                                     phase2_readableDetonationOrder:{string.Join(",",phase2_readableDetonationOrder)}
                                     phase2_disableCrimsonCycloneGuidance={phase2_disableCrimsonCycloneGuidance}
                                     phase2_initialDiscretizedRotation={phase2_discretizedInitialRotation}
                                     phase2_clockwise={phase2_clockwise}
                                     """);
                
            }

        }
        
        [ScriptMethod(name:"伊弗利特 第三次深红旋风 (起始位置指路)",
            eventType:EventTypeEnum.PlayActionTimeline,
            eventCondition:["SourceDataId:8730"])]

        public void 伊弗利特_第三次深红旋风_起始位置指路(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=2&&!skipPhaseChecks) {

                return;

            }

            if(phase!=5&&!skipPhaseChecks) {

                return;

            }
            
            if(!string.Equals(@event["Id"],"7737")) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }

            if(sourceId!=phase2_ifritId) {

                return;

            }
            
            bool signalled=phase2_thirdCrimsonCycloneSemaphore.WaitOne(COMMON_INTERVAL);
            
            if(!signalled) {

                return;

            }
            
            string prompt=String.Empty;

            if(phase2_disableCrimsonCycloneGuidance) {

                bool fatalError=false;

                if(phase2_readableDetonationOrder.Count!=4) {

                    fatalError=true;

                }

                else {

                    for(int i=0;i<phase2_readableDetonationOrder.Count;++i) {

                        if(phase2_readableDetonationOrder[i]<1||phase2_readableDetonationOrder[i]>4) {
                            
                            fatalError=true;

                            break;

                        }
                        
                    }
                    
                }

                if(fatalError) {

                    prompt="Fatal error while computing the detonation order.\nGuidance has been disabled.";

                }

                else {

                    prompt=$"Incorrect detonation order: {phase2_readableDetonationOrder[0]},{phase2_readableDetonationOrder[1]},{phase2_readableDetonationOrder[2]},{phase2_readableDetonationOrder[3]}.\nGuidance has been disabled.";

                }

            }

            else {

                Vector3 myPosition=rotatePosition(new Vector3(100,0,81.5f),ARENA_CENTER,Math.PI/4*phase2_discretizedInitialRotation);

                if(partyMembersWithSearingWind.Contains(accessory.Data.Me)) {
                    
                    myPosition=rotatePosition(myPosition,ARENA_CENTER,Math.PI);
                    
                }
                
                var currentProperties=accessory.Data.GetDefaultDrawProperties();

                currentProperties.Scale=new(2);
                currentProperties.Owner=accessory.Data.Me;
                currentProperties.TargetPosition=myPosition;
                currentProperties.ScaleMode|=ScaleMode.YByDistance;
                currentProperties.Color=accessory.Data.DefaultSafeColor;
                currentProperties.DestoryAt=7375;
            
                accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);

                if(!phase2_clockwise) {
                    
                    prompt=$"Correct detonation order: {phase2_readableDetonationOrder[0]},{phase2_readableDetonationOrder[1]},{phase2_readableDetonationOrder[2]},{phase2_readableDetonationOrder[3]}.\nMoving counterclockwise.";
                    
                }

                else {
                    
                    prompt=$"Nails 1 and 2 are in the wrong order: {phase2_readableDetonationOrder[0]},{phase2_readableDetonationOrder[1]},{phase2_readableDetonationOrder[2]},{phase2_readableDetonationOrder[3]}.\nGuidance adjusted; moving clockwise.";
                    
                }

            }
            
            if(enablePrompts) {
                    
                accessory.Method.TextInfo(prompt,7375,phase2_disableCrimsonCycloneGuidance);
                    
            }
                
            accessory.tts(prompt,enableVanillaTts,enableDailyRoutinesTts);

        }
        
        [ScriptMethod(name:"伊弗利特 第三次深红旋风 (路径指路)",
            eventType:EventTypeEnum.PlayActionTimeline,
            eventCondition:["SourceDataId:8730"])]

        public void 伊弗利特_第三次深红旋风_路径指路(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=2&&!skipPhaseChecks) {

                return;

            }

            if(phase!=5&&!skipPhaseChecks) {

                return;

            }
            
            if(!string.Equals(@event["Id"],"7747")) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }

            if(sourceId!=phase2_ifritId) {

                return;

            }
            
            Vector3 sourcePosition=ARENA_CENTER;

            try {

                sourcePosition=JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);

            } catch(Exception e) {
                
                accessory.Log.Error("SourcePosition deserialization failed.");

                return;

            }

            if(Vector3.Distance(sourcePosition,ARENA_CENTER)<18.5f) {

                return;

            }
            
            if(phase2_disableCrimsonCycloneGuidance) {

                return;

            }
            
            int discretizedPosition=discretizePosition(sourcePosition,ARENA_CENTER,8);

            float radius=float.Pi/4;

            if(discretizedPosition%2==phase2_discretizedInitialRotation%2) {

                radius=float.Pi/2;

            }
            
            Vector3 myPosition=rotatePosition(new Vector3(100,0,81.5f),ARENA_CENTER,Math.PI/4*phase2_discretizedInitialRotation);

            if(partyMembersWithSearingWind.Contains(accessory.Data.Me)) {
                    
                myPosition=rotatePosition(myPosition,ARENA_CENTER,Math.PI);
                    
            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();
                
            currentProperties.Scale=new(19.5f);
            currentProperties.InnerScale=new(18.5f);
            currentProperties.Radian=radius;
            currentProperties.Position=ARENA_CENTER;
            currentProperties.TargetPosition=myPosition;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=9250;

            if(!phase2_clockwise) {

                currentProperties.Rotation=radius/2;

            }
                
            else {
                    
                currentProperties.Rotation=-(radius/2);
                    
            }
                
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Donut,currentProperties);
            
            if(enableDebugLogging) {
                
                accessory.Log.Debug($"radius={radius}");
                
            }

        }
        
        [ScriptMethod(name:"伊弗利特 第二次烈焰焚烧 (范围)",
            eventType:EventTypeEnum.PlayActionTimeline,
            eventCondition:["SourceDataId:8730"])]

        public void 伊弗利特_第二次烈焰焚烧_范围(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=2&&!skipPhaseChecks) {

                return;

            }

            if(phase!=5&&!skipPhaseChecks) {

                return;

            }
            
            if(!string.Equals(@event["Id"],"7747")) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }

            if(sourceId!=phase2_ifritId) {

                return;

            }
            
            Vector3 sourcePosition=ARENA_CENTER;

            try {

                sourcePosition=JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);

            } catch(Exception e) {
                
                accessory.Log.Error("SourcePosition deserialization failed.");

                return;

            }

            if(Vector3.Distance(sourcePosition,ARENA_CENTER)>1) {

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(15);
            currentProperties.Radian=float.Pi/3*2;
            currentProperties.Owner=sourceId;
            currentProperties.TargetResolvePattern=PositionResolvePatternEnum.OwnerEnmityOrder;
            currentProperties.TargetOrderIndex=1;
            currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(1);
            currentProperties.DestoryAt=12500;
        
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Fan,currentProperties);

        }
        
        #endregion
        
        #region Titan
        
        [ScriptMethod(name:"泰坦 大地粉碎 (阶段控制)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11517"],
            userControl:false)]

        public void 泰坦_大地粉碎_阶段控制(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=2&&!skipPhaseChecks) {

                return;

            }
            
            majorPhase=3;
            phase=1;

            if(!preserveDrawingsWhileSwitchingPhase) {
                
                accessory.Method.RemoveDraw(".*");
                
            }
            
            if(phase2_enableNailOrderAssistance||phase3_enableRockThrowAssistance) {

                accessory.Method.MarkClear();
                
            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }

            phase3_titanId=sourceId;
            
            if(enableDebugLogging) {
                
                accessory.Log.Debug($"majorPhase={majorPhase}\nphase={phase}\nphase3_titanId={phase3_titanId}");
                
            }

        }
        
        [ScriptMethod(name:"泰坦 第一次碎岩山崩连击 (范围)",
            eventType:EventTypeEnum.ActionEffect,
            eventCondition:["ActionId:11517"],
            suppress:COMMON_INTERVAL)]

        public void 泰坦_第一次碎岩山崩连击_范围(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=3&&!skipPhaseChecks) {

                return;

            }

            if(phase!=1&&!skipPhaseChecks) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(10.5f);
            currentProperties.Owner=sourceId;
            currentProperties.Radian=float.Pi/2;
            currentProperties.TargetResolvePattern=PositionResolvePatternEnum.OwnerEnmityOrder;
            currentProperties.TargetOrderIndex=1;
            currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(1);
            currentProperties.DestoryAt=13625;
        
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Fan,currentProperties);
            
            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(15.5f);
            currentProperties.Owner=sourceId;
            currentProperties.Radian=float.Pi/2;
            currentProperties.TargetResolvePattern=PositionResolvePatternEnum.OwnerEnmityOrder;
            currentProperties.TargetOrderIndex=1;
            currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(1);
            currentProperties.Delay=13625;
            currentProperties.DestoryAt=3000;
        
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Fan,currentProperties);

        }
        
        [ScriptMethod(name:"泰坦 第二次大地粉碎 (泰坦位置与面向指示)",
            eventType:EventTypeEnum.PlayActionTimeline,
            eventCondition:["SourceDataId:8727"])]

        public void 泰坦_第二次大地粉碎_泰坦位置与面向指示(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=3&&!skipPhaseChecks) {

                return;

            }

            if(phase!=1&&!skipPhaseChecks) {

                return;

            }

            if(!string.Equals(@event["Id"],"7737")) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(4.5f);
            currentProperties.InnerScale=new(3.5f);
            currentProperties.Radian=float.Pi*2;
            currentProperties.Owner=sourceId;
            currentProperties.Color=colourOfDirectionIndicators.V4.WithW(1);
            currentProperties.DestoryAt=5000;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Donut,currentProperties);
            
            currentProperties=accessory.Data.GetDefaultDrawProperties();
            
            currentProperties.Scale=new(1,4);
            currentProperties.Owner=sourceId;
            currentProperties.Color=colourOfDirectionIndicators.V4.WithW(1);
            currentProperties.DestoryAt=5000;
        
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Arrow,currentProperties);

        }
        
        [ScriptMethod(name:"泰坦 第二次大地粉碎 (阶段控制)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11110"],
            userControl:false)]

        public void 泰坦_第二次大地粉碎_阶段控制(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=3&&!skipPhaseChecks) {

                return;

            }

            if(phase!=1&&!skipPhaseChecks) {

                return;

            }
            
            Vector3 targetPosition=ARENA_CENTER;

            try {

                targetPosition=JsonConvert.DeserializeObject<Vector3>(@event["TargetPosition"]);

            } catch(Exception e) {
                
                accessory.Log.Error("TargetPosition deserialization failed.");

                return;

            }
            
            phase3_discretizedLandingPosition=discretizePosition(targetPosition,ARENA_CENTER,4);

            phase=2;

            phase3_secondGeocrushSemaphore.Set();
            
            if(enableDebugLogging) {
                
                accessory.Log.Debug($"majorPhase={majorPhase}\nphase={phase}\nphase3_discretizedLandingPosition={phase3_discretizedLandingPosition}");
                
            }

        }
        
        [ScriptMethod(name:"泰坦 第二次大地粉碎 (指路)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11110"])]

        public void 泰坦_第二次大地粉碎_指路(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=3&&!skipPhaseChecks) {

                return;

            }

            bool signalled=phase3_secondGeocrushSemaphore.WaitOne(COMMON_INTERVAL);
            
            if(!signalled) {

                return;

            }
            
            if(phase!=2&&!skipPhaseChecks) {

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetPosition=rotatePosition(new Vector3(100,0,115),ARENA_CENTER,Math.PI/2*phase3_discretizedLandingPosition);
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=3000;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);

        }
        
        [ScriptMethod(name:"泰坦 爆破岩石 (数据获取)",
            eventType:EventTypeEnum.AddCombatant,
            eventCondition:["DataId:8728"],
            userControl:false)]

        public void 泰坦_爆破岩石_数据获取(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=3&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=2&&!skipPhaseChecks) {

                return;

            }

            if(phase3_boulderCounter>=5) {

                return;

            }
            
            Vector3 sourcePosition=ARENA_CENTER;

            try {

                sourcePosition=JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);

            } catch(Exception e) {
                
                accessory.Log.Error("SourcePosition deserialization failed.");

                return;

            }

            Vector3 relativePosition=rotatePosition(sourcePosition,ARENA_CENTER,Math.PI/2*(4-phase3_discretizedLandingPosition));

            lock(phase3_bombBoulderSemaphore) {

                if(relativePosition.X<100) {

                    Interlocked.Increment(ref phase3_bouldersOnLeft);

                }
                
                if(relativePosition.X>100) {

                    Interlocked.Increment(ref phase3_bouldersOnRight);

                }
                
                Interlocked.Increment(ref phase3_boulderCounter);

                if(phase3_boulderCounter==5) {

                    if(phase3_bouldersOnLeft<phase3_bouldersOnRight) {

                        phase3_leftSafeZone=true;

                    }
                    
                    if(phase3_bouldersOnLeft>phase3_bouldersOnRight) {

                        phase3_leftSafeZone=false;

                    }

                    phase3_bombBoulderSemaphore.Set();

                    if(enableDebugLogging) {
                
                        accessory.Log.Debug($"phase3_bouldersOnLeft={phase3_bouldersOnLeft}\nphase3_bouldersOnRight={phase3_bouldersOnRight}\nphase3_leftSafeZone={phase3_leftSafeZone}");
                
                    }

                }

            }

        }
        
        [ScriptMethod(name:"泰坦 第一次爆破岩石 (范围)",
            eventType:EventTypeEnum.AddCombatant,
            eventCondition:["DataId:8728"])]

        public void 泰坦_第一次爆破岩石_范围(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=3&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=2&&!skipPhaseChecks) {

                return;

            }
            
            Vector3 sourcePosition=ARENA_CENTER;

            try {

                sourcePosition=JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);

            } catch(Exception e) {
                
                accessory.Log.Error("SourcePosition deserialization failed.");

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(6.3f);
            currentProperties.Position=sourcePosition;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.DestoryAt=6500;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Circle,currentProperties);

        }
        
        [ScriptMethod(name:"泰坦 大怒震 (指路)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11111"])]

        public void 泰坦_大怒震_指路(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=3&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=2&&!skipPhaseChecks) {

                return;

            }
            
            bool signalled=phase3_bombBoulderSemaphore.WaitOne(COMMON_INTERVAL);
            
            if(!signalled) {

                return;

            }

            Vector3 myPosition=ARENA_CENTER;

            if(phase3_leftSafeZone) {

                myPosition=new Vector3(99.541504f,0,89.973636f);

            }

            else {
                
                myPosition=new Vector3(100.458496f,0,89.973636f);
                
            }
            // Geometric Construction:
            // https://www.geogebra.org/calculator/r79ncvch

            myPosition=rotatePosition(myPosition,ARENA_CENTER,Math.PI/2*phase3_discretizedLandingPosition);
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetPosition=myPosition;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=4375;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);

        }
        
        [ScriptMethod(name:"泰坦 花岗岩牢狱之前的地裂 (引导指路)",
            eventType:EventTypeEnum.ActionEffect,
            eventCondition:["ActionId:11111"],
            suppress:COMMON_INTERVAL)]

        public void 泰坦_花岗岩牢狱之前的地裂_引导指路(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=3&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=2&&!skipPhaseChecks) {

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(0.5f,32);
            currentProperties.Position=ARENA_CENTER;
            currentProperties.TargetPosition=rotatePosition(new Vector3(ARENA_CENTER.X,ARENA_CENTER.Y,ARENA_CENTER.Z-1),ARENA_CENTER,Math.PI/2*phase3_discretizedLandingPosition);
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=4250;
        
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Straight,currentProperties);

        }
        
        [ScriptMethod(name:"泰坦 花岗岩牢狱 (数据获取)",
            eventType:EventTypeEnum.ActionEffect,
            eventCondition:["ActionId:regex:^(11115|11116)$"],
            userControl:false)]

        public void 泰坦_花岗岩牢狱_数据获取(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=3&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=2&&!skipPhaseChecks) {

                return;

            }

            if(phase3_rockThrowCounter>=3) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["TargetId"], out var targetId)) {
                
                return;
                
            }

            int targetIndex=accessory.Data.PartyList.IndexOf(((uint)targetId));
            
            if(!isLegalPartyIndex(targetIndex)) {

                return;

            }

            lock(phase3_isRockThrow) {

                phase3_isRockThrow[targetIndex]=true;
                
                Interlocked.Increment(ref phase3_rockThrowCounter);

                if(phase3_rockThrowCounter==3) {

                    int[] temporaryOrder=[0,1,4,5,6,7,2,3];
                    phase3_rockThrowOrder.Clear();

                    for(int i=0;i<temporaryOrder.Length;++i) {

                        if(phase3_isRockThrow[temporaryOrder[i]]) {
                            
                            phase3_rockThrowOrder.Add(temporaryOrder[i]);
                            
                        }
                        
                    }

                    if(phase3_rockThrowOrder.Count!=3) {

                        return;

                    }

                    phase3_rockThrowSemaphore1.Set();
                    phase3_rockThrowSemaphore2.Set();
                    phase3_rockThrowSemaphore3.Set();

                    if(enableDebugLogging) {

                        accessory.Log.Debug($"""
                                             phase3_isRockThrow:{string.Join(",",phase3_isRockThrow)}
                                             phase3_rockThrowOrder:{string.Join(",",phase3_rockThrowOrder)}
                                             """);

                    }

                }

            }

        }
        
        [ScriptMethod(name:"泰坦 花岗岩牢狱 (指路)",
            eventType:EventTypeEnum.ActionEffect,
            eventCondition:["ActionId:regex:^(11115|11116)$"],
            suppress:COMMON_INTERVAL)]

        public void 泰坦_花岗岩牢狱_指路(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=3&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=2&&!skipPhaseChecks) {

                return;

            }

            bool signalled=phase3_rockThrowSemaphore1.WaitOne(COMMON_INTERVAL);
            
            if(!signalled) {

                return;

            }
            
            if(phase3_rockThrowOrder.Count!=3) {

                return;

            }
            
            int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            
            if(!isLegalPartyIndex(myIndex)) {

                return;

            }

            int myOrder=-1;

            if(!phase3_rockThrowOrder.Contains(myIndex)) {

                return;

            }

            else {

                myOrder=phase3_rockThrowOrder.IndexOf(myIndex);

            }

            Vector3 myPosition=myOrder switch {
                
                0 => new Vector3(100,0,94),
                1 => new Vector3(100,0,100),
                2 => ((phase3_leftSafeZone)?(new Vector3(97.653846f,0,105.630769f)):(new Vector3(102.346154f,0,105.630769f))),
                _ => ARENA_CENTER
                
            };
            // Geometric Construction:
            // https://www.geogebra.org/calculator/hjbpprjp
            
            myPosition=rotatePosition(myPosition,ARENA_CENTER,Math.PI/2*phase3_discretizedLandingPosition);
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetPosition=myPosition;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=5000;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);

        }
        
        [ScriptMethod(name:"泰坦 花岗岩牢狱 (碰撞箱与追踪爆炸范围)",
            eventType:EventTypeEnum.ActionEffect,
            eventCondition:["ActionId:regex:^(11115|11116)$"],
            suppress:COMMON_INTERVAL)]

        public void 泰坦_花岗岩牢狱_碰撞箱与追踪爆炸范围(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=3&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=2&&!skipPhaseChecks) {

                return;

            }

            bool signalled=phase3_rockThrowSemaphore2.WaitOne(COMMON_INTERVAL);
            
            if(!signalled) {

                return;

            }
            
            if(phase3_rockThrowOrder.Count!=3) {

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            for(int i=0;i<phase3_rockThrowOrder.Count;++i) {
                
                currentProperties=accessory.Data.GetDefaultDrawProperties();
                
                currentProperties.Scale=new(1.8f);
                currentProperties.Owner=accessory.Data.PartyList[phase3_rockThrowOrder[i]];
                currentProperties.Color=colourOfDirectionIndicators.V4.WithW(1);
                currentProperties.DestoryAt=7000;
            
                accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Circle,currentProperties);
                
                currentProperties=accessory.Data.GetDefaultDrawProperties();
                
                currentProperties.Scale=new(6);
                currentProperties.InnerScale=new(1.8f);
                currentProperties.Radian=float.Pi*2;
                currentProperties.Owner=accessory.Data.PartyList[phase3_rockThrowOrder[i]];
                currentProperties.Color=accessory.Data.DefaultDangerColor.WithW(1);
                currentProperties.DestoryAt=7000;
            
                accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Donut,currentProperties);
                
            }

        }
        
        [ScriptMethod(name:"泰坦 花岗岩牢狱 (小队指挥)",
            eventType:EventTypeEnum.ActionEffect,
            eventCondition:["ActionId:regex:^(11115|11116)$"],
            suppress:COMMON_INTERVAL)]

        public void 泰坦_花岗岩牢狱_小队指挥(Event @event,ScriptAccessory accessory) {

            if(!phase3_enableRockThrowAssistance) {

                return;

            }
            
            if(majorPhase!=3&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=2&&!skipPhaseChecks) {

                return;

            }

            bool signalled=phase3_rockThrowSemaphore3.WaitOne(COMMON_INTERVAL);
            
            if(!signalled) {

                return;

            }

            if(phase3_rockThrowOrder.Count!=3) {

                return;

            }
            
            accessory.Method.Mark(accessory.Data.PartyList[phase3_rockThrowOrder[0]],MarkType.Attack1);
            accessory.Method.Mark(accessory.Data.PartyList[phase3_rockThrowOrder[1]],MarkType.Attack2);
            accessory.Method.Mark(accessory.Data.PartyList[phase3_rockThrowOrder[2]],MarkType.Attack3);

            if(enableDebugLogging) {

                accessory.Log.Debug($"""
                                     Mark {phase3_rockThrowOrder[0]} as {MarkType.Attack1}
                                     Mark {phase3_rockThrowOrder[1]} as {MarkType.Attack2}
                                     Mark {phase3_rockThrowOrder[2]} as {MarkType.Attack3}
                                     """);

            }

        }
        
        [ScriptMethod(name:"泰坦 第二次大地之重 (阶段控制)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11109"],
            suppress:COMMON_INTERVAL,
            userControl:false)]

        public void 泰坦_第二次大地之重_阶段控制(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=3&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=2&&!skipPhaseChecks) {

                return;

            }

            phase=3;
            
            if(enableDebugLogging) {
                
                accessory.Log.Debug($"majorPhase={majorPhase}\nphase={phase}");
                
            }
            
        }
        
        [ScriptMethod(name:"泰坦 第三次大地粉碎 (泰坦位置与面向指示)",
            eventType:EventTypeEnum.PlayActionTimeline,
            eventCondition:["SourceDataId:8727"])]

        public void 泰坦_第三次大地粉碎_泰坦位置与面向指示(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=3&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=3&&!skipPhaseChecks) {

                return;

            }

            if(!string.Equals(@event["Id"],"7737")) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }

            if(sourceId!=phase3_titanId) {

                return;

            }
            
            var sourceObject=accessory.Data.Objects.SearchById(sourceId);
            uint currentHp=1440211;

            if(sourceObject==null) {

                return;
                
            }

            else {
                
                if(sourceObject is not ICharacter sourceICharacter) {

                    return;
                    
                }

                else {

                    currentHp=sourceICharacter.CurrentHp;

                    if(currentHp<=1) {

                        return;

                    }

                }
                
            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(4.5f);
            currentProperties.InnerScale=new(3.5f);
            currentProperties.Radian=float.Pi*2;
            currentProperties.Owner=sourceId;
            currentProperties.Color=colourOfDirectionIndicators.V4.WithW(1);
            currentProperties.DestoryAt=5000;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Donut,currentProperties);
            
            currentProperties=accessory.Data.GetDefaultDrawProperties();
            
            currentProperties.Scale=new(1,4);
            currentProperties.Owner=sourceId;
            currentProperties.Color=colourOfDirectionIndicators.V4.WithW(1);
            currentProperties.DestoryAt=5000;
        
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Arrow,currentProperties);

        }
        
        [ScriptMethod(name:"泰坦 第三次大地粉碎 (指路)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11110"])]

        public void 泰坦_第三次大地粉碎_指路(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=3&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=3&&!skipPhaseChecks) {

                return;

            }
            
            Vector3 targetPosition=ARENA_CENTER;

            try {

                targetPosition=JsonConvert.DeserializeObject<Vector3>(@event["TargetPosition"]);

            } catch(Exception e) {
                
                accessory.Log.Error("TargetPosition deserialization failed.");

                return;

            }
            
            int discretizedLandingPosition=discretizePosition(targetPosition,ARENA_CENTER,4);
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetPosition=rotatePosition(new Vector3(100,0,111),ARENA_CENTER,Math.PI/2*discretizedLandingPosition);
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=3000;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
            
            if(enableDebugLogging) {
                
                accessory.Log.Debug($"discretizedLandingPosition={discretizedLandingPosition}");
                
            }

        }
        
        [ScriptMethod(name:"泰坦 第二次花岗岩牢狱 (指路)",
            eventType:EventTypeEnum.ActionEffect,
            eventCondition:["ActionId:11115"])]

        public void 泰坦_第二次花岗岩牢狱_指路(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=3&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=3&&!skipPhaseChecks) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["TargetId"], out var targetId)) {
                
                return;
                
            }

            if(targetId!=accessory.Data.Me) {

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetPosition=ARENA_CENTER;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=5000;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);

        }
        
        [ScriptMethod(name:"泰坦 第二次花岗岩牢狱 (数据获取)",
            eventType:EventTypeEnum.ActionEffect,
            eventCondition:["ActionId:11115"],
            userControl:false)]

        public void 泰坦_第二次花岗岩牢狱_数据获取(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=3&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=3&&!skipPhaseChecks) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["TargetId"], out var targetId)) {
                
                return;
                
            }

            phase3_secondRockThrowTarget=targetId;

            if(enableDebugLogging) {
                
                accessory.Log.Debug($"phase3_secondRockThrowTarget={phase3_secondRockThrowTarget}");
                
            }

        }
        
        [ScriptMethod(name:"泰坦 第二次花岗岩牢狱 (目标指示)",
            eventType:EventTypeEnum.Targetable,
            eventCondition:["DataId:8729"])]

        public void 泰坦_第二次花岗岩牢狱_目标指示(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=3&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=3&&!skipPhaseChecks) {

                return;

            }
            
            if(!string.Equals(@event["Targetable"],"True")) {

                return;

            }

            if(accessory.Data.Me==phase3_secondRockThrowTarget) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Name="泰坦_第二次花岗岩牢狱_目标指示1";
            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetObject=sourceId;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=colourOfDirectionIndicators.V4.WithW(1);
            currentProperties.DestoryAt=7000;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
            
            currentProperties=accessory.Data.GetDefaultDrawProperties();
            
            currentProperties.Name="泰坦_第二次花岗岩牢狱_目标指示2";
            currentProperties.Scale=new(1.8f);
            currentProperties.Owner=sourceId;
            currentProperties.Color=colourOfDirectionIndicators.V4.WithW(1);
            currentProperties.DestoryAt=7000;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Circle,currentProperties);

        }
        
        [ScriptMethod(name:"泰坦 第二次花岗岩牢狱 (目标指示清除)",
            eventType:EventTypeEnum.CancelAction,
            eventCondition:["ActionId:11448"],
            userControl:false)]

        public void 泰坦_第二次花岗岩牢狱_目标指示清除(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=3&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=3&&!skipPhaseChecks) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }
            
            var sourceObject=accessory.Data.Objects.SearchById(sourceId);

            if(sourceObject==null) {

                return;

            }

            else {

                if(sourceObject.DataId!=8729) {

                    return;

                }
                
            }
            
            accessory.Method.RemoveDraw("泰坦_第二次花岗岩牢狱_目标指示1");
            accessory.Method.RemoveDraw("泰坦_第二次花岗岩牢狱_目标指示2");
            
        }
        
        [ScriptMethod(name:"泰坦 第二次怒震 (数据获取)",
            eventType:EventTypeEnum.ActionEffect,
            eventCondition:["ActionId:11288"],
            suppress:500,
            userControl:false)]

        public void 泰坦_第二次怒震_数据获取(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=3&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=3&&!skipPhaseChecks) {

                return;

            }

            if(phase3_tumultCounter>=6) {

                return;

            }

            lock(phase3_tumultSemaphore) {

                Interlocked.Increment(ref phase3_tumultCounter);

                if(phase3_tumultCounter==6) {

                    phase3_tumultSemaphore.Set();

                }

            }

        }
        
        [ScriptMethod(name:"泰坦 第二次碎岩山崩连击 (范围)",
            eventType:EventTypeEnum.ActionEffect,
            eventCondition:["ActionId:11288"],
            suppress:8000)]

        public void 泰坦_第二次碎岩山崩连击_范围(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=3&&!skipPhaseChecks) {

                return;

            }

            if(phase!=3&&!skipPhaseChecks) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }
            
            bool signalled=phase3_tumultSemaphore.WaitOne(8000);
            
            if(!signalled) {

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(10.5f);
            currentProperties.Owner=sourceId;
            currentProperties.Radian=float.Pi/2;
            currentProperties.TargetResolvePattern=PositionResolvePatternEnum.OwnerEnmityOrder;
            currentProperties.TargetOrderIndex=1;
            currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(1);
            currentProperties.DestoryAt=2250;
        
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Fan,currentProperties);
            
            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(15.5f);
            currentProperties.Owner=sourceId;
            currentProperties.Radian=float.Pi/2;
            currentProperties.TargetResolvePattern=PositionResolvePatternEnum.OwnerEnmityOrder;
            currentProperties.TargetOrderIndex=1;
            currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(1);
            currentProperties.Delay=2250;
            currentProperties.DestoryAt=4000;
        
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Fan,currentProperties);

        }
        
        [ScriptMethod(name:"泰坦 第二次爆破岩石 (范围)",
            eventType:EventTypeEnum.AddCombatant,
            eventCondition:["DataId:8728"])]

        public void 泰坦_第二次爆破岩石_范围(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=3&&!skipPhaseChecks) {

                return;

            }

            if(phase!=3&&!skipPhaseChecks) {

                return;

            }
            
            Vector3 sourcePosition=ARENA_CENTER;

            try {

                sourcePosition=JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);

            } catch(Exception e) {
                
                accessory.Log.Error("SourcePosition deserialization failed.");

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(6.3f);
            currentProperties.Position=sourcePosition;
            currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(1);
            currentProperties.DestoryAt=6375;
            
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,currentProperties);

        }
        
        [ScriptMethod(name:"泰坦 第二次爆破岩石 (数据获取)",
            eventType:EventTypeEnum.RemoveCombatant,
            eventCondition:["DataId:8728"],
            userControl:false)]

        public void 泰坦_第二次爆破岩石_数据获取(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=3&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=3&&!skipPhaseChecks) {

                return;

            }

            if(phase3_boulderRemovalCounter>=4) {

                return;

            }

            lock(phase3_boulderRemovalSemaphore) {

                Interlocked.Increment(ref phase3_boulderRemovalCounter);

                if(phase3_boulderRemovalCounter==4) {

                    phase=4;

                    phase3_boulderRemovalSemaphore.Set();

                }

            }

        }
        
        [ScriptMethod(name:"泰坦 第三次碎岩山崩连击 (范围)",
            eventType:EventTypeEnum.RemoveCombatant,
            eventCondition:["DataId:8728"],
            suppress:8500)]

        public void 泰坦_第三次碎岩山崩连击_范围(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=3&&!skipPhaseChecks) {

                return;

            }
            
            bool signalled=phase3_boulderRemovalSemaphore.WaitOne(8500);
            
            if(!signalled) {

                return;

            }
            
            if(phase!=4&&!skipPhaseChecks) {

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(10.5f);
            currentProperties.Owner=phase3_titanId;
            currentProperties.Radian=float.Pi/2;
            currentProperties.TargetResolvePattern=PositionResolvePatternEnum.OwnerEnmityOrder;
            currentProperties.TargetOrderIndex=1;
            currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(1);
            currentProperties.DestoryAt=2500;
        
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Fan,currentProperties);
            
            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(15.5f);
            currentProperties.Owner=phase3_titanId;
            currentProperties.Radian=float.Pi/2;
            currentProperties.TargetResolvePattern=PositionResolvePatternEnum.OwnerEnmityOrder;
            currentProperties.TargetOrderIndex=1;
            currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(1);
            currentProperties.Delay=2500;
            currentProperties.DestoryAt=4125;
        
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Fan,currentProperties);

        }
        
        #endregion
        
        #region Ascian_Lahabrea
        
        [ScriptMethod(name:"无影拉哈布雷亚 追踪爆炸 (阶段控制)",
            eventType:EventTypeEnum.ActionEffect,
            eventCondition:["ActionId:11509"],
            suppress:COMMON_INTERVAL,
            userControl:false)]

        public void 无影拉哈布雷亚_追踪爆炸_阶段控制(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=3&&!skipPhaseChecks) {

                return;

            }
            
            majorPhase=4;
            phase=1;

            if(!preserveDrawingsWhileSwitchingPhase) {
                
                accessory.Method.RemoveDraw(".*");
                
            }
            
            if(phase3_enableRockThrowAssistance) {

                accessory.Method.MarkClear();
                
            }
            
            if(enableDebugLogging) {
                
                accessory.Log.Debug($"majorPhase={majorPhase}\nphase={phase}");
                
            }

        }
        
        #endregion
        
        #region Ultima_Weapon
        
        [ScriptMethod(name:"究极神兵 魔导核爆 (阶段控制)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11143"],
            userControl:false)]

        public void 究极神兵_魔导核爆_阶段控制(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=4&&!skipPhaseChecks) {

                return;

            }
            
            majorPhase=5;
            phase=1;
            
            if(!preserveDrawingsWhileSwitchingPhase) {
                
                accessory.Method.RemoveDraw("^(?!究极神兵_第一次吸附式以太炸弹_范围$).*$");
                
            }

            phase5_firstTankPurgeSemaphore.Set();
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }

            phase5_ultimaWeaponId=sourceId;
            
            if(enableDebugLogging) {
                
                accessory.Log.Debug($"majorPhase={majorPhase}\nphase={phase}\nphase5_ultimaWeaponId={phase5_ultimaWeaponId}");
                
            }

        }
        
        [ScriptMethod(name:"究极神兵 第一次吸附式以太炸弹 (范围)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11143"])]

        public void 究极神兵_第一次吸附式以太炸弹_范围(Event @event,ScriptAccessory accessory) {
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }
            
            bool signalled=phase5_firstTankPurgeSemaphore.WaitOne(COMMON_INTERVAL);

            if(!signalled) {

                return;

            }
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=1&&!skipPhaseChecks) {

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Name="究极神兵_第一次吸附式以太炸弹_范围";
            currentProperties.Scale=new(2);
            currentProperties.Owner=sourceId;
            currentProperties.CentreResolvePattern=PositionResolvePatternEnum.OwnerEnmityOrder;
            currentProperties.CentreOrderIndex=1;
            currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(1);
            currentProperties.DestoryAt=6250;
        
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,currentProperties);

        }
        
        [ScriptMethod(name:"究极神兵 追踪射线 (范围)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11131"])]

        public void 究极神兵_追踪射线_范围(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["TargetId"], out var targetId)) {
                
                return;
                
            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(4);
            currentProperties.Owner=targetId;
            currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(1);
            currentProperties.DestoryAt=3000;
        
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,currentProperties);

        }
        
        [ScriptMethod(name:"究极神兵 追击之究极幻想 (阶段控制)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11126"],
            userControl:false)]

        public void 究极神兵_追击之究极幻想_阶段控制(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=1&&!skipPhaseChecks) {

                return;

            }

            phase=2;
            
            if(enableDebugLogging) {
                
                accessory.Log.Debug($"majorPhase={majorPhase}\nphase={phase}");
                
            }

        }
        
        [ScriptMethod(name:"究极神兵 追击之究极幻想前半 泰坦 (数据收集)",
            eventType:EventTypeEnum.PlayActionTimeline,
            eventCondition:["SourceDataId:8727"],
            userControl:false)]

        public void 究极神兵_追击之究极幻想前半_泰坦_数据收集(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=2&&!skipPhaseChecks) {

                return;

            }
            
            if(!string.Equals(@event["Id"],"7747")) {

                return;

            }
            
            if(phase5sub2_discretizedTitanPosition!=-1) {

                return;

            }
            
            Vector3 sourcePosition=ARENA_CENTER;

            try {

                sourcePosition=JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);

            } catch(Exception e) {
                
                accessory.Log.Error("SourcePosition deserialization failed.");

                return;

            }
            
            int discretizedPosition=discretizePosition(sourcePosition,ARENA_CENTER,4);

            phase5sub2_discretizedTitanPosition=discretizedPosition;

            phase5sub2_titanSemaphore.Set();
            
            if(enableDebugLogging) {
                
                accessory.Log.Debug($"phase5sub2_discretizedTitanPosition={phase5sub2_discretizedTitanPosition}");
                
            }

        }
        
        [ScriptMethod(name:"究极神兵 追击之究极幻想前半 起始位置 (指示)",
            eventType:EventTypeEnum.PlayActionTimeline,
            eventCondition:["SourceDataId:8722"])]

        public void 究极神兵_追击之究极幻想前半_起始位置_指示(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=2&&!skipPhaseChecks) {

                return;

            }
            
            if(!string.Equals(@event["Id"],"7747")) {

                return;

            }
            
            Vector3 sourcePosition=ARENA_CENTER;

            try {

                sourcePosition=JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);

            } catch(Exception e) {
                
                accessory.Log.Error("SourcePosition deserialization failed.");

                return;

            }
            
            int discretizedPosition=discretizePosition(sourcePosition,ARENA_CENTER,4,false);

            bool[] safeZone=[false,false,false,false];

            safeZone[(discretizedPosition+2)%4]=true;
            safeZone[(discretizedPosition+3)%4]=true;

            bool signalled=phase5sub2_titanSemaphore.WaitOne(COMMON_INTERVAL);

            if(!signalled) {

                return;

            }

            safeZone[phase5sub2_discretizedTitanPosition]=false;
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            for(int i=0;i<safeZone.Length;++i) {

                if(safeZone[i]) {
                    
                    currentProperties=accessory.Data.GetDefaultDrawProperties();
            
                    currentProperties.Scale=new(2,7);
                    currentProperties.Position=ARENA_CENTER;
                    currentProperties.TargetPosition=rotatePosition(new Vector3(100,0,93),ARENA_CENTER,double.Pi/2*i);
                    currentProperties.Color=colourOfDirectionIndicators.V4.WithW(1);
                    currentProperties.DestoryAt=8125;
        
                    accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Arrow,currentProperties);
                    
                }
                
            }
            
            if(enableDebugLogging) {
                
                accessory.Log.Debug($"""
                                     discretizedPosition={discretizedPosition}
                                     safeZone:{string.Join(",",safeZone)}
                                     """);
                
            }

        }
        
        [ScriptMethod(name:"究极神兵 追击之究极幻想前半 深红旋风 (范围)",
            eventType:EventTypeEnum.PlayActionTimeline,
            eventCondition:["SourceDataId:8730"])]

        public void 究极神兵_追击之究极幻想前半_深红旋风_范围(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=2&&!skipPhaseChecks) {

                return;

            }
            
            if(!string.Equals(@event["Id"],"7747")) {

                return;

            }
            
            Vector3 sourcePosition=ARENA_CENTER;

            try {

                sourcePosition=JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);

            } catch(Exception e) {
                
                accessory.Log.Error("SourcePosition deserialization failed.");

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(18,44);
            currentProperties.Position=sourcePosition;
            currentProperties.TargetPosition=ARENA_CENTER;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.DestoryAt=8125;
        
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Rect,currentProperties);

            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(10,44);
            currentProperties.Position=ARENA_CENTER;
            currentProperties.TargetPosition=new Vector3(ARENA_CENTER.X,ARENA_CENTER.Y,ARENA_CENTER.Z-1);
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.Delay=8125;
            currentProperties.DestoryAt=2125;
        
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Straight,currentProperties);
            
            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(10,44);
            currentProperties.Position=ARENA_CENTER;
            currentProperties.TargetPosition=new Vector3(ARENA_CENTER.X+1,ARENA_CENTER.Y,ARENA_CENTER.Z);
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.Delay=8125;
            currentProperties.DestoryAt=2125;
        
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Straight,currentProperties);
            
        }
        
        [ScriptMethod(name:"究极神兵 追击之究极幻想前半 邪气龙卷 (范围)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11086"])]

        public void 究极神兵_追击之究极幻想前半_邪气龙卷_范围(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=2&&!skipPhaseChecks) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();
                
            currentProperties.Scale=new(20);
            currentProperties.InnerScale=new(8.5f);
            currentProperties.Radian=float.Pi*2;
            currentProperties.Owner=sourceId;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.Delay=3000;
            currentProperties.DestoryAt=2250;
        
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Donut,currentProperties);
            
        }
        
        [ScriptMethod(name:"究极神兵 追击之究极幻想前半 究极神兵出现 (阶段控制)",
            eventType:EventTypeEnum.PlayActionTimeline,
            eventCondition:["SourceDataId:8734"],
            userControl:false)]

        public void 究极神兵_追击之究极幻想前半_究极神兵出现_阶段控制(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=2&&!skipPhaseChecks) {

                return;

            }
            
            if(!string.Equals(@event["Id"],"7747")) {

                return;

            }

            if(phase5sub2_ultimaWeaponAppearanceCounter>=2) {

                return;

            }

            Interlocked.Increment(ref phase5sub2_ultimaWeaponAppearanceCounter);

            if(phase5sub2_ultimaWeaponAppearanceCounter==1) {

                phase5sub2_ultimaWeaponAppearance1Semaphore.Set();
                
                if(enableDebugLogging) {
                
                    accessory.Log.Debug($"phase5sub2_ultimaWeaponAppearanceCounter={phase5sub2_ultimaWeaponAppearanceCounter}");
                
                }

            }

            else {
                
                if(phase5sub2_ultimaWeaponAppearanceCounter==2) {

                    phase2_infernalFetterDrawingCounter=0;

                    phase=3;

                    phase5sub2_ultimaWeaponAppearance2Semaphore.Set();
                    
                    if(enableDebugLogging) {
                
                        accessory.Log.Debug($"majorPhase={majorPhase}\nphase={phase}\nphase5sub2_ultimaWeaponAppearanceCounter={phase5sub2_ultimaWeaponAppearanceCounter}");
                
                    }

                }
                
            }
            
        }
        
        [ScriptMethod(name:"究极神兵 追击之究极幻想前半 青磷放射 (范围)",
            eventType:EventTypeEnum.PlayActionTimeline,
            eventCondition:["SourceDataId:8734"])]

        public void 究极神兵_追击之究极幻想前半_青磷放射_范围(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=2&&!skipPhaseChecks) {

                return;

            }
            
            if(!string.Equals(@event["Id"],"7747")) {

                return;

            }
            
            Vector3 sourcePosition=ARENA_CENTER;

            try {

                sourcePosition=JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);

            } catch(Exception e) {
                
                accessory.Log.Error("SourcePosition deserialization failed.");

                return;

            }
            
            bool signalled=phase5sub2_ultimaWeaponAppearance1Semaphore.WaitOne(COMMON_INTERVAL);

            if(!signalled) {

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(14);
            currentProperties.Position=sourcePosition;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.DestoryAt=8125;
        
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Circle,currentProperties);
            
            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(14);
            currentProperties.Position=sourcePosition;
            currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(1);
            currentProperties.Delay=8125;
            currentProperties.DestoryAt=2000;
        
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Circle,currentProperties);
            
        }
        
        [ScriptMethod(name:"究极神兵 追击之究极幻想后半 地火喷发 (指路,仅远程DPS)",
            eventType:EventTypeEnum.PlayActionTimeline,
            eventCondition:["SourceDataId:8734"])]

        public void 究极神兵_追击之究极幻想后半_地火喷发_指路_仅远程DPS(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }
            
            if(!string.Equals(@event["Id"],"7747")) {

                return;

            }
            
            int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            
            if(!isLegalPartyIndex(myIndex)) {

                return;

            }

            if(!isRangedDps(myIndex)) {

                return;

            }
            
            bool signalled=phase5sub2_ultimaWeaponAppearance2Semaphore.WaitOne(COMMON_INTERVAL);

            if(!signalled) {

                return;

            }
            
            if(phase!=3&&!skipPhaseChecks) {

                return;

            }

            Vector3 myDirection=myIndex switch {

                7 => new Vector3(ARENA_CENTER.X+1,ARENA_CENTER.Y,ARENA_CENTER.Z+1),
                6 => new Vector3(ARENA_CENTER.X-1,ARENA_CENTER.Y,ARENA_CENTER.Z+1),
                _ => ARENA_CENTER

            };
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetPosition=new Vector3(100,0,118.5f);
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=11500;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);

            currentProperties=accessory.Data.GetDefaultDrawProperties();
                
            currentProperties.Scale=new(19.5f);
            currentProperties.InnerScale=new(18.5f);
            currentProperties.Radian=float.Pi/2;
            currentProperties.Position=ARENA_CENTER;
            currentProperties.TargetPosition=myDirection;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=17500;
                
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Donut,currentProperties);

        }
        
        [ScriptMethod(name:"究极神兵 追击之究极幻想后半 火狱之锁 (连线指示)",
            eventType:EventTypeEnum.Tether,
            eventCondition:["Id:0009"])]

        public void 究极神兵_追击之究极幻想后半_火狱之锁_连线指示(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=3&&!skipPhaseChecks) {

                return;

            }
            
            accessory.Method.RemoveDraw($"究极神兵_追击之究极幻想后半_火狱之锁_连线指示_{phase2_infernalFetterDrawingCounter}");
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }
            
            if(!convertObjectIdToDecimal(@event["TargetId"], out var targetId)) {
                
                return;
                
            }

            if(accessory.Data.Me!=sourceId&&accessory.Data.Me!=targetId) {

                return;

            }
            
            int sourceIndex=accessory.Data.PartyList.IndexOf(((uint)sourceId));
            
            if(!isLegalPartyIndex(sourceIndex)) {

                return;

            }
            
            int targetIndex=accessory.Data.PartyList.IndexOf(((uint)targetId));
            
            if(!isLegalPartyIndex(targetIndex)) {

                return;
            }

            ulong sourceInDrawing=sourceId,targetInDrawing=targetId;
            bool anomalousTether=true;

            if(isTank(sourceIndex)&&(isDps(targetIndex)||isHealer(targetIndex))) {

                anomalousTether=false;

                sourceInDrawing=sourceId;
                targetInDrawing=targetId;

            }
            
            if(isTank(targetIndex)&&(isDps(sourceIndex)||isHealer(sourceIndex))) {

                anomalousTether=false;

                sourceInDrawing=targetId;
                targetInDrawing=sourceId;

            }

            if(anomalousTether) {

                if(accessory.Data.Me==sourceId) {
                    
                    sourceInDrawing=sourceId;
                    targetInDrawing=targetId;

                }
                
                if(accessory.Data.Me==targetId) {
                    
                    sourceInDrawing=targetId;
                    targetInDrawing=sourceId;

                }
                
            }
            
            Interlocked.Increment(ref phase2_infernalFetterDrawingCounter);
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Name=$"究极神兵_追击之究极幻想后半_火狱之锁_连线指示_{phase2_infernalFetterDrawingCounter}";
            currentProperties.Scale=new(2);
            currentProperties.Owner=sourceInDrawing;
            currentProperties.TargetObject=targetInDrawing;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=colourOfDirectionIndicators.V4.WithW(1);
            currentProperties.DestoryAt=21000;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
            
        }
        
        [ScriptMethod(name:"究极神兵 追击之究极幻想后半 爆破岩石 (范围)",
            eventType:EventTypeEnum.AddCombatant,
            eventCondition:["DataId:8728"])]

        public void 究极神兵_追击之究极幻想后半_爆破岩石_范围(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=3&&!skipPhaseChecks) {

                return;

            }
            
            Vector3 sourcePosition=ARENA_CENTER;

            try {

                sourcePosition=JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);

            } catch(Exception e) {
                
                accessory.Log.Error("SourcePosition deserialization failed.");

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(6.3f);
            currentProperties.Position=sourcePosition;
            currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(1);
            currentProperties.DestoryAt=6375;
            
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,currentProperties);

        }
        
        [ScriptMethod(name:"究极神兵 追击之究极幻想后半 地裂 (引导指路)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11476"])]

        public void 究极神兵_追击之究极幻想后半_地裂_引导指路(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=3&&!skipPhaseChecks) {

                return;

            }
            
            if(!string.Equals(@event["SourceDataId"],"8734")) {

                return;

            }
            
            if(!string.Equals(@event["TargetDataId"],"8734")) {

                return;

            }
            
            if(!string.Equals(@event["SourceId"],@event["TargetId"])) {

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(0.5f,19.5f);
            currentProperties.Position=ARENA_CENTER;
            // 引导轴线改为朝北(原版朝南Z+1)。
            currentProperties.TargetPosition=new Vector3(ARENA_CENTER.X,ARENA_CENTER.Y,ARENA_CENTER.Z-1);
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.Delay=12250;
            currentProperties.DestoryAt=10000;
        
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Rect,currentProperties);

        }
        
        [ScriptMethod(name:"究极神兵 第二次吸附式以太炸弹 (范围)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11475"])]

        public void 究极神兵_第二次吸附式以太炸弹_范围(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=3&&!skipPhaseChecks) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(2);
            currentProperties.Owner=sourceId;
            currentProperties.CentreResolvePattern=PositionResolvePatternEnum.OwnerEnmityOrder;
            currentProperties.CentreOrderIndex=1;
            currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(1);
            currentProperties.DestoryAt=5250;
        
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,currentProperties);

        }
        
        [ScriptMethod(name:"究极神兵 爆击之究极幻想 (阶段控制)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11596"],
            userControl:false)]

        public void 究极神兵_爆击之究极幻想_阶段控制(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=3&&!skipPhaseChecks) {

                return;

            }
            
            phase1_mesohighDrawingCounter.Clear();

            phase=4;

            phase5sub4_ultimateAnnihilationSemaphore.Set();
            
            if(enableDebugLogging) {
                
                accessory.Log.Debug($"majorPhase={majorPhase}\nphase={phase}");
                
            }

        }
        
        [ScriptMethod(name:"究极神兵 爆击之究极幻想 伊弗利特 (数据收集)",
            eventType:EventTypeEnum.PlayActionTimeline,
            eventCondition:["SourceDataId:8730"],
            userControl:false)]

        public void 究极神兵_爆击之究极幻想_伊弗利特_数据收集(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=4&&!skipPhaseChecks) {

                return;

            }

            if(0<=phase5sub4_discretizedIfritPosition&&phase5sub4_discretizedIfritPosition<8) {

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

            phase5sub4_discretizedIfritPosition=discretizedPosition;
            
            if(enableDebugLogging) {
                
                accessory.Log.Debug($"phase5sub4_discretizedIfritPosition={phase5sub4_discretizedIfritPosition}");
                
            }

        }
        
        [ScriptMethod(name:"究极神兵 爆击之究极幻想 中高压 (范围)",
            eventType:EventTypeEnum.Tether,
            eventCondition:["Id:0004"])]
    
        public void 究极神兵_爆击之究极幻想_中高压_范围(Event @event,ScriptAccessory accessory) {

            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=4&&!skipPhaseChecks) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }
            
            if(!convertObjectIdToDecimal(@event["TargetId"], out var targetId)) {
                
                return;
                
            }

            lock(phase1_mesohighDrawingCounter) {
                
                int lastDrawing=phase1_mesohighDrawingCounter.GetOrAdd(sourceId,0);
            
                accessory.Method.RemoveDraw($"究极神兵_爆击之究极幻想_中高压_范围_{sourceId}_{lastDrawing}");

                ++lastDrawing;
                phase1_mesohighDrawingCounter[sourceId]=lastDrawing;
            
                var currentProperties=accessory.Data.GetDefaultDrawProperties();

                currentProperties.Name=$"究极神兵_爆击之究极幻想_中高压_范围_{sourceId}_{lastDrawing}";
                currentProperties.Scale=new(3);
                currentProperties.Owner=targetId;
                currentProperties.Color=accessory.Data.DefaultDangerColor;
                currentProperties.DestoryAt=MAXIMUM_DURATION;

                accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,currentProperties);
                
            }
        
        }
        
        [ScriptMethod(name:"究极神兵 爆击之究极幻想 中高压 (范围清除)",
            eventType:EventTypeEnum.ActionEffect,
            eventCondition:["ActionId:11081"],
            suppress:COMMON_INTERVAL,
            userControl:false)]
    
        public void 究极神兵_爆击之究极幻想_中高压_范围清除(Event @event,ScriptAccessory accessory) {

            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=4&&!skipPhaseChecks) {

                return;

            }
            
            accessory.Method.RemoveDraw(@"^究极神兵_爆击之究极幻想_中高压_范围_.*$");
            
            phase1_mesohighDrawingCounter.Clear();
        
        }
        
        [ScriptMethod(name:"究极神兵 爆击之究极幻想 以太炸弹出现 (数据收集)",
            eventType:EventTypeEnum.AddCombatant,
            eventCondition:["DataId:8735"],
            userControl:false)]

        public void 究极神兵_爆击之究极幻想_以太炸弹出现_数据收集(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=4&&!skipPhaseChecks) {

                return;

            }

            if(phase5sub4_existingAetheroplasm.Count>=4) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }

            bool elementDoesntExist=false;

            lock(phase5sub4_existingAetheroplasm) {

                elementDoesntExist=phase5sub4_existingAetheroplasm.Add(sourceId);

                if(elementDoesntExist) {
                    
                    switch(phase5sub4_existingAetheroplasm.Count) {

                        case 1: {

                            phase5sub4_aetheroplasmAppearance1Semaphore.Set();

                            break;

                        }

                        case 2: {

                            phase5sub4_aetheroplasmAppearance234Semaphore.Set();

                            break;

                        }
                        
                        case 3: {

                            phase5sub4_aetheroplasmAppearance234Semaphore.Set();

                            break;

                        }
                        
                        case 4: {

                            phase5sub4_aetheroplasmAppearance234Semaphore.Set();
                            phase5sub4_aetheroplasmAppearance4Semaphore.Set();

                            break;

                        }

                        default: {

                            break;

                        }
                    
                    }
                    
                }

            }
            
            if(enableDebugLogging) {
                
                accessory.Log.Debug($"sourceId={sourceId}\nelementDoesntExist={elementDoesntExist}\nphase5sub4_existingAetheroplasm.Count={phase5sub4_existingAetheroplasm.Count}");
                
            }

        }
        
        [ScriptMethod(name:"究极神兵 爆击之究极幻想 以太炸弹引爆 (数据收集)",
            eventType:EventTypeEnum.ActionEffect,
            eventCondition:["ActionId:11137"],
            userControl:false)]

        public void 究极神兵_爆击之究极幻想_以太炸弹引爆_数据收集(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=4&&!skipPhaseChecks) {

                return;

            }
            
            if(!string.Equals(@event["TargetIndex"],"1")) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }
            
            var sourceObject=accessory.Data.Objects.SearchById(sourceId);

            if(sourceObject==null) {

                return;

            }

            if(sourceObject.DataId!=8735) {

                return;

            }
            
            accessory.Method.RemoveDraw($"究极神兵_爆击之究极幻想_以太炸弹_指路1_{sourceId}");
            accessory.Method.RemoveDraw($"究极神兵_爆击之究极幻想_以太炸弹_指路2_{sourceId}");
            
            if(phase5sub4_aetheroplasmDetonationCounter>=4) {

                return;

            }

            bool elementExists=false;

            lock(phase5sub4_existingAetheroplasm) {

                if(phase5sub4_existingAetheroplasm.Contains(sourceId)) {
                    
                    elementExists=true;
                    
                    Interlocked.Increment(ref phase5sub4_aetheroplasmDetonationCounter);
                    
                    switch(phase5sub4_aetheroplasmDetonationCounter) {

                        case 1: {

                            phase5sub4_aetheroplasmDetonation1Semaphore.Set();

                            break;

                        }
                        
                        case 2: {

                            phase5sub4_aetheroplasmDetonation2Semaphore.Set();

                            break;

                        }
                        
                        case 3: {

                            phase5sub4_aetheroplasmDetonation3Semaphore.Set();

                            break;

                        }
                        
                        case 4: {

                            phase5sub4_aetheroplasmDetonation4Semaphore.Set();

                            break;

                        }

                        default: {

                            break;

                        }
                    
                    }
                    
                }

            }
            
            if(enableDebugLogging) {
                
                accessory.Log.Debug($"sourceId={sourceId}\nelementExists={elementExists}\nphase5sub4_aetheroplasmDetonationCounter={phase5sub4_aetheroplasmDetonationCounter}");
                
            }

        }
        
        [ScriptMethod(name:"究极神兵 爆击之究极幻想 以太炸弹消失 (指路清除)",
            eventType:EventTypeEnum.RemoveCombatant,
            eventCondition:["DataId:8735"],
            userControl:false)]

        public void 究极神兵_爆击之究极幻想_以太炸弹消失_指路清除(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=4&&!skipPhaseChecks) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }
            
            accessory.Method.RemoveDraw($"究极神兵_爆击之究极幻想_以太炸弹_指路1_{sourceId}");
            accessory.Method.RemoveDraw($"究极神兵_爆击之究极幻想_以太炸弹_指路2_{sourceId}");

        }
        
        [ScriptMethod(name:"究极神兵 爆击之究极幻想 (起始指路)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11596"])]

        public void 究极神兵_爆击之究极幻想_起始指路(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }
            
            bool signalled=phase5sub4_ultimateAnnihilationSemaphore.WaitOne(COMMON_INTERVAL);

            if(!signalled) {

                return;

            }
            
            if(phase!=4&&!skipPhaseChecks) {

                return;

            }

            Vector3 leftPosition=new Vector3(94,0,96);
            Vector3 rightPosition=new Vector3(101,0,96);
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetPosition=leftPosition;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=11875;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
            
            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(2,7);
            currentProperties.Position=leftPosition;
            currentProperties.TargetPosition=rightPosition;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.Delay=11875;
            currentProperties.DestoryAt=3000;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Arrow,currentProperties);
            
            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(2,7);
            currentProperties.Position=rightPosition;
            currentProperties.TargetPosition=leftPosition;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.Delay=14875;
            currentProperties.DestoryAt=3000;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Arrow,currentProperties);
            
            int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            
            if(!isLegalPartyIndex(myIndex)) {

                return;

            }

            if(myIndex==7) {
                
                currentProperties=accessory.Data.GetDefaultDrawProperties();
                
                currentProperties.Scale=new(10.75f);
                currentProperties.InnerScale=new(8.75f);
                currentProperties.Radian=((float)convertDegreesToRadians(151.045));
                currentProperties.Position=new Vector3(100,0,119.5f);
                currentProperties.TargetPosition=new Vector3(100,0,118.5f);
                currentProperties.Color=accessory.Data.DefaultSafeColor;
                currentProperties.Delay=17875;
                currentProperties.DestoryAt=2250;
        
                accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Donut,currentProperties);
                
                currentProperties=accessory.Data.GetDefaultDrawProperties();

                currentProperties.Scale=new(2);
                currentProperties.Owner=accessory.Data.Me;
                currentProperties.TargetPosition=leftPosition;
                currentProperties.ScaleMode|=ScaleMode.YByDistance;
                currentProperties.Color=accessory.Data.DefaultSafeColor;
                currentProperties.Delay=20125;
                currentProperties.DestoryAt=2500;
            
                accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
                
            }
            
            if(myIndex==2||myIndex==3) {
                
                currentProperties=accessory.Data.GetDefaultDrawProperties();

                currentProperties.Name="究极神兵_爆击之究极幻想_起始指路_治疗";
                currentProperties.Scale=new(2);
                currentProperties.Owner=accessory.Data.Me;
                currentProperties.TargetPosition=rotatePosition(new Vector3(100,0,81.5f),ARENA_CENTER,-(Math.PI/8*3));
                currentProperties.ScaleMode|=ScaleMode.YByDistance;
                currentProperties.Color=accessory.Data.DefaultSafeColor;
                currentProperties.Delay=17875;
                currentProperties.DestoryAt=4750;
            
                accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
                
            }

            if(myIndex==4||myIndex==5||myIndex==6) {
                
                currentProperties=accessory.Data.GetDefaultDrawProperties();

                currentProperties.Scale=new(2);
                currentProperties.Owner=accessory.Data.Me;
                currentProperties.TargetPosition=new Vector3(94,0,89);
                currentProperties.ScaleMode|=ScaleMode.YByDistance;
                currentProperties.Color=accessory.Data.DefaultSafeColor;
                currentProperties.Delay=17875;
                currentProperties.DestoryAt=4750;
            
                accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
                
            }

        }
        
        [ScriptMethod(name:"究极神兵 爆击之究极幻想 灼热咆哮 (指路)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11099"])]

        public void 究极神兵_爆击之究极幻想_灼热咆哮_指路(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }
            
            if(phase!=4&&!skipPhaseChecks) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["TargetId"], out var targetId)) {
                
                return;
                
            }

            if(targetId!=accessory.Data.Me) {

                return;

            }
            
            accessory.Method.RemoveDraw("究极神兵_爆击之究极幻想_起始指路_治疗");
            
            Vector3 standbyPosition=rotatePosition(new Vector3(100,0,81.5f),ARENA_CENTER,-(Math.PI/4*3));
            Vector3 destination=new Vector3(100,0,118.5f);
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetPosition=standbyPosition;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.DestoryAt=1125;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
            
            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(2);
            currentProperties.Position=standbyPosition;
            currentProperties.TargetPosition=destination;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.DestoryAt=1125;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
            
            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetPosition=standbyPosition;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.Delay=1125;
            currentProperties.DestoryAt=2500;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
            
            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(2);
            currentProperties.Position=standbyPosition;
            currentProperties.TargetPosition=destination;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.Delay=1125;
            currentProperties.DestoryAt=2500;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);

        }
        
        [ScriptMethod(name:"究极神兵 爆击之究极幻想 第一个以太炸弹 (指路,坦克与D1)",
            eventType:EventTypeEnum.AddCombatant,
            eventCondition:["DataId:8735"])]

        public void 究极神兵_爆击之究极幻想_第一个以太炸弹_指路_仅坦克(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=4&&!skipPhaseChecks) {

                return;

            }
            
            int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            
            if(!isLegalPartyIndex(myIndex)) {

                return;

            }

            // 坦克与D1都参与处理第一颗炸弹。
            if(!isTank(myIndex)&&myIndex!=4) {

                return;

            }

            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {

                return;

            }

            bool signalled=phase5sub4_aetheroplasmAppearance1Semaphore.WaitOne(COMMON_INTERVAL);

            if(!signalled) {

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Name=$"究极神兵_爆击之究极幻想_以太炸弹_指路1_{sourceId}";
            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetObject=sourceId;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=MAXIMUM_DURATION;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
            
            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Name=$"究极神兵_爆击之究极幻想_以太炸弹_指路2_{sourceId}";
            currentProperties.Scale=new(1);
            currentProperties.Owner=sourceId;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=MAXIMUM_DURATION;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Circle,currentProperties);

        }
        
        [ScriptMethod(name:"究极神兵 爆击之究极幻想 第一个以太炸弹引爆 (指路,坦克与D1)",
            eventType:EventTypeEnum.ActionEffect,
            eventCondition:["ActionId:11137"])]

        public void 究极神兵_爆击之究极幻想_第一个以太炸弹引爆_指路_仅坦克(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=4&&!skipPhaseChecks) {

                return;

            }
            
            if(!string.Equals(@event["TargetIndex"],"1")) {

                return;

            }
            
            int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            
            if(!isLegalPartyIndex(myIndex)) {

                return;

            }

            // 坦克与D1引爆后都被指向东北内侧(106,89)。
            if(!isTank(myIndex)&&myIndex!=4) {

                return;

            }

            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {

                return;

            }

            var sourceObject=accessory.Data.Objects.SearchById(sourceId);

            if(sourceObject==null) {

                return;

            }

            if(sourceObject.DataId!=8735) {

                return;

            }
            
            bool signalled=phase5sub4_aetheroplasmDetonation1Semaphore.WaitOne(8750);

            if(!signalled) {

                return;

            }

            if(phase5sub4_existingAetheroplasm.Count>=2) {

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Name="究极神兵_爆击之究极幻想_第一个以太炸弹引爆_指路_仅坦克";
            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetPosition=new Vector3(106,0,89);
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=6250;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);

        }
        
        [ScriptMethod(name:"究极神兵 爆击之究极幻想 第一个以太炸弹引爆 (指路清除,坦克与D1)",
            eventType:EventTypeEnum.PlayActionTimeline,
            eventCondition:["SourceDataId:8722"],
            userControl:false)]

        public void 究极神兵_爆击之究极幻想_第一个以太炸弹引爆_指路清除_仅坦克(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=4&&!skipPhaseChecks) {

                return;

            }
            
            if(!string.Equals(@event["Id"],"7738")) {

                return;

            }
            
            bool signalled=phase5sub4_featherRainSemaphore2.WaitOne(COMMON_INTERVAL);

            if(!signalled) {

                return;

            }
            
            System.Threading.Tasks.Task.Delay(500).ContinueWith(_=> {
                
                accessory.Method.RemoveDraw("究极神兵_爆击之究极幻想_第一个以太炸弹引爆_指路_仅坦克");
                
            });

        }
        
        [ScriptMethod(name:"究极神兵 爆击之究极幻想 飞翎雨 (数据收集与范围清除)",
            eventType:EventTypeEnum.PlayActionTimeline,
            eventCondition:["SourceDataId:8722"],
            userControl:false)]

        public void 究极神兵_爆击之究极幻想_飞翎雨_数据收集与范围清除(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=4&&!skipPhaseChecks) {

                return;

            }
            
            if(!string.Equals(@event["Id"],"7738")) {

                return;

            }

            if(phase5sub4_featherRainCounter>=2) {

                return;

            }

            lock(phase5sub4_featherRainSemaphore1) {

                Interlocked.Increment(ref phase5sub4_featherRainCounter);

                if(phase5sub4_featherRainCounter==1) {

                    phase5sub4_featherRainSemaphore1.Set();
                    phase5sub4_featherRainSemaphore2.Set();

                }

                if(phase5sub4_featherRainCounter==2) {

                    System.Threading.Tasks.Task.Delay(500).ContinueWith(_ => {
                        
                        accessory.Method.RemoveDraw("究极神兵_爆击之究极幻想_第四个以太炸弹引爆_指路_仅ST");
                        
                    });
                    
                }

            }
            
            if(enableDebugLogging) {
                
                accessory.Log.Debug($"phase5sub4_featherRainCounter={phase5sub4_featherRainCounter}");
                
            }

        }
        
        [ScriptMethod(name:"究极神兵 爆击之究极幻想 第一次飞翎雨 (指路)",
            eventType:EventTypeEnum.PlayActionTimeline,
            eventCondition:["SourceDataId:8722"])]

        public void 究极神兵_爆击之究极幻想_第一次飞翎雨_指路(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=4&&!skipPhaseChecks) {

                return;

            }
            
            if(!string.Equals(@event["Id"],"7738")) {

                return;

            }

            bool signalled=phase5sub4_featherRainSemaphore1.WaitOne(COMMON_INTERVAL);

            if(!signalled) {

                return;

            }
            
            int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            
            if(!isLegalPartyIndex(myIndex)) {

                return;

            }

            Vector3 myPosition=ARENA_CENTER;
            int myDuration=0;

            // MT、ST、D1一同短暂前往北侧小点,其余人再按灼热状态分流南北。
            if(myIndex==0||myIndex==1||myIndex==4) {

                myPosition=new Vector3(102,0,96);
                myDuration=1750;

            }

            else {

                if(partyMembersWithSearingWind.Contains(accessory.Data.Me)) {
                    
                    myPosition=new Vector3(100,0,118.5f);
                    
                }

                else {

                    myPosition=new Vector3(100,0,81.5f);

                }

                myDuration=6500;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetPosition=myPosition;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.Delay=500;
            currentProperties.DestoryAt=myDuration;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);

        }
        
        [ScriptMethod(name:"究极神兵 爆击之究极幻想 第二三四个以太炸弹 (指路)",
            eventType:EventTypeEnum.AddCombatant,
            eventCondition:["DataId:8735"])]

        public void 究极神兵_爆击之究极幻想_第二三四个以太炸弹_指路_仅ST(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=4&&!skipPhaseChecks) {

                return;

            }
            
            int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            
            if(!isLegalPartyIndex(myIndex)) {

                return;

            }

            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {

                return;

            }

            bool signalled=phase5sub4_aetheroplasmAppearance234Semaphore.WaitOne(COMMON_INTERVAL);

            if(!signalled) {

                return;

            }

            int bombNumber;

            lock(phase5sub4_existingAetheroplasm) {

                bombNumber=phase5sub4_existingAetheroplasm.Count;

            }

            // 第二颗炸弹:MT、ST、D1都有指路;第三、四颗:仅MT。
            if(bombNumber==2) {

                if(myIndex!=0&&myIndex!=1&&myIndex!=4) {

                    return;

                }

            }

            else {

                if(myIndex!=0) {

                    return;

                }

            }

            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Name=$"究极神兵_爆击之究极幻想_以太炸弹_指路1_{sourceId}";
            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetObject=sourceId;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=MAXIMUM_DURATION;

            if(bombNumber==3) {

                currentProperties.Delay=875;

            }
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
            
            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Name=$"究极神兵_爆击之究极幻想_以太炸弹_指路2_{sourceId}";
            currentProperties.Scale=new(1);
            currentProperties.Owner=sourceId;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=MAXIMUM_DURATION;
            
            if(bombNumber==3) {

                currentProperties.Delay=875;

            }
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Circle,currentProperties);

        }
        
        [ScriptMethod(name:"究极神兵 爆击之究极幻想 第二个以太炸弹引爆 (指路,MT/ST/D1)",
            eventType:EventTypeEnum.ActionEffect,
            eventCondition:["ActionId:11137"])]

        public void 究极神兵_爆击之究极幻想_第二个以太炸弹引爆_指路_仅ST(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=4&&!skipPhaseChecks) {

                return;

            }
            
            if(!string.Equals(@event["TargetIndex"],"1")) {

                return;

            }
            
            int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            
            if(!isLegalPartyIndex(myIndex)) {

                return;

            }

            // 第二颗引爆后MT、ST、D1都被指向正北场边(100,81.5)。
            if(myIndex!=0&&myIndex!=1&&myIndex!=4) {

                return;

            }

            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {

                return;

            }

            var sourceObject=accessory.Data.Objects.SearchById(sourceId);

            if(sourceObject==null) {

                return;

            }

            if(sourceObject.DataId!=8735) {

                return;

            }

            bool signalled=phase5sub4_aetheroplasmDetonation2Semaphore.WaitOne(8500);

            if(!signalled) {

                return;

            }

            if(phase5sub4_existingAetheroplasm.Count>=3) {

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Name="究极神兵_爆击之究极幻想_第二个以太炸弹引爆_指路_仅ST";
            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetPosition=new Vector3(100,0,81.5f);
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=6000;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);

        }
        
        [ScriptMethod(name:"究极神兵 爆击之究极幻想 第二个以太炸弹引爆 (指路清除,MT/ST/D1)",
            eventType:EventTypeEnum.ActionEffect,
            eventCondition:["ActionId:11103"],
            suppress:COMMON_INTERVAL,
            userControl:false)]

        public void 究极神兵_爆击之究极幻想_第二个以太炸弹引爆_指路清除_仅ST(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=4&&!skipPhaseChecks) {

                return;

            }
            
            accessory.Method.RemoveDraw("究极神兵_爆击之究极幻想_第二个以太炸弹引爆_指路_仅ST");

        }
        
        [ScriptMethod(name:"究极神兵 爆击之究极幻想 深红旋风 (范围)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11103"])]

        public void 究极神兵_爆击之究极幻想_深红旋风_范围(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=4&&!skipPhaseChecks) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }

            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(18,44);
            currentProperties.Owner=sourceId;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.DestoryAt=3000;
        
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Rect,currentProperties);
            
            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(10,44);
            currentProperties.Position=ARENA_CENTER;
            currentProperties.TargetPosition=new Vector3(ARENA_CENTER.X,ARENA_CENTER.Y,ARENA_CENTER.Z-1);
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.Delay=3000;
            currentProperties.DestoryAt=2000;
        
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Straight,currentProperties);
            
            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(10,44);
            currentProperties.Position=ARENA_CENTER;
            currentProperties.TargetPosition=new Vector3(ARENA_CENTER.X+1,ARENA_CENTER.Y,ARENA_CENTER.Z);
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.Delay=3000;
            currentProperties.DestoryAt=2000;
        
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Straight,currentProperties);
            
        }
        
        [ScriptMethod(name:"究极神兵 爆击之究极幻想 (深红旋风后指路,除MT与有灼热状态的队员)",
            eventType:EventTypeEnum.ActionEffect,
            eventCondition:["ActionId:11103"],
            suppress:COMMON_INTERVAL)]

        public void 究极神兵_爆击之究极幻想_深红旋风后指路_除ST与有灼热状态的队员(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=4&&!skipPhaseChecks) {

                return;

            }
            
            int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            
            if(!isLegalPartyIndex(myIndex)) {

                return;

            }

            // MT有独立路线,这条适用于其余无灼热状态的队员(含ST)。
            if(myIndex==0) {

                return;

            }

            if(partyMembersWithSearingWind.Contains(accessory.Data.Me)) {

                return;

            }

            Vector3 position1=rotatePosition(new Vector3(100,0,81.5f),ARENA_CENTER,-(Math.PI/4));
            Vector3 position2=rotatePosition(new Vector3(100,0,89.5f),ARENA_CENTER,-(Math.PI/4));
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetPosition=position1;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=2750;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
            
            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(2);
            currentProperties.Position=position1;
            currentProperties.TargetPosition=position2;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.DestoryAt=2750;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
            
            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetPosition=position2;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.Delay=2750;
            currentProperties.DestoryAt=7500;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);

        }
        
        [ScriptMethod(name:"究极神兵 爆击之究极幻想 (深红旋风后指路,仅有灼热状态的队员)",
            eventType:EventTypeEnum.ActionEffect,
            eventCondition:["ActionId:11103"],
            suppress:COMMON_INTERVAL)]

        public void 究极神兵_爆击之究极幻想_深红旋风后指路_仅有灼热状态的队员(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=4&&!skipPhaseChecks) {

                return;

            }

            if(!partyMembersWithSearingWind.Contains(accessory.Data.Me)) {

                return;

            }

            Vector3 position1=rotatePosition(new Vector3(100,0,81.5f),ARENA_CENTER,(Math.PI/4)*phase5sub4_discretizedIfritPosition);
            Vector3 position2=new Vector3(100,0,110.5f);
            Vector3 position3=new Vector3(100,0,118.5f);
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetPosition=position1;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=2750;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
            
            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(2);
            currentProperties.Position=position1;
            currentProperties.TargetPosition=position2;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.DestoryAt=2750;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
            
            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetPosition=position2;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.Delay=2750;
            currentProperties.DestoryAt=3000;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
            
            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(2);
            currentProperties.Position=position2;
            currentProperties.TargetPosition=position3;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.Delay=2750;
            currentProperties.DestoryAt=3000;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
            
            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetPosition=position3;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.Delay=5750;
            currentProperties.DestoryAt=4500;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);

        }
        
        [ScriptMethod(name:"究极神兵 爆击之究极幻想 (深红旋风后指路,仅MT)",
            eventType:EventTypeEnum.ActionEffect,
            eventCondition:["ActionId:11103"],
            suppress:COMMON_INTERVAL)]

        public void 究极神兵_爆击之究极幻想_深红旋风后指路_仅ST(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=4&&!skipPhaseChecks) {

                return;

            }
            
            int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            
            if(!isLegalPartyIndex(myIndex)) {

                return;

            }

            // 深红旋风后按伊弗利特东西半场分位,改为仅MT。
            if(myIndex!=0) {

                return;

            }

            Vector3 myPosition=ARENA_CENTER;

            if(new List<int>{1,2,3}.Contains(phase5sub4_discretizedIfritPosition)) {

                myPosition=new Vector3(107,0,93);

            }
            
            if(new List<int>{5,6,7}.Contains(phase5sub4_discretizedIfritPosition)) {

                myPosition=new Vector3(93,0,93);

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetPosition=myPosition;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=2000;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);

        }
        
        [ScriptMethod(name:"究极神兵 爆击之究极幻想 第三个以太炸弹引爆 (指路,仅MT)",
            eventType:EventTypeEnum.ActionEffect,
            eventCondition:["ActionId:11137"])]

        public void 究极神兵_爆击之究极幻想_第三个以太炸弹引爆_指路_仅ST(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=4&&!skipPhaseChecks) {

                return;

            }
            
            if(!string.Equals(@event["TargetIndex"],"1")) {

                return;

            }
            
            int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            
            if(!isLegalPartyIndex(myIndex)) {

                return;

            }

            // 第三颗引爆后仅MT被指向正北内圈(100,89.5)。
            if(myIndex!=0) {

                return;

            }

            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {

                return;

            }

            var sourceObject=accessory.Data.Objects.SearchById(sourceId);

            if(sourceObject==null) {

                return;

            }

            if(sourceObject.DataId!=8735) {

                return;

            }

            bool signalled=phase5sub4_aetheroplasmDetonation3Semaphore.WaitOne(8500);

            if(!signalled) {

                return;

            }

            if(phase5sub4_existingAetheroplasm.Count>=4) {

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Name="究极神兵_爆击之究极幻想_第三个以太炸弹引爆_指路_仅ST";
            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetPosition=new Vector3(100,0,89.5f);
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=6000;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);

        }
        
        [ScriptMethod(name:"究极神兵 爆击之究极幻想 第三个以太炸弹引爆 (指路清除,仅MT)",
            eventType:EventTypeEnum.AddCombatant,
            eventCondition:["DataId:8735"],
            userControl:false)]

        public void 究极神兵_爆击之究极幻想_第三个以太炸弹引爆_指路清除_仅ST(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=4&&!skipPhaseChecks) {

                return;

            }
            
            bool signalled=phase5sub4_aetheroplasmAppearance4Semaphore.WaitOne(COMMON_INTERVAL);

            if(!signalled) {

                return;

            }
            
            accessory.Method.RemoveDraw("究极神兵_爆击之究极幻想_第三个以太炸弹引爆_指路_仅ST");

        }
        
        [ScriptMethod(name:"究极神兵 爆击之究极幻想 第四个以太炸弹引爆 (指路,仅MT)",
            eventType:EventTypeEnum.ActionEffect,
            eventCondition:["ActionId:11137"])]

        public void 究极神兵_爆击之究极幻想_第四个以太炸弹引爆_指路_仅ST(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=4&&!skipPhaseChecks) {

                return;

            }
            
            if(!string.Equals(@event["TargetIndex"],"1")) {

                return;

            }
            
            int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            
            if(!isLegalPartyIndex(myIndex)) {

                return;

            }

            // 第四颗引爆后仅MT被指向东北内圈(106,89)。
            if(myIndex!=0) {

                return;

            }

            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {

                return;

            }

            var sourceObject=accessory.Data.Objects.SearchById(sourceId);

            if(sourceObject==null) {

                return;

            }

            if(sourceObject.DataId!=8735) {

                return;

            }

            bool signalled=phase5sub4_aetheroplasmDetonation4Semaphore.WaitOne(8500);

            if(!signalled) {

                return;

            }

            if(phase5sub4_featherRainCounter>=2) {

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Name="究极神兵_爆击之究极幻想_第四个以太炸弹引爆_指路_仅ST";
            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetPosition=new Vector3(106,0,89);
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=3625;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);

        }
        
        [ScriptMethod(name:"究极神兵 乱击之究极幻想 (阶段控制)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11597"],
            userControl:false)]

        public void 究极神兵_乱击之究极幻想_阶段控制(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=4&&!skipPhaseChecks) {

                return;

            }

            phase1_mesohighDrawingCounter.Clear();
            
            phase=5;

            phase5sub5_ultimateSuppressionSemaphore.Set();
            
            if(enableDebugLogging) {
                
                accessory.Log.Debug($"majorPhase={majorPhase}\nphase={phase}");
                
            }

        }
        
        [ScriptMethod(name:"究极神兵 乱击之究极幻想 刚羽 (范围)",
            eventType:EventTypeEnum.AddCombatant,
            eventCondition:["DataId:8724"])]

        public void 究极神兵_乱击之究极幻想_刚羽_范围(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=5&&!skipPhaseChecks) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();
                
            currentProperties.Name=$"究极神兵_乱击之究极幻想_刚羽_范围1_{sourceId}";
            currentProperties.Scale=new(0.5f);
            currentProperties.Owner=sourceId;
            currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(1);
            currentProperties.DestoryAt=32875;
        
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Circle,currentProperties);
                    
            currentProperties=accessory.Data.GetDefaultDrawProperties();
            
            currentProperties.Name=$"究极神兵_乱击之究极幻想_刚羽_范围2_{sourceId}";
            currentProperties.Scale=new(1,3);
            currentProperties.Owner=sourceId;
            currentProperties.Color=colourOfExtremelyDangerousAttacks.V4.WithW(1);
            currentProperties.DestoryAt=32875;
        
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Arrow,currentProperties);

        }
        
        [ScriptMethod(name:"究极神兵 乱击之究极幻想 刚羽 (范围清除)",
            eventType:EventTypeEnum.RemoveCombatant,
            eventCondition:["DataId:8724"],
            userControl:false)]

        public void 究极神兵_乱击之究极幻想_刚羽_范围清除(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase<5&&!skipPhaseChecks) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }
            
            accessory.Method.RemoveDraw($"究极神兵_乱击之究极幻想_刚羽_范围1_{sourceId}");
            accessory.Method.RemoveDraw($"究极神兵_乱击之究极幻想_刚羽_范围2_{sourceId}");

        }
        
        [ScriptMethod(name:"究极神兵 乱击之究极幻想 (起始指路)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11597"])]

        public void 究极神兵_乱击之究极幻想_起始指路(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }
            
            int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            
            if(!isLegalPartyIndex(myIndex)) {

                return;

            }
            
            bool signalled=phase5sub5_ultimateSuppressionSemaphore.WaitOne(COMMON_INTERVAL);

            if(!signalled) {

                return;

            }
            
            if(phase!=5&&!skipPhaseChecks) {

                return;

            }

            Vector3 myPosition=ARENA_CENTER;
            int myDuration=0;

            if(isTank(myIndex)) {

                // MT、ST都站西南(原版MT在东南107.414,107.414)。
                myPosition=new Vector3(93.636f,0,106.364f);

                myDuration=17875;

            }

            else {

                // 北→西弧线站位,值k=北偏西18°×k:H1正北,D3=18°,D4=36°,H2=54°,D2=72°,D1正西。
                int[] discretizedRotation=[-1,-1,0,3,5,4,1,2];

                if(discretizedRotation[myIndex]==-1) {

                    return;

                }

                myPosition=rotatePosition(new Vector3(100,0,81.5f),ARENA_CENTER,-(Math.PI/10*discretizedRotation[myIndex]));
                
                myDuration=11750;
                
            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetPosition=myPosition;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=myDuration;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);

        }
        
        [ScriptMethod(name:"究极神兵 乱击之究极幻想 地火喷发 (数据收集)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11098"],
            userControl:false)]

        public void 究极神兵_乱击之究极幻想_地火喷发_数据收集(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase5sub5_eruptionCounter>=3) {

                return;

            }
            
            Vector3 effectPosition=ARENA_CENTER;

            try {

                effectPosition=JsonConvert.DeserializeObject<Vector3>(@event["EffectPosition"]);

            } catch(Exception e) {
                
                accessory.Log.Error("EffectPosition deserialization failed.");

                return;

            }

            int potentialTarget=-1;
            double shortestDistance=double.PositiveInfinity;

            for(int i=0;i<8;++i) {
                
                var targetObject=accessory.Data.Objects.SearchById(accessory.Data.PartyList[i]);

                if(targetObject==null) {

                    continue;

                }

                double currentDistance=Vector3.Distance(effectPosition,targetObject.Position);

                if(currentDistance<shortestDistance) {

                    potentialTarget=i;
                    shortestDistance=currentDistance;

                }

            }

            if(!isLegalPartyIndex(potentialTarget)) {

                return;

            }

            lock(phase5sub5_isEruption) {

                phase5sub5_isEruption[potentialTarget]=true;

                Interlocked.Increment(ref phase5sub5_eruptionCounter);

                if(phase5sub5_eruptionCounter==3) {

                    phase5sub5_eruptionSemaphore.Set();

                    if(enableDebugLogging) {
                        
                        accessory.Log.Debug($"""
                                             phase5sub5_eruptionCounter={phase5sub5_eruptionCounter}
                                             phase5sub5_isEruption:{string.Join(",",phase5sub5_isEruption)}
                                             """);
                        
                    }

                }

            }
            
            if(enableDebugLogging) {
                
                accessory.Log.Debug($"effectPosition={effectPosition}\npotentialTarget={potentialTarget}\nshortestDistance={shortestDistance}");
                        
            }

        }
        
        [ScriptMethod(name:"究极神兵 乱击之究极幻想 地火喷发 (指路,除坦克)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11097"])]

        public void 究极神兵_乱击之究极幻想_地火喷发_指路_除坦克(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=5&&!skipPhaseChecks) {

                return;

            }
            
            int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            
            if(!isLegalPartyIndex(myIndex)) {

                return;

            }
            
            if(isTank(myIndex)) {

                return;

            }
            
            bool signalled=phase5sub5_eruptionSemaphore.WaitOne(COMMON_INTERVAL);

            if(!signalled) {

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            if(phase5sub5_isEruption[myIndex]) {
                
                // 北→西弧线站位,值k=北偏西18°×k:H1正北,D3=18°,D4=36°,H2=54°,D2=72°,D1正西。
                int[] discretizedRotation=[-1,-1,0,3,5,4,1,2];

                if(discretizedRotation[myIndex]==-1) {

                    return;

                }

                Vector3 position1=rotatePosition(new Vector3(100,0,90.75f),ARENA_CENTER,-(Math.PI/10*discretizedRotation[myIndex]));
                Vector3 position2=ARENA_CENTER;
                Vector3 position3=new Vector3(107.414f,0,107.414f);
                
                currentProperties=accessory.Data.GetDefaultDrawProperties();

                currentProperties.Scale=new(2);
                currentProperties.Owner=accessory.Data.Me;
                currentProperties.TargetPosition=position1;
                currentProperties.ScaleMode|=ScaleMode.YByDistance;
                currentProperties.Color=accessory.Data.DefaultSafeColor;
                currentProperties.DestoryAt=2000;
            
                accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
            
                currentProperties=accessory.Data.GetDefaultDrawProperties();

                currentProperties.Scale=new(2);
                currentProperties.Position=position1;
                currentProperties.TargetPosition=position2;
                currentProperties.ScaleMode|=ScaleMode.YByDistance;
                currentProperties.Color=accessory.Data.DefaultDangerColor;
                currentProperties.DestoryAt=2000;
            
                accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
            
                currentProperties=accessory.Data.GetDefaultDrawProperties();

                currentProperties.Scale=new(2);
                currentProperties.Owner=accessory.Data.Me;
                currentProperties.TargetPosition=position2;
                currentProperties.ScaleMode|=ScaleMode.YByDistance;
                currentProperties.Color=accessory.Data.DefaultSafeColor;
                currentProperties.Delay=2000;
                currentProperties.DestoryAt=2000;
            
                accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
            
                currentProperties=accessory.Data.GetDefaultDrawProperties();

                currentProperties.Scale=new(2);
                currentProperties.Position=position2;
                currentProperties.TargetPosition=position3;
                currentProperties.ScaleMode|=ScaleMode.YByDistance;
                currentProperties.Color=accessory.Data.DefaultDangerColor;
                currentProperties.Delay=2000;
                currentProperties.DestoryAt=2000;
            
                accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
                
                currentProperties=accessory.Data.GetDefaultDrawProperties();

                currentProperties.Scale=new(2);
                currentProperties.Owner=accessory.Data.Me;
                currentProperties.TargetPosition=position3;
                currentProperties.ScaleMode|=ScaleMode.YByDistance;
                currentProperties.Color=accessory.Data.DefaultSafeColor;
                currentProperties.Delay=4000;
                currentProperties.DestoryAt=2000;
            
                accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
                
            }

            else {
                
                currentProperties=accessory.Data.GetDefaultDrawProperties();

                currentProperties.Scale=new(2);
                currentProperties.Owner=accessory.Data.Me;
                currentProperties.TargetObject=accessory.Data.PartyList[1];
                currentProperties.ScaleMode|=ScaleMode.YByDistance;
                currentProperties.Color=accessory.Data.DefaultSafeColor;
                currentProperties.DestoryAt=6125;
            
                accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
                
                currentProperties=accessory.Data.GetDefaultDrawProperties();

                currentProperties.Scale=new(3);
                currentProperties.Radian=float.Pi/2; 
                currentProperties.Owner=accessory.Data.PartyList[1];
                currentProperties.FixRotation=true;
                currentProperties.Rotation=-(float.Pi/4);
                currentProperties.Color=accessory.Data.DefaultSafeColor;
                currentProperties.DestoryAt=6125;
            
                accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Fan,currentProperties);
                
            }

        }
        
        [ScriptMethod(name:"究极神兵 乱击之究极幻想 寒风之歌 (西南方向指示,MT与ST)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11097"])]

        public void 究极神兵_乱击之究极幻想_寒风之歌_西南方向指示_仅ST(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=5&&!skipPhaseChecks) {

                return;

            }
            
            int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            
            if(!isLegalPartyIndex(myIndex)) {

                return;

            }
            
            // MT、ST都显示朝西南的引导扇形。
            if(myIndex!=0&&myIndex!=1) {

                return;

            }

            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(3);
            currentProperties.Radian=float.Pi/2;
            currentProperties.Owner=accessory.Data.PartyList[1];
            currentProperties.FixRotation=true;
            currentProperties.Rotation=-(float.Pi/4);
            currentProperties.Color=colourOfDirectionIndicators.V4.WithW(1);
            currentProperties.DestoryAt=6125;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Fan,currentProperties);

        }
        
        [ScriptMethod(name:"究极神兵 乱击之究极幻想 寒风之歌 (数据收集)",
            eventType:EventTypeEnum.TargetIcon,
            eventCondition:["Id:0010"],
            userControl:false)]

        public void 究极神兵_乱击之究极幻想_寒风之歌_数据收集(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase5sub5_mistralSongCounter>=2) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["TargetId"], out var targetId)) {
                
                return;
                
            }

            int targetIndex=accessory.Data.PartyList.IndexOf(((uint)targetId));
            
            if(!isLegalPartyIndex(targetIndex)) {

                return;

            }

            lock(phase5sub5_isMistralSong) {

                phase5sub5_isMistralSong[targetIndex]=true;

                Interlocked.Increment(ref phase5sub5_mistralSongCounter);

                if(phase5sub5_mistralSongCounter==2) {

                    phase5sub5_mistralSongSemaphore.Set();

                    if(enableDebugLogging) {
                        
                        accessory.Log.Debug($"""
                                             phase5sub5_mistralSongCounter={phase5sub5_mistralSongCounter}
                                             phase5sub5_isMistralSong:{string.Join(",",phase5sub5_isMistralSong)}
                                             """);
                        
                    }

                }

            }

        }
        
        [ScriptMethod(name:"究极神兵 乱击之究极幻想 大龙卷风 (范围)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11073"])]

        public void 究极神兵_乱击之究极幻想_大龙卷风_范围(Event @event,ScriptAccessory accessory) {

            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=5&&!skipPhaseChecks) {

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

            currentProperties.Scale=new(8);
            currentProperties.Position=effectPosition;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.DestoryAt=3000;
            
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,currentProperties);

        }
        
        [ScriptMethod(name:"究极神兵 乱击之究极幻想 (地火喷发后指路)",
            eventType:EventTypeEnum.ActionEffect,
            eventCondition:["ActionId:11115"])]

        public void 究极神兵_乱击之究极幻想_地火喷发后指路(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=5&&!skipPhaseChecks) {

                return;

            }
            
            int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            
            if(!isLegalPartyIndex(myIndex)) {

                return;

            }
            
            // 仅三名地火目标(不再包含MT),且自己不是石牢点名。
            if(!phase5sub5_isEruption[myIndex]) {

                return;

            }

            if(!convertObjectIdToDecimal(@event["TargetId"], out var targetId)) {
                
                return;
                
            }

            if(targetId==accessory.Data.Me) {

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();
            
            currentProperties.Scale=new(2,9);
            currentProperties.Position=new Vector3(107.414f,0,107.414f);
            currentProperties.TargetPosition=new Vector3(107.414f,0,98.414f);
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.Delay=4000;
            currentProperties.DestoryAt=5500;
        
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Arrow,currentProperties);

        }
        
        [ScriptMethod(name:"究极神兵 乱击之究极幻想 (寒风之歌后指路)",
            eventType:EventTypeEnum.TargetIcon,
            eventCondition:["Id:0010"],
            suppress:COMMON_INTERVAL)]

        public void 究极神兵_乱击之究极幻想_寒风之歌后指路(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=5&&!skipPhaseChecks) {

                return;

            }
            
            int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            
            if(!isLegalPartyIndex(myIndex)) {

                return;

            }
            
            bool signalled=phase5sub5_mistralSongSemaphore.WaitOne(COMMON_INTERVAL);

            if(!signalled) {

                return;

            }

            if(phase5sub5_isEruption[myIndex]) {

                return;

            }

            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            // 寒风点名、ST、MT:从ST位置向场中移动;其他人:向西北内侧。
            if(phase5sub5_isMistralSong[myIndex]||myIndex==0||myIndex==1) {

                currentProperties=accessory.Data.GetDefaultDrawProperties();

                currentProperties.Scale=new(2,9);
                currentProperties.Position=new Vector3(93.636f,0,106.364f);
                currentProperties.TargetPosition=new Vector3(94.636f,0,105.364f);
                currentProperties.Color=accessory.Data.DefaultSafeColor;
                currentProperties.Delay=5250;
                currentProperties.DestoryAt=5500;
        
                accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Arrow,currentProperties);
                
            }

            else {
                
                currentProperties=accessory.Data.GetDefaultDrawProperties();
            
                currentProperties.Scale=new(2,9);
                currentProperties.Position=new Vector3(93.636f,0,106.364f);
                currentProperties.TargetPosition=new Vector3(92.636f,0,105.364f);
                currentProperties.Color=accessory.Data.DefaultSafeColor;
                currentProperties.Delay=7375;
                currentProperties.DestoryAt=4000;
        
                accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Arrow,currentProperties);
                
                /*
                
                currentProperties=accessory.Data.GetDefaultDrawProperties();
            
                currentProperties.Scale=new(11);
                currentProperties.InnerScale=new(9);
                currentProperties.Radian=float.Pi/4*3;
                currentProperties.Position=new Vector3(93.636f,0,106.364f);
                currentProperties.TargetPosition=new Vector3(93.636f,0,105.364f);
                currentProperties.Rotation=-float.Pi/8;
                currentProperties.Color=accessory.Data.DefaultSafeColor;
                currentProperties.Delay=7375;
                currentProperties.DestoryAt=4000;
        
                accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Donut,currentProperties);
                
                */
                
            }

        }
        
        [ScriptMethod(name:"究极神兵 乱击之究极幻想 花岗岩牢狱 (数据获取)",
            eventType:EventTypeEnum.ActionEffect,
            eventCondition:["ActionId:11115"],
            userControl:false)]

        public void 究极神兵_乱击之究极幻想_花岗岩牢狱_数据获取(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=5&&!skipPhaseChecks) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["TargetId"], out var targetId)) {
                
                return;
                
            }

            phase5sub5_rockThrowTarget=targetId;

            if(enableDebugLogging) {
                
                accessory.Log.Debug($"phase5sub5_rockThrowTarget={phase5sub5_rockThrowTarget}");
                
            }

        }
        
        [ScriptMethod(name:"究极神兵 乱击之究极幻想 花岗岩牢狱 (目标指示)",
            eventType:EventTypeEnum.Targetable,
            eventCondition:["DataId:8729"])]

        public void 究极神兵_乱击之究极幻想_花岗岩牢狱_目标指示(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=5&&!skipPhaseChecks) {

                return;

            }
            
            if(!string.Equals(@event["Targetable"],"True")) {

                return;

            }

            if(accessory.Data.Me==phase5sub5_rockThrowTarget) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Name="究极神兵_乱击之究极幻想_花岗岩牢狱_目标指示1";
            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetObject=sourceId;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=colourOfDirectionIndicators.V4.WithW(1);
            currentProperties.DestoryAt=7000;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
            
            currentProperties=accessory.Data.GetDefaultDrawProperties();
            
            currentProperties.Name="究极神兵_乱击之究极幻想_花岗岩牢狱_目标指示2";
            currentProperties.Scale=new(1.8f);
            currentProperties.Owner=sourceId;
            currentProperties.Color=colourOfDirectionIndicators.V4.WithW(1);
            currentProperties.DestoryAt=7000;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Circle,currentProperties);

        }
        
        [ScriptMethod(name:"究极神兵 乱击之究极幻想 花岗岩牢狱 (目标指示清除)",
            eventType:EventTypeEnum.CancelAction,
            eventCondition:["ActionId:11448"],
            userControl:false)]

        public void 究极神兵_乱击之究极幻想_花岗岩牢狱_目标指示清除(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=5&&!skipPhaseChecks) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }
            
            var sourceObject=accessory.Data.Objects.SearchById(sourceId);

            if(sourceObject==null) {

                return;

            }

            else {

                if(sourceObject.DataId!=8729) {

                    return;

                }
                
            }
            
            accessory.Method.RemoveDraw("究极神兵_乱击之究极幻想_花岗岩牢狱_目标指示1");
            accessory.Method.RemoveDraw("究极神兵_乱击之究极幻想_花岗岩牢狱_目标指示2");
            
        }
        
        [ScriptMethod(name:"究极神兵 乱击之究极幻想 魔科学激光 (精确范围)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:regex:^(11140|11141|11142)$"])]

        public void 究极神兵_乱击之究极幻想_魔科学激光_精确范围(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=5&&!skipPhaseChecks) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(8,40);
            currentProperties.Owner=sourceId;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.DestoryAt=3000;
            
            if(string.Equals(@event["ActionId"],"11141")) {

                currentProperties.Rotation=-(float.Pi/4);

            }
            
            if(string.Equals(@event["ActionId"],"11142")) {

                currentProperties.Rotation=float.Pi/4;

            }
        
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Rect,currentProperties);

        }
        
        [ScriptMethod(name:"究极神兵 乱击之究极幻想 中高压 (范围)",
            eventType:EventTypeEnum.Tether,
            eventCondition:["Id:0004"])]
    
        public void 究极神兵_乱击之究极幻想_中高压_范围(Event @event,ScriptAccessory accessory) {

            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=5&&!skipPhaseChecks) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }
            
            if(!convertObjectIdToDecimal(@event["TargetId"], out var targetId)) {
                
                return;
                
            }

            lock(phase1_mesohighDrawingCounter) {
                
                int lastDrawing=phase1_mesohighDrawingCounter.GetOrAdd(sourceId,0);
            
                accessory.Method.RemoveDraw($"究极神兵_乱击之究极幻想_中高压_范围_{sourceId}_{lastDrawing}");

                ++lastDrawing;
                phase1_mesohighDrawingCounter[sourceId]=lastDrawing;
            
                var currentProperties=accessory.Data.GetDefaultDrawProperties();

                currentProperties.Name=$"究极神兵_乱击之究极幻想_中高压_范围_{sourceId}_{lastDrawing}";
                currentProperties.Scale=new(3);
                currentProperties.Owner=targetId;
                currentProperties.Color=accessory.Data.DefaultDangerColor;
                currentProperties.DestoryAt=MAXIMUM_DURATION;

                accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Circle,currentProperties);
                
            }
        
        }
        
        [ScriptMethod(name:"究极神兵 乱击之究极幻想 中高压 (范围清除)",
            eventType:EventTypeEnum.ActionEffect,
            eventCondition:["ActionId:11081"],
            suppress:COMMON_INTERVAL,
            userControl:false)]
    
        public void 究极神兵_乱击之究极幻想_中高压_范围清除(Event @event,ScriptAccessory accessory) {

            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=5&&!skipPhaseChecks) {

                return;

            }
            
            accessory.Method.RemoveDraw(@"^究极神兵_乱击之究极幻想_中高压_范围_.*$");
            
            phase1_mesohighDrawingCounter.Clear();
        
        }
        
        [ScriptMethod(name:"究极神兵 乱击之究极幻想 烈焰碎击 (指路)",
            eventType:EventTypeEnum.ActionEffect,
            eventCondition:["ActionId:11073"],
            suppress:COMMON_INTERVAL)]
    
        public void 究极神兵_乱击之究极幻想_烈焰碎击_指路(Event @event,ScriptAccessory accessory) {

            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=5&&!skipPhaseChecks) {

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetPosition=new Vector3(107.414f,0,107.414f);
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=6375;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
            
            int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            
            if(!isLegalPartyIndex(myIndex)) {

                return;

            }

            if(myIndex==0) {

                currentProperties=accessory.Data.GetDefaultDrawProperties();

                currentProperties.Scale=new(2);
                currentProperties.Owner=accessory.Data.Me;
                currentProperties.TargetPosition=rotatePosition(new Vector3(100,0,90.25f),ARENA_CENTER,-(Math.PI/4));
                currentProperties.ScaleMode|=ScaleMode.YByDistance;
                currentProperties.Color=accessory.Data.DefaultDangerColor;
                currentProperties.Delay=6375;
                currentProperties.DestoryAt=2250;
            
                accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
                
                currentProperties=accessory.Data.GetDefaultDrawProperties();

                currentProperties.Scale=new(2);
                currentProperties.Owner=accessory.Data.Me;
                currentProperties.TargetPosition=rotatePosition(new Vector3(100,0,90.25f),ARENA_CENTER,-(Math.PI/4));
                currentProperties.ScaleMode|=ScaleMode.YByDistance;
                currentProperties.Color=accessory.Data.DefaultSafeColor;
                currentProperties.Delay=8625;
                currentProperties.DestoryAt=2750;
            
                accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);

            }

            else {
                
                currentProperties=accessory.Data.GetDefaultDrawProperties();
            
                currentProperties.Scale=new(1,3);
                currentProperties.Position=new Vector3(107.414f,0,107.414f);
                currentProperties.TargetPosition=new Vector3(107.414f,0,106.414f);
                currentProperties.Color=accessory.Data.DefaultSafeColor;
                currentProperties.Delay=6375;
                currentProperties.DestoryAt=2250;
        
                accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Arrow,currentProperties);
                
            }
        
        }
        
        [ScriptMethod(name:"究极神兵 究极 (阶段控制)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11147"],
            userControl:false)]

        public void 究极神兵_究极_阶段控制(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=5&&!skipPhaseChecks) {

                return;

            }
            
            phase=6;

            phase5sub6_ultimaSemaphore.Set();
            
            if(enableDebugLogging) {
                
                accessory.Log.Debug($"majorPhase={majorPhase}\nphase={phase}");
                
            }

        }
        
        [ScriptMethod(name:"究极神兵 以太波动 (预站位指路)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11147"])]

        public void 究极神兵_以太波动_预站位指路(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }
            
            int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            
            if(!isLegalPartyIndex(myIndex)) {

                return;

            }
            
            bool signalled=phase5sub6_ultimaSemaphore.WaitOne(COMMON_INTERVAL);

            if(!signalled) {

                return;

            }

            if(phase!=6&&!skipPhaseChecks) {

                return;

            }

            int myDiscretizedPosition=-1;

            // MT、ST都走西南对角线(原版MT东北),其余六人东南对角线。
            if(isTank(myIndex)) {

                myDiscretizedPosition=2;

            }

            else {

                myDiscretizedPosition=1;

            }

            if(myDiscretizedPosition==-1) {

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(0.5f,19.5f);
            currentProperties.Position=ARENA_CENTER;
            currentProperties.TargetPosition=rotatePosition(new Vector3(ARENA_CENTER.X,ARENA_CENTER.Y,ARENA_CENTER.Z-1),ARENA_CENTER,Math.PI/2*myDiscretizedPosition+Math.PI/4);
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=11250;
        
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Rect,currentProperties);
            
        }
        
        [ScriptMethod(name:"究极神兵 以太波动 (击退指示)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11144"])]

        public void 究极神兵_以太波动_击退指示(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=6&&!skipPhaseChecks) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }

            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(2);
            currentProperties.Owner=sourceId;
            currentProperties.TargetObject=accessory.Data.Me;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=colourOfDirectionIndicators.V4.WithW(1);
            currentProperties.DestoryAt=4000;
                
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
                
            currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(2,10);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetObject=sourceId;
            currentProperties.Rotation=float.Pi;
            currentProperties.Color=colourOfDirectionIndicators.V4.WithW(1);
            currentProperties.DestoryAt=4000;
                
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);
            
        }
        
        [ScriptMethod(name:"究极神兵 以太波动 (指路)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11144"])]

        public void 究极神兵_以太波动_指路(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=6&&!skipPhaseChecks) {

                return;

            }
            
            int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            
            if(!isLegalPartyIndex(myIndex)) {

                return;

            }

            int myDiscretizedPosition=-1;

            // MT、ST都走西南对角线(原版MT东北),其余六人东南对角线。
            if(isTank(myIndex)) {

                myDiscretizedPosition=2;

            }

            else {

                myDiscretizedPosition=1;

            }

            if(myDiscretizedPosition==-1) {

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Name="究极神兵_以太波动_指路1";
            currentProperties.Scale=new(2,6);
            currentProperties.Position=rotatePosition(new Vector3(ARENA_CENTER.X,ARENA_CENTER.Y,ARENA_CENTER.Z-2),ARENA_CENTER,Math.PI/2*myDiscretizedPosition+Math.PI/4);
            currentProperties.TargetPosition=rotatePosition(new Vector3(ARENA_CENTER.X,ARENA_CENTER.Y,ARENA_CENTER.Z-8),ARENA_CENTER,Math.PI/2*myDiscretizedPosition+Math.PI/4);
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.DestoryAt=27250;
        
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Arrow,currentProperties);

            if(isTank(myIndex)) {

                // MT第二段与ST一致(原版MT为104,90→96,90)。
                currentProperties=accessory.Data.GetDefaultDrawProperties();

                currentProperties.Name="究极神兵_以太波动_指路2";
                currentProperties.Scale=new(2,8);
                currentProperties.Position=new Vector3(90,0,104);
                currentProperties.TargetPosition=new Vector3(90,0,96);
                currentProperties.Color=accessory.Data.DefaultSafeColor;
                currentProperties.DestoryAt=27250;

                accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Arrow,currentProperties);

            }
            
        }
        
        [ScriptMethod(name:"究极神兵 以太波动 (指路清除)",
            eventType:EventTypeEnum.ActionEffect,
            eventCondition:["ActionId:11145"],
            userControl:false)]

        public void 究极神兵_以太波动_指路清除(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=6&&!skipPhaseChecks) {

                return;

            }
            
            if(phase5sub6_ultimaplasmStackCounter>=4) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["TargetId"], out var targetId)) {
                
                return;
                
            }

            if(targetId!=accessory.Data.Me) {

                return;

            }

            lock(phase5sub6_ultimaplasm) {

                Interlocked.Increment(ref phase5sub6_ultimaplasmStackCounter);

                if(phase5sub6_ultimaplasmStackCounter==1) {
                    
                    accessory.Method.RemoveDraw("究极神兵_以太波动_指路1");
                    
                }
                
                if(phase5sub6_ultimaplasmStackCounter==2) {
                    
                    accessory.Method.RemoveDraw("究极神兵_以太波动_指路2");
                    
                }

            }
            
        }
        
        [ScriptMethod(name:"究极神兵 以太波动 究极炸弹 (数据获取)",
            eventType:EventTypeEnum.AddCombatant,
            eventCondition:["DataId:8736"],
            userControl:false)]

        public void 究极神兵_以太波动_究极炸弹_数据获取(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=6&&!skipPhaseChecks) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }
            
            Vector3 sourcePosition=ARENA_CENTER;

            try {

                sourcePosition=JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);

            } catch(Exception e) {
                
                accessory.Log.Error("SourcePosition deserialization failed.");

                return;

            }
            
            int discretizedPosition=discretizePosition(sourcePosition,ARENA_CENTER,4,false);

            lock(phase5sub6_ultimaplasm) {

                phase5sub6_ultimaplasm[discretizedPosition].Add(sourceId);

            }

            if(enableDebugLogging) {
                
                accessory.Log.Debug($"discretizedPosition={discretizedPosition}\nsourceId={sourceId}");
                
            }
            
        }
        
        [ScriptMethod(name:"究极神兵 以太波动 究极炸弹 (范围)",
            eventType:EventTypeEnum.AddCombatant,
            eventCondition:["DataId:8736"])]

        public void 究极神兵_以太波动_究极炸弹_范围(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=6&&!skipPhaseChecks) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }
            
            Vector3 sourcePosition=ARENA_CENTER;

            try {

                sourcePosition=JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);

            } catch(Exception e) {
                
                accessory.Log.Error("SourcePosition deserialization failed.");

                return;

            }
            
            int discretizedPosition=discretizePosition(sourcePosition,ARENA_CENTER,4,false);

            int myIndex=accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            
            if(!isLegalPartyIndex(myIndex)) {

                return;

            }

            bool nonTarget=false;

            if(isTank(myIndex)) {

                if(myIndex==0) {

                    if(discretizedPosition!=0&&discretizedPosition!=3) {

                        nonTarget=true;

                    }
                    
                }
                
                if(myIndex==1) {

                    if(discretizedPosition!=2&&discretizedPosition!=3) {

                        nonTarget=true;

                    }
                    
                }
                
            }

            else {
                
                if(discretizedPosition!=1) {

                    nonTarget=true;

                }
                
            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Name=$"究极神兵_以太波动_究极炸弹_范围_{sourceId}";
            currentProperties.Scale=new(1);
            currentProperties.Owner=sourceId;
            currentProperties.DestoryAt=21000;

            if(nonTarget) {

                currentProperties.Color=colourOfDirectionIndicators.V4.WithW(1);

            }

            else {
                
                currentProperties.Color=accessory.Data.DefaultSafeColor;
                
            }
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Circle,currentProperties);
            
        }
        
        [ScriptMethod(name:"究极神兵 以太波动 究极炸弹 (范围清除)",
            eventType:EventTypeEnum.ActionEffect,
            eventCondition:["ActionId:11145"],
            userControl:false)]

        public void 究极神兵_以太波动_究极炸弹_范围清除(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=6&&!skipPhaseChecks) {

                return;

            }
            
            if(!string.Equals(@event["TargetIndex"],"1")) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }
            
            accessory.Method.RemoveDraw($"究极神兵_以太波动_究极炸弹_范围_{sourceId}");

            for(int i=0;i<phase5sub6_ultimaplasm.Length;++i) {

                if(phase5sub6_ultimaplasm[i].Contains(sourceId)) {
                    
                    foreach(ulong j in phase5sub6_ultimaplasm[i]) {
                        
                        accessory.Method.RemoveDraw($"究极神兵_以太波动_究极炸弹_范围_{j}");
                        
                    }
                    
                }
                
            }
            
        }
        
        [ScriptMethod(name:"究极神兵 以太波动 究极炸弹 (范围清除2)",
            eventType:EventTypeEnum.RemoveCombatant,
            eventCondition:["DataId:8736"],
            userControl:false)]

        public void 究极神兵_以太波动_究极炸弹_范围清除2(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=6&&!skipPhaseChecks) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }
            
            accessory.Method.RemoveDraw($"究极神兵_以太波动_究极炸弹_范围_{sourceId}");
            
        }
        
        [ScriptMethod(name:"究极神兵 狂暴前 邪气龙卷 (范围)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11086"])]

        public void 究极神兵_狂暴前_邪气龙卷_范围(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=6&&!skipPhaseChecks) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();
                
            currentProperties.Scale=new(20);
            currentProperties.InnerScale=new(8.5f);
            currentProperties.Radian=float.Pi*2;
            currentProperties.Owner=sourceId;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.Delay=3000;
            currentProperties.DestoryAt=2250;
        
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Donut,currentProperties);
            
        }
        
        [ScriptMethod(name:"究极神兵 狂暴前 深红旋风 (范围)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11103"])]

        public void 究极神兵_狂暴前_深红旋风_范围(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=6&&!skipPhaseChecks) {

                return;

            }
            
            if(!convertObjectIdToDecimal(@event["SourceId"], out var sourceId)) {
                
                return;
                
            }

            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(18,44);
            currentProperties.Owner=sourceId;
            currentProperties.Color=accessory.Data.DefaultDangerColor;
            currentProperties.DestoryAt=3000;
        
            accessory.Method.SendDraw(DrawModeEnum.Default,DrawTypeEnum.Rect,currentProperties);
            
        }
        
        [ScriptMethod(name:"究极神兵 狂暴前 (数据收集)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:regex:^(11475|11476|11477)$"],
            userControl:false)]

        public void 究极神兵_狂暴前_数据收集(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=6&&!skipPhaseChecks) {

                return;

            }

            if(enragePhasePattern!=EnragePhasePatterns.UNKNOWN) {

                return;

            }

            else {

                if(string.Equals(@event["ActionId"],"11475")) {

                    enragePhasePattern=EnragePhasePatterns.GARUDA_IFRIT_TITAN;

                }
                
                if(string.Equals(@event["ActionId"],"11476")) {

                    enragePhasePattern=EnragePhasePatterns.IFRIT_GARUDA_TITAN;

                }
                
                if(string.Equals(@event["ActionId"],"11477")) {

                    enragePhasePattern=EnragePhasePatterns.TITAN_IFRIT_GARUDA;

                }

                phase5sub6_ifritSemaphore.Set();
                phase5sub6_titanSemaphore.Set();

                if(enableDebugLogging) {
                    
                    accessory.Log.Debug($"enragePhasePattern={enragePhasePattern}");
                    
                }

            }
            
        }
        
        [ScriptMethod(name:"究极神兵 狂暴前 伊弗利特 (正北指路)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11476"])]

        public void 究极神兵_狂暴前_伊弗利特_正北指路(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=6&&!skipPhaseChecks) {

                return;

            }

            bool signalled=phase5sub6_ifritSemaphore.WaitOne(COMMON_INTERVAL);

            if(!signalled) {

                return;

            }

            int delay=-1;
            int duration=-1;

            switch(enragePhasePattern) {

                case EnragePhasePatterns.GARUDA_IFRIT_TITAN: {

                    delay=1625;
                    duration=4625;
                    
                    break;

                }
                
                case EnragePhasePatterns.IFRIT_GARUDA_TITAN: {

                    delay=0;
                    duration=6250;
                    
                    break;

                }
                
                case EnragePhasePatterns.TITAN_IFRIT_GARUDA: {

                    delay=0;
                    duration=6250;
                    
                    break;

                }
                
                default: {

                    break;

                }
                
            }

            if(delay<0||duration<0) {

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetPosition=new Vector3(100,0,81.5f);
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.Delay=delay;
            currentProperties.DestoryAt=duration;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);

        }
        
        [ScriptMethod(name:"究极神兵 狂暴前 泰坦 (正北或西北指路)",
            eventType:EventTypeEnum.StartCasting,
            eventCondition:["ActionId:11477"])]

        public void 究极神兵_狂暴前_泰坦_正北或西北指路(Event @event,ScriptAccessory accessory) {
            
            if(majorPhase!=5&&!skipPhaseChecks) {

                return;

            }

            if(phase!=6&&!skipPhaseChecks) {

                return;

            }

            bool signalled=phase5sub6_titanSemaphore.WaitOne(COMMON_INTERVAL);

            if(!signalled) {

                return;

            }

            Vector3 myPosition=ARENA_CENTER;
            int delay=-1;
            int duration=-1;

            switch(enragePhasePattern) {

                case EnragePhasePatterns.GARUDA_IFRIT_TITAN: {

                    myPosition=rotatePosition(new Vector3(100,0,81.5f),ARENA_CENTER,-(Math.PI/4));
                    delay=0;
                    duration=4125;
                    
                    break;

                }
                
                case EnragePhasePatterns.IFRIT_GARUDA_TITAN: {

                    myPosition=new Vector3(100,0,81.5f);
                    delay=1625;
                    duration=2500;
                    
                    break;

                }
                
                case EnragePhasePatterns.TITAN_IFRIT_GARUDA: {

                    myPosition=new Vector3(100,0,81.5f);
                    delay=0;
                    duration=4125;
                    
                    break;

                }
                
                default: {

                    break;

                }
                
            }

            if(delay<0||duration<0) {

                return;

            }
            
            var currentProperties=accessory.Data.GetDefaultDrawProperties();

            currentProperties.Scale=new(2);
            currentProperties.Owner=accessory.Data.Me;
            currentProperties.TargetPosition=myPosition;
            currentProperties.ScaleMode|=ScaleMode.YByDistance;
            currentProperties.Color=accessory.Data.DefaultSafeColor;
            currentProperties.Delay=delay;
            currentProperties.DestoryAt=duration;
            
            accessory.Method.SendDraw(DrawModeEnum.Imgui,DrawTypeEnum.Displacement,currentProperties);

        }
        
        #endregion
        
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
        
    }

    #region Extensions
    
    public static class ScriptAccessoryExtensions
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
    
}