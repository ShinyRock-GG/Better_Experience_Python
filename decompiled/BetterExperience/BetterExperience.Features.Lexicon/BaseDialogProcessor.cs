using System;
using System.IO;
using Assets._ReusableScripts.CuchiCuchi.Dialogos;
using BetterExperience.GameScopes;
using BetterExperience.Utils;
using HarmonyLib;

namespace BetterExperience.Features.Lexicon;

internal class BaseDialogProcessor<T> : ITextProcessor where T : new()
{
	private bool dirtyFlag;

	public string FileName { get; }

	public PersistenceService Persistence { get; }

	protected T Data { get; private set; }

	public BaseDialogProcessor(string v, PersistenceService persistence)
	{
		FileName = Path.Combine("lexicon", v);
		Persistence = persistence;
	}

	public void Process()
	{
		bool persist = Data == null;
		if (persist)
		{
			Data = Persistence.Persisted(() => new T(), FileName, exchange: true);
		}
		TraverseTree();
		if (persist)
		{
			Persistence.Persist(Data, FileName, exchange: true);
		}
	}

	protected virtual void Visit(ADialogVariant variant, ListaDeDialogosBase dialogosBase)
	{
		bool reinitialize = false;
		foreach (DialogoInfo v in dialogosBase.dialogosInfoBase)
		{
			string text = GetDialogText(v);
			text = Translator.Instance.Encode(text);
			ADialogInfo di = variant.GetValueOrAdd("@" + text, () => new ADialogInfo(v.chance, text));
			if (text != di.T || di.C != v.chance)
			{
				UpdateDialog(v, di);
				dirtyFlag = true;
			}
			if (Traverse.Create((object)v).Field("m_isInit").GetValue<bool>())
			{
				reinitialize = true;
			}
		}
		if (!reinitialize)
		{
			return;
		}
		foreach (DialogoInfo v2 in dialogosBase.dialogosInfoBase)
		{
			Traverse.Create((object)v2).Field("m_isInit").SetValue((object)false);
		}
		Traverse.Create((object)dialogosBase).Field("m_init").SetValue((object)false);
		Traverse.Create((object)dialogosBase).Method("CheckInit", Array.Empty<object>()).GetValue();
	}

	protected virtual void TraverseTree()
	{
	}

	public static string GetDialogText(DialogoInfo dialogo)
	{
		return Traverse.Create((object)dialogo).Field("text").GetValue<string>();
	}

	public static void UpdateDialog(DialogoInfo dialogo, ADialogInfo di)
	{
		dialogo.chance = di.C;
		Traverse.Create((object)dialogo).Field("text").SetValue((object)Translator.Instance.Decode(di.T));
	}

	public static void Reset(IHolderDeCollecionDeDialogoInfo y)
	{
		Traverse.Create((object)y).Method("OnDeshabilitado", Array.Empty<object>()).GetValue();
	}

	protected bool IsDirty()
	{
		if (dirtyFlag)
		{
			dirtyFlag = false;
			return true;
		}
		return false;
	}
}
