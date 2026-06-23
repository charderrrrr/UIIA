using System;
using System.Collections.Generic;
using System.Linq;

namespace UIIA.Services
{
    public class LinearRegression
    {
        public RegressionResult Fit(List<double> x, List<double> y)
        {
            if (x.Count != y.Count || x.Count == 0)
            {
                return new RegressionResult(0, 0, 0);
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

            var slope = denominator != 0 ? numerator / denominator : 0;
            var intercept = meanY - slope * meanX;

            var ssRes = 0.0;
            var ssTot = 0.0;
            
            for (int i = 0; i < n; i++)
            {
                var predicted = slope * x[i] + intercept;
                ssRes += Math.Pow(y[i] - predicted, 2);
                ssTot += Math.Pow(y[i] - meanY, 2);
            }

            var rSquared = ssTot != 0 ? 1 - (ssRes / ssTot) : 0;

            return new RegressionResult(slope, intercept, rSquared);
        }
    }

    public class RegressionResult
    {
        public double Slope { get; }
        public double Intercept { get; }
        public double RSquared { get; }

        public RegressionResult(double slope, double intercept, double rSquared)
        {
            Slope = slope;
            Intercept = intercept;
            RSquared = rSquared;
        }

        public double Predict(double x) => Slope * x + Intercept;
    }
}