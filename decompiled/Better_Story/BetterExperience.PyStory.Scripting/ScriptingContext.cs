using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BetterExperience.CustomScene;
using BetterExperience.GameScopes;
using HarmonyLib;
using IronPython.Hosting;
using IronPython.Modules;
using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;
using Microsoft.Scripting.Hosting.Providers;
using Microsoft.Scripting.Runtime;

namespace BetterExperience.PyStory.Scripting;

internal class ScriptingContext : StrandService, IDisposable
{
	private delegate object ImportDelegate(CodeContext context, string moduleName, PythonDictionary globals, PythonDictionary locals, PythonTuple tuple, int level);

	private ScriptEngine python;

	private PythonScriptRepository scripts;

	private Dictionary<string, Scope> modules = new Dictionary<string, Scope>();

	private RepositoryMetaImporter moduleLoader;

	public ScriptingStage Stage { get; }

	public Traverse PythonExceptions_GetPythonException { get; }

	public Observable<string> OnErrorReport { get; } = new Observable<string>();

	public ScopeSupport ScriptingScope => base.scriptingScope;

	public ScriptEngine Engine => python;

	public bool CrashSafeMode { get; set; } = true;

	public ScriptingContext(DispatcherService dispatcher, PythonScriptRepository scripts, ScriptingStage stage)
		: base(dispatcher)
	{
		Stage = stage;
		this.scripts = scripts;
		python = Python.CreateEngine();
		PythonExceptions_GetPythonException = Traverse.Create(typeof(PythonExceptions)).Method("GetPythonException", new Type[1] { typeof(Exception) }, (object[])null);
		PythonList metapath = Python.GetSysModule(python).GetVariable<PythonList>("meta_path");
		metapath.append((object)(moduleLoader = new RepositoryMetaImporter(scripts)));
		base.scriptingScope.OnException += ScriptingScope_OnException;
		base.scriptingScope.Start();
	}

	public unsafe void PreloadModules(List<string> preloadFiles, AsyncTaskProgress preloaderProgress)
	{
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Expected O, but got Unknown
		try
		{
			preloaderProgress.Report(0, scripts.Scripts.Count + preloadFiles.Count);
			MeasureTime val = MeasureTime.Create(logger, (Func<long, string>)((long t) => $"Precompilation time: {t}ms"), true);
			try
			{
				PythonContext context = (PythonContext)HostingHelpers.GetLanguageContext(python);
				foreach (string scriptfile in scripts.Scripts)
				{
					string name = scriptfile;
					name = FileNameToModule(name);
					try
					{
						moduleLoader.CompileModule(context, name);
					}
					catch (Exception ex)
					{
						logger.Error(ex, "Failed to precompile module {0}", name);
					}
					preloaderProgress.Inc();
				}
				moduleLoader.NoTrace = true;
				try
				{
					foreach (string precompile in preloadFiles)
					{
						string mname = FileNameToModule(precompile);
						try
						{
							python.Execute("import " + mname);
						}
						catch (Exception ex2)
						{
							logger.Error(ex2, "Failed to preload module {0}", mname);
						}
						preloaderProgress.Inc();
					}
				}
				finally
				{
					moduleLoader.NoTrace = false;
				}
			}
			finally
			{
				((IDisposable)(*(MeasureTime*)(&val))/*cast due to constrained. prefix*/).Dispose();
			}
		}
		catch (Exception ex3)
		{
			logger.Error(ex3, "OOPS preloader failed");
		}
	}

	private void RunAotCompiler(List<string> precompileFiles)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Expected O, but got Unknown
		PythonContext context = (PythonContext)HostingHelpers.GetLanguageContext(python);
		PythonAotCompiler aotCompiler = new PythonAotCompiler(context, (string name) => ((LanguageContext)context).DefaultEncoding.GetBytes(scripts.GetScript(name)));
		Assembly asm = aotCompiler.Compile(precompileFiles);
		python.Runtime.LoadAssembly(asm);
	}

	public static string FileNameToModule(string name)
	{
		if (name.ToLowerInvariant().EndsWith(".py"))
		{
			name = name.Substring(0, name.Length - 3);
		}
		if (name.ToLowerInvariant().EndsWith("__init__"))
		{
			name = name.Substring(0, name.Length - 8);
		}
		if (name.EndsWith("\\"))
		{
			name = name.Substring(0, name.Length - 1);
		}
		if (name.StartsWith("\\scripts"))
		{
			name = name.Substring("\\scripts".Length + 1);
		}
		name = name.Replace(Path.DirectorySeparatorChar, '.');
		return name;
	}

	private void ScriptingScope_OnException(ScopeSupport.ScopeExceptionEvent obj)
	{
		obj.Handled = true;
		PyStrandFrame ct = PyStrandFrame.CurrentFrame;
		string pythonContext = "None";
		if (ct != null)
		{
			pythonContext = ct.DescribeContext();
		}
		string report = "PyStrand context: " + pythonContext + "\n\n";
		Exception exc = obj.Exception;
		exc = FindPythonExcOnStack(exc);
		if (CrashSafeMode && IsPythonException(exc))
		{
			report += "Crash safe reporting\n\n";
			Func<object, object> formatter = python.Execute<Func<object, object>>("import pycs\npycs._format_exc");
			object pyexobj = GetPythonException(exc);
			object pyex = ((pyexobj != null) ? formatter(pyexobj) : "");
			logger.Error("Python exception:\n{0}", pyex);
			report += "Python stacktrace:\n\n";
			report += pyex;
			report += "\n\n";
			SyntaxErrorException see = (SyntaxErrorException)(object)((exc is SyntaxErrorException) ? exc : null);
			if (see != null)
			{
				report += $"Syntax error: {((Exception)(object)see).Message} at {see.SourcePath} {see.Line}:{see.Column}";
			}
		}
		else
		{
			logger.Error(obj.Exception, "PyScript exception. Python context: {0}", pythonContext);
			report = WriteUnsafeReport(obj, report);
		}
		OnErrorReport.Invoke(report);
	}

	private Exception FindPythonExcOnStack(Exception exc)
	{
		for (Exception e = exc; e != null; e = e.InnerException)
		{
			logger.Info("Checking {0}: {1}", e.GetType(), e.Message);
			if (IsPythonException(e))
			{
				logger.Info("OK!");
				return e;
			}
		}
		logger.Info("Not found");
		return exc;
	}

	private bool IsPythonException(Exception exc)
	{
		return GetPythonException(exc) != null;
	}

	private object GetPythonException(Exception exc)
	{
		return PythonExceptions_GetPythonException.GetValue(new object[1] { exc });
	}

	private string WriteUnsafeReport(ScopeSupport.ScopeExceptionEvent obj, string report)
	{
		string pyex = python.GetService<ExceptionOperations>(Array.Empty<object>()).FormatException(obj.Exception);
		logger.Error("Python exception:\n{0}", pyex);
		report += "Python stacktrace:\n\n";
		report += pyex;
		report += "\n\n";
		Exception exception = obj.Exception;
		SyntaxErrorException see = (SyntaxErrorException)(object)((exception is SyntaxErrorException) ? exception : null);
		if (see != null)
		{
			report += $"Syntax error: {((Exception)(object)see).Message} at {see.SourcePath} {see.Line}:{see.Column}";
		}
		else
		{
			report += "Managed stacktrace:\n\n";
			report += obj.Exception.ToString().Replace("--->", "\n--->");
		}
		return report;
	}

	public void Dispose()
	{
		base.scriptingScope.Dispose();
		python.Runtime.Shutdown();
	}

	protected object DoDatabaseImport(CodeContext context, string moduleName, PythonDictionary globals, PythonDictionary locals, PythonTuple tuple, int level)
	{
		logger.Error("Import! {0}", moduleName);
		string filename = moduleName + ".py";
		string rawScript = scripts.GetScript(filename);
		if (rawScript != null)
		{
			if (modules.TryGetValue(moduleName, out var cached))
			{
				return cached;
			}
			ScriptSource source = python.CreateScriptSourceFromString(rawScript, filename);
			ScriptScope scope = python.CreateScope();
			source.Execute(scope);
			Scope modscope = HostingHelpers.GetScope(scope);
			scope.SetVariable(moduleName, (object)modscope);
			modules[moduleName] = modscope;
			return modscope;
		}
		return Builtin.__import__(context, moduleName, (object)null, (object)null, (object)null, 0);
	}
}
