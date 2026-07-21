using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using IronPython.Runtime;
using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

namespace BetterExperience.PyStory.Scripting;

public class PythonAotCompiler
{
	private PythonContext languageContext;

	private Func<string, byte[]> codeProvider;

	public object CompilationMode_toDisk { get; }

	public Traverse GetScriptCodeMethod { get; }

	public PythonAotCompiler(PythonContext context, Func<string, byte[]> provider)
	{
		languageContext = context;
		codeProvider = provider;
		Type compilationModeType = AccessTools.TypeByName("IronPython.Compiler.CompilationMode");
		CompilationMode_toDisk = Traverse.Create(compilationModeType).Field("ToDisk").GetValue();
		GetScriptCodeMethod = Traverse.Create((object)languageContext).Method("GetScriptCode", new Type[4]
		{
			typeof(SourceUnit),
			typeof(string),
			typeof(ModuleOptions),
			compilationModeType
		}, (object[])null);
	}

	public Assembly Compile(List<string> files)
	{
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Expected O, but got Unknown
		List<SavableScriptCode> list = new List<SavableScriptCode>();
		foreach (string fname in files)
		{
			string mname = ScriptingContext.FileNameToModule(fname);
			byte[] bytes = codeProvider(fname);
			SourceUnit sourceCode = ((LanguageContext)languageContext).CreateSourceUnit((TextContentProvider)(object)new RepositoryMetaImporter.MemoryStreamContentProvider(languageContext, bytes, fname), fname, (SourceCodeKind)4);
			ScriptCode scriptCode = GetScriptCodeToDisk(sourceCode, fname, (ModuleOptions)8);
			list.Add((SavableScriptCode)scriptCode);
		}
		SavableScriptCode.SaveToAssembly("precompiled_data.dll", list.ToArray());
		return Assembly.LoadFrom("precompiled_data.dll");
	}

	private ScriptCode GetScriptCodeToDisk(SourceUnit sourceUnit, string path, ModuleOptions moduleOptions)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Expected O, but got Unknown
		return (ScriptCode)GetScriptCodeMethod.GetValue(new object[4] { sourceUnit, path, moduleOptions, CompilationMode_toDisk });
	}
}
