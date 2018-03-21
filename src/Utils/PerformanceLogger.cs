using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace IndentGuide.Utils
{
    public static class PerformanceLogger
    {
        private static readonly object _lock = new object();
        private static List<Event> _events;
        private static DateTime _first = DateTime.MaxValue;

        public static event EventHandler DumpEvents;

        [Conditional("PERFORMANCE")]
        private static void EnsureInitialised()
        {
            if (_events == null) _events = new List<Event>();
        }

        [Conditional("PERFORMANCE")]
        public static void Start(ref object cookie, string suffix = null, [CallerMemberName] string name = null)
        {
            if (cookie != null) throw new ArgumentException("cookie must be null");

            Event e = new Event((name ?? "") + (suffix ?? ""));
            lock (_lock)
            {
                EnsureInitialised();
                _events.Add(e);
            }

            cookie = e;
        }

        [Conditional("PERFORMANCE")]
        public static void End(object cookie)
        {
            if (cookie == null) return;

            lock (_lock)
            {
                ((Event) cookie).Stop();

                if (_events.Count > 100)
                    Task.Run(() =>
                    {
                        string log = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "IndentGuide.csv"
                        );
                        EventHandler evt = DumpEvents;
                        if (evt == null)
                            using (StreamWriter f = new StreamWriter(log, true, Encoding.UTF8))
                            {
                                Dump(f, true);
                            }
                        else
                            evt(null, EventArgs.Empty);
                    });
            }
        }

        [Conditional("PERFORMANCE")]
        public static void Dump(TextWriter writer, bool clear = false)
        {
            List<Event> evts;
            lock (_lock)
            {
                evts = _events.Where(e => !e.IsRunning).ToList();
                if (clear) _events.RemoveAll(e => !e.IsRunning);
            }

            foreach (Event e in evts)
            {
                if (e.Start < _first) _first = e.Start;
                writer.WriteLine(string.Join(",", e.Name, (e.Start - _first).TotalMilliseconds,
                    e.Duration.TotalMilliseconds, e.Start.ToString("o")));
            }
        }

        public static IEnumerable<EventTime> Take()
        {
            List<Event> evts;
            lock (_lock)
            {
                evts = _events.Where(e => !e.IsRunning).ToList();
                _events.RemoveAll(e => !e.IsRunning);
            }

            foreach (Event e in evts)
            {
                if (e.Start < _first) _first = e.Start;
                yield return new EventTime
                {
                    Name = e.Name,
                    Start = (int) (e.Start - _first).Ticks,
                    Duration = (int) e.Duration.Ticks
                };
            }
        }

        internal static void Mark(string message)
        {
            lock (_lock)
            {
                Event evt = new Event(message);
                evt.Stop();
                _events.Add(evt);
            }
        }

        public struct EventTime
        {
            public string Name;
            public long Start;
            public long Duration;
        }

        private class Event
        {
            private Stopwatch _stopwatch;

            public Event(string name)
            {
                Name = name;
                Start = DateTime.Now;
                Duration = TimeSpan.Zero;
                _stopwatch = Stopwatch.StartNew();
            }

            public string Name { get; }
            public DateTime Start { get; }
            public DateTime End => Start + Duration;
            public TimeSpan Duration { get; private set; }

            public bool IsRunning => _stopwatch != null;

            public void Stop()
            {
                _stopwatch.Stop();
                Duration = _stopwatch.Elapsed;
                _stopwatch = null;
            }

            public override string ToString()
            {
                return string.Format("{0}: {1}ms", Name, Duration.TotalMilliseconds);
            }
        }
    }
}