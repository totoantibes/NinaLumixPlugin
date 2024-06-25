using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Roberthasson.NINA.Lumixcamera.LumixcameraTestCategory {
    [Export(typeof(ResourceDictionary))]
    public partial class PluginItemTemplate : ResourceDictionary {
        public PluginItemTemplate() {
            InitializeComponent();
        }
    }
}