using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Core.Utility.Notification;
using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Trigger;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Roberthasson.NINA.Lumixcamera.LumixcameraTestCategory {
    /// <summary>
    /// This Class shows the basic principle on how to add a new Sequence Trigger to the N.I.N.A. sequencer via the plugin interface
    /// For ease of use this class inherits the abstract SequenceTrigger which already handles most of the running logic, like logging, exception handling etc.
    /// A complete custom implementation by just implementing ISequenceTrigger is possible too
    /// The following MetaData can be set to drive the initial values
    /// --> Name - The name that will be displayed for the item
    /// --> Description - a brief summary of what the item is doing. It will be displayed as a tooltip on mouseover in the application
    /// --> Icon - a string to the key value of a Geometry inside N.I.N.A.'s geometry resources
    ///
    /// If the item has some preconditions that should be validated, it shall also extend the IValidatable interface and add the validation logic accordingly.
    /// </summary>
    [ExportMetadata("Name", "Plugin Template Trigger")]
    [ExportMetadata("Description", "This trigger will show a notification when a random generated number is even on evaluation")]
    [ExportMetadata("Icon", "Plugin_Test_SVG")]
    [ExportMetadata("Category", "LumixCamera")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]
    public class LumixcameraTrigger : SequenceTrigger {
        /// <summary>
        /// The constructor marked with [ImportingConstructor] will be used to import and construct the object
        /// General device interfaces can be added to the constructor parameters and will be automatically injected on instantiation by the plugin loader
        /// </summary>
        /// <remarks>
        /// Available interfaces to be injected:
        ///     - IProfileService,
        ///     - ICameraMediator,
        ///     - ITelescopeMediator,
        ///     - IFocuserMediator,
        ///     - IFilterWheelMediator,
        ///     - IGuiderMediator,
        ///     - IRotatorMediator,
        ///     - IFlatDeviceMediator,
        ///     - IWeatherDataMediator,
        ///     - IImagingMediator,
        ///     - IApplicationStatusMediator,
        ///     - INighttimeCalculator,
        ///     - IPlanetariumFactory,
        ///     - IImageHistoryVM,
        ///     - IDeepSkyObjectSearchVM,
        ///     - IDomeMediator,
        ///     - IImageSaveMediator,
        ///     - ISwitchMediator,
        ///     - ISafetyMonitorMediator,
        ///     - IApplicationMediator
        ///     - IApplicationResourceDictionary
        ///     - IFramingAssistantVM
        ///     - IList<IDateTimeProvider>
        /// </remarks>
        [ImportingConstructor]
        public LumixcameraTrigger() {
        }

        public override object Clone() {
            return new LumixcameraTrigger() {
                Icon = Icon,
                Name = Name,
                Category = Category,
                Description = Description
            };
        }

        /// <summary>
        /// The actual running logic for when the trigger should run
        /// </summary>
        /// <param name="context"></param>
        /// <param name="progress"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public override Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken token) {
            Notification.ShowSuccess("Trigger was fired");
            return Task.CompletedTask;
        }

        /// <summary>
        /// This method will be evaluated to see if the trigger should be executed.
        /// When true - the Execute method will be called
        /// Skipped otherwise
        ///
        /// For this example the trigger will fire when the random number generator generates an even number
        /// </summary>
        /// <param name="previousItem"></param>
        /// <param name="nextItem"></param>
        /// <returns></returns>
        public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem) {
            return random.Next(0, 1000) % 2 == 0;
        }

        Random random = new Random();

        /// <summary>
        /// This string will be used for logging
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(LumixcameraTrigger)}";
        }
    }
}