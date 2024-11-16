using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Plugins;
using Noggog;
using Reloaded.Memory.Extensions;

namespace kHighPolyHeadPatcher
{
    public class Program
    {
        private static readonly ModKey HPHModKey = ModKey.FromNameAndExtension("High Poly Head.esm");
        private static readonly string HPHHeadpartPrefix = "00KLH_";
        private static Lazy<Settings> _lazySettings = null!;
        public static Settings Settings => _lazySettings.Value;

        public static async Task<int> Main(string[] args)
        {
            return await SynthesisPipeline.Instance
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .SetAutogeneratedSettings(
                    nickname: "settings",
                    path: "Settings.json",
                    out _lazySettings
                )
                .SetTypicalOpen(GameRelease.SkyrimSE, "High Poly Head Synthesis Patcher.esp")
                .Run(args);
        }

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            state.LoadOrder.TryGetIfEnabledAndExists(HPHModKey, out var hphMod);
            if (hphMod is null)
            {
                throw new Exception("High Poly Head.esm not found. Please install and/or enable it, then run the patch again.");
            }

            Console.WriteLine("High Poly Head found. Running patch...");
            Console.WriteLine();

            var vanillaHphHeadpartDictionary = CreateHeadpartDictionary(hphMod, state);
            List<IFormLinkGetter<IHeadPartGetter>> vanillaHeadParts = [.. vanillaHphHeadpartDictionary.Keys];

            List<INpcGetter> npcsWithVanillaHeadparts = GetNpcsWithVanillaHeadparts(state, vanillaHeadParts);

            Console.WriteLine(npcsWithVanillaHeadparts.Count.ToString() + " NPCs with vanilla head parts found.");
            Console.WriteLine();

            var hphHeads = hphMod.HeadParts
                .Where(hdpt => hdpt.Type == HeadPart.TypeEnum.Face)
                .ToList();

            foreach (var npcWithVanillaHeadparts in npcsWithVanillaHeadparts)
            {
                var npcWithHphHeadparts = state.PatchMod.Npcs.GetOrAddAsOverride(npcWithVanillaHeadparts);
                var isFemale = npcWithHphHeadparts.Configuration.Flags.HasFlagFast(NpcConfiguration.Flag.Female);
                npcWithHphHeadparts.Race.TryResolve(state.LinkCache, out var npcRace);
                var isVampire = npcRace?.EditorID!.Contains("Vampire") ?? false;

                Console.WriteLine("Swapping existing vanilla head parts on " + (npcWithHphHeadparts.Name ?? npcWithHphHeadparts.EditorID) + " for High Poly Head counterparts...");

                // Straight swap
                foreach (var headpart in vanillaHphHeadpartDictionary)
                {
                    var vanillaHeadPart = headpart.Key;
                    var hphHeadPart = headpart.Value;

                    if (npcWithHphHeadparts.HeadParts
                        .Select(hdpt => hdpt.FormKey).ToList()
                        .Contains(vanillaHeadPart.FormKey))
                    {
                        npcWithHphHeadparts.HeadParts.Add(hphHeadPart);
                        npcWithHphHeadparts.HeadParts.RemoveWhere(hdpt => hdpt.FormKey == vanillaHeadPart.FormKey);
                        vanillaHeadPart.TryResolve(state.LinkCache, out var vanillaHeadpartBase);
                        hphHeadPart.TryResolve(state.LinkCache, out var hphHeadPartBase);
                        Console.WriteLine(vanillaHeadpartBase?.EditorID + " swapped out for " + hphHeadPartBase?.EditorID + " on " + (npcWithHphHeadparts.Name ?? npcWithHphHeadparts.EditorID) + ".");
                    }
                }

                Console.WriteLine("Adding High Poly Head to " + (npcWithHphHeadparts.Name ?? npcWithHphHeadparts.EditorID) + "...");

                // Adding new head where needed
                try
                {
                    foreach (var hphHead in hphHeads)
                    {
                        hphHead.ValidRaces.TryResolve(state.LinkCache, out var validRacesFormList);

                        if (validRacesFormList is null) continue;
                        if (!validRacesFormList.Items.Any(item => item.FormKey.Equals(npcRace?.FormKey))) continue;
                        if (isFemale && !hphHead.Flags.HasFlagFast(HeadPart.Flag.Female)) continue;
                        if (isVampire && !hphHead.EditorID!.Contains("Vampire")) continue;

                        npcWithHphHeadparts.HeadParts.Add(hphHead);
                        Console.WriteLine(hphHead.EditorID + " added to " + (npcWithHphHeadparts.Name ?? npcWithHphHeadparts.EditorID) + ".");
                        Console.WriteLine();
                        break;
                    }
                }
                catch (Exception ex) 
                {
                    throw new Exception(ex.ToString());
                }
            }
            Console.WriteLine("Patching complete!");
        }

        private static Dictionary<IFormLinkGetter<IHeadPartGetter>, IFormLinkGetter<IHeadPartGetter>> CreateHeadpartDictionary(
            ISkyrimModGetter hphMod,
            IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            var gameHeadParts = state.LoadOrder.PriorityOrder.OnlyEnabled().HeadPart().WinningOverrides();
            var vanillaHphHeadpartDictionary = new Dictionary<IFormLinkGetter<IHeadPartGetter>, IFormLinkGetter<IHeadPartGetter>>();

            foreach (var hphHeadPart in hphMod.HeadParts)
            {
                var vanillaHeadpart = gameHeadParts.FirstOrDefault(hdpt => hdpt.EditorID == hphHeadPart.EditorID!.Replace(HPHHeadpartPrefix, ""));

                if (vanillaHeadpart is null) continue;

                vanillaHphHeadpartDictionary[vanillaHeadpart.ToLinkGetter()] = hphHeadPart.ToLinkGetter();
            }

            return vanillaHphHeadpartDictionary;
        }

        private static List<INpcGetter> GetNpcsWithVanillaHeadparts(
            IPatcherState<ISkyrimMod, ISkyrimModGetter> state,
            IList<IFormLinkGetter<IHeadPartGetter>> vanillaHeadparts)
        {
            var modlist = Settings.PluginList.Count == 0 ?
                state.LoadOrder.PriorityOrder :
                state.LoadOrder.ListedOrder.Where(listing => Settings.PluginList.Contains(listing.ModKey)).ToList();

            return modlist.WinningOverrides<INpcGetter>()
                .Where(npc => vanillaHeadparts.Intersect(npc.HeadParts).Any())
                .ToList();
        }
    }
}
