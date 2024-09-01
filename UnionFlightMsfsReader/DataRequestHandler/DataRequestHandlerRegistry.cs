using Microsoft.FlightSimulator.SimConnect;
using System.Collections.Immutable;

namespace UnionFlightMsfsReader
{
    internal class DataRequestHandlerRegistry
    {

        private static uint dataRequestHandlerIndex = 0;

        private static ImmutableList<DataRequestHandler> defaultDataRequestHandlers = [
            new DataRequestHandler(AircraftReader.Instance.Altitude,"INDICATED ALTITUDE CALIBRATED", "feet", SIMCONNECT_DATATYPE.FLOAT64,SIMCONNECT_SIMOBJECT_TYPE.USER),
            new DataRequestHandler(AircraftReader.Instance.Name,"TITLE", null, SIMCONNECT_DATATYPE.STRING128,SIMCONNECT_SIMOBJECT_TYPE.USER),
            new DataRequestHandler(AircraftReader.Instance.TailNumber,"ATC ID", null, SIMCONNECT_DATATYPE.STRING128,SIMCONNECT_SIMOBJECT_TYPE.USER),
            new DataRequestHandler(AircraftReader.Instance.IndicatedAirspeed,"AIRSPEED INDICATED", "Knots", SIMCONNECT_DATATYPE.FLOAT64,SIMCONNECT_SIMOBJECT_TYPE.USER),
            new DataRequestHandler(AircraftReader.Instance.TrueAirspeed,"AIRSPEED TRUE", "Knots", SIMCONNECT_DATATYPE.FLOAT64,SIMCONNECT_SIMOBJECT_TYPE.USER),
            new DataRequestHandler(AircraftReader.Instance.Groundspeed,"GROUND VELOCITY", "Knots", SIMCONNECT_DATATYPE.FLOAT64,SIMCONNECT_SIMOBJECT_TYPE.USER),
            new DataRequestHandler(AircraftReader.Instance.VerticalSpeed,"VERTICAL SPEED", "ft/min", SIMCONNECT_DATATYPE.FLOAT64,SIMCONNECT_SIMOBJECT_TYPE.USER),
            new DataRequestHandler(AircraftReader.Instance.Heading,"PLANE HEADING DEGREES GYRO", "degrees", SIMCONNECT_DATATYPE.FLOAT64,SIMCONNECT_SIMOBJECT_TYPE.USER),
            ];


        private static Dictionary<uint, DataRequestHandler> dataRequestHandlerByIndex = new();

        static DataRequestHandlerRegistry()
        {
            defaultDataRequestHandlers.ForEach(dataRequestHandler =>
                {
                    dataRequestHandlerByIndex.Add(dataRequestHandlerIndex++, dataRequestHandler);
                }
            );
        }


        public static Dictionary<uint, DataRequestHandler> DataRequestHandlerByIndex => dataRequestHandlerByIndex;


    }
}
