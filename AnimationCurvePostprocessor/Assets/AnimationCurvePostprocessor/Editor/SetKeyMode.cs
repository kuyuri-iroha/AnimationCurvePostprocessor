using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kuyuri.Tools.AnimationPostprocess
{
    public class SetKeyMode : Postprocess
    {
        private WeightedMode _weightedMode;
        private bool _broken;
        private AnimationUtility.TangentMode _leftTangentMode;
        private AnimationUtility.TangentMode _rightTangentMode;
        
        public SetKeyMode()
        {
            name = "SetKeyMode";
            
            var weightMode = new EnumField
            {
                value = WeightedMode.Both,
                label = "Weight Mode",
                name = "weightMode",
            };
            weightMode.Init(WeightedMode.Both);
            weightMode.RegisterValueChangedCallback(evt =>
            {
                _weightedMode = (WeightedMode) evt.newValue;
            });
            Add(weightMode);
            
            var brokenToggle = new Toggle
            {
                value = false,
                label = "Broken",
                name = "brokenToggle",
            };
            brokenToggle.RegisterValueChangedCallback(evt =>
            {
                _broken = evt.newValue;
            });
            Add(brokenToggle);
            
            var leftTangentMode = new EnumField
            {
                label = "Left Tangent Mode",
                name = "leftTangentMode",
            };
            leftTangentMode.Init(AnimationUtility.TangentMode.Free);
            leftTangentMode.RegisterValueChangedCallback(evt =>
            {
                _leftTangentMode = (AnimationUtility.TangentMode) evt.newValue;
            });
            Add(leftTangentMode);
            
            var rightTangentMode = new EnumField
            {
                label = "Right Tangent Mode",
                name = "rightTangentMode"
            };
            rightTangentMode.Init(AnimationUtility.TangentMode.Free);
            rightTangentMode.RegisterValueChangedCallback(evt =>
            {
                _rightTangentMode = (AnimationUtility.TangentMode) evt.newValue;
            });
            Add(rightTangentMode);
        }

        public override void ExecuteToAnimationClip(out AnimationClip dist, AnimationClip source)
        {
            var newCurveBindings = new List<EditorCurveBinding>();
            var newCurves = new List<AnimationCurve>();
            
            foreach (var binding in AnimationUtility.GetCurveBindings(source))
            {
                var curve = AnimationUtility.GetEditorCurve(source, binding);

                var newCurve = new AnimationCurve();
                foreach (var key in curve.keys)
                {
                    newCurve.AddKey(new Keyframe
                    {
                        time = key.time,
                        value = key.value,
                        inTangent = key.inTangent,
                        outTangent = key.outTangent,
                        weightedMode = _weightedMode,
                        inWeight = key.inWeight,
                        outWeight = key.outWeight,
                    });
                }
                
                for (var i = 0; i < newCurve.keys.Length; i++)
                {
                    AnimationUtility.SetKeyBroken(newCurve, i, _broken);
                    AnimationUtility.SetKeyLeftTangentMode(newCurve, i, _leftTangentMode);
                    AnimationUtility.SetKeyRightTangentMode(newCurve, i, _rightTangentMode);
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