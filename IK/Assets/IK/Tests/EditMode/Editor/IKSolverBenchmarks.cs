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
        public void CompareDefaultSolversAcrossSharedTargetSets()
        {
            IKBenchmarkConfig config = new IKBenchmarkConfig();

            List<IKBenchmarkScenario> scenarios = new List<IKBenchmarkScenario>
            {
                new IKBenchmarkScenario("ReachableMidRange", 0.35f, 0.75f),
                new IKBenchmarkScenario("ReachableBoundary", 0.9f, 1.0f),
                new IKBenchmarkScenario("Unreachable", 1.1f, 1.35f)
            };

            List<IKSolverRegistration> solvers = new List<IKSolverRegistration>
            {
                new IKSolverRegistration("CCD(step=1.0)", () => new CCDSolver(), 1.0f),
                new IKSolverRegistration("JacobianTranspose(step=0.15)", () => new JacobianTransposeSolver(), 0.15f)
            };

            IKBenchmarkReport report = IKBenchmarkRunner.Run(config, solvers, scenarios);
            string textReport = report.BuildTextReport();
            string resultsDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "IK/Tests/Results"));
            Directory.CreateDirectory(resultsDirectory);

            string reportPath = Path.Combine(resultsDirectory, "IKSolverBenchmarkReport.txt");
            File.WriteAllText(reportPath, textReport);

            TestContext.Progress.WriteLine(textReport);
            TestContext.Progress.WriteLine("Benchmark report written to: " + reportPath);

            Assert.That(report.summaries.Count, Is.EqualTo(solvers.Count * scenarios.Count));
            Assert.That(File.Exists(reportPath), Is.True, "Benchmark report file was not created.");

            for (int i = 0; i < report.summaries.Count; i++)
            {
                Assert.That(report.summaries[i].totalSamples, Is.EqualTo(config.samplesPerScenario));
            }
        }
    }
}
