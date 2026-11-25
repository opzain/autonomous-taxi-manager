using System.Linq;
using Xunit;

namespace TaxiSim.Core.Tests
{
    /// <summary>Spec tests T07 (route validity) and T08 (blocked road → alternative or failure).</summary>
    public class RouteFinderTests
    {
        [Fact]
        public void T07_RouteIsValidChainOfRoads()
        {
            CityGraph g = BkcMap.Create().Graph;
            int from = g.Node("bandra_stn").Id, to = g.Node("kurla_stn").Id;
            RouteResult r = RouteFinder.FindFastest(g, from, to);

            Assert.True(r.Success);
            Assert.Equal(from, r.Nodes.First());
            Assert.Equal(to, r.Nodes.Last());
            Assert.Equal(r.Nodes.Count - 1, r.Edges.Count);
            for (int i = 0; i < r.Edges.Count; i++)
            {
                RoadEdge e = g.Edges[r.Edges[i]];
                Assert.True(e.Touches(r.Nodes[i]) && e.Touches(r.Nodes[i + 1]), $"edge {e} does not join consecutive nodes");
            }
            double minutes = r.Edges.Sum(id => g.Edges[id].CurrentMinutes);
            Assert.Equal(minutes, r.Minutes, 6);
            Assert.Equal(r.Edges.Sum(id => g.Edges[id].LengthKm), r.Km, 6);
        }

        [Fact]
        public void SameNode_IsZeroLengthRoute()
        {
            CityGraph g = BkcMap.Create().Graph;
            RouteResult r = RouteFinder.FindFastest(g, 3, 3);
            Assert.True(r.Success);
            Assert.Empty(r.Edges);
            Assert.Equal(0, r.Minutes);
        }

        [Fact]
        public void T08_BlockedRoad_FindsAlternative()
        {
            CityGraph g = BkcMap.Create().Graph;
            int from = g.Node("west_gate").Id, to = g.Node("trident").Id;
            RouteResult first = RouteFinder.FindFastest(g, from, to);
            Assert.True(first.Success);

            RoadEdge closed = g.Edges[first.Edges[0]];
            closed.Blocked = true;
            RouteResult second = RouteFinder.FindFastest(g, from, to);

            Assert.True(second.Success);
            Assert.DoesNotContain(closed.Id, second.Edges);
            Assert.True(second.Minutes >= first.Minutes - 1e-9, "detour cannot be faster than the original optimum");
        }

        [Fact]
        public void T08_IsolatedLocation_ReportsFailure()
        {
            CityGraph g = BkcMap.Create().Graph;
            CityNode target = g.Node("chunabhatti");
            foreach (RoadEdge e in g.EdgesAt(target.Id)) e.Blocked = true;

            RouteResult r = RouteFinder.FindFastest(g, g.Node("nse").Id, target.Id);
            Assert.False(r.Success);
            Assert.False(string.IsNullOrEmpty(r.FailureReason));
        }

        [Fact]
        public void HeavyTraffic_ChangesTheFastestRoute()
        {
            CityGraph g = BkcMap.Create().Graph;
            int from = g.Node("west_gate").Id, to = g.Node("trident").Id;
            RouteResult before = RouteFinder.FindFastest(g, from, to);
            foreach (int id in before.Edges) g.Edges[id].TrafficMultiplier = 3.0;
            RouteResult after = RouteFinder.FindFastest(g, from, to);

            Assert.True(after.Success);
            Assert.True(after.Minutes <= before.Edges.Sum(id => g.Edges[id].CurrentMinutes) + 1e-9);
        }

        [Fact]
        public void Routing_IsDeterministic()
        {
            CityGraph g1 = BkcMap.Create().Graph, g2 = BkcMap.Create().Graph;
            for (int a = 0; a < g1.Nodes.Count; a++)
            for (int b = 0; b < g1.Nodes.Count; b++)
            {
                RouteResult r1 = RouteFinder.FindFastest(g1, a, b), r2 = RouteFinder.FindFastest(g2, a, b);
                Assert.Equal(r1.Edges, r2.Edges);
            }
        }

        [Fact]
        public void AllPairs_AreReachable_WhenNoClosures()
        {
            CityGraph g = BkcMap.Create().Graph;
            for (int a = 0; a < g.Nodes.Count; a++)
            for (int b = 0; b < g.Nodes.Count; b++)
                Assert.True(RouteFinder.FindFastest(g, a, b).Success, $"{a}->{b}");
        }
    }
}
