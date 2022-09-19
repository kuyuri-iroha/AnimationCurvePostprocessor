
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Kuyuri.Tools.AnimationPostprocess
{
    public class EqualInterval : Postprocess
    {
        public EqualInterval()
        {
            name = "EqualInterval";
        }
        
        public override void ExecuteToAnimationClip(out AnimationClip dist, AnimationClip source, List<string> targetPropertyNames)
        {
            var newCurveBindings = new List<EditorCurveBinding>();
            var newCurves = new List<AnimationCurve>();

            foreach (var binding in AnimationUtility.GetCurveBindings(source))
            {
                var curve = AnimationUtility.GetEditorCurve(source, binding);
                
                if (!IsTargetProperty(binding, targetPropertyNames))
                {
                    newCurveBindings.Add(binding);
                    newCurves.Add(curve);
                    continue;
                }

                var newCurve = new AnimationCurve();
                var keyCnt = 0;
                var duration = curve.keys.Max(val => val.time);
                foreach (var key in curve.keys)
                {
                    newCurve.AddKey(new Keyframe
                    {
                        time = (float)keyCnt / (curve.keys.Length-1) * duration,
                        value = key.value,
                        inTangent = key.inTangent,
                        outTangent = key.outTangent,
                        weightedMode = key.weightedMode,
                        inWeight = key.inWeight,
                        outWeight = key.outWeight,
                    });
                    keyCnt++;
                }
                
                newCurveBindings.Add(binding);
                newCurves.Add(newCurve);
            }
            
            dist = new AnimationClip();
            AnimationUtility.SetEditorCurves(dist, newCurveBindings.ToArray(), newCurves.ToArray());
            EditorUtility.SetDirty(dist);
        }
    }
}