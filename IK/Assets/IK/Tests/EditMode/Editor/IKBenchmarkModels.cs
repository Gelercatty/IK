using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GelerIK.Runtime.Solvers;
using UnityEngine;

namespace GelerIK.Tests.EditMode
{
    internal sealed class IKBenchmarkConfig
    {
        public int samplesPerScenario = 64;
        public int warmupSamples = 4;
        public int maxIterations = 24;
        public float positionTolerance = 0.01f;
        public float stepScale = 0.15f;
        public int randomSeed = 20260406;
        public float[] boneLengths = { 1.0f, 0.9f, 0.75f, 0.55f };
    }

    internal struct IKBenchmarkScenario
    {
        public string name;
        public float minRadiusScale;
        public float maxRadiusScale;

        public IKBenchmarkScenario(string name, float minRadiusScale, float maxRadiusScale)
        {
            this.name = name;
            this.minRadiusScale = minRadiusScale;
            this.maxRadiusScale = maxRadiusScale;
        }
    }

    internal struct IKSolverRegistration
    {
        public string name;
        public Func<IIKSolver> createSolver;
        public float stepScale;

        public IKSolverRegistration(string name, Func<IIKSolver> createSolver, float stepScale = -1f)
        {
            this.name = name;
            this.createSolver = createSolver;
            this.stepScale = stepScale;
        }
    }

    internal sealed class IKBenchmarkSampleResult
    {
        public string solverName;
        public string scenarioName;
        public int sampleIndex;
        public bool converged;
        public int iterations;
        public float elapsedMilliseconds;
        public float finalPositionError;
        public float targetDistance;
    }

    internal sealed class IKBenchmarkSummary
    {
        public string solverName;
        public string scenarioName;
        public int totalSamples;
        public int successCount;
        public float successRate;
        public float averageElapsedMillisecondsAllSamples;
        public float averageElapsedMillisecondsOnSuccess;
        public float averageIterationsAllSamples;
        public float averageIterationsOnSuccess;
        public float averageFinalPositionError;
        public float averageFailureFinalPositionError;
    }

    internal sealed class IKBenchmarkReport
    {
        public IKBenchmarkConfig config;
        public List<IKBenchmarkSummary> summaries = new();

        public string BuildTextReport()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("IK Solver Benchmark");
            builder.AppendLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Samples/Scenario={0}, Warmup={1}, MaxIterations={2}, Tolerance={3:0.####}, StepScale={4:0.####}, Seed={5}",
                    config.samplesPerScenario,
                    config.warmupSamples,
                    config.maxIterations,
                    config.positionTolerance,
                    config.stepScale,
                    config.randomSeed));

            string currentScenario = null;
            for (int i = 0; i < summaries.Count; i++)
            {
                IKBenchmarkSummary summary = summaries[i];
                if (currentScenario != summary.scenarioName)
                {
                    currentScenario = summary.scenarioName;
                    builder.AppendLine();
                    builder.AppendLine("Scenario: " + currentScenario);
                    builder.AppendLine(
                        "Solver | SuccessRate | AvgConvTimeMs | AvgConvIter | AvgAllTimeMs | AvgAllIter | AvgFinalErr | AvgFailureErr");
                }

                builder.AppendLine(
                    summary.solverName + " | " +
                    string.Format(CultureInfo.InvariantCulture, "{0:P1}", summary.successRate) + " | " +
                    FormatSuccessMetric(summary.successCount, summary.averageElapsedMillisecondsOnSuccess, "0.###") + " | " +
                    FormatSuccessMetric(summary.successCount, summary.averageIterationsOnSuccess, "0.##") + " | " +
                    string.Format(CultureInfo.InvariantCulture, "{0:0.###}", summary.averageElapsedMillisecondsAllSamples) + " | " +
                    string.Format(CultureInfo.InvariantCulture, "{0:0.##}", summary.averageIterationsAllSamples) + " | " +
                    string.Format(CultureInfo.InvariantCulture, "{0:0.####}", summary.averageFinalPositionError) + " | " +
                    string.Format(CultureInfo.InvariantCulture, "{0:0.####}", summary.averageFailureFinalPositionError));
            }

            return builder.ToString();
        }

        private static string FormatSuccessMetric(int successCount, float value, string format)
        {
            return successCount > 0
                ? string.Format(CultureInfo.InvariantCulture, "{0:" + format + "}", value)
                : "n/a";
        }
    }
}
