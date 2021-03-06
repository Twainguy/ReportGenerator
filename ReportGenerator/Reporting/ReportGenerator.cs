﻿using System;
using System.Collections.Generic;
using System.Linq;
using Palmmedia.ReportGenerator.Logging;
using Palmmedia.ReportGenerator.Parser;
using Palmmedia.ReportGenerator.Parser.Analysis;
using Palmmedia.ReportGenerator.Properties;

namespace Palmmedia.ReportGenerator.Reporting
{
    /// <summary>
    /// Converts a coverage report generated by PartCover, OpenCover or NCover into a readable report.
    /// In contrast to the XSLT-Transformation included in PartCover, the report is more detailed.
    /// It does not only show the coverage quota, but also includes the source code and visualizes which line has been covered.
    /// </summary>
    internal class ReportGenerator
    {
        /// <summary>
        /// The Logger.
        /// </summary>
        private static readonly ILogger Logger = LoggerFactory.GetLogger(typeof(ReportGenerator));

        /// <summary>
        /// The parser to use.
        /// </summary>
        private readonly IParser parser;

        /// <summary>
        /// The renderers.
        /// </summary>
        private readonly IEnumerable<IReportBuilder> renderers;

        /// <summary>
        /// The assembly filter.
        /// </summary>
        private readonly IFilter assemblyFilter;

        /// <summary>
        /// The class filter.
        /// </summary>
        private readonly IFilter classFilter;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReportGenerator" /> class.
        /// </summary>
        /// <param name="parser">The IParser to use.</param>
        /// <param name="assemblyFilter">The assembly filter.</param>
        /// <param name="classFilter">The class filter.</param>
        /// <param name="renderers">The renderers.</param>
        internal ReportGenerator(IParser parser, IFilter assemblyFilter, IFilter classFilter, IEnumerable<IReportBuilder> renderers)
        {
            if (parser == null)
            {
                throw new ArgumentNullException(nameof(parser));
            }

            if (assemblyFilter == null)
            {
                throw new ArgumentNullException(nameof(assemblyFilter));
            }

            if (classFilter == null)
            {
                throw new ArgumentNullException(nameof(classFilter));
            }

            if (renderers == null)
            {
                throw new ArgumentNullException(nameof(renderers));
            }

            this.parser = parser;
            this.assemblyFilter = assemblyFilter;
            this.classFilter = classFilter;
            this.renderers = renderers;
        }

        /// <summary>
        /// Starts the generation of the report.
        /// </summary>
        /// <param name="addHistoricCoverage">if set to <c>true</c> historic coverage information is added to classes.</param>
        /// <param name="executionTime">The execution time.</param>
        internal void CreateReport(bool addHistoricCoverage, DateTime executionTime)
        {
            var filteredAssemblies = this.parser.Assemblies
                .Where(a => this.assemblyFilter.IsElementIncludedInReport(a.Name))
                .Select(a =>
                {
                    var newAssembly = new Assembly(a.Name);
                    foreach (var @class in a.Classes)
                    {
                        if (classFilter.IsElementIncludedInReport(@class.Name))
                        {
                            newAssembly.AddClass(@class);
                        }
                    }

                    return newAssembly;
                });

            int numberOfClasses = filteredAssemblies.Sum(a => a.Classes.Count());

            Logger.InfoFormat(Resources.AnalyzingClasses, numberOfClasses);

            int counter = 0;

            foreach (var assembly in filteredAssemblies)
            {
                foreach (var @class in assembly.Classes)
                {
                    counter++;

                    Logger.DebugFormat(
                        " " + Resources.CreatingReport,
                        counter,
                        numberOfClasses,
                        @class.Assembly.ShortName,
                        @class.Name);

                    foreach (var renderer in this.renderers)
                    {
                        var fileAnalyses = @class.Files.Select(f => f.AnalyzeFile()).ToArray();

                        try
                        {
                            if (addHistoricCoverage)
                            {
                                @class.AddHistoricCoverage(new HistoricCoverage(@class, executionTime));
                            }

                            renderer.CreateClassReport(@class, fileAnalyses);
                        }
                        catch (Exception ex)
                        {
                            Logger.ErrorFormat(
                                "  " + Resources.ErrorDuringRenderingClassReport,
                                @class.Name,
                                renderer.ReportType,
                                ex.Message);
                        }
                    }
                }
            }

            Logger.Debug(" " + Resources.CreatingSummary);
            SummaryResult summaryResult = new SummaryResult(filteredAssemblies, this.parser.ToString());

            foreach (var renderer in this.renderers)
            {
                try
                {
                    renderer.CreateSummaryReport(summaryResult);
                }
                catch (Exception ex)
                {
                    Logger.ErrorFormat(
                        "  " + Resources.ErrorDuringRenderingSummaryReport,
                        renderer.ReportType,
                        ex.Message);
                }
            }
        }
    }
}