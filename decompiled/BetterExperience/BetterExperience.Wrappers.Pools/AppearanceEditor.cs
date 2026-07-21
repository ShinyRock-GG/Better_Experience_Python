using System.Collections.Generic;
using Assets._ReusableScripts.CuchiCuchi.Chars.Alteradores.Clases;
using Assets._ReusableScripts.CuchiCuchi.Chars.Alteradores.Mapas.Genetica;
using Assets._ReusableScripts.CuchiCuchi.Chars.Alteradores.Mapas.Genetica.Handlers;

namespace BetterExperience.Wrappers.Pools;

internal class AppearanceEditor : AbstractGeneticsEditor<SujetoIdentificableAlteradoresAparienciaFemeninos>
{
	protected override IReadOnlyList<ModificadoresDeAlterador> Prefab => ((ProductorDeSujetosDeAparienciaFisicaFemenina)pool.Instance.aparienciaFisica.productor).@default.ObtenerAlteradorModificadores();

	protected override IReadOnlyList<ModificadoresDeAlterador> Current => impl.ObtenerAlteradorModificadores();

	public AppearanceEditor(SujetoIdentificableAlteradoresAparienciaFemeninos impl, GuestPool pool)
		: base(impl, pool)
	{
	}
}
