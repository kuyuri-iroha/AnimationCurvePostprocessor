using System;
using System.Collections.Generic;
using System.Text;
using System.Web.WebPages;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kuyuri.Tools.AnimationPostprocess
{
    public class RemoveProperties : Postprocess
    {
        private List<string> _propertyNames = new List<string>();
        
        public RemoveProperties()
        {
            name = "Remove Properties";
            _propertyNames.Clear();
            var propertyNames = new TextField()
            {
                name = "propertyNames",
                multiline = true,
                value = "",
                style =
                {
                    minHeight = 42,
                    maxHeight = 42*3
                }
            };
            propertyNames.RegisterValueChangedCallback(evt =>
            {
                _propertyNames.Clear();
                _propertyNames.AddRange(evt.newValue.Split(new[] {'\n'}, StringSplitOptions.RemoveEmptyEntries));
            });
            Add(propertyNames);
        }
        
        public override void ExecuteToAnimationClip(out AnimationClip dist, AnimationClip source)
        {
            var refCurveBindings = AnimationUtility.GetCurveBindings(source);
            var newCurveBindings = new List<EditorCurveBinding>();
            var newCurves = new List<AnimationCurve>();
        
            var hitCount = 0;
            foreach (var binding in refCurveBindings)
            {
                var curve =  AnimationUtility.GetEditorCurve(source,binding);
            
                // 検索文字列が含まれている場合は消す
                var isRemove = false;
                foreach (var propName in _propertyNames)
                {
                    isRemove |= !propName.IsEmpty() && binding.propertyName.Contains(propName);
                }

                // Propertyを個別に消すことができないので、消す対象でなかった場合は新規で追加し、値をコピーしないといけない
                if(!isRemove)
                {
                    var copyEditorCurveBinding = new EditorCurveBinding
                    {
                        type = binding.type,
                        path = binding.path,
                        propertyName = binding.propertyName
                    };
                    newCurveBindings.Add(copyEditorCurveBinding);
                    newCurves.Add(curve);
                }
                else
                {
                    hitCount++;
                }
            }

            dist = source;
            if (hitCount > 0)
            {
                var newClip = new AnimationClip();
                AnimationUtility.SetEditorCurves(newClip, newCurveBindings.ToArray(), newCurves.ToArray());
                EditorUtility.SetDirty(newClip);
                dist = newClip;
            }
        }
    }
}