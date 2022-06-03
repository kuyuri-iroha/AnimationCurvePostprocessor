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
            var propertyNames = new ListView
            {
                itemsSource = _propertyNames,
                showAddRemoveFooter = true,
                selectionType = SelectionType.Multiple,
                reorderable = false,
                showBorder = true,
                showFoldoutHeader = true,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
                showBoundCollectionSize = true
            };
            propertyNames.makeItem = () => new TextField()
            {
                value = "",
                multiline = false,
            };
            propertyNames.bindItem = (item, index) =>
            {
                var textField = item as TextField;
                textField.value = _propertyNames[index];
                textField.RegisterValueChangedCallback(evt =>
                {
                    _propertyNames[index] = evt.newValue;
                });
            };
            Add(propertyNames);
        }
        
        public override void ExecuteToAnimationClip(out AnimationClip dist, AnimationClip source)
        {
            var str = new StringBuilder();
            foreach (var propertyName in _propertyNames)
            {
                str.AppendLine(propertyName);
            }
            Debug.Log(str);
            
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