using FlightDataClass;
using Microsoft.FlightSimulator.SimConnect;
using System.Runtime.InteropServices;
using UnionFlight;
using UnionFlight.FlightData;

namespace UnionFlightMsfsReader
{
    public class MsfsReader : IFlightReader
    {
        private bool? isSimLaunched;

        SimConnect? simconnect = null;

        private const int CheckInterval_ms = 2000;


        private CancellationTokenSource cancelationTokenSource = new CancellationTokenSource();

        public IFlight flight => throw new NotImplementedException();

        public IAircraft aircraft => throw new NotImplementedException();

        public ISim simulator => throw new NotImplementedException();

        public bool IsSimLaunched()
        {

            if (simconnect == null) return false;
            if (isSimLaunched == null)
            {
                simconnect.ReceiveMessage();
                return false;
            }

            return (bool)isSimLaunched;
        }

        public void run()
        {
            if (simconnect == null)
            {
                // TODO: Log
                return;
            }

            var token = cancelationTokenSource.Token;

            Task.Factory.StartNew(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    simconnect.ReceiveMessage();
                    await Task.Delay(CheckInterval_ms).ConfigureAwait(false);
                }
            }, token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public void stop()
        {
            if (simconnect != null)
            {
                simconnect.Dispose();
                simconnect = null;
            }
        }


        private MsfsReader()
        {
            try
            {
                simconnect = new SimConnect("UnionFlight", IntPtr.Zero, 0, null, 0);
                initSimConnectEssentialsEvents();
            }
            catch (COMException ex)
            {
                // TODO : Message d'erreur

            }
        }


        private void initSimConnectEssentialsEvents()
        {
            if (simconnect == null)
            {
                return;
            }


            simconnect.OnRecvOpen += new SimConnect.RecvOpenEventHandler((sender, data) =>
                {
                    this.isSimLaunched = true;
                }
            );
            simconnect.OnRecvQuit += new SimConnect.RecvQuitEventHandler((sender, data) =>
                {
                    this.isSimLaunched = false;
                }
            );
            simconnect.OnRecvException += new SimConnect.RecvExceptionEventHandler((sender, data) =>
                {
                    // TODO: Logging
                }
            );
        }


        public static MsfsReader Instance
        {
            get { return Nested.instance; }
        }


        private class Nested
        {

            static Nested()
            {
            }

            internal static readonly MsfsReader instance = new();
        }
    }
}