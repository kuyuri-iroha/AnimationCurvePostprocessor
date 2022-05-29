using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.VisualScripting;
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
                
                Reduction(out var reducedCurve, curve, 0.1f);
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

        reducedCurve = new AnimationCurve
        {
            keys = reducedKeyframes
        };
    }
    
    private static void DouglasPeucker(out Keyframe[] outKeyFrames, in Keyframe[] inKeyframes, float eps)
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
            
            outKeyFrames = resKeys1.Concat(resKeys2).DistinctBy(key => key.time).ToArray();
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
        var sourceKeyLength = sourceCurve.keys.Length;
        
        if(sourceKeyLength < 3)
        {
            smoothedCurve.keys = sourceCurve.keys.ToArray();
            return;
        }
        
        var x = new float[sourceKeyLength];
        var y = new float[sourceKeyLength];
        var xi = new float[sourceKeyLength * 2 - 1];
        for(var i = 0; i < sourceKeyLength - 1; i++)
        {
            x[i] = sourceCurve.keys[i].time;
            y[i] = sourceCurve.keys[i].value;
            xi[i * 2] = sourceCurve.keys[i].time;
            xi[i * 2 + 1] = (sourceCurve.keys[i + 1].time + sourceCurve.keys[i].time) * 0.5f;
        }
        x[sourceKeyLength - 1] = sourceCurve.keys[sourceKeyLength - 1].time;
        y[sourceKeyLength - 1] = sourceCurve.keys[sourceKeyLength - 1].value;
        xi[(sourceKeyLength - 1) * 2] = x[sourceKeyLength - 1];

        FritschCarlson(out var yi, x, y, xi);
        
        // Tangentでスムージング
        for (var i = 0; i < sourceKeyLength; i++)
        {
            var interpIndex = i * 2;
            var inTangent = 0 < i ? (yi[interpIndex] - yi[interpIndex - 1]) / (xi[interpIndex] - xi[interpIndex - 1]) : 0;
            var outTangent = i < sourceKeyLength - 1 ? (yi[interpIndex + 1] - yi[interpIndex]) / (xi[interpIndex + 1] - xi[interpIndex]) : 0;
            smoothedCurve.AddKey(new Keyframe()
            {
                time = sourceCurve.keys[i].time,
                value = sourceCurve.keys[i].value,
                inTangent = inTangent,
                outTangent = outTangent,
                inWeight = 0,
                outWeight = 0,
            });
        }
    }

    // Stineman補間
    // Ref. https://github.com/jdh2358/py4science/blob/master/examples/extras/steinman_interp.py
    private static void StinemanInterpolation(out float[] yi, float[] x, float[] y, float[] xi, float[] yp = null)
    {
        if (x.Length != y.Length)
        {
            yi = null;
            return;
        }

        // 各点の傾きを事前計算
        var dx = new float[x.Length - 1];
        var dy = new float[x.Length - 1];
        var dydx = new float[x.Length - 1];
        for(var i = 0; i < x.Length - 1; i++)
        {
            dx[i] = x[i + 1] - x[i];
            dy[i] = y[i + 1] - y[i];
            dydx[i] = dy[i] / dx[i];
        }
        
        // 元データの各点の傾きの算出
        if (yp == null)
        {
            yp = new float[x.Length];
            for (var i = 1; i < x.Length - 1; i++)
            {
                yp[i] = (dydx[i - 1] * dx[i] + dydx[i] * dx[i - 1]) / (dx[i] + dx[i - 1]);
            }
            yp[0] = 2.0f * dy[0] / dx[0] - yp[1];
            yp[^1] = 2.0f * dy[^1] / dx[^1] - yp[^2];
        }
        
        // yiの算出
        yi = new float[xi.Length];
        for (var i = 0; i < xi.Length; i++)
        {
            var xii = xi[i];
            var index = Array.BinarySearch(x, xii);
            index = Mathf.Max((index < 0 ? ~index : index) - 1, 0);

            var baseDydx = dydx[index];
            var baseX = x[index];
            var baseY = y[index];
            var nextX = x[index + 1];
            var yo = baseY + baseDydx * (xii - baseX);

            var dy1 = (yp[index] - baseDydx) * (xii - baseX);
            var dy2 = (yp[index + 1] - baseDydx) * (xii - nextX);
            var dy1dy2 = dy1 * dy2;

            yi[i] = yo + dy1dy2 * Math.Sign(dy1dy2) switch {
                -1 => (2.0f * xii - baseX - nextX) / ((dy1 - dy2) * (nextX - baseX)),
                0 => 0.0f,
                _ => 1.0f / (dy1 + dy2)
            };
        }
    }
    
    // Fritsch-Carlson補間
    // Ref. https://codereview.stackexchange.com/questions/73622/monotone-cubic-interpolation
    private static void FritschCarlson(out float[] yi, float[] xs, float[] ys, float[] x_interp)
    {
        var length = xs.Length;

        // Deal with length issues
        if (length != ys.Length && length == 0)
        {
            yi = null;
            return;
        }
        if (length == 1)
        {
            yi = new float[] { ys[0] };;
            return;
        }
        
        // Get consecutive differences and slopes
        var delta = new float[length - 1];
        var m = new float[length];

        for (int i = 0; i < length - 1; i++)
        {
            delta[i] = (ys[i + 1] - ys[i]) / (xs[i + 1] - xs[i]);
            if (i > 0)
            {
                m[i] = (delta[i - 1] + delta[i]) / 2;
            }
        }
        var toFix = new List<int>();
        for (int i = 1; i < length - 1; i++)
        {
            if ((delta[i] > 0 && delta[i - 1] < 0) || (delta[i] < 0 && delta[i - 1] > 0))
            {
                toFix.Add(i);
            }
        }
        foreach (var val in toFix)
        {
            m[val] = 0;
        }

        m[0] = delta[0];
        m[length - 1] = delta[length - 2];

        toFix.Clear();
        for (int i = 0; i < length - 1; i++)
        {
            if (delta[i] == 0)
            {
                toFix.Add(i);
            }
        }
        foreach (var val in toFix)
        {
            m[val] = 0;
            m[val + 1] = 0;
        }

        var alpha = new float[length - 1];
        var beta = new float[length - 1];
        var dist = new float[length - 1];
        var tau = new float[length - 1];
        for (int i = 0; i < length - 1; i++)
        {
            alpha[i] = m[i] / delta[i];
            beta[i] = m[i + 1] / delta[i];
            dist[i] = Mathf.Pow(alpha[i], 2) + Mathf.Pow(beta[i], 2);
            tau[i] = 3.0f/Mathf.Sqrt(dist[i]);
        }

        toFix.Clear();
        for (int i = 0; i < length - 1; i++)
        {
            if (dist[i] > 9)
            {
                toFix.Add(i);
            }
        }

        foreach (var val in toFix)
        {
            m[val] = tau[val] * alpha[val] * delta[val];
            m[val + 1] = tau[val] * beta[val] * delta[val];
        }

        yi = new float[x_interp.Length];
        int ind = 0;

        foreach (var x in x_interp)
        {
            int i;
            for (i = xs.Length - 2; i >= 0; --i)
            {
                if (xs[i] <= x)
                {
                    break;
                }
            }
            var h = xs[i + 1] - xs[i];
            var t = (x - xs[i])/h;
            var t2 = Mathf.Pow(t, 2);
            var t3 = Mathf.Pow(t, 3);
            var h00 = 2*t3 - 3*t2 + 1;
            var h10 = t3 - 2*t2 + t;
            var h01 = -2*t3 + 3*t2;
            var h11 = t3 - t2;
            yi[ind++] = h00*ys[i] + h10*h*m[i] + h01*ys[i + 1] + h11*h*m[i + 1];

            continue;
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
