using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;

namespace MSBuild.Community.Tasks {

	public class AutoDataContract: Replace {

		static Dictionary<string, DateTime> Stamps;

		protected override IEnumerable<Replacing> GetReplacings(ITaskItem item, string text) {
			yield return new Replacing { Count = -1, Expression = @"\[(?<begin>[^\]]*)(?<!DataContract,\s*)AutoDataContract(?<end>[^\]]*)\]", Replacement="[${begin}DataContract, AutoDataContract${end}]" };
			yield return new Replacing {
				ScopeCount = -1, Scope=@"\[\s*(?:DataContract\s*,\s*)?AutoDataContract\s*\]\s+(?:public\s+)?(?:partial\s+)?(?:class|interface|struct)\s+[a-zA-Z0-9_<>,]+\s+(:\s+[a-zA-Z0-9_.<>?, \t\r\n]+)?{(?:[^}]*}(?![^{}]*(?:public\s+)?(?:partial\s+)?(?:class|interface|struct)))*",
				Count = -1, Expression = @"(?<!\[.*?(?:DataMember|IgnoreMember|XmlIgnore).*?\]\s*(?:\[[^\]]*\]\s*)*)(?<prop>(?<ident>[ \t]+)public\s+(virtual\s+)?[a-zA-Z0-9_.\[\]\<\>\?,]+\s+[a-zA-Z0-9_]+\s*{\s*(?:(?:get|set)\s*(?:;|(?:{[^}]*}))\s*)+})",
				Replacement = "${ident}[DataMember]\r\n${prop}"
			};
		}

		/*public override bool ShowProgress {
			get { return true; } set { base.ShowProgress = true; }
		}*/

		public override bool Execute() {
			var f = new DataContractSerializer(typeof(Dictionary<string, DateTime>));
			var stampspath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MSBuild.Community.Tasks");
			if (!Directory.Exists(stampspath)) Directory.CreateDirectory(stampspath);
			stampspath = Path.Combine(stampspath, "AutoDataContract.data");
			try {
				using (var stampsfile = new FileStream(stampspath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
					Stamps = (Dictionary<string, DateTime>)f.ReadObject(stampsfile);
				}
			} catch (FileNotFoundException) {
				Stamps = new Dictionary<string, DateTime>();
			}
			Files = Files.Where(file => {
				var fullpath = file.GetMetadata("FullPath");
				DateTime stamp;
				if (!Stamps.TryGetValue(fullpath, out stamp)) stamp = DateTime.MinValue;
				return File.GetLastWriteTimeUtc(fullpath) > stamp;
			})
			.ToArray();
			var res = base.Execute();
			var now = DateTime.UtcNow.AddSeconds(5);
			foreach (var file in Files) {
				var fullpath = file.GetMetadata("FullPath");
				Stamps[fullpath] = now;
			}
			using (var stampsfile = new FileStream(stampspath, FileMode.Create, FileAccess.Write, FileShare.Write)) {
				f.WriteObject(stampsfile, Stamps);
			}

			return res;
		}

	}
}
