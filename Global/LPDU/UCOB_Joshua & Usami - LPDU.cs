using System;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Script;
using KodakkuAssist.Module.GameEvent.Struct;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Data;
using KodakkuAssist.Module.Draw.Manager;
using KodakkuAssist.Module.GameEvent.Types;
using KodakkuAssist.Extensions;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Numerics;
using Newtonsoft.Json;
using System.Linq;
using System.Globalization;
using System.Runtime.CompilerServices;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace UsamisKodakku.Scripts._04_StormBlood.UcobReborn;

[ScriptType(name: Name, territorys: [733], guid: "e2e37136-72a2-46b0-abc7-ade17da161b7",
    version: Version, author: "Usami", note: NoteStr, updateInfo: UpdateInfo)]

public class UcobReborn
{
    const string NoteStr =
        $"""
        {Version}
        This is a modified version adapted for the LPDU strat.

        -----------Important-----------

        At the very start, when everyone is lining up, once everyone is in position, type **/e sort** in the chat can automatically assign the players closest to each marker into the correct order.

        MT H1 D1 D3 → L1 L2 L3 L4
        ST H2 D2 D4 → R1 R2 R3 R4

        -------------------------------

        A rework of the UCOB [The Unending Coil of Bahamut (Ultimate)] script.
        Adds a whole lot of new stuff.

        Original script authors: Joshua, Meva, KnightRider, Usami
        """;
    
    const string UpdateInfo =
        $"""
        {Version}
        LPDU adaption
        """;

    private const string Name = "The Unending Coil of Bahamut (Ultimate) UCOB - LPDU";
    private const string Version = "0.0.0.4";
    private const string DebugVersion = "g";
    private int _runId = 0;
    public const bool Debugging = false;
    
    public static readonly Vector3 Center = Vector3.Zero;
    
    private UcobParams _upm = new();
    private PriorityDict _pd = new();
    private readonly object _stateLock = new();

    [UserSetting("Special mode: includes drawings that use the game's native VFX")]
    public static bool SpecialMode { get; set; } = true;
    
    // [UserSetting("Shotcaller mode")]
    // public static bool CaptainMode { get; set; } = false;

    public void Init(ScriptAccessory sa)
    {
        _runId++;
        DrawTools.ResetLifecycle();
        _upm.Reset();
        _pd.Init("P1 Hatch");
        sa.Method.RemoveDraw(".*");
        sa.Method.ClearFrameworkUpdateAction(this);
        sa.DebugMsg($"Script {Name} v{Version}{DebugVersion} initialized, _runId {_runId}");
    }


    private async Task<bool> WaitUntilConditions(
        Func<bool>[] conditions,
        int timeoutMs = 2000,
        int intervalMs = 50)
    {
        var runId = _runId;
        var count = timeoutMs / intervalMs;
        for (int i = 0; i < count; i++)
        {
            if (_runId != runId) return false;
            lock (_stateLock)
            {
                if (conditions.All(condition => condition()))
                    return _runId == runId;
            }
            await Task.Delay(intervalMs);
        }
        return false;
    }

    #region Debug

    [ScriptMethod(name: "———————— [Debug] ————————",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void Debug_Divider(Event ev, ScriptAccessory sa)
    {
        sa.DebugMsg($"Hello Koda! {Name}");
    }
    
    [ScriptMethod(name: "Tank Spot",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void Debug_TankSpot(Event ev, ScriptAccessory sa)
    {
        _upm.SolveTankSpot();
        
        for (int i = 0; i < 2; i++)
        {
            if (!Debugging && sa.GetMyIndex() != i) continue;
            sa.DrawGuidance(sa.Data.PartyList[i], _upm.TankSpot, 0, 5000,
                $"P4_{_upm.Phase}_TankSpot", sa.Data.DefaultSafeColor);
        }

        var color = new Vector4(1f, 0.5f, 0.5f, 0.75f);
        sa.DrawCircle(_upm.TankSpot, 0, 5000, $"P4_{_upm.Phase}_TankSpot", 1f, color);

        DrawSpreadDirections(sa, 0, 5000);
    }
    
    [ScriptMethod(name: "Test Template",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:26071"],
        userControl: Debugging)]
    public void Debug_Template(Event ev, ScriptAccessory sa)
    {
        sa.DebugMsg($"hello");
        
        lock (_stateLock)
        {
            _upm.P3.TowerIconOffset++;
            sa.DrawCountDown(ev.TargetPosition, 3000, iconScale: 1f, objIdBias: _upm.P3.TowerIconOffset);
        }
    }
    
    #endregion Debug

    #region Sort Party by Waymarks

    // Waymark index 0..7 = A B C D 1 2 3 4, mapped to the role that stands on that mark
    private static readonly int[] WaymarkToRole = [0, 2, 4, 6, 1, 3, 5, 7];
    private static readonly string[] RoleNames = ["MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4"];
    // LPDU light-party slots, same order: MT H1 D1 D3 → L1-L4, ST H2 D2 D4 → R1-R4
    private static readonly string[] LpduNames = ["L1", "R1", "L2", "R2", "L3", "R3", "L4", "R4"];

    // /e sort: everyone stands on their own waymark, then PartyList is rebuilt one player per mark, globally nearest pair first
    [ScriptMethod(name: "Sort Party by Waymarks (/e sort)", eventType: EventTypeEnum.Chat,
        eventCondition: ["Type:Echo", "Message:regex:^\\s*sort\\s*$"])]
    public async void SortPartyByWaymarks(Event ev, ScriptAccessory sa)
    {
        var party = sa.Data.PartyList;
        if (party.Count < 8) { sa.Method.SendChat("/e [Sort] Party has fewer than 8 members"); return; }

        var marks = new List<(int Mark, Vector3 Pos)>();
        unsafe
        {
            var mc = FFXIVClientStructs.FFXIV.Client.Game.UI.MarkingController.Instance();
            for (int i = 0; i < 8; i++)
                if (mc->FieldMarkers[i].Active) marks.Add((i, mc->FieldMarkers[i].Position));
        }
        if (marks.Count < 8) { sa.Method.SendChat($"/e [Sort] Only {marks.Count} waymarks placed, 8 required"); return; }

        var players = new List<(uint Id, Vector3 Pos)>();
        foreach (var id in party)
        {
            var obj = sa.Data.Objects.SearchByEntityId(id);
            if (obj == null) { sa.Method.SendChat("/e [Sort] Can't read a party member's position"); return; }
            players.Add((id, obj.Position));
        }

        // Sort every (waymark, player) pair by horizontal distance, then take each pair whose mark and player are both still free
        var order = new uint[8];
        var usedMark = new HashSet<int>();
        var usedPlayer = new HashSet<uint>();
        var pairs = from m in marks from p in players
                    orderby Vector2.Distance(new(m.Pos.X, m.Pos.Z), new(p.Pos.X, p.Pos.Z))
                    select (m.Mark, p.Id);
        foreach (var (mark, id) in pairs)
        {
            if (usedMark.Contains(mark) || usedPlayer.Contains(id)) continue;
            usedMark.Add(mark);
            usedPlayer.Add(id);
            order[WaymarkToRole[mark]] = id;
        }

        if (!WritePartyOrder([.. order], out var err)) { sa.Method.SendChat($"/e [Sort] Failed to write the party order: {err}"); return; }
        // SendChat queues every line as its own framework task, and Dalamud runs same-tick tasks in
        // ConcurrentDictionary order, so back-to-back lines come out shuffled: give each line its own tick
        foreach (var role in WaymarkToRole) // A B C D 1 2 3 4 = L1-L4 R1-R4
        {
            sa.Method.SendChat($"/e [Sort] {LpduNames[role]}({RoleNames[role]}): {sa.Data.Objects.SearchByEntityId(order[role])?.Name}");
            await Task.Delay(100);
        }
    }

    // MemberList is internal static with a private setter: swap the reference via reflection, or reorder the live List in place if that fails (same trick as the BLU script)
    private static bool WritePartyOrder(List<uint> order, out string err)
    {
        err = "";
        try
        {
            var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            var t = typeof(InternalData).Assembly.GetType("KodakkuAssist.Data.PartyList.PartyList");
            var setter = t?.GetProperty("MemberList", flags)?.GetSetMethod(true);
            if (setter != null) { setter.Invoke(null, [order]); return true; }
            var live = InternalData.Party.PartyList;
            live.Clear();
            live.AddRange(order);
            return true;
        }
        catch (Exception ex) { err = ex.Message; return false; }
    }

    #endregion Sort Party by Waymarks

    #region General: Twintania

    [ScriptMethod(name: "============= [General: Twintania] =============",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void Twintania_Divider(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "GEN_Twister Warning",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(9898|9906)$"],
        userControl: true)]
    public void GEN_TwisterWarning(Event ev, ScriptAccessory sa)
    {
        var destroyMs = ev.ActionId == 9898 ? 2000 : 5500;
        for (var i = 0; i < sa.Data.PartyList.Count; i++)
            sa.DrawCircle(sa.Data.PartyList[i], 0, destroyMs, $"GEN_{_upm.Phase}_Twister{i}",
                1.5f, sa.Data.DefaultDangerColor.WithW(2), byTime: true);

        var txt = _upm.Phase < 1000 ? "Twisters -> Stack" : "Twisters - move!";
        var ttstxt = _upm.Phase < 1000 ? "Twisters, then stack" : "Twisters, move";
        
        sa.TextInfo(txt, destroyMs: 2000, isWarning: true);
        sa.TTS(ttstxt);
    }
    
    [ScriptMethod(name: "GEN_Twister Danger Zone",
        eventType: EventTypeEnum.ObjectChanged, eventCondition: ["DataId:2001168", "Operate:Add"],
        userControl: true)]
    public void GEN_TwisterDanger(Event ev, ScriptAccessory sa)
    {
        sa.DrawCircle(ev.SourcePosition, 0, 7000, $"GEN_TwisterDanger", 1.25f, new Vector4(1, 0, 0, 4));
    }
        
    [ScriptMethod(name: "GEN_Hatch Path",
        eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:8160"],
        userControl: true)]
    public void GEN_HatchPath(Event ev, ScriptAccessory sa)
    {
        var sid = ev.SourceId;
        var dp = sa.DrawLine(sid, 0, 3500, 10000, $"GEN_HatchPath{sid}",
            0, 2f, 6f, new Vector4(1, 1, 0, 3), draw: false);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        sa.DrawArrow(sid, 0, 3500, 10000, $"GEN_HatchPath{sid}", 0, 1f, 5.5f, new Vector4(0, 0, 1, 1));
    }
    
    [ScriptMethod(name: "GEN_Hatch Explosion in Neurolink",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9902"],
        userControl: Debugging)]
    public void GEN_NeurolinkHatchBlastAoe(Event ev, ScriptAccessory sa)
    {
        for (int i = 0; i < _upm.NeurolinkPositions.Count; i++)
        {
            var destroyMs = _upm.Phase.GetDecimalDigit(3) == 3 ? 3500 : 10000;
            sa.DrawCircle(_upm.NeurolinkPositions[i], 3500, destroyMs, $"GEN_NeurolinkHatchBlastAoe{i}", 8f, new Vector4(1, 1, 0, 0.4f));
        }
    }

    [ScriptMethod(name: "GEN_Hatch Explosion Cleanup",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9903", "TargetIndex:1"],
        userControl: Debugging)]
    public void GEN_HatchBlastCleanup(Event ev, ScriptAccessory sa)
    {
        var sid = ev.SourceId;
        var tid = ev.TargetId;
        var spos = ev.SourcePosition;

        int minIdx = _upm.GetNearestNeurolinkIndex(spos);
        sa.Method.RemoveDraw($"GEN_HatchPath{sid}.*");
        sa.Method.RemoveDraw($".*_{_upm.Phase}_HatchPartnerLink.*");
        if (_upm.Phase != 3500)
            sa.Method.RemoveDraw($"GEN_NeurolinkHatchBlastAoe{minIdx}.*");

        var tidx = sa.GetPlayerIdIndex((uint)tid);
        if (!sa.IsValidPartyIndex(tidx)) return;
        _pd[tidx] = 0;
        sa.Method.RemoveDraw($".*_{_upm.Phase}_HatchGuide{tidx}.*");
    }
    
    [ScriptMethod(name: "GEN_Clear Twister Drawings",
        eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:regex:^(9898)$", "TargetIndex:1"],
        userControl: Debugging)]
    public void GEN_ClearTwisterDraws(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw(@"GEN_\d{4}_Twister[0-9].*");
    }
    
    [ScriptMethod(name: "GEN_Clear Plummet Drawings",
        eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:regex:^(9896)$", "TargetIndex:1"],
        userControl: Debugging)]
    public void GEN_ClearPlummetDraws(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw(@"GEN_\d{4}_Plummet.*");
    }
    
    [ScriptMethod(name: "GEN_Clear Liquid Hell Drawings",
        eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:regex:^(9901)$", "TargetIndex:1"],
        userControl: Debugging)]
    public void GEN_ClearLiquidHellDraws(Event ev, ScriptAccessory sa)
    {
        _upm.LiquidHellHitCount++;
        sa.DebugMsg($"Liquid Hell hit count {_upm.LiquidHellHitCount}");
        if (_upm.LiquidHellHitCount < 5) return;
        _upm.LiquidHellHitCount = 0;
        sa.Method.RemoveDraw(@"GEN_\d{4}_LiquidHell.*");
    }
    
    [ScriptMethod(name: "GEN_Hatch Marker Tracking",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0076"], 
        userControl: Debugging)]
    public void GEN_HatchMarkerRecord(Event ev, ScriptAccessory sa)
    {
        lock (_stateLock)
        {
            var tidx = sa.GetPlayerIdIndex((uint)ev.TargetId);
            if (!sa.IsValidPartyIndex(tidx)) return;
            _pd.AddPriority(tidx, 100);
        }
    }
    
    [ScriptMethod(name: "GEN_Fireball Stack AoE",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0075"],
        userControl: true)]
    public void GEN_FireballStack(Event ev, ScriptAccessory sa)
    {
        sa.DrawCircle(ev.TargetId, 0, 20000, $"GEN_TwinFireballStackAoe", 4f, new Vector4(0.3f, 1, 0.3f, 1));
        if (_upm.Phase < 3000) return;
        sa.TextInfo("Fireball - Stack");
        sa.TTS("Fireball, stack");
        if (!SpecialMode) return;
        sa.DrawLockOn(ev.TargetId, 383, 100, 5000, new Vector3(2, 2, 2));
    }

    [ScriptMethod(name: "GEN_Fireball Stack AoE Cleanup",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9900", "TargetIndex:1"], 
        userControl: Debugging)]
    public void GEN_FireballStackCleanup(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw($"GEN_TwinFireballStackAoe");
    }
    
    [ScriptMethod(name: "GEN_Random Liquid Hell Puddle",
        eventType: EventTypeEnum.ActionEffect, 
        eventCondition: ["ActionId:regex:^(9901)$", "TargetIndex:1"],
        userControl: true)]
    public async void GEN_LiquidHellRandomPuddle(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase.GetDecimalDigit(3) == 3) return;
        
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _upm.Phase >= 1999 ||
                      (_upm.P1.SkillRotation[_upm.P1.SkillIndex] is TwinTaniaSkills.LiquidHell or TwinTaniaSkills.LiquidHellRandom),
                () => _upm.LiquidHellHitCount == 1
            ])) return;
        
        var color = new Vector4(0.3f, 0.7f, 1, 1.5f);
        sa.DrawCircle(ev.TargetId, 0, 10000, $"GEN_{_upm.Phase}_LiquidHellRandomPuddle", 5f, color);
    }
    
    // ClassJob RowIds of the physical ranged jobs BRD/MCH/DNC
    private static readonly uint[] PhysRangedJobIds = [23, 31, 38];
    // ClassJob RowIds of the melee jobs MNK/DRG/NIN/SAM/RPR/VPR
    private static readonly uint[] MeleeJobIds = [20, 22, 30, 34, 39, 41];

    private async void DrawLiquidHellBait(ScriptAccessory sa, bool phaseKeep)
    {
        if (_upm.Phase < 1999)
        {
            // Extra guard on the current phase
            var lastPhase = _upm.Phase;
            if (!await WaitUntilConditions(
                conditions:
                [
                    () => _upm.P1.SkillRotation[_upm.P1.SkillIndex] == TwinTaniaSkills.LiquidHell,
                ])) return;
            if (phaseKeep && _upm.Phase != lastPhase) return;
        }
    
        var color = new Vector4(0.3f, 0.3f, 1, 4f);
        sa.DrawDonut(_upm.P1.TwintaniaObjId, 0, 20000, $"GEN_{_upm.Phase}_LiquidHellBait", 16.5f, 15f, color);

        // The first physical ranged in party order baits; falls back to D3 if there is none
        var biasRole = Enumerable.Range(0, sa.Data.PartyList.Count).FirstOrDefault(i =>
            sa.GetById(sa.Data.PartyList[i]) is IBattleChara bc && PhysRangedJobIds.Contains(bc.ClassJob.RowId), 6);
        var ttsStr = sa.GetMyIndex() != biasRole ? "Stay inside the ring" : "Bait outside the ring";
        sa.TextInfo($"Liquid Hell: {ttsStr}", destroyMs: 1500);
        sa.TTS(ttsStr);
        
        // Liquid Hell warning
        var color2 = new Vector4(0.3f, 0.7f, 1, 1.5f);
        for (int i = 0; i < sa.Data.PartyList.Count; i++)
        {
            if (sa.GetById(sa.Data.PartyList[i]) is not { } obj) continue;
            if (sa.GetById(_upm.P1.TwintaniaObjId) is not { } bossObj) continue;
            
            var draw = sa.DrawCircle(sa.Data.PartyList[i], 0, 20000, 
                $"GEN_{_upm.Phase}_LiquidHellWarning{i}", 6f, color2, draw: false);
            
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, draw, dp =>
            {
                var distance = Vector3.Distance(obj.Position, bossObj.Position);
                dp.Scale = distance > 15 ? new Vector2(5) : new Vector2(0);
            });
        }
    }
    
    #endregion General: Twintania

    #region General: Nael

    [ScriptMethod(name: "============= [General: Nael] =============",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void Nael_Divider(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "GEN_Hypernova (Black Puddle) Danger Zone",
        eventType: EventTypeEnum.ObjectChanged, eventCondition: ["DataId:2003393", "Operate:Add"],
        userControl: true)]
    public void GEN_HypernovaDangerZone(Event ev, ScriptAccessory sa)
    {
        sa.DrawCircle(ev.SourcePosition, 0, 15000, $"GEN_HypernovaDanger", 5f, new Vector4(1, 0, 0, 3));
    }
    
    [ScriptMethod(name: "GEN_Nael Quote AoE Cleanup",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(992[01]|991[5678])$", "TargetIndex:1"], 
        userControl: Debugging)]
    public void GEN_NaelQuoteCleanup(Event ev, ScriptAccessory sa)
    {
        var aid = ev.ActionId;
        var tidx = -1;
        if (aid == 9920)
        {
            tidx = sa.GetPlayerIdIndex((uint)ev.TargetId);
            if (!sa.IsValidPartyIndex(tidx)) return;
        }
        var skillStr = aid switch
        {
            9915 => "IronChariot",
            9916 => "LunarDynamo",
            9917 => "ThermionicBeam",
            9918 => "RavenDive",
            9920 => $"MeteorStream{tidx}",
            9921 => "DalamudDive",
            _ => ""
        };
        if (skillStr == "") return;
        sa.Method.RemoveDraw(@$"GEN_\d{{4}}_Quote{skillStr}");
    }
    
    [ScriptMethod(name: "GEN_Divebomb (Cauterize) AoE",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(993[12345])$"],
        userControl: true)]
    public void GEN_DivebombAoe(Event ev, ScriptAccessory sa)
    {
        var sid = ev.SourceId;
        sa.DrawRect(ev.SourceId, 0, 0, 4000, $"GEN_{_upm.Phase}_DivebombAoe_{sid}", 
            0, 20, 60, sa.Data.DefaultDangerColor.WithW(1.5f), true);
        
        if (_upm.Phase != 2010) return;
        sa.Method.RemoveDraw($"P2C_{_upm.Phase}_DivebombBaitAoe_{sid}");
        sa.Method.RemoveDraw($"P2C_{_upm.Phase}_DivebombBaitSpotGuide_{sid}");
    }
    
    #endregion General: Nael

    #region P1

    [ScriptMethod(name: "———————— [P1] ————————",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P1_Divider(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "P1_Twintania ID Tracking",
        eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:627"],
        userControl: Debugging)]
    public void P1_RecordTwintaniaId(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase > 1999) return;
        
        if (sa.GetById(ev.TargetId) is not { } obj) return;
        if (obj.DataId != 8159) return;
        
        _upm.P1.TwintaniaObjId = ev.TargetId;
    }
    
    [ScriptMethod(name: "P1_Translucent Twintania + Center Dot",
        eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:627"],
        userControl: true)]
    public void P1_TwintaniaFadeAndCenter(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase > 1999) return;
        
        if (sa.GetById(ev.TargetId) is not { } obj) return;
        if (obj.DataId != 8159) return;
        
        sa.AlphaModify(obj, 0.5f);
        // Must not get wiped by the phase-transition draw cleanup
        sa.DrawCircle(_upm.P1.TwintaniaObjId, 0, Int32.MaxValue, 
            $"P1_9999_TwintaniaCenter_Inner", 0.4f, new Vector4(1, 0, 0, 2), useImgui: true);
        sa.DrawDonut(_upm.P1.TwintaniaObjId, 0, Int32.MaxValue, 
            $"P1_9999_TwintaniaCenter_Outer", 0.5f, 0.4f, new Vector4(0, 1, 1, 1), useImgui: true);
    }

    [ScriptMethod(name: "P1_Rotation Tracking",
        eventType: EventTypeEnum.ActionEffect, 
        eventCondition: ["ActionId:regex:^(989[678]|990[12])$", "TargetIndex:1"],
        userControl: Debugging)]
    public void P1_RotationTracker(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase > 1999) return;
        if (ev.ActionId == 9901)
        {
            // if (!await WaitUntilConditions([() => _upm.P1.LiquidHellRotationHitCount != _upm.LiquidHellHitCount])) return;
            _upm.P1.LiquidHellRotationHitCount++;
            sa.DebugMsg($"Liquid Hell rotation hit count changed {_upm.P1.LiquidHellRotationHitCount}");
            if (_upm.P1.LiquidHellRotationHitCount < 5) return;
            _upm.P1.LiquidHellRotationHitCount = 0;
        }
        _upm.P1.AdvanceCyclicSkillIndex();
        sa.DebugMsg($"Current rotation skill index: {_upm.P1.SkillIndex}, phase {_upm.Phase}");
    }

    private async void DrawPlummet(ScriptAccessory sa)
    {
        if (_upm.Phase < 1999)
        {
            if (!await WaitUntilConditions(
                conditions:
                [
                    () => _upm.P1.SkillRotation[_upm.P1.SkillIndex] == TwinTaniaSkills.Plummet,
                ])) return;
        }
        
        if (sa.GetById(_upm.P1.TwintaniaObjId) is not { } obj) return;
        if (obj.DataId != 8159) return;

        sa.TextInfo("Plummet (cleave) incoming", destroyMs: 1500);
        sa.TTS("Cleave incoming");

        var dp = sa.DrawFan(_upm.P1.TwintaniaObjId, 0, 10000, $"GEN_{_upm.Phase}_PlummetAoe", 90f.DegToRad(), 0, 11.96f, 0,
            sa.Data.DefaultDangerColor.WithW(1.5f), draw: false);
        dp.SetOwnerTarget(false);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }
    
    [ScriptMethod(name: "P1_Plummet AoE (Opener)",
        eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:627"],
        userControl: true)]
    public void P1_PlummetOpener(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase > 1999) return;
        
        if (sa.GetById(ev.TargetId) is not { } obj) return;
        if (obj.DataId != 8159) return;
        DrawPlummet(sa);
    }

    [ScriptMethod(name: "P1_Plummet AoE (Mid-phase)",
        eventType: EventTypeEnum.ActionEffect, 
        eventCondition: ["ActionId:regex:^(9898|9897)$", "TargetIndex:1"],
        userControl: true)]
    public void P1_PlummetMid(Event ev, ScriptAccessory sa)
    {
        // Plummet only follows Death Sentence or Twister
        if (_upm.Phase > 1999) return;
        DrawPlummet(sa);
    }
    
    [ScriptMethod(name: "P1_Liquid Hell Bait (Transition)",
        eventType: EventTypeEnum.PlayActionTimeline, eventCondition: ["SourceDataId:8159", "Id:148"],
        userControl: true)]
    public void P1_LiquidHellBaitTransition(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase > 1999) return;
        DrawLiquidHellBait(sa, phaseKeep: false);
    }
    
    [ScriptMethod(name: "P1_Liquid Hell Bait (Mid-phase)",
        eventType: EventTypeEnum.ActionEffect, 
        eventCondition: ["ActionId:regex:^(9902|9896)$", "TargetIndex:1"],
        userControl: true)]
    public void P1_LiquidHellBaitMid(Event ev, ScriptAccessory sa)
    {
        // Liquid Hell only comes at the start of a phase, or after Hatch, or after Plummet
        if (_upm.Phase > 1999) return;
        DrawLiquidHellBait(sa, phaseKeep: true);
    }

    
    [ScriptMethod(name: "P1_Neurolink Position Tracking",
        eventType: EventTypeEnum.ObjectChanged, eventCondition: ["DataId:2001151", "Operate:Add"],
        userControl: Debugging)]
    public void P1_RecordNeurolinks(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase > 2001) return;

        var tPos = ev.SourcePosition;
        _upm.NeurolinkPositions.Add(tPos);
        sa.DebugMsg($"{tPos.ToStr()}");

        // Per the CN strategy: once all three Neurolinks are collected, sort them B -> C -> D
        if (_upm.NeurolinkPositions.Count != 3) return;

        _upm.NeurolinkPositions = _upm.NeurolinkPositions
            .OrderBy(pos => pos.GetRadian(Center).RadianToRegion(3, 1, true, true))
            .ToList();
        sa.DebugMsg($"Sorted: {string.Join(", ", _upm.NeurolinkPositions.Select(p => p.ToStr()))}");
        _upm.SolveTankSpot();
    }

    [ScriptMethod(name: "P1_Hatch Partner Link",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0076"],
        userControl: true, suppress: 500)]
    public async void P1_HatchPartnerLink(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase > 1999) return;
        var cnt = _upm.NeurolinkPositions.Count;
        if (cnt == 1) return;
        
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _pd.SelectSpecificPriorityIndex(cnt - 1, true).Value >= 100,
            ])) return;

        if (!Debugging && _pd[sa.GetMyIndex()] < 100) return;
        var player1Idx = _pd.SelectSpecificPriorityIndex(0, true).Key;
        var player2Idx = _pd.SelectSpecificPriorityIndex(1, true).Key;
        sa.DrawConnection(sa.Data.PartyList[player1Idx], sa.Data.PartyList[player2Idx], 0, 20000,
            $"P1_{_upm.Phase}_HatchPartnerLink", new Vector4(1, 1, 0, 1));
    }
    

    [ScriptMethod(name: "P1_Hatch Guide",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0076"],
        userControl: true, suppress: 500)]
    public async void P1_HatchGuide(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase > 1999) return;
        var cnt = _upm.NeurolinkPositions.Count;
        
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _pd.SelectSpecificPriorityIndex(cnt - 1, true).Value >= 100,
            ])) return;
        
        // Add the players marked for Hatch to members
        List<PriorityEntry> members = [];
        for (int i = 0; i < cnt; i++)
            members.Add(_pd.SelectSpecificPriorityIndex(i, true));
        
        // In P1, everyone is guided to the nearest Neurolink
        for (int i = 0; i < members.Count; i++)
        {
            if (!Debugging && members[i].Key != sa.GetMyIndex()) continue;
            var draw = sa.DrawGuidance(sa.Data.PartyList[members[i].Key], _upm.NeurolinkPositions[0], 0, 20000, 
                $"P1_{_upm.Phase}_HatchGuide{members[i].Key}", sa.Data.DefaultSafeColor, draw: false);
            
            var i1 = i;
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, draw, dp =>
            {
                if (members.Count == 1)
                    dp.TargetPosition = _upm.NeurolinkPositions[0];
                else
                {
                    if (sa.GetById(sa.Data.PartyList[members[i1 == 0 ? 1 : 0].Key]) is not { } partnerObj) return;
                    var partnerPos = partnerObj.Position;
                    
                    if (sa.GetById(sa.Data.PartyList[members[i1].Key]) is not { } myObj) return;
                    var myPos = myObj.Position;

                    var myDistanceDelta =
                        Vector3.Distance(myPos, _upm.NeurolinkPositions[0]) - Vector3.Distance(myPos, _upm.NeurolinkPositions[1]);
                    var partnerDistanceDelta =
                        Vector3.Distance(partnerPos, _upm.NeurolinkPositions[0]) - Vector3.Distance(partnerPos, _upm.NeurolinkPositions[1]);
                    dp.TargetPosition = myDistanceDelta > partnerDistanceDelta ? _upm.NeurolinkPositions[1] : _upm.NeurolinkPositions[0];
                }
            });
        }
    }
    
    [ScriptMethod(name: "P1_Phase Transition via Twintania Animation",
        eventType: EventTypeEnum.PlayActionTimeline, eventCondition: ["SourceDataId:8159", "Id:148"],
        userControl: Debugging)]
    public void P1_PhaseByTwintaniaAnimation(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase > 1999) return;
        sa.Method.RemoveDraw($".*_{_upm.Phase}.*");
        
        _upm.Phase = _upm.Phase switch
        {
            1100 => 1200,
            1200 => 2000,
            _ => 1100,
        };

        if (_upm.Phase != 2000)
        {
            _upm.P1.SkillIndex = 0;
            _upm.P1.LoadPhaseRotation(_upm.Phase);
        }
        else
        {
            if (sa.GetById(ev.SourceId) is not { } obj) return;
            if (obj.DataId != 8159) return;
            // sa.AlphaModify(obj, 1f, currentAlpha => currentAlpha <= 0.6f);
            sa.Method.RemoveDraw("GEN.*");
            sa.Method.RemoveDraw("P1.*");
        }
        sa.DebugMsg($"{_upm.Phase}");
    }
    #endregion P1

    #region P2

    [ScriptMethod(name: "———————— [P2] ————————",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P2_Divider(Event ev, ScriptAccessory sa)
    {
    }
    
    #region P2A Opener 2000~2002

    [ScriptMethod(name: "============= [P2A Opener] =============",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P2A_Opener_Divider(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "P2A_Knockback Spot Guide",
        eventType: EventTypeEnum.PlayActionTimeline, eventCondition: ["SourceDataId:8159", "Id:148"],
        userControl: true, suppress: 500)]
    public async void P2A_KnockbackGuide(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase < 1200) return;
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _upm.Phase == 2000,
            ])) return;

        // Split by role: tanks go north (0,0,-7.87), everyone else goes south (0,0,8.7)
        for (int i = 0; i < sa.Data.PartyList.Count; i++)
        {
            if (!Debugging && sa.GetMyIndex() != i) continue;
            var isTank = sa.GetById(sa.Data.PartyList[i]) is IBattleChara bc && bc.IsTank();
            sa.DrawGuidance(sa.Data.PartyList[i], new Vector3(0, 0, isTank ? -7.87f : 8.7f), 0, 10000,
                $"P2A_{_upm.Phase}_KnockbackGuide{i}", sa.Data.DefaultSafeColor);
        }
        sa.DrawKnockBack(Center, 0, 10000, $"P2_KnockbackAoe", 1f, 10f, sa.Data.DefaultDangerColor.WithW(1.5f));
    }
    
    [ScriptMethod(name: "P2A_Heavensfall Pillar Death Zone",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9912"],
        userControl: true, suppress: 500)]
    public void P2A_HeavensfallDeathZone(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 2000) return;
        var color = new Vector4(1, 0.2f, 0.2f, 3f);
        if (sa.GetById(ev.SourceId) is not { } obj) return;
        var dp = sa.DrawRect(obj.Position, 0, 20000, $"P2A_{_upm.Phase}_HeavensfallDeathZone", 0, 9, 7, color, draw: false);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
    }
    
    [ScriptMethod(name: "P2A_Opening Meteor Stream Spread",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9912"],
        userControl: true, suppress: 500)]
    public void P2A_OpenerMeteorStreamSpread(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 2000) return;
        var color = new Vector4(0.4f, 1, 1, 1.5f);
        DrawNaelQuoteSkill(sa, NaelQuoteSkills.MeteorStream, 0, 20000, color);
    }
    
    [ScriptMethod(name: "P2A_Opening Meteor Stream Guide",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9912"],
        userControl: true, suppress: 500)]
    public void P2A_OpenerMeteorStreamGuide(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 2000) return;
        // region: 22.5° per step, counterclockwise from south: 0 S, 2 SE, 4 E, 6 NE, 8 N, 10 NW, 12 W, 14 SW
        List<(int region, bool inside)> tPosList =
        [
            (10, false), (8, true), (12, true), (6, true),
            (14, true), (4, true), (0, true), (2, true)
        ];

        for (int i = 0; i < sa.Data.PartyList.Count; i++)
        {
            if (!Debugging && sa.GetMyIndex() != i) continue;
            var tPos = new Vector3(0, 0, 20).RotateAndExtend(Center, 22.5f.DegToRad() * tPosList[i].region,
                tPosList[i].inside ? -13 : 0);  // Inner ring radius 7: spots 45° apart are 5.4y apart, more than the 4y Meteor Stream
            sa.DrawGuidance(sa.Data.PartyList[i], tPos, 0, 10000, $"P2A_{_upm.Phase}_MeteorStreamGuide{i}", sa.Data.DefaultSafeColor);
        }
    }
    
    [ScriptMethod(name: "P2A_Clear Meteor Stream Guide",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9920"],
        userControl: Debugging, suppress: 500)]
    public void P2A_ClearMeteorStreamGuide(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 2000) return;
        sa.Method.RemoveDraw($"P2A_{_upm.Phase}_MeteorStreamGuide.*");
    }
    
    [ScriptMethod(name: "P2A_Meteor Stream Phase Transition",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9920"],
        userControl: Debugging, suppress: 500)]
    public void P2A_MeteorStreamPhase(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase > 2002) return;
        _upm.Phase = _upm.Phase switch
        {
            2001 => 2002,
            _ => 2001
        };
        sa.DebugMsg($"{_upm.Phase}");
        if (_upm.Phase != 2002) return;
        sa.Method.RemoveDraw($"P2A_2000.*");
    }
    
    [ScriptMethod(name: "P2A_Dalamud Dive (Opener)",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9920"],
        userControl: true, suppress: 500)]
    public async void P2A_OpenerDalamudDive(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase is not (2000 or 2001 or 2002)) return;
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _upm.Phase == 2002,
            ])) return;
        sa.DrawCircle(sa.Data.PartyList[1], 0, 3000, $"GEN_{_upm.Phase}_QuoteDalamudDive", 5f,
            sa.Data.DefaultDangerColor.WithW(1.5f));
    }

    #endregion P2A Opener 2000~2002
    
    #region P2B Bahamut's Favor 2010
    
    [ScriptMethod(name: "============= [P2B Bahamut's Favor] =============",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P2B_BahamutsFavor_Divider(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "P2B_Bahamut's Favor_Phase Transition",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9922"],
        userControl: Debugging)]
    public void P2B_BahamutsFavor_Phase(Event ev, ScriptAccessory sa)
    {
        _upm.Phase = 2010;
        _upm.P2.NaelObjId = ev.SourceId;
        sa.Method.RemoveDraw(@".*");
        sa.DebugMsg($"{_upm.Phase}");
        _pd.Init("P2 Doom");
    }
    
    [ScriptMethod(name: "P2B_Translucent Nael",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9922"],
        userControl: Debugging)]
    public void P2B_NaelFade(Event ev, ScriptAccessory sa)
    {
        if (sa.GetById(ev.SourceId) is not { } obj) return;
        sa.AlphaModify(obj, 0.5f);
    }
    
    [ScriptMethod(name: "P2B_Firehorn Fireball Tether Stack",
        eventType: EventTypeEnum.Tether, eventCondition: ["Id:0005"],
        userControl: true)]
    public void P2B_FirehornTetherStack(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 2010) return;
        sa.DebugMsg($"Players hit last round: {string.Join(", ", _upm.P2.FireballHitPlayers.Select(x => sa.GetPlayerJobByIndex(x)))}");
        
        var myObj = sa.Data.MyObject;
        if (myObj == null) return;
        var tIdx = sa.GetPlayerIdIndex((uint)ev.TargetId);
        if (!sa.IsValidPartyIndex(tIdx)) return;
        
        _upm.P2.FireballRound++;
        sa.DebugMsg($"Firehorn tether on {sa.GetPlayerJobByIndex(tIdx)}, round {_upm.P2.FireballRound}");

        switch (_upm.P2.FireballRound)
        {
            case 3:
                sa.TextInfo("Lightning first, then Fireball");
                sa.TTS("Lightning first, then fireball");
                break;
            case 4:
                sa.TextInfo("Fireball first, then Lightning");
                sa.TTS("Fireball first, then lightning");
                break;
        }
        
        var draw = sa.DrawCircle(ev.TargetId, 0, 5200, 
            $"P2B_{_upm.Phase}_FirehornStackAoe", 4, sa.Data.DefaultSafeColor, draw: false);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, draw, dp =>
        {
            var myIndex = sa.GetMyIndex();
            var fireDanger = myObj.HasStatus(464) ||
                             (_upm.P2.FireballRound == 2 && tIdx != myIndex && _upm.P2.FireballHitPlayers.Contains(myIndex)) ||
                             (_upm.P2.FireballRound == 3 && tIdx != myIndex && _upm.P2.FireballHitPlayers.Contains(myIndex));
            dp.Color = (fireDanger ? sa.Data.DefaultDangerColor : sa.Data.DefaultSafeColor).WithW(1.5f);
        });
    }
    
    [ScriptMethod(name: "P2B_Firehorn Fireball Hit Reset",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9925"], 
        userControl: Debugging, suppress: 500)]
    public void P2B_FirehornHitReset(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 2010) return;
        _upm.P2.FireballHitPlayers.Clear();
        _upm.P2.FireballHitRecordRound = _upm.P2.FireballRound;
    }

    [ScriptMethod(name: "P2B_Firehorn Fireball Hit Tracking",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9925"], 
        userControl: Debugging)]
    public async void P2B_FirehornHitRecord(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 2010) return;
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _upm.P2.FireballHitRecordRound == _upm.P2.FireballRound,
            ])) return;
        var tIdx = sa.GetPlayerIdIndex((uint)ev.TargetId);
        if (!sa.IsValidPartyIndex(tIdx)) return;
        _upm.P2.FireballHitPlayers.Add(tIdx);
    }
    
    [ScriptMethod(name: "P2B_Firehorn Fireball Stack Cleanup",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9925", "TargetIndex:1"], 
        userControl: Debugging)]
    public void P2B_FirehornStackCleanup(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 2010) return;
        sa.Method.RemoveDraw(@"P2B_\d{4}_FirehornStackAoe");
        sa.Method.RemoveDraw(@"P2B_\d{4}_FirehornStackGuide");
    }
    
    [ScriptMethod(name: "P2B_Chain Lightning AoE",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9927"],
        userControl: true)]
    public void P2B_ChainLightningAoe(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 2010) return;
        var color = new Vector4(0.4f, 0.2f, 1f, 2f);
        sa.DrawCircle(ev.TargetId, 0, 6000, $"P2B_ChainLightningAoe", 5f, color);
        // if (!Debugging && sa.GetPlayerIdIndex((uint)ev.TargetId) != sa.GetMyIndex()) return;
        if (!SpecialMode) return;
        sa.DrawLockOn(ev.TargetId, 507, 0, 6000, new(1.5f, 1.5f, 1.5f), 7f / 6f);
    }
    
    [ScriptMethod(name: "P2B_Chain Lightning AoE Cleanup",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9928"],
        userControl: true)]
    public void P2B_ChainLightningCleanup(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 2010) return;
        sa.Method.RemoveDraw(@"P2B_ChainLightningAoe");
    }
    
    private void DrawNaelQuoteSkill(ScriptAccessory sa,
        NaelQuoteSkills nqs, int delayMs, int destroyMs, Vector4 color)
    {
        switch (nqs)
        {
            case NaelQuoteSkills.IronChariot:
                sa.DrawCircle(_upm.P2.NaelObjId, delayMs, destroyMs, $"GEN_{_upm.Phase}_QuoteIronChariot", 8.55f, color);
                break;
            case NaelQuoteSkills.LunarDynamo:
                sa.DrawDonut(_upm.P2.NaelObjId, delayMs, destroyMs, $"GEN_{_upm.Phase}_QuoteLunarDynamo", 22, 6, color);
                break;
            case NaelQuoteSkills.ThermionicBeam:
                sa.DrawCircle(Center, delayMs, destroyMs, $"GEN_{_upm.Phase}_QuoteThermionicBeam", 4, color);
                if (!SpecialMode) break;
                sa.DrawOmen(Center, 453, delayMs, destroyMs, new(4, 8, 4));
                break;
            case NaelQuoteSkills.DalamudDive:
                var dp = sa.DrawCircle(_upm.P2.NaelObjId, delayMs, destroyMs, 
                    $"GEN_{_upm.Phase}_QuoteDalamudDive", 5f, color, draw: false);
                dp.SetEnmityOrder(true, 1);
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                break;
            case NaelQuoteSkills.RavenDive:
                for (int i = 0; i < sa.Data.PartyList.Count; i++)
                    sa.DrawCircle(sa.Data.PartyList[i], delayMs, destroyMs, 
                        $"GEN_{_upm.Phase}_QuoteRavenDive{i}", 3f, color, byTime: true);
                break;
            case NaelQuoteSkills.MeteorStream:
                for (int i = 0; i < sa.Data.PartyList.Count; i++)
                    sa.DrawCircle(sa.Data.PartyList[i], delayMs, destroyMs, 
                        $"GEN_{_upm.Phase}_QuoteMeteorStream{i}", 4f, color);
                break;
            default:
                break;
        };
    }

    [ScriptMethod(name: "P2B_Nael Quotes",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:regex:^(649[234567]|650[01])$"], 
        userControl: true)]
    public async void P2B_NaelQuotes(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 2010) return;
        var quoteId = ev.Id0();
        var color = new Vector4(0.4f, 1, 1, 1.5f);
        switch (quoteId)
        {
            case 0x6492:
                // O hallowed moon, shine you the iron path!
                DrawNaelQuoteSkill(sa, NaelQuoteSkills.LunarDynamo, 0, 5000, color);
                DrawNaelQuoteSkill(sa, NaelQuoteSkills.IronChariot, 5000, 3000, color);
                sa.TextInfo("In -> Out", isWarning: true);
                sa.TTS("In, then out");
                break;
            case 0x6493:
                // O hallowed moon, take fire and scorch my foes!
                DrawNaelQuoteSkill(sa, NaelQuoteSkills.LunarDynamo, 0, 5000, color);
                DrawNaelQuoteSkill(sa, NaelQuoteSkills.ThermionicBeam, 5000, 3000, sa.Data.DefaultSafeColor);
                sa.TextInfo("In -> Stack", isWarning: true);
                sa.TTS("In, then stack");
                break;
            case 0x6494:
                // Blazing path, lead me to iron rule!
                DrawNaelQuoteSkill(sa, NaelQuoteSkills.ThermionicBeam, 0, 5000, sa.Data.DefaultSafeColor);
                DrawNaelQuoteSkill(sa, NaelQuoteSkills.IronChariot, 5000, 3000, color);
                sa.TextInfo("Stack -> Out", isWarning: true);
                sa.TTS("Stack, then out");
                break;
            case 0x6495:
                // Take fire, O hallowed moon!
                DrawNaelQuoteSkill(sa, NaelQuoteSkills.ThermionicBeam, 0, 5000, sa.Data.DefaultSafeColor);
                DrawNaelQuoteSkill(sa, NaelQuoteSkills.LunarDynamo, 5000, 3000, color);
                sa.TextInfo("Stack -> In", isWarning: true);
                sa.TTS("Stack, then in");
                break;
            case 0x6496:
                // From on high I descend, the iron path to walk!
                DrawNaelQuoteSkill(sa, NaelQuoteSkills.RavenDive, 0, 5000, color);
                DrawNaelQuoteSkill(sa, NaelQuoteSkills.IronChariot, 5000, 3000, color);
                sa.TextInfo("Spread -> Out", isWarning: true);
                sa.TTS("Spread, then out");
                break;
            case 0x6497:
                // From on high I descend, the hallowed moon to call!
                DrawNaelQuoteSkill(sa, NaelQuoteSkills.RavenDive, 0, 5000, color);
                DrawNaelQuoteSkill(sa, NaelQuoteSkills.LunarDynamo, 5000, 3000, color);
                sa.TextInfo("Spread -> In", isWarning: true);
                sa.TTS("Spread, then in");
                break;
            case 0x6500:
                // Fleeting light! Amid a rain of stars, exalt you the red moon!
                DrawNaelQuoteSkill(sa, NaelQuoteSkills.MeteorStream, 12000, 3000, color);
                DrawNaelQuoteSkill(sa, NaelQuoteSkills.DalamudDive, 15000, 2000, color);
                var runId1 = _runId;
                var phase1 = _upm.Phase;
                await Task.Delay(8000);
                if (_runId != runId1 || _upm.Phase != phase1) return;
                sa.TextInfo("Stay spread", isWarning: true);
                sa.TTS("Stay spread");
                break;
            case 0x6501:
                // Fleeting light! 'Neath the red moon, scorch you the earth!
                DrawNaelQuoteSkill(sa, NaelQuoteSkills.DalamudDive, 13000, 3000, color);
                DrawNaelQuoteSkill(sa, NaelQuoteSkills.ThermionicBeam, 15000, 2000, sa.Data.DefaultSafeColor);
                var runId2 = _runId;
                var phase2 = _upm.Phase;
                await Task.Delay(8000);
                if (_runId != runId2 || _upm.Phase != phase2) return;
                sa.TextInfo("After Nael jumps: tank out, party stack", isWarning: true);
                sa.TTS("Tank out, party stack");
                break;
        }
    }
    
    [ScriptMethod(name: "P2B_Doom Tracking",
        eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:210"], 
        userControl: Debugging)]
    public void P2B_DoomRecord(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 2010) return;
        if (ev.SourceId != 0xE0000000) return;
        var idx = sa.GetPlayerIdIndex((uint)ev.TargetId);
        if (!sa.IsValidPartyIndex(idx)) return;
        var time = ev.DurationMilliseconds();
        var priVal = time switch
        {
            > 15000 => 10,
            > 9000 => 20,
            _ => 30
        };
        _pd.AddPriority(idx, priVal);
        sa.DebugMsg($"{sa.GetPlayerJobByIndex(idx)} Doom {4 - priVal / 10}", order: 30 - priVal);
        
        if (priVal != 30) return;
        _upm.P2.Doom1Recorded = true;
    }

    [ScriptMethod(name: "P2B_Wings of Salvation Pre-Guide",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9930"],
        userControl: true)]
    public async void P2B_WingsOfSalvationPreGuide(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 2010) return;
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _upm.P2.Doom1Recorded,
            ])) return;

        var castIdx = _upm.P2.WingsOfSalvationIndex;
        var targetEntry = _pd.SelectSpecificPriorityIndex(castIdx, true);
        var tIdx = targetEntry.Key;
        _upm.P2.WingsOfSalvationIndex++;
        
        if (!Debugging && tIdx != sa.GetMyIndex()) return;
        var tPos = ev.EffectPosition;
        var dpName = $"P2B_{_upm.Phase}_{tIdx}_Doom_Ready";
        sa.DrawGuidance(sa.Data.PartyList[tIdx], tPos, 0, 4500, dpName, sa.Data.DefaultDangerColor);
        
    }

    [ScriptMethod(name: "P2B_Wings of Salvation Cleanse Guide",
        eventType: EventTypeEnum.ObjectChanged, eventCondition: ["Operate:Add", "DataId:2003412"],
        userControl: true)]
    public async void P2B_WingsOfSalvationGuide(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 2010) return;
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _upm.P2.Doom1Recorded,
            ])) return;
        
        var foodIdx = _upm.P2.CleansePuddleIndex;
        _upm.P2.CleansePuddleIndex++;
        var targetEntry = _pd.SelectSpecificPriorityIndex(foodIdx, true);
        var tIdx = targetEntry.Key;
        sa.Method.RemoveDraw($"P2B_{_upm.Phase}_{tIdx}_Doom_Ready");
        
        // Draw the AoE
        var dpRangeName = $"P2B_{_upm.Phase}_{tIdx}_{ev.SourceId}_Doom_Aoe";
        if (tIdx != sa.GetMyIndex())
            sa.DrawCircle(ev.SourceId, 0, 4500, dpRangeName, 1.25f, new Vector4(1, 0, 0, 4));
        
        // Draw the guide
        if (!Debugging && tIdx != sa.GetMyIndex()) return;
        var tPos = ev.SourcePosition;
        var dpGuideName = $"P2B_{_upm.Phase}_{tIdx}_{ev.SourceId}_Doom_Go";
        sa.DrawGuidance(sa.Data.PartyList[tIdx], tPos, 0, 4500, dpGuideName, sa.Data.DefaultSafeColor);
    }
    
    [ScriptMethod(name: "P2B_Doom Cleansed Cleanup",
        eventType: EventTypeEnum.StatusRemove, eventCondition: ["StatusID:210"],
        userControl: Debugging)]
    public void P2B_DoomClearedCleanup(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 2010) return;
        if (ev.SourceId != 0xE0000000) return;
        var tIdx = sa.GetPlayerIdIndex((uint)ev.TargetId);
        if (!sa.IsValidPartyIndex(tIdx)) return;
        var removeDrawName = @$"P2B_{_upm.Phase}_{tIdx}_\d+_Doom.*";
        sa.Method.RemoveDraw(removeDrawName);
        
        // Once nobody on the field has Doom anymore, reset the priority table
        foreach (var member in sa.Data.PartyList)
        {
            if (sa.GetById(member) is not { } obj) continue;
            if (((IPlayerCharacter)obj).HasStatus(210)) return;
        }
        _pd.Init("P2 Doom");
        _upm.P2.ResetDoom();
        sa.DebugMsg($"Doom state reset");
    }
    
    [ScriptMethod(name: "P2B_Cleanse Puddle Gone Cleanup",
        eventType: EventTypeEnum.ObjectChanged, eventCondition: ["Operate:Remove", "DataId:2003412"],
        userControl: Debugging)]
    public void P2B_PuddleGoneCleanup(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 2010) return;
        var sid = ev.SourceId;
        var removeDrawName = @$"P2B_{_upm.Phase}_\d_{sid}_Doom.*";
        sa.Method.RemoveDraw(removeDrawName);
    }
    
    #endregion P2B Bahamut's Favor 2010
    
    #region P2C Divebombs 2010
    
    [ScriptMethod(name: "============= [P2C Divebombs] =============",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P2C_Divebombs_Divider(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "P2C_Dragon Position Tracking",
        eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(816[34567])$"], 
        userControl: Debugging)]
    public void P2C_RecordDragons(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase is not (2002 or 2010)) return;
        lock (_stateLock)
        {
            var spos = ev.SourcePosition;
            // A is 0, increasing clockwise
            var region = spos.GetRadian(Center).RadianToRegion(8, 4, isDiagDiv: true, isCw: true);
            _upm.P2.Dragons.Add(new OuterDragon { ObjectId = ev.SourceId, Region = region });
            
            if (_upm.P2.Dragons.Count < 5) return;
            _upm.P2.Dragons = _upm.P2.Dragons.OrderBy(x => x.Region).ToList();
            _upm.P2.SolveDivebombBaitSpots();
            sa.DebugMsg($"Dragon directions {string.Join(", ", _upm.P2.Dragons.Select(x => x.Region))}\n" +
                        $"Bait spots {string.Join(", ", _upm.P2.DivebombBaitSpots.Select(x => x))}");
        }
    }

    [ScriptMethod(name: "P2C_Divebomb Round Counter",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0014"],
        userControl: Debugging)]
    public void P2C_DivebombRoundCounter(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 2010) return;
        var tIdx = sa.GetPlayerIdIndex((uint)ev.TargetId);
        if (!sa.IsValidPartyIndex(tIdx)) return;
        _upm.P2.DivebombMarkRound++;
        _upm.P2.DivebombBaiters.Add(tIdx);
        sa.DebugMsg($"Divebomb round {_upm.P2.DivebombMarkRound}: marker on {sa.GetPlayerJobByIndex(tIdx)}", order: 0);
    }

    [ScriptMethod(name: "P2C_Divebomb Return Callout",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(993[12345])$"],
        userControl: true, suppress: 500)]
    public async void P2C_DivebombReturnCall(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 2010) return;
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _upm.P2.DivebombMarkRound > _upm.P2.DivebombReturnHandledRound,
            ])) return;
        
        var round = _upm.P2.DivebombReturnHandledRound + 1;
        if (round > _upm.P2.DivebombMarkRound) return;
        _upm.P2.DivebombReturnHandledRound = round;

        var tIdx = _upm.P2.DivebombBaiters[round - 1];
        if (tIdx != sa.GetMyIndex()) return;
        sa.TextInfo("Get back in!", destroyMs: 2000, isWarning: true);
        sa.TTS("Get back in");
    }

    [ScriptMethod(name: "P2C_Divebomb Bait AoE Preview",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0014"],
        userControl: true)]
    public async void P2C_DivebombBaitAoe(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 2010) return;
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _upm.P2.DivebombMarkRound > _upm.P2.DivebombAoeHandledRound,
            ])) return;

        var round = _upm.P2.DivebombAoeHandledRound + 1;
        if (round > _upm.P2.DivebombMarkRound) return;
        _upm.P2.DivebombAoeHandledRound = round;
        
        var tid = ev.TargetId;
        if (!Debugging && tid != sa.Data.Me) return;
        
        var color = new Vector4(0.4f, 1, 1, 0.5f);
        switch (round)
        {
            case 1:
                sa.DrawRect(_upm.P2.Dragons[0].ObjectId, tid, 
                    0, 7300, $"P2C_{_upm.Phase}_DivebombBaitAoe_{_upm.P2.Dragons[0].ObjectId}", 0, 20, 45, color);
                sa.DrawRect(_upm.P2.Dragons[1].ObjectId, tid, 
                    0, 7300, $"P2C_{_upm.Phase}_DivebombBaitAoe_{_upm.P2.Dragons[1].ObjectId}", 0, 20, 45, color);
                break;
            case 2:
                sa.DrawRect(_upm.P2.Dragons[2].ObjectId, tid, 
                    0, 7300, $"P2C_{_upm.Phase}_DivebombBaitAoe_{_upm.P2.Dragons[2].ObjectId}", 0, 20, 45, color);
                break;
            case 3:
                sa.DrawRect(_upm.P2.Dragons[3].ObjectId, tid, 
                    0, 7300, $"P2C_{_upm.Phase}_DivebombBaitAoe_{_upm.P2.Dragons[3].ObjectId}", 0, 20, 45, color);
                sa.DrawRect(_upm.P2.Dragons[4].ObjectId, tid, 
                    0, 7300, $"P2C_{_upm.Phase}_DivebombBaitAoe_{_upm.P2.Dragons[4].ObjectId}", 0, 20, 45, color);
                break;
            default:
                return;
        }
    }

    [ScriptMethod(name: "P2C_Divebomb Bait Spot Guide",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0014"],
        userControl: true)]
    public async void P2C_DivebombBaitSpotGuide(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 2010) return;
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _upm.P2.DivebombMarkRound > _upm.P2.DivebombGuideHandledRound,
            ])) return;

        var round = _upm.P2.DivebombGuideHandledRound + 1;
        if (round > _upm.P2.DivebombMarkRound) return;
        _upm.P2.DivebombGuideHandledRound = round;
        
        var tid = ev.TargetId;
        if (!Debugging && tid != sa.Data.Me) return;
        
        var guideIndex = round - 1;
        if (guideIndex < 0 || guideIndex >= _upm.P2.DivebombBaitSpots.Count) return;
        var guideRegion = _upm.P2.DivebombBaitSpots[guideIndex];

        sa.DebugMsg($"{sa.GetPlayerJobById((uint)tid)} -> direction {guideRegion}, baits round {round}", order: 1);
        var tPos = new Vector3(0, 0, -20).RotateAndExtend(Center, -30f.DegToRad() * guideRegion);
        // Name the guide after the dragon's ObjId so it's easy to remove; conveniently, [1, 2, 3] each belong to one of the three rounds.
        var dragonObjId = _upm.P2.Dragons[round].ObjectId;
        sa.DrawGuidance(tid, tPos, 0, 6000, $"P2C_{_upm.Phase}_DivebombBaitSpotGuide_{dragonObjId}", sa.Data.DefaultSafeColor);
        if (!SpecialMode) return;
        sa.DrawCountDown(tPos, 2300, objIdBias: (uint)round);
    }

    #endregion P2C Divebombs 2010

    #region P2D Seventh Umbral Era 2020

    [ScriptMethod(name: "============= [P2D Seventh Umbral Era] =============",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P2D_SeventhUmbralEra_Divider(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "P2D_Phase Transition",
        eventType: EventTypeEnum.Targetable, eventCondition: ["DataId:8161", "Targetable:False"],
        // eventType: EventTypeEnum.Director, eventCondition: ["Command:80000001", "Instance:80037569"],
        userControl: Debugging)]
    public void P2D_Phase(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 2010) return;
        _upm.Phase = 2020;
        sa.Method.RemoveDraw($"P2[A-Z]_2010_.*");
        sa.Method.RemoveDraw($"GEN.*");
        sa.DebugMsg($"{_upm.Phase}");
        
        if (sa.GetById(_upm.P2.NaelObjId) is not { } obj) return;
        // sa.AlphaModify(obj, 1f, currentAlpha => currentAlpha <= 0.6f);
    }

    [ScriptMethod(name: "P2D_Knockback Spot Guide",
        eventType: EventTypeEnum.Director, eventCondition: ["Command:80000001", "Instance:80037569"],
        userControl: true)]
    public async void P2D_KnockbackGuide(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase is not (2010 or 2020)) return;
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _upm.Phase == 2020,
            ])) return;

        sa.DrawGuidance(new Vector3(-7.45f, 0, -4.19f), 0, 10000, $"P2D_{_upm.Phase}_KnockbackGuide", sa.Data.DefaultSafeColor);
        sa.DrawKnockBack(Center, 0, 10000, $"P2D_{_upm.Phase}_KnockbackAoe", 1f, 10f, sa.Data.DefaultDangerColor.WithW(1.5f));
    }

    [ScriptMethod(name: "P2D_Seventh Umbral Era Cleanup & Phase Transition",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(993[79])$"],
        userControl: Debugging, suppress: 500)]
    public void P2D_SeventhUmbralEraCleanup(Event ev, ScriptAccessory sa)
    {
        var aid = ev.ActionId;
        sa.Method.RemoveDraw($".*");
        
        // Last hit: Calamitous Blaze 9939
        if (aid != 9939) return;
        _upm.Phase = 3000;
        sa.DebugMsg($"{_upm.Phase}");
    }

    #endregion P2D Seventh Umbral Era 2020
    
    #endregion P2

    #region P3

    [ScriptMethod(name: "———————— [P3] ————————",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P3_Divider(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "P3_Translucent Bahamut",
        eventType: EventTypeEnum.Targetable, eventCondition: ["Targetable:True", "DataId:8168"],
        userControl: Debugging, suppress: 500)]
    public void P3_BahamutFade(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3000) return;
        if (sa.GetById(ev.SourceId) is not { } obj) return;
        sa.AlphaModify(obj, 0.5f);
    }

    [ScriptMethod(name: "P3_Bahamut ID Tracking",
        eventType: EventTypeEnum.Targetable, eventCondition: ["Targetable:True", "DataId:8168"],
        userControl: Debugging, suppress: 500)]
    public void P3A_RecordBahamutId(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3000) return;
        if (_upm.P3.BahamutObjId != 0) return;
        _upm.P3.BahamutObjId = ev.SourceId;
        _upm.P3.LoadPhaseRotation(_upm.Phase);
    }
    
    [ScriptMethod(name: "P3_Nael ID Tracking",
        eventType: EventTypeEnum.PlayActionTimeline, eventCondition: ["Id:7747", "SourceDataId:8161"],
        userControl: Debugging)]
    public void P3_RecordNaelId(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase is not (3000 or 3010)) return;
        if (_upm.P3.NaelObjId != 0) return;
        _upm.P3.NaelObjId = ev.SourceId;
    }
    
    [ScriptMethod(name: "P3_Twintania ID Tracking",
        eventType: EventTypeEnum.PlayActionTimeline, eventCondition: ["Id:7748", "SourceDataId:8159"],
        userControl: Debugging)]
    public void P3_RecordTwintaniaId(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase is not (3000 or 3010)) return;
        if (_upm.P3.TwintaniaObjId != 0) return;
        _upm.P3.TwintaniaObjId = ev.SourceId;
    }

    [ScriptMethod(name: "P3_Boss Position & Direction Tracking",
        eventType: EventTypeEnum.PlayActionTimeline, 
        eventCondition: ["SourceDataId:regex:^(8161|8159|8168)$", "Id:regex:^(774[78])$"], 
        userControl: Debugging)]
    public void P3_RecordBossPositions(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase.GetDecimalDigit(3) != 3) return;
        var spos = ev.SourcePosition;
        if (Vector3.Distance(spos, Center) < 18) return;
        var sdid = ev.SourceDataId();
        switch (sdid) {
            case 8161:
                _upm.P3.NaelPos = spos;
                _upm.P3.NaelDir = spos.GetRadian(Center).RadianToRegion(8, isDiagDiv: true);
                _upm.P3.NaelRecordedPhase = _upm.Phase;
                sa.DebugMsg($"{_upm.Phase} Nael direction updated: {_upm.P3.NaelDir} {_upm.P3.NaelPos.ToStr()}");
                break;
            case 8159:
                _upm.P3.TwintaniaPos = spos;
                _upm.P3.TwintaniaDir = spos.GetRadian(Center).RadianToRegion(8, isDiagDiv: true);
                _upm.P3.TwintaniaRecordedPhase = _upm.Phase;
                sa.DebugMsg($"{_upm.Phase} Twintania direction updated: {_upm.P3.TwintaniaDir} {_upm.P3.TwintaniaPos.ToStr()}");
                break;
            case 8168:
                _upm.P3.BahamutPos = spos;
                _upm.P3.BahamutDir = spos.GetRadian(Center).RadianToRegion(8, isDiagDiv: true);
                _upm.P3.BahamutRecordedPhase = _upm.Phase;
                sa.DebugMsg($"{_upm.Phase} Bahamut direction updated: {_upm.P3.BahamutDir} {_upm.P3.BahamutPos.ToStr()} ");
                break;
        }
    }
    
    [ScriptMethod(name: "P3_Bahamut Skill Tracking",
        eventType: EventTypeEnum.ActionEffect, 
        eventCondition: ["ActionId:regex:^(994[012])$", "TargetIndex:1"],
        userControl: Debugging, suppress: 500)]
    public void P3_BahamutSkillTracker(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase.GetDecimalDigit(3) != 3) return;
        
        var actionId = ev.ActionId;
        switch (actionId)
        {
            case UcobParamsP3.FlareBreath:
            {
                if (_upm.P3.SkillRotation[_upm.P3.SkillIndex] == BahamutSkills.TripleFlareBreath)
                {
                    _upm.P3.TripleBreathHitCount++;
                    sa.DebugMsg($"Triple Flare Breath hit count {_upm.P3.TripleBreathHitCount}");
                    if (_upm.P3.TripleBreathHitCount < 3) return;
                    _upm.P3.TripleBreathHitCount = 0;
                }
                sa.Method.RemoveDraw($"P3_{_upm.Phase}_FlareBreath.*");
                break;
            }
            case UcobParamsP3.Gigaflare:
                break;
            case UcobParamsP3.Flatten:
                break;
        }
        _upm.P3.AdvanceSkillIndex();
        sa.DebugMsg($"Current skill index: {_upm.P3.SkillIndex}, phase {_upm.Phase}");
    }

    private async void P3_DrawFlareBreath(ScriptAccessory sa)
    {
        try
        {
            if (!await WaitUntilConditions(
                timeoutMs: 500,
                conditions:
                [
                    () => _upm.P3.FlareBreathDrawnIndex < _upm.P3.SkillIndex,
                    () => _upm.P3.SkillRotation[_upm.P3.SkillIndex] is BahamutSkills.FlareBreath or BahamutSkills.TripleFlareBreath,
                    () => _upm.P3.BahamutObjId != 0,
                ])) return;

            _upm.P3.FlareBreathDrawnIndex = _upm.P3.SkillIndex;
            var color = new Vector4(0.3f, 1f, 1, 1f);
            var dp = sa.DrawFan(_upm.P3.BahamutObjId, 0, 15000, 
                $"P3_{_upm.Phase}_FlareBreathAoe", 90f.DegToRad(), 0, 30f, 0,
                color, draw: false);
            dp.SetOwnerTarget(false);
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }
        catch (Exception e)
        {
            sa.DebugMsg($"Error {e}");
        }
    }

    [ScriptMethod(name: "P3_Flare Breath AoE (Opener)",
        eventType: EventTypeEnum.Targetable, eventCondition: ["Targetable:True", "DataId:8168"],
        userControl: true)]
    public void P3_FlareBreathOpener(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase >= 3100) return;
        P3_DrawFlareBreath(sa);
    }

    [ScriptMethod(name: "P3_Flare Breath AoE (Mid-phase)",
        eventType: EventTypeEnum.ActionEffect, 
        eventCondition: ["ActionId:regex:^(994[0124])$", "TargetIndex:1"],
        userControl: true, suppress: 500)]
    public void P3_FlareBreathMid(Event ev, ScriptAccessory sa)
    {
        // Flare Breath only follows Flare Breath, Tempest Wing, Flatten (tankbuster) or Gigaflare
        if (_upm.Phase is > 3999 or < 3000) return;
        P3_DrawFlareBreath(sa);
    }

    [ScriptMethod(name: "P3_Twisting Dive / Lunar Dive / Megaflare Dive",
        eventType: EventTypeEnum.StartCasting, 
        eventCondition: ["ActionId:regex:^(9906|9923|9953)$"],
        userControl: true)]
    public void P3_Dives(Event ev, ScriptAccessory sa)
    {
        const uint TwistingDive = 9906;
        const uint LunarDive = 9923;
        const uint MegaflareDive = 9953;

        var color = new Vector4(0.3f, 1f, 1, 1f);
        
        var dpName = ev.ActionId switch
        {
            TwistingDive => $"P3_{_upm.Phase}_TwistingDive",
            LunarDive => $"P3_{_upm.Phase}_LunarDive",
            MegaflareDive => $"P3_{_upm.Phase}_MegaflareDive",
            _ => ""
        };

        sa.DrawRect(ev.SourceId, 0, 0, 4000, 
            dpName, 0, ev.ActionId == MegaflareDive ? 12 : 8, 45, color);
    }

    [ScriptMethod(name: "P3_Twisting Dive / Lunar Dive / Megaflare Dive Cleanup",
        eventType: EventTypeEnum.ActionEffect, 
        eventCondition: ["ActionId:regex:^(9906|9923|9953)$", "TargetIndex:1"],
        userControl: Debugging)]
    public void P3_DivesCleanup(Event ev, ScriptAccessory sa)
    {
        const uint TwistingDive = 9906;
        const uint LunarDive = 9923;
        const uint MegaflareDive = 9953;
        
        var dpName = ev.ActionId switch
        {
            TwistingDive => @"P3_\d{4}_TwistingDive",
            LunarDive => @"P3_\d{4}_LunarDive",
            MegaflareDive => @"P3_\d{4}_MegaflareDive",
            _ => ""
        };
        sa.Method.RemoveDraw(dpName);
    }
    
    [ScriptMethod(name: "P3_Tether Highlight",
        eventType: EventTypeEnum.Tether, eventCondition: ["Id:0004"],
        userControl: Debugging, suppress: 10000)]
    public void P3_TetherHighlight(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase is not (3100 or 3300)) return;
        var color = new Vector4(1f, 1f, 0.1f, 1f);
        
        // First tether
        var draw1 = sa.DrawLine(ev.SourceId, ev.TargetId, 0, 6000, 
            $"P3_{_upm.Phase}_TetherHighlight1", 0, 1f, 1f, color, byY: true, draw: false);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, draw1, dp =>
        {
            for (int i = 0; i < sa.Data.PartyList.Count; i++)
            {
                var member = sa.Data.PartyList[i];
                if (sa.GetById(member) is not { } obj) continue;
                var tetherSourceList = sa.GetTetherSource((IBattleChara)obj, 0x0004);
                if (tetherSourceList.Count == 0) continue;
                if (tetherSourceList[0] != _upm.P3.BahamutObjId) continue;
                dp.Owner = member;
                break;
            }
        });
        
        // Second tether
        var draw2 = sa.DrawLine(ev.SourceId, ev.TargetId, 0, 6000, 
            $"P3_{_upm.Phase}_TetherHighlight2", 0, 1f, 1f, color, byY: true, draw: false);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, draw2, dp =>
        {
            for (int i = 0; i < sa.Data.PartyList.Count; i++)
            {
                var memberIndex = sa.Data.PartyList.Count - 1 - i;
                var member = sa.Data.PartyList[memberIndex];
                if (sa.GetById(member) is not { } obj) continue;
                var tetherSourceList = sa.GetTetherSource((IBattleChara)obj, 0x0004);
                if (tetherSourceList.Count == 0) continue;
                if (tetherSourceList[0] != _upm.P3.BahamutObjId) continue;
                dp.Owner = member;
                break;
            }
        });
    }
    
    [ScriptMethod(name: "P3_Earthshaker AoE",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0028"],
        userControl: Debugging)]
    public void P3_EarthshakerAoe(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase is not (3100 or 3500)) return;
        var color = new Vector4(0.7f, 0.7f, 0.3f, _upm.Phase == 3500 ? 1f : 0.7f);
        sa.DrawFan(Center, ev.TargetId, 0, 5000, 
            $"P3_{_upm.Phase}_EarthshakerAoe", 90f.DegToRad(), 0, 50f, 0, color);
    }
    
    [ScriptMethod(name: "P3_Remove Aetheric Profusion Screen VFX",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9905"],
        userControl: true, suppress: 500)]
    public void P3_ClearAethericProfusionVfx(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase.GetDecimalDigit(3) != 3) return;
        if (sa.GetById(ev.SourceId) is not { } obj) return;
        sa.Redraw(obj);
    }
    
    [ScriptMethod(name: "P3_Remove Trio Transition Screen VFX",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(995[456789])$"],
        userControl: true, suppress: 500)]
    public void P3_ClearTransitionVfx(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase.GetDecimalDigit(3) != 3) return;
        if (sa.GetById(ev.SourceId) is not { } obj) return;
        sa.Redraw(obj);
    }

    [ScriptMethod(name: "P3_Remove Gigaflare Screen VFX",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9942"],
        userControl: true, suppress: 500)]
    public void P3_ClearGigaflareVfx(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase.GetDecimalDigit(3) != 3) return;
        if (sa.GetById(ev.SourceId) is not { } obj) return;
        sa.Redraw(obj);
    }
    
    #region P3A Quickmarch Trio 3100-3150

    [ScriptMethod(name: "============= [P3A Quickmarch Trio] =============",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P3A_Quickmarch_Divider(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "P3A_Quickmarch Phase Setup",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9954"],
        userControl: Debugging)]
    public void P3A_QuickmarchPhase(Event ev, ScriptAccessory sa)
    {
        _upm.Phase = 3100;
        _upm.P3.BahamutObjId = ev.SourceId;
        sa.Method.RemoveDraw(".*");
        _pd.Init("P3 Quickmarch");
        _pd.AddPriorities([1, 2, 3, 4, 5, 6, 7, 8]);
        sa.DebugMsg($"{_upm.Phase}");
    }

    [ScriptMethod(name: "P3A_Megaflare Spread AoE",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9953"],
        userControl: true)]
    public void P3A_MegaflareSpread(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3100) return;
        var color = new Vector4(0.3f, 1f, 1, 0.5f);
        for (int i = 0; i < sa.Data.PartyList.Count; i++)
            sa.DrawCircle(sa.Data.PartyList[i], 0, 4000, 
                $"P3A_{_upm.Phase}_MegaflareSpreadAoe{i}", 5f, color, byTime: true);
        sa.TextInfo("Spread out!", destroyMs: 2000, isWarning: true);
        sa.TTS("Spread out");
    }

    [ScriptMethod(name: "P3A_Twister 8-Way Spread Guide",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9953"],
        userControl: true)]
    public async void P3A_TwisterSpreadGuide(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3100) return;
        
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _upm.P3.BahamutRecordedPhase == _upm.Phase
            ])) return;
        
        var baseRad = _upm.P3.BahamutDir * 45f.DegToRad();
        var basePos = new Vector3(0, 0, 19.5f).RotateAndExtend(Center, baseRad);
        List<float> rotDeg = [48, -48, 76, -76, 104, -104, 132, -132];
        for (int i = 0; i < sa.Data.PartyList.Count; i++)
        {
            var color = i switch
            {
                0 or 1 => new Vector4(0.1f, 0.1f, 1, 1),
                2 or 3 => new Vector4(0.1f, 1f, 0.1f, 1),
                _ => new Vector4(1, 0.1f, 0.1f, 1),
            };
            sa.DrawLine(Center, 0, 0, 4000, $"P3A_{_upm.Phase}_TwisterSpreadGuide_Line{i}",
                baseRad + rotDeg[i].DegToRad(), 20f, 25f, color);
            
            if (!Debugging && sa.GetMyIndex() != i) continue;
            var member = sa.Data.PartyList[i];
            sa.DrawGuidance(member, basePos.RotateAndExtend(Center, rotDeg[i].DegToRad()), 
                0, 4000, $"P3A_{_upm.Phase}_TwisterSpreadGuide{i}", sa.Data.DefaultSafeColor);
        }
    }

    [ScriptMethod(name: "P3A_Post-Twister Guide Lines",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9948"],
        userControl: true, suppress: 500)]
    public void P3A_PostTwisterLines(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3100) return;
        var baseRad = _upm.P3.BahamutDir * 45f.DegToRad();
        List<float> rotDeg = [0, 90f, -90f, 180f, 90f, -90f];
        
        for (int i = 0; i < 6; i++)
        {
            var startPos = i is 1 or 2
                ? new Vector3(0, 0, 10).RotateAndExtend(Center, baseRad + rotDeg[i].DegToRad())
                : Center;
            var length = i is 0 or 3 ? 25 : 10;
            var dp = sa.DrawLine(startPos, 0, 0, 5000, $"P3A_{_upm.Phase}_PostTwisterLines{i}", 
                baseRad + rotDeg[i].DegToRad(), 20f, length, sa.Data.DefaultSafeColor, draw: false);
            dp.Color = i switch
            {
                0 => new Vector4(1, 0.1f, 0.1f, 1),
                1 or 2 => new Vector4(0.1f, 1f, 0.1f, 1),
                3 => new Vector4(0.1f, 1f, 1f, 1),
                4 or 5 => new Vector4(0.1f, 0.1f, 1f, 1),
            };
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Line, dp);
        }

        var towerPos = new Vector3(0, 0, -6).RotateAndExtend(Center, baseRad);
        if (!SpecialMode) return;
        sa.DrawOmen(towerPos, 453, 0, 5000, new(4, 8, 4));
    }

    [ScriptMethod(name: "P3A_Quickmarch Marker Collection",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(002[78])$"],
        userControl: Debugging)]
    public void P3A_QuickmarchMarkers(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3100) return;
        lock (_stateLock)
        {
            var iconId = ev.Id0();
            var priVal = iconId switch
            {
                0x0027 => 10, // Stack
                0x0028 => 20, // Earthshaker
                _ => 0
            };
            var tidx = sa.GetPlayerIdIndex((uint)ev.TargetId);
            if (!sa.IsValidPartyIndex(tidx)) return;
            _pd.AddPriority(tidx, priVal);
            _pd.AddActionCount();
            sa.DebugMsg($"{sa.GetPlayerJobByIndex(tidx)} marked: {(priVal == 10 ? "Stack" : "Earthshaker")}", priVal + _pd.ActionCount);
            if (_pd.ActionCount != 6) return;
            sa.DebugMsg($"Quickmarch markers collected", 30);
        }
    }

    [ScriptMethod(name: "P3A_Post-Twister Guide",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(002[78])$"],
        userControl: true, suppress: 500)]
    public async void P3A_PostTwisterGuide(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3100) return;
        if (!await WaitUntilConditions(
            timeoutMs: 4000,
            conditions:
            [
                () => _pd.ActionCount == 6,
            ])) return;
        for (int i = 0; i < sa.Data.PartyList.Count; i++)
        {
            // Descending order
            // 0 1 2: Earthshakers north, east, west
            // 3 4 5: stack south
            // 6 7: tethers east, west
            var pdEntry = _pd.SelectSpecificPriorityIndex(i, true);
            if (!Debugging && sa.GetMyIndex() != pdEntry.Key) continue;
            var member = sa.Data.PartyList[pdEntry.Key];
            DrawQuickmarchGuide(sa, member, i);
        }
    }

    private void DrawQuickmarchGuide(ScriptAccessory sa, ulong member, int pdIndex)
    {
        switch (pdIndex)
        {
            case 0 or 1 or 2:
                var rotRad1 = pdIndex switch
                {
                    0 => 0,
                    1 => -90f.DegToRad(),
                    2 => 90f.DegToRad()
                };
                var tPos1 = new Vector3(0, 0, 20f).RotateAndExtend(Center, rotRad1 + _upm.P3.BahamutDir * 45f.DegToRad());
                sa.DrawGuidance(member, tPos1, 0, 5000, $"P3A_{_upm.Phase}_QuickmarchGuide{pdIndex}_Earthshaker", sa.Data.DefaultSafeColor);
                break;
            case 3 or 4 or 5:
                var tPos2 = new Vector3(0, 0, -6f).RotateAndExtend(Center, _upm.P3.BahamutDir * 45f.DegToRad());
                sa.DrawGuidance(member, tPos2, 0, 5000, $"P3A_{_upm.Phase}_QuickmarchGuide{pdIndex}_Stack", sa.Data.DefaultSafeColor);
                break;
            case 6 or 7:
                var rotRad3 = pdIndex switch
                {
                    6 => -90f.DegToRad(),
                    7 => 90f.DegToRad(),
                };
                var tPos3 = new Vector3(0, 0, 5f).RotateAndExtend(Center, rotRad3 + _upm.P3.BahamutDir * 45f.DegToRad());
                sa.DrawGuidance(member, tPos3, 0, 5000, $"P3A_{_upm.Phase}_QuickmarchGuide{pdIndex}_Tether", sa.Data.DefaultSafeColor);
                break;
        }
    }

    [ScriptMethod(name: "P3A_Tempest Wing Tether AoE",
        eventType: EventTypeEnum.Tether, eventCondition: ["Id:0004"],
        userControl: Debugging, suppress: 10000)]
    public void P3A_TetherBuster(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3100) return;
        var color = sa.Data.DefaultDangerColor;
        
        // Only show the AoE while the tether is on a tank
        var draw1 = sa.DrawCircle(ev.TargetId, 0, 6000, 
            $"P3A_{_upm.Phase}_TetherBusterAoe1", 5f, color, draw: false);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, draw1, dp =>
        {
            for (int i = 0; i < sa.Data.PartyList.Count; i++)
            {
                var member = sa.Data.PartyList[i];
                if (sa.GetById(member) is not { } obj) continue;
                var tetherSourceList = sa.GetTetherSource((IBattleChara)obj, 0x0004);
                if (tetherSourceList.Count == 0) continue;
                if (tetherSourceList[0] != _upm.P3.BahamutObjId) continue;
                dp.Owner = member;
                dp.Color = dp.Color.WithW(i <= 1 ? 1 : 0);
                break;
            }
        });
        
        var draw2 = sa.DrawCircle(ev.TargetId, 0, 6000, 
            $"P3A_{_upm.Phase}_TetherBusterAoe2", 5f, color, draw: false);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, draw2, dp =>
        {
            for (int i = 0; i < sa.Data.PartyList.Count; i++)
            {
                var memberIndex = sa.Data.PartyList.Count - 1 - i;
                var member = sa.Data.PartyList[memberIndex];
                if (sa.GetById(member) is not { } obj) continue;
                var tetherSourceList = sa.GetTetherSource((IBattleChara)obj, 0x0004);
                if (tetherSourceList.Count == 0) continue;
                if (tetherSourceList[0] != _upm.P3.BahamutObjId) continue;
                dp.Owner = member;
                dp.Color = dp.Color.WithW(memberIndex <= 1 ? 1 : 0);
                break;
            }
        });
    }
    
    [ScriptMethod(name: "P3A_Phase Transition & Skill Refresh",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9944"],
        userControl: Debugging, suppress: 500)]
    public void P3A_PhaseEnd(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3100) return;
        sa.Method.RemoveDraw(@".*_3100.*");
        
        _upm.Phase = 3150;
        _upm.P3.LoadPhaseRotation(_upm.Phase);
        sa.DebugMsg($"{_upm.Phase}");
    }

    #endregion P3A Quickmarch Trio 3100-3150

    #region P3B Blackfire Trio 3200-3250
    
    [ScriptMethod(name: "============= [P3B Blackfire Trio] =============",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P3B_Blackfire_Divider(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "P3B_Blackfire Phase Setup",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9955"],
        userControl: Debugging)]
    public void P3B_BlackfirePhase(Event ev, ScriptAccessory sa)
    {
        _upm.Phase = 3200;
        _pd.Init("P3 Blackfire");
        _pd.AddPriorities([1, 2, 3, 4, 8, 7, 6, 5]);
        sa.DebugMsg($"{_upm.Phase}");
    }
    
    [ScriptMethod(name: "P3B_Blackfire Pre-Guide",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9955"],
        userControl: true)]
    public void P3B_BlackfirePrep(Event ev, ScriptAccessory sa)
    {
        sa.DrawGuidance(Center, 0, 4000, $"P3B_{_upm.Phase}_BlackfireCenterGuide", sa.Data.DefaultSafeColor);
        if (!SpecialMode) return;
        sa.DrawCountDown(Center, 3500, iconScale: 3f, objIdBias: 0);
        sa.TextInfo("When the countdown ends, move toward Nael", delayMs: 3500);
    }
    
    [ScriptMethod(name: "P3B_Movement Direction",
        eventType: EventTypeEnum.PlayActionTimeline, 
        eventCondition: ["SourceDataId:regex:^(8161)$", "Id:regex:^(7747)$"], 
        userControl: true)]
    public async void P3B_MoveDirection(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3200) return;
        var spos = ev.SourcePosition;
        if (Vector3.Distance(spos, Center) < 18) return;
        
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _upm.P3.NaelRecordedPhase == _upm.Phase,
            ])) return;
        
        var myIndex = sa.GetMyIndex();
        var rad1 = _upm.P3.NaelDir * 45f.DegToRad();
        var rad2 = (_upm.P3.NaelDir * 2 + (myIndex <= 3 ? 15 : 1)) % 16 * 22.5f.DegToRad();
        
        sa.DrawRect(Center, 0, 1000, $"P3B_{_upm.Phase}_MoveDirection1", 
            rad1, 2, 22, sa.Data.DefaultDangerColor.WithW(2f));
        sa.DrawFan(Center, 0, 1000, $"P3B_{_upm.Phase}_MoveDirection2", 
            45f.DegToRad(), rad2, 22, 20, sa.Data.DefaultDangerColor.WithW(2f));
        
        sa.DrawRect(Center, 1000, 7500, $"P3B_{_upm.Phase}_MoveDirection1", 
            rad1, 2, 22, sa.Data.DefaultSafeColor.WithW(2f));
        sa.DrawFan(Center, 1000, 7500, $"P3B_{_upm.Phase}_MoveDirection2", 
            45f.DegToRad(), rad2, 22, 20, sa.Data.DefaultSafeColor.WithW(2f));
    }

    [ScriptMethod(name: "P3B_Move Callout",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9901", "TargetIndex:1"],
        userControl: true, suppress: 10000)]
    public void P3B_GoCall(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3200) return;
        sa.TextInfo("Go go go!", destroyMs: 2000, isWarning: true);
        sa.TTS("Go go go");
    }
    
    [ScriptMethod(name: "P3B_Blackfire Marker Collection",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(0027)$"],
        userControl: Debugging)]
    public void P3B_BlackfireMarkers(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3200) return;
        lock (_stateLock)
        {
            var iconId = ev.Id0();
            var priVal = iconId switch
            {
                0x0027 => 10, // Stack
                _ => 0
            };
            var tidx = sa.GetPlayerIdIndex((uint)ev.TargetId);
            if (!sa.IsValidPartyIndex(tidx)) return;
            _pd.AddPriority(tidx, priVal);
            _pd.AddActionCount();
            sa.DebugMsg($"{sa.GetPlayerJobByIndex(tidx)} marked: Stack", priVal + _pd.ActionCount);
            if (_pd.ActionCount != 4) return;
            sa.DebugMsg($"Blackfire markers collected", 30);
        }
    }

    [ScriptMethod(name: "P3B_Tower & Stack Guide",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(0027)$"],
        userControl: true, suppress: 2000)]
    public async void P3B_TowerStackGuide(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3200) return;
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _pd.ActionCount == 4,
            ])) return;
        
        // Towers are fixed at Nael's direction +1 +3 +5 +7 and handed out by priority
        // The stack spot is fixed at Nael's direction +4

        var naelRegion = _upm.P3.NaelDir;
        int[] towerRegion = [
            (naelRegion + 7) % 8, (naelRegion + 5) % 8, (naelRegion + 3) % 8, (naelRegion + 1) % 8
        ];
        var stackPos = new Vector3(0, 0, -8.5f).RotateAndExtend(Center, naelRegion * 45f.DegToRad());
        
        for (int i = 0; i < sa.Data.PartyList.Count; i++)
        {
            if (!Debugging && sa.GetMyIndex() != i) continue;
            var member = sa.Data.PartyList[i];
            var priRank = _pd.FindPriorityIndexOfKey(i);
            if (priRank <= 3)
            {
                var targetTowerPos = new Vector3(0, 0, 14).RotateAndExtend(Center, towerRegion[priRank] * 45f.DegToRad());
                sa.DrawGuidance(member, targetTowerPos, 0, 5200, 
                    $"P3B_{_upm.Phase}_TowerStackGuide{i}Ready", sa.Data.DefaultDangerColor);
                sa.DrawGuidance(member, targetTowerPos, 5200, 2000, 
                    $"P3B_{_upm.Phase}_TowerStackGuide{i}Soak", sa.Data.DefaultSafeColor);
                sa.DrawCircle(targetTowerPos, 0, 5200, 
                    $"P3B_{_upm.Phase}_TowerStackGuide{i}TowerDanger", 5f, sa.Data.DefaultDangerColor.WithW(2f));

                if (sa.GetMyIndex() == i)
                {
                    sa.TextInfo(SpecialMode ? "Take the tower when the countdown ends" : "Wait for one Hypernova, then take the tower", destroyMs: 3200, isWarning: true);
                    sa.TTS("Wait, then take the tower");
                }
                if (SpecialMode)
                    sa.DrawCountDown(targetTowerPos, 200, objIdBias: (uint)i);
            }
            else
            {
                sa.DrawGuidance(member, stackPos, 0, 5000, $"P3B_{_upm.Phase}_TowerStackGuide{i}", sa.Data.DefaultSafeColor);
                if (!SpecialMode || sa.GetMyIndex() != i) continue;
                sa.DrawOmen(stackPos, 453, 0, 5000, new(4, 8, 4));
            }
        }
    }
    
    [ScriptMethod(name: "P3B_Phase Transition & Skill Refresh",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9951"],
        userControl: Debugging, suppress: 500)]
    public void P3B_PhaseEnd(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3200) return;
        sa.Method.RemoveDraw(@".*_3200.*");
        
        _upm.Phase = 3250;
        _upm.P3.LoadPhaseRotation(_upm.Phase);
        sa.DebugMsg($"{_upm.Phase}");
    }


    #endregion P3B Blackfire Trio 3200-3250

    #region P3C Fellruin Trio 3300-3350

    [ScriptMethod(name: "============= [P3C Fellruin Trio] =============",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P3C_Fellruin_Divider(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "P3C_Fellruin Phase Setup",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9956"],
        userControl: Debugging)]
    public void P3C_FellruinPhase(Event ev, ScriptAccessory sa)
    {
        _upm.Phase = 3300;
        _upm.P3.FellruinQuoteCount = 0;
        sa.Method.RemoveDraw(".*");
        sa.DebugMsg($"{_upm.Phase}");
    }

    [ScriptMethod(name: "P3C_Nael Quotes",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:regex:^(650[23])$"],
        userControl: true)]
    public void P3C_NaelQuotes(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3300) return;
        var quoteId = ev.Id0();
        var color = new Vector4(0.4f, 1, 1, 1.5f);
        switch (quoteId)
        {
            case 0x6502:
                // From on high I descend, the moon and stars to bring!
                DrawNaelQuoteSkill(sa, NaelQuoteSkills.RavenDive, 0, 5000, color);
                DrawNaelQuoteSkill(sa, NaelQuoteSkills.LunarDynamo, 5000, 3000, color);
                sa.TextInfo("Spread -> In", isWarning: true);
                sa.TTS("Spread, then in");
                break;
            case 0x6503:
                // From hallowed moon I descend, a rain of stars to bring!
                DrawNaelQuoteSkill(sa, NaelQuoteSkills.LunarDynamo, 0, 5000, color);
                DrawNaelQuoteSkill(sa, NaelQuoteSkills.RavenDive, 5000, 3000, color);
                sa.TextInfo("In -> Spread", isWarning: true);
                sa.TTS("In, then spread");
                break;
        }
    }

    [ScriptMethod(name: "P3C_Neurolink Assignment",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:regex:^(650[23])$"],
        userControl: Debugging)]
    public void P3C_AssignNeurolinks(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3300) return;
        var peopleIdx = _upm.GetNearestNeurolinkIndex(ev.SourcePosition);
        _upm.P3.FellruinNeurolinks[2] = peopleIdx;
        _upm.P3.FellruinNeurolinks[0] = (peopleIdx + 1) % 3;
        _upm.P3.FellruinNeurolinks[1] = (peopleIdx + 2) % 3;
        sa.DebugMsg($"Tanks (nearest) {_upm.P3.FellruinNeurolinks[0]}/{_upm.P3.FellruinNeurolinks[1]}, party {_upm.P3.FellruinNeurolinks[2]}");
    }
    
    [ScriptMethod(name: "P3C_Neurolink Guide",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(9918|9916)$", "TargetIndex:1"], 
        userControl: true)]
    public void P3C_NeurolinkGuide(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3300) return;
        _upm.P3.FellruinQuoteCount++;
        
        if (ev.ActionId == 9916 && sa.Data.PartyList.Count >= 2)
        {
            // Both tanks take the two Neurolinks the party isn't using, split by each tank's distance difference to the two spots (refreshed every frame)
            var pos0 = _upm.NeurolinkPositions[_upm.P3.FellruinNeurolinks[0]];
            var pos1 = _upm.NeurolinkPositions[_upm.P3.FellruinNeurolinks[1]];
            for (int i = 0; i < 2; i++)
            {
                if (!Debugging && sa.GetMyIndex() != i) continue;
                var draw = sa.DrawGuidance(sa.Data.PartyList[i], pos0, 0, 10000, $"P3C_{_upm.Phase}_NeurolinkGuide{i}",
                    sa.Data.DefaultSafeColor, draw: false);
                var i1 = i;
                sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, draw, dp =>
                {
                    if (sa.GetById(sa.Data.PartyList[0]) is not { } mt || sa.GetById(sa.Data.PartyList[1]) is not { } st) return;
                    var mtDelta = Vector3.Distance(mt.Position, pos0) - Vector3.Distance(mt.Position, pos1);
                    var stDelta = Vector3.Distance(st.Position, pos0) - Vector3.Distance(st.Position, pos1);
                    // The two tanks use opposite sides of the same inequality, so even a tie never sends both to the same spot
                    dp.TargetPosition = (i1 == 0 ? mtDelta <= stDelta : mtDelta > stDelta) ? pos0 : pos1;
                });
            }
        }
        if (_upm.P3.FellruinQuoteCount == 2)
        {
            var peopleIdx = _upm.P3.FellruinNeurolinks[2];
            for (int i = 0; i < sa.Data.PartyList.Count; i++)
            {
                if (i <= 1) continue;
                if (!Debugging && sa.GetMyIndex() != i) continue;
                sa.DrawGuidance(sa.Data.PartyList[i], _upm.NeurolinkPositions[peopleIdx], 0, 10000, $"P3C_{_upm.Phase}_NeurolinkGuide{i}",
                    sa.Data.DefaultSafeColor);
            }
        }
    }
    
    [ScriptMethod(name: "P3C_Post-Profusion Meteor Stream",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9905"],
        userControl: true, suppress: 500)]
    public void P3C_PostProfusionMeteorStream(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase is not (3300 or 3350)) return;
        var color = new Vector4(0.4f, 1, 1, 1.5f);
        DrawNaelQuoteSkill(sa, NaelQuoteSkills.MeteorStream, 0, 4000, color);
    }
        
    [ScriptMethod(name: "P3C_Phase Transition & Skill Refresh",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9905"],
        userControl: Debugging, suppress: 500)]
    public void P3C_PhaseEnd(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3300) return;
        sa.Method.RemoveDraw(@".*_3300.*");
        
        _upm.Phase = 3350;
        _upm.P3.LoadPhaseRotation(_upm.Phase);
        sa.DebugMsg($"{_upm.Phase}");
    }

    #endregion P3C Fellruin Trio 3300-3350

    #region P3D Heavensfall Trio 3400-3450

    [ScriptMethod(name: "============= [P3D Heavensfall Trio] =============",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P3D_Heavensfall_Divider(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "P3D_Heavensfall Phase Setup",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9957"],
        userControl: Debugging)]
    public void P3D_HeavensfallPhase(Event ev, ScriptAccessory sa)
    {
        _upm.Phase = 3400;
        sa.DebugMsg($"{_upm.Phase}");
    }

    [ScriptMethod(name: "P3D_Heavensfall Pre-Guide",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9957"],
        userControl: true)]
    public void P3D_HeavensfallPrep(Event ev, ScriptAccessory sa)
    {
        sa.DrawGuidance(Center, 0, 4000, $"P3D_{_upm.Phase}_HeavensfallCenterGuide", sa.Data.DefaultSafeColor);
        sa.TextInfo("Bait the dives in the middle, then move out", delayMs: 3500);
    }
    
    [ScriptMethod(name: "P3D_Heavensfall Twister Guide",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9906"],
        userControl: true)]
    public async void P3D_HeavensfallTwisterGuide(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3400) return;
        
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _upm.P3.BahamutRecordedPhase == _upm.Phase,
                () => _upm.P3.TwintaniaRecordedPhase == _upm.Phase,
                () => _upm.P3.NaelRecordedPhase == _upm.Phase,
            ])) return;
        
        var err = _upm.P3.SolveHeavensfallTwisters();
        if (err != 0) return;

        for (int i = 0; i < sa.Data.PartyList.Count; i++)
        {
            if (!Debugging && sa.GetMyIndex() != i) continue;
            var region = _upm.P3.HeavensfallTwisterDirs[i];
            if (region < 0) continue;

            var tPos = new Vector3(0, 0, 20).RotateAndExtend(Center, 45f.DegToRad() * region);
            sa.DrawGuidance(sa.Data.PartyList[i], tPos, 0, 3700, 
                $"P3D_{_upm.Phase}_HeavensfallTwisterGuide{i}", sa.Data.DefaultSafeColor);
        }
    }
    
    [ScriptMethod(name: "P3D_Heavensfall Tower Collection",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9951"], 
        userControl: Debugging)]
    public void P3D_HeavensfallTowerCollect(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3400) return;
        lock (_stateLock)
        {
            var spos = ev.SourcePosition;
            var towerRegion = spos.GetRadian(Center).RadianToRegion(16, isDiagDiv: true);
            _upm.P3.HeavensfallTowerDirs.Add(towerRegion);
        }
    }
    
    [ScriptMethod(name: "P3D_Heavensfall Tower Guide",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9951"], 
        userControl: true, suppress: 1000)]
    public async void P3D_HeavensfallTowerGuide(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3400) return;
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _upm.P3.HeavensfallTowerDirs.Count == 8
            ])) return;
        
        var err = _upm.P3.SolveHeavensfallTowers();
        if (err != 0) return;
        
        for (int i = 0; i < sa.Data.PartyList.Count; i++)
        {
            if (!Debugging && sa.GetMyIndex() != i) continue;
            var region = _upm.P3.HeavensfallTowerAssignments[i];
            if (region < 0) continue;

            var tPos = new Vector3(0, 0, 10).RotateAndExtend(Center, 22.5f.DegToRad() * region);
            sa.DrawGuidance(sa.Data.PartyList[i], tPos, 0, 6500, 
                $"P3D_{_upm.Phase}_HeavensfallTowerGuide{i}_KnockbackSpot", sa.Data.DefaultSafeColor);
            sa.DrawLine(Center, 0, 0, 6500, 
                $"P3D_{_upm.Phase}_HeavensfallTowerGuide{i}_Line", tPos.GetRadian(Center), 20f, 25f,
                sa.Data.DefaultSafeColor);
            
            if (!SpecialMode) continue;
            sa.DrawCountDown(sa.Data.PartyList[i], 50);
        }
    }
    
    [ScriptMethod(name: "P3D_Center Tower Knockback",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9911"],
        userControl: true)]
    public void P3D_CenterTowerKnockback(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3400) return;
        var color = new Vector4(0.4f, 1, 1, 1.5f);
        
        var dp = sa.DrawRect(Center, 0, 5000, 
            $"P3D_{_upm.Phase}_HeavensfallDeathZone", 0, 9, 7, color, draw: false);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
        
        sa.DrawKnockBack(Center, 0, 5000, 
            $"P3D_{_upm.Phase}_HeavensfallKnockback", 
            1.5f, 12f, sa.Data.DefaultDangerColor.WithW(2f));
    }
    
    [ScriptMethod(name: "P3D_Phase Transition & Skill Refresh",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0075"],
        userControl: Debugging)]
    public void P3D_PhaseEnd(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3400) return;
        _upm.Phase = 3450;
        _upm.P3.LoadPhaseRotation(_upm.Phase);
        sa.DebugMsg($"{_upm.Phase}");
    }

    #endregion P3D Heavensfall Trio 3400-3450

    #region P3E Tenstrike Trio 3500-3550

    [ScriptMethod(name: "============= [P3E Tenstrike Trio] =============",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P3E_Tenstrike_Divider(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "P3E_Tenstrike Phase Setup",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9958"],
        userControl: Debugging)]
    public void P3E_TenstrikePhase(Event ev, ScriptAccessory sa)
    {
        _upm.Phase = 3500;
        _pd.Init("P3 Tenstrike");
        _pd.AddPriorities([1, 2, 3, 4, 5, 6, 7, 8]);
        sa.DebugMsg($"{_upm.Phase}");
    }
    
    [ScriptMethod(name: "P3E_Tenstrike Pre-Guide",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9958"],
        userControl: true)]
    public void P3E_TenstrikePrep(Event ev, ScriptAccessory sa)
    {
        var basePos = new Vector3(0, 0, 15);
        List<float> rotDeg = [-120, 120, -155, 155, -85, 85, -20, 20];
        for (int i = 0; i < sa.Data.PartyList.Count; i++)
        {
            var color = i switch
            {
                0 or 1 => new Vector4(0.1f, 0.1f, 1, 1),
                2 or 3 => new Vector4(0.1f, 1f, 0.1f, 1),
                _ => new Vector4(1, 0.1f, 0.1f, 1),
            };
            sa.DrawLine(Center, 0, 0, 6500, $"P3E_{_upm.Phase}_TenstrikePrep_Line{i}",
                rotDeg[i].DegToRad(), 20f, 25f, color);
            
            if (!Debugging && sa.GetMyIndex() != i) continue;
            var member = sa.Data.PartyList[i];
            sa.DrawGuidance(member, basePos.RotateAndExtend(Center, rotDeg[i].DegToRad()), 
                0, 6500, $"P3E_{_upm.Phase}_TenstrikePrep{i}", sa.Data.DefaultSafeColor);
        }
    }
    
    [ScriptMethod(name: "P3E_Tenstrike Meteor Stream AoE",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9958"],
        userControl: true)]
    public void P3E_TenstrikeMeteorStream(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3500) return;
        var color = new Vector4(0.4f, 1, 1, 1.5f);
        DrawNaelQuoteSkill(sa, NaelQuoteSkills.MeteorStream, 4000, 15000, color);
    }
    
    [ScriptMethod(name: "P3E_Tenstrike Neurolink Lines",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9958"],
        userControl: true)]
    public void P3E_NeurolinkLines(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3500) return;
        
        var color = new Vector4(1f, 1f, 0f, 1f);
        for (var i = 0; i < _upm.NeurolinkPositions.Count; i++)
        {
            var rad = _upm.NeurolinkPositions[i].GetRadian(Center);
            sa.DrawLine(Center, 0, 4000, 10000, $"P3E_{_upm.Phase}_NeurolinkLines", rad, 20f, 25f, color);
        }
    }

    [ScriptMethod(name: "P3E_Hatch Partner Link",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0076"],
        userControl: true, suppress: 500)]
    public async void P3E_HatchPartnerLink(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3500) return;
        
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _pd.SelectSpecificPriorityIndex(2, true).Value >= 100,
            ])) return;
        
        if (!Debugging && _pd[sa.GetMyIndex()] < 100) return;
        var color = new Vector4(1, 1, 0, 1);
        
        for (int i = 0; i < 3; i++)
        {
            var idx1 = _pd.SelectSpecificPriorityIndex(i, true).Key;
            var idx2 = _pd.SelectSpecificPriorityIndex((i + 1) % 3, true).Key;
            sa.DrawConnection(sa.Data.PartyList[idx1], sa.Data.PartyList[idx2], 
                0, 20000, $"P3E_{_upm.Phase}_HatchPartnerLink{i}", color);
        }
    }
    
    [ScriptMethod(name: "P3E_Hatch Assignment",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0076"],
        userControl: Debugging, suppress: 500)]
    public async void P3E_HatchAssignment(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3500) return;
        if (!_upm.P3.TenstrikeHatchPlayers.Contains(-1)) return;
        
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _pd.SelectSpecificPriorityIndex(2, true).Value >= 100,
            ])) return;

        _upm.P3.TenstrikeMarkedPlayers = _pd.SelectLargePriorityIndices(3).Select(x => x.Key).ToList();
        // Lock the assignment to everyone's position at the moment all markers are out; no per-frame refresh, so the interceptor doesn't flip between players mid-mechanic
        var playerPositions = sa.Data.PartyList.Select(id => sa.GetById(id)?.Position).ToArray();
        _upm.P3.SolveTenstrikeHatchPlayers(_upm.NeurolinkPositions, playerPositions);
        sa.DebugMsg($"Hatch takers / interceptors: {string.Join(", ",
            _upm.P3.TenstrikeHatchPlayers.Select(x => sa.GetPlayerJobByIndex(x)))}");
    }

    [ScriptMethod(name: "P3E_Hatch Guide",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9902"],
        userControl: true)]
    public async void P3E_HatchGuide(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3500) return;
        if (_upm.P3.TenstrikeHatchDrawn) return;

        if (!await WaitUntilConditions(
            conditions:
            [
                () => !_upm.P3.TenstrikeHatchPlayers.Contains(-1),
            ])) return;
        var ls = _upm.P3.TenstrikeHatchPlayers.ToArray();
        var myIndex = sa.GetMyIndex();
        if (!sa.IsValidPartyIndex(myIndex)) return;
        
        for (int i = 0; i < 3; i++)
        {
            var pidx1 = ls[i];
            var pidx2 = ls[i + 3];
            if (!Debugging && myIndex != pidx1 && myIndex != pidx2) continue;
            sa.DrawGuidance(sa.Data.PartyList[pidx1], _upm.NeurolinkPositions[i], 
                0, 5000, $"P3E_{_upm.Phase}_HatchGuide{pidx1}", sa.Data.DefaultSafeColor);
            sa.DrawGuidance(sa.Data.PartyList[pidx2], _upm.NeurolinkPositions[i], 
                0, 5000, $"P3E_{_upm.Phase}_HatchGuide{pidx2}Ready", sa.Data.DefaultDangerColor);
            sa.DrawGuidance(sa.Data.PartyList[pidx2], _upm.NeurolinkPositions[i], 
                5000, 10000, $"P3E_{_upm.Phase}_HatchGuide{pidx2}", sa.Data.DefaultSafeColor);
        }
        _upm.P3.TenstrikeHatchDrawn = true;
    }

    [ScriptMethod(name: "P3E_Earthshaker Marker Collection",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0028"],
        userControl: Debugging)]
    public void P3E_EarthshakerMarkers(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3500) return;
        lock (_stateLock)
        {
            if (_pd.ActionCount >= 4)
                return;
            var tidx = sa.GetPlayerIdIndex((uint)ev.TargetId);
            if (!sa.IsValidPartyIndex(tidx)) return;
            _pd.AddPriority(tidx, 1000);
            _pd.AddActionCount();
            sa.Method.RemoveDraw($"GEN_NeurolinkHatchBlastAoe.*");
        }
    }

    [ScriptMethod(name: "P3E_Earthshaker Partner Link",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0028"],
        userControl: true, suppress: 500)]
    public async void P3E_EarthshakerPartnerLink(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3500) return;
        
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _pd.ActionCount == 4,
                () => _upm.P3.EarthshakerLinkDrawVersion <= _upm.P3.EarthshakerHitCount
            ])) return;
        
        _upm.P3.EarthshakerLinkDrawVersion++;
        var color = new Vector4(1, 1, 0, 1);

        List<int> pidx = _upm.P3.EarthshakerLinkDrawVersion == 1
            ? _pd.SelectLargePriorityIndices(4).Select(x => x.Key).ToList()
            : _pd.SelectSmallPriorityIndices(4).Select(x => x.Key).ToList();
        
        if (!Debugging && !pidx.Contains(sa.GetMyIndex())) return;
        for (int i = 0; i < 4; i++)
        {
            var draw = sa.DrawConnection(sa.Data.PartyList[pidx[i]], sa.Data.PartyList[pidx[(i + 1) % 4]], 0, 5000,
                $"P3E_{_upm.Phase}_EarthshakerPartnerLink{i}", color, draw: false);
            var i1 = i;
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, draw, dp =>
            {
                List<int> tempPidx = pidx
                    .Select(x => new { Index = x, Member = sa.GetById(sa.Data.PartyList[x]) })
                    .Where(x => x.Member != null)
                    .OrderBy(x => x.Member.Position.GetRadian(Center))
                    .Select(x => x.Index)
                    .ToList();
                if (tempPidx.Count != pidx.Count) return;
                dp.Owner = sa.Data.PartyList[tempPidx[i1]];
                dp.TargetObject = sa.Data.PartyList[tempPidx[(i1 + 1) % tempPidx.Count]];
            });
        }
    }

    [ScriptMethod(name: "P3E_Earthshaker Guide Lines",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0028"],
        userControl: true, suppress: 500)]
    public async void P3E_EarthshakerLines(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3500) return;
        
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _pd.ActionCount == 4,
                () => _upm.P3.EarthshakerLineDrawVersion <= _upm.P3.EarthshakerHitCount
            ])) return;
        
        _upm.P3.EarthshakerLineDrawVersion++;
        float[] rotDegs = [-40, 40, -100, 100];
        var isFirstRound = _pd.FindPriorityIndexOfKey(sa.GetMyIndex(), true) <= 3;
        if (!Debugging && (isFirstRound ^ (_upm.P3.EarthshakerLineDrawVersion == 1))) return;

        var color = Vector4.One;
        foreach (var deg in rotDegs)
            sa.DrawLine(Center, 0, 0, 5000, $"P3E_{_upm.Phase}_EarthshakerLines", deg.DegToRad(), 20f, 25, color);
    }
    
    [ScriptMethod(name: "P3E_Earthshaker Guide",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0028"],
        userControl: true, suppress: 500)]
    public async void P3E_EarthshakerGuide(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3500) return;
        
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _pd.ActionCount == 4,
                () => _upm.P3.EarthshakerGuideDrawVersion <= _upm.P3.EarthshakerHitCount
            ])) return;
        
        _upm.P3.EarthshakerGuideDrawVersion++;
        var safePos = new Vector3(0, 0, -8.5f);

        List<int> pidx = _upm.P3.EarthshakerGuideDrawVersion == 1
            ? _pd.SelectLargePriorityIndices(4).Select(x => x.Key).ToList()
            : _pd.SelectSmallPriorityIndices(4).Select(x => x.Key).ToList();
        var myIndex = sa.GetMyIndex();

        // float[] rotDegs = _upm.P3.EarthshakerGuideDrawVersion == 1
        //     ? [-100, -20, 20, 100]
        //     : [-140, -80, 80, 140];
        
        for (int i = 0; i < pidx.Count; i++)
        {
            var memberIndex = pidx[i];
            if (!Debugging && myIndex != memberIndex) continue;
            var member = sa.Data.PartyList[memberIndex];
            
            // Didn't work that well; keeping the idea, feature commented out
            // var drawGuidance = sa.DrawGuidance(member, Center,
            //     0, 5000, $"P3E_{_upm.Phase}_EarthshakerGuide{memberIndex}", sa.Data.DefaultSafeColor, draw: false);
            // sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, drawGuidance, dp =>
            // {
            //     List<int> tempPidx = pidx
            //         .Select(x => new { Index = x, Member = sa.GetById(sa.Data.PartyList[x]) })
            //         .Where(x => x.Member != null)
            //         .OrderBy(x => x.Member!.Position.GetRadian(Center))
            //         .Select(x => x.Index)
            //         .ToList();
            //     if (tempPidx.Count != rotDegs.Length) return;
            //     var targetIdx = tempPidx.IndexOf(memberIndex);
            //     if (targetIdx < 0) return;
            //     dp.TargetPosition = new Vector3(0, 0, 15).RotateAndExtend(Center, rotDegs[targetIdx].DegToRad());
            // });

            if (SpecialMode)
                sa.DrawCountDown(member, 50);
            if (myIndex != memberIndex) continue;
            sa.TextInfo("Bait Earthshaker");
            sa.TTS("Bait Earthshaker");
        }

        for (int i = 0; i < sa.Data.PartyList.Count; i++)
        {
            if (pidx.Contains(i)) continue;
            if (!Debugging && myIndex != i) continue;
            sa.DrawGuidance(sa.Data.PartyList[i], safePos,
                0, 5000, $"P3E_{_upm.Phase}_EarthshakerGuide{i}", sa.Data.DefaultSafeColor);
            if (myIndex != i) continue;
            sa.TextInfo("Go to the safe spot, pre-position for the 4 corners");
            sa.TTS("Safe spot, pre-position for the corners");
        }
    }

    [ScriptMethod(name: "P3E_Phase Transition & Skill Refresh",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9946"],
        userControl: Debugging, suppress: 500)]
    public void P3E_PhaseEnd(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3500) return;
        _upm.P3.EarthshakerHitCount++;
        if (_upm.P3.EarthshakerHitCount < 2) return;
        sa.Method.RemoveDraw(@".*_3500.*");
        _upm.Phase = 3550;
        _upm.P3.LoadPhaseRotation(_upm.Phase);
        sa.DebugMsg($"{_upm.Phase}");
    }

    #endregion P3E Tenstrike Trio 3500-3550

    #region P3F Grand Octet 3600

    [ScriptMethod(name: "============= [P3F Grand Octet] =============",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P3F_GrandOctet_Divider(Event ev, ScriptAccessory sa)
    {
    }

    
    [ScriptMethod(name: "P3F_Grand Octet Phase Setup",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9959"],
        userControl: Debugging)]
    public void P3F_OctetPhase(Event ev, ScriptAccessory sa)
    {
        _upm.Phase = 3600;
        _pd.Init($"P3 Grand Octet");
        sa.DebugMsg($"{_upm.Phase}");
        sa.Method.RemoveDraw(@".*_3550.*");
    }

    [ScriptMethod(name: "P3F_Grand Octet Pre-Guide",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9959"],
        userControl: true)]
    public void P3F_OctetPrep(Event ev, ScriptAccessory sa)
    {
        sa.DrawGuidance(Center, 0, 4000, $"P3F_{_upm.Phase}_OctetCenterGuide", sa.Data.DefaultSafeColor);
    }
    
    [ScriptMethod(name: "P3F_Grand Octet Running Guide",
        eventType: EventTypeEnum.SetObjPos, eventCondition: ["SourceDataId:8168"],
        userControl: true, suppress: 500)]
    public async void P3F_OctetRunGuide(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3600) return;
        
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _upm.P3.NaelRecordedPhase == 3600,
                () => _upm.P3.BahamutRecordedPhase == 3600,
                () => _upm.P3.TwintaniaRecordedPhase == 3600,
            ])) return;
        
        var err = _upm.P3.SolveOctetStart();
        if (err != 0) return;

        var rad1 = _upm.P3.OctetStartAndDirection[0] * 45f.DegToRad();
        var rad2 = rad1 + _upm.P3.OctetStartAndDirection[1] * 45f.DegToRad();

        var tPos1 = new Vector3(0, 0, 22).RotateAndExtend(Center, rad1);
        var tPos2 = new Vector3(0, 0, 22).RotateAndExtend(Center, rad2);
        
        sa.DrawGuidance(tPos1, 0, 5000, $"P3F_{_upm.Phase}_OctetRunGuide_StartReady", sa.Data.DefaultDangerColor);
        sa.DrawGuidance(tPos1, tPos2, 5000, 4000, $"P3F_{_upm.Phase}_OctetRunGuide_Start", sa.Data.DefaultDangerColor);
        
        sa.DrawGuidance(tPos1, 5000, 5500, $"P3F_{_upm.Phase}_OctetRunGuide_Start", sa.Data.DefaultSafeColor);
        sa.DrawGuidance(tPos1, tPos2, 5000, 5500, $"P3F_{_upm.Phase}_OctetRunGuide_Start", sa.Data.DefaultDangerColor);
        
        sa.DrawGuidance(tPos2, 10500, 2000, $"P3F_{_upm.Phase}_OctetRunGuide_Go", sa.Data.DefaultSafeColor);
    }

    [ScriptMethod(name: "P3F_Grand Octet Run Callout",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0077"],
        userControl: true)]
    public void P3F_OctetRunCall(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3600) return;
        var dirStr = _upm.P3.OctetStartAndDirection[1] == 1 ? "LEFT" : "RIGHT";
        sa.TextInfo($"Wait for Nael's dive, then face out and run {dirStr}", destroyMs: 4000);
        sa.TTS($"Get ready to run {dirStr}");
        if (!SpecialMode) return;
        sa.DrawCountDown(sa.Data.Me, 500);
    }
    
    [ScriptMethod(name: "P3F_Grand Octet Marker Tracking",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(0077|0029|0014)$"], 
        userControl: Debugging)]
    public void P3F_OctetMarkers(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3600) return;
        lock (_stateLock)
        {
            if (_pd.ActionCount >= 7) return;
            var tidx = sa.GetPlayerIdIndex((uint)ev.TargetId);
            if (!sa.IsValidPartyIndex(tidx)) return;
            _pd.AddPriority(tidx, 10);
            _pd.AddActionCount();
        }
    }
    
    [ScriptMethod(name: "P3F_Return to Center & Twintania Bait Guide",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(0029)$"], 
        userControl: true)]
    public async void P3F_ReturnAndTwinBaitGuide(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3600) return;
        
        if (!await WaitUntilConditions(
            timeoutMs: 500,
            conditions:
            [
                () => _pd.ActionCount == 7,
            ])) return;
        
        var tidx = _pd.SelectSpecificPriorityIndex(0).Key;
        var myIndex = sa.GetMyIndex();

        if (myIndex != tidx)
            sa.DrawGuidance(Center, 0, 5000, $"P3F_{_upm.Phase}_ReturnToCenter", sa.Data.DefaultSafeColor);

        if (Debugging || myIndex == tidx)
        {
            var member = sa.Data.PartyList[tidx];
            if (sa.GetById(member) is not { } obj) return;
            var tPos1 = new Vector3(0, 0, 22).RotateAndExtend(Center, (_upm.P3.TwintaniaDir * 45f + 13f).DegToRad() );
            var tPos2 = new Vector3(0, 0, 22).RotateAndExtend(Center, (_upm.P3.TwintaniaDir * 45f - 13f).DegToRad() );
            var draw = sa.DrawGuidance(member, 0, 0, 9000, $"P3F_{_upm.Phase}_TwinBaitGuide", sa.Data.DefaultSafeColor,
                draw: false);
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, draw, dp =>
            {
                if (!obj.IsValid()) return;
                var distanceTo1 = Vector3.Distance(obj.Position, tPos1);
                var distanceTo2 = Vector3.Distance(obj.Position, tPos2);
                dp.TargetPosition = distanceTo1 < distanceTo2 ? tPos1 : tPos2;
            });
        }
    }

    [ScriptMethod(name: "P3F_Stack Marker Tracking",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0027"],
        userControl: true)]
    public void P3F_StackMarkers(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3600) return;
        lock (_stateLock)
        {
            if (_pd.ActionCount >= 11) return;
            var tidx = sa.GetPlayerIdIndex((uint)ev.TargetId);
            if (!sa.IsValidPartyIndex(tidx)) return;
            _pd.AddPriority(tidx, 100);
            _pd.AddActionCount();
        }
    }
    
    [ScriptMethod(name: "P3F_Tower Partner Link",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0027"],
        userControl: true, suppress: 500)]
    public async void P3F_TowerPartnerLink(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3600) return;
        
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _pd.ActionCount == 11,
            ])) return;
        
        var color = new Vector4(1, 1, 0, 1);

        List<int> pidx = _pd.SelectSmallPriorityIndices(4).Select(x => x.Key).ToList();
        
        if (!Debugging && !pidx.Contains(sa.GetMyIndex())) return;
        for (int i = 0; i < 4; i++)
        {
            var draw = sa.DrawConnection(sa.Data.PartyList[pidx[i]], sa.Data.PartyList[pidx[(i + 1) % 4]], 0, 7000,
                $"P3F_{_upm.Phase}_TowerPartnerLink{i}", color, draw: false);
            var i1 = i;
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, draw, dp =>
            {
                List<int> tempPidx = pidx
                    .Select(x => new { Index = x, Member = sa.GetById(sa.Data.PartyList[x]) })
                    .Where(x => x.Member != null)
                    .OrderBy(x => x.Member.Position.GetRadian(Center))
                    .Select(x => x.Index)
                    .ToList();
                if (tempPidx.Count != pidx.Count) return;
                dp.Owner = sa.Data.PartyList[tempPidx[i1]];
                dp.TargetObject = sa.Data.PartyList[tempPidx[(i1 + 1) % tempPidx.Count]];
            });
        }
    }

    [ScriptMethod(name: "P3F_Tower Soak / Avoid Callout",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0027"],
        userControl: true, suppress: 500)]
    public async void P3F_TowerCall(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3600) return;

        if (!await WaitUntilConditions(
            conditions:
            [
                () => _pd.ActionCount == 11,
            ])) return;

        var myIndex = sa.GetMyIndex();
        var priVal = _pd[myIndex];
        if (priVal >= 100 && myIndex <= 1)
        {
            sa.TextInfo("Take a tower with your stack");
            sa.TTS("Take a tower with your stack");
        }
        else if (priVal >= 100)
        {
            sa.TextInfo("Avoid the towers");
            sa.TTS("Avoid the towers");
        }
        else
        {
            sa.TextInfo("Take a tower");
            sa.TTS("Take a tower");
        }
    }
    
    // [ScriptMethod(name: "P3F_Tower Soak Countdown",
    //     eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9951"],
    //     userControl: true)]
    // public void P3F_TowerCountdown(Event ev, ScriptAccessory sa)
    // {
    //     if (_upm.Phase != 3600) return;
    //     if (!SpecialMode) return;
    //     lock (_stateLock)
    //     {
    //         _upm.P3.TowerIconOffset++;
    //         sa.DrawCountDown(ev.SourcePosition, 3000, iconScale: 1f, objIdBias: _upm.P3.TowerIconOffset);
    //     }
    //     
    // }

    [ScriptMethod(name: "P3F_Cleanup & Phase Transition",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(9906)$", "TargetIndex:1"],
        userControl: Debugging)]
    public void P3F_CleanupAndPhase(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 3600) return;
        _upm.Phase = 4000;
        sa.Method.RemoveDraw($".*");
        _pd.Init($"P4 Hatch");
        sa.DebugMsg($"{_upm.Phase}");
    }

    #endregion P3F Grand Octet 3600

    #endregion P3

    #region P4

    [ScriptMethod(name: "———————— [P4] ————————",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P4_Divider(Event ev, ScriptAccessory sa)
    {
    }

    // [ScriptMethod(name: "P4_Show Tank Spot",
    //     eventType: EventTypeEnum.Targetable, eventCondition: ["DataId:8161", "Targetable:True"],
    //     userControl: Debugging)]
    // public void P4_ShowTankSpot(Event ev, ScriptAccessory sa)
    // {
    //     if (_upm.Phase != 4000) return;
    //     for (int i = 0; i < 2; i++)
    //     {
    //         if (!Debugging && sa.GetMyIndex() != i) continue;
    //         sa.DrawGuidance(sa.Data.PartyList[i], _upm.TankSpot, 0, 5000,
    //             $"P4_{_upm.Phase}_TankSpot", sa.Data.DefaultSafeColor);
    //     }

    //     var color = new Vector4(1f, 0.5f, 0.5f, 0.75f);
    //     sa.DrawCircle(_upm.TankSpot, 0, 140000, $"P4_{_upm.Phase}_TankSpot", 1f, color);
    // }
    
    [ScriptMethod(name: "P4_Show Both Boss Centers",
        eventType: EventTypeEnum.Targetable, eventCondition: ["DataId:8161", "Targetable:True"],
        userControl: Debugging)]
    public void P4_BossCenters(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 4000) return;
        sa.DrawCircle(_upm.P1.TwintaniaObjId, 0, Int32.MaxValue, 
            $"P4_{_upm.Phase}_TwintaniaCenter_Inner", 0.4f, new Vector4(1, 0, 0, 2), useImgui: true);
        sa.DrawDonut(_upm.P1.TwintaniaObjId, 0, Int32.MaxValue, 
            $"P4_{_upm.Phase}_TwintaniaCenter_Outer", 0.5f, 0.4f, new Vector4(0, 1, 1, 1), useImgui: true);
        sa.DrawCircle(_upm.P2.NaelObjId, 0, Int32.MaxValue, 
            $"P4_{_upm.Phase}_NaelCenter_Inner", 0.4f, new Vector4(1, 0, 0, 2), useImgui: true);
        sa.DrawDonut(_upm.P2.NaelObjId, 0, Int32.MaxValue, 
            $"P4_{_upm.Phase}_NaelCenter_Outer", 0.5f, 0.4f, new Vector4(0, 1, 1, 1), useImgui: true);
    }

    [ScriptMethod(name: "P4_First Plummet",
        eventType: EventTypeEnum.Targetable, eventCondition: ["DataId:8161", "Targetable:True"],
        userControl: Debugging)]
    public void P4_FirstPlummet(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 4000) return;
        DrawPlummet(sa);
    }
    
    [ScriptMethod(name: "P4_Second Plummet",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9897", "TargetIndex:1"],
        userControl: Debugging)]
    public void P4_SecondPlummet(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 4000) return;
        DrawPlummet(sa);
    }
    
    [ScriptMethod(name: "P4_Liquid Hell Bait",
        eventType: EventTypeEnum.ActionEffect, 
        eventCondition: ["ActionId:regex:^(9896)$", "TargetIndex:1"],
        userControl: true)]
    public void P4_LiquidHellBait(Event ev, ScriptAccessory sa)
    {
        // Liquid Hell only follows Plummet
        if (_upm.Phase != 4000) return;
        DrawLiquidHellBait(sa, phaseKeep: true);
    }
    
    [ScriptMethod(name: "P4_Nael Quotes",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:regex:^(650[4567])$"],
        userControl: Debugging)]
    public void P4_NaelQuotes(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 4000) return;
        var quoteId = ev.Id0();
        var color = new Vector4(0.4f, 1, 1, 1.5f);
        switch (quoteId)
        {
            case 0x6504:
                // Unbending iron, take fire and descend!
                DrawNaelQuoteSkill(sa, NaelQuoteSkills.IronChariot, 0, 5000, color);
                DrawNaelQuoteSkill(sa, NaelQuoteSkills.ThermionicBeam, 5000, 3000, sa.Data.DefaultSafeColor);
                DrawNaelQuoteSkill(sa, NaelQuoteSkills.RavenDive, 8000, 3000, color);
                // DrawSpreadDirections(sa, 8000, 8000);
                sa.TextInfo("Out -> Stack -> Spread", destroyMs: 5000, isWarning: true);
                sa.TTS("Out, stack, then spread");
                break;
            case 0x6505:
                // Unbending iron, descend with fiery edge!
                DrawNaelQuoteSkill(sa, NaelQuoteSkills.IronChariot, 0, 5000, color);
                DrawNaelQuoteSkill(sa, NaelQuoteSkills.RavenDive, 5000, 3000, color);
                DrawNaelQuoteSkill(sa, NaelQuoteSkills.ThermionicBeam, 8000, 3000, sa.Data.DefaultSafeColor);
                // DrawSpreadDirections(sa, 11000, 5000);
                sa.TextInfo("Out -> Spread -> Stack", destroyMs: 5000, isWarning: true);
                sa.TTS("Out, spread, then stack");
                break;
            case 0x6506:
                // From hallowed moon I descend, upon burning earth to tread!
                DrawNaelQuoteSkill(sa, NaelQuoteSkills.LunarDynamo, 0, 5000, color);
                DrawNaelQuoteSkill(sa, NaelQuoteSkills.RavenDive, 5000, 3000, color);
                DrawNaelQuoteSkill(sa, NaelQuoteSkills.ThermionicBeam, 8000, 3000, sa.Data.DefaultSafeColor);
                // DrawSpreadDirections(sa, 11000, 5000);
                sa.TextInfo("In -> Spread -> Stack", destroyMs: 5000, isWarning: true);
                sa.TTS("In, spread, then stack");
                break;
            case 0x6507:
                // From hallowed moon I bare iron, in my descent to wield!
                DrawNaelQuoteSkill(sa, NaelQuoteSkills.LunarDynamo, 0, 5000, color);
                DrawNaelQuoteSkill(sa, NaelQuoteSkills.IronChariot, 5000, 3000, color);
                DrawNaelQuoteSkill(sa, NaelQuoteSkills.RavenDive, 8000, 3000, color);
                // DrawSpreadDirections(sa, 8000, 8000);
                sa.TextInfo("In -> Out -> Spread", destroyMs: 5000, isWarning: true);
                sa.TTS("In, out, then spread");
                break;
        }
    }

    private void DrawSpreadDirections(ScriptAccessory sa, int delayMs, int destroyMs)
    {
        List<float> rotDegs = [20, -20, 105, -105, 60, -60, 150, -150];
        var baseRad = _upm.TankSpot.GetRadian(Center);
        var myIndex = sa.GetMyIndex();

        for (int i = 0; i < 8; i++)
        {
            var width = i == myIndex ? 20f : 10f;
            var color = i == myIndex ? sa.Data.DefaultSafeColor : Vector4.One;
            sa.DrawLine(Center, 0, delayMs, destroyMs, $"P4_{_upm.Phase}_SpreadDirections",
                baseRad + rotDegs[i].DegToRad(), width, 25, color);
        }
    }

    [ScriptMethod(name: "P4_Hatch Assignment",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9902"],
        userControl: Debugging)]
    public async void P4_HatchAssignment(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 4000) return;

        if (!await WaitUntilConditions(
            conditions:
            [
                () => _pd.SelectSpecificPriorityIndex(2, true).Value >= 100,
            ])) return;

        var keys = _pd.SelectLargePriorityIndices(3).Select(x => x.Key).ToArray();
        // Lock the assignment to everyone's position at the moment all markers are out; the TTS names the assigned Neurolink, so nobody can swap mid-mechanic
        var playerPositions = sa.Data.PartyList.Select(id => sa.GetById(id)?.Position).ToArray();
        var isMelee = sa.Data.PartyList.Select(id =>
            sa.GetById(id) is IBattleChara bc && MeleeJobIds.Contains(bc.ClassJob.RowId)).ToArray();
        _upm.P4.SolveHatchOrder(keys, isMelee, playerPositions, _upm.NeurolinkPositions);
    }

    [ScriptMethod(name: "P4_Hatch Guide & TTS",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9902"],
        userControl: true)]
    public async void P4_HatchGuideAndTts(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 4000) return;

        if (!await WaitUntilConditions(
            conditions:
            [
                () => !_upm.P4.HatchOrder.Contains(-1)
            ])) return;

        for (int i = 0; i < 3; i++)
        {
            var playerIndex = _upm.P4.HatchOrder[i];
            if (!Debugging && sa.GetMyIndex() != playerIndex) continue;

            var tPos = _upm.NeurolinkPositions[i];
            sa.DrawGuidance(sa.Data.PartyList[playerIndex], tPos, 0, 5500, 
                $"P4_{_upm.Phase}_HatchGuide{playerIndex}", sa.Data.DefaultSafeColor);

            var ttsStr = i == 2 ? "Hatch after Twister" : "Hatch before Twister";
            sa.DebugMsg($"{sa.GetPlayerJobByIndex(playerIndex)} -> Neurolink {i + 1}: {ttsStr}", order: i);
            if (sa.GetMyIndex() != playerIndex) continue;
            sa.TextInfo(ttsStr, isWarning: true);
            sa.TTS(ttsStr);
        }
    }

    [ScriptMethod(name: "P4_Hatch Reset",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9902"],
        userControl: Debugging)]
    public void P4_HatchReset(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 4000) return;
        _pd.Init($"P4 Hatch");
        _upm.P4.HatchOrder = [-1, -1, -1];
    }

    #endregion P4

    #region P5

    [ScriptMethod(name: "———————— [P5] ————————",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P5_Divider(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "P5_Phase Transition",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9970", "TargetIndex:1"], 
        userControl: Debugging)]
    public void P5_Phase(Event ev, ScriptAccessory sa)
    {
        _upm.Phase = 5000;
        sa.Method.RemoveDraw(".*");
    }
    
    [ScriptMethod(name: "P5_Morn Afah Stack",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9964"],
        userControl: true)]
    public void P5_MornAfah(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 5000) return;
        _upm.P5.StackRound++;
        sa.TextInfo($"Morn Afah #{_upm.P5.StackRound} - Stack", destroyMs: 4000, isWarning: true);
        sa.TTS($"Stack {_upm.P5.StackRound}");
        var color = sa.Data.DefaultSafeColor.WithW(3);
        sa.DrawCircle(ev.TargetId, 0, 6000, $"P5_{_upm.Phase}_MornAfah", 4f, color, byTime: true);
    }
    
    [ScriptMethod(name: "P5_Akh Morn Tankbuster",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9962"],
        userControl: true)]
    public void P5_AkhMorn(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 5000) return;
        _upm.P5.BusterRound++;
        var destroyMs = 6500 + 1000 * _upm.P5.BusterRound;
        sa.TextInfo($"Akh Morn #{_upm.P5.BusterRound} - Tankbuster", destroyMs: 4000, isWarning: true);
        sa.TTS($"Tankbuster {_upm.P5.BusterRound}");
        var color = sa.GetMyIndex() <= 1 ? sa.Data.DefaultSafeColor.WithW(3) : sa.Data.DefaultDangerColor.WithW(3);
        sa.DrawCircle(ev.TargetId, 0, destroyMs, $"P5_{_upm.Phase}_AkhMorn", 4f, color);
    }
    
    [ScriptMethod(name: "P5_Exaflare (Start)",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9968"],
        userControl: true)]
    public void P5_ExaflareStart(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 5000) return;
        var spos = ev.SourcePosition;
        var srot = ev.SourceRotation;
        var explodeColor = new Vector4(0f, 1f, 1f, 1.5f);
        var warnColor = new Vector4(0f, 0.5f, 1f, 1f);
        
        // Deliberately keep the second warning up a little longer so it doesn't vanish and reappear too abruptly
        int[] destroyMs = [4000, 4250, 6000];
        
        for (int i = 0; i < 3; i++)
        {
            var pos = spos.RotateAndExtend(spos, srot, i * 8);
            var color = i == 0 ? explodeColor : warnColor.WithW(0.8f / i);
            sa.DrawCircle(pos, 0, destroyMs[i], $"P5_Exaflare_Origin", 6f, color, byTime: i == 0);
        }
    }
    
    [ScriptMethod(name: "P5_Exaflare (Follow-up)",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(996[89])$", "TargetIndex:1"],
        userControl: true)]
    public void P5_ExaflareNext(Event ev, ScriptAccessory sa)
    {
        if (_upm.Phase != 5000) return;
        var srot = ev.SourceRotation;
        var spos = ev.SourcePosition;
        var explodeColor = new Vector4(0f, 1f, 1f, 1.5f);
        var warnColor = new Vector4(0f, 0.5f, 1f, 1f);
        
        // Deliberately keep the second warning up a little longer so it doesn't vanish and reappear too abruptly
        int[] destroyMs = [1500, 1750, 3000];
        
        for (int i = 0; i < 3; i++)
        {
            var pos = spos.RotateAndExtend(spos, srot, (i + 1) * 8);
            var color = i == 0 ? explodeColor : warnColor.WithW(0.8f / i);
            sa.DrawCircle(pos, 0, destroyMs[i], $"P5_Exaflare_Next", 6f, color, byTime: i == 0);
        }
    }

    #endregion P5

}

#region Priority Table
internal class PriorityEntry
{
    public int Key { get; set; }
    public string Name { get; set; } = "";
    public int Value { get; set; }
}

internal class PriorityDict
{
    public Dictionary<int, PriorityEntry> Entries { get; set; } = [];
    public string Annotation { get; set; } = "";
    public int ActionCount { get; set; } = 0;

    private static readonly List<string> DefaultName = ["MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4"];

    /// <summary>
    /// Indexer: reads/writes the Value of the given Key directly
    /// Throws if the Key does not exist
    /// </summary>
    public int this[int key]
    {
        get => Entries[key].Value;
        set => Entries[key].Value = value;
    }

    public void Init(string annotation, int entryCount = 8,
        List<string>? names = null, bool refreshActionCount = true)
    {
        Entries.Clear();

        // Validate the length of names
        if (names != null && names.Count != entryCount)
            throw new ArgumentException($"names length ({names.Count}) does not match entryCount ({entryCount})");

        // Resolve each entry's Name
        List<string> resolvedNames;
        if (names != null)
        {
            resolvedNames = names;
        }
        else if (entryCount == DefaultName.Count)
        {
            resolvedNames = DefaultName;
        }
        else
        {
            throw new ArgumentException($"entryCount = {entryCount} has no default names; please pass names");
        }

        for (var i = 0; i < entryCount; i++)
        {
            Entries.Add(i, new PriorityEntry { Key = i, Name = resolvedNames[i], Value = 0 });
        }
        Annotation = annotation;
        if (refreshActionCount)
            ActionCount = 0;
    }

    /// <summary>
    /// Adds priority to the given Key
    /// </summary>
    /// <param name="key">key</param>
    /// <param name="priority">Priority value</param>
    public void AddPriority(int key, int priority)
    {
        if (!Entries.TryGetValue(key, out var entry))
            throw new KeyNotFoundException($"Key {key} does not exist");
        entry.Value += priority;
    }

    /// <summary>
    /// Returns a new list of the num entries with the smallest values
    /// </summary>
    /// <param name="num"></param>
    /// <returns></returns>
    public List<PriorityEntry> SelectSmallPriorityIndices(int num)
    {
        return SelectMiddlePriorityIndices(0, num);
    }

    /// <summary>
    /// Returns a new list of the num entries with the largest values
    /// </summary>
    /// <param name="num"></param>
    /// <returns></returns>
    public List<PriorityEntry> SelectLargePriorityIndices(int num)
    {
        return SelectMiddlePriorityIndices(0, num, true);
    }

    /// <summary>
    /// Returns a new list taken from the middle of the ascending order
    /// </summary>
    /// <param name="skip">Number of entries to skip. To start from the second one, skip=1</param>
    /// <param name="num">Number of entries to take</param>
    /// <param name="descending">Sort descending; defaults to false</param>
    /// <returns></returns>
    public List<PriorityEntry> SelectMiddlePriorityIndices(int skip, int num, bool descending = false)
    {
        if (Entries.Count < skip + num)
            return new List<PriorityEntry>();

        IOrderedEnumerable<PriorityEntry> sortedEntries = descending
            ? Entries.Values.OrderByDescending(e => e.Value).ThenBy(e => e.Key)
            : Entries.Values.OrderBy(e => e.Value).ThenBy(e => e.Key);

        return sortedEntries.Skip(skip).Take(num).ToList();
    }

    /// <summary>
    /// Returns the entry at position idx of the ascending order
    /// </summary>
    /// <param name="idx"></param>
    /// <param name="descending">Sort descending; defaults to false</param>
    /// <returns></returns>
    public PriorityEntry SelectSpecificPriorityIndex(int idx, bool descending = false)
    {
        if (idx < 0 || idx >= Entries.Count)
            throw new ArgumentOutOfRangeException(nameof(idx));

        IOrderedEnumerable<PriorityEntry> sortedEntries = descending
            ? Entries.Values.OrderByDescending(e => e.Value).ThenBy(e => e.Key)
            : Entries.Values.OrderBy(e => e.Value).ThenBy(e => e.Key);

        return sortedEntries.ElementAt(idx);
    }

    /// <summary>
    /// Returns where the entry with the given key lands once sorted by Value
    /// </summary>
    /// <param name="key"></param>
    /// <param name="descending">Sort descending; defaults to false</param>
    /// <returns></returns>
    public int FindPriorityIndexOfKey(int key, bool descending = false)
    {
        if (!Entries.TryGetValue(key, out var targetEntry))
            throw new ArgumentOutOfRangeException(nameof(key));

        int count = 0;
        foreach (var pair in Entries)
        {
            if (pair.Key == key) continue;

            bool isBetter = descending
                ? pair.Value.Value > targetEntry.Value
                : pair.Value.Value < targetEntry.Value;

            if (isBetter) count++;
            else if (pair.Value.Value == targetEntry.Value && pair.Key < key)
                count++;
        }
        return count;
    }

    /// <summary>
    /// Adds priority values to all entries at once
    /// Usually for special priority orders (e.g. H-T-D-H)
    /// </summary>
    /// <param name="priorities"></param>
    public void AddPriorities(List<int> priorities)
    {
        if (Entries.Count != priorities.Count)
            throw new ArgumentException("Input list length differs from the entry count");

        for (var i = 0; i < Entries.Count; i++)
            AddPriority(i, priorities[i]);
    }

    /// <summary>
    /// Prints the Keys and priorities of the priority table
    /// </summary>
    /// <returns></returns>
    public string ShowPriorities(bool showName = true)
    {
        var str = $"{Annotation} ({ActionCount}-th) priority table:\n";
        if (Entries.Count == 0)
        {
            str += $"PriorityDict Empty.\n";
            return str;
        }
        foreach (var entry in Entries.Values)
        {
            str += $"Key {entry.Key} {(showName ? $"({entry.Name})" : "")}, Value {entry.Value}\n";
        }

        return str;
    }

    public void AddActionCount(int count = 1)
    {
        ActionCount += count;
    }

    public string ShowGroup(string name, List<PriorityEntry> entryList)
    {
        return $"{name}: {string.Join(" ", entryList.Select(x => $"({x.Name}, {x.Value})"))}";
    }
}

#endregion Priority Table

#region Parameter Containers
internal class UcobParams
{
    public int Phase = 0;
    public List<Vector3> NeurolinkPositions = [];
    public Vector3 TankSpot = Vector3.Zero;
    public int LiquidHellHitCount = 0;

    public UcobParamsP1 P1 = new();
    public UcobParamsP2 P2 = new();
    public UcobParamsP3 P3 = new();
    public UcobParamsP4 P4 = new();
    public UcobParamsP5 P5 = new();

    public void Reset()
    {
        Phase = 0;
        NeurolinkPositions.Clear();
        TankSpot = Vector3.Zero;
        LiquidHellHitCount = 0;
        P1.Reset();
        P2.Reset();
        P3.Reset();
        P4.Reset();
        P5.Reset();
    }
}

internal static class UcobExtension
{
    public static int GetNearestNeurolinkIndex(this UcobParams upm, Vector3 spos)
    {
        int minIdx = 0;
        float minLength = 999f;
        for (int i = 0; i < upm.NeurolinkPositions.Count; i++)
        {
            float length = upm.NeurolinkPositions[i].GetLength(spos);
            if (length < minLength)
            {
                minLength = length;
                minIdx = i;
            }
        }
        return minIdx;
    }

    public static int SolveTankSpot(this UcobParams upm)
    {
        if (upm.NeurolinkPositions.Count != 3) return -1;
        var rad0 = upm.NeurolinkPositions[0].GetRadian(UcobReborn.Center);
        var rad1 = upm.NeurolinkPositions[1].GetRadian(UcobReborn.Center);
        var rad = MathF.Atan2(MathF.Sin(rad0) + MathF.Sin(rad1), MathF.Cos(rad0) + MathF.Cos(rad1));
        upm.TankSpot = new Vector3(0, 0, 15f).RotateAndExtend(UcobReborn.Center, rad);
        return 0;
    } 
}

#region P1 Params

internal class UcobParamsP1
{
    public int SkillIndex = 0;
    public List<TwinTaniaSkills> SkillRotation = [TwinTaniaSkills.Plummet, TwinTaniaSkills.Twister, TwinTaniaSkills.DeathSentence];
    public ulong TwintaniaObjId = 0;
    public int LiquidHellRotationHitCount = 0;
    
    internal const uint Plummet = 9896;
    internal const uint Twister = 9898;
    internal const uint DeathSentence = 9897;
    internal const uint LiquidHell = 9901;
    internal const uint Hatch = 9902;
    
    public void Reset()
    {
        SkillIndex = 0;
        LiquidHellRotationHitCount = 0;
        this.LoadPhaseRotation(0);
        TwintaniaObjId = 0;
    }
}

internal enum TwinTaniaSkills
{
    Plummet,
    Twister,
    DeathSentence,
    LiquidHell,
    LiquidHellRandom,
    Hatch,
}

internal static class UcobP1Extension
{
    public static void LoadPhaseRotation(this UcobParamsP1 p1, int currentPhase)
    {
        // Return the skill rotation for the given phase
        p1.SkillRotation = currentPhase switch
        {
            1100 =>
            [
                TwinTaniaSkills.LiquidHell, TwinTaniaSkills.Hatch, TwinTaniaSkills.LiquidHell, TwinTaniaSkills.DeathSentence,
                TwinTaniaSkills.Hatch, TwinTaniaSkills.Twister, TwinTaniaSkills.Plummet
            ],
            1200 =>
            [
                TwinTaniaSkills.LiquidHell, TwinTaniaSkills.Hatch, TwinTaniaSkills.LiquidHellRandom, TwinTaniaSkills.DeathSentence,
                TwinTaniaSkills.Plummet, TwinTaniaSkills.Hatch, TwinTaniaSkills.Twister, TwinTaniaSkills.Plummet
            ],
            _ =>
            [
                TwinTaniaSkills.Plummet, TwinTaniaSkills.Twister, TwinTaniaSkills.DeathSentence
            ],
        };
    }
    public static void AdvanceCyclicSkillIndex(this UcobParamsP1 p1)
    {
        if (p1.SkillRotation.Count == 0) return;
        p1.SkillIndex = (p1.SkillIndex + 1) % p1.SkillRotation.Count;
    }
}

#endregion P1 Params

#region P2 Params
internal class OuterDragon
{
    public ulong ObjectId { get; set; }
    public int Region { get; set; }
}

internal enum NaelQuoteSkills
{
    IronChariot,
    LunarDynamo,
    ThermionicBeam,
    DalamudDive,
    RavenDive,
    MeteorStream,
}
internal class UcobParamsP2
{
    public ulong NaelObjId = 0;
    public List<OuterDragon> Dragons = [];
    public bool Doom1Recorded = false;
    public int WingsOfSalvationIndex = 0;
    public int CleansePuddleIndex = 0;
    public int FireballRound = 0;
    public int FireballHitRecordRound = 0;
    public List<int> FireballHitPlayers = [];
    public int DivebombMarkRound = 0;
    public int DivebombAoeHandledRound = 0;
    public int DivebombGuideHandledRound = 0;
    public int DivebombReturnHandledRound = 0;
    public List<int> DivebombBaitSpots = [];
    public List<int> DivebombBaiters = [];
    public void Reset()
    {
        NaelObjId = 0;
        Dragons.Clear();
        FireballRound = 0;
        FireballHitRecordRound = 0;
        FireballHitPlayers = [];
        ResetDoom();
        ResetDivebombs();
    }

    public void ResetDoom()
    {
        Doom1Recorded = false;
        WingsOfSalvationIndex = 0;
        CleansePuddleIndex = 0;
    }

    public void ResetDivebombs()
    {
        DivebombMarkRound = 0;
        DivebombAoeHandledRound = 0;
        DivebombGuideHandledRound = 0;
        DivebombReturnHandledRound = 0;
        DivebombBaitSpots.Clear();
        DivebombBaiters.Clear();
    }
}

internal static class UcobP2Extension
{
    public static void SolveDivebombBaitSpots(this UcobParamsP2 p2)
    {
        if (p2.Dragons.Count == 0) return;
        List<int> dragonRegionList = p2.Dragons.Select(t => t.Region).ToList();
        p2.DivebombBaitSpots = dragonRegionList switch
        {
            [0, 1, 2, 3, 4] => [11, 5, 7],
            [0, 1, 2, 3, 5] => [11, 5, 7],
            [0, 1, 2, 3, 6] => [11, 5, 7],
            [0, 1, 2, 3, 7] => [11, 5, 8],
            [0, 1, 2, 4, 5] => [2, 5, 8],
            [0, 1, 2, 4, 6] => [11, 5, 8],
            [0, 1, 2, 4, 7] => [2, 5, 8],
            [0, 1, 2, 5, 6] => [2, 5, 10],
            [0, 1, 2, 5, 7] => [11, 5, 8],
            [0, 1, 2, 6, 7] => [2, 5, 8],
            [0, 1, 3, 4, 5] => [2, 6, 9],
            [0, 1, 3, 4, 6] => [11, 3, 8],
            [0, 1, 3, 4, 7] => [2, 6, 8],
            [0, 1, 3, 5, 6] => [2, 6, 10],
            [0, 1, 3, 5, 7] => [2, 6, 9],
            [0, 1, 3, 6, 7] => [11, 3, 8],
            [0, 1, 4, 5, 6] => [2, 5, 10],
            [0, 1, 4, 5, 7] => [2, 5, 10],
            [0, 1, 4, 6, 7] => [11, 5, 8],
            [0, 1, 5, 6, 7] => [2, 6, 11],
            [0, 2, 3, 4, 5] => [2, 6, 8],
            [0, 2, 3, 4, 6] => [2, 6, 8],
            [0, 2, 3, 4, 7] => [2, 6, 8],
            [0, 2, 3, 5, 6] => [2, 6, 10],
            [0, 2, 3, 5, 7] => [2, 6, 9],
            [0, 2, 3, 6, 7] => [2, 6, 8],
            [0, 2, 4, 5, 6] => [2, 5, 10],
            [0, 2, 4, 5, 7] => [2, 5, 9],
            [0, 2, 4, 6, 7] => [2, 5, 8],
            [0, 2, 5, 6, 7] => [2, 6, 11],
            [0, 3, 4, 5, 6] => [2, 8, 10],
            [0, 3, 4, 5, 7] => [2, 8, 10],
            [0, 3, 4, 6, 7] => [2, 8, 11],
            [0, 3, 5, 6, 7] => [2, 6, 11],
            [0, 4, 5, 6, 7] => [3, 9, 11],
            [1, 2, 3, 4, 5] => [1, 6, 8],
            [1, 2, 3, 4, 6] => [1, 6, 8],
            [1, 2, 3, 4, 7] => [1, 6, 9],
            [1, 2, 3, 5, 6] => [4, 7, 10],
            [1, 2, 3, 5, 7] => [1, 6, 9],
            [1, 2, 3, 6, 7] => [4, 7, 11],
            [1, 2, 4, 5, 6] => [4, 8, 10],
            [1, 2, 4, 5, 7] => [1, 5, 9],
            [1, 2, 4, 6, 7] => [4, 8, 11],
            [1, 2, 5, 6, 7] => [11, 6, 1],
            [1, 3, 4, 5, 6] => [3, 8, 10],
            [1, 3, 4, 5, 7] => [3, 8, 10],
            [1, 3, 4, 6, 7] => [3, 8, 11],
            [1, 3, 5, 6, 7] => [3, 9, 11],
            [1, 4, 5, 6, 7] => [4, 9, 11],
            [2, 3, 4, 5, 6] => [2, 5, 10],
            [2, 3, 4, 5, 7] => [2, 7, 10],
            [2, 3, 4, 6, 7] => [2, 8, 11],
            [2, 3, 5, 6, 7] => [5, 9, 11],
            [2, 4, 5, 6, 7] => [5, 9, 11],
            [3, 4, 5, 6, 7] => [4, 9, 11],
            _ => []
        };
    }
}

#endregion P2 Params

#region P3 Params

internal enum BahamutSkills
{
    FlareBreath,
    TripleFlareBreath,
    Flatten,
    Gigaflare,
    None
}

internal class UcobParamsP3
{
    public ulong BahamutObjId = 0;
    public ulong NaelObjId = 0;
    public ulong TwintaniaObjId = 0;
    public int SkillIndex = 0;
    public int FlareBreathDrawnIndex = -1;
    public List<BahamutSkills> SkillRotation = [BahamutSkills.FlareBreath, BahamutSkills.Flatten, BahamutSkills.None];
    public int TripleBreathHitCount = 0;

    public Vector3 NaelPos = Vector3.Zero;
    public Vector3 TwintaniaPos = Vector3.Zero;
    public Vector3 BahamutPos = Vector3.Zero;
    
    internal const uint TempestWing = 9943;
    internal const uint FlareBreath = 9940;
    internal const uint Gigaflare = 9942;
    internal const uint Flatten = 9941;

    public int NaelDir = -1;
    public int TwintaniaDir = -1;
    public int BahamutDir = -1;
    public int NaelRecordedPhase = 0;
    public int TwintaniaRecordedPhase = 0;
    public int BahamutRecordedPhase = 0;

    public int FellruinQuoteCount = 0;
    public int[] FellruinNeurolinks = [-1, -1, -1];
    
    public int[] HeavensfallTwisterDirs = [-1, -1, -1, -1, -1, -1, -1, -1];
    public List<int> HeavensfallTowerDirs = [];
    public int[] HeavensfallTowerAssignments = [-1, -1, -1, -1, -1, -1, -1, -1];

    public List<int> TenstrikeMarkedPlayers = [];
    public int[] TenstrikeHatchPlayers = [-1, -1, -1, -1, -1, -1];
    public bool TenstrikeHatchDrawn = false;
    public int EarthshakerLinkDrawVersion = 0;
    public int EarthshakerLineDrawVersion = 0;
    public int EarthshakerGuideDrawVersion = 0;
    public int EarthshakerHitCount = 0;

    public int[] OctetStartAndDirection = [-1, -1];
    public uint TowerIconOffset = 0;
    
    public void Reset()
    {
        BahamutObjId = 0;
        NaelObjId = 0;
        TwintaniaObjId = 0;
        SkillIndex = 0;
        FlareBreathDrawnIndex = -1;
        TripleBreathHitCount = 0;
        this.LoadPhaseRotation(0);

        NaelRecordedPhase = 0;
        TwintaniaRecordedPhase = 0;
        BahamutRecordedPhase = 0;
        NaelPos = Vector3.Zero;
        TwintaniaPos = Vector3.Zero;
        BahamutPos = Vector3.Zero;
        this.ResetBossDirs();

        FellruinQuoteCount = 0;
        FellruinNeurolinks = [-1, -1, -1];

        ResetHeavensfall();
        ResetTenstrike();

        OctetStartAndDirection = [-1, -1];
        TowerIconOffset = 0;
    }

    public void ResetHeavensfall()
    {
        HeavensfallTwisterDirs = [-1, -1, -1, -1, -1, -1, -1, -1];
        HeavensfallTowerDirs = [];
        HeavensfallTowerAssignments = [-1, -1, -1, -1, -1, -1, -1, -1];
    }
    
    public void ResetTenstrike()
    {
        TenstrikeMarkedPlayers = [];
        TenstrikeHatchPlayers = [-1, -1, -1, -1, -1, -1];
        TenstrikeHatchDrawn = false;
        EarthshakerLinkDrawVersion = 0;
        EarthshakerLineDrawVersion = 0;
        EarthshakerGuideDrawVersion = 0;
        EarthshakerHitCount = 0;
    }
}

internal static class UcobP3Extension
{
    public static void LoadPhaseRotation(this UcobParamsP3 p3, int currentPhase)
    {
        // Return the skill rotation for the given phase
        p3.SkillRotation = currentPhase switch
        {
            3000 =>
            [
                BahamutSkills.FlareBreath, BahamutSkills.Flatten, BahamutSkills.None
            ],
            3150 =>
            [
                BahamutSkills.FlareBreath, BahamutSkills.Flatten, BahamutSkills.None
            ],
            3250 =>
            [
                BahamutSkills.Gigaflare, BahamutSkills.TripleFlareBreath, BahamutSkills.None
            ],
            3350 =>
            [
                BahamutSkills.Gigaflare, BahamutSkills.FlareBreath, BahamutSkills.Flatten, BahamutSkills.FlareBreath, BahamutSkills.None
            ],
            3450 =>
            [
                BahamutSkills.Gigaflare, BahamutSkills.TripleFlareBreath, BahamutSkills.None
            ],
            3550 =>
            [
                BahamutSkills.Gigaflare, BahamutSkills.Flatten, BahamutSkills.FlareBreath, BahamutSkills.None
            ],
            _ => 
            [
                BahamutSkills.FlareBreath, BahamutSkills.Flatten, BahamutSkills.None
            ],
        };
        p3.SkillIndex = 0;
        p3.FlareBreathDrawnIndex = -1;
    }
    public static void AdvanceSkillIndex(this UcobParamsP3 p3)
    {
        if (p3.SkillRotation.Count == 0) return;
        p3.SkillIndex += 1;
    }
    
    public static int SolveHeavensfallTwisters(this UcobParamsP3 p3)
    {
        // Nael not in the middle:
        //   MT, H1 go to the base direction rotated 90° counterclockwise (i.e. base + 2)
        //   D2, D4 go to the base direction rotated 90° clockwise (i.e. base - 2)
        //   ST, H2 go to Nael's direction
        //   D1, D3 go opposite Nael (i.e. Nael + 4)
        // Nael in the middle (Nael's direction == base direction):
        //   MT, ST go to base; H1, D1 to base + 2; H2, D2 to base - 2; D3, D4 to base + 4

        p3.HeavensfallTwisterDirs = [-1, -1, -1, -1, -1, -1, -1, -1];
        int[] bossDirections = [p3.BahamutDir, p3.NaelDir, p3.TwintaniaDir];

        if (bossDirections.Any(direction => direction is < 0 or > 7) ||
            bossDirections.Distinct().Count() != bossDirections.Length)
            return -1;

        var bossDirectionSet = new HashSet<int>(bossDirections);
        var baseDirection = -1;
        for (var idx = 0; idx < bossDirections.Length; idx++)
        {
            var candidate = bossDirections[idx];
            if (!bossDirectionSet.Contains((candidate + 7) % 8) ||
                !bossDirectionSet.Contains((candidate + 1) % 8))
                continue;

            baseDirection = candidate;
            break;
        }
        if (baseDirection == -1) return -1;

        var baseCcw = (baseDirection + 2) % 8;
        var baseCw = (baseDirection + 6) % 8;
        var oppositeNael = (p3.NaelDir + 4) % 8;
        p3.HeavensfallTwisterDirs = p3.NaelDir == baseDirection
            ?
            [
                p3.NaelDir, p3.NaelDir, baseCcw, baseCw,
                baseCcw, baseCw, oppositeNael, oppositeNael
            ]
            :
            [
                baseCcw, p3.NaelDir, baseCcw, p3.NaelDir,
                oppositeNael, baseCw, oppositeNael, baseCw
            ];
        return 0;
    }

    public static int SolveHeavensfallTowers(this UcobParamsP3 p3)
    {
        // Using Nael's direction as the base direction,
        // the towers met going counterclockwise from the base are taken by: ST MT H1 D1 D3 D4 D2 H2
        p3.HeavensfallTowerAssignments = [-1, -1, -1, -1, -1, -1, -1, -1];

        if (p3.NaelDir is < 0 or > 7 ||
            p3.HeavensfallTowerDirs.Count != 8 ||
            p3.HeavensfallTowerDirs.Any(direction => direction is < 0 or > 15))
            return -1;

        var towerDirectionSet = new HashSet<int>(p3.HeavensfallTowerDirs);
        if (towerDirectionSet.Count != p3.HeavensfallTowerDirs.Count) return -1;

        var baseDirection = p3.NaelDir * 2 + 1;
        int[] playerOrder = [0, 2, 4, 6, 7, 5, 3, 1];
        var towerIdx = 0;
        for (var offset = 0; offset < 16 && towerIdx < playerOrder.Length; offset++)
        {
            var towerDirection = (baseDirection + offset) % 16;
            if (!towerDirectionSet.Contains(towerDirection)) continue;

            p3.HeavensfallTowerAssignments[playerOrder[towerIdx]] = towerDirection;
            towerIdx++;
        }
        return 0;
    }

    public static void SolveTenstrikeHatchPlayers(this UcobParamsP3 p3, List<Vector3> neurolinks, Vector3?[] playerPositions)
    {
        // Hatch takers: match the three marked players to the three Neurolinks one-to-one with the smallest total distance (nearest-each would let two players fight over one Neurolink)
        // Interceptors: pick three of the remaining players and match them to the three Neurolinks the same way; whoever isn't picked stays put
        p3.TenstrikeHatchPlayers = [-1, -1, -1, -1, -1, -1];
        if (neurolinks.Count != 3) return;

        var unmarkedPlayers = Enumerable.Range(0, playerPositions.Length).Except(p3.TenstrikeMarkedPlayers).ToList();
        p3.TenstrikeHatchPlayers = [..AssignNearest(p3.TenstrikeMarkedPlayers), ..AssignNearest(unmarkedPlayers)];

        int[] AssignNearest(List<int> players)
        {
            // Players we can't find count as very far away and sort last
            float Dist(int p, int i) => playerPositions[p] is { } pos ? Vector3.Distance(pos, neurolinks[i]) : 999f;
            int[] best = [-1, -1, -1];
            var bestSum = float.MaxValue;
            foreach (var a in players)
            foreach (var b in players)
            foreach (var c in players)
            {
                if (a == b || a == c || b == c) continue;
                var sum = Dist(a, 0) + Dist(b, 1) + Dist(c, 2);
                if (sum >= bestSum) continue;
                bestSum = sum;
                best = [a, b, c];
            }
            return best;
        }
    }

    public static int SolveOctetStart(this UcobParamsP3 p3)
    {
        // Find this mechanic's starting direction and running direction from Bahamut's and Nael's positions.
        // Stored into OctetStartAndDirection: index 0 is the starting direction, index 1 is the running direction
        // If index 1 is -1, run clockwise; if it is 1, run counterclockwise

        // Rules:
        // 1. If Bahamut is on a cardinal (0,2,4,6, i.e. N/E/S/W), run counterclockwise; if on an intercardinal, run clockwise
        // 2. If the spot opposite Bahamut (+4) isn't Nael, start opposite Bahamut
        // 3. If Nael is opposite Bahamut, start one step past Nael in the running direction.
        p3.OctetStartAndDirection = [-1, -1];

        if (p3.BahamutDir is < 0 or > 7 || p3.NaelDir is < 0 or > 7)
            return -1;

        var runDirection = p3.BahamutDir % 2 == 0 ? 1 : -1;
        var oppositeBahamut = (p3.BahamutDir + 4) % 8;
        var startDirection = oppositeBahamut != p3.NaelDir
            ? oppositeBahamut
            : (p3.NaelDir + runDirection + 8) % 8;

        p3.OctetStartAndDirection = [startDirection, runDirection];
        return 0;
    }

    public static void ResetBossDirs(this UcobParamsP3 p3)
    {
        p3.NaelDir = -1;
        p3.TwintaniaDir = -1;
        p3.BahamutDir = -1;
    }
}

#endregion P3 Params

#region P4 Params

internal class UcobParamsP4
{
    public int[] HatchOrder = [-1, -1, -1];
    
    public void Reset()
    {
        HatchOrder = [-1, -1, -1];
    }
}

internal static class UcobP4Extension
{
    public static int SolveHatchOrder(this UcobParamsP4 p4, int[] markedPlayers, bool[] isMelee,
        Vector3?[] playerPositions, List<Vector3> neurolinks)
    {
        // Melee get priority for Neurolinks 0/1, which are close to the boss (the tank spot sits between 0 and 1); among assignments that satisfy this, take the smallest total distance
        // Melee priority = try not to give the far Neurolink 2 to a melee; doing so costs a 1000 penalty, which outweighs any distance difference
        p4.HatchOrder = [-1, -1, -1];
        if (neurolinks.Count != 3) return -1;

        // Players we can't find count as very far away; it doesn't matter where they go
        float Dist(int p, int i) => playerPositions[p] is { } pos ? Vector3.Distance(pos, neurolinks[i]) : 999f;
        int[] best = [-1, -1, -1];
        var bestCost = float.MaxValue;
        foreach (var a in markedPlayers)
        foreach (var b in markedPlayers)
        foreach (var c in markedPlayers)
        {
            if (a == b || a == c || b == c) continue;
            var cost = Dist(a, 0) + Dist(b, 1) + Dist(c, 2) + (isMelee[c] ? 1000f : 0f);
            if (cost >= bestCost) continue;
            bestCost = cost;
            best = [a, b, c];
        }

        // Write back all at once when done; the guide side polls until there's no -1, then draws
        p4.HatchOrder = best;
        return best.Contains(-1) ? -1 : 0;
    }
}

#endregion P4 Params

#region P5 Params

internal class UcobParamsP5
{
    public int StackRound = 0;
    public int BusterRound = 0;
    
    public void Reset()
    {
        StackRound = 0;
        BusterRound = 0;
    }
}

internal static class UcobP5Extension
{
}

#endregion P5 Params

#endregion Parameter Containers

#region Helpers
internal static class EventExtensions
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

    public static uint Id0(this Event ev)
    {
        return ParseHexId(ev["Id"], out var id) ? id : 0;
    }

    public static uint DurationMilliseconds(this Event ev)
    {
        return JsonConvert.DeserializeObject<uint>(ev["DurationMilliseconds"]);
    }

    public static uint DataId(this Event ev)
    {
        return JsonConvert.DeserializeObject<uint>(ev["DataId"]);
    }
    
    public static uint SourceDataId(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["SourceDataId"]);
    }
}

internal static class IbcHelper
{
    public static IGameObject? GetById(this ScriptAccessory sa, ulong gameObjectId)
    {
        return sa.Data.Objects.SearchById(gameObjectId);
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
#region Math

internal static class MathTools
{
    public static float DegToRad(this float deg) => (deg + 360f) % 360f / 180f * float.Pi;
    public static float RadToDeg(this float rad) => (rad + 2 * float.Pi) % (2 * float.Pi) / float.Pi * 180f;
    
    /// <summary>
    /// Gets the angle (in radians) of a point around a center: the (0, 0, 1) direction is 0 and (1, 0, 0) is pi/2.
    /// i.e. it increases counterclockwise.
    /// </summary>
    /// <param name="point">Any point</param>
    /// <param name="center">Center point</param>
    /// <returns></returns>
    public static float GetRadian(this Vector3 point, Vector3 center)
        => MathF.Atan2(point.X - center.X, point.Z - center.Z);

    /// <summary>
    /// Gets the horizontal distance between a point and the center.
    /// </summary>
    /// <param name="point">Any point</param>
    /// <param name="center">Center point</param>
    /// <returns></returns>
    public static float GetLength(this Vector3 point, Vector3 center)
        => new Vector2(point.X - center.X, point.Z - center.Z).Length();

    public static string ToStr(this Vector3 point, int digits = 2)
        => $"({point.X.ToString($"F{digits}")}, {point.Y.ToString($"F{digits}")}, {point.Z.ToString($"F{digits}")})";

    /// <summary>
    /// Rotates a point counterclockwise around the center and extends it outward.
    /// </summary>
    /// <param name="point">Any point</param>
    /// <param name="center">Center point</param>
    /// <param name="radian">Rotation in radians</param>
    /// <param name="length">Extra length to extend from the point</param>
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
    /// Gets which region an angle falls into
    /// </summary>
    /// <param name="radian">Input angle in radians</param>
    /// <param name="regionNum">Number of regions</param>
    /// <param name="baseRegionIdx">Index of the region that contains 0°</param>>
    /// <param name="isDiagDiv">Whether the regions are split diagonally; defaults to false</param>
    /// <param name="isCw">Whether the index increases clockwise; defaults to false</param>
    /// <returns></returns>
    public static int RadianToRegion(this float radian, int regionNum, int baseRegionIdx = 0, bool isDiagDiv = false, bool isCw = false)
    {
        var sepRad = float.Pi * 2 / regionNum;
        var inputAngle = radian * (isCw ? -1 : 1) + (isDiagDiv ? sepRad / 2 : 0);
        var rad = (inputAngle + 4 * float.Pi) % (2 * float.Pi);
        return ((int)Math.Floor(rad / sepRad) + baseRegionIdx + regionNum) % regionNum;
    }
    
    /// <summary>
    /// Gets the given decimal digit of an integer
    /// </summary>
    /// <param name="val">Input value</param>
    /// <param name="x">Digit position; the ones place is 0</param>
    /// <returns>The digit at that position, or 0 if x is out of range</returns>
    public static int GetDecimalDigit(this int val, int x)
        => (int)(Math.Abs(val) / Math.Pow(10, x) % 10);
    
}

#endregion Math

#region Party Index Helpers
internal static class IndexHelper
{
    /// <summary>
    /// Gets the party index for a player ID
    /// </summary>
    /// <param name="pid">Player SourceId</param>
    /// <param name="sa"></param>
    /// <returns>That player's party index</returns>
    public static int GetPlayerIdIndex(this ScriptAccessory sa, uint pid)
    {
        // Get the player's index
        return sa.Data.PartyList.IndexOf(pid);
    }

    /// <summary>
    /// Gets the party index of the local player
    /// </summary>
    /// <param name="sa"></param>
    /// <returns>The local player's party index</returns>
    public static int GetMyIndex(this ScriptAccessory sa)
    {
        return sa.Data.PartyList.IndexOf(sa.Data.Me);
    }

    public static bool IsValidPartyIndex(this ScriptAccessory sa, int index)
    {
        return index >= 0 && index < sa.Data.PartyList.Count;
    }

    /// <summary>
    /// Gets the role label for a player ID; for text output only
    /// </summary>
    /// <param name="pid">Player SourceId</param>
    /// <param name="sa"></param>
    /// <returns>That player's role label</returns>
    public static string GetPlayerJobById(this ScriptAccessory sa, uint pid)
    {
        // Get the player's role abbreviation; only used for DEBUG output
        var idx = sa.Data.PartyList.IndexOf(pid);
        var str = sa.GetPlayerJobByIndex(idx);
        return str;
    }

    /// <summary>
    /// Gets the role label for a party index; for text output only
    /// </summary>
    /// <param name="idx">Party index</param>
    /// <param name="fourPeople">Whether this is a 4-player duty</param>
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
#endregion Party Index Helpers

#region Drawing Helpers

internal static class DrawTools
{
    private static CancellationTokenSource _lifecycleCts = new();

    public static void ResetLifecycle()
    {
        _lifecycleCts.Cancel();
        _lifecycleCts.Dispose();
        _lifecycleCts = new CancellationTokenSource();
    }

    public static async void TextInfo(this ScriptAccessory sa, string msg,
        int delayMs = 0, int destroyMs = 3000, bool isWarning = false)
    {
        var token = _lifecycleCts.Token;
        try
        {
            await Task.Delay(delayMs, token);
            sa.Method.TextInfo(msg, destroyMs, isWarning);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public static async void TTS(this ScriptAccessory sa, string msg,
        int delayMs = 0, int voiceSpeed = 3)
    {
        var token = _lifecycleCts.Token;
        try
        {
            await Task.Delay(delayMs, token);
            sa.Method.TTS(msg, voiceSpeed);
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>
    /// Returns a drawing
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">Drawing anchor; a UID or a position</param>
    /// <param name="targetObj">What the drawing points at; a UID or a position</param>
    /// <param name="delay">Appears after delay ms</param>
    /// <param name="destroy">Disappears destroy ms after appearing</param>
    /// <param name="name">Drawing name</param>
    /// <param name="radian">Arc of the shape in radians</param>
    /// <param name="rotation">Rotation of the shape in radians, relative to the owner's facing, increasing counterclockwise</param>
    /// <param name="width">Shape width; some shapes keep it equal to the length</param>
    /// <param name="length">Shape length; some shapes keep it equal to the width</param>
    /// <param name="innerWidth">Shape inner width; some shapes keep it equal to the inner length</param>
    /// <param name="innerLength">Shape inner length; some shapes keep it equal to the inner width</param>
    /// <param name="drawModeEnum">Draw mode</param>
    /// <param name="drawTypeEnum">Draw type</param>
    /// <param name="color">Color</param>
    /// <param name="byTime">Animate the fill over time</param>
    /// <param name="byY">Animate the scale with distance</param>
    /// <param name="draw">Whether to draw immediately</param>
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
                throw new ArgumentException($"ownerObj {ownerObj} has an invalid type {ownerObj.GetType()}");
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
                throw new ArgumentException($"targetObj {targetObj} has an invalid type {targetObj.GetType()}");
        }
        
        if (draw)
            sa.Method.SendDraw(drawModeEnum, drawTypeEnum, dp);
        return dp;
    }

    /// <summary>
    /// Returns a guide (path arrow) drawing
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">Start point</param>
    /// <param name="targetObj">End point</param>
    /// <param name="delay">Delay</param>
    /// <param name="destroy">Lifetime</param>
    /// <param name="name">Drawing name</param>
    /// <param name="rotation">Arrow rotation</param>
    /// <param name="width">Arrow width</param>
    /// <param name="color">Color</param>
    /// <param name="draw">Whether to draw immediately</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawGuidance(this ScriptAccessory sa,
        object ownerObj, object targetObj, int delay, int destroy, string name,
        Vector4 color, float rotation = 0, float width = 1f, bool draw = true, bool useImgui = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, 0, rotation, width,
            width, 0, 0, useImgui ? DrawModeEnum.Imgui : DrawModeEnum.Default, 
            DrawTypeEnum.Displacement, color, false, true, draw);
    
    public static DrawPropertiesEdit DrawGuidance(this ScriptAccessory sa,
        object targetObj, int delay, int destroy, string name, Vector4 color, float rotation = 0, float width = 1f,
        bool draw = true, bool useImgui = true)
        => sa.DrawGuidance((ulong)sa.Data.Me, targetObj, delay, destroy, name, color, rotation, width, draw, useImgui);

    /// <summary>
    /// Returns a circle drawing
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">Center</param>
    /// <param name="delay">Delay</param>
    /// <param name="destroy">Lifetime</param>
    /// <param name="name">Drawing name</param>
    /// <param name="scale">Circle radius</param>
    /// <param name="byTime">Whether it fills over time</param>
    /// <param name="color">Color</param>
    /// <param name="draw">Whether to draw immediately</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawCircle(this ScriptAccessory sa,
        object ownerObj, int delay, int destroy, string name,
        float scale, Vector4 color, bool byTime = false, bool draw = true, bool useImgui = false)
        => sa.DrawOwnerBase(ownerObj, 0, delay, destroy, name, 2 * float.Pi, 0, scale, scale,
            0, 0, useImgui ? DrawModeEnum.Imgui : DrawModeEnum.Default, DrawTypeEnum.Circle, color, byTime,false, draw);

    /// <summary>
    /// Returns a donut drawing
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">Center</param>
    /// <param name="delay">Delay</param>
    /// <param name="destroy">Lifetime</param>
    /// <param name="name">Drawing name</param>
    /// <param name="outScale">Outer radius</param>
    /// <param name="innerScale">Inner radius</param>
    /// <param name="byTime">Whether it fills over time</param>
    /// <param name="color">Color</param>
    /// <param name="draw">Whether to draw immediately</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawDonut(this ScriptAccessory sa,
        object ownerObj, int delay, int destroy, string name,
        float outScale, float innerScale, Vector4 color, bool byTime = false, bool draw = true, bool useImgui = false)
        => sa.DrawOwnerBase(ownerObj, 0, delay, destroy, name, 2 * float.Pi, 0, outScale, outScale, innerScale,
            innerScale, useImgui ? DrawModeEnum.Imgui : DrawModeEnum.Default, 
            DrawTypeEnum.Donut, color, byTime, false, draw);
    
    /// <summary>
    /// Returns a fan (cone) drawing
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">Center</param>
    /// <param name="targetObj">Target</param>
    /// <param name="delay">Delay</param>
    /// <param name="destroy">Lifetime</param>
    /// <param name="name">Drawing name</param>
    /// <param name="radian">Arc in radians</param>
    /// <param name="rotation">Rotation</param>
    /// <param name="outScale">Outer radius</param>
    /// <param name="innerScale">Inner radius</param>
    /// <param name="byTime">Whether it fills over time</param>
    /// <param name="color">Color</param>
    /// <param name="draw">Whether to draw immediately</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawFan(this ScriptAccessory sa,
        object ownerObj, object targetObj, int delay, int destroy, string name, float radian, float rotation,
        float outScale, float innerScale, Vector4 color, bool byTime = false, bool draw = true, bool useImgui = false)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, radian, rotation, outScale, outScale, innerScale,
            innerScale, useImgui ? DrawModeEnum.Imgui : DrawModeEnum.Default, 
            innerScale == 0 ? DrawTypeEnum.Fan : DrawTypeEnum.Donut, color, byTime, false, draw);

    public static DrawPropertiesEdit DrawFan(this ScriptAccessory sa,
        object ownerObj, int delay, int destroy, string name, float radian, float rotation,
        float outScale, float innerScale, Vector4 color, bool byTime = false, bool draw = true, bool useImgui = false)
        => sa.DrawFan(ownerObj, 0, delay, destroy, name, radian, rotation, outScale, innerScale, 
            color, byTime, draw, useImgui);

    /// <summary>
    /// Returns a rectangle drawing
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">Rectangle origin</param>
    /// <param name="targetObj">Target</param>
    /// <param name="delay">Delay</param>
    /// <param name="destroy">Lifetime</param>
    /// <param name="name">Drawing name</param>
    /// <param name="rotation">Rotation</param>
    /// <param name="width">Rectangle width</param>
    /// <param name="length">Rectangle length</param>
    /// <param name="byTime">Whether it fills over time</param>
    /// <param name="byY">Whether it scales with distance</param>
    /// <param name="color">Color</param>
    /// <param name="draw">Whether to draw immediately</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawRect(this ScriptAccessory sa,
        object ownerObj, object targetObj, int delay, int destroy, string name, float rotation,
        float width, float length, Vector4 color, bool byTime = false, bool byY = false, 
        bool draw = true, bool useImgui = false)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, 0, rotation, width, length, 0, 0,
            useImgui ? DrawModeEnum.Imgui : DrawModeEnum.Default, DrawTypeEnum.Rect, color, byTime, byY, draw);
    
    public static DrawPropertiesEdit DrawRect(this ScriptAccessory sa,
        object ownerObj, int delay, int destroy, string name, float rotation,
        float width, float length, Vector4 color, bool byTime = false, bool byY = false, 
        bool draw = true, bool useImgui = false)
        => sa.DrawRect(ownerObj, 0, delay, destroy, name, rotation, width, length, color, byTime, byY, draw, useImgui);
    
    /// <summary>
    /// Returns a knockback drawing
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="targetObj">Knockback source</param>
    /// <param name="delay">Delay</param>
    /// <param name="destroy">Lifetime</param>
    /// <param name="name">Drawing name</param>
    /// <param name="width">Arrow width</param>
    /// <param name="length">Arrow length</param>
    /// <param name="color">Color</param>
    /// <param name="draw">Whether to draw immediately</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawKnockBack(this ScriptAccessory sa,
        object targetObj, int delay, int destroy, string name, float width, float length,
        Vector4 color, bool draw = true, bool useImgui = false)
        => sa.DrawOwnerBase(sa.Data.Me, targetObj, delay, destroy, name, 0, float.Pi, width, length, 0, 0,
            useImgui ? DrawModeEnum.Imgui : DrawModeEnum.Default, DrawTypeEnum.Displacement, color, false, false, draw);

    /// <summary>
    /// Returns a line drawing
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">Line start</param>
    /// <param name="targetObj">Line target</param>
    /// <param name="delay">Delay</param>
    /// <param name="destroy">Lifetime</param>
    /// <param name="name">Drawing name</param>
    /// <param name="rotation">Rotation</param>
    /// <param name="width">Line width</param>
    /// <param name="length">Line length</param>
    /// <param name="byTime">Whether it fills over time</param>
    /// <param name="byY">Whether it scales with distance</param>
    /// <param name="color">Color</param>
    /// <param name="draw">Whether to draw immediately</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawLine(this ScriptAccessory sa,
        object ownerObj, object targetObj, int delay, int destroy, string name, float rotation,
        float width, float length, Vector4 color, bool byTime = false, bool byY = false, 
        bool draw = true, bool useImgui = false)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, 1, rotation, width, length, 0, 0,
            useImgui ? DrawModeEnum.Imgui : DrawModeEnum.Default, DrawTypeEnum.Line, color, byTime, byY, draw);

    /// <summary>
    /// Returns an arrow drawing
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">Arrow start</param>
    /// <param name="targetObj">Arrow target</param>
    /// <param name="delay">Delay</param>
    /// <param name="destroy">Lifetime</param>
    /// <param name="name">Drawing name</param>
    /// <param name="rotation">Rotation</param>
    /// <param name="width">Arrow width</param>
    /// <param name="length">Arrow length</param>
    /// <param name="byTime">Whether it fills over time</param>
    /// <param name="byY">Whether it scales with distance</param>
    /// <param name="color">Color</param>
    /// <param name="draw">Whether to draw immediately</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawArrow(this ScriptAccessory sa,
        object ownerObj, object targetObj, int delay, int destroy, string name, float rotation,
        float width, float length, Vector4 color, 
        bool byTime = false, bool byY = false, bool draw = true, bool useImgui = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, 1, rotation, width, length, 0, 0,
            useImgui ? DrawModeEnum.Imgui : DrawModeEnum.Default, DrawTypeEnum.Arrow, color, byTime, byY, draw);

    /// <summary>
    /// Returns a connecting line drawn between two objects
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">Start object</param>
    /// <param name="targetObj">Target object</param>
    /// <param name="delay">Delay</param>
    /// <param name="destroy">Lifetime</param>
    /// <param name="name">Drawing name</param>
    /// <param name="width">Line width</param>
    /// <param name="color">Color</param>
    /// <param name="draw">Whether to draw immediately</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawConnection(this ScriptAccessory sa, object ownerObj, object targetObj,
        int delay, int destroy, string name, Vector4 color, float width = 1f, bool draw = true, bool useImgui = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, 0, 0, width, width,
            0, 0, useImgui ? DrawModeEnum.Imgui : DrawModeEnum.Default, DrawTypeEnum.Line, color, false, true, draw);

    /// <summary>
    /// Makes the given dp resolve its position by enmity order
    /// </summary>
    /// <param name="self"></param>
    /// <param name="setOwner">Assign the resolved target to the owner</param>
    /// <param name="orderIdx">Enmity order, starting from 1</param>
    /// <returns></returns>
    public static DrawPropertiesEdit SetEnmityOrder(this DrawPropertiesEdit self, bool setOwner, uint orderIdx)
    {
        if (setOwner)
        {
            self.CentreResolvePattern = PositionResolvePatternEnum.OwnerEnmityOrder;
            self.CentreOrderIndex = orderIdx;
        }
        else
        {
            self.TargetResolvePattern = PositionResolvePatternEnum.OwnerEnmityOrder;
            self.TargetOrderIndex = orderIdx;
        }

        return self;
    }
    
    /// <summary>
    /// Makes the given dp resolve its position from the owner's target
    /// </summary>
    /// <param name="self"></param>
    /// <param name="setOwner">Assign the resolved target to the owner</param>
    /// <returns></returns>
    public static DrawPropertiesEdit SetOwnerTarget(this DrawPropertiesEdit self, bool setOwner)
    {
        if (setOwner)
            self.CentreResolvePattern = PositionResolvePatternEnum.OwnerTarget;
        else
            self.TargetResolvePattern = PositionResolvePatternEnum.OwnerTarget;
        return self;
    }
    
    /// <summary>
    /// Adds an Omen at the given position
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="position">Spawn position</param>
    /// <param name="omenId">omenID</param>
    /// <param name="delayMs">Delay</param>
    /// <param name="destroyMs">Lifetime</param>
    /// <param name="omenScale">Omen scale multiplier</param>
    /// <param name="color">Omen color</param>
    /// <param name="rotation">Omen rotation in radians</param>
    /// <param name="speed">Playback speed</param>
    /// <returns></returns>
    public static void DrawOmen(this ScriptAccessory sa, Vector3 position, uint omenId,
        int delayMs, int destroyMs, Vector3 omenScale, Vector4? color = null, float rotation = 0f, float speed = 1f)
    {
        Task.Delay(Math.Max(50, delayMs)).ContinueWith(t =>
        {
            var handle = sa.Method.VfxMethod.CreateOmen(omenId,
                omenScale, position, rotation,
                color ?? Vector4.One, destroyAt: destroyMs);
            sa.Method.RunOnMainThreadAsync(() =>
            {
                sa.Method.VfxMethod.SetVfxSpeed(handle, speed);
            });
        });
    }
    
    /// <summary>
    /// Adds a LockOn marker on an entity
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="objectId">Entity</param>
    /// <param name="lockonId">Head marker ID</param>
    /// <param name="delayMs">Delay</param>
    /// <param name="destroyMs">Lifetime</param>
    /// <param name="iconScale">Head marker scale multiplier</param>
    /// <param name="speed">Playback speed</param>
    /// <returns></returns>
    public static void DrawLockOn(this ScriptAccessory sa, ulong objectId, uint lockonId,
        int delayMs, int destroyMs, Vector3 iconScale, float speed = 1)
    {
        Task.Delay(Math.Max(50, delayMs)).ContinueWith(t =>
        {
            var handle = sa.Method.VfxMethod.CreateLockOn(lockonId, objectId, Vector4.One, destroyAt: destroyMs,
                (ref Vector4 color, ref Vector4 pos, ref Vector3 scale) =>
                {
                    scale = iconScale;
                });
            sa.Method.RunOnMainThreadAsync(() =>
            {
                sa.Method.VfxMethod.SetVfxSpeed(handle, speed);
            });
        });
    }

    /// <summary>
    /// Adds a LockOn marker at the given position
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="position">Spawn position</param>
    /// <param name="lockonId">Head marker ID</param>
    /// <param name="delayMs">Delay</param>
    /// <param name="destroyMs">Lifetime</param>
    /// <param name="objIdBias">ID offset for the spawned dummy entity</param>
    /// <param name="iconScale">Head marker scale multiplier</param>
    /// <param name="speed">Playback speed</param>
    /// <returns></returns>
    public static void DrawLockOn(this ScriptAccessory sa, Vector3 position, uint lockonId,
        int delayMs, int destroyMs, Vector3 iconScale, uint objIdBias = 0, float speed = 1)
    {
        var virtualObjectId = 0x40001234u + objIdBias;
        var objHandle = sa.Method.ObjectMethod.CreateEmptyChara(virtualObjectId);
        
        Task.Delay(Math.Max(50, delayMs)).ContinueWith(t =>
        {
            var handle = sa.Method.VfxMethod.CreateLockOn(lockonId, virtualObjectId, Vector4.One, destroyAt: destroyMs,
                (ref Vector4 color, ref Vector4 pos, ref Vector3 scale) =>
                {
                    scale = iconScale;
                    unsafe
                    {
                        var obj = (BattleChara*)objHandle;
                        obj->Position = position;
                    }
                });
            sa.Method.RunOnMainThreadAsync(() =>
            {
                sa.Method.VfxMethod.SetVfxSpeed(handle, speed);
            });
            Task.Delay(destroyMs).ContinueWith(t =>
            {
                if (sa.GetById(virtualObjectId) is not { } obj) return;
                sa.Method.ObjectMethod.DestoryChara(objHandle);
            });
        });
    }

    public static void DrawCountDown(this ScriptAccessory sa, ulong objectId,
        int delayMs, float iconScale = 2f, float speed = 1)
        => sa.DrawLockOn(objectId, 184, delayMs, (int)(6000f / speed), new Vector3(iconScale, iconScale, iconScale),
            speed);

    public static void DrawCountDown(this ScriptAccessory sa, Vector3 position, 
        int delayMs, float iconScale = 2f, uint objIdBias = 0, float speed = 1)
        => sa.DrawLockOn(position, 184, delayMs, (int)(6000f / speed), new Vector3(iconScale, iconScale, iconScale), objIdBias, speed);

    public static void DrawLaser(this ScriptAccessory sa, Vector3 position, 
        int delayMs, int destroyMs, Vector3 omenScale, Vector4? color = null)
        => sa.DrawOmen(position, 358, delayMs, destroyMs, omenScale, color ?? Vector4.One);
}

#endregion Drawing Helpers

#region Debug Helpers

/// <summary>
/// Debug output helpers. All output goes through an ordered buffer and is flushed sorted by order priority.
/// </summary>
internal static class DebugFunction
{
    private static readonly List<string> PrefixWhiteList = [""];
    private static readonly List<string> PrefixBlackList = [""];
    
    /// <summary>
    /// One record in the ordered output buffer.
    /// </summary>
    private record Entry(int Order, long Seq, string Content, bool ShowInChatBox);

    private static readonly List<Entry> _buffer = new();
    private static long _seq;
    private static CancellationTokenSource? _cts;
    private static readonly object _lock = new();
    
    public static void DebugMsg(this ScriptAccessory sa, string msg,
        int order = 0,
        [CallerMemberName] string prefix = "",
        bool showInChatBox = true, bool enableWhiteList = false, bool enableBlackList = true,
        int flushDelayMs = 200, bool immediate = false)
    {
        if (!UcobReborn.Debugging) return;
        
        if (enableWhiteList)
            if (!PrefixWhiteList.Contains(prefix)) return;
        if (enableBlackList)
            if (PrefixBlackList.Contains(prefix)) return;

        var content = $"[{prefix}] {msg}";

        if (immediate)
        {
            sa.Log.Debug(content);
            if (showInChatBox)
                sa.Method.SendChat($"/e {content}");
            return;
        }
        
        lock (_lock)
        {
            _buffer.Add(new Entry(order, _seq++, content, showInChatBox));

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            Task.Delay(flushDelayMs, token).ContinueWith(t =>
            {
                if (t.IsCanceled) return;
                FlushInternal(sa);
            });
        }
    }

    public static void FlushDebugMsg(this ScriptAccessory sa)
    {
        FlushInternal(sa);
    }

    public static void ClearDebugMsg(this ScriptAccessory sa)
    {
        lock (_lock)
        {
            _cts?.Cancel();
            _buffer.Clear();
        }
    }

    private async static void FlushInternal(ScriptAccessory sa)
    {
        List<Entry> sorted;
        lock (_lock)
        {
            if (_buffer.Count == 0) return;
            sorted = _buffer.OrderBy(x => x.Order).ThenBy(x => x.Seq).ToList();
            _buffer.Clear();
        }

        foreach (var entry in sorted)
        {
            sa.Log.Debug(entry.Content);
            if (entry.ShowInChatBox)
                sa.Method.SendChat($"/e {entry.Content}");
            await Task.Delay(20);
        }
    }
}

#endregion Debug Helpers

#region Special Helpers

internal static class SpecialFunction
{
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
                sa.Log.Error($"The given IGameObject is invalid.");
                return;
            }
            
            if (!charaStruct->IsCharacter())
            {
                sa.Log.Error($"The given IGameObject is not a Character, so its alpha can't be changed.");
                return;
            }
            
            if (shouldModify != null && !shouldModify(charaStruct -> Alpha))
                return;

            charaStruct->Alpha = alpha;
            sa.DebugMsg($"AlphaModify => {obj.Name.TextValue} | {obj} => {alpha}");
        }
    }

    public static unsafe void Redraw(this ScriptAccessory sa, IGameObject? obj)
    {
        sa.Method.RunOnMainThreadAsync(Action);
        void Action()
        {
            if (obj == null) return;
            GameObject* charaStruct = (GameObject*)obj.Address;
            if (!obj.IsValid() || !charaStruct->IsReadyToDraw())
            {
                sa.Log.Error($"The given IGameObject is invalid.");
                return;
            }
            
            charaStruct->DisableDraw();
            charaStruct->EnableDraw();
        }
    }
}

#endregion Special Helpers

#endregion Helpers

