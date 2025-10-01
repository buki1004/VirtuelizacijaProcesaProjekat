using Common;
using Service;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace Client
{
    class Program
    {
        static void Main(string[] args)
        {
            var factory = new ChannelFactory<IBatteryService>("BatteryServiceEndpoint");
            IBatteryService client = factory.CreateChannel();

            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Hioki");
            var csvFiles = Directory.GetFiles(folder, "*.csv");

            foreach (var filePath in csvFiles)
            {
                string sessionId = Guid.NewGuid().ToString();
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                var parts = fileName.Split('_');

                string batteryId = parts[1];
                string testId = parts[2] + "_" + parts[3];
                int soc = int.Parse(parts[3]);

                var meta = new EisMeta
                {
                    SessionId = sessionId,
                    BatteryId = batteryId,
                    TestId = testId,
                    SoC = soc
                };

                try
                {
                    client.StartSession(meta);
                    Console.WriteLine($"Started session {sessionId} for {fileName}");

                    using (var reader = new CsvReaderWrapper(filePath))
                    {
                        reader.ReadLine();
                        int rowIndex = 0;
                        string line;

                        while ((line = reader.ReadLine()) != null)
                        {
                            try
                            {
                                var values = line.Split(',');

                                var sample = new EisSample
                                {
                                    SessionId = sessionId,
                                    RowIndex = rowIndex++,
                                    FrequencyHz = double.Parse(values[0], CultureInfo.InvariantCulture),
                                    R_ohm = double.Parse(values[1], CultureInfo.InvariantCulture),
                                    X_ohm = double.Parse(values[2], CultureInfo.InvariantCulture),
                                    T_degC = double.Parse(values[4], CultureInfo.InvariantCulture),
                                    Range_ohm = double.Parse(values[5], CultureInfo.InvariantCulture),
                                    TimestampLocal = DateTime.Now,
                                    SoC = meta.SoC
                                };

                                var response = client.PushSample(sample);

                                Console.WriteLine($"PushSample Row {sample.RowIndex}: {response.Status}");

                                if (!string.IsNullOrEmpty(response.WarningMessage))
                                    Console.WriteLine($"Warning: {response.WarningMessage}");

                                if (response.TemperatureSpike)
                                    Console.WriteLine($"Temperature spike detected: ΔT={response.DeltaT}, Direction={response.SpikeDirection}");
                            }
                            catch (FaultException<DataFormatFault> ex)
                            {
                                Console.WriteLine($"Data format error at row {rowIndex}: {ex.Detail.Message}");
                            }
                            catch (FaultException<ValidationFault> ex)
                            {
                                Console.WriteLine($"Validation error at row {rowIndex}: {ex.Detail.Message}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Unknown error at row {rowIndex}: {ex.Message}");
                            }
                        }
                    }

                    client.EndSession(meta);
                    Console.WriteLine($"Session {sessionId} ended for {fileName}");
                }
                catch (FaultException<DataFormatFault> ex)
                {
                    Console.WriteLine($"Data format error starting session: {ex.Detail.Message}");
                    continue;
                }
                catch (FaultException<ValidationFault> ex)
                {
                    Console.WriteLine($"Validation error starting session: {ex.Detail.Message}");
                    continue;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to start session for {fileName}: {ex.Message}");
                    continue;
                }
            }

            if (client is ICommunicationObject comm)
            {
                try { comm.Close(); }
                catch { comm.Abort(); }
            }

            Console.WriteLine("All CSVs processed. Press Enter to exit...");
            Console.ReadLine();
        }
    }
}