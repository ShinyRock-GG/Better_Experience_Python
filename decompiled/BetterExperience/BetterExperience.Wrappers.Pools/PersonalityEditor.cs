using System.Collections.Generic;
using Assets._ReusableScripts.CuchiCuchi.Chars.Alteradores.Clases;
using Assets._ReusableScripts.CuchiCuchi.Chars.Alteradores.Mapas.Genetica;
using Assets._ReusableScripts.CuchiCuchi.Chars.Alteradores.Mapas.Genetica.Handlers;

namespace BetterExperience.Wrappers.Pools;

internal class PersonalityEditor : AbstractGeneticsEditor<SujetoIdentificableAlteradoresPersonalidadFemeninos>
{
	protected override IReadOnlyList<ModificadoresDeAlterador> Prefab => ((ProductorDeSujetosDePersonalidadFemenina)pool.Instance.personalidad.productor).@default.ObtenerAlteradorModificadores();

	protected override IReadOnlyList<ModificadoresDeAlterador> Current => impl.ObtenerAlteradorModificadores();

	public PersonalityEditor(SujetoIdentificableAlteradoresPersonalidadFemeninos impl, GuestPool pool)
		: base(impl, pool)
	{
	}
}
