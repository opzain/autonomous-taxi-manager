using System;
using System.Collections.Generic;

namespace TaxiSim.Core
{
    /// <summary>
    /// PCG32 (XSH-RR) random number generator. Used everywhere instead of UnityEngine.Random or
    /// System.Random so that the same seed always produces the same city, demand and events,
    /// on every machine and every .NET runtime.
    /// </summary>
    public sealed class Pcg32
    {
        private ulong _state;
        private readonly ulong _inc;

        public Pcg32(ulong seed, ulong sequence = 54UL)
        {
            _state = 0UL;
            _inc = (sequence << 1) | 1UL;
            NextUInt();
            _state = unchecked(_state + seed);
            NextUInt();
        }

        /// <summary>Internal state (for save-game state hashes).</summary>
        public ulong State => _state;

        public uint NextUInt()
        {
            ulong old = _state;
            _state = unchecked(old * 6364136223846793005UL + _inc);
            uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorShifted >> rot) | (xorShifted << ((-rot) & 31));
        }

        /// <summary>Uniform double in [0, 1).</summary>
        public double NextDouble() => NextUInt() / 4294967296.0;

        public float NextFloat() => (float)NextDouble();

        public double Range(double min, double max) => min + (max - min) * NextDouble();

        public float Range(float min, float max) => (float)(min + (max - min) * NextDouble());

        /// <summary>Integer in [minInclusive, maxExclusive).</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return minInclusive + (int)(NextDouble() * (maxExclusive - minInclusive));
        }

        public bool Chance(double probability) => NextDouble() < probability;

        public T Pick<T>(IReadOnlyList<T> items)
        {
            if (items == null || items.Count == 0) throw new ArgumentException("Cannot pick from an empty list.");
            return items[Range(0, items.Count)];
        }
    }
}
