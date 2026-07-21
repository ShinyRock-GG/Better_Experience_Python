using System;
using System.Collections.Generic;
using System.Linq;
using Assets;
using Assets._ReusableScripts;
using Assets._ReusableScripts.Controllers;
using Assets._ReusableScripts.CuchiCuchi;
using Assets._ReusableScripts.CuchiCuchi._CharactersBasics;
using Assets._ReusableScripts.CuchiCuchi.Characters.Controladores.ControlladoresDeColoDePrioridad;
using Assets._ReusableScripts.CuchiCuchi.Controllers;
using Assets._ReusableScripts.CuchiCuchi.Controllers.Ojos.Parpadeos;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Controllers;
using Assets._ReusableScripts.Globales.Mapas;
using Assets.TValle.BeachGirl.Runtime;
using BetterExperience.GameScopes;
using UnityEngine;

namespace BetterExperience.Wrappers.Characters;

public class ComboGestureController
{
	private OjosExpresionController eyesExprCtl;

	private ControladorDeGestosDeBoca mouthCtl;

	private ControladorDeGestosConCabeza headCtl;

	private ControlladorDeGestosFacialesEmocionales faceCtl;

	private List<GesturesWeights> weights = new List<GesturesWeights>();

	private ModificadorDeFloat[] faceBlendShapes;

	private float eyeTickDuration = 0.5f;

	private float mouthTickDuration = 0.5f;

	private float faceExprTickDuration = 0.5f;

	private ModificadorDeFloat blendShape_lipsSmirk_RL;

	private ModificadorDeFloat blendShape_lipsDropSide_RL;

	private ModificadorDeFloat blendShape_lipsDrop_RL;

	private ScopeSupport scope;

	private bool hasActiveFaceBlendShapes;

	private GesturesWeights Weights { get; }

	public ComboGestureController(GameObject go, ScopeSupport scope)
	{
		this.scope = scope;
		MapaDeCCAnimationBlendShapes instance = MapaSingleton<MapaDeCCAnimationBlendShapes>.instance;
		Weights = new GesturesWeights(instance.valores.ToArray());
		eyesExprCtl = go.GetComponentInChildren<OjosExpresionController>();
		mouthCtl = go.GetComponentInChildren<ControladorDeGestosDeBoca>();
		headCtl = go.GetComponentInChildren<ControladorDeGestosConCabeza>();
		faceCtl = go.GetComponentInChildren<ControlladorDeGestosFacialesEmocionales>();
		IControladorDeAnimationBlendShapes blendShapesCtl = go.GetComponentInChildren<IControladorDeAnimationBlendShapes>();
		blendShape_lipsSmirk_RL = blendShapesCtl.ObtenerOrdenesDeID(instance.Expresion_Lips_Smirk__RL_31__, ControllerMultipleDirectoModificableDeUnSoloFloat.TipoDeOrden.obtenerMaximoAbsoluto).modificable.ObtenerModificadorNotNull((UnityEngine.Object)(object)scope.Lookup<Plugin>());
		blendShape_lipsDropSide_RL = blendShapesCtl.ObtenerOrdenesDeID(instance.Expresion_Lips_Drop_Sides__RL_35__, ControllerMultipleDirectoModificableDeUnSoloFloat.TipoDeOrden.obtenerMaximoAbsoluto).modificable.ObtenerModificadorNotNull((UnityEngine.Object)(object)scope.Lookup<Plugin>());
		blendShape_lipsDrop_RL = blendShapesCtl.ObtenerOrdenesDeID(instance.Expresion_Lips_Drop__RL_26__, ControllerMultipleDirectoModificableDeUnSoloFloat.TipoDeOrden.obtenerMaximoAbsoluto).modificable.ObtenerModificadorNotNull((UnityEngine.Object)(object)scope.Lookup<Plugin>());
		DispatcherService dispatcher = scope.Lookup<DispatcherService>();
		dispatcher.DoUpdate.Add(OnUpdate, scope);
		CreateBlendShapeAccessors(blendShapesCtl);
	}

	private void CreateBlendShapeAccessors(IControladorDeAnimationBlendShapes blendShapesCtl)
	{
		Plugin plugin = scope.Lookup<Plugin>();
		faceBlendShapes = new ModificadorDeFloat[Weights.FaceBlendShapes.Keys.Length];
		for (int i = 0; i < faceBlendShapes.Length; i++)
		{
			ControllerMultipleDirectoModificableDeUnSoloFloat.OrdenesDeID ordenes = blendShapesCtl.ObtenerOrdenesDeID(Weights.FaceBlendShapes.Keys[i], ControllerMultipleDirectoModificableDeUnSoloFloat.TipoDeOrden.obtenerMaximoAbsoluto);
			ModificadorDeFloat modificador = ordenes.modificable.ObtenerModificadorNotNull((UnityEngine.Object)(object)plugin);
			faceBlendShapes[i] = modificador;
		}
	}

	public GesturesWeights RequestWeightsAccessor(ScopeSupport scope)
	{
		GesturesWeights w = new GesturesWeights(Weights.FaceBlendShapes.Keys);
		weights.Add(w);
		scope.OnDispose += delegate
		{
			weights.Remove(w);
		};
		return w;
	}

	private void OnUpdate()
	{
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		UpdateState();
		if (Weights.EyesOverride)
		{
			EnumWeightArray<EyeExpressionType> expr = Weights.EyeExpressions;
			for (int i = 0; i < expr.Keys.Length; i++)
			{
				OjosExpresionController.Tipo tipo = (OjosExpresionController.Tipo)i;
				float weight = expr[i];
				if (weight > 0f)
				{
					eyesExprCtl.Cambiar(tipo, int.MaxValue, eyeTickDuration, ControllerPrioridadConfig.interrumpir, weight * 100f);
				}
			}
		}
		if (Weights.MouthOverride)
		{
			EnumWeightArray<MouthGesture> expr2 = Weights.MouthExpression;
			for (int j = 0; j < expr2.Keys.Length; j++)
			{
				TiposDeGestosDeBoca tipo2 = (TiposDeGestosDeBoca)j;
				float weight2 = expr2[j];
				if (weight2 > 0f)
				{
					mouthCtl.Gestuar(tipo2, weight2, int.MaxValue, ControllerPrioridadConfig.interrumpir, mouthTickDuration, puedePonerEnCola: false);
				}
			}
		}
		// TipoDeExpresion namespace not resolvable in SMA 23.1 — face expression override disabled
		// if (Weights.FaceOverride) { ... }
		if (!Weights.FaceBlendShapesOverride && !hasActiveFaceBlendShapes)
		{
			return;
		}
		hasActiveFaceBlendShapes = false;
		for (int l = 0; l < Weights.FaceBlendShapes.Keys.Length; l++)
		{
			float value = Weights.FaceBlendShapes[l];
			if (!Weights.FaceBlendShapesOverride)
			{
				value = 0f;
			}
			faceBlendShapes[l].valor.valor = value * 100f;
			hasActiveFaceBlendShapes |= value > 0f;
		}
	}

	private void UpdateBlendShapeFaceExpr(FaceExpressionType type, float weight)
	{
		switch (type)
		{
		case FaceExpressionType.happiness:
			blendShape_lipsSmirk_RL.valor.valor = 50f * weight;
			break;
		case FaceExpressionType.anger:
			blendShape_lipsDropSide_RL.valor.valor = 50f * weight;
			blendShape_lipsDrop_RL.valor.valor = 50f * weight;
			break;
		}
	}

	private void UpdateState()
	{
		if (IsDirty())
		{
			Weights.Enabled = Any((GesturesWeights x) => x.Enabled);
			Weights.EyesOverride = Any((GesturesWeights x) => x.EyesOverride);
			ApplyMax((GesturesWeights x) => x.EyesOverride, (GesturesWeights x) => x.EyeExpressions);
			Weights.MouthOverride = Any((GesturesWeights x) => x.MouthOverride);
			ApplyMax((GesturesWeights x) => x.MouthOverride, (GesturesWeights x) => x.MouthExpression);
			Weights.FaceOverride = Any((GesturesWeights x) => x.FaceOverride);
			ApplyMax((GesturesWeights x) => x.FaceOverride, (GesturesWeights x) => x.FaceExpression);
			Weights.FaceBlendShapesOverride = Any((GesturesWeights x) => x.FaceBlendShapesOverride);
			ApplyMax((GesturesWeights x) => x.FaceBlendShapesOverride, (GesturesWeights x) => x.FaceBlendShapes);
			weights.ForEach(delegate(GesturesWeights x)
			{
				x.Dirty = false;
			});
		}
	}

	private void ApplyMax<T>(Func<GesturesWeights, bool> when, Func<GesturesWeights, EnumWeightArray<T>> from) where T : Enum
	{
		EnumWeightArray<T> to = from(Weights);
		to.Clear();
		foreach (GesturesWeights k in weights)
		{
			if (k.Enabled && when(k))
			{
				EnumWeightArray<T> x = from(k);
				for (int i = 0; i < to.Keys.Length; i++)
				{
					to[i] = Mathf.Max(to[i], x[i]);
				}
			}
		}
		T maxk = to.MaxKey();
		T[] keys = to.Keys;
		for (int j = 0; j < keys.Length; j++)
		{
			T k2 = keys[j];
			if (k2.CompareTo(maxk) != 0)
			{
				to[k2] = 0f;
			}
		}
	}

	private void ApplyMax(Func<GesturesWeights, bool> when, Func<GesturesWeights, StringWeightArray> from)
	{
		StringWeightArray to = from(Weights);
		to.Clear();
		foreach (GesturesWeights k in weights)
		{
			if (k.Enabled && when(k))
			{
				StringWeightArray x = from(k);
				for (int i = 0; i < to.Keys.Length; i++)
				{
					to[i] = Mathf.Max(to[i], x[i]);
				}
			}
		}
	}

	private bool IsDirty()
	{
		foreach (GesturesWeights x in weights)
		{
			if (x.Dirty)
			{
				return true;
			}
		}
		return false;
	}

	private bool Any(Func<GesturesWeights, bool> predicate)
	{
		foreach (GesturesWeights k in weights)
		{
			if (k.Enabled && predicate(k))
			{
				return true;
			}
		}
		return false;
	}
}
