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


        public static float getKnotsFromMeterPerSeconds(float value)
        {
            return value * METER_PER_SECONDS_TO_KNOTS;
        }

        public static float getMeterPerSecondsFromKnots(float value)
        {
            return value * KNOTS_TO_METER_PER_SECONDS;
        }
    }
}