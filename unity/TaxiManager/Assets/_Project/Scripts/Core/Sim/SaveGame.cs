using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TaxiSim.Core.Sim
{
    /// <summary>
    /// Save files are event-sourced (spec T14): seed + difficulty + the player's command log + the tick and a
    /// state hash. Loading replays the log on a fresh simulation and checks the hash, which proves the
    /// replay reproduced the exact game (and doubles as a regression test).
    /// </summary>
    public static class SaveGame
    {
        public const string Header = "TAXISIM-SAVE v1";

        public static string Write(Simulation sim)
        {
            var sb = new StringBuilder();
            sb.AppendLine(Header);
            sb.AppendLine("seed=" + sim.Seed.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("difficulty=" + sim.Difficulty);
            sb.AppendLine("tick=" + sim.Tick.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("hash=" + sim.StateHash().ToString("X16", CultureInfo.InvariantCulture));
            foreach (Command c in sim.CommandLog) sb.AppendLine("cmd=" + c);
            return sb.ToString();
        }

        /// <summary>Rebuilds the game from a save. Throws if the file is damaged or the replay doesn't match.</summary>
        public static Simulation Load(string text, bool verify = true)
        {
            string[] lines = text.Replace("\r", "").Split('\n');
            if (lines.Length == 0 || lines[0].Trim() != Header) throw new FormatException("Not a Taxi Manager save file.");
            ulong seed = 0, hash = 0;
            int tick = 0;
            var difficulty = Difficulty.Normal;
            var commands = new List<Command>();
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                string key = line.Substring(0, eq), value = line.Substring(eq + 1);
                switch (key)
                {
                    case "seed": seed = ulong.Parse(value, CultureInfo.InvariantCulture); break;
                    case "difficulty": difficulty = (Difficulty)Enum.Parse(typeof(Difficulty), value); break;
                    case "tick": tick = int.Parse(value, CultureInfo.InvariantCulture); break;
                    case "hash": hash = ulong.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture); break;
                    case "cmd": commands.Add(Command.Parse(value)); break;
                }
            }

            var sim = new Simulation(seed, difficulty);
            int next = 0;
            while (sim.Tick < tick || next < commands.Count)
            {
                while (next < commands.Count && commands[next].Tick == sim.Tick) sim.Submit(commands[next++]);
                if (sim.Tick >= tick) break;
                if (sim.IsOver) break;
                sim.Step();
            }
            if (verify && sim.StateHash() != hash)
                throw new InvalidOperationException($"Replay mismatch: expected {hash:X16}, got {sim.StateHash():X16}. The save or the game version differs.");
            return sim;
        }
    }
}
