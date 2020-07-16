﻿using System;
using System.Collections.Generic;
using System.Linq;
using Hai.ComboGesture.Scripts.Components;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using Object = UnityEngine.Object;

namespace Hai.ComboGesture.Scripts.Editor.Internal
{
    internal class ComboGestureCompilerInternal
    {
        private const string EmptyClipPath = "Assets/Hai/ComboGesture/Hai_ComboGesture_EmptyClip.anim";

        internal const string HaiGestureComboParamName = "_Hai_GestureComboValue";
        private const string HaiGestureComboAreEyesClosed = "_Hai_GestureComboAreEyesClosed";
        private const string HaiGestureComboDisableExpressionsParamName = "_Hai_GestureComboDisableExpressions";
        private const string HaiGestureComboDisableBlinkingOverrideParamName = "_Hai_GestureComboDisableBlinkingOverride";

        public const string GestureLeftWeight = "GestureLeftWeight";
        public const string GestureRightWeight = "GestureRightWeight";
        public const string GestureLeft = "GestureLeft";
        public const string GestureRight = "GestureRight";
        
        private readonly string _activityStageName;
        private readonly List<GestureComboStageMapper> _comboLayers;
        private readonly AnimatorController _animatorController;
        private readonly AnimationClip _customEmptyClip;

        public ComboGestureCompilerInternal(string activityStageName, List<GestureComboStageMapper> comboLayers, RuntimeAnimatorController animatorController, AnimationClip customEmptyClip)
        {
            _activityStageName = activityStageName;
            _comboLayers = comboLayers;
            _animatorController = (AnimatorController) animatorController;
            _customEmptyClip = customEmptyClip;
        }

        public void DoOverwriteAnimatorFxLayer()
        {
            EditorUtility.DisplayProgressBar("GestureCombo", "Starting", 0f);
            var emptyClip = GetOrCreateEmptyClip();

            CreateParameters();
            CreateOrReplaceController(emptyClip);
            CreateOrReplaceExpressionsView(emptyClip);
            CreateOrReplaceBlinkingOverrideView(emptyClip);

            ReapAnimator();

            EditorUtility.ClearProgressBar();
        }

        private void ReapAnimator()
        {
            if (AssetDatabase.GetAssetPath(_animatorController) == "")
            {
                return;
            }

            var allSubAssets = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(_animatorController));
        
            var reachableMotions = ConcatStateMachines()
                .SelectMany(machine => machine.states)
                .Select(state => state.state.motion)
                .ToList<Object>();
            Reap(allSubAssets, typeof(BlendTree), reachableMotions, o => o.name.StartsWith("autoBT_"));

            if (false) {
                UnsafelyReapAssetsLeftoverByUnsafeDelete(allSubAssets);
            }
        }

        private void UnsafelyReapAssetsLeftoverByUnsafeDelete(Object[] allSubAssets)
        {
            var reachableStates = ConcatStateMachines()
                .SelectMany(machine => machine.states)
                .Select(state => state.state)
                .ToList<Object>();
            var reachableMonoBehaviors = ConcatStateMachines()
                .SelectMany(machine => machine.states)
                .SelectMany(state => state.state.behaviours)
                .ToList<Object>();
            var reachableTransitions = ConcatStateMachines()
                .SelectMany(machine => machine.states)
                .SelectMany(state => state.state.transitions)
                .ToList<Object>();

            Reap(allSubAssets, typeof(AnimatorState), reachableStates, o => true);
            Reap(allSubAssets, typeof(StateMachineBehaviour), reachableMonoBehaviors, o => true);
            Reap(allSubAssets, typeof(AnimatorStateTransition), reachableTransitions, o => true);
        }

        private IEnumerable<AnimatorStateMachine> ConcatStateMachines()
        {
            return _animatorController.layers.Select(layer => layer.stateMachine)
                .Concat(_animatorController.layers.SelectMany(layer => layer.stateMachine.stateMachines).Select(machine => machine.stateMachine));
        }

        private static void Reap(Object[] allAssets, Type type, List<Object> existingAssets, Predicate<Object> predicate)
        {
            foreach (var o in allAssets)
            {
                if (o != null && (o.GetType() == type || o.GetType().IsSubclassOf(type)) && !existingAssets.Contains(o) && predicate.Invoke(o))
                {
                    AssetDatabase.RemoveObjectFromAsset(o);
                }
            }
        }

        private void CreateParameters()
        {
            CreateParamIfNotExists("GestureLeft", AnimatorControllerParameterType.Int);
            CreateParamIfNotExists("GestureRight", AnimatorControllerParameterType.Int);
            CreateParamIfNotExists("GestureLeftWeight", AnimatorControllerParameterType.Float);
            CreateParamIfNotExists("GestureRightWeight", AnimatorControllerParameterType.Float);
            CreateParamIfNotExists(HaiGestureComboParamName, AnimatorControllerParameterType.Int);
            CreateParamIfNotExists(HaiGestureComboDisableExpressionsParamName, AnimatorControllerParameterType.Int);
            CreateParamIfNotExists(HaiGestureComboDisableBlinkingOverrideParamName, AnimatorControllerParameterType.Int);
            CreateParamIfNotExists(HaiGestureComboAreEyesClosed, AnimatorControllerParameterType.Int);
            
            if (_activityStageName != "")
            {
                CreateParamIfNotExists(_activityStageName, AnimatorControllerParameterType.Int);
            }
        }

        private AnimationClip GetOrCreateEmptyClip()
        {
            var emptyClip = _customEmptyClip;
            if (emptyClip == null)
            {
                emptyClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(EmptyClipPath);
            }
            if (emptyClip == null)
            {
                emptyClip = GenerateEmptyClipAsset();
            }

            return emptyClip;
        }

        private static AnimationClip GenerateEmptyClipAsset()
        {
            var emptyClip = new AnimationClip();
            var settings = AnimationUtility.GetAnimationClipSettings(emptyClip);
            settings.loopTime = false;
            Keyframe[] keyframes = {new Keyframe(0, 0), new Keyframe(1 / 60f, 0)};
            var curve = new AnimationCurve(keyframes);
            emptyClip.SetCurve("_ignored", typeof(GameObject), "m_IsActive", curve);

            if (!AssetDatabase.IsValidFolder("Assets/Hai"))
            {
                AssetDatabase.CreateFolder("Assets", "Hai");
            }

            if (!AssetDatabase.IsValidFolder("Assets/Hai/GestureCombo"))
            {
                AssetDatabase.CreateFolder("Assets/Hai", "GestureCombo");
            }

            AssetDatabase.CreateAsset(emptyClip, EmptyClipPath);
            return emptyClip;
        }

        private void CreateParamIfNotExists(string paramName,
            AnimatorControllerParameterType type)
        {
            if (_animatorController.parameters.FirstOrDefault(param => param.name == paramName) == null)
            {
                _animatorController.AddParameter(paramName, type);
            }
        }

        private void CreateOrReplaceController(AnimationClip emptyClip)
        {
            EditorUtility.DisplayProgressBar("GestureCombo", "Clearing combo controller layer", 0f);
            var machine = ReinitializeAnimatorLayer("Hai_GestureCtrl", 0f);
        
            EditorUtility.DisplayProgressBar("GestureCombo", "Creating combo controller layer", 0f);
        
            for (var left = 0; left < 8; left++)
            {
                for (var right = left; right < 8; right++)
                {
                    var state = machine.AddState(left + "" + right, GridPosition(right, left));
                    state.writeDefaultValues = false;
                    state.motion = emptyClip;
                
                    var driver = state.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                    driver.parameters = new List<VRC_AvatarParameterDriver.Parameter>
                    {
                        new VRC_AvatarParameterDriver.Parameter {name = HaiGestureComboParamName, value = left * 10 + right}
                    };

                    {
                        var normal = machine.AddAnyStateTransition(state);
                        SetupImmediateTransition(normal);
                        normal.AddCondition(AnimatorConditionMode.Equals, left, "GestureLeft");
                        normal.AddCondition(AnimatorConditionMode.Equals, right, "GestureRight");
                    }
                    if (left != right)
                    {
                        var reverse = machine.AddAnyStateTransition(state);
                        SetupImmediateTransition(reverse);
                        reverse.AddCondition(AnimatorConditionMode.Equals, right, "GestureLeft");
                        reverse.AddCondition(AnimatorConditionMode.Equals, left, "GestureRight");
                    }
                }
            }
        }

        private void CreateOrReplaceExpressionsView(AnimationClip emptyClip)
        {
            EditorUtility.DisplayProgressBar("GestureCombo", "Clearing expressions layer", 0f);
            var machine = ReinitializeAnimatorLayer("Hai_GestureExp", 1f);

            var defaultState = machine.AddState("Default", GridPosition(-1, -1));
            defaultState.motion = emptyClip;
            CreateTransitionWhenExpressionsAreDisabled(machine, defaultState);
            if (_activityStageName != "") { 
                CreateTransitionWhenActivityIsOutOfBounds(machine, defaultState);
            }

            var activityManifests = CreateManifest(emptyClip);
            var combinator = new IntermediateCombinator(activityManifests);

            new GestureCExpressionCombiner(_animatorController, machine, combinator.IntermediateToTransition, _activityStageName)
                .Populate();
        }

        private static void CreateTransitionWhenExpressionsAreDisabled(AnimatorStateMachine machine, AnimatorState defaultState)
        {
            var transition = machine.AddAnyStateTransition(defaultState);
            SetupDefaultTransition(transition);
            transition.AddCondition(AnimatorConditionMode.NotEqual, 0, HaiGestureComboDisableExpressionsParamName);
        }

        private void CreateOrReplaceBlinkingOverrideView(AnimationClip emptyClip)
        {
            EditorUtility.DisplayProgressBar("GestureCombo", "Clearing eyes blinking override layer", 0f);
            var machine = ReinitializeAnimatorLayer("Hai_GestureBlinking", 0f);
            var suspend = CreateSuspendState(machine, emptyClip);

            var activityManifests = CreateManifest(emptyClip);
            var combinator = new IntermediateBlinkingCombinator(activityManifests);
            if (!combinator.IntermediateToBlinking.ContainsKey(IntermediateBlinkingGroup.NewMotion(true)) &&
                !combinator.IntermediateToBlinking.ContainsKey(IntermediateBlinkingGroup.NewBlend(true, false)) &&
                !combinator.IntermediateToBlinking.ContainsKey(IntermediateBlinkingGroup.NewBlend(false, true)))
            {
                return;
            }

            var enableBlinking = CreateBlinkingState(machine, VRC_AnimatorTrackingControl.TrackingType.Tracking, emptyClip);
            var disableBlinking = CreateBlinkingState(machine, VRC_AnimatorTrackingControl.TrackingType.Animation, emptyClip);
            CreateTransitionWhenBlinkingIsDisabled(enableBlinking, suspend);
            CreateTransitionWhenBlinkingIsDisabled(disableBlinking, suspend);
            CreateTransitionWhenActivityIsOutOfBounds(enableBlinking, suspend);
            CreateTransitionWhenActivityIsOutOfBounds(disableBlinking, suspend);
            CreateInternalParameterDriverWhenEyesAreOpen(enableBlinking);
            CreateInternalParameterDriverWhenEyesAreClosed(disableBlinking);
        
            foreach (var layer in _comboLayers)
            {
                var transition = suspend.AddTransition(enableBlinking);
                SetupDefaultTransition(transition);
                transition.AddCondition(AnimatorConditionMode.Equals, layer.stageValue, _activityStageName);
                transition.AddCondition(AnimatorConditionMode.Equals, 0, HaiGestureComboDisableBlinkingOverrideParamName);
            }
        
            new GestureCBlinkingCombiner(combinator.IntermediateToBlinking, _activityStageName)
                .Populate(enableBlinking, disableBlinking);
        }

        private static void CreateInternalParameterDriverWhenEyesAreOpen(AnimatorState enableBlinking)
        {
            var driver = enableBlinking.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            driver.parameters = new List<VRC_AvatarParameterDriver.Parameter>
            {
                new VRC_AvatarParameterDriver.Parameter {name = HaiGestureComboAreEyesClosed, value = 0}
            };
        }

        private static void CreateInternalParameterDriverWhenEyesAreClosed(AnimatorState disableBlinking)
        {
            var driver = disableBlinking.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            driver.parameters = new List<VRC_AvatarParameterDriver.Parameter>
            {
                new VRC_AvatarParameterDriver.Parameter {name = HaiGestureComboAreEyesClosed, value = 1}
            };
        }

        private static RawGestureManifest FromManifest(ComboGestureActivity activity, AnimationClip fallbackWhen00ClipIsNull)
        {
            var neutral = activity.anim00 ? activity.anim00 : fallbackWhen00ClipIsNull;
            return new RawGestureManifest(new[]
            {
                activity.anim00, activity.anim01, activity.anim02, activity.anim03, activity.anim04, activity.anim05, activity.anim06, activity.anim07,
                activity.anim11, activity.anim12, activity.anim13, activity.anim14, activity.anim15, activity.anim16, activity.anim17,
                activity.anim22, activity.anim23, activity.anim24, activity.anim25, activity.anim26, activity.anim27,
                activity.anim33, activity.anim34, activity.anim35, activity.anim36, activity.anim37,
                activity.anim44, activity.anim45, activity.anim46, activity.anim47,
                activity.anim55, activity.anim56, activity.anim57,
                activity.anim66, activity.anim67,
                activity.anim77
            }.Select(clip => clip ? clip : neutral).ToList(), activity.blinking, activity.transitionDuration);
        }

        private List<ActivityManifest> CreateManifest(AnimationClip emptyClip)
        {
            return _comboLayers
                .Select((mapper, layerOrdinal) => new ActivityManifest(mapper.stageValue, FromManifest(mapper.activity, emptyClip), layerOrdinal))
                .ToList();
        }

        private static void CreateTransitionWhenBlinkingIsDisabled(AnimatorState from, AnimatorState to)
        {
            var transition = from.AddTransition(to);
            SetupDefaultBlinkingTransition(transition);
            transition.AddCondition(AnimatorConditionMode.NotEqual, 0, HaiGestureComboDisableBlinkingOverrideParamName);
        }

        private static AnimatorState CreateSuspendState(AnimatorStateMachine machine, AnimationClip emptyClip)
        {
            var enableBlinking = machine.AddState("SuspendBlinking", GridPosition(1, 1));
            enableBlinking.motion = emptyClip;
            enableBlinking.writeDefaultValues = false;
            return enableBlinking;
        }

        private static AnimatorState CreateBlinkingState(AnimatorStateMachine machine, VRC_AnimatorTrackingControl.TrackingType type,
            AnimationClip emptyClip)
        {
            var enableBlinking = machine.AddState(type == VRC_AnimatorTrackingControl.TrackingType.Tracking ? "EnableBlinking" : "DisableBlinking", GridPosition(type == VRC_AnimatorTrackingControl.TrackingType.Tracking ? 0 : 2, 3));
            enableBlinking.motion = emptyClip;
            enableBlinking.writeDefaultValues = false;
            var tracking = enableBlinking.AddStateMachineBehaviour<VRCAnimatorTrackingControl>();
            tracking.trackingEyes = type;
            return enableBlinking;
        }

        private void CreateTransitionWhenActivityIsOutOfBounds(AnimatorStateMachine machine, AnimatorState defaultState)
        {
            var transition = machine.AddAnyStateTransition(defaultState);
            SetupDefaultTransition(transition);

            foreach (var layer in _comboLayers)
            {
                transition.AddCondition(AnimatorConditionMode.NotEqual, layer.stageValue, _activityStageName);
            }
        }
    
        private void CreateTransitionWhenActivityIsOutOfBounds(AnimatorState from, AnimatorState to)
        {
            var transition = from.AddTransition(to);
            SetupDefaultTransition(transition);

            foreach (var layer in _comboLayers)
            {
                transition.AddCondition(AnimatorConditionMode.NotEqual, layer.stageValue, _activityStageName);
            }
        }

        private static void SetupImmediateTransition(AnimatorStateTransition transition)
        {
            SetupCommonTransition(transition);

            transition.duration = 0;
            transition.orderedInterruption = true;
            transition.canTransitionToSelf = false;
        }

        private static void SetupDefaultTransition(AnimatorStateTransition transition)
        {
            SetupCommonTransition(transition);
        
            transition.duration = 0.1f; // There seems to be a quirk if the duration is 0 when using DisableExpressions, so use 0.1f instead
            transition.orderedInterruption = true;
            transition.canTransitionToSelf = true; // This is relevant as normal transitions may not check activity nor disabled expressions
        }

        private static void SetupDefaultBlinkingTransition(AnimatorStateTransition transition)
        {
            SetupCommonTransition(transition);
        
            transition.duration = 0;
            transition.orderedInterruption = false; // Is the difference relevant?!
            transition.canTransitionToSelf = false;
        }

        private static void SetupCommonTransition(AnimatorStateTransition transition)
        {
            transition.hasExitTime = false;
            transition.exitTime = 0;
            transition.hasFixedDuration = true;
            transition.offset = 0;
            transition.interruptionSource = TransitionInterruptionSource.None;
        }

        private AnimatorStateMachine ReinitializeAnimatorLayer(string layerName, float weightWhenCreating)
        {
            var layer = TryGetLayer(layerName);
            if (layer == null)
            {
                AddLayerWithWeight(layerName, weightWhenCreating);
                layer = TryGetLayer(layerName);
            }

            var machine = layer.stateMachine;
            SafeEraseStates(machine);

            machine.anyStatePosition = GridPosition(0, 7);
            machine.entryPosition = GridPosition(0, -1);
            machine.exitPosition = GridPosition(7, -1);

            return machine;
        }

        private static void EraseStates(AnimatorStateMachine machine)
        {
            // Running this may cause issues since it may not remove blend trees..?
        
            var states = machine.states;
            ArrayUtility.Clear(ref states);
            machine.states = states;

            var stateMachines = machine.stateMachines;
            ArrayUtility.Clear(ref stateMachines);
            machine.stateMachines = stateMachines;
        }

        private static void SafeEraseStates(AnimatorStateMachine machine)
        {
            foreach (var state in machine.states)
            {
                machine.RemoveState(state.state);
            }
        }

        private AnimatorControllerLayer TryGetLayer(string layerName)
        {
            return _animatorController.layers.FirstOrDefault(it => it.name == layerName);
        }

        private void AddLayerWithWeight(string layerName, float weightWhenCreating)
        {
            // This function is a replication of AnimatorController::AddLayer(string) behavior, in order to change the weight.
            // For some reason I cannot find how to change the layer weight after it has been created.
        
            var newLayer = new AnimatorControllerLayer();
            newLayer.name = _animatorController.MakeUniqueLayerName(layerName);
            newLayer.stateMachine = new AnimatorStateMachine();
            newLayer.stateMachine.name = newLayer.name;
            newLayer.stateMachine.hideFlags = HideFlags.HideInHierarchy;
            newLayer.defaultWeight = weightWhenCreating;
            if (AssetDatabase.GetAssetPath(_animatorController) != "")
            {
                AssetDatabase.AddObjectToAsset(newLayer.stateMachine,
                    AssetDatabase.GetAssetPath(_animatorController));
            }

            _animatorController.AddLayer(newLayer);
        }

        private static Vector3 GridPosition(int x, int y)
        {
            return new Vector3(x * 200 , y * 70, 0);
        }
    }

    class ActivityManifest
    {
        public int StageValue { get; }
        public RawGestureManifest Manifest { get; }
        public int LayerOrdinal { get; }

        public ActivityManifest(int stageValue, RawGestureManifest manifest, int layerOrdinal)
        {
            StageValue = stageValue;
            Manifest = manifest;
            LayerOrdinal = layerOrdinal;
        }
    }
}