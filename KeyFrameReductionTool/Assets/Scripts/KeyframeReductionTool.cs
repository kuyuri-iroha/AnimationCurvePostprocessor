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
            foreach (var binding in AnimationUtility.GetCurveBindings(modifiedClip).ToArray())
            {
                var curve = AnimationUtility.GetEditorCurve(modifiedClip, binding);
                
                Reduction(out var reducedCurve, curve);
                //Smoothing(out var smoothedCurve, reducedCurve);
                
                Debug.Log($"{binding.path}/{binding.propertyName}: {reducedCurve.keys.Length}");
                AnimationUtility.SetEditorCurve(modifiedClip, binding, reducedCurve);
            }
            
            // AnimationClipの書き出し
            WriteAnimationCurve(modifiedClip, Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path));
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
    
    // Record範囲の決定と削除
    private static void DetectRecordRange(out RecordRageData[] recordRangeDatas, out AnimationCurve removedCurve,
        AnimationCurve sourceCurve, float brakingTime = 3f)
    {
        removedCurve = new AnimationCurve();
        
        var elapsedTime = 0f;
        var tmpRecordRangeDatas = new List<RecordRageData>();
        for (var i = 0; i < sourceCurve.keys.Length; i++)
        {
            //if( <= sourceCurve.keys[i].time - elapsedTime)
            elapsedTime = sourceCurve.keys[i].time;
            if (i % 2 == 0) removedCurve.AddKey(sourceCurve.keys[i]);
        }
        
        recordRangeDatas = tmpRecordRangeDatas.ToArray();
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
            var d = Mathf.Sqrt(CalcDistanceFromPointToLine(first, inKeyframes[i], last));
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

    private static float CalcDistanceFromPointToLine(Keyframe start, Keyframe mid, Keyframe end)
    {
        var a = end.time - start.time;
        var b = end.value - start.value;
        var a2 = a * a;
        var b2 = b * b;
        var r2 = a2 + b2;
        var tt = -(a * (start.time - mid.time) + b * (start.value - mid.value));

        if (tt < 0)
        {
            return Mathf.Pow(start.time - mid.time, 2) + Mathf.Pow(start.value - mid.value, 2);
        }

        if (r2 < tt)
        {
            return Mathf.Pow(end.time - mid.time, 2) + Mathf.Pow(end.value - mid.value, 2);
        }
        var f1 = a * (start.value - mid.value) - b * (start.time - mid.time);
        return Mathf.Pow(f1, 2) / r2;
    }

    // スムージング
    private static void Smoothing(out AnimationCurve smoothedCurve, AnimationCurve sourceCurve, float eps = 0.0001f)
    {
        smoothedCurve = new AnimationCurve();
        for (var i = 0; i < sourceCurve.keys.Length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(sourceCurve, i, AnimationUtility.TangentMode.Auto);
            AnimationUtility.SetKeyRightTangentMode(sourceCurve, i, AnimationUtility.TangentMode.Auto);
            if (i % 2 == 0) smoothedCurve.AddKey(sourceCurve.keys[i]);
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
