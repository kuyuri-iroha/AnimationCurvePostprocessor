using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kuyuri.Tools.AnimationPostprocess
{
    public class RDPReduction : Postprocess
    {
        public enum DistanceMethod
        {
            Perpendicular,
            Shortest,
        }
        
        private float _epsilon = 0.1f;
        private DistanceMethod _distanceMethod = DistanceMethod.Perpendicular;
        
        public RDPReduction()
        {
            name = "RDPReduction";
            
            var epsilon = new FloatField
            {
                label = "Epsilon",
                name = "epsilon",
                value = 0.5f
            };
            epsilon.RegisterValueChangedCallback(evt =>
            {
                _epsilon = evt.newValue;
            });
            Add(epsilon);
            
            var distanceMethod = new EnumField(DistanceMethod.Perpendicular)
            {
                label = "Distance Method",
                name = "distanceMethod",
            };
            distanceMethod.RegisterValueChangedCallback(evt =>
            {
                _distanceMethod = (DistanceMethod)evt.newValue;
            });
            Add(distanceMethod);
        }
        
        public override void ExecuteToAnimationClip(out AnimationClip dist, AnimationClip source)
        {
            var newCurveBindings = new List<EditorCurveBinding>();
            var newCurves = new List<AnimationCurve>();
            
            foreach (var binding in AnimationUtility.GetCurveBindings(source))
            {
                var curve = AnimationUtility.GetEditorCurve(source, binding);
                
                var keyframes = curve.keys;
                DouglasPeucker(out var reducedKeyframes, keyframes, _epsilon, _distanceMethod);

                var reducedCurve = new AnimationCurve
                {
                    keys = reducedKeyframes
                };
                
                for (var i = 0; i < reducedCurve.keys.Length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(reducedCurve, i, AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyRightTangentMode(reducedCurve, i, AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyBroken(reducedCurve, i, true);
                }
                
                newCurveBindings.Add(binding);
                newCurves.Add(reducedCurve);
            }
            
            dist = new AnimationClip();
            AnimationUtility.SetEditorCurves(dist, newCurveBindings.ToArray(), newCurves.ToArray());
            EditorUtility.SetDirty(dist);
        }
        
        private static void DouglasPeucker(out Keyframe[] outKeyFrames, in Keyframe[] inKeyframes, float eps, DistanceMethod method)
        {
            if (inKeyframes.Length < 3)
            {
                outKeyFrames = new Keyframe[inKeyframes.Length];
                Array.Copy(inKeyframes, outKeyFrames, inKeyframes.Length);
                return;
            }
        
            var first = inKeyframes.First();
            var last = inKeyframes.Last();

            var index = -1;
            var dist = 0f;
            for(var  i = 1; i < inKeyframes.Length - 1; i++)
            {
                var d = method switch
                {
                    DistanceMethod.Perpendicular => CalcPerpendicularDistance(first, inKeyframes[i], last),
                    DistanceMethod.Shortest => CalcShortestDistance(inKeyframes[i], first, last),
                    _ => throw new ArgumentOutOfRangeException(nameof(method), method, null)
                };
                
                if(dist < d)
                {
                    dist = d;
                    index = i;
                }
            }

            if (eps < dist)
            {
                var keys1 = new Keyframe[index + 1];
                Array.Copy(inKeyframes, 0, keys1, 0, keys1.Length);
                var keys2 = new Keyframe[inKeyframes.Length - index];
                Array.Copy(inKeyframes, index, keys2, 0, keys2.Length);
                DouglasPeucker(out var resKeys1, in keys1, eps, method);
                DouglasPeucker(out var resKeys2,in keys2, eps, method);
            
                outKeyFrames = resKeys1.Concat(resKeys2).Distinct().ToArray();
            }
            else
            {
                outKeyFrames = new Keyframe[] {first, last};
            }
        }
        
        // イマイチうまく動かなかった（点が一直線上にあるときに消えなかった）
        private static float CalcShortestDistance(Keyframe start, Keyframe mid, Keyframe end)
        {
            float PointDistanceSq(float x0, float y0, float x1, float y1)
            {
                return Mathf.Pow(x1 - x0, 2) + Mathf.Pow(y1 - y0, 2);
            }
        
            var lineLength = Mathf.Sqrt(PointDistanceSq(start.time, start.value, end.time, end.value));
            if (lineLength == 0)
            {
                return PointDistanceSq(start.time, start.value, mid.time, mid.value);
            }
            var t = ((mid.time - start.time) * (end.time - start.time) + (mid.value - start.value) * (end.value - start.value)) / lineLength;
            var distanceSq = t switch
            {
                < 0 => PointDistanceSq(mid.time, mid.value, start.time, start.value),
                > 1 => PointDistanceSq(mid.time, mid.value, end.time, end.value),
                _ => PointDistanceSq(mid.time, mid.value, start.time + t * (end.time - start.time),
                    start.value + t * (end.value - start.value))
            };

            return Mathf.Sqrt(distanceSq);
        }

        // 垂直距離
        private static float CalcPerpendicularDistance(Keyframe start, Keyframe mid, Keyframe end)
        {
            if(Mathf.Approximately(start.time, end.time))
            {
                return Mathf.Abs(mid.value - start.value);
            }

            var slope = (end.value - start.value) / (end.time - start.time);
            var intercept = start.value - slope * start.time;
            return Mathf.Abs(slope * mid.time + intercept - mid.value) / Mathf.Sqrt(slope * slope + 1);
        }
    }
}