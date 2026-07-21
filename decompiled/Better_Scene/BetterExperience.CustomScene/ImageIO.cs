using System;
using System.IO;
using System.Text;
using Assets;
using Assets._ReusableScripts.Memorias.Archivos;

namespace BetterExperience.CustomScene;

public class ImageIO
{
	public static readonly byte[] CHARACTER_SEPARATOR = Encoding.UTF8.GetBytes("TValle.Character.Data:");

	public static readonly byte[] POSE_SEPARATOR = Encoding.UTF8.GetBytes("TVallePortraitData");

	public static readonly byte[] OUTFIT_SEPARATOR = Encoding.UTF8.GetBytes("TValleOutfitData");

	public static string ReadPoseFromFile(string fname)
	{
		byte[] image = File.ReadAllBytes(fname);
		return ReadPoseFromMemory(image);
	}

	public static string ReadPoseFromMemory(byte[] image)
	{
		int num = FindSequence(image, 0, POSE_SEPARATOR) + POSE_SEPARATOR.Length;
		byte[] extradata = new byte[image.Length - num];
		Array.Copy(image, num, extradata, 0, extradata.Length);
		if (SaveLoadCharacters.CustomDataIsZipped(extradata))
		{
			return Zipiry.Unzip(extradata);
		}
		return Encoding.UTF8.GetString(extradata);
	}

	public static string ReadJsonPngFromMemory(byte[] image, byte[] separator)
	{
		int num = FindSequence(image, 0, separator) + separator.Length;
		byte[] extradata = new byte[image.Length - num];
		Array.Copy(image, num, extradata, 0, extradata.Length);
		SaveLoadCharacters.CustomDataIsZipped(extradata);
		return Encoding.UTF8.GetString(extradata);
	}

	private static int FindSequence(byte[] array, int start, byte[] sequence)
	{
		int num = array.Length - sequence.Length;
		byte b = sequence[0];
		while (start <= num)
		{
			if (array[start] == b)
			{
				int num2 = 1;
				while (true)
				{
					if (num2 != sequence.Length)
					{
						if (array[start + num2] != sequence[num2])
						{
							break;
						}
						num2++;
						continue;
					}
					return start;
				}
			}
			start++;
		}
		return -1;
	}
}
