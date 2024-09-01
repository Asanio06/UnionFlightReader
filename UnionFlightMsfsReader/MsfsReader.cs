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

        private const int CheckInterval_ms = 500;

        private EventWaitHandle eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
        private Thread? simconnectReceiveMessageThread = null;




        private CancellationTokenSource cancelationTokenSource = new CancellationTokenSource();

        public IFlight flight => throw new NotImplementedException();

        public IAircraft aircraft => Aircraft.Instance;

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

            DataRequestManager.Instance.manageDataRequest(simconnect);
        }

        private void SimConnect_MessageReceiveThreadHandler()
        {
            while (true)
            {
                eventHandle.WaitOne();

                try
                {
                    simconnect.ReceiveMessage();
                }
                catch
                {
                    // ignored
                }
            }
        }

        public void stop()
        {
            if (simconnect != null)
            {
                simconnect.Dispose();
                simconnect = null;
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
                    Console.WriteLine("Exception");
                }
            );
        }


        private MsfsReader()
        {
            try
            {
                simconnect = new SimConnect("UnionFlight", IntPtr.Zero, 0, eventHandle, 0);
                simconnectReceiveMessageThread = new Thread(new ThreadStart(SimConnect_MessageReceiveThreadHandler));
                simconnectReceiveMessageThread.IsBackground = true;
                simconnectReceiveMessageThread.Start();
                initSimConnectEssentialsEvents();
            }
            catch (COMException ex)
            {
                // TODO : Message d'erreur

            }
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