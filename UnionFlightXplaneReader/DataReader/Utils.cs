using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnionFlightXplaneReader.DataReader
{
    public class Utils
    {
        private static float METER_PER_SECONDS_TO_KNOTS = 1.943844f;
        private static float KNOTS_TO_METER_PER_SECONDS = 0.514444f;
        private static double FEET_TO_METER = 0.3048;
        private static double METER_TO_FEET = 3.280839895;


        public static float getKnotsFromMeterPerSeconds(float value)
        {
            return value * METER_PER_SECONDS_TO_KNOTS;
        }

        public static float getMeterPerSecondsFromKnots(float value)
        {
            return value * KNOTS_TO_METER_PER_SECONDS;
        }

        public static double getMeterFromFeet(double value)
        {
            return value * FEET_TO_METER;
        }
        public static double getFeetFromMeter(double value)
        {
            return value * METER_TO_FEET;
        }
    }
}