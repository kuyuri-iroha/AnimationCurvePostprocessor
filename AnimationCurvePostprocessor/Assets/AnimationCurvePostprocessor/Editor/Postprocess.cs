using UnityEngine;
using UnityEngine.UIElements;

namespace Kuyuri.Tools.AnimationPostprocess
{
    public class Postprocess : VisualElement
    {
        public virtual void ExecuteToAnimationClip(out AnimationClip dist, AnimationClip source)
        {
            dist = null;
        }
    }
}