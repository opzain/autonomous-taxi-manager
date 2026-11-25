using System;
using System.Globalization;
using System.Text;

namespace TaxiSim.Core.Dispatch
{
    /// <summary>The console buttons. Knob = the old rotary encoder's press (kept for older consoles);
    /// Next = the right joystick's click since v0.15 (it does what turning the knob did).</summary>
    public enum ConsoleButton
    {
        Up,
        Down,
        Left,
        Right,
        Back,
        Accept,
        Reject,
        Knob,
        Info,
        Next
    }

    public enum ButtonAction
    {
        Press,
        Long,
        Release
    }

    public enum ConsoleInputKind
    {
        Button,
        Encoder,
        Hello,
        KeepAlive,
        /// <summary>Both joysticks: LX / LY / RX / RY in -100..100 (0 = centre, +X right, +Y up).</summary>
        Sticks
    }

    /// <summary>One message from the ESP32 console (already parsed).</summary>
    public readonly struct ConsoleInput
    {
        public readonly ConsoleInputKind Kind;
        public readonly ConsoleButton Button;
        public readonly ButtonAction Action;
        public readonly int Steps;
        public readonly string Text;
        /// <summary>Joystick axes (only for <see cref="ConsoleInputKind.Sticks"/>), each -100..100.</summary>
        public readonly int LX, LY, RX, RY;

        public ConsoleInput(ConsoleInputKind kind, ConsoleButton button = ConsoleButton.Up, ButtonAction action = ButtonAction.Press, int steps = 0, string text = "")
        {
            Kind = kind;
            Button = button;
            Action = action;
            Steps = steps;
            Text = text ?? "";
            LX = LY = RX = RY = 0;
        }

        private ConsoleInput(int lx, int ly, int rx, int ry)
        {
            Kind = ConsoleInputKind.Sticks;
            Button = ConsoleButton.Up;
            Action = ButtonAction.Press;
            Steps = 0;
            Text = "";
            LX = lx; LY = ly; RX = rx; RY = ry;
        }

        public static ConsoleInput Press(ConsoleButton b) => new ConsoleInput(ConsoleInputKind.Button, b, ButtonAction.Press);
        public static ConsoleInput Long(ConsoleButton b) => new ConsoleInput(ConsoleInputKind.Button, b, ButtonAction.Long);
        public static ConsoleInput Release(ConsoleButton b) => new ConsoleInput(ConsoleInputKind.Button, b, ButtonAction.Release);
        public static ConsoleInput Turn(int steps) => new ConsoleInput(ConsoleInputKind.Encoder, steps: steps);
        public static ConsoleInput Sticks(int lx, int ly, int rx, int ry) => new ConsoleInput(lx, ly, rx, ry);

        public override string ToString() => Kind == ConsoleInputKind.Button ? $"{Button} {Action}" : Kind == ConsoleInputKind.Encoder ? $"Encoder {Steps:+0;-0}"
            : Kind == ConsoleInputKind.Sticks ? $"Sticks L({LX},{LY}) R({RX},{RY})" : Kind.ToString();
    }

    /// <summary>Text colours the TFT understands (one letter each on the wire).</summary>
    public enum RowColor
    {
        White,
        Grey,
        Green,
        Yellow,
        Orange,
        Red,
        Cyan,
        Blue,
        Magenta
    }

    /// <summary>
    /// The USB-serial protocol between the game (PC) and the ESP32 dispatch console. ASCII lines, 115200 baud.
    ///
    /// ESP32 → PC (comma separated, unchanged since the self-test firmware):
    ///   H,TAXI-CONSOLE,&lt;ver&gt;    hello after boot
    ///   B,&lt;BUTTON&gt;,P|L|R          press / long press (600 ms) / release   (UP DOWN LEFT RIGHT BACK ACCEPT REJECT KNOB INFO NEXT)
    ///   E,&lt;±n&gt;                     encoder detents
    ///   J,lx,ly,rx,ry               both joysticks, each -100..100 (sent ~20x a second while a stick is off-centre, then one 0,0,0,0)
    ///   K                           keep-alive, once a second
    ///
    /// PC → ESP32 ('|' separated so text may contain commas; every field is plain ASCII):
    ///   S|clock|cash|sat|surge|speed     header bar
    ///   T|page|pages|title               page title + page dots
    ///   R|row|style|text                 row 0..7; style = colour letter, '*' = selected (inverted), '2' = large text
    ///   F|text                           footer hint
    ///   M|style|text                     toast for 2 s over the footer
    ///   Z|name                           buzzer pattern (click ok err new urgent event warn coin over start)
    ///   L|rrggbb x8                      LED stick colours (ignored when no stick is fitted)
    /// </summary>
    public static class ConsoleProtocol
    {
        public const int Rows = 8;
        public const int SmallChars = 52;   // 320 px / 6 px per character (text size 1)
        public const int LargeChars = 26;   // text size 2

        // ------------------------------------------------------------------ ESP32 → PC

        public static bool TryParse(string line, out ConsoleInput input)
        {
            input = default;
            if (string.IsNullOrEmpty(line)) return false;
            line = line.Trim();
            if (line.Length == 0) return false;
            string[] p = line.Split(',');
            switch (p[0])
            {
                case "K":
                    input = new ConsoleInput(ConsoleInputKind.KeepAlive);
                    return true;
                case "H":
                    if (p.Length < 2 || p[1] != "TAXI-CONSOLE") return false;
                    input = new ConsoleInput(ConsoleInputKind.Hello, text: p.Length > 2 ? p[2] : "");
                    return true;
                case "E":
                    if (p.Length != 2 || !int.TryParse(p[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int steps) || steps == 0) return false;
                    input = ConsoleInput.Turn(steps);
                    return true;
                case "B":
                    if (p.Length != 3 || !TryButton(p[1], out ConsoleButton b)) return false;
                    switch (p[2])
                    {
                        case "P": input = ConsoleInput.Press(b); return true;
                        case "L": input = ConsoleInput.Long(b); return true;
                        case "R": input = ConsoleInput.Release(b); return true;
                    }
                    return false;
                case "J":
                    if (p.Length != 5) return false;
                    var v = new int[4];
                    for (int k = 0; k < 4; k++)
                        if (!int.TryParse(p[k + 1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out v[k]) || v[k] < -100 || v[k] > 100) return false;
                    input = ConsoleInput.Sticks(v[0], v[1], v[2], v[3]);
                    return true;
            }
            return false;
        }

        public static bool TryButton(string name, out ConsoleButton b)
        {
            switch (name)
            {
                case "UP": b = ConsoleButton.Up; return true;
                case "DOWN": b = ConsoleButton.Down; return true;
                case "LEFT": b = ConsoleButton.Left; return true;
                case "RIGHT": b = ConsoleButton.Right; return true;
                case "BACK": b = ConsoleButton.Back; return true;
                case "ACCEPT": b = ConsoleButton.Accept; return true;
                case "REJECT": b = ConsoleButton.Reject; return true;
                case "KNOB": b = ConsoleButton.Knob; return true;
                case "INFO": b = ConsoleButton.Info; return true;
                case "NEXT": b = ConsoleButton.Next; return true;
            }
            b = ConsoleButton.Up;
            return false;
        }

        public static string ButtonName(ConsoleButton b) => b.ToString().ToUpperInvariant();

        // ------------------------------------------------------------------ PC → ESP32

        public static char ColorLetter(RowColor c)
        {
            switch (c)
            {
                case RowColor.Grey: return 'D';
                case RowColor.Green: return 'G';
                case RowColor.Yellow: return 'Y';
                case RowColor.Orange: return 'O';
                case RowColor.Red: return 'R';
                case RowColor.Cyan: return 'C';
                case RowColor.Blue: return 'B';
                case RowColor.Magenta: return 'M';
                default: return 'W';
            }
        }

        public static string Style(RowColor c, bool selected = false, bool large = false)
        {
            var sb = new StringBuilder(3);
            sb.Append(ColorLetter(c));
            if (selected) sb.Append('*');
            if (large) sb.Append('2');
            return sb.ToString();
        }

        /// <summary>Plain printable ASCII, no field separators, no line breaks, at most <paramref name="max"/> characters.</summary>
        public static string Clean(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new StringBuilder(Math.Min(text.Length, max));
            foreach (char ch in text)
            {
                if (sb.Length >= max) break;
                switch (ch)
                {
                    case '₹': Append(sb, "Rs", max); continue;
                    case '→': Append(sb, ">", max); continue;
                    case '←': Append(sb, "<", max); continue;
                    case '×': Append(sb, "x", max); continue;
                    case '·': case '•': Append(sb, "-", max); continue;
                    case '−': case '–': case '—': Append(sb, "-", max); continue;
                    case '|': Append(sb, "/", max); continue;
                    case '\r': case '\n': case '\t': Append(sb, " ", max); continue;
                }
                if (ch >= 32 && ch < 127) sb.Append(ch);
            }
            return sb.ToString();
        }

        private static void Append(StringBuilder sb, string s, int max)
        {
            foreach (char c in s) if (sb.Length < max) sb.Append(c);
        }

        public static string Header(string clock, string cash, string satisfaction, string surge, string speed) =>
            $"S|{Clean(clock, 5)}|{Clean(cash, 10)}|{Clean(satisfaction, 4)}|{Clean(surge, 5)}|{Clean(speed, 6)}";

        public static string Title(int page, int pages, string title) => $"T|{page}|{pages}|{Clean(title, LargeChars - 2)}";

        public static string Row(int row, ConsoleRow r) =>
            $"R|{row}|{Style(r.Color, r.Selected, r.Large)}|{Clean(r.Text, r.Large ? LargeChars : SmallChars)}";

        public static string Footer(string text) => $"F|{Clean(text, SmallChars)}";

        public static string Toast(RowColor c, string text) => $"M|{ColorLetter(c)}|{Clean(text, SmallChars)}";

        public static string Buzz(string pattern) => $"Z|{Clean(pattern, 8)}";

        public static string Leds(uint[] rgb)
        {
            var sb = new StringBuilder("L|");
            for (int i = 0; i < 8; i++)
            {
                uint c = rgb != null && i < rgb.Length ? rgb[i] : 0u;
                sb.Append((c & 0xFFFFFF).ToString("X6", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        /// <summary>₹ amount with thousands separators, ASCII ("Rs 12,345", "-Rs 300").</summary>
        public static string Money(double amount)
        {
            string s = Math.Abs(amount).ToString("#,0", CultureInfo.InvariantCulture);
            return (amount < 0 ? "-Rs " : "Rs ") + s;
        }
    }
}
