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
        public List<double[]> DataSeries;
        public double ProgV1, ProgI1, ProgV2, ProgI2;

        // Trigger Configuration
        public InstrumentWorker.AcquireDetails details;
    }
}
