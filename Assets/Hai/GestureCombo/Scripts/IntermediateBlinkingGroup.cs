﻿#if UNITY_EDITOR
public class IntermediateBlinkingGroup
{
    public bool Posing { get; }
    public bool Resting { get; }
    public IntermediateNature Nature { get; }

    public static IntermediateBlinkingGroup NewMotion(bool posing)
    {
        return new IntermediateBlinkingGroup(posing, posing, IntermediateNature.Motion);
    }

    public static IntermediateBlinkingGroup NewBlend(bool posing, bool resting)
    {
        return new IntermediateBlinkingGroup(posing, resting, IntermediateNature.Blend);
    }

    private IntermediateBlinkingGroup(bool posing, bool resting, IntermediateNature nature)
    {
        Posing = posing;
        Resting = resting;
        Nature = nature;
    }

    protected bool Equals(IntermediateBlinkingGroup other)
    {
        return Posing == other.Posing && Resting == other.Resting && Nature == other.Nature;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((IntermediateBlinkingGroup) obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = Posing.GetHashCode();
            hashCode = (hashCode * 397) ^ Resting.GetHashCode();
            hashCode = (hashCode * 397) ^ (int) Nature;
            return hashCode;
        }
    }
}
#endif
