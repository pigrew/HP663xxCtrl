using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HP663xxCtrl {
    public class AcquisitionData {
        // General
        public DateTime StartAcquisitionTime;
        public double SamplingPeriod; // Rounded version, as returned by the instrument
        public List<double[]> DataSeries = new List<double[]>();
        public ProgramDetails ProgramDetails;
        public InstrumentWorker.AcquireDetails AcqDetails;

    }
}
