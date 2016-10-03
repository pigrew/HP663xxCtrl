using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace HP663xxCtrl  {
    public class MainWindowVm : INotifyPropertyChanged {

        private double _TriggerHysteresis = 0.0;
        public double TriggerHysteresis {
            get { return _TriggerHysteresis; }
            set { _TriggerHysteresis = value; }
        }
        private double _TriggerLevel = 0.0;
        public double TriggerLevel {
            get { return _TriggerLevel; }
            set { _TriggerLevel = value; }
        }
        private int _TriggerOffset = 0;
        public int TriggerOffset {
            get { return _TriggerOffset; }
            set { _TriggerOffset = value; }
        } 
        
        private int _AcqSegments = 1;
        public int AcqSegments {
            get { return _AcqSegments; }
            set { _AcqSegments = value; }
        }

        // Property Changed Handler
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            if(PropertyChanged != null)
                this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
