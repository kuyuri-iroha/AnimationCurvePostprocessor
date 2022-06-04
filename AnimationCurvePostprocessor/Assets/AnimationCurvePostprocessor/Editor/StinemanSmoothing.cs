using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kuyuri.Tools.AnimationPostprocess
{
    public class StinemanSmoothing : Postprocess
    {
        private float _resampleRate = 0.5f;
        private bool _retentionSourcePoint = false;
        private bool _onlySetTangent = false;
        
        public StinemanSmoothing()
        {
            name = "StinemanSmoothing";
            var resampleRate = new FloatField
            {
                label = "Resampling Rate",
                name = "resampleRateField",
                value = 0.5f
            };
            resampleRate.RegisterValueChangedCallback(evt =>
            {
                _resampleRate = evt.newValue;
            });
            resampleRate.RegisterCallback<BlurEvent>(evt =>
            {
                resampleRate.value = Mathf.Max(resampleRate.value, 1e-4f);
            });
            Add(resampleRate);
            
            var retentionSourcePoint = new Toggle
            {
                label = "Retention Source Point",
                name = "retentionSourcePointToggle",
                value = false
            };
            retentionSourcePoint.RegisterValueChangedCallback(evt =>
            {
                _retentionSourcePoint = evt.newValue;
            });
            Add(retentionSourcePoint);
            
            var onlySetTangent = new Toggle
            {
                label = "Only Set Tangent",
                name = "onlySetTangentToggle",
                value = false
            };
            onlySetTangent.RegisterValueChangedCallback(evt =>
            {
                _onlySetTangent = evt.newValue;
            });
            Add(onlySetTangent);
        }
        
        public override void ExecuteToAnimationClip(out AnimationClip dist, AnimationClip source)
        {
            var newCurveBindings = new List<EditorCurveBinding>();
            var newCurves = new List<AnimationCurve>();
            
            foreach (var binding in AnimationUtility.GetCurveBindings(source))
            {
                var curve = AnimationUtility.GetEditorCurve(source, binding);
                
                var smoothedCurve = new AnimationCurve();
                var sourceKeyLength = curve.keys.Length;
        
                // キーが3つ未満の場合はスムージングしない
                if(3 <= sourceKeyLength)
                {
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
                    
                    // 元のキーを保持
                    if (_retentionSourcePoint)
                    {
                        var tmpXi = new List<float>(xi);
                        tmpXi.AddRange(x);
                        xi = tmpXi.Distinct().OrderBy(val => val).ToArray();
                    }
                
                    // 補間
                    Stineman(out var yi, x, y, xi);
        
                    // Tangentを設定してスムージング
                    if (_onlySetTangent)
                    {
                        for (var i = 0; i < x.Length; i++)
                        {
                            var interpIndex = Array.BinarySearch(xi, x[i]);
                            var foundSame = 0 <= interpIndex;
                            interpIndex = Mathf.Max(interpIndex < 0 ? ~interpIndex - 1 : interpIndex, 0);
                            var inTangent = 0f;
                            var outTangent = 0f;

                            if (foundSame)
                            {
                                inTangent = 0 < interpIndex ? (yi[interpIndex] - yi[interpIndex - 1]) / (xi[interpIndex] - xi[interpIndex - 1]) : 0;
                                outTangent = interpIndex < xi.Length - 1 ? (yi[interpIndex + 1] - yi[interpIndex]) / (xi[interpIndex + 1] - xi[interpIndex]) : 0;
                            }
                            else
                            {
                                inTangent = interpIndex < xi.Length - 1 ? (yi[interpIndex + 1] - yi[interpIndex]) / (xi[interpIndex + 1] - xi[interpIndex]) : 0;
                                outTangent = inTangent;
                            }
                            
                            smoothedCurve.AddKey(new Keyframe()
                            {
                                time = x[i],
                                value = y[i],
                                inTangent = inTangent,
                                outTangent = outTangent,
                                inWeight = 0,
                                outWeight = 0
                            });
                        }
                    }
                    else
                    {
                        for (var i = 0; i < xi.Length; i++)
                        {
                            var inTangent = 0 < i ? (yi[i] - yi[i - 1]) / (xi[i] - xi[i - 1]) : 0;
                            var outTangent = i < xi.Length - 1 ? (yi[i + 1] - yi[i]) / (xi[i + 1] - xi[i]) : 0;
                            smoothedCurve.AddKey(new Keyframe()
                            {
                                time = xi[i],
                                value = yi[i],
                                inTangent = inTangent,
                                outTangent = outTangent,
                                inWeight = 0,
                                outWeight = 0
                            });
                        }
                    }
                }
                else
                {
                    smoothedCurve.keys = curve.keys.ToArray();
                }
                
                for (var i = 0; i < smoothedCurve.keys.Length; i++)
                {
                    AnimationUtility.SetKeyBroken(smoothedCurve, i, true);
                    AnimationUtility.SetKeyLeftTangentMode(smoothedCurve, i, AnimationUtility.TangentMode.Free);
                    AnimationUtility.SetKeyRightTangentMode(smoothedCurve, i, AnimationUtility.TangentMode.Free);
                }
                
                newCurveBindings.Add(binding);
                newCurves.Add(smoothedCurve);
            }
            
            dist = new AnimationClip();
            AnimationUtility.SetEditorCurves(dist, newCurveBindings.ToArray(), newCurves.ToArray());
            EditorUtility.SetDirty(dist);
        }
        
        // Stineman補間
        // Ref. https://github.com/jdh2358/py4science/blob/master/examples/extras/steinman_interp.py
        private static void Stineman(out float[] yi, float[] x, float[] y, float[] xi, float[] yp = null)
        {
            if (x.Length != y.Length)
            {
                yi = null;
                return;
            }
    
            // 各点の傾きを事前計算
            var dx = new float[x.Length - 1];
            var dy = new float[x.Length - 1];
            var dydx = new float[x.Length - 1];
            for(var i = 0; i < x.Length - 1; i++)
            {
                dx[i] = x[i + 1] - x[i];
                dy[i] = y[i + 1] - y[i];
                dydx[i] = dy[i] / dx[i];
            }
            
            // 元データの各点の傾きの算出
            if (yp == null)
            {
                yp = new float[x.Length];
                for (var i = 1; i < x.Length - 1; i++)
                {
                    yp[i] = (dydx[i - 1] * dx[i] + dydx[i] * dx[i - 1]) / (dx[i] + dx[i - 1]);
                }
                yp[0] = 2.0f * dy[0] / dx[0] - yp[1];
                yp[^1] = 2.0f * dy[^1] / dx[^1] - yp[^2];
            }
            
            // yiの算出
            yi = new float[xi.Length];
            for (var i = 0; i < xi.Length; i++)
            {
                var xii = xi[i];
                var index = Array.BinarySearch(x, xii);
                index = Mathf.Max((index < 0 ? ~index : index) - 1, 0);
    
                var baseDydx = dydx[index];
                var baseX = x[index];
                var baseY = y[index];
                var nextX = x[index + 1];
                var yo = baseY + baseDydx * (xii - baseX);
    
                var dy1 = (yp[index] - baseDydx) * (xii - baseX);
                var dy2 = (yp[index + 1] - baseDydx) * (xii - nextX);
                var dy1dy2 = dy1 * dy2;
    
                yi[i] = yo + dy1dy2 * Math.Sign(dy1dy2) switch {
                    -1 => (2.0f * xii - baseX - nextX) / ((dy1 - dy2) * (nextX - baseX)),
                    0 => 0.0f,
                    _ => 1.0f / (dy1 + dy2)
                };
            }
        }
    }
}