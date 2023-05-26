using ScottPlot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using Python.Runtime;
using System.Reflection;
using System.IO;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ScottPlot.Plottable;
using System.Drawing;
using System.Windows.Interop;
using Path = System.IO.Path;

using System.Collections.Concurrent;

namespace Potentials
{
    internal class Backend
    {
        public static Tuple<string, double[][], double[], double[], double[], double[], double[][]> RunPythonScript_AllPD(string pythonPath, string scriptPath, string arguments)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = pythonPath,
                Arguments = $"\"{scriptPath}\" {arguments}", // Заключите путь к скрипту в двойные кавычки
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using (Process process = new Process { StartInfo = startInfo })
            {
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrEmpty(error))
                {
                    throw new Exception($"Python script error: {error}");
                }

                var lines = output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                if (lines.Length < 2)
                {
                    throw new Exception("Python script output is not in the expected format.");
                }

                CultureInfo invariantCulture = CultureInfo.InvariantCulture;

                string input_intervals = lines[0];
                double[][] numbers = JsonConvert.DeserializeObject<double[][]>(input_intervals);

                string separating_path = lines[1];

                string input_dV0 = lines[2];
                double[] phase_0_speed_list = JsonConvert.DeserializeObject<double[]>(input_dV0);

                string input_dV4 = lines[3];
                double[] phase_4_speed_list = JsonConvert.DeserializeObject<double[]>(input_dV4);

                string input_num_of_APs = lines[4];
                double[] num_of_APs = JsonConvert.DeserializeObject<double[]>(input_num_of_APs);

                string input_radius_list = lines[5];
                double[] radius_list = JsonConvert.DeserializeObject<double[]>(input_radius_list);

                string input_x_y_list = lines[6];
                double[][] x_y_list = JsonConvert.DeserializeObject<double[][]>(input_x_y_list);

                return Tuple.Create(separating_path, numbers, phase_0_speed_list, phase_4_speed_list, num_of_APs, radius_list, x_y_list);
            }
        }

        public static (List<double> Times, List<double> Potentials) ReadFileData_ForAllPlot(string filePath)
        {
            int lineCount = 0;
            using (var reader = new StreamReader(filePath))
            {
                while (reader.ReadLine() != null)
                {
                    lineCount++;
                }
            }

            int estimatedSize = lineCount / 20;
            List<double> times = new List<double>(estimatedSize);
            List<double> potentials = new List<double>(estimatedSize);

            lineCount = 0;
            using (var reader = new StreamReader(filePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    // read every 20 lines
                    if (lineCount % 20 == 0)
                    {
                        var parts = line.Split('\t');
                        if (parts.Length == 2)
                        {
                            if (double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double time) &&
                                double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double potential))
                            {
                                times.Add(time);
                                potentials.Add(potential);
                            }
                        }
                    }
                    lineCount++;
                }
            }

            return (times, potentials);
        }


        // Делаем кортеж для потсроения доверительных интервалов с учетом windowSize
        public static Tuple<double[], double[], double[]> ConfidenceIntervals_same(double[] num_of_APs, double[] phase_0_4_speed_or_Radius_array, int windowSize)
        {
            // фокусы для доверительных интервалов
            List<double> num_of_APs_20 = new List<double>();
            List<double> phase_0_speed_array_20 = new List<double>();

            for (int i = 0; i < num_of_APs.Length; i++)
            {
                if (i % windowSize == 0)
                {
                    num_of_APs_20.Add(num_of_APs[i]);
                    phase_0_speed_array_20.Add(phase_0_4_speed_or_Radius_array[i]);
                }
            }

            //int windowSize = 10;
            List<double> means = new List<double>();
            List<double> standardDeviations = new List<double>();

            for (int i = 0; i < phase_0_4_speed_or_Radius_array.Length; i += windowSize)
            {
                double[] window = phase_0_4_speed_or_Radius_array.Skip(i).Take(windowSize).ToArray();
                double mean = window.Average();
                double sumOfSquaresOfDifferences = window.Select(val => (val - mean) * (val - mean)).Sum();
                double standardDeviation = Math.Sqrt(sumOfSquaresOfDifferences / window.Length);

                means.Add(mean);
                standardDeviations.Add(standardDeviation);
            }
            // Доверительные интервалы для каждого окна:
            double standardError = standardDeviations.Average() / Math.Sqrt(windowSize);
            double confidenceInterval95 = 1.96 * standardError;

            return Tuple.Create(num_of_APs_20.ToArray(), means.ToArray(), Enumerable.Repeat(confidenceInterval95, num_of_APs_20.Count).ToArray());
        }

        public static void ParseTime(string raw_time, out int minutes, out int seconds, out int fraction_seconds)
        {
            string[] time_parts = raw_time.Split(':');

            if (time_parts.Length != 3)
            {
                MessageBox.Show("Invalid time format, expected mm:ss:ff");
            }

            minutes = int.Parse(time_parts[0]);
            seconds = int.Parse(time_parts[1]);
            fraction_seconds = int.Parse(time_parts[2]);
        }

        // метод для поиска файла по номеру ПД, пока заглушка
        public static Tuple<string, double, double> FindFileForNumber(string separating_path, int numberAP)
        {
            string targetFile = System.IO.Path.Combine(separating_path, $"{numberAP}.txt");
            double startTime = 0;
            double endTime = 0;

            if (File.Exists(targetFile))
            {
                using (StreamReader sr = new StreamReader(targetFile))
                {
                    string[] firstLine = sr.ReadLine()?.Split('\t');
                    string[] lastLine = null;
                    string line;

                    while ((line = sr.ReadLine()) != null)
                    {
                        lastLine = line.Split('\t');
                    }

                    if (lastLine != null)
                    {
                        startTime = double.Parse(firstLine[0], CultureInfo.InvariantCulture);
                        endTime = double.Parse(lastLine[0], CultureInfo.InvariantCulture);
                    }
                }
            }

            return Tuple.Create(targetFile, startTime, endTime);
        }

        // меняет формат отображения с мс на нормальный mm:ss:ff
        public static string customTickFormatter(double position)
        {
            TimeSpan times = TimeSpan.FromMilliseconds(position);
            return times.ToString(@"mm\:ss\:fff");
        }

        // достать массивы для построения графика
        public static bool ParseFileData(string filePath, out double[] time, out double[] voltage)
        {
            List<double> timeList = new List<double>();
            List<double> voltageList = new List<double>();

            try
            {
                using (StreamReader reader = new StreamReader(filePath))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        string[] parts = line.Split('\t');
                        if (parts.Length == 2)
                        {
                            double timeValue, voltageValue;
                            if (double.TryParse(parts[0].Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out timeValue) &&
                                double.TryParse(parts[1].Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out voltageValue))
                            {
                                timeList.Add(timeValue);
                                voltageList.Add(voltageValue);
                            }
                        }
                    }
                }

                time = timeList.ToArray();
                voltage = voltageList.ToArray();
                return true;
            }
            catch (Exception)
            {
                time = null;
                voltage = null;
                return false;
            }
        }

        // считает производную для маленького графика
        public static double[] CalculateDerivative(double[] voltage, double[] time)
        {
            if (voltage.Length != time.Length)
            {
                throw new ArgumentException("The length of voltage and time arrays must be equal.");
            }

            int length = voltage.Length;
            double[] deriv = new double[length];

            for (int i = 0; i < length; i++)
            {
                if (i == 0)
                {
                    // Вычисляем первую производную для первого элемента
                    deriv[i] = (voltage[i + 1] - voltage[i]) / (time[i + 1] - time[i]);
                }
                else if (i == length - 1)
                {
                    // Вычисляем первую производную для последнего элемента
                    deriv[i] = (voltage[i] - voltage[i - 1]) / (time[i] - time[i - 1]);
                }
                else
                {
                    // Вычисляем среднее значение первых производных для внутренних элементов
                    double forwardDifference = (voltage[i + 1] - voltage[i]) / (time[i + 1] - time[i]);
                    double backwardDifference = (voltage[i] - voltage[i - 1]) / (time[i] - time[i - 1]);
                    deriv[i] = (forwardDifference + backwardDifference) / 2.0;
                }
            }

            return deriv;
        }

        // закинул конкретный момент времени, получил файлик и его границы
        public static Tuple<string, double, double> FindFileForTime(string separating_path, double time_ms)
        {
            string[] files = Directory.GetFiles(separating_path, "*.txt");
            string targetFile = string.Empty;
            double startTime = 0;
            double endTime = 0;

            foreach (string file in files)
            {
                using (StreamReader sr = new StreamReader(file))
                {
                    string[] firstLine = sr.ReadLine().Split('\t');
                    string[] lastLine = null;
                    string line;

                    while ((line = sr.ReadLine()) != null)
                    {
                        lastLine = line.Split('\t');
                    }

                    if (lastLine != null)
                    {
                        startTime = double.Parse(firstLine[0], CultureInfo.InvariantCulture);
                        endTime = double.Parse(lastLine[0], CultureInfo.InvariantCulture);

                        if (time_ms >= startTime && time_ms <= endTime)
                        {
                            targetFile = file;
                            break;
                        }
                    }
                }
            }

            return Tuple.Create(targetFile, startTime, endTime);
        }

        // достать данные для круга
        public static Tuple<double, double, double, double, double> RunPythonScriptCircle(string pythonPath, string scriptPath, string arguments)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = pythonPath,
                Arguments = $"\"{scriptPath}\" {arguments}", // Заключите путь к скрипту в двойные кавычки
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using (Process process = new Process { StartInfo = startInfo })
            {
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrEmpty(error))
                {
                    throw new Exception($"Python script error: {error}");
                }

                var lines = output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                if (lines.Length < 5)
                {
                    throw new Exception("Python script output is not in the expected format.");
                }

                CultureInfo invariantCulture = CultureInfo.InvariantCulture;

                double radius;
                if (!double.TryParse(lines[0], NumberStyles.Any, CultureInfo.InvariantCulture, out radius))
                {
                    radius = 0;
                }

                double x;
                if (!double.TryParse(lines[1], NumberStyles.Any, CultureInfo.InvariantCulture, out x))
                {
                    x = 0;
                }

                double y;
                if (!double.TryParse(lines[2], NumberStyles.Any, CultureInfo.InvariantCulture, out y))
                {
                    y = 0;
                }

                double dV0;
                if (!double.TryParse(lines[3], NumberStyles.Any, CultureInfo.InvariantCulture, out dV0))
                {
                    dV0 = 0;
                }

                double dv4;
                if (!double.TryParse(lines[4], NumberStyles.Any, CultureInfo.InvariantCulture, out dv4))
                {
                    dv4 = 0;
                }

                return Tuple.Create(radius, x, y, dV0, dv4);
            }
        }

        // найти границы времени по пути к файлику 
        public static Tuple<double, double> GetMinMaxTime(string targetFile)
        {
            List<double> times = new List<double>();
            CultureInfo invariantCulture = CultureInfo.InvariantCulture;

            using (StreamReader sr = new StreamReader(targetFile))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    string[] values = line.Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (values.Length >= 1)
                    {
                        times.Add(double.Parse(values[0], CultureInfo.InvariantCulture));
                    }
                }
            }

            double minTime = double.MaxValue;
            double maxTime = double.MinValue;

            foreach (double time in times)
            {
                minTime = Math.Min(minTime, time);
                maxTime = Math.Max(maxTime, time);
            }

            return Tuple.Create(minTime, maxTime);
        }

        // сохранение в табличку 
        public static string RunPythonScript_Save_xlxs(string pythonPath, string scriptPath, string arguments)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = pythonPath,
                Arguments = $"\"{scriptPath}\" {arguments}", // Заключите путь к скрипту в двойные кавычки
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using (Process process = new Process { StartInfo = startInfo })
            {
                process.Start();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrEmpty(error))
                {
                    throw new Exception($"Python script error: {error}");
                }
                string output = process.StandardOutput.ReadToEnd();

                return output.Trim();
            }
        }

        // получить путь
        public static string SaveFileAndGetPath(string raw_filepath_local)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Excel Files (*.xlsx)|*.xlsx";
            saveFileDialog.FileName = Path.GetFileNameWithoutExtension(raw_filepath_local);
            saveFileDialog.DefaultExt = ".xlsx";

            bool? result = saveFileDialog.ShowDialog();

            if (result == true)
            {
                return saveFileDialog.FileName;
            }

            return null;
        }

        // Вспомогательный метод для поиска дочерних элементов типа T
        public static IEnumerable<T> FindVisualChildren<T>(DependencyObject dependencyObject) where T : DependencyObject
        {
            if (dependencyObject != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(dependencyObject); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(dependencyObject, i);
                    if (child != null && child is T childType)
                    {
                        yield return childType;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }

        // Функция для поиска питоно_ехе_файла
        public static Tuple<bool, string> PromptForPythonExe(string prompt, string txtFilePath, string pythonExePath)
        {
            bool Is_python_file = false;

            MessageBoxResult result = MessageBox.Show(prompt, "Ошибка", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Filter = "Executable files (*.exe)|*.exe";

                if (openFileDialog.ShowDialog() == true)
                {
                    if (Path.GetFileName(openFileDialog.FileName) != "python.exe")
                    {
                        MessageBoxResult anotherResult = MessageBox.Show("Вы уверены, что хотите выбрать этот файл?", "Предупреждение", MessageBoxButton.YesNo);
                        if (anotherResult != MessageBoxResult.Yes)
                        {
                            return Tuple.Create(Is_python_file, pythonExePath);
                        }
                    }
                    pythonExePath = openFileDialog.FileName;
                    File.WriteAllText(txtFilePath, pythonExePath);
                    Is_python_file = true;
                }
            }
            return Tuple.Create(Is_python_file, pythonExePath);
        }
    }
}
