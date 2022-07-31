using System;
using System.Collections.Generic;
using System.Linq;
using Hai.ComboGesture.Scripts.Components;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hai.ComboGesture.Scripts.Editor.Internal
{
    class AnimationNeutralizer
    {
        private readonly List<CgeManifestBinding> _originalBindings;
        private readonly ConflictCvrLayerMode _compilerConflictLayerMode;
        private readonly HashSet<CgeCurveKey> _ignoreCurveKeys;
        private readonly HashSet<CgeCurveKey> _ignoreObjectReferences;
        private readonly Dictionary<CgeCurveKey, float> _curveKeyToFallbackValue;
        private readonly Dictionary<CgeCurveKey, Object> _objectReferenceToFallbackValue;
        private readonly CgeAssetContainer _assetContainer;
        private readonly bool _useExhaustiveAnimations;
        private readonly AnimationClip _emptyClip;
        private readonly bool _doNotFixSingleKeyframes;
        private readonly Animator _avatarDescriptorNullable;

        public AnimationNeutralizer(List<CgeManifestBinding> originalBindings,
            ConflictCvrLayerMode compilerConflictLayerMode,
            AnimationClip compilerIgnoreParamList,
            AnimationClip compilerFallbackParamList,
            CgeAssetContainer assetContainer,
            bool useExhaustiveAnimations,
            AnimationClip emptyClip,
            bool doNotFixSingleKeyframes,
            Animator avatarDescriptorNullable)
        {
            _originalBindings = originalBindings;
            _compilerConflictLayerMode = compilerConflictLayerMode;
            _ignoreCurveKeys = compilerIgnoreParamList == null ? new HashSet<CgeCurveKey>() : ExtractAllCurvesOf(compilerIgnoreParamList);
            _ignoreObjectReferences = compilerIgnoreParamList == null ? new HashSet<CgeCurveKey>() : ExtractAllObjectReferencesOf(compilerIgnoreParamList);
            _curveKeyToFallbackValue = compilerFallbackParamList == null ? new Dictionary<CgeCurveKey, float>() : ExtractFirstKeyframeValueOf(compilerFallbackParamList);
            _objectReferenceToFallbackValue = compilerFallbackParamList == null ? new Dictionary<CgeCurveKey, Object>() : ExtractFirstKeyframeObjectReferenceOf(compilerFallbackParamList);
            _assetContainer = assetContainer;
            _useExhaustiveAnimations = useExhaustiveAnimations;
            _emptyClip = emptyClip;
            _doNotFixSingleKeyframes = doNotFixSingleKeyframes;
            _avatarDescriptorNullable = avatarDescriptorNullable;
        }

        private static HashSet<CgeCurveKey> ExtractAllCurvesOf(AnimationClip clip)
        {
            return new HashSet<CgeCurveKey>(AnimationUtility.GetCurveBindings(clip).Select(CgeCurveKey.FromBinding));
        }

        private static HashSet<CgeCurveKey> ExtractAllObjectReferencesOf(AnimationClip clip)
        {
            return new HashSet<CgeCurveKey>(AnimationUtility.GetObjectReferenceCurveBindings(clip).Select(CgeCurveKey.FromBinding));
        }

        private static Dictionary<CgeCurveKey, float> ExtractFirstKeyframeValueOf(AnimationClip clip)
        {
            var curveKeyToFallbackValue = new Dictionary<CgeCurveKey, float>();
            foreach (var editorCurveBinding in AnimationUtility.GetCurveBindings(clip))
            {
                var curve = AnimationUtility.GetEditorCurve(clip, editorCurveBinding);

                if (curve.keys.Length > 0)
                {
                    curveKeyToFallbackValue.Add(CgeCurveKey.FromBinding(editorCurveBinding), curve.keys[0].value);
                }
            }

            return curveKeyToFallbackValue;
        }

        private static Dictionary<CgeCurveKey, Object> ExtractFirstKeyframeObjectReferenceOf(AnimationClip clip)
        {
            var curveKeyToFallbackValue = new Dictionary<CgeCurveKey, Object>();
            foreach (var editorCurveBinding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                var curve = AnimationUtility.GetObjectReferenceCurve(clip, editorCurveBinding);

                if (curve.Length > 0)
                {
                    curveKeyToFallbackValue.Add(CgeCurveKey.FromBinding(editorCurveBinding), curve[0].value);
                }
            }

            return curveKeyToFallbackValue;
        }

        internal List<CgeManifestBinding> NeutralizeManifestAnimationsInsideContainer()
        {
            var allQualifiedAnimations = QualifyAllAnimations();
            var allApplicableCurveKeys = FindAllApplicableCurveKeys(new HashSet<AnimationClip>(allQualifiedAnimations.Select(animation => animation.Clip).ToList()));
            var allApplicableObjectReferences = FindAllApplicableObjectReferences(new HashSet<AnimationClip>(allQualifiedAnimations.Select(animation => animation.Clip).ToList()));
            var animationRemapping = GenerateNeutralizedAnimationsIntoContainer(allQualifiedAnimations, allApplicableCurveKeys, allApplicableObjectReferences);

            var neutralizeManifestAnimations = _originalBindings
                .Select(binding =>
                {
                    // Since BlendTrees of different manifests can have different qualifications for a same animation,
                    // for the sake of simplicity, we do not deduplicate blend trees across multiple Manifests
                    // even if there would have been cases where they could have been deduplicated.
                    var qualifiedAnimations = binding.Manifest.AllQualifiedAnimations().ToList();
                    var biTreesReferences = binding.Manifest.AllBlendTreesFoundRecursively()
                        .ToDictionary(
                            originalTree => originalTree,
                            originalTree => new BlendTree { hideFlags = HideFlags.HideInHierarchy }
                        );

                    var blendToRemappedBlend = binding.Manifest.AllBlendTreesFoundRecursively()
                        .ToDictionary(
                            originalTree => originalTree,
                            originalTree => RemapAnimationsOfBlendTree(originalTree, animationRemapping, qualifiedAnimations, biTreesReferences)
                        );
                    foreach (var blendTree in blendToRemappedBlend.Values)
                    {
                        _assetContainer.AddBlendTree(blendTree);
                    }

                    return RemapManifest(binding, animationRemapping, blendToRemappedBlend);
                })
                .ToList();

            return neutralizeManifestAnimations;
        }

        internal AvatarMask GenerateAvatarMaskInsideContainerIfApplicableOrNull()
        {
            var allQualifiedAnimations = QualifyAllAnimations();
            var allApplicableObjectReferences = FindAllApplicableObjectReferences(new HashSet<AnimationClip>(allQualifiedAnimations.Select(animation => animation.Clip).ToList()));
            var avatarMaskNullable = GenerateAvatarMaskIntoContainerIfApplicableOrNull(allApplicableObjectReferences);

            return avatarMaskNullable;
        }

        private AvatarMask GenerateAvatarMaskIntoContainerIfApplicableOrNull(HashSet<CgeCurveKey> allApplicableObjectReferences)
        {
            if (allApplicableObjectReferences.Count <= 0) return null;

            var mask = new AvatarMask {name = "zAutogeneratedAvm_AvatarMask"};

            MutateMaskToAllowNonEmptyAnimatedPaths(mask, allApplicableObjectReferences.Select(key => key.Path).ToList());

            _assetContainer.AddAvatarMask(mask);

            return mask;
        }

        private static BlendTree RemapAnimationsOfBlendTree(BlendTree originalTree, Dictionary<CgeQualifiedAnimation, AnimationClip> remapping, List<CgeQualifiedAnimation> qualifiedAnimations, Dictionary<BlendTree, BlendTree> biTreesReferences)
        {
            // Object.Instantiate(...) is triggering some weird issues about assertions failures.
            // Copy the blend tree manually
            var newTree = biTreesReferences[originalTree];
            newTree.name = "zAutogeneratedPup_" + originalTree.name + "_DO_NOT_EDIT";
            newTree.blendType = originalTree.blendType;
            newTree.blendParameter = originalTree.blendParameter;
            newTree.blendParameterY = originalTree.blendParameterY;
            newTree.minThreshold = originalTree.minThreshold;
            newTree.maxThreshold = originalTree.maxThreshold;
            newTree.useAutomaticThresholds = originalTree.useAutomaticThresholds;

            var copyOfChildren = originalTree.children;
            newTree.children = copyOfChildren
                .Select(childMotion =>
                {
                    var remappedMotion = RemapMotion(remapping, qualifiedAnimations, biTreesReferences, childMotion);
                    return new ChildMotion
                    {
                        motion = remappedMotion,
                        threshold = childMotion.threshold,
                        position = childMotion.position,
                        timeScale = childMotion.timeScale,
                        cycleOffset = childMotion.cycleOffset,
                        directBlendParameter = childMotion.directBlendParameter,
                        mirror = childMotion.mirror
                    };
                })
                .ToArray();

            return newTree;
        }

        private static Motion RemapMotion(Dictionary<CgeQualifiedAnimation, AnimationClip> remapping, List<CgeQualifiedAnimation> qualifiedAnimations, Dictionary<BlendTree, BlendTree> biTreesReferences, ChildMotion copyOfChild)
        {
            switch (copyOfChild.motion)
            {
                case AnimationClip clip:
                    return remapping[qualifiedAnimations.First(animation => animation.Clip == clip)];
                case BlendTree tree:
                    return biTreesReferences[tree];
                default:
                    return copyOfChild.motion;
            }
        }

        private HashSet<CgeQualifiedAnimation> QualifyAllAnimations()
        {
            return new HashSet<CgeQualifiedAnimation>(_originalBindings
                .SelectMany(binding => binding.Manifest.AllQualifiedAnimations())
                .ToList());
        }

        private static CgeManifestBinding RemapManifest(CgeManifestBinding manifestBinding, Dictionary<CgeQualifiedAnimation, AnimationClip> remapping, Dictionary<BlendTree, BlendTree> blendToRemappedBlend)
        {
            var remappedManifest = manifestBinding.Manifest.NewFromRemappedAnimations(remapping, blendToRemappedBlend);
            return CgeManifestBinding.Remapping(manifestBinding, remappedManifest);
        }

        private Dictionary<CgeQualifiedAnimation, AnimationClip> GenerateNeutralizedAnimationsIntoContainer(HashSet<CgeQualifiedAnimation> allQualifiedAnimations, HashSet<CgeCurveKey> allApplicableCurveKeys, HashSet<CgeCurveKey> allApplicableObjectReferences)
        {
            var technicalCommonEmptyClip = Object.Instantiate(_emptyClip);
            technicalCommonEmptyClip.name = "zAutogeneratedExp_EmptyTechnical_DO_NOT_EDIT";
            _assetContainer.AddAnimation(technicalCommonEmptyClip);

            var remapping = new Dictionary<CgeQualifiedAnimation, AnimationClip>();

            foreach (var qualifiedAnimation in allQualifiedAnimations)
            {
                var neutralizedAnimation = CopyAndNeutralize(qualifiedAnimation.Clip, allApplicableCurveKeys, allApplicableObjectReferences, _useExhaustiveAnimations);

                var neutralizedAnimationHasNothingInIt = AnimationUtility.GetCurveBindings(neutralizedAnimation).Length == 0;
                if (true)
                {
                    if (neutralizedAnimationHasNothingInIt)
                    {
                        Keyframe[] keyframes = {new Keyframe(0, 0), new Keyframe(1 / 60f, 0)};
                        var curve = new AnimationCurve(keyframes);
                        neutralizedAnimation.SetCurve("_ignored", typeof(GameObject), "m_IsActive", curve);
                    }

                    _assetContainer.AddAnimation(neutralizedAnimation);
                }

                remapping.Add(qualifiedAnimation, neutralizedAnimation);
            }

            return remapping;
        }

        private void MutateMaskToAllowNonEmptyAnimatedPaths(AvatarMask mask, List<string> potentiallyAnimatedPaths)
        {
            mask.transformCount = potentiallyAnimatedPaths.Count;
            for (var index = 0; index < potentiallyAnimatedPaths.Count; index++)
            {
                var path = potentiallyAnimatedPaths[index];
                mask.SetTransformPath(index, path);
                mask.SetTransformActive(index, true);
            }
        }

        private AnimationClip CopyAndNeutralize(AnimationClip animationClipToBePreserved, HashSet<CgeCurveKey> allApplicableCurveKeys, HashSet<CgeCurveKey> allApplicableObjectReferences, bool useExhaustiveAnimations)
        {
            var copyOfAnimationClip = new AnimationClip {name = "zAutogeneratedExp_" + animationClipToBePreserved.name + "_DO_NOT_EDIT"};

            AnimationUtility.SetAnimationClipSettings(copyOfAnimationClip, AnimationUtility.GetAnimationClipSettings(animationClipToBePreserved));
            CopyCurveKeys(animationClipToBePreserved, copyOfAnimationClip);
            CopyObjectReferences(animationClipToBePreserved, copyOfAnimationClip);

            if (useExhaustiveAnimations)
            {
                var thisAnimationPaths = AnimationUtility.GetCurveBindings(animationClipToBePreserved)
                    .Select(CgeCurveKey.FromBinding)
                    .ToList();
                AddMissingCurveKeys(allApplicableCurveKeys, thisAnimationPaths, copyOfAnimationClip);

                var thisAnimationObjectReferences = AnimationUtility.GetObjectReferenceCurveBindings(animationClipToBePreserved)
                    .Select(CgeCurveKey.FromBinding)
                    .ToList();
                AddMissingObjectReferences(allApplicableObjectReferences, thisAnimationObjectReferences, copyOfAnimationClip);
            }

            return copyOfAnimationClip;
        }

        private void CopyCurveKeys(AnimationClip animationClipToBePreserved, AnimationClip copyOfAnimationClip)
        {
            var originalBindings = AnimationUtility.GetCurveBindings(animationClipToBePreserved);
            foreach (var binding in originalBindings)
            {
                var curveKey = CgeCurveKey.FromBinding(binding);
                var canCopyCurve = _compilerConflictLayerMode == ConflictCvrLayerMode.Keep || !ShouldRemoveCurve(curveKey);
                if (canCopyCurve)
                {
                    var curve = AnimationUtility.GetEditorCurve(animationClipToBePreserved, binding);
                    if (!_doNotFixSingleKeyframes && curve.keys.Length == 1)
                    {
                        curve = new AnimationCurve(MakeSingleKeyframeIntoTwo(curve));
                    }

                    AnimationUtility.SetEditorCurve(copyOfAnimationClip, binding, curve);
                }
            }
        }

        private void CopyObjectReferences(AnimationClip animationClipToBePreserved, AnimationClip copyOfAnimationClip)
        {
            var canCopyCurve = _compilerConflictLayerMode == ConflictCvrLayerMode.Keep;
            if (!canCopyCurve)
            {
                return;
            }

            var originalBindings = AnimationUtility.GetObjectReferenceCurveBindings(animationClipToBePreserved);
            foreach (var binding in originalBindings)
            {
                var curve = AnimationUtility.GetObjectReferenceCurve(animationClipToBePreserved, binding);
                if (!_doNotFixSingleKeyframes && curve.Length == 1)
                {
                    curve = new[]
                    {
                        new ObjectReferenceKeyframe
                        {
                            time = 0 / 60f,
                            value = curve[0].value
                        },
                        new ObjectReferenceKeyframe
                        {
                            time = curve[0].time == 0f ? 1 / 60f : curve[0].time,
                            value = curve[0].value
                        }
                    };
                }

                AnimationUtility.SetObjectReferenceCurve(copyOfAnimationClip, binding, curve);
            }
        }

        private static Keyframe[] MakeSingleKeyframeIntoTwo(AnimationCurve curve)
        {
            var originalKeyframe = curve.keys[0];
            var originalKeyframeIsZero = originalKeyframe.time == 0;
            var newKeyframe = new Keyframe
            {
                time = originalKeyframeIsZero ? 1 / 60f : 0f,
                value = originalKeyframe.value,
                inTangent = originalKeyframe.inTangent,
                outTangent = originalKeyframe.outTangent,
                tangentMode = originalKeyframe.tangentMode,
                weightedMode = originalKeyframe.weightedMode,
                inWeight = originalKeyframe.inWeight,
                outWeight = originalKeyframe.outWeight,
            };
            return originalKeyframeIsZero ? new[] {originalKeyframe, newKeyframe} : new[] {newKeyframe, originalKeyframe};
        }

        private bool ShouldRemoveCurve(CgeCurveKey curveKey)
        {
            switch (_compilerConflictLayerMode)
            {
                case ConflictCvrLayerMode.RemoveMuscles:
                    return curveKey.IsMuscleCurve();
                case ConflictCvrLayerMode.Keep:
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void AddMissingCurveKeys(HashSet<CgeCurveKey> allApplicableCurveKeys, List<CgeCurveKey> thisAnimationPaths, AnimationClip copyOfAnimationClip)
        {
            foreach (var curveKey in allApplicableCurveKeys)
            {
                if (!thisAnimationPaths.Contains(curveKey))
                {
                    var fallbackValue = _curveKeyToFallbackValue.ContainsKey(curveKey) ? _curveKeyToFallbackValue[curveKey] : FindFallbackInAvatar(curveKey);

                    Keyframe[] keyframes = {new Keyframe(0, fallbackValue), new Keyframe(1 / 60f, fallbackValue)};
                    var curve = new AnimationCurve(keyframes);
                    copyOfAnimationClip.SetCurve(curveKey.Path, curveKey.Type, curveKey.PropertyName, curve);
                }
            }
        }

        private float FindFallbackInAvatar(CgeCurveKey curveKey)
        {
            if (_avatarDescriptorNullable == null)
            {
                return 0;
            }

            try
            {
                var found = AnimationUtility.GetFloatValue(_avatarDescriptorNullable.gameObject, new EditorCurveBinding
                {
                    path = curveKey.Path,
                    type = curveKey.Type,
                    propertyName = curveKey.PropertyName
                }, out var data);

                if (!found)
                {
                    return 0;
                }

                return data;
            }
            catch (Exception e)
            {
                // #342 "Invalid Type" may be thrown on DynamicBone m_Enabled if is it uninstalled, which may become common as PhysBones replaces it.
                // We return 0 for these cases.
                Debug.LogWarning($"Error while getting value at {curveKey.Path} {curveKey.Type} {curveKey.PropertyName} for {_avatarDescriptorNullable}. This will be ignored. {e.Message}");
                return 0;
            }
        }

        private void AddMissingObjectReferences(HashSet<CgeCurveKey> allApplicableObjectReferences, List<CgeCurveKey> thisObjectReferences, AnimationClip copyOfAnimationClip)
        {
            foreach (var curveKey in allApplicableObjectReferences)
            {
                if (!thisObjectReferences.Contains(curveKey))
                {
                    // No default-if-missing fallback is provided for material references
                    if (_objectReferenceToFallbackValue.ContainsKey(curveKey))
                    {
                        var fallbackValue = _objectReferenceToFallbackValue[curveKey];

                        ObjectReferenceKeyframe[] keyframes =
                        {
                            new ObjectReferenceKeyframe
                            {
                                time = 0f,
                                value = fallbackValue
                            },
                            new ObjectReferenceKeyframe
                            {
                                time = 1 / 60f,
                                value = fallbackValue
                            }
                        };
                        AnimationUtility.SetObjectReferenceCurve(copyOfAnimationClip, new EditorCurveBinding
                        {
                            path = curveKey.Path,
                            type = curveKey.Type,
                            propertyName = curveKey.PropertyName
                        }, keyframes);
                    }
                }
            }
        }

        private HashSet<CgeCurveKey> FindAllApplicableCurveKeys(HashSet<AnimationClip> allAnimationClips)
        {
            var allCurveKeysFromAnimations = allAnimationClips
                .SelectMany(AnimationUtility.GetCurveBindings)
                .Select(CgeCurveKey.FromBinding)
                .ToList();

            var curveKeys = new[]{allCurveKeysFromAnimations}
                .SelectMany(keys => keys)
                .Where(curveKey => !_ignoreCurveKeys.Contains(curveKey))
                .Where(curveKey =>
                {
                    switch (_compilerConflictLayerMode)
                    {
                        case ConflictCvrLayerMode.RemoveMuscles: return !curveKey.IsMuscleCurve();
                        case ConflictCvrLayerMode.Keep: return true;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                })
                .ToList();

            return new HashSet<CgeCurveKey>(curveKeys);
        }

        private HashSet<CgeCurveKey> FindAllApplicableObjectReferences(HashSet<AnimationClip> allAnimationClips)
        {
            var allObjectReferencesFromAnimations = allAnimationClips
                .SelectMany(AnimationUtility.GetObjectReferenceCurveBindings)
                .Select(CgeCurveKey.FromBinding)
                .ToList();

            var objectReferences = allObjectReferencesFromAnimations
                .Where(curveKey => !_ignoreObjectReferences.Contains(curveKey))
                .ToList();

            return new HashSet<CgeCurveKey>(objectReferences);
        }
    }
}
