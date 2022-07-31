using System;
using Hai.ComboGesture.Scripts.Components;

namespace Hai.ComboGesture.Scripts.Editor.Internal
{
    public class CgeConflictPrevention
    {
        public bool ShouldGenerateExhaustiveAnimations { get; }
        public bool ShouldWriteDefaults { get; }

        private static readonly CgeConflictPrevention GenerateExhaustiveAnimationsWithWriteDefaults = new CgeConflictPrevention(true, true);
        private static readonly CgeConflictPrevention GenerateExhaustiveAnimationsWithoutWriteDefaults = new CgeConflictPrevention(true, false);

        private CgeConflictPrevention(bool shouldGenerateExhaustiveAnimations, bool shouldWriteDefaults)
        {
            ShouldGenerateExhaustiveAnimations = shouldGenerateExhaustiveAnimations;
            ShouldWriteDefaults = shouldWriteDefaults;
        }

        public static CgeConflictPrevention OfFxLayer(WriteDefaultsMode mode)
        {
            switch (mode)
            {
                case WriteDefaultsMode.Off:
                    return GenerateExhaustiveAnimationsWithoutWriteDefaults;
                case WriteDefaultsMode.On:
                    return GenerateExhaustiveAnimationsWithWriteDefaults;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        public static CgeConflictPrevention OfGestureLayer(WriteDefaultsMode compilerWriteDefaultsModeGesture, GestureLayerTransformCapture compilerGestureLayerTransformCapture)
        {
            return new CgeConflictPrevention(
                compilerGestureLayerTransformCapture == GestureLayerTransformCapture.CaptureDefaultTransformsFromAvatar,
                compilerWriteDefaultsModeGesture == WriteDefaultsMode.On);
        }
    }
}
