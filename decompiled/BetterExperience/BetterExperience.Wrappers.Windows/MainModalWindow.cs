using System.Collections.Generic;
using Assets._ReusableScripts.Memorias.Archivos;
using Assets._ReusableScripts.UI.Modales;
using Assets._ReusableScripts.UI.Modales.Globales;
using Assets.SingletonesAndSystemasGlobales.AbstractLayer.Globales;
using Assets.TValle.IU.Runtime.Drawing.Paneles.Modelos;
using Assets.TValle.IU.Runtime.Modales;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace BetterExperience.Wrappers.Windows;

public class MainModalWindow
{
	public bool Visible
	{
		get
		{
			if (Singleton<ModalWindow>.IsInScene)
			{
				return Singleton<ModalWindow>.instance.isShowing;
			}
			return false;
		}
	}

	public SavingPortraitDialog CurrentSavingPortraitDialog
	{
		get
		{
			if (Singleton<ModalWindow>.IsInScene)
			{
				ModalWindow wnd = Singleton<ModalWindow>.instance;
				List<GameObject> windows = Traverse.Create((object)wnd).Field<List<GameObject>>("m_modalesUsando").Value;
				foreach (GameObject go in windows)
				{
					SavingPortraitDialog dlg = go.GetComponent<SavingPortraitDialog>();
					if (dlg != null)
					{
						return dlg;
					}
				}
			}
			return null;
		}
	}

	public MayBeResult<bool> MessageBoxYesNo(string text, params object[] p)
	{
		if (p.Length != 0)
		{
			text = string.Format(text, p);
		}
		MayBeResult<bool> result = new MayBeResult<bool>();
		if (Singleton<ModalWindow>.IsInScene)
		{
			ModalWindow wnd = Singleton<ModalWindow>.instance;
			ConfirmacionMiembros confirmation = wnd.MostrarConfirmacion();
			confirmation.pregunta.text = text;
			confirmation.aceptar.onClick.AddListener(delegate
			{
				result.SetResult(value: true);
			});
			confirmation.cancelar.onClick.AddListener(delegate
			{
				result.SetResult(value: false);
			});
			confirmation.noMostrarOtraVezToggle.interactable = false;
			result.OnResult += delegate
			{
				wnd.Clear<ConfirmacionMiembros>();
			};
		}
		else
		{
			result.SetResult(value: false);
		}
		return result;
	}

	public MayBeResult<object> MessageError(string text)
	{
		MayBeResult<object> result = new MayBeResult<object>();
		if (Singleton<ModalWindow>.IsInScene)
		{
			ModalWindow wnd = Singleton<ModalWindow>.instance;
			if (wnd.isShowing)
			{
				Logger.Global.Warn("MessageError '" + text + "' was suppressed");
				result.SetResult(null);
				return result;
			}
			ErrorDialog confirmation = wnd.MostrarErrorDialog();
			confirmation.pregunta.text = text;
			confirmation.aceptar.onClick.AddListener(delegate
			{
				result.SetResult(null);
			});
			result.OnResult += delegate
			{
				wnd.Clear<ErrorDialog>();
			};
		}
		else
		{
			result.SetResult(false);
		}
		return result;
	}

	public MayBeResult<object> ShowBigMessage(string text)
	{
		MayBeResult<object> result = new MayBeResult<object>();
		if (Singleton<ModalWindow>.IsInScene)
		{
			ModalWindow wnd = Singleton<ModalWindow>.instance;
			InfoDialog confirmation = wnd.MostrarBigInfoDialog();
			confirmation.pregunta.text = text;
			confirmation.aceptar.onClick.AddListener(delegate
			{
				result.SetResult(null);
			});
			result.OnResult += delegate
			{
				wnd.Clear<InfoDialog>();
			};
		}
		else
		{
			result.SetResult(false);
		}
		return result;
	}

	public MayBeResult<string> RequestInput(string text, string def)
	{
		MayBeResult<string> result = new MayBeResult<string>();
		if (Singleton<ModalWindow>.IsInScene)
		{
			ModalWindow wnd = Singleton<ModalWindow>.instance;
			TextInputDialog dialog = wnd.MostrarTextInputDialog();
			dialog.pregunta.text = text;
			dialog.inputField.text = def;
			TMP_InputField.CharacterValidation originalValidation = dialog.inputField.characterValidation;
			dialog.inputField.characterValidation = TMP_InputField.CharacterValidation.None;
			dialog.inputField.characterLimit = 100;
			dialog.aceptar.onClick.AddListener(delegate
			{
				result.SetResult(dialog.inputField.text);
			});
			dialog.cancelar.onClick.AddListener(delegate
			{
				result.SetResult(null);
			});
			result.OnResult += delegate
			{
				dialog.inputField.characterValidation = originalValidation;
				wnd.Clear<TextInputDialog>();
			};
		}
		else
		{
			result.SetResult(null);
		}
		return result;
	}

	public MayBeResult<string> SelectPosePromGallery()
	{
		// PortraitsModelBase<T> generic type arg not resolvable without sources — gallery picker stubbed in SMA 23.1
		MayBeResult<string> result = new MayBeResult<string>();
		result.SetResult(null);
		return result;
	}

	public MayBeResult<string> SelectOutfitFromGallery()
	{
		// PortraitsModelBase<T> generic type arg not resolvable without sources — gallery picker stubbed in SMA 23.1
		MayBeResult<string> result = new MayBeResult<string>();
		result.SetResult(null);
		return result;
	}
}
