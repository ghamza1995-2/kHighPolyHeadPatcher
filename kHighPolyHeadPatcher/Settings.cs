using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Synthesis.Settings;

namespace kHighPolyHeadPatcher
{
    public class Settings
    {
        [SynthesisSettingName("Whitelist plugins")]
        [SynthesisTooltip(
            "Patch only NPCs found in this list with High Poly Head" +
            "\nLeave blank to patch any NPCs found in load order with High Poly Head")]
        public HashSet<ModKey> PluginList = new();
    }
}
