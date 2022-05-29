using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class KeyframeReductionTool : AssetPostprocessor
{
    private static readonly string[] RemoveProps = new string[]
    {
        "Position",
        "Scale"
    };

    private struct RecordRageData
    {
        public int startIndex;
        public int endIndex;
    }
    
    [MenuItem("Assets/KeyFrame Reduction")]
    private static void KeyReduction()
    {
        // 選択した各AnimationClipを処理
        foreach (var obj in Selection.GetFiltered(typeof(AnimationClip), SelectionMode.Editable))
        {
            var path = AssetDatabase.GetAssetPath(obj);
            var animClip = (AnimationClip) AssetDatabase.LoadAssetAtPath(path, typeof(AnimationClip));

            AnimationClip modifiedClip;
            RemoveProperties(out modifiedClip, animClip, RemoveProps);
            
            // 選択した各AnimationCurveを処理
            var newCurveBindings = new List<EditorCurveBinding>();
            var newCurves = new List<AnimationCurve>();
            foreach (var binding in AnimationUtility.GetCurveBindings(modifiedClip).ToArray())
            {
                var curve = AnimationUtility.GetEditorCurve(modifiedClip, binding);
                
                Reduction(out var reducedCurve, curve, 0.5f);
                Smoothing(out var smoothedCurve, reducedCurve);
                
                newCurveBindings.Add(binding);
                newCurves.Add(smoothedCurve);
            }
            
            // リダクション済みAnimationClipの生成
            var resultClip = new AnimationClip();
            AnimationUtility.SetEditorCurves(resultClip,newCurveBindings.ToArray(), newCurves.ToArray());
            EditorUtility.SetDirty(resultClip);
            resultClip.name = modifiedClip.name.Replace(".anim","_reduced.anim");
            
            // AnimationClipの書き出し
            WriteAnimationCurve(resultClip, Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path));
        }
    }
    
    // プロパティの削除
    private static void RemoveProperties(out AnimationClip removedClip, AnimationClip sourceClip, string[] propNames)
    {
        var refCurveBindings = AnimationUtility.GetCurveBindings(sourceClip);
        var newCurveBindings = new List<EditorCurveBinding>();
        var newCurves = new List<AnimationCurve>();
        
        var hitCount = 0;
        foreach (var binding in refCurveBindings)
        {
            var curve =  AnimationUtility.GetEditorCurve(sourceClip,binding);
            
            // 検索文字列が含まれている場合は消す
            var isRemove = false;
            foreach (var propName in propNames)
            {
                isRemove |= propName != "" && binding.propertyName.Contains(propName);
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

        removedClip = sourceClip;
        if (hitCount > 0)
        {
            var newClip = new AnimationClip();
            AnimationUtility.SetEditorCurves(newClip,newCurveBindings.ToArray(), newCurves.ToArray());
            EditorUtility.SetDirty(newClip);
            newClip.name = sourceClip.name.Replace(".anim","_removed.anim");
            removedClip = newClip;
        }
    }
    
    // リダクション
    private static void Reduction(out AnimationCurve reducedCurve, AnimationCurve sourceCurve, float eps = 1e-4f)
    {
        var keyframes = sourceCurve.keys;
        DouglasPeucker(out var reducedKeyframes, keyframes, eps);

        reducedCurve = new AnimationCurve(reducedKeyframes);
    }
    
    private static void DouglasPeucker(out Keyframe[] outKeyFrames, in Keyframe[] inKeyframes, float eps = 1e-4f)
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
            var d = CalcPerpendicularDistance(first, inKeyframes[i], last);
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
            DouglasPeucker(out var resKeys1, in keys1, eps);
            DouglasPeucker(out var resKeys2,in keys2, eps);
            
            outKeyFrames = resKeys1.Concat(resKeys2).ToArray();
        }
        else
        {
            outKeyFrames = new Keyframe[] {first, last};
        }
    }

    // イマイチうまく動かなかった（点が一直線上にあるときに消えなかった）
    private static float CalcDistanceSqFromPointToLine(Keyframe start, Keyframe mid, Keyframe end)
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
        return t switch
        {
            < 0 => PointDistanceSq(mid.time, mid.value, start.time, start.value),
            > 1 => PointDistanceSq(mid.time, mid.value, end.time, end.value),
            _ => PointDistanceSq(mid.time, mid.value, start.time + t * (end.time - start.time),
                start.value + t * (end.value - start.value))
        };
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

    // スムージング
    private static void Smoothing(out AnimationCurve smoothedCurve, AnimationCurve sourceCurve, float eps = 0.0001f)
    {
        smoothedCurve = new AnimationCurve();
        foreach (var keyframe in sourceCurve.keys)
        {
            smoothedCurve.AddKey(new Keyframe()
            {
                time = keyframe.time,
                value = keyframe.value,
                inTangent = keyframe.inTangent,
                outTangent = keyframe.outTangent,
                inWeight = keyframe.inWeight,
                outWeight = keyframe.outWeight,
                weightedMode = WeightedMode.Both,
            });
        }

        for (var i = 0; i < smoothedCurve.keys.Length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(smoothedCurve, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(smoothedCurve, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyBroken(smoothedCurve, i, true);
        }
    }
    
    // アニメーションクリップの保存
    private static void WriteAnimationCurve(AnimationClip animClip, string animClipDirectory, string animClipName)
    {
        var tmpName = $"{animClipDirectory}/{animClip.name}_tmp.anim";
        
        var copyClip = Object.Instantiate(animClip);
        AssetDatabase.CreateAsset(copyClip, tmpName);
        FileUtil.ReplaceFile(tmpName, $"{animClipDirectory}/{animClipName}_reduced.anim");
        AssetDatabase.DeleteAsset(tmpName);
        AssetDatabase.Refresh();
    }
}
