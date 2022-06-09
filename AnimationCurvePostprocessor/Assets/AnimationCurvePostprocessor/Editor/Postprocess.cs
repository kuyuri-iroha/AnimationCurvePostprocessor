using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kuyuri.Tools.AnimationPostprocess
{
    public abstract class Postprocess : VisualElement
    {
        public abstract void ExecuteToAnimationClip(out AnimationClip dist, AnimationClip source, List<string> targetPropertyNames);
        
        // 処理対象のアニメーションプロパティかの判定
        public static bool IsTargetProperty(EditorCurveBinding binding, List<string> targetPropertyNames)
        {
            return targetPropertyNames.Exists(targetName => binding.propertyName.Equals(targetName));
        }
    }
}