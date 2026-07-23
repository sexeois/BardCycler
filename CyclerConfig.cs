using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Windows.Forms;

namespace BardCycler
{
    public class CyclerConfig
    {
        public string ProfileName { get; set; } = "default";
        public Keys CycleHotkey { get; set; } = Keys.F;
        public List<int> TargetSlots { get; set; } = new List<int> { 1, 2, 5, 3 };
        public int RepeatDelayMs { get; set; } = 400;
        public int RepeatRateMs { get; set; } = 120;
        public int InputDelayMs { get; set; } = 15;
        public bool ConsumeHotkey { get; set; } = true;
        public bool Enabled { get; set; } = true;
        public bool AutoRepeatOnHold { get; set; } = true;

        private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public void Save(string path)
        {
            File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOpts));
        }

        public static CyclerConfig Load(string path)
        {
            if (!File.Exists(path))
            {
                var def = new CyclerConfig();
                def.Save(path);
                return def;
            }
            return JsonSerializer.Deserialize<CyclerConfig>(File.ReadAllText(path), JsonOpts);
        }

        public static CyclerConfig Defaults() => new CyclerConfig();

        public CyclerConfig Clone()
        {
            return new CyclerConfig
            {
                ProfileName = ProfileName,
                CycleHotkey = CycleHotkey,
                TargetSlots = new List<int>(TargetSlots),
                RepeatDelayMs = RepeatDelayMs,
                RepeatRateMs = RepeatRateMs,
                InputDelayMs = InputDelayMs,
                ConsumeHotkey = ConsumeHotkey,
                Enabled = Enabled,
                AutoRepeatOnHold = AutoRepeatOnHold
            };
        }
    }
}
