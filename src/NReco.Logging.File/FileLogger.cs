#region License
/*
 * NReco file logging provider (https://github.com/nreco/logging)
 * Copyright 2017 Vitaliy Fedorchenko
 * Distributed under the MIT license
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Concurrent;
using System.Text;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Reflection;

namespace NReco.Logging.File {

	/// <summary>
	/// Generic file logger that works in a similar way to standard ConsoleLogger.
	/// </summary>
	public class FileLogger : ILogger {

		private readonly string logName;
		private readonly FileLoggerProvider LoggerPrv;

		internal IExternalScopeProvider ScopeProvider { get; set; }

		/// <summary>
		/// Create new instance of <see cref="FileLogger"/>
		/// </summary>
		/// <param name="logName">Log file name</param>
		/// <param name="loggerPrv">Logger provider</param>
		/// <param name="scopeProvider">Scope provider</param>
		public FileLogger(string logName, FileLoggerProvider loggerPrv, IExternalScopeProvider scopeProvider) {
			this.logName = logName;
			LoggerPrv = loggerPrv;
			ScopeProvider = scopeProvider;
		}

		/// <inheritdoc />
		public IDisposable BeginScope<TState>(TState state) {
			return ScopeProvider?.Push(state) ?? EmptyScope.Instance;
		}

		/// <inheritdoc />
		public bool IsEnabled(LogLevel logLevel) {
			return logLevel >= LoggerPrv.MinLevel;
		}

		private string GetShortLogLevel(LogLevel logLevel) {
			switch (logLevel) {
				case LogLevel.Trace:
					return "TRCE";
				case LogLevel.Debug:
					return "DBUG";
				case LogLevel.Information:
					return "INFO";
				case LogLevel.Warning:
					return "WARN";
				case LogLevel.Error:
					return "FAIL";
				case LogLevel.Critical:
					return "CRIT";
			}
			return logLevel.ToString().ToUpper();
		}

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception exception,
			Func<TState, Exception, string> formatter) {
			if (!IsEnabled(logLevel)) {
				return;
			}

			if (formatter == null) {
				throw new ArgumentNullException(nameof(formatter));
			}

			string message = formatter(state, exception);

			if (LoggerPrv.Options.FilterLogEntry != null) {
				var filterLogObj = new LogMessage(logName, logLevel, eventId, message, exception);

				AppendScope(
				   filterLogObj.ScopeList,
				   filterLogObj.ScopeArgs,
				   state);

				if (!LoggerPrv.Options.FilterLogEntry(filterLogObj))
					return;
			}
			
			if (LoggerPrv.FormatLogEntry != null) {
				var logObj = new LogMessage(logName, logLevel, eventId, message, exception);

				AppendScope(
				   logObj.ScopeList,
				   logObj.ScopeArgs,
				   state);

				LoggerPrv.WriteEntry(LoggerPrv.FormatLogEntry(logObj));
			}
			else {
				// default formatting logic
				var logBuilder = new StringBuilder();
				if (!string.IsNullOrEmpty(message)) {
					DateTime timeStamp = LoggerPrv.UseUtcTimestamp ? DateTime.UtcNow : DateTime.Now;
					logBuilder.Append(timeStamp.ToString("o"));
					logBuilder.Append('\t');
					logBuilder.Append(GetShortLogLevel(logLevel));
					logBuilder.Append("\t[");
					logBuilder.Append(logName);
					logBuilder.Append("]");
					logBuilder.Append("\t[");
					logBuilder.Append(eventId);
					logBuilder.Append("]\t");
					logBuilder.Append(message);
				}

				if (exception != null) {
					// exception message
					logBuilder.AppendLine(exception.ToString());
				}
				LoggerPrv.WriteEntry(logBuilder.ToString());
			}
		}

		private void AppendScope<TState>(
			List<object> scopeList,
			IDictionary<string, object> scopeProperties,
			TState state) {
			ScopeProvider.ForEachScope((scope, state2) => AppendScope(scopeList, scopeProperties, scope), state);
		}

		/// <summary>
		/// Add scope objects to the proper log property
		/// </summary>
		/// <param name="scopeList"></param>
		/// <param name="scopeProperties"></param>
		/// <param name="scope"></param>
		/// <remarks>
		/// Sematic reference
		/// https://nblumhardt.com/2016/11/ilogger-beginscope/
		/// </remarks>
		private static void AppendScope(
			List<object> scopeList,
			IDictionary<string, object> scopeProperties,
			object scope) {
			if (scope == null)
				return;

			if (scope is EmptyScope)
				return;

			// The scope can be defined using BeginScope or LogXXX methods.
			// - logger.BeginScope(new { Author = "meziantou" })
			// - logger.LogInformation("Hello {Author}", "meziaantou")
			// Using LogXXX, an object of type FormattedLogValues is created. This type is internal but it implements IReadOnlyList, so we can use it.
			// https://github.com/aspnet/Extensions/blob/cc9a033c6a8a4470984a4cc8395e42b887c07c2e/src/Logging/Logging.Abstractions/src/FormattedLogValues.cs
			if (scope is IEnumerable<KeyValuePair<string, object>> formattedLogValues) {
				var strTemplate = new StringBuilder();
				foreach (var value in formattedLogValues) {
					// MethodInfo is set by ASP.NET Core when reaching a controller. This type cannot be serialized using JSON.NET, but I don't need it.
					if (value.Value is MethodInfo)
						continue;

					if (value.Key == "{OriginalFormat}") {
						if (value.Value is string strTmp) {
							strTemplate.Append(strTmp);
						}
					}
					else {
						scopeProperties[value.Key] = value.Value;
					}
				}
				if (strTemplate.Length > 0) {
					foreach (var scopeArg in scopeProperties) {
						_ = strTemplate.Replace("{" + scopeArg.Key + "}", scopeArg.Value.ToString());
					}
					scopeList.Add(strTemplate.ToString());
				}
			}
			else {
				scopeList.Add(scope);
			}
		}

	}
}
