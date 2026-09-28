using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using KodakkuAssist.Script;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.Draw.Manager;
using KodakkuAssist.Module.Draw.Vfx.VfxNative;

namespace Codaaaaaa.VfxSlotProbe;

[ScriptType(
    guid: "3f7c1a92-6d4b-4f18-9c20-8a5e11d7b6e3",
    name: "VFX槽位探针",
    version: "0.0.0.2",
    author: "Codaaaaaa",
    note: "/e vfx 汇总占用\n/e vfxlist 列出插件每一个绘图\n/e vfxnative 列出非插件的原生特效")]
public class VfxSlotProbe
{
    #region 反射句柄
    // DrawManager / IDrawable 都是 internal，只能反射。这些 MemberInfo 只解析一次。
    private static readonly Type? DrawManagerType =
        typeof(DrawProperties).Assembly.GetType("KodakkuAssist.Module.Draw.Manager.DrawManager");

    private static readonly FieldInfo? ElementsField =
        DrawManagerType?.GetField("DrawElementsList", BindingFlags.NonPublic | BindingFlags.Static);

    private const BindingFlags Inst = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    private static object? Member(object o, string name)
        => (object?)o.GetType().GetProperty(name, Inst)?.GetValue(o)
           ?? o.GetType().GetField(name, Inst)?.GetValue(o);

    private static IList? Elements() => ElementsField?.GetValue(null) as IList;
    #endregion

    // 一条绘图的实况：它现在到底占了几个 VFX 槽位
    private readonly record struct Row(
        string Name, string Shape, bool IsVfx, bool Active, int Slots, long Delay, long DestoryAt, long Remain);

    private static List<Row> Snapshot()
    {
        var rows = new List<Row>();
        var list = Elements();
        if (list is null) return rows;

        object[] snapshot;
        lock (list) snapshot = list.Cast<object>().ToArray();   // DrawManager 内部也是 lock(DrawElementsList)

        foreach (var e in snapshot)
        {
            if (e is null) continue;
            var t = e.GetType();
            var isVfx = t.Namespace?.Contains("Vfx.VfxDraw") == true;

            if (Member(e, "DrawProperties") is not DrawProperties dp) continue;

            // VfxOmenBase：Delay 走完才 new OmenElement，所以 !Active 时不占槽位；
            // ScaleMode.ByTime 会额外建一个 BoundOmenElement，一条绘图吃两个槽。
            var active = !isVfx || Member(e, "Active") as bool? == true;
            var slots = 0;
            if (isVfx && active)
            {
                if (Member(e, "NormalOmenElement") is not null) slots++;
                if (Member(e, "BoundOmenElement") is not null) slots++;
            }

            var running = Member(e, "runningTime") as long? ?? 0;
            rows.Add(new Row(dp.Name ?? "(无名)", t.Name, isVfx, active, slots,
                dp.Delay, dp.DestoryAt, dp.DestoryAt - (running - dp.Delay)));
        }
        return rows;
    }

    private static unsafe (int Total, int Valid, int PluginOmen, int PluginActor) ScanNative()
    {
        var list = VFXList.Instance();
        if (list == null) return (0, 0, 0, 0);

        var span = list->ListSpan;
        int valid = 0, omen = 0, actor = 0;
        for (var i = 0; i < span.Length; i++)
        {
            if (!span[i].IsValid()) continue;
            valid++;
            // OmenElement 构造时写的记号：100 = 插件 Omen，101 = 插件 ActorVfx
            var tag = *(byte*)(span[i].VFXHandle + 456);
            if (tag == 100) omen++;
            else if (tag == 101) actor++;
        }
        return (span.Length, valid, omen, actor);
    }

    [ScriptMethod(name: "VFX - 汇总", eventType: EventTypeEnum.Chat,
        eventCondition: ["Type:Echo", "Message:vfx"], userControl: false)]
    public void 汇总(Event evt, ScriptAccessory sa)
    {
        var (total, valid, omen, actor) = ScanNative();
        var rows = Snapshot();
        var claimed = rows.Sum(r => r.Slots);

        sa.Method.SendChat($"/e [VFX] 原生数组 {valid}/{total} 占用，剩 {total - valid}｜" +
                           $"其中插件Omen {omen}、插件ActorVfx {actor}、游戏自身 {valid - omen - actor}");
        sa.Method.SendChat($"/e [VFX] DrawManager 存活绘图 {rows.Count} 条" +
                           $"（VFX {rows.Count(r => r.IsVfx)} / ImGui {rows.Count(r => !r.IsVfx)}），" +
                           $"合计认领 {claimed} 个槽位");

        // 按脚本前缀归堆，一眼看出是哪个机制在铺
        foreach (var g in rows.Where(r => r.IsVfx && r.Slots > 0)
                     .GroupBy(r => r.Name.Split('-')[0])
                     .OrderByDescending(g => g.Sum(r => r.Slots)))
            sa.Method.SendChat($"/e [VFX]   {g.Key}：{g.Count()} 条 / {g.Sum(r => r.Slots)} 槽");
    }

    [ScriptMethod(name: "VFX - 绘图明细", eventType: EventTypeEnum.Chat,
        eventCondition: ["Type:Echo", "Message:vfxlist"], userControl: false)]
    public void 明细(Event evt, ScriptAccessory sa)
    {
        var rows = Snapshot();
        if (rows.Count == 0)
        {
            sa.Method.SendChat(ElementsField is null
                ? "/e [VFX] 反射不到 DrawManager.DrawElementsList，插件版本可能变了"
                : "/e [VFX] 当前没有存活绘图");
            return;
        }

        foreach (var r in rows.OrderByDescending(r => r.Slots).ThenBy(r => r.Remain).Take(40))
        {
            var state = !r.IsVfx ? "ImGui" : r.Active ? $"VFX×{r.Slots}" : $"等待Delay({r.Delay}ms)";
            sa.Method.SendChat($"/e [VFX] {r.Name} [{r.Shape}] {state} 剩{r.Remain}ms/共{r.DestoryAt}ms");
        }
        if (rows.Count > 40) sa.Method.SendChat($"/e [VFX] …还有 {rows.Count - 40} 条未列出");
    }

    [ScriptMethod(name: "VFX - 原生明细", eventType: EventTypeEnum.Chat,
        eventCondition: ["Type:Echo", "Message:vfxnative"], userControl: false)]
    public unsafe void 原生明细(Event evt, ScriptAccessory sa)
    {
        var list = VFXList.Instance();
        if (list == null) { sa.Method.SendChat("/e [VFX] 拿不到 VFXList 实例"); return; }

        var span = list->ListSpan;
        var shown = 0;
        var skipped = 0;
        for (var i = 0; i < span.Length; i++)
        {
            if (!span[i].IsValid()) continue;
            var tag = *(byte*)(span[i].VFXHandle + 456);
            if (tag == 100 || tag == 101) continue;   // 插件自己的，走 /e vfxlist 看
            if (shown >= 30) { skipped++; continue; }
            shown++;
            sa.Method.SendChat($"/e [VFX] 槽#{i} handle {span[i].VFXHandle:X} " +
                               $"CRC {span[i].CRC:X8} Index {span[i].Index} Destroy {span[i].Destroy}/{span[i].Destroy2}");
        }
        if (shown == 0) sa.Method.SendChat("/e [VFX] 当前没有非插件特效");
        else if (skipped > 0) sa.Method.SendChat($"/e [VFX] …还有 {skipped} 条未列出");
    }
}
