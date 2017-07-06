using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HP663xxCtrl {
    public class Current {
        public double I;

        public override string ToString() {
            return Current.PrettyPrint(I);
        }
        public static string PrettyPrint(double current) {
            if (current < 1e-3)
                return (current * 1.0e6).ToString() + " uA";
            if (current < 1)
                return (current * 1.0e3).ToString() + " mA";
            return current.ToString() + " A";
        }
    }
}
