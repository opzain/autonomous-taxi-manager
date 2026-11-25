using System.Linq;
using Xunit;

namespace TaxiSim.Core.Tests
{
    public class CityMapTests
    {
        private static CityMap Map() => BkcMap.Create();

        [Fact]
        public void Map_MeetsSpecMinimums()
        {
            CityMap map = Map();
            Assert.True(map.Graph.Nodes.Count >= 10, "Spec §3: at least 10–15 locations");
            Assert.Equal(23, map.Graph.Nodes.Count);
            Assert.Equal(38, map.Graph.Edges.Count);
            Assert.True(map.Graph.Nodes.Count(n => n.IsChargingStation) >= 2);
        }

        [Fact]
        public void Corridor_BkcToKurla_RunsThroughEquinoxAtlas()
        {
            CityGraph g = Map().Graph;
            int equinox = g.Node("equinox").Id;
            Assert.NotNull(g.FindEdge(g.Node("us_consulate").Id, equinox));
            Assert.NotNull(g.FindEdge(equinox, g.Node("kurla_stn").Id));
            Assert.NotNull(g.FindEdge(g.Node("phoenix").Id, equinox));
            Assert.NotNull(g.FindEdge(equinox, g.Node("kohinoor").Id));
            RouteResult r = RouteFinder.FindFastest(g, g.Node("us_consulate").Id, g.Node("kurla_stn").Id);
            Assert.True(r.Success);
            Assert.Contains(equinox, r.Nodes);
        }

        [Fact]
        public void FleetHub_IsAChargingDepotConnectedToTheNetwork()
        {
            CityGraph g = Map().Graph;
            CityNode hub = g.Node("fleet_hub");
            Assert.Equal(ZoneType.FleetDepot, hub.Zone);
            Assert.True(hub.IsChargingStation);
            Assert.True(g.EdgesAt(hub.Id).Count >= 2);
            Assert.True(RouteFinder.FindFastest(g, hub.Id, g.Node("kurla_stn").Id).Success);
        }

        [Fact]
        public void Graph_IsConnected()
        {
            Assert.True(Map().Graph.IsConnected());
        }

        [Fact]
        public void Roads_HavePositiveLengthAndTime()
        {
            foreach (RoadEdge e in Map().Graph.Edges)
            {
                Assert.True(e.LengthKm > 0.1, $"{e} too short");
                Assert.True(e.BaseMinutes > 0, $"{e} has no travel time");
                Assert.Equal(1.0, e.TrafficMultiplier);
                Assert.False(e.Blocked);
            }
        }

        [Fact]
        public void Flyovers_AreMarkedElevated()
        {
            CityGraph g = Map().Graph;
            RoadEdge connector = g.FindEdge(g.Node("ev_depot").Id, g.Node("chunabhatti").Id);
            Assert.NotNull(connector);
            Assert.True(connector.Elevated);
        }

        [Fact]
        public void Geography_RiverIsWater_GBlockIsLand_SeaIsWater()
        {
            CityGeography geo = Map().Geography;
            Vec2 onRiver = geo.River[4];
            Assert.True(geo.IsWater(onRiver.X, onRiver.Y), "river centre should be under water");
            Assert.False(geo.IsWater(175, 150), "G-Block should be dry land");
            Assert.True(geo.IsWater(-320, 100), "Arabian Sea west of Bandra should be water");
        }

        [Fact]
        public void Locations_AreOnLand()
        {
            CityMap map = Map();
            foreach (CityNode n in map.Graph.Nodes)
                Assert.False(map.Geography.IsWater(n.Position.X, n.Position.Z), $"{n.Name} is in the water");
        }

        [Fact]
        public void Rng_IsDeterministic()
        {
            var a = new Pcg32(42);
            var b = new Pcg32(42);
            for (int i = 0; i < 1000; i++) Assert.Equal(a.NextUInt(), b.NextUInt());
        }

        [Fact]
        public void Rng_RangesStayInBounds()
        {
            var r = new Pcg32(7);
            for (int i = 0; i < 10000; i++)
            {
                int v = r.Range(3, 9);
                Assert.InRange(v, 3, 8);
                double d = r.NextDouble();
                Assert.InRange(d, 0.0, 0.9999999999);
            }
        }
    }
}
