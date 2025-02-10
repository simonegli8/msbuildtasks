using System;
using System.Collections.Generic;
using System.IO;
using IO = System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using System.Threading.Tasks;

namespace MSBuild.Community.Tasks {

	public class ChangeBrand : Microsoft.Build.Utilities.Task {

		public static readonly string[] Extensions = new string[] { ".sln", ".csproj", ".vbproj", ".csproj.user", ".vbproj.user", ".xml", ".cs", ".vb", ".resx", ".xaml", ".aspx", ".ascx", ".ashx", ".asmx",
			".svc", ".master", ".config", ".md", ".htm", ".html", ".txt" };

		public string Path { get; set; }
		public string Source { get; set; }
		public string Destination { get; set; }

		public ChangeBrand() : base() { }

		public override bool Execute() {

			var exts = new HashSet<string>(Extensions);
			var root = new DirectoryInfo(Path);
			var regex = new Regex(Regex.Escape(Source)); 
			var dirs = root.EnumerateDirectories("*.*", SearchOption.AllDirectories);
			Parallel.ForEach(dirs, dir => {
				var ndir = dir.Name.Replace(Source, Destination);
				if (ndir != dir.Name) {
					ndir = IO.Path.Combine(IO.Path.GetDirectoryName(dir.FullName), ndir);
					Directory.Move(dir.FullName, ndir);
					Log.LogMessage("Directory moved to {0}", ndir);
				}
			});
			var files = root.EnumerateFiles("*.*", SearchOption.AllDirectories);
			Parallel.ForEach(files, file => {
				var nfile = file.Name.Replace(Source, Destination);
				if (nfile != file.Name) {
					nfile = IO.Path.Combine(IO.Path.GetDirectoryName(file.FullName), nfile);
					Directory.Move(file.FullName, nfile);
					Log.LogMessage("File moved to {0}", nfile);
				} else {
					nfile = file.Name;
				}

				if (exts.Contains(IO.Path.GetExtension(nfile))) {
					var otext = File.ReadAllText(nfile);
					var ismatch = false;
					var text = regex.Replace(otext, match => {
						ismatch = true;
						return match.Result(Destination);
					});
					if (ismatch) {
						File.WriteAllText(nfile, text);
						Log.LogMessage("File updated: {0}", nfile);
					}
				}
			});

			return true;
		}
	}
}
