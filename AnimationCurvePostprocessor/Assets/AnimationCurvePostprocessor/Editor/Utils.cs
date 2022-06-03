using System.Linq;
using UnityEngine;

namespace Kuyuri.Tools.AnimationPostprocess
{
    public static class Utils
    {
        public static void Resampling(out float[] xi, float[] x, float rate)
        {
            // Rateが大きすぎる場合は端の2点のみとする
            var xMax = x.Max();
            if (xMax <= rate)
            {
                xi = new float[2];
                xi[0] = x[0];
                xi[1] = x[^1];
                return;
            }

            var xiInterpLength = Mathf.CeilToInt(xMax / rate - 1);
            xi = new float[xiInterpLength + 2];
            for(var i = 1; i < xiInterpLength + 1; i++)
            {
                xi[i] = i * rate;
            }
            xi[0] = x[0];
            xi[^1] = x[^1];
        }
    }
}