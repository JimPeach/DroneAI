using System;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {
        public class PIDController
        {
            public struct Parameters
            {
                public double Kp; // Proportional gain
                public double Ki; // Integral gain
                public double Kd; // Differential gian
            }

            readonly protected Parameters parameters;
            readonly protected double dt, invDt;

            Vector3D integralState = Vector3D.Zero;
            Vector3D e_tk1 = Vector3D.Zero;

            public PIDController(Parameters parameters_, double stepsPerSecond = TickRate)
            {
                invDt = stepsPerSecond;
                dt = 1 / invDt;
                parameters = parameters_;
                Reset();
            }

            public double Filter(double e_tk0, bool resetIntegral = false)
            {
                if (resetIntegral)
                    integralState.X = 0.0;
                else
                    integralState.X += e_tk0 * dt;

                double de = (e_tk0 - e_tk1.X) * invDt;
                e_tk1.X = e_tk0;

                return parameters.Kp * e_tk0 + parameters.Ki * integralState.X + parameters.Kd * de;
            }

            public Vector3D Filter(Vector3D e_tk0, bool resetIntegral = false)
            {
                if (resetIntegral)
                    integralState = Vector3D.Zero;
                else
                    integralState += e_tk0 * dt;

                Vector3D de = (e_tk0 - e_tk1) * invDt;
                e_tk1 = e_tk0;

                return parameters.Kp * e_tk0 + parameters.Ki * integralState + parameters.Kd * de;
            }

            public double IntegralState { get { return integralState.X; } }
            public Vector3D IntegralStateV { get { return integralState; } }

            public void Reset()
            {
                integralState = Vector3D.Zero;
                e_tk1 = Vector3D.Zero;
            }
        }
    }
}
