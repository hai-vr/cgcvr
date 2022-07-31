using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hai.ComboGesture.Scripts.Components;
using Hai.ComboGesture.Scripts.Editor.Internal;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;
using BlendTree = UnityEditor.Animations.BlendTree;

namespace Hai.ComboGesture.Scripts.Editor.EditorUI
{
    [CustomEditor(typeof(ComboGestureForCVRCompiler))]
    public class ComboGestureCompilerEditor : UnityEditor.Editor
    {
        public ReorderableList comboLayersReorderableList;
        public SerializedProperty comboLayers;
        public SerializedProperty animatorController;
        public SerializedProperty activityStageName;
        public SerializedProperty customEmptyClip;
        public SerializedProperty analogBlinkingUpperThreshold;

        public SerializedProperty expressionsAvatarMask;

        public SerializedProperty writeDefaultsMode;
        public SerializedProperty gestureLayerTransformCapture;
        public SerializedProperty conflictLayerMode;
        public SerializedProperty ignoreParamList;
        public SerializedProperty fallbackParamList;
        public SerializedProperty folderToGenerateNeutralizedAssetsIn;

        public SerializedProperty avatarDescriptor;
        public SerializedProperty doNotFixSingleKeyframes;
        public SerializedProperty bypassMandatoryAvatarDescriptor;

        public SerializedProperty assetContainer;
        public SerializedProperty generateNewContainerEveryTime;

        public SerializedProperty editorAdvancedFoldout;

        public SerializedProperty useViveAdvancedControlsForNonFistAnalog;
        public SerializedProperty dynamics;

        private void OnEnable()
        {
            animatorController = serializedObject.FindProperty(nameof(ComboGestureForCVRCompiler.mainAnimatorController));
            activityStageName = serializedObject.FindProperty(nameof(ComboGestureForCVRCompiler.activityStageName));
            customEmptyClip = serializedObject.FindProperty(nameof(ComboGestureForCVRCompiler.customEmptyClip));
            analogBlinkingUpperThreshold = serializedObject.FindProperty(nameof(ComboGestureForCVRCompiler.analogBlinkingUpperThreshold));

            expressionsAvatarMask = serializedObject.FindProperty(nameof(ComboGestureForCVRCompiler.expressionsAvatarMask));

            writeDefaultsMode = serializedObject.FindProperty(nameof(ComboGestureForCVRCompiler.writeDefaultsMode));
            gestureLayerTransformCapture = serializedObject.FindProperty(nameof(ComboGestureForCVRCompiler.gestureLayerTransformCapture));
            conflictLayerMode = serializedObject.FindProperty(nameof(ComboGestureForCVRCompiler.conflictLayerMode));
            ignoreParamList = serializedObject.FindProperty(nameof(ComboGestureForCVRCompiler.ignoreParamList));
            fallbackParamList = serializedObject.FindProperty(nameof(ComboGestureForCVRCompiler.fallbackParamList));
            folderToGenerateNeutralizedAssetsIn = serializedObject.FindProperty(nameof(ComboGestureForCVRCompiler.folderToGenerateNeutralizedAssetsIn));

            comboLayers = serializedObject.FindProperty(nameof(ComboGestureForCVRCompiler.comboLayers));

            avatarDescriptor = serializedObject.FindProperty(nameof(ComboGestureForCVRCompiler.avatarDescriptor));
            doNotFixSingleKeyframes = serializedObject.FindProperty(nameof(ComboGestureForCVRCompiler.doNotFixSingleKeyframes));
            bypassMandatoryAvatarDescriptor = serializedObject.FindProperty(nameof(ComboGestureForCVRCompiler.bypassMandatoryAvatarDescriptor));

            assetContainer = serializedObject.FindProperty(nameof(ComboGestureForCVRCompiler.assetContainer));
            generateNewContainerEveryTime = serializedObject.FindProperty(nameof(ComboGestureForCVRCompiler.generateNewContainerEveryTime));

            useViveAdvancedControlsForNonFistAnalog = serializedObject.FindProperty(nameof(ComboGestureForCVRCompiler.useViveAdvancedControlsForNonFistAnalog));
            dynamics = serializedObject.FindProperty(nameof(ComboGestureForCVRCompiler.dynamics));

            // reference: https://blog.terresquall.com/2020/03/creating-reorderable-lists-in-the-unity-inspector/
            comboLayersReorderableList = new ReorderableList(
                serializedObject,
                serializedObject.FindProperty(nameof(ComboGestureForCVRCompiler.comboLayers)),
                true, true, true, true
            );
            comboLayersReorderableList.drawElementCallback = ComboLayersListElement;
            comboLayersReorderableList.drawHeaderCallback = ComboLayersListHeader;
            comboLayersReorderableList.onAddCallback = list =>
            {
                ReorderableList.defaultBehaviours.DoAddButton(list);

                if (comboLayers.arraySize <= 1)
                {
                    return;
                }

                var previous = comboLayers.GetArrayElementAtIndex(comboLayers.arraySize - 2).FindPropertyRelative(nameof(GestureComboStageMapper.stageValue)).intValue;
                var newlyAddedElement = comboLayers.GetArrayElementAtIndex(comboLayers.arraySize - 1);
                newlyAddedElement.FindPropertyRelative(nameof(GestureComboStageMapper.stageValue)).intValue = previous + 1;
                newlyAddedElement.FindPropertyRelative(nameof(GestureComboStageMapper.activity)).objectReferenceValue = null;
                newlyAddedElement.FindPropertyRelative(nameof(GestureComboStageMapper.puppet)).objectReferenceValue = null;
                serializedObject.ApplyModifiedProperties();
            };
            comboLayersReorderableList.elementHeight = EditorGUIUtility.singleLineHeight * 2.5f;

            _guideIcon16 = ComboGestureIcons.Instance.Guide16;
            _guideIcon32 = ComboGestureIcons.Instance.Guide32;

            editorAdvancedFoldout = serializedObject.FindProperty(nameof(ComboGestureForCVRCompiler.editorAdvancedFoldout));
        }

        private Texture _guideIcon16;
        private Texture _guideIcon32;

        public override void OnInspectorGUI()
        {
            if (AsCompiler().comboLayers == null)
            {
                AsCompiler().comboLayers = new List<GestureComboStageMapper>();
            }
            serializedObject.Update();
            var italic = new GUIStyle(GUI.skin.label) {fontStyle = FontStyle.Italic};

            if (GUILayout.Button("Switch language (English / 日本語)"))
            {
                CgeLocalization.CycleLocale();
            }

            if (CgeLocalization.IsEnglishLocaleActive())
            {
                EditorGUILayout.LabelField("");
            }
            else
            {
                EditorGUILayout.LabelField("一部の翻訳は正確ではありません。cge.jp.jsonを編集することができます。");
            }

            if (GUILayout.Button(new GUIContent(CgeLocale.CGEC_Documentation_and_tutorials, _guideIcon32)))
            {
                Application.OpenURL(CgeLocale.DocumentationUrl());
            }

            EditorGUILayout.PropertyField(avatarDescriptor, new GUIContent(CgeLocale.CGEC_Avatar_descriptor));
            var compiler = AsCompiler();
            EditorGUI.BeginDisabledGroup(comboLayers.arraySize <= 1);
            EditorGUILayout.PropertyField(activityStageName, new GUIContent(CgeLocale.CGEC_Parameter_Name));
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.PropertyField(dynamics, new GUIContent(CgeLocale.CGEC_MainDynamics));

            comboLayersReorderableList.DoLayoutList();

            bool ThereIsAnOverlap()
            {
                return compiler.comboLayers != null && comboLayers.arraySize != compiler.comboLayers.Select(mapper => mapper.stageValue).Distinct().Count();
            }

            bool ThereIsAPuppetWithNoBlendTree()
            {
                return compiler.comboLayers != null && compiler.comboLayers
                    .Where(mapper => mapper.kind == GestureComboStageKind.Puppet)
                    .Where(mapper => mapper.puppet != null)
                    .Any(mapper => !(mapper.puppet.mainTree is BlendTree));
            }

            bool ThereIsAMassiveBlendWithIncorrectConfiguration()
            {
                return compiler.comboLayers != null && compiler.comboLayers
                    .Where(mapper => mapper.kind == GestureComboStageKind.Massive)
                    .Where(mapper => mapper.massiveBlend != null)
                    .Any(mapper =>
                    {
                        var massiveBlend = mapper.massiveBlend;
                        switch (massiveBlend.mode)
                        {
                            case CgeMassiveBlendMode.Simple:
                                return massiveBlend.simpleZero == null || massiveBlend.simpleOne == null;
                            case CgeMassiveBlendMode.TwoDirections:
                                return massiveBlend.simpleZero == null || massiveBlend.simpleOne == null || massiveBlend.simpleMinusOne == null;
                            case CgeMassiveBlendMode.ComplexBlendTree:
                                return massiveBlend.blendTreeMoods.Count == 0
                                       || massiveBlend.blendTree == null
                                       || !(massiveBlend.blendTree is BlendTree)
                                       || ((BlendTree) massiveBlend.blendTree).children.Length != massiveBlend.blendTreeMoods.Count;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    });
            }

            bool ThereIsANullMapper()
            {
                return compiler.comboLayers != null && compiler.comboLayers.Any(mapper =>
                    mapper.kind == GestureComboStageKind.Activity && mapper.activity == null
                    || mapper.kind == GestureComboStageKind.Puppet && mapper.puppet == null
                    || mapper.kind == GestureComboStageKind.Massive && mapper.massiveBlend == null
                );
            }

            bool ThereIsNoActivityNameForMultipleActivities()
            {
                return comboLayers.arraySize >= 2 && (activityStageName.stringValue == null || activityStageName.stringValue.Trim() == "");
            }

            if (ThereIsAnOverlap())
            {
                EditorGUILayout.HelpBox(CgeLocale.CGEC_WarnValuesOverlap, MessageType.Error);
            }
            else if (ThereIsAPuppetWithNoBlendTree())
            {
                EditorGUILayout.HelpBox(CgeLocale.CGEC_WarnNoBlendTree, MessageType.Error);
            }
            else if (ThereIsAMassiveBlendWithIncorrectConfiguration())
            {
                EditorGUILayout.HelpBox(CgeLocale.CGEC_WarnNoMassiveBlend, MessageType.Error);
            }
            else if (ThereIsNoActivityNameForMultipleActivities())
            {
                EditorGUILayout.HelpBox(CgeLocale.CGEC_WarnNoActivityName, MessageType.Error);
            }
            else if (ThereIsANullMapper())
            {
                EditorGUILayout.HelpBox(CgeLocale.CGEC_WarnNoActivity, MessageType.Warning);
            }

            EditorGUILayout.LabelField(CgeLocale.CGEC_BackupFX, italic);
            EditorGUILayout.PropertyField(animatorController, new GUIContent(CgeLocale.CGEC_Animator_Controller));
            EditorGUILayout.PropertyField(writeDefaultsMode, new GUIContent(CgeLocale.CGEC_Write_Defaults));
            EditorGUILayout.PropertyField(gestureLayerTransformCapture, new GUIContent(CgeLocale.CGEC_Capture_Transforms_Mode));

            EditorGUILayout.Separator();

            EditorGUILayout.LabelField(CgeLocale.CGEC_Synchronization, EditorStyles.boldLabel);

            if (useViveAdvancedControlsForNonFistAnalog.boolValue)
            {
                EditorGUILayout.HelpBox(CgeLocale.CGEC_ViveAdvancedControlsWarning, MessageType.Error);
                EditorGUILayout.PropertyField(useViveAdvancedControlsForNonFistAnalog, new GUIContent("Use Vive Advanced Controls Analog"));
            }

            EditorGUI.BeginDisabledGroup(
                ThereIsNoAnimatorController() ||
                ThereIsNoActivity() ||
                ThereIsAnOverlap() ||
                ThereIsAPuppetWithNoBlendTree() ||
                ThereIsAMassiveBlendWithIncorrectConfiguration() ||
                ThereIsANullMapper() ||
                ThereIsNoActivityNameForMultipleActivities() ||
                ThereIsNoAvatarDescriptor()
            );

            bool ThereIsNoAnimatorController()
            {
                return animatorController.objectReferenceValue == null;
            }

            bool ThereIsNoActivity()
            {
                return comboLayers.arraySize == 0;
            }

            bool ThereIsNoAvatarDescriptor()
            {
                return !compiler.bypassMandatoryAvatarDescriptor
                       && compiler.avatarDescriptor == null;
            }

            if (GUILayout.Button(CgeLocale.CGEC_Synchronize_Animator_layers, GUILayout.Height(40)))
            {
                DoGenerateLayers();
                compiler.totalNumberOfGenerations++;
                if (compiler.totalNumberOfGenerations % 5 == 0)
                {
                    EditorUtility.DisplayDialog("ComboGestureExpressions", CgeLocale.CGEC_Slowness_warning, "OK", DialogOptOutDecisionType.ForThisSession, "CGE_SlownessWarning");
                }
            }
            if (compiler.totalNumberOfGenerations >= 5)
            {
                EditorGUILayout.HelpBox(CgeLocale.CGEC_Slowness_warning, MessageType.Warning);
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.HelpBox(
                CgeLocale.CGEC_SynchronizationConditionsV2, MessageType.Info);

            if (compiler.assetContainer != null) {
                EditorGUILayout.LabelField(CgeLocale.CGEC_Asset_generation, EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(assetContainer, new GUIContent(CgeLocale.CGEC_Asset_container));
            }

            EditorGUILayout.Separator();

            EditorGUILayout.LabelField(CgeLocale.CGEC_Other_tweaks, EditorStyles.boldLabel);
            // EditorGUILayout.PropertyField(analogBlinkingUpperThreshold, new GUIContent(CgeLocale.CGEC_Analog_fist_blinking_threshold, CgeLocale.CGEC_AnalogFist_Popup));

            editorAdvancedFoldout.boolValue = EditorGUILayout.Foldout(editorAdvancedFoldout.boolValue, CgeLocale.CGEC_Advanced);
            if (editorAdvancedFoldout.boolValue)
            {
                EditorGUILayout.LabelField("Fine tuning", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(customEmptyClip, new GUIContent("Custom 2-frame empty animation clip (optional)"));

                EditorGUILayout.Separator();

                EditorGUILayout.LabelField("Layer generation", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(expressionsAvatarMask, new GUIContent("Override Avatar Mask on Expressions layer"));

                EditorGUILayout.LabelField("Animation generation", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(assetContainer, new GUIContent(CgeLocale.CGEC_Asset_container));

                EditorGUI.BeginDisabledGroup(assetContainer.objectReferenceValue != null);
                EditorGUILayout.PropertyField(generateNewContainerEveryTime, new GUIContent("Don't keep track of newly generated containers"));
                EditorGUILayout.PropertyField(folderToGenerateNeutralizedAssetsIn, new GUIContent("Generate assets in the same folder as..."));
                if (animatorController.objectReferenceValue != null)
                {
                    EditorGUILayout.LabelField("Assets will be generated in:");
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.TextField(ResolveFolderToCreateNeutralizedAssetsIn((RuntimeAnimatorController)folderToGenerateNeutralizedAssetsIn.objectReferenceValue, (RuntimeAnimatorController)animatorController.objectReferenceValue));
                    EditorGUI.EndDisabledGroup();
                }
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.PropertyField(conflictLayerMode, new GUIContent("Muscles removal"));

                CpmRemovalWarning(true);
                EditorGUILayout.Separator();

                EditorGUILayout.LabelField("Fallback generation", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(ignoreParamList, new GUIContent("Ignored properties"));
                EditorGUILayout.PropertyField(fallbackParamList, new GUIContent("Fallback values"));
                EditorGUILayout.PropertyField(doNotFixSingleKeyframes, new GUIContent("Do not fix single keyframes"));
                EditorGUILayout.PropertyField(bypassMandatoryAvatarDescriptor, new GUIContent("Bypass mandatory avatar descriptor"));

                EditorGUILayout.LabelField("Special cases", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(useViveAdvancedControlsForNonFistAnalog, new GUIContent("Support non-Fist expression saving in Vive Advanced Controls"));

                EditorGUILayout.LabelField("Translations", EditorStyles.boldLabel);
                if (GUILayout.Button("(Debug) Print default translation file to console"))
                {
                    Debug.Log(CgeLocale.CompileDefaultLocaleJson());
                }
                if (GUILayout.Button("(Debug) Reload localization files"))
                {
                    CgeLocalization.ReloadLocalizations();
                }
            }
            else
            {
                CpmRemovalWarning(false);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void CpmRemovalWarning(bool advancedFoldoutIsOpen)
        {
            switch ((ConflictCvrLayerMode) conflictLayerMode.intValue)
            {
                case ConflictCvrLayerMode.RemoveMuscles:
                    if (advancedFoldoutIsOpen)
                    {
                        EditorGUILayout.HelpBox(@"Muscles will be removed.
This is the default behavior.", MessageType.Info);
                    }
                    break;
                case ConflictCvrLayerMode.Keep:
                        EditorGUILayout.HelpBox(@"Muscles will be kept.
This could cause problems some animations contains finger poses.", MessageType.Warning);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void DoGenerate()
        {
            var compiler = AsCompiler();

            var folderToCreateAssetIn = ResolveFolderToCreateNeutralizedAssetsIn(compiler.folderToGenerateNeutralizedAssetsIn, compiler.mainAnimatorController);
            var actualContainer = CreateContainerIfNotExists(compiler, folderToCreateAssetIn);
            if (actualContainer != null && compiler.assetContainer == null && !compiler.generateNewContainerEveryTime)
            {
                compiler.assetContainer = actualContainer.ExposeContainerAsset();
            }

            for (var index = 0; index < compiler.comboLayers.Count; index++)
            {
                var mapper = compiler.comboLayers[index];
                mapper.internalVirtualStageValue = mapper.stageValue;
                compiler.comboLayers[index] = mapper;
            }

            // if (compiler.avatarDescriptor.transform != null && (compiler.useGesturePlayableLayer || compiler.generatedAvatarMask != null))
            // {
                // CreateAvatarMaskAssetIfNecessary(compiler);
                // new CgeMaskApplicator(compiler.animatorController, compiler.generatedAvatarMask).UpdateMask();
            // }

            actualContainer.ExposeCgeAac().ClearPreviousAssets();
            new ComboGestureCompilerInternal(compiler, actualContainer).DoOverwriteAnimatorFxLayer();
        }

        private void DoGenerateLayers()
        {
            try
            {
                // var pfi = ProfilerDriver.GetPreviousFrameIndex(Time.frameCount);
                // Debug.Log($"PFI: {pfi}");
                Profiler.BeginSample("CGE");
                DoGenerate();
            }
            finally
            {
                Profiler.EndSample();
            }
        }

        private static CgeAssetContainer CreateContainerIfNotExists(ComboGestureForCVRCompiler compiler, string folderToCreateAssetIn)
        {
            return compiler.assetContainer == null ? CgeAssetContainer.CreateNew(folderToCreateAssetIn) : CgeAssetContainer.FromExisting(compiler.assetContainer);
        }

        private static string ResolveFolderToCreateNeutralizedAssetsIn(RuntimeAnimatorController preferredChoice, RuntimeAnimatorController defaultChoice)
        {
            var reference = preferredChoice == null ? defaultChoice : preferredChoice;

            var originalAssetPath = AssetDatabase.GetAssetPath(reference);
            var folder = originalAssetPath.Replace(Path.GetFileName(originalAssetPath), "");
            return folder;
        }

        private ComboGestureForCVRCompiler AsCompiler()
        {
            return (ComboGestureForCVRCompiler) target;
        }

        private void ComboLayersListElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var element = comboLayersReorderableList.serializedProperty.GetArrayElementAtIndex(index);

            EditorGUI.PropertyField(
                new Rect(rect.x, rect.y, 70, EditorGUIUtility.singleLineHeight),
                element.FindPropertyRelative(nameof(GestureComboStageMapper.kind)),
                GUIContent.none
            );

            var kind = (GestureComboStageKind) element.FindPropertyRelative(nameof(GestureComboStageMapper.kind)).intValue;
            var compiler = AsCompiler();
            var onlyOneLayer = compiler.comboLayers.Count <= 1;
            var trailingWidth = onlyOneLayer ? 0 : rect.width * 0.2f;
            EditorGUI.PropertyField(
                new Rect(rect.x + 70, rect.y, rect.width - 70 - 20 - trailingWidth, EditorGUIUtility.singleLineHeight),
                PropertyForKind(kind, element),
                GUIContent.none
            );

            if (!onlyOneLayer)
            {
                EditorGUI.PropertyField(
                    new Rect(rect.x + rect.width - 20 - trailingWidth, rect.y, trailingWidth, EditorGUIUtility.singleLineHeight),
                    element.FindPropertyRelative(nameof(GestureComboStageMapper.stageValue)),
                    GUIContent.none
                );
            }

            EditorGUI.LabelField(
                new Rect(rect.x, rect.y + EditorGUIUtility.singleLineHeight, 110, EditorGUIUtility.singleLineHeight),
                new GUIContent(CgeLocale.CGEC_Dynamics)
            );
            EditorGUI.PropertyField(
                new Rect(rect.x + 110, rect.y + EditorGUIUtility.singleLineHeight, rect.width - 110 - 20 - trailingWidth, EditorGUIUtility.singleLineHeight),
                element.FindPropertyRelative(nameof(GestureComboStageMapper.dynamics)),
                GUIContent.none);
        }

        private static SerializedProperty PropertyForKind(GestureComboStageKind kind, SerializedProperty element)
        {
            switch (kind)
            {
                case GestureComboStageKind.Puppet:
                    return element.FindPropertyRelative(nameof(GestureComboStageMapper.puppet));
                case GestureComboStageKind.Activity:
                    return element.FindPropertyRelative(nameof(GestureComboStageMapper.activity));
                case GestureComboStageKind.Massive:
                    return element.FindPropertyRelative(nameof(GestureComboStageMapper.massiveBlend));
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        private void ComboLayersListHeader(Rect rect)
        {
            EditorGUI.LabelField(new Rect(rect.x, rect.y, rect.width - 70 - 51, EditorGUIUtility.singleLineHeight), CgeLocale.CGEC_Mood_sets);
            if (AsCompiler().comboLayers.Count > 1)
            {
                EditorGUI.LabelField(new Rect(rect.x + rect.width - 70 - 51, rect.y, 50 + 51, EditorGUIUtility.singleLineHeight), CgeLocale.CGEC_Parameter_Value);
            }
        }
    }
}
