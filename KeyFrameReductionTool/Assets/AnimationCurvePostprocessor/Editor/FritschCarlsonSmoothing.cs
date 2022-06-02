﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kuyuri.Tools.AnimationPostprocess
{
    public class FritschCarlsonSmoothing : Postprocess
    {
        private float _resampleRate = 0.5f;
        
        public FritschCarlsonSmoothing()
        {
            name = "FritschCarlsonSmoothing";
            var resampleRate = new FloatField
            {
                label = "Resampling Rate",
                name = "resampleRateField",
                value = 0.5f
            };
            resampleRate.RegisterValueChangedCallback(evt =>
            {
                resampleRate.value = Mathf.Max(evt.newValue, 1e-4f);
                _resampleRate = resampleRate.value;
            });
            Add(resampleRate);
        }
        
        public void ExecuteToAnimationClip(out AnimationClip dist, AnimationClip source)
        {
            var newCurveBindings = new List<EditorCurveBinding>();
            var newCurves = new List<AnimationCurve>();
            
            foreach (var binding in AnimationUtility.GetCurveBindings(source).ToArray())
            {
                var curve = AnimationUtility.GetEditorCurve(source, binding);
                
                var smoothedCurve = new AnimationCurve();
                var sourceKeyLength = curve.keys.Length;
        
                // キーが3つ未満の場合はスムージングしない
                if(sourceKeyLength < 3)
                {
                    smoothedCurve.keys = curve.keys.ToArray();
                    continue;
                }
        
                // キーをx, yの配列に展開
                var x = new float[sourceKeyLength];
                var y = new float[sourceKeyLength];
                for(var i = 0; i < sourceKeyLength - 1; i++)
                {
                    x[i] = curve.keys[i].time;
                    y[i] = curve.keys[i].value;
                }
                x[^1] = curve.keys[^1].time;
                y[^1] = curve.keys[^1].value;
                
                // リサンプリング
                Utils.Resampling(out var xi, x, _resampleRate);

                // 補間
                FritschCarlson(out var yi, x, y, xi);
        
                // Tangentを設定してスムージング
                for (var i = 0; i < xi.Length; i++)
                {
                    var inTangent = 0 < i ? (yi[i] - yi[i - 1]) / (xi[i] - xi[i - 1]) : 0;
                    var outTangent = i < xi.Length - 1 ? (yi[i + 1] - yi[i]) / (xi[i + 1] - xi[i]) : 0;
                    smoothedCurve.AddKey(new Keyframe()
                    {
                        time = curve.keys[i].time,
                        value = curve.keys[i].value,
                        inTangent = inTangent,
                        outTangent = outTangent,
                        inWeight = 0,
                        outWeight = 0,
                    });
                }
                
                newCurveBindings.Add(binding);
                newCurves.Add(smoothedCurve);
            }
            
            dist = new AnimationClip();
            AnimationUtility.SetEditorCurves(dist, newCurveBindings.ToArray(), newCurves.ToArray());
            EditorUtility.SetDirty(dist);
        }

        
        
        // Fritsch-Carlson補間
        // Ref. https://codereview.stackexchange.com/questions/73622/monotone-cubic-interpolation
        private static void FritschCarlson(out float[] yi, float[] xs, float[] ys, float[] x_interp)
        {
            var length = xs.Length;

            // Deal with length issues
            if (length != ys.Length && length == 0)
            {
                yi = null;
                return;
            }
            if (length == 1)
            {
                yi = new float[] { ys[0] };;
                return;
            }
            
            // Get consecutive differences and slopes
            var delta = new float[length - 1];
            var m = new float[length];

            for (int i = 0; i < length - 1; i++)
            {
                delta[i] = (ys[i + 1] - ys[i]) / (xs[i + 1] - xs[i]);
                if (i > 0)
                {
                    m[i] = (delta[i - 1] + delta[i]) / 2;
                }
            }
            var toFix = new List<int>();
            for (int i = 1; i < length - 1; i++)
            {
                if ((delta[i] > 0 && delta[i - 1] < 0) || (delta[i] < 0 && delta[i - 1] > 0))
                {
                    toFix.Add(i);
                }
            }
            foreach (var val in toFix)
            {
                m[val] = 0;
            }

            m[0] = delta[0];
            m[length - 1] = delta[length - 2];

            toFix.Clear();
            for (int i = 0; i < length - 1; i++)
            {
                if (delta[i] == 0)
                {
                    toFix.Add(i);
                }
            }
            foreach (var val in toFix)
            {
                m[val] = 0;
                m[val + 1] = 0;
            }

            var alpha = new float[length - 1];
            var beta = new float[length - 1];
            var dist = new float[length - 1];
            var tau = new float[length - 1];
            for (int i = 0; i < length - 1; i++)
            {
                alpha[i] = m[i] / delta[i];
                beta[i] = m[i + 1] / delta[i];
                dist[i] = Mathf.Pow(alpha[i], 2) + Mathf.Pow(beta[i], 2);
                tau[i] = 3.0f/Mathf.Sqrt(dist[i]);
            }

            toFix.Clear();
            for (int i = 0; i < length - 1; i++)
            {
                if (dist[i] > 9)
                {
                    toFix.Add(i);
                }
            }

            foreach (var val in toFix)
            {
                m[val] = tau[val] * alpha[val] * delta[val];
                m[val + 1] = tau[val] * beta[val] * delta[val];
            }

            yi = new float[x_interp.Length];
            int ind = 0;

            foreach (var x in x_interp)
            {
                int i;
                for (i = xs.Length - 2; i >= 0; --i)
                {
                    if (xs[i] <= x)
                    {
                        break;
                    }
                }
                var h = xs[i + 1] - xs[i];
                var t = (x - xs[i])/h;
                var t2 = Mathf.Pow(t, 2);
                var t3 = Mathf.Pow(t, 3);
                var h00 = 2*t3 - 3*t2 + 1;
                var h10 = t3 - 2*t2 + t;
                var h01 = -2*t3 + 3*t2;
                var h11 = t3 - t2;
                yi[ind++] = h00*ys[i] + h10*h*m[i] + h01*ys[i + 1] + h11*h*m[i + 1];

                continue;
            }
        }
    }
}