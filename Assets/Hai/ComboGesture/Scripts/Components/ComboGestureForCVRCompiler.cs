using System.Collections.Generic;
using UnityEngine;

namespace Hai.ComboGesture.Scripts.Components
{
    public class ComboGestureForCVRCompiler : MonoBehaviour
    {
        public string activityStageName;
        public List<GestureComboStageMapper> comboLayers;
        public RuntimeAnimatorController mainAnimatorController;
        public RuntimeAnimatorController folderToGenerateNeutralizedAssetsIn;
        public RuntimeAnimatorController assetContainer;
        public bool generateNewContainerEveryTime;

        public AnimationClip customEmptyClip;
        public float analogBlinkingUpperThreshold = 0.95f;

        public bool doNotGenerateBlinkingOverrideLayer;
        public bool doNotGenerateWeightCorrectionLayer;

        public AvatarMask expressionsAvatarMask;
        public AvatarMask logicalAvatarMask;

        public WriteDefaultsMode writeDefaultsMode = WriteDefaultsMode.On;
        public GestureLayerTransformCapture gestureLayerTransformCapture = GestureLayerTransformCapture.CaptureDefaultTransformsFromAvatar;
        public ConflictCvrLayerMode conflictLayerMode = ConflictCvrLayerMode.RemoveMuscles;

        public bool useViveAdvancedControlsForNonFistAnalog;

        public bool editorAdvancedFoldout;

        public AnimationClip ignoreParamList;
        public AnimationClip fallbackParamList;
        public bool doNotFixSingleKeyframes;

        public Animator avatarDescriptor;
        public bool bypassMandatoryAvatarDescriptor;

        // public ParameterMode parameterMode;
        public ComboGestureDynamics dynamics;

        public int totalNumberOfGenerations;
    }

    [System.Serializable]
    public struct GestureComboStageMapper
    {
        public GestureComboStageKind kind;
        public ComboGestureActivity activity; // This can be null even when the kind is an Activity
        public ComboGesturePuppet puppet; // This can be null
        public ComboGestureMassiveBlend massiveBlend; // This can be null
        public ComboGestureDynamics dynamics; // This can be null
        public int stageValue;
        public string booleanParameterName;
        public int internalVirtualStageValue; // This is overwritten by the compiler process

        public string SimpleName()
        {
            switch (kind)
            {
                case GestureComboStageKind.Activity:
                    return activity != null ? activity.name : "";
                case GestureComboStageKind.Puppet:
                    return puppet != null ? puppet.name : "";
                case GestureComboStageKind.Massive:
                    return massiveBlend != null ? massiveBlend.name : "";
                default:
                    return "";
            }
        }
    }

    [System.Serializable]
    public enum GestureComboStageKind
    {
        Activity, Puppet, Massive
    }

    [System.Serializable]
    public enum WriteDefaultsMode
    {
        Off, On
    }

    [System.Serializable]
    public enum GestureLayerTransformCapture
    {
        CaptureDefaultTransformsFromAvatar, DoNotCaptureTransforms
    }
    
    [System.Serializable]
    public enum ConflictCvrLayerMode
    {
        RemoveMuscles, Keep
    }

    // [System.Serializable]
    // public enum ParameterMode
    // {
        // MultipleBools, SingleInt
    // }
}
