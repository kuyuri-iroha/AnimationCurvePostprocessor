using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class KeyframeReductionTool : AssetPostprocessor
{
    [MenuItem("Assets/KeyFrame Reduction")]
    private static void KeyReduction()
    {
        Debug.Log("Keyframe Reduction Tool Started.");
        foreach (var obj in Selection.GetFiltered(typeof(AnimationClip), SelectionMode.Editable))
        {
            var path = AssetDatabase.GetAssetPath(obj);
            var animClip = (AnimationClip) AssetDatabase.LoadAssetAtPath(path, typeof(AnimationClip));
            foreach (var binding in AnimationUtility.GetCurveBindings(animClip).ToArray())
            {
                var curve = AnimationUtility.GetEditorCurve(animClip, binding);
                SmoothAnimationCurve(curve);
                AnimationUtility.SetEditorCurve(animClip, binding, curve);
            }

            var animClipName = $"{Path.GetDirectoryName(path)}/{Path.GetFileNameWithoutExtension(path)}";
            WriteAnimationCurve(animClip, animClipName);
            
            Debug.Log("Keyframe Reduction Tool Inprogress.");
        }
        Debug.Log("Keyframe Reduction Tool Ended.");
    }

    private static void SmoothAnimationCurve(AnimationCurve curve, float eps = 0.0001f)
    {
        var smoothed = new AnimationCurve();
        for (var i = 0; i < curve.keys.Length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Auto);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Auto);
            if (i % 2 == 0) smoothed.AddKey(curve.keys[i]);
        }
    }

    private static void WriteAnimationCurve(AnimationClip animClip, string animClipName)
    {
        var tmpName = animClip + "_tmp.anim";
        
        var copyClip = Object.Instantiate(animClip);
        AssetDatabase.CreateAsset(copyClip, tmpName);
        FileUtil.ReplaceFile(tmpName, animClipName + ".anim");
        AssetDatabase.DeleteAsset(tmpName);
        AssetDatabase.Refresh();
    }
}
