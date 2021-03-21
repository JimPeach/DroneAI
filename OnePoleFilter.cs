using System;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {
        public struct OnePoleFilterD
        {
            readonly double b;
            double y;

            public OnePoleFilterD(double b_)
            {
                y = 0.0;
                b = b_;
            }

            public void Reset()
            {
                y = 0.0;
            }

            public double Filter(double x)
            {
                y += b * (x - y);
                return y;
            }

            public static double LPFCoeff(double f, double rate)
            {
                return 1 - Math.Exp(-2.0 * Math.PI * f / rate);
            }
        }

        // We can't use generics for this since C# generics don't work with operators :(
        public struct OnePoleFilterV
        {
            readonly float b;
            Vector3 y;

            public OnePoleFilterV(float b_)
            {
                y = Vector3.Zero;
                b = b_;
            }

            public void Reset()
            {
                y = Vector3.Zero;
            }

            public Vector3 Filter(Vector3D x)
            {
                y += b * (x - y);
                return y;
            }
        }
    }
}
