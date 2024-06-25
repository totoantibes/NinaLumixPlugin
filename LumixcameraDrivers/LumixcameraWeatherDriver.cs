using NINA.Core.Utility;
using NINA.Equipment.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Roberthasson.NINA.Lumixcamera.LumixcameraDrivers {
    /// <summary>
    /// This Class shows the basic principle on how to add a new Device driver to N.I.N.A. via the plugin interface
    /// The DeviceProvider will return an instance of this class as a sample weather device
    /// For this example the weather data will generate random numbers
    /// </summary>
    public class LumixcameraWeatherDriver : BaseINPC, IWeatherData {
        public LumixcameraWeatherDriver(string uniqueId, string deviceId) {
            Id = uniqueId;
            serialNumber = deviceId;
            Name = $"Lumixcamera Weather Data ({deviceId})";
            DriverInfo = $"Serial {deviceId}";
            random = new Random();
        }

        private string serialNumber;

        public string Name { get; }

        public string DisplayName => Name;

        public string Category => "Lumixcamera Weather Driver";

        public string Description => "My custom weather driver from a plugin";

        public string DriverInfo { get; }

        public string DriverVersion { get; private set; }

        public bool Connected { get; private set; }

        public IList<string> SupportedActions => new List<string>();

        public bool HasSetupDialog => false;

        public string Id { get; }

        private Random random;

        public double DewPoint => random.Next(-10, 10);

        public double Humidity => random.Next(0, 100);

        public double Temperature => random.Next(-10, 20);

        public async Task<bool> Connect(CancellationToken token) {
            Connected = true;

            return Connected;
        }

        public void Disconnect() {
            Connected = false;
        }

        #region Unsupported

        public double Pressure => double.NaN;
        public double RainRate => double.NaN;
        public double SkyBrightness => double.NaN;
        public double SkyQuality => double.NaN;
        public double SkyTemperature => double.NaN;
        public double StarFWHM => double.NaN;
        public double WindDirection => double.NaN;
        public double WindGust => double.NaN;
        public double WindSpeed => double.NaN;
        public double CloudCover => double.NaN;

        public double AveragePeriod {
            get => double.NaN;
            set { }
        }

        public void SendCommandBlind(string command, bool raw = true) {
            throw new NotImplementedException();
        }

        public bool SendCommandBool(string command, bool raw = true) {
            throw new NotImplementedException();
        }

        public string SendCommandString(string command, bool raw = true) {
            throw new NotImplementedException();
        }

        public void SetupDialog() {
            throw new NotImplementedException();
        }

        public string Action(string actionName, string actionParameters) {
            throw new NotImplementedException();
        }

        #endregion Unsupported
    }
}
