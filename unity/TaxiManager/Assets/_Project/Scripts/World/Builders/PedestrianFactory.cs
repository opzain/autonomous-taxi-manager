using TaxiSim.Core;
using UnityEngine;

namespace TaxiSim.World
{
    /// <summary>
    /// Low-poly Mumbai pedestrians for the Character shader: office workers, shirt-and-trousers,
    /// sarees, salwar kurtas, school kids and labourers. Every limb carries its id and pivot height in
    /// uv1 so the walk cycle runs on the GPU. People are about 1.6x real scale, like the vehicles.
    /// Facing +Z, feet at y = 0.
    /// </summary>
    public static class PedestrianFactory
    {
        public const float Hip = 0.135f;
        public const float Shoulder = 0.222f;
        public const int VariantCount = 16;

        private static readonly Color[] Skin =
        {
            new Color(0.55f, 0.38f, 0.27f), new Color(0.45f, 0.3f, 0.2f), new Color(0.62f, 0.45f, 0.33f),
            new Color(0.38f, 0.25f, 0.17f), new Color(0.68f, 0.5f, 0.38f)
        };

        private static readonly Color[] Shirts =
        {
            new Color(0.92f, 0.92f, 0.9f), new Color(0.55f, 0.7f, 0.9f), new Color(0.85f, 0.3f, 0.25f),
            new Color(0.25f, 0.35f, 0.6f), new Color(0.95f, 0.8f, 0.3f), new Color(0.35f, 0.6f, 0.4f),
            new Color(0.75f, 0.75f, 0.78f), new Color(0.6f, 0.3f, 0.55f), new Color(0.15f, 0.15f, 0.17f)
        };

        private static readonly Color[] Trousers =
        {
            new Color(0.12f, 0.13f, 0.18f), new Color(0.2f, 0.22f, 0.3f), new Color(0.35f, 0.3f, 0.25f),
            new Color(0.08f, 0.08f, 0.09f), new Color(0.45f, 0.45f, 0.48f)
        };

        private static readonly Color[] Sarees =
        {
            new Color(0.9f, 0.2f, 0.35f), new Color(0.95f, 0.6f, 0.1f), new Color(0.2f, 0.55f, 0.75f),
            new Color(0.55f, 0.15f, 0.5f), new Color(0.15f, 0.6f, 0.4f), new Color(0.95f, 0.85f, 0.4f),
            new Color(0.8f, 0.1f, 0.15f)
        };

        private static readonly Color Hair = new Color(0.04f, 0.035f, 0.03f);
        private static readonly Color Shoe = new Color(0.07f, 0.06f, 0.06f);

        /// <summary>One person mesh. Variant picks the outfit, seed the colours.</summary>
        public static MeshBuilder Build(int variant, ulong seed)
        {
            var rng = new Pcg32(seed * 977UL + (ulong)variant);
            var mb = new MeshBuilder();
            Color skin = rng.Pick(Skin);
            int outfit = variant % 6;
            float scale = outfit == 4 ? 0.72f : rng.Range(0.93f, 1.07f); // school kids are smaller
            Color shirt = outfit == 0 ? (rng.Chance(0.6) ? Shirts[0] : Shirts[1]) : rng.Pick(Shirts);
            Color legs = rng.Pick(Trousers);
            if (outfit == 4) { shirt = Shirts[0]; legs = new Color(0.15f, 0.2f, 0.4f); }

            // Legs (limbs 1, 2)
            for (int side = -1; side <= 1; side += 2)
            {
                int v0 = mb.VertexCount;
                mb.AddBox(new Vector3(side * 0.02f, Hip * 0.5f, 0), new Vector3(0.03f, Hip, 0.034f), legs);
                mb.AddBox(new Vector3(side * 0.02f, 0.008f, 0.008f), new Vector3(0.032f, 0.016f, 0.05f), Shoe);
                mb.OverrideUv1(v0, new Vector4(side < 0 ? 1f : 2f, Hip, 0, 0));
            }

            // Body (limb 0)
            int b0 = mb.VertexCount;
            switch (outfit)
            {
                case 2: // saree: draped skirt, blouse, pallu over the shoulder
                {
                    Color saree = rng.Pick(Sarees);
                    mb.AddCylinder(new Vector3(0, 0.012f, 0), 0.045f, 0.036f, Hip + 0.015f, 10, saree);
                    mb.AddBox(new Vector3(0, 0.18f, 0), new Vector3(0.07f, 0.095f, 0.04f), saree * 0.85f);
                    mb.AddBeam(new Vector3(-0.03f, 0.14f, 0.022f), new Vector3(0.036f, 0.225f, 0.0f), new Vector2(0.03f, 0.008f), saree);
                    break;
                }
                case 3: // salwar kurta: long tunic, dupatta
                {
                    Color kurta = rng.Pick(Sarees);
                    mb.AddBox(new Vector3(0, 0.16f, 0), new Vector3(0.074f, 0.14f, 0.042f), kurta);
                    mb.AddBeam(new Vector3(-0.04f, 0.215f, 0.0f), new Vector3(0.04f, 0.215f, 0.0f), new Vector2(0.026f, 0.012f), rng.Pick(Shirts));
                    break;
                }
                case 5: // labourer: vest and towel
                    mb.AddBox(new Vector3(0, 0.18f, 0), new Vector3(0.072f, 0.095f, 0.04f), new Color(0.82f, 0.8f, 0.74f));
                    mb.AddBeam(new Vector3(-0.03f, 0.225f, 0.0f), new Vector3(0.03f, 0.225f, 0.0f), new Vector2(0.02f, 0.012f), new Color(0.75f, 0.2f, 0.15f));
                    break;
                default: // shirt (office worker tucks in, with a belt)
                    mb.AddBox(new Vector3(0, 0.18f, 0), new Vector3(0.075f, 0.095f, 0.042f), shirt);
                    mb.AddBox(new Vector3(0, Hip + 0.004f, 0), new Vector3(0.072f, 0.008f, 0.04f), new Color(0.1f, 0.08f, 0.06f));
                    break;
            }
            // Neck, head, hair
            mb.AddBox(new Vector3(0, 0.231f, 0), new Vector3(0.018f, 0.02f, 0.018f), skin);
            mb.AddEllipsoid(new Vector3(0, 0.252f, 0), new Vector3(0.021f, 0.025f, 0.023f), 8, 6, skin, 0f);
            bool longHair = outfit == 2 || outfit == 3;
            mb.AddEllipsoid(new Vector3(0, 0.258f, -0.003f), new Vector3(0.023f, 0.021f, 0.025f), 8, 4, Hair, 0f, hemisphere: true);
            if (longHair) mb.AddBox(new Vector3(0, 0.235f, -0.022f), new Vector3(0.03f, 0.04f, 0.012f), Hair);
            // Backpack / office bag
            if (outfit == 0 || outfit == 4 || rng.Chance(0.2))
                mb.AddBox(new Vector3(0, 0.185f, -0.034f), new Vector3(0.052f, 0.065f, 0.024f), rng.Chance(0.5) ? new Color(0.1f, 0.1f, 0.12f) : rng.Pick(Shirts) * 0.6f);
            mb.OverrideUv1(b0, new Vector4(0f, 0f, 0, 0));

            // Arms (limbs 3, 4): sleeve + hand
            Color sleeve = outfit == 2 ? skin : outfit == 5 ? skin : outfit == 3 ? rng.Pick(Sarees) : shirt;
            for (int side = -1; side <= 1; side += 2)
            {
                int a0 = mb.VertexCount;
                mb.AddBox(new Vector3(side * 0.048f, 0.182f, 0), new Vector3(0.019f, 0.08f, 0.021f), sleeve);
                mb.AddBox(new Vector3(side * 0.048f, 0.134f, 0), new Vector3(0.017f, 0.02f, 0.019f), skin);
                mb.OverrideUv1(a0, new Vector4(side < 0 ? 3f : 4f, Shoulder, 0, 0));
            }

            if (Mathf.Abs(scale - 1f) > 0.001f)
            {
                var scaled = new MeshBuilder();
                scaled.Append(mb, Matrix4x4.Scale(Vector3.one * scale), Color.white);
                // Pivots scale with the body
                return Rescaled(scaled, scale);
            }
            return mb;
        }

        // Append() keeps uv1 unchanged; pivot heights (uv1.y) must follow the scale.
        private static MeshBuilder Rescaled(MeshBuilder mb, float scale)
        {
            mb.ScaleUv1Y(scale);
            return mb;
        }
    }
}
