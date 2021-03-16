namespace IngameScript
{
    partial class Program
    {
        public class PIDController
        {
            public struct Parameters
            {
                public double Kp;
                public double Ki;
                public double Kd;
            }

            //public struct ZNTuningParameters
            //{
            //    public enum TuningType { Classic, PessenIntegral, Overshoot };

            //    public double Ku = 0.0;
            //    public double Tu = 0.0;
            //    public TuningType Tuning = TuningType.Classic;

            //    public Parameters Paramters {
            //        get {
            //            switch (Tuning)
            //            {
            //                case TuningType.Classic:
            //                    return new Parameters()
            //                    {
            //                        Kp = 0.6 * Ku,
            //                        Ki = 1.2 * Ku / Tu,
            //                        Kd = 0.075 * Ku * Tu
            //                    };

            //                case TuningType.PessenIntegral:
            //                    return new Parameters()
            //                    {
            //                        Kp = 0.7 * Ku,
            //                        Ki = 1.75 * Ku / Tu,
            //                        Kd = 0.105 * Ku * Tu
            //                    };

            //                case TuningType.Overshoot:
            //                    return new Parameters()
            //                    {
            //                        Kp = 0.33 * Ku,
            //                        Ki = 0.66 * Ku / Tu,
            //                        Kd = 0.11 * Ku * Tu
            //                    };

            //                default:
            //                    return new Parameters();
            //            }
            //        }
            //    }
            //}

            double e_tk0_coeff;
            double e_tk1_coeff;
            double e_tk2_coeff;

            double u_tk1;
            double e_tk1, e_tk2;


            public PIDController(Parameters parameters, double stepsPerSecond = 60.0)
            {
                double invDt = stepsPerSecond;

                e_tk0_coeff = parameters.Kp + parameters.Ki / invDt + parameters.Kd * invDt;
                e_tk1_coeff = -parameters.Kp - 2.0 * parameters.Kd * invDt;
                e_tk2_coeff = parameters.Kd * invDt;

                Reset();
            }

            public double Filter(double e_tk0)
            {
                double u_tk0 = u_tk1 + e_tk0_coeff * e_tk0 + e_tk1_coeff * e_tk1 + e_tk2_coeff * e_tk2;

                u_tk1 = u_tk0;
                e_tk2 = e_tk1;
                e_tk1 = e_tk0;

                return u_tk0;
            }

            public void Reset()
            {
                u_tk1 = 0.0;
                e_tk1 = 0.0;
                e_tk2 = 0.0;
            }
        }
    }
}
