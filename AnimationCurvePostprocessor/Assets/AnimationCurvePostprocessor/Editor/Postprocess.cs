using UnityEngine;
using UnityEngine.UIElements;

namespace Kuyuri.Tools.AnimationPostprocess
{
    public abstract class Postprocess : VisualElement
    {
        public abstract void ExecuteToAnimationClip(out AnimationClip dist, AnimationClip source);
    }
}