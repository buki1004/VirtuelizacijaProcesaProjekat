using System;
using System.ServiceModel;
using System.Runtime.Serialization;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    [ServiceContract]
    public interface IBatteryService
    {
        [OperationContract]
        [FaultContract(typeof(DataFormatFault))]
        void StartSession(EisMeta meta);

        [OperationContract]
        [FaultContract(typeof(DataFormatFault))]
        [FaultContract(typeof(ValidationFault))]
        void PushSample(EisSample sample);

        [OperationContract]
        [FaultContract(typeof(DataFormatFault))]
        void EndSession(EisMeta meta);
    }

    [DataContract]
    public class EisMeta
    {
        [DataMember]
        public string SessionId { get; set; }

        [DataMember]
        public string BatteryId { get; set; }

        [DataMember]
        public string TestId { get; set; }

        [DataMember]
        public int SoC { get; set; }
    }

    [DataContract]
    public class EisSample
    {
        [DataMember]
        public string SessionId { get; set; } 

        [DataMember]
        public int RowIndex { get; set; }

        [DataMember]
        public double FrequencyHz { get; set; }

        [DataMember]
        public double R_ohm { get; set; }

        [DataMember]
        public double X_ohm { get; set; }

        [DataMember]
        public double T_degC { get; set; }

        [DataMember]
        public double Range_ohm { get; set; }

        [DataMember]
        public DateTime TimestampLocal { get; set; }
    }

    [DataContract]
    public class DataFormatFault
    {
        [DataMember] public string Message { get; set; }
    }

    [DataContract]
    public class ValidationFault
    {
        [DataMember] public string Message { get; set; }
    }
}
