using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace GelerIK.Tests.EditMode
{
    internal static class IKBenchmarkCsvExporter
    {
        public static void ExportAll(IKBenchmarkReport report, string resultsDirectory)
        {
            Directory.CreateDirectory(resultsDirectory);

            File.WriteAllText(
                Path.Combine(resultsDirectory, "IKBenchmarkExperimentPlan.txt"),
                report.BuildExperimentPlanText(),
                Encoding.UTF8);

            File.WriteAllText(
                Path.Combine(resultsDirectory, "IKBenchmarkScoreFormula.txt"),
                report.BuildScoreFormulaText(),
                Encoding.UTF8);

            File.WriteAllText(
                Path.Combine(resultsDirectory, "IKBenchmarkSummary.txt"),
                report.BuildSummaryText(),
                Encoding.UTF8);

            File.WriteAllText(
                Path.Combine(resultsDirectory, "IKBenchmarkSamples.csv"),
                BuildSamplesCsv(report.sampleDefinitions),
                Encoding.UTF8);

            File.WriteAllText(
                Path.Combine(resultsDirectory, "IKBenchmarkSampleResults.csv"),
                BuildSampleResultsCsv(report.sampleResults),
                Encoding.UTF8);

            File.WriteAllText(
                Path.Combine(resultsDirectory, "IKBenchmarkStepData.csv"),
                BuildStepDataCsv(report.stepPoints),
                Encoding.UTF8);

            File.WriteAllText(
                Path.Combine(resultsDirectory, "IKBenchmarkPlotReady.csv"),
                BuildPlotReadyCsv(report.stepPoints),
                Encoding.UTF8);

            File.WriteAllText(
                Path.Combine(resultsDirectory, "IKBenchmarkScoreCurve.csv"),
                BuildScoreCurveCsv(report.scorePoints),
                Encoding.UTF8);

            File.WriteAllText(
                Path.Combine(resultsDirectory, "IKBenchmarkBestScores.csv"),
                BuildBestScoresCsv(report.bestScorePoints),
                Encoding.UTF8);
        }

        private static string BuildSamplesCsv(IReadOnlyList<IKBenchmarkSampleDefinition> samples)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine(
                "sample_id,category_name,reachable_category,radius_ratio,target_distance,target_x,target_y,target_z");

            for (int i = 0; i < samples.Count; i++)
            {
                IKBenchmarkSampleDefinition sample = samples[i];
                builder.AppendLine(string.Join(",",
                    sample.sampleId.ToString(CultureInfo.InvariantCulture),
                    Escape(sample.categoryName),
                    sample.reachableCategory ? "1" : "0",
                    FormatFloat(sample.radiusRatio),
                    FormatFloat(sample.targetDistance),
                    FormatFloat(sample.targetPosition.x),
                    FormatFloat(sample.targetPosition.y),
                    FormatFloat(sample.targetPosition.z)));
            }

            return builder.ToString();
        }

        private static string BuildSampleResultsCsv(IReadOnlyList<IKBenchmarkSampleResult> results)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine(
                "sample_id,category_name,reachable_category,series_name,method_family,tuning_name,tuning_value,step_scale,converged,iterations,elapsed_ms,final_position_error,joint_rotation_deviation_deg");

            for (int i = 0; i < results.Count; i++)
            {
                IKBenchmarkSampleResult result = results[i];
                builder.AppendLine(string.Join(",",
                    result.sampleId.ToString(CultureInfo.InvariantCulture),
                    Escape(result.categoryName),
                    result.reachableCategory ? "1" : "0",
                    Escape(result.seriesName),
                    Escape(result.methodFamily),
                    Escape(result.tuningName),
                    FormatFloat(result.tuningValue),
                    FormatFloat(result.stepScale),
                    result.converged ? "1" : "0",
                    result.iterations.ToString(CultureInfo.InvariantCulture),
                    FormatFloat(result.elapsedMilliseconds),
                    FormatFloat(result.finalPositionError),
                    FormatFloat(result.jointRotationDeviationDegrees)));
            }

            return builder.ToString();
        }

        private static string BuildStepDataCsv(IReadOnlyList<IKBenchmarkStepPoint> stepPoints)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine(
                "category_name,reachable_category,series_name,method_family,tuning_name,tuning_value,step_scale,sample_count,success_count,success_rate,avg_converged_iterations,avg_runtime_all_samples,avg_final_error,avg_joint_rotation_deviation_deg");

            for (int i = 0; i < stepPoints.Count; i++)
            {
                IKBenchmarkStepPoint point = stepPoints[i];
                builder.AppendLine(string.Join(",",
                    Escape(point.categoryName),
                    point.reachableCategory ? "1" : "0",
                    Escape(point.seriesName),
                    Escape(point.methodFamily),
                    Escape(point.tuningName),
                    FormatFloat(point.tuningValue),
                    FormatFloat(point.stepScale),
                    point.sampleCount.ToString(CultureInfo.InvariantCulture),
                    point.successCount.ToString(CultureInfo.InvariantCulture),
                    FormatFloat(point.successRate),
                    FormatOptionalFloat(point.averageConvergedIterations),
                    FormatFloat(point.averageRuntimeAllSamples),
                    FormatFloat(point.averageFinalError),
                    FormatFloat(point.averageJointRotationDeviationDegrees)));
            }

            return builder.ToString();
        }

        private static string BuildPlotReadyCsv(IReadOnlyList<IKBenchmarkStepPoint> stepPoints)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine(
                "category_name,reachable_category,series_name,method_family,tuning_name,tuning_value,step_scale,metric_name,metric_value");

            for (int i = 0; i < stepPoints.Count; i++)
            {
                IKBenchmarkStepPoint point = stepPoints[i];
                AppendPlotRow(builder, point, "success_rate", point.successRate);
                AppendPlotRow(builder, point, "avg_converged_iterations", point.averageConvergedIterations);
                AppendPlotRow(builder, point, "avg_runtime_all_samples", point.averageRuntimeAllSamples);
                AppendPlotRow(builder, point, "avg_final_error", point.averageFinalError);
                AppendPlotRow(builder, point, "avg_joint_rotation_deviation_deg", point.averageJointRotationDeviationDegrees);
            }

            return builder.ToString();
        }

        private static string BuildScoreCurveCsv(IReadOnlyList<IKBenchmarkScorePoint> scorePoints)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine(
                "series_name,method_family,tuning_name,tuning_value,step_scale,composite_score,reachable_success_rate,reachable_avg_converged_iterations,reachable_avg_runtime_all_samples,reachable_avg_final_error,reachable_avg_joint_rotation_deviation_deg,unreachable_avg_runtime_all_samples,unreachable_avg_final_error,unreachable_avg_joint_rotation_deviation_deg");

            for (int i = 0; i < scorePoints.Count; i++)
            {
                IKBenchmarkScorePoint point = scorePoints[i];
                builder.AppendLine(string.Join(",",
                    Escape(point.seriesName),
                    Escape(point.methodFamily),
                    Escape(point.tuningName),
                    FormatFloat(point.tuningValue),
                    FormatFloat(point.stepScale),
                    FormatFloat(point.compositeScore),
                    FormatFloat(point.reachableSuccessRate),
                    FormatFloat(point.reachableAverageConvergedIterations),
                    FormatFloat(point.reachableAverageRuntimeAllSamples),
                    FormatFloat(point.reachableAverageFinalError),
                    FormatFloat(point.reachableAverageJointRotationDeviationDegrees),
                    FormatFloat(point.unreachableAverageRuntimeAllSamples),
                    FormatFloat(point.unreachableAverageFinalError),
                    FormatFloat(point.unreachableAverageJointRotationDeviationDegrees)));
            }

            return builder.ToString();
        }

        private static string BuildBestScoresCsv(IReadOnlyList<IKBenchmarkBestScorePoint> bestScorePoints)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine(
                "series_name,method_family,tuning_name,tuning_value,best_step_scale,composite_score");

            for (int i = 0; i < bestScorePoints.Count; i++)
            {
                IKBenchmarkBestScorePoint point = bestScorePoints[i];
                builder.AppendLine(string.Join(",",
                    Escape(point.seriesName),
                    Escape(point.methodFamily),
                    Escape(point.tuningName),
                    FormatFloat(point.tuningValue),
                    FormatFloat(point.bestStepScale),
                    FormatFloat(point.compositeScore)));
            }

            return builder.ToString();
        }

        private static void AppendPlotRow(StringBuilder builder, IKBenchmarkStepPoint point, string metricName, float metricValue)
        {
            builder.AppendLine(string.Join(",",
                Escape(point.categoryName),
                point.reachableCategory ? "1" : "0",
                Escape(point.seriesName),
                Escape(point.methodFamily),
                Escape(point.tuningName),
                FormatFloat(point.tuningValue),
                FormatFloat(point.stepScale),
                Escape(metricName),
                FormatOptionalFloat(metricValue)));
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (!value.Contains(",") && !value.Contains("\"") && !value.Contains("\n"))
            {
                return value;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        private static string FormatOptionalFloat(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? string.Empty
                : value.ToString("0.######", CultureInfo.InvariantCulture);
        }
    }
}
