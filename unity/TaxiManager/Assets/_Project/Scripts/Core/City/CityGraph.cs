using System;
using System.Collections.Generic;

namespace TaxiSim.Core
{
    public sealed class CityNode
    {
        public int Id { get; }
        public string Key { get; }
        public string Name { get; }
        public string ShortName { get; }
        public ZoneType Zone { get; }
        public Vec3 Position { get; }
        /// <summary>Number of charging bays; 0 means this is not a charging station.</summary>
        public int ChargerSlots { get; }
        public bool IsChargingStation => ChargerSlots > 0;

        public CityNode(int id, string key, string name, string shortName, ZoneType zone, Vec3 position, int chargerSlots = 0)
        {
            Id = id;
            Key = key;
            Name = name;
            ShortName = shortName;
            Zone = zone;
            Position = position;
            ChargerSlots = chargerSlots;
        }

        public override string ToString() => $"{Id}:{ShortName}";
    }

    public sealed class RoadEdge
    {
        public int Id { get; }
        public int A { get; }
        public int B { get; }
        public string Name { get; }
        public RoadClass Class { get; }
        /// <summary>True for flyovers / elevated expressways (drawn on a deck, z-axis in the 3D world).</summary>
        public bool Elevated { get; }
        public double LengthKm { get; }
        /// <summary>Free-flow travel time in minutes.</summary>
        public double BaseMinutes { get; }

        /// <summary>1.0 = free flow, 3.0 = gridlock. Changed by traffic events and time of day.</summary>
        public double TrafficMultiplier { get; set; } = 1.0;
        /// <summary>Road closure. Blocked edges are skipped by the route finder.</summary>
        public bool Blocked { get; set; }

        public double CurrentMinutes => BaseMinutes * TrafficMultiplier;

        public RoadEdge(int id, int a, int b, string name, RoadClass roadClass, bool elevated, double lengthKm)
        {
            Id = id;
            A = a;
            B = b;
            Name = name;
            Class = roadClass;
            Elevated = elevated;
            LengthKm = lengthKm;
            // Time = distance / speed, plus a small fixed junction delay.
            BaseMinutes = lengthKm / RoadClassInfo.SpeedKmh(roadClass) * 60.0 + 0.3;
        }

        public bool Touches(int node) => A == node || B == node;
        public int Other(int node) => node == A ? B : A;
        public override string ToString() => $"E{Id}:{A}-{B} {Name}";
    }

    /// <summary>City road network: intersections/locations are nodes, roads are undirected edges.</summary>
    public sealed class CityGraph
    {
        /// <summary>World scale: 1 world unit = 10 metres.</summary>
        public const double KmPerUnit = 0.01;

        private readonly List<CityNode> _nodes = new List<CityNode>();
        private readonly List<RoadEdge> _edges = new List<RoadEdge>();
        private readonly List<List<RoadEdge>> _adjacency = new List<List<RoadEdge>>();
        private readonly Dictionary<string, CityNode> _byKey = new Dictionary<string, CityNode>();

        public IReadOnlyList<CityNode> Nodes => _nodes;
        public IReadOnlyList<RoadEdge> Edges => _edges;

        public CityNode AddNode(string key, string name, string shortName, ZoneType zone, double x, double z, int chargerSlots = 0)
        {
            if (_byKey.ContainsKey(key)) throw new ArgumentException($"Duplicate node key '{key}'.");
            var node = new CityNode(_nodes.Count, key, name, shortName, zone, new Vec3(x, 0, z), chargerSlots);
            _nodes.Add(node);
            _adjacency.Add(new List<RoadEdge>());
            _byKey.Add(key, node);
            return node;
        }

        public RoadEdge AddRoad(string fromKey, string toKey, string name, RoadClass roadClass, bool elevated = false)
        {
            CityNode a = Node(fromKey);
            CityNode b = Node(toKey);
            if (a.Id == b.Id) throw new ArgumentException("A road cannot start and end at the same node.");
            if (FindEdge(a.Id, b.Id) != null) throw new ArgumentException($"Duplicate road {fromKey}-{toKey}.");
            double km = Vec3.DistanceXZ(a.Position, b.Position) * KmPerUnit;
            var edge = new RoadEdge(_edges.Count, a.Id, b.Id, name, roadClass, elevated, km);
            _edges.Add(edge);
            _adjacency[a.Id].Add(edge);
            _adjacency[b.Id].Add(edge);
            return edge;
        }

        public CityNode Node(string key)
        {
            if (!_byKey.TryGetValue(key, out CityNode node)) throw new KeyNotFoundException($"Unknown node key '{key}'.");
            return node;
        }

        public CityNode Node(int id) => _nodes[id];

        public IReadOnlyList<RoadEdge> EdgesAt(int nodeId) => _adjacency[nodeId];

        public RoadEdge FindEdge(int a, int b)
        {
            foreach (RoadEdge e in _adjacency[a])
                if (e.Other(a) == b) return e;
            return null;
        }

        /// <summary>True if every node can reach every other node (ignoring closures).</summary>
        public bool IsConnected()
        {
            if (_nodes.Count == 0) return true;
            var seen = new bool[_nodes.Count];
            var stack = new Stack<int>();
            stack.Push(0);
            seen[0] = true;
            int count = 1;
            while (stack.Count > 0)
            {
                int u = stack.Pop();
                foreach (RoadEdge e in _adjacency[u])
                {
                    int v = e.Other(u);
                    if (seen[v]) continue;
                    seen[v] = true;
                    count++;
                    stack.Push(v);
                }
            }
            return count == _nodes.Count;
        }
    }
}
