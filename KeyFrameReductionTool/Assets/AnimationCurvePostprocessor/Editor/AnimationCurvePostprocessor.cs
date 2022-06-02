using System;
using System.IO;
using System.Text;
using Kuyuri.Tools.AnimationPostprocess;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kuyuri.Tools
{
    public class AnimationCurvePostprocessor : EditorWindow
    {
        public enum Method
        {
            RemoveProperties,
            RDPWithPerpendicularDistance,
            FritschCarlsonSmoothing,
            StinemanSmoothing,
        }
        
        [SerializeField]
        private VisualTreeAsset m_VisualTreeAsset = default;

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

            var sourceAnimationClip = root.Q<ObjectField>("sourceAnimationClip");
            var clipInfo = root.Q<TextField>("clipInfo");
            var methodSelector = root.Q<EnumField>("methodSelector");
            var methodParameterContainer = root.Q<VisualElement>("methodParameterContainer");
            var overWriteToggle = root.Q<Toggle>("overwriteToggle");
            var exportFileName = root.Q<TextField>("exportFileName");
            var executeButton = root.Q<Button>("executeButton");
            
            sourceAnimationClip.RegisterValueChangedCallback(evt =>
            {
                var clip = evt.newValue as AnimationClip;
                executeButton.SetEnabled(clip != null);
                if (clip == null)
                {
                    clipInfo.SetValueWithoutNotify(string.Empty);
                    return;
                }

                clipInfo.SetValueWithoutNotify(GetClipInfo(clip));
            });
            
            methodSelector.Init(Method.RDPWithPerpendicularDistance);
            Postprocess postprocess = new RDPReduction();
            methodParameterContainer.Add(postprocess);
            
            methodSelector.RegisterValueChangedCallback(evt =>
            {
                var method = (Method)evt.newValue;

                methodParameterContainer.Clear();
                
                switch (method)
                {
                    case Method.RemoveProperties :
                        postprocess = new RemoveProperties();
                        methodParameterContainer.Add(postprocess);
                        break;
                    
                    case Method.RDPWithPerpendicularDistance:
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
            
            executeButton.RegisterCallback<ClickEvent>(evt =>
            {
                var sourceClip = sourceAnimationClip.value as AnimationClip;
                var path = AssetDatabase.GetAssetPath(sourceClip);
                postprocess.ExecuteToAnimationClip(out var distClip, sourceClip);
                var newFilename = overWriteToggle.value || string.IsNullOrEmpty(exportFileName.value) ? Path.GetFileNameWithoutExtension(path) : exportFileName.value;
                
                WriteAnimationCurve(distClip, Path.GetDirectoryName(path), newFilename);
                
                sourceAnimationClip.value = distClip;
            });
        }
        
        private static string GetClipInfo(AnimationClip clip)
        {
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
        
        // アニメーションクリップの保存
        private static void WriteAnimationCurve(AnimationClip animClip, string animClipDirectory, string animClipName)
        {
            animClip.name = animClipName;
            AssetDatabase.CreateAsset(animClip, $"{animClipDirectory}/{animClipName}.anim");
            AssetDatabase.Refresh();
        }
    }
}
