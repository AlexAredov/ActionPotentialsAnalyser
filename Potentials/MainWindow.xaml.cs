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
using System.Threading.Tasks;

namespace Potentials
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // добавление легенды на график 1 ПД и всех ПД
            One_AP_Plot.Plot.Legend();
            All_AP_Plot.Plot.Legend();

            RdV0_Plot.Plot.Legend();
            RdV4_Plot.Plot.Legend();
            dR_Plot.Plot.Legend();

            // добавление названия графика и подписей осей
            One_AP_Plot.Plot.Title("График выбранного ПД");
            One_AP_Plot.Plot.XLabel("Время (t), мс");
            One_AP_Plot.Plot.YLabel("Потенциал (V), мВ");

            All_AP_Plot.Plot.Title("График всех ПД в файле");
            All_AP_Plot.Plot.XLabel("Время (t), мс");
            All_AP_Plot.Plot.YLabel("Потенциал (V), мВ");

            RdV0_Plot.Plot.Title("Изменение (dV/dt)0 по ходу эксперимента", size: 8);
            RdV0_Plot.Plot.XLabel("Номер ПД (N)");
            RdV0_Plot.Plot.YLabel("Средняя скорость (dV/dt)0, В/c");

            RdV4_Plot.Plot.Title("Изменение (dV/dt)4 по ходу эксперимента", size: 8);
            RdV4_Plot.Plot.XLabel("Номер ПД (N)");
            RdV4_Plot.Plot.YLabel("Средняя скорость (dV/dt)4, В/c");

            dR_Plot.Plot.Title("Изменение R кривины по ходу эксперимента", size: 8);
            dR_Plot.Plot.XLabel("Номер ПД (N)");
            dR_Plot.Plot.YLabel("Радиус кривины (R)");

        }
        string raw_filepath = "c:\\";
        string pythonExePath = "C:/Python39/python.exe";
        string separating_path = "";
        double[][] intervals;
        double[] phase_0_speed_array;
        double[] phase_4_speed_array;
        double[] num_of_APs;
        double[] radius_array;

        int time_ms = 0;

        string targetFile = "";
        double start_time = 0;
        double end_time = 0;

        double current_radius = 0;
        double phase_0_speed = 0;
        double phase_4_speed = 0;

        bool check = false;

        // выбрать файлик
        private async void OpenfileBtn_Click(object sender, RoutedEventArgs e)
        {
            // создание диалогового окна выбора файла
            OpenFileDialog openFileDialog = new OpenFileDialog();

            // настройка параметров диалогового окна
            openFileDialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";

            if (!check)
            {
                openFileDialog.InitialDirectory = raw_filepath;
            }
            else
            {
                openFileDialog.InitialDirectory = System.IO.Path.GetDirectoryName(raw_filepath);
            }
            

            //openFileDialog.InitialDirectory = "c:\\";
            //openFileDialog.InitialDirectory = "D:\\programming\\projects\\py\\potentials\\";

            // отображение диалогового окна
            if (openFileDialog.ShowDialog() == true)
            {
                raw_filepath = openFileDialog.FileName;
                //await RunLongOperationAsync();

                // Здесь выполняется самая длительная операция

                string scriptPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PythonScripts", "Save_APs_to_C#.py");
                Tuple<string, double[][], double[], double[], double[], double[]> pythonData = RunPythonScript_AllPD(pythonExePath, scriptPath, raw_filepath);
                separating_path = pythonData.Item1;
                intervals = pythonData.Item2;
                phase_0_speed_array = pythonData.Item3;
                phase_4_speed_array = pythonData.Item4;
                num_of_APs = pythonData.Item5;
                radius_array = pythonData.Item6;

                check = true;
                // сохранение пути к выбранному файлу в переменной raw_filepath и "/" заменяем "\" на "/"
                FileNameTextBox.Text = System.IO.Path.GetFileName(raw_filepath); // извлекаем имя файла;

            }
        }
        // сюда не смотрим, это на случай если будем распараллеливать открытие большого файла
        private async Task RunLongOperationAsync()
        {
            await Task.Run(() =>
            {

            });
        }

        // самый гейский гей 
        private static Tuple<string, double[][], double[], double[], double[], double[]> RunPythonScript_AllPD(string pythonPath, string scriptPath, string arguments)
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


                return Tuple.Create(separating_path, numbers, phase_0_speed_list, phase_4_speed_list, num_of_APs, radius_list);
            }
        }

        // Построить большой график со всеми ПД и графики изменения скоростей и радиуса от номера ПД
        private void PlotAllBtn_Click(object sender, RoutedEventArgs e)
        {
            // Очистка графика перед построением
            All_AP_Plot.Plot.Clear();

            // Предварительно Нацеливаемся на 1 первый файл (хоть 1 то должен быть) 
            targetFile = separating_path + "/1.txt";

            // Обработка всех файлов в указанной директории
            foreach (string filePath in Directory.GetFiles(separating_path))
            {
                if (System.IO.Path.GetExtension(filePath) != ".txt") continue; // Игнорируем файлы, отличные от .txt

                // Считывание данных из файла
                var data = File.ReadLines(filePath)
                    .Select(line => line.Split('\t'))
                    .Where(parts => parts.Length == 2)
                    .Select(parts => new
                    {
                        Time = double.Parse(parts[0], CultureInfo.InvariantCulture),
                        Potential = double.Parse(parts[1], CultureInfo.InvariantCulture)
                    })
                    .ToArray();

                // Построение графика
                All_AP_Plot.Plot.AddScatter(
                    data.Select(point => point.Time).ToArray(),
                    data.Select(point => point.Potential).ToArray()
                );
            }

            // Добавление пунктирных линий, соответствующих значениям из массива intervals
            for (int i = 0; i < intervals.Length; i++)
            {
                for (int j = 0; j < intervals[i].Length; j++)
                {
                    double xValue = intervals[i][j];
                    All_AP_Plot.Plot.AddVerticalLine(xValue, color: System.Drawing.Color.Gray, style: LineStyle.Dash);
                }
            }

            // Обновление графика
            All_AP_Plot.Refresh();

            // находим длительность эксперимента в мс
            double[] lastInterval = intervals[intervals.Length - 1];
            double lastValue = lastInterval[lastInterval.Length - 1];

            // Переводим миллисекунды в минуты, секунды и доли секунды
            int minutes = (int)(lastValue / 60000);
            int seconds = (int)((lastValue % 60000) / 1000);
            int fractionalSeconds = (int)(lastValue % 1000);

            // Форматируем значения в нужном формате
            string formattedDuration = string.Format("{0:D2}:{1:D2}:{2:D3}", minutes, seconds, fractionalSeconds);

            // Выводим сообщение с отформатированным значением
            Experiment_duration_label.Content = "Experiment duration: " + formattedDuration;

            RdV0_Plot.Plot.Clear();
            RdV4_Plot.Plot.Clear();
            dR_Plot.Plot.Clear();
            RdV0_Plot.Plot.AddScatter(num_of_APs, phase_0_speed_array);
            RdV4_Plot.Plot.AddScatter(num_of_APs, phase_4_speed_array);
            dR_Plot.Plot.AddScatter(num_of_APs, radius_array);
            RdV0_Plot.Refresh();
            RdV4_Plot.Refresh();
            dR_Plot.Refresh();
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
        
        // найти по заданному времени участок и построить графики 
        private void FindAPBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string raw_time = TimeTextBox.Text.Trim();
                ParseTime(raw_time, out int minutes, out int seconds, out int fraction_seconds);

                time_ms = (minutes * 60 * 1000) + (seconds * 1000) + fraction_seconds;

                var result = FindFileForTime(separating_path, time_ms);
                targetFile = result.Item1;
                start_time = result.Item2;
                end_time = result.Item3;

                if (!string.IsNullOrEmpty(targetFile))
                {
                    Plot_And_Refresh();
                }
                else
                {
                    MessageBox.Show("File not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }
        // общий метод построения графиков в маленьком окошке и смещения красных прямых
        void Plot_And_Refresh()
        {
            double[] time, voltage;
            if (ParseFileData(targetFile, out time, out voltage))
            {
                // Очистка графика перед построением
                One_AP_Plot.Plot.Clear();

                // Убираем красные хуйни
                RemoveRedVerticalLines();


                // Строим заготовку
                One_AP_Plot.Plot.AddScatter(time, voltage);

                // Добавляем красные хуйни
                All_AP_Plot.Plot.AddVerticalLine(start_time, color: System.Drawing.Color.Red, style: LineStyle.Solid);
                All_AP_Plot.Plot.AddVerticalLine(end_time, color: System.Drawing.Color.Red, style: LineStyle.Solid);


                string scriptPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PythonScripts", "Circle_to_C#.py");
                // Достаем данные для круга и добавляем его на график
                Tuple<double, double, double, double, double> CircleData = RunPythonScriptCircle(pythonExePath, scriptPath, targetFile);
                current_radius = CircleData.Item1;
                double x = CircleData.Item2;
                double y = CircleData.Item3;
                phase_0_speed = CircleData.Item4;
                phase_4_speed = CircleData.Item5;

                One_AP_Plot.Plot.AddCircle(x, y, current_radius, color: System.Drawing.Color.Red);

                // добавление горизорнтальной полоски Овершут
                One_AP_Plot.Plot.AddHorizontalLine(0, color: System.Drawing.Color.Gray, style: LineStyle.Dash);
                //All_AP_Plot.Plot.AddHorizontalLine(0, color: System.Drawing.Color.Gray, style: LineStyle.Dash);

                One_AP_Plot.Refresh();
                All_AP_Plot.Refresh();

                // вывод радиуса и скоростей в лейблы
                RdМ_lbl.Visibility = Visibility.Visible;
                RdV_num_lbl.Visibility = Visibility.Visible;

                RdV_num_lbl.Content = $"= {current_radius}\r\n= {phase_0_speed}\r\n= {phase_4_speed}";
            }
        }

        // закинул конкретный момент времени, получил файлик и его границы
        static Tuple<string, double, double> FindFileForTime(string separating_path, double time_ms)
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

        // достать массивы для построения графика
        bool ParseFileData(string filePath, out double[] time, out double[] voltage)
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
        // убрать все красные VLines
        private void RemoveRedVerticalLines()
        {
            var redVerticalLines = All_AP_Plot.Plot.GetPlottables()
                                    .OfType<VLine>()
                                    .Where(vl => vl.Color == System.Drawing.Color.Red)
                                    .ToList();

            foreach (var redLine in redVerticalLines)
            {
                All_AP_Plot.Plot.Remove(redLine);
            }
        }

        // достать данные для круга
        private static Tuple<double, double, double, double, double> RunPythonScriptCircle(string pythonPath, string scriptPath, string arguments)
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

                if (lines.Length < 3)
                {
                    throw new Exception("Python script output is not in the expected format.");
                }

                CultureInfo invariantCulture = CultureInfo.InvariantCulture;

                double radius = double.Parse(lines[0], invariantCulture);
                double x = double.Parse(lines[1], invariantCulture);
                double y = double.Parse(lines[2], invariantCulture);
                double dV0 = double.Parse(lines[3], invariantCulture);
                double dv4 = double.Parse(lines[4], invariantCulture);

                return Tuple.Create(radius, x, y, dV0, dv4);
            }
        }

        private void Next_AP_Btn_Click(object sender, RoutedEventArgs e)
        {
            int currentNumber = int.Parse(System.IO.Path.GetFileNameWithoutExtension(targetFile));
            int newNumber = currentNumber + 1;
            string newFileName = newNumber.ToString() + ".txt";

            if (File.Exists(System.IO.Path.Combine(separating_path, newFileName)))
            {
                targetFile = System.IO.Path.Combine(separating_path, newFileName);

                var result = GetMinMaxTime(targetFile);
                start_time = result.Item1;
                end_time = result.Item2;

                Plot_And_Refresh();
            }
            else
            {
                MessageBox.Show("Смещение вправо невозможно.");
            }
        }

        private void Previos_AP_Btn_Click(object sender, RoutedEventArgs e)
        {
            int currentNumber = int.Parse(System.IO.Path.GetFileNameWithoutExtension(targetFile));
            int newNumber = currentNumber - 1;
            string newFileName = newNumber.ToString() + ".txt";

            if (File.Exists(System.IO.Path.Combine(separating_path, newFileName)))
            {
                
                targetFile = System.IO.Path.Combine(separating_path, newFileName);

                var result = GetMinMaxTime(targetFile);
                start_time = result.Item1;
                end_time = result.Item2;

                Plot_And_Refresh();
            }
            else
            {
                MessageBox.Show("Смещение влево невозможно.");
            }
        }
        // найти границы времени по пути к файлику 
        Tuple<double, double> GetMinMaxTime(string targetFile)
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

        private void SaveAP_Btn_Click(object sender, RoutedEventArgs e)
        {
            string savedFilePath = SaveFileAndGetPath();
            string ComboPath = savedFilePath + "\n" + raw_filepath;

            if (!string.IsNullOrEmpty(savedFilePath))
            {
                string scriptPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PythonScripts", "Table_to_C#.py");
                string bebra = RunPythonScript_Save_xlxs(pythonExePath, scriptPath, ComboPath);
                if (bebra == "true") { MessageBox.Show($"File saved successfully to\r\n{savedFilePath}"); }
            }
            else
            {
                MessageBox.Show("Операция сохранения была отменена.");
            }


        }
        // сохранение в табличку 
        private static string RunPythonScript_Save_xlxs(string pythonPath, string scriptPath, string arguments)
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
        private string SaveFileAndGetPath()
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Excel Files (*.xlsx)|*.xlsx";
            saveFileDialog.FileName = "NewExcelFile";
            saveFileDialog.DefaultExt = ".xlsx";

            bool? result = saveFileDialog.ShowDialog();

            if (result == true)
            {
                return saveFileDialog.FileName;
            }

            return null;
        }
    }
}
