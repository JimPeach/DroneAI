using System;

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

            readonly Parameters parameters;
            readonly double dt, invDt;

            double integralState = 0.0;
            double e_tk1 = 0.0;

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
                    integralState = 0.0;
                else
                    integralState += e_tk0 * dt;

                double de = (e_tk0 - e_tk1) * invDt;
                e_tk1 = e_tk0;

                return parameters.Kp * e_tk0 + parameters.Ki * integralState + parameters.Kd * de;
            }

            public double IntegralState { get { return integralState; } }

            public void Reset()
            {
                integralState = 0.0;
            }
        }
    }
}
