﻿using System;
using System.Collections.Generic;
using System.Text;

namespace NReco.Logging.File {
	/// <summary>
	/// Empty logging scope for <see cref="FileLogger"/>
	/// </summary>
	/// <remarks>
	/// https://www.meziantou.net/asp-net-core-json-logger.htm
	/// In ASP.NET Core 3.0 this classes is now internal. This means you need to add it to your code.
	/// </remarks>
	internal sealed class EmptyScope : IDisposable {
		public static EmptyScope Instance { get; } = new EmptyScope();
		private EmptyScope() { }
		public void Dispose() { }
	}
}