using System;
using System.Collections.Generic;
using System.Linq;

namespace UIIA.Services
{
    public class LinearRegression
    {
        public double Slope { get; private set; }
        public double Intercept { get; private set; }
        public double RSquared { get; private set; }

        public void Fit(List<double> x, List<double> y)
        {
            if (x.Count != y.Count || x.Count == 0)
            {
                Slope = 0;
                Intercept = 0;
                return;
            }

            var n = x.Count;
            var meanX = x.Average();
            var meanY = y.Average();
            
            var numerator = 0.0;
            var denominator = 0.0;
            
            for (int i = 0; i < n; i++)
            {
                numerator += (x[i] - meanX) * (y[i] - meanY);
                denominator += Math.Pow(x[i] - meanX, 2);
            }

            Slope = denominator != 0 ? numerator / denominator : 0;
            Intercept = meanY - Slope * meanX;

            var ssRes = 0.0;
            var ssTot = 0.0;
            
            for (int i = 0; i < n; i++)
            {
                var predicted = Predict(x[i]);
                ssRes += Math.Pow(y[i] - predicted, 2);
                ssTot += Math.Pow(y[i] - meanY, 2);
            }

            RSquared = ssTot != 0 ? 1 - (ssRes / ssTot) : 0;
        }

        public double Predict(double x)
        {
            return Slope * x + Intercept;
        }
    }
}