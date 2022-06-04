using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Kuyuri.Tools.AnimationPostprocess;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Kuyuri.Tools
{
    public class AnimationCurvePostprocessor : EditorWindow
    {
        public enum Method
        {
            RemoveProperties,
            RDPReduction,
            FritschCarlsonSmoothing,
            StinemanSmoothing,
        }

        private static string FilenameExpression = "$FILENAME";
        
        [SerializeField] private VisualTreeAsset m_VisualTreeAsset = default;

        private List<AnimationClip> _animationClips = new List<AnimationClip>(); 

        [MenuItem("Tools/AnimationCurvePostprocessor")]
        public static void ShowWindow()
        {
            var wnd = GetWindow<AnimationCurvePostprocessor>();
            wnd.titleContent = new GUIContent("Animation Curve Postprocessor");
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;

            VisualElement labelFromUxml = m_VisualTreeAsset.Instantiate();
            root.Add(labelFromUxml);

            var sourceAnimationClips = root.Q<ListView>("sourceAnimationClips");
            var clipInfo = root.Q<TextField>("clipInfo");
            var methodSelector = root.Q<EnumField>("methodSelector");
            var methodParameterContainer = root.Q<VisualElement>("methodParameterContainer");
            var overWriteToggle = root.Q<Toggle>("overwriteToggle");
            var exportFileName = root.Q<TextField>("exportFileName");
            var executeButton = root.Q<Button>("executeButton");

            sourceAnimationClips.itemsSource = _animationClips;
            sourceAnimationClips.makeItem = () => new ObjectField()
            {
                objectType = typeof(AnimationClip)
            };
            
            void ClipListValueChanged(ChangeEvent<Object> evt)
            {
                var data = (evt.target as ObjectField)?.userData;
                if (data == null) return;
                var clip = evt.newValue as AnimationClip;
                executeButton.SetEnabled(clip != null);
                if (clip != null)
                {
                    _animationClips[(int) data] = clip;
                    clipInfo.SetValueWithoutNotify(GetClipInfo(clip));
                }
                else
                {
                    _animationClips[(int) data] = null;
                    clipInfo.SetValueWithoutNotify(string.Empty);
                }
            }
            sourceAnimationClips.bindItem = (item, index) =>
            {
                if (item is not ObjectField objectField) return;
                objectField.value = _animationClips[index];
                objectField.userData = index;
                objectField.RegisterValueChangedCallback(ClipListValueChanged);
            };
            sourceAnimationClips.unbindItem = (item, index) =>
            {
                var textField = item as ObjectField;
                textField.UnregisterValueChangedCallback(ClipListValueChanged);
            };
            sourceAnimationClips.onSelectionChange += objects =>
            {
                var animationClips = objects.Cast<AnimationClip>().ToArray();
                var count = animationClips.Length;
                switch (count)
                {
                    case 0:
                        clipInfo.SetValueWithoutNotify(string.Empty);
                        break;
                    
                    case 1:
                        clipInfo.SetValueWithoutNotify(GetClipInfo(animationClips.FirstOrDefault()));
                        break;
                    
                    default:
                        clipInfo.SetValueWithoutNotify(GetClipsInfo(animationClips));
                        break;
                }
            };
            
            methodSelector.Init(Method.RDPReduction);
            Postprocess postprocess = new RDPReduction();
            methodParameterContainer.Add(postprocess);
            
            methodSelector.RegisterValueChangedCallback(evt =>
            {
                var method = (Method)evt.newValue;

                methodParameterContainer.Clear();
                
                switch (method)
                {
                    case Method.RemoveProperties :
                        postprocess = new RemoveProperties(_animationClips);
                        methodParameterContainer.Add(postprocess);
                        break;
                    
                    case Method.RDPReduction:
                        postprocess = new RDPReduction();
                        methodParameterContainer.Add(postprocess);
                        break;
                    
                    case Method.FritschCarlsonSmoothing:
                        postprocess = new FritschCarlsonSmoothing();
                        methodParameterContainer.Add(postprocess);
                        break;
                    
                    case Method.StinemanSmoothing:
                        methodParameterContainer.Add(new StinemanSmoothing());
                        break;
                    
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            });

            overWriteToggle.RegisterValueChangedCallback(evt =>
            {
                exportFileName.SetEnabled(!evt.newValue);
            });

            executeButton.SetEnabled(false);
            executeButton.RegisterCallback<ClickEvent>(evt =>
            {
                var format = new string(Enumerable.Range(1, _animationClips.Count.ToString().Length).Select(x => '0').ToArray());
                for(var i = 0; i < _animationClips.Count; i++)
                {
                    var sourceClip = _animationClips[i];
                    if(sourceClip == null) continue;
                    
                    // 処理の実行
                    postprocess.ExecuteToAnimationClip(out var distClip, sourceClip);
                    
                    // ファイル名の決定
                    var path = AssetDatabase.GetAssetPath(sourceClip);
                    var newFilename =
                        exportFileName.value.Replace(FilenameExpression, Path.GetFileNameWithoutExtension(path));
                    var fileIndex = _animationClips.Count == 1 ? "" : $"_{i.ToString(format)}";
                    if (overWriteToggle.value || string.IsNullOrEmpty(exportFileName.value))
                    {
                        newFilename = $"{Path.GetFileNameWithoutExtension(path)}";
                    }
                    else if (!exportFileName.value.Contains(FilenameExpression))
                    {
                        newFilename = $"{newFilename}{fileIndex}";
                    }
                
                    // 書き出し
                    WriteAnimationCurve(distClip, Path.GetDirectoryName(path), newFilename);

                    _animationClips[i] = distClip;
                }
                
                // 入れ替え
                sourceAnimationClips.RefreshItems();
            });
        }
        
        private static string GetClipInfo(AnimationClip clip)
        {
            if(null == clip) return string.Empty;
            
            var propertyCount = 0;
            var keyMax = 0;
            var keyMin = int.MaxValue;
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                keyMax = Mathf.Max(curve.length, keyMax);
                keyMin = Mathf.Min(curve.length, keyMin);
                propertyCount++;
            }
            
            var info = new StringBuilder();
            info.AppendLine($"Name: {clip.name}");
            info.AppendLine($"Frame rate: {clip.frameRate}");
            info.AppendLine($"Length: {clip.length}");
            info.AppendLine($"Property count: {propertyCount}");
            info.AppendLine($"Max keyframe: {keyMax}");
            info.AppendLine($"Min keyframe: {keyMin}");
            return info.ToString();
        }
        
        private static string GetClipsInfo(IEnumerable<AnimationClip> clips)
        {
            var frameRateMin = float.MaxValue;
            var frameRateMax = 0f;
            var lengthMin = float.MaxValue;
            var lengthMax = 0f;
            var propertyCountMin = int.MaxValue;
            var propertyCountMax = 0;
            var keyMin = int.MaxValue;
            var keyMax = 0;
            
            foreach (var clip in clips)
            {
                if(clip == null) continue;
                
                var propertyCount = 0;
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    var curve = AnimationUtility.GetEditorCurve(clip, binding);
                    keyMin = Mathf.Min(curve.length, keyMin);
                    keyMax = Mathf.Max(curve.length, keyMax);
                    propertyCount++;
                }
                
                frameRateMin = Mathf.Min(clip.frameRate, frameRateMin);
                frameRateMax = Mathf.Max(clip.frameRate, frameRateMax);
                lengthMin = Mathf.Min(clip.length, lengthMin);
                lengthMax = Mathf.Max(clip.length, lengthMax);
                propertyCountMin = Mathf.Min(propertyCount, propertyCountMin);
                propertyCountMax = Mathf.Max(propertyCount, propertyCountMax);
            }
            
            var info = new StringBuilder();
            info.AppendLine($"Name: -Multiple selected-");
            info.AppendLine($"Frame rate min: {frameRateMin}");
            info.AppendLine($"Frame rate max: {frameRateMax}");
            info.AppendLine($"Length min: {lengthMin}");
            info.AppendLine($"Length max: {lengthMax}");
            info.AppendLine($"Property count min: {propertyCountMin}");
            info.AppendLine($"Property count max: {propertyCountMax}");
            info.AppendLine($"Min keyframe: {keyMin}");
            info.AppendLine($"Max keyframe: {keyMax}");
            return info.ToString();
        }

        // アニメーションクリップの保存
        private static void WriteAnimationCurve(AnimationClip animClip, string animClipDirectory, string animClipName)
        {
            animClip.name = animClipName;
            AssetDatabase.CreateAsset(animClip, $"{animClipDirectory}/{animClipName}.anim");
            AssetDatabase.Refresh();
        }
    }
}
