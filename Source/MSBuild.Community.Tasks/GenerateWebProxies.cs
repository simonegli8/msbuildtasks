// Creates Mono mdb debug files from pdb files. 

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Threading;
using MissingLinq.Linq2Management.Context;
using MissingLinq.Linq2Management.Model.CIMv2;

namespace MSBuild.Community.Tasks {

	public class GenerateWebProxies: Task {

		class ProcessInfo {
			public Process Process;
			public string File;
			public string Url;
			public string Args;
			public DateTime StartTime;
		}

		class ProcessCollection: KeyedCollection<Process, ProcessInfo> {
			protected override Process GetKeyForItem(ProcessInfo item) => item.Process;
		}

		public static class IisExpress {
			#region Parameters
			public static string SiteFolder = null;
			public static int? Port { get; set; }
			public static int RandomPort => 15000 + new Random().Next(800);
			public static int ProcessStateChangeDelay = 18 * 1000;
			static readonly string IISPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"IIS Express\iisexpress.exe");
			#endregion

			public static void Start(int? port = null) {
				Port = port ?? Port;
				Process.Start(InvocationInfo);
				Thread.Sleep(400);
			}
			public static void Stop(bool wait = false) {
				var p = GetWin32Process();
				if (p == null) return;

				var pp = Process.GetProcessById((int)p.ProcessId);
				if (pp == null) return;

				pp.Kill();
				if (wait) pp.WaitForExit(ProcessStateChangeDelay);
			}
			public static bool IsStarted() {
				var p = GetWin32Process();
				return p != null;
			}

			static readonly string ProcessName = Path.GetFileName(IISPath);
			static string Quote(string value) { return "\"" + value.Trim() + "\""; }
			static string CmdLine { get { return string.Format(@"{0} /port:{1}", Quote("/path:" + SiteFolder), Port ?? RandomPort); } }

			static ProcessStartInfo InvocationInfo => new ProcessStartInfo() {
				FileName = IISPath,
				Arguments = CmdLine,
				WorkingDirectory = SiteFolder,
				CreateNoWindow = true,
				UseShellExecute = true,
				WindowStyle = ProcessWindowStyle.Minimized,
				LoadUserProfile = true
			};

			static Win32Process GetWin32Process() {
				//the linq over ManagementObjectContext implementation is simplistic so we do foreach instead
				try {
					using (var mo = new ManagementObjectContext())
						foreach (var p in mo.CIMv2.Win32Processes)
							if (p.Name == ProcessName && p.CommandLine.Contains(CmdLine)) return p;
				} catch { }
 				return null;
			}
		}

		public enum Protocols { SOAP, SOAP12, HttpGet, HttpPost }
		public enum Languages { CS, VB, JS, VJS, CPP }
		public enum Types { Client, WseSoapClient, WseWebClient, Server, ServerInterface, WCFClient }

		public int Timeout { get; set; } = 60;

		const string WseWsdl3Tool = @"Lib\Microsoft WSE\v3.0\Tools\WseWsdl3.exe";
		const string WsdlTool = @"wsdl.exe";
		const string WcfTool = "svcutil.exe";
		static readonly string SDKPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft SDKs\Windows");
		static readonly string IISPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"IIS Express\iisexpress.exe");

		public ITaskItem[] Urls { get; set; }

		public ITaskItem[] Files { get; set; }

		public ITaskItem[] Sources { get; set; }

		public ITaskItem[] Exclude { get; set; }

		public string SourceProject { get; set; }
		public string Project { get; set; }

		public string Namespace { get; set; } = "ServiceProxies";

		public string Language { get; set; } = Languages.CS.ToString();

		public string Protocol { get; set; } = Protocols.SOAP.ToString();

		public string Type { get; set; } = Types.Client.ToString();

		public string FileSuffix { get; set; }

		public string Meta { get; set; }

		public bool Sharetypes { get; set; } = false;

		public bool Clean { get; set; } = true;

		public string Serializer { get; set; } = "Auto";

		public string Reference { get; set; } = null;

		public bool ImportXmlTypes { get; set; } = true;

		public bool Async { get; set; } = true;

		public string Config { get; set; } = null;

		public string CollectionType { get; set; } = null;

		public bool UseSerializerForFaults { get; set; } = true;

		public string TargetClientVersion { get; set; } = null;
		
		public bool DataContractOnly { get; set; } = false;
		public bool ServiceContract { get; set; } = false;
		public bool MessageContract { get; set; } = false;
		public bool NoConfig { get; set; } = false;
		public bool NoStdLib { get; set; } = false;
		public bool MergeConfig { get; set; } = false;
		public bool Internal { get; set; } = false;
		public bool EnableDataBinding { get; set; } = false;
		public string ExcludeType { get; set; } = null;

		Types type => (Types)Enum.Parse(typeof(Types), Type);

		bool WSE => type == Types.WseSoapClient || type == Types.WseWebClient;
		bool WCF => type == Types.WCFClient;

		public class CompareSDK : IComparer<string> {

			public int Compare(string a, string b) {
				var ex = new Regex("v?(?<version>[0-9.]+)(?<revision>.*)$");
				var ma = ex.Match(a);
				var mb = ex.Match(b);
				if (ma.Success && mb.Success) {
					var av = new System.Version(ma.Groups["version"].Value);
					var bv = new System.Version(mb.Groups["version"].Value);
					if (av < bv) return -1;
					if (av > bv) return 1;
					var ar = ma.Groups["revision"].Value;
					var br = mb.Groups["revision"].Value;
					return string.Compare(ar, br);
				}
				return string.Compare(a, b);
			}
		}

		public string ToolExe {
			get {
				if (WSE) return Path.Combine(Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath), WseWsdl3Tool);
				string tool;
				if (WCF) tool = WcfTool;
				else tool = WsdlTool;

				var sdk = new DirectoryInfo(SDKPath);

				var compare = new CompareSDK();

				var dir = sdk.EnumerateDirectories("*", SearchOption.TopDirectoryOnly).OrderByDescending(d => d.Name, compare).FirstOrDefault(); // get latest SDK folder
				if (dir == null) return null;
				return dir.EnumerateFiles(tool, SearchOption.AllDirectories).LastOrDefault()?.FullName; // find wsdl.exe or svcutil.exe in SDK folder
			}
		}

		[Output]
		public ITaskItem[] Output { get; set; }

		private static void CleanXmlTypeDefinitions(string pathToFile) {
			Regex regex = new Regex(@"^\s*///.*$", RegexOptions.Multiline);
			string text = File.ReadAllText(pathToFile);
			text = regex.Replace(text, "");
			regex = new Regex(@"\[([^\]]{2,})\]");
			var r = new StringReader(text);
			var lines = new List<string>();
			var s = r.ReadLine();
			while (s != null) { lines.Add(s); s = r.ReadLine(); }

			string[] strArray = lines.ToArray();
			StringBuilder builder = new StringBuilder();
			StringBuilder builder2 = new StringBuilder();
			for (int i = 0; i < strArray.Length; i++) {
				string str = strArray[i];
				builder2.AppendLine(str);
				if (!str.Contains("///")) {
					if (regex.IsMatch(str)) {
						if (str.IndexOf("XmlTypeAttribute") > -1) {
							int braces = 0;
							bool started = false;
							while (!started || braces > 0) {
								if (str.IndexOf("}") != -1) {
									braces--;
									started = true;
								} else if (str.IndexOf("{") != -1) {
									braces++;
									started = true;
								}

								i++;
								str = strArray[i];
							}
							builder2.Remove(0, builder2.Length);
						}
					} else if (builder2.Length > 0) {
						builder.Append(builder2.ToString());
						builder2.Remove(0, builder2.Length);
					}
				}
			}
			text = builder.ToString();
			File.WriteAllText(pathToFile, text);
		}

		public void RemovePortInProxyUrl(string file, string url) {
			var urlWithoutPort = Regex.Replace(url, @"(?<=.*://[a-zA-Z0-9.\-_]+):[0-9]+", "");
			var text = File.ReadAllText(file);
			text = text.Replace(url, urlWithoutPort);
			File.WriteAllText(file, text);
		}

		bool IsLocked(string file) {
			try {
				var s = File.OpenWrite(file);
				s.Close();
				return false;
			} catch (Exception) {
				return true;
			}
		}

		bool Terminated(Process p,  ProcessCollection infos) {
			if (!p.HasExited) return false;

			p.WaitForExit();

			ProcessInfo info;

			lock (infos) {
				if (!infos.Contains(p)) return true;
				info = infos[p];
				infos.Remove(info);
			}

			if (WCF) {
			}

			if ((p.ExitCode == 0 || (WCF && (p.ExitCode == 3 || p.ExitCode == 4))) && File.Exists(info.File) && File.GetLastWriteTimeUtc(info.File) > info.StartTime) {

				if (Clean) CleanXmlTypeDefinitions(info.File);
				RemovePortInProxyUrl(info.File, info.Url);
				Log.LogMessage(MessageImportance.High, "Generated proxy class for {0}", new Uri(info.Url).AbsolutePath);
			} else {
				var cmd = p.StandardOutput.ReadToEnd();
				Log.LogError("Error generating proxy for {0}: {1} {2}; {3}", new Uri(info.Url).AbsolutePath, WSE ? "wsewsdl3.exe" : (WCF ? "scvutil.exe" : "wsdl.exe"), info.Args, cmd.Replace("\n", ", ").Replace("\r", ""));
				Log.LogCommandLine(cmd);
			}
			return true;
		}

public override bool Execute() {
			if (System.Type.GetType("Mono.Runtime") != null) { // Don't execute under Mono.
				Log.LogMessage(MessageImportance.High, "The GenerateWebProxies task doesn't run under Mono.");
				return true;
			}
			if ((Files == null || (Urls == null && Sources == null)) && SourceProject == null) return true;

			var tool = ToolExe;
			if (tool == null) Log.LogError("GenerateWebProxies: Cannot find " + (WCF ? "svcutil.exe" : "wsdl.exe") +", please install Windows SDK.");


			var projTime = string.IsNullOrEmpty(Project) ? DateTime.MinValue : File.GetLastWriteTimeUtc(Project);
			DateTime[] sourceTimes = null;

			var iisport = IisExpress.RandomPort;
			var useIisExpress = Urls == null;
			if (Urls == null) {
				IisExpress.SiteFolder = Path.GetFullPath(SourceProject).Trim('\\');
				var serverUrl = "http://localhost:" + iisport.ToString() + "/";

				Urls = (Sources ?? (Sources = Directory.EnumerateFiles(SourceProject, WCF ? "*.svc" : "*.asmx", SearchOption.AllDirectories).Where(p => !(Exclude?.Any(x => x.ItemSpec == p) ?? false))
					.Select(path => new TaskItem(path)).ToArray())
					.Select(src => new TaskItem(serverUrl + src.ItemSpec.Substring(SourceProject.Length).Trim('\\', '/', ' ').Replace('\\', '/') /*+ (WCF ? "?wsdl" : "") */))
					.ToArray());
				var files = Sources.Select(src => new TaskItem(Path.ChangeExtension(src.ItemSpec.Substring(SourceProject.Length), (FileSuffix ?? Type.ToString()) + "." + Language.ToString().ToLower())))
					.ToDictionary(src => src.ItemSpec);
				if (Files == null) Files = files.Values.ToArray();
				else {
					var items = Files.ToDictionary(f => f.ItemSpec);
					Files = files.Values.Select(f => items.ContainsKey(f.ItemSpec) ? items[f.ItemSpec] : f).ToArray();
				}
				var regex = new Regex(@"<%@ (?:WebService|ServiceHost).*?(?:CodeBehind|CodeFile)\s*=\s*(?:(?:""(?<code>[^""]*)"")|(?:'(?<code>[^']*)')).*?%>");
				sourceTimes = Sources.Select(src => { // get times of svc or asmx C# source files
					var sourceInfo = new FileInfo(src.ItemSpec);
					if (sourceInfo.Exists) {
						var m = regex.Match(File.ReadAllText(src.ItemSpec));
						var t = sourceInfo.LastWriteTimeUtc;
						if (m.Success) {
							var codeInfo = new FileInfo(Path.Combine(Path.GetDirectoryName(src.ItemSpec), m.Groups["code"].Value));
							var codet = codeInfo.Exists ? codeInfo.LastWriteTimeUtc : DateTime.MinValue;
							return codet > t ? codet : t;
						}
					}
					return DateTime.MinValue;
				}).ToArray();
			}

			var output = new List<TaskItem>();
			var processes = new ProcessCollection();
			var startTime = DateTime.UtcNow;

			var wait = new ManualResetEvent(false);

			var iisstarted = false;
			var anyprocesses = false;
			System.Threading.Tasks.Parallel.For(0, Urls.Length, i => {

				var meta = Files[i].GetMetadata(Meta);
				if (!string.IsNullOrEmpty(Meta) && !string.IsNullOrEmpty(meta) && meta != "false") return;

				var url = Urls[i].ItemSpec;
				var file = Files[i].ItemSpec;
				var src = Sources?[i].ItemSpec;
				var ns = Files[i].GetMetadata("Namespace");
				if (string.IsNullOrEmpty(ns)) ns = Namespace;
				var config = Config != null ? $"/config:{Config} /mergeConfig " : "";
				var serializer = Serializer != null ? $"/serializer:{Serializer} ": "";
				var useSerializerForFaults = Serializer != null && UseSerializerForFaults ? $"/useSerializerForFaults " : "";
				var async = Async ? "/async " : "/syncOnly ";
				string reference = "";
				if (Reference != null) {
					var refs = Reference.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim());
					var str = new StringBuilder();
					foreach (var r in refs) {
						str.Append("\"/r:");
						str.Append(Path.GetFullPath(r));
						str.Append("\" ");
					}
					reference = str.ToString();
				}
				var targetVersion = TargetClientVersion != null ? $"/targetClientVersion:{TargetClientVersion} " : "";
				var importXmlTypes = ImportXmlTypes ? "/importXmlTypes " : "";
				var dataContractOnly = DataContractOnly ? "/dataContractOnly " : "";
				var serviceContract = ServiceContract ? "/serviceContract " : "";
				var enableDataBinding = EnableDataBinding ? "/enableDataBinding " : "";
				string excludeType = "";
				if (ExcludeType != null) {
					var types = ExcludeType.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim());
					var str = new StringBuilder();
					foreach (var t in types) {
						str.Append("\"/et:");
						str.Append(Path.GetFullPath(t));
						str.Append("\" ");
					}
					excludeType = str.ToString();
				}
				var _internal = Internal ? "/internal " : "";
				var mergeConfig = MergeConfig ? "/mergeConfig " : "";
				var noConfig = NoConfig ? "/noConfig " : "";
				var noStdLib = NoStdLib ? "/noStdLib " : "";
				 
				var arg = WCF ? $"/t:code /out:{file} /namespace:{ns} /language:{Language} {config}{serializer}{useSerializerForFaults}{async}{targetVersion}{importXmlTypes}{reference}{dataContractOnly}{serviceContract}{enableDataBinding}{excludeType}{_internal}{mergeConfig}{noConfig}{noStdLib}{url}" :
					$"/out:{file} /namespace:{ns} /language:{Language} /protocol:{Protocol}" + (WSE ? " /type:" + (type == Types.WseSoapClient ? "soapClient" : "webClient") : (type == Types.Client ? "" : (type == Types.Server ? " /server" : " /serverInterface"))) + (Sharetypes ? " /sharetypes" : "") + " " + url;
				var fileInfo = new FileInfo(file);
				var fileTime = fileInfo.LastWriteTimeUtc;
				var sourceTime = sourceTimes[i];

				if (src == null || !fileInfo.Exists || fileTime < projTime || fileTime < sourceTime) {
					if (useIisExpress && !iisstarted) {
						iisstarted = true;
						if (IisExpress.IsStarted()) IisExpress.Stop();
						IisExpress.Start(iisport);
					}

					var wsdlstart = new ProcessStartInfo(tool) {
						UseShellExecute = false,
						WorkingDirectory = Environment.CurrentDirectory,
						CreateNoWindow = true,
						WindowStyle = ProcessWindowStyle.Hidden,
						RedirectStandardOutput = true,
						Arguments = arg
					};
					var p = new Process();
					p.StartInfo = wsdlstart;
					p.EnableRaisingEvents = true;
					p.Exited += (sender, a) => {
						var any = !Terminated(p, processes);
						ProcessInfo[] infos;
						lock (processes) infos = processes.ToArray();
						foreach (var info in infos) {
							any |= !Terminated(info.Process, processes);
						}
						if (!any) wait.Set();
					};
					lock (processes) processes.Add(new ProcessInfo { Process = p, File = file, Url = url, Args = arg });
					lock (output) output.Add(new TaskItem(Files[i]));
					anyprocesses = true;
					p.Start();
				}
			});

			
			//foreach (var p in processes.ToArray()) p.Start();


			if (anyprocesses && !wait.WaitOne(Timeout*1000)) {
				ProcessInfo[] infos;
				lock (processes) infos = processes.ToArray();
				foreach (var info in infos) {
					if (!info.Process.HasExited) info.Process.Kill();
					Terminated(info.Process, processes);
				}
			}

			if (iisstarted) IisExpress.Stop();

			Output = output.ToArray();
			return !Log.HasLoggedErrors;
		}
	}

}
