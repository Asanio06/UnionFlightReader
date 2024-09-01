using Microsoft.FlightSimulator.SimConnect;

namespace UnionFlightMsfsReader.EventHandler
{

    enum EVENTS
    {
        PAUSE_ON,
        PAUSE_OFF
    };

    enum NOTIFICATION_GROUPS
    {
        GROUP0,
    }

    internal class EventManager
    {


        public void manageSimulatorEvents(SimConnect simconnect)
        {
            simconnect.OnRecvEvent += new SimConnect.RecvEventEventHandler(handleOnReceiveEvent);

            registerSimulatorEvents(simconnect);
        }

        private void registerSimulatorEvents(SimConnect simconnect)
        {
            simconnect.MapClientEventToSimEvent(EVENTS.PAUSE_ON, "PAUSE_ON");
            simconnect.AddClientEventToNotificationGroup(NOTIFICATION_GROUPS.GROUP0, EVENTS.PAUSE_ON, false);
            simconnect.MapClientEventToSimEvent(EVENTS.PAUSE_OFF, "PAUSE_OFF");
            simconnect.AddClientEventToNotificationGroup(NOTIFICATION_GROUPS.GROUP0, EVENTS.PAUSE_OFF, false);

            // set the group priority
            simconnect.SetNotificationGroupPriority(NOTIFICATION_GROUPS.GROUP0, SimConnect.SIMCONNECT_GROUP_PRIORITY_HIGHEST);
        }



        private void handleOnReceiveEvent(SimConnect sender, SIMCONNECT_RECV_EVENT recEvent)
        {
            switch (recEvent.uEventID)
            {
                case (uint)EVENTS.PAUSE_OFF:

                    Console.WriteLine("PAUSE OFF");
                    break;
                case (uint)EVENTS.PAUSE_ON:

                    Console.WriteLine("PAUSE_ON");
                    break;
            }
        }


        private EventManager()
        {

        }


        public static EventManager Instance
        {
            get { return Nested.instance; }
        }


        private class Nested
        {

            static Nested()
            {
            }

            internal static readonly EventManager instance = new();
        }
    }
}

