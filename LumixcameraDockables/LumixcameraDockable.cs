using NINA.Astrometry;
using NINA.Astrometry.Interfaces;
using NINA.Equipment.Equipment.MyTelescope;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.ViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Roberthasson.NINA.Lumixcamera.LumixcameraDockables {
    /// <summary>
    /// This Class shows the basic principle on how to add a new panel to N.I.N.A. Imaging tab via the plugin interface
    /// In this example an altitude chart is added to the imaging tab that shows the altitude chart based on the position of the telescope    
    /// </summary>
    [Export(typeof(IDockableVM))]
    public class LumixcameraDockable : DockableVM, ITelescopeConsumer {
        private INighttimeCalculator nighttimeCalculator;
        private ITelescopeMediator telescopeMediator;

        [ImportingConstructor]
        public LumixcameraDockable(
            IProfileService profileService,
            ITelescopeMediator telescopeMediator,
            INighttimeCalculator nighttimeCalculator) : base(profileService) {

            // This will reference the resource dictionary to import the SVG graphic and assign it as the icon for the header bar
            var dict = new ResourceDictionary();
            dict.Source = new Uri("Roberthasson.NINA.Lumixcamera;component/LumixcameraDockables/LumixcameraDockableTemplates.xaml", UriKind.RelativeOrAbsolute);
            ImageGeometry = (System.Windows.Media.GeometryGroup)dict["Roberthasson.NINA.Lumixcamera_AltitudeSVG"];
            ImageGeometry.Freeze();

            this.nighttimeCalculator = nighttimeCalculator;
            this.telescopeMediator = telescopeMediator;
            telescopeMediator.RegisterConsumer(this);
            Title = "Altitude Chart";
            Target = null;

            // Some asynchronous initialization
            Task.Run(() => {
                NighttimeData = nighttimeCalculator.Calculate();
                nighttimeCalculator.OnReferenceDayChanged += NighttimeCalculator_OnReferenceDayChanged;
            });

            // Registering to profile service events to react on
            profileService.LocationChanged += (object sender, EventArgs e) => {
                Target?.SetDateAndPosition(NighttimeCalculator.GetReferenceDate(DateTime.Now), profileService.ActiveProfile.AstrometrySettings.Latitude, profileService.ActiveProfile.AstrometrySettings.Longitude);
            };

            profileService.HorizonChanged += (object sender, EventArgs e) => {
                Target?.SetCustomHorizon(profileService.ActiveProfile.AstrometrySettings.Horizon);
            };
        }

        private void NighttimeCalculator_OnReferenceDayChanged(object sender, EventArgs e) {
            NighttimeData = nighttimeCalculator.Calculate();
            RaisePropertyChanged(nameof(NighttimeData));
        }

        public void Dispose() {
            // On shutdown cleanup
            telescopeMediator.RemoveConsumer(this);
        }
        public NighttimeData NighttimeData { get; private set; }
        public TelescopeInfo TelescopeInfo { get; private set; }
        public DeepSkyObject Target { get; private set; }

        public void UpdateDeviceInfo(TelescopeInfo deviceInfo) {
            // The IsVisible flag indicates if the dock window is active or hidden
            if (IsVisible) {
                TelescopeInfo = deviceInfo;
                if (TelescopeInfo.Connected && TelescopeInfo.TrackingEnabled && NighttimeData != null) {
                    var showMoon = Target != null ? Target.Moon.DisplayMoon : false;
                    if (Target == null || (Target?.Coordinates - deviceInfo.Coordinates)?.Distance.Degree > 1) {
                        Target = new DeepSkyObject("", deviceInfo.Coordinates, "", profileService.ActiveProfile.AstrometrySettings.Horizon);
                        Target.SetDateAndPosition(NighttimeCalculator.GetReferenceDate(DateTime.Now), profileService.ActiveProfile.AstrometrySettings.Latitude, profileService.ActiveProfile.AstrometrySettings.Longitude);
                        if (showMoon) {
                            Target.Refresh();
                            Target.Moon.DisplayMoon = true;
                        }
                        RaisePropertyChanged(nameof(Target));
                    }
                } else {
                    Target = null;
                    RaisePropertyChanged(nameof(Target));
                }
                RaisePropertyChanged(nameof(TelescopeInfo));
            }
        }
    }
}
