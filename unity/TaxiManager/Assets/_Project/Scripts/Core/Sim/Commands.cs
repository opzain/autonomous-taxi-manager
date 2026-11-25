using System;
using System.Globalization;

namespace TaxiSim.Core.Sim
{
    public enum CommandType
    {
        Assign,       // A taxi request
        Reject,       // R request
        SendCharge,   // C taxi [station node]
        Reroute,      // X taxi
        Relocate,     // L taxi node
        ReleaseCharger, // F taxi
        SetSurge      // S level (0..4)
    }

    /// <summary>
    /// A player decision (from the ESP32 console, the keyboard or the HUD). Commands are the ONLY way
    /// the player changes the simulation; they are logged with their tick, which is what a save file stores.
    /// </summary>
    public readonly struct Command
    {
        public readonly CommandType Type;
        public readonly int A, B;
        public readonly int Tick;

        public Command(CommandType type, int a, int b = -1, int tick = -1)
        {
            Type = type;
            A = a;
            B = b;
            Tick = tick;
        }

        public Command At(int tick) => new Command(Type, A, B, tick);

        public static Command Assign(int taxi, int request) => new Command(CommandType.Assign, taxi, request);
        public static Command Reject(int request) => new Command(CommandType.Reject, request);
        public static Command SendCharge(int taxi, int station = -1) => new Command(CommandType.SendCharge, taxi, station);
        public static Command Reroute(int taxi) => new Command(CommandType.Reroute, taxi);
        public static Command Relocate(int taxi, int node) => new Command(CommandType.Relocate, taxi, node);
        public static Command ReleaseCharger(int taxi) => new Command(CommandType.ReleaseCharger, taxi);
        public static Command SetSurge(int level) => new Command(CommandType.SetSurge, level);

        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} {3}", Tick, Type, A, B);

        public static Command Parse(string line)
        {
            string[] p = line.Trim().Split(' ');
            if (p.Length != 4) throw new FormatException($"Bad command line '{line}'.");
            return new Command((CommandType)Enum.Parse(typeof(CommandType), p[1]),
                int.Parse(p[2], CultureInfo.InvariantCulture), int.Parse(p[3], CultureInfo.InvariantCulture),
                int.Parse(p[0], CultureInfo.InvariantCulture));
        }
    }

    public readonly struct CommandResult
    {
        public readonly bool Ok;
        public readonly string Message;

        public CommandResult(bool ok, string message)
        {
            Ok = ok;
            Message = message;
        }

        public static CommandResult Success(string m) => new CommandResult(true, m);
        public static CommandResult Fail(string m) => new CommandResult(false, m);
        public override string ToString() => (Ok ? "OK: " : "REFUSED: ") + Message;
    }

    public enum MessageKind
    {
        Info,
        Request,
        Event,
        Warning,
        Money,
        GameOver
    }

    /// <summary>One line of the event ticker (also drives the console buzzer / LEDs).</summary>
    public readonly struct SimMessage
    {
        public readonly int Tick;
        public readonly MessageKind Kind;
        public readonly string Text;

        public SimMessage(int tick, MessageKind kind, string text)
        {
            Tick = tick;
            Kind = kind;
            Text = text;
        }

        public override string ToString() => $"[{Tick}] {Kind}: {Text}";
    }
}
