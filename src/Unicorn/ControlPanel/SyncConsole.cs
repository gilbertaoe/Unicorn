﻿using System;
using System.Web;
using Kamsar.WebConsole;
using Sitecore.Pipelines;
using Unicorn.Configuration;
using Unicorn.ControlPanel.Headings;
using Unicorn.Logging;
using Unicorn.Pipelines.UnicornSyncEnd;
using Unicorn.Predicates;
using Sitecore.Diagnostics;

namespace Unicorn.ControlPanel
{
	/// <summary>
	/// Runs a Unicorn sync in a WebConsole of a configuration or configurations
	/// </summary>
	public class SyncConsole : ControlPanelConsole
	{
		public SyncConsole(bool isAutomatedTool) : base(isAutomatedTool, new HeadingService())
		{
		}

		protected override string Title
		{
			get { return "Sync Unicorn"; }
		}

		protected override void Process(IProgressStatus progress)
		{
			var configurations = ResolveConfigurations();
			int taskNumber = 1;

			foreach (var configuration in configurations)
			{
				var logger = configuration.Resolve<ILogger>();
				var helper = configuration.Resolve<SerializationHelper>();

				using (new LoggingContext(new WebConsoleLogger(progress), configuration))
				{
					try
					{
						logger.Info(string.Empty);
						logger.Info(configuration.Name + " is being synced.");

						using (new TransparentSyncDisabler())
						{
							var pathResolver = configuration.Resolve<PredicateRootPathResolver>();

							var roots = pathResolver.GetRootSerializedItems();

							var index = 0;

							helper.SyncTree(configuration, item =>
							{
								SetTaskProgress(progress, taskNumber, configurations.Length, (int) ((index/(double) roots.Length)*100));
								index++;
							}, roots);
						}
					}
					catch (DeserializationSoftFailureAggregateException ex)
					{
						logger.Error(ex);
						// allow execution to continue, because the exception was non-fatal
					}
					catch (Exception ex)
					{
						logger.Error(ex);
						break;
					}
				}

				taskNumber++;
			}

			try
			{
				CorePipeline.Run("unicornSyncEnd", new UnicornSyncEndPipelineArgs(progress, configurations));
			}
			catch (Exception exception)
			{
				Log.Error("Error occurred in unicornSyncEnd pipeline.", exception);
				progress.ReportException(exception);
			}
		}

		protected virtual IConfiguration[] ResolveConfigurations()
		{
			var config = HttpContext.Current.Request.QueryString["configuration"];
			var targetConfigurations = ControlPanelUtility.ResolveConfigurationsFromQueryParameter(config);

			if (targetConfigurations.Length == 0) throw new ArgumentException("Configuration(s) requested were not defined.");

			return targetConfigurations;
		}
	}
}
