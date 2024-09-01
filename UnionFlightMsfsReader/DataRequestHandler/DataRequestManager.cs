using Microsoft.FlightSimulator.SimConnect;
using System.Runtime.InteropServices;

namespace UnionFlightMsfsReader
{
    internal class DataRequestManager
    {


        enum DEFINITIONS
        {
            FAKE,
        }

        enum DATA_REQUESTS
        {
            FAKE,
        };


        // String properties must be packed inside of a struct
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        struct FakeStructToSupportStringDataRequest
        {
            // this is how you declare a fixed size string
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public String sValue;
        };


        private CancellationTokenSource cancelationTokenSource;

        private const int CheckInterval_ms = 1000;



        public void manageDataRequest(SimConnect simconnect)
        {
            simconnect.OnRecvSimobjectDataBytype += new SimConnect.RecvSimobjectDataBytypeEventHandler(onReceiveSimObjectData);

            registerFields(simconnect);

            var token = cancelationTokenSource.Token;
            Task.Factory.StartNew(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    requestFields(simconnect);

                    await Task.Delay(CheckInterval_ms).ConfigureAwait(false);
                }
            }, token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        }

        private void requestFields(SimConnect simconnect)
        {

            foreach (var (index, handler) in DataRequestHandlerRegistry.DataRequestHandlerByIndex)
            {
                simconnect.RequestDataOnSimObjectType((DATA_REQUESTS)index, (DEFINITIONS)index, 0, handler.SimObjectType);
            }

        }



        private void onReceiveSimObjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA_BYTYPE data)
        {
            var requestId = data.dwRequestID;

            var requestHandler = DataRequestHandlerRegistry.DataRequestHandlerByIndex[requestId];

            var requestedData = data.dwData[0];

            if (requestHandler.IsStringDataRequest())
            {
                var stringValueContainer = (FakeStructToSupportStringDataRequest)requestedData;
                requestHandler.UpdateValue(stringValueContainer.sValue);
                return;
            }

            requestHandler.UpdateValue(requestedData);
        }


        private void registerFields(SimConnect simconnect)
        {
            foreach (var (index, handler) in DataRequestHandlerRegistry.DataRequestHandlerByIndex)
            {

                simconnect.AddToDataDefinition((DEFINITIONS)index, handler.SimVariableName, handler.UnitsName, handler.SimconnectDataType, 0.0f, SimConnect.SIMCONNECT_UNUSED);

                if (handler.IsStringDataRequest())
                {

                    simconnect.RegisterDataDefineStruct<FakeStructToSupportStringDataRequest>((DEFINITIONS)index);
                    continue;
                }

                simconnect.RegisterDataDefineStruct<double>((DEFINITIONS)index);
            }
        }


        private DataRequestManager()
        {
            cancelationTokenSource = new CancellationTokenSource();
        }

        public static DataRequestManager Instance
        {
            get { return Nested.instance; }
        }

        private class Nested
        {
            // Explicit static constructor to tell C# compiler
            // not to mark type as beforefieldinit
            static Nested()
            {
            }

            internal static readonly DataRequestManager instance = new();
        }
    }
}
