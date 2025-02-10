#region Copyright © 2006 Andy Johns. All rights reserved.
/*
Copyright © 2006 Andy Johns. All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions
are met:

1. Redistributions of source code must retain the above copyright
   notice, this list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright
   notice, this list of conditions and the following disclaimer in the
   documentation and/or other materials provided with the distribution.
3. The name of the author may not be used to endorse or promote products
   derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE AUTHOR "AS IS" AND ANY EXPRESS OR
IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY DIRECT, INDIRECT,
INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF
THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE. 
*/
#endregion



using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.IO;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;

namespace MSBuild.Community.Tasks {
	/// <summary>
	/// Task to filter an Input list with a Regex expression.
	/// Output list contains items from Input list that matched given expression
	/// </summary>
	/// <example>Matches from TestGroup those names ending in a, b or c
	/// <code><![CDATA[
	/// <ItemGroup>
	///    <TestGroup Include="foo.my.foo.foo.test.o" />
	///    <TestGroup Include="foo.my.faa.foo.test.a" />
	///    <TestGroup Include="foo.my.fbb.foo.test.b" />
	///    <TestGroup Include="foo.my.fcc.foo.test.c" />
	///    <TestGroup Include="foo.my.fdd.foo.test.d" />
	///    <TestGroup Include="foo.my.fee.foo.test.e" />
	///    <TestGroup Include="foo.my.fff.foo.test.f" />
	/// </ItemGroup>
	/// <Target Name="Test">
	///    <!-- Outputs only items that end with a, b or c -->
	///    <RegexMatch Input="@(TestGroup)" Expression="[a-c]$">
	///       <Output ItemName ="MatchReturn" TaskParameter="Output" />
	///    </RegexMatch>
	///    <Message Text="&#xA;Output Match:&#xA;@(MatchReturn, '&#xA;')" />
	/// </Target>
	/// ]]></code>
	/// </example>
	public class UpdateFilter: Task {

		public ITaskItem[] Sources { get; set; }
		public ITaskItem[] Destinations { get; set; }

		public ITaskItem[] Items { get; set; }

		[Output]
		public ITaskItem[] NewSources { get; set; }
		[Output]
		public ITaskItem[] OldSources { get; set; }
		[Output]
		public ITaskItem[] OutdatedDestinations { get; set; }
		[Output]
		public ITaskItem[] UpToDateDestinations { get; set; }

		public string Meta { get; set; }
		/// <summary>
		/// Performs the Match task
		/// </summary>
		/// <returns><see langword="true"/> if the task ran successfully; 
		/// otherwise <see langword="false"/>.</returns>
		public override bool Execute() {

			if (Sources == null || Destinations == null) return true;

			if (Sources.Length != Destinations.Length) Log.LogError("UpdateFilter: Sources and Destinations count don't match: Sources: {0}, Destinations: {1}.", Sources.Length, Destinations.Length);
			else {
				var newsrc = new List<ITaskItem>();
				var oldsrc = new List<ITaskItem>();
				var outdateddest = new List<ITaskItem>();
				var upToDatedest = new List<ITaskItem>();

				var items = Items?.ToDictionary(item => item.ItemSpec) ?? new Dictionary<string, ITaskItem>();

				System.Threading.Tasks.Parallel.For(0, Sources.Length, i => {
					var destItem = Destinations[i];
					var srcItem = Sources[i];
					ITaskItem item;
					if (items.TryGetValue(srcItem.ItemSpec, out item)) srcItem = item;
					if (items.TryGetValue(destItem.ItemSpec, out item)) destItem = item;
					var src = srcItem.ItemSpec;
					var dest = destItem.ItemSpec;
					if (File.Exists(src)) {
						if ((string.IsNullOrEmpty(Meta) || string.IsNullOrEmpty(destItem.GetMetadata(Meta)) || destItem.GetMetadata(Meta) == "false")
							&& (!File.Exists(dest) || File.GetLastWriteTimeUtc(src) > File.GetLastWriteTimeUtc(dest))) {
							lock (outdateddest) outdateddest.Add(destItem);
							lock (newsrc) newsrc.Add(srcItem);
						} else {
							lock (upToDatedest) upToDatedest.Add(destItem);
							lock (srcItem) oldsrc.Add(srcItem);
						}
					}
				});
				OldSources = oldsrc.ToArray();
				NewSources = newsrc.ToArray();
				UpToDateDestinations = upToDatedest.ToArray();
				OutdatedDestinations = outdateddest.ToArray();
			}

			return !Log.HasLoggedErrors;
		}
	}

}