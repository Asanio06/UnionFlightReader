using Microsoft.FlightSimulator.SimConnect;

namespace UnionFlightMsfsReader
{

    // Class mère
    // Responsabilité : Retourne de manière statique un elément de l'énum DATAREQUEST
    // Contient une fonction qui défini la struct cible
    // Contient une fonction qui cast la cible
    // Prend en entrer simconnect + possède une fonction qui fait le Register
    internal class DataRequestHandler
    {

        public string SimVariableName { get; }
        public string? UnitsName { get; }

        public SIMCONNECT_DATATYPE SimconnectDataType { get; }
        public SIMCONNECT_SIMOBJECT_TYPE SimObjectType { get; }


        private readonly DataReader variableToUpdate;


        public DataRequestHandler(DataReader variableToUpdate, string SimVariableName, string? UnitsName, SIMCONNECT_DATATYPE SimconnectDataType, SIMCONNECT_SIMOBJECT_TYPE SimObjectType)
        {
            this.SimVariableName = SimVariableName;
            this.UnitsName = UnitsName;
            this.SimconnectDataType = SimconnectDataType;
            this.SimObjectType = SimObjectType;
            this.variableToUpdate = variableToUpdate;
        }


        public void UpdateValue(dynamic value)
        {
            variableToUpdate.UpdateValue(value);
        }

        public bool IsStringDataRequest()
        {
            return variableToUpdate.GetType() == typeof(StringDataReader);
        }




    }
}
