using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Assets;
using Assets._ReusableScripts.CuchiCuchi.Chars.Alteradores.Mapas.Genetica.NPCs.Handlers;
using Assets._ReusableScripts.Genetica;
using Assets._ReusableScripts.Genetica.NPCs;
using Assets._ReusableScripts.Tiempo;
using Assets.Productos.Juegos.Reception.Scripts.AutoRatingsProfiles;
using Assets.Productos.Juegos.Reception.Scripts.Genetica.Eventos;
using Assets.Productos.Juegos.Reception.Scripts.TimepoEventosDeJuego;
using Assets.SingletonesAndSystemasGlobales.AbstractLayer.Globales;
using BetterExperience.Features.AlternativeGenetics;
using BetterExperience.Features.Overlay;
using BetterExperience.GameScopes;
using BetterExperience.HarmonyPatches;
using BetterExperience.Utils;
using BetterExperience.Wrappers.Pools;

namespace BetterExperience.Features.AlternativeRating;

internal class AutoratingService : SessionService
{
	private static byte[] charasep = Encoding.UTF8.GetBytes("TValle.Character.Data:");

	private AlternativeGeneticsService alternativeGenetics;

	private string[,] ratingGroups = new string[21, 3]
	{
		{ "summarizing", null, "personality" },
		{ "angerManagement", null, "personality" },
		{ "servicing", null, "personality" },
		{ "slutness", null, "personality" },
		{ "painTolerance", null, "personality" },
		{ "exhibitionism", null, "personality" },
		{ "skin", null, "appearance" },
		{ "crotch", null, "appearance" },
		{ "buttocks", null, "appearance" },
		{ "hair", null, "appearance" },
		{ "breast", null, "appearance" },
		{ "height", "@body", "appearance" },
		{ "body", "@body", "appearance" },
		{ "waist_hip", "@body", "appearance" },
		{ "arms", "@body", "appearance" },
		{ "legs", "@body", "appearance" },
		{ "head", "@face", "appearance" },
		{ "face", "@face", "appearance" },
		{ "eyes", "@face", "appearance" },
		{ "nose", "@face", "appearance" },
		{ "mouth", "@face", "appearance" }
	};

	private Dictionary<string, AutoratingProfile> loadedProfiles = new Dictionary<string, AutoratingProfile>();

	private string autoratingdir;

	private AutoratingProfile profile;

	private AutoPulling autoPulling = new AutoPulling();

	private OverlayService overlay;

	private bool wasPulled;

	public AutoratingFeature Feature { get; set; }

	private string GetCurrentAutoratingProfile()
	{
		// PiscinasDeEventosDeEntrevista, HorariosNormalesDeEntrevistas, AutoRatings are [Obsolete] in SMA 23.1
		return null;
	}

	public override void OnStart()
	{
		base.OnStart();
		base.Session.PoolManager.OnGuestLoaded.Add(ApplyPulling, base.Scope);
		SMAGlobalPatches.AfterAutorating.Add(ApplyScoring, base.Scope);
		PersistenceService persistence = Lookup<PersistenceService>();
		autoratingdir = GameFolders.Obtener(GameFolders.Tipo.autoRatingPortraitsV2);
		if (!Directory.Exists(autoratingdir))
		{
			Directory.CreateDirectory(autoratingdir);
		}
		alternativeGenetics = TryLookup<AlternativeGeneticsService>();
		base.Session.OnGuestReady += delegate
		{
			base.Session.Guest.SynchronizeCharacterWithInstance();
		};
		overlay = Lookup<OverlayService>();
	}

	private void LoadProfile(string dir, AutoratingProfile profile)
	{
		if (!Directory.Exists(dir))
		{
			Directory.CreateDirectory(dir);
		}
		for (int i = 0; i < ratingGroups.GetLength(0); i++)
		{
			string g0 = ratingGroups[i, 0];
			string g1 = ratingGroups[i, 1];
			string g2 = ratingGroups[i, 2];
			string path = ((g1 != null) ? Path.Combine(dir, g2, g1, g0) : Path.Combine(dir, g2, g0));
			string[] files = Array.Empty<string>();
			if (Directory.Exists(path))
			{
				files = Directory.GetFiles(path);
			}
			else
			{
				Directory.CreateDirectory(path);
			}
			if (files.Length == 0)
			{
				path = Path.Combine(dir, g2, g0);
				if (Directory.Exists(path))
				{
					files = Directory.GetFiles(path);
				}
				if (files.Length == 0)
				{
					path = Path.Combine(dir, g2);
					if (Directory.Exists(path))
					{
						files = Directory.GetFiles(path);
					}
				}
			}
			if (files.Length == 0 && Directory.Exists(dir))
			{
				files = Directory.GetFiles(dir);
			}
			string[] array = files;
			foreach (string f in array)
			{
				if (f.ToLower().EndsWith(".png"))
				{
					byte[] bytes = File.ReadAllBytes(f);
					string json = ReadJsonPngFromMemory(bytes, charasep);
					MemoriaJsonGenerica memoriaJsonGenerica = new MemoriaJsonGenerica();
					memoriaJsonGenerica.root.Load(json);
					ISujetoIdentificableNpc npc = MemoriaDeSujetosNpcFemenina.LeerNpcEnMemoriaFirstOrDefault(memoriaJsonGenerica);
					profile.Templates.GetValueOrAdd(g0, () => new List<ISujetoIdentificableNpc>()).Add(npc);
				}
			}
		}
	}

	private void ApplyPulling(GuestInstance obj)
	{
		if (!CheckProfile(obj) || (obj.Level != 0 && !Feature.PullNonZeroLevels))
		{
			return;
		}
		autoPulling.DeterminismFactor = Feature.DeterministicPull;
		if (autoPulling.ApplyProfile(obj, profile, Feature.AppearancePull, Feature.PersonalityPull))
		{
			if (base.Session.Guest != null && base.Session.Guest.GuestInstance == obj)
			{
				base.Session.Guest.SynchronizeCharacterWithInstance();
			}
			wasPulled = true;
		}
	}

	private bool CheckProfile(GuestInstance instance = null)
	{
		if (base.Session.Guest == null && instance == null)
		{
			return false;
		}
		if (instance == null)
		{
			instance = base.Session.Guest.GuestInstance;
		}
		string currentProfile = GetCurrentAutoratingProfile();
		if (currentProfile == null)
		{
			return false;
		}
		profile = loadedProfiles.GetValueOrAdd(currentProfile, () => InstantiateProfile(currentProfile, instance.Pool));
		return true;
	}

	private AutoratingProfile InstantiateProfile(string currentProfile, GuestPool pool)
	{
		AutoratingProfile profile = new AutoratingProfile();
		profile.Name = currentProfile;
		LoadProfile(Path.Combine(autoratingdir, currentProfile), profile);
		if (!profile.Initialized)
		{
			profile.Initialize(pool, alternativeGenetics != null);
		}
		return profile;
	}

	internal void ApplyScoring(StringKeyFloatValueDictionary appearance, StringKeyFloatValueDictionary personality)
	{
		logger.Info("Autorate!");
		if (!CheckProfile())
		{
			return;
		}
		Dictionary<string, float> score = profile.Score(base.Session.Guest.GuestInstance.ExtractAll());
		string[] array = score.Keys.ToArray();
		foreach (string k in array)
		{
			score[k] = 1f - score[k];
		}
		if (alternativeGenetics != null)
		{
			UnsupervisedCleanup(score);
		}
		ISujetoIdentificableNpc sujeto = base.Session.Guest.GuestInstance.Instance;
		foreach (IConjuntoDeGenes c in sujeto.aparienciaFisica.conjuntos)
		{
			if (score.TryGetValue(c.conjuntoName, out var fitness))
			{
				float rating = Extensions.GetValueOrDefault(appearance, c.conjuntoName, 0f);
				float totalWeight = Feature.AppearanceRatingWeight + Feature.AppearanceScoreWeight;
				float finalScore = Feature.AppearanceRatingWeight * rating + Feature.AppearanceScoreWeight * fitness;
				finalScore = ((!(totalWeight > 0f)) ? 0f : (finalScore / totalWeight));
				logger.Info("Score {0} {1}x{2} + {3}x{4} = {5}", c.conjuntoName, Feature.AppearanceRatingWeight, rating, Feature.AppearanceScoreWeight, fitness, finalScore);
				c.fitnes = finalScore;
				appearance[c.conjuntoName] = finalScore;
			}
		}
		foreach (IConjuntoDeGenes c2 in sujeto.personalidad.conjuntos)
		{
			if (score.TryGetValue(c2.conjuntoName, out var fitness2))
			{
				float rating2 = Extensions.GetValueOrDefault(personality, c2.conjuntoName, 0f);
				float totalWeight2 = Feature.PersonalityRatingWeight + Feature.PersonalityScoreWeight;
				float finalScore2 = Feature.PersonalityRatingWeight * rating2 + Feature.PersonalityScoreWeight * fitness2;
				finalScore2 = ((!(totalWeight2 > 0f)) ? 0f : (finalScore2 / totalWeight2));
				logger.Info("Score {0} {1}x{2} + {3}x{4} = {5}", c2.conjuntoName, Feature.PersonalityRatingWeight, rating2, Feature.PersonalityScoreWeight, fitness2, finalScore2);
				c2.fitnes = finalScore2;
				personality[c2.conjuntoName] = finalScore2;
			}
		}
		float[] aFitness = appearance.Values.Select((float x) => x).ToArray();
		float aaF = aFitness.Average();
		float[] pFitness = personality.Values.Select((float x) => x).ToArray();
		float apF = pFitness.Average();
		string pname = profile.Name;
		overlay.InfoMessage(string.Format("Scoring {2} ({3}). Appearance: {0:0.00} Personality: {1:0.00}" + (wasPulled ? ". Pulled." : ""), aaF * 10f, apF * 10f, pname, base.Session.Guest.GuestInstance.Level + 1));
		wasPulled = false;
	}

	private void UnsupervisedCleanup(Dictionary<string, float> score)
	{
		alternativeGenetics.Factory.Groups.Values.ForEach(delegate(PoolingGroup group)
		{
			group.Pools.Values.ForEach(delegate(GenePool pool)
			{
				float num = pool.Data.Error / 10f;
				float num2 = num;
				if (pool.GetGeneration(GeneGeneration.Mature).Count >= 5)
				{
					num2 -= 1f;
				}
				if (pool.Data.Settings.Groups != null)
				{
					foreach (string current in pool.Data.Settings.Groups)
					{
						if (score.TryGetValue(current, out var value) && value < num2)
						{
							score[current] = 0f;
						}
					}
				}
			});
		});
	}

	private static string ReadJsonPngFromMemory(byte[] image, byte[] separator)
	{
		int num = FindSequence(image, 0, separator) + separator.Length;
		byte[] extradata = new byte[image.Length - num];
		Array.Copy(image, num, extradata, 0, extradata.Length);
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
