using System;
using System.Collections.Generic;
using System.Linq;
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
        private IReadOnlyList<AnimationClip> _sourceAnimationClips;
 
        public RemoveProperties(IReadOnlyList<AnimationClip> sourceAnimationClips)
        {
            name = "Remove Properties";
            _propertyNames.Clear();
            var propertyNames = new ListView
            {
                itemsSource = _propertyNames,
                showAddRemoveFooter = true,
                selectionType = SelectionType.Multiple,
                reorderable = false,
                showBorder = true,
                showFoldoutHeader = true,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
                showBoundCollectionSize = true,
                headerTitle = "Target Property Names"
            };
            propertyNames.makeItem = () => new TextField()
            {
                value = "",
                multiline = false,
            };
            
            void PropertyValueChanged(ChangeEvent<string> evt)
            {
                var data = (evt.target as TextField)?.userData;
                if (data != null)
                {
                    _propertyNames[(int) data] = evt.newValue;
                }
            }
            propertyNames.bindItem = (item, index) =>
            {
                if (item is not TextField textField) return;
                textField.value = _propertyNames[index];
                textField.userData = index;
                textField.RegisterValueChangedCallback(PropertyValueChanged);
            };
            propertyNames.unbindItem = (item, index) =>
            {
                var textField = item as TextField;
                textField.UnregisterValueChangedCallback(PropertyValueChanged);
            };
            Add(propertyNames);
            
            var predictResult = new TextField()
            {
                name = "predictResult",
                multiline = true,
                isReadOnly = true,
                value = "",
                style =
                {
                    minHeight = 42,
                    maxHeight = 42*3
                }
            };
            Add(predictResult);
            
            propertyNames.RegisterCallback<BlurEvent>(evt =>
            {
                predictResult.SetValueWithoutNotify(SearchPredictProperties());
            });
            
            _sourceAnimationClips = sourceAnimationClips;
        }
        
        public override void ExecuteToAnimationClip(out AnimationClip dist, AnimationClip source)
        {
            var newCurveBindings = new List<EditorCurveBinding>();
            var newCurves = new List<AnimationCurve>();

            foreach (var binding in AnimationUtility.GetCurveBindings(source))
            {
                var curve =  AnimationUtility.GetEditorCurve(source,binding);
            
                // 検索文字列が含まれている場合は消す
                if (IsRemove(binding)) continue;
                
                // Propertyを個別に消すことができないので、消す対象でなかった場合は新規で追加し、値をコピーしないといけない
                var copyEditorCurveBinding = new EditorCurveBinding
                {
                    type = binding.type,
                    path = binding.path,
                    propertyName = binding.propertyName
                };
                newCurveBindings.Add(copyEditorCurveBinding);
                newCurves.Add(curve);
            }

            var newClip = new AnimationClip();
            AnimationUtility.SetEditorCurves(newClip, newCurveBindings.ToArray(), newCurves.ToArray());
            EditorUtility.SetDirty(newClip);
            dist = newClip;
        }

        private bool IsRemove(in EditorCurveBinding binding)
        {
            var isRemove = false;
            foreach (var propName in _propertyNames)
            {
                isRemove |= !propName.IsEmpty() && binding.propertyName.Contains(propName);
            }

            return isRemove;
        }

        private string SearchPredictProperties()
        {
            var result = new StringBuilder();
            foreach (var clip in _sourceAnimationClips)
            {
                var propCount = AnimationUtility.GetCurveBindings(clip).Count(binding => IsRemove(binding));
                result.AppendLine($"{clip.name} : Found {propCount} properties.");
            }
            return result.ToString();
        }
    }
}