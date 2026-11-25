using System;
using System.Collections.Generic;

namespace TaxiSim.Core
{
    public sealed class RouteResult
    {
        public bool Success { get; }
        public int From { get; }
        public int To { get; }
        public IReadOnlyList<int> Nodes { get; }
        public IReadOnlyList<int> Edges { get; }
        public double Minutes { get; }
        public double Km { get; }
        public string FailureReason { get; }

        private RouteResult(bool success, int from, int to, List<int> nodes, List<int> edges, double minutes, double km, string reason)
        {
            Success = success;
            From = from;
            To = to;
            Nodes = nodes;
            Edges = edges;
            Minutes = minutes;
            Km = km;
            FailureReason = reason;
        }

        public static RouteResult Ok(int from, int to, List<int> nodes, List<int> edges, double minutes, double km) =>
            new RouteResult(true, from, to, nodes, edges, minutes, km, null);

        public static RouteResult Fail(int from, int to, string reason) =>
            new RouteResult(false, from, to, new List<int>(), new List<int>(), 0, 0, reason);
    }

    /// <summary>
    /// Weighted shortest path (Dijkstra). Edge weight = base minutes × traffic multiplier.
    /// Blocked roads are skipped; when no path exists the result explains why (spec §22).
    /// Ties are broken by lower node id, so the result is deterministic.
    /// </summary>
    public static class RouteFinder
    {
        public static RouteResult FindFastest(CityGraph graph, int from, int to)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            int n = graph.Nodes.Count;
            if (from < 0 || from >= n || to < 0 || to >= n) return RouteResult.Fail(from, to, "Unknown location.");
            if (from == to) return RouteResult.Ok(from, to, new List<int> { from }, new List<int>(), 0, 0);

            var dist = new double[n];
            var prevEdge = new int[n];
            var done = new bool[n];
            for (int i = 0; i < n; i++) { dist[i] = double.PositiveInfinity; prevEdge[i] = -1; }
            dist[from] = 0;

            for (int iter = 0; iter < n; iter++)
            {
                int u = -1;
                for (int i = 0; i < n; i++)
                    if (!done[i] && !double.IsPositiveInfinity(dist[i]) && (u < 0 || dist[i] < dist[u])) u = i;
                if (u < 0 || u == to) break;
                done[u] = true;

                foreach (RoadEdge e in graph.EdgesAt(u))
                {
                    if (e.Blocked) continue;
                    int v = e.Other(u);
                    if (done[v]) continue;
                    double alt = dist[u] + e.CurrentMinutes;
                    if (alt < dist[v] - 1e-9)
                    {
                        dist[v] = alt;
                        prevEdge[v] = e.Id;
                    }
                }
            }

            if (double.IsPositiveInfinity(dist[to]))
                return RouteResult.Fail(from, to, "No open road connects these locations (road closure).");

            var nodes = new List<int>();
            var edges = new List<int>();
            double km = 0;
            int cur = to;
            nodes.Add(cur);
            while (cur != from)
            {
                RoadEdge e = graph.Edges[prevEdge[cur]];
                edges.Add(e.Id);
                km += e.LengthKm;
                cur = e.Other(cur);
                nodes.Add(cur);
            }
            nodes.Reverse();
            edges.Reverse();
            return RouteResult.Ok(from, to, nodes, edges, dist[to], km);
        }
    }
}
