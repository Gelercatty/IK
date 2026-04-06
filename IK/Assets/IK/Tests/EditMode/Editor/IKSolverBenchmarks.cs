using System.Collections.Generic;
using System.IO;
using GelerIK.Runtime.Solvers;
using NUnit.Framework;
using UnityEngine;

namespace GelerIK.Tests.EditMode
{
    public class IKSolverBenchmarks
    {
        [Test]
        public void GenerateStepSweepBenchmarkCsvForCurrentSolvers()
        {
            IKBenchmarkConfig config = new IKBenchmarkConfig();

            List<IKBenchmarkCategory> categories = new List<IKBenchmarkCategory>
            {
                new IKBenchmarkCategory(
                    "Reachable",
                    true,
                    config.reachableMinRadiusRatio,
                    config.reachableMaxRadiusRatio),
                new IKBenchmarkCategory(
                    "Unreachable",
                    false,
                    config.unreachableMinRadiusRatio,
                    config.unreachableMaxRadiusRatio)
            };

            List<IKSolverRegistration> solvers = new List<IKSolverRegistration>
            {
                new IKSolverRegistration(
                    "CCD",
                    "CCD",
                    "step_scale",
                    1.0f,
                    () => new CCDSolver()),
                new IKSolverRegistration(
                    "JacobianTranspose",
                    "JacobianTranspose",
                    "step_scale",
                    1.0f,
                    () => new JacobianTransposeSolver()),
                new IKSolverRegistration(
                    "JacobianDLS_lambda_0.03",
                    "JacobianDLS",
                    "damping",
                    0.03f,
                    () => new JacobianDampedLeastSquaresSolver(0.03f)),
                new IKSolverRegistration(
                    "JacobianDLS_lambda_0.10",
                    "JacobianDLS",
                    "damping",
                    0.10f,
                    () => new JacobianDampedLeastSquaresSolver(0.10f)),
                new IKSolverRegistration(
                    "JacobianDLS_lambda_0.30",
                    "JacobianDLS",
                    "damping",
                    0.30f,
                    () => new JacobianDampedLeastSquaresSolver(0.30f)),
                new IKSolverRegistration(
                    "JacobianSvdDLS_default",
                    "JacobianSvdDLS",
                    "minimum_damping",
                    0.02f,
                    () => new JacobianSvdDampedLeastSquaresSolver(
                        minimumDamping: 0.02f,
                        singularityThreshold: 0.10f,
                        dampingGain: 1.5f,
                        maximumDamping: 1.0f))
            };

            IKBenchmarkReport report = IKBenchmarkRunner.Run(config, categories, solvers);

            string resultsDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "IK/Tests/Results"));
            IKBenchmarkCsvExporter.ExportAll(report, resultsDirectory);

            TestContext.Progress.WriteLine(report.BuildExperimentPlanText());
            TestContext.Progress.WriteLine(report.BuildScoreFormulaText());
            TestContext.Progress.WriteLine(report.BuildSummaryText());
            TestContext.Progress.WriteLine("Benchmark CSV written to: " + resultsDirectory);

            int expectedSampleCount = categories.Count * config.samplesPerCategory;
            int expectedStepPointCount = categories.Count * solvers.Count * config.stepScaleSweep.Length;
            int expectedScorePointCount = solvers.Count * config.stepScaleSweep.Length;

            Assert.That(report.sampleDefinitions.Count, Is.EqualTo(expectedSampleCount));
            Assert.That(report.stepSweepPoints.Count, Is.EqualTo(config.stepScaleSweep.Length));
            Assert.That(report.stepPoints.Count, Is.EqualTo(expectedStepPointCount));
            Assert.That(report.scorePoints.Count, Is.EqualTo(expectedScorePointCount));
            Assert.That(report.bestScorePoints.Count, Is.EqualTo(solvers.Count));

            Assert.That(File.Exists(Path.Combine(resultsDirectory, "IKBenchmarkExperimentPlan.txt")), Is.True);
            Assert.That(File.Exists(Path.Combine(resultsDirectory, "IKBenchmarkScoreFormula.txt")), Is.True);
            Assert.That(File.Exists(Path.Combine(resultsDirectory, "IKBenchmarkSummary.txt")), Is.True);
            Assert.That(File.Exists(Path.Combine(resultsDirectory, "IKBenchmarkSamples.csv")), Is.True);
            Assert.That(File.Exists(Path.Combine(resultsDirectory, "IKBenchmarkSampleResults.csv")), Is.True);
            Assert.That(File.Exists(Path.Combine(resultsDirectory, "IKBenchmarkStepData.csv")), Is.True);
            Assert.That(File.Exists(Path.Combine(resultsDirectory, "IKBenchmarkPlotReady.csv")), Is.True);
            Assert.That(File.Exists(Path.Combine(resultsDirectory, "IKBenchmarkScoreCurve.csv")), Is.True);
            Assert.That(File.Exists(Path.Combine(resultsDirectory, "IKBenchmarkBestScores.csv")), Is.True);
        }
    }
}
