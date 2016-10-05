using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Text;

namespace HP663xxCtrl  {
    public class MainWindowVm : INotifyPropertyChanged {

        bool _HasDVM = true;
        public bool HasDVM {
            get { return _HasDVM; }
            set { if (value != _HasDVM) { _HasDVM = value; NotifyPropertyChanged(); } }
        }
        bool _HasChannel2 = true;
        public bool HasChannel2 {
            get { return _HasChannel2; }
            set { if (value != _HasChannel2) { _HasChannel2 = value; NotifyPropertyChanged(); } }
        }

        private double _OVPLevel = 20;
        public double OVPLevel {
            get { return _OVPLevel; }
            set { if (value != _OVPLevel) { _OVPLevel = value; NotifyPropertyChanged(); } }
        }

        private double _V1 = 0.0;
        public double V1 {
            get { return _V1; }
            set { if (value != _V1) { _V1 = value; NotifyPropertyChanged(); } }
        }

        private double _I1 = 0.02;
        public double I1 {
            get { return _I1; }
            set { if (value != _I1) { _I1 = value; NotifyPropertyChanged(); } }
        }


        private double _V2 = 0.0;
        public double V2 {
            get { return _V2; }
            set { if (value != _V2) { _V2 = value; NotifyPropertyChanged(); } }
        }

        private double _I2 = 0.02;
        public double I2 {
            get { return _I2; }
            set { if (value != _I2) { _I2 = value; NotifyPropertyChanged(); } }
        }
        private double _AcqDuration = 0.1;
        public double AcqDuration {
            get { return _AcqDuration; }
            set { if (value != _AcqDuration) { _AcqDuration = value; NotifyPropertyChanged(); } }
        }
        private int _AcqNumPoints = 1024;
        public int AcqNumPoints {
            get { return _AcqNumPoints; }
            set { if (value != _AcqNumPoints) { _AcqNumPoints = value; NotifyPropertyChanged(); } }
        }

        private int _AcqSegments = 1024;
        public int AcqSegments {
            get { return _AcqSegments; }
            set { if (value != _AcqSegments) { _AcqSegments = value; NotifyPropertyChanged(); } }
        }

        private double _TriggerHysteresis = 0.0;
        public double TriggerHysteresis {
            get { return _TriggerHysteresis; }
            set { if (value != _TriggerHysteresis) { _TriggerHysteresis = value; NotifyPropertyChanged(); } }
        }
        private double _TriggerLevel = 0.0;
        public double TriggerLevel {
            get { return _TriggerLevel; }
            set { if (value != _TriggerLevel) { _TriggerLevel = value; NotifyPropertyChanged(); } }
        }
        private int _TriggerOffset = 0;
        public int TriggerOffset {
            get { return _TriggerOffset; }
            set { if (value != _TriggerOffset) { _TriggerOffset = value; NotifyPropertyChanged(); } }
        } 
        
        // Property Changed Handler
        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "") {
            if (PropertyChanged != null) {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
