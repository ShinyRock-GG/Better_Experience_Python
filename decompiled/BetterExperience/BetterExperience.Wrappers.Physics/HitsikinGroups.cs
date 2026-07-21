using BetterExperience.Utils;

namespace BetterExperience.Wrappers.Physics;

public static class HitsikinGroups
{
	public static readonly BitMask<PuppetReceiverPart> armL = BitMask<PuppetReceiverPart>.Of(PuppetReceiverPart.armL, PuppetReceiverPart.forearmL, PuppetReceiverPart.hand_L);

	public static readonly BitMask<PuppetReceiverPart> armR = BitMask<PuppetReceiverPart>.Of(PuppetReceiverPart.armR, PuppetReceiverPart.forearmR, PuppetReceiverPart.hand_R);

	public static readonly BitMask<PuppetReceiverPart> breastL = BitMask<PuppetReceiverPart>.Of(PuppetReceiverPart.breast000_L, PuppetReceiverPart.breast001_L);

	public static readonly BitMask<PuppetReceiverPart> breastR = BitMask<PuppetReceiverPart>.Of(PuppetReceiverPart.breast000_R, PuppetReceiverPart.breast001_R);

	public static readonly BitMask<PuppetReceiverPart> legL = BitMask<PuppetReceiverPart>.Of(PuppetReceiverPart.leg_L, PuppetReceiverPart.foot_L, PuppetReceiverPart.canilla_L);

	public static readonly BitMask<PuppetReceiverPart> legR = BitMask<PuppetReceiverPart>.Of(PuppetReceiverPart.leg_R, PuppetReceiverPart.foot_R, PuppetReceiverPart.canilla_R);

	public static BitMask<PuppetReceiverPart>[] Parts { get; } = new BitMask<PuppetReceiverPart>[6] { armL, armR, breastL, breastR, legL, legR };

	public static BitMask<PuppetReceiverPart> GroupByHitskinPart(PuppetReceiverPart part)
	{
		BitMask<PuppetReceiverPart>[] parts = Parts;
		for (int i = 0; i < parts.Length; i++)
		{
			BitMask<PuppetReceiverPart> bm = parts[i];
			if (bm.Contains(part))
			{
				return bm;
			}
		}
		return BitMask<PuppetReceiverPart>.Of(part);
	}
}
