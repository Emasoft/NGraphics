﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using System.Text.RegularExpressions;
using Mono.CSharp;

namespace NGraphics.Editor
{
	public class CompileResult
	{
		public string Code { get; set; }

		public IDrawable[] Drawables { get; set; }
	}

	public class CompileRequest
	{
		public string Code { get; private set; }

		Action<CompileResult> continuation = null;

		CancellationTokenSource cts = new CancellationTokenSource ();

		Evaluator eval = new Evaluator (new CompilerContext (new CompilerSettings (), new ConsoleReportPrinter ()));

		public CompileRequest (string code, Action<CompileResult> continuation)
		{
			Code = code;
			this.continuation = continuation;
			Compile ().ContinueWith (t => {
				if (t.IsFaulted) {
					Console.WriteLine (t.Exception);
				}
			});
		}

		async Task Compile ()
		{
			var token = cts.Token;
			var result = await Task.Run<CompileResult> (() => CompileCode (), token);
			if (!token.IsCancellationRequested && continuation != null) {
				continuation (result);
			}
		}

		static readonly Regex classRe = new Regex (@"class\s+(\w+)");

		CompileResult CompileCode ()
		{
			var r = new CompileResult {
				Code = Code
			};

			Console.WriteLine ("COMPILE!!!");

			var idt = typeof(IDrawable);

			eval.ReferenceAssembly (idt.Assembly);
			eval.ReferenceAssembly (typeof(Platforms).Assembly);

			eval.Run ("using System; using System.Linq; using System.Collections.Generic; using NGraphics;");

			object res;
			bool resSet;
			eval.Evaluate (Code, out res, out resSet);

			var drawables = new List<IDrawable> ();

			foreach (Match m in classRe.Matches (Code)) {
				var className = m.Groups [1].Value;
				eval.Evaluate ("new " + className + " ()", out res, out resSet);
				if (resSet) {
					var d = res as IDrawable;
					if (d != null) {
						drawables.Add (d);
					}
				}
			}

			r.Drawables = drawables.ToArray ();

			return r;
		}

		public void Cancel ()
		{
			cts.Cancel ();
			if (eval != null) {
				try {
					eval.Interrupt ();
				} catch (Exception ex) {
					Console.WriteLine (ex);
				}
			}
		}
	}
}

