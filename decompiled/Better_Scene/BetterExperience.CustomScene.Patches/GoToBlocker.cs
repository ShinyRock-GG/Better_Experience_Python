using System;
using Assets;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Controllers.Interacciones;
using Assets._ReusableScripts.UI.Interacciones.Donas;

namespace BetterExperience.CustomScene.Patches;

internal class GoToBlocker : CustomUpdatedMonobehaviourBase, ICheckerIsGreyOut
{
	private IInteraccionesDeCharacter m_interacciones;

	public InteractionManager InteractionManager { get; set; }

	bool ICheckerIsGreyOut.isGreyOut
	{
		get
		{
			if (m_interacciones == null)
			{
				return false;
			}
			if (HasActivePrimaryInteraction() && !InteractionManager.IsIdlePose())
			{
				return true;
			}
			if (InteractionManager.AnimationController.ChangingState)
			{
				return true;
			}
			return false;
		}
	}

	private bool HasActivePrimaryInteraction()
	{
		for (int i = 0; i < m_interacciones.interaccionesPrimariasBases.Count; i++)
		{
			Interaccion interaccion = m_interacciones.interaccionesPrimariasBases[i]?.instancia;
			if (interaccion != null && interaccion.algunaEstaEjecutandose)
			{
				return true;
			}
		}
		return false;
	}

	protected override void AwakeUnityEvent()
	{
		base.AwakeUnityEvent();
		m_interacciones = this.GetComponentEnRoot<IInteraccionesDeCharacter>();
		if (m_interacciones == null)
		{
			throw new ArgumentNullException("m_interacciones", "m_interacciones null reference.");
		}
	}
}
