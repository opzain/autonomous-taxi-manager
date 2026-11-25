using System.Collections.Concurrent;
using System.Threading;
#if NET_4_6 || NET_UNITY_4_8
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Text;
#endif

namespace TaxiSim.World
{
    /// <summary>
    /// USB-serial link to the ESP32 dispatch console, on a background thread so the game never stutters.
    ///  * finds the console by itself: tries every COM port at 115200 baud and keeps the one that sends a
    ///    console line (H,TAXI-CONSOLE / K / B / E) within 2.5 s; the last good port is tried first;
    ///  * DTR and RTS stay off so opening the port doesn't keep the ESP32 in reset;
    ///  * "link lost" after 3.5 s without a line (the console sends K every second) → searches again.
    /// Needs Unity's API Compatibility Level = .NET Framework (Taxi Manager → Setup sets it), because
    /// System.IO.Ports is not part of .NET Standard. Without it the game runs on the keyboard only.
    /// </summary>
    public sealed class SerialLink
    {
        public enum LinkState { Off, Searching, Connected, Unsupported }

        public volatile LinkState State = LinkState.Off;
        public volatile string PortName = "";
        public volatile string Status = "";

        private readonly ConcurrentQueue<string> _in = new ConcurrentQueue<string>();
        private readonly ConcurrentQueue<string> _out = new ConcurrentQueue<string>();
        private string _preferred = "";
#if NET_4_6 || NET_UNITY_4_8
        private Thread _thread;
        private volatile bool _run;
#endif

        public bool Connected => State == LinkState.Connected;

        public bool TryRead(out string line) => _in.TryDequeue(out line);

        public void Send(string line)
        {
            if (State != LinkState.Connected) return;
            if (_out.Count > 200) return; // console gone quiet: don't pile up
            _out.Enqueue(line);
        }

#if NET_4_6 || NET_UNITY_4_8
        private SerialPort _port;
        private readonly StringBuilder _partial = new StringBuilder();

        public void Start(string preferredPort)
        {
            if (_thread != null) return;
            _preferred = preferredPort ?? "";
            _run = true;
            State = LinkState.Searching;
            _thread = new Thread(Loop) { IsBackground = true, Name = "TaxiConsoleSerial" };
            _thread.Start();
        }

        public void Stop()
        {
            _run = false;
            try { _thread?.Join(800); } catch { /* shutting down */ }
            _thread = null;
            Close();
            State = LinkState.Off;
        }

        private void Loop()
        {
            while (_run)
            {
                if (_port == null)
                {
                    State = LinkState.Searching;
                    if (!Search()) Sleep(1500);
                    continue;
                }
                Pump();
            }
        }

        private bool Search()
        {
            string[] names;
            try { names = SerialPort.GetPortNames(); }
            catch (Exception e) { Status = "Can't list COM ports: " + e.Message; return false; }
            var order = new List<string>(names);
            order.Sort(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(_preferred) && order.Remove(_preferred)) order.Insert(0, _preferred);
            Status = order.Count == 0 ? "No COM ports — is the console plugged in (data cable, CP210x driver)?" : "Trying " + string.Join(", ", order);

            foreach (string name in order)
            {
                if (!_run) return false;
                SerialPort p = null;
                try
                {
                    p = new SerialPort(name, 115200, Parity.None, 8, StopBits.One)
                    {
                        DtrEnable = false, RtsEnable = false, ReadTimeout = 50, WriteTimeout = 300,
                        Encoding = Encoding.ASCII, NewLine = "\n"
                    };
                    p.Open();
                    _partial.Length = 0;
                    var clock = Stopwatch.StartNew();
                    while (clock.ElapsedMilliseconds < 2500 && _run)
                    {
                        foreach (string line in ReadLines(p))
                        {
                            if (!LooksLikeConsole(line)) continue;
                            _port = p;
                            PortName = name;
                            _preferred = name;
                            State = LinkState.Connected;
                            Status = "Console connected on " + name;
                            if (!line.StartsWith("H,")) _in.Enqueue("H,TAXI-CONSOLE,?"); // already running: ask for a full screen
                            _in.Enqueue(line);
                            _lastRx = Stopwatch.StartNew();
                            return true;
                        }
                        Sleep(20);
                    }
                }
                catch (Exception) { /* busy / not a console */ }
                try { p?.Close(); } catch { /* ignore */ }
            }
            return false;
        }

        private Stopwatch _lastRx = new Stopwatch();

        private void Pump()
        {
            try
            {
                bool any = false;
                foreach (string line in ReadLines(_port))
                {
                    _in.Enqueue(line);
                    any = true;
                }
                if (any) _lastRx.Restart();
                int sent = 0;
                while (sent < 40 && _out.TryDequeue(out string o))
                {
                    _port.Write(o + "\n");
                    sent++;
                }
                if (_lastRx.ElapsedMilliseconds > 3500)
                {
                    Status = "Link lost on " + PortName + " — searching";
                    Close();
                    return;
                }
                if (!any && sent == 0) Sleep(5);
            }
            catch (Exception e)
            {
                Status = "Link error: " + e.Message;
                Close();
            }
        }

        private IEnumerable<string> ReadLines(SerialPort p)
        {
            int n = p.BytesToRead;
            if (n <= 0) yield break;
            var buf = new byte[Math.Min(n, 4096)];
            int got = p.Read(buf, 0, buf.Length);
            for (int i = 0; i < got; i++)
            {
                char c = (char)buf[i];
                if (c == '\n')
                {
                    string line = _partial.ToString().Trim();
                    _partial.Length = 0;
                    if (line.Length > 0) yield return line;
                }
                else if (c != '\r' && _partial.Length < 256) _partial.Append(c);
            }
        }

        private static bool LooksLikeConsole(string line) =>
            line.StartsWith("H,TAXI-CONSOLE") || line == "K" || line.StartsWith("B,") || line.StartsWith("E,") || line.StartsWith("J,");

        private void Close()
        {
            try { _port?.Close(); } catch { /* ignore */ }
            _port = null;
            while (_out.TryDequeue(out _)) { }
            if (_run) State = LinkState.Searching;
        }

        private static void Sleep(int ms) => Thread.Sleep(ms);
#else
        public void Start(string preferredPort)
        {
            _preferred = preferredPort ?? "";
            State = LinkState.Unsupported;
            Status = "USB console needs Project Settings > Player > Api Compatibility Level = .NET Framework (run Taxi Manager > Setup). Keyboard works.";
        }

        public void Stop() => State = LinkState.Off;
#endif
    }
}
